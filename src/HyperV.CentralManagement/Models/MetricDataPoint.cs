using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class MetricDataPoint
{
    [Key]
    public long Id { get; set; }

    public Guid AgentHostId { get; set; }

    /// <summary>
    /// Optional: VM-level metric
    /// </summary>
    public Guid? VmInventoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string MetricName { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
