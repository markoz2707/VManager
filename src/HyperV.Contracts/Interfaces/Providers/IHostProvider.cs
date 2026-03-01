using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IHostProvider
{
    string HypervisorType { get; }
    string AgentVersion { get; }

    Task<HostInfoDto> GetHostInfoAsync();
    Task<HostPerformanceMetrics> GetPerformanceMetricsAsync();
    Task<HypervisorCapabilities> GetCapabilitiesAsync();
}
