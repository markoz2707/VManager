using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/metrics")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MetricsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get host metrics time series
    /// </summary>
    [HttpGet("host/{agentId:guid}")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetHostMetrics(
        Guid agentId,
        [FromQuery] string metricName = "host_cpu_usage",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int resolution = 60)
    {
        var fromTime = from ?? DateTimeOffset.UtcNow.AddHours(-1);
        var toTime = to ?? DateTimeOffset.UtcNow;

        var dataPoints = await _db.MetricDataPoints
            .Where(m => m.AgentHostId == agentId &&
                        m.MetricName == metricName &&
                        m.VmInventoryId == null &&
                        m.TimestampUtc >= fromTime &&
                        m.TimestampUtc <= toTime)
            .OrderBy(m => m.TimestampUtc)
            .Select(m => new { m.Value, m.TimestampUtc })
            .AsNoTracking()
            .ToListAsync();

        return Ok(new { agentId, metricName, from = fromTime, to = toTime, dataPoints });
    }

    /// <summary>
    /// Get VM metrics time series
    /// </summary>
    [HttpGet("vm/{vmId:guid}")]
    [RequirePermission("vm", "read")]
    public async Task<IActionResult> GetVmMetrics(
        Guid vmId,
        [FromQuery] string metricName = "vm_cpu_usage",
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null)
    {
        var fromTime = from ?? DateTimeOffset.UtcNow.AddHours(-1);
        var toTime = to ?? DateTimeOffset.UtcNow;

        var dataPoints = await _db.MetricDataPoints
            .Where(m => m.VmInventoryId == vmId &&
                        m.MetricName == metricName &&
                        m.TimestampUtc >= fromTime &&
                        m.TimestampUtc <= toTime)
            .OrderBy(m => m.TimestampUtc)
            .Select(m => new { m.Value, m.TimestampUtc })
            .AsNoTracking()
            .ToListAsync();

        return Ok(new { vmId, metricName, from = fromTime, to = toTime, dataPoints });
    }

    /// <summary>
    /// Get cluster summary metrics (latest values for all hosts)
    /// </summary>
    [HttpGet("cluster/{clusterId:guid}/summary")]
    [RequirePermission("cluster", "read")]
    public async Task<IActionResult> GetClusterSummary(Guid clusterId)
    {
        var agentIds = await _db.ClusterNodes
            .Where(cn => cn.ClusterId == clusterId)
            .Select(cn => cn.AgentHostId)
            .ToListAsync();

        var latestMetrics = new List<object>();

        foreach (var agentId in agentIds)
        {
            var agent = await _db.AgentHosts.FindAsync(agentId);
            if (agent == null) continue;

            var cpuMetric = await _db.MetricDataPoints
                .Where(m => m.AgentHostId == agentId && m.MetricName == "host_cpu_usage" && m.VmInventoryId == null)
                .OrderByDescending(m => m.TimestampUtc)
                .FirstOrDefaultAsync();

            var memMetric = await _db.MetricDataPoints
                .Where(m => m.AgentHostId == agentId && m.MetricName == "host_memory_usage" && m.VmInventoryId == null)
                .OrderByDescending(m => m.TimestampUtc)
                .FirstOrDefaultAsync();

            var vmCount = await _db.VmInventory.CountAsync(v => v.AgentHostId == agentId);

            latestMetrics.Add(new
            {
                agentId,
                hostname = agent.Hostname,
                status = agent.Status.ToString(),
                cpuUsagePercent = cpuMetric?.Value ?? 0,
                memoryUsagePercent = memMetric?.Value ?? 0,
                vmCount,
                lastUpdated = cpuMetric?.TimestampUtc
            });
        }

        return Ok(new { clusterId, hosts = latestMetrics });
    }

    /// <summary>
    /// Get dashboard data (overview of all agents)
    /// </summary>
    [HttpGet("dashboard")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetDashboardData()
    {
        var agents = await _db.AgentHosts.AsNoTracking().ToListAsync();
        var totalVms = await _db.VmInventory.CountAsync();
        var runningVms = await _db.VmInventory.CountAsync(v => v.State == "Running");
        var activeAlerts = await _db.AlertInstances.CountAsync(a => a.Status == Models.AlertInstanceStatus.Active);
        var onlineAgents = agents.Count(a => a.Status == Models.AgentStatus.Online);

        return Ok(new
        {
            totalAgents = agents.Count,
            onlineAgents,
            totalVms,
            runningVms,
            activeAlerts,
            agents = agents.Select(a => new
            {
                a.Id,
                a.Hostname,
                status = a.Status.ToString(),
                a.HostType,
                a.LastSeenUtc
            })
        });
    }
}
