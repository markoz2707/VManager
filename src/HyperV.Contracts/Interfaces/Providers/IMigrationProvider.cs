using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IMigrationProvider
{
    bool SupportsLiveMigration { get; }
    Task<MigrationResultDto> MigrateVmAsync(string vmNameOrId, string destinationHost, bool live, bool includeStorage);
    Task<MigrationStatusDto?> GetMigrationStatusAsync(string jobId);
    Task CancelMigrationAsync(string jobId);
}
