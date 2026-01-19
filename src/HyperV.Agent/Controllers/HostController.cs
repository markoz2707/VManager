using Microsoft.AspNetCore.Mvc;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using System.ComponentModel.DataAnnotations;

namespace HyperV.Agent.Controllers
{
    [ApiController]
    [Route("api/v1/host")]
    public class HostController : ControllerBase
    {
        private readonly IHostInfoService _hostService;

        public HostController(IHostInfoService hostService)
        {
            _hostService = hostService;
        }

        /// <summary>
        /// Gets host hardware information.
        /// </summary>
        [HttpGet("hardware")]
        public async Task<ActionResult<HostHardwareInfo>> GetHardware()
        {
            try
            {
                var hardware = await _hostService.GetHostHardwareInfoAsync();
                return Ok(hardware);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets system information (OS details).
        /// </summary>
        [HttpGet("system")]
        public async Task<ActionResult<SystemInfo>> GetSystem()
        {
            try
            {
                var system = await _hostService.GetSystemInfoAsync();
                return Ok(system);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets current performance summary.
        /// </summary>
        [HttpGet("performance")]
        public async Task<ActionResult<PerformanceSummary>> GetPerformance()
        {
            try
            {
                var performance = await _hostService.GetPerformanceSummaryAsync();
                return Ok(performance);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets recent tasks (last hour by default).
        /// </summary>
        /// <param name="limit">Number of tasks (default: 10).</param>
        [HttpGet("tasks")]
        public async Task<ActionResult<List<RecentTask>>> GetRecentTasks([FromQuery, Range(1, 100)] int limit = 10)
        {
            try
            {
                var tasks = await _hostService.GetRecentTasksAsync(limit);
                return Ok(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets combined host details (hardware, system, performance).
        /// </summary>
        [HttpGet("details")]
        public async Task<ActionResult<HostDetails>> GetHostDetails()
        {
            try
            {
                var hardware = await _hostService.GetHostHardwareInfoAsync();
                var system = await _hostService.GetSystemInfoAsync();
                var performance = await _hostService.GetPerformanceSummaryAsync();

                var details = new HostDetails
                {
                    Hardware = hardware,
                    System = system,
                    Performance = performance
                };

                return Ok(details);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets performance stats (CPU, memory, storage).
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<PerformanceSummary>> GetHostStats()
        {
            try
            {
                var stats = await _hostService.GetPerformanceSummaryAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Gets detailed usage metrics for the physical host.
        /// </summary>
        [HttpGet("metrics/usage")]
        [ProducesResponseType(typeof(HostUsageSummary), 200)]
        public IActionResult GetHostUsageMetrics()
        {
            try
            {
                var usage = GetHostMetrics();
                return Ok(usage);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private HostUsageSummary GetHostMetrics()
        {
            var usage = new HostUsageSummary();

            try
            {
                // Get CPU metrics
                usage.Cpu = GetCpuMetrics();

                // Get Memory metrics
                usage.Memory = GetMemoryMetrics();

                // Get Physical Disk metrics
                usage.PhysicalDisks = GetPhysicalDiskMetrics();

                // Get Network Adapter metrics
                usage.NetworkAdapters = GetNetworkAdapterMetrics();

                // Get Storage Adapter metrics
                usage.StorageAdapters = GetStorageAdapterMetrics();
            }
            catch (Exception ex)
            {
                // Log error but return partial data
                Console.WriteLine($"Error collecting host metrics: {ex.Message}");
            }

            return usage;
        }

        private HostCpuUsage GetCpuMetrics()
        {
            var cpu = new HostCpuUsage();

            try
            {
                var scope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
                scope.Connect();

                // Get CPU usage percentage
                var cpuQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_Processor");
                using var searcher = new System.Management.ManagementObjectSearcher(scope, cpuQuery);
                var cpuCollection = searcher.Get();

                foreach (System.Management.ManagementObject cpuObj in cpuCollection)
                {
                    var loadPercentage = cpuObj["LoadPercentage"];
                    if (loadPercentage != null)
                    {
                        cpu.UsagePercent = Convert.ToDouble(loadPercentage);
                    }

                    var cores = cpuObj["NumberOfCores"];
                    if (cores != null)
                    {
                        cpu.Cores = Convert.ToInt32(cores);
                    }

                    var logicalProcessors = cpuObj["NumberOfLogicalProcessors"];
                    if (logicalProcessors != null)
                    {
                        cpu.LogicalProcessors = Convert.ToInt32(logicalProcessors);
                    }

                    break; // Use first processor info
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting CPU metrics: {ex.Message}");
            }

            return cpu;
        }

        private HostMemoryUsage GetMemoryMetrics()
        {
            var memory = new HostMemoryUsage();

            try
            {
                var scope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
                scope.Connect();

                // Get total physical memory
                var osQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                using var osSearcher = new System.Management.ManagementObjectSearcher(scope, osQuery);
                var osCollection = osSearcher.Get();

                foreach (System.Management.ManagementObject os in osCollection)
                {
                    var totalMemoryKB = os["TotalVisibleMemorySize"];
                    var freeMemoryKB = os["FreePhysicalMemory"];

                    if (totalMemoryKB != null)
                    {
                        memory.TotalPhysicalMB = Convert.ToInt64(totalMemoryKB) / 1024;
                    }

                    if (freeMemoryKB != null)
                    {
                        memory.AvailableMB = Convert.ToInt64(freeMemoryKB) / 1024;
                        memory.UsedMB = memory.TotalPhysicalMB - memory.AvailableMB;

                        if (memory.TotalPhysicalMB > 0)
                        {
                            memory.UsagePercent = Math.Round((double)memory.UsedMB / memory.TotalPhysicalMB * 100, 2);
                        }
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting memory metrics: {ex.Message}");
            }

            return memory;
        }

        private List<PhysicalDiskUsage> GetPhysicalDiskMetrics()
        {
            var disks = new List<PhysicalDiskUsage>();

            try
            {
                var scope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
                scope.Connect();

                var diskQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name != '_Total'");
                using var searcher = new System.Management.ManagementObjectSearcher(scope, diskQuery);
                var diskCollection = searcher.Get();

                foreach (System.Management.ManagementObject disk in diskCollection)
                {
                    var diskUsage = new PhysicalDiskUsage
                    {
                        Name = disk["Name"]?.ToString() ?? "Unknown"
                    };

                    var diskReadsPerSec = disk["DiskReadsPerSec"];
                    var diskWritesPerSec = disk["DiskWritesPerSec"];
                    if (diskReadsPerSec != null && diskWritesPerSec != null)
                    {
                        diskUsage.Iops = Convert.ToInt64(diskReadsPerSec) + Convert.ToInt64(diskWritesPerSec);
                    }

                    var diskReadBytesPerSec = disk["DiskReadBytesPerSec"];
                    var diskWriteBytesPerSec = disk["DiskWriteBytesPerSec"];
                    if (diskReadBytesPerSec != null && diskWriteBytesPerSec != null)
                    {
                        diskUsage.ThroughputMBps = Math.Round((Convert.ToDouble(diskReadBytesPerSec) + Convert.ToDouble(diskWriteBytesPerSec)) / 1024 / 1024, 2);
                    }

                    var avgDiskSecPerTransfer = disk["AvgDiskSecPerTransfer"];
                    if (avgDiskSecPerTransfer != null)
                    {
                        diskUsage.LatencyMs = Math.Round(Convert.ToDouble(avgDiskSecPerTransfer) * 1000, 2);
                    }

                    var currentDiskQueueLength = disk["CurrentDiskQueueLength"];
                    if (currentDiskQueueLength != null)
                    {
                        diskUsage.QueueLength = Convert.ToInt64(currentDiskQueueLength);
                    }

                    disks.Add(diskUsage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting disk metrics: {ex.Message}");
            }

            return disks;
        }

        private List<HostNetworkAdapterUsage> GetNetworkAdapterMetrics()
        {
            var adapters = new List<HostNetworkAdapterUsage>();

            try
            {
                var scope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
                scope.Connect();

                // Get network adapter performance data
                var perfQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_PerfFormattedData_Tcpip_NetworkInterface");
                using var perfSearcher = new System.Management.ManagementObjectSearcher(scope, perfQuery);
                var perfCollection = perfSearcher.Get();

                foreach (System.Management.ManagementObject perf in perfCollection)
                {
                    var adapter = new HostNetworkAdapterUsage
                    {
                        Name = perf["Name"]?.ToString() ?? "Unknown"
                    };

                    var currentBandwidth = perf["CurrentBandwidth"];
                    if (currentBandwidth != null)
                    {
                        adapter.SpeedMbps = Convert.ToInt64(currentBandwidth) / 1000000;
                    }

                    var bytesSentPerSec = perf["BytesSentPerSec"];
                    if (bytesSentPerSec != null)
                    {
                        adapter.BytesSentPerSec = Convert.ToInt64(bytesSentPerSec);
                    }

                    var bytesReceivedPerSec = perf["BytesReceivedPerSec"];
                    if (bytesReceivedPerSec != null)
                    {
                        adapter.BytesReceivedPerSec = Convert.ToInt64(bytesReceivedPerSec);
                    }

                    adapters.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting network adapter metrics: {ex.Message}");
            }

            return adapters;
        }

        private List<HostStorageAdapterUsage> GetStorageAdapterMetrics()
        {
            var adapters = new List<HostStorageAdapterUsage>();

            try
            {
                var scope = new System.Management.ManagementScope("\\\\.\\root\\cimv2");
                scope.Connect();

                // Get SCSI controllers (HBAs, RAID controllers)
                var controllerQuery = new System.Management.ObjectQuery("SELECT * FROM Win32_SCSIController");
                using var searcher = new System.Management.ManagementObjectSearcher(scope, controllerQuery);
                var controllerCollection = searcher.Get();

                foreach (System.Management.ManagementObject controller in controllerCollection)
                {
                    var adapter = new HostStorageAdapterUsage
                    {
                        Name = controller["Name"]?.ToString() ?? "Unknown",
                        Type = controller["Description"]?.ToString() ?? "SCSI Controller"
                    };

                    var status = controller["Status"];
                    if (status != null)
                    {
                        adapter.Status = status.ToString() ?? "Unknown";
                    }

                    // Throughput would require performance counters specific to the adapter
                    // For now, set to 0 as placeholder
                    adapter.ThroughputMBps = 0;

                    adapters.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting storage adapter metrics: {ex.Message}");
            }

            return adapters;
        }
    }

    /// <summary>
    /// Combined host details model.
    /// </summary>
    public class HostDetails
    {
        public HostHardwareInfo Hardware { get; set; } = new();
        public SystemInfo System { get; set; } = new();
        public PerformanceSummary Performance { get; set; } = new();
    }

    /// <summary>
    /// Host usage metrics summary.
    /// </summary>
    public class HostUsageSummary
    {
        public HostCpuUsage Cpu { get; set; } = new();
        public HostMemoryUsage Memory { get; set; } = new();
        public List<PhysicalDiskUsage> PhysicalDisks { get; set; } = new();
        public List<HostNetworkAdapterUsage> NetworkAdapters { get; set; } = new();
        public List<HostStorageAdapterUsage> StorageAdapters { get; set; } = new();
    }

    /// <summary>
    /// Host CPU usage metrics.
    /// </summary>
    public class HostCpuUsage
    {
        public double UsagePercent { get; set; }
        public int Cores { get; set; }
        public int LogicalProcessors { get; set; }
    }

    /// <summary>
    /// Host memory usage metrics.
    /// </summary>
    public class HostMemoryUsage
    {
        public long TotalPhysicalMB { get; set; }
        public long AvailableMB { get; set; }
        public long UsedMB { get; set; }
        public double UsagePercent { get; set; }
    }

    /// <summary>
    /// Physical disk usage metrics.
    /// </summary>
    public class PhysicalDiskUsage
    {
        public string Name { get; set; } = string.Empty;
        public long Iops { get; set; }
        public double ThroughputMBps { get; set; }
        public double LatencyMs { get; set; }
        public long QueueLength { get; set; }
    }

    /// <summary>
    /// Network adapter usage metrics.
    /// </summary>
    public class HostNetworkAdapterUsage
    {
        public string Name { get; set; } = string.Empty;
        public long SpeedMbps { get; set; }
        public long BytesSentPerSec { get; set; }
        public long BytesReceivedPerSec { get; set; }
    }

    /// <summary>
    /// Storage adapter (HBA/RAID controller) usage metrics.
    /// </summary>
    public class HostStorageAdapterUsage
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double ThroughputMBps { get; set; }
        public string Status { get; set; } = "Unknown";
    }
}