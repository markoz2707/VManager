using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class Datacenter
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
