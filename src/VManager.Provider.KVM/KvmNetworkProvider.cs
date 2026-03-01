using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;

namespace VManager.Provider.KVM;

/// <summary>
/// INetworkProvider implementation backed by libvirt virtual networks.
/// </summary>
public class KvmNetworkProvider : INetworkProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmNetworkProvider> _logger;

    public KvmNetworkProvider(IOptions<KvmOptions> options, ILogger<KvmNetworkProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    private LibvirtConnection OpenConnection() => new(_options.LibvirtUri);

    public Task<List<VirtualNetworkInfo>> ListNetworksAsync()
    {
        var result = new List<VirtualNetworkInfo>();
        using var conn = OpenConnection();

        var count = LibvirtNative.virConnectListAllNetworks(conn.Handle, out var networksPtr, 0);
        if (count <= 0) return Task.FromResult(result);

        var netPtrs = new IntPtr[count];
        Marshal.Copy(networksPtr, netPtrs, 0, count);

        foreach (var netPtr in netPtrs)
        {
            try
            {
                var namePtr = LibvirtNative.virNetworkGetName(netPtr);
                var name = Marshal.PtrToStringUTF8(namePtr) ?? "unknown";

                var uuid = new byte[37];
                LibvirtNative.virNetworkGetUUIDString(netPtr, uuid);
                var uuidStr = Encoding.UTF8.GetString(uuid).TrimEnd('\0');

                var isActive = LibvirtNative.virNetworkIsActive(netPtr) == 1;

                // Determine network type from XML
                var networkType = "NAT";
                var isExternal = false;
                var xmlPtr = LibvirtNative.virNetworkGetXMLDesc(netPtr, 0);
                if (xmlPtr != IntPtr.Zero)
                {
                    var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
                    try
                    {
                        var doc = XDocument.Parse(xml);
                        var forwardElement = doc.Root?.Element("forward");
                        if (forwardElement != null)
                        {
                            var mode = forwardElement.Attribute("mode")?.Value ?? "nat";
                            networkType = mode switch
                            {
                                "nat" => "NAT",
                                "route" => "Routed",
                                "bridge" => "Bridge",
                                "open" => "Open",
                                "hostdev" => "Hostdev",
                                _ => mode
                            };
                            isExternal = mode == "bridge" || mode == "route" || mode == "open";
                        }
                        else
                        {
                            networkType = "Isolated";
                        }
                    }
                    catch
                    {
                        // XML parsing failure is non-fatal
                    }
                }

                result.Add(new VirtualNetworkInfo
                {
                    Id = uuidStr,
                    Name = name,
                    Type = networkType,
                    IsExternal = isExternal,
                    ExtendedProperties = new Dictionary<string, object>
                    {
                        ["active"] = isActive,
                        ["backend"] = "libvirt"
                    }
                });
            }
            finally
            {
                LibvirtNative.virNetworkFree(netPtr);
            }
        }

        Marshal.FreeHGlobal(networksPtr);
        return Task.FromResult(result);
    }

    public async Task<VirtualNetworkInfo?> GetNetworkAsync(string networkId)
    {
        var networks = await ListNetworksAsync();
        return networks.FirstOrDefault(n => n.Id == networkId || n.Name == networkId);
    }

    public Task<string> CreateNetworkAsync(CreateNetworkSpec spec)
    {
        using var conn = OpenConnection();

        var forwardMode = spec.Type?.ToLowerInvariant() switch
        {
            "nat" => "nat",
            "bridge" => "bridge",
            "routed" or "route" => "route",
            "isolated" => null,
            _ => "nat"
        };

        var sb = new StringBuilder();
        sb.AppendLine("<network>");
        sb.AppendLine($"  <name>{EscapeXml(spec.Name)}</name>");

        if (forwardMode != null)
        {
            sb.AppendLine($"  <forward mode='{forwardMode}'/>");
        }

        if (forwardMode == "bridge" && spec.PhysicalAdapterId != null)
        {
            sb.AppendLine($"  <bridge name='{EscapeXml(spec.PhysicalAdapterId)}'/>");
        }
        else
        {
            sb.AppendLine($"  <bridge name='virbr-{EscapeXml(spec.Name)}' stp='on' delay='0'/>");
        }

        sb.AppendLine("</network>");

        var network = conn.DefineNetwork(sb.ToString());
        if (network == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to define network '{spec.Name}'");

        // Start the network immediately
        LibvirtNative.virNetworkCreate(network);
        LibvirtNative.virNetworkFree(network);

        _logger.LogInformation("Created libvirt network '{NetworkName}' with mode '{Mode}'", spec.Name, forwardMode ?? "isolated");
        return Task.FromResult(spec.Name);
    }

    public Task DeleteNetworkAsync(string networkId)
    {
        using var conn = OpenConnection();
        var network = LibvirtNative.virNetworkLookupByName(conn.Handle, networkId);
        if (network == IntPtr.Zero)
            throw new InvalidOperationException($"Network '{networkId}' not found");

        try
        {
            // Destroy first if active
            if (LibvirtNative.virNetworkIsActive(network) == 1)
            {
                LibvirtNative.virNetworkDestroy(network);
            }

            LibvirtNative.virNetworkUndefine(network);
            _logger.LogInformation("Deleted libvirt network '{NetworkId}'", networkId);
        }
        finally
        {
            LibvirtNative.virNetworkFree(network);
        }

        return Task.CompletedTask;
    }

    public Task<List<VmNetworkAdapterDto>> GetVmNetworkAdaptersAsync(string vmNameOrId)
    {
        var result = new List<VmNetworkAdapterDto>();
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

            int index = 0;
            foreach (var iface in devices.Elements("interface"))
            {
                var type = iface.Attribute("type")?.Value ?? "unknown";
                var macAddress = iface.Element("mac")?.Attribute("address")?.Value;
                var networkName = iface.Element("source")?.Attribute("network")?.Value
                               ?? iface.Element("source")?.Attribute("bridge")?.Value;
                var model = iface.Element("model")?.Attribute("type")?.Value;

                result.Add(new VmNetworkAdapterDto
                {
                    Id = $"nic-{index}",
                    Name = $"vnet{index}",
                    NetworkName = networkName,
                    MacAddress = macAddress,
                    ExtendedProperties = new Dictionary<string, object>
                    {
                        ["type"] = type,
                        ["model"] = model ?? "virtio"
                    }
                });
                index++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse network adapters for VM '{VmName}'", vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.FromResult(result);
    }

    public Task AttachNetworkAdapterAsync(string vmNameOrId, string networkId)
    {
        using var conn = OpenConnection();
        var domain = conn.LookupDomainByName(vmNameOrId);
        if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
        if (domain == IntPtr.Zero)
            throw new InvalidOperationException($"VM '{vmNameOrId}' not found");

        try
        {
            // Get current XML and add a new interface element
            var xmlPtr = LibvirtNative.virDomainGetXMLDesc(domain, 0);
            if (xmlPtr == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to get XML for VM '{vmNameOrId}'");

            var xml = Marshal.PtrToStringUTF8(xmlPtr) ?? string.Empty;
            var doc = XDocument.Parse(xml);
            var devices = doc.Root?.Element("devices");
            if (devices == null)
                throw new InvalidOperationException($"No devices section found in VM '{vmNameOrId}' definition");

            var newIface = new XElement("interface",
                new XAttribute("type", "network"),
                new XElement("source", new XAttribute("network", networkId)),
                new XElement("model", new XAttribute("type", "virtio")));
            devices.Add(newIface);

            // Redefine domain
            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' with new network adapter");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Attached network adapter for network '{NetworkId}' to VM '{VmName}'", networkId, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task DetachNetworkAdapterAsync(string vmNameOrId, string adapterId)
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
                throw new InvalidOperationException($"No devices section found in VM '{vmNameOrId}' definition");

            // Find and remove the interface by index (adapterId is "nic-{index}")
            var interfaces = devices.Elements("interface").ToList();
            if (adapterId.StartsWith("nic-") && int.TryParse(adapterId.Substring(4), out var index) && index < interfaces.Count)
            {
                interfaces[index].Remove();
            }
            else
            {
                throw new InvalidOperationException($"Network adapter '{adapterId}' not found on VM '{vmNameOrId}'");
            }

            var newDomain = LibvirtNative.virDomainDefineXML(conn.Handle, doc.ToString());
            if (newDomain == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to redefine domain '{vmNameOrId}' after detaching adapter");

            LibvirtNative.virDomainFree(newDomain);
            _logger.LogInformation("Detached network adapter '{AdapterId}' from VM '{VmName}'", adapterId, vmNameOrId);
        }
        finally
        {
            LibvirtNative.virDomainFree(domain);
        }

        return Task.CompletedTask;
    }

    public Task<List<PhysicalAdapterDto>> ListPhysicalAdaptersAsync()
    {
        // On Linux, physical adapters are enumerated via /sys/class/net rather than libvirt.
        // Return empty list as this is host-level and not managed by libvirt.
        _logger.LogDebug("ListPhysicalAdapters called - physical adapter enumeration is host-level, not libvirt-managed");
        return Task.FromResult(new List<PhysicalAdapterDto>());
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
