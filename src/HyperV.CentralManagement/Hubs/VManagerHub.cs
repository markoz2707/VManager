using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HyperV.CentralManagement.Hubs;

[Authorize]
public class VManagerHub : Hub
{
    private readonly ILogger<VManagerHub> _logger;

    public VManagerHub(ILogger<VManagerHub> logger)
    {
        _logger = logger;
    }

    // Group subscription methods - clients join groups to receive targeted events

    public async Task JoinClusterGroup(Guid clusterId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"cluster:{clusterId}");
        _logger.LogDebug("Client {ConnectionId} joined cluster:{ClusterId}", Context.ConnectionId, clusterId);
    }

    public async Task LeaveClusterGroup(Guid clusterId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"cluster:{clusterId}");
    }

    public async Task JoinAgentGroup(Guid agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        _logger.LogDebug("Client {ConnectionId} joined agent:{AgentId}", Context.ConnectionId, agentId);
    }

    public async Task LeaveAgentGroup(Guid agentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"agent:{agentId}");
    }

    public async Task JoinAlertsGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "alerts");
    }

    public async Task LeaveAlertsGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "alerts");
    }

    public async Task JoinMigrationsGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "migrations");
    }

    public async Task LeaveMigrationsGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "migrations");
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}, User: {User}",
            Context.ConnectionId, Context.User?.Identity?.Name);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

// Event payload classes for type-safe SignalR events

public class VmStateChangedEvent
{
    public Guid VmInventoryId { get; set; }
    public string VmName { get; set; } = string.Empty;
    public Guid AgentHostId { get; set; }
    public string PreviousState { get; set; } = string.Empty;
    public string NewState { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class AgentStatusChangedEvent
{
    public Guid AgentHostId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class AlertFiredEvent
{
    public Guid AlertInstanceId { get; set; }
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double ThresholdValue { get; set; }
    public string? AgentHostName { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class AlertResolvedEvent
{
    public Guid AlertInstanceId { get; set; }
    public string AlertName { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class MigrationProgressEvent
{
    public Guid MigrationTaskId { get; set; }
    public string VmName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string SourceAgent { get; set; } = string.Empty;
    public string DestinationAgent { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class MetricsUpdateEvent
{
    public Guid AgentHostId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
