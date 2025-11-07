using Microsoft.AspNetCore.Mvc;
using HyperV.Contracts.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace HyperV.Agent.Controllers;

/// <summary>
/// Controller for managing Storage Quality of Service (QoS) policies and their application to virtual machines.
/// Provides REST API endpoints for creating, managing, and applying storage QoS policies that control IOPS and bandwidth limits.
/// </summary>
[ApiController]
[Route("api/v1/storage/qos")]
public class StorageQoSController : ControllerBase
{
    private readonly IStorageQoSService _storageQoSService;

    public StorageQoSController(IStorageQoSService storageQoSService)
    {
        _storageQoSService = storageQoSService;
    }

    /// <summary>
    /// Creates a new Storage QoS policy with specified IOPS and bandwidth limits.
    /// </summary>
    /// <param name="request">The QoS policy creation parameters including policy ID, IOPS limit, bandwidth limit, and description.</param>
    /// <returns>JSON response indicating the creation status of the QoS policy.</returns>
    [HttpPost("policies")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    [SwaggerOperation(
        Summary = "Create Storage QoS Policy",
        Description = "Creates a new storage QoS policy that can be applied to virtual machines to control their IOPS and bandwidth usage."
    )]
    public IActionResult CreateQoSPolicy([FromBody] CreateQoSPolicyRequest request)
    {
        try
        {
            // Validate the request
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(request.PolicyId))
            {
                return BadRequest(new { error = "PolicyId field is required" });
            }

            // Validate limits
            if (request.MaxIops < 0)
            {
                return BadRequest(new { error = "MaxIops must be a non-negative value" });
            }

            if (request.MaxBandwidth < 0)
            {
                return BadRequest(new { error = "MaxBandwidth must be a non-negative value" });
            }

            // Call the service
            var resultJson = _storageQoSService.CreateQoSPolicy(
                request.PolicyId,
                (uint)request.MaxIops,
                (uint)request.MaxBandwidth,
                request.Description ?? string.Empty
            );

            // Parse and return the result
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var result = JsonSerializer.Deserialize<object>(doc.RootElement);
                return Ok(result);
            }
            catch (JsonException)
            {
                // Fallback if JSON parsing fails
                return Ok(new { rawResult = resultJson });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to create QoS policy: {ex.Message}" });
        }
    }

    /// <summary>
    /// Deletes an existing Storage QoS policy.
    /// </summary>
    /// <param name="policyId">The unique identifier of the QoS policy to delete.</param>
    /// <returns>Success response if the policy was deleted, or appropriate error if not found.</returns>
    [HttpDelete("policies/{policyId}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(
        Summary = "Delete Storage QoS Policy",
        Description = "Deletes an existing storage QoS policy. The policy must not be currently applied to any virtual machines."
    )]
    public IActionResult DeleteQoSPolicy([FromRoute] Guid policyId)
    {
        try
        {
            _storageQoSService.DeleteQoSPolicy(policyId);
            return Ok(new { message = "QoS policy deleted successfully", policyId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"QoS policy {policyId} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to delete QoS policy: {ex.Message}" });
        }
    }

    /// <summary>
    /// Retrieves information about a specific Storage QoS policy.
    /// </summary>
    /// <param name="policyId">The unique identifier of the QoS policy to retrieve.</param>
    /// <returns>JSON response containing the QoS policy details including IOPS and bandwidth limits.</returns>
    [HttpGet("policies/{policyId}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(
        Summary = "Get Storage QoS Policy Information",
        Description = "Retrieves detailed information about a specific storage QoS policy including its IOPS and bandwidth configuration."
    )]
    public IActionResult GetQoSPolicyInfo([FromRoute] Guid policyId)
    {
        try
        {
            var resultJson = _storageQoSService.GetQoSPolicyInfo(policyId);

            // Parse and return the result
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var result = JsonSerializer.Deserialize<object>(doc.RootElement);
                return Ok(result);
            }
            catch (JsonException)
            {
                // Fallback if JSON parsing fails
                return Ok(new { rawResult = resultJson });
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"QoS policy {policyId} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to get QoS policy info: {ex.Message}" });
        }
    }

    /// <summary>
    /// Applies a Storage QoS policy to a virtual machine.
    /// </summary>
    /// <param name="vmName">The name of the virtual machine to apply the policy to.</param>
    /// <param name="request">The request containing the policy ID to apply.</param>
    /// <returns>Success response if the policy was applied successfully.</returns>
    [HttpPost("{vmName}/qos-policy")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [SwaggerOperation(
        Summary = "Apply QoS Policy to VM",
        Description = "Applies a storage QoS policy to a virtual machine, controlling its IOPS and bandwidth usage."
    )]
    public IActionResult ApplyQoSPolicyToVm([FromRoute] string vmName, [FromBody] ApplyQoSPolicyRequest request)
    {
        try
        {
            // Validate the request
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate required fields
            if (string.IsNullOrEmpty(vmName))
            {
                return BadRequest(new { error = "VM name is required" });
            }

            if (request.PolicyId == Guid.Empty)
            {
                return BadRequest(new { error = "Valid PolicyId is required" });
            }

            _storageQoSService.ApplyQoSPolicyToVm(vmName, request.PolicyId);
            return Ok(new { message = "QoS policy applied successfully", vmName, policyId = request.PolicyId });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = $"VM '{vmName}' or QoS policy {request.PolicyId} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Failed to apply QoS policy: {ex.Message}" });
        }
    }
}

/// <summary>
/// Request model for creating a new Storage QoS policy.
/// </summary>
public class CreateQoSPolicyRequest
{
    /// <summary>
    /// The unique identifier for the QoS policy.
    /// </summary>
    [Required]
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>
    /// The maximum IOPS (Input/Output Operations Per Second) limit for the policy.
    /// Set to 0 for unlimited IOPS.
    /// </summary>
    [Range(0, uint.MaxValue)]
    public uint MaxIops { get; set; } = 0;

    /// <summary>
    /// The maximum bandwidth limit in bytes per second for the policy.
    /// Set to 0 for unlimited bandwidth.
    /// </summary>
    [Range(0, uint.MaxValue)]
    public uint MaxBandwidth { get; set; } = 0;

    /// <summary>
    /// Optional description for the QoS policy.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Request model for applying a QoS policy to a virtual machine.
/// </summary>
public class ApplyQoSPolicyRequest
{
    /// <summary>
    /// The unique identifier of the QoS policy to apply.
    /// </summary>
    [Required]
    public Guid PolicyId { get; set; }
}