using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// VM template types for simplified creation.
/// </summary>
public enum VmTemplateType
{
    /// <summary>Development template - higher resources for development work.</summary>
    Development = 0,

    /// <summary>Production template - balanced resources for production workloads.</summary>
    Production = 1,

    /// <summary>Lightweight template - minimal resources for testing or small applications.</summary>
    Lightweight = 2
}

/// <summary>
/// Configuration for VM templates.
/// </summary>
public class VmTemplateConfiguration
{
    /// <summary>Template type.</summary>
    public VmTemplateType Type { get; set; }

    /// <summary>Default memory in MB.</summary>
    public int DefaultMemoryMB { get; set; }

    /// <summary>Default CPU count.</summary>
    public int DefaultCpuCount { get; set; }

    /// <summary>Default disk size in GB.</summary>
    public int DefaultDiskSizeGB { get; set; }

    /// <summary>Default generation (1 or 2).</summary>
    public int DefaultGeneration { get; set; }

    /// <summary>Default secure boot enabled.</summary>
    public bool DefaultSecureBoot { get; set; }

    /// <summary>Supported creation modes.</summary>
    public List<VmCreationMode> SupportedModes { get; set; } = new();

    /// <summary>Description of the template.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// VM template stored in the system.
/// </summary>
public class VmTemplate
{
    /// <summary>Unique template ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Template name.</summary>
    [Required(ErrorMessage = "Template name is required")]
    [StringLength(100, ErrorMessage = "Template name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Template description.</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Source VM name used to create this template.</summary>
    [Required(ErrorMessage = "Source VM name is required")]
    [StringLength(100, ErrorMessage = "Source VM name cannot exceed 100 characters")]
    public string SourceVmName { get; set; } = string.Empty;

    /// <summary>Backend used for the source VM.</summary>
    public VmCreationMode SourceBackend { get; set; }

    /// <summary>Memory configuration in MB.</summary>
    [Range(256, 1048576, ErrorMessage = "Memory must be between 256 MB and 1048576 MB")]
    public int MemoryMB { get; set; }

    /// <summary>CPU count.</summary>
    [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128")]
    public int CpuCount { get; set; }

    /// <summary>Disk size in GB.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Disk size must be at least 1 GB")]
    public int DiskSizeGB { get; set; }

    /// <summary>VM generation (1 or 2).</summary>
    [Range(1, 2, ErrorMessage = "Generation must be 1 or 2")]
    public int Generation { get; set; }

    /// <summary>Secure boot enabled.</summary>
    public bool SecureBoot { get; set; }

    /// <summary>Network switch name.</summary>
    [StringLength(100, ErrorMessage = "Switch name cannot exceed 100 characters")]
    public string? SwitchName { get; set; }

    /// <summary>Template category/type.</summary>
    public VmTemplateType Category { get; set; } = VmTemplateType.Production;

    /// <summary>Whether the template is public (available to all users).</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>Owner/creator of the template.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last modified timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Template version.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Tags for categorization.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Supported backends for this template.</summary>
    public List<VmCreationMode> SupportedBackends { get; set; } = new() { VmCreationMode.WMI, VmCreationMode.HCS };
}

/// <summary>
/// Request model for creating a template from a VM.
/// </summary>
public class CreateTemplateFromVmRequest
{
    /// <summary>Source VM name to create template from.</summary>
    [Required(ErrorMessage = "Source VM name is required")]
    [StringLength(100, ErrorMessage = "Source VM name cannot exceed 100 characters")]
    public string SourceVmName { get; set; } = string.Empty;

    /// <summary>Template name.</summary>
    [Required(ErrorMessage = "Template name is required")]
    [StringLength(100, ErrorMessage = "Template name cannot exceed 100 characters")]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Template description.</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Template category.</summary>
    public VmTemplateType Category { get; set; } = VmTemplateType.Production;

    /// <summary>Whether the template is public.</summary>
    public bool IsPublic { get; set; } = true;

    /// <summary>Tags for the template.</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Custom memory setting (overrides source VM memory).</summary>
    [Range(256, 1048576, ErrorMessage = "Memory must be between 256 MB and 1048576 MB")]
    public int? CustomMemoryMB { get; set; }

    /// <summary>Custom CPU count (overrides source VM CPU count).</summary>
    [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128")]
    public int? CustomCpuCount { get; set; }

    /// <summary>Custom disk size (overrides source VM disk size).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Disk size must be at least 1 GB")]
    public int? CustomDiskSizeGB { get; set; }
}

/// <summary>
/// Request model for updating a template.
/// </summary>
public class UpdateTemplateRequest
{
    /// <summary>New template name.</summary>
    [StringLength(100, ErrorMessage = "Template name cannot exceed 100 characters")]
    public string? Name { get; set; }

    /// <summary>New description.</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>New category.</summary>
    public VmTemplateType? Category { get; set; }

    /// <summary>New public status.</summary>
    public bool? IsPublic { get; set; }

    /// <summary>New tags.</summary>
    public List<string>? Tags { get; set; }

    /// <summary>New memory setting.</summary>
    [Range(256, 1048576, ErrorMessage = "Memory must be between 256 MB and 1048576 MB")]
    public int? MemoryMB { get; set; }

    /// <summary>New CPU count.</summary>
    [Range(1, 128, ErrorMessage = "CPU count must be between 1 and 128")]
    public int? CpuCount { get; set; }

    /// <summary>New disk size.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Disk size must be at least 1 GB")]
    public int? DiskSizeGB { get; set; }

    /// <summary>New version.</summary>
    [StringLength(20, ErrorMessage = "Version cannot exceed 20 characters")]
    public string? Version { get; set; }
}

/// <summary>
/// Response model for template operations.
/// </summary>
public class TemplateOperationResponse
{
    /// <summary>Template ID.</summary>
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Template name.</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Operation performed.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Success status.</summary>
    public bool Success { get; set; }

    /// <summary>Additional details.</summary>
    public Dictionary<string, string> Details { get; set; } = new();

    /// <summary>Timestamp of operation.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}