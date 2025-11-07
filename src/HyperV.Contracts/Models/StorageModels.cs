using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Information about a storage device (drive).
    /// </summary>
    public class StorageDeviceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Filesystem { get; set; } = string.Empty;
        public long Size { get; set; }
        public long UsedSpace { get; set; }
        public long FreeSpace { get; set; }
    }

    /// <summary>
    /// Suggested location for VHDX storage on a drive.
    /// </summary>
    public class StorageLocation
    {
        public string Drive { get; set; } = string.Empty;
        public long FreeSpaceBytes { get; set; }
        public double FreeSpaceGb { get; set; }
        public List<string> SuggestedPaths { get; set; } = new();
        public bool IsSuitable { get; set; }
    }
}