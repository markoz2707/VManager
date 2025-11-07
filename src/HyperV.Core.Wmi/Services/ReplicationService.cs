using System;
using System.Linq;
using System.Management;
using System.Text.Json;
using System.Threading;
using HyperV.Core.Wmi;
using HyperV.Contracts;
using Microsoft.Extensions.Logging;

namespace HyperV.Core.Wmi.Services
{
    public class ReplicationService
    {
        private readonly ILogger _logger;

        public ReplicationService(ILogger<ReplicationService> logger)
        {
            _logger = logger;
        }

        private ManagementScope GetScope()
        {
            var scope = new ManagementScope(@"root\virtualization\v2");
            scope.Connect();
            return scope;
        }

        private ManagementObject GetReplicationService()
        {
            var scope = GetScope();
            var query = new ObjectQuery("SELECT * FROM Msvm_ReplicationService");
            using var searcher = new ManagementObjectSearcher(scope, query);
            var collection = searcher.Get();
            var service = collection.OfType<ManagementObject>().FirstOrDefault();
            if (service == null)
            {
                _logger.LogError("Msvm_ReplicationService not found");
                throw new InvalidOperationException("Replication service not available");
            }
            return service;
        }

        public string CreateReplicationRelationship(string sourceVm, string targetHost, string authMode = "Certificate")
        {
            try
            {
                using var service = GetReplicationService();
                var authType = authMode switch
                {
                    "Certificate" => 0,
                    "Kerberos" => 1,
                    _ => 0
                };

                var inParams = service.GetMethodParameters("CreateReplicationRelationship");
                inParams["SourceSystem"] = sourceVm;
                inParams["DestinationSystem"] = targetHost;
                inParams["AuthenticationType"] = authType;

                var outParams = service.InvokeMethod("CreateReplicationRelationship", inParams, null);
                WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject ?? throw new InvalidOperationException("Invalid WMI output"), service.Scope);

                var job = outParams["Job"] as ManagementObject;
                string relationshipId = outParams["ReplicationRelationship"]?.ToString() ?? string.Empty;
                string status = job != null ? GetJobStatus(job) : "Completed";

                var result = new { relationshipId, status };
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create replication relationship for VM {SourceVm} to {TargetHost}", sourceVm, targetHost);
                throw;
            }
        }

        public void StartReplication(string vmName)
        {
            try
            {
                using var service = GetReplicationService();
                var inParams = service.GetMethodParameters("StartReplication");
                inParams["AffectedSystem"] = vmName;

                var outParams = service.InvokeMethod("StartReplication", inParams, null);
                WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, service.Scope);

                var job = outParams["Job"] as ManagementObject;
                if (job != null)
                {
                    WaitForJob(job, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start replication for VM {VmName}", vmName);
                throw;
            }
        }

        public string InitiateFailover(string vmName, string mode = "Planned")
        {
            try
            {
                using var service = GetReplicationService();
                var failoverType = mode switch
                {
                    "Planned" => 1,
                    "Test" => 2,
                    "Live" => 3,
                    _ => 1
                };

                var inParams = service.GetMethodParameters("InitiateFailover");
                inParams["AffectedSystem"] = vmName;
                inParams["FailoverType"] = failoverType;

                var outParams = service.InvokeMethod("InitiateFailover", inParams, null);
                WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, service.Scope);

                var job = outParams["Job"] as ManagementObject;
                string status = job != null ? GetJobStatus(job) : "Completed";

                var result = new { jobId = job?.GetPropertyValue("InstanceID")?.ToString() ?? string.Empty, status };
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate failover for VM {VmName} with mode {Mode}", vmName, mode);
                throw;
            }
        }

        public void ReverseReplicationRelationship(string vmName)
        {
            try
            {
                using var service = GetReplicationService();
                var inParams = service.GetMethodParameters("ReverseReplicationRelationship");
                inParams["AffectedSystem"] = vmName;

                var outParams = service.InvokeMethod("ReverseReplicationRelationship", inParams, null);
                WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, service.Scope);

                var job = outParams["Job"] as ManagementObject;
                if (job != null)
                {
                    WaitForJob(job, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reverse replication relationship for VM {VmName}", vmName);
                throw;
            }
        }

        public string GetReplicationState(string vmName)
        {
            try
            {
                var scope = GetScope();
                var query = new ObjectQuery($"SELECT EnabledState, ReplicationHealth FROM Msvm_ReplicationRelationship WHERE SourceSystem LIKE '%{vmName}%'");
                using var searcher = new ManagementObjectSearcher(scope, query);
                var collection = searcher.Get();
                var relationship = collection.OfType<ManagementObject>().FirstOrDefault();

                if (relationship == null)
                {
                    return JsonSerializer.Serialize(new { error = "Relationship not found" });
                }

                var enabledState = relationship["EnabledState"]?.ToString() ?? "Unknown";
                var health = relationship["ReplicationHealth"]?.ToString() ?? "Unknown";

                var state = new { vmName, enabledState, replicationHealth = health };
                return JsonSerializer.Serialize(state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get replication state for VM {VmName}", vmName);
                throw;
            }
        }

        public void AddAuthorizationEntry(string relationshipId, string entry)
        {
            try
            {
                using var service = GetReplicationService();
                var inParams = service.GetMethodParameters("AddAuthorizationEntry");
                inParams["ReplicationRelationship"] = relationshipId;
                inParams["AuthorizationEntry"] = entry;

                var outParams = service.InvokeMethod("AddAuthorizationEntry", inParams, null);
                WmiUtilities.ValidateOutput(outParams["ReturnValue"] as ManagementBaseObject, service.Scope);

                var job = outParams["Job"] as ManagementObject;
                if (job != null)
                {
                    WaitForJob(job, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add authorization entry for relationship {RelationshipId}", relationshipId);
                throw;
            }
        }

        private string GetJobStatus(ManagementObject job)
        {
            if (job == null) return "Unknown";
            var state = (uint)job["JobState"];
            return state switch
            {
                3 => "Running",
                4 => "Completed",
                5 => "Failed",
                6 => "Completed with Errors",
                7 => "Cancelled",
                _ => "Unknown"
            };
        }

        private void WaitForJob(ManagementObject job, ILogger logger)
        {
            if (job == null) return;
            while ((uint)job["JobState"] == 3) // Running
            {
                Thread.Sleep(1000);
                job.Get();
            }
            var state = (uint)job["JobState"];
            if (state == 5 || state == 6 || state == 7)
            {
                logger.LogWarning("Job completed with errors: {State}", state);
            }
            job.Dispose();
        }

        /// <summary>Checks if a VM exists for replication operations.</summary>
        public bool IsVmPresent(string vmName)
        {
            try
            {
                var scope = GetScope();
                var query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}' OR Name = '{vmName}'");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var collection = searcher.Get();
                return collection.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking VM presence for replication: {VmName}", vmName);
                return false;
            }
        }

        /// <summary>Saves VM state.</summary>
        public void SaveVm(string vmName)
        {
            try
            {
                var scope = GetScope();
                var query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}' OR Name = '{vmName}'");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var collection = searcher.Get();
                
                foreach (ManagementObject vm in collection)
                {
                    using (vm)
                    {
                        var inParams = vm.GetMethodParameters("RequestStateChange");
                        inParams["RequestedState"] = 32769; // Save State
                        
                        var outParams = vm.InvokeMethod("RequestStateChange", inParams, null);
                        var returnValue = (uint)outParams["ReturnValue"];
                        
                        if (returnValue != 0 && returnValue != 4096)
                        {
                            throw new InvalidOperationException($"Failed to save VM state. Return code: {returnValue}");
                        }
                        
                        var job = outParams["Job"] as ManagementObject;
                        if (job != null)
                        {
                            WaitForJob(job, _logger);
                        }
                        return;
                    }
                }
                
                throw new InvalidOperationException($"VM {vmName} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save VM state for {VmName}", vmName);
                throw;
            }
        }

        /// <summary>Modifies VM configuration.</summary>
        public void ModifyVm(string vmName, string configuration)
        {
            try
            {
                var scope = GetScope();
                var query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName = '{vmName}' OR Name = '{vmName}'");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var collection = searcher.Get();
                
                foreach (ManagementObject vm in collection)
                {
                    using (vm)
                    {
                        // This is a placeholder implementation
                        // Full implementation would require modifying Msvm_VirtualSystemSettingData
                        _logger.LogInformation("VM modification requested for {VmName} with configuration: {Configuration}", vmName, configuration);
                        throw new NotImplementedException("VM modification via ReplicationService not yet implemented");
                    }
                }
                
                throw new InvalidOperationException($"VM {vmName} not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to modify VM {VmName}", vmName);
                throw;
            }
        }
    }
}
