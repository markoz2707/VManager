using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.Json;

namespace HyperV.Core.Wmi.Services;

/// <summary>Comprehensive WMI VM Service providing all VM operations using Hyper-V WMI API.</summary>
public sealed class VmService
{
    /// <summary>Lists all VMs in WMI.</summary>
    public string ListVms()
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var query = "SELECT * FROM Msvm_ComputerSystem WHERE Caption = 'Maszyna wirtualna'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            
            var vms = new List<object>();
            
            foreach (ManagementObject vm in searcher.Get())
            {
                using (vm)
                {
                    var enabledState = (ushort)vm["EnabledState"];
                    var healthState = (ushort)vm["HealthState"];
                    
                    vms.Add(new
                    {
                        Id = vm["Name"]?.ToString(),
                        Name = vm["ElementName"]?.ToString(),
                        State = GetVmStateString(enabledState),
                        EnabledState = enabledState,
                        HealthState = GetHealthStateString(healthState),
                        Description = vm["Description"]?.ToString(),
                        CreationTime = vm["TimeOfLastConfigurationChange"]?.ToString(),
                        Backend = "WMI"
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                Count = vms.Count,
                VMs = vms,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list VMs: {ex.Message}", ex);
        }
    }

    /// <summary>Checks if a VM exists in WMI.</summary>
    public bool IsVmPresent(string vmId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmId}' OR Name = '{vmId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            
            return searcher.Get().Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking VM presence: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>Gets VM properties and status.</summary>
    public string GetVmProperties(string vmId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var enabledState = (ushort)vm["EnabledState"];
                var healthState = (ushort)vm["HealthState"];
                var operationalStatus = vm["OperationalStatus"] as ushort[];
                
                var state = GetVmStateString(enabledState);
                var health = GetHealthStateString(healthState);
                
                return JsonSerializer.Serialize(new
                {
                    Name = vm["ElementName"]?.ToString(),
                    State = state,
                    EnabledState = enabledState,
                    HealthState = health,
                    OperationalStatus = operationalStatus,
                    Description = vm["Description"]?.ToString(),
                    CreationTime = vm["TimeOfLastConfigurationChange"]?.ToString(),
                    Backend = "WMI"
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM properties: {ex.Message}", ex);
        }
    }
    
    /// <summary>Starts a VM.</summary>
    public void StartVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Starting WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 2; // Running
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Start VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "start");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to start VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} started successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Stops a VM gracefully.</summary>
    public void StopVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Stopping WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 3; // Off
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Stop VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "stop");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to stop VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} stopped successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stop VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Terminates a VM forcefully.</summary>
    public string TerminateVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Terminating WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 32768; // Hard Reset/Force Off
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Terminate VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "terminate");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to terminate VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} terminated successfully");
                
                return JsonSerializer.Serialize(new
                {
                    Status = 0,
                    ExitType = "ForcedTermination",
                    Attribution = new[]
                    {
                        new { SystemExit = new { Detail = "Terminate", Initiator = "WMI" } }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to terminate VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Pauses a VM.</summary>
    public void PauseVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Pausing WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 9; // Paused
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Pause VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "pause");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to pause VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} paused successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to pause VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Resumes a paused VM.</summary>
    public void ResumeVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Resuming WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 2; // Running
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Resume VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "resume");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to resume VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} resumed successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resume VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Deletes a VM and its associated resources.</summary>
    public void DeleteVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Deleting WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                // First, ensure VM is stopped
                var enabledState = (ushort)vm["EnabledState"];
                if (enabledState == 2) // Running
                {
                    Console.WriteLine("VM is running, stopping before deletion...");
                    StopVm(vmId);
                }
                
                // Get management service
                using var managementService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null);
                using var managementServiceInstance = managementService.GetInstances().Cast<ManagementObject>().First();
                
                // Delete the VM
                var inParams = managementServiceInstance.GetMethodParameters("DestroySystem");
                inParams["AffectedSystem"] = vm.Path.Path;
                
                var result = managementServiceInstance.InvokeMethod("DestroySystem", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Delete VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "delete");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to delete VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} deleted successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete VM: {ex.Message}", ex);
        }
    }
    
    /// <summary>Resets a VM.</summary>
    public void ResetVm(string vmId)
    {
        try
        {
            Console.WriteLine($"Resetting WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 11; // Reset
                
                var result = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Reset VM returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "reset");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to reset VM. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM {vmId} reset successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to reset VM: {ex.Message}", ex);
        }
    }

    /// <summary>Modifies VM configuration (memory, CPU, etc.).</summary>
    public void ModifyVmConfiguration(string vmId, int? startupMemoryMB = null, int? cpuCount = null, string? notes = null, bool? enableDynamicMemory = null, int? minimumMemoryMB = null, int? maximumMemoryMB = null, int? targetMemoryBuffer = null, int? virtualMachineReserve = null, int? virtualMachineLimit = null, int? relativeWeight = null, bool? limitProcessorFeatures = null, int? maxProcessorsPerNumaNode = null, int? maxNumaNodesPerSocket = null, int? hwThreadsPerCore = null)
    {
        try
        {
            Console.WriteLine($"Modifying WMI VM configuration: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                Console.WriteLine($"Found VM: {vm["ElementName"]}, Path: {vm.Path.Path}");
                
                // Get management service
                using var managementService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null);
                using var managementServiceInstance = managementService.GetInstances().Cast<ManagementObject>().First();
                
                Console.WriteLine("Got management service instance");
                
                // Get current VM settings using a more specific query
                var vmSettingsQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE InstanceID LIKE '%{vm["Name"]}%' AND VirtualSystemType = 'Microsoft:Hyper-V:System:Realized'";
                Console.WriteLine($"VM settings query: {vmSettingsQuery}");
                
                using var vmSettingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(vmSettingsQuery));
                
                ManagementObject? vmSettings = null;
                var settingsResults = vmSettingsSearcher.Get();
                Console.WriteLine($"Found {settingsResults.Count} VM settings objects");
                
                foreach (ManagementObject setting in settingsResults)
                {
                    using (setting)
                    {
                        Console.WriteLine($"Setting: {setting["ElementName"]}, InstanceID: {setting["InstanceID"]}, VirtualSystemType: {setting["VirtualSystemType"]}");
                        if (setting["VirtualSystemType"]?.ToString() == "Microsoft:Hyper-V:System:Realized")
                        {
                            vmSettings = setting.Clone() as ManagementObject;
                            Console.WriteLine("Found matching VM settings");
                            break;
                        }
                    }
                }
                
                if (vmSettings == null)
                {
                    Console.WriteLine("VM settings not found, trying alternative approach");
                    
                    // Alternative approach: Get settings via association
                    var associationQuery = $"ASSOCIATORS OF {{{vm.Path.Path}}} WHERE AssocClass = Msvm_SettingsDefineState ResultClass = Msvm_VirtualSystemSettingData";
                    Console.WriteLine($"Association query: {associationQuery}");
                    
                    using var associationSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(associationQuery));
                    var associationResults = associationSearcher.Get();
                    Console.WriteLine($"Found {associationResults.Count} associated settings");
                    
                    foreach (ManagementObject setting in associationResults)
                    {
                        using (setting)
                        {
                            Console.WriteLine($"Associated setting: {setting["ElementName"]}, VirtualSystemType: {setting["VirtualSystemType"]}");
                            if (setting["VirtualSystemType"]?.ToString() == "Microsoft:Hyper-V:System:Realized")
                            {
                                vmSettings = setting.Clone() as ManagementObject;
                                Console.WriteLine("Found VM settings via association");
                                break;
                            }
                        }
                    }
                }
                
                if (vmSettings == null)
                {
                    throw new InvalidOperationException("VM settings not found");
                }
                
                using (vmSettings)
                {
                    bool modified = false;
                    
                    // Update notes if provided
                    if (!string.IsNullOrEmpty(notes))
                    {
                        Console.WriteLine($"Updating notes to: {notes}");
                        vmSettings["Notes"] = new string[] { notes };
                        modified = true;
                    }
                    
                    if (modified)
                    {
                        Console.WriteLine("Modifying VM settings...");
                        
                        // Modify VM settings
                        var inParams = managementServiceInstance.GetMethodParameters("ModifySystemSettings");
                        inParams["SystemSettings"] = vmSettings.GetText(TextFormat.WmiDtd20);
                        
                        var result = managementServiceInstance.InvokeMethod("ModifySystemSettings", inParams, null);
                        var returnValue = (uint)result["ReturnValue"];
                        
                        Console.WriteLine($"Modify VM settings returned: {returnValue}");
                        
                        if (returnValue == 4096)
                        {
                            var jobPath = result["Job"] as string;
                            if (!string.IsNullOrEmpty(jobPath))
                            {
                                Console.WriteLine($"Waiting for job: {jobPath}");
                                WaitForJob(scope, jobPath, "modify settings");
                            }
                        }
                        else if (returnValue != 0)
                        {
                            throw new InvalidOperationException($"Failed to modify VM settings. Return value: {returnValue}");
                        }
                    }
                    
                    // Modify memory if provided
                    if (startupMemoryMB.HasValue)
                    {
                        Console.WriteLine($"Modifying memory to {startupMemoryMB.Value}MB");
                        ModifyVmMemory(scope, vm, startupMemoryMB.Value, enableDynamicMemory, minimumMemoryMB, maximumMemoryMB, targetMemoryBuffer);
                    }
                    
                    // Modify CPU if provided
                    if (cpuCount.HasValue)
                    {
                        Console.WriteLine($"Modifying CPU count to {cpuCount.Value}");
                        ModifyVmProcessor(scope, vm, cpuCount.Value, virtualMachineReserve, virtualMachineLimit, relativeWeight, limitProcessorFeatures, maxProcessorsPerNumaNode, maxNumaNodesPerSocket, hwThreadsPerCore);
                    }
                }
                
                Console.WriteLine($"VM {vmId} configuration modified successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ModifyVmConfiguration: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Failed to modify VM configuration: {ex.Message}", ex);
        }
    }

    private void ModifyVmMemory(ManagementScope scope, ManagementObject vm, int startupMemoryMB, bool? enableDynamicMemory, int? minimumMemoryMB, int? maximumMemoryMB, int? targetMemoryBuffer)
    {
        try
        {
            Console.WriteLine($"[MEMORY] Starting memory modification for VM: {vm["ElementName"]}");

            var vmGuid = vm["Name"]?.ToString();
            Console.WriteLine($"[MEMORY] VM GUID: {vmGuid}");

            var memoryRasdQuery = $"SELECT * FROM Msvm_MemorySettingData WHERE InstanceID LIKE '%{vmGuid}%'";
            Console.WriteLine($"[MEMORY] Memory RASD query: {memoryRasdQuery}");

            ManagementObject? memoryRasd = null;
            using (var rasdSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(memoryRasdQuery)))
            {
                var rasdResults = rasdSearcher.Get();
                Console.WriteLine($"[MEMORY] Found {rasdResults.Count} Memory RASD objects");
                
                foreach (ManagementObject rasd in rasdResults)
                {
                    using (rasd)
                    {
                        var instanceId = rasd["InstanceID"]?.ToString() ?? "unknown";
                        if (vmGuid != null && instanceId.Contains(vmGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            memoryRasd = rasd.Clone() as ManagementObject;
                            Console.WriteLine($"[MEMORY] Selected Memory RASD: InstanceID='{instanceId}'");
                            break;
                        }
                    }
                }
            }

            if (memoryRasd == null)
            {
                throw new InvalidOperationException("Memory ResourceAllocationSettingData not found");
            }

            using (memoryRasd)
            {
                Console.WriteLine($"[MEMORY] Current memory properties:");
                foreach (PropertyData prop in memoryRasd.Properties)
                {
                    try
                    {
                        var value = prop.Value?.ToString() ?? "null";
                        Console.WriteLine($"[MEMORY]   {prop.Name}: {value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MEMORY]   {prop.Name}: Error reading - {ex.Message}");
                    }
                }

                if (enableDynamicMemory.HasValue && enableDynamicMemory.Value)
                {
                    Console.WriteLine("[MEMORY] Enabling dynamic memory");
                    memoryRasd["DynamicMemoryEnabled"] = true;
                    memoryRasd["VirtualQuantity"] = (ulong)startupMemoryMB;
                    memoryRasd["Reservation"] = (ulong)(minimumMemoryMB ?? startupMemoryMB);
                    memoryRasd["Limit"] = (ulong)(maximumMemoryMB ?? startupMemoryMB);
                    if (targetMemoryBuffer.HasValue)
                    {
                        memoryRasd["TargetMemoryBuffer"] = (uint)targetMemoryBuffer.Value;
                    }
                }
                else
                {
                    Console.WriteLine("[MEMORY] Disabling dynamic memory");
                    memoryRasd["DynamicMemoryEnabled"] = false;
                    memoryRasd["VirtualQuantity"] = (ulong)startupMemoryMB;
                    memoryRasd["Reservation"] = (ulong)startupMemoryMB;
                    memoryRasd["Limit"] = (ulong)startupMemoryMB;
                }

                var wmiDtd = memoryRasd.GetText(TextFormat.WmiDtd20);
                Console.WriteLine($"[MEMORY] WMI DTD representation length: {wmiDtd.Length} characters");
                Console.WriteLine($"[MEMORY] WMI DTD (first 500 chars): {wmiDtd.Substring(0, Math.Min(500, wmiDtd.Length))}...");

                using var managementService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null);
                using var managementServiceInstance = managementService.GetInstances().Cast<ManagementObject>().First();

                Console.WriteLine($"[MEMORY] Got management service: {managementServiceInstance.Path.Path}");

                var inParams = managementServiceInstance.GetMethodParameters("ModifyResourceSettings");
                inParams["ResourceSettings"] = new[] { wmiDtd };

                Console.WriteLine("[MEMORY] Calling ModifyResourceSettings...");
                var result = managementServiceInstance.InvokeMethod("ModifyResourceSettings", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                Console.WriteLine($"[MEMORY] ModifyResourceSettings returned: {returnValue}");
                
                foreach (PropertyData prop in result.Properties)
                {
                    try
                    {
                        var value = prop.Value?.ToString() ?? "null";
                        Console.WriteLine($"[MEMORY] Result.{prop.Name}: {value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MEMORY] Result.{prop.Name}: Error reading - {ex.Message}");
                    }
                }

                if (returnValue == 4096)
                {
                    var jobPath = result["Job"] as string;
                    Console.WriteLine($"[MEMORY] Job started: {jobPath}");
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJobVerbose(scope, jobPath, "modify memory");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to modify memory. Return value: {returnValue}");
                }

                Console.WriteLine("[MEMORY] Memory modification completed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MEMORY] Error in ModifyVmMemory: {ex.Message}");
            Console.WriteLine($"[MEMORY] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Failed to modify VM memory: {ex.Message}", ex);
        }
    }

    private void ModifyVmProcessor(ManagementScope scope, ManagementObject vm, int? cpuCount, int? virtualMachineReserve, int? virtualMachineLimit, int? relativeWeight, bool? limitProcessorFeatures, int? maxProcessorsPerNumaNode, int? maxNumaNodesPerSocket, int? hwThreadsPerCore)
    {
        try
        {
            Console.WriteLine($"[CPU] Starting CPU modification for VM: {vm["ElementName"]}");

            var vmGuid = vm["Name"]?.ToString();
            Console.WriteLine($"[CPU] VM GUID: {vmGuid}");

            var processorRasdQuery = $"SELECT * FROM Msvm_ProcessorSettingData WHERE InstanceID LIKE '%{vmGuid}%'";
            Console.WriteLine($"[CPU] Processor RASD query: {processorRasdQuery}");

            ManagementObject? cpuRasd = null;
            using (var rasdSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(processorRasdQuery)))
            {
                var rasdResults = rasdSearcher.Get();
                Console.WriteLine($"[CPU] Found {rasdResults.Count} Processor RASD objects");
                
                foreach (ManagementObject rasd in rasdResults)
                {
                    using (rasd)
                    {
                        var instanceId = rasd["InstanceID"]?.ToString() ?? "unknown";
                        if (vmGuid != null && instanceId.Contains(vmGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            cpuRasd = rasd.Clone() as ManagementObject;
                            Console.WriteLine($"[CPU] Selected Processor RASD: InstanceID='{instanceId}'");
                            break;
                        }
                    }
                }
            }

            if (cpuRasd == null)
            {
                throw new InvalidOperationException("Processor ResourceAllocationSettingData not found");
            }

            using (cpuRasd)
            {
                Console.WriteLine($"[CPU] Current processor properties:");
                foreach (PropertyData prop in cpuRasd.Properties)
                {
                    try
                    {
                        var value = prop.Value?.ToString() ?? "null";
                        Console.WriteLine($"[CPU]   {prop.Name}: {value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CPU]   {prop.Name}: Error reading - {ex.Message}");
                    }
                }

                if (cpuCount.HasValue) cpuRasd["VirtualQuantity"] = (ulong)cpuCount.Value;
                if (virtualMachineReserve.HasValue) cpuRasd["Reservation"] = (ulong)virtualMachineReserve.Value;
                if (virtualMachineLimit.HasValue) cpuRasd["Limit"] = (ulong)virtualMachineLimit.Value;
                if (relativeWeight.HasValue) cpuRasd["Weight"] = (uint)relativeWeight.Value;
                if (limitProcessorFeatures.HasValue) cpuRasd["LimitProcessorFeatures"] = limitProcessorFeatures.Value;
                if (maxProcessorsPerNumaNode.HasValue) cpuRasd["MaxProcessorsPerNumaNode"] = (ulong)maxProcessorsPerNumaNode.Value;
                if (maxNumaNodesPerSocket.HasValue) cpuRasd["MaxNumaNodesPerSocket"] = (ulong)maxNumaNodesPerSocket.Value;
                if (hwThreadsPerCore.HasValue) cpuRasd["HwThreadsPerCore"] = (ulong)hwThreadsPerCore.Value;

                var wmiDtd = cpuRasd.GetText(TextFormat.WmiDtd20);
                Console.WriteLine($"[CPU] WMI DTD representation length: {wmiDtd.Length} characters");
                Console.WriteLine($"[CPU] WMI DTD (first 500 chars): {wmiDtd.Substring(0, Math.Min(500, wmiDtd.Length))}...");

                using var managementService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null);
                using var managementServiceInstance = managementService.GetInstances().Cast<ManagementObject>().First();

                Console.WriteLine($"[CPU] Got management service: {managementServiceInstance.Path.Path}");

                var inParams = managementServiceInstance.GetMethodParameters("ModifyResourceSettings");
                inParams["ResourceSettings"] = new[] { wmiDtd };

                Console.WriteLine("[CPU] Calling ModifyResourceSettings...");
                var result = managementServiceInstance.InvokeMethod("ModifyResourceSettings", inParams, null);
                var returnValue = (uint)result["ReturnValue"];

                Console.WriteLine($"[CPU] ModifyResourceSettings returned: {returnValue}");
                
                foreach (PropertyData prop in result.Properties)
                {
                    try
                    {
                        var value = prop.Value?.ToString() ?? "null";
                        Console.WriteLine($"[CPU] Result.{prop.Name}: {value}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CPU] Result.{prop.Name}: Error reading - {ex.Message}");
                    }
                }

                if (returnValue == 4096)
                {
                    var jobPath = result["Job"] as string;
                    Console.WriteLine($"[CPU] Job started: {jobPath}");
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJobVerbose(scope, jobPath, "modify processor");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to modify processor. Return value: {returnValue}");
                }

                Console.WriteLine("[CPU] Processor modification completed successfully");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CPU] Error in ModifyVmProcessor: {ex.Message}");
            Console.WriteLine($"[CPU] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Failed to modify VM processor: {ex.Message}", ex);
        }
    }

    /// <summary>Lists all snapshots for a VM.</summary>
    public string ListVmSnapshots(string vmId)
    {
        try
        {
            Console.WriteLine($"Listing snapshots for WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                var vmGuid = vm["Name"]?.ToString();
                Console.WriteLine($"VM GUID: {vmGuid}");
                
                // Get all snapshots for this VM using the VM's GUID
                var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemType = 'Microsoft:Hyper-V:Snapshot:Realized'";
                using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
                
                var snapshots = new List<object>();
                var allSnapshots = snapshotSearcher.Get();
                Console.WriteLine($"Total snapshots found in system: {allSnapshots.Count}");
                
                foreach (ManagementObject snapshot in allSnapshots)
                {
                    try
                    {
                        using (snapshot)
                        {
                            // Try to get basic properties with error handling
                            string? snapshotSystemName = null;
                            string? instanceId = null;
                            string? elementName = null;
                            
                            try { snapshotSystemName = snapshot["SystemName"]?.ToString(); } catch (Exception ex) { Console.WriteLine($"Error getting SystemName: {ex.Message}"); }
                            try { instanceId = snapshot["InstanceID"]?.ToString(); } catch (Exception ex) { Console.WriteLine($"Error getting InstanceID: {ex.Message}"); }
                            try { elementName = snapshot["ElementName"]?.ToString(); } catch (Exception ex) { Console.WriteLine($"Error getting ElementName: {ex.Message}"); }
                            
                            Console.WriteLine($"Processing snapshot: '{elementName}', InstanceID: '{instanceId}', SystemName: '{snapshotSystemName}', VM GUID: '{vmGuid}'");
                            
                            // Try to get all available properties for debugging
                            try
                            {
                                Console.WriteLine($"Snapshot properties available:");
                                foreach (PropertyData prop in snapshot.Properties)
                                {
                                    try
                                    {
                                        var value = prop.Value?.ToString() ?? "null";
                                        Console.WriteLine($"  {prop.Name}: {value}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"  {prop.Name}: Error - {ex.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error enumerating properties: {ex.Message}");
                            }
                            
                            // Try different approaches to match snapshots to VM
                            bool isVmSnapshot = false;
                            
                            // Method 1: Use VirtualSystemIdentifier property (most reliable)
                            try
                            {
                                var virtualSystemId = snapshot["VirtualSystemIdentifier"]?.ToString();
                                if (!string.IsNullOrEmpty(virtualSystemId) && virtualSystemId.Equals(vmGuid, StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"Match found via VirtualSystemIdentifier for snapshot '{elementName}'");
                                    isVmSnapshot = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error getting VirtualSystemIdentifier: {ex.Message}");
                            }
                            
                            // Method 2: Direct SystemName comparison (fallback)
                            if (!isVmSnapshot && !string.IsNullOrEmpty(snapshotSystemName) && snapshotSystemName.Equals(vmGuid, StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"Match found via SystemName for snapshot '{elementName}'");
                                isVmSnapshot = true;
                            }
                            
                            // Method 3: Check parent-child relationship through InstanceID pattern (fallback)
                            if (!isVmSnapshot && !string.IsNullOrEmpty(instanceId))
                            {
                                if (vmGuid != null && instanceId.Contains(vmGuid, StringComparison.OrdinalIgnoreCase))
                                {
                                    Console.WriteLine($"Match found via InstanceID pattern for snapshot '{elementName}'");
                                    isVmSnapshot = true;
                                }
                            }
                            
                            if (isVmSnapshot)
                            {
                                Console.WriteLine($"Adding snapshot '{elementName}' to results");
                                snapshots.Add(new
                                {
                                    Id = instanceId,
                                    Name = elementName,
                                    Notes = snapshot["Notes"]?.ToString(),
                                    CreationTime = snapshot["CreationTime"]?.ToString(),
                                    ParentSnapshotId = snapshot["Parent"]?.ToString(),
                                    SystemName = snapshotSystemName,
                                    Backend = "WMI"
                                });
                            }
                            else
                            {
                                Console.WriteLine($"No match found for snapshot '{elementName}'");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing snapshot: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Found {snapshots.Count} snapshots for VM {vmId}");
                
                return JsonSerializer.Serialize(new
                {
                    VmId = vmId,
                    VmName = vm["ElementName"]?.ToString(),
                    VmGuid = vmGuid,
                    Count = snapshots.Count,
                    Snapshots = snapshots,
                    Backend = "WMI"
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list VM snapshots: {ex.Message}", ex);
        }
    }

    /// <summary>Creates a snapshot of a VM.</summary>
    public string CreateVmSnapshot(string vmId, string snapshotName, string? notes = null)
    {
        try
        {
            Console.WriteLine($"Creating snapshot '{snapshotName}' for WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vm = FindVmByName(scope, vmId);
            if (vm == null)
            {
                throw new InvalidOperationException($"VM {vmId} not found");
            }
            
            using (vm)
            {
                // Get snapshot service
                using var snapshotService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemSnapshotService"), null);
                using var snapshotServiceInstance = snapshotService.GetInstances().Cast<ManagementObject>().First();
                
                // Create snapshot
                var inParams = snapshotServiceInstance.GetMethodParameters("CreateSnapshot");
                inParams["AffectedSystem"] = vm.Path.Path;
                inParams["SnapshotSettings"] = "";
                inParams["SnapshotType"] = 2; // Full snapshot
                
                var result = snapshotServiceInstance.InvokeMethod("CreateSnapshot", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Create snapshot returned: {returnValue}");
                
                string? snapshotPath = null;
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "create snapshot");
                        
                        // Get the created snapshot from job result
                        using var job = new ManagementObject(jobPath);
                        job.Get();
                        var jobResult = job["Result"] as string;
                        if (!string.IsNullOrEmpty(jobResult))
                        {
                            snapshotPath = jobResult;
                        }
                    }
                }
                else if (returnValue == 0)
                {
                    // Completed immediately
                    snapshotPath = result["ResultingSnapshot"] as string;
                }
                else
                {
                    throw new InvalidOperationException($"Failed to create snapshot. Return value: {returnValue}");
                }
                
                // Update snapshot name and notes if provided
                if (!string.IsNullOrEmpty(snapshotPath) && (!string.IsNullOrEmpty(snapshotName) || !string.IsNullOrEmpty(notes)))
                {
                    try
                    {
                        using var snapshot = new ManagementObject(snapshotPath);
                        snapshot.Get();
                        
                        if (!string.IsNullOrEmpty(snapshotName))
                        {
                            snapshot["ElementName"] = snapshotName;
                        }
                        
                        if (!string.IsNullOrEmpty(notes))
                        {
                            snapshot["Notes"] = notes;
                        }
                        
                        snapshot.Put();
                        Console.WriteLine($"Updated snapshot name to '{snapshotName}' and notes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to update snapshot name/notes: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Snapshot '{snapshotName}' created successfully for VM {vmId}");
                
                return JsonSerializer.Serialize(new
                {
                    VmId = vmId,
                    SnapshotId = snapshotPath?.Split('=').LastOrDefault()?.Trim('"'),
                    SnapshotName = snapshotName,
                    Notes = notes,
                    CreationTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Status = "Created",
                    Backend = "WMI"
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create VM snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>Deletes a VM snapshot.</summary>
    public void DeleteVmSnapshot(string vmId, string snapshotId)
    {
        try
        {
            Console.WriteLine($"Deleting snapshot '{snapshotId}' for WMI VM: {vmId}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            // Find the snapshot
            var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE InstanceID = '{snapshotId}'";
            using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
            
            ManagementObject? snapshot = null;
            foreach (ManagementObject snap in snapshotSearcher.Get())
            {
                snapshot = snap;
                break;
            }
            
            if (snapshot == null)
            {
                throw new InvalidOperationException($"Snapshot {snapshotId} not found");
            }
            
            using (snapshot)
            {
                // Get snapshot service
                using var snapshotService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemSnapshotService"), null);
                using var snapshotServiceInstance = snapshotService.GetInstances().Cast<ManagementObject>().First();
                
                // Delete snapshot
                var inParams = snapshotServiceInstance.GetMethodParameters("DestroySnapshot");
                inParams["AffectedSnapshot"] = snapshot.Path.Path;
                
                var result = snapshotServiceInstance.InvokeMethod("DestroySnapshot", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Delete snapshot returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "delete snapshot");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to delete snapshot. Return value: {returnValue}");
                }
                
                Console.WriteLine($"Snapshot '{snapshotId}' deleted successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete VM snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>Reverts a VM to a specific snapshot.</summary>
    public void RevertVmToSnapshot(string vmId, string snapshotId)
    {
        try
        {
            Console.WriteLine($"Reverting WMI VM '{vmId}' to snapshot '{snapshotId}'");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            // Find the snapshot
            var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE InstanceID = '{snapshotId}'";
            using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
            
            ManagementObject? snapshot = null;
            foreach (ManagementObject snap in snapshotSearcher.Get())
            {
                snapshot = snap;
                break;
            }
            
            if (snapshot == null)
            {
                throw new InvalidOperationException($"Snapshot {snapshotId} not found");
            }
            
            using (snapshot)
            {
                // Get snapshot service
                using var snapshotService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemSnapshotService"), null);
                using var snapshotServiceInstance = snapshotService.GetInstances().Cast<ManagementObject>().First();
                
                // Apply snapshot (revert)
                var inParams = snapshotServiceInstance.GetMethodParameters("ApplySnapshot");
                inParams["Snapshot"] = snapshot.Path.Path;
                
                var result = snapshotServiceInstance.InvokeMethod("ApplySnapshot", inParams, null);
                var returnValue = (uint)result["ReturnValue"];
                
                Console.WriteLine($"Revert to snapshot returned: {returnValue}");
                
                if (returnValue == 4096)
                {
                    // Job started - wait for completion
                    var jobPath = result["Job"] as string;
                    if (!string.IsNullOrEmpty(jobPath))
                    {
                        WaitForJob(scope, jobPath, "revert to snapshot");
                    }
                }
                else if (returnValue != 0)
                {
                    throw new InvalidOperationException($"Failed to revert to snapshot. Return value: {returnValue}");
                }
                
                Console.WriteLine($"VM '{vmId}' reverted to snapshot '{snapshotId}' successfully");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to revert VM to snapshot: {ex.Message}", ex);
        }
    }
    
    private ManagementObject? FindVmByName(ManagementScope scope, string vmName)
    {
        try
        {
            var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            
            foreach (ManagementObject vm in searcher.Get())
            {
                return vm; // Return the first match (don't dispose here)
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding VM by name: {ex.Message}");
            return null;
        }
    }
    
    private void WaitForJob(ManagementScope scope, string jobPath, string operation)
    {
        try
        {
            using var job = new ManagementObject(jobPath);
            
            Console.WriteLine($"Waiting for {operation} job to complete...");
            
            // Wait for job completion (max 60 seconds)
            for (int i = 0; i < 60; i++)
            {
                job.Get();
                var jobState = (ushort)job["JobState"];
                
                // JobState: 2 = New, 3 = Starting, 4 = Running, 7 = Completed, 8 = Terminated, 9 = Killed, 10 = Exception
                if (jobState == 7) // Completed
                {
                    Console.WriteLine($"VM {operation} job completed successfully");
                    return;
                }
                else if (jobState == 8 || jobState == 9 || jobState == 10) // Failed states
                {
                    var errorDescription = job["ErrorDescription"]?.ToString() ?? "Unknown error";
                    Console.WriteLine($"Job failed with state {jobState}. Error: {errorDescription}");
                    throw new InvalidOperationException($"VM {operation} job failed: {errorDescription}");
                }
                
                System.Threading.Thread.Sleep(1000); // Wait 1 second
            }
            
            throw new InvalidOperationException($"VM {operation} job timed out");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error waiting for {operation} job: {ex.Message}");
            throw;
        }
    }

    private void WaitForJobVerbose(ManagementScope scope, string jobPath, string operation)
    {
        try
        {
            using var job = new ManagementObject(jobPath);
            
            Console.WriteLine($"[JOB] Waiting for {operation} job to complete: {jobPath}");
            
            // Wait for job completion (max 60 seconds)
            for (int i = 0; i < 60; i++)
            {
                job.Get();
                var jobState = (ushort)job["JobState"];
                var percentComplete = job["PercentComplete"]?.ToString() ?? "unknown";
                var statusDescriptions = job["StatusDescriptions"] as string[];
                var status = statusDescriptions?.FirstOrDefault() ?? "unknown";
                
                Console.WriteLine($"[JOB] Iteration {i+1}: JobState={jobState}, PercentComplete={percentComplete}%, Status='{status}'");
                
                // Log all job properties for debugging
                if (i == 0) // Only on first iteration to avoid spam
                {
                    Console.WriteLine($"[JOB] All job properties:");
                    foreach (PropertyData prop in job.Properties)
                    {
                        try
                        {
                            var value = prop.Value?.ToString() ?? "null";
                            Console.WriteLine($"[JOB]   {prop.Name}: {value}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[JOB]   {prop.Name}: Error reading - {ex.Message}");
                        }
                    }
                }
                
                // JobState: 2 = New, 3 = Starting, 4 = Running, 7 = Completed, 8 = Terminated, 9 = Killed, 10 = Exception
                if (jobState == 7) // Completed
                {
                    Console.WriteLine($"[JOB] {operation} job completed successfully");
                    
                    // Log final job result
                    try
                    {
                        var jobResult = job["Result"]?.ToString();
                        var jobOutput = job["Output"]?.ToString();
                        Console.WriteLine($"[JOB] Final result: {jobResult}");
                        Console.WriteLine($"[JOB] Final output: {jobOutput}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JOB] Error reading final result: {ex.Message}");
                    }
                    
                    return;
                }
                else if (jobState == 8 || jobState == 9 || jobState == 10) // Failed states
                {
                    var errorDescription = job["ErrorDescription"]?.ToString() ?? "Unknown error";
                    var errorCode = job["ErrorCode"]?.ToString() ?? "unknown";
                    Console.WriteLine($"[JOB] Job failed with state {jobState}. ErrorCode: {errorCode}, Error: {errorDescription}");
                    
                    // Try to get more error details
                    try
                    {
                        var errors = job["Errors"] as ManagementBaseObject[];
                        if (errors != null)
                        {
                            Console.WriteLine($"[JOB] Detailed errors ({errors.Length}):");
                            foreach (var error in errors)
                            {
                                Console.WriteLine($"[JOB]   Error: {error}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[JOB] Error reading detailed errors: {ex.Message}");
                    }
                    
                    throw new InvalidOperationException($"VM {operation} job failed: {errorDescription} (Code: {errorCode})");
                }
                
                System.Threading.Thread.Sleep(1000); // Wait 1 second
            }
            
            throw new InvalidOperationException($"VM {operation} job timed out after 60 seconds");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[JOB] Error waiting for {operation} job: {ex.Message}");
            throw;
        }
    }
    
    private string GetVmStateString(ushort enabledState)
    {
        return enabledState switch
        {
            0 => "Unknown",
            2 => "Running",
            3 => "Off",
            6 => "Saved",
            9 => "Paused",
            10 => "Starting",
            32768 => "Paused-Critical",
            32769 => "Saved-Critical",
            _ => $"State-{enabledState}"
        };
    }
    
    private string GetHealthStateString(ushort healthState)
    {
        return healthState switch
        {
            5 => "OK",
            10 => "Degraded/Warning",
            15 => "Minor failure",
            20 => "Major failure",
            25 => "Critical failure",
            30 => "Non-recoverable error",
            _ => $"Health-{healthState}"
        };
    }
}
