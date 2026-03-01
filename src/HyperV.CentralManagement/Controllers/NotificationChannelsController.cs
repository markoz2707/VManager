using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/notification-channels")]
[Authorize]
public class NotificationChannelsController : ControllerBase
{
    private readonly AlertService _alertService;
    private readonly NotificationService _notificationService;
    private readonly AuditLogService _audit;

    public NotificationChannelsController(
        AlertService alertService,
        NotificationService notificationService,
        AuditLogService audit)
    {
        _alertService = alertService;
        _notificationService = notificationService;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetChannels()
    {
        var channels = await _alertService.GetChannelsAsync();
        return Ok(channels);
    }

    [HttpPost]
    [RequirePermission("host", "create")]
    public async Task<IActionResult> CreateChannel([FromBody] CreateChannelDto dto)
    {
        var channel = new NotificationChannel
        {
            Name = dto.Name,
            Type = dto.Type,
            Configuration = dto.Configuration,
            IsEnabled = dto.IsEnabled
        };

        var result = await _alertService.CreateChannelAsync(channel);
        await _audit.WriteAsync(User.Identity?.Name, "notification_channel_created", channel.Name);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> UpdateChannel(Guid id, [FromBody] CreateChannelDto dto)
    {
        var updated = new NotificationChannel
        {
            Name = dto.Name,
            Type = dto.Type,
            Configuration = dto.Configuration,
            IsEnabled = dto.IsEnabled
        };

        if (!await _alertService.UpdateChannelAsync(id, updated))
            return NotFound();

        await _audit.WriteAsync(User.Identity?.Name, "notification_channel_updated", dto.Name);
        return Ok();
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("host", "delete")]
    public async Task<IActionResult> DeleteChannel(Guid id)
    {
        if (!await _alertService.DeleteChannelAsync(id))
            return NotFound();

        await _audit.WriteAsync(User.Identity?.Name, "notification_channel_deleted", id.ToString());
        return Ok();
    }

    [HttpPost("{id:guid}/test")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> TestChannel(Guid id)
    {
        var channels = await _alertService.GetChannelsAsync();
        var channel = channels.FirstOrDefault(c => c.Id == id);
        if (channel == null) return NotFound();

        await _notificationService.SendAsync(channel, "[VManager] Test Notification", "This is a test notification from VManager.");
        return Ok(new { success = true, message = "Test notification sent." });
    }
}

public class CreateChannelDto
{
    public string Name { get; set; } = string.Empty;
    public NotificationChannelType Type { get; set; }
    public string Configuration { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
}
