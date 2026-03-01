using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Agent.Tests;

public class ContainersControllerTests
{
    private readonly Mock<IContainerProvider> _containerProviderMock;
    private readonly Mock<ILogger<ContainersController>> _loggerMock;
    private readonly ContainersController _controller;

    public ContainersControllerTests()
    {
        _containerProviderMock = new Mock<IContainerProvider>();
        _loggerMock = new Mock<ILogger<ContainersController>>();
        _controller = new ContainersController(_containerProviderMock.Object, _loggerMock.Object);
    }

    // ──────────────────── ListContainers ────────────────────

    [Fact]
    public async Task ListContainers_ReturnsOkWithContainers()
    {
        // Arrange
        var containers = new List<ContainerSummaryDto>
        {
            new() { Id = "c-1", Name = "WebApp", State = "Running", Image = "mcr.microsoft.com/windows/servercore", Backend = "HCS" },
            new() { Id = "c-2", Name = "Database", State = "Stopped", Image = "mcr.microsoft.com/mssql/server", Backend = "Docker" }
        };
        _containerProviderMock.Setup(p => p.ListContainersAsync()).ReturnsAsync(containers);

        // Act
        var result = await _controller.ListContainers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<ContainerSummaryDto>>(okResult.Value);
        Assert.Equal(2, returned.Count);
        _containerProviderMock.Verify(p => p.ListContainersAsync(), Times.Once);
    }

    [Fact]
    public async Task ListContainers_WhenEmpty_ReturnsOkWithEmptyList()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.ListContainersAsync()).ReturnsAsync(new List<ContainerSummaryDto>());

        // Act
        var result = await _controller.ListContainers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<ContainerSummaryDto>>(okResult.Value);
        Assert.Empty(returned);
    }

    [Fact]
    public async Task ListContainers_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.ListContainersAsync())
            .ThrowsAsync(new Exception("HCS service unavailable"));

        // Act
        var result = await _controller.ListContainers();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetContainer ────────────────────

    [Fact]
    public async Task GetContainer_WhenFound_ReturnsOk()
    {
        // Arrange
        var container = new ContainerDetailsDto
        {
            Id = "c-1",
            Name = "WebApp",
            State = "Running",
            Image = "mcr.microsoft.com/windows/servercore",
            Backend = "HCS",
            CpuCount = 2,
            MemoryMB = 1024
        };
        _containerProviderMock.Setup(p => p.GetContainerAsync("c-1")).ReturnsAsync(container);

        // Act
        var result = await _controller.GetContainer("c-1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<ContainerDetailsDto>(okResult.Value);
        Assert.Equal("c-1", returned.Id);
        Assert.Equal("Running", returned.State);
        Assert.Equal(2, returned.CpuCount);
    }

    [Fact]
    public async Task GetContainer_WhenNotFound_Returns404()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.GetContainerAsync("nonexistent"))
            .ReturnsAsync((ContainerDetailsDto?)null);

        // Act
        var result = await _controller.GetContainer("nonexistent");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.GetContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Provider error"));

        // Act
        var result = await _controller.GetContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── CreateContainer ────────────────────

    [Fact]
    public async Task CreateContainer_WithValidSpec_ReturnsOkWithId()
    {
        // Arrange
        var spec = new CreateContainerSpec
        {
            Name = "NewContainer",
            Image = "mcr.microsoft.com/windows/servercore",
            CpuCount = 2,
            MemoryMB = 512
        };
        _containerProviderMock.Setup(p => p.CreateContainerAsync(spec)).ReturnsAsync("new-container-id");

        // Act
        var result = await _controller.CreateContainer(spec);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.CreateContainerAsync(spec), Times.Once);
    }

    [Fact]
    public async Task CreateContainer_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateContainerSpec { Name = "", Image = "test-image" };

        // Act
        var result = await _controller.CreateContainer(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _containerProviderMock.Verify(p => p.CreateContainerAsync(It.IsAny<CreateContainerSpec>()), Times.Never);
    }

    [Fact]
    public async Task CreateContainer_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateContainerSpec { Name = null!, Image = "test-image" };

        // Act
        var result = await _controller.CreateContainer(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        var spec = new CreateContainerSpec { Name = "NewContainer", Image = "test-image" };
        _containerProviderMock.Setup(p => p.CreateContainerAsync(spec))
            .ThrowsAsync(new Exception("Image not found"));

        // Act
        var result = await _controller.CreateContainer(spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── DeleteContainer ────────────────────

    [Fact]
    public async Task DeleteContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.DeleteContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.DeleteContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task DeleteContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.DeleteContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Container in use"));

        // Act
        var result = await _controller.DeleteContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── StartContainer ────────────────────

    [Fact]
    public async Task StartContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.StartContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StartContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.StartContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task StartContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.StartContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Start failed"));

        // Act
        var result = await _controller.StartContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── StopContainer ────────────────────

    [Fact]
    public async Task StopContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.StopContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StopContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.StopContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task StopContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.StopContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Stop failed"));

        // Act
        var result = await _controller.StopContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── PauseContainer ────────────────────

    [Fact]
    public async Task PauseContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.PauseContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PauseContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.PauseContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task PauseContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.PauseContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Pause failed"));

        // Act
        var result = await _controller.PauseContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ResumeContainer ────────────────────

    [Fact]
    public async Task ResumeContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.ResumeContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ResumeContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.ResumeContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task ResumeContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.ResumeContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Resume failed"));

        // Act
        var result = await _controller.ResumeContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── TerminateContainer ────────────────────

    [Fact]
    public async Task TerminateContainer_Success_ReturnsOk()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.TerminateContainerAsync("c-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TerminateContainer("c-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _containerProviderMock.Verify(p => p.TerminateContainerAsync("c-1"), Times.Once);
    }

    [Fact]
    public async Task TerminateContainer_WhenProviderThrows_Returns500()
    {
        // Arrange
        _containerProviderMock.Setup(p => p.TerminateContainerAsync("c-1"))
            .ThrowsAsync(new Exception("Terminate failed"));

        // Act
        var result = await _controller.TerminateContainer("c-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
