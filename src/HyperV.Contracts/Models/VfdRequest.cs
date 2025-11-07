namespace HyperV.Contracts.Models
{
    /// <summary>
    /// Request model for creating virtual floppy disks
    /// </summary>
    public class CreateVfdRequest
    {
        /// <summary>
        /// Path where the VFD file will be created
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Size of the floppy disk (default: 1.44MB)
        /// </summary>
        public VfdSize Size { get; set; } = VfdSize.Floppy144MB;

        /// <summary>
        /// Whether to format the floppy disk
        /// </summary>
        public bool Format { get; set; } = true;

        /// <summary>
        /// File system to use (FAT12 for floppies)
        /// </summary>
        public string FileSystem { get; set; } = "FAT12";

        /// <summary>
        /// Volume label for the floppy disk
        /// </summary>
        public string? VolumeLabel { get; set; }
    }

    /// <summary>
    /// Supported virtual floppy disk sizes
    /// </summary>
    public enum VfdSize
    {
        /// <summary>
        /// 1.44 MB floppy disk (standard)
        /// </summary>
        Floppy144MB = 1474560,

        /// <summary>
        /// 1.2 MB floppy disk
        /// </summary>
        Floppy12MB = 1228800,

        /// <summary>
        /// 720 KB floppy disk
        /// </summary>
        Floppy720KB = 737280,

        /// <summary>
        /// 360 KB floppy disk
        /// </summary>
        Floppy360KB = 368640
    }

    /// <summary>
    /// Request model for attaching/detaching VFD to/from VM
    /// </summary>
    public class VfdAttachRequest
    {
        /// <summary>
        /// Name of the VM
        /// </summary>
        public string VmName { get; set; } = string.Empty;

        /// <summary>
        /// Path to the VFD file
        /// </summary>
        public string VfdPath { get; set; } = string.Empty;

        /// <summary>
        /// Whether to attach as read-only
        /// </summary>
        public bool ReadOnly { get; set; } = false;
    }
}
