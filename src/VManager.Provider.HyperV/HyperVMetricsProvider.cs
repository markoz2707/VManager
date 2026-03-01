using global::HyperV.Contracts.Interfaces;
using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models.Common;

namespace VManager.Provider.HyperV;

public class HyperVMetricsProvider : IMetricsProvider
{
    private readonly IHostInfoService _hostInfoService;

    public HyperVMetricsProvider(IHostInfoService hostInfoService)
    {
        _hostInfoService = hostInfoService;
    }

    public async Task<HostUsageDto> GetHostUsageAsync()
    {
        var perf = await _hostInfoService.GetPerformanceSummaryAsync();
        var hardware = await _hostInfoService.GetHostHardwareInfoAsync();
        var totalMemMb = (long)(hardware.TotalPhysicalMemory / 1024 / 1024);
        var availMemMb = (long)(totalMemMb * (1.0 - perf.MemoryUsagePercent / 100.0));

        return new HostUsageDto
        {
            CpuUsagePercent = perf.CpuUsagePercent,
            TotalMemoryMB = totalMemMb,
            AvailableMemoryMB = availMemMb,
            MemoryUsagePercent = perf.MemoryUsagePercent
        };
    }

    public Task<VmUsageDto> GetVmUsageAsync(string vmNameOrId)
    {
        // Hyper-V VM-level metrics require WMI performance counter sampling
        return Task.FromResult(new VmUsageDto());
    }

    public Task<DiskMetricsDto?> GetDiskMetricsAsync(string vmNameOrId)
    {
        return Task.FromResult<DiskMetricsDto?>(null);
    }

    public Task<NetworkMetricsDto?> GetNetworkMetricsAsync(string vmNameOrId)
    {
        return Task.FromResult<NetworkMetricsDto?>(null);
    }
}
