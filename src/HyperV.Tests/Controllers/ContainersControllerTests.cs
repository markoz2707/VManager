using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using HyperV.Tests.Helpers;
using Moq;
using FluentAssertions;
using System.Text.Json;

namespace HyperV.Tests.Controllers;

/// <summary>
/// Testy jednostkowe dla ContainersController
/// </summary>
public class ContainersControllerTests : IDisposable
{
    private readonly ContainersController _controller;
    private readonly Mock<HyperV.Core.Hcs.Services.ContainerService> _mockHcsContainerService;
    private readonly Mock<HyperV.Core.Wmi.Services.ContainerService> _mockWmiContainerService;

    public ContainersControllerTests()
    {
        _mockHcsContainerService = ServiceMocks.ConfigureHcsContainerServiceMock();
        _mockWmiContainerService = ServiceMocks.ConfigureWmiContainerServiceMock();

        _controller = new ContainersController(
            _mockHcsContainerService.Object,
            _mockWmiContainerService.Object
        );
    }

    #region CreateContainer Tests

    [Fact]
    public void CreateContainer_WithValidHcsRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateContainerRequest(mode: ContainerCreationMode.HCS);
        var mockResult = "{ \"containerId\": \"" + request.Id + "\" }";
        _mockHcsContainerService.Setup(x => x.Create(request.Id, request)).Returns(mockResult);

        // Act
        var result = _controller.CreateContainer(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.Create(request.Id, request), Times.Once);
        _mockWmiContainerService.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<CreateContainerRequest>()), Times.Never);
    }

    [Fact]
    public void CreateContainer_WithValidWmiRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateContainerRequest(mode: ContainerCreationMode.WMI);
        var mockResult = "{ \"containerId\": \"" + request.Id + "\" }";
        _mockWmiContainerService.Setup(x => x.Create(request.Id, request)).Returns(mockResult);

        // Act
        var result = _controller.CreateContainer(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.Create(request.Id, request), Times.Once);
        _mockHcsContainerService.Verify(x => x.Create(It.IsAny<string>(), It.IsAny<CreateContainerRequest>()), Times.Never);
    }

    [Fact]
    public void CreateContainer_WithInvalidModelState_ShouldReturnBadRequest()
    {
        // Arrange
        var request = TestDataGenerator.CreateContainerRequest();
        _controller.ModelState.AddModelError("Name", "Required");

        // Act
        var result = _controller.CreateContainer(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void CreateContainer_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        var request = TestDataGenerator.CreateContainerRequest(mode: ContainerCreationMode.HCS);
        _mockHcsContainerService.Setup(x => x.Create(request.Id, request))
            .Throws(new Exception("HCS error"));

        // Act
        var result = _controller.CreateContainer(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region ListContainers Tests

    [Fact]
    public void ListContainers_ShouldReturnOkResult()
    {
        // Act
        var result = _controller.ListContainers();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void ListContainers_ShouldReturnPlaceholderMessage()
    {
        // Act
        var result = _controller.ListContainers();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.TryGetProperty("HcsContainers", out _).Should().BeTrue();
        root.TryGetProperty("WmiContainers", out _).Should().BeTrue();
        root.TryGetProperty("Message", out _).Should().BeTrue();
    }

    #endregion

    #region GetContainer Tests

    [Fact]
    public void GetContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        var mockProperties = "{ \"id\": \"" + containerId + "\", \"state\": \"Running\" }";
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);
        _mockHcsContainerService.Setup(x => x.GetContainerProperties(containerId)).Returns(mockProperties);

        // Act
        var result = _controller.GetContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("backend").GetString().Should().Be("HCS");
        root.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void GetContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        var mockProperties = "{ \"id\": \"" + containerId + "\", \"state\": \"Running\" }";
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);
        _mockWmiContainerService.Setup(x => x.GetContainerProperties(containerId)).Returns(mockProperties);

        // Act
        var result = _controller.GetContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("backend").GetString().Should().Be("WMI");
        root.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public void GetContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.GetContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region StartContainer Tests

    [Fact]
    public void StartContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.StartContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.StartContainer(containerId), Times.Once);
    }

    [Fact]
    public void StartContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.StartContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.StartContainer(containerId), Times.Once);
    }

    [Fact]
    public void StartContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.StartContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region StopContainer Tests

    [Fact]
    public void StopContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.StopContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.StopContainer(containerId), Times.Once);
    }

    [Fact]
    public void StopContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.StopContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.StopContainer(containerId), Times.Once);
    }

    [Fact]
    public void StopContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.StopContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region TerminateContainer Tests

    [Fact]
    public void TerminateContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.TerminateContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
    }

    [Fact]
    public void TerminateContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.TerminateContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
    }

    [Fact]
    public void TerminateContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.TerminateContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region PauseContainer Tests

    [Fact]
    public void PauseContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.PauseContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.PauseContainer(containerId), Times.Once);
    }

    [Fact]
    public void PauseContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.PauseContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.PauseContainer(containerId), Times.Once);
    }

    [Fact]
    public void PauseContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.PauseContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region ResumeContainer Tests

    [Fact]
    public void ResumeContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.ResumeContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.ResumeContainer(containerId), Times.Once);
    }

    [Fact]
    public void ResumeContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.ResumeContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.ResumeContainer(containerId), Times.Once);
    }

    [Fact]
    public void ResumeContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.ResumeContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteContainer Tests

    [Fact]
    public void DeleteContainer_WithExistingHcsContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.DeleteContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
    }

    [Fact]
    public void DeleteContainer_WithExistingWmiContainer_ShouldReturnOkResult()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.DeleteContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
    }

    [Fact]
    public void DeleteContainer_WithNonExistentContainer_ShouldReturnNotFound()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(false);

        // Act
        var result = _controller.DeleteContainer(containerId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void DeleteContainer_WithBothBackendsPresent_ShouldTerminateBoth()
    {
        // Arrange
        var containerId = TestDataGenerator.GenerateContainerId();
        _mockHcsContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);
        _mockWmiContainerService.Setup(x => x.IsContainerPresent(containerId)).Returns(true);

        // Act
        var result = _controller.DeleteContainer(containerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockHcsContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
        _mockWmiContainerService.Verify(x => x.TerminateContainer(containerId), Times.Once);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}