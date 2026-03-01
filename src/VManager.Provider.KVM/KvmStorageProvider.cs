using System.Runtime.InteropServices;
using System.Xml.Linq;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;
using VManager.Libvirt.Xml;

namespace VManager.Provider.KVM;

/// <summary>
/// IStorageProvider implementation backed by libvirt storage pools and volumes.
/// </summary>
public class KvmStorageProvider : IStorageProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmStorageProvider> _logger;

    public KvmStorageProvider(IOptions<KvmOptions> options, ILogger<KvmStorageProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private LibvirtConnection OpenConnection() => new(_options.LibvirtUri);

    public Task CreateDiskAsync(CreateDiskSpec spec)
    {
        using var conn = OpenConnection();
        var pool = LibvirtNative.virStoragePoolLookupByName(conn.Handle, _options.DefaultStoragePool);
        if (pool == IntPtr.Zero)
            throw new InvalidOperationException($"Storage pool '{_options.DefaultStoragePool}' not found");

        try
        {
            var format = string.IsNullOrEmpty(spec.Format) ? _options.DefaultDiskFormat : spec.Format;
            var fileName = Path.GetFileName(spec.Path);
            if (string.IsNullOrEmpty(fileName))
                fileName = $"disk.{format}";

            var volXml = VolumeXmlBuilder.Build(fileName, spec.SizeBytes, format);
            var vol = LibvirtNative.virStorageVolCreateXML(pool, volXml, 0);
            if (vol == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create volume '{fileName}'");

            LibvirtNative.virStorageVolFree(vol);
            _logger.LogInformation("Created volume '{FileName}' ({SizeBytes} bytes, format={Format})", fileName, spec.SizeBytes, format);
        }
        finally
        {
            LibvirtNative.virStoragePoolFree(pool);
        }

        return Task.CompletedTask;
    }

    public Task DeleteDiskAsync(string diskPath)
    {
        using var conn = OpenConnection();

        // Find the volume across all pools
        var vol = FindVolumeByPath(conn, diskPath);
        if (vol == IntPtr.Zero)
            throw new InvalidOperationException($"Volume at path '{diskPath}' not found");

        try
        {
            var ret = LibvirtNative.virStorageVolDelete(vol, 0);
            if (ret < 0)
                throw new InvalidOperationException($"Failed to delete volume at '{diskPath}'");

            _logger.LogInformation("Deleted volume at '{DiskPath}'", diskPath);
        }
        finally
        {
            LibvirtNative.virStorageVolFree(vol);
        }

        return Task.CompletedTask;
    }

    public Task ResizeDiskAsync(string diskPath, long newSizeBytes)
    {
        using var conn = OpenConnection();

        var vol = FindVolumeByPath(conn, diskPath);
        if (vol == IntPtr.Zero)
            throw new InvalidOperationException($"Volume at path '{diskPath}' not found");

        try
        {
            var ret = LibvirtNative.virStorageVolResize(vol, (ulong)newSizeBytes, 0);
            if (ret < 0)
                throw new InvalidOperationException($"Failed to resize volume at '{diskPath}' to {newSizeBytes} bytes");

            _logger.LogInformation("Resized volume at '{DiskPath}' to {NewSizeBytes} bytes", diskPath, newSizeBytes);
        }
        finally
        {
            LibvirtNative.virStorageVolFree(vol);
        }

        return Task.CompletedTask;
    }

    public Task<DiskInfoDto?> GetDiskInfoAsync(string diskPath)
    {
        using var conn = OpenConnection();

        var vol = FindVolumeByPath(conn, diskPath);
        if (vol == IntPtr.Zero)
            return Task.FromResult<DiskInfoDto?>(null);

        try
        {
            LibvirtNative.virStorageVolGetInfo(vol, out var info);

            var format = info.Type switch
            {
                0 => "file",
                1 => "block",
                2 => "dir",
                3 => "network",
                _ => "unknown"
            };

            return Task.FromResult<DiskInfoDto?>(new DiskInfoDto
            {
                Path = diskPath,
                SizeBytes = (long)info.Capacity,
                UsedBytes = (long)info.Allocation,
                Format = format,
                IsDynamic = info.Allocation < info.Capacity
            });
        }
        finally
        {
            LibvirtNative.virStorageVolFree(vol);
        }
    }

    public async Task ConvertDiskAsync(string sourcePath, string destPath, string format)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("qemu-img", $"convert -f auto -O {format} \"{sourcePath}\" \"{destPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start qemu-img");
        var error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"qemu-img convert failed: {error}");
    }

    public Task AttachDiskToVmAsync(string vmNameOrId, string diskPath)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero)
            throw new InvalidOperationException($"VM '{vmNameOrId}' not found");

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get XML for VM '{vmNameOrId}'");

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
            var doc = XDocument.Parse(xml);
            var devices = doc.Root?.Element("devices");
            if (devices == null)
                throw new InvalidOperationException($"No devices section in VM '{vmNameOrId}' definition");

            // Determine next available device name (vdb, vdc, ...)
            var existingDisks = devices.Elements("disk")
                .Select(d => d.Element("target")?.Attribute("dev")?.Value)
                .Where(d => d != null)
                .ToHashSet();

            string nextDev = "vdb";
            for (char c = 'b'; c <= 'z'; c++)
            {
                var candidate = $"vd{c}";
                if (!existingDisks.Contains(candidate))
                {
                    nextDev = candidate;
                    break;
                }
            }

            var format = diskPath.EndsWith(".qcow2", StringComparison.OrdinalIgnoreCase) ? "qcow2"
                       : diskPath.EndsWith(".raw", StringComparison.OrdinalIgnoreCase) ? "raw"
                       : _options.DefaultDiskFormat;

            var diskElement = new XElement("disk",
                new XAttribute("type", "file"),
                new XAttribute("device", "disk"),
                new XElement("driver", new XAttribute("name", "qemu"), new XAttribute("type", format)),
                new XElement("source", new XAttribute("file", diskPath)),
                new XElement("target", new XAttribute("dev", nextDev), new XAttribute("bus", "virtio")));
            devices.Add(diskElement);

            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' with attached disk");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Attached disk '{DiskPath}' as {Dev} to VM '{VmName}'", diskPath, nextDev, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task DetachDiskFromVmAsync(string vmNameOrId, string diskPath)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero)
            throw new InvalidOperationException($"VM '{vmNameOrId}' not found");

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get XML for VM '{vmNameOrId}'");

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
            var doc = XDocument.Parse(xml);
            var devices = doc.Root?.Element("devices");
            if (devices == null)
                throw new InvalidOperationException($"No devices section in VM '{vmNameOrId}' definition");

            var diskElement = devices.Elements("disk")
                .FirstOrDefault(d => d.Element("source")?.Attribute("file")?.Value == diskPath);

            if (diskElement == null)
                throw new InvalidOperationException($"Disk '{diskPath}' not found attached to VM '{vmNameOrId}'");

            diskElement.Remove();

            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' after detaching disk");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Detached disk '{DiskPath}' from VM '{VmName}'", diskPath, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task<List<DiskInfoDto>> GetVmDisksAsync(string vmNameOrId)
    {
        var result = new List<DiskInfoDto>();
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero) return Task.FromResult(result);

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero) return Task.FromResult(result);

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
            var doc = XDocument.Parse(xml);
            var devices = doc.Root?.Element("devices");
            if (devices == null) return Task.FromResult(result);

            foreach (var disk in devices.Elements("disk"))
            {
                var deviceType = disk.Attribute("device")?.Value;
                if (deviceType != "disk") continue;

                var sourcePath = disk.Element("source")?.Attribute("file")?.Value ?? string.Empty;
                var format = disk.Element("driver")?.Attribute("type")?.Value ?? "unknown";

                result.Add(new DiskInfoDto
                {
                    Path = sourcePath,
                    Format = format,
                    IsDynamic = format == "qcow2"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse disk info for VM '{VmName}'", vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.FromResult(result);
    }

    public Task<List<StoragePoolDto>> ListStoragePoolsAsync()
    {
        var result = new List<StoragePoolDto>();
        using var conn = OpenConnection();

        var count = LibvirtNative.virConnectListAllStoragePools(conn.Handle, out var poolsPtr, 0);
        if (count <= 0) return Task.FromResult(result);

        var poolPtrs = new IntPtr[count];
        Marshal.Copy(poolsPtr, poolPtrs, 0, count);

        foreach (var poolPtr in poolPtrs)
        {
            try
            {
                var namePtr = LibvirtNative.virStoragePoolGetName(poolPtr);
                var name = Marshal.PtrToStringUTF8(namePtr) ?? "unknown";

                LibvirtNative.virStoragePoolGetInfo(poolPtr, out var info);

                var poolType = "dir"; // Default type
                var poolPath = string.Empty;

                // Try to get path from XML
                var xmlPtr = LibvirtNative.virStoragePoolGetXMLDesc(poolPtr, 0);
                if (xmlPtr != IntPtr.Zero)
                {
                    var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
                    try
                    {
                        var doc = XDocument.Parse(xml);
                        poolType = doc.Root?.Attribute("type")?.Value ?? "dir";
                        poolPath = doc.Root?.Element("target")?.Element("path")?.Value ?? string.Empty;
                    }
                    catch
                    {
                        // Non-fatal
                    }
                }

                result.Add(new StoragePoolDto
                {
                    Name = name,
                    Path = poolPath,
                    TotalBytes = (long)info.Capacity,
                    FreeBytes = (long)info.Available,
                    Type = poolType
                });
            }
            finally
            {
                LibvirtNative.virStoragePoolFree(poolPtr);
            }
        }

        Marshal.FreeHGlobal(poolsPtr);
        return Task.FromResult(result);
    }

    public async Task<StorageCapacityDto> GetStorageCapacityAsync()
    {
        var pools = await ListStoragePoolsAsync();
        var totalBytes = pools.Sum(p => p.TotalBytes);
        var freeBytes = pools.Sum(p => p.FreeBytes);

        return new StorageCapacityDto
        {
            TotalBytes = totalBytes,
            FreeBytes = freeBytes,
            UsedBytes = totalBytes - freeBytes,
            Pools = pools
        };
    }

    public Task<List<StorageDeviceDto>> GetVmStorageDevicesAsync(string vmNameOrId)
    {
        var result = new List<StorageDeviceDto>();
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero) return Task.FromResult(result);

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero) return Task.FromResult(result);
            var xml = Marshal.PtrToStringUTF8(xmlPtr)!;
            LibvirtNative.virFree(xmlPtr);

            var doc = XDocument.Parse(xml);
            var disks = doc.Root?.Element("devices")?.Elements("disk") ?? Enumerable.Empty<XElement>();
            int idx = 0;
            foreach (var disk in disks)
            {
                var deviceType = disk.Attribute("device")?.Value ?? "disk";
                var target = disk.Element("target");
                var source = disk.Element("source");
                result.Add(new StorageDeviceDto
                {
                    Id = $"disk-{idx}",
                    Name = target?.Attribute("dev")?.Value ?? $"disk-{idx}",
                    Type = deviceType,
                    Path = source?.Attribute("file")?.Value ?? source?.Attribute("dev")?.Value,
                    ControllerType = target?.Attribute("bus")?.Value
                });
                idx++;
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.FromResult(result);
    }

    public Task<List<StorageControllerDto>> GetVmStorageControllersAsync(string vmNameOrId)
    {
        var result = new List<StorageControllerDto>();
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero) return Task.FromResult(result);

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero) return Task.FromResult(result);
            var xml = Marshal.PtrToStringUTF8(xmlPtr)!;
            LibvirtNative.virFree(xmlPtr);

            var doc = XDocument.Parse(xml);
            var controllers = doc.Root?.Element("devices")?.Elements("controller") ?? Enumerable.Empty<XElement>();
            int idx = 0;
            foreach (var ctrl in controllers)
            {
                var type = ctrl.Attribute("type")?.Value ?? "unknown";
                if (type == "scsi" || type == "virtio-serial" || type == "sata" || type == "ide")
                {
                    result.Add(new StorageControllerDto
                    {
                        Id = $"ctrl-{idx}",
                        Name = $"{type}-{ctrl.Attribute("index")?.Value ?? idx.ToString()}",
                        Type = type,
                        ControllerNumber = int.TryParse(ctrl.Attribute("index")?.Value, out var i) ? i : idx,
                        MaxDevices = type == "scsi" ? 16 : type == "ide" ? 4 : 8
                    });
                    idx++;
                }
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.FromResult(result);
    }

    public Task AddStorageDeviceToVmAsync(string vmNameOrId, AddStorageDeviceSpec spec)
    {
        // Delegate to existing AttachDiskToVmAsync for disk type
        if (spec.Path != null)
            return AttachDiskToVmAsync(vmNameOrId, spec.Path);
        throw new ArgumentException("Path is required for adding storage device");
    }

    public Task RemoveStorageDeviceFromVmAsync(string vmNameOrId, string deviceId)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero) throw new InvalidOperationException($"VM '{vmNameOrId}' not found");

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero) throw new InvalidOperationException("Failed to get domain XML");
            var xml = Marshal.PtrToStringUTF8(xmlPtr)!;
            LibvirtNative.virFree(xmlPtr);

            var doc = XDocument.Parse(xml);
            var disks = doc.Root?.Element("devices")?.Elements("disk").ToList() ?? new();
            // Parse "disk-{index}" to get the index
            if (deviceId.StartsWith("disk-") && int.TryParse(deviceId.AsSpan(5), out var index) && index < disks.Count)
            {
                disks[index].Remove();
                LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            }
            else
            {
                throw new InvalidOperationException($"Device '{deviceId}' not found");
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds a storage volume across all pools by its path.
    /// </summary>
    private IntPtr FindVolumeByPath(LibvirtConnection conn, string path)
    {
        var poolCount = LibvirtNative.virConnectListAllStoragePools(conn.Handle, out var poolsPtr, 0);
        if (poolCount <= 0) return IntPtr.Zero;

        var poolPtrs = new IntPtr[poolCount];
        Marshal.Copy(poolsPtr, poolPtrs, 0, poolCount);

        IntPtr foundVol = IntPtr.Zero;

        foreach (var poolPtr in poolPtrs)
        {
            try
            {
                var volCount = LibvirtNative.virStoragePoolListAllVolumes(poolPtr, out var volsPtr, 0);
                if (volCount <= 0) continue;

                var volPtrs = new IntPtr[volCount];
                Marshal.Copy(volsPtr, volPtrs, 0, volCount);

                foreach (var volPtr in volPtrs)
                {
                    if (foundVol != IntPtr.Zero)
                    {
                        LibvirtNative.virStorageVolFree(volPtr);
                        continue;
                    }

                    var pathPtr = LibvirtNative.virStorageVolGetPath(volPtr);
                    var volPath = Marshal.PtrToStringUTF8(pathPtr) ?? string.Empty;

                    if (volPath == path)
                    {
                        foundVol = volPtr; // Don't free this one - caller will free it
                    }
                    else
                    {
                        LibvirtNative.virStorageVolFree(volPtr);
                    }
                }

                Marshal.FreeHGlobal(volsPtr);
            }
            finally
            {
                LibvirtNative.virStoragePoolFree(poolPtr);
            }
        }

        Marshal.FreeHGlobal(poolsPtr);
        return foundVol;
    }
}
