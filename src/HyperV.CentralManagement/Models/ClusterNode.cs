using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class ClusterNode
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClusterId { get; set; }

    public Cluster? Cluster { get; set; }

    [Required]
    public Guid AgentHostId { get; set; }

    public AgentHost? AgentHost { get; set; }
}
