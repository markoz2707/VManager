using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum MigrationStatus
{
    Pending = 0,
    PreChecking = 1,
    Migrating = 2,
    PostProcessing = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public class MigrationTask
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VmInventoryId { get; set; }

    [MaxLength(200)]
    public string VmName { get; set; } = string.Empty;

    public Guid SourceAgentId { get; set; }

    [MaxLength(200)]
    public string SourceAgentName { get; set; } = string.Empty;

    public Guid DestinationAgentId { get; set; }

    [MaxLength(200)]
    public string DestinationAgentName { get; set; } = string.Empty;

    public MigrationStatus Status { get; set; } = MigrationStatus.Pending;

    public int ProgressPercent { get; set; }

    public bool LiveMigration { get; set; } = true;

    public bool IncludeStorage { get; set; }

    [MaxLength(200)]
    public string? InitiatedBy { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [MaxLength(2000)]
    public string? PreCheckResults { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? StartedUtc { get; set; }

    public DateTimeOffset? CompletedUtc { get; set; }
}
