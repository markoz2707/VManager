using HyperV.CentralManagement.Authorization;
using HyperV.CentralManagement.Models;
using HyperV.CentralManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HyperV.CentralManagement.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AlertService _alertService;
    private readonly AuditLogService _audit;

    public AlertsController(AlertService alertService, AuditLogService audit)
    {
        _alertService = alertService;
        _audit = audit;
    }

    // Alert Definitions

    [HttpGet("definitions")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetDefinitions()
    {
        var definitions = await _alertService.GetDefinitionsAsync();
        return Ok(definitions);
    }

    [HttpGet("definitions/{id:guid}")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetDefinition(Guid id)
    {
        var definition = await _alertService.GetDefinitionAsync(id);
        if (definition == null) return NotFound();
        return Ok(definition);
    }

    [HttpPost("definitions")]
    [RequirePermission("host", "create")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateAlertDefinitionDto dto)
    {
        var definition = new AlertDefinition
        {
            Name = dto.Name,
            Description = dto.Description,
            MetricName = dto.MetricName,
            Condition = dto.Condition,
            ThresholdValue = dto.ThresholdValue,
            Severity = dto.Severity,
            EvaluationPeriods = dto.EvaluationPeriods,
            CooldownSeconds = dto.CooldownSeconds,
            IsEnabled = dto.IsEnabled,
            ClusterId = dto.ClusterId,
            AgentHostId = dto.AgentHostId
        };

        var result = await _alertService.CreateDefinitionAsync(definition);
        await _audit.WriteAsync(User.Identity?.Name, "alert_definition_created", definition.Name);
        return CreatedAtAction(nameof(GetDefinition), new { id = result.Id }, result);
    }

    [HttpPut("definitions/{id:guid}")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> UpdateDefinition(Guid id, [FromBody] CreateAlertDefinitionDto dto)
    {
        var updated = new AlertDefinition
        {
            Name = dto.Name,
            Description = dto.Description,
            MetricName = dto.MetricName,
            Condition = dto.Condition,
            ThresholdValue = dto.ThresholdValue,
            Severity = dto.Severity,
            EvaluationPeriods = dto.EvaluationPeriods,
            CooldownSeconds = dto.CooldownSeconds,
            IsEnabled = dto.IsEnabled,
            ClusterId = dto.ClusterId,
            AgentHostId = dto.AgentHostId
        };

        if (!await _alertService.UpdateDefinitionAsync(id, updated))
            return NotFound();

        await _audit.WriteAsync(User.Identity?.Name, "alert_definition_updated", dto.Name);
        return Ok();
    }

    [HttpDelete("definitions/{id:guid}")]
    [RequirePermission("host", "delete")]
    public async Task<IActionResult> DeleteDefinition(Guid id)
    {
        if (!await _alertService.DeleteDefinitionAsync(id))
            return NotFound();

        await _audit.WriteAsync(User.Identity?.Name, "alert_definition_deleted", id.ToString());
        return Ok();
    }

    // Alert Instances

    [HttpGet("active")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetActiveAlerts()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
        return Ok(alerts);
    }

    [HttpGet("history")]
    [RequirePermission("host", "read")]
    public async Task<IActionResult> GetAlertHistory([FromQuery] int limit = 100)
    {
        var alerts = await _alertService.GetAlertHistoryAsync(limit);
        return Ok(alerts);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> AcknowledgeAlert(Guid id)
    {
        if (!await _alertService.AcknowledgeAlertAsync(id, User.Identity?.Name ?? "unknown"))
            return BadRequest(new { error = "Cannot acknowledge this alert." });

        await _audit.WriteAsync(User.Identity?.Name, "alert_acknowledged", id.ToString());
        return Ok();
    }

    [HttpPost("{id:guid}/resolve")]
    [RequirePermission("host", "update")]
    public async Task<IActionResult> ResolveAlert(Guid id)
    {
        if (!await _alertService.ResolveAlertAsync(id))
            return BadRequest(new { error = "Cannot resolve this alert." });

        await _audit.WriteAsync(User.Identity?.Name, "alert_resolved", id.ToString());
        return Ok();
    }
}

public class CreateAlertDefinitionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public AlertCondition Condition { get; set; }
    public double ThresholdValue { get; set; }
    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;
    public int EvaluationPeriods { get; set; } = 1;
    public int CooldownSeconds { get; set; } = 300;
    public bool IsEnabled { get; set; } = true;
    public Guid? ClusterId { get; set; }
    public Guid? AgentHostId { get; set; }
}
