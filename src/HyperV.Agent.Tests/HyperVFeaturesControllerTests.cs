using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.Contracts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Agent.Tests;

public class HyperVFeaturesControllerTests
{
    private readonly Mock<IReplicationService> _replicationServiceMock;
    private readonly Mock<IStorageQoSService> _storageQoSServiceMock;
    private readonly Mock<IImageManagementService> _imageManagementServiceMock;
    private readonly Mock<ILogger<HyperVFeaturesController>> _loggerMock;
    private readonly HyperVFeaturesController _controller;

    public HyperVFeaturesControllerTests()
    {
        _replicationServiceMock = new Mock<IReplicationService>();
        _storageQoSServiceMock = new Mock<IStorageQoSService>();
        _imageManagementServiceMock = new Mock<IImageManagementService>();
        _loggerMock = new Mock<ILogger<HyperVFeaturesController>>();

        _controller = new HyperVFeaturesController(
            _replicationServiceMock.Object,
            _storageQoSServiceMock.Object,
            _imageManagementServiceMock.Object,
            _loggerMock.Object);
    }

    // ──────────────────── CreateReplicationRelationship ────────────────────

    [Fact]
    public void CreateReplicationRelationship_Success_ReturnsOk()
    {
        // Arrange
        var request = new CreateReplicationRequest
        {
            SourceVm = "TestVM",
            TargetHost = "HV-REPLICA-01",
            AuthMode = "Certificate"
        };
        _replicationServiceMock.Setup(s => s.CreateReplicationRelationship("TestVM", "HV-REPLICA-01", "Certificate"))
            .Returns("{\"relationshipId\":\"rel-1\",\"status\":\"Completed\"}");

        // Act
        var result = _controller.CreateReplicationRelationship(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _replicationServiceMock.Verify(
            s => s.CreateReplicationRelationship("TestVM", "HV-REPLICA-01", "Certificate"), Times.Once);
    }

    [Fact]
    public void CreateReplicationRelationship_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new CreateReplicationRequest
        {
            SourceVm = "TestVM",
            TargetHost = "HV-REPLICA-01"
        };
        _replicationServiceMock.Setup(s => s.CreateReplicationRelationship("TestVM", "HV-REPLICA-01", "Certificate"))
            .Throws(new InvalidOperationException("Replication service not available"));

        // Act
        var result = _controller.CreateReplicationRelationship(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── StartReplication ────────────────────

    [Fact]
    public void StartReplication_Success_ReturnsOk()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.StartReplication("TestVM"));

        // Act
        var result = _controller.StartReplication("TestVM");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _replicationServiceMock.Verify(s => s.StartReplication("TestVM"), Times.Once);
    }

    [Fact]
    public void StartReplication_WhenServiceThrows_Returns500()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.StartReplication("TestVM"))
            .Throws(new Exception("VM not configured for replication"));

        // Act
        var result = _controller.StartReplication("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── InitiateFailover ────────────────────

    [Fact]
    public void InitiateFailover_Success_ReturnsAccepted()
    {
        // Arrange
        var request = new FailoverRequest { Mode = "Planned" };
        _replicationServiceMock.Setup(s => s.InitiateFailover("TestVM", "Planned"))
            .Returns("{\"jobId\":\"job-1\",\"status\":\"Running\"}");

        // Act
        var result = _controller.InitiateFailover("TestVM", request);

        // Assert
        Assert.IsType<AcceptedResult>(result);
    }

    [Fact]
    public void InitiateFailover_WithNullRequest_UsesPlannedMode()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.InitiateFailover("TestVM", "Planned"))
            .Returns("{\"jobId\":\"job-1\",\"status\":\"Running\"}");

        // Act
        var result = _controller.InitiateFailover("TestVM", null);

        // Assert
        Assert.IsType<AcceptedResult>(result);
        _replicationServiceMock.Verify(s => s.InitiateFailover("TestVM", "Planned"), Times.Once);
    }

    [Fact]
    public void InitiateFailover_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new FailoverRequest { Mode = "Live" };
        _replicationServiceMock.Setup(s => s.InitiateFailover("TestVM", "Live"))
            .Throws(new Exception("Failover failed"));

        // Act
        var result = _controller.InitiateFailover("TestVM", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ReverseReplication ────────────────────

    [Fact]
    public void ReverseReplication_Success_ReturnsOk()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.ReverseReplicationRelationship("TestVM"));

        // Act
        var result = _controller.ReverseReplication("TestVM");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _replicationServiceMock.Verify(s => s.ReverseReplicationRelationship("TestVM"), Times.Once);
    }

    [Fact]
    public void ReverseReplication_WhenServiceThrows_Returns500()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.ReverseReplicationRelationship("TestVM"))
            .Throws(new Exception("Reverse failed"));

        // Act
        var result = _controller.ReverseReplication("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetReplicationState ────────────────────

    [Fact]
    public void GetReplicationState_Success_ReturnsOk()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.GetReplicationState("TestVM"))
            .Returns("{\"vmName\":\"TestVM\",\"enabledState\":\"Enabled\",\"replicationHealth\":\"Normal\"}");

        // Act
        var result = _controller.GetReplicationState("TestVM");

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetReplicationState_WhenServiceThrows_Returns500()
    {
        // Arrange
        _replicationServiceMock.Setup(s => s.GetReplicationState("TestVM"))
            .Throws(new Exception("State query failed"));

        // Act
        var result = _controller.GetReplicationState("TestVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── AddAuthorizationEntry ────────────────────

    [Fact]
    public void AddAuthorizationEntry_Success_ReturnsOk()
    {
        // Arrange
        var request = new AuthorizationEntryRequest { Entry = "*.contoso.com" };
        _replicationServiceMock.Setup(s => s.AddAuthorizationEntry("rel-1", "*.contoso.com"));

        // Act
        var result = _controller.AddAuthorizationEntry("rel-1", request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _replicationServiceMock.Verify(s => s.AddAuthorizationEntry("rel-1", "*.contoso.com"), Times.Once);
    }

    [Fact]
    public void AddAuthorizationEntry_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new AuthorizationEntryRequest { Entry = "*.contoso.com" };
        _replicationServiceMock.Setup(s => s.AddAuthorizationEntry("rel-1", "*.contoso.com"))
            .Throws(new Exception("Authorization failed"));

        // Act
        var result = _controller.AddAuthorizationEntry("rel-1", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── CreateQoSPolicy ────────────────────

    [Fact]
    public void CreateQoSPolicy_Success_ReturnsOk()
    {
        // Arrange
        var request = new CreateQoSPolicyRequest
        {
            PolicyId = "gold-tier",
            MaxIops = 10000,
            MaxBandwidth = 500,
            Description = "Gold tier storage policy"
        };
        _storageQoSServiceMock.Setup(s => s.CreateQoSPolicy("gold-tier", 10000u, 500u, "Gold tier storage policy"))
            .Returns("{\"policyId\":\"gold-tier\",\"status\":\"created\"}");

        // Act
        var result = _controller.CreateQoSPolicy(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _storageQoSServiceMock.Verify(
            s => s.CreateQoSPolicy("gold-tier", 10000u, 500u, "Gold tier storage policy"), Times.Once);
    }

    [Fact]
    public void CreateQoSPolicy_WithNullDescription_UsesEmptyString()
    {
        // Arrange
        var request = new CreateQoSPolicyRequest
        {
            PolicyId = "basic",
            MaxIops = 1000,
            MaxBandwidth = 100,
            Description = null
        };
        _storageQoSServiceMock.Setup(s => s.CreateQoSPolicy("basic", 1000u, 100u, ""))
            .Returns("{\"policyId\":\"basic\"}");

        // Act
        var result = _controller.CreateQoSPolicy(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _storageQoSServiceMock.Verify(s => s.CreateQoSPolicy("basic", 1000u, 100u, ""), Times.Once);
    }

    [Fact]
    public void CreateQoSPolicy_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new CreateQoSPolicyRequest
        {
            PolicyId = "gold-tier",
            MaxIops = 10000,
            MaxBandwidth = 500
        };
        _storageQoSServiceMock.Setup(s => s.CreateQoSPolicy(It.IsAny<string>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
            .Throws(new Exception("Policy creation failed"));

        // Act
        var result = _controller.CreateQoSPolicy(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── DeleteQoSPolicy ────────────────────

    [Fact]
    public void DeleteQoSPolicy_Success_ReturnsOk()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        _storageQoSServiceMock.Setup(s => s.DeleteQoSPolicy(policyId));

        // Act
        var result = _controller.DeleteQoSPolicy(policyId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _storageQoSServiceMock.Verify(s => s.DeleteQoSPolicy(policyId), Times.Once);
    }

    [Fact]
    public void DeleteQoSPolicy_WhenServiceThrows_Returns500()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        _storageQoSServiceMock.Setup(s => s.DeleteQoSPolicy(policyId))
            .Throws(new Exception("Policy not found"));

        // Act
        var result = _controller.DeleteQoSPolicy(policyId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetQoSPolicy ────────────────────

    [Fact]
    public void GetQoSPolicy_Success_ReturnsOk()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        _storageQoSServiceMock.Setup(s => s.GetQoSPolicyInfo(policyId))
            .Returns("{\"policyId\":\"" + policyId + "\",\"maxIops\":10000}");

        // Act
        var result = _controller.GetQoSPolicy(policyId);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetQoSPolicy_WhenServiceThrows_Returns500()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        _storageQoSServiceMock.Setup(s => s.GetQoSPolicyInfo(policyId))
            .Throws(new Exception("Policy query failed"));

        // Act
        var result = _controller.GetQoSPolicy(policyId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ApplyQoSPolicy ────────────────────

    [Fact]
    public void ApplyQoSPolicy_Success_ReturnsOk()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        var request = new ApplyQoSPolicyRequest { PolicyId = policyId };
        _storageQoSServiceMock.Setup(s => s.ApplyQoSPolicyToVm("TestVM", policyId));

        // Act
        var result = _controller.ApplyQoSPolicy("TestVM", request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _storageQoSServiceMock.Verify(s => s.ApplyQoSPolicyToVm("TestVM", policyId), Times.Once);
    }

    [Fact]
    public void ApplyQoSPolicy_WhenServiceThrows_Returns500()
    {
        // Arrange
        var policyId = Guid.NewGuid();
        var request = new ApplyQoSPolicyRequest { PolicyId = policyId };
        _storageQoSServiceMock.Setup(s => s.ApplyQoSPolicyToVm("TestVM", policyId))
            .Throws(new Exception("VM not found"));

        // Act
        var result = _controller.ApplyQoSPolicy("TestVM", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── CompactImage ────────────────────

    [Fact]
    public async Task CompactImage_Success_ReturnsOk()
    {
        // Arrange
        var request = new CompactVhdRequest { Path = @"C:\VMs\disk.vhdx", Mode = VhdCompactMode.Full };
        _imageManagementServiceMock.Setup(s => s.CompactVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.CompactImage(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _imageManagementServiceMock.Verify(
            s => s.CompactVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompactImage_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new CompactVhdRequest { Path = @"C:\VMs\disk.vhdx", Mode = VhdCompactMode.Full };
        _imageManagementServiceMock.Setup(s => s.CompactVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Disk in use"));

        // Act
        var result = await _controller.CompactImage(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── MergeImage ────────────────────

    [Fact]
    public async Task MergeImage_Success_ReturnsOk()
    {
        // Arrange
        var request = new MergeDiskRequest
        {
            ChildPath = @"C:\VMs\child.avhdx",
            DestinationPath = @"C:\VMs\parent.vhdx",
            MergeDepth = 1
        };
        _imageManagementServiceMock.Setup(s => s.MergeVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.MergeImage(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task MergeImage_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new MergeDiskRequest
        {
            ChildPath = @"C:\VMs\child.avhdx",
            DestinationPath = @"C:\VMs\parent.vhdx"
        };
        _imageManagementServiceMock.Setup(s => s.MergeVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Merge failed: disk locked"));

        // Act
        var result = await _controller.MergeImage(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ConvertImage ────────────────────

    [Fact]
    public async Task ConvertImage_Success_ReturnsOk()
    {
        // Arrange
        var request = new ConvertVhdRequest
        {
            SourcePath = @"C:\VMs\disk.vhd",
            DestinationPath = @"C:\VMs\disk.vhdx",
            TargetFormat = VirtualDiskFormat.VHDX
        };
        _imageManagementServiceMock.Setup(s => s.ConvertVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync("conversion-job-1");

        // Act
        var result = await _controller.ConvertImage(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ConvertImage_WhenServiceThrows_Returns500()
    {
        // Arrange
        var request = new ConvertVhdRequest
        {
            SourcePath = @"C:\VMs\disk.vhd",
            DestinationPath = @"C:\VMs\disk.vhdx",
            TargetFormat = VirtualDiskFormat.VHDX
        };
        _imageManagementServiceMock.Setup(s => s.ConvertVirtualHardDiskAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Source file not found"));

        // Act
        var result = await _controller.ConvertImage(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetImageSettings ────────────────────

    [Fact]
    public async Task GetImageSettings_Success_ReturnsOk()
    {
        // Arrange
        var path = @"C:\VMs\disk.vhdx";
        var settingData = new VirtualHardDiskSettingData
        {
            Path = path,
            Type = VirtualDiskType.Dynamic,
            Format = VirtualDiskFormat.VHDX,
            MaxInternalSize = 107374182400,
            BlockSize = 33554432,
            LogicalSectorSize = 512,
            PhysicalSectorSize = 4096
        };
        _imageManagementServiceMock.Setup(s => s.GetVirtualHardDiskSettingDataAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(settingData);

        // Act
        var result = await _controller.GetImageSettings(path);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<VirtualHardDiskSettingData>(okResult.Value);
        Assert.Equal(path, returned.Path);
        Assert.Equal(VirtualDiskType.Dynamic, returned.Type);
    }

    [Fact]
    public async Task GetImageSettings_WhenServiceThrows_Returns500()
    {
        // Arrange
        var path = @"C:\VMs\nonexistent.vhdx";
        _imageManagementServiceMock.Setup(s => s.GetVirtualHardDiskSettingDataAsync(path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("File not found"));

        // Act
        var result = await _controller.GetImageSettings(path);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetImageState ────────────────────

    [Fact]
    public async Task GetImageState_Success_ReturnsOk()
    {
        // Arrange
        var path = @"C:\VMs\disk.vhdx";
        var state = new VirtualHardDiskState
        {
            InUse = 1,
            Health = 0,
            OperationalStatus = 2,
            InUseBy = 1
        };
        _imageManagementServiceMock.Setup(s => s.GetVirtualHardDiskStateAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var result = await _controller.GetImageState(path);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<VirtualHardDiskState>(okResult.Value);
        Assert.Equal(1, returned.InUse);
    }

    [Fact]
    public async Task GetImageState_WhenServiceThrows_Returns500()
    {
        // Arrange
        var path = @"C:\VMs\nonexistent.vhdx";
        _imageManagementServiceMock.Setup(s => s.GetVirtualHardDiskStateAsync(path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Cannot read disk state"));

        // Act
        var result = await _controller.GetImageState(path);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
