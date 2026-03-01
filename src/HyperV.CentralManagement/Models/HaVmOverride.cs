using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public enum HaRestartPriority
{
    Disabled = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public class HaVmOverride
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HaConfigurationId { get; set; }
    public HaConfiguration? HaConfiguration { get; set; }

    public Guid VmInventoryId { get; set; }

    public HaRestartPriority RestartPriority { get; set; } = HaRestartPriority.Medium;

    /// <summary>
    /// Lower numbers restart first
    /// </summary>
    public int RestartOrder { get; set; } = 100;
}
