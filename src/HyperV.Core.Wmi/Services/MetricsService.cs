using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.Json;

namespace HyperV.Core.Wmi.Services;

public class MetricsService
{
    private readonly ManagementScope _scope;

    public MetricsService()
    {
        _scope = new ManagementScope(@"root\virtualization\v2");
    }

    /// <summary>
    /// Queries whether metric collection is enabled for a given VM and metric definition.
    /// Based on Microsoft Metrics/EnumerateMetrics.cs QueryMetricCollectionEnabledForVirtualMachine.
    /// </summary>
    public virtual string QueryMetricCollectionEnabled(string vmName, string metricDefinitionName)
    {
        try
        {
            Console.WriteLine($"Querying metric collection enabled for VM '{vmName}', metric '{metricDefinitionName}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
            using var metricDefinition = GetMetricDefinition(metricDefinitionName, _scope);
            
            var queryWql = string.Format(
                "SELECT * FROM Msvm_MetricDefForME WHERE Antecedent=\"{0}\" AND Dependent=\"{1}\"",
                WmiUtilities.EscapeObjectPath(vm.Path.Path),
                WmiUtilities.EscapeObjectPath(metricDefinition.Path.Path));
            
            using var searcher = new ManagementObjectSearcher(_scope, new ObjectQuery(queryWql));
            using var collection = searcher.Get();
            
            if (collection.Count != 1)
            {
                throw new InvalidOperationException($"Msvm_MetricDefForME not found for VM '{vmName}' and metric '{metricDefinitionName}'");
            }
            
            using var metricDefForMe = collection.Cast<ManagementObject>().First();
            var enabledState = (ushort)metricDefForMe["MetricCollectionEnabled"];
            
            var metricEnabledState = enabledState switch
            {
                0 => "Unknown",
                2 => "Enabled",
                3 => "Disabled",
                32768 => "PartiallyEnabled",
                _ => $"State-{enabledState}"
            };
            
            return JsonSerializer.Serialize(new
            {
                VmName = vmName,
                MetricDefinition = metricDefinitionName,
                MetricCollectionEnabled = enabledState,
                EnabledState = metricEnabledState,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to query metric collection enabled: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enumerates discrete metrics that compose aggregate metrics for a VM.
    /// Based on Microsoft Metrics/EnumerateMetrics.cs EnumerateDiscreteMetricsForVm.
    /// </summary>
    public virtual string EnumerateDiscreteMetricsForVm(string vmName)
    {
        try
        {
            Console.WriteLine($"Enumerating discrete metrics for VM '{vmName}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
            Console.WriteLine($"Retrieved VM path: {vm.Path.Path}");
            
            return EnumerateMetricsInternal(vm, vmName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to enumerate discrete metrics for VM: {ex.Message}", ex);
        }
    }

    private string EnumerateMetricsInternal(ManagementObject vm, string vmName)
    {
        using var vmMetricCollection = vm.GetRelated(
            "Msvm_AggregationMetricValue",
            "Msvm_MetricForME",
            null, null, null, null, false, null);
        
        Console.WriteLine($"Found {vmMetricCollection.Count} aggregate metrics for VM '{vmName}'");
        
        // List all found aggregate metrics for debugging
        if (vmMetricCollection.Count > 0)
        {
            Console.WriteLine($"Listing aggregate metrics for VM '{vmName}':");
            int metricIndex = 1;
            foreach (ManagementObject metric in vmMetricCollection)
            {
                using (metric)
                {
                    var metricId = metric["Id"]?.ToString() ?? "N/A";
                    var elementName = metric["ElementName"]?.ToString() ?? "N/A";
                    var metricDefinitionId = metric["MetricDefinitionId"]?.ToString() ?? "N/A";
                    var metricValue = metric["MetricValue"]?.ToString() ?? "N/A";
                    var timeStamp = metric["TimeStamp"]?.ToString() ?? "N/A";
                    
                    Console.WriteLine($"  [{metricIndex}] Aggregate Metric:");
                    Console.WriteLine($"      ID: {metricId}");
                    Console.WriteLine($"      ElementName: {elementName}");
                    Console.WriteLine($"      MetricDefinitionId: {metricDefinitionId}");
                    Console.WriteLine($"      MetricValue: {metricValue}");
                    Console.WriteLine($"      TimeStamp: {timeStamp}");
                    metricIndex++;
                }
            }
        }
        
        if (vmMetricCollection.Count == 0)
        {
            Console.WriteLine($"No aggregate metrics found for VM '{vmName}'. Metrics collection may not be enabled. Attempting to enable metrics automatically.");
            
            try
            {
                // Automatically enable all metrics for the VM
                string vmPath = vm.Path.Path;
                ControlMetrics(vmPath, null, 2, _scope); // 2 = Enable all metrics
                Console.WriteLine($"Successfully enabled all metrics for VM '{vmName}'. Retrying enumeration...");
                
                // Retry getting metrics after enabling
                using var retryMetricCollection = vm.GetRelated(
                    "Msvm_AggregationMetricValue",
                    "Msvm_MetricForME",
                    null, null, null, null, false, null);
                
                Console.WriteLine($"After enabling metrics, found {retryMetricCollection.Count} aggregate metrics for VM '{vmName}'");
                
                if (retryMetricCollection.Count == 0)
                {
                    Console.WriteLine($"Still no metrics found after enabling. This may be expected for some VM states.");
                    return JsonSerializer.Serialize(new
                    {
                        VmName = vmName,
                        Metrics = new object[0],
                        MetricsEnabled = true,
                        Message = "Metrics enabled but no data available yet",
                        Backend = "WMI"
                    });
                }
                
                // Process the retry collection
                return ProcessMetricCollection(retryMetricCollection, vmName);
            }
            catch (Exception enableEx)
            {
                Console.WriteLine($"Failed to automatically enable metrics for VM '{vmName}': {enableEx.Message}");
                return JsonSerializer.Serialize(new
                {
                    VmName = vmName,
                    Metrics = new object[0],
                    MetricsEnabled = false,
                    Error = $"Metrics not enabled and auto-enable failed: {enableEx.Message}",
                    Backend = "WMI"
                });
            }
        }
        
        return ProcessMetricCollection(vmMetricCollection, vmName);
    }

    private string ProcessMetricCollection(ManagementObjectCollection vmMetricCollection, string vmName)
    {
        try
        {
            using var vmAggregateMetric = WmiUtilities.GetFirstObjectFromCollection(vmMetricCollection);
            Console.WriteLine($"Processing aggregate metric ID: {vmAggregateMetric["Id"]?.ToString()}");
            
            using var discreteMetricCollection = vmAggregateMetric.GetRelated(
                null,
                "Msvm_MetricCollectionDependency",
                null, null, null, null, false, null);
            
            Console.WriteLine($"Found {discreteMetricCollection.Count} discrete metrics for aggregate");
            
            var discreteMetrics = new List<object>();
            foreach (ManagementObject discreteMetric in discreteMetricCollection)
            {
                using (discreteMetric)
                {
                    var elementName = discreteMetric["ElementName"]?.ToString();
                    var metricValue = discreteMetric["MetricValue"]?.ToString();
                    Console.WriteLine($"Discrete metric: ElementName='{elementName}', Value='{metricValue}'");
                    discreteMetrics.Add(new
                    {
                        MetricValue = metricValue,
                        ElementName = elementName
                    });
                }
            }
            
            Console.WriteLine($"Returning {discreteMetrics.Count} discrete metrics for VM '{vmName}'");
            
            return JsonSerializer.Serialize(new
            {
                VmName = vmName,
                AggregateMetricId = vmAggregateMetric["Id"]?.ToString(),
                DiscreteMetrics = discreteMetrics,
                Backend = "WMI"
            });
        }
        catch (ArgumentException ex) when (ex.Message.Contains("collection contains no objects"))
        {
            Console.WriteLine($"No aggregate metrics available in collection for VM '{vmName}': {ex.Message}");
            return JsonSerializer.Serialize(new
            {
                VmName = vmName,
                AggregateMetricId = "N/A",
                DiscreteMetrics = new object[0],
                Message = "Aggregate metrics collection is empty",
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing metric collection for VM '{vmName}': {ex.Message}");
            throw new InvalidOperationException($"Failed to process metric collection: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Enumerates metrics for a given resource pool.
    /// Based on Microsoft Metrics/EnumerateMetrics.cs EnumerateMetricsForResourcePool.
    /// </summary>
    public virtual string EnumerateMetricsForResourcePool(string resourceType, string resourceSubType, string poolId)
    {
        try
        {
            Console.WriteLine($"Enumerating metrics for resource pool: Type='{resourceType}', SubType='{resourceSubType}', PoolId='{poolId}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var pool = WmiUtilities.GetResourcePool(resourceType, resourceSubType, poolId, _scope);
            using var metricDefinitionCollection = pool.GetRelated(
                "Msvm_AggregationMetricDefinition",
                "Msvm_MetricDefForME",
                null, null, null, null, false, null);
            using var metricValueCollection = pool.GetRelated(
                "Msvm_AggregationMetricValue",
                "Msvm_MetricForME",
                null, null, null, null, false, null);
            
            var metrics = new List<object>();
            Console.WriteLine($"Processing {metricDefinitionCollection.Count} metric definitions for resource pool");
            foreach (ManagementObject metricDefinition in metricDefinitionCollection)
            {
                using (metricDefinition)
                {
                    var metricName = metricDefinition["ElementName"]?.ToString() ?? "Unknown";
                    var id = metricDefinition["Id"]?.ToString();
                    
                    string? metricValue = null;
                    bool matched = false;
                    foreach (ManagementObject metricValueObj in metricValueCollection)
                    {
                        using (metricValueObj)
                        {
                            var metricDefinitionId = metricValueObj["MetricDefinitionId"]?.ToString();
                            if (metricDefinitionId == id)
                            {
                                metricValue = metricValueObj["MetricValue"]?.ToString();
                                matched = true;
                                break;
                            }
                        }
                    }
                    
                    if (!matched)
                    {
                        Console.WriteLine($"Warning: No matching metric value found for definition ID '{id}' (ElementName: '{metricName}')");
                    }
                    
                    metrics.Add(new
                    {
                        MetricDefinition = metricName,
                        Id = id,
                        MetricValue = metricValue ?? "N/A"
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                ResourceType = resourceType,
                ResourceSubType = resourceSubType,
                PoolId = poolId,
                Metrics = metrics,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to enumerate metrics for resource pool: {ex.Message}", ex);
        }
    }

/// <summary>
/// Enables metric collection for a VM using the Msvm_MetricService::ControlMetrics method.
/// Based on Microsoft Metrics/ControlMetrics.cs EnableMetricsForVirtualMachine.
/// </summary>
public virtual string EnableMetricsCollection(string vmName, string[]? metricNames = null)
{
    try
    {
        Console.WriteLine($"Enabling metrics collection for VM '{vmName}'");
        
        if (!_scope.IsConnected) _scope.Connect();
        
        using var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
        string vmPath = vm.Path.Path;
        
        // If no specific metrics are requested, enable all metrics (pass null to ControlMetrics)
        if (metricNames == null || metricNames.Length == 0)
        {
            Console.WriteLine($"Enabling all metrics for VM '{vmName}'");
            ControlMetrics(vmPath, null, 2, _scope); // 2 = Enable
            
            return JsonSerializer.Serialize(new
            {
                VmName = vmName,
                Status = "All metrics enabled",
                Method = "ControlMetrics",
                Backend = "WMI"
            });
        }
        
        // Enable specific metrics
        var enabledMetrics = new List<object>();
        
        foreach (string metricName in metricNames)
        {
            try
            {
                using var metricDefinition = GetMetricDefinition(metricName, _scope);
                string metricDefinitionPath = metricDefinition.Path.Path;
                
                Console.WriteLine($"Enabling metric '{metricName}' for VM '{vmName}'");
                ControlMetrics(vmPath, metricDefinitionPath, 2, _scope); // 2 = Enable
                
                enabledMetrics.Add(new { 
                    MetricName = metricName, 
                    Status = "Enabled",
                    Method = "ControlMetrics"
                });
                
                Console.WriteLine($"Successfully enabled metric '{metricName}' for VM '{vmName}'");
            }
            catch (Exception metricEx)
            {
                Console.WriteLine($"Failed to enable metric '{metricName}': {metricEx.Message}");
                enabledMetrics.Add(new { 
                    MetricName = metricName, 
                    Status = "Failed", 
                    Error = metricEx.Message 
                });
            }
        }
        
        return JsonSerializer.Serialize(new
        {
            VmName = vmName,
            EnabledMetrics = enabledMetrics,
            TotalEnabled = enabledMetrics.Count(m => ((dynamic)m).Status.ToString() == "Enabled"),
            Method = "ControlMetrics",
            Backend = "WMI"
        });
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to enable metrics collection for VM: {ex.Message}", ex);
    }
}
    /// <summary>
    /// Acts as a wrapper around the MetricService::ControlMetrics WMI method. It is used to 
    /// enable, disable, or reset metrics.
    /// Based on Microsoft Metrics/ControlMetrics.cs ControlMetrics method.
    /// </summary>
    /// <param name="managedElementPath">The path to the managed element for which to control
    /// metrics. This can be the path to a Cim_ResourceAllocationSettingData derived class, a
    /// Msvm_ComputerSystem, or a Cim_ResourcePool derived class. Null indicates all virtual 
    /// machines.</param>
    /// <param name="metricDefinitionPath">The path to the metric definition to control metrics
    /// for. Null indicates all definitions.</param>
    /// <param name="operation">The MetricOperation (2=Enable, 3=Disable, 4=Reset).</param>
    /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
    private void ControlMetrics(
        string managedElementPath,
        string? metricDefinitionPath,
        uint operation,
        ManagementScope scope)
    {
        using var metricService = GetMetricService(scope);
        using var inParams = metricService.GetMethodParameters("ControlMetrics");
        
        inParams["Subject"] = managedElementPath;
        inParams["Definition"] = metricDefinitionPath;
        inParams["MetricCollectionEnabled"] = operation;

        using var outParams = metricService.InvokeMethod("ControlMetrics", inParams, null);
        WmiUtilities.ValidateOutput(outParams, scope);
    }

    /// <summary>
    /// Gets the metric service.
    /// Based on Microsoft Metrics/MetricUtilities.cs GetMetricService method.
    /// </summary>
    /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
    /// <returns>The metric service management object.</returns>
    private ManagementObject GetMetricService(ManagementScope scope)
    {
        using var metricServiceClass = new ManagementClass("Msvm_MetricService");
        metricServiceClass.Scope = scope;

        var metricService = WmiUtilities.GetFirstObjectFromCollection(metricServiceClass.GetInstances());
        return metricService;
    }

    private ManagementObject GetMetricDefinition(string metricDefinitionName, ManagementScope scope)
    {
        // Align with sample: Use derived classes if CIM_BaseMetricDefinition doesn't suffice; current query preserved but log for verification
        Console.WriteLine($"Querying metric definition: '{metricDefinitionName}'");
        var query = new ObjectQuery($"SELECT * FROM CIM_BaseMetricDefinition WHERE ElementName = '{metricDefinitionName}'");
        using var searcher = new ManagementObjectSearcher(scope, query);
        using var results = searcher.Get();
        
        var definition = results.Cast<ManagementObject>().FirstOrDefault();
        if (definition == null)
        {
            throw new InvalidOperationException($"Metric definition '{metricDefinitionName}' not found in CIM_BaseMetricDefinition");
        }
        
        return definition;
    }
}
