using System;
using System.Runtime.InteropServices;

namespace HyperV.Core.Hcs.Interop
{
    /// <summary>
    /// Native interop for virtdisk.dll - Windows Virtual Disk API
    /// Used for direct VHD/VHDX manipulation without WMI overhead
    /// </summary>
    public static class VirtDiskNative
    {
        // VHD/VHDX format GUIDs
        public static readonly Guid VIRTUAL_STORAGE_TYPE_DEVICE_VHD = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");
        public static readonly Guid VIRTUAL_STORAGE_TYPE_DEVICE_VHDX = new Guid("CA60C1D0-1E96-4B8B-B026-41652992DFCC");

        /// <summary>
        /// Creates a virtual hard disk (VHD) image file.
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern int CreateVirtualDisk(
            [In] ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
            [In] string Path,
            [In] VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
            [In] IntPtr SecurityDescriptor,
            [In] CREATE_VIRTUAL_DISK_FLAG Flags,
            [In] uint ProviderSpecificFlags,
            [In] ref CREATE_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped,
            [Out] out IntPtr Handle);

        /// <summary>
        /// Opens a virtual hard disk (VHD) for use.
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern int OpenVirtualDisk(
            [In] ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
            [In] string Path,
            [In] VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
            [In] OPEN_VIRTUAL_DISK_FLAG Flags,
            [In] ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters,
            [Out] out IntPtr Handle);

        /// <summary>
        /// Attaches a virtual hard disk (VHD) by locating an appropriate VHD provider to accomplish the attachment.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int AttachVirtualDisk(
            [In] IntPtr VirtualDiskHandle,
            [In] IntPtr SecurityDescriptor,
            [In] ATTACH_VIRTUAL_DISK_FLAG Flags,
            [In] uint ProviderSpecificFlags,
            [In] ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped);

        /// <summary>
        /// Detaches a virtual hard disk (VHD) by locating an appropriate VHD provider to accomplish the detachment.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int DetachVirtualDisk(
            [In] IntPtr VirtualDiskHandle,
            [In] DETACH_VIRTUAL_DISK_FLAG Flags,
            [In] uint ProviderSpecificFlags);

        /// <summary>
        /// Resizes a virtual hard disk (VHD).
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int ResizeVirtualDisk(
            [In] IntPtr VirtualDiskHandle,
            [In] RESIZE_VIRTUAL_DISK_FLAG Flags,
            [In] ref RESIZE_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped);

        /// <summary>
        /// Retrieves information about a VHD.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int GetVirtualDiskInformation(
            [In] IntPtr VirtualDiskHandle,
            [In, Out] ref uint VirtualDiskInfoSize,
            [Out] IntPtr VirtualDiskInfo,
            [Out] out uint SizeUsed);

        /// <summary>
        /// Closes a handle to a virtual disk.
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Gets the last Win32 error code.
        /// </summary>
        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();

        /// <summary>
        /// Sets information for a virtual disk.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int SetVirtualDiskInformation(
            [In] IntPtr VirtualDiskHandle,
            [In] ref SET_VIRTUAL_DISK_INFO VirtualDiskInfo);

        /// <summary>
        /// Gets the physical path of an attached virtual disk.
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern int GetVirtualDiskPhysicalPath(
            [In] IntPtr VirtualDiskHandle,
            [In, Out] ref uint DiskPathSizeInBytes,
            [Out] IntPtr DiskPath);

        /// <summary>
        /// Merges a child virtual hard disk in a differencing chain with parent disks in the chain.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int MergeVirtualDisk(
            [In] IntPtr VirtualDiskHandle,
            [In] MERGE_VIRTUAL_DISK_FLAG Flags,
            [In] ref MERGE_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped);

        /// <summary>
        /// Creates a differencing virtual hard disk file.
        /// </summary>
        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern int CreateVirtualDiskFromSource(
            [In] ref VIRTUAL_STORAGE_TYPE VirtualStorageType,
            [In] string SourcePath,
            [In] string Path,
            [In] VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask,
            [In] IntPtr SecurityDescriptor,
            [In] CREATE_VIRTUAL_DISK_FLAG Flags,
            [In] uint ProviderSpecificFlags,
            [In] ref CREATE_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped,
            [Out] out IntPtr Handle);

        /// <summary>
        /// Compacts a virtual hard disk file.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int CompactVirtualDisk(
            [In] IntPtr VirtualDiskHandle,
            [In] COMPACT_VIRTUAL_DISK_FLAG Flags,
            [In] ref COMPACT_VIRTUAL_DISK_PARAMETERS Parameters,
            [In] IntPtr Overlapped);

        /// <summary>
        /// Breaks a parent-child relationship between VHD files.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int BreakMirrorVirtualDisk(
            [In] IntPtr VirtualDiskHandle);

        /// <summary>
        /// Applies a snapshot to a VHD Set file.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int ApplySnapshotVhdSet(
            [In] IntPtr VirtualDiskHandle,
            [In] ref APPLY_SNAPSHOT_VHDSET_PARAMETERS Parameters,
            [In] APPLY_SNAPSHOT_VHDSET_FLAG Flags);

        /// <summary>
        /// Deletes a snapshot from a VHD Set file.
        /// </summary>
        [DllImport("virtdisk.dll")]
        public static extern int DeleteSnapshotVhdSet(
            [In] IntPtr VirtualDiskHandle,
            [In] ref DELETE_SNAPSHOT_VHDSET_PARAMETERS Parameters,
            [In] DELETE_SNAPSHOT_VHDSET_FLAG Flags);
    }
}
