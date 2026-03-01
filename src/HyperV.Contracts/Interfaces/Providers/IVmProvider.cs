using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IVmProvider
{
    Task<List<VmSummaryDto>> ListVmsAsync();
    Task<VmDetailsDto?> GetVmAsync(string vmId);
    Task<VmPropertiesDto?> GetVmPropertiesAsync(string vmNameOrId);
    Task<string> CreateVmAsync(CreateVmSpec spec);
    Task DeleteVmAsync(string vmId);

    // Power operations
    Task StartVmAsync(string vmNameOrId);
    Task StopVmAsync(string vmNameOrId);
    Task ShutdownVmAsync(string vmNameOrId);
    Task PauseVmAsync(string vmNameOrId);
    Task ResumeVmAsync(string vmNameOrId);
    Task SaveVmAsync(string vmNameOrId);
    Task RestartVmAsync(string vmNameOrId);

    // Resource configuration
    Task SetCpuCountAsync(string vmNameOrId, int cpuCount);
    Task SetMemoryAsync(string vmNameOrId, long memoryMB);
    Task ConfigureVmAsync(string vmNameOrId, VmConfigurationSpec config);

    // Bulk operations
    Task<BulkOperationResultDto> BulkStartAsync(string[] vmNames);
    Task<BulkOperationResultDto> BulkStopAsync(string[] vmNames);
    Task<BulkOperationResultDto> BulkShutdownAsync(string[] vmNames);
    Task<BulkOperationResultDto> BulkTerminateAsync(string[] vmNames);

    // Clone
    Task<string> CloneVmAsync(string sourceVmName, string newName);

    // Snapshots
    Task<List<VmSnapshotDto>> ListSnapshotsAsync(string vmNameOrId);
    Task<string> CreateSnapshotAsync(string vmNameOrId, string snapshotName);
    Task DeleteSnapshotAsync(string vmNameOrId, string snapshotId);
    Task ApplySnapshotAsync(string vmNameOrId, string snapshotId);

    // Console
    Task<ConsoleInfoDto?> GetConsoleInfoAsync(string vmNameOrId);
}
