using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Management;
using System.Diagnostics;
using System.ServiceProcess;

namespace HyperV.Agent.Services;

/// <summary>
/// Custom health check for WMI connectivity
/// </summary>
public class WmiHealthCheck : IHealthCheck
{
    private readonly ILogger<WmiHealthCheck> _logger;

    public WmiHealthCheck(ILogger<WmiHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("root\\virtualization\\v2",
                    "SELECT * FROM Msvm_ComputerSystem WHERE Caption='Hosting Computer System'");
                var collection = searcher.Get();

                if (collection.Count == 0)
                {
                    throw new Exception("Hyper-V WMI namespace not accessible");
                }
            }, cancellationToken);

            return HealthCheckResult.Healthy("WMI connectivity is working");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI health check failed");
            return HealthCheckResult.Degraded($"WMI check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Custom health check for HCS connectivity
/// </summary>
public class HcsHealthCheck : IHealthCheck
{
    private readonly ILogger<HcsHealthCheck> _logger;

    public HcsHealthCheck(ILogger<HcsHealthCheck> logger)
    {
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                // Check if HCS service is running
                var vmcompute = ServiceController.GetServices()
                    .FirstOrDefault(s => s.ServiceName.Equals("vmcompute", StringComparison.OrdinalIgnoreCase));

                if (vmcompute == null)
                {
                    throw new Exception("HCS service (vmcompute) not found");
                }

                if (vmcompute.Status != ServiceControllerStatus.Running)
                {
                    throw new Exception($"HCS service is {vmcompute.Status}");
                }
            }, cancellationToken);

            return HealthCheckResult.Healthy("HCS service is running");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HCS health check failed");
            return HealthCheckResult.Unhealthy($"HCS check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Custom health check for disk space
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly long _minimumFreeMB;

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger, IConfiguration configuration)
    {
        _logger = logger;
        _minimumFreeMB = configuration.GetValue<long>("HealthChecks:MinimumFreeDiskSpaceMB", 1024);
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\");
            var freeMB = drive.AvailableFreeSpace / 1024 / 1024;

            if (freeMB < _minimumFreeMB)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Low disk space: {freeMB} MB free (minimum: {_minimumFreeMB} MB)"));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space OK: {freeMB} MB free"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk space health check failed");
            return Task.FromResult(HealthCheckResult.Degraded($"Disk check failed: {ex.Message}"));
        }
    }
}
