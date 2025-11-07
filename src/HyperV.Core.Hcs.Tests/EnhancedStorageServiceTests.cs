using HyperV.Core.Hcs.Services;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Moq;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Core.Hcs.Tests;

public class EnhancedStorageServiceTests
{
    private readonly Mock<VmService> _vmServiceMock;
    private readonly Mock<ContainerService> _containerServiceMock;
    private readonly EnhancedStorageService _storageService;

    public EnhancedStorageServiceTests()
    {
        _vmServiceMock = new Mock<VmService>();
        _containerServiceMock = new Mock<ContainerService>();
        _storageService = new EnhancedStorageService(_vmServiceMock.Object, _containerServiceMock.Object);
    }

    [Fact]
    public void Constructor_WithNullVmService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EnhancedStorageService(null, _containerServiceMock.Object));
        Assert.Equal("vmService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullContainerService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new EnhancedStorageService(_vmServiceMock.Object, null));
        Assert.Equal("containerService", exception.ParamName);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithNullRequest_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "", Format = "VHDX", Type = "DYNAMIC" };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = "DYNAMIC", MaxInternalSize = 0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("MaxInternalSize must be greater than 0", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithNullFormat_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = null, Type = "DYNAMIC", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("Format cannot be null or empty", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithNullType_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = null, MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("Type cannot be null or empty", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithInvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.invalid", Format = "INVALID", Type = "DYNAMIC", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("Unsupported VHD format: INVALID", exception.Message);
    }

    [Fact]
    public void CreateVirtualHardDisk_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = "INVALID", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.CreateVirtualHardDisk(request));
        Assert.Contains("Unsupported VHD type: INVALID", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithNullRequest_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(null, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "", Format = "VHDX", Type = "DYNAMIC" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = "DYNAMIC", MaxInternalSize = 0 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("MaxInternalSize must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithNullFormat_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = null, Type = "DYNAMIC", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("Format cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithNullType_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = null, MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("Type cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithInvalidFormat_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.invalid", Format = "INVALID", Type = "DYNAMIC", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("Unsupported VHD format: INVALID", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualHardDiskAsync_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVhdRequest { Path = "test.vhdx", Format = "VHDX", Type = "INVALID", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("Unsupported VHD type: INVALID", exception.Message);
    }

    [Fact]
    public void AttachVirtualHardDisk_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.AttachVirtualHardDisk(null, "test.vhdx"));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public void AttachVirtualHardDisk_WithNullVhdPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.AttachVirtualHardDisk("TestVM", null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void AttachVirtualHardDisk_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            _storageService.AttachVirtualHardDisk("TestVM", "nonexistent.vhdx"));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualHardDiskAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.AttachVirtualHardDiskAsync(null, "test.vhdx", CancellationToken.None, null));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualHardDiskAsync_WithNullVhdPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.AttachVirtualHardDiskAsync("TestVM", null, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualHardDiskAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.AttachVirtualHardDiskAsync("TestVM", "nonexistent.vhdx", CancellationToken.None, null));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public void DetachVirtualHardDisk_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.DetachVirtualHardDisk(null, "test.vhdx"));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public void DetachVirtualHardDisk_WithNullVhdPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.DetachVirtualHardDisk("TestVM", null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task DetachVirtualHardDiskAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.DetachVirtualHardDiskAsync(null, "test.vhdx", CancellationToken.None, null));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task DetachVirtualHardDiskAsync_WithNullVhdPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.DetachVirtualHardDiskAsync("TestVM", null, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ResizeVirtualHardDisk_WithNullRequest_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDisk(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ResizeVirtualHardDisk_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDisk(request));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ResizeVirtualHardDisk_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "nonexistent.vhdx", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            _storageService.ResizeVirtualHardDisk(request));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public void ResizeVirtualHardDisk_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "test.vhdx", MaxInternalSize = 0 };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDisk(request));
        Assert.Contains("MaxInternalSize must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task ResizeVirtualHardDiskAsync_WithNullRequest_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDiskAsync(null, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ResizeVirtualHardDiskAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ResizeVirtualHardDiskAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "nonexistent.vhdx", MaxInternalSize = 1024 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.ResizeVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task ResizeVirtualHardDiskAsync_WithZeroSize_ThrowsArgumentException()
    {
        // Arrange
        var request = new ResizeVhdRequest { Path = "test.vhdx", MaxInternalSize = 0 };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ResizeVirtualHardDiskAsync(request, CancellationToken.None, null));
        Assert.Contains("MaxInternalSize must be greater than 0", exception.Message);
    }

    [Fact]
    public async Task CreateDifferencingDiskAsync_WithNullChildPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateDifferencingDiskAsync(null, "parent.vhdx", CancellationToken.None, null));
        Assert.Contains("Child path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateDifferencingDiskAsync_WithNullParentPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateDifferencingDiskAsync("child.vhdx", null, CancellationToken.None, null));
        Assert.Contains("Parent path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CreateDifferencingDiskAsync_WithNonExistentParent_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.CreateDifferencingDiskAsync("child.vhdx", "nonexistent.vhdx", CancellationToken.None, null));
        Assert.Contains("Parent VHD not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task MergeDifferencingDiskAsync_WithNullChildPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.MergeDifferencingDiskAsync(null, 1, CancellationToken.None, null));
        Assert.Contains("Child path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task MergeDifferencingDiskAsync_WithNonExistentChild_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.MergeDifferencingDiskAsync("nonexistent.vhdx", 1, CancellationToken.None, null));
        Assert.Contains("Child VHD not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task GetVhdMetadataAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVhdMetadataAsync(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVhdMetadataAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.GetVhdMetadataAsync("nonexistent.vhdx"));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task SetVhdMetadataAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var update = new VhdMetadataUpdate();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.SetVhdMetadataAsync(null, update));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SetVhdMetadataAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Arrange
        var update = new VhdMetadataUpdate();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.SetVhdMetadataAsync("nonexistent.vhdx", update));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task CompactVhdAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CompactVhdAsync(null, CancellationToken.None, null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task CompactVhdAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.CompactVhdAsync("nonexistent.vhdx", CancellationToken.None, null));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task GetVhdStateAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVhdStateAsync(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVhdStateAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.GetVhdStateAsync("nonexistent.vhdx"));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task ValidateVhdAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storageService.ValidateVhdAsync(null, CancellationToken.None));
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task ValidateVhdAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new VhdValidationRequest { Path = null };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ValidateVhdAsync(request, CancellationToken.None));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ValidateVhdAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Arrange
        var request = new VhdValidationRequest { Path = "nonexistent.vhdx" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.ValidateVhdAsync(request, CancellationToken.None));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task ConvertVhdAsync_WithNullSourcePath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ConvertVhdAsync(null, "dest.vhdx", "VHDX", CancellationToken.None, null));
        Assert.Contains("Source path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ConvertVhdAsync_WithNullDestinationPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.ConvertVhdAsync("source.vhdx", null, "VHDX", CancellationToken.None, null));
        Assert.Contains("Destination path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task ConvertVhdAsync_WithNonExistentSource_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.ConvertVhdAsync("nonexistent.vhdx", "dest.vhdx", "VHDX", CancellationToken.None, null));
        Assert.Contains("Source VHD not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task CreateVirtualFloppyDiskAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storageService.CreateVirtualFloppyDiskAsync(null, CancellationToken.None));
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task CreateVirtualFloppyDiskAsync_WithNullPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new CreateVfdRequest { Path = null };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.CreateVirtualFloppyDiskAsync(request, CancellationToken.None));
        Assert.Contains("VFD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualFloppyDiskAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storageService.AttachVirtualFloppyDiskAsync(null));
    }

    [Fact]
    public async Task AttachVirtualFloppyDiskAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Arrange
        var request = new VfdAttachRequest { VmName = null, VfdPath = "test.vfd" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.AttachVirtualFloppyDiskAsync(request));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualFloppyDiskAsync_WithNullVfdPath_ThrowsArgumentException()
    {
        // Arrange
        var request = new VfdAttachRequest { VmName = "TestVM", VfdPath = null };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.AttachVirtualFloppyDiskAsync(request));
        Assert.Contains("VFD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AttachVirtualFloppyDiskAsync_WithNonExistentVfd_ThrowsFileNotFoundException()
    {
        // Arrange
        var request = new VfdAttachRequest { VmName = "TestVM", VfdPath = "nonexistent.vfd" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.AttachVirtualFloppyDiskAsync(request));
        Assert.Contains("VFD file not found: nonexistent.vfd", exception.Message);
    }

    [Fact]
    public async Task DetachVirtualFloppyDiskAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.DetachVirtualFloppyDiskAsync(null, "test.vfd"));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task DetachVirtualFloppyDiskAsync_WithNullVfdPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.DetachVirtualFloppyDiskAsync("TestVM", null));
        Assert.Contains("VFD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVmStorageDevicesAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVmStorageDevicesAsync(null));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVmStorageControllersAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVmStorageControllersAsync(null));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AddStorageDeviceToVmAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Arrange
        var request = new AddStorageDeviceRequest();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.AddStorageDeviceToVmAsync(null, request));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task AddStorageDeviceToVmAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _storageService.AddStorageDeviceToVmAsync("TestVM", null));
        Assert.Equal("request", exception.ParamName);
    }

    [Fact]
    public async Task RemoveStorageDeviceFromVmAsync_WithNullVmName_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.RemoveStorageDeviceFromVmAsync(null, "device-id"));
        Assert.Contains("VM name cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task RemoveStorageDeviceFromVmAsync_WithNullDeviceId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.RemoveStorageDeviceFromVmAsync("TestVM", null));
        Assert.Contains("Device ID cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task EnableChangeTrackingAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.EnableChangeTrackingAsync(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task EnableChangeTrackingAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.EnableChangeTrackingAsync("nonexistent.vhdx"));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }

    [Fact]
    public async Task DisableChangeTrackingAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.DisableChangeTrackingAsync(null));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVirtualDiskChangesAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVirtualDiskChangesAsync(null, "change-id"));
        Assert.Contains("VHD path cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVirtualDiskChangesAsync_WithNullChangeTrackingId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            _storageService.GetVirtualDiskChangesAsync("test.vhdx", null));
        Assert.Contains("Change tracking ID cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task GetVirtualDiskChangesAsync_WithNonExistentVhd_ThrowsFileNotFoundException()
    {
        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _storageService.GetVirtualDiskChangesAsync("nonexistent.vhdx", "change-id"));
        Assert.Contains("VHD file not found: nonexistent.vhdx", exception.Message);
    }
}