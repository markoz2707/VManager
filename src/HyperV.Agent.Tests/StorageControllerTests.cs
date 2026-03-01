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

public class StorageControllerTests
{
    private readonly Mock<IStorageService> _storageServiceMock;
    private readonly StorageController _controller;

    public StorageControllerTests()
    {
        _storageServiceMock = new Mock<IStorageService>();
        _controller = new StorageController(_storageServiceMock.Object);
    }

    // --- GetStorageDevices Tests ---

    [Fact]
    public async Task GetStorageDevices_ReturnsOkResult()
    {
        // Arrange
        var expectedDevices = new List<StorageDeviceInfo>
        {
            new StorageDeviceInfo { Name = "C:", Filesystem = "NTFS", Size = 500L * 1024 * 1024 * 1024 }
        };
        _storageServiceMock.Setup(s => s.ListStorageDevicesAsync()).ReturnsAsync(expectedDevices);

        // Act
        var result = await _controller.GetStorageDevices();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var devices = Assert.IsType<List<StorageDeviceInfo>>(okResult.Value);
        Assert.Single(devices);
        Assert.Equal("C:", devices[0].Name);
    }

    [Fact]
    public async Task GetStorageDevices_WhenEmpty_ReturnsOkResultWithEmptyList()
    {
        // Arrange
        _storageServiceMock.Setup(s => s.ListStorageDevicesAsync()).ReturnsAsync(new List<StorageDeviceInfo>());

        // Act
        var result = await _controller.GetStorageDevices();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var devices = Assert.IsType<List<StorageDeviceInfo>>(okResult.Value);
        Assert.Empty(devices);
    }

    [Fact]
    public async Task GetStorageDevices_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _storageServiceMock.Setup(s => s.ListStorageDevicesAsync()).ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetStorageDevices();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // --- GetSuitableVhdLocations Tests ---

    [Fact]
    public async Task GetSuitableVhdLocations_ReturnsOkResult()
    {
        // Arrange
        var expectedLocations = new List<StorageLocation>
        {
            new StorageLocation { Drive = "D:", FreeSpaceGb = 200 }
        };
        _storageServiceMock.Setup(s => s.GetSuitableVhdLocationsAsync(10)).ReturnsAsync(expectedLocations);

        // Act
        var result = await _controller.GetSuitableVhdLocations();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var locations = Assert.IsType<List<StorageLocation>>(okResult.Value);
        Assert.Single(locations);
    }

    [Fact]
    public async Task GetSuitableVhdLocations_WithCustomMinGb_ReturnsOkResult()
    {
        // Arrange
        var expectedLocations = new List<StorageLocation>();
        _storageServiceMock.Setup(s => s.GetSuitableVhdLocationsAsync(50)).ReturnsAsync(expectedLocations);

        // Act
        var result = await _controller.GetSuitableVhdLocations(50);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var locations = Assert.IsType<List<StorageLocation>>(okResult.Value);
        Assert.Empty(locations);
    }

    [Fact]
    public async Task GetSuitableVhdLocations_WhenServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        _storageServiceMock.Setup(s => s.GetSuitableVhdLocationsAsync(It.IsAny<long>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetSuitableVhdLocations();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
