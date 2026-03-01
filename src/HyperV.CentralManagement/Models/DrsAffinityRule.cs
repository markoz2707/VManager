using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum AffinityRuleType
{
    Affinity = 0,
    AntiAffinity = 1
}

public class DrsAffinityRule
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DrsConfigurationId { get; set; }
    public DrsConfiguration? DrsConfiguration { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AffinityRuleType Type { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Hard rules block violations; soft rules only generate warnings.</summary>
    public bool IsMandatory { get; set; }

    public List<DrsAffinityRuleVm> VmMembers { get; set; } = new();

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public class DrsAffinityRuleVm
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DrsAffinityRuleId { get; set; }
    public DrsAffinityRule? DrsAffinityRule { get; set; }

    public Guid VmInventoryId { get; set; }
    public VmInventory? VmInventory { get; set; }
}
