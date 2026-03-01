using System.Text.Json;
using HyperV.Contracts.Models.Common;

namespace HyperV.Agent.Services;

public class ScheduleStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private List<ScheduledTaskDto> _tasks = new();

    public ScheduleStore(IConfiguration configuration)
    {
        var basePath = AppContext.BaseDirectory;
        _filePath = Path.Combine(basePath, "schedules.json");
        Load();
    }

    public List<ScheduledTaskDto> GetAll()
    {
        lock (_lock)
        {
            return _tasks.ToList();
        }
    }

    public ScheduledTaskDto? GetById(string id)
    {
        lock (_lock)
        {
            return _tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public ScheduledTaskDto Add(CreateScheduledTaskRequest request)
    {
        var task = new ScheduledTaskDto
        {
            Id = Guid.NewGuid().ToString("N")[..12],
            Name = request.Name,
            CronExpression = request.CronExpression,
            Action = request.Action,
            TargetVms = request.TargetVms,
            IsEnabled = true
        };

        lock (_lock)
        {
            _tasks.Add(task);
            Save();
        }

        return task;
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            var removed = _tasks.RemoveAll(t => t.Id == id);
            if (removed > 0) Save();
            return removed > 0;
        }
    }

    public bool SetEnabled(string id, bool enabled)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return false;
            task.IsEnabled = enabled;
            Save();
            return true;
        }
    }

    public void UpdateLastRun(string id, DateTime lastRunUtc, string result)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return;
            task.LastRunUtc = lastRunUtc;
            task.LastRunResult = result;
            Save();
        }
    }

    public void UpdateNextRun(string id, DateTime? nextRunUtc)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null) return;
            task.NextRunUtc = nextRunUtc;
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _tasks = JsonSerializer.Deserialize<List<ScheduledTaskDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();
            }
        }
        catch
        {
            _tasks = new();
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_tasks, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Silently fail if file write fails
        }
    }
}
