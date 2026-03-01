using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/content-library")]
[Authorize]
public class ContentLibraryController : ControllerBase
{
    private readonly ContentLibraryService _service;

    public ContentLibraryController(ContentLibraryService service)
    {
        _service = service;
    }

    /// <summary>Lists all content library items with optional filtering.</summary>
    [HttpGet]
    [RequirePermission("content-library", "read")]
    public async Task<IActionResult> ListItems(
        [FromQuery] ContentLibraryItemType? type = null,
        [FromQuery] string? category = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var items = await _service.ListItemsAsync(type, category, tag, ct);
        return Ok(items.Select(i => new
        {
            i.Id,
            i.Name,
            i.Description,
            Type = i.Type.ToString(),
            i.Version,
            i.FileSize,
            i.Checksum,
            i.Tags,
            i.Category,
            i.IsPublic,
            i.OwnerId,
            i.CreatedUtc,
            i.ModifiedUtc,
            SubscriptionCount = i.Subscriptions?.Count ?? 0
        }));
    }

    /// <summary>Gets details of a specific content library item.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("content-library", "read")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct = default)
    {
        var item = await _service.GetItemAsync(id, ct);
        if (item == null)
            return NotFound(new { error = $"Content library item {id} not found" });

        return Ok(new
        {
            item.Id,
            item.Name,
            item.Description,
            Type = item.Type.ToString(),
            item.Version,
            item.FilePath,
            item.FileSize,
            item.Checksum,
            item.Tags,
            item.Category,
            item.IsPublic,
            item.OwnerId,
            item.CreatedUtc,
            item.ModifiedUtc,
            Subscriptions = item.Subscriptions?.Select(s => new
            {
                s.Id,
                s.AgentHostId,
                AgentHostname = s.AgentHost?.Hostname,
                SyncStatus = s.SyncStatus.ToString(),
                s.LastSyncUtc,
                s.SyncError
            })
        });
    }

    /// <summary>Uploads a new content library item.</summary>
    [HttpPost]
    [RequirePermission("content-library", "write")]
    [RequestSizeLimit(10L * 1024 * 1024 * 1024)] // 10GB limit
    public async Task<IActionResult> CreateItem(
        [FromForm] string name,
        [FromForm] string? description,
        [FromForm] ContentLibraryItemType type,
        [FromForm] string? version,
        [FromForm] string? tags,
        [FromForm] string? category,
        [FromForm] bool isPublic,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required" });

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Name is required" });

        await using var stream = file.OpenReadStream();
        var item = await _service.CreateItemAsync(
            name, description, type, version, tags, category, isPublic, null,
            stream, file.FileName, ct);

        return Created($"/api/content-library/{item.Id}", new
        {
            item.Id,
            item.Name,
            item.Description,
            Type = item.Type.ToString(),
            item.Version,
            item.FileSize,
            item.Checksum,
            item.CreatedUtc
        });
    }

    /// <summary>Updates metadata of a content library item.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission("content-library", "write")]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateContentLibraryItemRequest request, CancellationToken ct = default)
    {
        var item = await _service.UpdateItemAsync(
            id, request.Name, request.Description, request.Version,
            request.Tags, request.Category, request.IsPublic, ct);

        if (item == null)
            return NotFound(new { error = $"Content library item {id} not found" });

        return Ok(new { item.Id, item.Name, item.ModifiedUtc });
    }

    /// <summary>Deletes a content library item.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("content-library", "delete")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct = default)
    {
        var deleted = await _service.DeleteItemAsync(id, ct);
        if (!deleted)
            return NotFound(new { error = $"Content library item {id} not found" });

        return Ok(new { message = "Deleted" });
    }

    /// <summary>Deploys a content library item to an agent host.</summary>
    [HttpPost("{id:guid}/deploy/{agentId:guid}")]
    [RequirePermission("content-library", "write")]
    public async Task<IActionResult> DeployToAgent(Guid id, Guid agentId, CancellationToken ct = default)
    {
        try
        {
            var subscription = await _service.DeployToAgentAsync(id, agentId, ct);
            return Accepted(new
            {
                subscription.Id,
                subscription.AgentHostId,
                subscription.LibraryItemId,
                SyncStatus = subscription.SyncStatus.ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Deploys a template as a new VM on the specified agent.</summary>
    [HttpPost("{id:guid}/deploy")]
    [RequirePermission("content-library", "write")]
    public async Task<IActionResult> DeployTemplate(Guid id, [FromBody] DeployTemplateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.VmName))
            return BadRequest(new { error = "VmName is required" });

        try
        {
            var spec = new DeployVmSpec
            {
                VmName = request.VmName,
                CpuCount = request.CpuCount,
                MemoryMB = request.MemoryMB,
                Generation = request.Generation,
                DiskSizeGB = request.DiskSizeGB,
                NetworkName = request.NetworkName
            };

            var result = await _service.DeployTemplateAsync(id, request.TargetAgentId, spec, ct);
            return result.Success ? Ok(result) : StatusCode(500, result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Triggers synchronization of a content library item to all its subscribers.</summary>
    [HttpPost("{id:guid}/sync")]
    [RequirePermission("content-library", "write")]
    public async Task<IActionResult> SyncItem(Guid id, CancellationToken ct = default)
    {
        try
        {
            await _service.SyncItemAsync(id, ct);
            return Ok(new { message = "Sync initiated" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}

public class DeployTemplateRequest
{
    public Guid TargetAgentId { get; set; }
    public string VmName { get; set; } = string.Empty;
    public int CpuCount { get; set; } = 2;
    public long MemoryMB { get; set; } = 2048;
    public int Generation { get; set; } = 2;
    public long DiskSizeGB { get; set; } = 40;
    public string? NetworkName { get; set; }
}

public class UpdateContentLibraryItemRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Tags { get; set; }
    public string? Category { get; set; }
    public bool? IsPublic { get; set; }
}
