using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Tests.Helpers;
using Moq;
using FluentAssertions;
using System.Text.Json;

namespace HyperV.Tests.Controllers;

/// <summary>
/// Testy jednostkowe dla JobsController
/// </summary>
public class JobsControllerTests : IDisposable
{
    private readonly JobsController _controller;
    private readonly Mock<IJobService> _mockJobService;

    public JobsControllerTests()
    {
        _mockJobService = ServiceMocks.ConfigureJobServiceMock();
        _controller = new JobsController(_mockJobService.Object);
    }

    #region GetStorageJobs Tests

    [Fact]
    public async Task GetStorageJobs_ShouldReturnOkResult()
    {
        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetStorageJobs_ShouldReturnJobsList()
    {
        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        var okResult = (OkObjectResult)result;
        var jobs = okResult.Value as List<StorageJobResponse>;
        
        jobs.Should().NotBeNull();
        jobs.Should().HaveCount(1);
        jobs![0].JobId.Should().Be("job-1");
        jobs[0].OperationType.Should().Be("CreateVHD");
        jobs[0].State.Should().Be(StorageJobState.Completed);
    }

    [Fact]
    public async Task GetStorageJobs_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        _mockJobService.Setup(x => x.GetStorageJobsAsync())
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.GetStorageJobs();

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetStorageJob Tests

    [Fact]
    public async Task GetStorageJob_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var jobId = TestDataGenerator.GenerateJobId();

        // Act
        var result = await _controller.GetStorageJob(jobId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetStorageJob_WithValidId_ShouldReturnJobDetails()
    {
        // Arrange
        var jobId = "job-1";

        // Act
        var result = await _controller.GetStorageJob(jobId);

        // Assert
        var okResult = (OkObjectResult)result;
        var job = okResult.Value as StorageJobResponse;
        
        job.Should().NotBeNull();
        job!.JobId.Should().Be("job-1");
        job.OperationType.Should().Be("CreateVHD");
        job.State.Should().Be(StorageJobState.Completed);
        job.PercentComplete.Should().Be(100);
    }

    [Fact]
    public async Task GetStorageJob_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var jobId = "non-existent-job";
        _mockJobService.Setup(x => x.GetStorageJobAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Job not found"));

        // Act
        var result = await _controller.GetStorageJob(jobId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetStorageJob_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        var jobId = "job-1";
        _mockJobService.Setup(x => x.GetStorageJobAsync(jobId))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.GetStorageJob(jobId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetJobAffectedElements Tests

    [Fact]
    public async Task GetJobAffectedElements_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var jobId = "job-1";

        // Act
        var result = await _controller.GetJobAffectedElements(jobId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetJobAffectedElements_WithValidId_ShouldReturnElementsList()
    {
        // Arrange
        var jobId = "job-1";

        // Act
        var result = await _controller.GetJobAffectedElements(jobId);

        // Assert
        var okResult = (OkObjectResult)result;
        var elements = okResult.Value as List<AffectedElementResponse>;
        
        elements.Should().NotBeNull();
        elements.Should().HaveCount(1);
        elements![0].ElementId.Should().Be("element-1");
        elements[0].ElementName.Should().Be("test.vhdx");
        elements[0].ElementType.Should().Be("VirtualHardDisk");
    }

    [Fact]
    public async Task GetJobAffectedElements_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var jobId = "non-existent-job";
        _mockJobService.Setup(x => x.GetJobAffectedElementsAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Job not found"));

        // Act
        var result = await _controller.GetJobAffectedElements(jobId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.Should().Be(404);
    }

    #endregion

    #region CancelStorageJob Tests

    [Fact]
    public async Task CancelStorageJob_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var jobId = "job-1";

        // Act
        var result = await _controller.CancelStorageJob(jobId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
        
        // Verify service was called
        _mockJobService.Verify(x => x.CancelStorageJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task CancelStorageJob_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var jobId = "non-existent-job";
        _mockJobService.Setup(x => x.CancelStorageJobAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Job not found"));

        // Act
        var result = await _controller.CancelStorageJob(jobId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CancelStorageJob_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        var jobId = "job-1";
        _mockJobService.Setup(x => x.CancelStorageJobAsync(jobId))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.CancelStorageJob(jobId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region DeleteStorageJob Tests

    [Fact]
    public async Task DeleteStorageJob_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var jobId = "job-1";

        // Act
        var result = await _controller.DeleteStorageJob(jobId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
        
        // Verify service was called
        _mockJobService.Verify(x => x.DeleteStorageJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task DeleteStorageJob_WithInvalidId_ShouldReturn404()
    {
        // Arrange
        var jobId = "non-existent-job";
        _mockJobService.Setup(x => x.DeleteStorageJobAsync(jobId))
            .ThrowsAsync(new InvalidOperationException("Job not found"));

        // Act
        var result = await _controller.DeleteStorageJob(jobId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;
        notFoundResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task DeleteStorageJob_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        var jobId = "job-1";
        _mockJobService.Setup(x => x.DeleteStorageJobAsync(jobId))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.DeleteStorageJob(jobId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region Verification Tests

    [Fact]
    public async Task GetStorageJobs_ShouldCallJobService()
    {
        // Act
        await _controller.GetStorageJobs();

        // Assert
        _mockJobService.Verify(x => x.GetStorageJobsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetStorageJob_ShouldCallJobServiceWithCorrectId()
    {
        // Arrange
        var jobId = "test-job-id";

        // Act
        await _controller.GetStorageJob(jobId);

        // Assert
        _mockJobService.Verify(x => x.GetStorageJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task GetJobAffectedElements_ShouldCallJobServiceWithCorrectId()
    {
        // Arrange
        var jobId = "test-job-id";

        // Act
        await _controller.GetJobAffectedElements(jobId);

        // Assert
        _mockJobService.Verify(x => x.GetJobAffectedElementsAsync(jobId), Times.Once);
    }

    #endregion

    public void Dispose()
    {
        // Clean up if needed
    }
}