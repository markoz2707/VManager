using System.Text.Json;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VManager.Libvirt.Connection;
using VManager.Libvirt.Native;

namespace VManager.Provider.KVM;

public class KvmGuestAgentProvider : IGuestAgentProvider
{
    private readonly KvmOptions _options;
    private readonly ILogger<KvmGuestAgentProvider> _logger;

    public bool IsAvailable => true; // QEMU Guest Agent may or may not be installed

    public KvmGuestAgentProvider(IOptions<KvmOptions> options, ILogger<KvmGuestAgentProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<GuestInfoDto?> GetGuestInfoAsync(string vmNameOrId)
    {
        try
        {
            using var conn = new LibvirtConnection(_options.LibvirtUri);
            var domain = conn.LookupDomainByName(vmNameOrId);
            if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
            if (domain == IntPtr.Zero)
                return Task.FromResult<GuestInfoDto?>(null);

            try
            {
                // Try QEMU Guest Agent command
                var result = LibvirtNative.virDomainQemuAgentCommand(domain, "{\"execute\":\"guest-info\"}", -1, 0);
                if (result != IntPtr.Zero)
                {
                    var json = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(result);
                    LibvirtNative.virFree(result);

                    return Task.FromResult<GuestInfoDto?>(new GuestInfoDto
                    {
                        IsAgentAvailable = true,
                        ExtendedProperties = new Dictionary<string, object>
                        {
                            ["guestAgentResponse"] = json ?? ""
                        }
                    });
                }
            }
            catch
            {
                // Guest agent not available
            }
            finally
            {
                LibvirtNative.virDomainFree(domain);
            }

            return Task.FromResult<GuestInfoDto?>(new GuestInfoDto { IsAgentAvailable = false });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get guest info for {Vm}", vmNameOrId);
            return Task.FromResult<GuestInfoDto?>(new GuestInfoDto { IsAgentAvailable = false });
        }
    }

    public Task CopyFileToGuestAsync(string vmNameOrId, string sourcePath, string destPath)
    {
        try
        {
            using var conn = new LibvirtConnection(_options.LibvirtUri);
            var domain = conn.LookupDomainByName(vmNameOrId);
            if (domain == IntPtr.Zero) domain = conn.LookupDomainByUuid(vmNameOrId);
            if (domain == IntPtr.Zero)
                throw new InvalidOperationException($"VM '{vmNameOrId}' not found");

            try
            {
                var fileContent = System.IO.File.ReadAllBytes(sourcePath);
                var base64Content = Convert.ToBase64String(fileContent);

                // Open file in guest
                var openCmd = JsonSerializer.Serialize(new
                {
                    execute = "guest-file-open",
                    arguments = new { path = destPath, mode = "w" }
                });
                var openResult = LibvirtNative.virDomainQemuAgentCommand(domain, openCmd, -1, 0);
                if (openResult == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to open file in guest");

                var openJson = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(openResult);
                LibvirtNative.virFree(openResult);

                var openDoc = JsonDocument.Parse(openJson!);
                var handle = openDoc.RootElement.GetProperty("return").GetInt64();

                // Write file content
                var writeCmd = JsonSerializer.Serialize(new
                {
                    execute = "guest-file-write",
                    arguments = new { handle, @buf_b64 = base64Content }
                });
                var writeResult = LibvirtNative.virDomainQemuAgentCommand(domain, writeCmd, -1, 0);
                if (writeResult != IntPtr.Zero) LibvirtNative.virFree(writeResult);

                // Close file
                var closeCmd = JsonSerializer.Serialize(new
                {
                    execute = "guest-file-close",
                    arguments = new { handle }
                });
                var closeResult = LibvirtNative.virDomainQemuAgentCommand(domain, closeCmd, -1, 0);
                if (closeResult != IntPtr.Zero) LibvirtNative.virFree(closeResult);
            }
            finally
            {
                LibvirtNative.virDomainFree(domain);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file to guest {Vm}", vmNameOrId);
            throw;
        }
    }
}
