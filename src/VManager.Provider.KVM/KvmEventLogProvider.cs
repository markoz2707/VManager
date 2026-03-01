using System.Diagnostics;
using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.KVM;

public class KvmEventLogProvider : IEventLogProvider
{
    private readonly ILogger<KvmEventLogProvider> _logger;

    private static readonly List<string> LogSources = new()
    {
        "libvirtd",
        "qemu",
        "kernel",
        "systemd",
        "syslog"
    };

    public KvmEventLogProvider(ILogger<KvmEventLogProvider> logger)
    {
        _logger = logger;
    }

    public async Task<LogsResponse> GetLogsAsync(string? source, string? level, DateTime? start, DateTime? end, int limit, string? search)
    {
        var entries = new List<LogEntryDto>();
        var startTime = start ?? DateTime.UtcNow.AddHours(-24);
        var endTime = end ?? DateTime.UtcNow;

        try
        {
            var args = BuildJournalctlArgs(source, level, startTime, endTime, search);
            var output = await RunCommandAsync("journalctl", args);

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var entry = ParseJournalEntry(line);
                    if (entry != null)
                        entries.Add(entry);
                }
                catch
                {
                    // Skip unparseable lines
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read journalctl logs, trying /var/log/syslog");

            try
            {
                var syslogEntries = await ReadSyslogAsync(source, level, startTime, endTime, search);
                entries.AddRange(syslogEntries);
            }
            catch (Exception syslogEx)
            {
                _logger.LogWarning(syslogEx, "Failed to read syslog");
            }
        }

        entries = entries.OrderByDescending(e => e.Timestamp).ToList();
        var totalCount = entries.Count;
        entries = entries.Take(limit).ToList();

        return new LogsResponse
        {
            Entries = entries,
            TotalCount = totalCount,
            Sources = LogSources
        };
    }

    public Task<List<string>> GetLogSourcesAsync()
    {
        return Task.FromResult(new List<string>(LogSources));
    }

    private static string BuildJournalctlArgs(string? source, string? level, DateTime startTime, DateTime endTime, string? search)
    {
        var args = new List<string>
        {
            "-o", "json",
            "--no-pager",
            "--since", startTime.ToString("yyyy-MM-dd HH:mm:ss"),
            "--until", endTime.ToString("yyyy-MM-dd HH:mm:ss")
        };

        if (!string.IsNullOrEmpty(source))
        {
            var unit = source.ToLowerInvariant() switch
            {
                "libvirtd" => "libvirtd",
                "qemu" => "qemu*",
                "kernel" => "_TRANSPORT=kernel",
                _ => source
            };

            if (source.ToLowerInvariant() == "kernel")
                args.AddRange(new[] { "-k" });
            else
                args.AddRange(new[] { "--unit", unit });
        }
        else
        {
            // Default: libvirt and qemu related
            args.AddRange(new[] { "--unit", "libvirtd", "--unit", "qemu*" });
        }

        if (!string.IsNullOrEmpty(level))
        {
            var priority = level.ToLowerInvariant() switch
            {
                "critical" => "2",
                "error" => "3",
                "warning" => "4",
                "information" => "6",
                _ => null
            };

            if (priority != null)
                args.AddRange(new[] { "-p", priority });
        }

        if (!string.IsNullOrEmpty(search))
            args.AddRange(new[] { "--grep", search });

        return string.Join(" ", args);
    }

    private static LogEntryDto? ParseJournalEntry(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        var timestamp = DateTime.UtcNow;
        if (root.TryGetProperty("__REALTIME_TIMESTAMP", out var ts) && long.TryParse(ts.GetString(), out var usec))
            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(usec / 1000).UtcDateTime;

        var priority = root.TryGetProperty("PRIORITY", out var p) ? p.GetString() : "6";
        var levelStr = priority switch
        {
            "0" or "1" or "2" => "Critical",
            "3" => "Error",
            "4" => "Warning",
            "5" or "6" => "Information",
            _ => "Information"
        };

        var message = root.TryGetProperty("MESSAGE", out var msg) ? msg.GetString() ?? "" : "";
        var unit = root.TryGetProperty("_SYSTEMD_UNIT", out var u) ? u.GetString() ?? "unknown" : "unknown";
        var source = unit.Replace(".service", "");

        return new LogEntryDto
        {
            Id = root.TryGetProperty("__CURSOR", out var cursor) ? cursor.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
            Timestamp = timestamp,
            Level = levelStr,
            Source = source,
            Message = message,
            EventId = root.TryGetProperty("_PID", out var pid) && int.TryParse(pid.GetString(), out var pidInt) ? pidInt : null,
            Category = root.TryGetProperty("SYSLOG_IDENTIFIER", out var ident) ? ident.GetString() : null
        };
    }

    private async Task<List<LogEntryDto>> ReadSyslogAsync(string? source, string? level, DateTime startTime, DateTime endTime, string? search)
    {
        var entries = new List<LogEntryDto>();
        var syslogPath = "/var/log/syslog";

        if (!File.Exists(syslogPath))
            syslogPath = "/var/log/messages";

        if (!File.Exists(syslogPath))
            return entries;

        var lines = await File.ReadAllLinesAsync(syslogPath);
        var counter = 0;

        foreach (var line in lines.Reverse())
        {
            if (counter >= 500) break;

            if (!string.IsNullOrEmpty(source) && !line.Contains(source, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrEmpty(search) && !line.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;

            entries.Add(new LogEntryDto
            {
                Id = (++counter).ToString(),
                Timestamp = DateTime.UtcNow,
                Level = "Information",
                Source = "syslog",
                Message = line
            });
        }

        return entries;
    }

    private static async Task<string> RunCommandAsync(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
