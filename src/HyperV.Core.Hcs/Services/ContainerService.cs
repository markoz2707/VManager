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

/// <summary>Container Service using Host Compute System (HCS) API for lightweight containers.</summary>
public sealed class ContainerService
{
    // Dictionary to track created HCS containers
    private static readonly Dictionary<string, IntPtr> _hcsContainers = new Dictionary<string, IntPtr>();

    /// <summary>Creates a container using HCS API with container-specific configuration.</summary>
    public string Create(string id, CreateContainerRequest req)
    {
        // Create container storage if needed
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            "HyperV.Agent", "Containers", id);
        
        Directory.CreateDirectory(storagePath);

        // Create HCS container configuration
        var config = CreateContainerConfiguration(id, req, storagePath);

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
                throw new Win32Exception(hr, $"Failed to create container: {hr:X8}");

            // Wait for creation to complete
            createResult = WaitForResult(op, 120000);
            
            // Close the operation handle after use
            SafeCloseOperation(ref op);
            
            // Validate container creation
            if (system == IntPtr.Zero)
            {
                throw new InvalidOperationException("Container creation failed: system handle is null");
            }
            
            Console.WriteLine($"Container {id} creation completed. Result: {createResult}");
            
            // Store the container handle for later management operations
            _hcsContainers[id] = system;
            
            // Try to start the container
            try
            {
                startResult = StartContainer(system);
                Console.WriteLine($"Container {id} started successfully. Start result: {startResult}");
                
                return JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created and Started",
                    StoragePath = storagePath,
                    Image = req.Image,
                    CreateResult = createResult,
                    StartResult = startResult
                });
            }
            catch (Exception startEx)
            {
                Console.WriteLine($"Container {id} created but failed to start: {startEx.Message}");
                
                return JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created (Start Failed)",
                    StoragePath = storagePath,
                    Image = req.Image,
                    CreateResult = createResult,
                    StartError = startEx.Message
                });
            }
        }
        catch (Exception ex)
        {
            // Clean up on failure
            if (system != IntPtr.Zero)
            {
                SafeCloseComputeSystem(ref system);
            }
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, true); } catch { }
            }
            throw new InvalidOperationException($"Container creation failed: {ex.Message}", ex);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Creates HCS container configuration optimized for containers.</summary>
    private string CreateContainerConfiguration(string id, CreateContainerRequest req, string storagePath)
    {
        var config = new
        {
            SchemaVersion = new { Major = 2, Minor = 1 },
            Owner = req.Name,
            ShouldTerminateOnLastHandleClosed = true,
            Container = new
            {
                Image = req.Image,
                ComputeTopology = new
                {
                    Memory = new
                    {
                        SizeInMB = req.MemoryMB
                    },
                    Processor = new
                    {
                        Count = req.CpuCount
                    }
                },
                Storage = new
                {
                    Path = storagePath,
                    SizeInGB = req.StorageSizeGB
                },
                Environment = req.Environment.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray(),
                NetworkEndpoints = req.PortMappings.Select(pm => new
                {
                    HostPort = pm.Key,
                    ContainerPort = pm.Value,
                    Protocol = "TCP"
                }).ToArray(),
                MappedDirectories = req.VolumeMounts.Select(vm => new
                {
                    HostPath = vm.Key,
                    ContainerPath = vm.Value,
                    ReadOnly = false
                }).ToArray()
            }
        };

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        Console.WriteLine($"HCS Container Configuration for {id}:");
        Console.WriteLine(json);
        
        return json;
    }

    /// <summary>Starts a container using HCS API.</summary>
    private string StartContainer(IntPtr system)
    {
        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create start operation");

            var hr = HcsNative.HcsStartComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to start container: {hr:X8}");

            return WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Checks if an HCS container exists by name.</summary>
    public bool IsContainerPresent(string containerName)
    {
        return _hcsContainers.ContainsKey(containerName);
    }

    /// <summary>Starts an HCS container by name.</summary>
    public void StartContainer(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create start operation");

            var hr = HcsNative.HcsStartComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to start HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Stops an HCS container by name.</summary>
    public void StopContainer(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create stop operation");

            var hr = HcsNative.HcsShutDownComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to stop HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Terminates an HCS container by name.</summary>
    public void TerminateContainer(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create terminate operation");

            var hr = HcsNative.HcsTerminateComputeSystem(system, op);
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to terminate HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Pauses an HCS container by name.</summary>
    public void PauseContainer(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create pause operation");

            var hr = HcsNative.HcsPauseComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to pause HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Resumes an HCS container by name.</summary>
    public void ResumeContainer(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create resume operation");

            var hr = HcsNative.HcsResumeComputeSystem(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to resume HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Gets properties of an HCS container by name.</summary>
    public string GetContainerProperties(string containerName)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create properties operation");

            var hr = HcsNative.HcsGetComputeSystemProperties(system, op, "");
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to get HCS container {containerName} properties: {hr:X8}");

            return WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    /// <summary>Modifies an HCS container by name.</summary>
    public void ModifyContainer(string containerName, string configuration)
    {
        if (!_hcsContainers.TryGetValue(containerName, out var system))
            throw new InvalidOperationException($"HCS container {containerName} not found");

        IntPtr op = IntPtr.Zero;
        try
        {
            op = HcsNative.HcsCreateOperation(IntPtr.Zero, IntPtr.Zero);
            if (op == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create modify operation");

            var hr = HcsNative.HcsModifyComputeSystem(system, op, configuration, IntPtr.Zero);
            if (hr != 0)
                throw new Win32Exception(hr, $"Failed to modify HCS container {containerName}: {hr:X8}");

            WaitForResult(op, 60000);
        }
        finally
        {
            SafeCloseOperation(ref op);
        }
    }

    // Helper methods (shared with VmService)
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
            return result;
        }
        finally 
        { 
            if (resultPtr != IntPtr.Zero) LocalFree(resultPtr); 
        }
    }

    private static void SafeCloseOperation(ref IntPtr op)
    {
        if (op != IntPtr.Zero)
        {
            try
            {
                HcsNative.HcsCloseOperation(op);
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

    private static void SafeCloseComputeSystem(ref IntPtr system)
    {
        if (system != IntPtr.Zero)
        {
            try
            {
                HcsNative.HcsCloseComputeSystem(system);
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
}
