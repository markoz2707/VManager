using System.Threading;
using System.Threading.Tasks;
using HyperV.Contracts.Models;

namespace HyperV.Contracts.Interfaces
{
    /// <summary>
    /// Interface for Hyper-V Image Management Service operations
    /// Manages virtual media (.vhd, .vhdx, .iso, .vfd files) for virtual machines
    /// </summary>
    public interface IImageManagementService
    {
        Task CompactVirtualHardDiskAsync(CompactVhdRequest request, CancellationToken cancellationToken = default);
        Task MergeVirtualHardDiskAsync(MergeDiskRequest request, CancellationToken cancellationToken = default);
        Task<VirtualHardDiskSettingData> GetVirtualHardDiskSettingDataAsync(string path, CancellationToken cancellationToken = default);
        Task<VirtualHardDiskState> GetVirtualHardDiskStateAsync(string path, CancellationToken cancellationToken = default);
        // Basic VHD Operations (already implemented in IStorageService)
        Task<string> ConvertVirtualHardDiskAsync(ConvertVhdRequest request, CancellationToken cancellationToken = default);
        Task<string> ConvertVirtualHardDiskToVHDSetAsync(ConvertToVhdSetRequest request, CancellationToken cancellationToken = default);
        Task DeleteVHDSnapshotAsync(string vhdSetPath, string snapshotId, CancellationToken cancellationToken = default);
        Task<MountedStorageImageResponse> FindMountedStorageImageInstanceAsync(string imagePath, CancellationToken cancellationToken = default);
        Task<VhdSetInformationResponse> GetVHDSetInformationAsync(string vhdSetPath, CancellationToken cancellationToken = default);
        Task<VhdSnapshotInformationResponse> GetVHDSnapshotInformationAsync(string vhdSetPath, string snapshotId, CancellationToken cancellationToken = default);
        Task<VirtualDiskChangesResponse> GetVirtualDiskChangesAsync(GetVirtualDiskChangesRequest request, CancellationToken cancellationToken = default);
        Task OptimizeVHDSetAsync(string vhdSetPath, CancellationToken cancellationToken = default);
        Task SetVHDSnapshotInformationAsync(SetVhdSnapshotInfoRequest request, CancellationToken cancellationToken = default);
        Task<bool> ValidatePersistentReservationSupportAsync(string path, CancellationToken cancellationToken = default);
    }
}
