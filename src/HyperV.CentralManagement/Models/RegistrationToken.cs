using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class RegistrationToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresUtc { get; set; }

    public Guid? UsedByAgentId { get; set; }
}
