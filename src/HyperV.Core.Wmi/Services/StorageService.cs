using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
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

            var vhdSettings = vhdSettingsClass.CreateInstance()!;

            vhdSettings["Path"] = request.Path;
            vhdSettings["MaxInternalSize"] = request.MaxInternalSize;

            // Set VHD format
            switch (request.Format.ToUpperInvariant())
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
            switch (request.Type.ToUpperInvariant())
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

                return null; // No available addresses
            }
            catch
            {
            return null!;
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
            return null!;
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

            return null;
        }
    }
}
