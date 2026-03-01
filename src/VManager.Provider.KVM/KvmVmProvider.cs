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

public class KvmVmProvider : IVmProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmVmProvider> _logger;

    public KvmVmProvider(IOptions<KvmOptions> options, ILogger<KvmVmProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private LibvirtConnection OpenConnection() => new(_options.LibvirtUri);

    private static string GetDomainState(byte state) => state switch
    {
        1 => "Running",
        2 => "Blocked",
        3 => "Paused",
        4 => "Shutting down",
        5 => "Off",
        6 => "Crashed",
        7 => "Suspended",
        _ => "Unknown"
    };

    public Task<List<VmSummaryDto>> ListVmsAsync()
    {
        using var conn = OpenConnection();
        var result = new List<VmSummaryDto>();

        var count = LibvirtNative.virConnectListAllDomains(conn.Handle, out var domainsPtr, 0);
        if (count <= 0) return Task.FromResult(result);

        var domainPtrs = new IntPtr[count];
        Marshal.Copy(domainsPtr, domainPtrs, 0, count);

        foreach (var domPtr in domainPtrs)
        {
            try
            {
                LibvirtNative.virDomainGetInfo(domPtr, out var info);
                var namePtr = LibvirtNative.virDomainGetName(domPtr);
                var name = Marshal.PtrToStringUTF8(namePtr) ?? "unknown";

                var uuid = new byte[37];
                LibvirtNative.virDomainGetUUIDString(domPtr, uuid);
                var uuidStr = System.Text.Encoding.UTF8.GetString(uuid).TrimEnd('\0');

                result.Add(new VmSummaryDto
                {
                    Id = uuidStr,
                    Name = name,
                    State = GetDomainState(info.State),
                    CpuCount = info.NrVirtCpu,
                    MemoryMB = (long)(info.MaxMem / 1024)
                });
            }
            finally
            {
                LibvirtNative.virDomainFree(domPtr);
            }
        }

        Marshal.FreeHGlobal(domainsPtr);
        return Task.FromResult(result);
    }

    public async Task<VmDetailsDto?> GetVmAsync(string vmId)
    {
        var vms = await ListVmsAsync();
        var vm = vms.FirstOrDefault(v => v.Id == vmId || v.Name == vmId);
        if (vm == null) return null;

        return new VmDetailsDto
        {
            Id = vm.Id,
            Name = vm.Name,
            State = vm.State,
            CpuCount = vm.CpuCount,
            MemoryMB = vm.MemoryMB
        };
    }

    public async Task<VmPropertiesDto?> GetVmPropertiesAsync(string vmNameOrId)
    {
        var vm = await GetVmAsync(vmNameOrId);
        if (vm == null) return null;

        return new VmPropertiesDto
        {
            CpuCount = vm.CpuCount,
            MemoryMB = vm.MemoryMB
        };
    }

    public Task<string> CreateVmAsync(CreateVmSpec spec)
    {
        using var conn = OpenConnection();

        var diskPath = Path.Combine(_options.DefaultDiskPath, $"{spec.Name}.{_options.DefaultDiskFormat}");

        // Create disk volume
        var pool = LibvirtNative.virStoragePoolLookupByName(conn.Handle, _options.DefaultStoragePool);
        if (pool != IntPtr.Zero)
        {
            var volXml = VolumeXmlBuilder.Build($"{spec.Name}.{_options.DefaultDiskFormat}", spec.DiskSizeGB * 1024L * 1024L * 1024L, _options.DefaultDiskFormat);
            var vol = LibvirtNative.virStorageVolCreateXML(pool, volXml, 0);
            if (vol != IntPtr.Zero) LibvirtNative.virStorageVolFree(vol);
            LibvirtNative.virStoragePoolFree(pool);
        }

        // Define domain
        var xml = DomainXmlBuilder.Build(spec, diskPath, spec.NetworkName);
        var domain = conn.DefineDomain(xml);

        if (domain == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to define domain '{spec.Name}'");

        LibvirtNative.virDomainFree(domain);
        return Task.FromResult(spec.Name);
    }

    public Task DeleteVmAsync(string vmId)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmId);
        if (domain == IntPtr.Zero) throw new InvalidOperationException($"VM '{vmId}' not found");

        LibvirtNative.virDomainGetInfo(domain, out var info);
        if (info.State == 1) LibvirtNative.virDomainDestroy(domain);
        LibvirtNative.virDomainUndefine(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    private IntPtr LookupDomain(LibvirtConnection conn, string vmNameOrId)
    {
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero) throw new InvalidOperationException($"VM '{vmNameOrId}' not found");
        return domain;
    }

    public Task StartVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainCreate(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task StopVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainDestroy(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task ShutdownVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainShutdown(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task PauseVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainSuspend(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task ResumeVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainResume(domain);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task SaveVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        var savePath = Path.Combine(_options.DefaultDiskPath, $"{vmNameOrId}.save");
        LibvirtNative.virDomainSave(domain, savePath);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task RestartVmAsync(string vmNameOrId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        LibvirtNative.virDomainReboot(domain, 0);
        LibvirtNative.virDomainFree(domain);
        return Task.CompletedTask;
    }

    public Task SetCpuCountAsync(string vmNameOrId, int cpuCount)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        try
        {
            // Get current domain XML
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get XML description for VM '{vmNameOrId}'");

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;

            // Parse and modify the vcpu element
            var doc = XDocument.Parse(xml);
            var vcpuElement = doc.Root?.Element("vcpu");
            if (vcpuElement != null)
            {
                vcpuElement.Value = cpuCount.ToString();
            }
            else
            {
                doc.Root?.Add(new XElement("vcpu", new XAttribute("placement", "static"), cpuCount.ToString()));
            }

            // Redefine the domain with updated XML
            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' with updated CPU count");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Set CPU count to {CpuCount} for VM '{VmName}'", cpuCount, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task SetMemoryAsync(string vmNameOrId, long memoryMB)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        try
        {
            // Get current domain XML
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get XML description for VM '{vmNameOrId}'");

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;

            // Parse and modify memory elements
            var doc = XDocument.Parse(xml);
            var memoryElement = doc.Root?.Element("memory");
            var currentMemoryElement = doc.Root?.Element("currentMemory");

            if (memoryElement != null)
            {
                memoryElement.SetAttributeValue("unit", "MiB");
                memoryElement.Value = memoryMB.ToString();
            }
            else
            {
                doc.Root?.Add(new XElement("memory", new XAttribute("unit", "MiB"), memoryMB.ToString()));
            }

            if (currentMemoryElement != null)
            {
                currentMemoryElement.SetAttributeValue("unit", "MiB");
                currentMemoryElement.Value = memoryMB.ToString();
            }
            else
            {
                doc.Root?.Add(new XElement("currentMemory", new XAttribute("unit", "MiB"), memoryMB.ToString()));
            }

            // Redefine the domain with updated XML
            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' with updated memory");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Set memory to {MemoryMB} MiB for VM '{VmName}'", memoryMB, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task<List<VmSnapshotDto>> ListSnapshotsAsync(string vmNameOrId)
    {
        var result = new List<VmSnapshotDto>();
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        try
        {
            var count = LibvirtNative.virDomainSnapshotNum(domain, 0);
            if (count <= 0) return Task.FromResult(result);

            // Allocate array of IntPtr for snapshot names
            var namesPtr = Marshal.AllocHGlobal(IntPtr.Size * count);
            try
            {
                var listed = LibvirtNative.virDomainSnapshotListNames(domain, namesPtr, count, 0);
                if (listed <= 0) return Task.FromResult(result);

                for (int i = 0; i < listed; i++)
                {
                    var namePtr = Marshal.ReadIntPtr(namesPtr, i * IntPtr.Size);
                    var name = Marshal.PtrToStringUTF8(namePtr) ?? "unknown";

                    var snapshot = LibvirtNative.virDomainSnapshotLookupByName(domain, name, 0);
                    if (snapshot == IntPtr.Zero) continue;

                    try
                    {
                        var dto = new VmSnapshotDto
                        {
                            Id = name,
                            Name = name,
                            CreatedTime = DateTime.MinValue
                        };

                        // Try to extract creation time from snapshot XML
                        var xmlPtr = LibvirtNative.virDomainSnapshotGetXMLDesc(snapshot, 0);
                        if (xmlPtr != IntPtr.Zero)
                        {
                            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
                            try
                            {
                                var doc = XDocument.Parse(xml);
                                var creationTimeStr = doc.Root?.Element("creationTime")?.Value;
                                if (creationTimeStr != null && long.TryParse(creationTimeStr, out var unixTime))
                                {
                                    dto.CreatedTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;
                                }

                                var parentName = doc.Root?.Element("parent")?.Element("name")?.Value;
                                dto.ParentId = parentName;
                            }
                            catch
                            {
                                // XML parsing failure is non-fatal for listing
                            }
                        }

                        result.Add(dto);
                    }
                    finally
                    {
                        LibvirtNative.virDomainSnapshotFree(snapshot);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(namesPtr);
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.FromResult(result);
    }

    public Task<string> CreateSnapshotAsync(string vmNameOrId, string snapshotName)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        var xml = DomainXmlBuilder.BuildSnapshotXml(snapshotName);
        var snap = LibvirtNative.virDomainSnapshotCreateXML(domain, xml, 0);
        LibvirtNative.virDomainFree(domain);
        return Task.FromResult(snapshotName);
    }

    public Task DeleteSnapshotAsync(string vmNameOrId, string snapshotId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        try
        {
            var snapshot = LibvirtNative.virDomainSnapshotLookupByName(domain, snapshotId, 0);
            if (snapshot == IntPtr.Zero)
                throw new InvalidOperationException($"Snapshot '{snapshotId}' not found for VM '{vmNameOrId}'");

            try
            {
                // flags=0 deletes just this snapshot (children are reparented)
                var ret = LibvirtNative.virDomainSnapshotDelete(snapshot, 0);
                if (ret < 0)
                    throw new InvalidOperationException($"Failed to delete snapshot '{snapshotId}' for VM '{vmNameOrId}'");

                _logger.LogInformation("Deleted snapshot '{SnapshotId}' for VM '{VmName}'", snapshotId, vmNameOrId);
            }
            finally
            {
                LibvirtNative.virDomainSnapshotFree(snapshot);
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task ApplySnapshotAsync(string vmNameOrId, string snapshotId)
    {
        using var conn = OpenConnection();
        var domain = LookupDomain(conn, vmNameOrId);
        try
        {
            var snapshot = LibvirtNative.virDomainSnapshotLookupByName(domain, snapshotId, 0);
            if (snapshot == IntPtr.Zero)
                throw new InvalidOperationException($"Snapshot '{snapshotId}' not found for VM '{vmNameOrId}'");

            try
            {
                var ret = LibvirtNative.virDomainSnapshotRevert(snapshot, 0);
                if (ret < 0)
                    throw new InvalidOperationException($"Failed to revert to snapshot '{snapshotId}' for VM '{vmNameOrId}'");

                _logger.LogInformation("Reverted VM '{VmName}' to snapshot '{SnapshotId}'", vmNameOrId, snapshotId);
            }
            finally
            {
                LibvirtNative.virDomainSnapshotFree(snapshot);
            }
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task ConfigureVmAsync(string vmNameOrId, VmConfigurationSpec config)
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
            var domainEl = doc.Root!;

            if (config.CpuCount.HasValue)
                domainEl.Element("vcpu")!.Value = config.CpuCount.Value.ToString();

            if (config.MemoryMB.HasValue)
            {
                var memKiB = config.MemoryMB.Value * 1024;
                domainEl.Element("memory")!.Value = memKiB.ToString();
                domainEl.Element("currentMemory")!.Value = memKiB.ToString();
            }

            if (!string.IsNullOrEmpty(config.Notes))
                domainEl.SetElementValue("description", config.Notes);

            LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public async Task<BulkOperationResultDto> BulkStartAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StartVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkStopAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StopVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkShutdownAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, ShutdownVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkTerminateAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StopVmAsync);
    }

    public async Task<string> CloneVmAsync(string sourceVmName, string newName)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(sourceVmName);
        if (domain == IntPtr.Zero) throw new InvalidOperationException($"VM '{sourceVmName}' not found");

        try
        {
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero) throw new InvalidOperationException("Failed to get domain XML");
            var xml = Marshal.PtrToStringUTF8(xmlPtr)!;
            LibvirtNative.virFree(xmlPtr);

            var doc = XDocument.Parse(xml);
            var domainEl = doc.Root!;

            // Update name and remove UUID (will be auto-generated)
            domainEl.Element("name")!.Value = newName;
            domainEl.Element("uuid")?.Remove();

            // Define the cloned domain
            var clonedDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (clonedDomain == IntPtr.Zero) throw new InvalidOperationException("Failed to define cloned domain");
            LibvirtNative.virDomainFree(clonedDomain);

            return newName;
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }
    }

    public Task<ConsoleInfoDto?> GetConsoleInfoAsync(string vmNameOrId)
    {
        return Task.FromResult<ConsoleInfoDto?>(new ConsoleInfoDto
        {
            Type = "vnc",
            Host = "0.0.0.0",
            Port = -1, // Auto-assigned
            ExtendedProperties = new Dictionary<string, object>
            {
                ["vmName"] = vmNameOrId,
                ["method"] = "noVNC"
            }
        });
    }

    private static async Task<BulkOperationResultDto> ExecuteBulkOperationAsync(string[] vmNames, Func<string, Task> operation)
    {
        var tasks = vmNames.Select(async name =>
        {
            try
            {
                await operation(name);
                return new BulkOperationItemResult { VmName = name, Success = true };
            }
            catch (Exception ex)
            {
                return new BulkOperationItemResult { VmName = name, Success = false, ErrorMessage = ex.Message };
            }
        });

        var results = (await Task.WhenAll(tasks)).ToList();

        return new BulkOperationResultDto
        {
            TotalCount = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };
    }
}
