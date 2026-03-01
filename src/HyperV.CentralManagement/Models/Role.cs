using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class Role
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsBuiltIn { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<RolePermission> RolePermissions { get; set; } = new();
    public List<UserRole> UserRoles { get; set; } = new();
}
