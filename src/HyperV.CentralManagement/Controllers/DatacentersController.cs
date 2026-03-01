using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/datacenters")]
public class DatacentersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public DatacentersController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("datacenter", "read")]
    public async Task<IActionResult> GetDatacenters()
    {
        var datacenters = await _db.Datacenters
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();

        return Ok(datacenters);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("datacenter", "read")]
    public async Task<IActionResult> GetDatacenter(Guid id)
    {
        var datacenter = await _db.Datacenters.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        return datacenter == null ? NotFound() : Ok(datacenter);
    }

    [HttpPost]
    [RequirePermission("datacenter", "create")]
    public async Task<IActionResult> CreateDatacenter([FromBody] DatacenterRequest request)
    {
        var datacenter = new Datacenter
        {
            Name = request.Name,
            Description = request.Description
        };

        _db.Datacenters.Add(datacenter);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "datacenter_created", datacenter.Name);

        return Ok(datacenter);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("datacenter", "update")]
    public async Task<IActionResult> UpdateDatacenter(Guid id, [FromBody] DatacenterRequest request)
    {
        var datacenter = await _db.Datacenters.FirstOrDefaultAsync(d => d.Id == id);
        if (datacenter == null)
        {
            return NotFound();
        }

        datacenter.Name = request.Name;
        datacenter.Description = request.Description;
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "datacenter_updated", datacenter.Name);

        return Ok(datacenter);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("datacenter", "delete")]
    public async Task<IActionResult> DeleteDatacenter(Guid id)
    {
        var datacenter = await _db.Datacenters.FirstOrDefaultAsync(d => d.Id == id);
        if (datacenter == null)
        {
            return NotFound();
        }

        _db.Datacenters.Remove(datacenter);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "datacenter_deleted", datacenter.Name);
        return Ok();
    }
}

public record DatacenterRequest(string Name, string? Description);
