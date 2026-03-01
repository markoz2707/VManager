using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface INetworkProvider
{
    Task<List<VirtualNetworkInfo>> ListNetworksAsync();
    Task<VirtualNetworkInfo?> GetNetworkAsync(string networkId);
    Task<string> CreateNetworkAsync(CreateNetworkSpec spec);
    Task DeleteNetworkAsync(string networkId);

    Task<List<VmNetworkAdapterDto>> GetVmNetworkAdaptersAsync(string vmNameOrId);
    Task AttachNetworkAdapterAsync(string vmNameOrId, string networkId);
    Task DetachNetworkAdapterAsync(string vmNameOrId, string adapterId);

    Task<List<PhysicalAdapterDto>> ListPhysicalAdaptersAsync();
}
