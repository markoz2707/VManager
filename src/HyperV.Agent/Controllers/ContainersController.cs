using Microsoft.AspNetCore.Mvc;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using System.Text.Json;

namespace HyperV.Agent.Controllers;

/// <summary>Container management API controller.</summary>
[ApiController]
[Route("api/v1/[controller]")]
public class ContainersController : ControllerBase
{
    private readonly Core.Hcs.Services.ContainerService _hcsContainerSvc;
    private readonly Core.Wmi.Services.ContainerService _wmiContainerSvc;

    public ContainersController(
        Core.Hcs.Services.ContainerService hcsContainerSvc,
        Core.Wmi.Services.ContainerService wmiContainerSvc)
    {
        _hcsContainerSvc = hcsContainerSvc;
        _wmiContainerSvc = wmiContainerSvc;
    }

    /// <summary>Creates a new container using specified backend (HCS or WMI).</summary>
    [HttpPost]
    public IActionResult CreateContainer([FromBody] CreateContainerRequest req)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string resultJson;

            switch (req.Mode)
            {
                case ContainerCreationMode.HCS:
                    // Use HCS service for lightweight containers
                    resultJson = _hcsContainerSvc.Create(req.Id, req);
                    break;

                case ContainerCreationMode.WMI:
                    // Use WMI service for Hyper-V isolated containers
                    resultJson = _wmiContainerSvc.Create(req.Id, req);
                    break;

                default:
                    return BadRequest(new { error = $"Unsupported container creation mode: {req.Mode}" });
            }

            var result = JsonSerializer.Deserialize<JsonElement>(resultJson);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Lists all containers from both HCS and WMI backends.</summary>
    [HttpGet]
    public IActionResult ListContainers()
    {
        try
        {
            var hcsContainers = _hcsContainerSvc.ListContainers();
            var wmiContainers = _wmiContainerSvc.ListContainers();

            var obj = new
            {
                HcsContainers = hcsContainers,
                WmiContainers = wmiContainers,
                TotalCount = hcsContainers.Count + wmiContainers.Count,
                Message = "Combined container list from HCS and WMI backends"
            };
            return Ok(obj);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Gets container information by ID.</summary>
    [HttpGet("{id}")]
    public IActionResult GetContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                var properties = _hcsContainerSvc.GetContainerProperties(id);
                var result = JsonSerializer.Deserialize<JsonElement>(properties);
                return Ok(new { backend = "HCS", properties = result });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                var properties = _wmiContainerSvc.GetContainerProperties(id);
                var result = JsonSerializer.Deserialize<JsonElement>(properties);
                return Ok(new { backend = "WMI", properties = result });
            }

            return NotFound(new { error = $"Container {id} not found in any backend" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Starts a container by ID.</summary>
    [HttpPost("{id}/start")]
    public IActionResult StartContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.StartContainer(id);
                return Ok(new { message = $"HCS container {id} started successfully" });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.StartContainer(id);
                return Ok(new { message = $"WMI container {id} started successfully" });
            }

            return NotFound(new { error = $"Container {id} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Stops a container by ID.</summary>
    [HttpPost("{id}/stop")]
    public IActionResult StopContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.StopContainer(id);
                return Ok(new { message = $"HCS container {id} stopped successfully" });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.StopContainer(id);
                return Ok(new { message = $"WMI container {id} stopped successfully" });
            }

            return NotFound(new { error = $"Container {id} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Terminates a container by ID.</summary>
    [HttpPost("{id}/terminate")]
    public IActionResult TerminateContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.TerminateContainer(id);
                return Ok(new { message = $"HCS container {id} terminated successfully" });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.TerminateContainer(id);
                return Ok(new { message = $"WMI container {id} terminated successfully" });
            }

            return NotFound(new { error = $"Container {id} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Pauses a container by ID.</summary>
    [HttpPost("{id}/pause")]
    public IActionResult PauseContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.PauseContainer(id);
                return Ok(new { message = $"HCS container {id} paused successfully" });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.PauseContainer(id);
                return Ok(new { message = $"WMI container {id} paused successfully" });
            }

            return NotFound(new { error = $"Container {id} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Resumes a container by ID.</summary>
    [HttpPost("{id}/resume")]
    public IActionResult ResumeContainer(string id)
    {
        try
        {
            // Try HCS first
            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.ResumeContainer(id);
                return Ok(new { message = $"HCS container {id} resumed successfully" });
            }

            // Try WMI
            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.ResumeContainer(id);
                return Ok(new { message = $"WMI container {id} resumed successfully" });
            }

            return NotFound(new { error = $"Container {id} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Deletes a container by ID.</summary>
    [HttpDelete("{id}")]
    public IActionResult DeleteContainer(string id)
    {
        try
        {
            // Try to terminate and clean up the container
            bool found = false;

            if (_hcsContainerSvc.IsContainerPresent(id))
            {
                _hcsContainerSvc.TerminateContainer(id);
                found = true;
            }

            if (_wmiContainerSvc.IsContainerPresent(id))
            {
                _wmiContainerSvc.TerminateContainer(id);
                found = true;
            }

            if (!found)
            {
                return NotFound(new { error = $"Container {id} not found" });
            }

            return Ok(new { message = $"Container {id} deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
