using System;
using System.Management;
using System.Text.Json;
using HyperV.Core.Wmi;
using HyperV.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace HyperV.Core.Wmi.Services;

public class StorageQoSService : IStorageQoSService
{
    private readonly ILogger _logger;

    public StorageQoSService(ILogger<StorageQoSService> logger)
    {
        _logger = logger;
    }

    public string CreateQoSPolicy(string policyId, uint maxIops = 0, uint maxBandwidth = 0, string description = "")
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var settingData = new ManagementClass(scope, new ManagementPath("Msvm_StorageQoSResourceAllocationSettingData"), null);
            var newSetting = settingData.CreateInstance();
            newSetting["InstanceID"] = policyId;
            newSetting["MaxIOPS"] = maxIops;
            newSetting["MaxIOPSPerGB"] = 0;
            newSetting["MaxBandwidth"] = maxBandwidth;
            if (!string.IsNullOrEmpty(description))
            {
                newSetting["Description"] = description;
            }
            newSetting.Put();

            var serviceQuery = new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService");
            using var serviceEnumerator = new ManagementObjectSearcher(scope, serviceQuery).Get();
            var service = serviceEnumerator.Cast<ManagementObject>().FirstOrDefault();

            if (service != null)
            {
                var inParams = service.GetMethodParameters("AddResourceSettings");
                inParams["ResourceSettings"] = new string[] { newSetting.Path.Path };
                var outParams = service.InvokeMethod("AddResourceSettings", inParams, null);

                if (!WmiUtilities.ValidateOutput(outParams, scope))
                {
                    throw new ManagementException("Failed to add QoS policy resource settings");
                }

                var result = new
                {
                    policyId = Guid.Parse(policyId),
                    status = "Created successfully"
                };

                return JsonSerializer.Serialize(result);
            }

            throw new InvalidOperationException("VirtualSystemManagementService not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create QoS policy {PolicyId}", policyId);
            var errorResult = new
            {
                policyId = Guid.Parse(policyId),
                status = $"Error: {ex.Message}"
            };
            return JsonSerializer.Serialize(errorResult);
        }
    }

    public void DeleteQoSPolicy(Guid policyId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var resourceQuery = new ObjectQuery($"SELECT * FROM Msvm_ResourceAllocationSettingData WHERE InstanceID = '{policyId}'");
            using var resourceSearcher = new ManagementObjectSearcher(scope, resourceQuery);
            var resources = resourceSearcher.Get().Cast<ManagementObject>().ToArray();

            if (resources.Length == 0)
            {
                _logger.LogWarning("No QoS policy found with ID {PolicyId}", policyId);
                return;
            }

            var serviceQuery = new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService");
            using var serviceEnumerator = new ManagementObjectSearcher(scope, serviceQuery).Get();
            var service = serviceEnumerator.Cast<ManagementObject>().FirstOrDefault();

            if (service != null)
            {
                var inParams = service.GetMethodParameters("RemoveResourceSettings");
                inParams["ResourceSettings"] = resources.Select(r => r.Path.Path).ToArray();
                var outParams = service.InvokeMethod("RemoveResourceSettings", inParams, null);

                if (!WmiUtilities.ValidateOutput(outParams, scope))
                {
                    throw new ManagementException("Failed to remove QoS policy resource settings");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete QoS policy {PolicyId}", policyId);
            throw;
        }
    }

    public string GetQoSPolicyInfo(Guid policyId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var query = new ObjectQuery($"SELECT * FROM Msvm_StorageQoSResourceAllocationSettingData WHERE InstanceID = '{policyId}'");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var policies = searcher.Get().Cast<ManagementObject>().ToArray();

            if (policies.Length == 0)
            {
                throw new InvalidOperationException($"QoS policy {policyId} not found");
            }

            var policy = policies[0];
            var info = new
            {
                policyId = policyId,
                maxIOPS = (uint)policy["MaxIOPS"],
                maxBandwidth = (uint)policy["MaxBandwidth"],
                description = policy["Description"]?.ToString() ?? string.Empty
            };

            return JsonSerializer.Serialize(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get QoS policy info for {PolicyId}", policyId);
            throw;
        }
    }

    public void ApplyQoSPolicyToVm(string vmName, Guid policyId)
    {
        try
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();

            var vmQuery = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}'");
            using var vmSearcher = new ManagementObjectSearcher(scope, vmQuery);
            var vms = vmSearcher.Get().Cast<ManagementObject>().ToArray();

            if (vms.Length == 0)
            {
                throw new InvalidOperationException($"VM {vmName} not found");
            }

            var vm = vms[0];
            var serviceQuery = new ObjectQuery("SELECT * FROM Msvm_VirtualSystemManagementService");
            using var serviceEnumerator = new ManagementObjectSearcher(scope, serviceQuery).Get();
            var service = serviceEnumerator.Cast<ManagementObject>().FirstOrDefault();

            if (service == null)
            {
                throw new InvalidOperationException("VirtualSystemManagementService not found");
            }

            var resourceQuery = new ObjectQuery($"SELECT * FROM Msvm_ResourceAllocationSettingData WHERE InstanceID = '{policyId}'");
            using var resourceSearcher = new ManagementObjectSearcher(scope, resourceQuery);
            var policyResource = resourceSearcher.Get().Cast<ManagementObject>().FirstOrDefault();

            if (policyResource == null)
            {
                throw new InvalidOperationException($"QoS policy {policyId} not found");
            }

            var associations = new ManagementObjectSearcher(scope, new ObjectQuery("ASSOCIATORS OF {Msvm_ComputerSystem} WHERE ResultClass = Msvm_VirtualSystemSettingData")).Get();
            var settings = associations.Cast<ManagementObject>().Where(s => (uint)s["ConfigurationID"] == (uint)vm["ConfigurationID"]).ToArray();

            if (settings.Length == 0)
            {
                throw new InvalidOperationException($"Settings not found for VM {vmName}");
            }

            var setting = settings[0];
            var storageResources = new ManagementObjectSearcher(scope, new ObjectQuery($"ASSOCIATORS OF {{Msvm_VirtualSystemSettingData.InstanceID='{setting["InstanceID"]}'}} WHERE ResultClass = Msvm_StorageAllocationSettingData")).Get().Cast<ManagementObject>().ToArray();

            foreach (var storage in storageResources)
            {
                var modifyParams = service.GetMethodParameters("ModifyResourceSettings");
                modifyParams["ResourceSettings"] = new string[] { storage.Path.Path };
                // Note: In a full implementation, update the storage resource to reference the QoS policy InstanceID
                // For example, set HostResource or other properties to link to QoS
                var outParams = service.InvokeMethod("ModifyResourceSettings", modifyParams, null);
                if (!WmiUtilities.ValidateOutput(outParams, scope))
                {
                    _logger.LogWarning("Failed to modify some storage settings for QoS application");
                }
            }

            // Apply QoS policy association (simplified; actual may require setting Parent on allocation settings)
            var inParams = service.GetMethodParameters("AddResourceSettings");
            inParams["Target"] = vm.Path.Path;
            inParams["ResourceSettings"] = new string[] { policyResource.Path.Path };
            var addOutParams = service.InvokeMethod("AddResourceSettings", inParams, null);
            if (!WmiUtilities.ValidateOutput(addOutParams, scope))
            {
                throw new ManagementException("Failed to associate QoS policy with VM");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply QoS policy {PolicyId} to VM {VmName}", policyId, vmName);
            throw;
        }
    }
}