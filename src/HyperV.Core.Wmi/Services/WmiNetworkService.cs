using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;

namespace HyperV.Core.Wmi.Services;

public class WmiNetworkService
{
    private readonly ManagementScope _scope;

    public WmiNetworkService()
    {
        // Do not connect in ctor to avoid DI activation failures when running without admin or WMI not available
        _scope = new ManagementScope(@"\\.\root\virtualization\v2");
    }

    /// <summary>
    /// Lists physical network adapters for external switch creation.
    /// </summary>
    public virtual IEnumerable<PhysicalAdapterInfo> ListPhysicalAdapters()
    {
        var cimScope = new ManagementScope(@"\\.\root\cimv2");
        try
        {
            cimScope.Connect();
        }
        catch
        {
            yield break;
        }

        var query = new ObjectQuery("SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter = TRUE");
        using var searcher = new ManagementObjectSearcher(cimScope, query);
        ManagementObjectCollection results;
        try
        {
            results = searcher.Get();
        }
        catch
        {
            yield break;
        }

        foreach (ManagementObject mo in results)
        {
            using (mo)
            {
                var name = TryString(mo, "NetConnectionID") ?? TryString(mo, "Name") ?? string.Empty;
                var pnp = TryString(mo, "PNPDeviceID") ?? string.Empty;
                var guid = TryString(mo, "GUID") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;
                yield return new PhysicalAdapterInfo { Name = name, Guid = guid, PnpDeviceId = pnp };
            }
        }
    }

    /// <summary>
    /// Lists virtual switches using Msvm_VirtualEthernetSwitch.
    /// Returns Id (GUID) and Name (ElementName). Never throws; returns empty on errors.
    /// </summary>
    public virtual IEnumerable<SwitchInfo> ListVirtualSwitches()
    {
        // Try connect lazily; on failure, return empty (no throw)
        try
        {
            if (!_scope.IsConnected)
            {
                _scope.Connect();
            }
        }
        catch
        {
            yield break;
        }

        ManagementObjectCollection? results = null;
        try
        {
            var query = new ObjectQuery("SELECT Name, ElementName FROM Msvm_VirtualEthernetSwitch");
            using var searcher = new ManagementObjectSearcher(_scope, query);
            results = searcher.Get();
        }
        catch
        {
            yield break; 
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ManagementObject mo in results)
        {
            using (mo)
            {
                var id = TryString(mo, "Name") ?? string.Empty;           // GUID
                var name = TryString(mo, "ElementName") ?? id;             // Friendly name

                if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
                    continue;

                if (!string.IsNullOrEmpty(id))
                {
                    if (!seen.Add(id))
                        continue; // skip duplicates
                }

                yield return new SwitchInfo { Id = id, Name = name };
            }
        }
    }

    /// <summary>
    /// Returns a more detailed list with type, notes and basic metadata.
    /// </summary>
    public virtual IEnumerable<SwitchSummary> ListVirtualSwitchesSummary()
    {
        foreach (var sw in ListVirtualSwitches())
        {
            var details = GetSwitchSettings(sw.Id);
            yield return new SwitchSummary
            {
                Id = sw.Id,
                Name = sw.Name,
                Type = details.switchType,
                Notes = details.notes
            };
        }
    }

    /// <summary>
    /// Creates a virtual switch (Internal/Private/External supported). Returns the switch Guid.
    /// Based on Microsoft CreateSwitch sample from Windows-classic-samples.
    /// </summary>
    public virtual Guid CreateVirtualSwitch(string name, WmiSwitchType type, string? notes = null, string? externalAdapterName = null, bool allowManagementOS = false)
    {
        // Lazy connect
        if (!_scope.IsConnected)
            _scope.Connect();

        return type switch
        {
            WmiSwitchType.Private => CreatePrivateSwitch(name, notes),
            WmiSwitchType.Internal => CreateInternalSwitch(name, notes),
            WmiSwitchType.External => CreateExternalSwitch(name, notes, externalAdapterName!, allowManagementOS),
            _ => throw new ArgumentException($"Unsupported switch type: {type}")
        };
    }

    /// <summary>
    /// Creates a private virtual switch (no host access).
    /// </summary>
    private Guid CreatePrivateSwitch(string name, string? notes)
    {
        var ports = new string[0]; // No ports for private switch
        return CreateSwitchInternal(name, notes, ports);
    }

    /// <summary>
    /// Creates an internal virtual switch (with host access).
    /// </summary>
    private Guid CreateInternalSwitch(string name, string? notes)
    {
        // Get the host computer system for internal port
        using var hostComputerSystem = GetHostComputerSystem();
        
        // Create internal port configuration
        using var portToCreateClass = new ManagementClass(_scope, new ManagementPath("Msvm_EthernetPortAllocationSettingData"), null);
        using var portToCreate = portToCreateClass.CreateInstance();
        
        portToCreate["ElementName"] = name + "_Internal";
        portToCreate["HostResource"] = new string[] { hostComputerSystem.Path.Path };
        
        var ports = new string[] { portToCreate.GetText(TextFormat.WmiDtd20) };
        return CreateSwitchInternal(name, notes, ports);
    }

    /// <summary>
    /// Creates an external-only virtual switch (no host access).
    /// </summary>
    private Guid CreateExternalOnlySwitch(string name, string? notes, string externalAdapterName)
    {
        using var externalAdapter = FindExternalAdapter(externalAdapterName);
        if (externalAdapter == null)
            throw new InvalidOperationException($"External adapter '{externalAdapterName}' not found");

        using var portToCreate = GetDefaultEthernetPortAllocationSettingData(_scope);
        portToCreate["ElementName"] = name + "_External";
        portToCreate["HostResource"] = new string[] { externalAdapter.Path.Path };

        var ports = new string[] { portToCreate.GetText(TextFormat.WmiDtd20) };
        return CreateSwitchInternal(name, notes, ports);
    }

    /// <summary>
    /// Creates an external virtual switch (with host access via internal port).
    /// </summary>
    private Guid CreateExternalSwitch(string name, string? notes, string externalAdapterName, bool allowManagementOS)
    {
        using var externalAdapter = FindExternalAdapter(externalAdapterName);
        if (externalAdapter == null)
            throw new InvalidOperationException($"External adapter '{externalAdapterName}' not found");

        using var hostComputerSystem = GetHostComputerSystem();

        using var externalPortToCreate = GetDefaultEthernetPortAllocationSettingData(_scope);
        externalPortToCreate["ElementName"] = name + "_External";
        externalPortToCreate["HostResource"] = new string[] { externalAdapter.Path.Path };

        using var internalPortToCreate = (ManagementObject)externalPortToCreate.Clone();
        internalPortToCreate["ElementName"] = name + "_Internal";
        internalPortToCreate["HostResource"] = new string[] { hostComputerSystem.Path.Path };
        internalPortToCreate["Address"] = externalAdapter["PermanentAddress"];

        var ports = new string[] { externalPortToCreate.GetText(TextFormat.WmiDtd20), internalPortToCreate.GetText(TextFormat.WmiDtd20) };
        return CreateSwitchInternal(name, notes, ports);
    }

    /// <summary>
    /// Core method to create the switch with specified ports.
    /// Based on Microsoft CreateSwitch sample implementation.
    /// </summary>
    private Guid CreateSwitchInternal(string name, string? notes, string[] ports)
    {
        // Create the switch settings object
        using var switchSettingsClass = new ManagementClass(_scope, new ManagementPath("Msvm_VirtualEthernetSwitchSettingData"), null);
        using var switchSettings = switchSettingsClass.CreateInstance();
        
        switchSettings["ElementName"] = name;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            switchSettings["Notes"] = new string[] { notes! };
        }
        
        var switchSettingsText = switchSettings.GetText(TextFormat.WmiDtd20);
        
        // Get the switch management service
        using var switchService = GetEthernetSwitchManagementService();
        using var inParams = switchService.GetMethodParameters("DefineSystem");
        
        inParams["SystemSettings"] = switchSettingsText;
        inParams["ReferenceConfiguration"] = null;
        inParams["ResourceSettings"] = ports;
        
        using var outParams = switchService.InvokeMethod("DefineSystem", inParams, null);
        WmiUtilities.ValidateOutput(outParams, _scope, true, true);

        // Extract GUID from ResultingSystem
        var resultingSystem = outParams["ResultingSystem"] as ManagementPath;
        if (resultingSystem != null)
        {
            var guidString = resultingSystem.Path.Split('=')[1].Trim('"');
            return Guid.Parse(guidString);
        }
        
        // Fallback: poll for the switch by name
        var guid = WaitForSwitchByName(name, timeoutMs: 10000);
        if (guid != Guid.Empty)
            return guid;
        
        throw new InvalidOperationException($"Switch '{name}' was created successfully but could not retrieve its ID");
    }

    /// <summary>
    /// Gets the Ethernet Switch Management Service.
    /// </summary>
    private ManagementObject GetEthernetSwitchManagementService()
    {
        var query = new ObjectQuery("SELECT * FROM Msvm_VirtualEthernetSwitchManagementService");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        using var results = searcher.Get();
        return results.Cast<ManagementObject>().FirstOrDefault()
               ?? throw new InvalidOperationException("Could not find Msvm_VirtualEthernetSwitchManagementService");
    }

    /// <summary>
    /// Gets the host computer system for internal switch ports.
    /// </summary>
    private ManagementObject GetHostComputerSystem()
    {
        // Microsoft uses Environment.MachineName to find the host computer system
        var hostName = Environment.MachineName;
        var query = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName=\"{hostName}\"");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        using var results = searcher.Get();
        return results.Cast<ManagementObject>().FirstOrDefault()
               ?? throw new InvalidOperationException($"Could not find host computer system with name '{hostName}'");
    }

    /// <summary>
    /// Gets default Msvm_EthernetPortAllocationSettingData instance.
    /// </summary>
    private ManagementObject GetDefaultEthernetPortAllocationSettingData(ManagementScope scope)
    {
        var portClass = new ManagementClass(scope, new ManagementPath("Msvm_EthernetPortAllocationSettingData"), null);
        var port = portClass.CreateInstance();

        port["ElementName"] = "DefaultPort";
        port["HostResource"] = new string[0]; // No host resource initially
        port["AddressType"] = 1; // MAC address
        port["AddressSize"] = 6; // MAC size
        port["Address"] = new byte[6]; // Empty MAC

        return port;
    }

    /// <summary>
    /// Polls for switch by name until found or timeout.
    /// </summary>
    private Guid WaitForSwitchByName(string elementName, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var query = new ObjectQuery($"SELECT Name FROM Msvm_VirtualEthernetSwitch WHERE ElementName='{elementName}'");
                using var searcher = new ManagementObjectSearcher(_scope, query);
                var mo = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (mo != null)
                {
                    using (mo)
                    {
                        var id = TryString(mo, "Name");
                        if (!string.IsNullOrWhiteSpace(id) && Guid.TryParse(id, out var guid))
                            return guid;
                    }
                }
            }
            catch { }
            Thread.Sleep(200);
        }
        return Guid.Empty;
    }

    /// <summary>
    /// Deletes a virtual switch. Based on Microsoft DeleteSwitch sample.
    /// Note: This can delete any type of switch. Any ports connected to external or internal resources are also deleted.
    /// </summary>
    public virtual void DeleteVirtualSwitch(Guid id)
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        // Find the switch by GUID
        var switchQuery = new ObjectQuery($"SELECT * FROM Msvm_VirtualEthernetSwitch WHERE Name='{id}'");
        using var switchSearcher = new ManagementObjectSearcher(_scope, switchQuery);
        using var switchResults = switchSearcher.Get();
        using var ethernetSwitch = switchResults.Cast<ManagementObject>().FirstOrDefault();
        
        if (ethernetSwitch == null)
        {
            throw new InvalidOperationException($"Virtual switch with ID {id} not found");
        }

        // Get the switch management service and delete the switch
        using var switchService = GetEthernetSwitchManagementService();
        using var inParams = switchService.GetMethodParameters("DestroySystem");
        
        inParams["AffectedSystem"] = ethernetSwitch.Path.Path;
        
        using var outParams = switchService.InvokeMethod("DestroySystem", inParams, null);
        WmiUtilities.ValidateOutput(outParams, _scope, true, true);
    }

    /// <summary>
    /// Updates basic switch settings (name, notes). Throws on failure.
    /// </summary>
    public virtual void UpdateVirtualSwitch(Guid id, string? newName = null, string? newNotes = null)
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        var (settingsMo, _, _) = GetSwitchSettings(id.ToString());
        if (settingsMo == null) 
            throw new InvalidOperationException($"Virtual switch with ID {id} not found");

        using (settingsMo)
        {
            if (!string.IsNullOrWhiteSpace(newName)) settingsMo["ElementName"] = newName;
            if (!string.IsNullOrWhiteSpace(newNotes)) settingsMo["Notes"] = new string[] { newNotes! };
            
            // Use correct switch management service like Microsoft ModifySwitch.cs
            using var switchService = GetEthernetSwitchManagementService();
            using var inParams = switchService.GetMethodParameters("ModifySystemSettings");
            inParams["SystemSettings"] = settingsMo.GetText(TextFormat.WmiDtd20);
            using var outParams = switchService.InvokeMethod("ModifySystemSettings", inParams, null);
            WmiUtilities.ValidateOutput(outParams, _scope, true, true);
        }
    }

    /// <summary>
    /// Lists installed Ethernet switch extensions on host.
    /// </summary>
    public virtual IEnumerable<SwitchExtensionInfo> ListInstalledExtensions()
    {
        var list = new List<SwitchExtensionInfo>();
        try
        {
            if (!_scope.IsConnected) _scope.Connect();
            var q = new ObjectQuery("SELECT Name, FriendlyName, ExtensionType, Vendor, Version FROM Msvm_EthernetSwitchExtension");
            using var searcher = new ManagementObjectSearcher(_scope, q);
            foreach (ManagementObject mo in searcher.Get())
            {
                using (mo)
                {
                    list.Add(new SwitchExtensionInfo
                    {
                        Name = TryString(mo, "Name") ?? string.Empty,
                        FriendlyName = TryString(mo, "FriendlyName") ?? string.Empty,
                        Vendor = TryString(mo, "Vendor") ?? string.Empty,
                        Version = TryString(mo, "Version") ?? string.Empty,
                        Type = MapExtensionType(mo["ExtensionType"])
                    });
                }
            }
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Enables or disables a specific extension on a virtual switch.
    /// Based on Microsoft Networking/ManageExtension.cs SetExtensionEnabledState using RequestStateChange.
    /// </summary>
    public virtual void SetExtensionEnabledState(string switchName, string extensionName, bool enabled)
    {
        try
        {
            Console.WriteLine($"Setting extension '{extensionName}' {(enabled ? "enabled" : "disabled")} on switch '{switchName}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var ethernetSwitch = FindEthernetSwitch(switchName);
            if (ethernetSwitch == null)
            {
                throw new InvalidOperationException($"Switch '{switchName}' not found");
            }
            
            using var extensions = ethernetSwitch.GetRelated(
                "Msvm_EthernetSwitchExtension",
                "Msvm_HostedEthernetSwitchExtension",
                null, null, null, null, false, null);
            
            ManagementObject? targetExtension = null;
            foreach (ManagementObject extension in extensions)
            {
                using (extension)
                {
                    if (string.Equals((string)extension["ElementName"], extensionName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetExtension = extension.Clone() as ManagementObject;
                        break;
                    }
                }
            }
            
            if (targetExtension == null)
            {
                throw new InvalidOperationException($"Extension '{extensionName}' not found on switch '{switchName}'");
            }
            
            using (targetExtension)
            {
                var currentState = (ushort)targetExtension["EnabledState"];
                if (currentState == 5) // NotApplicable
                {
                    throw new InvalidOperationException($"Enabled state cannot be changed for extension '{extensionName}'");
                }
                
                if ((currentState == 2 && enabled) || (currentState == 3 && !enabled))
                {
                    Console.WriteLine($"Extension '{extensionName}' is already {(enabled ? "enabled" : "disabled")}");
                    return;
                }
                
                var requestedState = enabled ? 2u : 3u; // Enabled/Disabled
                using var inParams = targetExtension.GetMethodParameters("RequestStateChange");
                inParams["RequestedState"] = requestedState;
                
                using var outParams = targetExtension.InvokeMethod("RequestStateChange", inParams, null);
                WmiUtilities.ValidateOutput(outParams, _scope, true, true);
                
                Console.WriteLine($"Extension '{extensionName}' successfully {(enabled ? "enabled" : "disabled")} on switch '{switchName}'");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set extension state: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks if a virtual switch supports trunk mode.
    /// Based on Microsoft Networking/SupportsTrunkMode.cs using Msvm_EthernetSwitchPortFeatures.
    /// </summary>
    public virtual bool SupportsTrunkMode(string switchName)
    {
        try
        {
            Console.WriteLine($"Checking trunk mode support for switch '{switchName}'");
            
            if (!_scope.IsConnected) _scope.Connect();
            
            using var ethernetSwitch = FindEthernetSwitch(switchName);
            if (ethernetSwitch == null)
            {
                throw new InvalidOperationException($"Switch '{switchName}' not found");
            }
            
            using var switchPorts = ethernetSwitch.GetRelated(
                "Msvm_EthernetSwitchPort",
                "Msvm_SystemDevice",
                null, null, null, null, false, null);
            
            if (switchPorts.Count == 0)
            {
                return false; // No ports, cannot support trunk mode
            }
            
            // Check features for the first port (assuming consistent across switch)
            using var firstPort = WmiUtilities.GetFirstObjectFromCollection(switchPorts);
            using var portFeatures = firstPort.GetRelated(
                "Msvm_EthernetSwitchPortFeatures",
                "Msvm_ElementCapabilities",
                null, null, null, null, false, null);
            
            foreach (ManagementObject feature in portFeatures)
            {
                using (feature)
                {
                    var featureType = (ushort)feature["Type"];
                    if (featureType == 10) // TrunkMode
                    {
                        var supported = (bool)feature["Supported"];
                        Console.WriteLine($"Switch '{switchName}' {(supported ? "supports" : "does not support")} trunk mode");
                        return supported;
                    }
                }
            }
            
            Console.WriteLine($"Trunk mode features not found for switch '{switchName}'");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking trunk mode support: {ex.Message}");
            return false;
        }
    }

    private static string MapExtensionType(object? value)
    {
        try
        {
            var u = Convert.ToUInt16(value);
            return u switch
            {
                1 => "Monitoring",
                2 => "Filtering",
                3 => "Forwarding",
                _ => "Unknown"
            };
        }
        catch { return "Unknown"; }
    }

    private (ManagementObject? settings, string switchType, string notes) GetSwitchSettings(string switchId)
    {
        try
        {
            if (!_scope.IsConnected) _scope.Connect();
            // Find the switch
            var swQuery = new ObjectQuery($"SELECT * FROM Msvm_VirtualEthernetSwitch WHERE Name='{switchId}' OR ElementName='{switchId}'");
            using var swSearcher = new ManagementObjectSearcher(_scope, swQuery);
            var sw = swSearcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (sw == null) return (null, string.Empty, string.Empty);

            // Get settings via association
            using var settingsCollection = sw.GetRelated("Msvm_VirtualEthernetSwitchSettingData", "Msvm_SettingsDefineState", null, null, null, null, false, null);
            var settings = settingsCollection.Cast<ManagementObject>().FirstOrDefault();
            if (settings == null) return (null, string.Empty, string.Empty);

            string type = string.Empty;
            try
            {
                var st = settings["SwitchType"];
                if (st is ushort u)
                {
                    type = u switch { 1 => "External", 2 => "Internal", 3 => "Private", _ => "Unknown" };
                }
            }
            catch { }

            string notes = string.Empty;
            try
            {
                if (settings["Notes"] is string[] arr && arr.Length > 0)
                    notes = string.Join("\n", arr);
            }
            catch { }

            return (settings, type, notes);
        }
        catch
        {
            return (null, string.Empty, string.Empty);
        }
    }

    private static string? TryString(ManagementObject mo, string prop)
    {
        try { return mo.Properties[prop]?.Value?.ToString(); }
        catch { return null; }
    }

    /// <summary>
    /// Finds a physical network adapter by name. Returns the ManagementObject or null if not found.
    /// </summary>
    public ManagementObject? FindExternalAdapter(string adapterName)
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        var query = new ObjectQuery($"SELECT * FROM Msvm_ExternalEthernetPort WHERE ElementName='{adapterName}' OR Name='{adapterName}'");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
    }


    /// <summary>
    /// Lists all physical network adapters from Msvm_ExternalEthernetPort. Returns an enumerable of ManagementObject.
    /// </summary>
    public IEnumerable<ManagementObject> ListPhysicalAdaptersRaw()
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        var query = new ObjectQuery($"SELECT * FROM Msvm_ExternalEthernetPort");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        return searcher.Get().Cast<ManagementObject>();
    }


    // DTOs
    public class SwitchInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class SwitchSummary : SwitchInfo
    {
        public string Type { get; set; } = string.Empty; // External/Internal/Private/Unknown
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class PhysicalAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Guid { get; set; } = string.Empty;
        public string PnpDeviceId { get; set; } = string.Empty;
    }

    public sealed class SwitchExtensionInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    /// <summary>
    /// Connects a VM to a virtual switch. Based on Microsoft ConnectVmToSwitch sample.
    /// </summary>
    public virtual void ConnectVmToSwitch(string vmName, string switchName)
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        using var managementService = WmiUtilities.GetVirtualMachineManagementService(_scope);
        using var ethernetSwitch = FindEthernetSwitch(switchName);
        using var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
        using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);

        // Add synthetic adapter to VM
        using var syntheticAdapter = AddSyntheticAdapter(vm);

        // Create connection settings
        using var connectionSettings = GetDefaultEthernetPortAllocationSettingData();
        connectionSettings["Parent"] = syntheticAdapter.Path.Path;
        connectionSettings["HostResource"] = new string[] { ethernetSwitch.Path.Path };

        // Add connection settings
        using var inParams = managementService.GetMethodParameters("AddResourceSettings");
        inParams["AffectedConfiguration"] = vmSettings.Path.Path;
        inParams["ResourceSettings"] = new string[] { connectionSettings.GetText(TextFormat.WmiDtd20) };

        using var outParams = managementService.InvokeMethod("AddResourceSettings", inParams, null);
        WmiUtilities.ValidateOutput(outParams, _scope, true, true);
    }

    /// <summary>
    /// Disconnects a VM from a virtual switch. Based on Microsoft ConnectVmToSwitch sample.
    /// </summary>
    public virtual void DisconnectVmFromSwitch(string vmName, string switchName)
    {
        if (!_scope.IsConnected)
            _scope.Connect();

        using var managementService = WmiUtilities.GetVirtualMachineManagementService(_scope);
        using var ethernetSwitch = FindEthernetSwitch(switchName);
        using var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);

        var connectionsToSwitch = FindConnectionsToSwitch(vm, ethernetSwitch);

        try
        {
            foreach (var connection in connectionsToSwitch)
            {
                connection["EnabledState"] = 3; // Disabled

                using var inParams = managementService.GetMethodParameters("ModifyResourceSettings");
                inParams["ResourceSettings"] = new string[] { connection.GetText(TextFormat.WmiDtd20) };

                using var outParams = managementService.InvokeMethod("ModifyResourceSettings", inParams, null);
                WmiUtilities.ValidateOutput(outParams, _scope, true, true);
            }
        }
        finally
        {
            foreach (var connection in connectionsToSwitch)
            {
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Adds a synthetic network adapter to a VM. Based on Microsoft NetworkingUtilities.
    /// </summary>
    private ManagementObject AddSyntheticAdapter(ManagementObject vm)
    {
        using var managementService = WmiUtilities.GetVirtualMachineManagementService(_scope);
        using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
        using var adapterToAdd = GetDefaultSyntheticAdapter();

        adapterToAdd["VirtualSystemIdentifiers"] = new string[] { Guid.NewGuid().ToString("B") };
        adapterToAdd["ElementName"] = "Network Adapter";
        adapterToAdd["StaticMacAddress"] = false;

        using var inParams = managementService.GetMethodParameters("AddResourceSettings");
        inParams["AffectedConfiguration"] = vmSettings.Path.Path;
        inParams["ResourceSettings"] = new string[] { adapterToAdd.GetText(TextFormat.WmiDtd20) };

        using var outParams = managementService.InvokeMethod("AddResourceSettings", inParams, null);
        WmiUtilities.ValidateOutput(outParams, _scope, true, true);

        if (outParams["ResultingResourceSettings"] != null)
        {
            var addedAdapter = new ManagementObject(((string[])outParams["ResultingResourceSettings"])[0]);
            addedAdapter.Get();
            return addedAdapter;
        }
        else
        {
            using var job = new ManagementObject((string)outParams["Job"]);
            return WmiUtilities.GetFirstObjectFromCollection(job.GetRelated(null, "Msvm_AffectedJobElement", null, null, null, null, false, null));
        }
    }

    /// <summary>
    /// Gets default synthetic adapter settings.
    /// </summary>
    private ManagementObject GetDefaultSyntheticAdapter()
    {
        var query = new ObjectQuery("select * from Msvm_ResourcePool where ResourceSubType = 'Microsoft:Hyper-V:Synthetic Ethernet Port' and Primordial = True");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        using var resourcePool = WmiUtilities.GetFirstObjectFromCollection(searcher.Get());
        return GetDefaultObjectFromResourcePool(resourcePool);
    }

    /// <summary>
    /// Gets default ethernet port allocation setting data.
    /// </summary>
    private ManagementObject GetDefaultEthernetPortAllocationSettingData()
    {
        var query = new ObjectQuery("select * from Msvm_ResourcePool where ResourceType = 33 and Primordial = True");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        using var resourcePool = WmiUtilities.GetFirstObjectFromCollection(searcher.Get());
        return GetDefaultObjectFromResourcePool(resourcePool);
    }

    /// <summary>
    /// Finds ethernet switch by name.
    /// </summary>
    private ManagementObject FindEthernetSwitch(string switchName)
    {
        var query = new ObjectQuery($"select * from Msvm_VirtualEthernetSwitch where ElementName = \"{switchName}\"");
        using var searcher = new ManagementObjectSearcher(_scope, query);
        using var results = searcher.Get();
        
        if (results.Count == 0)
        {
            throw new ManagementException($"There is no switch with name: {switchName}");
        }
        
        return WmiUtilities.GetFirstObjectFromCollection(results);
    }

    /// <summary>
    /// Finds VM connections to a specific switch.
    /// </summary>
    private List<ManagementObject> FindConnectionsToSwitch(ManagementObject vm, ManagementObject ethernetSwitch)
    {
        var connectionsToSwitch = new List<ManagementObject>();
        
        using var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
        using var connectionCollection = vmSettings.GetRelated("Msvm_EthernetPortAllocationSettingData", "Msvm_VirtualSystemSettingDataComponent", null, null, null, null, false, null);

        foreach (ManagementObject connection in connectionCollection)
        {
            var hostResource = (string[])connection["HostResource"];
            if (hostResource != null && hostResource.Length > 0 &&
                string.Equals(hostResource[0], ethernetSwitch.Path.Path, StringComparison.OrdinalIgnoreCase))
            {
                connectionsToSwitch.Add(connection);
                continue;
            }

            // Check dynamic connections
            using var connectedPortCollection = connection.GetRelated("Msvm_EthernetSwitchPort", "Msvm_ElementSettingData", null, null, null, null, false, null);
            if (connectedPortCollection.Count > 0)
            {
                using var connectedPort = WmiUtilities.GetFirstObjectFromCollection(connectedPortCollection);
                if (string.Equals((string)connectedPort["SystemName"], (string)ethernetSwitch["Name"], StringComparison.OrdinalIgnoreCase))
                {
                    connectionsToSwitch.Add(connection);
                }
                else
                {
                    connection.Dispose();
                }
            }
        }

        return connectionsToSwitch;
    }

    /// <summary>
    /// Gets default object from resource pool.
    /// </summary>
    private ManagementObject GetDefaultObjectFromResourcePool(ManagementObject resourcePool)
    {
        ManagementObject? defaultSettingAssociation = null;

        using var capabilitiesCollection = resourcePool.GetRelated("Msvm_AllocationCapabilities", "Msvm_ElementCapabilities", null, null, null, null, false, null);
        using var capabilities = WmiUtilities.GetFirstObjectFromCollection(capabilitiesCollection);

        foreach (ManagementObject settingAssociation in capabilities.GetRelationships("Msvm_SettingsDefineCapabilities"))
        {
            if ((ushort)settingAssociation["ValueRole"] == 0)
            {
                defaultSettingAssociation = settingAssociation;
                break;
            }
            else
            {
                settingAssociation.Dispose();
            }
        }

        if (defaultSettingAssociation == null)
        {
            throw new ManagementException("Unable to find the default settings!");
        }

        var defaultSettingPath = (string)defaultSettingAssociation["PartComponent"];
        defaultSettingAssociation.Dispose();

        var defaultSetting = new ManagementObject(defaultSettingPath);
        defaultSetting.Scope = _scope;
        defaultSetting.Get();

        return defaultSetting;
    }

    /// <summary>
    /// Sets VLAN configuration on a VM's network adapter port.
    /// Uses Msvm_EthernetSwitchPortVlanSettingData.
    /// </summary>
    public virtual void SetVlanConfiguration(string vmName, int vlanId, int operationMode, int? nativeVlanId = null, int[]? trunkVlanIds = null)
    {
        try
        {
            if (!_scope.IsConnected) _scope.Connect();

            // Find the VM
            var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
            if (vm == null)
                throw new InvalidOperationException($"VM '{vmName}' not found");

            using (vm)
            {
                // Get VM settings
                var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
                using (vmSettings)
                {
                    // Find ethernet port allocation settings (network adapters)
                    var ethernetPorts = vmSettings.GetRelated("Msvm_EthernetPortAllocationSettingData");
                    ManagementObject? targetPort = null;

                    foreach (ManagementObject port in ethernetPorts)
                    {
                        targetPort = port;
                        break; // Use first adapter
                    }

                    if (targetPort == null)
                        throw new InvalidOperationException($"No network adapter found on VM '{vmName}'");

                    using (targetPort)
                    {
                        // Get or create VLAN setting data for this port
                        var existingVlanSettings = targetPort.GetRelated("Msvm_EthernetSwitchPortVlanSettingData");
                        ManagementObject? vlanSetting = null;
                        bool isNew = false;

                        foreach (ManagementObject existing in existingVlanSettings)
                        {
                            vlanSetting = existing;
                            break;
                        }

                        if (vlanSetting == null)
                        {
                            // Create new VLAN setting from default
                            isNew = true;
                            var defaultVlan = GetDefaultFeatureSetting("Msvm_EthernetSwitchPortVlanSettingData");
                            vlanSetting = defaultVlan;
                        }

                        using (vlanSetting)
                        {
                            // Set VLAN properties
                            vlanSetting["AccessVlanId"] = vlanId;
                            vlanSetting["OperationMode"] = (uint)operationMode;

                            if (operationMode == 2) // Trunk mode
                            {
                                if (nativeVlanId.HasValue)
                                    vlanSetting["NativeVlanId"] = nativeVlanId.Value;
                                if (trunkVlanIds != null && trunkVlanIds.Length > 0)
                                    vlanSetting["TrunkVlanIdArray"] = trunkVlanIds;
                            }

                            var vlanSettingText = vlanSetting.GetText(TextFormat.WmiDtd20);

                            // Get the switch management service
                            using var switchService = GetEthernetSwitchManagementService();

                            if (isNew)
                            {
                                // Add new feature settings
                                using var inParams = switchService.GetMethodParameters("AddFeatureSettings");
                                inParams["AffectedConfiguration"] = targetPort.Path.Path;
                                inParams["FeatureSettings"] = new[] { vlanSettingText };

                                using var outParams = switchService.InvokeMethod("AddFeatureSettings", inParams, null);
                                WmiUtilities.ValidateOutput(outParams, _scope, true, true);
                            }
                            else
                            {
                                // Modify existing feature settings
                                using var inParams = switchService.GetMethodParameters("ModifyFeatureSettings");
                                inParams["FeatureSettings"] = new[] { vlanSettingText };

                                using var outParams = switchService.InvokeMethod("ModifyFeatureSettings", inParams, null);
                                WmiUtilities.ValidateOutput(outParams, _scope, true, true);
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"VLAN {vlanId} (mode {operationMode}) set on VM '{vmName}'");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to set VLAN configuration on VM '{vmName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current VLAN configuration for a VM's network adapter.
    /// </summary>
    public virtual object? GetVlanConfiguration(string vmName)
    {
        try
        {
            if (!_scope.IsConnected) _scope.Connect();

            var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
            if (vm == null)
                throw new InvalidOperationException($"VM '{vmName}' not found");

            using (vm)
            {
                var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
                using (vmSettings)
                {
                    var ethernetPorts = vmSettings.GetRelated("Msvm_EthernetPortAllocationSettingData");

                    foreach (ManagementObject port in ethernetPorts)
                    {
                        using (port)
                        {
                            var vlanSettings = port.GetRelated("Msvm_EthernetSwitchPortVlanSettingData");

                            foreach (ManagementObject vlanSetting in vlanSettings)
                            {
                                using (vlanSetting)
                                {
                                    var accessVlanId = Convert.ToInt32(vlanSetting["AccessVlanId"]);
                                    var operationMode = Convert.ToInt32(vlanSetting["OperationMode"]);
                                    var nativeVlanId = vlanSetting["NativeVlanId"] != null ? Convert.ToInt32(vlanSetting["NativeVlanId"]) : (int?)null;
                                    var trunkVlanIdArray = vlanSetting["TrunkVlanIdArray"] as int[];

                                    return new
                                    {
                                        VlanId = accessVlanId,
                                        OperationMode = operationMode,
                                        OperationModeName = operationMode switch
                                        {
                                            1 => "Access",
                                            2 => "Trunk",
                                            3 => "Private",
                                            _ => "Unknown"
                                        },
                                        NativeVlanId = nativeVlanId,
                                        TrunkVlanIds = trunkVlanIdArray
                                    };
                                }
                            }
                        }
                    }
                }
            }

            return new { VlanId = 0, OperationMode = 0, OperationModeName = "None", NativeVlanId = (int?)null, TrunkVlanIds = (int[]?)null };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get VLAN configuration for VM '{vmName}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Removes VLAN configuration from a VM's network adapter.
    /// </summary>
    public virtual void RemoveVlanConfiguration(string vmName)
    {
        try
        {
            if (!_scope.IsConnected) _scope.Connect();

            var vm = WmiUtilities.GetVirtualMachine(vmName, _scope);
            if (vm == null)
                throw new InvalidOperationException($"VM '{vmName}' not found");

            using (vm)
            {
                var vmSettings = WmiUtilities.GetVirtualMachineSettings(vm);
                using (vmSettings)
                {
                    var ethernetPorts = vmSettings.GetRelated("Msvm_EthernetPortAllocationSettingData");

                    foreach (ManagementObject port in ethernetPorts)
                    {
                        using (port)
                        {
                            var vlanSettings = port.GetRelated("Msvm_EthernetSwitchPortVlanSettingData");

                            foreach (ManagementObject vlanSetting in vlanSettings)
                            {
                                using (vlanSetting)
                                {
                                    using var switchService = GetEthernetSwitchManagementService();
                                    using var inParams = switchService.GetMethodParameters("RemoveFeatureSettings");
                                    inParams["FeatureSettings"] = new[] { vlanSetting.Path.Path };

                                    using var outParams = switchService.InvokeMethod("RemoveFeatureSettings", inParams, null);
                                    WmiUtilities.ValidateOutput(outParams, _scope, true, true);
                                }
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"VLAN configuration removed from VM '{vmName}'");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to remove VLAN configuration from VM '{vmName}': {ex.Message}", ex);
        }
    }

    private ManagementObject GetDefaultFeatureSetting(string className)
    {
        using var featureCapClass = new ManagementClass(_scope, new ManagementPath("Msvm_EthernetSwitchFeatureCapabilities"), null);

        foreach (ManagementObject capabilities in featureCapClass.GetInstances())
        {
            var featureId = capabilities["FeatureId"]?.ToString() ?? "";

            // Match by class name in the caption/description
            var caption = capabilities["Caption"]?.ToString() ?? "";
            if (caption.Contains("Vlan", StringComparison.OrdinalIgnoreCase) ||
                featureId.Contains("vlan", StringComparison.OrdinalIgnoreCase))
            {
                foreach (ManagementObject settingAssociation in capabilities.GetRelationships("Msvm_SettingsDefineCapabilities"))
                {
                    if ((ushort)settingAssociation["ValueRole"] == 0) // Default
                    {
                        var settingPath = (string)settingAssociation["PartComponent"];
                        settingAssociation.Dispose();

                        var setting = new ManagementObject(settingPath);
                        setting.Scope = _scope;
                        setting.Get();
                        capabilities.Dispose();
                        return setting;
                    }
                    settingAssociation.Dispose();
                }
            }
            capabilities.Dispose();
        }

        // Fallback: create instance directly
        using var vlanClass = new ManagementClass(_scope, new ManagementPath(className), null);
        return vlanClass.CreateInstance();
    }

    public enum WmiSwitchType : ushort
    {
        Unknown = 0,
        External = 1,
        Internal = 2,
        Private = 3
    }
}
