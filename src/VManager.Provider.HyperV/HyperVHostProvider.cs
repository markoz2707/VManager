using global::HyperV.Contracts.Interfaces;
using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVHostProvider : IHostProvider
{
    private readonly IHostInfoService _hostInfoService;
    private readonly ILogger<HyperVHostProvider> _logger;

    public string HypervisorType => "Hyper-V";
    public string AgentVersion => "1.0.0";

    public HyperVHostProvider(IHostInfoService hostInfoService, ILogger<HyperVHostProvider> logger)
    {
        _hostInfoService = hostInfoService;
        _logger = logger;
    }

    public async Task<HostInfoDto> GetHostInfoAsync()
    {
        var hardware = await _hostInfoService.GetHostHardwareInfoAsync();
        var system = await _hostInfoService.GetSystemInfoAsync();

        return new HostInfoDto
        {
            Hostname = Environment.MachineName,
            HypervisorType = "Hyper-V",
            HypervisorVersion = system.OsVersion,
            OperatingSystem = system.OsName,
            OsVersion = system.OsBuildNumber,
            TotalMemoryMB = (long)(hardware.TotalPhysicalMemory / 1024 / 1024),
            ExtendedProperties = new Dictionary<string, object>
            {
                ["manufacturer"] = hardware.Manufacturer,
                ["model"] = hardware.Model,
                ["biosVersion"] = hardware.BiosVersion
            }
        };
    }

    public async Task<HostPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        var perf = await _hostInfoService.GetPerformanceSummaryAsync();
        return new HostPerformanceMetrics
        {
            CpuUsagePercent = perf.CpuUsagePercent,
            MemoryUsagePercent = perf.MemoryUsagePercent,
            StorageUsagePercent = perf.StorageUsagePercent
        };
    }

    public Task<HypervisorCapabilities> GetCapabilitiesAsync()
    {
        return Task.FromResult(new HypervisorCapabilities
        {
            HypervisorType = "Hyper-V",
            SupportsLiveMigration = true,
            SupportsSnapshots = true,
            SupportsDynamicMemory = true,
            SupportsNestedVirtualization = true,
            SupportsContainers = true,
            SupportsReplication = true,
            SupportsFibreChannel = true,
            SupportsStorageQoS = true,
            ConsoleType = "rdp",
            MaxVmCount = 1024,
            MaxCpuPerVm = 240,
            MaxMemoryPerVmMB = 12 * 1024 * 1024, // 12 TB
            SupportedDiskFormats = new List<string> { "vhd", "vhdx" }
        });
    }
}
