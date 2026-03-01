using HyperV.CentralManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AgentHost> AgentHosts => Set<AgentHost>();
    public DbSet<Cluster> Clusters => Set<Cluster>();
    public DbSet<ClusterNode> ClusterNodes => Set<ClusterNode>();
    public DbSet<Datacenter> Datacenters => Set<Datacenter>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RegistrationToken> RegistrationTokens => Set<RegistrationToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<VmInventory> VmInventory => Set<VmInventory>();
    public DbSet<VmFolder> VmFolders => Set<VmFolder>();

    // RBAC
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    // Migration Tasks
    public DbSet<MigrationTask> MigrationTasks => Set<MigrationTask>();

    // Alerting
    public DbSet<AlertDefinition> AlertDefinitions => Set<AlertDefinition>();
    public DbSet<AlertInstance> AlertInstances => Set<AlertInstance>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<AlertNotificationChannel> AlertNotificationChannels => Set<AlertNotificationChannel>();

    // Metrics
    public DbSet<MetricDataPoint> MetricDataPoints => Set<MetricDataPoint>();

    // HA
    public DbSet<HaConfiguration> HaConfigurations => Set<HaConfiguration>();
    public DbSet<HaVmOverride> HaVmOverrides => Set<HaVmOverride>();
    public DbSet<HaEvent> HaEvents => Set<HaEvent>();

    // DRS
    public DbSet<DrsConfiguration> DrsConfigurations => Set<DrsConfiguration>();
    public DbSet<DrsRecommendation> DrsRecommendations => Set<DrsRecommendation>();

    // Content Library
    public DbSet<ContentLibraryItem> ContentLibraryItems => Set<ContentLibraryItem>();
    public DbSet<ContentLibrarySubscription> ContentLibrarySubscriptions => Set<ContentLibrarySubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Datacenter relationships
        modelBuilder.Entity<Datacenter>()
            .HasIndex(d => d.Name)
            .IsUnique();

        modelBuilder.Entity<Cluster>()
            .HasOne(c => c.Datacenter)
            .WithMany()
            .HasForeignKey(c => c.DatacenterId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AgentHost>()
            .HasOne(a => a.Datacenter)
            .WithMany()
            .HasForeignKey(a => a.DatacenterId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClusterNode>()
            .HasOne(n => n.Cluster)
            .WithMany(c => c.Nodes)
            .HasForeignKey(n => n.ClusterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ClusterNode>()
            .HasOne(n => n.AgentHost)
            .WithMany()
            .HasForeignKey(n => n.AgentHostId)
            .OnDelete(DeleteBehavior.Cascade);

        // VmInventory relationships
        modelBuilder.Entity<VmInventory>()
            .HasOne(v => v.AgentHost)
            .WithMany()
            .HasForeignKey(v => v.AgentHostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VmInventory>()
            .HasOne(v => v.Folder)
            .WithMany(f => f.Vms)
            .HasForeignKey(v => v.FolderId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VmInventory>()
            .HasIndex(v => new { v.AgentHostId, v.VmId })
            .IsUnique();

        // VmFolder self-referencing relationship
        modelBuilder.Entity<VmFolder>()
            .HasOne(f => f.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // RBAC: RolePermission composite key
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // RBAC: Permission unique constraint
        modelBuilder.Entity<Permission>()
            .HasIndex(p => new { p.Resource, p.Action })
            .IsUnique();

        // RBAC: Role name unique
        modelBuilder.Entity<Role>()
            .HasIndex(r => r.Name)
            .IsUnique();

        // RBAC: UserRole
        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // MigrationTask
        modelBuilder.Entity<MigrationTask>()
            .HasIndex(m => m.Status);

        // AlertNotificationChannel composite key
        modelBuilder.Entity<AlertNotificationChannel>()
            .HasKey(anc => new { anc.AlertDefinitionId, anc.NotificationChannelId });

        modelBuilder.Entity<AlertNotificationChannel>()
            .HasOne(anc => anc.AlertDefinition)
            .WithMany(ad => ad.NotificationChannels)
            .HasForeignKey(anc => anc.AlertDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AlertNotificationChannel>()
            .HasOne(anc => anc.NotificationChannel)
            .WithMany(nc => nc.AlertDefinitions)
            .HasForeignKey(anc => anc.NotificationChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        // AlertInstance
        modelBuilder.Entity<AlertInstance>()
            .HasOne(ai => ai.AlertDefinition)
            .WithMany(ad => ad.Instances)
            .HasForeignKey(ai => ai.AlertDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AlertInstance>()
            .HasIndex(ai => ai.Status);

        // MetricDataPoints indexes
        modelBuilder.Entity<MetricDataPoint>()
            .HasIndex(m => new { m.AgentHostId, m.MetricName, m.TimestampUtc });

        modelBuilder.Entity<MetricDataPoint>()
            .HasIndex(m => m.TimestampUtc);

        // HA
        modelBuilder.Entity<HaConfiguration>()
            .HasIndex(h => h.ClusterId)
            .IsUnique();

        modelBuilder.Entity<HaVmOverride>()
            .HasIndex(h => new { h.HaConfigurationId, h.VmInventoryId })
            .IsUnique();

        modelBuilder.Entity<HaEvent>()
            .HasIndex(h => h.TimestampUtc);

        // DRS
        modelBuilder.Entity<DrsConfiguration>()
            .HasIndex(d => d.ClusterId)
            .IsUnique();

        modelBuilder.Entity<DrsRecommendation>()
            .HasIndex(d => d.Status);

        // Content Library
        modelBuilder.Entity<ContentLibraryItem>()
            .HasIndex(c => c.Name);

        modelBuilder.Entity<ContentLibraryItem>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ContentLibrarySubscription>()
            .HasOne(s => s.LibraryItem)
            .WithMany(i => i.Subscriptions)
            .HasForeignKey(s => s.LibraryItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentLibrarySubscription>()
            .HasOne(s => s.AgentHost)
            .WithMany()
            .HasForeignKey(s => s.AgentHostId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContentLibrarySubscription>()
            .HasIndex(s => new { s.AgentHostId, s.LibraryItemId })
            .IsUnique();
    }
}
