using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IMetricsProvider
{
    Task<HostUsageDto> GetHostUsageAsync();
    Task<VmUsageDto> GetVmUsageAsync(string vmNameOrId);
    Task<DiskMetricsDto?> GetDiskMetricsAsync(string vmNameOrId);
    Task<NetworkMetricsDto?> GetNetworkMetricsAsync(string vmNameOrId);
}
