using System;
using System.Runtime.InteropServices;

namespace HyperV.Core.Hcs.Interop
{
    public enum VIRTUAL_DISK_ACCESS_MASK : uint
    {
        VIRTUAL_DISK_ACCESS_NONE = 0x00000000,
        VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,
        VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,
        VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,
        VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,
        VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,
        VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000,
        VIRTUAL_DISK_ACCESS_READ = 0x000d0000,
        VIRTUAL_DISK_ACCESS_ALL = 0x003f0000,
        VIRTUAL_DISK_ACCESS_WRITABLE = 0x00320000
    }

    public enum CREATE_VIRTUAL_DISK_FLAG : uint
    {
        CREATE_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        CREATE_VIRTUAL_DISK_FLAG_FULL = 0x00000001,
        CREATE_VIRTUAL_DISK_FLAG_DYNAMIC = 0x00000002,
        CREATE_VIRTUAL_DISK_FLAG_DIFFERENCING = 0x00000004
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct VIRTUAL_STORAGE_TYPE
    {
        public uint DeviceId;
        public Guid VendorId;
    }

    public enum CREATE_VIRTUAL_DISK_VERSION : uint
    {
        CREATE_VIRTUAL_DISK_VERSION_1 = 1,
        CREATE_VIRTUAL_DISK_VERSION_2 = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS_V1
    {
        public ulong MaximumSize;
        public uint BlockSizeInBytes;
        public uint SectorSizeInBytes;
        public IntPtr ParentPath;
        public IntPtr SourcePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS_V2
    {
        public ulong MaximumSize;
        public uint BlockSizeInBytes;
        public uint SectorSizeInBytes;
        public uint PhysicalSectorSizeInBytes;
        public IntPtr ParentPath;
        public IntPtr SourcePath;
        public uint OpenFlags;
        public Guid ParentVirtualStorageType;
        public Guid SourceVirtualStorageType;
        public Guid ResiliencyGuid;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public CREATE_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public CREATE_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public CREATE_VIRTUAL_DISK_PARAMETERS_V2 Version2;
    }

    // Open Virtual Disk Types
    public enum OPEN_VIRTUAL_DISK_FLAG : uint
    {
        OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001,
        OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002,
        OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x00000004,
        OPEN_VIRTUAL_DISK_FLAG_CACHED_IO = 0x00000008,
        OPEN_VIRTUAL_DISK_FLAG_CUSTOM_DIFF_CHAIN = 0x00000010,
        OPEN_VIRTUAL_DISK_FLAG_PARENT_CACHED_IO = 0x00000020,
        OPEN_VIRTUAL_DISK_FLAG_VHDSET_FILE_ONLY = 0x00000040,
        OPEN_VIRTUAL_DISK_FLAG_IGNORE_RELATIVE_PARENT_LOCATOR = 0x00000080,
        OPEN_VIRTUAL_DISK_FLAG_NO_WRITE_HARDENING = 0x00000100
    }

    public enum OPEN_VIRTUAL_DISK_VERSION : uint
    {
        OPEN_VIRTUAL_DISK_VERSION_1 = 1,
        OPEN_VIRTUAL_DISK_VERSION_2 = 2,
        OPEN_VIRTUAL_DISK_VERSION_3 = 3
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint RWDepth;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V2
    {
        public bool GetInfoOnly;
        public bool ReadOnly;
        public Guid ResiliencyGuid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V3
    {
        public bool GetInfoOnly;
        public bool ReadOnly;
        public Guid ResiliencyGuid;
        public Guid SnapshotId;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public OPEN_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public OPEN_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public OPEN_VIRTUAL_DISK_PARAMETERS_V2 Version2;

        [FieldOffset(4)]
        public OPEN_VIRTUAL_DISK_PARAMETERS_V3 Version3;
    }

    // Attach Virtual Disk Types
    public enum ATTACH_VIRTUAL_DISK_FLAG : uint
    {
        ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001,
        ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002,
        ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004,
        ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008
    }

    public enum ATTACH_VIRTUAL_DISK_VERSION : uint
    {
        ATTACH_VIRTUAL_DISK_VERSION_1 = 1,
        ATTACH_VIRTUAL_DISK_VERSION_2 = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS_V2
    {
        public uint Reserved;
        public Guid RestrictedOffset;
        public Guid RestrictedLength;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public ATTACH_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public ATTACH_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public ATTACH_VIRTUAL_DISK_PARAMETERS_V2 Version2;
    }

    // Detach Virtual Disk Types
    public enum DETACH_VIRTUAL_DISK_FLAG : uint
    {
        DETACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000
    }

    // Resize Virtual Disk Types
    public enum RESIZE_VIRTUAL_DISK_FLAG : uint
    {
        RESIZE_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        RESIZE_VIRTUAL_DISK_FLAG_ALLOW_UNSAFE_VIRTUAL_SIZE = 0x00000001,
        RESIZE_VIRTUAL_DISK_FLAG_RESIZE_TO_SMALLEST_SAFE_VIRTUAL_SIZE = 0x00000002
    }

    public enum RESIZE_VIRTUAL_DISK_VERSION : uint
    {
        RESIZE_VIRTUAL_DISK_VERSION_1 = 1
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RESIZE_VIRTUAL_DISK_PARAMETERS_V1
    {
        public ulong NewSize;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct RESIZE_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public RESIZE_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public RESIZE_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    // Virtual Disk Information Types
    public enum GET_VIRTUAL_DISK_INFO_VERSION : uint
    {
        GET_VIRTUAL_DISK_INFO_UNSPECIFIED = 0,
        GET_VIRTUAL_DISK_INFO_SIZE = 1,
        GET_VIRTUAL_DISK_INFO_IDENTIFIER = 2,
        GET_VIRTUAL_DISK_INFO_PARENT_LOCATION = 3,
        GET_VIRTUAL_DISK_INFO_PARENT_IDENTIFIER = 4,
        GET_VIRTUAL_DISK_INFO_PARENT_TIMESTAMP = 5,
        GET_VIRTUAL_DISK_INFO_VIRTUAL_STORAGE_TYPE = 6,
        GET_VIRTUAL_DISK_INFO_PROVIDER_SUBTYPE = 7,
        GET_VIRTUAL_DISK_INFO_IS_4K_ALIGNED = 8,
        GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK = 9,
        GET_VIRTUAL_DISK_INFO_VHD_PHYSICAL_SECTOR_SIZE = 10,
        GET_VIRTUAL_DISK_INFO_SMALLEST_SAFE_VIRTUAL_SIZE = 11,
        GET_VIRTUAL_DISK_INFO_FRAGMENTATION = 12,
        GET_VIRTUAL_DISK_INFO_IS_LOADED = 13,
        GET_VIRTUAL_DISK_INFO_VIRTUAL_DISK_ID = 14,
        GET_VIRTUAL_DISK_INFO_CHANGE_TRACKING_STATE = 15
    }

    // Set Virtual Disk Information Types
    public enum SET_VIRTUAL_DISK_INFO_VERSION : uint
    {
        SET_VIRTUAL_DISK_INFO_PARENT_PATH = 1,
        SET_VIRTUAL_DISK_INFO_IDENTIFIER = 2,
        SET_VIRTUAL_DISK_INFO_PARENT_PATH_WITH_DEPTH = 3,
        SET_VIRTUAL_DISK_INFO_PHYSICAL_SECTOR_SIZE = 4,
        SET_VIRTUAL_DISK_INFO_VIRTUAL_DISK_ID = 5,
        SET_VIRTUAL_DISK_INFO_CHANGE_TRACKING_STATE = 6,
        SET_VIRTUAL_DISK_INFO_PARENT_LOCATOR = 7
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GET_VIRTUAL_DISK_INFO_SIZE
    {
        public ulong VirtualSize;
        public ulong PhysicalSize;
        public uint BlockSize;
        public uint SectorSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GET_VIRTUAL_DISK_INFO_PARENT_LOCATION
    {
        public bool ParentResolved;
        public IntPtr ParentLocationBuffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK
    {
        public uint LogicalSectorSize;
        public uint PhysicalSectorSize;
        public bool IsRemote;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct GET_VIRTUAL_DISK_INFO
    {
        [FieldOffset(0)]
        public GET_VIRTUAL_DISK_INFO_VERSION Version;

        [FieldOffset(4)]
        public GET_VIRTUAL_DISK_INFO_SIZE Size;

        [FieldOffset(4)]
        public Guid Identifier;

        [FieldOffset(4)]
        public GET_VIRTUAL_DISK_INFO_PARENT_LOCATION ParentLocation;

        [FieldOffset(4)]
        public Guid ParentIdentifier;

        [FieldOffset(4)]
        public uint ParentTimestamp;

        [FieldOffset(4)]
        public VIRTUAL_STORAGE_TYPE VirtualStorageType;

        [FieldOffset(4)]
        public uint ProviderSubtype;

        [FieldOffset(4)]
        public bool Is4kAligned;

        [FieldOffset(4)]
        public GET_VIRTUAL_DISK_INFO_PHYSICAL_DISK PhysicalDisk;

        [FieldOffset(4)]
        public uint VhdPhysicalSectorSize;

        [FieldOffset(4)]
        public ulong SmallestSafeVirtualSize;

        [FieldOffset(4)]
        public uint FragmentationPercentage;

        [FieldOffset(4)]
        public Guid VirtualDiskId;

        [FieldOffset(4)]
        public bool ChangeTrackingState;
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    public struct SET_VIRTUAL_DISK_INFO
    {
        [FieldOffset(0)]
        public SET_VIRTUAL_DISK_INFO_VERSION Version;

        [FieldOffset(4)]
        public IntPtr ParentFilePath;

        [FieldOffset(4)]
        public Guid Identifier;

        [FieldOffset(4)]
        public SET_VIRTUAL_DISK_INFO_PARENT_PATH_WITH_DEPTH ParentPathWithDepth;

        [FieldOffset(4)]
        public uint PhysicalSectorSize;

        [FieldOffset(4)]
        public Guid VirtualDiskId;

        [FieldOffset(4)]
        public bool ChangeTrackingEnabled;

        [FieldOffset(4)]
        public IntPtr ParentLocator;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SET_VIRTUAL_DISK_INFO_PARENT_PATH_WITH_DEPTH
    {
        public uint ChildDepth;
        public IntPtr ParentFilePath;
    }

    // Merge Virtual Disk Types
    public enum MERGE_VIRTUAL_DISK_FLAG : uint
    {
        MERGE_VIRTUAL_DISK_FLAG_NONE = 0x00000000
    }

    public enum MERGE_VIRTUAL_DISK_VERSION : uint
    {
        MERGE_VIRTUAL_DISK_VERSION_1 = 1,
        MERGE_VIRTUAL_DISK_VERSION_2 = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MERGE_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint MergeDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MERGE_VIRTUAL_DISK_PARAMETERS_V2
    {
        public uint MergeSourceDepth;
        public uint MergeTargetDepth;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MERGE_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public MERGE_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public MERGE_VIRTUAL_DISK_PARAMETERS_V1 Version1;

        [FieldOffset(4)]
        public MERGE_VIRTUAL_DISK_PARAMETERS_V2 Version2;
    }

    // Compact Virtual Disk Types
    public enum COMPACT_VIRTUAL_DISK_FLAG : uint
    {
        COMPACT_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
        COMPACT_VIRTUAL_DISK_FLAG_NO_ZERO_SCAN = 0x00000001,
        COMPACT_VIRTUAL_DISK_FLAG_NO_BLOCK_MOVES = 0x00000002
    }

    public enum COMPACT_VIRTUAL_DISK_VERSION : uint
    {
        COMPACT_VIRTUAL_DISK_VERSION_1 = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMPACT_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct COMPACT_VIRTUAL_DISK_PARAMETERS
    {
        [FieldOffset(0)]
        public COMPACT_VIRTUAL_DISK_VERSION Version;

        [FieldOffset(4)]
        public COMPACT_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    // VHD Set Snapshot Types
    public enum APPLY_SNAPSHOT_VHDSET_FLAG : uint
    {
        APPLY_SNAPSHOT_VHDSET_FLAG_NONE = 0x00000000,
        APPLY_SNAPSHOT_VHDSET_FLAG_WRITEABLE = 0x00000001
    }

    public enum APPLY_SNAPSHOT_VHDSET_VERSION : uint
    {
        APPLY_SNAPSHOT_VHDSET_VERSION_1 = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPLY_SNAPSHOT_VHDSET_PARAMETERS_V1
    {
        public Guid SnapshotId;
        public Guid LeafSnapshotId;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct APPLY_SNAPSHOT_VHDSET_PARAMETERS
    {
        [FieldOffset(0)]
        public APPLY_SNAPSHOT_VHDSET_VERSION Version;

        [FieldOffset(4)]
        public APPLY_SNAPSHOT_VHDSET_PARAMETERS_V1 Version1;
    }

    public enum DELETE_SNAPSHOT_VHDSET_FLAG : uint
    {
        DELETE_SNAPSHOT_VHDSET_FLAG_NONE = 0x00000000,
        DELETE_SNAPSHOT_VHDSET_FLAG_PERSIST_RCT = 0x00000001
    }

    public enum DELETE_SNAPSHOT_VHDSET_VERSION : uint
    {
        DELETE_SNAPSHOT_VHDSET_VERSION_1 = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DELETE_SNAPSHOT_VHDSET_PARAMETERS_V1
    {
        public Guid SnapshotId;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct DELETE_SNAPSHOT_VHDSET_PARAMETERS
    {
        [FieldOffset(0)]
        public DELETE_SNAPSHOT_VHDSET_VERSION Version;

        [FieldOffset(4)]
        public DELETE_SNAPSHOT_VHDSET_PARAMETERS_V1 Version1;
    }

    // Progress reporting callback delegate
    public delegate uint VirtualDiskProgressCallback(
        IntPtr ProgressMessage,
        IntPtr CallbackData);

    // Overlapped structure for asynchronous operations
    [StructLayout(LayoutKind.Sequential)]
    public struct OVERLAPPED
    {
        public IntPtr Internal;
        public IntPtr InternalHigh;
        public uint Offset;
        public uint OffsetHigh;
        public IntPtr hEvent;
    }

    // Progress message structure
    [StructLayout(LayoutKind.Sequential)]
    public struct VIRTUAL_DISK_PROGRESS
    {
        public uint OperationStatus;
        public ulong CurrentValue;
        public ulong CompletionValue;
    }

    // Common Win32 error codes for VirtDisk operations
    public static class VirtDiskErrorCodes
    {
        public const int ERROR_SUCCESS = 0;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_PARAMETER = 87;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int ERROR_INVALID_NAME = 123;
        public const int ERROR_ALREADY_EXISTS = 183;
        public const int ERROR_FILE_EXISTS = 80;
        public const int ERROR_DISK_FULL = 112;
        public const int ERROR_NOT_SUPPORTED = 50;
        public const int ERROR_IO_PENDING = 997;
        public const int ERROR_INVALID_STATE = 5023;
        public const int ERROR_VHD_SHARED = unchecked((int)0xC03A0014);
        public const int ERROR_VHD_PARENT_VHD_ACCESS_DENIED = unchecked((int)0xC03A0015);
        public const int ERROR_VHD_CHILD_PARENT_ID_MISMATCH = unchecked((int)0xC03A0016);
        public const int ERROR_VHD_CHILD_PARENT_TIMESTAMP_MISMATCH = unchecked((int)0xC03A0017);
    }

    // HCS integration types for VM-specific operations
    public static class HcsStorageTypes
    {
        public const string SCSI_CONTROLLER = "scsi";
        public const string IDE_CONTROLLER = "ide";
        public const string VIRTUAL_DISK = "virtualdisk";
    }

    // Container storage integration types
    public static class ContainerStorageTypes
    {
        public const string CONTAINER_LAYER = "layer";
        public const string CONTAINER_SCRATCH = "scratch";
        public const string CONTAINER_MAPPED_DIRECTORY = "mappeddirectory";
    }
}
