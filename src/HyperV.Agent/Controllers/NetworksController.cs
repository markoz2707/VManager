using HyperV.Core.Hcn.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Operacje sieciowe (HCN).</summary>
[ApiController]
[Route("api/v1/networks")]
public sealed class NetworksController : ControllerBase
{
    private readonly NetworkService _svc;
    public NetworksController(NetworkService svc) => _svc = svc;

    /// <summary>Tworzy prostą sieć NAT.</summary>
    [HttpPost("nat")]
    [ProducesResponseType(typeof(object), 200)]
    [SwaggerOperation(Summary = "Create NAT network", Description = "Creates a NAT virtual switch via HCN API.")]
    public IActionResult CreateNat([FromQuery] string name, [FromQuery] string prefix = "192.168.100.0/24")
    {
        var id = _svc.CreateNATNetwork(name, prefix);
        return Ok(new { id });
    }

    /// <summary>Usuwa sieć.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete network", Description = "Deletes a network by ID.")]
    public IActionResult DeleteNetwork([FromRoute] Guid id)
    {
        try
        {
            _svc.DeleteNetwork(id);
            return Ok();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return NotFound();
        }
    }

    /// <summary>Pobiera właściwości sieci.</summary>
    [HttpGet("{id}/properties")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get network properties", Description = "Gets properties of a network by ID.")]
    public IActionResult GetNetworkProperties([FromRoute] Guid id, [FromQuery] string query = "")
    {
        try
        {
            var properties = _svc.QueryNetworkProperties(id, query);
            return Ok(properties);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return NotFound();
        }
    }

    /// <summary>Tworzy endpoint w sieci.</summary>
    [HttpPost("{networkId}/endpoints")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Create endpoint", Description = "Creates an endpoint in the specified network.")]
    public IActionResult CreateEndpoint([FromRoute] Guid networkId, [FromQuery] string name, [FromQuery] string ipAddress = "")
    {
        try
        {
            var endpointId = _svc.CreateEndpoint(networkId, name, ipAddress);
            return Ok(new { id = endpointId });
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return NotFound();
        }
    }

    /// <summary>Usuwa endpoint.</summary>
    [HttpDelete("endpoints/{endpointId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete endpoint", Description = "Deletes an endpoint by ID.")]
    public IActionResult DeleteEndpoint([FromRoute] Guid endpointId)
    {
        try
        {
            _svc.DeleteEndpoint(endpointId);
            return Ok();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return NotFound();
        }
    }

    /// <summary>Pobiera właściwości endpoint.</summary>
    [HttpGet("endpoints/{endpointId}/properties")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get endpoint properties", Description = "Gets properties of an endpoint by ID.")]
    public IActionResult GetEndpointProperties([FromRoute] Guid endpointId, [FromQuery] string query = "")
    {
        try
        {
            var properties = _svc.QueryEndpointProperties(endpointId, query);
            return Ok(properties);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return NotFound();
        }
    }
}
