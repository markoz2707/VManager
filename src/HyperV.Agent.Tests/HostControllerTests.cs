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
    private readonly Mock<IEventLogProvider> _eventLogProviderMock;
    private readonly Mock<ILogger<HostController>> _loggerMock;
    private readonly HostController _controller;

    public HostControllerTests()
    {
        _hostProviderMock = new Mock<IHostProvider>();
        _metricsProviderMock = new Mock<IMetricsProvider>();
        _eventLogProviderMock = new Mock<IEventLogProvider>();
        _loggerMock = new Mock<ILogger<HostController>>();
        _controller = new HostController(_hostProviderMock.Object, _metricsProviderMock.Object, _eventLogProviderMock.Object, _loggerMock.Object);
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

    // ──────────────────── GetLogs ────────────────────

    [Fact]
    public async Task GetLogs_ReturnsOkWithEntries()
    {
        // Arrange
        var response = new LogsResponse
        {
            Entries = new List<LogEntryDto>
            {
                new LogEntryDto { Id = "1", Timestamp = DateTime.UtcNow, Level = "Information", Source = "HyperV-VMMS", Message = "VM started" },
                new LogEntryDto { Id = "2", Timestamp = DateTime.UtcNow, Level = "Warning", Source = "HyperV-Worker", Message = "Memory pressure" }
            },
            TotalCount = 2,
            Sources = new List<string> { "HyperV-VMMS", "HyperV-Worker" }
        };
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync(null, null, null, null, 100, null))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetLogs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<LogsResponse>(okResult.Value);
        Assert.Equal(2, returned.Entries.Count);
        Assert.Equal(2, returned.TotalCount);
    }

    [Fact]
    public async Task GetLogs_WithFilters_PassesFiltersToProvider()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var response = new LogsResponse
        {
            Entries = new List<LogEntryDto>
            {
                new LogEntryDto { Id = "1", Timestamp = start, Level = "Error", Source = "HyperV-VMMS", Message = "Disk error" }
            },
            TotalCount = 1,
            Sources = new List<string> { "HyperV-VMMS" }
        };
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync("HyperV-VMMS", "Error", start, end, 50, "disk"))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.GetLogs(source: "HyperV-VMMS", level: "Error", start: start, end: end, limit: 50, search: "disk");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<LogsResponse>(okResult.Value);
        Assert.Single(returned.Entries);
        Assert.Equal("Error", returned.Entries[0].Level);
        _eventLogProviderMock.Verify(p => p.GetLogsAsync("HyperV-VMMS", "Error", start, end, 50, "disk"), Times.Once);
    }

    [Fact]
    public async Task GetLogs_WhenProviderThrows_Returns500()
    {
        // Arrange
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Event log service unavailable"));

        // Act
        var result = await _controller.GetLogs();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── GetLogSources ────────────────────

    [Fact]
    public async Task GetLogSources_ReturnsOkWithSourceList()
    {
        // Arrange
        var sources = new List<string> { "HyperV-VMMS", "HyperV-Worker", "System", "Application" };
        _eventLogProviderMock.Setup(p => p.GetLogSourcesAsync()).ReturnsAsync(sources);

        // Act
        var result = await _controller.GetLogSources();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<string>>(okResult.Value);
        Assert.Equal(4, returned.Count);
        Assert.Contains("HyperV-VMMS", returned);
    }

    [Fact]
    public async Task GetLogSources_WhenProviderThrows_Returns500()
    {
        // Arrange
        _eventLogProviderMock.Setup(p => p.GetLogSourcesAsync())
            .ThrowsAsync(new Exception("Cannot enumerate log sources"));

        // Act
        var result = await _controller.GetLogSources();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ExportLogs ────────────────────

    [Fact]
    public async Task ExportLogs_Json_ReturnsFileResult()
    {
        // Arrange
        var response = new LogsResponse
        {
            Entries = new List<LogEntryDto>
            {
                new LogEntryDto { Id = "1", Timestamp = DateTime.UtcNow, Level = "Information", Source = "System", Message = "Test message" }
            },
            TotalCount = 1,
            Sources = new List<string> { "System" }
        };
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync(null, null, null, null, 1000, null))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ExportLogs(format: "json");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json", fileResult.ContentType);
        Assert.Equal("logs.json", fileResult.FileDownloadName);
        Assert.True(fileResult.FileContents.Length > 0);
    }

    [Fact]
    public async Task ExportLogs_Csv_ReturnsCsvFile()
    {
        // Arrange
        var response = new LogsResponse
        {
            Entries = new List<LogEntryDto>
            {
                new LogEntryDto { Id = "1", Timestamp = new DateTime(2026, 1, 15, 10, 30, 0, DateTimeKind.Utc), Level = "Warning", Source = "HyperV-Worker", Message = "High memory usage" }
            },
            TotalCount = 1,
            Sources = new List<string> { "HyperV-Worker" }
        };
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync(null, null, null, null, 1000, null))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.ExportLogs(format: "csv");

        // Assert
        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", fileResult.ContentType);
        Assert.Equal("logs.csv", fileResult.FileDownloadName);
        var csv = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        Assert.Contains("Timestamp,Level,Source,Message,EventId,Category", csv);
        Assert.Contains("High memory usage", csv);
    }

    [Fact]
    public async Task ExportLogs_WhenProviderThrows_Returns500()
    {
        // Arrange
        _eventLogProviderMock
            .Setup(p => p.GetLogsAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Export failed"));

        // Act
        var result = await _controller.ExportLogs();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    // ──────────────────── ShutdownHost ────────────────────

    [Fact]
    public async Task ShutdownHost_WithConfirm_ReturnsOk()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.ShutdownHostAsync(false)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ShutdownHost(confirm: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value!.GetType();
        Assert.Equal("shutdown_initiated", valueType.GetProperty("status")!.GetValue(okResult.Value));
        _hostProviderMock.Verify(p => p.ShutdownHostAsync(false), Times.Once);
    }

    [Fact]
    public async Task ShutdownHost_WithoutConfirm_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ShutdownHost(confirm: false);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _hostProviderMock.Verify(p => p.ShutdownHostAsync(It.IsAny<bool>()), Times.Never);
    }

    // ──────────────────── RebootHost ────────────────────

    [Fact]
    public async Task RebootHost_WithConfirm_ReturnsOk()
    {
        // Arrange
        _hostProviderMock.Setup(p => p.RebootHostAsync(false)).Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RebootHost(confirm: true);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value!.GetType();
        Assert.Equal("reboot_initiated", valueType.GetProperty("status")!.GetValue(okResult.Value));
        _hostProviderMock.Verify(p => p.RebootHostAsync(false), Times.Once);
    }

    [Fact]
    public async Task RebootHost_WithoutConfirm_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.RebootHost(confirm: false);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
        _hostProviderMock.Verify(p => p.RebootHostAsync(It.IsAny<bool>()), Times.Never);
    }
}
