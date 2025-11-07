using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using HyperV.Contracts.Interfaces;
using HyperV.Contracts.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace HyperV.Core.Wmi.Services
{
    /// <summary>
    /// Service for retrieving host information, hardware, system, performance, and recent tasks.
    /// Uses WMI queries to gather data for the Host dashboard.
    /// </summary>
    public class HostInfoService : IHostInfoService
    {
        private readonly ILogger<HostInfoService> _logger;
        private readonly IMemoryCache _cache;
        private const string Cimv2Namespace = @"root\CIMV2";
        private const string VirtualizationNamespace = @"root\virtualization\v2";

        public HostInfoService(ILogger<HostInfoService> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Retrieves hardware information for the host.
        /// </summary>
        /// <returns>Host hardware details.</returns>
        public async Task<HostHardwareInfo> GetHostHardwareInfoAsync()
        {
            const string cacheKey = "HostHardwareInfo";
            if (_cache.TryGetValue(cacheKey, out HostHardwareInfo cachedInfo))
            {
                _logger.LogInformation("Returning cached host hardware info");
                return cachedInfo;
            }

            var info = await Task.Run(() =>
            {
                var scope = new ManagementScope(Cimv2Namespace);
                scope.Connect();

                var hardware = new HostHardwareInfo();

                // Computer System (Model, Manufacturer)
                var systemQuery = new ObjectQuery("SELECT * FROM Win32_ComputerSystem");
                using var systemSearcher = new ManagementObjectSearcher(scope, systemQuery);
                using var systemResults = systemSearcher.Get();
                foreach (ManagementObject system in systemResults)
                {
                    hardware.Manufacturer = system["Manufacturer"]?.ToString() ?? "Unknown";
                    hardware.Model = system["Model"]?.ToString() ?? "Unknown";
                    hardware.TotalPhysicalMemory = (ulong?)system["TotalPhysicalMemory"] ?? 0;
                    system.Dispose();
                }

                // BIOS (Version, Serial Number)
                var biosQuery = new ObjectQuery("SELECT * FROM Win32_BIOS");
                using var biosSearcher = new ManagementObjectSearcher(scope, biosQuery);
                using var biosResults = biosSearcher.Get();
                foreach (ManagementObject bios in biosResults)
                {
                    hardware.BiosVersion = bios["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
                    hardware.BiosSerialNumber = bios["SerialNumber"]?.ToString() ?? "Unknown";
                    bios.Dispose();
                }

                // Base Board (Motherboard details, if needed)
                var boardQuery = new ObjectQuery("SELECT * FROM Win32_BaseBoard");
                using var boardSearcher = new ManagementObjectSearcher(scope, boardQuery);
                using var boardResults = boardSearcher.Get();
                foreach (ManagementObject board in boardResults)
                {
                    hardware.MotherboardManufacturer = board["Manufacturer"]?.ToString() ?? "Unknown";
                    hardware.MotherboardModel = board["Product"]?.ToString() ?? "Unknown";
                    board.Dispose();
                }

                // System UUID
                var uuidQuery = new ObjectQuery("SELECT * FROM Win32_ComputerSystemProduct");
                using var uuidSearcher = new ManagementObjectSearcher(scope, uuidQuery);
                using var uuidResults = uuidSearcher.Get();
                foreach (ManagementObject uuidObj in uuidResults)
                {
                    hardware.SystemUuid = uuidObj["UUID"]?.ToString() ?? "Unknown";
                    uuidObj.Dispose();
                }

                return hardware;
            });

            _cache.Set(cacheKey, info, TimeSpan.FromMinutes(10));
            _logger.LogInformation("Cached host hardware info");
            return info;
        }

        /// <summary>
        /// Retrieves system information (OS details).
        /// </summary>
        /// <returns>System information.</returns>
        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(Cimv2Namespace);
                scope.Connect();

                var info = new SystemInfo();

                var osQuery = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                using var osSearcher = new ManagementObjectSearcher(scope, osQuery);
                using var osResults = osSearcher.Get();
                foreach (ManagementObject os in osResults)
                {
                    info.OsName = os["Caption"]?.ToString() ?? "Unknown";
                    info.OsVersion = os["Version"]?.ToString() ?? "Unknown";
                    info.OsBuildNumber = os["BuildNumber"]?.ToString() ?? "Unknown";
                    info.LastBootUpTime = ManagementDateTimeConverter.ToDateTime(os["LastBootUpTime"]?.ToString() ?? string.Empty);
                    info.TotalVisibleMemorySize = (ulong?)os["TotalVisibleMemorySize"] ?? 0;
                    info.FreePhysicalMemory = (ulong?)os["FreePhysicalMemory"] ?? 0;
                    os.Dispose();
                }

                return info;
            });
        }

        /// <summary>
        /// Retrieves current performance summary (CPU, Memory, Storage usage).
        /// </summary>
        /// <returns>Performance summary.</returns>
        public async Task<PerformanceSummary> GetPerformanceSummaryAsync()
        {
            return await Task.Run(() =>
            {
                var scope = new ManagementScope(Cimv2Namespace);
                scope.Connect();

                var summary = new PerformanceSummary
                {
                    CpuUsagePercent = 0,
                    MemoryUsagePercent = 0,
                    StorageUsagePercent = new Dictionary<string, double>()
                };

                // CPU Usage (% Processor Time, average across cores)
                try
                {
                    var cpuQuery = new ObjectQuery("SELECT * FROM Win32_PerfFormattedData_PerfProc_Processor WHERE Name='_Total'");
                    using var cpuSearcher = new ManagementObjectSearcher(scope, cpuQuery);
                    using var cpuResults = cpuSearcher.Get();
                    // Sprawdź, czy kolekcja nie jest pusta i nie rzuca wyj�tku
                    foreach (ManagementObject cpu in cpuResults)
                    {
                        summary.CpuUsagePercent = double.Parse(cpu["PercentProcessorTime"]?.ToString() ?? "0");
                        cpu.Dispose();
                    }
                }
                catch (ManagementException)
                {
                    // Logowanie lub alternatywna obsuga
                    summary.CpuUsagePercent = 0; // lub inna warto domylna
                }

                // Memory Usage
                try
                {
                    var memQuery = new ObjectQuery("SELECT * FROM Win32_PerfFormattedData_PerfOS_Memory");
                    using var memSearcher = new ManagementObjectSearcher(scope, memQuery);
                    using var memResults = memSearcher.Get();
                    foreach (ManagementObject mem in memResults)
                    {
                        var availableBytes = ulong.Parse(mem["AvailableBytes"]?.ToString() ?? "0");
                        var totalBytes = ulong.Parse(mem["TotalVisibleMemorySize"]?.ToString() ?? "0") * 1024;
                        summary.MemoryUsagePercent = totalBytes > 0 ? (1 - (double)availableBytes / totalBytes) * 100 : 0;
                        mem.Dispose();
                    }
                }
                catch (ManagementException)
                {
                    summary.MemoryUsagePercent = 0;
                }

                // Storage Usage
                try
                {
                    var diskQuery = new ObjectQuery("SELECT * FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name LIKE '%:%'");
                    using var diskSearcher = new ManagementObjectSearcher(scope, diskQuery);
                    using var diskResults = diskSearcher.Get();
                    foreach (ManagementObject disk in diskResults)
                    {
                        var drive = disk["Name"]?.ToString() ?? string.Empty;
                        var freePercent = double.Parse(disk["PercentFreeSpace"]?.ToString() ?? "0");
                        summary.StorageUsagePercent[drive] = 100 - freePercent;
                        disk.Dispose();
                    }
                }
                catch (ManagementException)
                {
                    summary.StorageUsagePercent.Clear();
                }

                return summary;
            });
        }

        /// <summary>
        /// Retrieves recent Hyper-V tasks/jobs.
        /// </summary>
        /// <param name="limit">Number of recent tasks to return (default: 10).</param>
        /// <returns>List of recent tasks.</returns>
        public async Task<List<RecentTask>> GetRecentTasksAsync(int limit = 10)
        {
            return await Task.Run(() =>
            {
                var tasks = new List<RecentTask>();
                var scope = new ManagementScope(VirtualizationNamespace);
                scope.Connect();

                // Query recent Msvm_Job instances (Hyper-V jobs)
                var endTime = DateTime.Now.AddHours(-1).ToString("yyyyMMddHHmmss.000000+000"); // Last hour
                var query = new ObjectQuery($"SELECT * FROM Msvm_Job WHERE TimeSubmitted > '{endTime}' ORDER BY TimeSubmitted DESC");
                using var searcher = new ManagementObjectSearcher(scope, query);
                using var results = searcher.Get();

                var count = 0;
                foreach (ManagementObject job in results)
                {
                    if (count >= limit) break;

                    try
                    {
                        var task = new RecentTask
                        {
                            Target = job["TargetInstance"]?.ToString() ?? "Unknown",
                            Initiator = job["Owner"]?.ToString() ?? "System",
                            Status = (uint?)job["JobState"] switch
                            {
                                7 => "Completed",
                                4 => "Running",
                                10 => "Failed",
                                _ => "Pending"
                            },
                            Started = ManagementDateTimeConverter.ToDateTime(job["TimeSubmitted"]?.ToString() ?? string.Empty),
                            Result = job["ErrorCode"]?.ToString() ?? "0"
                        };
                        tasks.Add(task);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing job");
                    }
                    finally
                    {
                        job.Dispose();
                    }
                }

                return tasks;
            });
        }
    }

}