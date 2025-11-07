using System;
using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Response model containing VHD Set information
    /// </summary>
    public class VhdSetInformationResponse
    {
        /// <summary>
        /// Path to the VHD Set file
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Virtual size of the VHD Set in bytes
        /// </summary>
        public ulong VirtualSize { get; set; }

        /// <summary>
        /// Physical size of the VHD Set in bytes
        /// </summary>
        public ulong PhysicalSize { get; set; }

        /// <summary>
        /// Block size of the VHD Set
        /// </summary>
        public uint BlockSize { get; set; }

        /// <summary>
        /// Logical sector size
        /// </summary>
        public uint LogicalSectorSize { get; set; }

        /// <summary>
        /// Physical sector size
        /// </summary>
        public uint PhysicalSectorSize { get; set; }

        /// <summary>
        /// Creation timestamp of the VHD Set
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Last modification timestamp
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Unique identifier of the VHD Set
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// List of snapshots in the VHD Set
        /// </summary>
        public List<VhdSnapshotInfo> Snapshots { get; set; } = new List<VhdSnapshotInfo>();

        /// <summary>
        /// Whether the VHD Set is currently mounted
        /// </summary>
        public bool IsMounted { get; set; }

        /// <summary>
        /// Whether the VHD Set supports resilient change tracking
        /// </summary>
        public bool SupportsResilientChangeTracking { get; set; }
    }

    /// <summary>
    /// Information about a VHD snapshot
    /// </summary>
    public class VhdSnapshotInfo
    {
        /// <summary>
        /// Unique identifier of the snapshot
        /// </summary>
        public string SnapshotId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the snapshot
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the snapshot
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp of the snapshot
        /// </summary>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// Parent snapshot identifier (if any)
        /// </summary>
        public string? ParentSnapshotId { get; set; }

        /// <summary>
        /// Whether this is the active snapshot
        /// </summary>
        public bool IsActive { get; set; }
    }
}
