using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// Request model for creating VM from template.
/// </summary>
public class CreateVmFromTemplateRequest
{
    /// <summary>Unikalny identyfikator VM (UUID lub friendly name).</summary>
    [Required, SwaggerSchema("Unique VM ID")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Nazwa VM widoczna w menedżerze Hyper-V.</summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>Template type to use for VM creation.</summary>
    [Required]
    public VmTemplateType Template { get; init; }

    /// <summary>Optional custom memory in MB (overrides template default).</summary>
    [Range(256, 1048576)]
    public int? MemoryMB { get; init; }

    /// <summary>Optional custom CPU count (overrides template default).</summary>
    [Range(1, 128)]
    public int? CpuCount { get; init; }

    /// <summary>Optional custom disk size in GB (overrides template default).</summary>
    [Range(1, int.MaxValue)]
    public int? DiskSizeGB { get; init; }

    /// <summary>Optional custom generation (overrides template default).</summary>
    [Range(1, 2)]
    public int? Generation { get; init; }

    /// <summary>Optional custom secure boot setting (overrides template default).</summary>
    public bool? SecureBoot { get; init; }

    /// <summary>Ścieżka do pliku VHD/VHDX. Jeśli nie podano, zostanie utworzona automatycznie.</summary>
    public string? VhdPath { get; init; }

    /// <summary>Nazwa przełącznika sieciowego do podłączenia VM.</summary>
    public string? SwitchName { get; init; }

    /// <summary>Notatki/opis VM.</summary>
    public string? Notes { get; init; }

    /// <summary>Preferred backend (auto-selected if not specified).</summary>
    public VmCreationMode? PreferredBackend { get; init; }
}