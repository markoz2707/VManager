using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that polls agents for metrics and stores historical data in PostgreSQL
/// </summary>
public class MetricsCollectionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsCollectionService> _logger;
    private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(60);

    public MetricsCollectionService(IServiceProvider serviceProvider, ILogger<MetricsCollectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics Collection Service started");
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics collection service");
            }

            await Task.Delay(_collectionInterval, stoppingToken);
        }
    }

    private async Task CollectMetricsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentClient = scope.ServiceProvider.GetRequiredService<AgentApiClient>();

        var agents = await context.AgentHosts
            .Where(a => a.Status == AgentStatus.Online)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;

        foreach (var agent in agents)
        {
            try
            {
                var hostMetrics = await agentClient.GetHostMetricsAsync(agent.ApiBaseUrl);
                if (hostMetrics == null) continue;

                // Store host metrics
                context.MetricDataPoints.AddRange(new[]
                {
                    new MetricDataPoint
                    {
                        AgentHostId = agent.Id,
                        MetricName = "host_cpu_usage",
                        Value = hostMetrics.Cpu.UsagePercent,
                        TimestampUtc = now
                    },
                    new MetricDataPoint
                    {
                        AgentHostId = agent.Id,
                        MetricName = "host_memory_usage",
                        Value = hostMetrics.Memory.UsagePercent,
                        TimestampUtc = now
                    },
                    new MetricDataPoint
                    {
                        AgentHostId = agent.Id,
                        MetricName = "host_memory_available_mb",
                        Value = hostMetrics.Memory.AvailableMB,
                        TimestampUtc = now
                    },
                    new MetricDataPoint
                    {
                        AgentHostId = agent.Id,
                        MetricName = "host_memory_total_mb",
                        Value = hostMetrics.Memory.TotalPhysicalMB,
                        TimestampUtc = now
                    }
                });

                // Store VM metrics
                var vms = await context.VmInventory
                    .Where(v => v.AgentHostId == agent.Id && v.State == "Running")
                    .ToListAsync(ct);

                foreach (var vm in vms)
                {
                    try
                    {
                        var vmMetrics = await agentClient.GetVmMetricsAsync(agent.ApiBaseUrl, vm.Name);
                        if (vmMetrics == null) continue;

                        context.MetricDataPoints.AddRange(new[]
                        {
                            new MetricDataPoint
                            {
                                AgentHostId = agent.Id,
                                VmInventoryId = vm.Id,
                                MetricName = "vm_cpu_usage",
                                Value = vmMetrics.Cpu.UsagePercent,
                                TimestampUtc = now
                            },
                            new MetricDataPoint
                            {
                                AgentHostId = agent.Id,
                                VmInventoryId = vm.Id,
                                MetricName = "vm_memory_usage",
                                Value = vmMetrics.Memory.UsagePercent,
                                TimestampUtc = now
                            },
                            new MetricDataPoint
                            {
                                AgentHostId = agent.Id,
                                VmInventoryId = vm.Id,
                                MetricName = "vm_memory_assigned_mb",
                                Value = vmMetrics.Memory.AssignedMB,
                                TimestampUtc = now
                            }
                        });
                    }
                    catch
                    {
                        // Individual VM metric failure is not critical
                    }
                }

                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect metrics from agent {Agent}", agent.Hostname);
            }
        }
    }
}
