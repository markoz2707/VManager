using System.Text.Json;
using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models;
using global::HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVContainerProvider : IContainerProvider
{
    private readonly global::HyperV.Core.Hcs.Services.ContainerService _hcsService;
    private readonly global::HyperV.Core.Wmi.Services.ContainerService _wmiService;
    private readonly ILogger<HyperVContainerProvider> _logger;

    public HyperVContainerProvider(
        global::HyperV.Core.Hcs.Services.ContainerService hcsService,
        global::HyperV.Core.Wmi.Services.ContainerService wmiService,
        ILogger<HyperVContainerProvider> logger)
    {
        _hcsService = hcsService;
        _wmiService = wmiService;
        _logger = logger;
    }

    public Task<List<ContainerSummaryDto>> ListContainersAsync()
    {
        var result = new List<ContainerSummaryDto>();

        try
        {
            var hcsContainers = _hcsService.ListContainers();
            foreach (var c in hcsContainers)
            {
                result.Add(new ContainerSummaryDto
                {
                    Id = c.TryGetValue("Id", out var id) ? id?.ToString() ?? "" : "",
                    Name = c.TryGetValue("Name", out var name) ? name?.ToString() ?? "" : "",
                    State = c.TryGetValue("Status", out var status) ? status?.ToString() ?? "Unknown" : "Unknown",
                    Backend = "HCS"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list HCS containers");
        }

        try
        {
            var wmiContainers = _wmiService.ListContainers();
            foreach (var c in wmiContainers)
            {
                result.Add(new ContainerSummaryDto
                {
                    Id = c.TryGetValue("Id", out var id) ? id?.ToString() ?? "" : "",
                    Name = c.TryGetValue("Name", out var name) ? name?.ToString() ?? "" : "",
                    State = c.TryGetValue("Status", out var status) ? status?.ToString() ?? "Unknown" : "Unknown",
                    Backend = "WMI"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list WMI containers");
        }

        return Task.FromResult(result);
    }

    public Task<ContainerDetailsDto?> GetContainerAsync(string containerId)
    {
        // Try HCS first
        try
        {
            if (_hcsService.IsContainerPresent(containerId))
            {
                var propsJson = _hcsService.GetContainerProperties(containerId);
                return Task.FromResult<ContainerDetailsDto?>(new ContainerDetailsDto
                {
                    Id = containerId,
                    Name = containerId,
                    State = "Running",
                    Backend = "HCS",
                    ExtendedProperties = new Dictionary<string, object> { ["properties"] = propsJson }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Container {Id} not found in HCS", containerId);
        }

        // Try WMI
        try
        {
            if (_wmiService.IsContainerPresent(containerId))
            {
                var propsJson = _wmiService.GetContainerProperties(containerId);
                return Task.FromResult<ContainerDetailsDto?>(new ContainerDetailsDto
                {
                    Id = containerId,
                    Name = containerId,
                    State = "Running",
                    Backend = "WMI",
                    ExtendedProperties = new Dictionary<string, object> { ["properties"] = propsJson }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Container {Id} not found in WMI", containerId);
        }

        return Task.FromResult<ContainerDetailsDto?>(null);
    }

    public Task<string> CreateContainerAsync(CreateContainerSpec spec)
    {
        var request = new CreateContainerRequest
        {
            Name = spec.Name,
            Image = spec.Image,
            MemoryMB = (int)spec.MemoryMB,
            CpuCount = spec.CpuCount,
            StorageSizeGB = (int)spec.StorageSizeGB
        };

        // Default to HCS
        var result = _hcsService.Create(spec.Name, request);
        return Task.FromResult(result);
    }

    public Task DeleteContainerAsync(string containerId)
    {
        var deleted = false;
        try
        {
            if (_hcsService.IsContainerPresent(containerId))
            {
                _hcsService.TerminateContainer(containerId);
                deleted = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete container {Id} from HCS", containerId);
        }

        try
        {
            if (_wmiService.IsContainerPresent(containerId))
            {
                _wmiService.StopContainer(containerId);
                deleted = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to delete container {Id} from WMI", containerId);
        }

        if (!deleted) throw new InvalidOperationException($"Container {containerId} not found");
        return Task.CompletedTask;
    }

    public Task StartContainerAsync(string containerId)
    {
        ExecuteOnContainer(containerId, () => _hcsService.StartContainer(containerId), () => _wmiService.StartContainer(containerId));
        return Task.CompletedTask;
    }

    public Task StopContainerAsync(string containerId)
    {
        ExecuteOnContainer(containerId, () => _hcsService.StopContainer(containerId), () => _wmiService.StopContainer(containerId));
        return Task.CompletedTask;
    }

    public Task PauseContainerAsync(string containerId)
    {
        ExecuteOnContainer(containerId, () => _hcsService.PauseContainer(containerId), () => _wmiService.PauseContainer(containerId));
        return Task.CompletedTask;
    }

    public Task ResumeContainerAsync(string containerId)
    {
        ExecuteOnContainer(containerId, () => _hcsService.ResumeContainer(containerId), () => _wmiService.ResumeContainer(containerId));
        return Task.CompletedTask;
    }

    public Task TerminateContainerAsync(string containerId)
    {
        ExecuteOnContainer(containerId, () => _hcsService.TerminateContainer(containerId), () => _wmiService.TerminateContainer(containerId));
        return Task.CompletedTask;
    }

    private void ExecuteOnContainer(string containerId, Action hcsAction, Action wmiAction)
    {
        try
        {
            if (_hcsService.IsContainerPresent(containerId))
            {
                hcsAction();
                return;
            }
        }
        catch { }

        try
        {
            if (_wmiService.IsContainerPresent(containerId))
            {
                wmiAction();
                return;
            }
        }
        catch { }

        throw new InvalidOperationException($"Container {containerId} not found");
    }
}
