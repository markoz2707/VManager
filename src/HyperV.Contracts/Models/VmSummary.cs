namespace HyperV.Contracts.Models;

/// <summary>Podsumowanie stanu VM.</summary>
public sealed class VmSummary
{
    /// <summary>ID VM.</summary>
    public required string Id { get; init; }
    /// <summary>Nazwa VM.</summary>
    public required string Name { get; init; }
    /// <summary>Bieżący stan.</summary>
    public string State { get; init; } = "Unknown";
}