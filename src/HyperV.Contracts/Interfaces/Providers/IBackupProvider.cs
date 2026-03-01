using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IBackupProvider
{
    Task<BackupResultDto> BackupVmAsync(string vmNameOrId, string destinationPath, BackupOptions? options = null);
    Task<RestoreResultDto> RestoreVmAsync(string backupPath, string? newVmName = null);
    Task<List<BackupInfoDto>> ListBackupsAsync(string? vmNameOrId = null);
    Task DeleteBackupAsync(string backupId);
    Task<BackupInfoDto?> GetBackupInfoAsync(string backupId);
}
