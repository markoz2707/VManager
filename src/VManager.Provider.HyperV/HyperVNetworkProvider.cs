using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using HyperV.Core.Hcn.Services;
using HyperV.Core.Wmi.Services;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVNetworkProvider : INetworkProvider
{
    private readonly NetworkService _hcnService;
    private readonly WmiNetworkService _wmiService;
    private readonly ILogger<HyperVNetworkProvider> _logger;

    public HyperVNetworkProvider(
        NetworkService hcnService,
        WmiNetworkService wmiService,
        ILogger<HyperVNetworkProvider> logger)
    {
        _hcnService = hcnService;
        _wmiService = wmiService;
        _logger = logger;
    }

    public Task<List<VirtualNetworkInfo>> ListNetworksAsync()
    {
        var result = new List<VirtualNetworkInfo>();

        // WMI virtual switches
        try
        {
            foreach (var sw in _wmiService.ListVirtualSwitchesSummary())
            {
                result.Add(new VirtualNetworkInfo
                {
                    Id = sw.Id,
                    Name = sw.Name,
                    Type = sw.Type,
                    IsExternal = sw.Type == "External",
                    ExtendedProperties = new Dictionary<string, object>
                    {
                        ["backend"] = "WMI",
                        ["notes"] = sw.Notes ?? ""
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list WMI virtual switches");
        }

        // HCN networks
        try
        {
            var hcnJson = _hcnService.ListNetworks();
            if (!string.IsNullOrWhiteSpace(hcnJson))
            {
                var doc = JsonDocument.Parse(hcnJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var net in doc.RootElement.EnumerateArray())
                    {
                        var id = net.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? "" : "";
                        var name = net.TryGetProperty("Name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                        // Don't add if already present from WMI (same name)
                        if (!result.Any(r => r.Name == name))
                        {
                            result.Add(new VirtualNetworkInfo
                            {
                                Id = id,
                                Name = name,
                                Type = "NAT",
                                IsExternal = false,
                                ExtendedProperties = new Dictionary<string, object>
                                {
                                    ["backend"] = "HCN"
                                }
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list HCN networks");
        }

        return Task.FromResult(result);
    }

    public async Task<VirtualNetworkInfo?> GetNetworkAsync(string networkId)
    {
        var networks = await ListNetworksAsync();
        return networks.FirstOrDefault(n =>
            n.Id.Equals(networkId, StringComparison.OrdinalIgnoreCase) ||
            n.Name.Equals(networkId, StringComparison.OrdinalIgnoreCase));
    }

    public Task<string> CreateNetworkAsync(CreateNetworkSpec spec)
    {
        var type = spec.Type?.ToLowerInvariant() ?? "internal";

        if (type == "nat")
        {
            var prefix = spec.ExtendedProperties?.TryGetValue("addressPrefix", out var ap) == true
                ? ap?.ToString() ?? "192.168.100.0/24"
                : "192.168.100.0/24";
            var id = _hcnService.CreateNATNetwork(spec.Name, prefix);
            return Task.FromResult(id.ToString());
        }

        var wmiType = type switch
        {
            "external" => WmiNetworkService.WmiSwitchType.External,
            "private" => WmiNetworkService.WmiSwitchType.Private,
            _ => WmiNetworkService.WmiSwitchType.Internal,
        };

        var notes = spec.ExtendedProperties?.TryGetValue("notes", out var n) == true ? n?.ToString() : null;
        var allowMgmt = spec.ExtendedProperties?.TryGetValue("allowManagementOS", out var amo) == true && amo is true;

        var switchId = _wmiService.CreateVirtualSwitch(spec.Name, wmiType, notes, spec.PhysicalAdapterId, allowMgmt);
        return Task.FromResult(switchId.ToString());
    }

    public Task DeleteNetworkAsync(string networkId)
    {
        // Try as HCN GUID first
        if (Guid.TryParse(networkId, out var guid))
        {
            try
            {
                _hcnService.DeleteNetwork(guid);
                return Task.CompletedTask;
            }
            catch
            {
                // Not an HCN network, try WMI
            }

            try
            {
                _wmiService.DeleteVirtualSwitch(guid);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete network {NetworkId}", networkId);
                throw;
            }
        }

        throw new ArgumentException($"Invalid network ID: {networkId}");
    }

    public Task<List<VmNetworkAdapterDto>> GetVmNetworkAdaptersAsync(string vmNameOrId)
    {
        // WMI doesn't have a direct "list VM adapters" method, but we can derive from switch connections
        // Return empty for now - controllers that need this will use IStorageService.GetVmStorageDevicesAsync pattern
        return Task.FromResult(new List<VmNetworkAdapterDto>());
    }

    public Task AttachNetworkAdapterAsync(string vmNameOrId, string networkId)
    {
        _wmiService.ConnectVmToSwitch(vmNameOrId, networkId);
        return Task.CompletedTask;
    }

    public Task DetachNetworkAdapterAsync(string vmNameOrId, string adapterId)
    {
        _wmiService.DisconnectVmFromSwitch(vmNameOrId, adapterId);
        return Task.CompletedTask;
    }

    public Task<List<PhysicalAdapterDto>> ListPhysicalAdaptersAsync()
    {
        var result = new List<PhysicalAdapterDto>();
        try
        {
            foreach (var adapter in _wmiService.ListPhysicalAdapters())
            {
                result.Add(new PhysicalAdapterDto
                {
                    Id = adapter.Guid ?? adapter.Name,
                    Name = adapter.Name,
                    Status = "Up"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list physical adapters");
        }

        return Task.FromResult(result);
    }
}
