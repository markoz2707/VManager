using System.ComponentModel.DataAnnotations;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for setting VHD snapshot information
    /// </summary>
    public class SetVhdSnapshotInfoRequest
    {
        /// <summary>
        /// Path to the VHD Set file
        /// </summary>
        [Required]
        public string VhdSetPath { get; set; } = string.Empty;

        /// <summary>
        /// Unique identifier of the snapshot
        /// </summary>
        [Required]
        public string SnapshotId { get; set; } = string.Empty;

        /// <summary>
        /// New display name for the snapshot (optional)
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// New description for the snapshot (optional)
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Whether to mark this snapshot as active (optional)
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
