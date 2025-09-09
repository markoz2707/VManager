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
}
