using System;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Response model containing VHD snapshot information
    /// </summary>
    public class VhdSnapshotInformationResponse
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

        /// <summary>
        /// Path to the VHD Set containing this snapshot
        /// </summary>
        public string VhdSetPath { get; set; } = string.Empty;

        /// <summary>
        /// Virtual size of the snapshot in bytes
        /// </summary>
        public ulong VirtualSize { get; set; }

        /// <summary>
        /// Physical size of the snapshot in bytes
        /// </summary>
        public ulong PhysicalSize { get; set; }

        /// <summary>
        /// Whether the snapshot supports resilient change tracking
        /// </summary>
        public bool SupportsResilientChangeTracking { get; set; }
    }
}
