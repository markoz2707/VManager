using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>VM backup and restore operations.</summary>
[ApiController]
[Route("api/v1")]
public class BackupController : ControllerBase
{
    private readonly IBackupProvider _backupProvider;
    private readonly ILogger<BackupController> _logger;

    public BackupController(IBackupProvider backupProvider, ILogger<BackupController> logger)
    {
        _backupProvider = backupProvider;
        _logger = logger;
    }

    /// <summary>Backup a VM.</summary>
    [HttpPost("vms/{name}/backup")]
    [Authorize]
    [ProducesResponseType(typeof(BackupResultDto), 200)]
    [SwaggerOperation(Summary = "Backup VM", Description = "Creates a backup of the specified VM.")]
    public async Task<IActionResult> BackupVm(string name, [FromBody] BackupVmRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.DestinationPath))
                return BadRequest(new { error = "DestinationPath is required" });

            var options = new BackupOptions
            {
                IncludeSnapshots = request.IncludeSnapshots,
                Description = request.Description
            };

            var result = await _backupProvider.BackupVmAsync(name, request.DestinationPath, options);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error backing up VM '{Name}'", name);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Restore a VM from backup.</summary>
    [HttpPost("backups/{id}/restore")]
    [Authorize]
    [ProducesResponseType(typeof(RestoreResultDto), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Restore VM", Description = "Restores a VM from the specified backup.")]
    public async Task<IActionResult> RestoreVm(string id, [FromBody] RestoreVmRequest? request = null)
    {
        try
        {
            var backup = await _backupProvider.GetBackupInfoAsync(id);
            if (backup == null)
                return NotFound(new { error = $"Backup '{id}' not found" });

            var result = await _backupProvider.RestoreVmAsync(backup.BackupPath, request?.NewVmName);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring from backup '{Id}'", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>List all backups.</summary>
    [HttpGet("backups")]
    [ProducesResponseType(typeof(List<BackupInfoDto>), 200)]
    [SwaggerOperation(Summary = "List backups", Description = "Lists all available VM backups.")]
    public async Task<IActionResult> ListBackups([FromQuery] string? vmName = null)
    {
        try
        {
            var backups = await _backupProvider.ListBackupsAsync(vmName);
            return Ok(backups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing backups");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get backup details.</summary>
    [HttpGet("backups/{id}")]
    [ProducesResponseType(typeof(BackupInfoDto), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get backup", Description = "Returns details of a specific backup.")]
    public async Task<IActionResult> GetBackup(string id)
    {
        try
        {
            var backup = await _backupProvider.GetBackupInfoAsync(id);
            if (backup == null)
                return NotFound(new { error = $"Backup '{id}' not found" });
            return Ok(backup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backup '{Id}'", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete a backup.</summary>
    [HttpDelete("backups/{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete backup", Description = "Deletes a VM backup.")]
    public async Task<IActionResult> DeleteBackup(string id)
    {
        try
        {
            await _backupProvider.DeleteBackupAsync(id);
            return Ok(new { status = "deleted", id });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting backup '{Id}'", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class BackupVmRequest
{
    public string DestinationPath { get; set; } = string.Empty;
    public bool IncludeSnapshots { get; set; }
    public string? Description { get; set; }
}

public class RestoreVmRequest
{
    public string? NewVmName { get; set; }
}
