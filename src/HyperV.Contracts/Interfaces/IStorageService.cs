using HyperV.Contracts.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HyperV.Contracts.Interfaces
{
    public interface IStorageService
    {
        // Basic synchronous operations (existing)
        void CreateVirtualHardDisk(CreateVhdRequest request);
        void AttachVirtualHardDisk(string vmName, string vhdPath);
        void DetachVirtualHardDisk(string vmName, string vhdPath);
        void ResizeVirtualHardDisk(ResizeVhdRequest request);

        // Advanced asynchronous operations with progress reporting
        Task CreateVirtualHardDiskAsync(CreateVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);
        Task AttachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);
        Task DetachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);
        Task ResizeVirtualHardDiskAsync(ResizeVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);

        // Differencing disk operations
        Task CreateDifferencingDiskAsync(string childPath, string parentPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);
        Task MergeDifferencingDiskAsync(string childPath, uint mergeDepth, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);

        // VHD metadata operations
        Task<VhdMetadata> GetVhdMetadataAsync(string vhdPath);
        Task SetVhdMetadataAsync(string vhdPath, VhdMetadataUpdate update);

        // VHD optimization operations
        Task CompactVhdAsync(string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);

        // VHD state and validation operations
        Task<VhdStateResponse> GetVhdStateAsync(string vhdPath);
        Task<VhdValidationResponse> ValidateVhdAsync(VhdValidationRequest request, CancellationToken cancellationToken);
        Task ConvertVhdAsync(string sourcePath, string destinationPath, string targetFormat, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress);

        // Virtual Floppy Disk operations
        Task CreateVirtualFloppyDiskAsync(CreateVfdRequest request, CancellationToken cancellationToken);
        Task AttachVirtualFloppyDiskAsync(VfdAttachRequest request);
        Task DetachVirtualFloppyDiskAsync(string vmName, string vfdPath);

        // Mounted storage operations
        Task<List<MountedStorageImageResponse>> GetMountedStorageImagesAsync();

        // Change tracking operations
        Task EnableChangeTrackingAsync(string vhdPath);
        Task DisableChangeTrackingAsync(string vhdPath);
        Task<List<string>> GetVirtualDiskChangesAsync(string vhdPath, string changeTrackingId);

        // Storage device management operations
        Task<List<StorageDeviceResponse>> GetVmStorageDevicesAsync(string vmName);
        Task<List<StorageControllerResponse>> GetVmStorageControllersAsync(string vmName);
        Task AddStorageDeviceToVmAsync(string vmName, AddStorageDeviceRequest request);
        Task RemoveStorageDeviceFromVmAsync(string vmName, string deviceId);

        /// <summary>
        /// Lists all fixed storage devices with details.
        /// </summary>
        Task<List<StorageDeviceInfo>> ListStorageDevicesAsync();

        /// <summary>
        /// Gets suitable locations for VHDX storage based on free space.
        /// </summary>
        Task<List<StorageLocation>> GetSuitableVhdLocationsAsync(long minFreeSpaceGb = 10);
    }
}
