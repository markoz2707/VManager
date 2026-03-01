using System.Diagnostics.Eventing.Reader;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVEventLogProvider : IEventLogProvider
{
    private readonly ILogger<HyperVEventLogProvider> _logger;

    private static readonly string[] DefaultLogChannels =
    {
        "Microsoft-Windows-Hyper-V-VMMS-Admin",
        "Microsoft-Windows-Hyper-V-Worker-Admin",
        "Microsoft-Windows-Hyper-V-VmSwitch-Operational",
        "Microsoft-Windows-Hyper-V-Hypervisor-Admin",
        "Microsoft-Windows-Hyper-V-StorageVSP-Admin",
        "System",
        "Application"
    };

    private static readonly Dictionary<string, string> SourceToChannel = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hyper-V-VMMS"] = "Microsoft-Windows-Hyper-V-VMMS-Admin",
        ["Hyper-V-Worker"] = "Microsoft-Windows-Hyper-V-Worker-Admin",
        ["Hyper-V-VmSwitch"] = "Microsoft-Windows-Hyper-V-VmSwitch-Operational",
        ["Hyper-V-Hypervisor"] = "Microsoft-Windows-Hyper-V-Hypervisor-Admin",
        ["Hyper-V-StorageVSP"] = "Microsoft-Windows-Hyper-V-StorageVSP-Admin",
        ["System"] = "System",
        ["Application"] = "Application"
    };

    public HyperVEventLogProvider(ILogger<HyperVEventLogProvider> logger)
    {
        _logger = logger;
    }

    public async Task<LogsResponse> GetLogsAsync(string? source, string? level, DateTime? start, DateTime? end, int limit, string? search)
    {
        return await Task.Run(() =>
        {
            var entries = new List<LogEntryDto>();
            var channels = GetChannelsForSource(source);
            var startTime = start ?? DateTime.UtcNow.AddHours(-24);
            var endTime = end ?? DateTime.UtcNow;

            foreach (var channel in channels)
            {
                try
                {
                    var query = BuildQuery(channel, level, startTime, endTime);
                    using var reader = new EventLogReader(new EventLogQuery(channel, PathType.LogName, query));

                    EventRecord? record;
                    while ((record = reader.ReadEvent()) != null)
                    {
                        using (record)
                        {
                            var entry = MapToLogEntry(record, channel);

                            if (!string.IsNullOrEmpty(search) &&
                                !entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                                !entry.Source.Contains(search, StringComparison.OrdinalIgnoreCase))
                                continue;

                            entries.Add(entry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read event log channel {Channel}", channel);
                }
            }

            entries = entries.OrderByDescending(e => e.Timestamp).ToList();
            var totalCount = entries.Count;
            entries = entries.Take(limit).ToList();

            return new LogsResponse
            {
                Entries = entries,
                TotalCount = totalCount,
                Sources = SourceToChannel.Keys.ToList()
            };
        });
    }

    public Task<List<string>> GetLogSourcesAsync()
    {
        return Task.FromResult(SourceToChannel.Keys.ToList());
    }

    private static string[] GetChannelsForSource(string? source)
    {
        if (string.IsNullOrEmpty(source))
            return DefaultLogChannels;

        if (SourceToChannel.TryGetValue(source, out var channel))
            return new[] { channel };

        return DefaultLogChannels;
    }

    private static string BuildQuery(string channel, string? level, DateTime startTime, DateTime endTime)
    {
        var startStr = startTime.ToUniversalTime().ToString("o");
        var endStr = endTime.ToUniversalTime().ToString("o");

        var query = $"*[System[TimeCreated[@SystemTime>='{startStr}' and @SystemTime<='{endStr}']";

        if (!string.IsNullOrEmpty(level))
        {
            var eventLevel = level.ToLowerInvariant() switch
            {
                "critical" => 1,
                "error" => 2,
                "warning" => 3,
                "information" => 4,
                _ => (int?)null
            };

            if (eventLevel.HasValue)
                query += $" and Level={eventLevel.Value}";
        }

        query += "]]";
        return query;
    }

    private static LogEntryDto MapToLogEntry(EventRecord record, string channel)
    {
        var levelStr = record.Level switch
        {
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 or 0 => "Information",
            _ => "Information"
        };

        var sourceName = channel switch
        {
            var c when c.Contains("VMMS") => "Hyper-V-VMMS",
            var c when c.Contains("Worker") => "Hyper-V-Worker",
            var c when c.Contains("VmSwitch") => "Hyper-V-VmSwitch",
            var c when c.Contains("Hypervisor") => "Hyper-V-Hypervisor",
            var c when c.Contains("StorageVSP") => "Hyper-V-StorageVSP",
            "System" => "System",
            "Application" => "Application",
            _ => channel
        };

        string message;
        try
        {
            message = record.FormatDescription() ?? record.ToXml();
        }
        catch
        {
            message = $"Event ID {record.Id}";
        }

        return new LogEntryDto
        {
            Id = $"{record.RecordId}",
            Timestamp = record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow,
            Level = levelStr,
            Source = sourceName,
            Message = message,
            EventId = record.Id,
            Category = record.TaskDisplayName
        };
    }
}
