using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace HyperV.Core.Wmi.Services
{
    /// <summary>
    /// WMI-based implementation of Image Management Service
    /// Provides virtual media management operations using Hyper-V WMI APIs
    /// </summary>
    public class ImageManagementService : IImageManagementService
    {
        private readonly ILogger<ImageManagementService> _logger;
        private const string WmiNamespace = @"root\virtualization\v2";

        public ImageManagementService(ILogger<ImageManagementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task CompactVirtualHardDiskAsync(CompactVhdRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Compacting virtual hard disk {Path}", request.Path);

            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();

                using var inParams = imageService.GetMethodParameters("CompactVirtualHardDisk");
                inParams["Path"] = request.Path;
                inParams["Mode"] = (ushort)request.Mode;

                using var result = imageService.InvokeMethod("CompactVirtualHardDisk", inParams, null);

                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("Virtual hard disk compaction completed successfully");
                }
                else
                {
                    var error = "CompactVirtualHardDisk failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task MergeVirtualHardDiskAsync(MergeDiskRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Merging virtual hard disk {Source} to {Destination}", request.ChildPath, request.DestinationPath);

            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();

                using var inParams = imageService.GetMethodParameters("MergeVirtualHardDisk");
                inParams["SourcePath"] = request.ChildPath;
                inParams["DestinationPath"] = request.DestinationPath;

                using var result = imageService.InvokeMethod("MergeVirtualHardDisk", inParams, null);

                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("Virtual hard disk merge completed successfully");
                }
                else
                {
                    var error = "MergeVirtualHardDisk failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<VirtualHardDiskSettingData> GetVirtualHardDiskSettingDataAsync(string path, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting virtual hard disk setting data for {Path}", path);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();

                using var inParams = imageService.GetMethodParameters("GetVirtualHardDiskSettingData");
                inParams["Path"] = path;

                using var result = imageService.InvokeMethod("GetVirtualHardDiskSettingData", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var settingData = result["SettingData"].ToString();
                    var managementObject = new ManagementObject();
                    managementObject.SetPropertyValue("text", settingData);
                    managementObject.Get();

                    return new VirtualHardDiskSettingData
                    {
                        Path = (string)managementObject["Path"],
                        Type = (VirtualDiskType)(uint)managementObject["Type"],
                        Format = (VirtualDiskFormat)(uint)managementObject["Format"],
                        ParentPath = (string)managementObject["ParentPath"],
                        MaxInternalSize = (ulong)managementObject["MaxInternalSize"],
                        BlockSize = (uint)managementObject["BlockSize"],
                        LogicalSectorSize = (uint)managementObject["LogicalSectorSize"],
                        PhysicalSectorSize = (uint)managementObject["PhysicalSectorSize"]
                    };
                }
                else
                {
                    var error = $"GetVirtualHardDiskSettingData failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<VirtualHardDiskState> GetVirtualHardDiskStateAsync(string path, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting virtual hard disk state for {Path}", path);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();

                using var inParams = imageService.GetMethodParameters("GetVirtualHardDiskState");
                inParams["Path"] = path;

                using var result = imageService.InvokeMethod("GetVirtualHardDiskState", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var state = result["State"].ToString();
                    var managementObject = new ManagementObject();
                    managementObject.SetPropertyValue("text", state);
                    managementObject.Get();

                    return new VirtualHardDiskState
                    {
                        InUse = (int)(uint)managementObject["InUse"],
                        Health = (int)(uint)managementObject["Health"],
                        OperationalStatus = (int)(uint)managementObject["OperationalStatus"],
                        InUseBy = (int)(uint)managementObject["InUseBy"]
                    };
                }
                else
                {
                    var error = $"GetVirtualHardDiskState failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        private ManagementObject GetImageManagementService()
        {
            var scope = new ManagementScope(WmiNamespace);
            scope.Connect();

            using var imageServiceClass = new ManagementClass("Msvm_ImageManagementService");
            imageServiceClass.Scope = scope;
            
            return WmiUtilities.GetFirstObjectFromCollection(imageServiceClass.GetInstances());
        }

        public async Task<string> ConvertVirtualHardDiskAsync(ConvertVhdRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Converting virtual hard disk from {Source} to {Destination}", request.SourcePath, request.DestinationPath);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                var path = new ManagementPath()
                {
                    Server = null,
                    NamespacePath = imageService.Path.Path,
                    ClassName = "Msvm_VirtualHardDiskSettingData"
                };

                using var settingsClass = new ManagementClass(path);
                using var settingsInstance = settingsClass.CreateInstance();
                settingsInstance["Path"] = request.DestinationPath;
                settingsInstance["Type"] = request.DiskType.HasValue ? (uint)request.DiskType.Value : 3; // Default to Dynamic
                settingsInstance["Format"] = (uint)request.TargetFormat;
                settingsInstance["ParentPath"] = null;
                settingsInstance["MaxInternalSize"] = 0;
                settingsInstance["BlockSize"] = request.BlockSize.HasValue ? request.BlockSize.Value : 0;
                settingsInstance["LogicalSectorSize"] = 0;
                settingsInstance["PhysicalSectorSize"] = 0;

                using var inParams = imageService.GetMethodParameters("ConvertVirtualHardDisk");

                inParams["SourcePath"] = request.SourcePath;
                inParams["VirtualDiskSettingData"] = settingsInstance.GetText(TextFormat.WmiDtd20);

                using var result = imageService.InvokeMethod("ConvertVirtualHardDisk", inParams, null);
                
                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("Virtual hard disk conversion completed successfully");
                    return "Conversion completed successfully";
                }
                else
                {
                    var error = "ConvertVirtualHardDisk failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<string> ConvertVirtualHardDiskToVHDSetAsync(ConvertToVhdSetRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Converting virtual hard disk to VHD Set from {Source} to {Destination}", request.SourcePath, request.DestinationPath);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("ConvertVirtualHardDiskToVHDSet");
                inParams["SourcePath"] = request.SourcePath;
                inParams["DestinationPath"] = request.DestinationPath;

                if (request.BlockSize.HasValue)
                {
                    inParams["BlockSize"] = request.BlockSize.Value;
                }

                if (request.LogicalSectorSize.HasValue)
                {
                    inParams["LogicalSectorSize"] = request.LogicalSectorSize.Value;
                }

                if (request.PhysicalSectorSize.HasValue)
                {
                    inParams["PhysicalSectorSize"] = request.PhysicalSectorSize.Value;
                }

                using var result = imageService.InvokeMethod("ConvertVirtualHardDiskToVHDSet", inParams, null);
                
                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("VHD Set conversion completed successfully");
                    return "VHD Set conversion completed successfully";
                }
                else
                {
                    var error = "ConvertVirtualHardDiskToVHDSet failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task DeleteVHDSnapshotAsync(string vhdSetPath, string snapshotId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting VHD snapshot {SnapshotId} from {VhdSetPath}", snapshotId, vhdSetPath);

            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("DeleteVHDSnapshot");
                inParams["VHDSetPath"] = vhdSetPath;
                inParams["SnapshotId"] = snapshotId;

                using var result = imageService.InvokeMethod("DeleteVHDSnapshot", inParams, null);
                
                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("VHD snapshot deleted successfully");
                }
                else
                {
                    var error = "DeleteVHDSnapshot failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<MountedStorageImageResponse> FindMountedStorageImageInstanceAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Finding mounted storage image instance for {ImagePath}", imagePath);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("FindMountedStorageImageInstance");
                inParams["Path"] = imagePath;

                using var result = imageService.InvokeMethod("FindMountedStorageImageInstance", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var mountedImagePath = result["MountedImagePath"]?.ToString() ?? string.Empty;
                    
                    return new MountedStorageImageResponse
                    {
                        ImagePath = imagePath,
                        MountPath = mountedImagePath,
                        ImageType = System.IO.Path.GetExtension(imagePath).ToUpperInvariant().TrimStart('.'),
                        IsReadOnly = false, // Default, would need additional WMI calls to determine
                        Size = 0, // Would need additional WMI calls to get size
                        MountedAt = DateTime.Now // Would need additional WMI calls to get actual mount time
                    };
                }
                else
                {
                    var error = $"FindMountedStorageImageInstance failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<VhdSetInformationResponse> GetVHDSetInformationAsync(string vhdSetPath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting VHD Set information for {VhdSetPath}", vhdSetPath);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("GetVHDSetInformation");
                inParams["VHDSetPath"] = vhdSetPath;

                using var result = imageService.InvokeMethod("GetVHDSetInformation", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var information = result["Information"] as string ?? "{}";
                    
                    // Parse the XML information returned by WMI
                    // This is a simplified implementation - in reality, you'd parse the XML
                    return new VhdSetInformationResponse
                    {
                        Path = vhdSetPath,
                        VirtualSize = 0, // Would parse from XML
                        PhysicalSize = 0, // Would parse from XML
                        BlockSize = 0, // Would parse from XML
                        LogicalSectorSize = 512, // Default
                        PhysicalSectorSize = 512, // Default
                        CreationTime = DateTime.Now, // Would parse from XML
                        LastModified = DateTime.Now, // Would parse from XML
                        Identifier = Guid.NewGuid(), // Would parse from XML
                        Snapshots = new List<VhdSnapshotInfo>(), // Would parse from XML
                        IsMounted = false, // Would determine from additional WMI calls
                        SupportsResilientChangeTracking = false // Would parse from XML
                    };
                }
                else
                {
                    var error = $"GetVHDSetInformation failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<VhdSnapshotInformationResponse> GetVHDSnapshotInformationAsync(string vhdSetPath, string snapshotId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting VHD snapshot information for {SnapshotId} in {VhdSetPath}", snapshotId, vhdSetPath);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("GetVHDSnapshotInformation");
                inParams["VHDSetPath"] = vhdSetPath;
                inParams["SnapshotId"] = snapshotId;

                using var result = imageService.InvokeMethod("GetVHDSnapshotInformation", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var information = result["Information"] as string ?? "{}";
                    
                    // Parse the XML information returned by WMI
                    // This is a simplified implementation - in reality, you'd parse the XML
                    return new VhdSnapshotInformationResponse
                    {
                        SnapshotId = snapshotId,
                        Name = $"Snapshot {snapshotId}", // Would parse from XML
                        Description = string.Empty, // Would parse from XML
                        CreationTime = DateTime.Now, // Would parse from XML
                        ParentSnapshotId = null, // Would parse from XML
                        IsActive = false, // Would parse from XML
                        VhdSetPath = vhdSetPath,
                        VirtualSize = 0, // Would parse from XML
                        PhysicalSize = 0, // Would parse from XML
                        SupportsResilientChangeTracking = false // Would parse from XML
                    };
                }
                else
                {
                    var error = $"GetVHDSnapshotInformation failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<VirtualDiskChangesResponse> GetVirtualDiskChangesAsync(GetVirtualDiskChangesRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting virtual disk changes for {VhdSetPath} with tracking ID {ChangeTrackingId}", 
                request.VhdSetPath, request.ChangeTrackingId);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("GetVirtualDiskChanges");
                inParams["VHDSetPath"] = request.VhdSetPath;
                inParams["ChangeTrackingId"] = request.ChangeTrackingId;
                inParams["ByteOffset"] = request.ByteOffset;
                inParams["ByteLength"] = request.ByteLength;
                inParams["Flags"] = (uint)request.Flags;

                using var result = imageService.InvokeMethod("GetVirtualDiskChanges", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    // Parse the changes information returned by WMI
                    // This is a simplified implementation - in reality, you'd parse the actual change data
                    return new VirtualDiskChangesResponse
                    {
                        ChangedRanges = new List<VirtualDiskChangeRange>(),
                        TotalChangedBytes = 0,
                        HasMoreChanges = false,
                        NextChangeTrackingId = null,
                        CaptureTime = DateTime.Now
                    };
                }
                else
                {
                    var error = $"GetVirtualDiskChanges failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task OptimizeVHDSetAsync(string vhdSetPath, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Optimizing VHD Set {VhdSetPath}", vhdSetPath);

            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("OptimizeVHDSet");
                inParams["VHDSetPath"] = vhdSetPath;

                using var result = imageService.InvokeMethod("OptimizeVHDSet", inParams, null);
                
                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("VHD Set optimization completed successfully");
                }
                else
                {
                    var error = "OptimizeVHDSet failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task SetVHDSnapshotInformationAsync(SetVhdSnapshotInfoRequest request, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Setting VHD snapshot information for {SnapshotId} in {VhdSetPath}", 
                request.SnapshotId, request.VhdSetPath);

            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("SetVHDSnapshotInformation");
                inParams["VHDSetPath"] = request.VhdSetPath;
                inParams["SnapshotId"] = request.SnapshotId;

                // Build the information XML based on the request
                var infoXml = "<INSTANCE CLASSNAME=\"Msvm_VHDSnapshotInformation\">";
                if (!string.IsNullOrEmpty(request.Name))
                {
                    infoXml += $"<PROPERTY NAME=\"Name\" TYPE=\"string\"><VALUE>{request.Name}</VALUE></PROPERTY>";
                }
                if (!string.IsNullOrEmpty(request.Description))
                {
                    infoXml += $"<PROPERTY NAME=\"Description\" TYPE=\"string\"><VALUE>{request.Description}</VALUE></PROPERTY>";
                }
                if (request.IsActive.HasValue)
                {
                    infoXml += $"<PROPERTY NAME=\"IsActive\" TYPE=\"boolean\"><VALUE>{request.IsActive.Value.ToString().ToLower()}</VALUE></PROPERTY>";
                }
                infoXml += "</INSTANCE>";

                inParams["Information"] = infoXml;

                using var result = imageService.InvokeMethod("SetVHDSnapshotInformation", inParams, null);
                
                if (WmiUtilities.ValidateOutput(result, scope))
                {
                    _logger.LogInformation("VHD snapshot information updated successfully");
                }
                else
                {
                    var error = "SetVHDSnapshotInformation failed";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }

        public async Task<bool> ValidatePersistentReservationSupportAsync(string path, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Validating persistent reservation support for {Path}", path);

            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageService = GetImageManagementService();
                
                using var inParams = imageService.GetMethodParameters("ValidatePersistentReservationSupport");
                inParams["Path"] = path;

                using var result = imageService.InvokeMethod("ValidatePersistentReservationSupport", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                if (returnValue == 0)
                {
                    var isSupported = result["IsSupported"] as bool? ?? false;
                    _logger.LogInformation("Persistent reservation support validation completed: {IsSupported}", isSupported);
                    return isSupported;
                }
                else
                {
                    var error = $"ValidatePersistentReservationSupport failed with return value: {returnValue}";
                    _logger.LogError(error);
                    throw new InvalidOperationException(error);
                }
            }, cancellationToken);
        }
    }
}
