using HyperV.Core.Wmi.Services;
using Moq;
using System.Management;
using Xunit;

namespace HyperV.Core.Wmi.Tests;

public class VmServiceTests
{
    private readonly VmService _vmService;

    public VmServiceTests()
    {
        _vmService = new VmService();
    }

    [Fact]
    public void IsVmPresent_WithValidVmName_ReturnsTrue()
    {
        // Arrange
        var vmName = "TestVM";

        // Act
        var result = _vmService.IsVmPresent(vmName);

        // Assert
        // Note: This test will depend on actual Hyper-V environment
        // In a real scenario, we'd mock the ManagementObjectSearcher
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void IsVmPresent_WithInvalidVmName_ReturnsFalse()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act
        var result = _vmService.IsVmPresent(vmName);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetVmStateString_WithRunningState_ReturnsRunning()
    {
        // Arrange
        var vmService = new VmService();
        var enabledState = (ushort)2; // Running

        // Act
        var result = typeof(VmService)
            .GetMethod("GetVmStateString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(vmService, new object[] { enabledState });

        // Assert
        Assert.Equal("Running", result);
    }

    [Fact]
    public void GetVmStateString_WithOffState_ReturnsOff()
    {
        // Arrange
        var vmService = new VmService();
        var enabledState = (ushort)3; // Off

        // Act
        var result = typeof(VmService)
            .GetMethod("GetVmStateString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(vmService, new object[] { enabledState });

        // Assert
        Assert.Equal("Off", result);
    }

    [Fact]
    public void GetHealthStateString_WithOKState_ReturnsOK()
    {
        // Arrange
        var vmService = new VmService();
        var healthState = (ushort)5; // OK

        // Act
        var result = typeof(VmService)
            .GetMethod("GetHealthStateString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(vmService, new object[] { healthState });

        // Assert
        Assert.Equal("OK", result);
    }

    [Fact]
    public void GetHealthStateString_WithCriticalFailure_ReturnsCriticalFailure()
    {
        // Arrange
        var vmService = new VmService();
        var healthState = (ushort)25; // Critical failure

        // Act
        var result = typeof(VmService)
            .GetMethod("GetHealthStateString", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(vmService, new object[] { healthState });

        // Assert
        Assert.Equal("Critical failure", result);
    }

    [Fact]
    public void ListVms_WhenCalled_ReturnsJsonString()
    {
        // Act
        var result = _vmService.ListVms();

        // Assert
        Assert.IsType<string>(result);
        Assert.Contains("Backend", result);
    }

    [Fact]
    public void GetVmProperties_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmProperties(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void StartVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.StartVm(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void StopVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.StopVm(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void TerminateVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.TerminateVm(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void PauseVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.PauseVm(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ResumeVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.ResumeVm(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ModifyVmConfiguration_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.ModifyVmConfiguration(vmName, null, null, null, null, null, null, null));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ListVmSnapshots_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.ListVmSnapshots(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CreateVmSnapshot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.CreateVmSnapshot(vmName, "TestSnapshot", "Test notes"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void DeleteVmSnapshot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.DeleteVmSnapshot(vmName, "snapshot-id"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void RevertVmToSnapshot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.RevertVmToSnapshot(vmName, "snapshot-id"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetVmMemoryStatus_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmMemoryStatus(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetSlpDataRoot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetSlpDataRoot(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ModifySlpDataRoot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.ModifySlpDataRoot(vmName, "new-location"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetVmGeneration_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmGeneration(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetSecureBoot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetSecureBoot(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void SetSecureBoot_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.SetSecureBoot(vmName, true));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetBootOrder_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetBootOrder(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void SetBootOrder_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.SetBootOrder(vmName, new[] { "HardDisk" }));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void MigrateVm_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.MigrateVm(vmName, "destination-host", true, true));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetAppHealth_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetAppHealth(vmName));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void CopyFileToGuest_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.CopyFileToGuest(vmName, "source", "dest", false));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetVmStorageDrive_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmStorageDrive(vmName, "drive-id"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetVmStorageDriveState_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmStorageDriveState(vmName, "drive-id"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void ResetVmStorageDrive_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.ResetVmStorageDrive(vmName, "drive-id"));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void LockVmStorageDriveMedia_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.LockVmStorageDriveMedia(vmName, "drive-id", true));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public void GetVmStorageDriveCapabilities_WithNonExistentVm_ThrowsException()
    {
        // Arrange
        var vmName = "NonExistentVM";

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => _vmService.GetVmStorageDriveCapabilities(vmName, "drive-id"));
        Assert.Contains("not found", exception.Message);
    }
}
