using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Contracts.Models.Common;
using HyperV.Contracts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers;

/// <summary>Hyper-V specific features not available on other hypervisors.</summary>
[ApiController]
[Route("api/v1/hyperv")]
[SwaggerTag("Hyper-V specific features (Fibre Channel, QoS, Replication, Dynamic Memory, SecureBoot, VLAN, Image Management)")]
public class HyperVFeaturesController : ControllerBase
{
    private readonly IReplicationService _replicationService;
    private readonly IStorageQoSService _storageQoSService;
    private readonly IImageManagementService _imageManagementService;
    private readonly ILogger<HyperVFeaturesController> _logger;

    public HyperVFeaturesController(
        IReplicationService replicationService,
        IStorageQoSService storageQoSService,
        IImageManagementService imageManagementService,
        ILogger<HyperVFeaturesController> logger)
    {
        _replicationService = replicationService;
        _storageQoSService = storageQoSService;
        _imageManagementService = imageManagementService;
        _logger = logger;
    }

    // ---- Replication ----

    /// <summary>Create replication relationship.</summary>
    [HttpPost("replication/relationships")]
    [SwaggerOperation(Summary = "Create replication relationship")]
    public IActionResult CreateReplicationRelationship([FromBody] CreateReplicationRequest req)
    {
        try
        {
            var result = _replicationService.CreateReplicationRelationship(req.SourceVm, req.TargetHost, req.AuthMode);
            return Ok(new { status = "created", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating replication relationship");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Start replication for a VM.</summary>
    [HttpPost("replication/{vmName}/start")]
    [SwaggerOperation(Summary = "Start replication")]
    public IActionResult StartReplication(string vmName)
    {
        try
        {
            _replicationService.StartReplication(vmName);
            return Ok(new { status = "started", vmName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting replication for {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Initiate failover.</summary>
    [HttpPost("replication/{vmName}/failover")]
    [SwaggerOperation(Summary = "Initiate failover")]
    public IActionResult InitiateFailover(string vmName, [FromBody] FailoverRequest? req)
    {
        try
        {
            var mode = req?.Mode ?? "Planned";
            var result = _replicationService.InitiateFailover(vmName, mode);
            return Accepted(new { status = "failover_initiated", vmName, mode, result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating failover for {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Reverse replication relationship.</summary>
    [HttpPut("replication/{vmName}/reverse")]
    [SwaggerOperation(Summary = "Reverse replication")]
    public IActionResult ReverseReplication(string vmName)
    {
        try
        {
            _replicationService.ReverseReplicationRelationship(vmName);
            return Ok(new { status = "reversed", vmName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reversing replication for {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get replication state.</summary>
    [HttpGet("replication/{vmName}/state")]
    [SwaggerOperation(Summary = "Get replication state")]
    public IActionResult GetReplicationState(string vmName)
    {
        try
        {
            var state = _replicationService.GetReplicationState(vmName);
            return Ok(new { vmName, state });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting replication state for {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Add authorization entry.</summary>
    [HttpPut("replication/{relationshipId}/authorization")]
    [SwaggerOperation(Summary = "Add replication authorization")]
    public IActionResult AddAuthorizationEntry(string relationshipId, [FromBody] AuthorizationEntryRequest req)
    {
        try
        {
            _replicationService.AddAuthorizationEntry(relationshipId, req.Entry);
            return Ok(new { status = "authorized", relationshipId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding authorization entry");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---- Storage QoS ----

    /// <summary>Create QoS policy.</summary>
    [HttpPost("qos/policies")]
    [SwaggerOperation(Summary = "Create QoS policy")]
    public IActionResult CreateQoSPolicy([FromBody] CreateQoSPolicyRequest req)
    {
        try
        {
            var result = _storageQoSService.CreateQoSPolicy(req.PolicyId, req.MaxIops, req.MaxBandwidth, req.Description ?? "");
            return Ok(new { status = "created", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating QoS policy");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Delete QoS policy.</summary>
    [HttpDelete("qos/policies/{policyId}")]
    [SwaggerOperation(Summary = "Delete QoS policy")]
    public IActionResult DeleteQoSPolicy(Guid policyId)
    {
        try
        {
            _storageQoSService.DeleteQoSPolicy(policyId);
            return Ok(new { status = "deleted", policyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting QoS policy {PolicyId}", policyId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get QoS policy info.</summary>
    [HttpGet("qos/policies/{policyId}")]
    [SwaggerOperation(Summary = "Get QoS policy")]
    public IActionResult GetQoSPolicy(Guid policyId)
    {
        try
        {
            var result = _storageQoSService.GetQoSPolicyInfo(policyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting QoS policy {PolicyId}", policyId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Apply QoS policy to VM.</summary>
    [HttpPost("qos/{vmName}/apply")]
    [SwaggerOperation(Summary = "Apply QoS policy to VM")]
    public IActionResult ApplyQoSPolicy(string vmName, [FromBody] ApplyQoSPolicyRequest req)
    {
        try
        {
            _storageQoSService.ApplyQoSPolicyToVm(vmName, req.PolicyId);
            return Ok(new { status = "applied", vmName, policyId = req.PolicyId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying QoS policy to {VmName}", vmName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ---- Image Management (VHD-specific) ----

    /// <summary>Compact VHD/VHDX.</summary>
    [HttpPost("images/compact")]
    [SwaggerOperation(Summary = "Compact VHD")]
    public async Task<IActionResult> CompactImage([FromBody] CompactVhdRequest req)
    {
        try
        {
            await _imageManagementService.CompactVirtualHardDiskAsync(req);
            return Ok(new { status = "compacted", path = req.Path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error compacting VHD");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Merge differencing disk.</summary>
    [HttpPost("images/merge")]
    [SwaggerOperation(Summary = "Merge VHD")]
    public async Task<IActionResult> MergeImage([FromBody] MergeDiskRequest req)
    {
        try
        {
            await _imageManagementService.MergeVirtualHardDiskAsync(req);
            return Ok(new { status = "merged" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging VHD");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Convert VHD format.</summary>
    [HttpPost("images/convert")]
    [SwaggerOperation(Summary = "Convert VHD")]
    public async Task<IActionResult> ConvertImage([FromBody] ConvertVhdRequest req)
    {
        try
        {
            var result = await _imageManagementService.ConvertVirtualHardDiskAsync(req);
            return Ok(new { status = "converted", result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting VHD");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get VHD settings.</summary>
    [HttpGet("images/settings")]
    [SwaggerOperation(Summary = "Get VHD settings")]
    public async Task<IActionResult> GetImageSettings([FromQuery] string path)
    {
        try
        {
            var result = await _imageManagementService.GetVirtualHardDiskSettingDataAsync(path);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VHD settings");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Get VHD state.</summary>
    [HttpGet("images/state")]
    [SwaggerOperation(Summary = "Get VHD state")]
    public async Task<IActionResult> GetImageState([FromQuery] string path)
    {
        try
        {
            var result = await _imageManagementService.GetVirtualHardDiskStateAsync(path);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting VHD state");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request models for HyperV-specific features
public class CreateReplicationRequest
{
    [Required] public string SourceVm { get; set; } = string.Empty;
    [Required] public string TargetHost { get; set; } = string.Empty;
    public string AuthMode { get; set; } = "Certificate";
}

public class FailoverRequest
{
    public string Mode { get; set; } = "Planned";
}

public class AuthorizationEntryRequest
{
    [Required] public string Entry { get; set; } = string.Empty;
}

public class CreateQoSPolicyRequest
{
    [Required] public string PolicyId { get; set; } = string.Empty;
    [Range(0, uint.MaxValue)] public uint MaxIops { get; set; }
    [Range(0, uint.MaxValue)] public uint MaxBandwidth { get; set; }
    public string? Description { get; set; }
}

public class ApplyQoSPolicyRequest
{
    [Required] public Guid PolicyId { get; set; }
}
