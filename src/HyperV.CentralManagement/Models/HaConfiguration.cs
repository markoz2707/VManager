using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class HaConfiguration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClusterId { get; set; }
    public Cluster? Cluster { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>
    /// Heartbeat interval in seconds
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Number of missed heartbeats before declaring host failure
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// Enable admission control to reserve capacity for failover
    /// </summary>
    public bool AdmissionControl { get; set; } = true;

    /// <summary>
    /// Percent of cluster CPU to reserve for failover
    /// </summary>
    public int ReservedCpuPercent { get; set; } = 25;

    /// <summary>
    /// Percent of cluster memory to reserve for failover
    /// </summary>
    public int ReservedMemoryPercent { get; set; } = 25;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }

    public List<HaVmOverride> VmOverrides { get; set; } = new();
}
