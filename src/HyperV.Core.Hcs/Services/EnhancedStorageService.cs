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
                throw new ArgumentException("VHD
