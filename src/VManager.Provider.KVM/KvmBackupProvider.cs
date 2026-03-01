using System.Diagnostics;
using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VManager.Provider.KVM;

public class KvmBackupProvider : IBackupProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmBackupProvider> _logger;
    private static readonly string MetadataFileName = "backup-metadata.json";

    public KvmBackupProvider(IOptions<KvmOptions> options, ILogger<KvmBackupProvider> logger)
    {
        _options = options.Value;
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

            // 1. Create temporary snapshot for consistency
            var snapshotName = $"backup_{backupId}";
            await RunCommandAsync("virsh", $"-c {_options.LibvirtUri} snapshot-create-as {vmNameOrId} {snapshotName} --disk-only --atomic");

            try
            {
                // 2. Dump XML definition
                var xmlOutput = await RunCommandAsync("virsh", $"-c {_options.LibvirtUri} dumpxml {vmNameOrId}");
                var xmlPath = Path.Combine(backupDir, $"{vmNameOrId}.xml");
                await File.WriteAllTextAsync(xmlPath, xmlOutput);

                // 3. Copy disk files
                var diskInfo = await RunCommandAsync("virsh", $"-c {_options.LibvirtUri} domblklist {vmNameOrId} --details");
                foreach (var line in diskInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4 && parts[0] == "file" && File.Exists(parts[3]))
                    {
                        var diskPath = parts[3];
                        var diskName = Path.GetFileName(diskPath);
                        var destDisk = Path.Combine(backupDir, diskName);
                        await RunCommandAsync("cp", $"--sparse=always \"{diskPath}\" \"{destDisk}\"");
                    }
                }
            }
            finally
            {
                // 4. Cleanup snapshot
                try
                {
                    await RunCommandAsync("virsh", $"-c {_options.LibvirtUri} snapshot-delete {vmNameOrId} {snapshotName} --metadata");
                }
                catch { }
            }

            var size = GetDirectorySize(backupDir);

            // Write metadata
            var metadata = new BackupInfoDto
            {
                Id = backupId,
                VmName = vmNameOrId,
                CreatedUtc = DateTime.UtcNow,
                SizeBytes = size,
                BackupPath = backupDir,
                HypervisorType = "KVM",
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
            // Find XML and disk files
            var xmlFiles = Directory.GetFiles(backupPath, "*.xml");
            if (xmlFiles.Length == 0)
                throw new FileNotFoundException("No XML domain definition found in backup");

            var xmlPath = xmlFiles[0];
            var xml = await File.ReadAllTextAsync(xmlPath);

            // If new name is provided, replace in XML
            if (!string.IsNullOrEmpty(newVmName))
            {
                xml = System.Text.RegularExpressions.Regex.Replace(xml, @"<name>.*?</name>", $"<name>{newVmName}</name>");
                xml = System.Text.RegularExpressions.Regex.Replace(xml, @"<uuid>.*?</uuid>", $"<uuid>{Guid.NewGuid()}</uuid>");
            }

            // Copy disks to default pool location
            var diskFiles = Directory.GetFiles(backupPath, "*.qcow2")
                .Concat(Directory.GetFiles(backupPath, "*.raw"))
                .Concat(Directory.GetFiles(backupPath, "*.img"))
                .ToArray();

            var defaultPool = "/var/lib/libvirt/images";
            foreach (var disk in diskFiles)
            {
                var destPath = Path.Combine(defaultPool, Path.GetFileName(disk));
                await RunCommandAsync("cp", $"--sparse=always \"{disk}\" \"{destPath}\"");
            }

            // Define the domain
            var tempXml = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempXml, xml);
            await RunCommandAsync("virsh", $"-c {_options.LibvirtUri} define \"{tempXml}\"");
            File.Delete(tempXml);

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
        var searchPaths = new[] { "/var/lib/libvirt/backups", "/opt/backups/vms", "/backup/vms" };

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

    private static async Task<string> RunCommandAsync(string command, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            throw new Exception($"Command '{command}' failed: {error}");

        return output;
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .GetFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}
