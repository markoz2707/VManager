using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that periodically syncs VM inventory from all registered agents
/// </summary>
public class VmSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<VManagerHub> _hubContext;
    private readonly ILogger<VmSyncBackgroundService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromSeconds(30);

    public VmSyncBackgroundService(
        IServiceProvider serviceProvider,
        IHubContext<VManagerHub> hubContext,
        ILogger<VmSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VM Sync Background Service started");

        // Initial delay to let the application start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllAgentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VM sync background service");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("VM Sync Background Service stopped");
    }

    private async Task SyncAllAgentsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentClient = scope.ServiceProvider.GetRequiredService<AgentApiClient>();

        var agents = await context.AgentHosts
            .Where(a => a.Status == Models.AgentStatus.Online)
            .ToListAsync(stoppingToken);

        if (!agents.Any())
        {
            return;
        }

        _logger.LogDebug("Starting VM sync for {Count} online agents", agents.Count);

        foreach (var agent in agents)
        {
            try
            {
                await SyncAgentAsync(context, agentClient, agent, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync VMs from agent {AgentName}", agent.Hostname);
            }
        }
    }

    private async Task SyncAgentAsync(
        AppDbContext context,
        AgentApiClient agentClient,
        Models.AgentHost agent,
        CancellationToken stoppingToken)
    {
        var vmList = await agentClient.GetVmsAsync(agent.ApiBaseUrl);
        if (vmList == null) return;

        var now = DateTimeOffset.UtcNow;

        // Get existing VMs for comparison
        var existingVms = await context.VmInventory
            .Where(v => v.AgentHostId == agent.Id)
            .ToDictionaryAsync(v => v.VmId, stoppingToken);

        var processedIds = new HashSet<string>();

        async Task ProcessVm(VmInfo vm, string environment)
        {
            processedIds.Add(vm.Id);

            if (existingVms.TryGetValue(vm.Id, out var existing))
            {
                var previousState = existing.State;
                existing.Name = vm.Name;
                existing.State = vm.State;
                existing.CpuCount = vm.CpuCount;
                existing.MemoryMB = vm.MemoryMB;
                existing.Environment = environment;
                existing.LastSyncUtc = now;

                // Push SignalR event if state changed
                if (previousState != vm.State)
                {
                    await _hubContext.Clients.Group($"agent:{agent.Id}").SendAsync("VmStateChanged",
                        new VmStateChangedEvent
                        {
                            VmInventoryId = existing.Id,
                            VmName = vm.Name,
                            AgentHostId = agent.Id,
                            PreviousState = previousState,
                            NewState = vm.State,
                        }, stoppingToken);
                }
            }
            else
            {
                context.VmInventory.Add(new Models.VmInventory
                {
                    Id = Guid.NewGuid(),
                    AgentHostId = agent.Id,
                    VmId = vm.Id,
                    Name = vm.Name,
                    State = vm.State,
                    CpuCount = vm.CpuCount,
                    MemoryMB = vm.MemoryMB,
                    Environment = environment,
                    LastSyncUtc = now
                });
            }
        }

        foreach (var vm in vmList.Hcs)
            await ProcessVm(vm, "HCS");

        foreach (var vm in vmList.Wmi)
            await ProcessVm(vm, "WMI");

        // Remove stale VMs
        var staleVms = existingVms
            .Where(kvp => !processedIds.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        if (staleVms.Any())
        {
            context.VmInventory.RemoveRange(staleVms);
        }

        await context.SaveChangesAsync(stoppingToken);

        // Push metrics update via SignalR
        try
        {
            var hostMetrics = await agentClient.GetHostMetricsAsync(agent.ApiBaseUrl);
            if (hostMetrics != null)
            {
                await _hubContext.Clients.Group($"agent:{agent.Id}").SendAsync("MetricsUpdate",
                    new MetricsUpdateEvent
                    {
                        AgentHostId = agent.Id,
                        Hostname = agent.Hostname,
                        CpuUsagePercent = hostMetrics.Cpu.UsagePercent,
                        MemoryUsagePercent = hostMetrics.Memory.UsagePercent
                    }, stoppingToken);
            }
        }
        catch
        {
            // Non-critical - metrics push failure shouldn't block sync
        }
    }
}
