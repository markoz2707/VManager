using Microsoft.AspNetCore.Mvc;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers
{
    [ApiController]
    [Route("api/v1/host")]
    public class HostController : ControllerBase
    {
        private readonly IHostInfoService _hostService;

        public HostController(IHostInfoService hostService)
        {
            _hostService = hostService;
        }

        /// <summary>
        /// Gets host hardware information.
        /// </summary>
        [HttpGet("hardware")]
        public async Task<ActionResult<HostHardwareInfo>> GetHardware()
        {
            try
            {
                var hardware = await _hostService.GetHostHardwareInfoAsync();
                return Ok(hardware);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets system information (OS details).
        /// </summary>
        [HttpGet("system")]
        public async Task<ActionResult<SystemInfo>> GetSystem()
        {
            try
            {
                var system = await _hostService.GetSystemInfoAsync();
                return Ok(system);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets current performance summary.
        /// </summary>
        [HttpGet("performance")]
        public async Task<ActionResult<PerformanceSummary>> GetPerformance()
        {
            try
            {
                var performance = await _hostService.GetPerformanceSummaryAsync();
                return Ok(performance);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets recent tasks (last hour by default).
        /// </summary>
        /// <param name="limit">Number of tasks (default: 10).</param>
        [HttpGet("tasks")]
        public async Task<ActionResult<List<RecentTask>>> GetRecentTasks([FromQuery, Range(1, 100)] int limit = 10)
        {
            try
            {
                var tasks = await _hostService.GetRecentTasksAsync(limit);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets combined host details (hardware, system, performance).
        /// </summary>
        [HttpGet("details")]
        public async Task<ActionResult<HostDetails>> GetHostDetails()
        {
            try
            {
                var hardware = await _hostService.GetHostHardwareInfoAsync();
                var system = await _hostService.GetSystemInfoAsync();
                var performance = await _hostService.GetPerformanceSummaryAsync();

                var details = new HostDetails
                {
                    Hardware = hardware,
                    System = system,
                    Performance = performance
                };

                return Ok(details);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets performance stats (CPU, memory, storage).
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<PerformanceSummary>> GetHostStats()
        {
            try
            {
                var stats = await _hostService.GetPerformanceSummaryAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Combined host details model.
    /// </summary>
    public class HostDetails
    {
        public HostHardwareInfo Hardware { get; set; } = new();
        public SystemInfo System { get; set; } = new();
        public PerformanceSummary Performance { get; set; } = new();
    }
}