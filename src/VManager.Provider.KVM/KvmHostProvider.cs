using System.Runtime.InteropServices;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;

namespace VManager.Provider.KVM;

public class KvmHostProvider : IHostProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmHostProvider> _logger;

    public string HypervisorType => "KVM";
    public string AgentVersion => "1.0.0";

    public KvmHostProvider(IOptions<KvmOptions> options, ILogger<KvmHostProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HostInfoDto> GetHostInfoAsync()
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var nodeInfo = conn.GetNodeInfo();
        var hostname = conn.GetHostname();
        var version = conn.GetVersion();

        return Task.FromResult(new HostInfoDto
        {
            Hostname = hostname,
            HypervisorType = "KVM",
            HypervisorVersion = FormatVersion(version),
            OperatingSystem = "Linux",
            OsVersion = Environment.OSVersion.VersionString,
            CpuCores = (int)nodeInfo.Cores,
            LogicalProcessors = (int)nodeInfo.Cpus,
            TotalMemoryMB = (long)(nodeInfo.Memory / 1024)
        });
    }

    public Task<HostPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var nodeInfo = conn.GetNodeInfo();
        var freeMem = conn.GetFreeMemory();
        var totalMem = nodeInfo.Memory * 1024; // KB -> bytes

        double memUsage = totalMem > 0 ? (1.0 - (double)freeMem / totalMem) * 100 : 0;

        // CPU usage from /proc/stat would be more accurate, this is simplified
        return Task.FromResult(new HostPerformanceMetrics
        {
            CpuUsagePercent = 0, // Would need /proc/stat parsing for real value
            MemoryUsagePercent = memUsage,
            MemoryAvailableMB = (long)(freeMem / 1024 / 1024)
        });
    }

    public Task ShutdownHostAsync(bool force = false)
    {
        _logger.LogWarning("Initiating host shutdown (force={Force})", force);
        var args = force ? "-h now" : "-h +0";
        System.Diagnostics.Process.Start("shutdown", args);
        return Task.CompletedTask;
    }

    public Task RebootHostAsync(bool force = false)
    {
        _logger.LogWarning("Initiating host reboot (force={Force})", force);
        var args = force ? "-r now" : "-r +0";
        System.Diagnostics.Process.Start("shutdown", args);
        return Task.CompletedTask;
    }

    public Task<HypervisorCapabilities> GetCapabilitiesAsync()
    {
        return Task.FromResult(new HypervisorCapabilities
        {
            HypervisorType = "KVM",
            SupportsLiveMigration = true,
            SupportsSnapshots = true,
            SupportsDynamicMemory = true,
            SupportsNestedVirtualization = true,
            SupportsContainers = true, // Docker/Podman
            SupportsReplication = false,
            SupportsFibreChannel = false,
            SupportsStorageQoS = false,
            ConsoleType = "vnc",
            MaxVmCount = 0, // No hard limit
            MaxCpuPerVm = 288,
            MaxMemoryPerVmMB = 6 * 1024 * 1024, // 6 TB
            SupportedDiskFormats = new List<string> { "qcow2", "raw", "vmdk", "vdi" }
        });
    }

    private static string FormatVersion(ulong version)
    {
        var major = version / 1000000;
        var minor = (version % 1000000) / 1000;
        var release = version % 1000;
        return $"{major}.{minor}.{release}";
    }
}
