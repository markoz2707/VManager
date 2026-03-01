using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public RolesController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("user", "read")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .ToListAsync();

        return Ok(roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            IsBuiltIn = r.IsBuiltIn,
            CreatedUtc = r.CreatedUtc,
            Permissions = r.RolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission!.Id,
                Resource = rp.Permission.Resource,
                Action = rp.Permission.Action,
                Description = rp.Permission.Description
            }).ToList()
        }));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("user", "read")]
    public async Task<IActionResult> GetRole(Guid id)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            IsBuiltIn = role.IsBuiltIn,
            CreatedUtc = role.CreatedUtc,
            Permissions = role.RolePermissions.Select(rp => new PermissionDto
            {
                Id = rp.Permission!.Id,
                Resource = rp.Permission.Resource,
                Action = rp.Permission.Action,
                Description = rp.Permission.Description
            }).ToList()
        });
    }

    [HttpPost]
    [RequirePermission("user", "create")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == request.Name))
            return Conflict(new { error = "Role name already exists." });

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description,
            IsBuiltIn = false
        };

        if (request.PermissionIds?.Any() == true)
        {
            var permissions = await _db.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync();

            foreach (var p in permissions)
            {
                role.RolePermissions.Add(new RolePermission { Permission = p });
            }
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        await _audit.WriteAsync(User.Identity?.Name, "role_created", role.Name);
        return CreatedAtAction(nameof(GetRole), new { id = role.Id }, new { role.Id, role.Name });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("user", "update")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();
        if (role.IsBuiltIn) return BadRequest(new { error = "Cannot modify built-in roles." });

        role.Name = request.Name;
        role.Description = request.Description;

        // Replace permissions
        role.RolePermissions.Clear();

        if (request.PermissionIds?.Any() == true)
        {
            var permissions = await _db.Permissions
                .Where(p => request.PermissionIds.Contains(p.Id))
                .ToListAsync();

            foreach (var p in permissions)
            {
                role.RolePermissions.Add(new RolePermission { PermissionId = p.Id });
            }
        }

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "role_updated", role.Name);
        return Ok(new { role.Id, role.Name });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("user", "delete")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id);
        if (role == null) return NotFound();
        if (role.IsBuiltIn) return BadRequest(new { error = "Cannot delete built-in roles." });

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "role_deleted", role.Name);
        return Ok();
    }

    [HttpGet("permissions")]
    [RequirePermission("user", "read")]
    public async Task<IActionResult> GetAllPermissions()
    {
        var permissions = await _db.Permissions.AsNoTracking().ToListAsync();
        return Ok(permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Resource = p.Resource,
            Action = p.Action,
            Description = p.Description
        }));
    }
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public record CreateRoleRequest(string Name, string? Description, List<Guid>? PermissionIds);
public record UpdateRoleRequest(string Name, string? Description, List<Guid>? PermissionIds);
