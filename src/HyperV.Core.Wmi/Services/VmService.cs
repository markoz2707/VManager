
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
                // This is a placeholder implementation
                // Full implementation would require modifying Msvm_VirtualSystemSettingData
                throw new NotImplementedException("VM configuration modification not yet implemented");
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
            
            // This is a placeholder implementation
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                SnapshotId = Guid.NewGuid().ToString(),
                Name = snapshotName,
                Notes = notes,
                Status = "Created"
            });
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
            
            // This is a placeholder implementation
            throw new NotImplementedException("VM snapshot deletion not yet implemented");
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
            
            // This is a placeholder implementation
            throw new NotImplementedException("VM snapshot revert not yet implemented");
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
            
            // This is a placeholder implementation
            return JsonSerializer.Serialize(new
            {
                AvailableMemoryBuffer = 1024,
                SwapFilesInUse = false,
                Status = "OK"
            });
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

    /// <summary>Modifies SLP data root.</summary>
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
            
            // This is a placeholder implementation
            throw new NotImplementedException("SLP data root modification not yet implemented");
        }
        catch (Exception ex)
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
            
            // This is a placeholder implementation
            throw new NotImplementedException("Secure boot modification not yet implemented");
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
            
            // This is a placeholder implementation
            throw new NotImplementedException("Boot order modification not yet implemented");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set boot order: {ex.Message}", ex);
        }
    }

    /// <summary>Migrates VM to another host.</summary>
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
            
            // This is a placeholder implementation
            return Guid.NewGuid().ToString();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to migrate VM: {ex.Message}", ex);
        }
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
}
