namespace HyperV.Contracts.Models
{
    public class VirtualHardDiskSettingData
    {
        public string? Path { get; set; }
        public VirtualDiskType Type { get; set; }
        public VirtualDiskFormat Format { get; set; }
        public string? ParentPath { get; set; }
        public ulong MaxInternalSize { get; set; }
        public uint BlockSize { get; set; }
        public uint LogicalSectorSize { get; set; }
        public uint PhysicalSectorSize { get; set; }
    }
}
