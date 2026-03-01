using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class UserRole
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }
    public UserAccount? User { get; set; }

    [Required]
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    /// <summary>
    /// Optional scope: restrict role to a specific cluster
    /// </summary>
    public Guid? ClusterId { get; set; }

    /// <summary>
    /// Optional scope: restrict role to a specific agent host
    /// </summary>
    public Guid? AgentHostId { get; set; }

    public DateTimeOffset AssignedUtc { get; set; } = DateTimeOffset.UtcNow;
}
