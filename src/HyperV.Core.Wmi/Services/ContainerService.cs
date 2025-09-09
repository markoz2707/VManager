using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.Json;
using HyperV.Contracts.Models;

namespace HyperV.Core.Wmi.Services;

/// <summary>Container Service using WMI for Hyper-V isolated containers.</summary>
public sealed class ContainerService
{
    private readonly ManagementScope _scope;

    public ContainerService()
    {
        _scope = new ManagementScope(@"\\.\root\virtualization\v2");
        _scope.Connect();
    }

    /// <summary>Creates a Hyper-V isolated container using WMI.</summary>
    public string Create(string id, CreateContainerRequest req)
    {
        try
        {
            Console.WriteLine($"Creating Hyper-V isolated container using WMI: {id}");

            // Get the Hyper-V virtual system management service
            var service = GetVirtualSystemManagementService();
            
            // Create container configuration
            var containerConfig = CreateContainerConfiguration(req);
            
            // Define the container system
            var inParams = service.GetMethodParameters("DefineSystem");
            inParams["SystemSettings"] = containerConfig;
            inParams["ResourceSettings"] = CreateContainerResourceSettings(req);
            inParams["ReferenceConfiguration"] = null;

            var outParams = service.InvokeMethod("DefineSystem", inParams, null);
            var returnValue = Convert.ToUInt32(outParams["ReturnValue"]);

            Console.WriteLine($"DefineSystem returned: {returnValue}");

            if (returnValue == 0)
            {
                // Success - container created immediately
                return JsonSerializer.Serialize(new
                {
                    Id = id,
                    Name = req.Name,
                    Status = "Created",
                    Image = req.Image,
                    Mode = "WMI"
                });
            }
            else if (returnValue == 4096)
            {
                // Job started - wait for completion
                var job = outParams["Job"] as ManagementObject;
                if (job != null)
                {
                    Console.WriteLine("Container creation job started, waiting for completion...");
                    
                    var jobResult = WaitForJob(job);
                    if (jobResult.Success)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            Id = id,
                            Name = req.Name,
                            Status = "Created",
                            Image = req.Image,
                            Mode = "WMI",
                            JobResult = jobResult.Message
                        });
                    }
                    else
                    {
                        throw new InvalidOperationException($"Container creation job failed: {jobResult.Message}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Container creation job started but no job object returned");
                }
            }
            else
            {
                throw new InvalidOperationException($"DefineSystem failed with return value: {returnValue}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating Hyper-V isolated container: {ex.Message}");
            throw new InvalidOperationException($"Container creation failed: {ex.Message}", ex);
        }
    }

    /// <summary>Creates WMI container configuration.</summary>
    private string CreateContainerConfiguration(CreateContainerRequest req)
    {
        // Get the default container settings
        var settingsClass = new ManagementClass(_scope, new ManagementPath("Msvm_VirtualSystemSettingData"), null);
        var settingsInstance = settingsClass.CreateInstance();

        // Configure as Hyper-V isolated container
        settingsInstance["ElementName"] = req.Name;
        settingsInstance["Description"] = $"Hyper-V isolated container: {req.Name}";
        settingsInstance["VirtualSystemType"] = "Microsoft:Hyper-V:Container";
        settingsInstance["VirtualSystemSubType"] = "Microsoft:Hyper-V:Container:Isolated";
        settingsInstance["ConfigurationID"] = req.Id;

        return settingsInstance.GetText(TextFormat.WmiDtd20);
    }

    /// <summary>Creates container resource settings.</summary>
    private string[] CreateContainerResourceSettings(CreateContainerRequest req)
    {
        var resourceSettings = new List<string>();

        // Memory settings
        var memoryClass = new ManagementClass(_scope, new ManagementPath("Msvm_MemorySettingData"), null);
        var memoryInstance = memoryClass.CreateInstance();
        memoryInstance["ElementName"] = "Memory";
        memoryInstance["VirtualQuantity"] = (ulong)req.MemoryMB;
        memoryInstance["Reservation"] = (ulong)req.MemoryMB;
        memoryInstance["Limit"] = (ulong)req.MemoryMB;
        resourceSettings.Add(memoryInstance.GetText(TextFormat.WmiDtd20));

        // Processor settings
        var processorClass = new ManagementClass(_scope, new ManagementPath("Msvm_ProcessorSettingData"), null);
        var processorInstance = processorClass.CreateInstance();
        processorInstance["ElementName"] = "Processor";
        processorInstance["VirtualQuantity"] = (ulong)req.CpuCount;
        processorInstance["Reservation"] = (ulong)req.CpuCount;
        processorInstance["Limit"] = (ulong)req.CpuCount;
        resourceSettings.Add(processorInstance.GetText(TextFormat.WmiDtd20));

        return resourceSettings.ToArray();
    }

    /// <summary>Gets the virtual system management service.</summary>
    private ManagementObject GetVirtualSystemManagementService()
    {
        var query = new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService");
        var searcher = new ManagementObjectSearcher(_scope, query);
        var collection = searcher.Get();

        foreach (ManagementObject obj in collection)
        {
            return obj; // Return the first (and typically only) service
        }

        throw new InvalidOperationException("Hyper-V Virtual System Management Service not found");
    }

    /// <summary>Waits for a WMI job to complete.</summary>
    private (bool Success, string Message) WaitForJob(ManagementObject job)
    {
        const int timeout = 300000; // 5 minutes
        const int pollInterval = 1000; // 1 second
        int elapsed = 0;

        while (elapsed < timeout)
        {
            job.Get(); // Refresh the job object
            var jobState = Convert.ToUInt16(job["JobState"]);
            var description = job["Description"]?.ToString() ?? "Unknown";

            Console.WriteLine($"Job state: {jobState}, Description: {description}");

            switch (jobState)
            {
                case 7: // Completed
                    return (true, "Container creation completed successfully");
                case 8: // Terminated
                case 9: // Killed
                case 10: // Exception
                    var errorDescription = job["ErrorDescription"]?.ToString() ?? "Unknown error";
                    return (false, $"Container creation job failed: {errorDescription}");
                case 4: // Running
                case 3: // Starting
                    // Continue waiting
                    break;
                default:
                    Console.WriteLine($"Unexpected job state: {jobState}");
                    break;
            }

            System.Threading.Thread.Sleep(pollInterval);
            elapsed += pollInterval;
        }

        return (false, "Container creation job timed out");
    }

    /// <summary>Checks if a WMI container exists by name.</summary>
    public bool IsContainerPresent(string containerName)
    {
        try
        {
            var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{containerName}' AND Caption = 'Virtual Machine'";
            var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery(query));
            return searcher.Get().Cast<ManagementObject>().Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Starts a WMI container by name.</summary>
    public void StartContainer(string containerName)
    {
        ExecuteContainerStateChange(containerName, 2, "start");
    }

    /// <summary>Stops a WMI container by name.</summary>
    public void StopContainer(string containerName)
    {
        ExecuteContainerStateChange(containerName, 3, "stop");
    }

    /// <summary>Terminates a WMI container by name.</summary>
    public void TerminateContainer(string containerName)
    {
        ExecuteContainerStateChange(containerName, 3, "terminate");
    }

    /// <summary>Pauses a WMI container by name.</summary>
    public void PauseContainer(string containerName)
    {
        ExecuteContainerStateChange(containerName, 9, "pause");
    }

    /// <summary>Resumes a WMI container by name.</summary>
    public void ResumeContainer(string containerName)
    {
        ExecuteContainerStateChange(containerName, 2, "resume");
    }

    /// <summary>Gets properties of a WMI container by name.</summary>
    public string GetContainerProperties(string containerName)
    {
        var container = GetContainer(containerName);
        if (container == null) 
            throw new InvalidOperationException($"WMI container {containerName} not found");

        var properties = new Dictionary<string, object>();
        foreach (PropertyData prop in container.Properties)
        {
            properties[prop.Name] = prop.Value;
        }

        return JsonSerializer.Serialize(properties, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Modifies a WMI container by name.</summary>
    public void ModifyContainer(string containerName, string configuration)
    {
        // WMI container modification would require specific implementation
        // based on what aspects need to be modified
        throw new NotImplementedException("WMI container modification not yet implemented");
    }

    /// <summary>Executes a state change operation on a container.</summary>
    private void ExecuteContainerStateChange(string containerName, uint requestedState, string operation)
    {
        var container = GetContainer(containerName);
        if (container == null)
            throw new InvalidOperationException($"WMI container {containerName} not found");

        // Execute the state change
        var inParams = container.GetMethodParameters("RequestStateChange");
        inParams["RequestedState"] = requestedState;

        var outParams = container.InvokeMethod("RequestStateChange", inParams, null);
        var returnValue = Convert.ToUInt32(outParams["ReturnValue"]);

        if (returnValue == 0)
        {
            Console.WriteLine($"Container {operation} completed successfully");
        }
        else if (returnValue == 4096)
        {
            // Job started
            var job = outParams["Job"] as ManagementObject;
            if (job != null)
            {
                var jobResult = WaitForJob(job);
                if (!jobResult.Success)
                {
                    throw new InvalidOperationException($"Container {operation} job failed: {jobResult.Message}");
                }
            }
        }
        else
        {
            throw new InvalidOperationException($"Container {operation} failed with return value: {returnValue}");
        }
    }

    /// <summary>Gets a container by name.</summary>
    private ManagementObject? GetContainer(string containerName)
    {
        var query = $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{containerName}' AND Caption = 'Virtual Machine'";
        var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery(query));
        return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
    }
}
