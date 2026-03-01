using global::HyperV.Contracts.Interfaces.Providers;
using global::HyperV.Contracts.Models.Common;
using global::HyperV.Core.Wmi.Services;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVMigrationProvider : IMigrationProvider
{
    private readonly global::HyperV.Core.Wmi.Services.VmService _wmiVmService;
    private readonly ILogger<HyperVMigrationProvider> _logger;

    public bool SupportsLiveMigration => true;

    public HyperVMigrationProvider(
        global::HyperV.Core.Wmi.Services.VmService wmiVmService,
        ILogger<HyperVMigrationProvider> logger)
    {
        _wmiVmService = wmiVmService;
        _logger = logger;
    }

    public Task<MigrationResultDto> MigrateVmAsync(string vmNameOrId, string destinationHost, bool live, bool includeStorage)
    {
        try
        {
            _wmiVmService.MigrateVm(vmNameOrId, destinationHost, live, includeStorage);
            return Task.FromResult(new MigrationResultDto
            {
                Success = true,
                Message = $"Migration of {vmNameOrId} to {destinationHost} initiated."
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MigrationResultDto
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    public Task<MigrationStatusDto?> GetMigrationStatusAsync(string jobId)
    {
        // Hyper-V WMI migration is synchronous/blocking
        return Task.FromResult<MigrationStatusDto?>(null);
    }

    public Task CancelMigrationAsync(string jobId)
    {
        // Not easily supported for WMI-based migration
        return Task.CompletedTask;
    }
}
