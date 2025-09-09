using System;
using System.Runtime.InteropServices;

namespace HyperV.Core.Hcs.Interop
{
    public static class VirtDiskNative
    {
        [DllImport("virtdisk.dll")]
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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }
}
