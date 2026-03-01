using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using HyperV.CentralManagement.Hubs;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Manages a SignalR HubConnection to /hubs/vmanager for Blazor components.
/// Scoped service — each circuit gets its own connection.
/// </summary>
public class SignalRClientService : IAsyncDisposable
{
    private HubConnection? _hub;
    private readonly NavigationManager _nav;
    private readonly AuthSession _session;
    private readonly ILogger<SignalRClientService> _logger;

    public event Action<VmStateChangedEvent>? OnVmStateChanged;
    public event Action<AgentStatusChangedEvent>? OnAgentStatusChanged;
    public event Action<AlertFiredEvent>? OnAlertFired;
    public event Action<AlertResolvedEvent>? OnAlertResolved;
    public event Action<MigrationProgressEvent>? OnMigrationProgress;
    public event Action<MetricsUpdateEvent>? OnMetricsUpdate;

    public bool IsConnected => _hub?.State == HubConnectionState.Connected;

    public SignalRClientService(NavigationManager nav, AuthSession session, ILogger<SignalRClientService> logger)
    {
        _nav = nav;
        _session = session;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_hub is not null) return;
        if (string.IsNullOrWhiteSpace(_session.Token)) return;

        var hubUrl = _nav.ToAbsoluteUri("/hubs/vmanager");

        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl, opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(_session.Token);
            })
            .WithAutomaticReconnect()
            .Build();

        _hub.On<VmStateChangedEvent>("VmStateChanged", e => OnVmStateChanged?.Invoke(e));
        _hub.On<AgentStatusChangedEvent>("AgentStatusChanged", e => OnAgentStatusChanged?.Invoke(e));
        _hub.On<AlertFiredEvent>("AlertFired", e => OnAlertFired?.Invoke(e));
        _hub.On<AlertResolvedEvent>("AlertResolved", e => OnAlertResolved?.Invoke(e));
        _hub.On<MigrationProgressEvent>("MigrationProgress", e => OnMigrationProgress?.Invoke(e));
        _hub.On<MetricsUpdateEvent>("MetricsUpdate", e => OnMetricsUpdate?.Invoke(e));

        _hub.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        try
        {
            await _hub.StartAsync();
            _logger.LogInformation("SignalR connected to /hubs/vmanager");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect SignalR hub");
        }
    }

    public async Task JoinAlertsGroupAsync()
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("JoinAlertsGroup");
    }

    public async Task JoinMigrationsGroupAsync()
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("JoinMigrationsGroup");
    }

    public async Task JoinAgentGroupAsync(Guid agentId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("JoinAgentGroup", agentId);
    }

    public async Task JoinClusterGroupAsync(Guid clusterId)
    {
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("JoinClusterGroup", clusterId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null)
        {
            await _hub.DisposeAsync();
            _hub = null;
        }
    }
}
