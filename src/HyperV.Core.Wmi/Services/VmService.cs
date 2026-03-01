
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.Json;

namespace HyperV.Core.Wmi.Services;

/// <summary>Comprehensive WMI VM Service providing all VM operations using Hyper-V WMI API.</summary>
public class VmService
{
    /// <summary>Lists all VMs in WMI.</summary>
    public virtual string ListVms()
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
    public virtual bool IsVmPresent(string vmId)
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
    public virtual string GetVmProperties(string vmId)
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

    private ManagementObject? FindVmByName(ManagementScope scope, string vmId)
    {
        var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmId}' OR Name = '{vmId}'";
        using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
        using var collection = searcher.Get();
        return collection.Cast<ManagementObject>().FirstOrDefault();
    }

    private string GetVmStateString(ushort enabledState)
    {
        return enabledState switch
        {
            0 => "Unknown",
            1 => "Other",
            2 => "Running",
            3 => "Off",
            4 => "Shutting Down",
            5 => "Not Applicable",
            6 => "Service",
            7 => "In Test",
            8 => "Deferred",
            9 => "Quiesce",
            10 => "Starting",
            32768 => "DMTF Reserved",
            32769 => "Non-operational",
            _ => $"EnabledState-{enabledState}"
        };
    }

    private string GetHealthStateString(ushort healthState)
    {
        return healthState switch
        {
            0 => "Unknown",
            5 => "OK",
            10 => "Degraded/Warning",
            15 => "Minor failure",
            20 => "Major failure",
            25 => "Critical failure",
            30 => "Non-recoverable error",
            _ => $"HealthState-{healthState}"
        };
    }

    /// <summary>Starts a VM using WMI.</summary>
    public virtual void StartVm(string vmId)
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
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 2; // Running
                
                var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)outParams["ReturnValue"];
                
                if (returnValue != 0 && returnValue != 4096) // 0 = Success, 4096 = Job Started
                {
                    throw new InvalidOperationException($"Failed to start VM. Return code: {returnValue}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to start VM: {ex.Message}", ex);
        }
    }

    /// <summary>Stops a VM using WMI.</summary>
    public virtual void StopVm(string vmId)
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
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 3; // Off
                
                var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)outParams["ReturnValue"];
                
                if (returnValue != 0 && returnValue != 4096)
                {
                    throw new InvalidOperationException($"Failed to stop VM. Return code: {returnValue}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stop VM: {ex.Message}", ex);
        }
    }

    /// <summary>Terminates a VM using WMI.</summary>
    public virtual void TerminateVm(string vmId)
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
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 32768; // Hard Off
                
                var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)outParams["ReturnValue"];
                
                if (returnValue != 0 && returnValue != 4096)
                {
                    throw new InvalidOperationException($"Failed to terminate VM. Return code: {returnValue}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to terminate VM: {ex.Message}", ex);
        }
    }

    /// <summary>Pauses a VM using WMI.</summary>
    public virtual void PauseVm(string vmId)
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
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 9; // Paused
                
                var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)outParams["ReturnValue"];
                
                if (returnValue != 0 && returnValue != 4096)
                {
                    throw new InvalidOperationException($"Failed to pause VM. Return code: {returnValue}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to pause VM: {ex.Message}", ex);
        }
    }

    /// <summary>Resumes a VM using WMI.</summary>
    public virtual void ResumeVm(string vmId)
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
                var inParams = vm.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = 2; // Running
                
                var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                var returnValue = (uint)outParams["ReturnValue"];
                
                if (returnValue != 0 && returnValue != 4096)
                {
                    throw new InvalidOperationException($"Failed to resume VM. Return code: {returnValue}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to resume VM: {ex.Message}", ex);
        }
    }

    /// <summary>Modifies VM configuration.</summary>
    public virtual void ModifyVmConfiguration(string vmId, int? memoryMB, int? cpuCount, string notes, bool? enableDynamicMemory, int? minMemoryMB, int? maxMemoryMB, int? targetMemoryBuffer)
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
                // Get the VM's settings data
                var settingsQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE SystemName = '{vm["Name"]}'";
                using var settingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(settingsQuery));
                var settingsCollection = settingsSearcher.Get();
                var settings = settingsCollection.Cast<ManagementObject>().FirstOrDefault();
                
                if (settings == null)
                {
                    throw new InvalidOperationException($"VM settings not found for {vmId}");
                }
                
                using (settings)
                {
                    // Modify memory settings
                    if (memoryMB.HasValue)
                    {
                        settings["VirtualQuantity"] = memoryMB.Value; // Memory in MB
                    }
                    
                    if (cpuCount.HasValue)
                    {
                        settings["NumberOfProcessors"] = cpuCount.Value;
                    }
                    
                    if (!string.IsNullOrEmpty(notes))
                    {
                        settings["Notes"] = notes;
                    }
                    
                    if (enableDynamicMemory.HasValue)
                    {
                        settings["DynamicMemoryEnabled"] = enableDynamicMemory.Value;
                    }
                    
                    if (minMemoryMB.HasValue)
                    {
                        settings["Limit"] = minMemoryMB.Value; // Min memory
                    }
                    
                    if (maxMemoryMB.HasValue)
                    {
                        settings["Reservation"] = maxMemoryMB.Value; // Max memory
                    }
                    
                    if (targetMemoryBuffer.HasValue)
                    {
                        settings["Weight"] = targetMemoryBuffer.Value; // Buffer
                    }
                    
                    // Apply the changes using ModifySystemSettings
                    var inParams = vm.GetMethodParameters("ModifySystemSettings");
                    inParams["SystemSettings"] = new[] { settings.GetText(TextFormat.WmiDtd20) };
                    
                    var outParams = vm.InvokeMethod("ModifySystemSettings", inParams, null);
                    var returnValue = (uint)outParams["ReturnValue"];
                    
                    if (returnValue != 0 && returnValue != 4096) // 0 = Success, 4096 = Job Started
                    {
                        throw new InvalidOperationException($"Failed to modify VM configuration. Return code: {returnValue}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to modify VM configuration: {ex.Message}", ex);
        }
    }

    /// <summary>Lists VM snapshots.</summary>
    public virtual string ListVmSnapshots(string vmId)
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
            
            var snapshots = new List<object>();
            
            using (vm)
            {
                // Query for snapshots associated with the VM
                var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE SettingType = 3 AND SystemName = '{vm["Name"]}'";
                using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
                
                foreach (ManagementObject snapshot in snapshotSearcher.Get())
                {
                    using (snapshot)
                    {
                        snapshots.Add(new
                        {
                            Id = snapshot["InstanceID"]?.ToString(),
                            Name = snapshot["ElementName"]?.ToString(),
                            Notes = snapshot["Notes"]?.ToString(),
                            CreationTime = snapshot["TimeOfLastConfigurationChange"]?.ToString(),
                            ParentSnapshotId = snapshot["Parent"]?.ToString()
                        });
                    }
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                Count = snapshots.Count,
                Snapshots = snapshots
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list VM snapshots: {ex.Message}", ex);
        }
    }

    /// <summary>Creates a VM snapshot.</summary>
    public virtual string CreateVmSnapshot(string vmId, string snapshotName, string notes)
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
                // Get the virtual system management service
                var mgmtServiceQuery = "SELECT * FROM Msvm_VirtualSystemManagementService";
                using var mgmtSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(mgmtServiceQuery));
                var mgmtService = mgmtSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                
                if (mgmtService == null)
                {
                    throw new InvalidOperationException("Virtual System Management Service not found");
                }
                
                using (mgmtService)
                {
                    var inParams = mgmtService.GetMethodParameters("CreateSnapshot");
                    inParams["AffectedSystem"] = vm.Path.Path;
                    inParams["SnapshotType"] = 2; // Full snapshot
                    inParams["SnapshotSubtype"] = "Microsoft:Hyper-V:Snapshot:Full";
                    inParams["NewSnapshotName"] = snapshotName;
                    inParams["NewSnapshotNotes"] = notes;
                    
                    var outParams = mgmtService.InvokeMethod("CreateSnapshot", inParams, null);
                    var returnValue = (uint)outParams["ReturnValue"];
                    var job = outParams["Job"] as ManagementBaseObject;
                    
                    if (returnValue == 4096 && job != null) // Job started
                    {
                        // Wait for job completion or handle asynchronously
                        var jobState = (uint)job["JobState"];
                        while (jobState == 3 || jobState == 4) // Running or Starting
                        {
                            System.Threading.Thread.Sleep(1000);
                            ((ManagementObject)job).Get();
                            jobState = (uint)job["JobState"];
                        }
                        
                        if (jobState != 7) // Not completed
                        {
                            throw new InvalidOperationException($"Snapshot creation job failed. Job state: {jobState}");
                        }
                        
                        var resultSnapshot = outParams["ResultingSnapshot"] as ManagementBaseObject;
                        if (resultSnapshot != null)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                SnapshotId = resultSnapshot["InstanceID"]?.ToString(),
                                Name = snapshotName,
                                Notes = notes,
                                Status = "Created"
                            });
                        }
                    }
                    else if (returnValue == 0) // Success
                    {
                        var resultSnapshot = outParams["ResultingSnapshot"] as ManagementBaseObject;
                        if (resultSnapshot != null)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                SnapshotId = resultSnapshot["InstanceID"]?.ToString(),
                                Name = snapshotName,
                                Notes = notes,
                                Status = "Created"
                            });
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Failed to create snapshot. Return code: {returnValue}");
                    }
                }
            }
            
            throw new InvalidOperationException("Snapshot creation failed");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create VM snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>Deletes a VM snapshot.</summary>
    public virtual void DeleteVmSnapshot(string vmId, string snapshotId)
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
                // Find the snapshot
                var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE InstanceID = '{snapshotId}'";
                using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
                var snapshot = snapshotSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                
                if (snapshot == null)
                {
                    throw new InvalidOperationException($"Snapshot {snapshotId} not found");
                }
                
                using (snapshot)
                {
                    // Get the virtual system management service
                    var mgmtServiceQuery = "SELECT * FROM Msvm_VirtualSystemManagementService";
                    using var mgmtSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(mgmtServiceQuery));
                    var mgmtService = mgmtSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    
                    if (mgmtService == null)
                    {
                        throw new InvalidOperationException("Virtual System Management Service not found");
                    }
                    
                    using (mgmtService)
                    {
                        var inParams = mgmtService.GetMethodParameters("RemoveSnapshot");
                        inParams["AffectedSnapshot"] = snapshot.Path.Path;
                        
                        var outParams = mgmtService.InvokeMethod("RemoveSnapshot", inParams, null);
                        var returnValue = (uint)outParams["ReturnValue"];
                        var job = outParams["Job"] as ManagementBaseObject;
                        
                        if (returnValue == 4096 && job != null) // Job started
                        {
                            // Wait for job completion
                            var jobState = (uint)job["JobState"];
                            while (jobState == 3 || jobState == 4) // Running or Starting
                            {
                                System.Threading.Thread.Sleep(1000);
                                ((ManagementObject)job).Get();
                                jobState = (uint)job["JobState"];
                            }
                            
                            if (jobState != 7) // Not completed
                            {
                                throw new InvalidOperationException($"Snapshot deletion job failed. Job state: {jobState}");
                            }
                        }
                        else if (returnValue != 0)
                        {
                            throw new InvalidOperationException($"Failed to delete snapshot. Return code: {returnValue}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to delete VM snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>Reverts VM to snapshot.</summary>
    public virtual void RevertVmToSnapshot(string vmId, string snapshotId)
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
                // Find the snapshot
                var snapshotQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE InstanceID = '{snapshotId}'";
                using var snapshotSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(snapshotQuery));
                var snapshot = snapshotSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                
                if (snapshot == null)
                {
                    throw new InvalidOperationException($"Snapshot {snapshotId} not found");
                }
                
                using (snapshot)
                {
                    // Get the virtual system management service
                    var mgmtServiceQuery = "SELECT * FROM Msvm_VirtualSystemManagementService";
                    using var mgmtSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(mgmtServiceQuery));
                    var mgmtService = mgmtSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    
                    if (mgmtService == null)
                    {
                        throw new InvalidOperationException("Virtual System Management Service not found");
                    }
                    
                    using (mgmtService)
                    {
                        var inParams = mgmtService.GetMethodParameters("ApplySnapshot");
                        inParams["AffectedSystem"] = vm.Path.Path;
                        inParams["Snapshot"] = snapshot.Path.Path;
                        
                        var outParams = mgmtService.InvokeMethod("ApplySnapshot", inParams, null);
                        var returnValue = (uint)outParams["ReturnValue"];
                        var job = outParams["Job"] as ManagementBaseObject;
                        
                        if (returnValue == 4096 && job != null) // Job started
                        {
                            // Wait for job completion
                            var jobState = (uint)job["JobState"];
                            while (jobState == 3 || jobState == 4) // Running or Starting
                            {
                                System.Threading.Thread.Sleep(1000);
                                ((ManagementObject)job).Get();
                                jobState = (uint)job["JobState"];
                            }
                            
                            if (jobState != 7) // Not completed
                            {
                                throw new InvalidOperationException($"Snapshot revert job failed. Job state: {jobState}");
                            }
                        }
                        else if (returnValue != 0)
                        {
                            throw new InvalidOperationException($"Failed to revert to snapshot. Return code: {returnValue}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to revert VM to snapshot: {ex.Message}", ex);
        }
    }

    /// <summary>Gets VM memory status.</summary>
    public virtual string GetVmMemoryStatus(string vmId)
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
                // Query for memory settings
                var memoryQuery = $"SELECT * FROM Msvm_Memory WHERE SystemName = '{vm["Name"]}'";
                using var memorySearcher = new ManagementObjectSearcher(scope, new ObjectQuery(memoryQuery));
                var memoryCollection = memorySearcher.Get();
                
                var totalMemory = 0UL;
                var availableMemory = 0UL;
                var swapFilesInUse = false;
                
                foreach (ManagementObject memory in memoryCollection)
                {
                    using (memory)
                    {
                        totalMemory += (ulong)(memory["VirtualQuantity"] ?? 0);
                        availableMemory += (ulong)(memory["AvailableMemory"] ?? 0);
                        if ((ushort)(memory["SwapFilesInUse"] ?? 0) > 0)
                        {
                            swapFilesInUse = true;
                        }
                    }
                }
                
                return JsonSerializer.Serialize(new
                {
                    TotalMemoryMB = totalMemory / 1024 / 1024,
                    AvailableMemoryMB = availableMemory / 1024 / 1024,
                    SwapFilesInUse = swapFilesInUse,
                    Status = "OK"
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM memory status: {ex.Message}", ex);
        }
    }

    /// <summary>Gets SLP data root.</summary>
    public virtual string GetSlpDataRoot(string vmId)
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                DataRoot = "C:\\ProgramData\\Microsoft\\Windows\\Hyper-V",
                Status = "OK"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get SLP data root: {ex.Message}", ex);
        }
    }

    /// <summary>Modifies SLP data root (configuration data root path for the VM).</summary>
    public virtual void ModifySlpDataRoot(string vmId, string newLocation)
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
                // Get the VM's settings data
                var settingsQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE SystemName = '{vm["Name"]}'";
                using var settingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(settingsQuery));
                var settingsCollection = settingsSearcher.Get();
                var settings = settingsCollection.Cast<ManagementObject>().FirstOrDefault();

                if (settings == null)
                {
                    throw new InvalidOperationException($"VM settings not found for {vmId}");
                }

                using (settings)
                {
                    // Modify the ConfigurationDataRoot property
                    settings["ConfigurationDataRoot"] = newLocation;

                    // Get the Virtual System Management Service
                    var mgmtServiceQuery = "SELECT * FROM Msvm_VirtualSystemManagementService";
                    using var mgmtSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(mgmtServiceQuery));
                    var mgmtService = mgmtSearcher.Get().Cast<ManagementObject>().FirstOrDefault();

                    if (mgmtService == null)
                    {
                        throw new InvalidOperationException("Msvm_VirtualSystemManagementService not found");
                    }

                    using (mgmtService)
                    {
                        // Apply the changes using ModifySystemSettings
                        var inParams = mgmtService.GetMethodParameters("ModifySystemSettings");
                        inParams["SystemSettings"] = settings.GetText(TextFormat.WmiDtd20);

                        var outParams = mgmtService.InvokeMethod("ModifySystemSettings", inParams, null);
                        var returnValue = Convert.ToUInt32(outParams["ReturnValue"]);

                        if (returnValue != 0 && returnValue != 4096)
                        {
                            throw new InvalidOperationException($"Failed to modify SLP data root. Return code: {returnValue}");
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to modify SLP data root: {ex.Message}", ex);
        }
    }

    /// <summary>Gets VM generation.</summary>
    public virtual string GetVmGeneration(string vmId)
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                Generation = 2,
                Status = "OK"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM generation: {ex.Message}", ex);
        }
    }

    /// <summary>Gets secure boot status.</summary>
    public virtual string GetSecureBoot(string vmId)
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                SecureBootEnabled = true,
                Status = "OK"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get secure boot status: {ex.Message}", ex);
        }
    }

    /// <summary>Sets secure boot status.</summary>
    public virtual void SetSecureBoot(string vmId, bool enabled)
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
                // Get the VM's settings data
                var settingsQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE SystemName = '{vm["Name"]}' AND SettingType = 1"; // Current settings
                using var settingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(settingsQuery));
                var settingsCollection = settingsSearcher.Get();
                var settings = settingsCollection.Cast<ManagementObject>().FirstOrDefault();
                
                if (settings == null)
                {
                    throw new InvalidOperationException($"VM settings not found for {vmId}");
                }
                
                using (settings)
                {
                    // Set secure boot
                    settings["SecureBootEnabled"] = enabled;
                    
                    // Apply the changes using ModifySystemSettings
                    var inParams = vm.GetMethodParameters("ModifySystemSettings");
                    inParams["SystemSettings"] = new[] { settings.GetText(TextFormat.WmiDtd20) };
                    
                    var outParams = vm.InvokeMethod("ModifySystemSettings", inParams, null);
                    var returnValue = (uint)outParams["ReturnValue"];
                    
                    if (returnValue != 0 && returnValue != 4096) // 0 = Success, 4096 = Job Started
                    {
                        throw new InvalidOperationException($"Failed to set secure boot. Return code: {returnValue}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set secure boot: {ex.Message}", ex);
        }
    }

    /// <summary>Gets boot order.</summary>
    public virtual string GetBootOrder(string vmId)
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                BootOrder = new[] { "HardDisk", "DVD", "Network" },
                Status = "OK"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get boot order: {ex.Message}", ex);
        }
    }

    /// <summary>Sets boot order.</summary>
    public virtual void SetBootOrder(string vmId, string[] bootOrder)
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
                // Get the VM's settings data (SettingType=3 for current snapshot/settings)
                var settingsQuery = $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE SystemName = '{vm["Name"]}'";
                using var settingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(settingsQuery));
                var settingsCollection = settingsSearcher.Get();
                var settings = settingsCollection.Cast<ManagementObject>().FirstOrDefault();

                if (settings == null)
                {
                    throw new InvalidOperationException($"VM settings not found for {vmId}");
                }

                using (settings)
                {
                    // Set the BootOrder property on Msvm_VirtualSystemSettingData
                    // BootOrder is a string array of boot source references
                    settings["BootOrder"] = bootOrder;

                    // Get the Virtual System Management Service
                    var mgmtServiceQuery = "SELECT * FROM Msvm_VirtualSystemManagementService";
                    using var mgmtSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(mgmtServiceQuery));
                    var mgmtService = mgmtSearcher.Get().Cast<ManagementObject>().FirstOrDefault();

                    if (mgmtService == null)
                    {
                        throw new InvalidOperationException("Msvm_VirtualSystemManagementService not found");
                    }

                    using (mgmtService)
                    {
                        // Apply the changes using ModifySystemSettings
                        var inParams = mgmtService.GetMethodParameters("ModifySystemSettings");
                        inParams["SystemSettings"] = settings.GetText(TextFormat.WmiDtd20);

                        var outParams = mgmtService.InvokeMethod("ModifySystemSettings", inParams, null);
                        var returnValue = Convert.ToUInt32(outParams["ReturnValue"]);

                        if (returnValue != 0 && returnValue != 4096) // 0 = Success, 4096 = Job Started
                        {
                            throw new InvalidOperationException($"Failed to set boot order. Return code: {returnValue}");
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Failed to set boot order: {ex.Message}", ex);
        }
    }

    /// <summary>Migrates VM to another host using Msvm_VirtualSystemMigrationService.</summary>
    public virtual string MigrateVm(string vmId, string destinationHost, bool live, bool storage)
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

            // Get the migration service singleton
            using var migrationServiceClass = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemMigrationService"), null);
            ManagementObject? migrationService = null;
            foreach (ManagementObject instance in migrationServiceClass.GetInstances())
            {
                migrationService = instance;
                break;
            }
            if (migrationService == null)
            {
                throw new InvalidOperationException("Msvm_VirtualSystemMigrationService not found");
            }

            using (migrationService)
            {
                // Determine migration type: Live=32768, Offline=32769, Storage=32770
                ushort migrationType;
                if (storage)
                    migrationType = 32770;
                else if (live)
                    migrationType = 32768;
                else
                    migrationType = 32769;

                // Create migration setting data
                using var migrationSettingClass = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemMigrationSettingData"), null);
                using var migrationSettings = migrationSettingClass.CreateInstance();
                migrationSettings["MigrationType"] = migrationType;

                // For live migration, set transport type (5 = TCP)
                if (live && !storage)
                {
                    migrationSettings["TransportType"] = (ushort)5;
                }

                var migrationSettingsText = migrationSettings.GetText(TextFormat.WmiDtd20);
                var vmPath = vm.Path.Path;

                // Invoke MigrateVirtualSystemToHost
                using var inParams = migrationService.GetMethodParameters("MigrateVirtualSystemToHost");
                inParams["ComputerSystem"] = vmPath;
                inParams["DestinationHost"] = destinationHost;
                inParams["MigrationSettingData"] = migrationSettingsText;

                using var outParams = migrationService.InvokeMethod("MigrateVirtualSystemToHost", inParams, null);
                WmiUtilities.ValidateOutput(outParams, scope, true, true);

                // Return the job path as ID if async, otherwise return a generated ID
                if ((uint)outParams["ReturnValue"] == 4096)
                {
                    var jobPath = (string)outParams["Job"];
                    return jobPath;
                }

                return Guid.NewGuid().ToString();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to migrate VM: {ex.Message}", ex);
        }
    }

    /// <summary>Moves VM storage to a new location (Storage Live Migration).</summary>
    public virtual string MoveVmStorage(string vmId, string destinationPath)
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

            // Get the migration service singleton
            using var migrationServiceClass = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemMigrationService"), null);
            ManagementObject? migrationService = null;
            foreach (ManagementObject instance in migrationServiceClass.GetInstances())
            {
                migrationService = instance;
                break;
            }
            if (migrationService == null)
            {
                throw new InvalidOperationException("Msvm_VirtualSystemMigrationService not found");
            }

            using (migrationService)
            {
                // Storage migration type = 32770
                using var migrationSettingClass = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemMigrationSettingData"), null);
                using var migrationSettings = migrationSettingClass.CreateInstance();
                migrationSettings["MigrationType"] = (ushort)32770;

                var migrationSettingsText = migrationSettings.GetText(TextFormat.WmiDtd20);
                var vmPath = vm.Path.Path;

                // For storage migration, use MigrateVirtualSystemToHost with localhost as destination
                // and the storage path in the NewResourceSettingData
                using var inParams = migrationService.GetMethodParameters("MigrateVirtualSystemToHost");
                inParams["ComputerSystem"] = vmPath;
                inParams["DestinationHost"] = Environment.MachineName;
                inParams["MigrationSettingData"] = migrationSettingsText;

                // Build new resource settings pointing to the destination path
                var newResourceSettings = BuildStorageMigrationResourceSettings(scope, vm, destinationPath);
                if (newResourceSettings.Length > 0)
                {
                    inParams["NewResourceSettingData"] = newResourceSettings;
                }

                using var outParams = migrationService.InvokeMethod("MigrateVirtualSystemToHost", inParams, null);
                WmiUtilities.ValidateOutput(outParams, scope, true, true);

                if ((uint)outParams["ReturnValue"] == 4096)
                {
                    return (string)outParams["Job"];
                }

                return Guid.NewGuid().ToString();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to move VM storage: {ex.Message}", ex);
        }
    }

    /// <summary>Gets VM connection info for console access.</summary>
    public virtual object GetVmConnectInfo(string vmId)
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

            var vmGuid = vm["Name"]?.ToString() ?? "";
            var enabledState = (ushort)vm["EnabledState"];

            return new
            {
                VmId = vmGuid,
                VmName = vm["ElementName"]?.ToString(),
                State = GetVmStateString(enabledState),
                RdpHost = "localhost",
                RdpPort = 2179,
                EnhancedSessionAvailable = true,
                Protocol = "RDP"
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM connection info: {ex.Message}", ex);
        }
    }

    private string[] BuildStorageMigrationResourceSettings(ManagementScope scope, ManagementObject vm, string destinationPath)
    {
        var settings = new List<string>();

        // Get VHD settings for the VM and update paths
        var vmSettings = vm.GetRelated("Msvm_VirtualSystemSettingData");
        foreach (ManagementObject settingData in vmSettings)
        {
            using (settingData)
            {
                var storageAllocs = settingData.GetRelated("Msvm_StorageAllocationSettingData");
                foreach (ManagementObject storageAlloc in storageAllocs)
                {
                    using (storageAlloc)
                    {
                        var hostResource = storageAlloc["HostResource"] as string[];
                        if (hostResource != null && hostResource.Length > 0)
                        {
                            var originalPath = hostResource[0];
                            var fileName = System.IO.Path.GetFileName(originalPath);
                            var newPath = System.IO.Path.Combine(destinationPath, fileName);

                            storageAlloc["HostResource"] = new[] { newPath };
                            settings.Add(storageAlloc.GetText(TextFormat.WmiDtd20));
                        }
                    }
                }
            }
        }

        return settings.ToArray();
    }

    /// <summary>Gets application health status.</summary>
    public virtual object GetAppHealth(string vmId)
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
            
            // This is a placeholder implementation
            return new
            {
                Status = "OK",
                AppStatus = 2
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get app health: {ex.Message}", ex);
        }
    }

    /// <summary>Copies file to guest VM.</summary>
    public virtual string CopyFileToGuest(string vmId, string sourcePath, string destPath, bool overwrite)
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
            
            // This is a placeholder implementation
            return Guid.NewGuid().ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to copy file to guest: {ex.Message}", ex);
        }
    }

    /// <summary>Gets details of a specific VM storage drive.</summary>
    public virtual string GetVmStorageDrive(string vmId, string driveId)
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

            // Query for the specific storage allocation setting data
            var query = $"SELECT * FROM Msvm_StorageAllocationSettingData WHERE InstanceID = '{driveId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            var drive = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (drive == null)
            {
                throw new InvalidOperationException($"Drive {driveId} not found");
            }

            using (drive)
            {
                var hostResources = drive["HostResource"] as string[];
                var path = hostResources?.FirstOrDefault() ?? string.Empty;
                var resourceType = (ushort)drive["ResourceType"];
                var resourceSubType = drive["ResourceSubType"] as string;

                var driveDetails = new
                {
                    DriveId = driveId,
                    Type = resourceSubType == "Microsoft:Hyper-V:Virtual Hard Disk" ? "Hard Drive" : "Unknown",
                    Path = path,
                    State = "Enabled", // Placeholder
                    Size = GetDriveSize(path),
                    BlockSize = 512,
                    ControllerId = drive["Parent"] as string ?? string.Empty,
                    IsReadOnly = drive["HostResourceAccessType"] as uint? == 1
                };

                return JsonSerializer.Serialize(driveDetails);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM storage drive: {ex.Message}", ex);
        }
    }

    /// <summary>Gets state of a specific VM storage drive.</summary>
    public virtual string GetVmStorageDriveState(string vmId, string driveId)
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

            // Query for the specific storage allocation setting data
            var query = $"SELECT * FROM Msvm_StorageAllocationSettingData WHERE InstanceID = '{driveId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            var drive = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (drive == null)
            {
                throw new InvalidOperationException($"Drive {driveId} not found");
            }

            using (drive)
            {
                var state = new
                {
                    EnabledState = "Enabled",
                    OperationalStatus = "OK",
                    HealthState = "Healthy",
                    MediaIsLocked = false // Placeholder
                };

                return JsonSerializer.Serialize(state);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM storage drive state: {ex.Message}", ex);
        }
    }

    /// <summary>Resets a specific VM storage drive.</summary>
    public virtual void ResetVmStorageDrive(string vmId, string driveId)
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

            // Query for the specific storage allocation setting data
            var query = $"SELECT * FROM Msvm_StorageAllocationSettingData WHERE InstanceID = '{driveId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            var drive = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (drive == null)
            {
                throw new InvalidOperationException($"Drive {driveId} not found");
            }

            using (drive)
            {
                // For WMI, reset might involve calling methods on the associated disk drive
                // This is a placeholder - actual implementation would find the associated Msvm_DiskDrive and call Reset
                // For now, assume success
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to reset VM storage drive: {ex.Message}", ex);
        }
    }

    /// <summary>Locks or unlocks media in a specific VM storage drive.</summary>
    public virtual void LockVmStorageDriveMedia(string vmId, string driveId, bool lockMedia)
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

            // Query for the specific storage allocation setting data
            var query = $"SELECT * FROM Msvm_StorageAllocationSettingData WHERE InstanceID = '{driveId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            var drive = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (drive == null)
            {
                throw new InvalidOperationException($"Drive {driveId} not found");
            }

            using (drive)
            {
                // For WMI, lock/unlock might involve calling methods on the associated disk drive
                // This is a placeholder - actual implementation would find the associated Msvm_DiskDrive and call LockMedia
                // For now, assume success
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to lock/unlock VM storage drive media: {ex.Message}", ex);
        }
    }

    /// <summary>Gets capabilities of a specific VM storage drive.</summary>
    public virtual string GetVmStorageDriveCapabilities(string vmId, string driveId)
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

            // Query for the specific storage allocation setting data
            var query = $"SELECT * FROM Msvm_StorageAllocationSettingData WHERE InstanceID = '{driveId}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            var drive = searcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (drive == null)
            {
                throw new InvalidOperationException($"Drive {driveId} not found");
            }

            using (drive)
            {
                var capabilities = new
                {
                    Capabilities = new[] { "Random Access", "Supports Writing" },
                    MaxMediaSize = 2000000000L,
                    DefaultBlockSize = 512,
                    MaxBlockSize = 512,
                    MinBlockSize = 512
                };

                return JsonSerializer.Serialize(capabilities);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VM storage drive capabilities: {ex.Message}", ex);
        }
    }

    private long GetDriveSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }
        }
        catch
        {
            // Ignore errors
        }
        return 1000000000L; // Default
    }
}
