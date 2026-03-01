using HyperV.Agent.Controllers;
using HyperV.Contracts.Interfaces.Providers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using Xunit;

namespace HyperV.Agent.Tests;

public class ServiceControllerTests
{
    private readonly Mock<IHostProvider> _hostProviderMock;
    private readonly ServiceController _controller;

    public ServiceControllerTests()
    {
        _hostProviderMock = new Mock<IHostProvider>();
        _hostProviderMock.Setup(p => p.AgentVersion).Returns("1.0.0");
        _hostProviderMock.Setup(p => p.HypervisorType).Returns("HyperV");
        _controller = new ServiceController(_hostProviderMock.Object);
    }

    // ──────────────────── GetHealth ────────────────────

    [Fact]
    public void GetHealth_ReturnsOkResult()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetHealth_ReturnsHealthyStatus()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var valueType = okResult.Value!.GetType();
        var statusProperty = valueType.GetProperty("status");
        Assert.NotNull(statusProperty);
        Assert.Equal("healthy", statusProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetHealth_ReturnsVersionFromProvider()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.AgentVersion).Returns("2.5.0");

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var versionProperty = valueType.GetProperty("version");
        Assert.NotNull(versionProperty);
        Assert.Equal("2.5.0", versionProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetHealth_ReturnsHypervisorTypeFromProvider()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var hypervisorTypeProperty = valueType.GetProperty("hypervisorType");
        Assert.NotNull(hypervisorTypeProperty);
        Assert.Equal("HyperV", hypervisorTypeProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetHealth_ReturnsTimestamp()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var timestampProperty = valueType.GetProperty("timestamp");
        Assert.NotNull(timestampProperty);
        var timestamp = (DateTime)timestampProperty!.GetValue(okResult.Value)!;
        Assert.True((DateTime.UtcNow - timestamp).TotalSeconds < 5);
    }

    [Fact]
    public void GetHealth_WithKvmProvider_ReturnsKvmType()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.HypervisorType).Returns("KVM");

        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var hypervisorTypeProperty = valueType.GetProperty("hypervisorType");
        Assert.Equal("KVM", hypervisorTypeProperty!.GetValue(okResult.Value)?.ToString());
    }

    // ──────────────────── GetInfo ────────────────────

    [Fact]
    public void GetInfo_ReturnsOkResult()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetInfo_ReturnsAgentName()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var nameProperty = valueType.GetProperty("name");
        Assert.NotNull(nameProperty);
        Assert.Equal("VManager Agent", nameProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetInfo_ReturnsVersionFromProvider()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.AgentVersion).Returns("3.0.0");

        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var versionProperty = valueType.GetProperty("version");
        Assert.Equal("3.0.0", versionProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetInfo_ReturnsHypervisorType()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var hypervisorTypeProperty = valueType.GetProperty("hypervisorType");
        Assert.NotNull(hypervisorTypeProperty);
        Assert.Equal("HyperV", hypervisorTypeProperty!.GetValue(okResult.Value)?.ToString());
    }

    [Fact]
    public void GetInfo_ReturnsDescription()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var descriptionProperty = valueType.GetProperty("description");
        Assert.NotNull(descriptionProperty);
        var description = descriptionProperty!.GetValue(okResult.Value)?.ToString();
        Assert.Contains("VManager agent", description);
    }

    [Fact]
    public void GetInfo_ReturnsEndpoints()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var valueType = okResult.Value!.GetType();
        var endpointsProperty = valueType.GetProperty("endpoints");
        Assert.NotNull(endpointsProperty);

        var endpoints = endpointsProperty!.GetValue(okResult.Value);
        var endpointsType = endpoints!.GetType();

        Assert.Equal("/api/v1/vms", endpointsType.GetProperty("vms")!.GetValue(endpoints)?.ToString());
        Assert.Equal("/api/v1/containers", endpointsType.GetProperty("containers")!.GetValue(endpoints)?.ToString());
        Assert.Equal("/api/v1/networks", endpointsType.GetProperty("networks")!.GetValue(endpoints)?.ToString());
        Assert.Equal("/api/v1/storage", endpointsType.GetProperty("storage")!.GetValue(endpoints)?.ToString());
        Assert.Equal("/api/v1/host", endpointsType.GetProperty("host")!.GetValue(endpoints)?.ToString());
    }
}
