using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/v1/vms")]
[Authorize]
public class VmInventoryController : ControllerBase
{
    private readonly VmInventoryService _vmInventoryService;
    private readonly MigrationOrchestrator _migrationOrchestrator;
    private readonly AuditLogService _auditLogService;

    public VmInventoryController(
        VmInventoryService vmInventoryService,
        MigrationOrchestrator migrationOrchestrator,
        AuditLogService auditLogService)
    {
        _vmInventoryService = vmInventoryService;
        _migrationOrchestrator = migrationOrchestrator;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// Get all VMs across all agents
    /// </summary>
    [HttpGet]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmInventoryDto>>> GetAllVms([FromQuery] string? search = null)
    {
        List<VmInventory> vms;
        if (!string.IsNullOrWhiteSpace(search))
        {
            vms = await _vmInventoryService.SearchVmsAsync(search);
        }
        else
        {
            vms = await _vmInventoryService.GetAllVmsAsync();
        }

        return Ok(vms.Select(MapToDto));
    }

    /// <summary>
    /// Get VMs for a specific agent
    /// </summary>
    [HttpGet("agent/{agentId:guid}")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmInventoryDto>>> GetVmsByAgent(Guid agentId)
    {
        var vms = await _vmInventoryService.GetVmsByAgentAsync(agentId);
        return Ok(vms.Select(MapToDto));
    }

    /// <summary>
    /// Get a specific VM by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<VmInventoryDto>> GetVm(Guid id)
    {
        var vm = await _vmInventoryService.GetVmAsync(id);
        if (vm == null)
            return NotFound();

        return Ok(MapToDto(vm));
    }

    /// <summary>
    /// Get VMs by folder
    /// </summary>
    [HttpGet("folder/{folderId:guid}")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmInventoryDto>>> GetVmsByFolder(Guid folderId)
    {
        var vms = await _vmInventoryService.GetVmsByFolderAsync(folderId);
        return Ok(vms.Select(MapToDto));
    }

    /// <summary>
    /// Get VMs without folder
    /// </summary>
    [HttpGet("folder/root")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmInventoryDto>>> GetVmsWithoutFolder()
    {
        var vms = await _vmInventoryService.GetVmsByFolderAsync(null);
        return Ok(vms.Select(MapToDto));
    }

    /// <summary>
    /// Get VM statistics
    /// </summary>
    [HttpGet("statistics")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<VmStatistics>> GetStatistics()
    {
        var stats = await _vmInventoryService.GetStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Update VM metadata (folder, tags, notes)
    /// </summary>
    [HttpPatch("{id:guid}/metadata")]
    [RequirePermission("vm", "update")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] VmMetadataUpdateDto dto)
    {
        var success = await _vmInventoryService.UpdateVmMetadataAsync(id, dto.FolderId, dto.Tags, dto.Notes);
        if (!success)
            return NotFound();

        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "UpdateVmMetadata", $"VM ID: {id}");
        return NoContent();
    }

    /// <summary>
    /// Power operation on a VM
    /// </summary>
    [HttpPost("{id:guid}/power")]
    [RequirePermission("vm", "power")]
    public async Task<IActionResult> PowerOperation(Guid id, [FromBody] PowerOperationDto dto)
    {
        var validOperations = new[] { "start", "stop", "shutdown", "pause", "resume" };
        if (!validOperations.Contains(dto.Operation.ToLower()))
            return BadRequest(new { error = $"Invalid operation. Valid operations: {string.Join(", ", validOperations)}" });

        var success = await _vmInventoryService.PowerOperationAsync(id, dto.Operation);
        if (!success)
            return BadRequest(new { error = "Power operation failed" });

        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "VmPower", $"VM ID: {id}, Operation: {dto.Operation}");
        return Ok(new { success = true, operation = dto.Operation });
    }

    /// <summary>
    /// Pre-check migration feasibility
    /// </summary>
    [HttpPost("{id:guid}/migrate/precheck")]
    [RequirePermission("vm", "migrate")]
    public async Task<ActionResult<MigrationPreCheckResult>> MigratePreCheck(Guid id, [FromBody] MigratePreCheckDto dto)
    {
        var result = await _migrationOrchestrator.PreCheckAsync(id, dto.DestinationAgentId);
        return Ok(result);
    }

    /// <summary>
    /// Migrate VM to another host (orchestrated)
    /// </summary>
    [HttpPost("{id:guid}/migrate")]
    [RequirePermission("vm", "migrate")]
    public async Task<ActionResult<MigrationTask>> MigrateVm(Guid id, [FromBody] MigrationRequestDto dto)
    {
        try
        {
            var task = await _migrationOrchestrator.InitiateMigrationAsync(
                id, dto.DestinationAgentId, dto.Live, dto.IncludeStorage, User.Identity?.Name);

            await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "VmMigrate",
                $"VM ID: {id}, Destination: {dto.DestinationAgentId}, Task: {task.Id}");

            return Ok(task);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get migration history for a VM
    /// </summary>
    [HttpGet("{id:guid}/migrate/history")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<MigrationTask>>> GetMigrationHistory(Guid id)
    {
        var history = await _migrationOrchestrator.GetMigrationHistoryAsync(id);
        return Ok(history);
    }

    /// <summary>
    /// Cancel a migration
    /// </summary>
    [HttpPost("migrate/{taskId:guid}/cancel")]
    [RequirePermission("vm", "migrate")]
    public async Task<IActionResult> CancelMigration(Guid taskId)
    {
        var success = await _migrationOrchestrator.CancelMigrationAsync(taskId);
        if (!success) return BadRequest(new { error = "Cannot cancel this migration." });
        return Ok(new { success = true });
    }

    /// <summary>
    /// Move VM to a folder
    /// </summary>
    [HttpPost("{id:guid}/move")]
    [RequirePermission("vm", "update")]
    public async Task<IActionResult> MoveToFolder(Guid id, [FromBody] MoveToFolderDto dto)
    {
        var success = await _vmInventoryService.MoveVmToFolderAsync(id, dto.FolderId);
        if (!success)
            return NotFound();

        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "MoveVmToFolder", $"VM ID: {id}, Folder: {dto.FolderId}");
        return NoContent();
    }

    // Folder endpoints

    /// <summary>
    /// Get all folders
    /// </summary>
    [HttpGet("folders")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmFolderDto>>> GetFolders()
    {
        var folders = await _vmInventoryService.GetFoldersAsync();
        return Ok(folders.Select(MapToDto));
    }

    /// <summary>
    /// Get root folders
    /// </summary>
    [HttpGet("folders/root")]
    [RequirePermission("vm", "read")]
    public async Task<ActionResult<List<VmFolderDto>>> GetRootFolders()
    {
        var folders = await _vmInventoryService.GetRootFoldersAsync();
        return Ok(folders.Select(MapToDto));
    }

    /// <summary>
    /// Create a folder
    /// </summary>
    [HttpPost("folders")]
    [RequirePermission("vm", "create")]
    public async Task<ActionResult<VmFolderDto>> CreateFolder([FromBody] CreateFolderDto dto)
    {
        var folder = await _vmInventoryService.CreateFolderAsync(dto.Name, dto.ParentId);
        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "CreateFolder", $"Folder: {dto.Name}");
        return CreatedAtAction(nameof(GetFolders), MapToDto(folder));
    }

    /// <summary>
    /// Rename a folder
    /// </summary>
    [HttpPatch("folders/{id:guid}")]
    [RequirePermission("vm", "update")]
    public async Task<IActionResult> RenameFolder(Guid id, [FromBody] RenameFolderDto dto)
    {
        var success = await _vmInventoryService.RenameFolderAsync(id, dto.Name);
        if (!success)
            return NotFound();

        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "RenameFolder", $"Folder ID: {id}, New Name: {dto.Name}");
        return NoContent();
    }

    /// <summary>
    /// Delete a folder
    /// </summary>
    [HttpDelete("folders/{id:guid}")]
    [RequirePermission("vm", "delete")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        var success = await _vmInventoryService.DeleteFolderAsync(id);
        if (!success)
            return NotFound();

        await _auditLogService.WriteAsync(User.Identity?.Name ?? "unknown", "DeleteFolder", $"Folder ID: {id}");
        return NoContent();
    }

    // Mapping helpers

    private static VmInventoryDto MapToDto(VmInventory vm) => new()
    {
        Id = vm.Id,
        AgentHostId = vm.AgentHostId,
        AgentHostName = vm.AgentHost?.Hostname,
        VmId = vm.VmId,
        Name = vm.Name,
        State = vm.State,
        CpuCount = vm.CpuCount,
        MemoryMB = vm.MemoryMB,
        Environment = vm.Environment,
        LastSyncUtc = vm.LastSyncUtc,
        FolderId = vm.FolderId,
        FolderName = vm.Folder?.Name,
        Tags = vm.Tags,
        Notes = vm.Notes
    };

    private static VmFolderDto MapToDto(VmFolder folder) => new()
    {
        Id = folder.Id,
        Name = folder.Name,
        ParentId = folder.ParentId,
        ChildCount = folder.Children.Count
    };
}

// DTOs

public class VmInventoryDto
{
    public Guid Id { get; set; }
    public Guid AgentHostId { get; set; }
    public string? AgentHostName { get; set; }
    public string VmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public string Environment { get; set; } = string.Empty;
    public DateTimeOffset LastSyncUtc { get; set; }
    public Guid? FolderId { get; set; }
    public string? FolderName { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
}

public class VmFolderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int ChildCount { get; set; }
}

public class VmMetadataUpdateDto
{
    public Guid? FolderId { get; set; }
    public string? Tags { get; set; }
    public string? Notes { get; set; }
}

public class PowerOperationDto
{
    public string Operation { get; set; } = string.Empty;
}

public class MigrationRequestDto
{
    public Guid DestinationAgentId { get; set; }
    public bool Live { get; set; } = true;
    public bool IncludeStorage { get; set; } = false;
}

public class MigratePreCheckDto
{
    public Guid DestinationAgentId { get; set; }
}

public class MoveToFolderDto
{
    public Guid? FolderId { get; set; }
}

public class CreateFolderDto
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
}

public class RenameFolderDto
{
    public string Name { get; set; } = string.Empty;
}
