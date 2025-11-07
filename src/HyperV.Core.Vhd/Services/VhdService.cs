using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.IO;
using HyperV.Contracts.Models;

namespace HyperV.Core.Vhd.Services;

/// <summary>
/// VHD/VHDX operations using simplified Virtual Disk API
/// Provides basic VHD manipulation capabilities without complex P/Invoke structures
/// </summary>
public sealed class VhdService
{
    #region Simplified VirtDisk API Imports

    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    private static extern uint CreateVirtualDisk(
        ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
        string Path,
        VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
        IntPtr SecurityDescriptor,
        CREATE_VIRTUAL_DISK_FLAG Flags,
        uint ProviderSpecificFlags,
        ref CREATE_VIRTUAL_DISK_PARAMETERS Parameters,
        IntPtr Overlapped,
        out IntPtr Handle);

    [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
    private static extern uint OpenVirtualDisk(
        ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
        string Path,
        VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
        OPEN_VIRTUAL_DISK_FLAG Flags,
        ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters,
        out IntPtr Handle);

    [DllImport("virtdisk.dll")]
    private static extern uint AttachVirtualDisk(
        IntPtr VirtualDiskHandle,
        IntPtr SecurityDescriptor,
        ATTACH_VIRTUAL_DISK_FLAG Flags,
        uint ProviderSpecificFlags,
        ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters,
        IntPtr Overlapped);

    [DllImport("virtdisk.dll")]
    private static extern uint DetachVirtualDisk(
        IntPtr VirtualDiskHandle,
        DETACH_VIRTUAL_DISK_FLAG Flags,
        uint ProviderSpecificFlags);

    [DllImport("virtdisk.dll")]
    private static extern uint ResizeVirtualDisk(
        IntPtr VirtualDiskHandle,
        RESIZE_VIRTUAL_DISK_FLAG Flags,
        ref RESIZE_VIRTUAL_DISK_PARAMETERS Parameters,
        IntPtr Overlapped);

    [DllImport("virtdisk.dll")]
    private static extern uint CompactVirtualDisk(
        IntPtr VirtualDiskHandle,
        COMPACT_VIRTUAL_DISK_FLAG Flags,
        ref COMPACT_VIRTUAL_DISK_PARAMETERS Parameters,
        IntPtr Overlapped);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    #endregion

    #region Constants and Enums

    public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = 
        new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");

    public const uint VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 2;
    public const uint VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = 3;

    [Flags]
    public enum VIRTUAL_DISK_ACCESS_MASK : uint
    {
        VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,
        VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,
        VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,
        VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,
        VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,
        VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000
    }

    [Flags]
    public enum CREATE_VIRTUAL_DISK_FLAG : uint
    {
        CREATE_VIRTUAL_DISK_FLAG_NONE = 0x0,
        CREATE_VIRTUAL_DISK_FLAG_FULL_PHYSICAL_ALLOCATION = 0x1
    }

    [Flags]
    public enum ATTACH_VIRTUAL_DISK_FLAG : uint
    {
        ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x0,
        ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x1,
        ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x2
    }

    [Flags]
    public enum DETACH_VIRTUAL_DISK_FLAG : uint
    {
        DETACH_VIRTUAL_DISK_FLAG_NONE = 0x0
    }

    [Flags]
    public enum OPEN_VIRTUAL_DISK_FLAG : uint
    {
        OPEN_VIRTUAL_DISK_FLAG_NONE = 0x0
    }

    [Flags]
    public enum RESIZE_VIRTUAL_DISK_FLAG : uint
    {
        RESIZE_VIRTUAL_DISK_FLAG_NONE = 0x0
    }

    [Flags]
    public enum COMPACT_VIRTUAL_DISK_FLAG : uint
    {
        COMPACT_VIRTUAL_DISK_FLAG_NONE = 0x0
    }

    public enum CREATE_VIRTUAL_DISK_VERSION : uint
    {
        CREATE_VIRTUAL_DISK_VERSION_1 = 1
    }

    public enum OPEN_VIRTUAL_DISK_VERSION : uint
    {
        OPEN_VIRTUAL_DISK_VERSION_1 = 1
    }

    #endregion

    #region Simplified Structures

    [StructLayout(LayoutKind.Sequential)]
    public struct VIRTUAL_STORAGE_TYPE
    {
        public uint DeviceId;
        public Guid VendorId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS
    {
        public CREATE_VIRTUAL_DISK_VERSION Version;
        public CREATE_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CREATE_VIRTUAL_DISK_PARAMETERS_V1
    {
        public Guid UniqueId;
        public ulong MaximumSize;
        public uint BlockSizeInBytes;
        public uint SectorSizeInBytes;
        public IntPtr ParentPath;
        public IntPtr SourcePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS
    {
        public OPEN_VIRTUAL_DISK_VERSION Version;
        public OPEN_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct OPEN_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint RWDepth;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS
    {
        public uint Version;
        public ATTACH_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ATTACH_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RESIZE_VIRTUAL_DISK_PARAMETERS
    {
        public uint Version;
        public RESIZE_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RESIZE_VIRTUAL_DISK_PARAMETERS_V1
    {
        public ulong NewSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMPACT_VIRTUAL_DISK_PARAMETERS
    {
        public uint Version;
        public COMPACT_VIRTUAL_DISK_PARAMETERS_V1 Version1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COMPACT_VIRTUAL_DISK_PARAMETERS_V1
    {
        public uint Reserved;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a new VHD/VHDX file
    /// </summary>
    public void CreateVirtualDisk(CreateVhdRequest request)
    {
        var storageType = GetStorageType(request.Format);
        
        var parameters = new CREATE_VIRTUAL_DISK_PARAMETERS
        {
            Version = CREATE_VIRTUAL_DISK_VERSION.CREATE_VIRTUAL_DISK_VERSION_1,
            Version1 = new CREATE_VIRTUAL_DISK_PARAMETERS_V1
            {
                UniqueId = Guid.NewGuid(),
                MaximumSize = request.MaxInternalSize,
                BlockSizeInBytes = 0, // Use default
                SectorSizeInBytes = 512,
                ParentPath = IntPtr.Zero,
                SourcePath = IntPtr.Zero
            }
        };

        var flags = request.Type?.ToUpperInvariant() == "FIXED" 
            ? CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_FULL_PHYSICAL_ALLOCATION
            : CREATE_VIRTUAL_DISK_FLAG.CREATE_VIRTUAL_DISK_FLAG_NONE;

        var result = CreateVirtualDisk(
            ref storageType,
            request.Path ?? throw new ArgumentNullException(nameof(request.Path)),
            VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_CREATE,
            IntPtr.Zero,
            flags,
            0,
            ref parameters,
            IntPtr.Zero,
            out var handle);

        if (result != 0)
        {
            throw new Win32Exception((int)result, $"Failed to create virtual disk: 0x{result:X8}");
        }

        CloseHandle(handle);
        Console.WriteLine($"Successfully created VHD: {request.Path}");
    }

    /// <summary>
    /// Attaches a VHD/VHDX file to the system
    /// </summary>
    public string AttachVirtualDisk(string vhdPath, bool readOnly = false, bool noDriveLetter = false)
    {
        var handle = OpenVirtualDiskHandle(vhdPath, 
            VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ATTACH_RW);

        try
        {
            var parameters = new ATTACH_VIRTUAL_DISK_PARAMETERS
            {
                Version = 1,
                Version1 = new ATTACH_VIRTUAL_DISK_PARAMETERS_V1 { Reserved = 0 }
            };

            var flags = ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NONE;
            if (readOnly) flags |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY;
            if (noDriveLetter) flags |= ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER;

            var result = AttachVirtualDisk(handle, IntPtr.Zero, flags, 0, ref parameters, IntPtr.Zero);
            
            if (result != 0)
            {
                throw new Win32Exception((int)result, $"Failed to attach virtual disk: 0x{result:X8}");
            }

            Console.WriteLine($"Successfully attached VHD: {vhdPath}");
            return "Attached successfully";
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Detaches a VHD/VHDX file from the system
    /// </summary>
    public void DetachVirtualDisk(string vhdPath)
    {
        var handle = OpenVirtualDiskHandle(vhdPath, VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_DETACH);

        try
        {
            var result = DetachVirtualDisk(handle, DETACH_VIRTUAL_DISK_FLAG.DETACH_VIRTUAL_DISK_FLAG_NONE, 0);
            
            if (result != 0)
            {
                throw new Win32Exception((int)result, $"Failed to detach virtual disk: 0x{result:X8}");
            }

            Console.WriteLine($"Successfully detached VHD: {vhdPath}");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Resizes a VHD/VHDX file
    /// </summary>
    public void ResizeVirtualDisk(string vhdPath, ulong newSize)
    {
        var handle = OpenVirtualDiskHandle(vhdPath, VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS);

        try
        {
            var parameters = new RESIZE_VIRTUAL_DISK_PARAMETERS
            {
                Version = 1,
                Version1 = new RESIZE_VIRTUAL_DISK_PARAMETERS_V1 { NewSize = newSize }
            };

            var result = ResizeVirtualDisk(handle, RESIZE_VIRTUAL_DISK_FLAG.RESIZE_VIRTUAL_DISK_FLAG_NONE, ref parameters, IntPtr.Zero);
            
            if (result != 0)
            {
                throw new Win32Exception((int)result, $"Failed to resize virtual disk: 0x{result:X8}");
            }

            Console.WriteLine($"Successfully resized VHD: {vhdPath} to {newSize} bytes");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Compacts a VHD/VHDX file
    /// </summary>
    public void CompactVirtualDisk(string vhdPath)
    {
        var handle = OpenVirtualDiskHandle(vhdPath, VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_METAOPS);

        try
        {
            var parameters = new COMPACT_VIRTUAL_DISK_PARAMETERS
            {
                Version = 1,
                Version1 = new COMPACT_VIRTUAL_DISK_PARAMETERS_V1 { Reserved = 0 }
            };

            var result = CompactVirtualDisk(handle, COMPACT_VIRTUAL_DISK_FLAG.COMPACT_VIRTUAL_DISK_FLAG_NONE, ref parameters, IntPtr.Zero);
            
            if (result != 0)
            {
                throw new Win32Exception((int)result, $"Failed to compact virtual disk: 0x{result:X8}");
            }

            Console.WriteLine($"Successfully compacted VHD: {vhdPath}");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Gets information about a VHD/VHDX file using basic file information
    /// </summary>
    public VhdMetadata GetVirtualDiskInfo(string vhdPath)
    {
        if (!File.Exists(vhdPath))
        {
            throw new FileNotFoundException($"VHD file not found: {vhdPath}");
        }

        var fileInfo = new FileInfo(vhdPath);
        var extension = Path.GetExtension(vhdPath).ToUpperInvariant();
        var format = extension == ".VHDX" ? "VHDX" : "VHD";

        // Use basic file information for now to avoid P/Invoke complexity
        return new VhdMetadata
        {
            Path = vhdPath,
            Format = format,
            VirtualSize = (ulong)fileInfo.Length, // Approximation
            Size = (ulong)fileInfo.Length,
            PhysicalSectorSize = 512, // Standard sector size
            UniqueId = Guid.NewGuid(), // Generate new ID
            IsAttached = false,
            CreatedAt = fileInfo.CreationTime,
            ModifiedAt = fileInfo.LastWriteTime,
            ParentPath = null
        };
    }

    #endregion

    #region Private Helper Methods

    private IntPtr OpenVirtualDiskHandle(string vhdPath, VIRTUAL_DISK_ACCESS_MASK accessMask)
    {
        if (!File.Exists(vhdPath))
        {
            throw new FileNotFoundException($"VHD file not found: {vhdPath}");
        }

        var extension = Path.GetExtension(vhdPath).ToUpperInvariant();
        var storageType = extension == ".VHDX" 
            ? GetStorageType("VHDX") 
            : GetStorageType("VHD");

        var parameters = new OPEN_VIRTUAL_DISK_PARAMETERS
        {
            Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1,
            Version1 = new OPEN_VIRTUAL_DISK_PARAMETERS_V1 { RWDepth = 1 }
        };

        var result = OpenVirtualDisk(
            ref storageType,
            vhdPath,
            accessMask,
            OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
            ref parameters,
            out var handle);

        if (result != 0)
        {
            throw new Win32Exception((int)result, $"Failed to open virtual disk: 0x{result:X8}");
        }

        return handle;
    }

    private static VIRTUAL_STORAGE_TYPE GetStorageType(string? format)
    {
        return new VIRTUAL_STORAGE_TYPE
        {
            DeviceId = format?.ToUpperInvariant() == "VHDX" 
                ? VIRTUAL_STORAGE_TYPE_DEVICE_VHDX 
                : VIRTUAL_STORAGE_TYPE_DEVICE_VHD,
            VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT
        };
    }

    #endregion
}