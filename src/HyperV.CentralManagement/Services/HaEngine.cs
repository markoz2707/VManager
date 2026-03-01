using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Hubs;
using HyperV.CentralManagement.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that monitors host health and restarts VMs on failed hosts
/// </summary>
public class HaEngine : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<VManagerHub> _hubContext;
    private readonly ILogger<HaEngine> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);

    public HaEngine(
        IServiceProvider serviceProvider,
        IHubContext<VManagerHub> hubContext,
        ILogger<HaEngine> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HA Engine started");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckHostHealthAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HA engine");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckHostHealthAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentClient = scope.ServiceProvider.GetRequiredService<AgentApiClient>();

        var haConfigs = await context.HaConfigurations
            .Where(h => h.IsEnabled)
            .Include(h => h.VmOverrides)
            .ToListAsync(ct);

        foreach (var haConfig in haConfigs)
        {
            var clusterNodes = await context.ClusterNodes
                .Where(cn => cn.ClusterId == haConfig.ClusterId)
                .Include(cn => cn.AgentHost)
                .ToListAsync(ct);

            foreach (var node in clusterNodes)
            {
                if (node.AgentHost == null) continue;

                var agent = node.AgentHost;
                var timeSinceLastSeen = DateTimeOffset.UtcNow - agent.LastSeenUtc;
                var failureTimeout = TimeSpan.FromSeconds(haConfig.HeartbeatIntervalSeconds * haConfig.FailureThreshold);

                if (agent.Status == AgentStatus.Online && timeSinceLastSeen > failureTimeout)
                {
                    // Host failure detected
                    _logger.LogCritical("HA: Host failure detected for {Host} (last seen {TimeSince} ago)",
                        agent.Hostname, timeSinceLastSeen);

                    agent.Status = AgentStatus.Offline;
                    await context.SaveChangesAsync(ct);

                    // Log HA event
                    var failureEvent = new HaEvent
                    {
                        ClusterId = haConfig.ClusterId,
                        EventType = HaEventType.HostFailureDetected,
                        AgentHostId = agent.Id,
                        AgentHostName = agent.Hostname,
                        Details = $"Host not seen for {timeSinceLastSeen.TotalSeconds:F0}s (threshold: {failureTimeout.TotalSeconds:F0}s)"
                    };
                    context.HaEvents.Add(failureEvent);
                    await context.SaveChangesAsync(ct);

                    // Broadcast via SignalR
                    await _hubContext.Clients.Group($"cluster:{haConfig.ClusterId}").SendAsync("AgentStatusChanged",
                        new AgentStatusChangedEvent
                        {
                            AgentHostId = agent.Id,
                            Hostname = agent.Hostname,
                            PreviousStatus = "Online",
                            NewStatus = "Offline"
                        }, ct);

                    // Restart VMs on another host
                    await RestartVmsFromFailedHostAsync(context, agentClient, haConfig, agent, clusterNodes, ct);
                }
                else if (agent.Status == AgentStatus.Offline)
                {
                    // Try to check if host recovered
                    try
                    {
                        var health = await agentClient.GetHealthAsync(agent.ApiBaseUrl);
                        if (health?.Status == "ok")
                        {
                            agent.Status = AgentStatus.Online;
                            agent.LastSeenUtc = DateTimeOffset.UtcNow;
                            await context.SaveChangesAsync(ct);

                            context.HaEvents.Add(new HaEvent
                            {
                                ClusterId = haConfig.ClusterId,
                                EventType = HaEventType.HostRecovered,
                                AgentHostId = agent.Id,
                                AgentHostName = agent.Hostname
                            });
                            await context.SaveChangesAsync(ct);

                            _logger.LogInformation("HA: Host {Host} recovered", agent.Hostname);

                            await _hubContext.Clients.Group($"cluster:{haConfig.ClusterId}").SendAsync("AgentStatusChanged",
                                new AgentStatusChangedEvent
                                {
                                    AgentHostId = agent.Id,
                                    Hostname = agent.Hostname,
                                    PreviousStatus = "Offline",
                                    NewStatus = "Online"
                                }, ct);
                        }
                    }
                    catch
                    {
                        // Still offline
                    }
                }
            }
        }
    }

    private async Task RestartVmsFromFailedHostAsync(
        AppDbContext context,
        AgentApiClient agentClient,
        HaConfiguration haConfig,
        AgentHost failedHost,
        List<ClusterNode> clusterNodes,
        CancellationToken ct)
    {
        // Get VMs from failed host
        var vmsToRestart = await context.VmInventory
            .Where(v => v.AgentHostId == failedHost.Id && v.State == "Running")
            .ToListAsync(ct);

        if (!vmsToRestart.Any())
        {
            _logger.LogInformation("HA: No running VMs on failed host {Host}", failedHost.Hostname);
            return;
        }

        // Sort by HA priority if overrides exist
        var overrides = haConfig.VmOverrides.ToDictionary(o => o.VmInventoryId);

        var sortedVms = vmsToRestart
            .Select(vm =>
            {
                overrides.TryGetValue(vm.Id, out var over);
                return new
                {
                    Vm = vm,
                    Priority = over?.RestartPriority ?? HaRestartPriority.Medium,
                    Order = over?.RestartOrder ?? 100
                };
            })
            .Where(x => x.Priority != HaRestartPriority.Disabled)
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Order)
            .ToList();

        // Find online hosts in the cluster
        var onlineHosts = clusterNodes
            .Where(cn => cn.AgentHost != null && cn.AgentHostId != failedHost.Id && cn.AgentHost.Status == AgentStatus.Online)
            .Select(cn => cn.AgentHost!)
            .ToList();

        if (!onlineHosts.Any())
        {
            _logger.LogCritical("HA: No online hosts available for VM restart from {Host}", failedHost.Hostname);
            return;
        }

        // Round-robin assignment to available hosts
        var hostIndex = 0;
        foreach (var item in sortedVms)
        {
            var targetHost = onlineHosts[hostIndex % onlineHosts.Count];
            hostIndex++;

            _logger.LogInformation("HA: Attempting to restart VM '{VmName}' on host {Host}",
                item.Vm.Name, targetHost.Hostname);

            try
            {
                var success = await agentClient.StartVmAsync(targetHost.ApiBaseUrl, item.Vm.Name);

                context.HaEvents.Add(new HaEvent
                {
                    ClusterId = haConfig.ClusterId,
                    EventType = success ? HaEventType.VmRestarted : HaEventType.VmRestartFailed,
                    AgentHostId = targetHost.Id,
                    AgentHostName = targetHost.Hostname,
                    VmInventoryId = item.Vm.Id,
                    VmName = item.Vm.Name,
                    Details = success ? $"Restarted on {targetHost.Hostname}" : "Failed to restart"
                });
            }
            catch (Exception ex)
            {
                context.HaEvents.Add(new HaEvent
                {
                    ClusterId = haConfig.ClusterId,
                    EventType = HaEventType.VmRestartFailed,
                    VmInventoryId = item.Vm.Id,
                    VmName = item.Vm.Name,
                    Details = ex.Message
                });

                _logger.LogError(ex, "HA: Failed to restart VM '{VmName}'", item.Vm.Name);
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
