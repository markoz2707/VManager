using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

/// <summary>
/// Represents a VM in the central inventory (cached from agents)
/// </summary>
public class VmInventory
{
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the agent host that owns this VM
    /// </summary>
    public Guid AgentHostId { get; set; }

    /// <summary>
    /// Original VM ID from the agent
    /// </summary>
    [MaxLength(100)]
    public string VmId { get; set; } = string.Empty;

    /// <summary>
    /// VM name
    /// </summary>
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current state (Running, Off, Paused, Saved, etc.)
    /// </summary>
    [MaxLength(50)]
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Number of virtual CPUs
    /// </summary>
    public int CpuCount { get; set; }

    /// <summary>
    /// Assigned memory in MB
    /// </summary>
    public long MemoryMB { get; set; }

    /// <summary>
    /// VM source environment (HCS or WMI)
    /// </summary>
    [MaxLength(10)]
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Last time this VM was synced from the agent
    /// </summary>
    public DateTimeOffset LastSyncUtc { get; set; }

    /// <summary>
    /// Optional folder for organization
    /// </summary>
    public Guid? FolderId { get; set; }

    /// <summary>
    /// Comma-separated tags for categorization
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Additional notes or description
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation property
    public AgentHost? AgentHost { get; set; }
    public VmFolder? Folder { get; set; }
}

/// <summary>
/// Folder for organizing VMs hierarchically
/// </summary>
public class VmFolder
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public VmFolder? Parent { get; set; }
    public List<VmFolder> Children { get; set; } = new();
    public List<VmInventory> Vms { get; set; } = new();
}
