using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Host information and metrics.</summary>
[ApiController]
[Route("api/v1/host")]
public class HostController : ControllerBase
{
    private readonly IHostProvider _hostProvider;
    private readonly IMetricsProvider _metricsProvider;
    private readonly ILogger<HostController> _logger;

    public HostController(IHostProvider hostProvider, IMetricsProvider metricsProvider, ILogger<HostController> logger)
    {
        _hostProvider = hostProvider;
        _metricsProvider = metricsProvider;
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
}
