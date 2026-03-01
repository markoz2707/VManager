using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

public class MaintenanceModeService
{
    private readonly AppDbContext _db;
    private readonly AgentApiClient _agentClient;
    private readonly MigrationOrchestrator _migrationOrchestrator;
    private readonly AuditLogService _audit;
    private readonly ILogger<MaintenanceModeService> _logger;

    public MaintenanceModeService(
        AppDbContext db,
        AgentApiClient agentClient,
        MigrationOrchestrator migrationOrchestrator,
        AuditLogService audit,
        ILogger<MaintenanceModeService> logger)
    {
        _db = db;
        _agentClient = agentClient;
        _migrationOrchestrator = migrationOrchestrator;
        _audit = audit;
        _logger = logger;
    }

    public async Task<MaintenanceModeResult> EnterMaintenanceModeAsync(Guid agentId, string? initiatedBy = null)
    {
        var agent = await _db.AgentHosts.FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent == null)
            throw new InvalidOperationException($"Agent {agentId} not found");

        if (agent.IsInMaintenanceMode)
            return new MaintenanceModeResult { Success = true, Message = "Already in maintenance mode" };

        _logger.LogInformation("Entering maintenance mode for agent {Hostname} ({AgentId})", agent.Hostname, agentId);

        agent.IsInMaintenanceMode = true;
        agent.MaintenanceModeStartedUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(initiatedBy ?? "system", "maintenance_mode_enter", agent.Hostname);

        // Evacuate VMs to other hosts in the same cluster
        var evacuationResult = await EvacuateVmsAsync(agent);

        return new MaintenanceModeResult
        {
            Success = true,
            Message = $"Maintenance mode enabled. {evacuationResult.MigratedCount} VMs evacuated, {evacuationResult.FailedCount} failures.",
            MigratedVmCount = evacuationResult.MigratedCount,
            FailedVmCount = evacuationResult.FailedCount
        };
    }

    public async Task<MaintenanceModeResult> ExitMaintenanceModeAsync(Guid agentId, string? initiatedBy = null)
    {
        var agent = await _db.AgentHosts.FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent == null)
            throw new InvalidOperationException($"Agent {agentId} not found");

        if (!agent.IsInMaintenanceMode)
            return new MaintenanceModeResult { Success = true, Message = "Not in maintenance mode" };

        _logger.LogInformation("Exiting maintenance mode for agent {Hostname} ({AgentId})", agent.Hostname, agentId);

        agent.IsInMaintenanceMode = false;
        agent.MaintenanceModeStartedUtc = null;
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(initiatedBy ?? "system", "maintenance_mode_exit", agent.Hostname);

        return new MaintenanceModeResult { Success = true, Message = "Maintenance mode disabled" };
    }

    private async Task<EvacuationResult> EvacuateVmsAsync(AgentHost agent)
    {
        var result = new EvacuationResult();

        // Find VMs running on this agent
        var vms = await _db.VmInventory
            .Where(v => v.AgentHostId == agent.Id && v.State == "Running")
            .ToListAsync();

        if (!vms.Any())
        {
            _logger.LogInformation("No running VMs to evacuate from {Hostname}", agent.Hostname);
            return result;
        }

        // Find candidate destination hosts in the same cluster
        var destinationHosts = await GetDestinationHostsAsync(agent);

        if (!destinationHosts.Any())
        {
            _logger.LogWarning("No available destination hosts for evacuation from {Hostname}", agent.Hostname);
            result.FailedCount = vms.Count;
            return result;
        }

        // Migrate each VM using round-robin across destination hosts
        var hostIndex = 0;
        foreach (var vm in vms)
        {
            try
            {
                var destHost = destinationHosts[hostIndex % destinationHosts.Count];
                hostIndex++;

                _logger.LogInformation("Evacuating VM '{VmName}' from {Source} to {Dest}",
                    vm.Name, agent.Hostname, destHost.Hostname);

                await _migrationOrchestrator.InitiateMigrationAsync(
                    vm.Id, destHost.Id, live: true, includeStorage: false,
                    initiatedBy: "Maintenance-Mode");

                result.MigratedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evacuate VM '{VmName}' from {Hostname}", vm.Name, agent.Hostname);
                result.FailedCount++;
            }
        }

        return result;
    }

    private async Task<List<AgentHost>> GetDestinationHostsAsync(AgentHost sourceAgent)
    {
        // Find hosts in the same cluster that are online and not in maintenance mode
        if (string.IsNullOrEmpty(sourceAgent.ClusterName))
        {
            // Not in a cluster - find any other online host
            return await _db.AgentHosts
                .Where(a => a.Id != sourceAgent.Id &&
                           a.Status == AgentStatus.Online &&
                           !a.IsInMaintenanceMode)
                .ToListAsync();
        }

        // Use DRS scoring to select the best hosts
        var clusterHosts = await _db.AgentHosts
            .Where(a => a.Id != sourceAgent.Id &&
                       a.ClusterName == sourceAgent.ClusterName &&
                       a.Status == AgentStatus.Online &&
                       !a.IsInMaintenanceMode)
            .ToListAsync();

        // Score hosts by available capacity
        var scoredHosts = new List<(AgentHost Host, double Score)>();
        foreach (var host in clusterHosts)
        {
            try
            {
                var metrics = await _agentClient.GetHostMetricsAsync(host.ApiBaseUrl);
                if (metrics != null)
                {
                    // Lower usage = higher score (better destination)
                    var score = (100 - metrics.Cpu.UsagePercent) * 0.6 +
                               (100 - metrics.Memory.UsagePercent) * 0.4;
                    scoredHosts.Add((host, score));
                }
            }
            catch
            {
                // Skip unreachable hosts
            }
        }

        return scoredHosts.OrderByDescending(s => s.Score).Select(s => s.Host).ToList();
    }

    private class EvacuationResult
    {
        public int MigratedCount { get; set; }
        public int FailedCount { get; set; }
    }
}

public class MaintenanceModeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int MigratedVmCount { get; set; }
    public int FailedVmCount { get; set; }
}
