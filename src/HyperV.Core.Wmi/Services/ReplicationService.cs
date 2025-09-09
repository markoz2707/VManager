using System.Linq;
using System.Management;

namespace HyperV.Core.Wmi.Services;

/// <summary>Serwis replikacji maszyn wirtualnych (WMI).</summary>
public sealed class ReplicationService
{
    private readonly ManagementScope _scope;

    public ReplicationService()
    {
        _scope = new ManagementScope(@"\\.\root\virtualization\v2");
        _scope.Connect();
    }

    /// <summary>Sprawdza, czy istnieje VM o danej nazwie.</summary>
    public bool IsVmPresent(string vmName)
    {
        var q = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{vmName}'");
        using var searcher = new ManagementObjectSearcher(_scope, q);
        return searcher.Get().Cast<ManagementObject>().Any();
    }

    public void StartVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 2; // Enabled (Running)
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096) // 0 = Completed, 4096 = Job Started
        {
            throw new InvalidOperationException($"Failed to start VM {vmName}. Return code: {returnValue}");
        }
    }

    public void StopVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 3; // Disabled (Stopped)
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to stop VM {vmName}. Return code: {returnValue}");
        }
    }

    public void ShutdownVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 4; // Shutdown
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to shutdown VM {vmName}. Return code: {returnValue}");
        }
    }

    public void TerminateVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 32768; // Hard Reset/Terminate
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to terminate VM {vmName}. Return code: {returnValue}");
        }
    }

    public void PauseVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 9; // Paused
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to pause VM {vmName}. Return code: {returnValue}");
        }
    }

    public void ResumeVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 2; // Enabled (Running) - Resume from pause
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to resume VM {vmName}. Return code: {returnValue}");
        }
    }

    public void SaveVm(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var methodParams = vm.GetMethodParameters("RequestStateChange");
        methodParams["RequestedState"] = 6; // Offline (Saved)
        var result = vm.InvokeMethod("RequestStateChange", methodParams, null);
        var returnValue = (uint)result["ReturnValue"];
        if (returnValue != 0 && returnValue != 4096)
        {
            throw new InvalidOperationException($"Failed to save VM {vmName}. Return code: {returnValue}");
        }
    }

    public string GetVmProperties(string vmName)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        var properties = new
        {
            Name = vm["ElementName"]?.ToString(),
            State = GetVmState((ushort)(vm["EnabledState"] ?? 0)),
            ProcessorCount = vm["NumberOfNumaNodes"]?.ToString(),
            MemoryMB = vm["TotalPhysicalMemory"]?.ToString(),
            CreationTime = vm["TimeOfLastConfigurationChange"]?.ToString(),
            Notes = vm["Notes"]?.ToString()
        };

        return System.Text.Json.JsonSerializer.Serialize(properties);
    }

    public void ModifyVm(string vmName, string configuration)
    {
        var vm = GetVm(vmName);
        if (vm == null) throw new InvalidOperationException($"VM {vmName} not found");

        // For WMI, modification is complex and would require specific property changes
        // This is a placeholder implementation
        throw new NotImplementedException("VM modification through WMI requires specific property changes");
    }

    private string GetVmState(ushort state)
    {
        return state switch
        {
            0 => "Unknown",
            2 => "Running",
            3 => "Off",
            4 => "Stopping",
            6 => "Saved",
            9 => "Paused",
            10 => "Starting",
            32768 => "Paused-Critical",
            32769 => "Stopping-Critical",
            32770 => "Stopped-Critical",
            32771 => "Paused-Shutting-Down",
            32772 => "Stopped-Shutting-Down",
            32773 => "Not-Applicable",
            32774 => "Enabled-but-Offline",
            32775 => "In-Test",
            32776 => "Deferred",
            32777 => "Quiesce",
            32778 => "Starting",
            _ => $"Unknown-{state}"
        };
    }

    private ManagementObject? GetVm(string vmName)
    {
        var q = new ObjectQuery($"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{vmName}'");
        using var searcher = new ManagementObjectSearcher(_scope, q);
        return searcher.Get().Cast<ManagementObject>().FirstOrDefault();
    }
}
