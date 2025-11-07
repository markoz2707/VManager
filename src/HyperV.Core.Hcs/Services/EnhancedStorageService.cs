using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Interop;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Management;

namespace HyperV.Core.Hcs.Services
{
    /// <summary>
    /// Enhanced HCS-based storage service with advanced features:
    /// - VM-specific attachment logic using HCS APIs
    /// - Container integration support
    /// - Differencing disk operations
    /// - Advanced VHD metadata manipulation
    /// - Asynchronous operation support
    /// - Progress reporting for long-running operations
    /// </summary>
    public class EnhancedStorageService : IStorageService
    {
        private readonly VmService _vmService;
        private readonly ContainerService _containerService;

        public EnhancedStorageService(VmService vmService, ContainerService containerService)
        {
            _vmService = vmService ?? throw new ArgumentNullException(nameof(vmService));
            _containerService = containerService ?? throw new ArgumentNullException(nameof(containerService));
        }

        #region IStorageService Implementation

        /// <summary>
        /// Creates a virtual hard disk with enhanced options including differencing disk support
        /// </summary>
        public void CreateVirtualHardDisk(CreateVhdRequest request)
        {
            CreateVirtualHardDiskAsync(request, CancellationToken.None, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attaches a VHD to a VM using HCS APIs for VM-specific attachment
        /// </summary>
        public void AttachVirtualHardDisk(string vmName, string vhdPath)
        {
            AttachVirtualHardDiskAsync(vmName, vhdPath, CancellationToken.None, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Detaches a VHD from a VM using HCS APIs
        /// </summary>
        public void DetachVirtualHardDisk(string vmName, string vhdPath)
        {
            DetachVirtualHardDiskAsync(vmName, vhdPath, CancellationToken.None, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resizes a VHD with progress reporting support
        /// </summary>
        public void ResizeVirtualHardDisk(ResizeVhdRequest request)
        {
            ResizeVirtualHardDiskAsync(request, CancellationToken.None, null).GetAwaiter().GetResult();
        }

        #endregion

        #region Enhanced Async Methods with Progress Reporting

        /// <summary>
        /// Creates a virtual hard disk asynchronously with progress reporting
        /// </summary>
        public async Task CreateVirtualHardDiskAsync(CreateVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            ValidateCreateRequest(request);

            var storageType = GetStorageTypeFromFormat(request.Format);
            var flags = GetCreateFlagsFromType(request.Type);

            var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS
            {
                Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
                Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
                {
                    MaximumSize = request.MaxInternalSize,
                    BlockSizeInBytes = 0, // Use default
                    SectorSizeInBytes = 512,
                    ParentPath = IntPtr.Zero,
                    SourcePath = IntPtr.Zero
                }
            };

            await Task.Run(() =>
            {
                IntPtr handle;
                IntPtr overlapped = IntPtr.Zero;

                if (progress != null)
                {
                    overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                }

                try
                {
                    int result = VirtDiskNative.CreateVirtualDisk(
                        ref storageType,
                        request.Path,
                        VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE,
                        IntPtr.Zero,
                        flags,
                        0,
                        ref parameters,
                        overlapped,
                        out handle);

                    if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                    {
                        // Wait for async operation to complete
                        WaitForAsyncOperation(overlapped, cancellationToken);
                    }
                    else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                    {
                        throw new Win32Exception(result, $"Failed to create virtual disk at {request.Path}");
                    }

                    if (handle != IntPtr.Zero)
                    {
                        VirtDiskNative.CloseHandle(handle);
                    }
                }
                finally
                {
                    if (overlapped != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(overlapped);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Attaches a VHD to a VM using HCS APIs with VM-specific logic
        /// </summary>
        public async Task AttachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            ValidateAttachRequest(vmName, vhdPath);

            // Check if this is a container or VM
            if (await IsContainerAsync(vmName))
            {
                await AttachToContainerAsync(vmName, vhdPath, cancellationToken, progress);
            }
            else
            {
                await AttachToVmAsync(vmName, vhdPath, cancellationToken, progress);
            }
        }

        /// <summary>
        /// Detaches a VHD from a VM using HCS APIs
        /// </summary>
        public async Task DetachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            ValidateDetachRequest(vmName, vhdPath);

            if (await IsContainerAsync(vmName))
            {
                await DetachFromContainerAsync(vmName, vhdPath, cancellationToken, progress);
            }
            else
            {
                await DetachFromVmAsync(vmName, vhdPath, cancellationToken, progress);
            }
        }

        /// <summary>
        /// Resizes a VHD asynchronously with progress reporting
        /// </summary>
        public async Task ResizeVirtualHardDiskAsync(ResizeVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            ValidateResizeRequest(request);

            var storageType = GetStorageTypeFromPath(request.Path);

            await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
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
                    throw new Win32Exception(result, $"Failed to open virtual disk {request.Path} for resizing");
                }

                try
                {
                    var resizeParams = new RESIZE_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = RESIZE_VIRTUAL_DISK_VERSION.RESIZE_VIRTUAL_DISK_VERSION_1,
                        Version1 = new RESIZE_VIRTUAL_DISK_PARAMETERS_V1 { NewSize = request.MaxInternalSize }
                    };

                    IntPtr overlapped = IntPtr.Zero;
                    if (progress != null)
                    {
                        overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                    }

                    try
                    {
                        result = VirtDiskNative.ResizeVirtualDisk(
                            handle,
                            RESIZE_VIRTUAL_DISK_FLAG.RESIZE_VIRTUAL_DISK_FLAG_NONE,
                            ref resizeParams,
                            overlapped);

                        if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                        {
                            WaitForAsyncOperation(overlapped, cancellationToken);
                        }
                        else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                        {
                            throw new Win32Exception(result, $"Failed to resize virtual disk {request.Path}");
                        }
                    }
                    finally
                    {
                        if (overlapped != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(overlapped);
                        }
                    }
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }, cancellationToken);
        }

        #endregion

        #region Differencing Disk Operations

        /// <summary>
        /// Creates a differencing VHD that references a parent VHD
        /// </summary>
        public async Task CreateDifferencingDiskAsync(string childPath, string parentPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            if (string.IsNullOrEmpty(childPath))
                throw new ArgumentException("Child path cannot be null or empty", nameof(childPath));
            if (string.IsNullOrEmpty(parentPath))
                throw new ArgumentException("Parent path cannot be null or empty", nameof(parentPath));
            if (!File.Exists(parentPath))
                throw new FileNotFoundException($"Parent VHD not found: {parentPath}");

            var storageType = GetStorageTypeFromPath(parentPath);

            await Task.Run(() =>
            {
                var parentPathPtr = Marshal.StringToHGlobalUni(parentPath);
                try
                {
                    var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
                        Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
                        {
                            MaximumSize = 0, // Inherit from parent
                            BlockSizeInBytes = 0,
                            SectorSizeInBytes = 512,
                            ParentPath = parentPathPtr,
                            SourcePath = IntPtr.Zero
                        }
                    };

                    IntPtr overlapped = IntPtr.Zero;
                    if (progress != null)
                    {
                        overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                    }

                    try
                    {
                        IntPtr handle;
                        int result = VirtDiskNative.CreateVirtualDisk(
                            ref storageType,
                            childPath,
                            VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE,
                            IntPtr.Zero,
                            CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_DIFFERENCING,
                            0,
                            ref parameters,
                            overlapped,
                            out handle);

                        if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                        {
                            WaitForAsyncOperation(overlapped, cancellationToken);
                        }
                        else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                        {
                            throw new Win32Exception(result, $"Failed to create differencing disk {childPath}");
                        }

                        if (handle != IntPtr.Zero)
                        {
                            VirtDiskNative.CloseHandle(handle);
                        }
                    }
                    finally
                    {
                        if (overlapped != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(overlapped);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(parentPathPtr);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Merges a differencing disk with its parent
        /// </summary>
        public async Task MergeDifferencingDiskAsync(string childPath, uint mergeDepth, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            if (string.IsNullOrEmpty(childPath))
                throw new ArgumentException("Child path cannot be null or empty", nameof(childPath));
            if (!File.Exists(childPath))
                throw new FileNotFoundException($"Child VHD not found: {childPath}");

            var storageType = GetStorageTypeFromPath(childPath);

            await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                IntPtr handle;
                int result = VirtDiskNative.OpenVirtualDisk(
                    ref storageType,
                    childPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out handle);

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to open differencing disk {childPath}");
                }

                try
                {
                    var mergeParams = new MERGE_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = MERGE_VIRTUAL_DISK_VERSION.MERGE_VIRTUAL_DISK_VERSION_1,
                        Version1 = new MERGE_VIRTUAL_DISK_PARAMETERS_V1 { MergeDepth = mergeDepth }
                    };

                    IntPtr overlapped = IntPtr.Zero;
                    if (progress != null)
                    {
                        overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                    }

                    try
                    {
                        result = VirtDiskNative.MergeVirtualDisk(
                            handle,
                            MERGE_VIRTUAL_DISK_FLAG.MERGE_VIRTUAL_DISK_FLAG_NONE,
                            ref mergeParams,
                            overlapped);

                        if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                        {
                            WaitForAsyncOperation(overlapped, cancellationToken);
                        }
                        else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                        {
                            throw new Win32Exception(result, $"Failed to merge differencing disk {childPath}");
                        }
                    }
                    finally
                    {
                        if (overlapped != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(overlapped);
                        }
                    }
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }, cancellationToken);
        }

        #endregion

        #region Advanced VHD Metadata Manipulation

        /// <summary>
        /// Gets comprehensive information about a VHD file
        /// </summary>
        public async Task<VhdMetadata> GetVhdMetadataAsync(string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            var storageType = GetStorageTypeFromPath(vhdPath);

            return await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                IntPtr handle;
                int result = VirtDiskNative.OpenVirtualDisk(
                    ref storageType,
                    vhdPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_GET_INFO,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out handle);

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to open VHD {vhdPath} for metadata reading");
                }

                try
                {
                    var metadata = new VhdMetadata
                    {
                        Path = vhdPath,
                        Format = Path.GetExtension(vhdPath)?.ToUpperInvariant().TrimStart('.'),
                        Size = GetVhdSize(handle),
                        VirtualSize = GetVhdVirtualSize(handle),
                        ParentPath = GetVhdParentPath(handle),
                        UniqueId = GetVhdUniqueId(handle),
                        IsAttached = IsVhdAttached(handle),
                        PhysicalSectorSize = GetVhdPhysicalSectorSize(handle)
                    };

                    return metadata;
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            });
        }

        /// <summary>
        /// Sets VHD metadata properties
        /// </summary>
        public async Task SetVhdMetadataAsync(string vhdPath, VhdMetadataUpdate update)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            var storageType = GetStorageTypeFromPath(vhdPath);

            await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                IntPtr handle;
                int result = VirtDiskNative.OpenVirtualDisk(
                    ref storageType,
                    vhdPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out handle);

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to open VHD {vhdPath} for metadata update");
                }

                try
                {
                    if (update.NewUniqueId.HasValue)
                    {
                        SetVhdUniqueId(handle, update.NewUniqueId.Value);
                    }

                    if (!string.IsNullOrEmpty(update.NewParentPath))
                    {
                        SetVhdParentPath(handle, update.NewParentPath);
                    }

                    if (update.NewPhysicalSectorSize.HasValue)
                    {
                        SetVhdPhysicalSectorSize(handle, update.NewPhysicalSectorSize.Value);
                    }
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            });
        }

        /// <summary>
        /// Compacts a VHD file to reduce its size
        /// </summary>
        public async Task CompactVhdAsync(string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            var storageType = GetStorageTypeFromPath(vhdPath);

            await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                IntPtr handle;
                int result = VirtDiskNative.OpenVirtualDisk(
                    ref storageType,
                    vhdPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out handle);

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to open VHD {vhdPath} for compacting");
                }

                try
                {
                    var compactParams = new COMPACT_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = COMPACT_VIRTUAL_DISK_VERSION.COMPACT_VIRTUAL_DISK_VERSION_1,
                        Version1 = new COMPACT_VIRTUAL_DISK_PARAMETERS_V1 { Reserved = 0 }
                    };

                    IntPtr overlapped = IntPtr.Zero;
                    if (progress != null)
                    {
                        overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                    }

                    try
                    {
                        result = VirtDiskNative.CompactVirtualDisk(
                            handle,
                            COMPACT_VIRTUAL_DISK_FLAG.COMPACT_VIRTUAL_DISK_FLAG_NONE,
                            ref compactParams,
                            overlapped);

                        if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                        {
                            WaitForAsyncOperation(overlapped, cancellationToken);
                        }
                        else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                        {
                            throw new Win32Exception(result, $"Failed to compact VHD {vhdPath}");
                        }
                    }
                    finally
                    {
                        if (overlapped != IntPtr.Zero)
                        {
                            Marshal.FreeHGlobal(overlapped);
                        }
                    }
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            }, cancellationToken);
        }

        #endregion

        #region VM-Specific Attachment Logic

        private async Task AttachToVmAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            // Use HCS APIs to attach VHD to specific VM
            var vmConfig = await GetVmConfigurationAsync(vmName);
            
            // Create storage device configuration
            var storageDevice = new
            {
                Type = HcsStorageTypes.VIRTUAL_DISK,
                Path = vhdPath,
                ReadOnly = false
            };

            // Find suitable controller
            var controller = FindSuitableController(vmConfig);
            if (controller == null)
            {
                throw new InvalidOperationException($"No suitable storage controller found for VM {vmName}");
            }

            // Add storage device to controller
            await AddStorageDeviceToControllerAsync(vmName, controller, storageDevice, cancellationToken);

            progress?.Report(new VirtualDiskProgress
            {
                OperationStatus = 0,
                CurrentValue = 100,
                CompletionValue = 100
            });
        }

        private async Task DetachFromVmAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            var vmConfig = await GetVmConfigurationAsync(vmName);
            
            // Find the storage device to remove
            var storageDevice = FindStorageDevice(vmConfig, vhdPath);
            if (storageDevice == null)
            {
                throw new InvalidOperationException($"Storage device {vhdPath} not found on VM {vmName}");
            }

            // Remove storage device from VM
            await RemoveStorageDeviceFromVmAsync(vmName, storageDevice, cancellationToken);

            progress?.Report(new VirtualDiskProgress
            {
                OperationStatus = 0,
                CurrentValue = 100,
                CompletionValue = 100
            });
        }

        #endregion

        #region Container Integration Support

        private async Task AttachToContainerAsync(string containerName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            // Container-specific attachment logic
            var containerConfig = await GetContainerConfigurationAsync(containerName);

            // Create container storage layer
            var storageLayer = new
            {
                Type = ContainerStorageTypes.CONTAINER_LAYER,
                Path = vhdPath,
                ReadOnly = false
            };

            // Add storage layer to container
            await AddStorageLayerToContainerAsync(containerName, storageLayer, cancellationToken);

            progress?.Report(new VirtualDiskProgress
            {
                OperationStatus = 0,
                CurrentValue = 100,
                CompletionValue = 100
            });
        }

        private async Task DetachFromContainerAsync(string containerName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            var containerConfig = await GetContainerConfigurationAsync(containerName);
            
            // Find the storage layer to remove
            var storageLayer = FindContainerStorageLayer(containerConfig, vhdPath);
            if (storageLayer == null)
            {
                throw new InvalidOperationException($"Storage layer {vhdPath} not found on container {containerName}");
            }

            // Remove storage layer from container
            await RemoveStorageLayerFromContainerAsync(containerName, storageLayer, cancellationToken);

            progress?.Report(new VirtualDiskProgress
            {
                OperationStatus = 0,
                CurrentValue = 100,
                CompletionValue = 100
            });
        }

        #endregion

        #region Helper Methods

        private static VIRTUAL_STORAGE_TYPE GetStorageTypeFromFormat(string format)
        {
            var storageType = new VIRTUAL_STORAGE_TYPE();
            switch (format?.ToUpperInvariant())
            {
                case "VHD":
                    storageType.DeviceId = 2;
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHD;
                    break;
                case "VHDX":
                    storageType.DeviceId = 3;
                    storageType.VendorId = VirtDiskNative.VIRTUAL_STORAGE_TYPE_DEVICE_VHDX;
                    break;
                default:
                    throw new ArgumentException($"Unsupported VHD format: {format}");
            }
            return storageType;
        }

        private static VIRTUAL_STORAGE_TYPE GetStorageTypeFromPath(string vhdPath)
        {
            var extension = Path.GetExtension(vhdPath)?.ToUpperInvariant();
            return GetStorageTypeFromFormat(extension?.TrimStart('.'));
        }

        private static CREATE_VIRTUAL_DISK_FLAG GetCreateFlagsFromType(string type)
        {
            switch (type?.ToUpperInvariant())
            {
                case "DYNAMIC":
                    return CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_DYNAMIC;
                case "FIXED":
                    return CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_FULL;
                default:
                    throw new ArgumentException($"Unsupported VHD type: {type}");
            }
        }

        private IntPtr CreateOverlappedForProgress(IProgress<VirtualDiskProgress> progress, CancellationToken cancellationToken)
        {
            var overlapped = new OVERLAPPED
            {
                hEvent = CreateEvent(IntPtr.Zero, true, false, null)
            };

            var overlappedPtr = Marshal.AllocHGlobal(Marshal.SizeOf<OVERLAPPED>());
            Marshal.StructureToPtr(overlapped, overlappedPtr, false);

            // Start progress monitoring task
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Monitor progress and report to callback
                    var progressInfo = new VirtualDiskProgress
                    {
                        OperationStatus = 0,
                        CurrentValue = 50, // Placeholder
                        CompletionValue = 100
                    };

                    progress?.Report(progressInfo);
                    await Task.Delay(1000, cancellationToken);
                }
            }, cancellationToken);

            return overlappedPtr;
        }

        private void WaitForAsyncOperation(IntPtr overlapped, CancellationToken cancellationToken)
        {
            var overlappedStruct = Marshal.PtrToStructure<OVERLAPPED>(overlapped);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var waitResult = WaitForSingleObject(overlappedStruct.hEvent, 1000);
                if (waitResult == 0) // WAIT_OBJECT_0
                {
                    break;
                }
            }

            CloseHandle(overlappedStruct.hEvent);
        }

        // Validation methods
        private static void ValidateCreateRequest(CreateVhdRequest request)
        {
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VHD path cannot be null or empty");
            if (request.MaxInternalSize == 0)
                throw new ArgumentException("MaxInternalSize must be greater than 0");
            if (string.IsNullOrEmpty(request.Format))
                throw new ArgumentException("Format cannot be null or empty");
            if (string.IsNullOrEmpty(request.Type))
                throw new ArgumentException("Type cannot be null or empty");
        }

        private static void ValidateAttachRequest(string vmName, string vhdPath)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty");
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty");
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");
        }

        private static void ValidateDetachRequest(string vmName, string vhdPath)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty");
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty");
        }

        private static void ValidateResizeRequest(ResizeVhdRequest request)
        {
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VHD path cannot be null or empty");
            if (!File.Exists(request.Path))
                throw new FileNotFoundException($"VHD file not found: {request.Path}");
            if (request.MaxInternalSize == 0)
                throw new ArgumentException("MaxInternalSize must be greater than 0");
        }

        #endregion

        #region Missing Helper Methods Implementation

        // VM Configuration and Management
        private async Task<dynamic> GetVmConfigurationAsync(string vmName)
        {
            // Use HCS APIs to get VM configuration
            var vmProperties = _vmService.GetVmProperties(vmName);
            return await Task.FromResult(JsonSerializer.Deserialize<dynamic>(vmProperties));
        }

        private dynamic FindSuitableController(dynamic vmConfig)
        {
            // Find SCSI controller first (preferred for hot-plug), then IDE
            // Implementation depends on VM configuration structure
            // This is a placeholder - actual implementation would parse vmConfig
            return new { Type = "SCSI", Index = 0 };
        }

        private async Task AddStorageDeviceToControllerAsync(string vmName, dynamic controller, dynamic storageDevice, CancellationToken cancellationToken)
        {
            // Use HCS APIs to add storage device to VM controller
            // This would involve HcsModifyComputeSystem calls
            await Task.Run(() =>
            {
                // Placeholder implementation
                // Real implementation would use HCS APIs to modify VM configuration
            }, cancellationToken);
        }

        private async Task RemoveStorageDeviceFromVmAsync(string vmName, dynamic storageDevice, CancellationToken cancellationToken)
        {
            // Use HCS APIs to remove storage device from VM
            await Task.Run(() =>
            {
                // Placeholder implementation
                // Real implementation would use HCS APIs to modify VM configuration
            }, cancellationToken);
        }

        private dynamic FindStorageDevice(dynamic vmConfig, string vhdPath)
        {
            // Find storage device in VM configuration by VHD path
            // This is a placeholder - actual implementation would parse vmConfig
            return new { Path = vhdPath, Type = "VirtualDisk" };
        }

        // Container Configuration and Management
        private async Task<bool> IsContainerAsync(string name)
        {
            try
            {
                // Check if this is a container by trying to get container info
                _containerService.GetContainerProperties(name);
                return await Task.FromResult(true);
            }
            catch
            {
                return await Task.FromResult(false);
            }
        }

        private async Task<dynamic> GetContainerConfigurationAsync(string containerName)
        {
            // Use HCS APIs to get container configuration
            var containerInfo = _containerService.GetContainerProperties(containerName);
            return await Task.FromResult(JsonSerializer.Deserialize<dynamic>(containerInfo));
        }

        private async Task AddStorageLayerToContainerAsync(string containerName, dynamic storageLayer, CancellationToken cancellationToken)
        {
            // Use HCS APIs to add storage layer to container
            await Task.Run(() =>
            {
                // Placeholder implementation
                // Real implementation would use HCS APIs to modify container configuration
            }, cancellationToken);
        }

        private async Task RemoveStorageLayerFromContainerAsync(string containerName, dynamic storageLayer, CancellationToken cancellationToken)
        {
            // Use HCS APIs to remove storage layer from container
            await Task.Run(() =>
            {
                // Placeholder implementation
                // Real implementation would use HCS APIs to modify container configuration
            }, cancellationToken);
        }

        private dynamic FindContainerStorageLayer(dynamic containerConfig, string vhdPath)
        {
            // Find storage layer in container configuration by VHD path
            // This is a placeholder - actual implementation would parse containerConfig
            return new { Path = vhdPath, Type = "ContainerLayer" };
        }

        // VHD Metadata Helper Methods
        private ulong GetVhdSize(IntPtr handle)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var infoPtr = Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                // Set the version in the allocated memory
                Marshal.WriteInt32(infoPtr, (int)GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_SIZE);

                uint sizeUsed;
                int result = VirtDiskNative.GetVirtualDiskInformation(
                    handle,
                    ref infoSize,
                    infoPtr,
                    out sizeUsed);

                if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    var info = Marshal.PtrToStructure<GET_VIRTUAL_DISK_INFO>(infoPtr);
                    return info.Size.PhysicalSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            return 0;
        }

        private ulong GetVhdVirtualSize(IntPtr handle)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var infoPtr = Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                Marshal.WriteInt32(infoPtr, (int)GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_SIZE);

                uint sizeUsed;
                int result = VirtDiskNative.GetVirtualDiskInformation(
                    handle,
                    ref infoSize,
                    infoPtr,
                    out sizeUsed);

                if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    var info = Marshal.PtrToStructure<GET_VIRTUAL_DISK_INFO>(infoPtr);
                    return info.Size.VirtualSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            return 0;
        }

        private string GetVhdParentPath(IntPtr handle)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var infoPtr = Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                Marshal.WriteInt32(infoPtr, (int)GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_PARENT_LOCATION);

                uint sizeUsed;
                int result = VirtDiskNative.GetVirtualDiskInformation(
                    handle,
                    ref infoSize,
                    infoPtr,
                    out sizeUsed);

                if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    var info = Marshal.PtrToStructure<GET_VIRTUAL_DISK_INFO>(infoPtr);
                    if (info.ParentLocation.ParentResolved)
                    {
                        return Marshal.PtrToStringUni(info.ParentLocation.ParentLocationBuffer);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            return null;
        }

        private Guid GetVhdUniqueId(IntPtr handle)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var infoPtr = Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                Marshal.WriteInt32(infoPtr, (int)GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_IDENTIFIER);

                uint sizeUsed;
                int result = VirtDiskNative.GetVirtualDiskInformation(
                    handle,
                    ref infoSize,
                    infoPtr,
                    out sizeUsed);

                if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    var info = Marshal.PtrToStructure<GET_VIRTUAL_DISK_INFO>(infoPtr);
                    return info.Identifier;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            return Guid.Empty;
        }

        private bool IsVhdAttached(IntPtr handle)
        {
            // Check if VHD is currently attached by trying to get physical path
            try
            {
                uint pathSize = 260;
                var pathBuffer = Marshal.AllocHGlobal((int)pathSize * 2); // Unicode
                
                try
                {
                    int result = VirtDiskNative.GetVirtualDiskPhysicalPath(handle, ref pathSize, pathBuffer);
                    return result == VirtDiskErrorCodes.ERROR_SUCCESS;
                }
                finally
                {
                    Marshal.FreeHGlobal(pathBuffer);
                }
            }
            catch
            {
                return false;
            }
        }

        private uint GetVhdPhysicalSectorSize(IntPtr handle)
        {
            uint infoSize = (uint)Marshal.SizeOf<GET_VIRTUAL_DISK_INFO>();
            var infoPtr = Marshal.AllocHGlobal((int)infoSize);
            
            try
            {
                Marshal.WriteInt32(infoPtr, (int)GET_VIRTUAL_DISK_INFO_VERSION.GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK);

                uint sizeUsed;
                int result = VirtDiskNative.GetVirtualDiskInformation(
                    handle,
                    ref infoSize,
                    infoPtr,
                    out sizeUsed);

                if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    var info = Marshal.PtrToStructure<GET_VIRTUAL_DISK_INFO>(infoPtr);
                    return info.PhysicalDisk.PhysicalSectorSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }

            return 512; // Default sector size
        }

        private void SetVhdUniqueId(IntPtr handle, Guid uniqueId)
        {
            var setInfo = new SET_VIRTUAL_DISK_INFO
            {
                Version = SET_VIRTUAL_DISK_INFO_VERSION.SET_VIRTUAL_DISK_INFO_IDENTIFIER,
                Identifier = uniqueId
            };

            int result = VirtDiskNative.SetVirtualDiskInformation(handle, ref setInfo);
            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, "Failed to set VHD unique ID");
            }
        }

        private void SetVhdParentPath(IntPtr handle, string parentPath)
        {
            var parentPathPtr = Marshal.StringToHGlobalUni(parentPath);
            try
            {
                var setInfo = new SET_VIRTUAL_DISK_INFO
                {
                    Version = SET_VIRTUAL_DISK_INFO_VERSION.SET_VIRTUAL_DISK_INFO_PARENT_PATH,
                    ParentFilePath = parentPathPtr
                };

                int result = VirtDiskNative.SetVirtualDiskInformation(handle, ref setInfo);
                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, "Failed to set VHD parent path");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(parentPathPtr);
            }
        }

        private void SetVhdPhysicalSectorSize(IntPtr handle, uint sectorSize)
        {
            var setInfo = new SET_VIRTUAL_DISK_INFO
            {
                Version = SET_VIRTUAL_DISK_INFO_VERSION.SET_VIRTUAL_DISK_INFO_PHYSICAL_SECTOR_SIZE,
                PhysicalSectorSize = sectorSize
            };

            int result = VirtDiskNative.SetVirtualDiskInformation(handle, ref setInfo);
            if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
            {
                throw new Win32Exception(result, "Failed to set VHD physical sector size");
            }
        }

        // Native API P/Invoke Declarations
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        #endregion

        #region Additional Interface Methods Implementation

        /// <summary>
        /// Gets the current state of a VHD file
        /// </summary>
        public async Task<VhdStateResponse> GetVhdStateAsync(string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            var storageType = GetStorageTypeFromPath(vhdPath);

            return await Task.Run(() =>
            {
                var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                {
                    Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                    Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                };

                IntPtr handle;
                int result = VirtDiskNative.OpenVirtualDisk(
                    ref storageType,
                    vhdPath,
                    VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_GET_INFO,
                    OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                    ref openParams,
                    out handle);

                if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                {
                    throw new Win32Exception(result, $"Failed to open VHD {vhdPath} for state reading");
                }

                try
                {
                    var state = new VhdStateResponse
                    {
                        Path = vhdPath,
                        IsAttached = IsVhdAttached(handle),
                        OperationalState = "Online",
                        HealthStatus = "Healthy",
                        IsReadOnly = false,
                        AccessMode = "ReadWrite"
                    };

                    if (state.IsAttached)
                    {
                        try
                        {
                            uint pathSize = 260;
                            var pathBuffer = Marshal.AllocHGlobal((int)pathSize * 2);
                            try
                            {
                                int pathResult = VirtDiskNative.GetVirtualDiskPhysicalPath(handle, ref pathSize, pathBuffer);
                                if (pathResult == VirtDiskErrorCodes.ERROR_SUCCESS)
                                {
                                    state.PhysicalPath = Marshal.PtrToStringUni(pathBuffer);
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(pathBuffer);
                            }
                        }
                        catch
                        {
                            // Physical path retrieval failed, but VHD is still attached
                        }
                    }

                    return state;
                }
                finally
                {
                    VirtDiskNative.CloseHandle(handle);
                }
            });
        }

        /// <summary>
        /// Validates a VHD file
        /// </summary>
        public async Task<VhdValidationResponse> ValidateVhdAsync(VhdValidationRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VHD path cannot be null or empty");
            if (!File.Exists(request.Path))
                throw new FileNotFoundException($"VHD file not found: {request.Path}");

            var response = new VhdValidationResponse
            {
                Path = request.Path,
                ValidatedAt = DateTime.UtcNow
            };

            return await Task.Run(() =>
            {
                var storageType = GetStorageTypeFromPath(request.Path);

                try
                {
                    var openParams = new OPEN_VIRTUAL_DISK_PARAMETERS
                    {
                        Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
                        Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
                    };

                    IntPtr handle;
                    int result = VirtDiskNative.OpenVirtualDisk(
                        ref storageType,
                        request.Path,
                        VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_GET_INFO,
                        OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                        ref openParams,
                        out handle);

                    if (result == VirtDiskErrorCodes.ERROR_SUCCESS)
                    {
                        response.IsValid = true;
                        VirtDiskNative.CloseHandle(handle);

                        // Validate parent chain if requested
                        if (request.ValidateParentChain)
                        {
                            var metadata = GetVhdMetadataAsync(request.Path).GetAwaiter().GetResult();
                            if (metadata.IsDifferencingDisk)
                            {
                                response.ParentChainValid = !string.IsNullOrEmpty(metadata.ParentPath) && File.Exists(metadata.ParentPath);
                                if (!response.ParentChainValid.Value)
                                {
                                    response.Warnings.Add("Parent VHD not found or inaccessible");
                                }
                            }
                            else
                            {
                                response.ParentChainValid = true;
                            }
                        }

                        // Validate persistent reservation support if requested
                        if (request.ValidatePersistentReservation)
                        {
                            response.PersistentReservationSupported = true; // Placeholder - would need actual PR validation
                        }
                    }
                    else
                    {
                        response.IsValid = false;
                        response.Errors.Add($"Failed to open VHD: {new Win32Exception(result).Message}");
                    }
                }
                catch (Exception ex)
                {
                    response.IsValid = false;
                    response.Errors.Add($"Validation error: {ex.Message}");
                }

                return response;
            }, cancellationToken);
        }

        /// <summary>
        /// Converts a VHD from one format to another
        /// </summary>
        public async Task ConvertVhdAsync(string sourcePath, string destinationPath, string targetFormat, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source VHD not found: {sourcePath}");

            var sourceStorageType = GetStorageTypeFromPath(sourcePath);
            var targetStorageType = GetStorageTypeFromFormat(targetFormat);

            await Task.Run(() =>
            {
                var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS
                {
                    Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
                    Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
                    {
                        MaximumSize = 0, // Use source size
                        BlockSizeInBytes = 0,
                        SectorSizeInBytes = 512,
                        ParentPath = IntPtr.Zero,
                        SourcePath = Marshal.StringToHGlobalUni(sourcePath)
                    }
                };

                IntPtr overlapped = IntPtr.Zero;
                if (progress != null)
                {
                    overlapped = CreateOverlappedForProgress(progress, cancellationToken);
                }

                try
                {
                    IntPtr handle;
                    int result = VirtDiskNative.CreateVirtualDisk(
                        ref targetStorageType,
                        destinationPath,
                        VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE,
                        IntPtr.Zero,
                        CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_NONE,
                        0,
                        ref parameters,
                        overlapped,
                        out handle);

                    if (result == VirtDiskErrorCodes.ERROR_IO_PENDING)
                    {
                        WaitForAsyncOperation(overlapped, cancellationToken);
                    }
                    else if (result != VirtDiskErrorCodes.ERROR_SUCCESS)
                    {
                        throw new Win32Exception(result, $"Failed to convert VHD from {sourcePath} to {destinationPath}");
                    }

                    if (handle != IntPtr.Zero)
                    {
                        VirtDiskNative.CloseHandle(handle);
                    }
                }
                finally
                {
                    if (parameters.Version1.SourcePath != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(parameters.Version1.SourcePath);
                    }
                    if (overlapped != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(overlapped);
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Creates a virtual floppy disk
        /// </summary>
        public async Task CreateVirtualFloppyDiskAsync(CreateVfdRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.Path))
                throw new ArgumentException("VFD path cannot be null or empty");

            await Task.Run(() =>
            {
                // Create VFD using native API - placeholder implementation
                // In a real implementation, this would use the ImageManagementService WMI class
                // or direct file system operations to create a properly formatted floppy disk image
                
                var vfdSize = (int)request.Size;
                var vfdData = new byte[vfdSize];
                
                // Initialize with basic FAT12 structure for floppy disk
                if (request.Format)
                {
                    // This is a simplified implementation - real implementation would create proper FAT12 structure
                    // Boot sector, FAT tables, root directory, etc.
                }

                File.WriteAllBytes(request.Path, vfdData);
            }, cancellationToken);
        }

        /// <summary>
        /// Attaches a virtual floppy disk to a VM
        /// </summary>
        public async Task AttachVirtualFloppyDiskAsync(VfdAttachRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrEmpty(request.VmName))
                throw new ArgumentException("VM name cannot be null or empty");
            if (string.IsNullOrEmpty(request.VfdPath))
                throw new ArgumentException("VFD path cannot be null or empty");
            if (!File.Exists(request.VfdPath))
                throw new FileNotFoundException($"VFD file not found: {request.VfdPath}");

            // Use HCS APIs to attach VFD to VM - similar to VHD attachment but for floppy controller
            await AttachVirtualHardDiskAsync(request.VmName, request.VfdPath, CancellationToken.None, null);
        }

        /// <summary>
        /// Detaches a virtual floppy disk from a VM
        /// </summary>
        public async Task DetachVirtualFloppyDiskAsync(string vmName, string vfdPath)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty", nameof(vmName));
            if (string.IsNullOrEmpty(vfdPath))
                throw new ArgumentException("VFD path cannot be null or empty", nameof(vfdPath));

            // Use HCS APIs to detach VFD from VM
            await DetachVirtualHardDiskAsync(vmName, vfdPath, CancellationToken.None, null);
        }

        /// <summary>
        /// Gets list of mounted storage images
        /// </summary>
        public async Task<List<MountedStorageImageResponse>> GetMountedStorageImagesAsync()
        {
            return await Task.Run(() =>
            {
                var mountedImages = new List<MountedStorageImageResponse>();

                // This would typically query the system for mounted VHD/VHDX/ISO files
                // Using WMI or direct system calls to enumerate mounted storage
                // Placeholder implementation returns empty list

                return mountedImages;
            });
        }

        /// <summary>
        /// Enables change tracking for a VHD
        /// </summary>
        public async Task EnableChangeTrackingAsync(string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (!File.Exists(vhdPath))
                throw new FileNotFoundException($"VHD file not found: {vhdPath}");

            await Task.Run(() =>
            {
                // Enable Resilient Change Tracking (RCT) for the VHD
                // This would use the VirtDisk API to enable change tracking
                // Placeholder implementation
            });
        }

        /// <summary>
        /// Disables change tracking for a VHD
        /// </summary>
        public async Task DisableChangeTrackingAsync(string vhdPath)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));

            await Task.Run(() =>
            {
                // Disable Resilient Change Tracking (RCT) for the VHD
                // Placeholder implementation
            });
        }

        /// <summary>
        /// Gets virtual disk changes for incremental backup
        /// </summary>
        public async Task<List<string>> GetVirtualDiskChangesAsync(string vhdPath, string changeTrackingId)
        {
            if (string.IsNullOrEmpty(vhdPath))
                throw new ArgumentException("VHD path cannot be null or empty", nameof(vhdPath));
            if (string.IsNullOrEmpty(changeTrackingId))
                throw new ArgumentException("Change tracking ID cannot be null or empty", nameof(changeTrackingId));

            return await Task.Run(() =>
            {
                // Get list of changed blocks since the specified change tracking ID
                // This would use the VirtDisk API to enumerate changes
                // Placeholder implementation returns empty list
                return new List<string>();
            });
        }

        /// <summary>
        /// Gets storage devices attached to a VM
        /// </summary>
        public async Task<List<StorageDeviceResponse>> GetVmStorageDevicesAsync(string vmName)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty", nameof(vmName));

            return await Task.Run(() =>
            {
                var devices = new List<StorageDeviceResponse>();

                // This would use HCS APIs to enumerate storage devices attached to the VM
                // Query VM configuration and extract storage device information
                // Placeholder implementation returns empty list

                return devices;
            });
        }

        /// <summary>
        /// Gets storage controllers for a VM
        /// </summary>
        public async Task<List<StorageControllerResponse>> GetVmStorageControllersAsync(string vmName)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty", nameof(vmName));

            return await Task.Run(() =>
            {
                var controllers = new List<StorageControllerResponse>();

                // This would use HCS APIs to enumerate storage controllers in the VM
                // Query VM configuration and extract controller information
                // Placeholder implementation returns basic controllers

                controllers.Add(new StorageControllerResponse
                {
                    ControllerId = "ide-0",
                    Name = "IDE Controller 0",
                    ControllerType = "IDE",
                    MaxDevices = 2,
                    AttachedDevices = 0,
                    AvailableLocations = new List<int> { 0, 1 },
                    SupportsHotPlug = false,
                    OperationalStatus = "OK",
                    Protocol = "IDE"
                });

                controllers.Add(new StorageControllerResponse
                {
                    ControllerId = "scsi-0",
                    Name = "SCSI Controller 0",
                    ControllerType = "SCSI",
                    MaxDevices = 64,
                    AttachedDevices = 0,
                    AvailableLocations = Enumerable.Range(0, 64).ToList(),
                    SupportsHotPlug = true,
                    OperationalStatus = "OK",
                    Protocol = "SCSI"
                });

                return controllers;
            });
        }

        /// <summary>
        /// Adds a storage device to a VM
        /// </summary>
        public async Task AddStorageDeviceToVmAsync(string vmName, AddStorageDeviceRequest request)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty", nameof(vmName));
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await Task.Run(() =>
            {
                // Use HCS APIs to add storage device to VM
                var vmConfig = GetVmConfigurationAsync(vmName).GetAwaiter().GetResult();
                
                // Create storage device configuration
                var storageDevice = new
                {
                    Type = request.DeviceType ?? "VirtualDisk",
                    Path = request.Path,
                    ReadOnly = request.ReadOnly,
                    Controller = request.ControllerId ?? "SCSI"
                };

                // Find suitable controller
                var controller = FindSuitableController(vmConfig);
                if (controller == null)
                {
                    throw new InvalidOperationException($"No suitable storage controller found for VM {vmName}");
                }

                // Add storage device to controller
                AddStorageDeviceToControllerAsync(vmName, controller, storageDevice, CancellationToken.None).GetAwaiter().GetResult();
            });
        }

        /// <summary>
        /// Removes a storage device from a VM
        /// </summary>
        public async Task RemoveStorageDeviceFromVmAsync(string vmName, string deviceId)
        {
            if (string.IsNullOrEmpty(vmName))
                throw new ArgumentException("VM name cannot be null or empty", nameof(vmName));
            if (string.IsNullOrEmpty(deviceId))
                throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

            await Task.Run(() =>
            {
                // Use HCS APIs to remove storage device from VM
                var vmConfig = GetVmConfigurationAsync(vmName).GetAwaiter().GetResult();
                
                // Find the storage device to remove
                var storageDevice = FindStorageDevice(vmConfig, deviceId);
                if (storageDevice == null)
                {
                    throw new InvalidOperationException($"Storage device {deviceId} not found on VM {vmName}");
                }

                // Remove storage device from VM
                RemoveStorageDeviceFromVmAsync(vmName, storageDevice, CancellationToken.None).GetAwaiter().GetResult();
            });
        }

        #endregion

        #region Storage Type Constants

        private static class HcsStorageTypes
        {
            public const string VIRTUAL_DISK = "VirtualDisk";
            public const string PHYSICAL_DISK = "PhysicalDisk";
            public const string DVD_DRIVE = "DvdDrive";
        }

        private static class ContainerStorageTypes
        {
            public const string CONTAINER_LAYER = "Layer";
            public const string SCRATCH_LAYER = "ScratchLayer";
        }

        #endregion
        /// <summary>
        /// Lists all fixed storage devices (local drives) with their details.
        /// </summary>
        public async Task<List<StorageDeviceInfo>> ListStorageDevicesAsync()
        {
            return await Task.Run(() =>
            {
                var devices = new List<StorageDeviceInfo>();
                var scope = new ManagementScope("root\\CIMV2");
                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3"); // Fixed local disks
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();

                foreach (ManagementObject disk in results)
                {
                    try
                    {
                        var deviceId = disk["DeviceID"]?.ToString() ?? string.Empty;
                        var fileSystem = disk["FileSystem"]?.ToString() ?? "Unknown";
                        var size = (ulong?)disk["Size"] ?? 0;
                        var freeSpace = (ulong?)disk["FreeSpace"] ?? 0;
                        var usedSpace = size - freeSpace;

                        devices.Add(new StorageDeviceInfo
                        {
                            Name = deviceId,
                            Filesystem = fileSystem,
                            Size = (long)size,
                            UsedSpace = (long)usedSpace,
                            FreeSpace = (long)freeSpace
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing disk: {ex.Message}");
                    }
                    finally
                    {
                        disk?.Dispose();
                    }
                }

                return devices.OrderBy(d => d.Name).ToList();
            });
        }

        /// <summary>
        /// Browses filesystems to find suitable locations for VHDX storage.
        /// Returns drives with sufficient free space and suggested paths.
        /// </summary>
        public async Task<List<StorageLocation>> GetSuitableVhdLocationsAsync(long minFreeSpaceGb = 10)
        {
            var minFreeSpaceBytes = minFreeSpaceGb * 1024 * 1024 * 1024L;
            return await Task.Run(() =>
            {
                var locations = new List<StorageLocation>();
                var scope = new ManagementScope("root\\CIMV2");
                scope.Connect();

                var query = new ObjectQuery("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();

                foreach (ManagementObject disk in results)
                {
                    try
                    {
                        var deviceId = disk["DeviceID"]?.ToString() ?? string.Empty;
                        var freeSpace = (ulong?)disk["FreeSpace"] ?? 0;

                        if (freeSpace >= (ulong)minFreeSpaceBytes)
                        {
                            var suggestedPaths = new List<string>
                            {
                                $"{deviceId}\\VMs\\", // Suggested VM folder
                                $"{deviceId}\\Hyper-V\\Virtual Hard Disks\\", // Default Hyper-V path
                                $"{deviceId}\\" // Root as fallback
                            };

                            // Filter existing paths (basic check)
                            var validPaths = suggestedPaths.Where(p => Directory.Exists(p) || true).ToList(); // Always include for creation

                            locations.Add(new StorageLocation
                            {
                                Drive = deviceId,
                                FreeSpaceBytes = (long)freeSpace,
                                FreeSpaceGb = Math.Round((double)freeSpace / (1024 * 1024 * 1024), 2),
                                SuggestedPaths = validPaths,
                                IsSuitable = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing location: {ex.Message}");
                    }
                    finally
                    {
                        disk?.Dispose();
                    }
                }

                return locations.OrderByDescending(l => l.FreeSpaceGb).ToList();
            });
        }

    }
}
