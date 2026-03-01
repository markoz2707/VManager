using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IEventLogProvider
{
    Task<LogsResponse> GetLogsAsync(string? source, string? level, DateTime? start, DateTime? end, int limit, string? search);
    Task<List<string>> GetLogSourcesAsync();
}
