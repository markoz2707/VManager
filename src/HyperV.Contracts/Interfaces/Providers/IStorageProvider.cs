using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IStorageProvider
{
    Task CreateDiskAsync(CreateDiskSpec spec);
    Task DeleteDiskAsync(string diskPath);
    Task ResizeDiskAsync(string diskPath, long newSizeBytes);
    Task<DiskInfoDto?> GetDiskInfoAsync(string diskPath);
    Task ConvertDiskAsync(string sourcePath, string destPath, string format);

    Task AttachDiskToVmAsync(string vmNameOrId, string diskPath);
    Task DetachDiskFromVmAsync(string vmNameOrId, string diskPath);
    Task<List<DiskInfoDto>> GetVmDisksAsync(string vmNameOrId);

    Task<List<StorageDeviceDto>> GetVmStorageDevicesAsync(string vmNameOrId);
    Task<List<StorageControllerDto>> GetVmStorageControllersAsync(string vmNameOrId);
    Task AddStorageDeviceToVmAsync(string vmNameOrId, AddStorageDeviceSpec spec);
    Task RemoveStorageDeviceFromVmAsync(string vmNameOrId, string deviceId);

    Task<List<StoragePoolDto>> ListStoragePoolsAsync();
    Task<StorageCapacityDto> GetStorageCapacityAsync();
}
