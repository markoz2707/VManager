using HyperV.Agent.Controllers;
using HyperV.Core.Wmi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Agent.Tests;

public class VmsControllerTests
{
    private readonly Mock<ILogger<VmsController>> _loggerMock;
    private readonly Mock<VmService> _vmServiceMock;
    private readonly VmsController _controller;

    public VmsControllerTests()
    {
        _loggerMock = new Mock<ILogger<VmsController>>();
        _vmServiceMock = new Mock<VmService>();
        _controller = new VmsController(_loggerMock.Object, _vmServiceMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new VmsController(null, _vmServiceMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullVmService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new VmsController(_loggerMock.Object, null));
        Assert.Equal("vmService", exception.ParamName);
    }

    [Fact]
    public void GetVms_ReturnsOkResult()
    {
        // Arrange
        var expectedJson = "{\"Count\":0,\"VMs\":[],\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.ListVms()).Returns(expectedJson);

        // Act
        var result = _controller.GetVms();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetVms_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _vmServiceMock.Setup(s => s.ListVms()).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetVms();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetVm_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"Name\":\"test-vm\",\"State\":\"Running\",\"EnabledState\":2,\"HealthState\":\"OK\",\"OperationalStatus\":null,\"Description\":null,\"CreationTime\":null,\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmProperties(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetVm(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmProperties(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void StartVm_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.StartVm(vmId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void StartVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.StartVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void StartVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.StartVm(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.StartVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void StopVm_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.StopVm(vmId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void StopVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.StopVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void StopVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.StopVm(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.StopVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void TerminateVm_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.TerminateVm(vmId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void TerminateVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.TerminateVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void TerminateVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.TerminateVm(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.TerminateVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void PauseVm_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.PauseVm(vmId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void PauseVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.PauseVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void PauseVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.PauseVm(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.PauseVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void ResumeVm_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.ResumeVm(vmId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ResumeVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.ResumeVm(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ResumeVm_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.ResumeVm(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.ResumeVm(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void ModifyVmConfiguration_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new VmConfigurationRequest { StartupMemoryMB = 2048, CpuCount = 2 };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.ModifyVmConfiguration(vmId, request);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ModifyVmConfiguration_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var request = new VmConfigurationRequest { StartupMemoryMB = 2048, CpuCount = 2 };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.ModifyVmConfiguration(vmId, request);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ModifyVmConfiguration_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new VmConfigurationRequest { StartupMemoryMB = 2048, CpuCount = 2 };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.ModifyVmConfiguration(vmId, 2048, 2, null, null, null, null, null, null, null, null, null, null, null)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.ModifyVmConfiguration(vmId, request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void ListVmSnapshots_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"Snapshots\":[],\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.ListVmSnapshots(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.ListVmSnapshots(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void ListVmSnapshots_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.ListVmSnapshots(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ListVmSnapshots_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.ListVmSnapshots(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.ListVmSnapshots(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void CreateVmSnapshot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new CreateSnapshotRequest { SnapshotName = "TestSnapshot", Notes = "Test notes" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.CreateVmSnapshot(vmId, request);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void CreateVmSnapshot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var request = new CreateSnapshotRequest { SnapshotName = "TestSnapshot", Notes = "Test notes" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.CreateVmSnapshot(vmId, request);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void CreateVmSnapshot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new CreateSnapshotRequest { SnapshotName = "TestSnapshot", Notes = "Test notes" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.CreateVmSnapshot(vmId, "TestSnapshot", "Test notes")).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.CreateVmSnapshot(vmId, request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void DeleteVmSnapshot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.DeleteVmSnapshot(vmId, snapshotId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void DeleteVmSnapshot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.DeleteVmSnapshot(vmId, snapshotId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void DeleteVmSnapshot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.DeleteVmSnapshot(vmId, snapshotId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.DeleteVmSnapshot(vmId, snapshotId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void RevertVmToSnapshot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.RevertVmToSnapshot(vmId, snapshotId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void RevertVmToSnapshot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.RevertVmToSnapshot(vmId, snapshotId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void RevertVmToSnapshot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var snapshotId = "snapshot-id";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.RevertVmToSnapshot(vmId, snapshotId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.RevertVmToSnapshot(vmId, snapshotId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetVmMemoryStatus_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"MemoryUsage\":1024,\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmMemoryStatus(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetVmMemoryStatus(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetVmMemoryStatus_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetVmMemoryStatus(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetVmMemoryStatus_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmMemoryStatus(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetVmMemoryStatus(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetSlpDataRoot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"SlpDataRoot\":\"C:\\\\SlpData\",\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetSlpDataRoot(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetSlpDataRoot(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetSlpDataRoot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetSlpDataRoot(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetSlpDataRoot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetSlpDataRoot(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetSlpDataRoot(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void ModifySlpDataRoot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var newLocation = "C:\\NewSlpData";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.ModifySlpDataRoot(vmId, newLocation);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ModifySlpDataRoot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var newLocation = "C:\\NewSlpData";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.ModifySlpDataRoot(vmId, newLocation);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void ModifySlpDataRoot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var newLocation = "C:\\NewSlpData";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.ModifySlpDataRoot(vmId, newLocation)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.ModifySlpDataRoot(vmId, newLocation);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetVmGeneration_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"Generation\":2,\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmGeneration(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetVmGeneration(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetVmGeneration_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetVmGeneration(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetVmGeneration_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetVmGeneration(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetVmGeneration(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetSecureBoot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"SecureBoot\":true,\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetSecureBoot(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetSecureBoot(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetSecureBoot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetSecureBoot(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetSecureBoot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetSecureBoot(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetSecureBoot(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void SetSecureBoot_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var secureBoot = true;
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.SetSecureBoot(vmId, secureBoot);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SetSecureBoot_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var secureBoot = true;
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.SetSecureBoot(vmId, secureBoot);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void SetSecureBoot_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var secureBoot = true;
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.SetSecureBoot(vmId, secureBoot)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.SetSecureBoot(vmId, secureBoot);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetBootOrder_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedJson = "{\"BootOrder\":[\"HardDisk\",\"Network\"],\"Backend\":\"WMI\"}";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetBootOrder(vmId)).Returns(expectedJson);

        // Act
        var result = _controller.GetBootOrder(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedJson, okResult.Value);
    }

    [Fact]
    public void GetBootOrder_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetBootOrder(vmId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetBootOrder_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetBootOrder(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetBootOrder(vmId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void SetBootOrder_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var bootOrder = new[] { "HardDisk", "Network" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);

        // Act
        var result = _controller.SetBootOrder(vmId, bootOrder);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SetBootOrder_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var bootOrder = new[] { "HardDisk", "Network" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.SetBootOrder(vmId, bootOrder);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void SetBootOrder_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var vmId = "test-vm";
        var bootOrder = new[] { "HardDisk", "Network" };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.SetBootOrder(vmId, bootOrder)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.SetBootOrder(vmId, bootOrder);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(statusResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void MigrateVm_WithValidRequest_ReturnsAcceptedResult()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new MigrateRequest { DestinationHost = "remote-host", Live = true, Storage = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.MigrateVm(vmId, "remote-host", true, false)).Returns("job-123");

        // Act
        var result = _controller.MigrateVm(vmId, request);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        var response = Assert.IsType<Dictionary<string, object>>(acceptedResult.Value);
        Assert.Equal("job-123", response["jobId"]);
    }

    [Fact]
    public void MigrateVm_WithNullDestinationHost_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new MigrateRequest { DestinationHost = null, Live = true, Storage = false };

        // Act
        var result = _controller.MigrateVm(vmId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("DestinationHost is required", errorResponse["error"]);
    }

    [Fact]
    public void MigrateVm_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var request = new MigrateRequest { DestinationHost = "remote-host", Live = true, Storage = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.MigrateVm(vmId, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(notFoundResult.Value);
        Assert.Equal($"VM '{vmId}' not found", errorResponse["error"]);
    }

    [Fact]
    public void MigrateVm_WhenServiceThrowsException_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new MigrateRequest { DestinationHost = "remote-host", Live = true, Storage = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.MigrateVm(vmId, "remote-host", true, false)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.MigrateVm(vmId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void GetAppHealth_WithValidVm_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var expectedHealth = new AppHealthResponse { Status = "OK", AppStatus = 2 };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetAppHealth(vmId)).Returns(JsonSerializer.Serialize(expectedHealth));

        // Act
        var result = _controller.GetAppHealth(vmId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var health = Assert.IsType<AppHealthResponse>(okResult.Value);
        Assert.Equal("OK", health.Status);
        Assert.Equal(2, health.AppStatus);
    }

    [Fact]
    public void GetAppHealth_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.GetAppHealth(vmId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(notFoundResult.Value);
        Assert.Equal($"VM '{vmId}' not found", errorResponse["error"]);
    }

    [Fact]
    public void GetAppHealth_WhenServiceThrowsException_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.GetAppHealth(vmId)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.GetAppHealth(vmId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }

    [Fact]
    public void CopyFileToGuest_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new GuestFileRequest { SourcePath = "C:\\host\\file.txt", DestPath = "C:\\guest\\file.txt", Overwrite = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.CopyFileToGuest(vmId, "C:\\host\\file.txt", "C:\\guest\\file.txt", false)).Returns("job-456");

        // Act
        var result = _controller.CopyFileToGuest(vmId, request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<Dictionary<string, object>>(okResult.Value);
        Assert.Equal("job-456", response["jobId"]);
    }

    [Fact]
    public void CopyFileToGuest_WithNullSourcePath_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new GuestFileRequest { SourcePath = null, DestPath = "C:\\guest\\file.txt", Overwrite = false };

        // Act
        var result = _controller.CopyFileToGuest(vmId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("SourcePath and DestPath are required", errorResponse["error"]);
    }

    [Fact]
    public void CopyFileToGuest_WithNullDestPath_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new GuestFileRequest { SourcePath = "C:\\host\\file.txt", DestPath = null, Overwrite = false };

        // Act
        var result = _controller.CopyFileToGuest(vmId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("SourcePath and DestPath are required", errorResponse["error"]);
    }

    [Fact]
    public void CopyFileToGuest_WithNonExistentVm_ReturnsNotFound()
    {
        // Arrange
        var vmId = "nonexistent-vm";
        var request = new GuestFileRequest { SourcePath = "C:\\host\\file.txt", DestPath = "C:\\guest\\file.txt", Overwrite = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(false);

        // Act
        var result = _controller.CopyFileToGuest(vmId, request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(notFoundResult.Value);
        Assert.Equal($"VM '{vmId}' not found", errorResponse["error"]);
    }

    [Fact]
    public void CopyFileToGuest_WhenServiceThrowsException_ReturnsBadRequest()
    {
        // Arrange
        var vmId = "test-vm";
        var request = new GuestFileRequest { SourcePath = "C:\\host\\file.txt", DestPath = "C:\\guest\\file.txt", Overwrite = false };
        _vmServiceMock.Setup(s => s.IsVmPresent(vmId)).Returns(true);
        _vmServiceMock.Setup(s => s.CopyFileToGuest(vmId, "C:\\host\\file.txt", "C:\\guest\\file.txt", false)).Throws(new Exception("Test exception"));

        // Act
        var result = _controller.CopyFileToGuest(vmId, request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<Dictionary<string, object>>(badRequestResult.Value);
        Assert.Equal("Test exception", errorResponse["error"]);
    }
}