using System.ComponentModel.DataAnnotations;

namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for converting virtual hard disk formats
    /// </summary>
    public class ConvertVhdRequest
    {
        /// <summary>
        /// Path to the source virtual hard disk file
        /// </summary>
        [Required]
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Path where the converted virtual hard disk will be created
        /// </summary>
        [Required]
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>
        /// Target format for conversion (VHD, VHDX)
        /// </summary>
        [Required]
        public VirtualDiskFormat TargetFormat { get; set; }

        /// <summary>
        /// Type of virtual disk (Fixed, Dynamic, Differencing)
        /// </summary>
        public VirtualDiskType? DiskType { get; set; }

        /// <summary>
        /// Block size for the converted disk (optional)
        /// </summary>
        public uint? BlockSize { get; set; }

        /// <summary>
        /// Whether to overwrite existing destination file
        /// </summary>
        public bool OverwriteDestination { get; set; } = false;
    }

    /// <summary>
    /// Virtual disk format enumeration
    /// </summary>
    public enum VirtualDiskFormat
    {
        VHD = 1,
        VHDX = 2,
        ISO = 3,
        VFD = 4
    }

    /// <summary>
    /// Virtual disk type enumeration
    /// </summary>
    public enum VirtualDiskType
    {
        Fixed = 1,
        Dynamic = 2,
        Differencing = 3
    }
}
