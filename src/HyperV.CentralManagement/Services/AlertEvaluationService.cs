using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that periodically evaluates alert definitions against agent metrics
/// </summary>
public class AlertEvaluationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertEvaluationService> _logger;
    private readonly TimeSpan _evaluationInterval = TimeSpan.FromSeconds(30);

    public AlertEvaluationService(IServiceProvider serviceProvider, ILogger<AlertEvaluationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Alert Evaluation Service started");
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateAlertsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in alert evaluation service");
            }

            await Task.Delay(_evaluationInterval, stoppingToken);
        }
    }

    private async Task EvaluateAlertsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var agentClient = scope.ServiceProvider.GetRequiredService<AgentApiClient>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var definitions = await context.AlertDefinitions
            .Include(ad => ad.NotificationChannels)
            .ThenInclude(anc => anc.NotificationChannel)
            .Where(ad => ad.IsEnabled)
            .ToListAsync(ct);

        if (!definitions.Any()) return;

        var agents = await context.AgentHosts
            .Where(a => a.Status == AgentStatus.Online)
            .ToListAsync(ct);

        foreach (var agent in agents)
        {
            try
            {
                var hostMetrics = await agentClient.GetHostMetricsAsync(agent.ApiBaseUrl);
                if (hostMetrics == null) continue;

                var metricValues = new Dictionary<string, double>
                {
                    ["host_cpu_usage"] = hostMetrics.Cpu.UsagePercent,
                    ["host_memory_usage"] = hostMetrics.Memory.UsagePercent,
                    ["host_memory_available_mb"] = hostMetrics.Memory.AvailableMB
                };

                foreach (var definition in definitions)
                {
                    // Check scope
                    if (definition.AgentHostId.HasValue && definition.AgentHostId != agent.Id)
                        continue;

                    if (!metricValues.TryGetValue(definition.MetricName, out var currentValue))
                        continue;

                    var conditionMet = definition.Condition switch
                    {
                        AlertCondition.GreaterThan => currentValue > definition.ThresholdValue,
                        AlertCondition.LessThan => currentValue < definition.ThresholdValue,
                        AlertCondition.Equals => Math.Abs(currentValue - definition.ThresholdValue) < 0.01,
                        _ => false
                    };

                    if (conditionMet)
                    {
                        // Check cooldown
                        var lastInstance = await context.AlertInstances
                            .Where(ai => ai.AlertDefinitionId == definition.Id &&
                                         ai.AgentHostId == agent.Id)
                            .OrderByDescending(ai => ai.FiredUtc)
                            .FirstOrDefaultAsync(ct);

                        if (lastInstance != null &&
                            lastInstance.Status != AlertInstanceStatus.Resolved &&
                            (DateTimeOffset.UtcNow - lastInstance.FiredUtc).TotalSeconds < definition.CooldownSeconds)
                        {
                            continue;
                        }

                        // Create alert instance
                        var instance = new AlertInstance
                        {
                            AlertDefinitionId = definition.Id,
                            AgentHostId = agent.Id,
                            AgentHostName = agent.Hostname,
                            CurrentValue = currentValue,
                            Status = AlertInstanceStatus.Active
                        };

                        context.AlertInstances.Add(instance);
                        await context.SaveChangesAsync(ct);

                        _logger.LogWarning("Alert fired: {AlertName} on {Host} - {Metric}: {Value} {Condition} {Threshold}",
                            definition.Name, agent.Hostname, definition.MetricName, currentValue,
                            definition.Condition, definition.ThresholdValue);

                        // Send notifications
                        foreach (var anc in definition.NotificationChannels)
                        {
                            if (anc.NotificationChannel?.IsEnabled == true)
                            {
                                var subject = $"[VManager Alert] {definition.Severity}: {definition.Name}";
                                var message = $"Alert: {definition.Name}\nHost: {agent.Hostname}\nMetric: {definition.MetricName}\nValue: {currentValue:F2}\nThreshold: {definition.ThresholdValue:F2}\nSeverity: {definition.Severity}";

                                await notificationService.SendAsync(anc.NotificationChannel, subject, message);
                            }
                        }
                    }
                    else
                    {
                        // Auto-resolve active alerts for this definition/agent if condition no longer met
                        var activeAlerts = await context.AlertInstances
                            .Where(ai => ai.AlertDefinitionId == definition.Id &&
                                         ai.AgentHostId == agent.Id &&
                                         ai.Status == AlertInstanceStatus.Active)
                            .ToListAsync(ct);

                        foreach (var alert in activeAlerts)
                        {
                            alert.Status = AlertInstanceStatus.Resolved;
                            alert.ResolvedUtc = DateTimeOffset.UtcNow;
                        }

                        if (activeAlerts.Any())
                        {
                            await context.SaveChangesAsync(ct);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate alerts for agent {Agent}", agent.Hostname);
            }
        }
    }
}
