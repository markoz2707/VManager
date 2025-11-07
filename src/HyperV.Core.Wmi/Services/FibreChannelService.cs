using System;
using System.Linq;
using System.Management;
using System.Text.Json;
using HyperV.Core.Wmi;
using Microsoft.Extensions.Logging;

namespace HyperV.Core.Wmi.Services;

public class FibreChannelService
{
    private readonly ILogger _logger;

    public FibreChannelService(ILogger<FibreChannelService> logger)
    {
        _logger = logger;
    }

    private static Guid ExtractGuidFromInstanceId(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
            throw new ArgumentException("Invalid InstanceID");

        // InstanceID format: "Microsoft:...,Name=guid-string"
        var parts = instanceId.Split(',');
        foreach (var part in parts)
        {
            if (part.StartsWith("Name=") || part.StartsWith("InstanceID="))
            {
                var guidStr = part.Split('=')[1].Trim('"');
                return new Guid(guidStr);
            }
        }
        throw new ArgumentException("Could not extract GUID from InstanceID");
    }

    public string CreateSan(string sanName, string[] wwpnArray, string[] wwnnArray, string notes = "")
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var query = new ObjectQuery("SELECT * FROM Msvm_ResourcePoolConfigurationService");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var services = searcher.Get().Cast<ManagementObject>().ToArray();

            if (services.Length == 0)
            {
                throw new InvalidOperationException("No Msvm_ResourcePoolConfigurationService found.");
            }

            var configService = services[0];

            // Create PoolSettings
            var poolSettings = new ManagementClass(scope, new ManagementPath("Msvm_ResourceAllocationSettingData"), null).CreateInstance();
            poolSettings["ElementName"] = sanName;
            poolSettings["Notes"] = notes;
            poolSettings["ResourceType"] = 23u; // Fibre Channel
            poolSettings["ResourceSubType"] = "Microsoft:FibreChannel SAN Pool";

            // ParentPools: primordial pool
            var primordialQuery = new ObjectQuery("SELECT * FROM Msvm_ResourcePool WHERE InstanceID LIKE '%Primordial%' AND ResourceType=23");
            using var primordialSearcher = new ManagementObjectSearcher(scope, primordialQuery);
            var primordialPools = primordialSearcher.Get().Cast<ManagementObject>().ToArray();
            if (primordialPools.Length == 0)
            {
                throw new InvalidOperationException("No primordial Fibre Channel pool found.");
            }
            var parentPoolRef = primordialPools[0]["__PATH"];

            // AllocationSettings: array of Msvm_FibreChannelHostResourceAllocationSettingData
            var allocationSettings = new ManagementObject[wwpnArray.Length];
            for (int i = 0; i < wwpnArray.Length; i++)
            {
                var allocSetting = new ManagementClass(scope, new ManagementPath("Msvm_FibreChannelHostResourceAllocationSettingData"), null).CreateInstance();
                allocSetting["Address"] = wwpnArray[i];
                allocSetting["NodeAddress"] = wwnnArray.Length > i ? wwnnArray[i] : "";
                allocSetting["HostResource"] = parentPoolRef;
                allocationSettings[i] = allocSetting;
            }

            var inParams = configService.GetMethodParameters("CreatePool");
            inParams["PoolSettings"] = new object[] { poolSettings.GetText(TextFormat.WmiDtd20) };
            inParams["ParentPools"] = new object[] { parentPoolRef };
            inParams["AllocationSettings"] = allocationSettings.Select(a => a.GetText(TextFormat.WmiDtd20)).ToArray();

            var outParams = configService.InvokeMethod("CreatePool", inParams, null);
            WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, scope);

            var poolInstanceId = outParams["Pool"]?.ToString();
            var poolId = ExtractGuidFromInstanceId(poolInstanceId);

            var result = new
            {
                poolId,
                status = "Created successfully"
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create SAN {SanName}", sanName);
            throw;
        }
    }

    public void DeleteSan(Guid poolId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var query = new ObjectQuery("SELECT * FROM Msvm_ResourcePoolConfigurationService");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var services = searcher.Get().Cast<ManagementObject>().ToArray();

            if (services.Length == 0)
            {
                throw new InvalidOperationException("No Msvm_ResourcePoolConfigurationService found.");
            }

            var configService = services[0];

            var poolQuery = new ObjectQuery($"SELECT * FROM Msvm_ResourcePool WHERE InstanceID LIKE '%{poolId}%'");
            using var poolSearcher = new ManagementObjectSearcher(scope, poolQuery);
            var pools = poolSearcher.Get().Cast<ManagementObject>().ToArray();
            if (pools.Length == 0)
            {
                throw new InvalidOperationException($"SAN pool {poolId} not found.");
            }
            var poolRef = pools[0]["__PATH"];

            var inParams = configService.GetMethodParameters("DestroyPool");
            inParams["Pool"] = poolRef;

            var outParams = configService.InvokeMethod("DestroyPool", inParams, null);
            WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SAN {PoolId}", poolId);
            throw;
        }
    }

    public string GetSanInfo(Guid poolId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var poolQuery = new ObjectQuery($"SELECT * FROM Msvm_ResourcePool WHERE InstanceID LIKE '%{poolId}%'");
            using var searcher = new ManagementObjectSearcher(scope, poolQuery);
            var pools = searcher.Get().Cast<ManagementObject>().ToArray();

            if (pools.Length == 0)
            {
                throw new InvalidOperationException($"SAN pool {poolId} not found.");
            }

            var pool = pools[0];
            var name = pool["ElementName"]?.ToString() ?? "";
            var notes = pool["Notes"]?.ToString() ?? "";

            // Get AllocationSettings
            var allocationQuery = new ObjectQuery($"ASSOCIATORS OF {{Msvm_ResourcePool.InstanceID='{pool["InstanceID"]}'}} WHERE AssocClass=Msvm_AllocationSettingsAssociation RESULTCLASS=Msvm_FibreChannelHostResourceAllocationSettingData");
            using var allocSearcher = new ManagementObjectSearcher(scope, allocationQuery);
            var allocations = allocSearcher.Get().Cast<ManagementObject>().ToArray();

            var allocInfos = allocations.Select(a => new
            {
                Address = a["Address"]?.ToString(),
                NodeAddress = a["NodeAddress"]?.ToString()
            }).ToArray();

            var info = new
            {
                name,
                notes,
                allocations = allocInfos
            };

            return JsonSerializer.Serialize(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SAN info for {PoolId}", poolId);
            throw;
        }
    }

    public string CreateVirtualFcPort(string vmName, string sanPoolId, string wwpn, string wwnn)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            // Find VM
            var vmQuery = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{vmName}'");
            using var vmSearcher = new ManagementObjectSearcher(scope, vmQuery);
            var vms = vmSearcher.Get().Cast<ManagementObject>().ToArray();
            if (vms.Length == 0)
            {
                throw new InvalidOperationException($"VM {vmName} not found.");
            }
            var vm = vms[0];
            var vsmsRef = vm["__PATH"];

            // Find management service
            var serviceQuery = new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService");
            using var serviceSearcher = new ManagementObjectSearcher(scope, serviceQuery);
            var services = serviceSearcher.Get().Cast<ManagementObject>().ToArray();
            if (services.Length == 0)
            {
                throw new InvalidOperationException("No Msvm_VirtualSystemManagementService found.");
            }
            var managementService = services[0];

            // Find SAN pool
            var poolQuery = new ObjectQuery($"SELECT * FROM Msvm_ResourcePool WHERE InstanceID LIKE '%{sanPoolId}%'");
            using var poolSearcher = new ManagementObjectSearcher(scope, poolQuery);
            var pools = poolSearcher.Get().Cast<ManagementObject>().ToArray();
            if (pools.Length == 0)
            {
                throw new InvalidOperationException($"SAN pool {sanPoolId} not found.");
            }
            var sanPoolRef = pools[0]["__PATH"];

            // Create allocation settings for FibreChannelVirtualPort
            var allocSetting = new ManagementClass(scope, new ManagementPath("Msvm_FibreChannelVirtualPortAllocationSettingData"), null).CreateInstance();
            allocSetting["Address"] = wwpn;
            allocSetting["NodeAddress"] = wwnn;
            allocSetting["HostResource"] = sanPoolRef;
            allocSetting["ResourceType"] = 23u;
            allocSetting["ResourceSubType"] = "Microsoft:Fibre Channel HBA";

            var inParams = managementService.GetMethodParameters("AddResourceSettings");
            inParams["AffectedSystem"] = vsmsRef;
            inParams["ResourceSettings"] = new object[] { allocSetting.GetText(TextFormat.WmiDtd20) };

            var outParams = managementService.InvokeMethod("AddResourceSettings", inParams, null);
            WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, scope);

            var portId = ExtractGuidFromInstanceId(allocSetting["InstanceID"]?.ToString());

            var result = new
            {
                portId
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create virtual FC port for VM {VmName}", vmName);
            throw;
        }
    }
}