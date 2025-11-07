using System;
using System.Globalization;
using System.Management;
using System.Threading;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace HyperV.Core.Wmi
{
    enum JobState
    {
        New = 2,
        Starting = 3,
        Running = 4,
        Suspended = 5,
        ShuttingDown = 6,
        Completed = 7,
        Terminated = 8,
        Killed = 9,
        Exception = 10,
        CompletedWithWarnings = 32768
    }

    public static class VirtualSystemTypeNames
    {
        public const string RealizedVM = "Microsoft:Hyper-V:System:Realized";
        public const string PlannedVM = "Microsoft:Hyper-V:System:Planned";
        public const string RealizedSnapshot = "Microsoft:Hyper-V:Snapshot:Realized";
        public const string RecoverySnapshot = "Microsoft:Hyper-V:Snapshot:Recovery";
        public const string PlannedSnapshot = "Microsoft:Hyper-V:Snapshot:Planned";
        public const string MissingSnapshot = "Microsoft:Hyper-V:Snapshot:Missing";
        public const string ReplicaStandardRecoverySnapshot = "Microsoft:Hyper-V:Snapshot:Replica:Standard";
        public const string ReplicaApplicationConsistentRecoverySnapshot = "Microsoft:Hyper-V:Snapshot:Replica:ApplicationConsistent";
        public const string ReplicaPlannedRecoverySnapshot = "Microsoft:Hyper-V:Snapshot:Replica:PlannedFailover";
        public const string ReplicaSettings = "Microsoft:Hyper-V:Replica";
    }

    public static class WmiUtilities
    {
        /// <summary>
        /// Gets the Msvm_ComputerSystem instance that matches the host computer system.
        /// </summary>
        /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
        /// <returns>The Msvm_ComputerSystem instance for the host computer system.</returns>
        public static ManagementObject GetHostComputerSystem(ManagementScope scope)
        {
            return GetVirtualMachine(Environment.MachineName, scope);
        }

        /// <summary>
        /// Gets the Msvm_ComputerSystem instance for the host computer system with name hostName.
        /// </summary>
        /// <param name="hostName">Host computer system name.</param>
        /// <param name="scope">The ManagementScope to use to connect to WMI.</param>
        /// <returns>The Msvm_ComputerSystem instance for the host computer system.</returns>
        public static ManagementObject GetHostComputerSystem(string hostName, ManagementScope scope)
        {
            return GetVirtualMachine(hostName, scope);
        }

        public static bool ValidateOutput(ManagementBaseObject outputParameters, ManagementScope scope)
        {
            return ValidateOutput(outputParameters, scope, true, false);
        }

        public static bool ValidateOutput(ManagementBaseObject outputParameters, ManagementScope scope, bool throwIfFailed, bool printErrors)
        {
            bool succeeded = true;
            string errorMessage = "The method call failed.";

            if ((uint)outputParameters["ReturnValue"] == 4096)
            {
                using (ManagementObject job = new ManagementObject((string)outputParameters["Job"]))
                {
                    job.Scope = scope;

                    while (!IsJobComplete(job["JobState"]))
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        job.Get();
                    }

                    if (!IsJobSuccessful(job["JobState"]))
                    {
                        succeeded = false;

                        if (!string.IsNullOrEmpty((string)job["ErrorDescription"]))
                        {
                            errorMessage = (string)job["ErrorDescription"];
                        }

                        if (printErrors)
                        {
                            PrintMsvmErrors(job);
                        }

                        if (throwIfFailed)
                        {
                            throw new ManagementException(errorMessage);
                        }
                    }
                }
            }
            else if ((uint)outputParameters["ReturnValue"] != 0)
            {
                succeeded = false;

                if (throwIfFailed)
                {
                    throw new ManagementException(errorMessage);
                }
            }

            return succeeded;
        }

        public static void PrintMsvmErrors(ManagementObject job)
        {
            string[] errorList;

            using (ManagementBaseObject inParams = job.GetMethodParameters("GetErrorEx"))
            using (ManagementBaseObject outParams = job.InvokeMethod("GetErrorEx", inParams, null))
            {
                if ((uint)outParams["ReturnValue"] != 0)
                {
                    throw new ManagementException(string.Format(CultureInfo.CurrentCulture,
                                                                "GetErrorEx() call on the job failed"));
                }

                errorList = (string[])outParams["Errors"];
            }

            if (errorList == null)
            {
                Console.WriteLine("No errors found.");
                return;
            }

            Console.WriteLine("Detailed errors: \n");

            foreach (string error in errorList)
            {
                string errorSource = string.Empty;
                string errorMessage = string.Empty;
                int propId = 0;
                
                XmlReader reader = XmlReader.Create(new StringReader(error));

                while (reader.Read())
                {
                    if (reader.Name.Equals("PROPERTY", StringComparison.OrdinalIgnoreCase))
                    {
                        propId = 0;

                        if (reader.HasAttributes)
                        {
                            string propName = reader.GetAttribute(0);

                            if (propName.Equals("ErrorSource", StringComparison.OrdinalIgnoreCase))
                            {
                                propId = 1;
                            }
                            else if (propName.Equals("Message", StringComparison.OrdinalIgnoreCase))
                            {
                                propId = 2;
                            }
                        }
                    }
                    else if (reader.Name.Equals("VALUE", StringComparison.OrdinalIgnoreCase))
                    {
                        if (propId == 1)
                        {
                            errorSource = reader.ReadElementContentAsString();
                        }
                        else if (propId == 2)
                        {
                            errorMessage = reader.ReadElementContentAsString();
                        }

                        propId = 0;
                    }
                    else
                    {
                        propId = 0;
                    }
                }

                Console.WriteLine("Error Message: {0}", errorMessage);
                Console.WriteLine("Error Source:  {0}\n", errorSource);
            }
        }

        public static ManagementObject GetVirtualMachine(string name, ManagementScope scope)
        {
            return GetVmObject(name, "Msvm_ComputerSystem", scope);
        }
        
        public static ManagementObject GetPlannedVirtualMachine(string name, ManagementScope scope)
        {
            return GetVmObject(name, "Msvm_PlannedComputerSystem", scope);
        }

        private static ManagementObject GetVmObject(string name, string className, ManagementScope scope)
        {
            string vmQueryWql = string.Format(CultureInfo.InvariantCulture,
                "SELECT * FROM {0} WHERE ElementName=\"{1}\"", className, name);

            SelectQuery vmQuery = new SelectQuery(vmQueryWql);

            using (ManagementObjectSearcher vmSearcher = new ManagementObjectSearcher(scope, vmQuery))
            using (ManagementObjectCollection vmCollection = vmSearcher.Get())
            {
                if (vmCollection.Count == 0)
                {
                    throw new ManagementException(string.Format(CultureInfo.CurrentCulture,
                        "No {0} could be found with name \"{1}\"",
                        className,
                        name));
                }
                
                ManagementObject vm = GetFirstObjectFromCollection(vmCollection);

                return vm;
            }
        }
        
        public static ManagementObject GetVirtualMachineSettings(ManagementObject virtualMachine)
        {
            using (ManagementObjectCollection settingsCollection = 
                    virtualMachine.GetRelated("Msvm_VirtualSystemSettingData", "Msvm_SettingsDefineState",
                    null, null, null, null, false, null))
            {
                ManagementObject virtualMachineSettings =
                    GetFirstObjectFromCollection(settingsCollection);

                return virtualMachineSettings;
            }
        }

        public static ManagementObject GetResourcePool(string resourceType, string resourceSubtype, string poolId, ManagementScope scope)
        {
            string poolQueryWql;
            
            // Escape the poolId for WQL query
            string escapedPoolId = poolId.Replace("\\", "\\\\");

            if (resourceType == "1") // OtherResourceType
            {
                poolQueryWql = string.Format(CultureInfo.InvariantCulture,
                    "SELECT * FROM CIM_ResourcePool WHERE ResourceType=\"{0}\" AND " +
                    "OtherResourceType=\"{1}\" AND PoolId=\"{2}\"",
                    resourceType, resourceSubtype, escapedPoolId);
            }
            else
            {
                poolQueryWql = string.Format(CultureInfo.InvariantCulture,
                    "SELECT * FROM CIM_ResourcePool WHERE ResourceType=\"{0}\" AND " +
                    "ResourceSubType=\"{1}\" AND PoolId=\"{2}\"",
                    resourceType, resourceSubtype, escapedPoolId);
            }

            SelectQuery poolQuery = new SelectQuery(poolQueryWql);
            
            using (ManagementObjectSearcher poolSearcher = new ManagementObjectSearcher(scope, poolQuery))
            using (ManagementObjectCollection poolCollection = poolSearcher.Get())
            {
                if (poolCollection.Count != 1)
                {
                    throw new ManagementException(string.Format(CultureInfo.CurrentCulture,
                        "A single CIM_ResourcePool derived instance could not be found for " +
                        "ResourceType \"{0}\", ResourceSubtype \"{1}\" and PoolId \"{2}\"",
                        resourceType, resourceSubtype, poolId));
                }

                ManagementObject pool = GetFirstObjectFromCollection(poolCollection);

                return pool!;
            }
        }

        public static ManagementObjectCollection GetResourcePools(string resourceType, string resourceSubtype, ManagementScope scope)
        {
            string poolQueryWql;

            if (resourceType == "1") // OtherResourceType
            {
                poolQueryWql = string.Format(CultureInfo.InvariantCulture,
                    "SELECT * FROM CIM_ResourcePool WHERE ResourceType=\"{0}\" AND " +
                    "OtherResourceType=\"{1}\"",
                    resourceType, resourceSubtype);
            }
            else
            {
                poolQueryWql = string.Format(CultureInfo.InvariantCulture,
                    "SELECT * FROM CIM_ResourcePool WHERE ResourceType=\"{0}\" AND " +
                    "ResourceSubType=\"{1}\"",
                    resourceType, resourceSubtype);
            }

            SelectQuery poolQuery = new SelectQuery(poolQueryWql);

            using (ManagementObjectSearcher poolSearcher = new ManagementObjectSearcher(scope, poolQuery))
            {
                return poolSearcher.Get();
            }
        }

        public static ManagementObject[] GetVhdSettings(ManagementObject virtualMachine)
        {
            using (ManagementObject vssd = WmiUtilities.GetVirtualMachineSettings(virtualMachine))
            {
                return GetVhdSettingsFromVirtualMachineSettings(vssd);
            }
        }

        public static ManagementObject[] GetVhdSettingsFromVirtualMachineSettings(ManagementObject virtualMachineSettings)
        {
            const UInt16 SASDResourceTypeLogicalDisk = 31;

            List<ManagementObject> sasdList = new List<ManagementObject>();

            using (ManagementObjectCollection sasdCollection =
                virtualMachineSettings.GetRelated("Msvm_StorageAllocationSettingData",
                    "Msvm_VirtualSystemSettingDataComponent",
                    null, null, null, null, false, null))
            {
                foreach (ManagementObject sasd in sasdCollection)
                {
                    if ((UInt16)sasd["ResourceType"] == SASDResourceTypeLogicalDisk)
                    {
                        sasdList.Add(sasd);
                    }
                    else
                    {
                        sasd.Dispose();
                    }
                }
            }

            if (sasdList.Count == 0)
            {
                return null!;
            }
            else
            {
                return sasdList.ToArray();
            }
        }

        public static ManagementObject GetVirtualMachineManagementService(ManagementScope scope)
        {
            using (ManagementClass managementServiceClass =
                new ManagementClass("Msvm_VirtualSystemManagementService"))
            {
                managementServiceClass.Scope = scope;

                ManagementObject managementService =
                    GetFirstObjectFromCollection(managementServiceClass.GetInstances());

                return managementService!;
            }
        }

        public static ManagementObject GetVirtualMachineManagementServiceSettings(ManagementScope scope)
        {
            using (ManagementClass serviceSettingsClass =
                new ManagementClass("Msvm_VirtualSystemManagementServiceSettingData"))
            {
                serviceSettingsClass.Scope = scope;

                ManagementObject serviceSettings =
                    GetFirstObjectFromCollection(serviceSettingsClass.GetInstances());

                return serviceSettings!;
            }
        }

        public static ManagementObject GetVirtualMachineSnapshotService(ManagementScope scope)
        {
            using (ManagementClass snapshotServiceClass =
                new ManagementClass("Msvm_VirtualSystemSnapshotService"))
            {
                snapshotServiceClass.Scope = scope;

                ManagementObject snapshotService =
                    GetFirstObjectFromCollection(snapshotServiceClass.GetInstances());

                return snapshotService!;
            }
        }
        
        public static ManagementObject GetFirstObjectFromCollection(ManagementObjectCollection collection)
        {
            if (collection.Count == 0)
            {
                throw new ArgumentException("The collection contains no objects", "collection");
            }

            foreach (ManagementObject managementObject in collection)
            {
                return managementObject;
            }

            return null!;
        }

        public static string EscapeObjectPath(string objectPath)
        {
            string escapedObjectPath = objectPath.Replace("\\", "\\\\");
            escapedObjectPath = escapedObjectPath.Replace("\"", "\\\"");

            return escapedObjectPath;
        }

        private static bool IsJobComplete(object jobStateObj)
        {
            JobState jobState = (JobState)((ushort)jobStateObj);

            return (jobState == JobState.Completed) || 
                (jobState == JobState.CompletedWithWarnings) ||(jobState == JobState.Terminated) ||
                (jobState == JobState.Exception) || (jobState == JobState.Killed);
        }

        private static bool IsJobSuccessful(object jobStateObj)
        {
            JobState jobState = (JobState)((ushort)jobStateObj);

            return (jobState == JobState.Completed) || (jobState == JobState.CompletedWithWarnings);
        }
    }
}
