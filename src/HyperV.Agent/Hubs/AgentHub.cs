using Microsoft.AspNetCore.SignalR;

namespace HyperV.Agent.Hubs;

/// <summary>
/// SignalR hub for real-time agent events (VM state changes, metrics, job progress, containers).
/// Clients subscribe to groups: "vm-events", "metrics", "jobs", "containers".
/// </summary>
public class AgentHub : Hub
{
    private readonly ILogger<AgentHub> _logger;

    public AgentHub(ILogger<AgentHub> logger)
    {
        _logger = logger;
    }

    // --- Group subscriptions ---

    public async Task SubscribeToVmEvents()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "vm-events");

    public async Task SubscribeToMetrics()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "metrics");

    public async Task SubscribeToJobs()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "jobs");

    public async Task SubscribeToContainers()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "containers");

    public async Task UnsubscribeFromVmEvents()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "vm-events");

    public async Task UnsubscribeFromMetrics()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "metrics");

    public async Task UnsubscribeFromJobs()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "jobs");

    public async Task UnsubscribeFromContainers()
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, "containers");

    // --- Lifecycle ---

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
