using HyperV.Contracts.Interfaces.Providers;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Agent service diagnostics.</summary>
[ApiController]
[Route("api/v1/service")]
public sealed class ServiceController : ControllerBase
{
    private readonly IHostProvider _hostProvider;

    public ServiceController(IHostProvider hostProvider)
    {
        _hostProvider = hostProvider;
    }

    /// <summary>Checks agent health status.</summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Agent Health Check", Description = "Returns the health status of the agent.")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = _hostProvider.AgentVersion,
            hypervisorType = _hostProvider.HypervisorType
        });
    }

    /// <summary>Gets agent information.</summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Agent Information", Description = "Returns information about the VManager agent.")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            name = "VManager Agent",
            version = _hostProvider.AgentVersion,
            hypervisorType = _hostProvider.HypervisorType,
            description = "VManager agent providing REST API for VM, container, network and storage operations",
            endpoints = new
            {
                vms = "/api/v1/vms",
                containers = "/api/v1/containers",
                networks = "/api/v1/networks",
                storage = "/api/v1/storage",
                host = "/api/v1/host"
            }
        });
    }
}
