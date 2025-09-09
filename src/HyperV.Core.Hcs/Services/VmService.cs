using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Interop;

namespace HyperV.Core.Hcs.Services;

/// <summary>VM Service using Host Compute System (HCS) API for real Hyper-V integration.</summary>
public sealed class VmService
{
    /// <summary>Creates a VM using HCS API with comprehensive configuration.</summary>
    public string Create(string id, CreateVmRequest req)
    {
        // Create virtual disk first
        var vhdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            "HyperV.Agent", "VHDs", $"{id}.vhdx");
        
        Directory.CreateDirectory(Path.GetDirectoryName(vhdPath)!);
        var actualVhdPath = CreateVirtualDisk(vhdPath, (uint)req.DiskSizeGB);

        // Debug output to see what path is being used
        Console.WriteLine($"Original VHD path: {vhdPath}");
        Console.WriteLine($"Actual VHD path: {actualVhdPath}");

        // Enhanced HCS configuration with proper VM settings
        var config = CreateVmConfiguration(id, req, actualVhdPath);

        IntPtr system = IntPtr.Zero;
        IntPtr op = IntPtr.Zero;
        string createResult = "{}";
        string startResult = "{}";
        
        try
        {
            // Create operation handle
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create HCS operation");

            // Create compute system
            var hr = HcsNative.HcsCreateComputeSystem(id, config, op, IntPtr.Zero, out system);
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to create compute system: {hr:X8}");

            // Wait for creation to complete
            createResult = WaitForResult(op, 120000);
            
            // Close the operation handle after use to prevent double cleanup
            SafeCloseOperation(ref op);
            
            // Validate that the VM was actually created successfully
            if (system == IntPtr.Zero)
            {
                throw new InvalidOperationException("VM creation failed: system handle is null");
            }
            
            // Check if we got a meaningful result
            if (string.IsNullOrEmpty(createResult) || createResult == "{}")
            {
                Console.WriteLine($"WARNING: VM {id} creation returned empty result. This may indicate the operation failed silently.");
            }
            else
            {
                Console.WriteLine($"VM {id} creation completed. Result: {createResult}");
            }
            
            // Store the VM handle for later management operations
            _hcsVms[id] = system;
            
            // Try to start the VM, but don't fail the entire operation if start fails
            try
            {
                startResult = StartVm(system);
                Console.WriteLine($"VM {id} started successfully. Start result: {startResult}");
                
                return JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created and Started",
                    VhdPath = actualVhdPath,
                    CreateResult = createResult,
                    StartResult = startResult
                });
            }
            catch (Exception startEx)
            {
                Console.WriteLine($"VM {id} created but failed to start: {startEx.Message}");
                
                return JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created (Start Failed)",
                    VhdPath = actualVhdPath,
                    CreateResult = createResult,
                    StartError = startEx.Message,
                    Note = "VM was created successfully but failed to start. This may be due to missing Hyper-V features or insufficient privileges."
                });
            }
        }
        catch (Exception ex)
        {
            // Clean up on failure - close system handle only if VM creation failed
            if (system != IntPtr.Zero)
            {
                SafeCloseComputeSystem(ref system);
            }
            if (File.Exists(vhdPath))
            {
                try { File.Delete(vhdPath); } catch { }
            }
            throw new InvalidOperationException($"VM creation failed: {ex.Message}", ex);
        }
        finally
        {
            // Only close operation handle, keep system handle for VM management
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Creates proper Hyper-V VM configuration for HCS based on NanaBox sample.</summary>
    private string CreateVmConfiguration(string id, CreateVmRequest req, string vhdPath)
    {
        // HCS configuration based on NanaBox sample - creates actual VMs, not containers
        var config = new
        {
            SchemaVersion = new { Major = 2, Minor = 1 },
            Owner = req.Name,
            ShouldTerminateOnLastHandleClosed = true,
            VirtualMachine = new
            {
                Chipset = new
                {
                    Uefi = new
                    {
                        Console = "Default",
                        ApplySecureBootTemplate = "Apply",
                        SecureBootTemplateId = "1734c6e8-3154-4dda-ba5f-a874cc483422"
                    }
                },
                ComputeTopology = new
                {
                    Memory = new
                    {
                        SizeInMB = req.MemoryMB,
                        AllowOvercommit = true
                    },
                    Processor = new
                    {
                        Count = req.CpuCount
                    }
                },
                Devices = new
                {
                    VideoMonitor = new
                    {
                        HorizontalResolution = 1024,
                        VerticalResolution = 768,
                        ConnectionOptions = new
                        {
                            NamedPipe = $"\\\\.\\pipe\\{req.Name}.BasicSession",
                            AccessSids = new[] { GetCurrentUserSid() }
                        }
                    },
                    EnhancedModeVideo = new
                    {
                        ConnectionOptions = new
                        {
                            NamedPipe = $"\\\\.\\pipe\\{req.Name}.EnhancedSession",
                            AccessSids = new[] { GetCurrentUserSid() }
                        }
                    },
                    Keyboard = new { },
                    Mouse = new { },
                    Scsi = new Dictionary<string, object>
                    {
                        ["HyperV Agent SCSI Controller"] = new
                        {
                            Attachments = new Dictionary<string, object>
                            {
                                ["0"] = new
                                {
                                    Type = "VirtualDisk",
                                    Path = vhdPath
                                }
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        // Log the configuration for debugging
        Console.WriteLine($"NanaBox-based HCS VM Configuration for {id}:");
        Console.WriteLine(json);
        
        return json;
    }

    /// <summary>Starts a VM using HCS API.</summary>
    private string StartVm(IntPtr system)
    {
        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create start operation");

            var hr = HcsNative.HcsStartComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to start compute system: {hr:X8}");

            return WaitForResult(op, 60000);
        }
        finally
        {
            if (op != IntPtr.Zero) HcsNative.HcsCloseOperation(op);
        }
    }

    /// <summary>Generates a random MAC address for the VM.</summary>
    private string GenerateMacAddress()
    {
        var random = new Random();
        var mac = new byte[6];
        random.NextBytes(mac);
        
        // Set the locally administered bit and ensure unicast
        mac[0] = (byte)((mac[0] & 0xFE) | 0x02);
        
        return string.Join(":", mac.Select(b => b.ToString("X2")));
    }

    /// <summary>Gets the current user's SID for HCS access control.</summary>
    private string GetCurrentUserSid()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? "S-1-5-32-544"; // Default to Administrators group
        }
        catch
        {
            // Fallback to Administrators group SID if we can't get current user
            return "S-1-5-32-544";
        }
    }

private string CreateVirtualDisk(string path, uint sizeGb)
{
    try
    {
        Console.WriteLine($"Creating VHDX file: {path} with size {sizeGb}GB");
        
        // Use Version 2 parameters for VHDX format (better for HCS)
        var paramsStruct = new CREATE_VIRTUAL_DISK_PARAMETERS
        {
            Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_2,
            Version2 = new CREATE_VIRTUAL_DISK_PARAMETERS_V2
            {
                MaximumSize = (ulong)sizeGb * 1024 * 1024 * 1024,
                BlockSizeInBytes = 0, // Use default block size
                SectorSizeInBytes = 512,
                PhysicalSectorSizeInBytes = 4096,
                ParentPath = IntPtr.Zero,
                SourcePath = IntPtr.Zero,
                OpenFlags = 0, // No special open flags
                ParentVirtualStorageType = Guid.Empty,
                SourceVirtualStorageType = Guid.Empty,
                ResiliencyGuid = Guid.Empty
            }
        };

        var diskType = VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE;

        var hr = VirtDiskNative.CreateVirtualDisk(
            ref _defaultVirtDiskType,
            path,
            diskType,
            IntPtr.Zero,
            CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_NONE, // Use default flags for VHDX
            0,
            ref paramsStruct,
            IntPtr.Zero,
            out var handle);

        if (hr != 0)
        {
            Console.WriteLine($"VirtDisk API failed with HRESULT: {hr:X8}");
            Console.WriteLine($"Error details: {new Win32Exception(hr).Message}");
            
            // Try with Version 1 parameters as fallback
            return CreateVirtualDiskV1(path, sizeGb);
        }
        
        // Close the handle if successful
        if (handle != IntPtr.Zero)
        {
            VirtDiskNative.CloseHandle(handle);
        }
        
        Console.WriteLine($"Successfully created VHDX: {path}");
        return path;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating virtual disk: {ex.Message}");
        return CreateVirtualDiskV1(path, sizeGb);
    }
}

private string CreateVirtualDiskV1(string path, uint sizeGb)
{
    try
    {
        Console.WriteLine($"Fallback: Creating VHD with Version 1 parameters");
        
        // Use Version 1 parameters for better compatibility
        var paramsStruct = new CREATE_VIRTUAL_DISK_PARAMETERS
        {
            Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
            Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
            {
                MaximumSize = (ulong)sizeGb * 1024 * 1024 * 1024,
                BlockSizeInBytes = 0, // Use default block size
                SectorSizeInBytes = 512,
                ParentPath = IntPtr.Zero,
                SourcePath = IntPtr.Zero
            }
        };

        var diskType = VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE;

        var hr = VirtDiskNative.CreateVirtualDisk(
            ref _defaultVirtDiskType,
            path,
            diskType,
            IntPtr.Zero,
            CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_NONE,
            0,
            ref paramsStruct,
            IntPtr.Zero,
            out var handle);

        if (hr != 0)
        {
            Console.WriteLine($"VirtDisk V1 API also failed with HRESULT: {hr:X8}");
            Console.WriteLine($"Error details: {new Win32Exception(hr).Message}");
            
            // Use PowerShell as last resort to create proper VHDX
            return CreateVhdxWithPowerShell(path, sizeGb);
        }
        
        // Close the handle if successful
        if (handle != IntPtr.Zero)
        {
            VirtDiskNative.CloseHandle(handle);
        }
        
        Console.WriteLine($"Successfully created VHD with V1 parameters: {path}");
        return path;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"V1 fallback failed: {ex.Message}");
        return CreateVhdxWithPowerShell(path, sizeGb);
    }
}

private string CreateVhdxWithPowerShell(string path, uint sizeGb)
{
    try
    {
        Console.WriteLine($"Using PowerShell to create VHDX: {path}");
        
        // Delete existing file if it exists (force parameter)
        if (File.Exists(path))
        {
            Console.WriteLine($"Deleting existing VHD file: {path}");
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
            process.WaitForExit(30000); // 30 second timeout
            
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            Console.WriteLine($"PowerShell output: {output}");
            if (!string.IsNullOrEmpty(error))
            {
                Console.WriteLine($"PowerShell error: {error}");
            }
            
            if (process.ExitCode == 0 && File.Exists(path))
            {
                Console.WriteLine($"Successfully created VHDX with PowerShell: {path}");
                return path;
            }
        }
        
        throw new InvalidOperationException("PowerShell VHD creation failed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"PowerShell VHD creation failed: {ex.Message}");
        
        // Final fallback: delete existing and create empty file
        Console.WriteLine("Creating empty file as final fallback");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.WriteAllText(path, "");
        return path;
    }
}

    public const uint VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 3;
    public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");

    private static VIRTUAL_STORAGE_TYPE _defaultVirtDiskType = new VIRTUAL_STORAGE_TYPE
    {
        DeviceId = VIRTUAL_STORAGE_TYPE_DEVICE_VHDX,
        VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
    };

    private static string WaitForResult(IntPtr op, uint ms)
    {
        var hr = HcsNative.HcsWaitForOperationResult(op, ms, out var resultPtr);
        
        Console.WriteLine($"HcsWaitForOperationResult returned HRESULT: {hr:X8}");
        
        if (hr != 0) 
        {
            Console.WriteLine($"HCS operation failed with HRESULT: {hr:X8}");
            throw new Win32Exception(hr, $"HCS operation failed: {hr:X8}");
        }
        
        try 
        { 
            var result = resultPtr != IntPtr.Zero ? Marshal.PtrToStringUni(resultPtr)! : "{}";
            Console.WriteLine($"HCS operation result: {result}");
            
            // Parse the result to check for errors
            if (!string.IsNullOrEmpty(result) && result != "{}")
            {
                try
                {
                    using var doc = JsonDocument.Parse(result);
                    if (doc.RootElement.TryGetProperty("Error", out var errorElement))
                    {
                        var errorMessage = errorElement.GetString();
                        Console.WriteLine($"HCS operation returned error: {errorMessage}");
                        throw new InvalidOperationException($"HCS operation failed: {errorMessage}");
                    }
                    
                    if (doc.RootElement.TryGetProperty("ErrorCode", out var errorCodeElement))
                    {
                        var errorCode = errorCodeElement.GetInt32();
                        if (errorCode != 0)
                        {
                            Console.WriteLine($"HCS operation returned error code: {errorCode}");
                            throw new InvalidOperationException($"HCS operation failed with error code: {errorCode}");
                        }
                    }
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, just return the raw result
                    Console.WriteLine("Could not parse HCS result as JSON, returning raw result");
                }
            }
            
            return result;
        }
        finally 
        { 
            if (resultPtr != IntPtr.Zero) LocalFree(resultPtr); 
        }
    }

    /// <summary>Safely closes an HCS operation handle, preventing access violations.</summary>
    private static void SafeCloseOperation(ref IntPtr op)
    {
        if (op != IntPtr.Zero)
        {
            try
            {
                HcsNative.HcsCloseOperation(op);
            }
            catch (AccessViolationException ex)
            {
                Console.WriteLine($"AccessViolationException during HcsCloseOperation: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during HcsCloseOperation: {ex.Message}");
            }
            finally
            {
                op = IntPtr.Zero;
            }
        }
    }

    /// <summary>Safely closes an HCS compute system handle, preventing access violations.</summary>
    private static void SafeCloseComputeSystem(ref IntPtr system)
    {
        if (system != IntPtr.Zero)
        {
            try
            {
                HcsNative.HcsCloseComputeSystem(system);
            }
            catch (AccessViolationException ex)
            {
                Console.WriteLine($"AccessViolationException during HcsCloseComputeSystem: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during HcsCloseComputeSystem: {ex.Message}");
            }
            finally
            {
                system = IntPtr.Zero;
            }
        }
    }

    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr hMem);

    // Dictionary to track created HCS VMs (in a real implementation, this should be persistent storage)
    private static readonly Dictionary<string, IntPtr> _hcsVms = new Dictionary<string, IntPtr>();

    /// <summary>Lists all HCS VMs.</summary>
    public string ListVms()
    {
        try
        {
            var vms = new List<object>();
            
            foreach (var kvp in _hcsVms)
            {
                vms.Add(new
                {
                    Id = kvp.Key,
                    Name = kvp.Key,
                    State = "Unknown", // HCS doesn't provide easy state querying
                    Backend = "HCS",
                    Handle = kvp.Value.ToString()
                });
            }
            
            return JsonSerializer.Serialize(new
            {
                Count = vms.Count,
                VMs = vms,
                Backend = "HCS"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list HCS VMs: {ex.Message}", ex);
        }
    }

    /// <summary>Checks if an HCS VM exists by name.</summary>
    public bool IsVmPresent(string vmName)
    {
        return _hcsVms.ContainsKey(vmName);
    }

    /// <summary>Starts an HCS VM by name.</summary>
    public void StartVm(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create start operation");

            var hr = HcsNative.HcsStartComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to start HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Stops an HCS VM by name.</summary>
    public void StopVm(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create stop operation");

            var hr = HcsNative.HcsShutDownComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to stop HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Terminates an HCS VM by name.</summary>
    public void TerminateVm(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create terminate operation");

            var hr = HcsNative.HcsTerminateComputeSystem(system, op);
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to terminate HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Pauses an HCS VM by name.</summary>
    public void PauseVm(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create pause operation");

            var hr = HcsNative.HcsPauseComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to pause HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Resumes an HCS VM by name.</summary>
    public void ResumeVm(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create resume operation");

            var hr = HcsNative.HcsResumeComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to resume HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Gets properties of an HCS VM by name.</summary>
    public string GetVmProperties(string vmName)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create properties operation");

            var hr = HcsNative.HcsGetComputeSystemProperties(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to get HCS VM {vmName} properties: {hr:X8}");

            return WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Modifies an HCS VM by name.</summary>
    public void ModifyVm(string vmName, string configuration)
    {
        if (!_hcsVms.TryGetValue(vmName, out var system))
            throw new InvalidOperationException($"HCS VM {vmName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create modify operation");

            var hr = HcsNative.HcsModifyComputeSystem(system, op, configuration, IntPtr.Zero);
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to modify HCS VM {vmName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Lists snapshots for an HCS VM (not supported - returns empty list).</summary>
    public string ListVmSnapshots(string vmName)
    {
        // HCS VMs don't support snapshots in the same way as traditional Hyper-V VMs
        return JsonSerializer.Serialize(new
        {
            VmId = vmName,
            VmName = vmName,
            Count = 0,
            Snapshots = new object[0],
            Backend = "HCS",
            Note = "Snapshots are not supported for HCS VMs"
        });
    }

    /// <summary>Creates a snapshot for an HCS VM (not supported).</summary>
    public string CreateVmSnapshot(string vmName, string snapshotName, string? notes = null)
    {
        throw new NotSupportedException("Snapshots are not supported for HCS VMs");
    }

    /// <summary>Deletes a snapshot for an HCS VM (not supported).</summary>
    public void DeleteVmSnapshot(string vmName, string snapshotId)
    {
        throw new NotSupportedException("Snapshots are not supported for HCS VMs");
    }

    /// <summary>Reverts an HCS VM to a snapshot (not supported).</summary>
    public void RevertVmToSnapshot(string vmName, string snapshotId)
    {
        throw new NotSupportedException("Snapshots are not supported for HCS VMs");
    }
}
