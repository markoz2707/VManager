using System.ComponentModel.DataAnnotations;

namespace HyperV.CentralManagement.Models;

public class ResourcePool
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid ClusterId { get; set; }
    public Cluster? Cluster { get; set; }

    public int MaxCpuCores { get; set; }
    public long MaxMemoryMB { get; set; }
    public long MaxStorageGB { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<ResourcePoolVm> AssignedVms { get; set; } = new();
}

public class ResourcePoolVm
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResourcePoolId { get; set; }
    public ResourcePool? ResourcePool { get; set; }

    public Guid VmInventoryId { get; set; }
    public VmInventory? VmInventory { get; set; }
}
