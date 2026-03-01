using global::HyperV.Contracts.Interfaces;
using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models;
using global::HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVStorageProvider : IStorageProvider
{
    private readonly IStorageService _storageService;
    private readonly ILogger<HyperVStorageProvider> _logger;

    public HyperVStorageProvider(IStorageService storageService, ILogger<HyperVStorageProvider> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public async Task CreateDiskAsync(CreateDiskSpec spec)
    {
        var format = spec.Format?.ToLowerInvariant() ?? "vhdx";
        var request = new CreateVhdRequest
        {
            Path = spec.Path,
            MaxInternalSize = (ulong)spec.SizeBytes,
            Format = format == "vhd" ? "VHD" : "VHDX",
            Type = spec.Dynamic ? "Dynamic" : "Fixed"
        };
        await _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null!);
    }

    public async Task DeleteDiskAsync(string diskPath)
    {
        // WMI doesn't have a direct "delete VHD" - we detach and delete the file
        if (System.IO.File.Exists(diskPath))
        {
            System.IO.File.Delete(diskPath);
        }
        await Task.CompletedTask;
    }

    public async Task ResizeDiskAsync(string diskPath, long newSizeBytes)
    {
        var request = new ResizeVhdRequest
        {
            Path = diskPath,
            MaxInternalSize = (ulong)newSizeBytes
        };
        await _storageService.ResizeVirtualHardDiskAsync(request, CancellationToken.None, null!);
    }

    public async Task<DiskInfoDto?> GetDiskInfoAsync(string diskPath)
    {
        try
        {
            var metadata = await _storageService.GetVhdMetadataAsync(diskPath);
            return new DiskInfoDto
            {
                Path = metadata.Path ?? diskPath,
                Format = metadata.Format ?? "vhdx",
                ExtendedProperties = new Dictionary<string, object>
                {
                    ["uniqueId"] = metadata.UniqueId.ToString(),
                    ["isAttached"] = metadata.IsAttached
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get disk info for {DiskPath}", diskPath);
            return null;
        }
    }

    public async Task ConvertDiskAsync(string sourcePath, string destPath, string format)
    {
        await _storageService.ConvertVhdAsync(sourcePath, destPath, format, CancellationToken.None, null!);
    }

    public async Task AttachDiskToVmAsync(string vmNameOrId, string diskPath)
    {
        await _storageService.AttachVirtualHardDiskAsync(vmNameOrId, diskPath, CancellationToken.None, null!);
    }

    public async Task DetachDiskFromVmAsync(string vmNameOrId, string diskPath)
    {
        await _storageService.DetachVirtualHardDiskAsync(vmNameOrId, diskPath, CancellationToken.None, null!);
    }

    public async Task<List<DiskInfoDto>> GetVmDisksAsync(string vmNameOrId)
    {
        var devices = await _storageService.GetVmStorageDevicesAsync(vmNameOrId);
        var result = new List<DiskInfoDto>();
        foreach (var device in devices)
        {
            if (!string.IsNullOrEmpty(device.Path))
            {
                result.Add(new DiskInfoDto
                {
                    Path = device.Path,
                    Format = device.Path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase) ? "vhd" : "vhdx"
                });
            }
        }
        return result;
    }

    public async Task<List<StorageDeviceDto>> GetVmStorageDevicesAsync(string vmNameOrId)
    {
        var devices = await _storageService.GetVmStorageDevicesAsync(vmNameOrId);
        return devices.Select(d => new StorageDeviceDto
        {
            Id = d.DeviceId ?? "",
            Name = d.Name ?? "",
            Type = d.DeviceType ?? "",
            Path = d.Path,
            ControllerType = d.ControllerType,
            ExtendedProperties = new Dictionary<string, object>
            {
                ["isReadOnly"] = d.IsReadOnly,
                ["operationalStatus"] = d.OperationalStatus ?? ""
            }
        }).ToList();
    }

    public async Task<List<StorageControllerDto>> GetVmStorageControllersAsync(string vmNameOrId)
    {
        var controllers = await _storageService.GetVmStorageControllersAsync(vmNameOrId);
        return controllers.Select(c => new StorageControllerDto
        {
            Id = c.ControllerId ?? "",
            Name = c.Name ?? "",
            Type = c.ControllerType ?? "",
            MaxDevices = c.MaxDevices,
            ExtendedProperties = new Dictionary<string, object>
            {
                ["attachedDevices"] = c.AttachedDevices,
                ["supportsHotPlug"] = c.SupportsHotPlug,
                ["availableLocations"] = c.AvailableLocations
            }
        }).ToList();
    }

    public async Task AddStorageDeviceToVmAsync(string vmNameOrId, AddStorageDeviceSpec spec)
    {
        var request = new AddStorageDeviceRequest
        {
            DeviceType = spec.Type,
            Path = spec.Path ?? "",
            ControllerLocation = spec.ControllerLocation
        };
        await _storageService.AddStorageDeviceToVmAsync(vmNameOrId, request);
    }

    public async Task RemoveStorageDeviceFromVmAsync(string vmNameOrId, string deviceId)
    {
        await _storageService.RemoveStorageDeviceFromVmAsync(vmNameOrId, deviceId);
    }

    public async Task<List<StoragePoolDto>> ListStoragePoolsAsync()
    {
        var devices = await _storageService.ListStorageDevicesAsync();
        return devices.Select(d => new StoragePoolDto
        {
            Name = d.Name ?? "",
            Path = d.Name ?? "",
            TotalBytes = d.Size,
            FreeBytes = d.FreeSpace,
            Type = d.Filesystem ?? "NTFS"
        }).ToList();
    }

    public async Task<StorageCapacityDto> GetStorageCapacityAsync()
    {
        var pools = await ListStoragePoolsAsync();
        return new StorageCapacityDto
        {
            TotalBytes = pools.Sum(p => p.TotalBytes),
            FreeBytes = pools.Sum(p => p.FreeBytes),
            UsedBytes = pools.Sum(p => p.TotalBytes - p.FreeBytes),
            Pools = pools
        };
    }
}
