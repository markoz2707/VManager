using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Usługi i diagnostyka agenta.</summary>
[ApiController]
[Route("api/v1/service")]
public sealed class ServiceController : ControllerBase
{
    public ServiceController() 
    {
    }

    /// <summary>Sprawdza status agenta HyperV.</summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Agent Health Check", Description = "Returns the health status of the HyperV agent.")]
    public IActionResult GetHealth()
    {
        return Ok(new 
        { 
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            services = new
            {
                hcs = "available",
                wmi = "available",
                hcn = "available"
            }
        });
    }

    /// <summary>Pobiera informacje o agencie.</summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Agent Information", Description = "Returns information about the HyperV agent.")]
    public IActionResult GetInfo()
    {
        return Ok(new 
        { 
            name = "HyperV Agent",
            version = "1.0.0",
            description = "HyperV management agent providing REST API for VM, container, network and storage operations",
            endpoints = new
            {
                vms = "/api/v1/vms",
                containers = "/api/v1/containers", 
                networks = "/api/v1/networks",
                storage = "/api/v1/storage"
            },
            capabilities = new[]
            {
                "VM management (HCS & WMI)",
                "Container management", 
                "Network management",
                "Storage management",
                "Snapshot operations",
                "Replication services"
            }
        });
    }
}
