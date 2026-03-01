using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVBackupProvider : IBackupProvider
{
    private readonly ILogger<HyperVBackupProvider> _logger;
    private static readonly string MetadataFileName = "backup-metadata.json";

    public HyperVBackupProvider(ILogger<HyperVBackupProvider> logger)
    {
        _logger = logger;
    }

    public async Task<BackupResultDto> BackupVmAsync(string vmNameOrId, string destinationPath, BackupOptions? options = null)
    {
        _logger.LogInformation("Starting backup of VM '{VmName}' to '{Destination}'", vmNameOrId, destinationPath);

        var backupId = Guid.NewGuid().ToString("N")[..12];
        var backupDir = Path.Combine(destinationPath, $"{vmNameOrId}_{backupId}");

        try
        {
            Directory.CreateDirectory(backupDir);

            // Use WMI ExportSystemDefinition for consistent VM backup
            var exportArgs = $"Export-VM -Name \"{vmNameOrId}\" -Path \"{backupDir}\"";
            if (options?.IncludeSnapshots != true)
                exportArgs += " -CaptureLiveState CaptureSavedState";

            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{exportArgs}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                    throw new Exception($"Export-VM failed: {stderr}");
            }

            // Calculate backup size
            var size = GetDirectorySize(backupDir);

            // Write metadata sidecar
            var metadata = new BackupInfoDto
            {
                Id = backupId,
                VmName = vmNameOrId,
                CreatedUtc = DateTime.UtcNow,
                SizeBytes = size,
                BackupPath = backupDir,
                HypervisorType = "Hyper-V",
                IncludesSnapshots = options?.IncludeSnapshots ?? false
            };

            var metadataPath = Path.Combine(backupDir, MetadataFileName);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(metadataPath, json);

            _logger.LogInformation("Backup completed: VM '{VmName}', ID={BackupId}, Size={Size}bytes", vmNameOrId, backupId, size);

            return new BackupResultDto
            {
                BackupId = backupId,
                Success = true,
                Message = "Backup completed successfully",
                SizeBytes = size
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed for VM '{VmName}'", vmNameOrId);
            return new BackupResultDto
            {
                BackupId = backupId,
                Success = false,
                Message = $"Backup failed: {ex.Message}"
            };
        }
    }

    public async Task<RestoreResultDto> RestoreVmAsync(string backupPath, string? newVmName = null)
    {
        _logger.LogInformation("Starting restore from '{BackupPath}', newName={NewName}", backupPath, newVmName);

        try
        {
            var importArgs = $"Import-VM -Path \"{backupPath}\" -Copy -GenerateNewId";
            if (!string.IsNullOrEmpty(newVmName))
                importArgs = $"Import-VM -Path \"{backupPath}\" -Copy -GenerateNewId; Rename-VM -VM (Get-VM | Sort-Object CreationTime -Descending | Select-Object -First 1) -NewName \"{newVmName}\"";

            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -Command \"{importArgs}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                var stderr = await process.StandardError.ReadToEndAsync();

                if (process.ExitCode != 0)
                    throw new Exception($"Import-VM failed: {stderr}");
            }

            return new RestoreResultDto
            {
                Success = true,
                RestoredVmName = newVmName,
                Message = "VM restored successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed from '{BackupPath}'", backupPath);
            return new RestoreResultDto
            {
                Success = false,
                Message = $"Restore failed: {ex.Message}"
            };
        }
    }

    public Task<List<BackupInfoDto>> ListBackupsAsync(string? vmNameOrId = null)
    {
        var backups = new List<BackupInfoDto>();

        // Scan known backup locations for metadata files
        var searchPaths = new[] { @"C:\Backups\VMs", @"D:\Backups\VMs", @"E:\Backups\VMs" };

        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            foreach (var dir in Directory.GetDirectories(basePath))
            {
                var metadataPath = Path.Combine(dir, MetadataFileName);
                if (!File.Exists(metadataPath)) continue;

                try
                {
                    var json = File.ReadAllText(metadataPath);
                    var info = JsonSerializer.Deserialize<BackupInfoDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (info != null)
                    {
                        if (vmNameOrId == null || info.VmName.Equals(vmNameOrId, StringComparison.OrdinalIgnoreCase))
                            backups.Add(info);
                    }
                }
                catch { }
            }
        }

        return Task.FromResult(backups.OrderByDescending(b => b.CreatedUtc).ToList());
    }

    public Task DeleteBackupAsync(string backupId)
    {
        var backups = ListBackupsAsync().Result;
        var backup = backups.FirstOrDefault(b => b.Id == backupId);
        if (backup == null)
            throw new InvalidOperationException($"Backup '{backupId}' not found");

        if (Directory.Exists(backup.BackupPath))
            Directory.Delete(backup.BackupPath, true);

        return Task.CompletedTask;
    }

    public Task<BackupInfoDto?> GetBackupInfoAsync(string backupId)
    {
        var backups = ListBackupsAsync().Result;
        return Task.FromResult(backups.FirstOrDefault(b => b.Id == backupId));
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .GetFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}
