using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.Json;

namespace HyperV.Core.Wmi.Services;

public class ResourcePoolsService
{
    private readonly ManagementScope _scope;

    public ResourcePoolsService()
    {
        _scope = new ManagementScope(@"root\virtualization\v2");
    }

    /// <summary>
    /// Lists all resource pools with basic information.
    /// </summary>
    public virtual string ListResourcePools()
    {
        try
        {
            Console.WriteLine("Listing all WMI resource pools");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            var query = new ObjectQuery("SELECT * FROM Msvm_ResourcePool");
            using var searcher = new ManagementObjectSearcher(_scope, query);
            using var results = searcher.Get();
            
            var pools = new List<object>();
            foreach (ManagementObject pool in results)
            {
                using (pool)
                {
                    pools.Add(new
                    {
                        PoolId = pool["PoolID"]?.ToString(),
                        InstanceId = pool["InstanceID"]?.ToString(),
                        ResourceType = (ushort)pool["ResourceType"],
                        ResourceSubType = pool["ResourceSubType"]?.ToString(),
                        ElementName = pool["ElementName"]?.ToString(),
                        Backend = "WMI"
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                Count = pools.Count,
                ResourcePools = pools,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to list resource pools: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets detailed information for a specific resource pool.
    /// Based on Microsoft ResourcePools/MsvmResourcePool.cs DisplayResourcePool and DisplayPoolVerbose.
    /// </summary>
    public virtual string GetResourcePool(string resourceType, string resourceSubType, string poolId)
    {
        try
        {
            Console.WriteLine($"Getting resource pool: Type='{resourceType}', SubType='{resourceSubType}', PoolId='{poolId}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var pool = WmiUtilities.GetResourcePool(resourceType, resourceSubType, poolId, _scope);
            
            var poolInfo = new
            {
                PoolId = pool["PoolID"]?.ToString(),
                InstanceId = pool["InstanceID"]?.ToString(),
                ResourceType = (ushort)pool["ResourceType"],
                ResourceSubType = pool["ResourceSubType"]?.ToString(),
                ElementName = pool["ElementName"]?.ToString(),
                Description = pool["Description"]?.ToString(),
                Backend = "WMI"
            };
            
            // Get allocation settings
            using var allocationSettings = pool.GetRelated(
                "Msvm_ResourceAllocationSettingData",
                "Msvm_AllocatedFromPool",
                null, null, null, null, false, null);
            
            var allocations = new List<object>();
            foreach (ManagementObject allocation in allocationSettings)
            {
                using (allocation)
                {
                    allocations.Add(new
                    {
                        Address = allocation["Address"]?.ToString(),
                        AllocationUnits = allocation["AllocationUnits"]?.ToString(),
                        VirtualQuantity = (ulong?)allocation["VirtualQuantity"],
                        Reservation = (ulong?)allocation["Reservation"],
                        Limit = (ulong?)allocation["Limit"],
                        Weight = (uint?)allocation["Weight"]
                    });
                }
            }
            
            // Get pool settings
            using var poolSettings = pool.GetRelated(
                "Msvm_ResourcePoolSettingData",
                "Msvm_SettingsDefineState",
                null, null, null, null, false, null);
            
            var settings = new List<object>();
            foreach (ManagementObject setting in poolSettings)
            {
                using (setting)
                {
                    settings.Add(new
                    {
                        ElementName = setting["ElementName"]?.ToString(),
                        Notes = setting["Notes"] as string[] ?? Array.Empty<string>()
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                ResourcePool = poolInfo,
                Allocations = allocations,
                Settings = settings,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get resource pool: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists child pools for a given resource pool.
    /// Based on Microsoft ResourcePools/MsvmResourcePool.cs DisplayChildPools using Msvm_ElementAllocatedFromPool.
    /// </summary>
    public virtual string GetChildPools(string resourceType, string resourceSubType, string poolId)
    {
        try
        {
            Console.WriteLine($"Getting child pools for: Type='{resourceType}', SubType='{resourceSubType}', PoolId='{poolId}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var pool = WmiUtilities.GetResourcePool(resourceType, resourceSubType, poolId, _scope);
            using var childCollection = pool.GetRelated(
                "Msvm_ResourcePool",
                "Msvm_ElementAllocatedFromPool",
                null, null, "Dependent", "Antecedent", false, null);
            
            var children = new List<object>();
            foreach (ManagementObject child in childCollection)
            {
                using (child)
                {
                    children.Add(new
                    {
                        PoolId = child["PoolID"]?.ToString(),
                        InstanceId = child["InstanceID"]?.ToString(),
                        ResourceType = (ushort)child["ResourceType"],
                        ResourceSubType = child["ResourceSubType"]?.ToString(),
                        ElementName = child["ElementName"]?.ToString(),
                        Backend = "WMI"
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                ParentPoolId = poolId,
                ChildPools = children,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get child pools: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists parent pools for a given resource pool.
    /// Based on Microsoft ResourcePools/MsvmResourcePool.cs DisplayParentPools using Msvm_ElementAllocatedFromPool.
    /// </summary>
    public virtual string GetParentPools(string resourceType, string resourceSubType, string poolId)
    {
        try
        {
            Console.WriteLine($"Getting parent pools for: Type='{resourceType}', SubType='{resourceSubType}', PoolId='{poolId}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var pool = WmiUtilities.GetResourcePool(resourceType, resourceSubType, poolId, _scope);
            using var parentCollection = pool.GetRelated(
                "Msvm_ResourcePool",
                "Msvm_ElementAllocatedFromPool",
                null, null, "Antecedent", "Dependent", false, null);
            
            var parents = new List<object>();
            foreach (ManagementObject parent in parentCollection)
            {
                using (parent)
                {
                    parents.Add(new
                    {
                        PoolId = parent["PoolID"]?.ToString(),
                        InstanceId = parent["InstanceID"]?.ToString(),
                        ResourceType = (ushort)parent["ResourceType"],
                        ResourceSubType = parent["ResourceSubType"]?.ToString(),
                        ElementName = parent["ElementName"]?.ToString(),
                        Backend = "WMI"
                    });
                }
            }
            
            return JsonSerializer.Serialize(new
            {
                ChildPoolId = poolId,
                ParentPools = parents,
                Backend = "WMI"
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get parent pools: {ex.Message}", ex);
        }
    }
}