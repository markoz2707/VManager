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
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<RegistrationToken> RegistrationTokens => Set<RegistrationToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
