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

public class NetworksControllerTests
{
    private readonly Mock<INetworkProvider> _networkProviderMock;
    private readonly Mock<ILogger<NetworksController>> _loggerMock;
    private readonly NetworksController _controller;

    public NetworksControllerTests()
    {
        _networkProviderMock = new Mock<INetworkProvider>();
        _loggerMock = new Mock<ILogger<NetworksController>>();
        _controller = new NetworksController(_networkProviderMock.Object, _loggerMock.Object);
    }

    // ──────────────────── ListNetworks ────────────────────

    [Fact]
    public async Task ListNetworks_ReturnsOkWithNetworks()
    {
        // Arrange
        var networks = new List<VirtualNetworkInfo>
        {
            new() { Id = "net-1", Name = "Default Switch", Type = "Internal" },
            new() { Id = "net-2", Name = "External Network", Type = "External", IsExternal = true }
        };
        _networkProviderMock.Setup(p => p.ListNetworksAsync()).ReturnsAsync(networks);

        // Act
        var result = await _controller.ListNetworks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var paginated = Assert.IsType<PaginatedResult<VirtualNetworkInfo>>(okResult.Value);
        Assert.Equal(2, paginated.Items.Count);
        Assert.Equal(2, paginated.TotalCount);
        _networkProviderMock.Verify(p => p.ListNetworksAsync(), Times.Once);
    }

    [Fact]
    public async Task ListNetworks_WhenEmpty_ReturnsOkWithEmptyList()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.ListNetworksAsync()).ReturnsAsync(new List<VirtualNetworkInfo>());

        // Act
        var result = await _controller.ListNetworks();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var paginated = Assert.IsType<PaginatedResult<VirtualNetworkInfo>>(okResult.Value);
        Assert.Empty(paginated.Items);
        Assert.Equal(0, paginated.TotalCount);
    }

    [Fact]
    public async Task ListNetworks_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.ListNetworksAsync())
            .ThrowsAsync(new Exception("WMI failure"));

        // Act
        var result = await _controller.ListNetworks();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetNetwork ────────────────────

    [Fact]
    public async Task GetNetwork_WhenFound_ReturnsOk()
    {
        // Arrange
        var network = new VirtualNetworkInfo { Id = "net-1", Name = "Default Switch", Type = "Internal" };
        _networkProviderMock.Setup(p => p.GetNetworkAsync("net-1")).ReturnsAsync(network);

        // Act
        var result = await _controller.GetNetwork("net-1");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<VirtualNetworkInfo>(okResult.Value);
        Assert.Equal("net-1", returned.Id);
        Assert.Equal("Default Switch", returned.Name);
    }

    [Fact]
    public async Task GetNetwork_WhenNotFound_Returns404()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.GetNetworkAsync("missing"))
            .ReturnsAsync((VirtualNetworkInfo?)null);

        // Act
        var result = await _controller.GetNetwork("missing");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetNetwork_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.GetNetworkAsync("net-1"))
            .ThrowsAsync(new InvalidOperationException("Provider error"));

        // Act
        var result = await _controller.GetNetwork("net-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── CreateNetwork ────────────────────

    [Fact]
    public async Task CreateNetwork_WithValidSpec_Returns201Created()
    {
        // Arrange
        var spec = new CreateNetworkSpec { Name = "TestNet", Type = "Internal" };
        _networkProviderMock.Setup(p => p.CreateNetworkAsync(spec)).ReturnsAsync("new-id-123");

        // Act
        var result = await _controller.CreateNetwork(spec);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal("/api/v1/networks/new-id-123", createdResult.Location);
        _networkProviderMock.Verify(p => p.CreateNetworkAsync(spec), Times.Once);
    }

    [Fact]
    public async Task CreateNetwork_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateNetworkSpec { Name = "", Type = "Internal" };

        // Act
        var result = await _controller.CreateNetwork(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _networkProviderMock.Verify(p => p.CreateNetworkAsync(It.IsAny<CreateNetworkSpec>()), Times.Never);
    }

    [Fact]
    public async Task CreateNetwork_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var spec = new CreateNetworkSpec { Name = null!, Type = "External" };

        // Act
        var result = await _controller.CreateNetwork(spec);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateNetwork_WhenProviderThrows_Returns500()
    {
        // Arrange
        var spec = new CreateNetworkSpec { Name = "TestNet", Type = "External" };
        _networkProviderMock.Setup(p => p.CreateNetworkAsync(spec))
            .ThrowsAsync(new Exception("Creation failed"));

        // Act
        var result = await _controller.CreateNetwork(spec);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── DeleteNetwork ────────────────────

    [Fact]
    public async Task DeleteNetwork_Success_ReturnsOk()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.DeleteNetworkAsync("net-1")).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteNetwork("net-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _networkProviderMock.Verify(p => p.DeleteNetworkAsync("net-1"), Times.Once);
    }

    [Fact]
    public async Task DeleteNetwork_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.DeleteNetworkAsync("net-1"))
            .ThrowsAsync(new Exception("Delete failed"));

        // Act
        var result = await _controller.DeleteNetwork("net-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ListPhysicalAdapters ────────────────────

    [Fact]
    public async Task ListPhysicalAdapters_ReturnsOkWithAdapters()
    {
        // Arrange
        var adapters = new List<PhysicalAdapterDto>
        {
            new() { Id = "nic-1", Name = "Ethernet", Speed = "1 Gbps", Status = "Up" },
            new() { Id = "nic-2", Name = "WiFi", Speed = "300 Mbps", Status = "Up" }
        };
        _networkProviderMock.Setup(p => p.ListPhysicalAdaptersAsync()).ReturnsAsync(adapters);

        // Act
        var result = await _controller.ListPhysicalAdapters();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<PhysicalAdapterDto>>(okResult.Value);
        Assert.Equal(2, returned.Count);
    }

    [Fact]
    public async Task ListPhysicalAdapters_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.ListPhysicalAdaptersAsync())
            .ThrowsAsync(new Exception("Adapter query failed"));

        // Act
        var result = await _controller.ListPhysicalAdapters();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetVmNetworkAdapters ────────────────────

    [Fact]
    public async Task GetVmNetworkAdapters_ReturnsOkWithAdapters()
    {
        // Arrange
        var adapters = new List<VmNetworkAdapterDto>
        {
            new() { Id = "adapter-1", Name = "Network Adapter", NetworkName = "Default Switch", MacAddress = "00:15:5D:01:02:03" }
        };
        _networkProviderMock.Setup(p => p.GetVmNetworkAdaptersAsync("TestVM")).ReturnsAsync(adapters);

        // Act
        var result = await _controller.GetVmNetworkAdapters("TestVM");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<VmNetworkAdapterDto>>(okResult.Value);
        Assert.Single(returned);
        Assert.Equal("adapter-1", returned[0].Id);
    }

    [Fact]
    public async Task GetVmNetworkAdapters_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.GetVmNetworkAdaptersAsync("BadVM"))
            .ThrowsAsync(new Exception("VM not found"));

        // Act
        var result = await _controller.GetVmNetworkAdapters("BadVM");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── AttachNetworkAdapter ────────────────────

    [Fact]
    public async Task AttachNetworkAdapter_Success_ReturnsOk()
    {
        // Arrange
        var request = new AttachNetworkAdapterRequest { NetworkId = "net-1" };
        _networkProviderMock.Setup(p => p.AttachNetworkAdapterAsync("TestVM", "net-1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AttachNetworkAdapter("TestVM", request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _networkProviderMock.Verify(p => p.AttachNetworkAdapterAsync("TestVM", "net-1"), Times.Once);
    }

    [Fact]
    public async Task AttachNetworkAdapter_WhenProviderThrows_Returns500()
    {
        // Arrange
        var request = new AttachNetworkAdapterRequest { NetworkId = "net-1" };
        _networkProviderMock.Setup(p => p.AttachNetworkAdapterAsync("TestVM", "net-1"))
            .ThrowsAsync(new Exception("Attach failed"));

        // Act
        var result = await _controller.AttachNetworkAdapter("TestVM", request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── DetachNetworkAdapter ────────────────────

    [Fact]
    public async Task DetachNetworkAdapter_Success_ReturnsOk()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.DetachNetworkAdapterAsync("TestVM", "adapter-1"))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DetachNetworkAdapter("TestVM", "adapter-1");

        // Assert
        Assert.IsType<OkObjectResult>(result);
        _networkProviderMock.Verify(p => p.DetachNetworkAdapterAsync("TestVM", "adapter-1"), Times.Once);
    }

    [Fact]
    public async Task DetachNetworkAdapter_WhenProviderThrows_Returns500()
    {
        // Arrange
        _networkProviderMock.Setup(p => p.DetachNetworkAdapterAsync("TestVM", "adapter-1"))
            .ThrowsAsync(new Exception("Detach failed"));

        // Act
        var result = await _controller.DetachNetworkAdapter("TestVM", "adapter-1");

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
