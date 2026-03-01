using System.Text.Json;
using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models.Common;
using global::HyperV.Core.Wmi.Services;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVVmProvider : IVmProvider
{
    private readonly global::HyperV.Core.Wmi.Services.VmService _wmiVmService;
    private readonly VmCreationService _vmCreationService;
    private readonly ILogger<HyperVVmProvider> _logger;

    public HyperVVmProvider(
        global::HyperV.Core.Wmi.Services.VmService wmiVmService,
        VmCreationService vmCreationService,
        ILogger<HyperVVmProvider> logger)
    {
        _wmiVmService = wmiVmService;
        _vmCreationService = vmCreationService;
        _logger = logger;
    }

    public Task<List<VmSummaryDto>> ListVmsAsync()
    {
        var json = _wmiVmService.ListVms();
        var doc = JsonDocument.Parse(json);
        var result = new List<VmSummaryDto>();

        if (doc.RootElement.TryGetProperty("VMs", out var vmsArray))
        {
            foreach (var vm in vmsArray.EnumerateArray())
            {
                result.Add(new VmSummaryDto
                {
                    Id = vm.GetProperty("Id").GetString() ?? "",
                    Name = vm.GetProperty("Name").GetString() ?? "",
                    State = vm.GetProperty("State").GetString() ?? "Unknown",
                });
            }
        }

        return Task.FromResult(result);
    }

    public async Task<VmDetailsDto?> GetVmAsync(string vmId)
    {
        var vms = await ListVmsAsync();
        var vm = vms.FirstOrDefault(v => v.Id == vmId || v.Name == vmId);
        if (vm == null) return null;

        // Enrich with properties
        try
        {
            var propsJson = _wmiVmService.GetVmProperties(vm.Id);
            var props = JsonDocument.Parse(propsJson).RootElement;

            return new VmDetailsDto
            {
                Id = vm.Id,
                Name = vm.Name,
                State = vm.State,
                CpuCount = props.TryGetProperty("CpuCount", out var cpu) ? cpu.GetInt32() : 0,
                MemoryMB = props.TryGetProperty("MemoryMB", out var mem) ? mem.GetInt64() : 0,
            };
        }
        catch
        {
            return new VmDetailsDto { Id = vm.Id, Name = vm.Name, State = vm.State };
        }
    }

    public Task<VmPropertiesDto?> GetVmPropertiesAsync(string vmNameOrId)
    {
        try
        {
            var json = _wmiVmService.GetVmProperties(vmNameOrId);
            var props = JsonDocument.Parse(json).RootElement;

            return Task.FromResult<VmPropertiesDto?>(new VmPropertiesDto
            {
                CpuCount = props.TryGetProperty("CpuCount", out var cpu) ? cpu.GetInt32() : 0,
                MemoryMB = props.TryGetProperty("MemoryMB", out var mem) ? mem.GetInt64() : 0,
            });
        }
        catch
        {
            return Task.FromResult<VmPropertiesDto?>(null);
        }
    }

    public Task<string> CreateVmAsync(CreateVmSpec spec)
    {
        var request = new global::HyperV.Contracts.Models.CreateVmRequest
        {
            Name = spec.Name,
            MemoryMB = (int)spec.MemoryMB,
            CpuCount = spec.CpuCount,
            Generation = spec.Generation,
            DiskSizeGB = (int)spec.DiskSizeGB,
            SwitchName = spec.NetworkName ?? ""
        };
        _vmCreationService.CreateHyperVVm(spec.Name, request);
        return Task.FromResult(spec.Name);
    }

    public Task DeleteVmAsync(string vmId)
    {
        _wmiVmService.StopVm(vmId);
        // WMI doesn't have a direct "delete VM" - would need to undefine + remove files
        _logger.LogWarning("DeleteVm via WMI not fully implemented - stopped VM only");
        return Task.CompletedTask;
    }

    public Task StartVmAsync(string vmNameOrId)
    {
        _wmiVmService.StartVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task StopVmAsync(string vmNameOrId)
    {
        _wmiVmService.TerminateVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task ShutdownVmAsync(string vmNameOrId)
    {
        _wmiVmService.StopVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task PauseVmAsync(string vmNameOrId)
    {
        _wmiVmService.PauseVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task ResumeVmAsync(string vmNameOrId)
    {
        _wmiVmService.ResumeVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task SaveVmAsync(string vmNameOrId)
    {
        // Hyper-V save state = stop with state preservation
        _wmiVmService.StopVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task RestartVmAsync(string vmNameOrId)
    {
        _wmiVmService.StopVm(vmNameOrId);
        _wmiVmService.StartVm(vmNameOrId);
        return Task.CompletedTask;
    }

    public Task SetCpuCountAsync(string vmNameOrId, int cpuCount)
    {
        _wmiVmService.ModifyVmConfiguration(vmNameOrId, null, cpuCount, "", null, null, null, null);
        return Task.CompletedTask;
    }

    public Task SetMemoryAsync(string vmNameOrId, long memoryMB)
    {
        _wmiVmService.ModifyVmConfiguration(vmNameOrId, (int)memoryMB, null, "", null, null, null, null);
        return Task.CompletedTask;
    }

    public Task ConfigureVmAsync(string vmNameOrId, VmConfigurationSpec config)
    {
        _wmiVmService.ModifyVmConfiguration(
            vmNameOrId,
            config.MemoryMB.HasValue ? (int)config.MemoryMB.Value : null,
            config.CpuCount,
            config.Notes ?? "",
            config.EnableDynamicMemory,
            config.MinMemoryMB.HasValue ? (int)config.MinMemoryMB.Value : null,
            config.NumaNodesCount,
            config.NumaMemoryPerNode);
        return Task.CompletedTask;
    }

    public async Task<BulkOperationResultDto> BulkStartAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StartVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkStopAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StopVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkShutdownAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, ShutdownVmAsync);
    }

    public async Task<BulkOperationResultDto> BulkTerminateAsync(string[] vmNames)
    {
        return await ExecuteBulkOperationAsync(vmNames, StopVmAsync);
    }

    public async Task<string> CloneVmAsync(string sourceVmName, string newName)
    {
        // Get source VM properties and create a new VM with the same config
        var sourceProps = await GetVmPropertiesAsync(sourceVmName);
        if (sourceProps == null) throw new InvalidOperationException($"Source VM '{sourceVmName}' not found");

        var spec = new CreateVmSpec
        {
            Name = newName,
            CpuCount = sourceProps.CpuCount,
            MemoryMB = sourceProps.MemoryMB,
        };
        return await CreateVmAsync(spec);
    }

    private static async Task<BulkOperationResultDto> ExecuteBulkOperationAsync(string[] vmNames, Func<string, Task> operation)
    {
        var results = new List<BulkOperationItemResult>();
        var tasks = vmNames.Select(async name =>
        {
            try
            {
                await operation(name);
                return new BulkOperationItemResult { VmName = name, Success = true };
            }
            catch (Exception ex)
            {
                return new BulkOperationItemResult { VmName = name, Success = false, ErrorMessage = ex.Message };
            }
        });

        results.AddRange(await Task.WhenAll(tasks));

        return new BulkOperationResultDto
        {
            TotalCount = results.Count,
            SuccessCount = results.Count(r => r.Success),
            FailureCount = results.Count(r => !r.Success),
            Results = results
        };
    }

    public Task<List<VmSnapshotDto>> ListSnapshotsAsync(string vmNameOrId)
    {
        try
        {
            var json = _wmiVmService.ListVmSnapshots(vmNameOrId);
            var doc = JsonDocument.Parse(json);
            var result = new List<VmSnapshotDto>();

            if (doc.RootElement.TryGetProperty("Snapshots", out var snapArray))
            {
                foreach (var snap in snapArray.EnumerateArray())
                {
                    result.Add(new VmSnapshotDto
                    {
                        Id = snap.TryGetProperty("Id", out var id) ? id.GetString() ?? "" : "",
                        Name = snap.TryGetProperty("Name", out var name) ? name.GetString() ?? "" : "",
                        CreatedTime = snap.TryGetProperty("CreationTime", out var time)
                            ? DateTime.TryParse(time.GetString(), out var dt) ? dt : DateTime.MinValue
                            : DateTime.MinValue,
                    });
                }
            }

            return Task.FromResult(result);
        }
        catch
        {
            return Task.FromResult(new List<VmSnapshotDto>());
        }
    }

    public Task<string> CreateSnapshotAsync(string vmNameOrId, string snapshotName)
    {
        _wmiVmService.CreateVmSnapshot(vmNameOrId, snapshotName, "");
        return Task.FromResult(snapshotName);
    }

    public Task DeleteSnapshotAsync(string vmNameOrId, string snapshotId)
    {
        _wmiVmService.DeleteVmSnapshot(vmNameOrId, snapshotId);
        return Task.CompletedTask;
    }

    public Task ApplySnapshotAsync(string vmNameOrId, string snapshotId)
    {
        _wmiVmService.RevertVmToSnapshot(vmNameOrId, snapshotId);
        return Task.CompletedTask;
    }

    public Task<ConsoleInfoDto?> GetConsoleInfoAsync(string vmNameOrId)
    {
        return Task.FromResult<ConsoleInfoDto?>(new ConsoleInfoDto
        {
            Type = "rdp",
            Host = Environment.MachineName,
            ExtendedProperties = new Dictionary<string, object>
            {
                ["vmName"] = vmNameOrId,
                ["method"] = "VMConnect"
            }
        });
    }
}
