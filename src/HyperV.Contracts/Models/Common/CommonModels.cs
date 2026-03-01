namespace HyperV.Contracts.Models.Common;

// VM models
public class VmSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "Unknown";
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class VmDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "Unknown";
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public int Generation { get; set; }
    public string? OperatingSystem { get; set; }
    public List<string> DiskPaths { get; set; } = new();
    public List<string> NetworkAdapters { get; set; } = new();
    public DateTime? CreatedTime { get; set; }
    public TimeSpan? Uptime { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class VmPropertiesDto
{
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public bool EnableDynamicMemory { get; set; }
    public long? MinMemoryMB { get; set; }
    public long? MaxMemoryMB { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class CreateVmSpec
{
    public string Name { get; set; } = string.Empty;
    public int CpuCount { get; set; } = 1;
    public long MemoryMB { get; set; } = 1024;
    public int Generation { get; set; } = 2;
    public string? DiskPath { get; set; }
    public long DiskSizeGB { get; set; } = 40;
    public string? NetworkName { get; set; }
    public string? IsoPath { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class VmSnapshotDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public string? ParentId { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class ConsoleInfoDto
{
    public string Type { get; set; } = string.Empty; // "rdp", "vnc", "spice"
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Token { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Network models
public class VirtualNetworkInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "External", "Internal", "Private", "NAT", "Bridge"
    public bool IsExternal { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class CreateNetworkSpec
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Internal";
    public string? PhysicalAdapterId { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class VmNetworkAdapterDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NetworkName { get; set; }
    public string? MacAddress { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class PhysicalAdapterDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Speed { get; set; }
    public string Status { get; set; } = string.Empty;
}

// VLAN models
public enum VlanOperationMode
{
    Access = 1,
    Trunk = 2,
    Private = 3
}

public class VlanConfiguration
{
    public int VlanId { get; set; }
    public VlanOperationMode OperationMode { get; set; } = VlanOperationMode.Access;
    public int? NativeVlanId { get; set; }
    public int[]? TrunkVlanIds { get; set; }
}

public class SetVlanRequest
{
    public string VmName { get; set; } = string.Empty;
    public int VlanId { get; set; }
    public VlanOperationMode OperationMode { get; set; } = VlanOperationMode.Access;
    public int? NativeVlanId { get; set; }
    public int[]? TrunkVlanIds { get; set; }
}

// Storage models
public class CreateDiskSpec
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Format { get; set; } = "vhdx"; // vhdx, qcow2, raw
    public bool Dynamic { get; set; } = true;
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class DiskInfoDto
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long UsedBytes { get; set; }
    public string Format { get; set; } = string.Empty;
    public bool IsDynamic { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class StoragePoolDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class StorageCapacityDto
{
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public List<StoragePoolDto> Pools { get; set; } = new();
}

// Host models
public class HostInfoDto
{
    public string Hostname { get; set; } = string.Empty;
    public string HypervisorType { get; set; } = string.Empty;
    public string HypervisorVersion { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public int LogicalProcessors { get; set; }
    public long TotalMemoryMB { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class HostPerformanceMetrics
{
    public double CpuUsagePercent { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long MemoryAvailableMB { get; set; }
    public Dictionary<string, double>? StorageUsagePercent { get; set; }
}

public class HypervisorCapabilities
{
    public string HypervisorType { get; set; } = string.Empty;
    public bool SupportsLiveMigration { get; set; }
    public bool SupportsSnapshots { get; set; }
    public bool SupportsDynamicMemory { get; set; }
    public bool SupportsNestedVirtualization { get; set; }
    public bool SupportsContainers { get; set; }
    public bool SupportsReplication { get; set; }
    public bool SupportsFibreChannel { get; set; }
    public bool SupportsStorageQoS { get; set; }
    public string ConsoleType { get; set; } = "rdp"; // "rdp", "vnc", "spice"
    public int MaxVmCount { get; set; }
    public int MaxCpuPerVm { get; set; }
    public long MaxMemoryPerVmMB { get; set; }
    public List<string> SupportedDiskFormats { get; set; } = new();
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Metrics models
public class HostUsageDto
{
    public double CpuUsagePercent { get; set; }
    public int CpuCores { get; set; }
    public int LogicalProcessors { get; set; }
    public long TotalMemoryMB { get; set; }
    public long AvailableMemoryMB { get; set; }
    public double MemoryUsagePercent { get; set; }
}

public class VmUsageDto
{
    public double CpuUsagePercent { get; set; }
    public long MemoryAssignedMB { get; set; }
    public long MemoryDemandMB { get; set; }
    public double MemoryUsagePercent { get; set; }
}

public class DiskMetricsDto
{
    public long ReadBytesPerSec { get; set; }
    public long WriteBytesPerSec { get; set; }
    public long ReadOperationsPerSec { get; set; }
    public long WriteOperationsPerSec { get; set; }
}

public class NetworkMetricsDto
{
    public long BytesSentPerSec { get; set; }
    public long BytesReceivedPerSec { get; set; }
}

// Container models
public class ContainerSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "Unknown";
    public string Image { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty; // "HCS", "WMI", "Docker", "Podman"
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class ContainerDetailsDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = "Unknown";
    public string Image { get; set; } = string.Empty;
    public string Backend { get; set; } = string.Empty;
    public int CpuCount { get; set; }
    public long MemoryMB { get; set; }
    public DateTime? CreatedTime { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class CreateContainerSpec
{
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public int CpuCount { get; set; } = 1;
    public long MemoryMB { get; set; } = 512;
    public long StorageSizeGB { get; set; } = 20;
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Guest agent models
public class GuestInfoDto
{
    public string? Hostname { get; set; }
    public string? OsType { get; set; }
    public string? OsVersion { get; set; }
    public string? IpAddress { get; set; }
    public bool IsAgentAvailable { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Bulk operation models
public class BulkOperationResultDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<BulkOperationItemResult> Results { get; set; } = new();
}

public class BulkOperationItemResult
{
    public string VmName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

// VM configuration spec
public class VmConfigurationSpec
{
    public int? CpuCount { get; set; }
    public long? MemoryMB { get; set; }
    public string? Notes { get; set; }
    public bool? EnableDynamicMemory { get; set; }
    public long? MinMemoryMB { get; set; }
    public long? MaxMemoryMB { get; set; }
    public int? NumaNodesCount { get; set; }
    public int? NumaMemoryPerNode { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Storage device models
public class StorageDeviceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "VirtualHardDisk", "VirtualDVD", "VirtualFloppy", "disk", "cdrom"
    public string? Path { get; set; }
    public string? ControllerType { get; set; }
    public int? ControllerNumber { get; set; }
    public int? ControllerLocation { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class StorageControllerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "IDE", "SCSI", "virtio-scsi", "sata"
    public int ControllerNumber { get; set; }
    public int MaxDevices { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class AddStorageDeviceSpec
{
    public string Type { get; set; } = "VirtualHardDisk"; // VirtualHardDisk, VirtualDVD, disk, cdrom
    public string? Path { get; set; }
    public string? ControllerType { get; set; }
    public int? ControllerNumber { get; set; }
    public int? ControllerLocation { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

// Event log models
public class LogEntryDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Information"; // Information, Warning, Error, Critical
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? EventId { get; set; }
    public string? Category { get; set; }
}

public class LogsResponse
{
    public List<LogEntryDto> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public List<string> Sources { get; set; } = new();
}

// Pagination models
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}

// Backup models
public class BackupResultDto
{
    public string BackupId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Message { get; set; }
    public long SizeBytes { get; set; }
}

public class RestoreResultDto
{
    public bool Success { get; set; }
    public string? RestoredVmName { get; set; }
    public string? Message { get; set; }
}

public class BackupInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string VmName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public long SizeBytes { get; set; }
    public string BackupPath { get; set; } = string.Empty;
    public string HypervisorType { get; set; } = string.Empty;
    public bool IncludesSnapshots { get; set; }
}

public class BackupOptions
{
    public bool IncludeSnapshots { get; set; }
    public string? Description { get; set; }
}

// Scheduled task models
public class ScheduledTaskDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // start, stop, shutdown, snapshot
    public string[] TargetVms { get; set; } = Array.Empty<string>();
    public bool IsEnabled { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public string? LastRunResult { get; set; }
}

public class CreateScheduledTaskRequest
{
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string[] TargetVms { get; set; } = Array.Empty<string>();
}

// Migration models
public class MigrationResultDto
{
    public bool Success { get; set; }
    public string? JobId { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object>? ExtendedProperties { get; set; }
}

public class MigrationStatusDto
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
}
