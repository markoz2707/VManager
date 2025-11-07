using Xunit;
using Microsoft.AspNetCore.Mvc;
using HyperV.Agent.Controllers;
using FluentAssertions;
using System.Text.Json;

namespace HyperV.Tests.Controllers;

/// <summary>
/// Testy jednostkowe dla ServiceController
/// </summary>
public class ServiceControllerTests
{
    private readonly ServiceController _controller;

    public ServiceControllerTests()
    {
        _controller = new ServiceController();
    }

    #region Health Endpoint Tests

    [Fact]
    public void GetHealth_ShouldReturnOkResult()
    {
        // Arrange & Act
        var result = _controller.GetHealth();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void GetHealth_ShouldReturnHealthyStatus()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.GetProperty("status").GetString().Should().Be("healthy");
        root.TryGetProperty("timestamp", out _).Should().BeTrue();
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.TryGetProperty("services", out _).Should().BeTrue();
    }

    [Fact]
    public void GetHealth_ShouldIncludeAllServices()
    {
        // Act
        var result = _controller.GetHealth();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var services = root.GetProperty("services");
        services.GetProperty("hcs").GetString().Should().Be("available");
        services.GetProperty("wmi").GetString().Should().Be("available");
        services.GetProperty("hcn").GetString().Should().Be("available");
    }

    #endregion

    #region Info Endpoint Tests

    [Fact]
    public void GetInfo_ShouldReturnOkResult()
    {
        // Arrange & Act
        var result = _controller.GetInfo();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public void GetInfo_ShouldReturnAgentInformation()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        root.GetProperty("name").GetString().Should().Be("HyperV Agent");
        root.GetProperty("version").GetString().Should().Be("1.0.0");
        root.TryGetProperty("description", out _).Should().BeTrue();
        root.TryGetProperty("endpoints", out _).Should().BeTrue();
        root.TryGetProperty("capabilities", out _).Should().BeTrue();
    }

    [Fact]
    public void GetInfo_ShouldIncludeAllEndpoints()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var endpoints = root.GetProperty("endpoints");
        endpoints.GetProperty("vms").GetString().Should().Be("/api/v1/vms");
        endpoints.GetProperty("containers").GetString().Should().Be("/api/v1/containers");
        endpoints.GetProperty("networks").GetString().Should().Be("/api/v1/networks");
        endpoints.GetProperty("storage").GetString().Should().Be("/api/v1/storage");
    }

    [Fact]
    public void GetInfo_ShouldIncludeCapabilities()
    {
        // Act
        var result = _controller.GetInfo();

        // Assert
        var okResult = (OkObjectResult)result;
        var json = JsonSerializer.Serialize(okResult.Value);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        var capabilities = root.GetProperty("capabilities");
        var capabilitiesList = new List<string>();
        
        foreach (var capability in capabilities.EnumerateArray())
        {
            capabilitiesList.Add(capability.GetString()!);
        }
        
        capabilitiesList.Should().Contain("VM management (HCS & WMI)");
        capabilitiesList.Should().Contain("Container management");
        capabilitiesList.Should().Contain("Network management");
        capabilitiesList.Should().Contain("Storage management");
        capabilitiesList.Should().Contain("Snapshot operations");
        capabilitiesList.Should().Contain("Replication services");
    }

    #endregion
}