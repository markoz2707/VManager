using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Network operations (unified provider).</summary>
[ApiController]
[Route("api/v1/networks")]
public sealed class NetworksController : ControllerBase
{
    private readonly INetworkProvider _networkProvider;
    private readonly ILogger<NetworksController> _logger;

    public NetworksController(INetworkProvider networkProvider, ILogger<NetworksController> logger)
    {
        _networkProvider = networkProvider;
        _logger = logger;
    }

    /// <summary>List all virtual networks.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<VirtualNetworkInfo>), 200)]
    [SwaggerOperation(Summary = "List networks", Description = "Lists all virtual networks from the current hypervisor.")]
    public async Task<IActionResult> ListNetworks([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 200) pageSize = 200;

            var networks = await _networkProvider.ListNetworksAsync();
            var totalCount = networks.Count;
            var items = networks.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new PaginatedResult<VirtualNetworkInfo>
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
            _logger.LogError(ex, "Error listing networks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get network details.</summary>
    [HttpGet("{networkId}")]
    [ProducesResponseType(typeof(VirtualNetworkInfo), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get network", Description = "Gets details of a specific network.")]
    public async Task<IActionResult> GetNetwork(string networkId)
    {
        try
        {
            var network = await _networkProvider.GetNetworkAsync(networkId);
            if (network == null) return NotFound(new { error = $"Network '{networkId}' not found" });
            return Ok(network);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting network {NetworkId}", networkId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Create a new virtual network.</summary>
    [HttpPost]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create network", Description = "Creates a virtual network. Type can be: Internal, External, Private, NAT, Bridge, Routed, Isolated.")]
    public async Task<IActionResult> CreateNetwork([FromBody] CreateNetworkSpec spec)
    {
        try
        {
            if (string.IsNullOrEmpty(spec.Name))
                return BadRequest(new { error = "Network name is required" });

            var id = await _networkProvider.CreateNetworkAsync(spec);
            return Created($"/api/v1/networks/{id}", new { id, name = spec.Name, type = spec.Type });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating network");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete a virtual network.</summary>
    [HttpDelete("{networkId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete network", Description = "Deletes a virtual network by ID.")]
    public async Task<IActionResult> DeleteNetwork(string networkId)
    {
        try
        {
            await _networkProvider.DeleteNetworkAsync(networkId);
            return Ok(new { status = "deleted", id = networkId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting network {NetworkId}", networkId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>List physical network adapters.</summary>
    [HttpGet("adapters")]
    [ProducesResponseType(typeof(List<PhysicalAdapterDto>), 200)]
    [SwaggerOperation(Summary = "List physical adapters", Description = "Lists all physical network adapters on the host.")]
    public async Task<IActionResult> ListPhysicalAdapters()
    {
        try
        {
            var adapters = await _networkProvider.ListPhysicalAdaptersAsync();
            return Ok(adapters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing physical adapters");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get VM network adapters.</summary>
    [HttpGet("vms/{vmName}/adapters")]
    [ProducesResponseType(typeof(List<VmNetworkAdapterDto>), 200)]
    [SwaggerOperation(Summary = "Get VM network adapters", Description = "Lists all network adapters attached to a VM.")]
    public async Task<IActionResult> GetVmNetworkAdapters(string vmName)
    {
        try
        {
            var adapters = await _networkProvider.GetVmNetworkAdaptersAsync(vmName);
            return Ok(adapters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VM network adapters for {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Attach network adapter to VM.</summary>
    [HttpPost("vms/{vmName}/adapters")]
    [ProducesResponseType(200)]
    [SwaggerOperation(Summary = "Attach network adapter", Description = "Attaches a network adapter to a VM.")]
    public async Task<IActionResult> AttachNetworkAdapter(string vmName, [FromBody] AttachNetworkAdapterRequest request)
    {
        try
        {
            await _networkProvider.AttachNetworkAdapterAsync(vmName, request.NetworkId);
            return Ok(new { status = "attached", vmName, networkId = request.NetworkId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching network adapter to {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Detach network adapter from VM.</summary>
    [HttpDelete("vms/{vmName}/adapters/{adapterId}")]
    [ProducesResponseType(200)]
    [SwaggerOperation(Summary = "Detach network adapter", Description = "Detaches a network adapter from a VM.")]
    public async Task<IActionResult> DetachNetworkAdapter(string vmName, string adapterId)
    {
        try
        {
            await _networkProvider.DetachNetworkAdapterAsync(vmName, adapterId);
            return Ok(new { status = "detached", vmName, adapterId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detaching network adapter from {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public class AttachNetworkAdapterRequest
{
    public string NetworkId { get; set; } = string.Empty;
}
