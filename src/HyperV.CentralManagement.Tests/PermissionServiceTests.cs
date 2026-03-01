using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HyperV.CentralManagement.Tests;

public class PermissionServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly PermissionService _permissionService;

    public PermissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        _permissionService = new PermissionService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private async Task<(Guid userId, Guid roleId)> SeedAdminUserAsync()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        // Create an admin role with all permissions
        var role = new Role { Id = roleId, Name = "Admin", IsBuiltIn = true };
        _context.Roles.Add(role);

        // Create permissions
        var permissions = new[]
        {
            new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "read" },
            new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "create" },
            new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "update" },
            new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "delete" },
            new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "power" },
            new Permission { Id = Guid.NewGuid(), Resource = "host", Action = "read" },
            new Permission { Id = Guid.NewGuid(), Resource = "host", Action = "update" },
        };
        _context.Permissions.AddRange(permissions);

        // Assign all permissions to admin role
        foreach (var perm in permissions)
        {
            _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });
        }

        // Create user
        var user = new UserAccount { Id = userId, Username = "admin", PasswordHash = "hash", Role = "Admin" };
        _context.UserAccounts.Add(user);

        // Assign role to user
        _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

        await _context.SaveChangesAsync();
        return (userId, roleId);
    }

    private async Task<(Guid userId, Guid roleId)> SeedReadonlyUserAsync()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var role = new Role { Id = roleId, Name = "ReadOnly" };
        _context.Roles.Add(role);

        var readPermission = new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "read" };
        var hostReadPermission = new Permission { Id = Guid.NewGuid(), Resource = "host", Action = "read" };
        _context.Permissions.AddRange(readPermission, hostReadPermission);

        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = readPermission.Id });
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = hostReadPermission.Id });

        var user = new UserAccount { Id = userId, Username = "readonly", PasswordHash = "hash", Role = "ReadOnly" };
        _context.UserAccounts.Add(user);

        _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

        await _context.SaveChangesAsync();
        return (userId, roleId);
    }

    private async Task<(Guid userId, Guid roleId)> SeedOperatorUserAsync()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var role = new Role { Id = roleId, Name = "Operator" };
        _context.Roles.Add(role);

        var readPermission = new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "read" };
        var powerPermission = new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "power" };
        _context.Permissions.AddRange(readPermission, powerPermission);

        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = readPermission.Id });
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = powerPermission.Id });

        var user = new UserAccount { Id = userId, Username = "operator", PasswordHash = "hash", Role = "Operator" };
        _context.UserAccounts.Add(user);

        _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });

        await _context.SaveChangesAsync();
        return (userId, roleId);
    }

    private async Task<(Guid userId, Guid roleId, Guid clusterId)> SeedScopedUserAsync()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();

        var role = new Role { Id = roleId, Name = "ClusterAdmin" };
        _context.Roles.Add(role);

        var readPermission = new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "read" };
        var createPermission = new Permission { Id = Guid.NewGuid(), Resource = "vm", Action = "create" };
        _context.Permissions.AddRange(readPermission, createPermission);

        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = readPermission.Id });
        _context.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = createPermission.Id });

        var user = new UserAccount { Id = userId, Username = "scoped-user", PasswordHash = "hash", Role = "ClusterAdmin" };
        _context.UserAccounts.Add(user);

        // Assign role scoped to a specific cluster
        _context.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId, ClusterId = clusterId });

        await _context.SaveChangesAsync();
        return (userId, roleId, clusterId);
    }

    // --- HasPermissionAsync Tests ---

    [Fact]
    public async Task HasPermissionAsync_AdminUser_HasAllAccess()
    {
        // Arrange
        var (userId, _) = await SeedAdminUserAsync();

        // Act & Assert
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "read"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "create"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "update"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "delete"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "power"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "host", "read"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "host", "update"));
    }

    [Fact]
    public async Task HasPermissionAsync_ReadonlyUser_HasOnlyReadAccess()
    {
        // Arrange
        var (userId, _) = await SeedReadonlyUserAsync();

        // Act & Assert
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "read"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "host", "read"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "create"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "update"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "delete"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "power"));
    }

    [Fact]
    public async Task HasPermissionAsync_OperatorUser_HasReadAndPowerAccess()
    {
        // Arrange
        var (userId, _) = await SeedOperatorUserAsync();

        // Act & Assert
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "read"));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "power"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "create"));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "delete"));
    }

    [Fact]
    public async Task HasPermissionAsync_ScopedUser_HasAccessWithinScope()
    {
        // Arrange
        var (userId, _, clusterId) = await SeedScopedUserAsync();

        // Act & Assert - with scope
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "read", clusterId));
        Assert.True(await _permissionService.HasPermissionAsync(userId, "vm", "create", clusterId));
    }

    [Fact]
    public async Task HasPermissionAsync_ScopedUser_NoAccessOutsideScope()
    {
        // Arrange
        var (userId, _, _) = await SeedScopedUserAsync();
        var otherClusterId = Guid.NewGuid();

        // Act & Assert - with different scope
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "read", otherClusterId));
        Assert.False(await _permissionService.HasPermissionAsync(userId, "vm", "create", otherClusterId));
    }

    [Fact]
    public async Task HasPermissionAsync_NonExistentUser_ReturnsFalse()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _permissionService.HasPermissionAsync(nonExistentUserId, "vm", "read");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasPermissionAsync_NonExistentPermission_ReturnsFalse()
    {
        // Arrange
        var (userId, _) = await SeedAdminUserAsync();

        // Act
        var result = await _permissionService.HasPermissionAsync(userId, "nonexistent", "action");

        // Assert
        Assert.False(result);
    }

    // --- GetUserPermissionsAsync Tests ---

    [Fact]
    public async Task GetUserPermissionsAsync_AdminUser_ReturnsAllPermissions()
    {
        // Arrange
        var (userId, _) = await SeedAdminUserAsync();

        // Act
        var permissions = await _permissionService.GetUserPermissionsAsync(userId);

        // Assert
        Assert.NotEmpty(permissions);
        Assert.Equal(7, permissions.Count);
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ReadonlyUser_ReturnsReadPermissionsOnly()
    {
        // Arrange
        var (userId, _) = await SeedReadonlyUserAsync();

        // Act
        var permissions = await _permissionService.GetUserPermissionsAsync(userId);

        // Assert
        Assert.Equal(2, permissions.Count);
        Assert.All(permissions, p => Assert.Equal("read", p.Action));
    }

    [Fact]
    public async Task GetUserPermissionsAsync_NonExistentUser_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var permissions = await _permissionService.GetUserPermissionsAsync(nonExistentUserId);

        // Assert
        Assert.Empty(permissions);
    }

    // --- GetUserRoleNamesAsync Tests ---

    [Fact]
    public async Task GetUserRoleNamesAsync_AdminUser_ReturnsAdminRole()
    {
        // Arrange
        var (userId, _) = await SeedAdminUserAsync();

        // Act
        var roles = await _permissionService.GetUserRoleNamesAsync(userId);

        // Assert
        Assert.Single(roles);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task GetUserRoleNamesAsync_NonExistentUser_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var roles = await _permissionService.GetUserRoleNamesAsync(nonExistentUserId);

        // Assert
        Assert.Empty(roles);
    }
}
