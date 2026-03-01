using HyperV.Agent.Controllers;
using HyperV.Agent.Services;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace HyperV.Agent.Tests;

public class ScheduleControllerTests : IDisposable
{
    private readonly ScheduleStore _store;
    private readonly Mock<ILogger<ScheduleController>> _loggerMock;
    private readonly ScheduleController _controller;
    private readonly string _schedulesFilePath;

    public ScheduleControllerTests()
    {
        // Clean up any persisted schedules.json so each test class starts fresh
        _schedulesFilePath = Path.Combine(AppContext.BaseDirectory, "schedules.json");
        if (File.Exists(_schedulesFilePath))
            File.Delete(_schedulesFilePath);

        var configMock = new Mock<IConfiguration>();
        _store = new ScheduleStore(configMock.Object);
        _loggerMock = new Mock<ILogger<ScheduleController>>();
        _controller = new ScheduleController(_store, _loggerMock.Object);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_schedulesFilePath))
                File.Delete(_schedulesFilePath);
        }
        catch { }
    }

    private CreateScheduledTaskRequest MakeValidRequest(string action = "start") => new()
    {
        Name = "Daily Shutdown",
        CronExpression = "0 22 * * *",
        Action = action,
        TargetVms = new[] { "VM1", "VM2" }
    };

    // ──────────────────── GetAll ────────────────────

    [Fact]
    public void GetAll_ReturnsOkWithTasks()
    {
        // Arrange - add a task via the controller
        _controller.CreateSchedule(MakeValidRequest());

        // Act
        var result = _controller.ListSchedules();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<ScheduledTaskDto>>(okResult.Value);
        Assert.Single(tasks);
        Assert.Equal("Daily Shutdown", tasks[0].Name);
    }

    [Fact]
    public void GetAll_Empty_ReturnsEmptyList()
    {
        // Act
        var result = _controller.ListSchedules();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsType<List<ScheduledTaskDto>>(okResult.Value);
        Assert.Empty(tasks);
    }

    // ──────────────────── GetById ────────────────────

    [Fact]
    public void GetById_ExistingTask_ReturnsOk()
    {
        // Arrange
        var createResult = _controller.CreateSchedule(MakeValidRequest());
        var created = Assert.IsType<CreatedResult>(createResult);
        var task = Assert.IsType<ScheduledTaskDto>(created.Value);

        // Act
        var result = _controller.GetSchedule(task.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<ScheduledTaskDto>(okResult.Value);
        Assert.Equal(task.Id, returned.Id);
        Assert.Equal("Daily Shutdown", returned.Name);
    }

    [Fact]
    public void GetById_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = _controller.GetSchedule("nonexistent-id");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────── Create ────────────────────

    [Fact]
    public void Create_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = MakeValidRequest();

        // Act
        var result = _controller.CreateSchedule(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var task = Assert.IsType<ScheduledTaskDto>(createdResult.Value);
        Assert.Equal("Daily Shutdown", task.Name);
        Assert.Equal("0 22 * * *", task.CronExpression);
        Assert.Equal("start", task.Action);
        Assert.Equal(2, task.TargetVms.Length);
        Assert.True(task.IsEnabled);
        Assert.False(string.IsNullOrEmpty(task.Id));
    }

    [Fact]
    public void Create_InvalidCron_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "Bad Cron Task",
            CronExpression = "not-a-valid-cron",
            Action = "start",
            TargetVms = new[] { "VM1" }
        };

        // Act
        var result = _controller.CreateSchedule(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Create_InvalidAction_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "Invalid Action",
            CronExpression = "0 22 * * *",
            Action = "destroy",
            TargetVms = new[] { "VM1" }
        };

        // Act
        var result = _controller.CreateSchedule(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public void Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "",
            CronExpression = "0 22 * * *",
            Action = "start",
            TargetVms = new[] { "VM1" }
        };

        // Act
        var result = _controller.CreateSchedule(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void Create_SnapshotAction_ReturnsCreated()
    {
        // Arrange
        var request = new CreateScheduledTaskRequest
        {
            Name = "Nightly Snapshot",
            CronExpression = "0 3 * * *",
            Action = "snapshot",
            TargetVms = new[] { "WebServer", "DBServer" }
        };

        // Act
        var result = _controller.CreateSchedule(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var task = Assert.IsType<ScheduledTaskDto>(createdResult.Value);
        Assert.Equal("snapshot", task.Action);
        Assert.Equal("Nightly Snapshot", task.Name);
    }

    // ──────────────────── Delete ────────────────────

    [Fact]
    public void Delete_ExistingTask_ReturnsOk()
    {
        // Arrange
        var createResult = _controller.CreateSchedule(MakeValidRequest());
        var created = Assert.IsType<CreatedResult>(createResult);
        var task = Assert.IsType<ScheduledTaskDto>(created.Value);

        // Act
        var result = _controller.DeleteSchedule(task.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Verify it's actually deleted
        var getResult = _controller.GetSchedule(task.Id);
        Assert.IsType<NotFoundObjectResult>(getResult);
    }

    [Fact]
    public void Delete_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = _controller.DeleteSchedule("nonexistent-id");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    // ──────────────────── Enable / Disable ────────────────────

    [Fact]
    public void Enable_ExistingTask_ReturnsOk()
    {
        // Arrange - create and then disable
        var createResult = _controller.CreateSchedule(MakeValidRequest());
        var created = Assert.IsType<CreatedResult>(createResult);
        var task = Assert.IsType<ScheduledTaskDto>(created.Value);
        _controller.DisableSchedule(task.Id);

        // Act
        var result = _controller.EnableSchedule(task.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Verify it's enabled
        var getResult = _controller.GetSchedule(task.Id);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var returned = Assert.IsType<ScheduledTaskDto>(okResult.Value);
        Assert.True(returned.IsEnabled);
    }

    [Fact]
    public void Enable_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = _controller.EnableSchedule("nonexistent-id");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void Disable_ExistingTask_ReturnsOk()
    {
        // Arrange
        var createResult = _controller.CreateSchedule(MakeValidRequest());
        var created = Assert.IsType<CreatedResult>(createResult);
        var task = Assert.IsType<ScheduledTaskDto>(created.Value);

        // Act
        var result = _controller.DisableSchedule(task.Id);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        // Verify it's disabled
        var getResult = _controller.GetSchedule(task.Id);
        var okResult = Assert.IsType<OkObjectResult>(getResult);
        var returned = Assert.IsType<ScheduledTaskDto>(okResult.Value);
        Assert.False(returned.IsEnabled);
    }

    [Fact]
    public void Disable_NonExistent_ReturnsNotFound()
    {
        // Act
        var result = _controller.DisableSchedule("nonexistent-id");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
