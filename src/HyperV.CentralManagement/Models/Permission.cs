using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class Permission
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Resource type: vm, host, cluster, network, storage, user, audit
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Resource { get; set; } = string.Empty;

    /// <summary>
    /// Action: read, create, update, delete, power, migrate, snapshot
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public List<RolePermission> RolePermissions { get; set; } = new();
}
