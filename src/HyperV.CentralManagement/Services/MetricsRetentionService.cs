using HyperV.CentralManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

/// <summary>
/// Background service that cleans up old metric data points (default: 30 days retention)
/// </summary>
public class MetricsRetentionService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricsRetentionService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);
    private readonly int _retentionDays = 30;

    public MetricsRetentionService(IServiceProvider serviceProvider, ILogger<MetricsRetentionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics Retention Service started (retention: {Days} days)", _retentionDays);
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metrics retention service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CleanupOldMetricsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_retentionDays);

        var deleted = await context.MetricDataPoints
            .Where(m => m.TimestampUtc < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} metric data points older than {Cutoff}", deleted, cutoff);
        }
    }
}
