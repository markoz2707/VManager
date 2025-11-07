namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Response model for storage device information
    /// </summary>
    public class StorageDeviceResponse
    {
        /// <summary>
        /// Unique identifier for the storage device
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the storage device
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of storage device (VirtualDisk, PhysicalDisk, DVD, Floppy)
        /// </summary>
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>
        /// Path to the storage file (for virtual devices)
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Controller the device is attached to
        /// </summary>
        public string ControllerId { get; set; } = string.Empty;

        /// <summary>
        /// Controller type (IDE, SCSI)
        /// </summary>
        public string ControllerType { get; set; } = string.Empty;

        /// <summary>
        /// Location on the controller (0, 1, etc.)
        /// </summary>
        public int ControllerLocation { get; set; }

        /// <summary>
        /// Whether the device is read-only
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Current operational status
        /// </summary>
        public string OperationalStatus { get; set; } = "Unknown";

        /// <summary>
        /// Size of the storage device in bytes
        /// </summary>
        public ulong? Size { get; set; }

        /// <summary>
        /// Whether the device supports hot-plug operations
        /// </summary>
        public bool SupportsHotPlug { get; set; }
    }

    /// <summary>
    /// Response model for storage controller information
    /// </summary>
    public class StorageControllerResponse
    {
        /// <summary>
        /// Unique identifier for the storage controller
        /// </summary>
        public string ControllerId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the storage controller
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Type of storage controller (IDE, SCSI)
        /// </summary>
        public string ControllerType { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of devices this controller can support
        /// </summary>
        public int MaxDevices { get; set; }

        /// <summary>
        /// Number of devices currently attached
        /// </summary>
        public int AttachedDevices { get; set; }

        /// <summary>
        /// Available locations on the controller
        /// </summary>
        public List<int> AvailableLocations { get; set; } = new List<int>();

        /// <summary>
        /// Whether the controller supports hot-plug operations
        /// </summary>
        public bool SupportsHotPlug { get; set; }

        /// <summary>
        /// Current operational status
        /// </summary>
        public string OperationalStatus { get; set; } = "Unknown";

        /// <summary>
        /// Protocol supported by the controller
        /// </summary>
        public string Protocol { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for adding a storage device to a VM
    /// </summary>
    public class AddStorageDeviceRequest
    {
        /// <summary>
        /// Type of storage device to add
        /// </summary>
        public string DeviceType { get; set; } = string.Empty;

        /// <summary>
        /// Path to the storage file (for virtual devices)
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// Controller ID to attach the device to
        /// </summary>
        public string? ControllerId { get; set; }

        /// <summary>
        /// Specific location on the controller (optional, auto-assign if not specified)
        /// </summary>
        public int? ControllerLocation { get; set; }

        /// <summary>
        /// Whether to attach as read-only
        /// </summary>
        public bool ReadOnly { get; set; } = false;
    }

    /// <summary>
    /// Response model for mounted storage images
    /// </summary>
    public class MountedStorageImageResponse
    {
        /// <summary>
        /// Path to the mounted storage image
        /// </summary>
        public string ImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Physical path where the image is mounted
        /// </summary>
        public string MountPath { get; set; } = string.Empty;

        /// <summary>
        /// Type of storage image (VHD, VHDX, ISO, VFD)
        /// </summary>
        public string ImageType { get; set; } = string.Empty;

        /// <summary>
        /// Whether the image is mounted read-only
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// VM name if attached to a specific VM
        /// </summary>
        public string? VmName { get; set; }

        /// <summary>
        /// Size of the mounted image in bytes
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// When the image was mounted
        /// </summary>
        public DateTime MountedAt { get; set; }
    }

    /// <summary>
    /// Specifies the mode for the VHD compact operation.
    /// </summary>
    public enum VhdCompactMode
    {
        /// <summary>
        /// Frees unused blocks and reduces the file size.
        /// </summary>
        Full = 0,
        /// <summary>
        /// Frees unused blocks but does not reduce file size.
        /// </summary>
        Quick = 1,
        /// <summary>
        /// Scans for unmapped blocks and reclaims them.
        /// </summary>
        Retrim = 2,
        /// <summary>
        /// For use after the disk has been trimmed.
        /// </summary>
        Pretrimmed = 3,
        /// <summary>
        /// For use after the disk has been zeroed.
        /// </summary>
        Prezeroed = 4
    }

    /// <summary>
    /// Request model for compacting a virtual hard disk.
    /// </summary>
    public class CompactVhdRequest
    {
        /// <summary>
        /// A fully qualified path that specifies the location of the VHD file.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The mode for the compact operation.
        /// </summary>
        public VhdCompactMode Mode { get; set; }
    }
}
