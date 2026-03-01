using System.Text.Json;
using HyperV.Contracts.Interfaces;
using HyperV.Core.Wmi.Services;
using Prometheus;

namespace HyperV.Agent.Services;

/// <summary>
/// Background service that collects Hyper-V metrics and exposes them as Prometheus gauges
/// </summary>
public class PrometheusMetricsCollector : BackgroundService
{
    private readonly IHostInfoService _hostInfoService;
    private readonly HyperV.Core.Wmi.Services.VmService _wmiVmService;
    private readonly ILogger<PrometheusMetricsCollector> _logger;
    private readonly TimeSpan _collectionInterval = TimeSpan.FromSeconds(15);

    // Host-level gauges
    private static readonly Gauge HostCpuUsage = Metrics.CreateGauge(
        "vmanager_host_cpu_usage_percent", "Host CPU usage percentage");
    private static readonly Gauge HostMemoryTotalMb = Metrics.CreateGauge(
        "vmanager_host_memory_total_mb", "Total physical memory in MB");
    private static readonly Gauge HostMemoryAvailableMb = Metrics.CreateGauge(
        "vmanager_host_memory_available_mb", "Available memory in MB");
    private static readonly Gauge HostMemoryUsagePercent = Metrics.CreateGauge(
        "vmanager_host_memory_usage_percent", "Host memory usage percentage");

    // VM-level gauges
    private static readonly Gauge VmCount = Metrics.CreateGauge(
        "vmanager_vm_count", "Number of VMs", new GaugeConfiguration
        {
            LabelNames = new[] { "state" }
        });
    private static readonly Gauge VmMemoryAssignedMb = Metrics.CreateGauge(
        "vmanager_vm_memory_assigned_mb", "VM assigned memory in MB", new GaugeConfiguration
        {
            LabelNames = new[] { "vm_name" }
        });

    public PrometheusMetricsCollector(
        IHostInfoService hostInfoService,
        HyperV.Core.Wmi.Services.VmService wmiVmService,
        ILogger<PrometheusMetricsCollector> logger)
    {
        _hostInfoService = hostInfoService;
        _wmiVmService = wmiVmService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Prometheus Metrics Collector started");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect Prometheus metrics");
            }

            await Task.Delay(_collectionInterval, stoppingToken);
        }
    }

    private async Task CollectMetricsAsync()
    {
        // Host metrics via IHostInfoService
        try
        {
            var perf = await _hostInfoService.GetPerformanceSummaryAsync();
            var hardware = await _hostInfoService.GetHostHardwareInfoAsync();
            var totalMemMb = hardware.TotalPhysicalMemory / 1024 / 1024;
            var availMemMb = totalMemMb * (1.0 - perf.MemoryUsagePercent / 100.0);

            HostCpuUsage.Set(perf.CpuUsagePercent);
            HostMemoryTotalMb.Set(totalMemMb);
            HostMemoryAvailableMb.Set(availMemMb);
            HostMemoryUsagePercent.Set(perf.MemoryUsagePercent);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect host metrics");
        }

        // VM metrics via WMI VmService.ListVms() JSON parsing
        try
        {
            var json = _wmiVmService.ListVms();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("VMs", out var vmsArray))
            {
                var stateGroups = new Dictionary<string, int>();

                foreach (var vm in vmsArray.EnumerateArray())
                {
                    var state = vm.GetProperty("State").GetString() ?? "Unknown";
                    stateGroups[state] = stateGroups.GetValueOrDefault(state, 0) + 1;

                    // Extract per-VM memory if available
                    var vmName = vm.GetProperty("Name").GetString() ?? "unknown";
                    if (vm.TryGetProperty("MemoryAssigned", out var memProp))
                    {
                        var memMb = memProp.GetInt64() / 1024 / 1024;
                        VmMemoryAssignedMb.WithLabels(vmName).Set(memMb);
                    }
                }

                foreach (var group in stateGroups)
                {
                    VmCount.WithLabels(group.Key).Set(group.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to collect VM metrics");
        }
    }
}
