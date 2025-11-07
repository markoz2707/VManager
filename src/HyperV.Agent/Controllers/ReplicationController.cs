using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using HyperV.Contracts.Services;

namespace HyperV.Agent.Controllers;

[ApiController]
[Route("api/v1/replication")]
public class ReplicationController : ControllerBase
{
    private readonly IReplicationService _replicationService;

    public ReplicationController(IReplicationService replicationService)
    {
        _replicationService = replicationService;
    }

    [HttpPost("relationships")]
    public IActionResult CreateReplicationRelationship([FromBody] CreateReplicationRequest request)
    {
        try
        {
            var result = _replicationService.CreateReplicationRelationship(request.SourceVm, request.TargetHost, request.AuthMode);
            return Ok(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{vmName}/start")]
    public IActionResult StartReplication(string vmName)
    {
        try
        {
            _replicationService.StartReplication(vmName);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{vmName}/failover")]
    public IActionResult InitiateFailover(string vmName, [FromBody] FailoverRequest request)
    {
        try
        {
            string result = _replicationService.InitiateFailover(vmName, request.Mode);
            return Accepted(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{vmName}/reverse")]
    public IActionResult ReverseReplicationRelationship(string vmName)
    {
        try
        {
            _replicationService.ReverseReplicationRelationship(vmName);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{vmName}/state")]
    public IActionResult GetReplicationState(string vmName)
    {
        try
        {
            string result = _replicationService.GetReplicationState(vmName);
            return Ok(JsonDocument.Parse(result));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{relationshipId}/authorization")]
    public IActionResult AddAuthorizationEntry(string relationshipId, [FromBody] string entry)
    {
        try
        {
            _replicationService.AddAuthorizationEntry(relationshipId, entry);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class CreateReplicationRequest
{
    public required string SourceVm { get; set; }
    public required string TargetHost { get; set; }
    public string AuthMode { get; set; } = "Certificate";
}

public class FailoverRequest
{
    public string Mode { get; set; } = "Planned";
}
