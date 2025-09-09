using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// Container creation mode - HCS or WMI.
/// </summary>
public enum ContainerCreationMode
{
    /// <summary>Host Compute System API (lightweight containers).</summary>
    HCS = 0,
    
    /// <summary>Windows Management Instrumentation API (Hyper-V containers).</summary>
    WMI = 1
}

/// <summary>
/// Request to create a new Hyper-V container.
/// </summary>
public sealed class CreateContainerRequest
{
    /// <summary>Unique container ID (UUID or friendly name).</summary>
    [Required, SwaggerSchema("Unique Container ID")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Container name visible in management tools.</summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>Memory limit in MB.</summary>
    [Range(128, 1048576)]
    public int MemoryMB { get; init; } = 1024;

    /// <summary>CPU limit (number of cores).</summary>
    [Range(1, 128)]
    public int CpuCount { get; init; } = 1;

    /// <summary>Storage size in GB (for persistent containers).</summary>
    [Range(1, int.MaxValue)]
    public int StorageSizeGB { get; set; } = 10;

    /// <summary>Container image or base OS.</summary>
    [SwaggerSchema("Container base image or OS")]
    public string Image { get; init; } = "mcr.microsoft.com/windows/servercore:ltsc2022";

    /// <summary>Container creation mode - HCS (lightweight) or WMI (Hyper-V isolated).</summary>
    [SwaggerSchema("Container creation mode: HCS (lightweight) or WMI (Hyper-V isolated)")]
    public ContainerCreationMode Mode { get; init; } = ContainerCreationMode.HCS;

    /// <summary>Environment variables for the container.</summary>
    public Dictionary<string, string> Environment { get; init; } = new();

    /// <summary>Port mappings for the container.</summary>
    public Dictionary<int, int> PortMappings { get; init; } = new();

    /// <summary>Volume mounts for the container.</summary>
    public Dictionary<string, string> VolumeMounts { get; init; } = new();
}
