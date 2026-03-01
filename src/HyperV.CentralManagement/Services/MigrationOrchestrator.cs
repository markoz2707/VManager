using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

public class MigrationOrchestrator
{
    private readonly AppDbContext _context;
    private readonly AgentApiClient _agentClient;
    private readonly ILogger<MigrationOrchestrator> _logger;

    public MigrationOrchestrator(
        AppDbContext context,
        AgentApiClient agentClient,
        ILogger<MigrationOrchestrator> logger)
    {
        _context = context;
        _agentClient = agentClient;
        _logger = logger;
    }

    /// <summary>
    /// Pre-check migration feasibility
    /// </summary>
    public async Task<MigrationPreCheckResult> PreCheckAsync(Guid vmInventoryId, Guid destinationAgentId)
    {
        var result = new MigrationPreCheckResult();

        var vm = await _context.VmInventory
            .Include(v => v.AgentHost)
            .FirstOrDefaultAsync(v => v.Id == vmInventoryId);

        if (vm?.AgentHost == null)
        {
            result.Errors.Add("VM or source agent not found.");
            return result;
        }

        var destAgent = await _context.AgentHosts.FindAsync(destinationAgentId);
        if (destAgent == null)
        {
            result.Errors.Add("Destination agent not found.");
            return result;
        }

        if (vm.AgentHostId == destinationAgentId)
        {
            result.Errors.Add("Source and destination are the same host.");
            return result;
        }

        // Check destination agent is online
        if (destAgent.Status != AgentStatus.Online)
        {
            result.Errors.Add($"Destination host '{destAgent.Hostname}' is not online.");
            return result;
        }

        // Check source agent is online
        if (vm.AgentHost.Status != AgentStatus.Online)
        {
            result.Errors.Add($"Source host '{vm.AgentHost.Hostname}' is not online.");
            return result;
        }

        // Check destination capacity
        try
        {
            var destMetrics = await _agentClient.GetHostMetricsAsync(destAgent.ApiBaseUrl);
            if (destMetrics != null)
            {
                if (destMetrics.Memory.AvailableMB < vm.MemoryMB)
                {
                    result.Warnings.Add($"Destination has {destMetrics.Memory.AvailableMB}MB available, VM requires {vm.MemoryMB}MB.");
                }

                if (destMetrics.Cpu.UsagePercent > 90)
                {
                    result.Warnings.Add($"Destination CPU usage is {destMetrics.Cpu.UsagePercent:F1}%.");
                }
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"Could not verify destination capacity: {ex.Message}");
        }

        // Check network compatibility (both hosts must be in same cluster ideally)
        var sourceInCluster = await _context.ClusterNodes.AnyAsync(cn => cn.AgentHostId == vm.AgentHostId);
        var destInCluster = await _context.ClusterNodes.AnyAsync(cn => cn.AgentHostId == destinationAgentId);

        if (sourceInCluster != destInCluster)
        {
            result.Warnings.Add("Source and destination hosts are not in the same cluster configuration.");
        }

        result.IsValid = !result.Errors.Any();
        result.SourceAgent = vm.AgentHost.Hostname;
        result.DestinationAgent = destAgent.Hostname;
        result.VmName = vm.Name;

        return result;
    }

    /// <summary>
    /// Initiate a migration task
    /// </summary>
    public async Task<MigrationTask> InitiateMigrationAsync(
        Guid vmInventoryId,
        Guid destinationAgentId,
        bool live,
        bool includeStorage,
        string? initiatedBy)
    {
        var vm = await _context.VmInventory
            .Include(v => v.AgentHost)
            .FirstOrDefaultAsync(v => v.Id == vmInventoryId);

        var destAgent = await _context.AgentHosts.FindAsync(destinationAgentId);

        if (vm?.AgentHost == null || destAgent == null)
            throw new InvalidOperationException("VM or agent not found.");

        var task = new MigrationTask
        {
            VmInventoryId = vmInventoryId,
            VmName = vm.Name,
            SourceAgentId = vm.AgentHostId,
            SourceAgentName = vm.AgentHost.Hostname,
            DestinationAgentId = destinationAgentId,
            DestinationAgentName = destAgent.Hostname,
            LiveMigration = live,
            IncludeStorage = includeStorage,
            Status = MigrationStatus.PreChecking,
            InitiatedBy = initiatedBy,
            StartedUtc = DateTimeOffset.UtcNow
        };

        _context.MigrationTasks.Add(task);
        await _context.SaveChangesAsync();

        // Run pre-check
        var preCheck = await PreCheckAsync(vmInventoryId, destinationAgentId);
        task.PreCheckResults = System.Text.Json.JsonSerializer.Serialize(preCheck);

        if (!preCheck.IsValid)
        {
            task.Status = MigrationStatus.Failed;
            task.ErrorMessage = string.Join("; ", preCheck.Errors);
            task.CompletedUtc = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync();
            return task;
        }

        // Initiate migration via agent
        task.Status = MigrationStatus.Migrating;
        task.ProgressPercent = 10;
        await _context.SaveChangesAsync();

        try
        {
            var migrationRequest = new MigrationRequest
            {
                DestinationHost = destAgent.Hostname,
                Live = live,
                IncludeStorage = includeStorage
            };

            var response = await _agentClient.MigrateVmAsync(vm.AgentHost.ApiBaseUrl, vm.Name, migrationRequest);

            if (response?.Success == true)
            {
                task.Status = MigrationStatus.Completed;
                task.ProgressPercent = 100;
                task.CompletedUtc = DateTimeOffset.UtcNow;

                _logger.LogInformation("Migration of VM '{VmName}' from {Source} to {Dest} completed",
                    vm.Name, vm.AgentHost.Hostname, destAgent.Hostname);
            }
            else
            {
                task.Status = MigrationStatus.Failed;
                task.ErrorMessage = response?.Message ?? "Migration failed with no message.";
                task.CompletedUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (Exception ex)
        {
            task.Status = MigrationStatus.Failed;
            task.ErrorMessage = ex.Message;
            task.CompletedUtc = DateTimeOffset.UtcNow;
            _logger.LogError(ex, "Migration failed for VM '{VmName}'", vm.Name);
        }

        await _context.SaveChangesAsync();
        return task;
    }

    /// <summary>
    /// Get migration status
    /// </summary>
    public async Task<MigrationTask?> GetMigrationStatusAsync(Guid migrationTaskId)
    {
        return await _context.MigrationTasks.FindAsync(migrationTaskId);
    }

    /// <summary>
    /// Get migration history for a VM
    /// </summary>
    public async Task<List<MigrationTask>> GetMigrationHistoryAsync(Guid vmInventoryId)
    {
        return await _context.MigrationTasks
            .Where(m => m.VmInventoryId == vmInventoryId)
            .OrderByDescending(m => m.CreatedUtc)
            .ToListAsync();
    }

    /// <summary>
    /// Cancel a pending/running migration
    /// </summary>
    public async Task<bool> CancelMigrationAsync(Guid migrationTaskId)
    {
        var task = await _context.MigrationTasks.FindAsync(migrationTaskId);
        if (task == null) return false;

        if (task.Status is MigrationStatus.Completed or MigrationStatus.Failed or MigrationStatus.Cancelled)
            return false;

        task.Status = MigrationStatus.Cancelled;
        task.CompletedUtc = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}

public class MigrationPreCheckResult
{
    public bool IsValid { get; set; }
    public string VmName { get; set; } = string.Empty;
    public string SourceAgent { get; set; } = string.Empty;
    public string DestinationAgent { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
