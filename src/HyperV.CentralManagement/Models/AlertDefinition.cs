using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AlertCondition
{
    GreaterThan = 0,
    LessThan = 1,
    Equals = 2
}

public class AlertDefinition
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    public AlertCondition Condition { get; set; }

    public double ThresholdValue { get; set; }

    public AlertSeverity Severity { get; set; } = AlertSeverity.Warning;

    /// <summary>
    /// How many consecutive evaluation periods the condition must be true before firing
    /// </summary>
    public int EvaluationPeriods { get; set; } = 1;

    /// <summary>
    /// Cooldown in seconds before the same alert can fire again
    /// </summary>
    public int CooldownSeconds { get; set; } = 300;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Optional scope: specific cluster
    /// </summary>
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Optional scope: specific agent host
    /// </summary>
    public Guid? AgentHostId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<AlertInstance> Instances { get; set; } = new();
    public List<AlertNotificationChannel> NotificationChannels { get; set; } = new();
}
