using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using HyperV.Contracts.Models;

namespace HyperV.Core.Wmi.Services;

/// <summary>VM Creation Service using Hyper-V WMI API for proper VM registration.</summary>
public sealed class VmCreationService
{
    /// <summary>Creates a VM using Hyper-V WMI API that appears in Hyper-V Manager.</summary>
    public string CreateHyperVVm(string id, CreateVmRequest req)
    {
        try
        {
            Console.WriteLine($"Creating Hyper-V VM using PowerShell fallback: {id}");
            
            // Determine VHD path - use provided path or create default
            var vhdPath = req.VhdPath;
            if (string.IsNullOrEmpty(vhdPath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                vhdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "HyperV.Agent", "VHDs", $"{id}-{timestamp}.vhdx");
            }
            
            // Create VHD if it doesn't exist
            if (!File.Exists(vhdPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(vhdPath)!);
                CreateVhdWithPowerShell(vhdPath, (uint)req.DiskSizeGB);
            }
            
            // Use PowerShell to create the VM with enhanced parameters
            var vmName = req.Name;
            var memoryBytes = (ulong)req.MemoryMB * 1024 * 1024;
            var cpuCount = req.CpuCount;
            var generation = req.Generation;
            
            // Build PowerShell command step by step
            var psCommands = new List<string>();
            
            // Create VM without VHD initially, then add it manually for better control
            psCommands.Add($"New-VM -Name '{vmName}' -MemoryStartupBytes {memoryBytes} -Generation {generation} -NoVHD");
            
            // Add the VHD to the VM
            psCommands.Add($"Add-VMHardDiskDrive -VMName '{vmName}' -Path '{vhdPath}'");
            
            // Set CPU count
            psCommands.Add($"Set-VMProcessor -VMName '{vmName}' -Count {cpuCount}");
            
            // Configure Secure Boot for Generation 2 VMs
            if (generation == 2)
            {
                var secureBootState = req.SecureBoot ? "On" : "Off";
                psCommands.Add($"Set-VMFirmware -VMName '{vmName}' -EnableSecureBoot {secureBootState}");
            }
            
            // Remove default network adapter and add custom one if switch specified
            psCommands.Add($"Remove-VMNetworkAdapter -VMName '{vmName}' -Name 'Network Adapter'");
            if (!string.IsNullOrEmpty(req.SwitchName))
            {
                psCommands.Add($"Add-VMNetworkAdapter -VMName '{vmName}' -SwitchName '{req.SwitchName}' -Name 'Network Adapter'");
            }
            
            // Add notes if specified
            if (!string.IsNullOrEmpty(req.Notes))
            {
                var escapedNotes = req.Notes.Replace("'", "''").Replace("`", "``");
                psCommands.Add($"Set-VM -VMName '{vmName}' -Notes '{escapedNotes}'");
            }
            
            var psCommand = string.Join("; ", psCommands);
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"{psCommand}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(60000);
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"PowerShell VM creation failed: {error}\nOutput: {output}");
                }
                
                Console.WriteLine($"PowerShell VM creation output: {output}");
            }
            
            // Connect to WMI to get VM path for consistency
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            var vmPath = FindVmByName(scope, vmName);
            
            if (string.IsNullOrEmpty(vmPath))
            {
                throw new InvalidOperationException("VM was created but could not be found via WMI");
            }
            
            Console.WriteLine($"VM created successfully: {vmPath}");
            
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                Id = id,
                Name = req.Name,
                Status = "Created Successfully",
                VhdPath = vhdPath,
                VmPath = vmPath,
                Note = "VM created using PowerShell and should be visible in Hyper-V Manager"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Hyper-V VM: {ex.Message}");
            throw new InvalidOperationException($"Hyper-V VM creation failed: {ex.Message}", ex);
        }
    }
    
    private string CreateVmSettingsXml(string name, int memoryMB, int cpuCount)
    {
        // Minimal WMI VM settings - only essential properties
        return $@"
<INSTANCE CLASSNAME=""Msvm_VirtualSystemSettingData"">
    <PROPERTY NAME=""ElementName"" TYPE=""string"">
        <VALUE>{name}</VALUE>
    </PROPERTY>
    <PROPERTY NAME=""VirtualSystemType"" TYPE=""string"">
        <VALUE>Microsoft:Hyper-V:System:Realized</VALUE>
    </PROPERTY>
</INSTANCE>";
    }
    
    private void ConfigureVmResources(ManagementScope scope, ManagementObject vm, int memoryMB, int cpuCount)
    {
        try
        {
            Console.WriteLine($"Configuring VM resources: Memory={memoryMB}MB, CPU={cpuCount}");
            
            // Get VM settings
            var settingsQuery = $"ASSOCIATORS OF {{{vm.Path}}} WHERE AssocClass = Msvm_SettingsDefineState ResultClass = Msvm_VirtualSystemSettingData";
            using var settingsSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(settingsQuery));
            
            ManagementObject? vmSettings = null;
            foreach (ManagementObject obj in settingsSearcher.Get())
            {
                vmSettings = obj;
                break;
            }
            
            if (vmSettings == null)
            {
                Console.WriteLine("Could not find VM settings to configure resources");
                return;
            }
            
            // Configure memory
            var memoryQuery = $"ASSOCIATORS OF {{{vmSettings.Path}}} WHERE AssocClass = Msvm_VirtualSystemSettingDataComponent ResultClass = Msvm_MemorySettingData";
            using var memorySearcher = new ManagementObjectSearcher(scope, new ObjectQuery(memoryQuery));
            
            foreach (ManagementObject memorySettings in memorySearcher.Get())
            {
                memorySettings["VirtualQuantity"] = (ulong)memoryMB;
                memorySettings["Reservation"] = (ulong)memoryMB;
                memorySettings["Limit"] = (ulong)memoryMB;
                memorySettings.Put();
                Console.WriteLine($"Memory configured to {memoryMB}MB");
                break;
            }
            
            // Configure CPU
            var cpuQuery = $"ASSOCIATORS OF {{{vmSettings.Path}}} WHERE AssocClass = Msvm_VirtualSystemSettingDataComponent ResultClass = Msvm_ProcessorSettingData";
            using var cpuSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(cpuQuery));
            
            foreach (ManagementObject cpuSettings in cpuSearcher.Get())
            {
                cpuSettings["VirtualQuantity"] = (ulong)cpuCount;
                cpuSettings["Reservation"] = (ulong)cpuCount;
                cpuSettings["Limit"] = (ulong)cpuCount;
                cpuSettings.Put();
                Console.WriteLine($"CPU configured to {cpuCount} cores");
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring VM resources: {ex.Message}");
            // Don't throw - this is not critical for VM creation
        }
    }
    
    private void AttachVhdToVm(ManagementScope scope, ManagementObject vm, string vhdPath)
    {
        try
        {
            Console.WriteLine($"Attaching VHD to VM: {vhdPath}");
            
            // Get VM's SCSI controller
            var scsiQuery = $"ASSOCIATORS OF {{{vm.Path}}} WHERE AssocClass = Msvm_SystemDevice ResultClass = Msvm_ResourceAllocationSettingData";
            using var scsiSearcher = new ManagementObjectSearcher(scope, new ObjectQuery(scsiQuery));
            
            ManagementObject? scsiController = null;
            foreach (ManagementObject obj in scsiSearcher.Get())
            {
                if (obj["ResourceSubType"]?.ToString() == "Microsoft:Hyper-V:Synthetic SCSI Controller")
                {
                    scsiController = obj;
                    break;
                }
            }
            
            if (scsiController == null)
            {
                Console.WriteLine("No SCSI controller found, VM created without disk attachment");
                return;
            }
            
            // Create VHD attachment settings
            using var managementService = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemManagementService"), null);
            using var managementServiceInstance = managementService.GetInstances().Cast<ManagementObject>().First();
            
            var diskSettings = CreateDiskSettingsXml(vhdPath, scsiController["InstanceID"]?.ToString());
            
            var inParams = managementServiceInstance.GetMethodParameters("AddResourceSettings");
            inParams["AffectedSystem"] = vm.Path.Path;
            inParams["ResourceSettings"] = new string[] { diskSettings };
            
            var result = managementServiceInstance.InvokeMethod("AddResourceSettings", inParams, null);
            var returnValue = (uint)result["ReturnValue"];
            
            if (returnValue == 0)
            {
                Console.WriteLine("VHD attached successfully to VM");
            }
            else
            {
                Console.WriteLine($"Failed to attach VHD. Return value: {returnValue}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error attaching VHD: {ex.Message}");
        }
    }
    
    private string CreateDiskSettingsXml(string vhdPath, string? parentInstanceId)
    {
        return $@"
<INSTANCE CLASSNAME=""Msvm_StorageAllocationSettingData"">
    <PROPERTY NAME=""ResourceType"" TYPE=""uint16"">
        <VALUE>31</VALUE>
    </PROPERTY>
    <PROPERTY NAME=""ResourceSubType"" TYPE=""string"">
        <VALUE>Microsoft:Hyper-V:Virtual Hard Disk</VALUE>
    </PROPERTY>
    <PROPERTY NAME=""Parent"" TYPE=""string"">
        <VALUE>{parentInstanceId}</VALUE>
    </PROPERTY>
    <PROPERTY NAME=""Connection"" TYPE=""string"" ISARRAY=""true"">
        <VALUE.ARRAY>
            <VALUE>{vhdPath}</VALUE>
        </VALUE.ARRAY>
    </PROPERTY>
    <PROPERTY NAME=""Address"" TYPE=""string"">
        <VALUE>0</VALUE>
    </PROPERTY>
</INSTANCE>";
    }
    
    private void CreateVhdWithPowerShell(string path, uint sizeGb)
    {
        try
        {
            Console.WriteLine($"Creating VHDX with PowerShell: {path}");
            
            // Delete existing file if it exists
            if (File.Exists(path))
            {
                Console.WriteLine($"Deleting existing VHDX file: {path}");
                File.Delete(path);
            }
            
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-Command \"New-VHD -Path '{path}' -SizeBytes {(ulong)sizeGb * 1024 * 1024 * 1024} -Dynamic\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                process.WaitForExit(30000);
                
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                
                if (process.ExitCode == 0 && File.Exists(path))
                {
                    Console.WriteLine($"VHDX created successfully: {path}");
                }
                else
                {
                    throw new InvalidOperationException($"PowerShell VHD creation failed: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create VHDX: {ex.Message}", ex);
        }
    }
    
    private void WaitForJob(ManagementScope scope, string jobPath)
    {
        try
        {
            using var job = new ManagementObject(jobPath);
            
            // Wait for job completion (max 60 seconds)
            for (int i = 0; i < 60; i++)
            {
                job.Get();
                var jobState = (ushort)job["JobState"];
                var percentComplete = job["PercentComplete"]?.ToString() ?? "Unknown";
                
                Console.WriteLine($"Job state: {jobState}, Progress: {percentComplete}%");
                
                // JobState: 2 = New, 3 = Starting, 4 = Running, 7 = Completed, 8 = Terminated, 9 = Killed, 10 = Exception
                if (jobState == 7) // Completed
                {
                    Console.WriteLine("VM creation job completed successfully");
                    return;
                }
                else if (jobState == 8 || jobState == 9 || jobState == 10) // Failed states
                {
                    var errorDescription = job["ErrorDescription"]?.ToString() ?? "Unknown error";
                    var errorCode = job["ErrorCode"]?.ToString() ?? "Unknown";
                    var errorSummaryDescription = job["ErrorSummaryDescription"]?.ToString() ?? "";
                    
                    Console.WriteLine($"Job failed with state {jobState}");
                    Console.WriteLine($"Error Code: {errorCode}");
                    Console.WriteLine($"Error Description: {errorDescription}");
                    Console.WriteLine($"Error Summary: {errorSummaryDescription}");
                    
                    // Don't throw exception immediately - this might be an environment issue
                    // Log the error but continue with VM creation attempt
                    Console.WriteLine("Job failed but continuing with VM creation attempt...");
                    return;
                }
                
                System.Threading.Thread.Sleep(1000); // Wait 1 second
            }
            
            Console.WriteLine("VM creation job timed out, but continuing with VM creation attempt...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error waiting for job: {ex.Message}");
            Console.WriteLine("Job monitoring failed but continuing with VM creation attempt...");
        }
    }
    
    private string? FindVmByName(ManagementScope scope, string vmName)
    {
        try
        {
            var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}'";
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(query));
            
            foreach (ManagementObject vm in searcher.Get())
            {
                return vm.Path.Path;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error finding VM by name: {ex.Message}");
            return null;
        }
    }
}
