using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Core.Hcs.Services;
using HyperV.Core.Wmi.Services;
using HyperV.Tests.Helpers;
using Moq;
using FluentAssertions;
using System.Text.Json;
using HyperV.Core.Wmi;

namespace HyperV.Tests.Controllers;

/// <summary>
/// Template testów dla VmsController - przykład implementacji
/// </summary>
public class VmsControllerTests : IDisposable
{
    private readonly VmsController _controller;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<HyperV.Core.Hcs.Services.VmService> _mockHcsVmService;
    private readonly Mock<HyperV.Core.Wmi.Services.VmService> _mockWmiVmService;
    private readonly Mock<VmCreationService> _mockVmCreationService;
    private readonly Mock<ReplicationService> _mockReplicationService;
    private readonly Mock<MetricsService> _mockMetricsService;
    private readonly Mock<ResourcePoolsService> _mockResourcePoolsService;

    public VmsControllerTests()
    {
        // Inicjalizacja mocków z pomocą ServiceMocks
        _mockStorageService = ServiceMocks.ConfigureStorageServiceMock();
        _mockHcsVmService = ServiceMocks.ConfigureHcsVmServiceMock();
        _mockWmiVmService = ServiceMocks.ConfigureWmiVmServiceMock();
        _mockVmCreationService = ServiceMocks.ConfigureVmCreationServiceMock();
        _mockReplicationService = ServiceMocks.ConfigureReplicationServiceMock();
        _mockMetricsService = new Mock<MetricsService>();
        _mockResourcePoolsService = new Mock<ResourcePoolsService>();

        _controller = new VmsController(
            _mockHcsVmService.Object,
            _mockVmCreationService.Object,
            _mockWmiVmService.Object,
            _mockMetricsService.Object,
            _mockResourcePoolsService.Object,
            _mockReplicationService.Object,
            _mockStorageService.Object
        );
    }

    #region VmPresent Tests - PRZYKŁAD IMPLEMENTACJI

    [Fact]
    public void VmPresent_WithNonExistentVm_ShouldReturnNotPresent()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();

        // Act
        var result = _controller.VmPresent(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.GetProperty("present").GetBoolean().Should().BeFalse();
        root.GetProperty("hcs").GetBoolean().Should().BeFalse();
        root.GetProperty("wmi").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void VmPresent_WithExistingHcsVm_ShouldReturnPresentInHcs()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.VmPresent(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.GetProperty("present").GetBoolean().Should().BeTrue();
        root.GetProperty("hcs").GetBoolean().Should().BeTrue();
        root.GetProperty("wmi").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region ListVms Tests - PRZYKŁAD IMPLEMENTACJI

    [Fact]
    public void ListVms_ShouldReturnOkResult()
    {
        // Act
        var result = _controller.ListVms();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void ListVms_ShouldReturnBothBackendResults()
    {
        // Act
        var result = _controller.ListVms();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.TryGetProperty("HCS", out _).Should().BeTrue();
        root.TryGetProperty("WMI", out _).Should().BeTrue();
        
        // Verify that both services were called
        _mockHcsVmService.Verify(x => x.ListVms(), Times.Once);
        _mockWmiVmService.Verify(x => x.ListVms(), Times.Once);
    }

    #endregion

    #region Create Tests - PRZYKŁAD IMPLEMENTACJI

    [Fact]
    public void Create_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateVmRequest(mode: VmCreationMode.WMI);

        // Act
        var result = _controller.Create(request);

        // Assert
        result.Should().BeOfType<ContentResult>();
        var contentResult = (ContentResult)result;
        contentResult.ContentType.Should().Be("application/json");
        
        // Verify that WMI creation service was called
        _mockVmCreationService.Verify(x => x.CreateHyperVVm(request.Id, request), Times.Once);
    }

    [Fact]
    public void Create_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var result = _controller.Create(null!);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Create_WithEmptyId_ShouldReturnBadRequest()
    {
        // Arrange
        var request = TestDataGenerator.CreateVmRequest(id: "");

        // Act
        var result = _controller.Create(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public void Create_WithHcsMode_ShouldUseHcsService()
    {
        // Arrange
        var request = TestDataGenerator.CreateVmRequest(mode: VmCreationMode.HCS);

        // Act
        var result = _controller.Create(request);

        // Assert
        result.Should().BeOfType<ContentResult>();
        
        // Verify that HCS service was called instead of WMI
        _mockHcsVmService.Verify(x => x.Create(request.Id, request), Times.Once);
        _mockVmCreationService.Verify(x => x.CreateHyperVVm(It.IsAny<string>(), It.IsAny<CreateVmRequest>()), Times.Never);
    }

    #endregion

    #region StartVm Tests

    [Fact]
    public void StartVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.StartVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.StartVm(vmName), Times.Once);
        _mockWmiVmService.Verify(x => x.StartVm(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StartVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.StartVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.StartVm(It.IsAny<string>()), Times.Never);
        _mockWmiVmService.Verify(x => x.StartVm(vmName), Times.Once);
    }

    [Fact]
    public void StartVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.StartVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockHcsVmService.Verify(x => x.StartVm(It.IsAny<string>()), Times.Never);
        _mockWmiVmService.Verify(x => x.StartVm(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StartVm_WhenHcsServiceThrows_ShouldReturn500()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockHcsVmService.Setup(x => x.StartVm(vmName)).Throws(new Exception("HCS error"));

        // Act
        var result = _controller.StartVm(vmName);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region StopVm Tests

    [Fact]
    public void StopVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.StopVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.StopVm(vmName), Times.Once);
        _mockWmiVmService.Verify(x => x.StopVm(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StopVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.StopVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.StopVm(It.IsAny<string>()), Times.Never);
        _mockWmiVmService.Verify(x => x.StopVm(vmName), Times.Once);
    }

    [Fact]
    public void StopVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.StopVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void StopVm_WhenServiceThrowsNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockHcsVmService.Setup(x => x.StopVm(vmName)).Throws(new InvalidOperationException("VM not found"));

        // Act
        var result = _controller.StopVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region ShutdownVm Tests

    [Fact]
    public void ShutdownVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ShutdownVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.StopVm(vmName), Times.Once);
    }

    [Fact]
    public void ShutdownVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ShutdownVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.StopVm(vmName), Times.Once);
    }

    [Fact]
    public void ShutdownVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.ShutdownVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region TerminateVm Tests

    [Fact]
    public void TerminateVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.TerminateVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.TerminateVm(vmName), Times.Once);
    }

    [Fact]
    public void TerminateVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.TerminateVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.TerminateVm(vmName), Times.Once);
    }

    [Fact]
    public void TerminateVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.TerminateVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region PauseVm Tests

    [Fact]
    public void PauseVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.PauseVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.PauseVm(vmName), Times.Once);
    }

    [Fact]
    public void PauseVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.PauseVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.PauseVm(vmName), Times.Once);
    }

    [Fact]
    public void PauseVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.PauseVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region ResumeVm Tests

    [Fact]
    public void ResumeVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ResumeVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.ResumeVm(vmName), Times.Once);
    }

    [Fact]
    public void ResumeVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ResumeVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.ResumeVm(vmName), Times.Once);
    }

    [Fact]
    public void ResumeVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.ResumeVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region SaveVm Tests

    [Fact]
    public void SaveVm_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.SaveVm(vmName);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void SaveVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.SaveVm(vmName);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.SaveVm(vmName), Times.Once);
    }

    [Fact]
    public void SaveVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.SaveVm(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region GetVmProperties Tests

    [Fact]
    public void GetVmProperties_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var mockProperties = "{ \"name\": \"" + vmName + "\", \"state\": \"Running\" }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockHcsVmService.Setup(x => x.GetVmProperties(vmName)).Returns(mockProperties);

        // Act
        var result = _controller.GetVmProperties(vmName);

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
    public void GetVmProperties_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var mockProperties = "{ \"name\": \"" + vmName + "\", \"state\": \"Running\" }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.GetVmProperties(vmName)).Returns(mockProperties);

        // Act
        var result = _controller.GetVmProperties(vmName);

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
    public void GetVmProperties_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.GetVmProperties(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region ModifyVm Tests

    [Fact]
    public void ModifyVm_WithExistingHcsVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var configuration = "{ \"memory\": 4096 }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ModifyVm(vmName, configuration);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockHcsVmService.Verify(x => x.ModifyVm(vmName, configuration), Times.Once);
    }

    [Fact]
    public void ModifyVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var configuration = "{ \"memory\": 4096 }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ModifyVm(vmName, configuration);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.ModifyVm(vmName, configuration), Times.Once);
    }

    [Fact]
    public void ModifyVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var configuration = "{ \"memory\": 4096 }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.ModifyVm(vmName, configuration);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void ModifyVm_WhenNotImplemented_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var configuration = "{ \"memory\": 4096 }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.ModifyVm(vmName, configuration))
            .Throws(new NotImplementedException("VM modification not implemented for WMI backend"));

        // Act
        var result = _controller.ModifyVm(vmName, configuration);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    #endregion

    #region ConfigureVm Tests

    [Fact]
    public void ConfigureVm_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new VmConfigurationRequest { StartupMemoryMB = 4096, CpuCount = 2 };
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ConfigureVm(vmName, request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void ConfigureVm_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new VmConfigurationRequest
        {
            StartupMemoryMB = 4096,
            CpuCount = 2,
            Notes = "Test VM",
            EnableDynamicMemory = true,
            MinimumMemoryMB = 512,
            MaximumMemoryMB = 8192,
            TargetMemoryBuffer = 20
        };
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ConfigureVm(vmName, request);

        // Assert
        result.Should().BeOfType<OkResult>();
        // Verify that the WMI service was called (cannot verify exact parameters due to optional parameters)
        _mockWmiVmService.VerifyAll();
    }

    [Fact]
    public void ConfigureVm_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new VmConfigurationRequest { StartupMemoryMB = 4096, CpuCount = 2 };
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.ConfigureVm(vmName, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Snapshot Tests

    [Fact]
    public void ListVmSnapshots_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.ListVmSnapshots(vmName);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void ListVmSnapshots_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var mockSnapshots = "[ { \"id\": \"snapshot-1\", \"name\": \"Test Snapshot\" } ]";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.ListVmSnapshots(vmName)).Returns(mockSnapshots);

        // Act
        var result = _controller.ListVmSnapshots(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiVmService.Verify(x => x.ListVmSnapshots(vmName), Times.Once);
    }

    [Fact]
    public void CreateVmSnapshot_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new CreateSnapshotRequest { SnapshotName = "Test Snapshot", Notes = "Test Notes" };
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.CreateVmSnapshot(vmName, request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void CreateVmSnapshot_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new CreateSnapshotRequest { SnapshotName = "Test Snapshot", Notes = "Test Notes" };
        var mockResult = "{ \"snapshotId\": \"snapshot-1\" }";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.CreateVmSnapshot(vmName, request.SnapshotName, request.Notes))
            .Returns(mockResult);

        // Act
        var result = _controller.CreateVmSnapshot(vmName, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockWmiVmService.Verify(x => x.CreateVmSnapshot(vmName, request.SnapshotName, request.Notes), Times.Once);
    }

    [Fact]
    public void DeleteVmSnapshot_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var snapshotId = "snapshot-1";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.DeleteVmSnapshot(vmName, snapshotId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void DeleteVmSnapshot_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var snapshotId = "snapshot-1";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.DeleteVmSnapshot(vmName, snapshotId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.DeleteVmSnapshot(vmName, snapshotId), Times.Once);
    }

    [Fact]
    public void RevertVmToSnapshot_WithHcsVm_ShouldReturn501()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var snapshotId = "snapshot-1";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.RevertVmToSnapshot(vmName, snapshotId);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(501);
    }

    [Fact]
    public void RevertVmToSnapshot_WithExistingWmiVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var snapshotId = "snapshot-1";
        _mockHcsVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);

        // Act
        var result = _controller.RevertVmToSnapshot(vmName, snapshotId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockWmiVmService.Verify(x => x.RevertVmToSnapshot(vmName, snapshotId), Times.Once);
    }

    #endregion

    #region Storage Tests

    [Fact]
    public async Task GetVmStorageDevices_WithValidVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var mockDevices = new List<StorageDeviceResponse>
        {
            new StorageDeviceResponse { DeviceId = "device-1", DeviceType = "HardDisk" }
        };
        _mockStorageService.Setup(x => x.GetVmStorageDevicesAsync(vmName))
            .ReturnsAsync(mockDevices);

        // Act
        var result = await _controller.GetVmStorageDevices(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(mockDevices);
    }

    [Fact]
    public async Task GetVmStorageDevices_WithNonExistentVm_ShouldReturnNotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockStorageService.Setup(x => x.GetVmStorageDevicesAsync(vmName))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.GetVmStorageDevices(vmName);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddVmStorageDevice_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new AddStorageDeviceRequest { DeviceType = "HardDisk", Path = "C:\\test.vhdx" };
        _mockStorageService.Setup(x => x.AddStorageDeviceToVmAsync(vmName, request))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AddVmStorageDevice(vmName, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorageService.Verify(x => x.AddStorageDeviceToVmAsync(vmName, request), Times.Once);
    }

    [Fact]
    public async Task RemoveVmStorageDevice_WithValidDevice_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var deviceId = "device-1";
        _mockStorageService.Setup(x => x.RemoveStorageDeviceFromVmAsync(vmName, deviceId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveVmStorageDevice(vmName, deviceId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorageService.Verify(x => x.RemoveStorageDeviceFromVmAsync(vmName, deviceId), Times.Once);
    }

    [Fact]
    public async Task GetVmStorageControllers_WithValidVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var mockControllers = new List<StorageControllerResponse>
        {
            new StorageControllerResponse { ControllerId = "controller-1", ControllerType = "IDE" }
        };
        _mockStorageService.Setup(x => x.GetVmStorageControllersAsync(vmName))
            .ReturnsAsync(mockControllers);

        // Act
        var result = await _controller.GetVmStorageControllers(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(mockControllers);
    }

    [Fact]
    public async Task GetVmStorageDrives_WithValidVm_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();

        // Act
        var result = await _controller.GetVmStorageDrives(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVmStorageDrive_WithValidDrive_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var driveId = "drive-1";

        // Act
        var result = await _controller.GetVmStorageDrive(vmName, driveId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetVmStorageDriveState_WithValidDrive_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var driveId = "drive-1";

        // Act
        var result = await _controller.GetVmStorageDriveState(vmName, driveId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetVmStorageDrive_WithValidDrive_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var driveId = "drive-1";

        // Act
        var result = await _controller.ResetVmStorageDrive(vmName, driveId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task LockVmStorageDriveMedia_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var driveId = "drive-1";
        var request = new LockMediaRequest { Lock = true };

        // Act
        var result = await _controller.LockVmStorageDriveMedia(vmName, driveId, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetVmStorageDriveCapabilities_WithValidDrive_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var driveId = "drive-1";

        // Act
        var result = await _controller.GetVmStorageDriveCapabilities(vmName, driveId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region New Extension Tests

    [Fact]
    public void TestMigrateVm_Success()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new MigrateRequest { DestinationHost = "TargetHost", Live = true, Storage = true };
        var jobId = "\"migration-job-123\"";
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.MigrateVm(vmName, request.DestinationHost, request.Live, request.Storage)).Returns(jobId);

        // Act
        var result = _controller.MigrateVm(vmName, request);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        var acceptedResult = (AcceptedResult)result;
        acceptedResult.Value.Should().BeEquivalentTo(new { jobId = "migration-job-123" });
        _mockWmiVmService.Verify(x => x.MigrateVm(vmName, request.DestinationHost, request.Live, request.Storage), Times.Once);
    }

    [Fact]
    public void TestMigrateVm_NotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new MigrateRequest { DestinationHost = "TargetHost", Live = true, Storage = true };
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.MigrateVm(vmName, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockWmiVmService.Verify(x => x.MigrateVm(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void TestGetAppHealth_Success()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var healthJson = "{\"status\":\"Healthy\",\"timestamp\":\"2023-01-01T00:00:00Z\"}";
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.GetAppHealth(vmName)).Returns(healthJson);

        // Act
        var result = _controller.GetAppHealth(vmName);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(new { status = "Healthy", timestamp = "2023-01-01T00:00:00Z" });
        _mockWmiVmService.Verify(x => x.GetAppHealth(vmName), Times.Once);
    }

    [Fact]
    public void TestGetAppHealth_NotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.GetAppHealth(vmName);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockWmiVmService.Verify(x => x.GetAppHealth(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void TestCopyFileToGuest_Success()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new GuestFileRequest { SourcePath = @"C:\local\file.txt", DestPath = @"C:\Guests\VM\file.txt", Overwrite = true };
        var jobId = "\"copy-job-456\"";
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(true);
        _mockWmiVmService.Setup(x => x.CopyFileToGuest(vmName, request.SourcePath, request.DestPath, request.Overwrite)).Returns(jobId);

        // Act
        var result = _controller.CopyFileToGuest(vmName, request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(new { jobId = "copy-job-456" });
        _mockWmiVmService.Verify(x => x.CopyFileToGuest(vmName, request.SourcePath, request.DestPath, request.Overwrite), Times.Once);
    }

    [Fact]
    public void TestCopyFileToGuest_NotFound()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var request = new GuestFileRequest { SourcePath = @"C:\local\file.txt", DestPath = @"C:\Guests\VM\file.txt", Overwrite = true };
        _mockWmiVmService.Setup(x => x.IsVmPresent(vmName)).Returns(false);

        // Act
        var result = _controller.CopyFileToGuest(vmName, request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
        _mockWmiVmService.Verify(x => x.CopyFileToGuest(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}