using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Interfaces.Providers;
using HyperV.Contracts.Models;
using HyperV.Contracts.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Agent.Tests;

public class VmsControllerTests
{
    private readonly Mock<IVmProvider> _vmProviderMock;
    private readonly Mock<IStorageProvider> _storageProviderMock;
    private readonly Mock<IMetricsProvider> _metricsProviderMock;
    private readonly Mock<IMigrationProvider> _migrationProviderMock;
    private readonly Mock<IJobService> _jobServiceMock;
    private readonly Mock<ILogger<VmsController>> _loggerMock;
    private readonly VmsController _controller;

    public VmsControllerTests()
    {
        _vmProviderMock = new Mock<IVmProvider>();
        _storageProviderMock = new Mock<IStorageProvider>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
        _migrationProviderMock = new Mock<IMigrationProvider>();
        _jobServiceMock = new Mock<IJobService>();
        _loggerMock = new Mock<ILogger<VmsController>>();

        _controller = new VmsController(
            _vmProviderMock.Object,
            _storageProviderMock.Object,
            _metricsProviderMock.Object,
            _migrationProviderMock.Object,
            _jobServiceMock.Object,
            _loggerMock.Object);
    }

    #region Create

    [Fact]
    public async Task Create_ValidSpec_ReturnsContentResult()
    {
        // Arrange
        var spec = new CreateVmSpec { Name = "TestVM", CpuCount = 2, MemoryMB = 2048 };
        _vmProviderMock.Setup(x => x.CreateVmAsync(spec)).ReturnsAsync("{\"id\":\"123\"}");

        // Act
        var result = await _controller.Create(spec);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Equal("{\"id\":\"123\"}", contentResult.Content);
    }

    [Fact]
    public async Task Create_NullSpec_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.Create(null!);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateVmSpec { Name = "", CpuCount = 2, MemoryMB = 2048 };

        // Act
        var result = await _controller.Create(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateVmSpec { Name = "TestVM", CpuCount = 2, MemoryMB = 2048 };
        _vmProviderMock.Setup(x => x.CreateVmAsync(spec)).ThrowsAsync(new Exception("Creation failed"));

        // Act
        var result = await _controller.Create(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region ListVms

    [Fact]
    public async Task ListVms_ReturnsOkWithVmList()
    {
        // Arrange
        var vms = new List<VmSummaryDto>
        {
            new VmSummaryDto { Id = "1", Name = "VM1", State = "Running" },
            new VmSummaryDto { Id = "2", Name = "VM2", State = "Off" }
        };
        _vmProviderMock.Setup(x => x.ListVmsAsync()).ReturnsAsync(vms);

        // Act
        var result = await _controller.ListVms();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedVms = Assert.IsType<List<VmSummaryDto>>(okResult.Value);
        Assert.Equal(2, returnedVms.Count);
    }

    [Fact]
    public async Task ListVms_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ListVmsAsync()).ThrowsAsync(new Exception("Provider error"));

        // Act
        var result = await _controller.ListVms();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region GetVm (VmPresent)

    [Fact]
    public async Task VmPresent_VmExists_ReturnsOkWithPresentTrue()
    {
        // Arrange
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);

        // Act
        var result = await _controller.VmPresent("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task VmPresent_VmNotFound_ReturnsOkWithPresentFalse()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("NonExistent")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.VmPresent("NonExistent");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task VmPresent_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.VmPresent("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region StartVm

    [Fact]
    public async Task StartVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StartVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StartVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task StartVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StartVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.StartVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StartVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StartVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Failed to start"));

        // Act
        var result = await _controller.StartVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region StopVm

    [Fact]
    public async Task StopVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.StopVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task StopVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.StopVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task StopVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Stop failed"));

        // Act
        var result = await _controller.StopVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ShutdownVm

    [Fact]
    public async Task ShutdownVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ShutdownVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ShutdownVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ShutdownVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ShutdownVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.ShutdownVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ShutdownVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ShutdownVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Shutdown failed"));

        // Act
        var result = await _controller.ShutdownVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region PauseVm

    [Fact]
    public async Task PauseVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.PauseVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.PauseVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task PauseVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.PauseVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.PauseVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PauseVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.PauseVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Pause failed"));

        // Act
        var result = await _controller.PauseVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ResumeVm

    [Fact]
    public async Task ResumeVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ResumeVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ResumeVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ResumeVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ResumeVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.ResumeVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ResumeVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ResumeVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Resume failed"));

        // Act
        var result = await _controller.ResumeVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region SaveVm

    [Fact]
    public async Task SaveVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.SaveVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SaveVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task SaveVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.SaveVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.SaveVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SaveVm_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.SaveVmAsync("TestVM"))
            .ThrowsAsync(new NotSupportedException("Save not supported"));

        // Act
        var result = await _controller.SaveVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task SaveVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.SaveVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Save failed"));

        // Act
        var result = await _controller.SaveVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region TerminateVm

    [Fact]
    public async Task TerminateVm_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TerminateVm("TestVM");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task TerminateVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.TerminateVm("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task TerminateVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Terminate failed"));

        // Act
        var result = await _controller.TerminateVm("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ConfigureVm

    [Fact]
    public async Task ConfigureVm_Success_ReturnsOk()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4, MemoryMB = 4096 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ConfigureVm("TestVM", spec);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ConfigureVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.ConfigureVm("TestVM", spec);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ConfigureVm_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.ConfigureVm("TestVM", spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task ConfigureVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new Exception("Configure failed"));

        // Act
        var result = await _controller.ConfigureVm("TestVM", spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ModifyVm (backwards compatibility)

    [Fact]
    public async Task ModifyVm_Success_ReturnsOk()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4, MemoryMB = 8192 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ModifyVm("TestVM", spec);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task ModifyVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.ModifyVm("TestVM", spec);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ModifyVm_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.ModifyVm("TestVM", spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task ModifyVm_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        var spec = new VmConfigurationSpec { CpuCount = 4 };
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", spec))
            .ThrowsAsync(new Exception("Modify failed"));

        // Act
        var result = await _controller.ModifyVm("TestVM", spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region CloneVm

    [Fact]
    public async Task CloneVm_Success_ReturnsOkWithCloneInfo()
    {
        // Arrange
        var request = new CloneVmRequest { NewVmName = "ClonedVM", CloneType = VmCloneType.Full };
        var sourceVm = new VmDetailsDto { Id = "1", Name = "SourceVM", State = "Off" };

        _vmProviderMock.Setup(x => x.GetVmAsync("SourceVM")).ReturnsAsync(sourceVm);
        _vmProviderMock.Setup(x => x.GetVmAsync("ClonedVM")).ReturnsAsync((VmDetailsDto?)null);
        _vmProviderMock.Setup(x => x.CloneVmAsync("SourceVM", "ClonedVM")).ReturnsAsync("clone-result-id");

        // Act
        var result = await _controller.CloneVm("SourceVM", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CloneVm_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.CloneVm("SourceVM", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CloneVm_EmptyNewName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CloneVmRequest { NewVmName = "" };

        // Act
        var result = await _controller.CloneVm("SourceVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CloneVm_SourceNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new CloneVmRequest { NewVmName = "ClonedVM" };
        _vmProviderMock.Setup(x => x.GetVmAsync("SourceVM")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.CloneVm("SourceVM", request);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Fact]
    public async Task CloneVm_TargetAlreadyExists_ReturnsBadRequest()
    {
        // Arrange
        var request = new CloneVmRequest { NewVmName = "ExistingVM" };
        var sourceVm = new VmDetailsDto { Id = "1", Name = "SourceVM", State = "Off" };
        var existingTarget = new VmDetailsDto { Id = "2", Name = "ExistingVM", State = "Off" };

        _vmProviderMock.Setup(x => x.GetVmAsync("SourceVM")).ReturnsAsync(sourceVm);
        _vmProviderMock.Setup(x => x.GetVmAsync("ExistingVM")).ReturnsAsync(existingTarget);

        // Act
        var result = await _controller.CloneVm("SourceVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CloneVm_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new CloneVmRequest { NewVmName = "ClonedVM" };
        var sourceVm = new VmDetailsDto { Id = "1", Name = "SourceVM", State = "Off" };

        _vmProviderMock.Setup(x => x.GetVmAsync("SourceVM")).ReturnsAsync(sourceVm);
        _vmProviderMock.Setup(x => x.GetVmAsync("ClonedVM")).ReturnsAsync((VmDetailsDto?)null);
        _vmProviderMock.Setup(x => x.CloneVmAsync("SourceVM", "ClonedVM"))
            .ThrowsAsync(new Exception("Clone failed"));

        // Act
        var result = await _controller.CloneVm("SourceVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region GetVmProperties

    [Fact]
    public async Task GetVmProperties_Success_ReturnsOkWithProperties()
    {
        // Arrange
        var properties = new VmPropertiesDto { CpuCount = 4, MemoryMB = 8192, EnableDynamicMemory = true };
        _vmProviderMock.Setup(x => x.GetVmPropertiesAsync("TestVM")).ReturnsAsync(properties);

        // Act
        var result = await _controller.GetVmProperties("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedProps = Assert.IsType<VmPropertiesDto>(okResult.Value);
        Assert.Equal(4, returnedProps.CpuCount);
        Assert.Equal(8192, returnedProps.MemoryMB);
    }

    [Fact]
    public async Task GetVmProperties_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmPropertiesAsync("TestVM")).ReturnsAsync((VmPropertiesDto?)null);

        // Act
        var result = await _controller.GetVmProperties("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetVmProperties_VmNotFoundViaException_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmPropertiesAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.GetVmProperties("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetVmProperties_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmPropertiesAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.GetVmProperties("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ListSnapshots

    [Fact]
    public async Task ListVmSnapshots_Success_ReturnsOkWithSnapshots()
    {
        // Arrange
        var snapshots = new List<VmSnapshotDto>
        {
            new VmSnapshotDto { Id = "snap1", Name = "Snapshot 1" },
            new VmSnapshotDto { Id = "snap2", Name = "Snapshot 2" }
        };
        _vmProviderMock.Setup(x => x.ListSnapshotsAsync("TestVM")).ReturnsAsync(snapshots);

        // Act
        var result = await _controller.ListVmSnapshots("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedSnapshots = Assert.IsType<List<VmSnapshotDto>>(okResult.Value);
        Assert.Equal(2, returnedSnapshots.Count);
    }

    [Fact]
    public async Task ListVmSnapshots_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ListSnapshotsAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.ListVmSnapshots("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ListVmSnapshots_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ListSnapshotsAsync("TestVM"))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.ListVmSnapshots("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task ListVmSnapshots_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ListSnapshotsAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.ListVmSnapshots("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region CreateSnapshot

    [Fact]
    public async Task CreateVmSnapshot_Success_ReturnsOkWithSnapshotInfo()
    {
        // Arrange
        var request = new CreateSnapshotRequest { SnapshotName = "MySnapshot" };
        _vmProviderMock.Setup(x => x.CreateSnapshotAsync("TestVM", "MySnapshot")).ReturnsAsync("snap-id-123");

        // Act
        var result = await _controller.CreateVmSnapshot("TestVM", request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateVmSnapshot_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateSnapshotRequest { SnapshotName = "MySnapshot" };
        _vmProviderMock.Setup(x => x.CreateSnapshotAsync("TestVM", "MySnapshot"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.CreateVmSnapshot("TestVM", request);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task CreateVmSnapshot_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        var request = new CreateSnapshotRequest { SnapshotName = "MySnapshot" };
        _vmProviderMock.Setup(x => x.CreateSnapshotAsync("TestVM", "MySnapshot"))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.CreateVmSnapshot("TestVM", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task CreateVmSnapshot_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        var request = new CreateSnapshotRequest { SnapshotName = "MySnapshot" };
        _vmProviderMock.Setup(x => x.CreateSnapshotAsync("TestVM", "MySnapshot"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.CreateVmSnapshot("TestVM", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region DeleteSnapshot

    [Fact]
    public async Task DeleteVmSnapshot_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.DeleteSnapshotAsync("TestVM", "snap1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteVmSnapshot("TestVM", "snap1");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task DeleteVmSnapshot_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.DeleteSnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.DeleteVmSnapshot("TestVM", "snap1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteVmSnapshot_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.DeleteSnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.DeleteVmSnapshot("TestVM", "snap1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task DeleteVmSnapshot_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.DeleteSnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.DeleteVmSnapshot("TestVM", "snap1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region ApplySnapshot (RevertVmToSnapshot)

    [Fact]
    public async Task RevertVmToSnapshot_Success_ReturnsOk()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ApplySnapshotAsync("TestVM", "snap1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RevertVmToSnapshot("TestVM", "snap1");

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task RevertVmToSnapshot_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ApplySnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.RevertVmToSnapshot("TestVM", "snap1");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RevertVmToSnapshot_NotSupported_ReturnsStatusCode501()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ApplySnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new NotSupportedException("Not supported"));

        // Act
        var result = await _controller.RevertVmToSnapshot("TestVM", "snap1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, statusResult.StatusCode);
    }

    [Fact]
    public async Task RevertVmToSnapshot_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ApplySnapshotAsync("TestVM", "snap1"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.RevertVmToSnapshot("TestVM", "snap1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region GetConsoleInfo

    [Fact]
    public async Task GetConsoleInfo_Success_ReturnsOkWithConsoleInfo()
    {
        // Arrange
        var consoleInfo = new ConsoleInfoDto { Type = "rdp", Host = "localhost", Port = 2179 };
        _vmProviderMock.Setup(x => x.GetConsoleInfoAsync("TestVM")).ReturnsAsync(consoleInfo);

        // Act
        var result = await _controller.GetConsoleInfo("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedInfo = Assert.IsType<ConsoleInfoDto>(okResult.Value);
        Assert.Equal("rdp", returnedInfo.Type);
        Assert.Equal(2179, returnedInfo.Port);
    }

    [Fact]
    public async Task GetConsoleInfo_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetConsoleInfoAsync("TestVM")).ReturnsAsync((ConsoleInfoDto?)null);

        // Act
        var result = await _controller.GetConsoleInfo("TestVM");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetConsoleInfo_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetConsoleInfoAsync("TestVM"))
            .ThrowsAsync(new Exception("Console error"));

        // Act
        var result = await _controller.GetConsoleInfo("TestVM");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region BulkStart

    [Fact]
    public async Task BulkStartVms_Success_ReturnsOkWithResult()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string> { "VM1", "VM2" } };
        var bulkResult = new BulkOperationResultDto
        {
            TotalCount = 2,
            SuccessCount = 2,
            FailureCount = 0,
            Results = new List<BulkOperationItemResult>
            {
                new BulkOperationItemResult { VmName = "VM1", Success = true },
                new BulkOperationItemResult { VmName = "VM2", Success = true }
            }
        };
        _vmProviderMock.Setup(x => x.BulkStartAsync(It.IsAny<string[]>())).ReturnsAsync(bulkResult);

        // Act
        var result = await _controller.BulkStartVms(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedResult = Assert.IsType<BulkOperationResultDto>(okResult.Value);
        Assert.Equal(2, returnedResult.TotalCount);
        Assert.Equal(2, returnedResult.SuccessCount);
    }

    [Fact]
    public async Task BulkStartVms_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.BulkStartVms(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BulkStartVms_EmptyVmIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string>() };

        // Act
        var result = await _controller.BulkStartVms(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region BulkStop

    [Fact]
    public async Task BulkStopVms_Success_ReturnsOkWithResult()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string> { "VM1", "VM2" } };
        var bulkResult = new BulkOperationResultDto
        {
            TotalCount = 2,
            SuccessCount = 2,
            FailureCount = 0
        };
        _vmProviderMock.Setup(x => x.BulkStopAsync(It.IsAny<string[]>())).ReturnsAsync(bulkResult);

        // Act
        var result = await _controller.BulkStopVms(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task BulkStopVms_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.BulkStopVms(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BulkStopVms_EmptyVmIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string>() };

        // Act
        var result = await _controller.BulkStopVms(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region BulkShutdown

    [Fact]
    public async Task BulkShutdownVms_Success_ReturnsOkWithResult()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string> { "VM1" } };
        var bulkResult = new BulkOperationResultDto
        {
            TotalCount = 1,
            SuccessCount = 1,
            FailureCount = 0
        };
        _vmProviderMock.Setup(x => x.BulkShutdownAsync(It.IsAny<string[]>())).ReturnsAsync(bulkResult);

        // Act
        var result = await _controller.BulkShutdownVms(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task BulkShutdownVms_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.BulkShutdownVms(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BulkShutdownVms_EmptyVmIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string>() };

        // Act
        var result = await _controller.BulkShutdownVms(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region BulkTerminate

    [Fact]
    public async Task BulkTerminateVms_Success_ReturnsOkWithResult()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string> { "VM1", "VM2", "VM3" } };
        var bulkResult = new BulkOperationResultDto
        {
            TotalCount = 3,
            SuccessCount = 2,
            FailureCount = 1
        };
        _vmProviderMock.Setup(x => x.BulkTerminateAsync(It.IsAny<string[]>())).ReturnsAsync(bulkResult);

        // Act
        var result = await _controller.BulkTerminateVms(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task BulkTerminateVms_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.BulkTerminateVms(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task BulkTerminateVms_EmptyVmIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new BulkVmOperationRequest { VmIds = new List<string>() };

        // Act
        var result = await _controller.BulkTerminateVms(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region GetStorageDevices

    [Fact]
    public async Task GetVmStorageDevices_Success_ReturnsOkWithDevices()
    {
        // Arrange
        var devices = new List<StorageDeviceDto>
        {
            new StorageDeviceDto { Id = "dev1", Name = "Hard Drive", Type = "VirtualHardDisk" },
            new StorageDeviceDto { Id = "dev2", Name = "DVD Drive", Type = "VirtualDVD" }
        };
        _storageProviderMock.Setup(x => x.GetVmStorageDevicesAsync("TestVM")).ReturnsAsync(devices);

        // Act
        var result = await _controller.GetVmStorageDevices("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedDevices = Assert.IsType<List<StorageDeviceDto>>(okResult.Value);
        Assert.Equal(2, returnedDevices.Count);
    }

    [Fact]
    public async Task GetVmStorageDevices_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.GetVmStorageDevicesAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.GetVmStorageDevices("TestVM");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetVmStorageDevices_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.GetVmStorageDevicesAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.GetVmStorageDevices("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region AddVmStorageDevice

    [Fact]
    public async Task AddVmStorageDevice_Success_ReturnsOkWithMessage()
    {
        // Arrange
        var spec = new AddStorageDeviceSpec { Type = "VirtualHardDisk", Path = @"C:\VMs\disk.vhdx" };
        _storageProviderMock.Setup(x => x.AddStorageDeviceToVmAsync("TestVM", spec)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AddVmStorageDevice("TestVM", spec);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task AddVmStorageDevice_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var spec = new AddStorageDeviceSpec { Type = "VirtualHardDisk" };
        _storageProviderMock.Setup(x => x.AddStorageDeviceToVmAsync("TestVM", spec))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.AddVmStorageDevice("TestVM", spec);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddVmStorageDevice_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        var spec = new AddStorageDeviceSpec { Type = "VirtualHardDisk" };
        _storageProviderMock.Setup(x => x.AddStorageDeviceToVmAsync("TestVM", spec))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.AddVmStorageDevice("TestVM", spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region RemoveVmStorageDevice

    [Fact]
    public async Task RemoveVmStorageDevice_Success_ReturnsOkWithMessage()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.RemoveStorageDeviceFromVmAsync("TestVM", "dev1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveVmStorageDevice("TestVM", "dev1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task RemoveVmStorageDevice_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.RemoveStorageDeviceFromVmAsync("TestVM", "dev1"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.RemoveVmStorageDevice("TestVM", "dev1");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RemoveVmStorageDevice_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.RemoveStorageDeviceFromVmAsync("TestVM", "dev1"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.RemoveVmStorageDevice("TestVM", "dev1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region GetVmStorageControllers

    [Fact]
    public async Task GetVmStorageControllers_Success_ReturnsOkWithControllers()
    {
        // Arrange
        var controllers = new List<StorageControllerDto>
        {
            new StorageControllerDto { Id = "ctrl1", Name = "IDE Controller", Type = "IDE", ControllerNumber = 0 },
            new StorageControllerDto { Id = "ctrl2", Name = "SCSI Controller", Type = "SCSI", ControllerNumber = 0 }
        };
        _storageProviderMock.Setup(x => x.GetVmStorageControllersAsync("TestVM")).ReturnsAsync(controllers);

        // Act
        var result = await _controller.GetVmStorageControllers("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedControllers = Assert.IsType<List<StorageControllerDto>>(okResult.Value);
        Assert.Equal(2, returnedControllers.Count);
    }

    [Fact]
    public async Task GetVmStorageControllers_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.GetVmStorageControllersAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.GetVmStorageControllers("TestVM");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetVmStorageControllers_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.GetVmStorageControllersAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.GetVmStorageControllers("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region Migrate

    [Fact]
    public async Task MigrateVm_Success_ReturnsAccepted()
    {
        // Arrange
        var request = new MigrateRequest { DestinationHost = "host2.local", Live = true, Storage = false };
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };
        var migrationResult = new MigrationResultDto { Success = true, JobId = "job-123", Message = "Migration initiated" };

        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _migrationProviderMock.Setup(x => x.MigrateVmAsync("TestVM", "host2.local", true, false))
            .ReturnsAsync(migrationResult);

        // Act
        var result = await _controller.MigrateVm("TestVM", request);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, acceptedResult.StatusCode);
    }

    [Fact]
    public async Task MigrateVm_NoDestinationHost_ReturnsBadRequest()
    {
        // Arrange
        var request = new MigrateRequest { DestinationHost = "" };

        // Act
        var result = await _controller.MigrateVm("TestVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MigrateVm_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.MigrateVm("TestVM", null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MigrateVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new MigrateRequest { DestinationHost = "host2.local", Live = true };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.MigrateVm("TestVM", request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MigrateVm_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new MigrateRequest { DestinationHost = "host2.local", Live = true };
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };

        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _migrationProviderMock.Setup(x => x.MigrateVmAsync("TestVM", "host2.local", true, false))
            .ThrowsAsync(new Exception("Migration failed"));

        // Act
        var result = await _controller.MigrateVm("TestVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region MigrateVmStorage

    [Fact]
    public async Task MigrateVmStorage_Success_ReturnsAccepted()
    {
        // Arrange
        var request = new MigrateStorageRequest { DestinationPath = @"D:\VMs\TestVM" };
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };
        var migrationResult = new MigrationResultDto { Success = true, JobId = "job-456", Message = "Storage migration initiated" };

        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _migrationProviderMock.Setup(x => x.MigrateVmAsync("TestVM", @"D:\VMs\TestVM", false, true))
            .ReturnsAsync(migrationResult);

        // Act
        var result = await _controller.MigrateVmStorage("TestVM", request);

        // Assert
        var acceptedResult = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(202, acceptedResult.StatusCode);
    }

    [Fact]
    public async Task MigrateVmStorage_NoDestinationPath_ReturnsBadRequest()
    {
        // Arrange
        var request = new MigrateStorageRequest { DestinationPath = "" };

        // Act
        var result = await _controller.MigrateVmStorage("TestVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MigrateVmStorage_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new MigrateStorageRequest { DestinationPath = @"D:\VMs" };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.MigrateVmStorage("TestVM", request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MigrateVmStorage_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new MigrateStorageRequest { DestinationPath = @"D:\VMs" };
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };

        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _migrationProviderMock.Setup(x => x.MigrateVmAsync("TestVM", @"D:\VMs", false, true))
            .ThrowsAsync(new Exception("Migration failed"));

        // Act
        var result = await _controller.MigrateVmStorage("TestVM", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region GetVmUsage (VmUsageMetrics)

    [Fact]
    public async Task GetVmUsageMetrics_Success_ReturnsOkWithUsageSummary()
    {
        // Arrange
        var vmUsage = new VmUsageDto
        {
            CpuUsagePercent = 45.0,
            MemoryAssignedMB = 4096,
            MemoryDemandMB = 2048,
            MemoryUsagePercent = 50.0
        };
        var diskMetrics = new DiskMetricsDto
        {
            ReadBytesPerSec = 1000,
            WriteBytesPerSec = 500,
            ReadOperationsPerSec = 100,
            WriteOperationsPerSec = 50
        };
        var networkMetrics = new NetworkMetricsDto
        {
            BytesSentPerSec = 2000,
            BytesReceivedPerSec = 3000
        };

        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM")).ReturnsAsync(vmUsage);
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync(diskMetrics);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync(networkMetrics);

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<VmUsageSummary>(okResult.Value);
        Assert.Equal(45.0, usage.Cpu.UsagePercent);
        Assert.Equal(4096, usage.Memory.AssignedMB);
        Assert.Equal(50.0, usage.Memory.UsagePercent);
        Assert.Single(usage.Disks);
        Assert.Single(usage.Networks);
    }

    [Fact]
    public async Task GetVmUsageMetrics_NullDiskAndNetworkMetrics_ReturnsOkWithEmptyLists()
    {
        // Arrange
        var vmUsage = new VmUsageDto
        {
            CpuUsagePercent = 10.0,
            MemoryAssignedMB = 2048,
            MemoryDemandMB = 512,
            MemoryUsagePercent = 25.0
        };

        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM")).ReturnsAsync(vmUsage);
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync((DiskMetricsDto?)null);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync((NetworkMetricsDto?)null);

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<VmUsageSummary>(okResult.Value);
        Assert.Empty(usage.Disks);
        Assert.Empty(usage.Networks);
    }

    [Fact]
    public async Task GetVmUsageMetrics_MemoryHealthy_StatusIsHealthy()
    {
        // Arrange
        var vmUsage = new VmUsageDto
        {
            CpuUsagePercent = 10.0,
            MemoryAssignedMB = 2048,
            MemoryDemandMB = 512,
            MemoryUsagePercent = 50.0
        };

        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM")).ReturnsAsync(vmUsage);
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync((DiskMetricsDto?)null);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync((NetworkMetricsDto?)null);

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<VmUsageSummary>(okResult.Value);
        Assert.Equal("Healthy", usage.Memory.Status);
    }

    [Fact]
    public async Task GetVmUsageMetrics_MemoryWarning_StatusIsWarning()
    {
        // Arrange
        var vmUsage = new VmUsageDto
        {
            CpuUsagePercent = 10.0,
            MemoryAssignedMB = 2048,
            MemoryDemandMB = 1536,
            MemoryUsagePercent = 75.0
        };

        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM")).ReturnsAsync(vmUsage);
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync((DiskMetricsDto?)null);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync((NetworkMetricsDto?)null);

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<VmUsageSummary>(okResult.Value);
        Assert.Equal("Warning", usage.Memory.Status);
    }

    [Fact]
    public async Task GetVmUsageMetrics_MemoryCritical_StatusIsCritical()
    {
        // Arrange
        var vmUsage = new VmUsageDto
        {
            CpuUsagePercent = 10.0,
            MemoryAssignedMB = 2048,
            MemoryDemandMB = 1900,
            MemoryUsagePercent = 95.0
        };

        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM")).ReturnsAsync(vmUsage);
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync((DiskMetricsDto?)null);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync((NetworkMetricsDto?)null);

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var usage = Assert.IsType<VmUsageSummary>(okResult.Value);
        Assert.Equal("Critical", usage.Memory.Status);
    }

    [Fact]
    public async Task GetVmUsageMetrics_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM"))
            .ThrowsAsync(new InvalidOperationException("VM not found"));

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetVmUsageMetrics_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM"))
            .ThrowsAsync(new Exception("Metrics error"));

        // Act
        var result = await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region GetBootOrder

    [Fact]
    public async Task GetBootOrder_VmWithBootOrder_ReturnsOkWithBootOrder()
    {
        // Arrange
        var bootOrder = new string[] { "HardDrive", "Network", "DVD" };
        var vm = new VmDetailsDto
        {
            Id = "1",
            Name = "TestVM",
            State = "Off",
            ExtendedProperties = new Dictionary<string, object> { ["BootOrder"] = bootOrder }
        };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);

        // Act
        var result = await _controller.GetBootOrder("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetBootOrder_VmWithoutBootOrder_ReturnsOkWithDefaults()
    {
        // Arrange
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Off" };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);

        // Act
        var result = await _controller.GetBootOrder("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task GetBootOrder_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.GetBootOrder("TestVM");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetBootOrder_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.GetBootOrder("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region SetBootOrder

    [Fact]
    public async Task SetBootOrder_Success_ReturnsOk()
    {
        // Arrange
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Off" };
        var bootOrder = new string[] { "HardDrive", "Network" };
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _vmProviderMock.Setup(x => x.ConfigureVmAsync("TestVM", It.IsAny<VmConfigurationSpec>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.SetBootOrder("TestVM", bootOrder);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task SetBootOrder_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.SetBootOrder("TestVM", new[] { "HardDrive" });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task SetBootOrder_ProviderThrows_ReturnsStatusCode500()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM"))
            .ThrowsAsync(new Exception("Error"));

        // Act
        var result = await _controller.SetBootOrder("TestVM", new[] { "HardDrive" });

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    #endregion

    #region GetVmTemplates

    [Fact]
    public void GetVmTemplates_ReturnsOkWithTemplates()
    {
        // Arrange & Act
        var result = _controller.GetVmTemplates();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var templates = Assert.IsType<List<VmTemplateConfiguration>>(okResult.Value);
        Assert.Equal(3, templates.Count);
    }

    #endregion

    #region CreateVmFromTemplate

    [Fact]
    public async Task CreateVmFromTemplate_ValidRequest_ReturnsContent()
    {
        // Arrange
        var request = new CreateVmFromTemplateRequest
        {
            Id = "vm-123",
            Name = "TemplateVM",
            Template = VmTemplateType.Development
        };
        _vmProviderMock.Setup(x => x.CreateVmAsync(It.IsAny<CreateVmSpec>())).ReturnsAsync("{\"id\":\"vm-123\"}");

        // Act
        var result = await _controller.CreateVmFromTemplate(request);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Fact]
    public async Task CreateVmFromTemplate_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.CreateVmFromTemplate(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmFromTemplate_EmptyId_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateVmFromTemplateRequest
        {
            Id = "",
            Name = "TemplateVM",
            Template = VmTemplateType.Development
        };

        // Act
        var result = await _controller.CreateVmFromTemplate(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmFromTemplate_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateVmFromTemplateRequest
        {
            Id = "vm-123",
            Name = "",
            Template = VmTemplateType.Development
        };

        // Act
        var result = await _controller.CreateVmFromTemplate(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmFromTemplate_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateVmFromTemplateRequest
        {
            Id = "vm-123",
            Name = "TemplateVM",
            Template = VmTemplateType.Development
        };
        _vmProviderMock.Setup(x => x.CreateVmAsync(It.IsAny<CreateVmSpec>()))
            .ThrowsAsync(new Exception("Create failed"));

        // Act
        var result = await _controller.CreateVmFromTemplate(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region CreateTemplateFromVm

    [Fact]
    public async Task CreateTemplateFromVm_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new CreateTemplateFromVmRequest
        {
            SourceVmName = "SourceVM",
            TemplateName = "MyTemplate",
            Description = "Test template",
            Category = VmTemplateType.Production
        };
        var sourceVm = new VmDetailsDto
        {
            Id = "1",
            Name = "SourceVM",
            CpuCount = 4,
            MemoryMB = 8192,
            Generation = 2
        };
        _vmProviderMock.Setup(x => x.GetVmAsync("SourceVM")).ReturnsAsync(sourceVm);

        // Act
        var result = await _controller.CreateTemplateFromVm(request);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.NotNull(createdResult.Value);
    }

    [Fact]
    public async Task CreateTemplateFromVm_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.CreateTemplateFromVm(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTemplateFromVm_EmptySourceVmName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTemplateFromVmRequest
        {
            SourceVmName = "",
            TemplateName = "MyTemplate"
        };

        // Act
        var result = await _controller.CreateTemplateFromVm(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTemplateFromVm_EmptyTemplateName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTemplateFromVmRequest
        {
            SourceVmName = "SourceVM",
            TemplateName = ""
        };

        // Act
        var result = await _controller.CreateTemplateFromVm(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTemplateFromVm_VmNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new CreateTemplateFromVmRequest
        {
            SourceVmName = "NonExistent",
            TemplateName = "MyTemplate"
        };
        _vmProviderMock.Setup(x => x.GetVmAsync("NonExistent")).ReturnsAsync((VmDetailsDto?)null);

        // Act
        var result = await _controller.CreateTemplateFromVm(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region GetVmWizardRecommendations

    [Fact]
    public void GetVmWizardRecommendations_ValidRequest_ReturnsOkWithRecommendations()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "WizardVM",
            WorkloadType = VmWorkloadType.Development,
            ResourceLevel = VmResourceLevel.Medium
        };

        // Act
        var result = _controller.GetVmWizardRecommendations(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VmWizardResponse>(okResult.Value);
        Assert.NotNull(response.RecommendedConfiguration);
    }

    [Fact]
    public void GetVmWizardRecommendations_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = _controller.GetVmWizardRecommendations(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetVmWizardRecommendations_ContainerWorkload_UsesLightweightTemplate()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "ContainerVM",
            WorkloadType = VmWorkloadType.Container,
            ResourceLevel = VmResourceLevel.Medium
        };

        // Act
        var result = _controller.GetVmWizardRecommendations(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VmWizardResponse>(okResult.Value);
        Assert.Equal(VmTemplateType.Lightweight, response.TemplateUsed);
        Assert.Equal(VmCreationMode.HCS, response.BackendSelected);
    }

    [Fact]
    public void GetVmWizardRecommendations_WebServerWorkload_UsesProductionTemplate()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "WebServerVM",
            WorkloadType = VmWorkloadType.WebServer,
            ResourceLevel = VmResourceLevel.Medium
        };

        // Act
        var result = _controller.GetVmWizardRecommendations(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<VmWizardResponse>(okResult.Value);
        Assert.Equal(VmTemplateType.Production, response.TemplateUsed);
    }

    #endregion

    #region CreateVmViaWizard

    [Fact]
    public async Task CreateVmViaWizard_ValidRequest_ReturnsContent()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "WizardVM",
            WorkloadType = VmWorkloadType.Development,
            ResourceLevel = VmResourceLevel.Medium
        };
        _vmProviderMock.Setup(x => x.CreateVmAsync(It.IsAny<CreateVmSpec>())).ReturnsAsync("{\"id\":\"vm-123\"}");

        // Act
        var result = await _controller.CreateVmViaWizard(request);

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("application/json", contentResult.ContentType);
    }

    [Fact]
    public async Task CreateVmViaWizard_NullRequest_ReturnsBadRequest()
    {
        // Arrange & Act
        var result = await _controller.CreateVmViaWizard(null!);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmViaWizard_EmptyId_ReturnsBadRequest()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "",
            Name = "WizardVM",
            WorkloadType = VmWorkloadType.Development
        };

        // Act
        var result = await _controller.CreateVmViaWizard(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmViaWizard_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "",
            WorkloadType = VmWorkloadType.Development
        };

        // Act
        var result = await _controller.CreateVmViaWizard(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateVmViaWizard_ProviderThrows_ReturnsBadRequest()
    {
        // Arrange
        var request = new VmWizardRequest
        {
            Id = "vm-123",
            Name = "WizardVM",
            WorkloadType = VmWorkloadType.Development,
            ResourceLevel = VmResourceLevel.Medium
        };
        _vmProviderMock.Setup(x => x.CreateVmAsync(It.IsAny<CreateVmSpec>()))
            .ThrowsAsync(new Exception("Create failed"));

        // Act
        var result = await _controller.CreateVmViaWizard(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region Provider Verification

    [Fact]
    public async Task StartVm_CallsCorrectProviderMethod()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StartVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        await _controller.StartVm("TestVM");

        // Assert
        _vmProviderMock.Verify(x => x.StartVmAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task StopVm_CallsCorrectProviderMethod()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        await _controller.StopVm("TestVM");

        // Assert
        _vmProviderMock.Verify(x => x.StopVmAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task ShutdownVm_CallsCorrectProviderMethod()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.ShutdownVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        await _controller.ShutdownVm("TestVM");

        // Assert
        _vmProviderMock.Verify(x => x.ShutdownVmAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task TerminateVm_CallsStopVmAsync()
    {
        // Arrange
        _vmProviderMock.Setup(x => x.StopVmAsync("TestVM")).Returns(Task.CompletedTask);

        // Act
        await _controller.TerminateVm("TestVM");

        // Assert
        _vmProviderMock.Verify(x => x.StopVmAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task GetVmStorageDevices_CallsStorageProvider()
    {
        // Arrange
        _storageProviderMock.Setup(x => x.GetVmStorageDevicesAsync("TestVM"))
            .ReturnsAsync(new List<StorageDeviceDto>());

        // Act
        await _controller.GetVmStorageDevices("TestVM");

        // Assert
        _storageProviderMock.Verify(x => x.GetVmStorageDevicesAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task GetVmUsageMetrics_CallsMetricsProvider()
    {
        // Arrange
        _metricsProviderMock.Setup(x => x.GetVmUsageAsync("TestVM"))
            .ReturnsAsync(new VmUsageDto { CpuUsagePercent = 10.0, MemoryUsagePercent = 50.0 });
        _metricsProviderMock.Setup(x => x.GetDiskMetricsAsync("TestVM")).ReturnsAsync((DiskMetricsDto?)null);
        _metricsProviderMock.Setup(x => x.GetNetworkMetricsAsync("TestVM")).ReturnsAsync((NetworkMetricsDto?)null);

        // Act
        await _controller.GetVmUsageMetrics("TestVM");

        // Assert
        _metricsProviderMock.Verify(x => x.GetVmUsageAsync("TestVM"), Times.Once);
        _metricsProviderMock.Verify(x => x.GetDiskMetricsAsync("TestVM"), Times.Once);
        _metricsProviderMock.Verify(x => x.GetNetworkMetricsAsync("TestVM"), Times.Once);
    }

    [Fact]
    public async Task MigrateVm_CallsMigrationProvider()
    {
        // Arrange
        var request = new MigrateRequest { DestinationHost = "host2.local", Live = true, Storage = true };
        var vm = new VmDetailsDto { Id = "1", Name = "TestVM", State = "Running" };
        var migrationResult = new MigrationResultDto { Success = true, JobId = "job-1" };

        _vmProviderMock.Setup(x => x.GetVmAsync("TestVM")).ReturnsAsync(vm);
        _migrationProviderMock.Setup(x => x.MigrateVmAsync("TestVM", "host2.local", true, true))
            .ReturnsAsync(migrationResult);

        // Act
        await _controller.MigrateVm("TestVM", request);

        // Assert
        _migrationProviderMock.Verify(x => x.MigrateVmAsync("TestVM", "host2.local", true, true), Times.Once);
    }

    #endregion
}
