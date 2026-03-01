using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum DrsAutomationLevel
{
    Manual = 0,
    Partial = 1,
    Full = 2
}

public class DrsConfiguration
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClusterId { get; set; }
    public Cluster? Cluster { get; set; }

    public bool IsEnabled { get; set; }

    public DrsAutomationLevel AutomationLevel { get; set; } = DrsAutomationLevel.Manual;

    /// <summary>
    /// CPU imbalance threshold (percent difference between most/least loaded host)
    /// </summary>
    public int CpuImbalanceThreshold { get; set; } = 25;

    /// <summary>
    /// Memory imbalance threshold
    /// </summary>
    public int MemoryImbalanceThreshold { get; set; } = 25;

    /// <summary>
    /// Evaluation interval in seconds
    /// </summary>
    public int EvaluationIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Minimum improvement (percent) required for a migration recommendation
    /// </summary>
    public int MinBenefitPercent { get; set; } = 10;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedUtc { get; set; }
}
