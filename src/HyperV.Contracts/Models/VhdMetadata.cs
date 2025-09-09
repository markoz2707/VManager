using System;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Represents comprehensive metadata information about a VHD file
    /// </summary>
    public class VhdMetadata
    {
        /// <summary>
        /// Full path to the VHD file
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// VHD format (VHD or VHDX)
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Physical size of the VHD file on disk (in bytes)
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// Virtual size of the VHD (maximum capacity in bytes)
        /// </summary>
        public ulong VirtualSize { get; set; }

        /// <summary>
        /// Path to parent VHD (for differencing disks)
        /// </summary>
        public string? ParentPath { get; set; }

        /// <summary>
        /// Unique identifier of the VHD
        /// </summary>
        public Guid UniqueId { get; set; }

        /// <summary>
        /// Whether the VHD is currently attached to the system
        /// </summary>
        public bool IsAttached { get; set; }

        /// <summary>
        /// Physical sector size in bytes
        /// </summary>
        public uint PhysicalSectorSize { get; set; }

        /// <summary>
        /// Whether this is a differencing disk
        /// </summary>
        public bool IsDifferencingDisk => !string.IsNullOrEmpty(ParentPath);

        /// <summary>
        /// Compression ratio (physical size / virtual size)
        /// </summary>
        public double CompressionRatio => VirtualSize > 0 ? (double)Size / VirtualSize : 0;

        /// <summary>
        /// Creation timestamp (if available)
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// Last modified timestamp (if available)
        /// </summary>
        public DateTime? ModifiedAt { get; set; }
    }

    /// <summary>
    /// Represents updates to VHD metadata
    /// </summary>
    public class VhdMetadataUpdate
    {
        /// <summary>
        /// New unique identifier for the VHD
        /// </summary>
        public Guid? NewUniqueId { get; set; }

        /// <summary>
        /// New parent path for differencing disks
        /// </summary>
        public string? NewParentPath { get; set; }

        /// <summary>
        /// New physical sector size
        /// </summary>
        public uint? NewPhysicalSectorSize { get; set; }
    }
}
