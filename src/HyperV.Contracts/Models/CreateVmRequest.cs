using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Contracts.Models;

/// <summary>
/// Tryb tworzenia VM - HCS lub WMI.
/// </summary>
public enum VmCreationMode
{
    /// <summary>Host Compute System API (kontener-like, nie widoczny w Hyper-V Manager).</summary>
    HCS = 0,
    
    /// <summary>Windows Management Instrumentation API (pełna VM widoczna w Hyper-V Manager).</summary>
    WMI = 1
}

/// <summary>
/// Żądanie utworzenia nowej maszyny wirtualnej Hyper-V.
/// </summary>
public sealed class CreateVmRequest
{
    /// <summary>Unikalny identyfikator VM (UUID lub friendly name).</summary>
    [Required, SwaggerSchema("Unique VM ID")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Nazwa VM widoczna w menedżerze Hyper-V.</summary>
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>Pamięć RAM w MB.</summary>
    [Range(256, 1048576)]
    public int MemoryMB { get; init; } = 2048;

    /// <summary>Liczba CPU vCore.</summary>
    [Range(1, 128)]
    public int CpuCount { get; init; } = 2;

    /// <summary>Rozmiar dysku w GB.</summary>
    [Range(1, int.MaxValue)]
    public int DiskSizeGB { get; set; } = 20;

    /// <summary>Tryb tworzenia VM - HCS (kontener-like) lub WMI (pełna VM).</summary>
    [SwaggerSchema("VM creation mode: HCS (container-like) or WMI (full VM visible in Hyper-V Manager)")]
    public VmCreationMode Mode { get; init; } = VmCreationMode.WMI;

    /// <summary>Generacja VM (1 lub 2). Domyślnie 2.</summary>
    [Range(1, 2)]
    public int Generation { get; init; } = 2;

    /// <summary>Czy włączyć Secure Boot (tylko dla Generation 2).</summary>
    public bool SecureBoot { get; init; } = true;

    /// <summary>Ścieżka do pliku VHD/VHDX. Jeśli nie podano, zostanie utworzona automatycznie.</summary>
    public string? VhdPath { get; init; }

    /// <summary>Nazwa przełącznika sieciowego do podłączenia VM.</summary>
    public string? SwitchName { get; init; }

    /// <summary>Notatki/opis VM.</summary>
    public string? Notes { get; init; }
}
