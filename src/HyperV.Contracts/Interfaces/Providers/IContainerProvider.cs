using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IContainerProvider
{
    Task<List<ContainerSummaryDto>> ListContainersAsync();
    Task<ContainerDetailsDto?> GetContainerAsync(string containerId);
    Task<string> CreateContainerAsync(CreateContainerSpec spec);
    Task DeleteContainerAsync(string containerId);
    Task StartContainerAsync(string containerId);
    Task StopContainerAsync(string containerId);
    Task PauseContainerAsync(string containerId);
    Task ResumeContainerAsync(string containerId);
    Task TerminateContainerAsync(string containerId);
}
