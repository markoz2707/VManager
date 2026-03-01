using HyperV.Agent.Hubs;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;

namespace HyperV.Agent.Services;

/// <summary>
/// Monitors VM state changes by polling the VM provider every 5 seconds
/// and emitting granular SignalR events for create/delete/state changes.
/// </summary>
public class VmStateMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAgentHubNotifier _notifier;
    private readonly ILogger<VmStateMonitorService> _logger;
    private Dictionary<string, VmSnapshot> _previousState = new();

    public VmStateMonitorService(
        IServiceProvider serviceProvider,
        IAgentHubNotifier notifier,
        ILogger<VmStateMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VM State Monitor Service started");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VM state monitor");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task CheckForChangesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var vmProvider = scope.ServiceProvider.GetRequiredService<IVmProvider>();

        List<VmSummaryDto> currentVms;
        try
        {
            currentVms = await vmProvider.ListVmsAsync();
        }
        catch
        {
            return; // Skip this cycle if provider is unavailable
        }

        var currentState = currentVms.ToDictionary(
            v => v.Id,
            v => new VmSnapshot { Id = v.Id, Name = v.Name, State = v.State });

        // Detect new VMs
        foreach (var (id, vm) in currentState)
        {
            if (!_previousState.ContainsKey(id))
            {
                _logger.LogDebug("VM created: {VmName} ({VmId})", vm.Name, id);
                await _notifier.NotifyVmCreatedAsync(new { vmId = id, vmName = vm.Name, state = vm.State });
            }
        }

        // Detect deleted VMs
        foreach (var (id, vm) in _previousState)
        {
            if (!currentState.ContainsKey(id))
            {
                _logger.LogDebug("VM deleted: {VmName} ({VmId})", vm.Name, id);
                await _notifier.NotifyVmDeletedAsync(id);
            }
        }

        // Detect state changes
        foreach (var (id, current) in currentState)
        {
            if (_previousState.TryGetValue(id, out var previous) && previous.State != current.State)
            {
                _logger.LogDebug("VM state changed: {VmName} {OldState} -> {NewState}",
                    current.Name, previous.State, current.State);
                await _notifier.NotifyVmStateChanged(current.Name, previous.State, current.State);
            }
        }

        _previousState = currentState;
    }

    private class VmSnapshot
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string State { get; set; } = "";
    }
}
