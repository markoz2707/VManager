using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.Linq;

namespace HyperV.Agent.Controllers;

/// <summary>Container management API controller.</summary>
[ApiController]
[Route("api/v1/[controller]")]
public class ContainersController : ControllerBase
{
    private readonly IContainerProvider _containerProvider;
    private readonly ILogger<ContainersController> _logger;

    public ContainersController(IContainerProvider containerProvider, ILogger<ContainersController> logger)
    {
        _containerProvider = containerProvider;
        _logger = logger;
    }

    /// <summary>Creates a new container.</summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create container", Description = "Creates a new container using the current hypervisor's container runtime.")]
    public async Task<IActionResult> CreateContainer([FromBody] CreateContainerSpec spec)
    {
        try
        {
            if (string.IsNullOrEmpty(spec.Name))
                return BadRequest(new { error = "Container name is required" });

            var result = await _containerProvider.CreateContainerAsync(spec);
            return Ok(new { status = "created", id = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating container");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Lists all containers.</summary>
    [HttpGet]
    [SwaggerOperation(Summary = "List containers", Description = "Lists all containers from the current hypervisor.")]
    public async Task<IActionResult> ListContainers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var containers = await _containerProvider.ListContainersAsync();
            var totalCount = containers.Count;
            var items = containers.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new PaginatedResult<ContainerSummaryDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                HasMore = (page * pageSize) < totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing containers");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets container details by ID.</summary>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Get container", Description = "Gets details of a specific container.")]
    public async Task<IActionResult> GetContainer(string id)
    {
        try
        {
            var container = await _containerProvider.GetContainerAsync(id);
            if (container == null) return NotFound(new { error = $"Container '{id}' not found" });
            return Ok(container);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Starts a container.</summary>
    [HttpPost("{id}/start")]
    [SwaggerOperation(Summary = "Start container")]
    public async Task<IActionResult> StartContainer(string id)
    {
        try
        {
            await _containerProvider.StartContainerAsync(id);
            return Ok(new { status = "started", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Stops a container.</summary>
    [HttpPost("{id}/stop")]
    [SwaggerOperation(Summary = "Stop container")]
    public async Task<IActionResult> StopContainer(string id)
    {
        try
        {
            await _containerProvider.StopContainerAsync(id);
            return Ok(new { status = "stopped", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Terminates a container forcefully.</summary>
    [HttpPost("{id}/terminate")]
    [SwaggerOperation(Summary = "Terminate container")]
    public async Task<IActionResult> TerminateContainer(string id)
    {
        try
        {
            await _containerProvider.TerminateContainerAsync(id);
            return Ok(new { status = "terminated", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Pauses a container.</summary>
    [HttpPost("{id}/pause")]
    [SwaggerOperation(Summary = "Pause container")]
    public async Task<IActionResult> PauseContainer(string id)
    {
        try
        {
            await _containerProvider.PauseContainerAsync(id);
            return Ok(new { status = "paused", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Resumes a paused container.</summary>
    [HttpPost("{id}/resume")]
    [SwaggerOperation(Summary = "Resume container")]
    public async Task<IActionResult> ResumeContainer(string id)
    {
        try
        {
            await _containerProvider.ResumeContainerAsync(id);
            return Ok(new { status = "resumed", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Deletes a container.</summary>
    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Delete container")]
    public async Task<IActionResult> DeleteContainer(string id)
    {
        try
        {
            await _containerProvider.DeleteContainerAsync(id);
            return Ok(new { status = "deleted", id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting container {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
