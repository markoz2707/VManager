using HyperV.Agent.Services;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace HyperV.Agent.Controllers;

/// <summary>Scheduled task management.</summary>
[ApiController]
[Route("api/v1/schedules")]
public class ScheduleController : ControllerBase
{
    private readonly ScheduleStore _store;
    private readonly ILogger<ScheduleController> _logger;

    public ScheduleController(ScheduleStore store, ILogger<ScheduleController> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>List all scheduled tasks.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ScheduledTaskDto>), 200)]
    [SwaggerOperation(Summary = "List schedules", Description = "Returns all scheduled tasks.")]
    public IActionResult ListSchedules()
    {
        return Ok(_store.GetAll());
    }

    /// <summary>Get a specific scheduled task.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScheduledTaskDto), 200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Get schedule", Description = "Returns a specific scheduled task.")]
    public IActionResult GetSchedule(string id)
    {
        var task = _store.GetById(id);
        if (task == null) return NotFound(new { error = $"Schedule '{id}' not found" });
        return Ok(task);
    }

    /// <summary>Create a new scheduled task.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ScheduledTaskDto), 201)]
    [ProducesResponseType(400)]
    [SwaggerOperation(Summary = "Create schedule", Description = "Creates a new scheduled task with a cron expression.")]
    public IActionResult CreateSchedule([FromBody] CreateScheduledTaskRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest(new { error = "Name is required" });

        if (string.IsNullOrEmpty(request.CronExpression))
            return BadRequest(new { error = "CronExpression is required" });

        if (string.IsNullOrEmpty(request.Action))
            return BadRequest(new { error = "Action is required" });

        var validActions = new[] { "start", "stop", "shutdown", "snapshot" };
        if (!validActions.Contains(request.Action.ToLowerInvariant()))
            return BadRequest(new { error = $"Action must be one of: {string.Join(", ", validActions)}" });

        if (request.TargetVms == null || request.TargetVms.Length == 0)
            return BadRequest(new { error = "At least one target VM is required" });

        try
        {
            // Validate cron expression
            Cronos.CronExpression.Parse(request.CronExpression);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Invalid cron expression: {ex.Message}" });
        }

        var task = _store.Add(request);
        _logger.LogInformation("Created scheduled task {TaskId}: {TaskName}", task.Id, task.Name);
        return Created($"/api/v1/schedules/{task.Id}", task);
    }

    /// <summary>Delete a scheduled task.</summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Delete schedule", Description = "Deletes a scheduled task.")]
    public IActionResult DeleteSchedule(string id)
    {
        if (!_store.Delete(id))
            return NotFound(new { error = $"Schedule '{id}' not found" });

        _logger.LogInformation("Deleted scheduled task {TaskId}", id);
        return Ok(new { status = "deleted", id });
    }

    /// <summary>Enable a scheduled task.</summary>
    [HttpPost("{id}/enable")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Enable schedule", Description = "Enables a scheduled task.")]
    public IActionResult EnableSchedule(string id)
    {
        if (!_store.SetEnabled(id, true))
            return NotFound(new { error = $"Schedule '{id}' not found" });

        return Ok(new { status = "enabled", id });
    }

    /// <summary>Disable a scheduled task.</summary>
    [HttpPost("{id}/disable")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    [SwaggerOperation(Summary = "Disable schedule", Description = "Disables a scheduled task.")]
    public IActionResult DisableSchedule(string id)
    {
        if (!_store.SetEnabled(id, false))
            return NotFound(new { error = $"Schedule '{id}' not found" });

        return Ok(new { status = "disabled", id });
    }
}
