using Microsoft.AspNetCore.SignalR;

namespace HyperV.Agent.Hubs;

/// <summary>
/// Concrete implementation that pushes events through the <see cref="AgentHub"/> to subscribed SignalR groups.
/// </summary>
public class AgentHubNotifier : IAgentHubNotifier
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<AgentHubNotifier> _logger;

    public AgentHubNotifier(IHubContext<AgentHub> hubContext, ILogger<AgentHubNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyVmStateChanged(string vmName, string oldState, string newState)
    {
        _logger.LogDebug("Broadcasting VmStateChanged: {VmName} {OldState} -> {NewState}", vmName, oldState, newState);
        await _hubContext.Clients.Group("vm-events")
            .SendAsync("VmStateChanged", vmName, oldState, newState);
    }

    public async Task NotifyMetricsUpdate(object metrics)
    {
        await _hubContext.Clients.Group("metrics")
            .SendAsync("MetricsUpdate", metrics);
    }

    public async Task NotifyJobProgress(string jobId, int progress, string status)
    {
        _logger.LogDebug("Broadcasting JobProgress: {JobId} {Progress}% {Status}", jobId, progress, status);
        await _hubContext.Clients.Group("jobs")
            .SendAsync("JobProgress", jobId, progress, status);
    }

    public async Task NotifyContainerStateChanged(string containerId, string oldState, string newState)
    {
        _logger.LogDebug("Broadcasting ContainerStateChanged: {ContainerId} {OldState} -> {NewState}", containerId, oldState, newState);
        await _hubContext.Clients.Group("containers")
            .SendAsync("ContainerStateChanged", containerId, oldState, newState);
    }
}
