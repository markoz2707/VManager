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

public class HostControllerTests
{
    private readonly Mock<IHostProvider> _hostProviderMock;
    private readonly Mock<IMetricsProvider> _metricsProviderMock;
    private readonly Mock<ILogger<HostController>> _loggerMock;
    private readonly HostController _controller;

    public HostControllerTests()
    {
        _hostProviderMock = new Mock<IHostProvider>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
        _loggerMock = new Mock<ILogger<HostController>>();
        _controller = new HostController(_hostProviderMock.Object, _metricsProviderMock.Object, _loggerMock.Object);
    }

    // ──────────────────── GetHardwareInfo ────────────────────

    [Fact]
    public async Task GetHardwareInfo_ReturnsOkWithHostInfo()
    {
        // Arrange
        var info = new HostInfoDto
        {
            Hostname = "HV-HOST-01",
            HypervisorType = "HyperV",
            HypervisorVersion = "10.0.26100",
            OperatingSystem = "Windows Server 2025",
            OsVersion = "10.0.26100",
            CpuCores = 8,
            LogicalProcessors = 16,
            TotalMemoryMB = 32768
        };
        _hostProviderMock.Setup(p => p.GetHostInfoAsync()).ReturnsAsync(info);

        // Act
        var result = await _controller.GetHardwareInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<HostInfoDto>(okResult.Value);
        Assert.Equal("HV-HOST-01", returned.Hostname);
        Assert.Equal(16, returned.LogicalProcessors);
    }

    [Fact]
    public async Task GetHardwareInfo_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetHostInfoAsync())
            .ThrowsAsync(new Exception("WMI access denied"));

        // Act
        var result = await _controller.GetHardwareInfo();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetSystemInfo ────────────────────

    [Fact]
    public async Task GetSystemInfo_ReturnsOkWithSystemDetails()
    {
        // Arrange
        var info = new HostInfoDto
        {
            Hostname = "HV-HOST-01",
            HypervisorType = "HyperV",
            HypervisorVersion = "10.0.26100",
            OperatingSystem = "Windows Server 2025",
            OsVersion = "10.0.26100"
        };
        _hostProviderMock.Setup(p => p.GetHostInfoAsync()).ReturnsAsync(info);

        // Act
        var result = await _controller.GetSystemInfo();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify the anonymous object properties
        var valueType = okResult.Value!.GetType();
        Assert.Equal("HV-HOST-01", valueType.GetProperty("hostname")!.GetValue(okResult.Value));
        Assert.Equal("HyperV", valueType.GetProperty("hypervisorType")!.GetValue(okResult.Value));
        Assert.Equal("Windows Server 2025", valueType.GetProperty("osName")!.GetValue(okResult.Value));
    }

    [Fact]
    public async Task GetSystemInfo_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetHostInfoAsync())
            .ThrowsAsync(new Exception("System info unavailable"));

        // Act
        var result = await _controller.GetSystemInfo();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetPerformanceSummary ────────────────────

    [Fact]
    public async Task GetPerformanceSummary_ReturnsOkWithMetrics()
    {
        // Arrange
        var metrics = new HostPerformanceMetrics
        {
            CpuUsagePercent = 35.5,
            MemoryUsagePercent = 68.2,
            MemoryAvailableMB = 10240
        };
        _hostProviderMock.Setup(p => p.GetPerformanceMetricsAsync()).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetPerformanceSummary();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<HostPerformanceMetrics>(okResult.Value);
        Assert.Equal(35.5, returned.CpuUsagePercent);
        Assert.Equal(68.2, returned.MemoryUsagePercent);
    }

    [Fact]
    public async Task GetPerformanceSummary_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetPerformanceMetricsAsync())
            .ThrowsAsync(new Exception("Performance counters unavailable"));

        // Act
        var result = await _controller.GetPerformanceSummary();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetHostDetails ────────────────────

    [Fact]
    public async Task GetHostDetails_ReturnsOkWithCombinedData()
    {
        // Arrange
        var info = new HostInfoDto
        {
            Hostname = "HV-HOST-01",
            HypervisorType = "HyperV",
            CpuCores = 8,
            TotalMemoryMB = 32768
        };
        var perf = new HostPerformanceMetrics
        {
            CpuUsagePercent = 25.0,
            MemoryUsagePercent = 50.0,
            MemoryAvailableMB = 16384
        };
        _hostProviderMock.Setup(p => p.GetHostInfoAsync()).ReturnsAsync(info);
        _hostProviderMock.Setup(p => p.GetPerformanceMetricsAsync()).ReturnsAsync(perf);

        // Act
        var result = await _controller.GetHostDetails();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var valueType = okResult.Value!.GetType();
        var hardwareProperty = valueType.GetProperty("hardware");
        var performanceProperty = valueType.GetProperty("performance");
        Assert.NotNull(hardwareProperty);
        Assert.NotNull(performanceProperty);
        Assert.Equal(info, hardwareProperty!.GetValue(okResult.Value));
        Assert.Equal(perf, performanceProperty!.GetValue(okResult.Value));
    }

    [Fact]
    public async Task GetHostDetails_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetHostInfoAsync())
            .ThrowsAsync(new Exception("Host info unavailable"));

        // Act
        var result = await _controller.GetHostDetails();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetHostStats ────────────────────

    [Fact]
    public async Task GetHostStats_ReturnsOkWithPerformanceMetrics()
    {
        // Arrange
        var metrics = new HostPerformanceMetrics
        {
            CpuUsagePercent = 42.0,
            MemoryUsagePercent = 75.0,
            MemoryAvailableMB = 8192
        };
        _hostProviderMock.Setup(p => p.GetPerformanceMetricsAsync()).ReturnsAsync(metrics);

        // Act
        var result = await _controller.GetHostStats();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<HostPerformanceMetrics>(okResult.Value);
        Assert.Equal(42.0, returned.CpuUsagePercent);
    }

    [Fact]
    public async Task GetHostStats_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetPerformanceMetricsAsync())
            .ThrowsAsync(new Exception("Stats unavailable"));

        // Act
        var result = await _controller.GetHostStats();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetHostUsageMetrics ────────────────────

    [Fact]
    public async Task GetHostUsageMetrics_ReturnsOkWithUsage()
    {
        // Arrange
        var usage = new HostUsageDto
        {
            CpuUsagePercent = 55.0,
            CpuCores = 8,
            LogicalProcessors = 16,
            TotalMemoryMB = 32768,
            AvailableMemoryMB = 14336,
            MemoryUsagePercent = 56.25
        };
        _metricsProviderMock.Setup(p => p.GetHostUsageAsync()).ReturnsAsync(usage);

        // Act
        var result = await _controller.GetHostUsageMetrics();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<HostUsageDto>(okResult.Value);
        Assert.Equal(55.0, returned.CpuUsagePercent);
        Assert.Equal(16, returned.LogicalProcessors);
    }

    [Fact]
    public async Task GetHostUsageMetrics_WhenProviderThrows_Returns500()
    {
        // Arrange
        _metricsProviderMock.Setup(p => p.GetHostUsageAsync())
            .ThrowsAsync(new Exception("Metrics collection failed"));

        // Act
        var result = await _controller.GetHostUsageMetrics();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetCapabilities ────────────────────

    [Fact]
    public async Task GetCapabilities_ReturnsOkWithCapabilities()
    {
        // Arrange
        var capabilities = new HypervisorCapabilities
        {
            HypervisorType = "HyperV",
            SupportsLiveMigration = true,
            SupportsSnapshots = true,
            SupportsDynamicMemory = true,
            SupportsNestedVirtualization = true,
            SupportsContainers = true,
            SupportsReplication = true,
            SupportsFibreChannel = true,
            SupportsStorageQoS = true,
            ConsoleType = "rdp",
            MaxVmCount = 1024,
            MaxCpuPerVm = 240,
            MaxMemoryPerVmMB = 12582912,
            SupportedDiskFormats = new List<string> { "vhd", "vhdx" }
        };
        _hostProviderMock.Setup(p => p.GetCapabilitiesAsync()).ReturnsAsync(capabilities);

        // Act
        var result = await _controller.GetCapabilities();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<HypervisorCapabilities>(okResult.Value);
        Assert.Equal("HyperV", returned.HypervisorType);
        Assert.True(returned.SupportsLiveMigration);
        Assert.True(returned.SupportsReplication);
        Assert.Equal(1024, returned.MaxVmCount);
    }

    [Fact]
    public async Task GetCapabilities_WhenProviderThrows_Returns500()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.GetCapabilitiesAsync())
            .ThrowsAsync(new Exception("Capabilities query failed"));

        // Act
        var result = await _controller.GetCapabilities();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
