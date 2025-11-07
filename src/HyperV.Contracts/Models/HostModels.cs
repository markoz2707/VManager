using System;
using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Host hardware information.
    /// </summary>
    public class HostHardwareInfo
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string BiosVersion { get; set; } = string.Empty;
        public string BiosSerialNumber { get; set; } = string.Empty;
        public string SystemUuid { get; set; } = string.Empty;
        public string MotherboardManufacturer { get; set; } = string.Empty;
        public string MotherboardModel { get; set; } = string.Empty;
        public ulong TotalPhysicalMemory { get; set; } // In bytes
    }

    /// <summary>
    /// System information (OS details).
    /// </summary>
    public class SystemInfo
    {
        public string OsName { get; set; } = string.Empty;
        public string OsVersion { get; set; } = string.Empty;
        public string OsBuildNumber { get; set; } = string.Empty;
        public DateTime LastBootUpTime { get; set; }
        public ulong TotalVisibleMemorySize { get; set; } // In KB
        public ulong FreePhysicalMemory { get; set; } // In KB
    }

    /// <summary>
    /// Current performance summary.
    /// </summary>
    public class PerformanceSummary
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsagePercent { get; set; }
        public Dictionary<string, double> StorageUsagePercent { get; set; } = new(); // Drive -> %
    }

    /// <summary>
    /// Recent task/job information.
    /// </summary>
    public class RecentTask
    {
        public string Target { get; set; } = string.Empty;
        public string Initiator { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Started { get; set; }
        public string Result { get; set; } = string.Empty;
    }
}