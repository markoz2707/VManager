using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Agent.Tests;

public class JobsControllerTests
{
    private readonly Mock<IJobService> _jobServiceMock;
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _jobServiceMock = new Mock<IJobService>();
        _controller = new JobsController(_jobServiceMock.Object);
    }

    // --- GetStorageJobs Tests ---

    [Fact]
    public async Task GetStorageJobs_ReturnsOkResult()
    {
        // Arrange
        var expectedJobs = new List<StorageJobResponse>
        {
            new StorageJobResponse { JobId = "job-1" }
        };
        _jobServiceMock.Setup(s => s.GetStorageJobsAsync()).ReturnsAsync(expectedJobs);

        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var jobs = Assert.IsType<List<StorageJobResponse>>(okResult.Value);
        Assert.Single(jobs);
    }

    [Fact]
    public async Task GetStorageJobs_WhenEmpty_ReturnsOkResultWithEmptyList()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetStorageJobsAsync()).ReturnsAsync(new List<StorageJobResponse>());

        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var jobs = Assert.IsType<List<StorageJobResponse>>(okResult.Value);
        Assert.Empty(jobs);
    }

    [Fact]
    public async Task GetStorageJobs_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetStorageJobsAsync()).ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // --- GetStorageJob Tests ---

    [Fact]
    public async Task GetStorageJob_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var expectedJob = new StorageJobResponse { JobId = "job-1" };
        _jobServiceMock.Setup(s => s.GetStorageJobAsync("job-1")).ReturnsAsync(expectedJob);

        // Act
        var result = await _controller.GetStorageJob("job-1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var job = Assert.IsType<StorageJobResponse>(okResult.Value);
        Assert.Equal("job-1", job.JobId);
    }

    [Fact]
    public async Task GetStorageJob_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetStorageJobAsync("nonexistent"))
            .ThrowsAsync(new InvalidOperationException("Job 'nonexistent' not found"));

        // Act
        var result = await _controller.GetStorageJob("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetStorageJob_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetStorageJobAsync("job-1"))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetStorageJob("job-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // --- GetJobAffectedElements Tests ---

    [Fact]
    public async Task GetJobAffectedElements_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var expectedElements = new List<AffectedElementResponse>
        {
            new AffectedElementResponse()
        };
        _jobServiceMock.Setup(s => s.GetJobAffectedElementsAsync("job-1")).ReturnsAsync(expectedElements);

        // Act
        var result = await _controller.GetJobAffectedElements("job-1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetJobAffectedElements_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetJobAffectedElementsAsync("nonexistent"))
            .ThrowsAsync(new InvalidOperationException("Job 'nonexistent' not found"));

        // Act
        var result = await _controller.GetJobAffectedElements("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetJobAffectedElements_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.GetJobAffectedElementsAsync("job-1"))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetJobAffectedElements("job-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // --- CancelStorageJob Tests ---

    [Fact]
    public async Task CancelStorageJob_WithValidId_ReturnsOkResult()
    {
        // Act
        var result = await _controller.CancelStorageJob("job-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CancelStorageJob_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.CancelStorageJobAsync("nonexistent"))
            .ThrowsAsync(new InvalidOperationException("Job 'nonexistent' not found"));

        // Act
        var result = await _controller.CancelStorageJob("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CancelStorageJob_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.CancelStorageJobAsync("job-1"))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.CancelStorageJob("job-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // --- DeleteStorageJob Tests ---

    [Fact]
    public async Task DeleteStorageJob_WithValidId_ReturnsOkResult()
    {
        // Act
        var result = await _controller.DeleteStorageJob("job-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DeleteStorageJob_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.DeleteStorageJobAsync("nonexistent"))
            .ThrowsAsync(new InvalidOperationException("Job 'nonexistent' not found"));

        // Act
        var result = await _controller.DeleteStorageJob("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteStorageJob_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _jobServiceMock.Setup(s => s.DeleteStorageJobAsync("job-1"))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.DeleteStorageJob("job-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
