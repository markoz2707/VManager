using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum ContentLibraryItemType
{
    Template = 0,
    ISO = 1,
    Script = 2,
    OVF = 3
}

public enum ContentSyncStatus
{
    Pending = 0,
    Syncing = 1,
    Synced = 2,
    Failed = 3
}

public class ContentLibraryItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public ContentLibraryItemType Type { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(128)]
    public string? Checksum { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsPublic { get; set; } = true;

    public Guid? OwnerId { get; set; }
    public UserAccount? Owner { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedUtc { get; set; }

    public List<ContentLibrarySubscription> Subscriptions { get; set; } = new();
}

public class ContentLibrarySubscription
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AgentHostId { get; set; }
    public AgentHost? AgentHost { get; set; }

    public Guid LibraryItemId { get; set; }
    public ContentLibraryItem? LibraryItem { get; set; }

    public ContentSyncStatus SyncStatus { get; set; } = ContentSyncStatus.Pending;

    public DateTimeOffset? LastSyncUtc { get; set; }

    [MaxLength(500)]
    public string? SyncError { get; set; }
}
