namespace HyperV.Contracts.Models
{
    public class VirtualHardDiskState
    {
        public int InUse { get; set; }
        public int Health { get; set; }
        public int OperationalStatus { get; set; }
        public int InUseBy { get; set; }
    }
}
