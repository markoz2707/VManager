using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly AuditLogService _audit;

    public UsersController(AppDbContext db, PasswordHasher hasher, AuditLogService audit)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("user", "read")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.UserAccounts
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .ToListAsync();

        return Ok(users.Select(MapToDto));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("user", "read")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var user = await _db.UserAccounts
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();
        return Ok(MapToDto(user));
    }

    [HttpPost]
    [RequirePermission("user", "create")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (await _db.UserAccounts.AnyAsync(u => u.Username == request.Username))
            return Conflict(new { error = "Username already exists." });

        var user = new UserAccount
        {
            Username = request.Username,
            PasswordHash = _hasher.Hash(request.Password),
            Email = request.Email,
            Role = "User",
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync();

        // Assign roles if provided
        if (request.RoleIds?.Any() == true)
        {
            var roles = await _db.Roles
                .Where(r => request.RoleIds.Contains(r.Id))
                .ToListAsync();

            foreach (var role in roles)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });
            }

            await _db.SaveChangesAsync();
        }

        await _audit.WriteAsync(User.Identity?.Name, "user_created", user.Username);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { user.Id, user.Username });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("user", "update")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(request.Password))
            user.PasswordHash = _hasher.Hash(request.Password);

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "user_updated", user.Username);
        return Ok(new { user.Id, user.Username });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("user", "delete")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        _db.UserAccounts.Remove(user);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "user_deleted", user.Username);
        return Ok();
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission("user", "update")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest request)
    {
        if (!await _db.UserAccounts.AnyAsync(u => u.Id == id))
            return NotFound();

        if (!await _db.Roles.AnyAsync(r => r.Id == request.RoleId))
            return BadRequest(new { error = "Role not found." });

        if (await _db.UserRoles.AnyAsync(ur =>
            ur.UserId == id && ur.RoleId == request.RoleId &&
            ur.ClusterId == request.ClusterId && ur.AgentHostId == request.AgentHostId))
        {
            return Conflict(new { error = "Role already assigned with this scope." });
        }

        var userRole = new UserRole
        {
            UserId = id,
            RoleId = request.RoleId,
            ClusterId = request.ClusterId,
            AgentHostId = request.AgentHostId
        };

        _db.UserRoles.Add(userRole);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "role_assigned", $"User: {id}, Role: {request.RoleId}");
        return Ok(userRole);
    }

    [HttpDelete("{id:guid}/roles/{userRoleId:guid}")]
    [RequirePermission("user", "update")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid userRoleId)
    {
        var userRole = await _db.UserRoles
            .FirstOrDefaultAsync(ur => ur.Id == userRoleId && ur.UserId == id);

        if (userRole == null) return NotFound();

        _db.UserRoles.Remove(userRole);
        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "role_removed", $"User: {id}, UserRole: {userRoleId}");
        return Ok();
    }

    private static UserDto MapToDto(UserAccount user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        IsActive = user.IsActive,
        CreatedUtc = user.CreatedUtc,
        LastLoginUtc = user.LastLoginUtc,
        Roles = user.UserRoles.Select(ur => new UserRoleDto
        {
            Id = ur.Id,
            RoleId = ur.RoleId,
            RoleName = ur.Role?.Name ?? "",
            ClusterId = ur.ClusterId,
            AgentHostId = ur.AgentHostId,
            AssignedUtc = ur.AssignedUtc
        }).ToList()
    };
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }
    public List<UserRoleDto> Roles { get; set; } = new();
}

public class UserRoleDto
{
    public Guid Id { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public Guid? ClusterId { get; set; }
    public Guid? AgentHostId { get; set; }
    public DateTimeOffset AssignedUtc { get; set; }
}

public record CreateUserRequest(string Username, string Password, string? Email, List<Guid>? RoleIds);
public record UpdateUserRequest(string? Email, string? Password, bool? IsActive);
public record AssignRoleRequest(Guid RoleId, Guid? ClusterId, Guid? AgentHostId);
