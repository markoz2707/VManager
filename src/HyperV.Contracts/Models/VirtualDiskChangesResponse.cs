using System.Collections.Generic;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Response model containing virtual disk changes information
    /// </summary>
    public class VirtualDiskChangesResponse
    {
        /// <summary>
        /// List of changed disk ranges
        /// </summary>
        public List<VirtualDiskChangeRange> ChangedRanges { get; set; } = new List<VirtualDiskChangeRange>();

        /// <summary>
        /// Total number of changed bytes
        /// </summary>
        public ulong TotalChangedBytes { get; set; }

        /// <summary>
        /// Whether more changes are available beyond this response
        /// </summary>
        public bool HasMoreChanges { get; set; }

        /// <summary>
        /// Next change tracking identifier for subsequent queries
        /// </summary>
        public string? NextChangeTrackingId { get; set; }

        /// <summary>
        /// Timestamp when the changes were captured
        /// </summary>
        public System.DateTime CaptureTime { get; set; }
    }

    /// <summary>
    /// Represents a range of changed bytes in a virtual disk
    /// </summary>
    public class VirtualDiskChangeRange
    {
        /// <summary>
        /// Starting byte offset of the changed range
        /// </summary>
        public ulong ByteOffset { get; set; }

        /// <summary>
        /// Length of the changed range in bytes
        /// </summary>
        public ulong ByteLength { get; set; }

        /// <summary>
        /// Type of change that occurred
        /// </summary>
        public VirtualDiskChangeType ChangeType { get; set; }
    }

    /// <summary>
    /// Types of changes that can occur in a virtual disk
    /// </summary>
    public enum VirtualDiskChangeType
    {
        /// <summary>
        /// Data was written to this range
        /// </summary>
        DataWritten = 1,
        /// <summary>
        /// Data was deallocated from this range
        /// </summary>
        DataDeallocated = 2,
        /// <summary>
        /// Metadata was changed for this range
        /// </summary>
        MetadataChanged = 3,
        /// <summary>
        /// Range was zeroed
        /// </summary>
        DataZeroed = 4
    }
}
