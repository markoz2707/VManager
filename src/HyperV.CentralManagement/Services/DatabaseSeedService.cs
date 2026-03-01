using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HyperV.CentralManagement.Services;

public class InitialAdminOptions
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin";
}

public class DatabaseSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly InitialAdminOptions _options;

    public DatabaseSeedService(IServiceProvider serviceProvider, IOptions<InitialAdminOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    // All resources and actions for permission matrix
    private static readonly string[] Resources = { "vm", "host", "cluster", "network", "storage", "user", "audit", "datacenter" };
    private static readonly string[] Actions = { "read", "create", "update", "delete", "power", "migrate", "snapshot" };

    // Which resource-action combinations are valid
    private static readonly Dictionary<string, string[]> ResourceActions = new()
    {
        ["vm"] = new[] { "read", "create", "update", "delete", "power", "migrate", "snapshot" },
        ["host"] = new[] { "read", "create", "update", "delete" },
        ["cluster"] = new[] { "read", "create", "update", "delete" },
        ["network"] = new[] { "read", "create", "update", "delete" },
        ["storage"] = new[] { "read", "create", "update", "delete" },
        ["user"] = new[] { "read", "create", "update", "delete" },
        ["audit"] = new[] { "read" },
        ["datacenter"] = new[] { "read", "create", "update", "delete" }
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        await db.Database.MigrateAsync(cancellationToken);

        await SeedPermissionsAsync(db, cancellationToken);
        await SeedRolesAsync(db, cancellationToken);
        await SeedAdminUserAsync(db, hasher, cancellationToken);
    }

    private async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Permissions.AnyAsync(ct))
            return;

        foreach (var (resource, actions) in ResourceActions)
        {
            foreach (var action in actions)
            {
                db.Permissions.Add(new Permission
                {
                    Resource = resource,
                    Action = action,
                    Description = $"{action} {resource}"
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct))
            return;

        var allPermissions = await db.Permissions.ToListAsync(ct);

        // Administrator - all permissions
        var adminRole = new Role
        {
            Name = "Administrator",
            Description = "Full access to all resources and actions",
            IsBuiltIn = true
        };

        foreach (var p in allPermissions)
        {
            adminRole.RolePermissions.Add(new RolePermission { Permission = p });
        }

        db.Roles.Add(adminRole);

        // Operator - read all, power/migrate/snapshot VMs, read audit
        var operatorRole = new Role
        {
            Name = "Operator",
            Description = "Can manage VMs (power, migrate, snapshot) and read all resources",
            IsBuiltIn = true
        };

        var operatorPermissions = allPermissions.Where(p =>
            p.Action == "read" ||
            (p.Resource == "vm" && new[] { "power", "migrate", "snapshot", "update" }.Contains(p.Action)));

        foreach (var p in operatorPermissions)
        {
            operatorRole.RolePermissions.Add(new RolePermission { Permission = p });
        }

        db.Roles.Add(operatorRole);

        // ReadOnly - read all
        var readOnlyRole = new Role
        {
            Name = "ReadOnly",
            Description = "Read-only access to all resources",
            IsBuiltIn = true
        };

        var readPermissions = allPermissions.Where(p => p.Action == "read");
        foreach (var p in readPermissions)
        {
            readOnlyRole.RolePermissions.Add(new RolePermission { Permission = p });
        }

        db.Roles.Add(readOnlyRole);

        // VmAdmin - full VM access, read host/cluster/network/storage
        var vmAdminRole = new Role
        {
            Name = "VmAdmin",
            Description = "Full VM management with read access to infrastructure",
            IsBuiltIn = true
        };

        var vmAdminPermissions = allPermissions.Where(p =>
            p.Resource == "vm" ||
            (new[] { "host", "cluster", "network", "storage", "audit" }.Contains(p.Resource) && p.Action == "read"));

        foreach (var p in vmAdminPermissions)
        {
            vmAdminRole.RolePermissions.Add(new RolePermission { Permission = p });
        }

        db.Roles.Add(vmAdminRole);

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminUserAsync(AppDbContext db, PasswordHasher hasher, CancellationToken ct)
    {
        if (await db.UserAccounts.AnyAsync(ct))
            return;

        var adminUser = new UserAccount
        {
            Username = _options.Username,
            PasswordHash = hasher.Hash(_options.Password),
            Role = "Admin",
            IsActive = true,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        db.UserAccounts.Add(adminUser);
        await db.SaveChangesAsync(ct);

        // Assign Administrator role
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Administrator", ct);
        if (adminRole != null)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRole.Id
            });

            await db.SaveChangesAsync(ct);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
