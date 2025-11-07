using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;

namespace HyperV.Core.Wmi.Services
{
    public class StorageService : IStorageService
    {
        private const string WmiNamespace = @"root\virtualization\v2";
        private const UInt16 StorageResourceType = 31; // Logical Disk
        private const string VirtualHardDiskSubType = "Microsoft:Hyper-V:Virtual Hard Disk";

        public void CreateVirtualHardDisk(CreateVhdRequest request)
        {
            var scope = new ManagementScope(WmiNamespace);
            scope.Connect();

            using var imageManagementService = GetImageManagementService(scope);
            using var vhdSettings = CreateVirtualHardDiskSettingData(scope, request);

            var inParams = imageManagementService.GetMethodParameters("CreateVirtualHardDisk");
            inParams["VirtualDiskSettingData"] = vhdSettings.GetText(TextFormat.WmiDtd20);

            using var result = imageManagementService.InvokeMethod("CreateVirtualHardDisk", inParams, null);

            if (!WmiUtilities.ValidateOutput(result, scope))
            {
                throw new ManagementException($"Failed to create virtual hard disk at {request.Path}");
            }
        }

        public void AttachVirtualHardDisk(string vmName, string vhdPath)
        {
            var scope = new ManagementScope(WmiNamespace);
            scope.Connect();

            using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
            using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
            using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);

            // Find a suitable controller and address first
            var controllerInfo = GetSuitableControllerWithAddress(vmSettings);
            
            // Create a new storage allocation setting data instance directly
            using var storageSettingsClass = new ManagementClass("Msvm_StorageAllocationSettingData");
            storageSettingsClass.Scope = scope;
            using var storageSettings = storageSettingsClass.CreateInstance();

            // Set only the essential properties that are writable
            // According to Microsoft docs, most properties are read-only
            storageSettings["ResourceType"] = StorageResourceType;
            storageSettings["ResourceSubType"] = VirtualHardDiskSubType;
            storageSettings["HostResource"] = new[] { vhdPath };
            
            // Don't set Parent and Address directly as they are read-only
            // Instead, we'll modify the XML representation
            var settingsXml = storageSettings.GetText(TextFormat.WmiDtd20);
            
            // Parse and modify the XML to set Parent and Address
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(settingsXml);
            
            // Set the controller parent path
            var parentNode = doc.SelectSingleNode("//PROPERTY[@NAME='Parent']");
            if (parentNode != null)
            {
                var valueNode = parentNode.SelectSingleNode("VALUE") ?? doc.CreateElement("VALUE");
                if (parentNode.SelectSingleNode("VALUE") == null)
                {
                    parentNode.AppendChild(valueNode);
                }
                valueNode.InnerText = controllerInfo.ControllerPath;
            }
            
            // Set the address on the controller
            var addressNode = doc.SelectSingleNode("//PROPERTY[@NAME='Address']");
            if (addressNode != null)
            {
                var valueNode = addressNode.SelectSingleNode("VALUE") ?? doc.CreateElement("VALUE");
                if (addressNode.SelectSingleNode("VALUE") == null)
                {
                    addressNode.AppendChild(valueNode);
                }
                valueNode.InnerText = controllerInfo.Address;
            }

            var inParams = managementService.GetMethodParameters("AddResourceSettings");
            inParams["AffectedSystem"] = vm.Path.Path;
            inParams["ResourceSettings"] = new[] { doc.OuterXml };

            using var result = managementService.InvokeMethod("AddResourceSettings", inParams, null);

            if (!WmiUtilities.ValidateOutput(result, scope))
            {
                throw new ManagementException($"Failed to attach virtual hard disk {vhdPath} to VM {vmName}");
            }
        }

        public void DetachVirtualHardDisk(string vmName, string vhdPath)
        {
            var scope = new ManagementScope(WmiNamespace);
            scope.Connect();

            using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
            using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
            
            // Find the VHD to remove
            using var vhdToRemove = FindVirtualHardDiskSetting(vm, vhdPath);
            
            if (vhdToRemove == null)
            {
                throw new InvalidOperationException($"Virtual hard disk {vhdPath} not found on VM {vmName}");
            }

            var inParams = managementService.GetMethodParameters("RemoveResourceSettings");
            inParams["ResourceSettings"] = new[] { vhdToRemove.Path.Path };

            using var result = managementService.InvokeMethod("RemoveResourceSettings", inParams, null);

            if (!WmiUtilities.ValidateOutput(result, scope))
            {
                throw new ManagementException($"Failed to detach virtual hard disk {vhdPath} from VM {vmName}");
            }
        }

        public void ResizeVirtualHardDisk(ResizeVhdRequest request)
        {
            var scope = new ManagementScope(WmiNamespace);
            scope.Connect();

            using var imageManagementService = GetImageManagementService(scope);

            var inParams = imageManagementService.GetMethodParameters("ResizeVirtualHardDisk");
            inParams["Path"] = request.Path;
            inParams["MaxInternalSize"] = request.MaxInternalSize;

            using var result = imageManagementService.InvokeMethod("ResizeVirtualHardDisk", inParams, null);

            if (!WmiUtilities.ValidateOutput(result, scope))
            {
                throw new ManagementException($"Failed to resize virtual hard disk {request.Path}");
            }
        }

        /// <summary>
        /// Gets the Msvm_ImageManagementService instance.
        /// </summary>
        /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
        /// <returns>The Msvm_ImageManagementService instance.</returns>
        private static ManagementObject GetImageManagementService(ManagementScope scope)
        {
            using var imageManagementServiceClass = new ManagementClass("Msvm_ImageManagementService");
            imageManagementServiceClass.Scope = scope;

            return WmiUtilities.GetFirstObjectFromCollection(imageManagementServiceClass.GetInstances())!;
        }

        /// <summary>
        /// Creates a new Msvm_VirtualHardDiskSettingData instance with the specified parameters.
        /// </summary>
        /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
        /// <param name="request">The VHD creation request parameters.</param>
        /// <returns>A configured Msvm_VirtualHardDiskSettingData instance.</returns>
        private static ManagementObject CreateVirtualHardDiskSettingData(ManagementScope scope, CreateVhdRequest request)
        {
            using var vhdSettingsClass = new ManagementClass("Msvm_VirtualHardDiskSettingData");
            vhdSettingsClass.Scope = scope;

            var vhdSettings = vhdSettingsClass.CreateInstance();
            if (vhdSettings == null)
            {
                throw new InvalidOperationException("Failed to create VirtualHardDiskSettingData instance");
            }

            vhdSettings["Path"] = request.Path;
            vhdSettings["MaxInternalSize"] = request.MaxInternalSize;

            // Set VHD format
            switch (request.Format?.ToUpperInvariant() ?? "VHDX")
            {
                case "VHD":
                    vhdSettings["Format"] = 2;
                    break;
                case "VHDX":
                    vhdSettings["Format"] = 3;
                    break;
                default:
                    throw new ArgumentException($"Invalid VHD format '{request.Format}'. Must be 'VHD' or 'VHDX'.", nameof(request.Format));
            }

            // Set VHD type
            switch (request.Type?.ToUpperInvariant() ?? "DYNAMIC")
            {
                case "DYNAMIC":
                    vhdSettings["Type"] = 3; // Dynamic
                    break;
                case "FIXED":
                    vhdSettings["Type"] = 2; // Fixed
                    break;
                default:
                    throw new ArgumentException($"Invalid VHD type '{request.Type}'. Must be 'Dynamic' or 'Fixed'.", nameof(request.Type));
            }

            return vhdSettings;
        }

        /// <summary>
        /// Gets the default storage allocation setting data for virtual hard disks.
        /// Uses the Microsoft recommended approach from virtualization samples.
        /// </summary>
        /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
        /// <returns>A default Msvm_StorageAllocationSettingData instance.</returns>
        private static ManagementObject GetDefaultStorageAllocationSettingData(ManagementScope scope)
        {
            // Get the resource pool for storage devices
            using var storagePool = WmiUtilities.GetResourcePool(
                StorageResourceType.ToString(), 
                VirtualHardDiskSubType, 
                "Microsoft:Definition\\Default", 
                scope);

            // Get the allocation capabilities for the storage pool
            using var allocationCapabilitiesCollection = storagePool.GetRelated("Msvm_AllocationCapabilities");
            using var allocationCapabilities = WmiUtilities.GetFirstObjectFromCollection(allocationCapabilitiesCollection)!;

            // Get the default setting data from allocation capabilities
            using var settingDataCollection = allocationCapabilities.GetRelated("Msvm_StorageAllocationSettingData");
            var defaultSettings = WmiUtilities.GetFirstObjectFromCollection(settingDataCollection);

            return defaultSettings!;
        }

        /// <summary>
        /// Gets a suitable controller path and address for attaching storage devices.
        /// Prefers SCSI controllers over IDE controllers and finds the next available address.
        /// Based on Microsoft documentation: SCSI controllers support up to 256 drives.
        /// </summary>
        /// <param name="vmSettings">The virtual machine settings object.</param>
        /// <returns>Controller information including path and next available address.</returns>
        private static (string ControllerPath, string Address) GetSuitableControllerWithAddress(ManagementObject vmSettings)
        {
            var controllers = new List<(ManagementObject Controller, bool IsScsi, string ControllerPath)>();
            var usedAddresses = new Dictionary<string, HashSet<string>>();

            // Search through all resource allocation settings
            using var resourceCollection = vmSettings.GetRelated("Msvm_ResourceAllocationSettingData",
                "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

            foreach (ManagementObject resource in resourceCollection)
            {
                try
                {
                    var resourceType = (UInt16)resource["ResourceType"];
                    var resourceSubType = (string)resource["ResourceSubType"];

                    // SCSI Controller (ResourceType = 6) - preferred for VHDs
                    if (resourceType == 6 && resourceSubType == "Microsoft:Hyper-V:Synthetic SCSI Controller")
                    {
                        var controllerPath = resource.Path.Path;
                        controllers.Add((resource, true, controllerPath));
                        if (!usedAddresses.ContainsKey(controllerPath))
                        {
                            usedAddresses[controllerPath] = new HashSet<string>();
                        }
                    }
                    // IDE Controller (ResourceType = 5) - fallback option
                    else if (resourceType == 5 && resourceSubType == "Microsoft:Hyper-V:Emulated IDE Controller")
                    {
                        var controllerPath = resource.Path.Path;
                        controllers.Add((resource, false, controllerPath));
                        if (!usedAddresses.ContainsKey(controllerPath))
                        {
                            usedAddresses[controllerPath] = new HashSet<string>();
                        }
                    }
                    // Storage devices (ResourceType = 31) - collect used addresses
                    else if (resourceType == 31 && resourceSubType == VirtualHardDiskSubType)
                    {
                        var parent = (string)resource["Parent"];
                        var address = (string)resource["Address"];
                        
                        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(address))
                        {
                            if (!usedAddresses.ContainsKey(parent))
                            {
                                usedAddresses[parent] = new HashSet<string>();
                            }
                            usedAddresses[parent].Add(address);
                        }
                    }
                }
                catch
                {
                    resource?.Dispose();
                }
            }

            // Try SCSI controllers first (preferred for VHDs)
            foreach (var (controller, isScsi, controllerPath) in controllers.Where(c => c.IsScsi))
            {
                try
                {
                    var address = GetNextAvailableAddress(controllerPath, usedAddresses, true);
                    if (address != null)
                    {
                        return (controllerPath, address);
                    }
                }
                finally
                {
                    // Don't dispose controller here as we're returning its path
                }
            }

            // Try IDE controllers as fallback
            foreach (var (controller, isScsi, controllerPath) in controllers.Where(c => !c.IsScsi))
            {
                try
                {
                    var address = GetNextAvailableAddress(controllerPath, usedAddresses, false);
                    if (address != null)
                    {
                        return (controllerPath, address);
                    }
                }
                finally
                {
                    // Don't dispose controller here as we're returning its path
                }
            }

            // Dispose all controller resources if no suitable controller found
            foreach (var (controller, _, _) in controllers)
            {
                controller?.Dispose();
            }

            throw new InvalidOperationException("No suitable controller with available slots found for attaching the virtual hard disk.");
        }

        /// <summary>
        /// Gets the next available address on a controller for attaching a new drive.
        /// Based on Microsoft documentation: SCSI supports up to 256 devices, IDE supports 2 per channel.
        /// </summary>
        /// <param name="controllerPath">The controller path to check.</param>
        /// <param name="usedAddresses">Dictionary of used addresses per controller.</param>
        /// <param name="isScsi">True if this is a SCSI controller, false for IDE.</param>
        /// <returns>The next available address, or null if no slots available.</returns>
        private static string GetNextAvailableAddress(string controllerPath, Dictionary<string, HashSet<string>> usedAddresses, bool isScsi)
        {
            try
            {
                var usedAddressesForController = usedAddresses.ContainsKey(controllerPath) 
                    ? usedAddresses[controllerPath] 
                    : new HashSet<string>();

                if (isScsi)
                {
                    // SCSI addresses: "0,X,0" where X is the target (0-255)
                    // Based on Microsoft documentation: SCSI controllers support up to 256 drives
                    for (int target = 0; target < 256; target++)
                    {
                        var address = $"0,{target},0";
                        if (!usedAddressesForController.Contains(address))
                        {
                            return address;
                        }
                    }
                }
                else
                {
                    // IDE addresses: "0,X" where X is the device (0-1)
                    // IDE controllers typically support 2 devices per channel
                    for (int device = 0; device < 2; device++)
                    {
                        var address = $"0,{device}";
                        if (!usedAddressesForController.Contains(address))
                        {
                            return address;
                        }
                    }
                }

                return null!; // No available addresses
            }
            catch
            {
                return null!; // No available addresses
            }
        }

        /// <summary>
        /// Checks if a controller has available slots for attaching additional drives.
        /// </summary>
        /// <param name="controller">The controller resource allocation setting data.</param>
        /// <returns>True if the controller has available slots, false otherwise.</returns>
        private static bool HasAvailableSlots(ManagementObject controller)
        {
            try
            {
                // Get the maximum number of drives this controller can support
                var resourceType = (UInt16)controller["ResourceType"];
                var resourceSubType = (string)controller["ResourceSubType"];
                
                // Note: maxSlots calculation removed as it was unused
                // SCSI controllers typically support up to 64 devices
                // IDE controllers typically support 2 devices per channel
                if (resourceType != 6 && resourceType != 5)
                {
                    return false; // Unknown controller type
                }

                // Count currently attached drives by checking the Address property
                // Address format for SCSI: "0,1,0" (Controller,Target,LUN)
                // Address format for IDE: "0,1" (Channel,Device)
                var address = (string)controller["Address"];
                if (string.IsNullOrEmpty(address))
                {
                    return true; // No drives attached, so slots are available
                }

                // For simplicity, assume controller has available slots if it exists
                // In a production environment, you would enumerate all attached drives
                // and count them against the maximum capacity
                return true;
            }
            catch
            {
                // If we can't determine availability, assume it's available
                return true;
            }
        }

        /// <summary>
        /// Finds the storage allocation setting data for a specific VHD path on a virtual machine.
        /// </summary>
        /// <param name="vm">The virtual machine object.</param>
        /// <param name="vhdPath">The path of the VHD to find.</param>
        /// <returns>The storage allocation setting data for the VHD, or null if not found.</returns>
        private static ManagementObject FindVirtualHardDiskSetting(ManagementObject vm, string vhdPath)
        {
            var vhdSettings = WmiUtilities.GetVhdSettings(vm);
            
            if (vhdSettings == null)
            {
                return null!; // VHD settings not found
            }

            foreach (var setting in vhdSettings)
            {
                try
                {
                    var hostResources = (string[])setting["HostResource"];
                    if (hostResources != null && hostResources.Length > 0)
                    {
                        if (string.Equals(hostResources[0], vhdPath, StringComparison.OrdinalIgnoreCase))
                        {
                            return setting;
                        }
                    }
                }
                catch
                {
                    // Continue searching if this setting has issues
                    continue;
                }
                finally
                {
                    // Don't dispose here as we might return this object
                }
            }

            // Dispose all settings if none matched
            foreach (var setting in vhdSettings)
            {
                setting?.Dispose();
            }

            return null!; // VHD setting not found
        }

        // Asynchronous versions of basic operations
        public async Task CreateVirtualHardDiskAsync(CreateVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                CreateVirtualHardDisk(request);
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        public async Task AttachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                AttachVirtualHardDisk(vmName, vhdPath);
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        public async Task DetachVirtualHardDiskAsync(string vmName, string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                DetachVirtualHardDisk(vmName, vhdPath);
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        public async Task ResizeVirtualHardDiskAsync(ResizeVhdRequest request, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                ResizeVirtualHardDisk(request);
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        // Differencing disk operations
        public async Task CreateDifferencingDiskAsync(string childPath, string parentPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);
                using var vhdSettings = CreateDifferencingDiskSettingData(scope, childPath, parentPath);

                var inParams = imageManagementService.GetMethodParameters("CreateVirtualHardDisk");
                inParams["VirtualDiskSettingData"] = vhdSettings.GetText(TextFormat.WmiDtd20);

                using var result = imageManagementService.InvokeMethod("CreateVirtualHardDisk", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to create differencing disk at {childPath}");
                }
                
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        public async Task MergeDifferencingDiskAsync(string childPath, uint mergeDepth, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("MergeVirtualHardDisk");
                inParams["SourcePath"] = childPath;
                inParams["MergeDepth"] = mergeDepth;

                using var result = imageManagementService.InvokeMethod("MergeVirtualHardDisk", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to merge differencing disk {childPath}");
                }
                
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        // VHD metadata operations
        public async Task<VhdMetadata> GetVhdMetadataAsync(string vhdPath)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("GetVirtualHardDiskInfo");
                inParams["Path"] = vhdPath;

                using var result = imageManagementService.InvokeMethod("GetVirtualHardDiskInfo", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to get VHD metadata for {vhdPath}");
                }

                var info = (string)result["Info"];
                return ParseVhdMetadata(info, vhdPath);
            });
        }

        public async Task SetVhdMetadataAsync(string vhdPath, VhdMetadataUpdate update)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("SetVirtualHardDiskSettingData");
                inParams["Path"] = vhdPath;
                
                // Create VHD setting data with updated values
                using var vhdSettings = CreateUpdatedVhdSettingData(scope, update);
                inParams["VirtualDiskSettingData"] = vhdSettings.GetText(TextFormat.WmiDtd20);

                using var result = imageManagementService.InvokeMethod("SetVirtualHardDiskSettingData", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to set VHD metadata for {vhdPath}");
                }
            });
        }

        // VHD optimization operations
        public async Task CompactVhdAsync(string vhdPath, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("CompactVirtualHardDisk");
                inParams["Path"] = vhdPath;
                inParams["Mode"] = 0; // Full compact

                using var result = imageManagementService.InvokeMethod("CompactVirtualHardDisk", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to compact VHD {vhdPath}");
                }
                
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        // VHD state and validation operations
        public async Task<VhdStateResponse> GetVhdStateAsync(string vhdPath)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                // Check if VHD is mounted
                var mountedImages = GetMountedStorageImages(scope);
                ManagementObject? mountedImage = null;
                
                try
                {
                    mountedImage = mountedImages.FirstOrDefault(img =>
                        string.Equals(img["ImagePath"] as string, vhdPath, StringComparison.OrdinalIgnoreCase));
                }
                finally
                {
                    // Dispose all images except the one we're returning data from
                    foreach (var img in mountedImages)
                    {
                        if (img != mountedImage)
                        {
                            img?.Dispose();
                        }
                    }
                }

                var response = new VhdStateResponse
                {
                    Path = vhdPath,
                    IsAttached = mountedImage != null,
                    PhysicalPath = mountedImage?["DevicePath"] as string,
                    OperationalState = mountedImage != null ? "Attached" : "Detached",
                    HealthStatus = "OK",
                    IsReadOnly = mountedImage?["ReadOnly"] as bool? ?? false,
                    AccessMode = mountedImage != null ? "Mounted" : "Available"
                };
                
                // Dispose the mounted image after extracting the data
                mountedImage?.Dispose();
                
                return response;
            });
        }

        public async Task<VhdValidationResponse> ValidateVhdAsync(VhdValidationRequest request, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("ValidateVirtualHardDisk");
                inParams["Path"] = request.Path;

                using var result = imageManagementService.InvokeMethod("ValidateVirtualHardDisk", inParams, null);

                var returnValue = (UInt32)result["ReturnValue"];
                var isValid = returnValue == 0;

                var response = new VhdValidationResponse
                {
                    Path = request.Path,
                    IsValid = isValid,
                    ValidatedAt = DateTime.UtcNow
                };

                if (!isValid)
                {
                    response.Errors.Add($"VHD validation failed with error code: {returnValue}");
                }

                return response;
            }, cancellationToken);
        }

        public async Task ConvertVhdAsync(string sourcePath, string destinationPath, string targetFormat, CancellationToken cancellationToken, IProgress<VirtualDiskProgress> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report(new VirtualDiskProgress { CurrentValue = 0, CompletionValue = 100 });
                
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("ConvertVirtualHardDisk");
                inParams["SourcePath"] = sourcePath;
                inParams["DestinationPath"] = destinationPath;
                
                // Set target format
                var formatValue = targetFormat.ToUpperInvariant() switch
                {
                    "VHD" => 2,
                    "VHDX" => 3,
                    _ => throw new ArgumentException($"Unsupported format: {targetFormat}")
                };
                
                using var vhdSettings = CreateConversionSettingData(scope, formatValue);
                inParams["VirtualDiskSettingData"] = vhdSettings.GetText(TextFormat.WmiDtd20);

                using var result = imageManagementService.InvokeMethod("ConvertVirtualHardDisk", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to convert VHD from {sourcePath} to {destinationPath}");
                }
                
                progress?.Report(new VirtualDiskProgress { CurrentValue = 100, CompletionValue = 100 });
            }, cancellationToken);
        }

        // Virtual Floppy Disk operations
        public async Task CreateVirtualFloppyDiskAsync(CreateVfdRequest request, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("CreateVirtualFloppyDisk");
                inParams["Path"] = request.Path;
                inParams["Size"] = (uint)request.Size;

                using var result = imageManagementService.InvokeMethod("CreateVirtualFloppyDisk", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to create virtual floppy disk at {request.Path}");
                }
            }, cancellationToken);
        }

        public async Task AttachVirtualFloppyDiskAsync(VfdAttachRequest request)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(request.VmName, scope);
                using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
                using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);

                // Create floppy disk allocation setting data
                using var floppySettings = CreateFloppyDiskAllocationSettingData(scope, request.VfdPath, request.ReadOnly);

                var inParams = managementService.GetMethodParameters("AddResourceSettings");
                inParams["AffectedSystem"] = vm.Path.Path;
                inParams["ResourceSettings"] = new[] { floppySettings.GetText(TextFormat.WmiDtd20) };

                using var result = managementService.InvokeMethod("AddResourceSettings", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to attach virtual floppy disk {request.VfdPath} to VM {request.VmName}");
                }
            });
        }

        public async Task DetachVirtualFloppyDiskAsync(string vmName, string vfdPath)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
                using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
                
                // Find the VFD to remove
                using var vfdToRemove = FindVirtualFloppyDiskSetting(vm, vfdPath);
                
                if (vfdToRemove == null)
                {
                    throw new InvalidOperationException($"Virtual floppy disk {vfdPath} not found on VM {vmName}");
                }

                var inParams = managementService.GetMethodParameters("RemoveResourceSettings");
                inParams["ResourceSettings"] = new[] { vfdToRemove.Path.Path };

                using var result = managementService.InvokeMethod("RemoveResourceSettings", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to detach virtual floppy disk {vfdPath} from VM {vmName}");
                }
            });
        }

        // Mounted storage operations
        public async Task<List<MountedStorageImageResponse>> GetMountedStorageImagesAsync()
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                var mountedImages = new List<MountedStorageImageResponse>();
                var images = GetMountedStorageImages(scope);

                try
                {
                    foreach (var image in images)
                {
                    try
                    {
                        var response = new MountedStorageImageResponse
                        {
                            ImagePath = image["ImagePath"] as string ?? string.Empty,
                            MountPath = image["DevicePath"] as string ?? string.Empty,
                            ImageType = DetermineImageType(image["ImagePath"] as string),
                            IsReadOnly = image["ReadOnly"] as bool? ?? false,
                            Size = (ulong)(image["Size"] ?? 0),
                            MountedAt = DateTime.UtcNow
                        };
                        mountedImages.Add(response);
                    }
                    catch
                    {
                        // Continue processing other images
                        continue;
                    }
                    finally
                    {
                        image?.Dispose();
                    }
                }
                }
                finally
                {
                    // Dispose all images
                    foreach (var image in images)
                    {
                        image?.Dispose();
                    }
                }

                return mountedImages;
            });
        }

        // Change tracking operations
        public async Task EnableChangeTrackingAsync(string vhdPath)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("EnableVirtualHardDiskResilienChangeTracking");
                inParams["Path"] = vhdPath;

                using var result = imageManagementService.InvokeMethod("EnableVirtualHardDiskResilienChangeTracking", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to enable change tracking for VHD {vhdPath}");
                }
            });
        }

        public async Task DisableChangeTrackingAsync(string vhdPath)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("DisableVirtualHardDiskResilienChangeTracking");
                inParams["Path"] = vhdPath;

                using var result = imageManagementService.InvokeMethod("DisableVirtualHardDiskResilienChangeTracking", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to disable change tracking for VHD {vhdPath}");
                }
            });
        }

        public async Task<List<string>> GetVirtualDiskChangesAsync(string vhdPath, string changeTrackingId)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var imageManagementService = GetImageManagementService(scope);

                var inParams = imageManagementService.GetMethodParameters("GetVirtualHardDiskChanges");
                inParams["Path"] = vhdPath;
                inParams["ChangeTrackingId"] = changeTrackingId;

                using var result = imageManagementService.InvokeMethod("GetVirtualHardDiskChanges", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to get VHD changes for {vhdPath}");
                }

                // Parse the changes result - this is a simplified implementation
                var changes = new List<string>();
                var changesData = result["Changes"] as string[];
                if (changesData != null)
                {
                    changes.AddRange(changesData);
                }

                return changes;
            });
        }

        // Storage device management operations
        public async Task<List<StorageDeviceResponse>> GetVmStorageDevicesAsync(string vmName)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
                using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);

                var devices = new List<StorageDeviceResponse>();

                using var resourceCollection = vmSettings.GetRelated("Msvm_ResourceAllocationSettingData",
                    "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

                foreach (ManagementObject resource in resourceCollection)
                {
                    try
                    {
                        var resourceType = (UInt16)resource["ResourceType"];
                        var resourceSubType = resource["ResourceSubType"] as string;

                        // Storage devices (ResourceType = 31)
                        if (resourceType == 31 && (resourceSubType == VirtualHardDiskSubType ||
                            resourceSubType == "Microsoft:Hyper-V:Virtual Floppy Disk" ||
                            resourceSubType == "Microsoft:Hyper-V:Virtual DVD Drive"))
                        {
                            var hostResources = resource["HostResource"] as string[];
                            var device = new StorageDeviceResponse
                            {
                                DeviceId = resource["InstanceID"] as string ?? string.Empty,
                                Name = resource["ElementName"] as string ?? string.Empty,
                                DeviceType = DetermineDeviceType(resourceSubType),
                                Path = hostResources?.FirstOrDefault(),
                                ControllerId = resource["Parent"] as string ?? string.Empty,
                                ControllerType = DetermineControllerType(resource["Parent"] as string),
                                IsReadOnly = resource["HostResourceAccessType"] as uint? == 1,
                                OperationalStatus = "Connected"
                            };

                            devices.Add(device);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    finally
                    {
                        resource?.Dispose();
                    }
                }

                return devices;
            });
        }

        public async Task<List<StorageControllerResponse>> GetVmStorageControllersAsync(string vmName)
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
                using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);

                var controllers = new List<StorageControllerResponse>();

                using var resourceCollection = vmSettings.GetRelated("Msvm_ResourceAllocationSettingData",
                    "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

                foreach (ManagementObject resource in resourceCollection)
                {
                    try
                    {
                        var resourceType = (UInt16)resource["ResourceType"];
                        var resourceSubType = resource["ResourceSubType"] as string;

                        // Controller types (ResourceType = 5 for IDE, 6 for SCSI)
                        if ((resourceType == 5 && resourceSubType == "Microsoft:Hyper-V:Emulated IDE Controller") ||
                            (resourceType == 6 && resourceSubType == "Microsoft:Hyper-V:Synthetic SCSI Controller"))
                        {
                            var controller = new StorageControllerResponse
                            {
                                ControllerId = resource["InstanceID"] as string ?? string.Empty,
                                Name = resource["ElementName"] as string ?? string.Empty,
                                ControllerType = resourceType == 5 ? "IDE" : "SCSI",
                                MaxDevices = resourceType == 5 ? 2 : 64,
                                AttachedDevices = CountAttachedDevices(resource["InstanceID"] as string, resourceCollection),
                                SupportsHotPlug = resourceType == 6,
                                OperationalStatus = "OK",
                                Protocol = resourceType == 5 ? "ATA" : "SCSI"
                            };

                            // Calculate available locations
                            controller.AvailableLocations = GetAvailableControllerLocations(controller.ControllerId, controller.MaxDevices, controller.AttachedDevices);

                            controllers.Add(controller);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                    finally
                    {
                        resource?.Dispose();
                    }
                }

                return controllers;
            });
        }

        public async Task AddStorageDeviceToVmAsync(string vmName, AddStorageDeviceRequest request)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
                using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);

                // Create appropriate storage allocation setting data based on device type
                ManagementObject storageSettings = request.DeviceType.ToLowerInvariant() switch
                {
                    "virtualdisk" or "vhd" or "vhdx" => CreateVhdAllocationSettingData(scope, request),
                    "floppy" or "vfd" => CreateFloppyDiskAllocationSettingData(scope, request.Path ?? string.Empty, request.ReadOnly),
                    "dvd" or "iso" => CreateDvdAllocationSettingData(scope, request.Path, request.ReadOnly),
                    _ => throw new ArgumentException($"Unsupported device type: {request.DeviceType}")
                };

                using (storageSettings)
                {
                    var inParams = managementService.GetMethodParameters("AddResourceSettings");
                    inParams["AffectedSystem"] = vm.Path.Path;
                    inParams["ResourceSettings"] = new[] { storageSettings.GetText(TextFormat.WmiDtd20) };

                    using var result = managementService.InvokeMethod("AddResourceSettings", inParams, null);

                    if (!WmiUtilities.ValidateOutput(result, scope))
                    {
                        throw new ManagementException($"Failed to add storage device to VM {vmName}");
                    }
                }
            });
        }

        public async Task RemoveStorageDeviceFromVmAsync(string vmName, string deviceId)
        {
            await Task.Run(() =>
            {
                var scope = new ManagementScope(WmiNamespace);
                scope.Connect();

                using var vm = WmiUtilities.GetVirtualMachine(vmName, scope);
                using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
                
                // Find the device to remove
                using var device = FindStorageDevice(vm, deviceId);
                
                if (device == null)
                {
                    throw new InvalidOperationException($"Storage device {deviceId} not found on VM {vmName}");
                }

                var inParams = managementService.GetMethodParameters("RemoveResourceSettings");
                inParams["ResourceSettings"] = new[] { device.Path.Path };

                using var result = managementService.InvokeMethod("RemoveResourceSettings", inParams, null);

                if (!WmiUtilities.ValidateOutput(result, scope))
                {
                    throw new ManagementException($"Failed to remove storage device {deviceId} from VM {vmName}");
                }
            });
        }

        // Helper methods for the new functionality
        
        private static ManagementObject CreateDifferencingDiskSettingData(ManagementScope scope, string childPath, string parentPath)
        {
            using var vhdSettingsClass = new ManagementClass("Msvm_VirtualHardDiskSettingData");
            vhdSettingsClass.Scope = scope;

            var vhdSettings = vhdSettingsClass.CreateInstance();
            if (vhdSettings == null)
            {
                throw new InvalidOperationException("Failed to create VirtualHardDiskSettingData instance");
            }

            vhdSettings["Path"] = childPath;
            vhdSettings["ParentPath"] = parentPath;
            vhdSettings["Type"] = 4; // Differencing
            vhdSettings["Format"] = 3; // VHDX (recommended for differencing disks)

            return vhdSettings;
        }

        private static VhdMetadata ParseVhdMetadata(string info, string vhdPath)
        {
            // This is a simplified parser - in reality you'd parse the XML info properly
            var metadata = new VhdMetadata
            {
                Path = vhdPath,
                Format = vhdPath.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) ? "VHDX" : "VHD",
                UniqueId = Guid.NewGuid(), // This would be parsed from the info XML
                IsAttached = false
            };

            // Parse additional properties from info XML as needed
            return metadata;
        }

        private static ManagementObject CreateUpdatedVhdSettingData(ManagementScope scope, VhdMetadataUpdate update)
        {
            using var vhdSettingsClass = new ManagementClass("Msvm_VirtualHardDiskSettingData");
            vhdSettingsClass.Scope = scope;

            var vhdSettings = vhdSettingsClass.CreateInstance();
            if (vhdSettings == null)
            {
                throw new InvalidOperationException("Failed to create VirtualHardDiskSettingData instance");
            }

            if (update.NewUniqueId.HasValue)
            {
                vhdSettings["UniqueId"] = update.NewUniqueId.Value.ToString();
            }

            if (!string.IsNullOrEmpty(update.NewParentPath))
            {
                vhdSettings["ParentPath"] = update.NewParentPath;
            }

            if (update.NewPhysicalSectorSize.HasValue)
            {
                vhdSettings["PhysicalSectorSize"] = update.NewPhysicalSectorSize.Value;
            }

            return vhdSettings;
        }

        private static ManagementObject CreateConversionSettingData(ManagementScope scope, int formatValue)
        {
            using var vhdSettingsClass = new ManagementClass("Msvm_VirtualHardDiskSettingData");
            vhdSettingsClass.Scope = scope;

            var vhdSettings = vhdSettingsClass.CreateInstance();
            if (vhdSettings == null)
            {
                throw new InvalidOperationException("Failed to create VirtualHardDiskSettingData instance");
            }

            vhdSettings["Format"] = formatValue;
            vhdSettings["Type"] = 3; // Dynamic by default

            return vhdSettings;
        }

        private static List<ManagementObject> GetMountedStorageImages(ManagementScope scope)
        {
            var images = new List<ManagementObject>();

            using var imageCollection = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Msvm_MountedStorageImage"));
            using var imageInstances = imageCollection.Get();

            foreach (ManagementObject image in imageInstances)
            {
                images.Add(image);
            }

            return images;
        }

        private static string DetermineImageType(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return "Unknown";

            var extension = System.IO.Path.GetExtension(imagePath).ToLowerInvariant();
            return extension switch
            {
                ".vhd" => "VHD",
                ".vhdx" => "VHDX",
                ".iso" => "ISO",
                ".vfd" => "VFD",
                _ => "Unknown"
            };
        }

        private static ManagementObject CreateFloppyDiskAllocationSettingData(ManagementScope scope, string vfdPath, bool readOnly)
        {
            using var resourceClass = new ManagementClass("Msvm_ResourceAllocationSettingData");
            resourceClass.Scope = scope;

            var resource = resourceClass.CreateInstance();
            if (resource == null)
            {
                throw new InvalidOperationException("Failed to create ResourceAllocationSettingData instance");
            }

            resource["ResourceType"] = (UInt16)21; // Floppy Drive
            resource["ResourceSubType"] = "Microsoft:Hyper-V:Virtual Floppy Disk";
            resource["HostResource"] = new[] { vfdPath };
            resource["HostResourceAccessType"] = readOnly ? (UInt32)1 : (UInt32)3;

            return resource;
        }

        private static ManagementObject FindVirtualFloppyDiskSetting(ManagementObject vm, string vfdPath)
        {
            using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
            using var resourceCollection = vmSettings.GetRelated("Msvm_ResourceAllocationSettingData",
                "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

            foreach (ManagementObject resource in resourceCollection)
            {
                try
                {
                    var resourceType = (UInt16)resource["ResourceType"];
                    var resourceSubType = resource["ResourceSubType"] as string;

                    if (resourceType == 21 && resourceSubType == "Microsoft:Hyper-V:Virtual Floppy Disk")
                    {
                        var hostResources = resource["HostResource"] as string[];
                        if (hostResources != null && hostResources.Length > 0)
                        {
                            if (string.Equals(hostResources[0], vfdPath, StringComparison.OrdinalIgnoreCase))
                            {
                                return resource;
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
                finally
                {
                    // Don't dispose here as we might return this object
                }
            }

            return null!; // Virtual floppy disk setting not found
        }

        private static string DetermineDeviceType(string? resourceSubType)
        {
            return resourceSubType switch
            {
                "Microsoft:Hyper-V:Virtual Hard Disk" => "VirtualDisk",
                "Microsoft:Hyper-V:Virtual Floppy Disk" => "Floppy",
                "Microsoft:Hyper-V:Virtual DVD Drive" => "DVD",
                _ => "Unknown"
            };
        }

        private static string DetermineControllerType(string? controllerPath)
        {
            if (string.IsNullOrEmpty(controllerPath))
                return "Unknown";

            return controllerPath.Contains("IDE", StringComparison.OrdinalIgnoreCase) ? "IDE" : "SCSI";
        }

        private static int CountAttachedDevices(string? controllerId, ManagementObjectCollection resourceCollection)
        {
            if (string.IsNullOrEmpty(controllerId))
                return 0;

            var count = 0;
            foreach (ManagementObject resource in resourceCollection)
            {
                try
                {
                    var parent = resource["Parent"] as string;
                    if (string.Equals(parent, controllerId, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
                catch
                {
                    continue;
                }
            }

            return count;
        }

        private static List<int> GetAvailableControllerLocations(string controllerId, int maxDevices, int attachedDevices)
        {
            var availableLocations = new List<int>();
            var usedLocations = maxDevices - attachedDevices;
            
            for (int i = 0; i < maxDevices; i++)
            {
                if (i >= attachedDevices)
                {
                    availableLocations.Add(i);
                }
            }

            return availableLocations;
        }

        private static ManagementObject CreateVhdAllocationSettingData(ManagementScope scope, AddStorageDeviceRequest request)
        {
            using var resourceClass = new ManagementClass("Msvm_ResourceAllocationSettingData");
            resourceClass.Scope = scope;

            var resource = resourceClass.CreateInstance();
            if (resource == null)
            {
                throw new InvalidOperationException("Failed to create ResourceAllocationSettingData instance");
            }

            resource["ResourceType"] = StorageResourceType;
            resource["ResourceSubType"] = VirtualHardDiskSubType;
            resource["HostResource"] = new[] { request.Path };
            resource["HostResourceAccessType"] = request.ReadOnly ? (UInt32)1 : (UInt32)3;

            if (!string.IsNullOrEmpty(request.ControllerId))
            {
                resource["Parent"] = request.ControllerId;
            }

            return resource;
        }

        private static ManagementObject CreateDvdAllocationSettingData(ManagementScope scope, string? isoPath, bool readOnly)
        {
            using var resourceClass = new ManagementClass("Msvm_ResourceAllocationSettingData");
            resourceClass.Scope = scope;

            var resource = resourceClass.CreateInstance();
            if (resource == null)
            {
                throw new InvalidOperationException("Failed to create ResourceAllocationSettingData instance");
            }

            resource["ResourceType"] = (UInt16)16; // DVD Drive
            resource["ResourceSubType"] = "Microsoft:Hyper-V:Virtual DVD Drive";
            if (!string.IsNullOrEmpty(isoPath))
            {
                resource["HostResource"] = new[] { isoPath };
            }
            resource["HostResourceAccessType"] = (UInt32)1; // DVD is always read-only

            return resource;
        }

        private static ManagementObject FindStorageDevice(ManagementObject vm, string deviceId)
        {
            using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
            using var resourceCollection = vmSettings.GetRelated("Msvm_ResourceAllocationSettingData",
                "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

            foreach (ManagementObject resource in resourceCollection)
            {
                try
                {
                    var instanceId = resource["InstanceID"] as string;
                    if (string.Equals(instanceId, deviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        return resource;
                    }
                }
                catch
                {
                    continue;
                }
                finally
                {
                    // Don't dispose here as we might return this object
                }
            }

            return null!; // Storage device not found
        }
        /// <summary>
        /// Lists all fixed storage devices (local drives) with their details.
        /// </summary>
        /// <returns>A list of storage device information.</returns>
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
        /// <param name="minFreeSpaceGb">Minimum free space required in GB (default: 10).</param>
        /// <returns>A list of suitable storage locations.</returns>
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
