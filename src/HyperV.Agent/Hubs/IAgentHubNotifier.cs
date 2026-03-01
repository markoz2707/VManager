namespace HyperV.Agent.Hubs;

/// <summary>
/// Abstraction for pushing real-time events to SignalR clients.
/// Inject this into any service that needs to broadcast state changes.
/// </summary>
public interface IAgentHubNotifier
{
    /// <summary>Notify subscribed clients that a VM changed state.</summary>
    Task NotifyVmStateChanged(string vmName, string oldState, string newState);

    /// <summary>Broadcast a metrics snapshot to the "metrics" group.</summary>
    Task NotifyMetricsUpdate(object metrics);

    /// <summary>Report progress on a background job.</summary>
    Task NotifyJobProgress(string jobId, int progress, string status);

    /// <summary>Notify subscribed clients that a container changed state.</summary>
    Task NotifyContainerStateChanged(string containerId, string oldState, string newState);

    /// <summary>Notify that a new VM was created.</summary>
    Task NotifyVmCreatedAsync(object vmSummary);

    /// <summary>Notify that a VM was deleted.</summary>
    Task NotifyVmDeletedAsync(string vmId);

    /// <summary>Broadcast a single metric value to the "metrics" group.</summary>
    Task NotifyMetricSnapshotAsync(string metricName, double value);
}
