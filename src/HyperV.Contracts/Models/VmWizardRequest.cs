using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// Request model for VM creation wizard.
/// </summary>
public class VmWizardRequest
{
    /// <summary>Unikalny identyfikator VM (UUID lub friendly name).</summary>
    [Required, SwaggerSchema("Unique VM ID")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Nazwa VM widoczna w menedżerze Hyper-V.</summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>Workload type to determine optimal defaults.</summary>
    [Required]
    public VmWorkloadType WorkloadType { get; init; }

    /// <summary>Resource level preference.</summary>
    public VmResourceLevel ResourceLevel { get; init; } = VmResourceLevel.Medium;

    /// <summary>Preferred backend (auto-selected if not specified).</summary>
    public VmCreationMode? PreferredBackend { get; init; }

    /// <summary>Ścieżka do pliku VHD/VHDX. Jeśli nie podano, zostanie utworzona automatycznie.</summary>
    public string? VhdPath { get; init; }

    /// <summary>Nazwa przełącznika sieciowego do podłączenia VM.</summary>
    public string? SwitchName { get; init; }

    /// <summary>Notatki/opis VM.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// VM workload types for wizard.
/// </summary>
public enum VmWorkloadType
{
    /// <summary>General purpose computing.</summary>
    General = 0,

    /// <summary>Web server.</summary>
    WebServer = 1,

    /// <summary>Database server.</summary>
    Database = 2,

    /// <summary>Development environment.</summary>
    Development = 3,

    /// <summary>Testing/CI.</summary>
    Testing = 4,

    /// <summary>Container host.</summary>
    Container = 5
}

/// <summary>
/// Resource level preferences.
/// </summary>
public enum VmResourceLevel
{
    /// <summary>Minimal resources.</summary>
    Minimal = 0,

    /// <summary>Low resources.</summary>
    Low = 1,

    /// <summary>Medium resources (default).</summary>
    Medium = 2,

    /// <summary>High resources.</summary>
    High = 3,

    /// <summary>Maximum resources.</summary>
    Maximum = 4
}

/// <summary>
/// Wizard response with recommended configuration.
/// </summary>
public class VmWizardResponse
{
    /// <summary>Recommended VM configuration.</summary>
    public CreateVmRequest RecommendedConfiguration { get; set; } = new();

    /// <summary>Template used as basis.</summary>
    public VmTemplateType TemplateUsed { get; set; }

    /// <summary>Backend selected.</summary>
    public VmCreationMode BackendSelected { get; set; }

    /// <summary>Validation messages.</summary>
    public List<string> ValidationMessages { get; set; } = new();

    /// <summary>Resource recommendations.</summary>
    public Dictionary<string, string> Recommendations { get; set; } = new();
}