using HyperV.Contracts.Models.Common;

namespace HyperV.Contracts.Interfaces.Providers;

public interface IGuestAgentProvider
{
    bool IsAvailable { get; }
    Task<GuestInfoDto?> GetGuestInfoAsync(string vmNameOrId);
    Task CopyFileToGuestAsync(string vmNameOrId, string sourcePath, string destPath);
}
