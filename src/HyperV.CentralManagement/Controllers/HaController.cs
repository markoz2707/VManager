using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/ha")]
[Authorize]
public class HaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _audit;

    public HaController(AppDbContext db, AuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("config/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetConfiguration(Guid clusterId)
    {
        var config = await _db.HaConfigurations
            .Include(h => h.VmOverrides)
            .FirstOrDefaultAsync(h => h.ClusterId == clusterId);

        if (config == null)
            return Ok(new { configured = false, clusterId });

        return Ok(config);
    }

    [HttpPost("config/{clusterId:guid}")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> CreateOrUpdateConfiguration(Guid clusterId, [FromBody] HaConfigDto dto)
    {
        var existing = await _db.HaConfigurations.FirstOrDefaultAsync(h => h.ClusterId == clusterId);

        if (existing != null)
        {
            existing.IsEnabled = dto.IsEnabled;
            existing.HeartbeatIntervalSeconds = dto.HeartbeatIntervalSeconds;
            existing.FailureThreshold = dto.FailureThreshold;
            existing.AdmissionControl = dto.AdmissionControl;
            existing.ReservedCpuPercent = dto.ReservedCpuPercent;
            existing.ReservedMemoryPercent = dto.ReservedMemoryPercent;
            existing.UpdatedUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.HaConfigurations.Add(new HaConfiguration
            {
                ClusterId = clusterId,
                IsEnabled = dto.IsEnabled,
                HeartbeatIntervalSeconds = dto.HeartbeatIntervalSeconds,
                FailureThreshold = dto.FailureThreshold,
                AdmissionControl = dto.AdmissionControl,
                ReservedCpuPercent = dto.ReservedCpuPercent,
                ReservedMemoryPercent = dto.ReservedMemoryPercent
            });
        }

        await _db.SaveChangesAsync();
        await _audit.WriteAsync(User.Identity?.Name, "ha_config_updated", clusterId.ToString());
        return Ok();
    }

    [HttpPost("config/{clusterId:guid}/enable")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> EnableHa(Guid clusterId)
    {
        var config = await _db.HaConfigurations.FirstOrDefaultAsync(h => h.ClusterId == clusterId);
        if (config == null) return NotFound();

        config.IsEnabled = true;
        config.UpdatedUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("config/{clusterId:guid}/disable")]
    [RequirePermission("cluster", "update")]
    public async Task<IActionResult> DisableHa(Guid clusterId)
    {
        var config = await _db.HaConfigurations.FirstOrDefaultAsync(h => h.ClusterId == clusterId);
        if (config == null) return NotFound();

        config.IsEnabled = false;
        config.UpdatedUtc = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("events/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetEvents(Guid clusterId, [FromQuery] int limit = 50)
    {
        var events = await _db.HaEvents
            .Where(e => e.ClusterId == clusterId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync();

        return Ok(events);
    }

    [HttpPut("vm-override")]
    [RequirePermission("vm", "update")]
    public async Task<IActionResult> SetVmOverride([FromBody] HaVmOverrideDto dto)
    {
        var existing = await _db.HaVmOverrides
            .FirstOrDefaultAsync(o => o.HaConfigurationId == dto.HaConfigurationId && o.VmInventoryId == dto.VmInventoryId);

        if (existing != null)
        {
            existing.RestartPriority = dto.RestartPriority;
            existing.RestartOrder = dto.RestartOrder;
        }
        else
        {
            _db.HaVmOverrides.Add(new HaVmOverride
            {
                HaConfigurationId = dto.HaConfigurationId,
                VmInventoryId = dto.VmInventoryId,
                RestartPriority = dto.RestartPriority,
                RestartOrder = dto.RestartOrder
            });
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("status/{clusterId:guid}")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetStatus(Guid clusterId)
    {
        var config = await _db.HaConfigurations.FirstOrDefaultAsync(h => h.ClusterId == clusterId);
        var nodes = await _db.ClusterNodes
            .Where(cn => cn.ClusterId == clusterId)
            .Include(cn => cn.AgentHost)
            .ToListAsync();

        var recentEvents = await _db.HaEvents
            .Where(e => e.ClusterId == clusterId)
            .OrderByDescending(e => e.TimestampUtc)
            .Take(10)
            .ToListAsync();

        return Ok(new
        {
            isEnabled = config?.IsEnabled ?? false,
            totalHosts = nodes.Count,
            onlineHosts = nodes.Count(n => n.AgentHost?.Status == AgentStatus.Online),
            offlineHosts = nodes.Count(n => n.AgentHost?.Status == AgentStatus.Offline),
            recentEvents
        });
    }
}

public class HaConfigDto
{
    public bool IsEnabled { get; set; } = true;
    public int HeartbeatIntervalSeconds { get; set; } = 10;
    public int FailureThreshold { get; set; } = 3;
    public bool AdmissionControl { get; set; } = true;
    public int ReservedCpuPercent { get; set; } = 25;
    public int ReservedMemoryPercent { get; set; } = 25;
}

public class HaVmOverrideDto
{
    public Guid HaConfigurationId { get; set; }
    public Guid VmInventoryId { get; set; }
    public HaRestartPriority RestartPriority { get; set; }
    public int RestartOrder { get; set; } = 100;
}
