using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using HyperV.Core.Wmi.Services;
using Microsoft.Extensions.Logging;

namespace VManager.Provider.HyperV;

public class HyperVGuestAgentProvider : IGuestAgentProvider
{
    private readonly global::HyperV.Core.Wmi.Services.VmService _wmiVmService;
    private readonly ILogger<HyperVGuestAgentProvider> _logger;

    public bool IsAvailable => true; // Integration Services available on Hyper-V

    public HyperVGuestAgentProvider(
        global::HyperV.Core.Wmi.Services.VmService wmiVmService,
        ILogger<HyperVGuestAgentProvider> logger)
    {
        _wmiVmService = wmiVmService;
        _logger = logger;
    }

    public Task<GuestInfoDto?> GetGuestInfoAsync(string vmNameOrId)
    {
        try
        {
            var healthResult = _wmiVmService.GetAppHealth(vmNameOrId);
            return Task.FromResult<GuestInfoDto?>(new GuestInfoDto
            {
                IsAgentAvailable = true,
                ExtendedProperties = new Dictionary<string, object>
                {
                    ["integrationServices"] = "Available",
                    ["applicationHealth"] = healthResult?.ToString() ?? ""
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get guest info for {Vm}", vmNameOrId);
            return Task.FromResult<GuestInfoDto?>(new GuestInfoDto { IsAgentAvailable = false });
        }
    }

    public Task CopyFileToGuestAsync(string vmNameOrId, string sourcePath, string destPath)
    {
        _wmiVmService.CopyFileToGuest(vmNameOrId, sourcePath, destPath, false);
        return Task.CompletedTask;
    }
}
