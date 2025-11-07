using System.ComponentModel.DataAnnotations;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for getting virtual disk changes
    /// </summary>
    public class GetVirtualDiskChangesRequest
    {
        /// <summary>
        /// Path to the VHD Set file
        /// </summary>
        [Required]
        public string VhdSetPath { get; set; } = string.Empty;

        /// <summary>
        /// Change tracking identifier to query changes from
        /// </summary>
        [Required]
        public string ChangeTrackingId { get; set; } = string.Empty;

        /// <summary>
        /// Byte offset to start querying changes from
        /// </summary>
        public ulong ByteOffset { get; set; } = 0;

        /// <summary>
        /// Number of bytes to query changes for
        /// </summary>
        public ulong ByteLength { get; set; } = 0;

        /// <summary>
        /// Flags for the query operation
        /// </summary>
        public VirtualDiskChangeFlags Flags { get; set; } = VirtualDiskChangeFlags.None;
    }

    /// <summary>
    /// Flags for virtual disk change queries
    /// </summary>
    [System.Flags]
    public enum VirtualDiskChangeFlags : uint
    {
        None = 0,
        /// <summary>
        /// Include only changed blocks
        /// </summary>
        ChangedOnly = 1,
        /// <summary>
        /// Include metadata changes
        /// </summary>
        IncludeMetadata = 2,
        /// <summary>
        /// Use resilient change tracking
        /// </summary>
        ResilientChangeTracking = 4
    }
}
