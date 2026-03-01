using HyperV.CentralManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Services;

public class PermissionService
{
    private readonly AppDbContext _context;

    public PermissionService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Check if a user has a specific permission, optionally scoped to a cluster or agent
    /// </summary>
    public async Task<bool> HasPermissionAsync(Guid userId, string resource, string action, Guid? scopeId = null)
    {
        var query = _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Where(rp => rp.Permission!.Resource == resource && rp.Permission.Action == action);

        if (scopeId.HasValue)
        {
            // Check for global roles (no scope) OR matching scope
            var userRolesWithPermission = _context.UserRoles
                .Where(ur => ur.UserId == userId &&
                    (ur.ClusterId == null && ur.AgentHostId == null) ||
                    ur.ClusterId == scopeId.Value ||
                    ur.AgentHostId == scopeId.Value)
                .Select(ur => ur.RoleId);

            query = _context.RolePermissions
                .Where(rp => userRolesWithPermission.Contains(rp.RoleId) &&
                    rp.Permission!.Resource == resource &&
                    rp.Permission.Action == action);
        }

        return await query.AnyAsync();
    }

    /// <summary>
    /// Get all permissions for a user (aggregated from all roles)
    /// </summary>
    public async Task<List<PermissionInfo>> GetUserPermissionsAsync(Guid userId)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role!.RolePermissions)
            .Select(rp => new PermissionInfo
            {
                Resource = rp.Permission!.Resource,
                Action = rp.Permission.Action
            })
            .Distinct()
            .ToListAsync();
    }

    /// <summary>
    /// Get all role names for a user
    /// </summary>
    public async Task<List<string>> GetUserRoleNamesAsync(Guid userId)
    {
        return await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name)
            .Distinct()
            .ToListAsync();
    }
}

public class PermissionInfo
{
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
