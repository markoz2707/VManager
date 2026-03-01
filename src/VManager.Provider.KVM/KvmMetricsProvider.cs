using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;

namespace VManager.Provider.KVM;

public class KvmMetricsProvider : IMetricsProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmMetricsProvider> _logger;

    public KvmMetricsProvider(IOptions<KvmOptions> options, ILogger<KvmMetricsProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<HostUsageDto> GetHostUsageAsync()
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var nodeInfo = conn.GetNodeInfo();
        var freeMem = conn.GetFreeMemory();
        var totalMemKb = nodeInfo.Memory;
        var totalMemMb = (long)(totalMemKb / 1024);
        var freeMemMb = (long)(freeMem / 1024 / 1024);
        var usedPercent = totalMemKb > 0 ? (1.0 - (double)freeMem / (totalMemKb * 1024)) * 100 : 0;

        return Task.FromResult(new HostUsageDto
        {
            CpuCores = (int)nodeInfo.Cores,
            LogicalProcessors = (int)nodeInfo.Cpus,
            TotalMemoryMB = totalMemMb,
            AvailableMemoryMB = freeMemMb,
            MemoryUsagePercent = usedPercent
        });
    }

    public Task<VmUsageDto> GetVmUsageAsync(string vmNameOrId)
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero)
            return Task.FromResult(new VmUsageDto());

        LibvirtNative.virDomainGetInfo(domain, out var info);
        LibvirtNative.virDomainFree(domain);

        return Task.FromResult(new VmUsageDto
        {
            MemoryAssignedMB = (long)(info.MaxMem / 1024),
            MemoryDemandMB = (long)(info.Memory / 1024),
            MemoryUsagePercent = info.MaxMem > 0 ? (double)info.Memory / info.MaxMem * 100 : 0,
            CpuUsagePercent = 0 // Would need sampling over time
        });
    }

    public Task<DiskMetricsDto?> GetDiskMetricsAsync(string vmNameOrId)
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) return Task.FromResult<DiskMetricsDto?>(null);

        try
        {
            // Parse domain XML to find actual disk device names
            var diskDevice = GetFirstDeviceName(domain, "disk");

            var result = LibvirtNative.virDomainBlockStats(domain, diskDevice, out var stats,
                (nint)System.Runtime.InteropServices.Marshal.SizeOf<VirDomainBlockStats>());

            if (result < 0) return Task.FromResult<DiskMetricsDto?>(null);

            return Task.FromResult<DiskMetricsDto?>(new DiskMetricsDto
            {
                ReadBytesPerSec = stats.RdBytes,
                WriteBytesPerSec = stats.WrBytes,
                ReadOperationsPerSec = stats.RdReq,
                WriteOperationsPerSec = stats.WrReq
            });
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }
    }

    public Task<NetworkMetricsDto?> GetNetworkMetricsAsync(string vmNameOrId)
    {
        using var conn = new LibvirtConnection(_options.LibvirtUri);
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) return Task.FromResult<NetworkMetricsDto?>(null);

        try
        {
            // Parse domain XML to find actual network interface target dev
            var netDevice = GetFirstInterfaceTarget(domain);

            var result = LibvirtNative.virDomainInterfaceStats(domain, netDevice, out var stats,
                (nint)System.Runtime.InteropServices.Marshal.SizeOf<VirDomainInterfaceStats>());

            if (result < 0) return Task.FromResult<NetworkMetricsDto?>(null);

            return Task.FromResult<NetworkMetricsDto?>(new NetworkMetricsDto
            {
                BytesReceivedPerSec = stats.RxBytes,
                BytesSentPerSec = stats.TxBytes
            });
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }
    }

    private static string GetFirstDeviceName(IntPtr domain, string deviceType)
    {
        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr != IntPtr.Zero)
            {
                var xml = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(xmlPtr)!;
                LibvirtNative.virFree(xmlPtr);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var disk = doc.Root?.Element("devices")?.Elements("disk")
                    .FirstOrDefault(d => d.Attribute("device")?.Value == deviceType);
                var target = disk?.Element("target")?.Attribute("dev")?.Value;
                if (!string.IsNullOrEmpty(target)) return target;
            }
        }
        catch { }

        return "vda"; // fallback
    }

    private static string GetFirstInterfaceTarget(IntPtr domain)
    {
        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr != IntPtr.Zero)
            {
                var xml = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(xmlPtr)!;
                LibvirtNative.virFree(xmlPtr);
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var iface = doc.Root?.Element("devices")?.Element("interface");
                var target = iface?.Element("target")?.Attribute("dev")?.Value;
                if (!string.IsNullOrEmpty(target)) return target;
            }
        }
        catch { }

        return "vnet0"; // fallback
    }
}
