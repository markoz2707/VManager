using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Interop;

namespace HyperV.Core.Hcs.Services
{
    /// <summary>
    /// HCS-based storage service implementation using virtdisk.dll
    /// Provides direct VHD/VHDX manipulation without WMI overhead
    /// </summary>
    public class StorageService : IStorageService
    {
        /// <summary>
        /// Creates a virtual hard disk using the Windows Virtual Disk API (virtdisk.dll)
        /// This approach bypasses WMI and provides direct access to the underlying Windows API
        /// </summary>
        public void CreateVirtualHardDisk(CreateVhdRequest request)
        {
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(request.Path));

            if (request.MaxInternalSize == 0)
                throw new ArgumentException("MaxInternalSize must be greater than 0", nameof(request.MaxInternalSize));

            // Determine VHD format
            var storageType = new VIRTUAL_STORAGE_TYPE();
            switch (request.Format?.ToUpperInvariant())
            {
                case "VHD":
                    storageType.DeviceId = 2; // VHD_STORAGE_TYPE_DEVICE_VHD
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHD;
                    break;
                case "VHDX":
                    storageType.DeviceId = 3; // VHD_STORAGE_TYPE_DEVICE_VHDX
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHDX;
                    break;
                default:
                    throw new ArgumentException($"Unsupported VHD format: {request.Format}. Supported formats: VHD, VHDX", nameof(request.Format));
            }

            // Determine VHD type flags
            CREATE_VIRTUAL_DISK_FLAG flags;
            switch (request.Type?.ToUpperInvariant())
            {
                case "DYNAMIC":
                    flags = CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_DYNAMIC;
                    break;
                case "FIXED":
                    flags = CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_FULL;
                    break;
                default:
                    throw new ArgumentException($"Unsupported VHD type: {request.Type}. Supported types: Dynamic, Fixed", nameof(request.Type));
            }

            // Set up creation parameters
            var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS
            {
                Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
                Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
                {
                    MaximumSize = request.MaxInternalSize,
                    BlockSizeInBytes = 0, // Use default block size
                    SectorSizeInBytes = 512, // Standard sector size
                    ParentPath = IntPtr.Zero,
                    SourcePath = IntPtr.Zero
                }
            };

            // Create the virtual disk
            IntPtr handle;
            int result = VirtDiskNative.CreateVirtualDisk(
                ref storageType,
                request.Path,
                VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE,
                IntPtr.Zero, // No security descriptor
                flags,
                0, // No provider-specific flags
                ref parameters,
                IntPtr.Zero, // No overlapped
                out handle);

            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, $"Failed to create virtual disk at {request.Path}. Error code: {result}");
            }

            // Clean up handle
            if (handle != IntPtr.Zero)
            {
                VirtDiskNative.CloseHandle(handle);
            }
        }

        /// <summary>
        /// Attaches a VHD/VHDX file to the system using virtdisk.dll
        /// Note: This attaches the VHD as a disk to the host system, not to a specific VM
        /// For VM attachment, use the WMI-based StorageService instead
        /// </summary>
        public void AttachVirtualHardDisk(string vmName, string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));

            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            // Note: vmName parameter is ignored in this implementation as virtdisk.dll
            // attaches VHDs to the host system, not to specific VMs
            // For VM-specific attachment, use the WMI-based implementation

            // Determine storage type based on file extension
            var storageType = GetStorageTypeFromPath(vhdPath);

            // Open the virtual disk
            var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
            {
                Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1
                {
                    RWDepth = 1 // Read-write access depth
                }
            };

            IntPtr handle;
            int result = VirtDiskNative.OpenVirtualDisk(
                ref storageType,
                vhdPath,
                VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ATTACH_RW,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                ref openParams,
                out handle);

            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, $"Failed to open virtual disk {vhdPath}. Error code: {result}");
            }

            try
            {
                // Attach the virtual disk
                var attachParams = new ATTACH_VIRTUAL_DISK_PARAMETERS
                {
                    Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1,
                    Version1 = new ATTACH_VIRTUAL_DISK_PARAMETERS_V1
                    {
                        Reserved = 0
                    }
                };

                result = VirtDiskNative.AttachVirtualDisk(
                    handle,
                    IntPtr.Zero, // No security descriptor
                    ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME,
                    0, // No provider-specific flags
                    ref attachParams,
                    IntPtr.Zero); // No overlapped

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to attach virtual disk {vhdPath}. Error code: {result}");
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }
        }

        /// <summary>
        /// Detaches a VHD/VHDX file from the system using virtdisk.dll
        /// Note: This detaches the VHD from the host system, not from a specific VM
        /// For VM detachment, use the WMI-based StorageService instead
        /// </summary>
        public void DetachVirtualHardDisk(string vmName, string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));

            // Note: vmName parameter is ignored in this implementation as virtdisk.dll
            // detaches VHDs from the host system, not from specific VMs

            // Determine storage type based on file extension
            var storageType = GetStorageTypeFromPath(vhdPath);

            // Open the virtual disk
            var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
            {
                Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1
                {
                    RWDepth = 1
                }
            };

            IntPtr handle;
            int result = VirtDiskNative.OpenVirtualDisk(
                ref storageType,
                vhdPath,
                VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_DETACH,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                ref openParams,
                out handle);

            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, $"Failed to open virtual disk {vhdPath} for detachment. Error code: {result}");
            }

            try
            {
                // Detach the virtual disk
                result = VirtDiskNative.DetachVirtualDisk(
                    handle,
                    DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE,
                    0); // No provider-specific flags

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to detach virtual disk {vhdPath}. Error code: {result}");
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }
        }

        /// <summary>
        /// Resizes a VHD/VHDX file using virtdisk.dll
        /// This provides direct access to the Windows Virtual Disk API for resizing operations
        /// </summary>
        public void ResizeVirtualHardDisk(ResizeVhdRequest request)
        {
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(request.Path));

            if (!File.Exists(request.Path))
                throw new FileNotFoundException($"VHD file not found: {request.Path}");

            if (request.MaxInternalSize == 0)
                throw new ArgumentException("MaxInternalSize must be greater than 0", nameof(request.MaxInternalSize));

            // Determine storage type based on file extension
            var storageType = GetStorageTypeFromPath(request.Path);

            // Open the virtual disk
            var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
            {
                Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1
                {
                    RWDepth = 1
                }
            };

            IntPtr handle;
            int result = VirtDiskNative.OpenVirtualDisk(
                ref storageType,
                request.Path,
                VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                ref openParams,
                out handle);

            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, $"Failed to open virtual disk {request.Path} for resizing. Error code: {result}");
            }

            try
            {
                // Set up resize parameters
                var resizeParams = new RESIZE_VIRTUAL_DISK_PARAMETERS
                {
                    Version = RESIZE_VIRTUAL_DISK_VERSION.RESIZE_VIRTUAL_DISK_VERSION_1,
                    Version1 = new RESIZE_VIRTUAL_DISK_PARAMETERS_V1
                    {
                        NewSize = request.MaxInternalSize
                    }
                };

                // Resize the virtual disk
                result = VirtDiskNative.ResizeVirtualDisk(
                    handle,
                    RESIZE_VIRTUAL_DISK_FLAG.RESIZE_VIRTUAL_DISK_FLAG_NONE,
                    ref resizeParams,
                    IntPtr.Zero); // No overlapped

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to resize virtual disk {request.Path}. Error code: {result}");
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }
        }

        /// <summary>
        /// Determines the storage type based on the file path extension
        /// </summary>
        /// <param name="vhdPath">Path to the VHD/VHDX file</param>
        /// <returns>VIRTUAL_STORAGE_TYPE structure for the file format</returns>
        private static VIRTUAL_STORAGE_TYPE GetStorageTypeFromPath(string vhdPath)
        {
            var extension = Path.GetExtension(vhdPath)?.ToUpperInvariant();
            
            var storageType = new VIRTUAL_STORAGE_TYPE();
            
            switch (extension)
            {
                case ".VHD":
                    storageType.DeviceId = 2; // VHD_STORAGE_TYPE_DEVICE_VHD
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHD;
                    break;
                case ".VHDX":
                    storageType.DeviceId = 3; // VHD_STORAGE_TYPE_DEVICE_VHDX
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHDX;
                    break;
                default:
                    throw new ArgumentException($"Unsupported file extension: {extension}. Supported extensions: .vhd, .vhdx");
            }

            return storageType;
        }
    }
}
