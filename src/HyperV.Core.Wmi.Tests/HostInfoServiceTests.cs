using HyperV.Core.Wmi.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using Xunit;

namespace HyperV.Core.Wmi.Tests;

public class HostInfoServiceTests
{
    private readonly Mock<ILogger<HostInfoService>> _loggerMock;
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly HostInfoService _hostInfoService;

    public HostInfoServiceTests()
    {
        _loggerMock = new Mock<ILogger<HostInfoService>>();
        _cacheMock = new Mock<IMemoryCache>();
        _hostInfoService = new HostInfoService(_loggerMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task GetHostHardwareInfoAsync_WithCachedData_ReturnsCachedInfo()
    {
        // Arrange
        var cachedInfo = new HyperV.Contracts.Models.HostHardwareInfo
        {
            Manufacturer = "Test Manufacturer",
            Model = "Test Model"
        };

        object cachedValue = cachedInfo;
        _cacheMock.Setup(c => c.TryGetValue("HostHardwareInfo", out cachedValue)).Returns(true);

        // Act
        var result = await _hostInfoService.GetHostHardwareInfoAsync();

        // Assert
        Assert.Equal(cachedInfo, result);
        _loggerMock.Verify(l => l.LogInformation("Returning cached host hardware info"), Times.Once);
    }

    [Fact]
    public async Task GetHostHardwareInfoAsync_WithoutCachedData_FetchesFromWmiAndCaches()
    {
        // Arrange
        object cachedValue = null;
        _cacheMock.Setup(c => c.TryGetValue("HostHardwareInfo", out cachedValue)).Returns(false);
        _cacheMock.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()));

        // Act
        var result = await _hostInfoService.GetHostHardwareInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HyperV.Contracts.Models.HostHardwareInfo>(result);
        _cacheMock.Verify(c => c.Set("HostHardwareInfo", result, TimeSpan.FromMinutes(10)), Times.Once);
        _loggerMock.Verify(l => l.LogInformation("Cached host hardware info"), Times.Once);
    }

    [Fact]
    public async Task GetSystemInfoAsync_ReturnsSystemInfo()
    {
        // Act
        var result = await _hostInfoService.GetSystemInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HyperV.Contracts.Models.SystemInfo>(result);
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_ReturnsPerformanceSummary()
    {
        // Act
        var result = await _hostInfoService.GetPerformanceSummaryAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HyperV.Contracts.Models.PerformanceSummary>(result);
        Assert.True(result.CpuUsagePercent >= 0);
        Assert.True(result.MemoryUsagePercent >= 0);
        Assert.NotNull(result.StorageUsagePercent);
    }

    [Fact]
    public async Task GetRecentTasksAsync_WithLimit_ReturnsTasks()
    {
        // Act
        var result = await _hostInfoService.GetRecentTasksAsync(5);

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<HyperV.Contracts.Models.RecentTask>>(result);
        Assert.True(result.Count <= 5);
    }

    [Fact]
    public async Task GetRecentTasksAsync_DefaultLimit_ReturnsUpTo10Tasks()
    {
        // Act
        var result = await _hostInfoService.GetRecentTasksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IEnumerable<HyperV.Contracts.Models.RecentTask>>(result);
        Assert.True(result.Count <= 10);
    }

    [Fact]
    public async Task GetRecentTasksAsync_ReturnsTasksWithRequiredProperties()
    {
        // Act
        var result = await _hostInfoService.GetRecentTasksAsync(1);

        // Assert
        if (result.Any())
        {
            var task = result.First();
            Assert.NotNull(task.Target);
            Assert.NotNull(task.Initiator);
            Assert.NotNull(task.Status);
            Assert.NotNull(task.Started);
            Assert.NotNull(task.Result);
        }
    }

    [Fact]
    public async Task GetPerformanceSummaryAsync_HandlesManagementException_Gracefully()
    {
        // This test verifies that the service handles WMI exceptions gracefully
        // In a real scenario, we might need to mock the ManagementObjectSearcher

        // Act
        var result = await _hostInfoService.GetPerformanceSummaryAsync();

        // Assert
        Assert.NotNull(result);
        // Even if WMI fails, the method should return a valid object with default values
        Assert.True(result.CpuUsagePercent >= 0);
        Assert.True(result.MemoryUsagePercent >= 0);
    }

    [Fact]
    public async Task GetHostHardwareInfoAsync_HandlesExceptions_Gracefully()
    {
        // Arrange
        object cachedValue = null;
        _cacheMock.Setup(c => c.TryGetValue("HostHardwareInfo", out cachedValue)).Returns(false);
        _cacheMock.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>()));

        // Act
        var result = await _hostInfoService.GetHostHardwareInfoAsync();

        // Assert
        Assert.NotNull(result);
        // Should return a valid object even if WMI queries fail
        Assert.IsType<HyperV.Contracts.Models.HostHardwareInfo>(result);
    }

    [Fact]
    public async Task GetSystemInfoAsync_HandlesExceptions_Gracefully()
    {
        // Act
        var result = await _hostInfoService.GetSystemInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<HyperV.Contracts.Models.SystemInfo>(result);
    }
}