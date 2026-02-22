using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that evaluates cluster balance and creates migration recommendations
/// </summary>
public class DrsEngine : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DrsEngine> _logger;

    public DrsEngine(IServiceProvider serviceProvider, ILogger<DrsEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DRS Engine started");
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateClustersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DRS engine");
            }

            // Wait for the shortest configured interval
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task EvaluateClustersAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentClient = scope.ServiceProvider.GetRequiredService<AgentApiClient>();

        var drsConfigs = await context.DrsConfigurations
            .Where(d => d.IsEnabled)
            .ToListAsync(ct);

        foreach (var config in drsConfigs)
        {
            try
            {
                await EvaluateClusterBalanceAsync(context, agentClient, config, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DRS: Failed to evaluate cluster {ClusterId}", config.ClusterId);
            }
        }
    }

    private async Task EvaluateClusterBalanceAsync(
        AppDbContext context,
        AgentApiClient agentClient,
        DrsConfiguration config,
        CancellationToken ct)
    {
        var nodes = await context.ClusterNodes
            .Where(cn => cn.ClusterId == config.ClusterId)
            .Include(cn => cn.AgentHost)
            .Where(cn => cn.AgentHost != null && cn.AgentHost.Status == AgentStatus.Online)
            .ToListAsync(ct);

        if (nodes.Count < 2) return;

        // Collect current usage
        var hostUsage = new Dictionary<Guid, HostUsageInfo>();

        foreach (var node in nodes)
        {
            if (node.AgentHost == null) continue;

            try
            {
                var metrics = await agentClient.GetHostMetricsAsync(node.AgentHost.ApiBaseUrl);
                if (metrics == null) continue;

                var vmCount = await context.VmInventory
                    .CountAsync(v => v.AgentHostId == node.AgentHostId, ct);

                hostUsage[node.AgentHostId] = new HostUsageInfo
                {
                    AgentHostId = node.AgentHostId,
                    Hostname = node.AgentHost.Hostname,
                    CpuUsagePercent = metrics.Cpu.UsagePercent,
                    MemoryUsagePercent = metrics.Memory.UsagePercent,
                    MemoryAvailableMb = metrics.Memory.AvailableMB,
                    VmCount = vmCount
                };
            }
            catch
            {
                // Skip hosts we can't reach
            }
        }

        if (hostUsage.Count < 2) return;

        // Calculate cluster averages
        var avgCpu = hostUsage.Values.Average(h => h.CpuUsagePercent);
        var avgMemory = hostUsage.Values.Average(h => h.MemoryUsagePercent);
        var maxCpu = hostUsage.Values.Max(h => h.CpuUsagePercent);
        var minCpu = hostUsage.Values.Min(h => h.CpuUsagePercent);
        var cpuImbalance = maxCpu - minCpu;

        // Check memory imbalance as well
        var maxMemory = hostUsage.Values.Max(h => h.MemoryUsagePercent);
        var minMemory = hostUsage.Values.Min(h => h.MemoryUsagePercent);
        var memoryImbalance = maxMemory - minMemory;

        var hasCpuImbalance = cpuImbalance >= config.CpuImbalanceThreshold;
        var hasMemoryImbalance = memoryImbalance >= config.MemoryImbalanceThreshold;

        if (!hasCpuImbalance && !hasMemoryImbalance)
        {
            _logger.LogDebug("DRS: Cluster {ClusterId} is balanced (CPU imbalance: {CpuImbalance:F1}%, Memory imbalance: {MemImbalance:F1}%)",
                config.ClusterId, cpuImbalance, memoryImbalance);
            return;
        }

        var imbalanceType = hasCpuImbalance && hasMemoryImbalance ? "CPU+Memory"
            : hasCpuImbalance ? "CPU" : "Memory";

        _logger.LogInformation("DRS: Cluster {ClusterId} imbalance detected ({Type} - CPU: {CpuImbalance:F1}%, Memory: {MemImbalance:F1}%)",
            config.ClusterId, imbalanceType, cpuImbalance, memoryImbalance);

        // Find overloaded host based on combined score (weighted: 60% CPU, 40% Memory)
        var overloaded = hostUsage.Values
            .OrderByDescending(h => h.CpuUsagePercent * 0.6 + h.MemoryUsagePercent * 0.4)
            .First();

        // Find underloaded host
        var underloaded = hostUsage.Values
            .OrderBy(h => h.CpuUsagePercent * 0.6 + h.MemoryUsagePercent * 0.4)
            .First();

        // Find candidate VMs to migrate from overloaded host
        var vmsOnOverloaded = await context.VmInventory
            .Where(v => v.AgentHostId == overloaded.AgentHostId && v.State == "Running")
            .OrderBy(v => v.CpuCount)
            .ToListAsync(ct);

        if (!vmsOnOverloaded.Any()) return;

        // Pick a candidate VM (smallest first to minimize disruption)
        var candidate = vmsOnOverloaded.First();

        var estimatedBenefit = (cpuImbalance * 0.6 + memoryImbalance * 0.4) * 0.3;

        if (estimatedBenefit < config.MinBenefitPercent) return;

        // Check for existing pending recommendation for same VM
        var existingRec = await context.DrsRecommendations
            .AnyAsync(r => r.VmInventoryId == candidate.Id &&
                          r.Status == DrsRecommendationStatus.Pending, ct);

        if (existingRec) return;

        var reason = $"{imbalanceType} imbalance - CPU: {cpuImbalance:F1}% (src: {overloaded.CpuUsagePercent:F1}%, dst: {underloaded.CpuUsagePercent:F1}%), " +
                     $"Memory: {memoryImbalance:F1}% (src: {overloaded.MemoryUsagePercent:F1}%, dst: {underloaded.MemoryUsagePercent:F1}%)";

        var recommendation = new DrsRecommendation
        {
            DrsConfigurationId = config.Id,
            VmInventoryId = candidate.Id,
            VmName = candidate.Name,
            SourceAgentId = overloaded.AgentHostId,
            SourceAgentName = overloaded.Hostname,
            DestinationAgentId = underloaded.AgentHostId,
            DestinationAgentName = underloaded.Hostname,
            Reason = reason,
            EstimatedBenefitPercent = estimatedBenefit
        };

        context.DrsRecommendations.Add(recommendation);
        await context.SaveChangesAsync(ct);

        _logger.LogInformation("DRS: Created recommendation to migrate VM '{VmName}' from {Source} to {Dest} (reason: {Reason})",
            candidate.Name, overloaded.Hostname, underloaded.Hostname, reason);

        // Write audit log for DRS decision
        context.AuditLogs.Add(new AuditLog
        {
            Action = "DRS_RECOMMENDATION",
            Username = "DRS-Engine",
            Details = $"Cluster {config.ClusterId}: Recommend migrating VM '{candidate.Name}' from {overloaded.Hostname} to {underloaded.Hostname}. {reason}. Estimated benefit: {estimatedBenefit:F1}%"
        });
        await context.SaveChangesAsync(ct);

        // Auto-apply if Full automation
        if (config.AutomationLevel == DrsAutomationLevel.Full)
        {
            var migrationOrchestrator = _serviceProvider.CreateScope().ServiceProvider
                .GetRequiredService<MigrationOrchestrator>();

            try
            {
                await migrationOrchestrator.InitiateMigrationAsync(
                    candidate.Id, underloaded.AgentHostId, true, false, "DRS-Auto");

                recommendation.Status = DrsRecommendationStatus.Applied;
                recommendation.AppliedUtc = DateTimeOffset.UtcNow;
                recommendation.AppliedBy = "DRS-Auto";

                context.AuditLogs.Add(new AuditLog
                {
                    Action = "DRS_AUTO_MIGRATION",
                    Username = "DRS-Engine",
                    Details = $"Auto-applied: Migrated VM '{candidate.Name}' from {overloaded.Hostname} to {underloaded.Hostname}"
                });

                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                recommendation.Status = DrsRecommendationStatus.Failed;

                context.AuditLogs.Add(new AuditLog
                {
                    Action = "DRS_AUTO_MIGRATION_FAILED",
                    Username = "DRS-Engine",
                    Details = $"Failed: Migrating VM '{candidate.Name}' from {overloaded.Hostname} to {underloaded.Hostname}. Error: {ex.Message}"
                });

                await context.SaveChangesAsync(ct);
                _logger.LogError(ex, "DRS: Auto-migration failed for VM '{VmName}'", candidate.Name);
            }
        }
    }

    private class HostUsageInfo
    {
        public Guid AgentHostId { get; set; }
        public string Hostname { get; set; } = "";
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long MemoryAvailableMb { get; set; }
        public int VmCount { get; set; }
    }
}
