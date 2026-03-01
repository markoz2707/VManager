using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum DrsRecommendationStatus
{
    Pending = 0,
    Approved = 1,
    Applied = 2,
    Rejected = 3,
    Failed = 4
}

public class DrsRecommendation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DrsConfigurationId { get; set; }

    public Guid VmInventoryId { get; set; }

    [MaxLength(200)]
    public string VmName { get; set; } = string.Empty;

    public Guid SourceAgentId { get; set; }

    [MaxLength(200)]
    public string SourceAgentName { get; set; } = string.Empty;

    public Guid DestinationAgentId { get; set; }

    [MaxLength(200)]
    public string DestinationAgentName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Estimated improvement in cluster balance (percent)
    /// </summary>
    public double EstimatedBenefitPercent { get; set; }

    public DrsRecommendationStatus Status { get; set; } = DrsRecommendationStatus.Pending;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? AppliedUtc { get; set; }

    [MaxLength(200)]
    public string? AppliedBy { get; set; }
}
