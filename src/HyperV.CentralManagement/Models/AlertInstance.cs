using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum AlertInstanceStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2
}

public class AlertInstance
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AlertDefinitionId { get; set; }
    public AlertDefinition? AlertDefinition { get; set; }

    public AlertInstanceStatus Status { get; set; } = AlertInstanceStatus.Active;

    public double CurrentValue { get; set; }

    /// <summary>
    /// Which agent host triggered this alert
    /// </summary>
    public Guid? AgentHostId { get; set; }

    [MaxLength(200)]
    public string? AgentHostName { get; set; }

    /// <summary>
    /// Which VM triggered this alert (if VM-level metric)
    /// </summary>
    public Guid? VmInventoryId { get; set; }

    [MaxLength(200)]
    public string? VmName { get; set; }

    public DateTimeOffset FiredUtc { get; set; } = DateTimeOffset.UtcNow;

    [MaxLength(200)]
    public string? AcknowledgedBy { get; set; }

    public DateTimeOffset? AcknowledgedUtc { get; set; }

    public DateTimeOffset? ResolvedUtc { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}
