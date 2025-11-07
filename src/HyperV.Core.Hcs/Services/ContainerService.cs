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
public class ContainerService
{
    // Dictionary to track created HCS containers
    private static readonly Dictionary<string, IntPtr> _hcsContainers = new Dictionary<string, IntPtr>();

    /// <summary>Creates a container using HCS API with container-specific configuration.</summary>
    public virtual string Create(string id, CreateContainerRequest req)
    {
        // Create container storage if needed
        var storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
            "HyperV.Agent", "Containers", id);
        
        Directory.CreateDirectory(storagePath);

        // Try to create HCS container, but fall back to mock if HCS is not available
        try
        {
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
            finally
            {
                SafeCloseOperation(ref op);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HCS container creation failed: {ex.Message}");
            
            // Clean up storage directory on failure
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, true); } catch { }
            }
            
            // Don't fall back to mock - throw proper error with system information
            var errorMessage = ex.Message.Contains("8037010D") 
                ? "Windows Container subsystem is not available. Please ensure Windows Container features are enabled and the Host Compute Service is running."
                : $"Container creation failed: {ex.Message}";
                
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    /// <summary>Creates HCS container configuration optimized for containers.</summary>
    private string CreateContainerConfiguration(string id, CreateContainerRequest req, string storagePath)
    {
        // Use a simpler, more compatible HCS configuration for basic containers
        var config = new
        {
            SchemaVersion = new { Major = 2, Minor = 1 },
            Owner = req.Name,
            ShouldTerminateOnLastHandleClosed = true,
            VirtualMachine = new
            {
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
                Devices = new
                {
                    HvSocket = new
                    {
                        HvSocketConfig = new
                        {
                            DefaultBindSecurityDescriptor = "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;GRGW;;;S-1-15-2-1)"
                        }
                    }
                }
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
    public virtual bool IsContainerPresent(string containerName)
    {
        return _hcsContainers.ContainsKey(containerName);
    }

    /// <summary>Lists all HCS containers.</summary>
    public virtual List<Dictionary<string, object>> ListContainers()
    {
        var containers = new List<Dictionary<string, object>>();

        foreach (var kvp in _hcsContainers)
        {
            try
            {
                // Get properties for real HCS containers
                var properties = GetContainerProperties(kvp.Key);
                var containerInfo = new Dictionary<string, object>
                {
                    ["Id"] = kvp.Key,
                    ["Name"] = kvp.Key, // Use ID as name for now
                    ["Backend"] = "HCS",
                    ["Status"] = "Running", // Assume running if tracked
                    ["Properties"] = properties
                };
                containers.Add(containerInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting properties for HCS container {kvp.Key}: {ex.Message}");
                // Add basic info even if properties fail
                containers.Add(new Dictionary<string, object>
                {
                    ["Id"] = kvp.Key,
                    ["Name"] = kvp.Key,
                    ["Backend"] = "HCS",
                    ["Status"] = "Unknown",
                    ["Error"] = ex.Message
                });
            }
        }

        return containers;
    }

    /// <summary>Starts an HCS container by name.</summary>
    public virtual void StartContainer(string containerName)
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
    public virtual void StopContainer(string containerName)
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
    public virtual void TerminateContainer(string containerName)
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
    public virtual void PauseContainer(string containerName)
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
    public virtual void ResumeContainer(string containerName)
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
    public virtual string GetContainerProperties(string containerName)
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
    public virtual void ModifyContainer(string containerName, string configuration)
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
