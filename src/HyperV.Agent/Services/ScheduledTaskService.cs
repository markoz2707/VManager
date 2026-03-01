using Cronos;
using HyperV.Contracts.Interfaces.Providers;

namespace HyperV.Agent.Services;

public class ScheduledTaskService : BackgroundService
{
    private readonly ScheduleStore _store;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTaskService> _logger;

    public ScheduledTaskService(
        ScheduleStore store,
        IServiceProvider serviceProvider,
        ILogger<ScheduledTaskService> logger)
    {
        _store = store;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled task service started");

        // Compute initial NextRunUtc for all tasks
        foreach (var task in _store.GetAll())
        {
            if (task.IsEnabled)
                ComputeNextRun(task.Id, task.CronExpression);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await CheckAndExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in scheduled task loop");
            }
        }

        _logger.LogInformation("Scheduled task service stopped");
    }

    private async Task CheckAndExecuteAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tasks = _store.GetAll();

        foreach (var task in tasks)
        {
            if (!task.IsEnabled || ct.IsCancellationRequested) continue;

            try
            {
                var cron = CronExpression.Parse(task.CronExpression);
                var nextRun = task.NextRunUtc ?? cron.GetNextOccurrence(now.AddSeconds(-31), TimeZoneInfo.Utc);

                if (nextRun.HasValue && nextRun.Value <= now)
                {
                    _logger.LogInformation("Executing scheduled task {TaskName} ({Action}) for VMs: {VMs}",
                        task.Name, task.Action, string.Join(", ", task.TargetVms));

                    var result = await ExecuteTaskAsync(task.Action, task.TargetVms, ct);
                    _store.UpdateLastRun(task.Id, now, result);
                    ComputeNextRun(task.Id, task.CronExpression);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scheduled task {TaskId}", task.Id);
                _store.UpdateLastRun(task.Id, now, $"Error: {ex.Message}");
                ComputeNextRun(task.Id, task.CronExpression);
            }
        }
    }

    private void ComputeNextRun(string taskId, string cronExpression)
    {
        try
        {
            var cron = CronExpression.Parse(cronExpression);
            var next = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
            _store.UpdateNextRun(taskId, next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute next run for task {TaskId}", taskId);
        }
    }

    private async Task<string> ExecuteTaskAsync(string action, string[] targetVms, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var vmProvider = scope.ServiceProvider.GetRequiredService<IVmProvider>();

        var successes = 0;
        var failures = 0;

        foreach (var vm in targetVms)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "start":
                        await vmProvider.StartVmAsync(vm);
                        break;
                    case "stop":
                        await vmProvider.StopVmAsync(vm);
                        break;
                    case "shutdown":
                        await vmProvider.ShutdownVmAsync(vm);
                        break;
                    case "snapshot":
                        await vmProvider.CreateSnapshotAsync(vm, $"Scheduled_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
                        break;
                    default:
                        _logger.LogWarning("Unknown scheduled action: {Action}", action);
                        failures++;
                        continue;
                }
                successes++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to execute {Action} on VM {Vm}", action, vm);
                failures++;
            }
        }

        return failures == 0 ? $"Success ({successes}/{targetVms.Length})" : $"Partial ({successes}/{targetVms.Length}, {failures} failures)";
    }
}
