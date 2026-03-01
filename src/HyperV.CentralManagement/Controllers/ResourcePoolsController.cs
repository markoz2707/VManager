using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/resource-pools")]
[Authorize]
public class ResourcePoolsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public ResourcePoolsController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> ListPools([FromQuery] Guid? clusterId = null)
    {
        var query = _db.ResourcePools.AsQueryable();
        if (clusterId.HasValue)
            query = query.Where(rp => rp.ClusterId == clusterId.Value);

        var pools = await query
            .Include(rp => rp.AssignedVms)
            .AsNoTracking()
            .ToListAsync();

        return Ok(pools.Select(rp => new
        {
            rp.Id,
            rp.Name,
            rp.ClusterId,
            rp.MaxCpuCores,
            rp.MaxMemoryMB,
            rp.MaxStorageGB,
            rp.Description,
            AssignedVmCount = rp.AssignedVms.Count,
            rp.CreatedUtc
        }));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetPool(Guid id)
    {
        var pool = await _db.ResourcePools
            .Include(rp => rp.AssignedVms)
            .AsNoTracking()
            .FirstOrDefaultAsync(rp => rp.Id == id);

        if (pool == null) return NotFound();

        // Calculate current usage
        var vmIds = pool.AssignedVms.Select(av => av.VmInventoryId).ToList();
        var vms = await _db.VmInventory
            .Where(v => vmIds.Contains(v.Id))
            .ToListAsync();

        var usedCpu = vms.Sum(v => v.CpuCount);
        var usedMemory = vms.Sum(v => v.MemoryMB);

        return Ok(new
        {
            pool.Id,
            pool.Name,
            pool.ClusterId,
            pool.MaxCpuCores,
            pool.MaxMemoryMB,
            pool.MaxStorageGB,
            pool.Description,
            UsedCpuCores = usedCpu,
            UsedMemoryMB = usedMemory,
            AssignedVms = vms.Select(v => new { v.Id, v.Name, v.State, v.CpuCount, v.MemoryMB }),
            pool.CreatedUtc
        });
    }

    [HttpPost]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> CreatePool([FromBody] CreateResourcePoolDto dto)
    {
        if (string.IsNullOrEmpty(dto.Name))
            return BadRequest(new { error = "Name is required" });

        var pool = new ResourcePool
        {
            Name = dto.Name,
            ClusterId = dto.ClusterId,
            MaxCpuCores = dto.MaxCpuCores,
            MaxMemoryMB = dto.MaxMemoryMB,
            MaxStorageGB = dto.MaxStorageGB,
            Description = dto.Description
        };

        _db.ResourcePools.Add(pool);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "resource_pool_created", pool.Name);
        return Created($"/api/resource-pools/{pool.Id}", new { pool.Id, pool.Name });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> UpdatePool(Guid id, [FromBody] CreateResourcePoolDto dto)
    {
        var pool = await _db.ResourcePools.FindAsync(id);
        if (pool == null) return NotFound();

        pool.Name = dto.Name;
        pool.MaxCpuCores = dto.MaxCpuCores;
        pool.MaxMemoryMB = dto.MaxMemoryMB;
        pool.MaxStorageGB = dto.MaxStorageGB;
        pool.Description = dto.Description;

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "resource_pool_updated", pool.Name);
        return Ok(new { pool.Id, pool.Name });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> DeletePool(Guid id)
    {
        var pool = await _db.ResourcePools.FindAsync(id);
        if (pool == null) return NotFound();

        _db.ResourcePools.Remove(pool);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "resource_pool_deleted", pool.Name);
        return Ok(new { message = "Deleted" });
    }

    [HttpPost("{id:guid}/assign/{vmId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> AssignVm(Guid id, Guid vmId)
    {
        var pool = await _db.ResourcePools
            .Include(rp => rp.AssignedVms)
            .FirstOrDefaultAsync(rp => rp.Id == id);
        if (pool == null) return NotFound(new { error = "Resource pool not found" });

        var vm = await _db.VmInventory.FindAsync(vmId);
        if (vm == null) return NotFound(new { error = "VM not found" });

        // Check quota
        var currentCpu = await _db.VmInventory
            .Where(v => pool.AssignedVms.Select(av => av.VmInventoryId).Contains(v.Id))
            .SumAsync(v => v.CpuCount);

        var currentMemory = await _db.VmInventory
            .Where(v => pool.AssignedVms.Select(av => av.VmInventoryId).Contains(v.Id))
            .SumAsync(v => v.MemoryMB);

        if (pool.MaxCpuCores > 0 && currentCpu + vm.CpuCount > pool.MaxCpuCores)
            return BadRequest(new { error = $"Adding this VM would exceed CPU quota ({currentCpu + vm.CpuCount}/{pool.MaxCpuCores} cores)" });

        if (pool.MaxMemoryMB > 0 && currentMemory + vm.MemoryMB > pool.MaxMemoryMB)
            return BadRequest(new { error = $"Adding this VM would exceed memory quota ({currentMemory + vm.MemoryMB}/{pool.MaxMemoryMB} MB)" });

        if (pool.AssignedVms.Any(av => av.VmInventoryId == vmId))
            return BadRequest(new { error = "VM is already assigned to this pool" });

        _db.ResourcePoolVms.Add(new ResourcePoolVm { ResourcePoolId = id, VmInventoryId = vmId });
        await _db.SaveChangesAsync();

        return Ok(new { status = "assigned", poolId = id, vmId });
    }

    [HttpDelete("{id:guid}/assign/{vmId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> UnassignVm(Guid id, Guid vmId)
    {
        var assignment = await _db.ResourcePoolVms
            .FirstOrDefaultAsync(rpv => rpv.ResourcePoolId == id && rpv.VmInventoryId == vmId);

        if (assignment == null)
            return NotFound(new { error = "VM is not assigned to this pool" });

        _db.ResourcePoolVms.Remove(assignment);
        await _db.SaveChangesAsync();

        return Ok(new { status = "unassigned", poolId = id, vmId });
    }
}

public class CreateResourcePoolDto
{
    public string Name { get; set; } = string.Empty;
    public Guid ClusterId { get; set; }
    public int MaxCpuCores { get; set; }
    public long MaxMemoryMB { get; set; }
    public long MaxStorageGB { get; set; }
    public string? Description { get; set; }
}
