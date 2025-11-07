using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using HyperV.HyperV.Contracts.Services;
using HyperV.Tests.Helpers;
using Moq;
using FluentAssertions;

namespace HyperV.Tests.Controllers;

/// <summary>
/// Testy jednostkowe dla StorageController
/// </summary>
public class StorageControllerTests : IDisposable
{
    private readonly StorageController _controller;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<IImageManagementService> _mockImageService;
    private readonly Mock<IStorageQoSService> _mockStorageQoSService;

    public StorageControllerTests()
    {
        _mockStorageService = ServiceMocks.ConfigureStorageServiceMock();
        _mockImageService = ServiceMocks.ConfigureImageManagementServiceMock();
        _mockStorageQoSService = new Mock<IStorageQoSService>();

        _controller = new StorageController(
            _mockStorageService.Object,
            _mockImageService.Object,
            _mockStorageQoSService.Object
        );
    }

    #region Basic VHD Operations Tests

    [Fact]
    public void CreateVhd_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateVhdRequest();
        _mockStorageService.Setup(x => x.CreateVirtualHardDisk(request))
            .Verifiable();

        // Act
        var result = _controller.CreateVhd(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
        
        _mockStorageService.Verify(x => x.CreateVirtualHardDisk(request), Times.Once);
    }

    [Fact]
    public void CreateVhd_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        var request = TestDataGenerator.CreateVhdRequest();
        _mockStorageService.Setup(x => x.CreateVirtualHardDisk(request))
            .Throws(new Exception("VHD creation failed"));

        // Act
        var result = _controller.CreateVhd(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public void AttachVhd_WithValidParameters_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var vhdPath = TestDataGenerator.GenerateVhdPath();
        _mockStorageService.Setup(x => x.AttachVirtualHardDisk(vmName, vhdPath))
            .Verifiable();

        // Act
        var result = _controller.AttachVhd(vmName, vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorageService.Verify(x => x.AttachVirtualHardDisk(vmName, vhdPath), Times.Once);
    }

    [Fact]
    public void DetachVhd_WithValidParameters_ShouldReturnOkResult()
    {
        // Arrange
        var vmName = TestDataGenerator.GenerateVmName();
        var vhdPath = TestDataGenerator.GenerateVhdPath();
        _mockStorageService.Setup(x => x.DetachVirtualHardDisk(vmName, vhdPath))
            .Verifiable();

        // Act
        var result = _controller.DetachVhd(vmName, vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorageService.Verify(x => x.DetachVirtualHardDisk(vmName, vhdPath), Times.Once);
    }

    [Fact]
    public void ResizeVhd_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateResizeVhdRequest();
        _mockStorageService.Setup(x => x.ResizeVirtualHardDisk(request))
            .Verifiable();

        // Act
        var result = _controller.ResizeVhd(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mockStorageService.Verify(x => x.ResizeVirtualHardDisk(request), Times.Once);
    }

    #endregion

    #region Advanced VHD Operations Tests

    [Fact]
    public async Task GetVhdMetadata_WithValidPath_ShouldReturnOkResult()
    {
        // Arrange
        var vhdPath = TestDataGenerator.GenerateVhdPath();
        var mockMetadata = new VhdMetadata
        {
            Path = vhdPath,
            Format = "VHDX",
            UniqueId = Guid.NewGuid(),
            IsAttached = false
        };
        _mockStorageService.Setup(x => x.GetVhdMetadataAsync(vhdPath))
            .ReturnsAsync(mockMetadata);

        // Act
        var result = await _controller.GetVhdMetadata(vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(mockMetadata);
    }

    [Fact]
    public async Task CompactVhd_WithValidRequest_ShouldReturnAccepted()
    {
        // Arrange
        var request = new CompactVhdRequest { Path = TestDataGenerator.GenerateVhdPath() };

        // Act
        var result = await _controller.CompactVhd(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(202);
    }

    [Fact]
    public async Task ConvertVhd_WithValidRequest_ShouldReturnAccepted()
    {
        // Arrange
        var request = TestDataGenerator.CreateConvertVhdRequest();

        // Act
        var result = await _controller.ConvertVhd(request);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(202);
    }

    [Fact]
    public async Task GetVhdSettings_WithValidPath_ShouldReturnOkResult()
    {
        // Arrange
        var vhdPath = TestDataGenerator.GenerateVhdPath();
        var mockSettings = new VirtualHardDiskSettingData
        {
            Path = vhdPath,
            MaxInternalSize = 10737418240, // 10GB
            Format = VirtualDiskFormat.VHDX,
            Type = VirtualDiskType.Dynamic
        };
        _mockImageManagementService.Setup(x => x.GetVirtualHardDiskSettingDataAsync(vhdPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockSettings);

        // Act
        var result = await _controller.GetVhdSettings(vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(mockSettings);
    }

    [Fact]
    public async Task GetVhdState_WithValidPath_ShouldReturnOkResult()
    {
        // Arrange
        var vhdPath = TestDataGenerator.GenerateVhdPath();
        var mockState = new VirtualHardDiskState();
        _mockImageManagementService.Setup(x => x.GetVirtualHardDiskStateAsync(vhdPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockState);

        // Act
        var result = await _controller.GetVhdState(vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(mockState);
    }

    #endregion

    #region Differencing Disk Operations Tests

    [Fact]
    public async Task CreateDifferencingDisk_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateDifferencingDiskRequest();
        // Act
        var result = await _controller.CreateDifferencingDisk(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MergeDifferencingDisk_WithValidRequest_ShouldReturnOkResult()
    {
        // Arrange
        var request = TestDataGenerator.CreateMergeDiskRequest();

        // Act
        var result = await _controller.MergeDifferencingDisk(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Basic Storage Tests

    [Fact]
    public async Task EnableChangeTracking_WithValidPath_ShouldReturnOkResult()
    {
        // Arrange
        var vhdPath = TestDataGenerator.GenerateVhdPath();

        // Act
        var result = await _controller.EnableChangeTracking(vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DisableChangeTracking_WithValidPath_ShouldReturnOkResult()
    {
        // Arrange
        var vhdPath = TestDataGenerator.GenerateVhdPath();

        // Act
        var result = await _controller.DisableChangeTracking(vhdPath);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Storage Extent Operations Tests

    [Fact]
    public async Task GetStorageExtents_ShouldReturnOkResult()
    {
        // Act
        var result = await _controller.GetStorageExtents();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetExtentDependencies_WithValidId_ShouldReturnOkResult()
    {
        // Arrange
        var extentId = "extent-1";

        // Act
        var result = await _controller.GetExtentDependencies(extentId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetControllerDevices_WithValidControllerId_ShouldReturnOkResult()
    {
        // Arrange
        var controllerId = "controller-1";

        // Act
        var result = await _controller.GetControllerDevices(controllerId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
    }

    #endregion

    #endregion

    #region QoS Extension Tests

    [Fact]
    public void TestCreateQoSPolicy_Success()
    {
        // Arrange
        var request = new CreateQoSPolicyRequest { Name = "TestPolicy", MaxIops = 1000, MaxBandwidth = 100 };
        var expectedResult = "{\"policyId\":\"qos-123\",\"status\":\"Created\"}";
        _mockStorageQoSService.Setup(x => x.CreateQoSPolicy(request)).Returns(expectedResult);

        // Act
        var result = _controller.CreateQoSPolicy(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("qos-123");
        _mockStorageQoSService.Verify(x => x.CreateQoSPolicy(request), Times.Once);
    }

    [Fact]
    public void TestCreateQoSPolicy_BadRequest()
    {
        // Arrange
        var request = new CreateQoSPolicyRequest { Name = "TestPolicy", MaxIops = 1000, MaxBandwidth = 100 };
        _mockStorageQoSService.Setup(x => x.CreateQoSPolicy(request))
            .Throws(new InvalidOperationException("Policy creation failed"));

        // Act
        var result = _controller.CreateQoSPolicy(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestDeleteQoSPolicy_OK()
    {
        // Arrange
        var policyId = "qos-123";
        _mockStorageQoSService.Setup(x => x.DeleteQoSPolicy(policyId));

        // Act
        var result = _controller.DeleteQoSPolicy(policyId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockStorageQoSService.Verify(x => x.DeleteQoSPolicy(policyId), Times.Once);
    }

    [Fact]
    public void TestDeleteQoSPolicy_NotFound()
    {
        // Arrange
        var policyId = "qos-123";
        _mockStorageQoSService.Setup(x => x.DeleteQoSPolicy(policyId))
            .Throws(new InvalidOperationException("Policy not found"));

        // Act
        var result = _controller.DeleteQoSPolicy(policyId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void TestGetQoSPolicyInfo_OK()
    {
        // Arrange
        var policyId = "qos-123";
        var expectedInfo = "{\"id\":\"qos-123\",\"name\":\"TestPolicy\",\"maxIops\":1000,\"maxBandwidth\":100,\"status\":\"OK\"}";
        _mockStorageQoSService.Setup(x => x.GetQoSPolicyInfo(policyId)).Returns(expectedInfo);

        // Act
        var result = _controller.GetQoSPolicyInfo(policyId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("OK");
        _mockStorageQoSService.Verify(x => x.GetQoSPolicyInfo(policyId), Times.Once);
    }

    [Fact]
    public void TestGetQoSPolicyInfo_NotFound()
    {
        // Arrange
        var policyId = "qos-123";
        _mockStorageQoSService.Setup(x => x.GetQoSPolicyInfo(policyId))
            .Throws(new InvalidOperationException("Policy not found"));

        // Act
        var result = _controller.GetQoSPolicyInfo(policyId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void TestApplyQoSPolicyToVm_OK()
    {
        // Arrange
        var vmName = "TestVM";
        var policyId = "qos-123";
        _mockStorageQoSService.Setup(x => x.ApplyQoSPolicyToVm(vmName, policyId));

        // Act
        var result = _controller.ApplyQoSPolicyToVm(vmName, policyId);

        // Assert
        result.Should().BeOfType<OkResult>();
        _mockStorageQoSService.Verify(x => x.ApplyQoSPolicyToVm(vmName, policyId), Times.Once);
    }

    [Fact]
    public void TestApplyQoSPolicyToVm_BadRequest()
    {
        // Arrange
        var vmName = "TestVM";
        var policyId = "qos-123";
        _mockStorageQoSService.Setup(x => x.ApplyQoSPolicyToVm(vmName, policyId))
            .Throws(new InvalidOperationException("Apply failed"));

        // Act
        var result = _controller.ApplyQoSPolicyToVm(vmName, policyId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    public void Dispose()
    {
        // Cleanup if needed
    }
}