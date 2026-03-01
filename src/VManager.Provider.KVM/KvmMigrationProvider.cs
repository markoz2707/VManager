using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;

namespace VManager.Provider.KVM;

public class KvmMigrationProvider : IMigrationProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmMigrationProvider> _logger;

    public bool SupportsLiveMigration => true;

    public KvmMigrationProvider(IOptions<KvmOptions> options, ILogger<KvmMigrationProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<MigrationResultDto> MigrateVmAsync(string vmNameOrId, string destinationHost, bool live, bool includeStorage)
    {
        try
        {
            using var conn = new LibvirtConnection(_options.LibvirtUri);
            var domain = conn.LookupDomainByName(vmNameOrId);
            if (domain == IntPtr.Zero)
                return Task.FromResult(new MigrationResultDto { Success = false, Message = "VM not found" });

            var destUri = $"qemu+ssh://{destinationHost}/system";
            uint flags = live ? 1u : 0u; // VIR_MIGRATE_LIVE = 1

            var result = LibvirtNative.virDomainMigrateToURI3(domain, destUri, IntPtr.Zero, 0, flags);
            LibvirtNative.virDomainFree(domain);

            return Task.FromResult(new MigrationResultDto
            {
                Success = result == 0,
                Message = result == 0 ? "Migration completed" : $"Migration failed (error code: {result})"
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
        return Task.FromResult<MigrationStatusDto?>(null);
    }

    public Task CancelMigrationAsync(string jobId)
    {
        return Task.CompletedTask;
    }
}
