using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// Clone type for VM cloning operations.
/// </summary>
public enum VmCloneType
{
    /// <summary>Full clone - complete copy of the VM with independent disks.</summary>
    Full = 0,

    /// <summary>Linked clone - shares base disks with parent VM, uses differencing disks.</summary>
    Linked = 1
}

/// <summary>
/// Request model for cloning a VM.
/// </summary>
public class CloneVmRequest
{
    /// <summary>Source VM name to clone from.</summary>
    [Required(ErrorMessage = "Source VM name is required")]
    [StringLength(100, ErrorMessage = "Source VM name cannot exceed 100 characters")]
    public string SourceVmName { get; set; } = string.Empty;

    /// <summary>Name for the new cloned VM.</summary>
    [Required(ErrorMessage = "New VM name is required")]
    [StringLength(100, ErrorMessage = "New VM name cannot exceed 100 characters")]
    public string NewVmName { get; set; } = string.Empty;

    /// <summary>Type of clone to create.</summary>
    public VmCloneType CloneType { get; set; } = VmCloneType.Full;

    /// <summary>Optional new memory allocation in MB (overrides source VM memory).</summary>
    [Range(256, 1048576, ErrorMessage = "Memory must be between 256 MB and 1048576 MB")]
    public int? NewMemoryMB { get; set; }

    /// <summary>Optional new CPU count (overrides source VM CPU count).</summary>
    [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128")]
    public int? NewCpuCount { get; set; }

    /// <summary>Optional new disk size in GB (for full clones, expands the copied disk).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Disk size must be at least 1 GB")]
    public int? NewDiskSizeGB { get; set; }

    /// <summary>Optional new VHD path for the cloned VM.</summary>
    [StringLength(260, ErrorMessage = "VHD path cannot exceed 260 characters")]
    public string? NewVhdPath { get; set; }

    /// <summary>Optional new network switch name.</summary>
    [StringLength(100, ErrorMessage = "Switch name cannot exceed 100 characters")]
    public string? NewSwitchName { get; set; }

    /// <summary>Optional notes for the cloned VM.</summary>
    [StringLength(1024, ErrorMessage = "Notes cannot exceed 1024 characters")]
    public string? Notes { get; set; }

    /// <summary>Whether to start the cloned VM after creation.</summary>
    public bool StartAfterClone { get; set; } = false;

    /// <summary>Preferred backend for the cloned VM.</summary>
    public VmCreationMode? PreferredBackend { get; set; }
}

/// <summary>
/// Response model for clone operations.
/// </summary>
public class CloneVmResponse
{
    /// <summary>Name of the cloned VM.</summary>
    public string ClonedVmName { get; set; } = string.Empty;

    /// <summary>Clone type used.</summary>
    public VmCloneType CloneType { get; set; }

    /// <summary>Backend used for cloning.</summary>
    public string Backend { get; set; } = string.Empty;

    /// <summary>Whether the clone was started automatically.</summary>
    public bool Started { get; set; }

    /// <summary>Timestamp of clone completion.</summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>Additional information about the clone operation.</summary>
    public Dictionary<string, string> Details { get; set; } = new();
}