using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum HaEventType
{
    HostFailureDetected = 0,
    VmRestarted = 1,
    VmRestartFailed = 2,
    HostRecovered = 3,
    AdmissionControlViolation = 4
}

public class HaEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClusterId { get; set; }

    public HaEventType EventType { get; set; }

    public Guid? AgentHostId { get; set; }

    [MaxLength(200)]
    public string? AgentHostName { get; set; }

    public Guid? VmInventoryId { get; set; }

    [MaxLength(200)]
    public string? VmName { get; set; }

    [MaxLength(2000)]
    public string? Details { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
