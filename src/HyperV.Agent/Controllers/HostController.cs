using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.Text;
using System.Text.Json;

namespace HyperV.Agent.Controllers;

/// <summary>Host information, metrics, logs, and power management.</summary>
[ApiController]
[Route("api/v1/host")]
public class HostController : ControllerBase
{
    private readonly IHostProvider _hostProvider;
    private readonly IMetricsProvider _metricsProvider;
    private readonly IEventLogProvider _eventLogProvider;
    private readonly ILogger<HostController> _logger;

    public HostController(
        IHostProvider hostProvider,
        IMetricsProvider metricsProvider,
        IEventLogProvider eventLogProvider,
        ILogger<HostController> logger)
    {
        _hostProvider = hostProvider;
        _metricsProvider = metricsProvider;
        _eventLogProvider = eventLogProvider;
        _logger = logger;
    }

    /// <summary>Get host hardware information.</summary>
    [HttpGet("hardware")]
    [ProducesResponseType(typeof(HostInfoDto), 200)]
    [SwaggerOperation(Summary = "Get hardware info", Description = "Returns host hardware information including CPU, memory, and hypervisor details.")]
    public async Task<IActionResult> GetHardwareInfo()
    {
        try
        {
            var info = await _hostProvider.GetHostInfoAsync();
            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting hardware info");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get host system information.</summary>
    [HttpGet("system")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Get system info", Description = "Returns operating system and hypervisor information.")]
    public async Task<IActionResult> GetSystemInfo()
    {
        try
        {
            var info = await _hostProvider.GetHostInfoAsync();
            return Ok(new
            {
                hostname = info.Hostname,
                hypervisorType = info.HypervisorType,
                hypervisorVersion = info.HypervisorVersion,
                osName = info.OperatingSystem,
                osVersion = info.OsVersion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system info");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get host performance summary.</summary>
    [HttpGet("performance")]
    [ProducesResponseType(typeof(HostPerformanceMetrics), 200)]
    [SwaggerOperation(Summary = "Get performance summary", Description = "Returns CPU, memory, and storage usage percentages.")]
    public async Task<IActionResult> GetPerformanceSummary()
    {
        try
        {
            var metrics = await _hostProvider.GetPerformanceMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance summary");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get combined host details.</summary>
    [HttpGet("details")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Get host details", Description = "Returns combined hardware, system, and performance information.")]
    public async Task<IActionResult> GetHostDetails()
    {
        try
        {
            var info = await _hostProvider.GetHostInfoAsync();
            var perf = await _hostProvider.GetPerformanceMetricsAsync();
            return Ok(new { hardware = info, performance = perf });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting host details");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get host stats (alias for performance).</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(HostPerformanceMetrics), 200)]
    [SwaggerOperation(Summary = "Get host stats", Description = "Alias for performance summary.")]
    public async Task<IActionResult> GetHostStats()
    {
        return await GetPerformanceSummary();
    }

    /// <summary>Get detailed host usage metrics.</summary>
    [HttpGet("metrics/usage")]
    [ProducesResponseType(typeof(HostUsageDto), 200)]
    [SwaggerOperation(Summary = "Get host usage metrics", Description = "Returns detailed host CPU, memory usage metrics.")]
    public async Task<IActionResult> GetHostUsageMetrics()
    {
        try
        {
            var usage = await _metricsProvider.GetHostUsageAsync();
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting host usage metrics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get hypervisor capabilities.</summary>
    [HttpGet("capabilities")]
    [ProducesResponseType(typeof(HypervisorCapabilities), 200)]
    [SwaggerOperation(Summary = "Get capabilities", Description = "Returns hypervisor capabilities and supported features.")]
    public async Task<IActionResult> GetCapabilities()
    {
        try
        {
            var capabilities = await _hostProvider.GetCapabilitiesAsync();
            return Ok(capabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting capabilities");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #region Event Logs

    /// <summary>Get system event logs with filtering.</summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(LogsResponse), 200)]
    [SwaggerOperation(Summary = "Get system logs", Description = "Returns filtered event logs from the host system.")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? source = null,
        [FromQuery] string? level = null,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int limit = 100,
        [FromQuery] string? search = null)
    {
        try
        {
            if (limit < 1) limit = 1;
            if (limit > 1000) limit = 1000;

            var logs = await _eventLogProvider.GetLogsAsync(source, level, start, end, limit, search);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting event logs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get available log sources.</summary>
    [HttpGet("logs/sources")]
    [ProducesResponseType(typeof(List<string>), 200)]
    [SwaggerOperation(Summary = "Get log sources", Description = "Returns available log source names.")]
    public async Task<IActionResult> GetLogSources()
    {
        try
        {
            var sources = await _eventLogProvider.GetLogSourcesAsync();
            return Ok(sources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting log sources");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Export logs as JSON or CSV.</summary>
    [HttpGet("logs/export")]
    [SwaggerOperation(Summary = "Export logs", Description = "Exports filtered logs as JSON or CSV file.")]
    public async Task<IActionResult> ExportLogs(
        [FromQuery] string? source = null,
        [FromQuery] string? level = null,
        [FromQuery] DateTime? start = null,
        [FromQuery] DateTime? end = null,
        [FromQuery] int limit = 1000,
        [FromQuery] string? search = null,
        [FromQuery] string format = "json")
    {
        try
        {
            if (limit < 1) limit = 1;
            if (limit > 10000) limit = 10000;

            var logs = await _eventLogProvider.GetLogsAsync(source, level, start, end, limit, search);

            if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Level,Source,Message,EventId,Category");
                foreach (var entry in logs.Entries)
                {
                    var msg = entry.Message.Replace("\"", "\"\"");
                    sb.AppendLine($"\"{entry.Timestamp:O}\",\"{entry.Level}\",\"{entry.Source}\",\"{msg}\",\"{entry.EventId}\",\"{entry.Category}\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "logs.csv");
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(logs, new JsonSerializerOptions { WriteIndented = true });
            return File(json, "application/json", "logs.json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting logs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Host Power Management

    /// <summary>Shutdown the host.</summary>
    [HttpPost("shutdown")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Shutdown host", Description = "Shuts down the host machine. Requires confirm=true.")]
    public async Task<IActionResult> ShutdownHost([FromQuery] bool confirm = false, [FromQuery] bool force = false)
    {
        if (!confirm)
            return BadRequest(new { error = "Shutdown requires confirm=true parameter" });

        try
        {
            _logger.LogWarning("Host shutdown initiated by user (force={Force})", force);
            await _hostProvider.ShutdownHostAsync(force);
            return Ok(new { status = "shutdown_initiated", message = "Host shutdown has been initiated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error shutting down host");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Reboot the host.</summary>
    [HttpPost("reboot")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Reboot host", Description = "Reboots the host machine. Requires confirm=true.")]
    public async Task<IActionResult> RebootHost([FromQuery] bool confirm = false, [FromQuery] bool force = false)
    {
        if (!confirm)
            return BadRequest(new { error = "Reboot requires confirm=true parameter" });

        try
        {
            _logger.LogWarning("Host reboot initiated by user (force={Force})", force);
            await _hostProvider.RebootHostAsync(force);
            return Ok(new { status = "reboot_initiated", message = "Host reboot has been initiated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebooting host");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion
}
