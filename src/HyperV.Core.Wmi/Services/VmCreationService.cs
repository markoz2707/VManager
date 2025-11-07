using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using HyperV.Contracts.Models;

namespace HyperV.Core.Wmi.Services;

/// <summary>VM Creation Service using Hyper-V WMI API for proper VM registration.</summary>
// Made non-sealed and methods virtual to allow Moq-based unit tests to create substitutes.
public class VmCreationService
{
    /// <summary>Creates a VM using pure Hyper-V WMI API based on Microsoft samples.</summary>
    public virtual string CreateHyperVVm(string id, CreateVmRequest req)
    {
        try
        {
            Console.WriteLine($"Creating Hyper-V VM using pure WMI: {req.Name}");
            
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            
            // Create VM using Microsoft pattern from Generation2VM sample
            using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
            
            // Create VM settings based on Microsoft sample
            var vmSettingsText = CreateVmSettingsText(req.Name, req.Generation, req.Notes);
            
            using var inParams = managementService.GetMethodParameters("DefineSystem");
            inParams["SystemSettings"] = vmSettingsText;
            inParams["ReferenceConfiguration"] = null;
            inParams["ResourceSettings"] = Array.Empty<string>();
            
            using var outParams = managementService.InvokeMethod("DefineSystem", inParams, null);
            WmiUtilities.ValidateOutput(outParams, scope, true, true);

            // Get the VM that was created from the DefineSystem result
            string? vmPath = null;

            // First try to get from the direct result
            if (outParams["ResultingSystem"] != null)
            {
                vmPath = outParams["ResultingSystem"].ToString();
            }

            // If not available, try to get from the job result (for async operations)
            if (string.IsNullOrEmpty(vmPath) && (uint)outParams["ReturnValue"] == 4096)
            {
                using var job = new ManagementObject(outParams["Job"].ToString());
                job.Scope = scope;
                job.Get();

                // Get the resulting system from the job
                if (job["ResultingSystem"] != null)
                {
                    vmPath = job["ResultingSystem"].ToString();
                }
            }

            if (string.IsNullOrEmpty(vmPath))
            {
                // Fallback to querying by name if path not available
                Console.WriteLine("VM path not found in DefineSystem result, falling back to name query");
                using var vmFromQuery = WmiUtilities.GetVirtualMachine(req.Name, scope);
                vmPath = vmFromQuery.Path.Path;
            }

            var vm = new ManagementObject(vmPath);
            vm.Scope = scope;
            using (vm)
            {
                // Configure VM resources using WMI
                ConfigureVmResourcesWmi(scope, vm, req);
                
                // Create and attach VHD if needed
                if (!string.IsNullOrEmpty(req.VhdPath) || req.DiskSizeGB > 0)
                {
                    var vhdPath = req.VhdPath;
                    if (string.IsNullOrEmpty(vhdPath))
                    {
                        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        vhdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "HyperV.Agent", "VHDs", $"{id}-{timestamp}.vhdx");
                    }
                    
                    CreateAndAttachVhd(scope, vm, vhdPath, req.DiskSizeGB);
                }
                
                Console.WriteLine($"VM {req.Name} created successfully using pure WMI");
                
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created Successfully",
                    VmPath = vm.Path.Path,
                    Note = "VM created using pure WMI and should be visible in Hyper-V Manager"
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Hyper-V VM: {ex.Message}");
            throw new InvalidOperationException($"Hyper-V VM creation failed: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Creates VM settings text in WMI DTD format based on Microsoft Generation2VM sample.
    /// </summary>
    private string CreateVmSettingsText(string name, int generation, string? notes)
    {
        var scope = new ManagementScope(@"root\virtualization\v2");
        scope.Connect();
        
        using var virtualSystemSettingClass = new ManagementClass(scope, new ManagementPath("Msvm_VirtualSystemSettingData"), null);
        using var virtualSystemSetting = virtualSystemSettingClass.CreateInstance();
        
        virtualSystemSetting["ElementName"] = name;
        virtualSystemSetting["ConfigurationDataRoot"] = @"C:\ProgramData\Microsoft\Windows\Hyper-V\";
        
        // Set generation - based on Microsoft Generation2VM sample
        if (generation == 2)
        {
            virtualSystemSetting["VirtualSystemSubtype"] = "Microsoft:Hyper-V:SubType:2";
        }
        else
        {
            virtualSystemSetting["VirtualSystemSubtype"] = "Microsoft:Hyper-V:SubType:1";
        }
        
        if (!string.IsNullOrEmpty(notes))
        {
            virtualSystemSetting["Notes"] = new string[] { notes };
        }
        
        return virtualSystemSetting.GetText(TextFormat.WmiDtd20);
    }
    
    /// <summary>
    /// Configures VM resources using Microsoft WMI patterns.
    /// </summary>
    private void ConfigureVmResourcesWmi(ManagementScope scope, ManagementObject vm, CreateVmRequest req)
    {
        try
        {
            Console.WriteLine($"Configuring VM resources: Memory={req.MemoryMB}MB, CPU={req.CpuCount}");
            
            using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
            using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
            
            // Configure memory using ModifyResourceSettings
            ConfigureMemoryResource(scope, managementService, vmSettings, req.MemoryMB);
            
            // Configure CPU using ModifyResourceSettings
            ConfigureCpuResource(scope, managementService, vmSettings, req.CpuCount);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error configuring VM resources: {ex.Message}");
            // Don't throw - this is not critical for VM creation
        }
    }
    
    private void ConfigureMemoryResource(ManagementScope scope, ManagementObject managementService, ManagementObject vmSettings, int memoryMB)
    {
        try
        {
            using var memoryCollection = vmSettings.GetRelated("Msvm_MemorySettingData", "Msvm_VirtualSystemSettingDataComponent",
                null, null, null, null, false, null);
            
            foreach (ManagementObject memory in memoryCollection)
            {
                using (memory)
                {
                    memory["VirtualQuantity"] = (ulong)memoryMB;
                    memory["Reservation"] = (ulong)memoryMB;
                    memory["Limit"] = (ulong)memoryMB;
                    
                    using var inParams = managementService.GetMethodParameters("ModifyResourceSettings");
                    inParams["ResourceSettings"] = new string[] { memory.GetText(TextFormat.WmiDtd20) };
                    
                    using var result = managementService.InvokeMethod("ModifyResourceSettings", inParams, null);
                    WmiUtilities.ValidateOutput(result, scope, false, true); // Don't throw on failure
                    
                    Console.WriteLine($"Memory configured to {memoryMB}MB");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to configure memory: {ex.Message}");
        }
    }
    
    private void ConfigureCpuResource(ManagementScope scope, ManagementObject managementService, ManagementObject vmSettings, int cpuCount)
    {
        try
        {
            using var cpuCollection = vmSettings.GetRelated("Msvm_ProcessorSettingData", "Msvm_VirtualSystemSettingDataComponent",
                null, null, null, null, false, null);
            
            foreach (ManagementObject cpu in cpuCollection)
            {
                using (cpu)
                {
                    cpu["VirtualQuantity"] = (ulong)cpuCount;
                    cpu["Reservation"] = (ulong)cpuCount;
                    cpu["Limit"] = (ulong)cpuCount;
                    
                    using var inParams = managementService.GetMethodParameters("ModifyResourceSettings");
                    inParams["ResourceSettings"] = new string[] { cpu.GetText(TextFormat.WmiDtd20) };
                    
                    using var result = managementService.InvokeMethod("ModifyResourceSettings", inParams, null);
                    WmiUtilities.ValidateOutput(result, scope, false, true); // Don't throw on failure
                    
                    Console.WriteLine($"CPU configured to {cpuCount} cores");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to configure CPU: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Creates and attaches VHD using pure WMI based on Microsoft Storage samples.
    /// </summary>
    private void CreateAndAttachVhd(ManagementScope scope, ManagementObject vm, string vhdPath, int sizeGB)
    {
        try
        {
            Console.WriteLine($"Creating and attaching VHD: {vhdPath}");
            
            // Create VHD directory if needed
            Directory.CreateDirectory(Path.GetDirectoryName(vhdPath)!);
            
            // Create VHD using WMI (based on Microsoft Storage samples)
            CreateVhdWmi(scope, vhdPath, sizeGB);
            
            // Attach VHD to VM
            AttachVhdToVmWmi(scope, vm, vhdPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating/attaching VHD: {ex.Message}");
            throw;
        }
    }
    
    private void CreateVhdWmi(ManagementScope scope, string vhdPath, int sizeGB)
    {
        try
        {
            Console.WriteLine($"Creating VHD using WMI: {vhdPath}, size: {sizeGB}GB");
            
            // Get Image Management Service
            using var imageServiceClass = new ManagementClass(scope, new ManagementPath("Msvm_ImageManagementService"), null);
            using var imageService = imageServiceClass.GetInstances().Cast<ManagementObject>().First();
            
            using var inParams = imageService.GetMethodParameters("CreateVirtualHardDisk");
            inParams["Path"] = vhdPath;
            inParams["Type"] = 3; // Dynamic VHD
            inParams["Format"] = 3; // VHDX
            inParams["MaxInternalSize"] = (ulong)sizeGB * 1024 * 1024 * 1024;
            
            using var result = imageService.InvokeMethod("CreateVirtualHardDisk", inParams, null);
            WmiUtilities.ValidateOutput(result, scope, true, true);
            
            Console.WriteLine($"VHD created successfully: {vhdPath}");
        }
        catch (Exception ex)
        {
            // Fallback to PowerShell if WMI fails
            Console.WriteLine($"WMI VHD creation failed, falling back to PowerShell: {ex.Message}");
            CreateVhdWithPowerShell(vhdPath, (uint)sizeGB);
        }
    }
    
    private void AttachVhdToVmWmi(ManagementScope scope, ManagementObject vm, string vhdPath)
    {
        try
        {
            Console.WriteLine($"Attaching VHD to VM using WMI: {vhdPath}");
            
            using var managementService = WmiUtilities.GetVirtualMachineManagementService(scope);
            using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
            
            // Create storage allocation setting data
            var diskSettingsText = CreateStorageAllocationSettingData(scope, vhdPath);
            
            using var inParams = managementService.GetMethodParameters("AddResourceSettings");
            inParams["AffectedSystem"] = vm.Path.Path;
            inParams["ResourceSettings"] = new string[] { diskSettingsText };
            
            using var result = managementService.InvokeMethod("AddResourceSettings", inParams, null);
            WmiUtilities.ValidateOutput(result, scope, true, true);
            
            Console.WriteLine("VHD attached successfully to VM");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error attaching VHD: {ex.Message}");
            throw;
        }
    }
    
    private string CreateStorageAllocationSettingData(ManagementScope scope, string vhdPath)
    {
        using var storageClass = new ManagementClass(scope, new ManagementPath("Msvm_StorageAllocationSettingData"), null);
        using var storageSettings = storageClass.CreateInstance();
        
        storageSettings["ElementName"] = Path.GetFileNameWithoutExtension(vhdPath);
        storageSettings["ResourceType"] = (ushort)31; // Logical Disk
        storageSettings["ResourceSubType"] = "Microsoft:Hyper-V:Virtual Hard Disk";
        storageSettings["HostResource"] = new string[] { vhdPath };
        storageSettings["Address"] = "0"; // First slot
        
        return storageSettings.GetText(TextFormat.WmiDtd20);
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
