using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class UserAccount
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Legacy role field kept for backward compatibility during migration
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Role { get; set; } = "Admin";

    [MaxLength(300)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginUtc { get; set; }

    public List<UserRole> UserRoles { get; set; } = new();
}
