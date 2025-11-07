namespace HyperV.Contracts.Models
{
    public class UpdateVhdSnapshotRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool? IsActive { get; set; }
    }
}