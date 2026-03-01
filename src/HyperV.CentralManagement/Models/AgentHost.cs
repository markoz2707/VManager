using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum AgentStatus
{
    Unknown = 0,
    Online = 1,
    Offline = 2
}

public class AgentHost
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Hostname { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ApiBaseUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? IpAddress { get; set; }

    [MaxLength(200)]
    public string? HyperVVersion { get; set; }

    [MaxLength(100)]
    public string? HypervisorVersion { get; set; }

    [MaxLength(200)]
    public string? OperatingSystem { get; set; }

    [MaxLength(50)]
    public string? AgentVersion { get; set; }

    [MaxLength(50)]
    public string HostType { get; set; } = "Hyper-V";

    [MaxLength(2000)]
    public string? Tags { get; set; }

    public AgentStatus Status { get; set; } = AgentStatus.Unknown;

    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.MinValue;

    [MaxLength(200)]
    public string? ClusterName { get; set; }

    public bool IsInMaintenanceMode { get; set; }
    public DateTimeOffset? MaintenanceModeStartedUtc { get; set; }

    public Guid? DatacenterId { get; set; }
    public Datacenter? Datacenter { get; set; }
}
