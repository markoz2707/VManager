using System.Runtime.InteropServices;

namespace VManager.Libvirt.Native;

/// <summary>
/// P/Invoke bindings to libvirt.so (Linux) or libvirt-0.dll (Windows dev)
/// </summary>
public static partial class LibvirtNative
{
    private const string LibvirtLib = "libvirt";

    // Connection
    [LibraryImport(LibvirtLib, EntryPoint = "virConnectOpen", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virConnectOpen(string? uri);

    [LibraryImport(LibvirtLib, EntryPoint = "virConnectClose")]
    public static partial int virConnectClose(IntPtr conn);

    [LibraryImport(LibvirtLib, EntryPoint = "virConnectGetHostname", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virConnectGetHostname(IntPtr conn);

    [LibraryImport(LibvirtLib, EntryPoint = "virConnectGetVersion")]
    public static partial int virConnectGetVersion(IntPtr conn, out ulong hvVer);

    [LibraryImport(LibvirtLib, EntryPoint = "virConnectListAllDomains")]
    public static partial int virConnectListAllDomains(IntPtr conn, out IntPtr domains, uint flags);

    // Domain
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainDefineXML", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainDefineXML(IntPtr conn, string xml);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainCreate")]
    public static partial int virDomainCreate(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainShutdown")]
    public static partial int virDomainShutdown(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainDestroy")]
    public static partial int virDomainDestroy(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSuspend")]
    public static partial int virDomainSuspend(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainResume")]
    public static partial int virDomainResume(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSave", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainSave(IntPtr domain, string path);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainReboot")]
    public static partial int virDomainReboot(IntPtr domain, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainUndefine")]
    public static partial int virDomainUndefine(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainFree")]
    public static partial int virDomainFree(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainGetName(IntPtr domain);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetUUIDString", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainGetUUIDString(IntPtr domain, [Out] byte[] uuid);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetInfo")]
    public static partial int virDomainGetInfo(IntPtr domain, out VirDomainInfo info);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetXMLDesc", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainGetXMLDesc(IntPtr domain, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainLookupByName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainLookupByName(IntPtr conn, string name);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainLookupByUUIDString", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainLookupByUUIDString(IntPtr conn, string uuid);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotCreateXML", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainSnapshotCreateXML(IntPtr domain, string xml, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotListNames")]
    public static partial int virDomainSnapshotListNames(IntPtr domain, IntPtr names, int nameslen, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotNum")]
    public static partial int virDomainSnapshotNum(IntPtr domain, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainMigrateToURI3", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainMigrateToURI3(IntPtr domain, string dconnuri, IntPtr @params, uint nparams, uint flags);

    // Domain Block Stats
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainBlockStats", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainBlockStats(IntPtr domain, string disk, out VirDomainBlockStats stats, nint size);

    // Domain Interface Stats
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainInterfaceStats", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainInterfaceStats(IntPtr domain, string path, out VirDomainInterfaceStats stats, nint size);

    // Node (Host) info
    [LibraryImport(LibvirtLib, EntryPoint = "virNodeGetInfo")]
    public static partial int virNodeGetInfo(IntPtr conn, out VirNodeInfo info);

    [LibraryImport(LibvirtLib, EntryPoint = "virNodeGetFreeMemory")]
    public static partial ulong virNodeGetFreeMemory(IntPtr conn);

    // Network
    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkDefineXML", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virNetworkDefineXML(IntPtr conn, string xml);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkCreate")]
    public static partial int virNetworkCreate(IntPtr network);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkDestroy")]
    public static partial int virNetworkDestroy(IntPtr network);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkUndefine")]
    public static partial int virNetworkUndefine(IntPtr network);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkFree")]
    public static partial int virNetworkFree(IntPtr network);

    [LibraryImport(LibvirtLib, EntryPoint = "virConnectListAllNetworks")]
    public static partial int virConnectListAllNetworks(IntPtr conn, out IntPtr networks, uint flags);

    // Storage Pool
    [LibraryImport(LibvirtLib, EntryPoint = "virConnectListAllStoragePools")]
    public static partial int virConnectListAllStoragePools(IntPtr conn, out IntPtr pools, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolFree")]
    public static partial int virStoragePoolFree(IntPtr pool);

    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolGetInfo")]
    public static partial int virStoragePoolGetInfo(IntPtr pool, out VirStoragePoolInfo info);

    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolLookupByName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStoragePoolLookupByName(IntPtr conn, string name);

    // Storage Volume
    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolCreateXML", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStorageVolCreateXML(IntPtr pool, string xml, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolDelete")]
    public static partial int virStorageVolDelete(IntPtr vol, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolResize")]
    public static partial int virStorageVolResize(IntPtr vol, ulong capacity, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolGetInfo")]
    public static partial int virStorageVolGetInfo(IntPtr vol, out VirStorageVolInfo info);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolFree")]
    public static partial int virStorageVolFree(IntPtr vol);

    // Snapshot management
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotDelete")]
    public static partial int virDomainSnapshotDelete(IntPtr snapshot, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotRevert")]
    public static partial int virDomainSnapshotRevert(IntPtr snapshot, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotLookupByName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainSnapshotLookupByName(IntPtr domain, string name, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotFree")]
    public static partial int virDomainSnapshotFree(IntPtr snapshot);

    [LibraryImport(LibvirtLib, EntryPoint = "virDomainSnapshotGetXMLDesc", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainSnapshotGetXMLDesc(IntPtr snapshot, uint flags);

    // Network provider support
    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkGetXMLDesc", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virNetworkGetXMLDesc(IntPtr network, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkGetName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virNetworkGetName(IntPtr network);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkGetUUIDString", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virNetworkGetUUIDString(IntPtr network, [Out] byte[] uuid);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkLookupByName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virNetworkLookupByName(IntPtr conn, string name);

    [LibraryImport(LibvirtLib, EntryPoint = "virNetworkIsActive")]
    public static partial int virNetworkIsActive(IntPtr network);

    // Storage pool additional support
    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolGetXMLDesc", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStoragePoolGetXMLDesc(IntPtr pool, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolListAllVolumes")]
    public static partial int virStoragePoolListAllVolumes(IntPtr pool, out IntPtr vols, uint flags);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolGetName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStorageVolGetName(IntPtr vol);

    [LibraryImport(LibvirtLib, EntryPoint = "virStorageVolGetPath", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStorageVolGetPath(IntPtr vol);

    [LibraryImport(LibvirtLib, EntryPoint = "virStoragePoolGetName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virStoragePoolGetName(IntPtr pool);

    // QEMU Guest Agent
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainQemuAgentCommand", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr virDomainQemuAgentCommand(IntPtr domain, string cmd, int timeout, uint flags);

    // Migration status
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetJobStats")]
    public static partial int virDomainGetJobStats(IntPtr domain, out int type, out IntPtr @params, out int nparams, uint flags);

    // Online disk resize
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainBlockResize", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int virDomainBlockResize(IntPtr domain, string disk, ulong size, uint flags);

    // Guest OS info (libvirt 5.7+)
    [LibraryImport(LibvirtLib, EntryPoint = "virDomainGetGuestInfo")]
    public static partial int virDomainGetGuestInfo(IntPtr domain, uint types, out IntPtr @params, out int nparams, uint flags);

    // Free helper
    [LibraryImport(LibvirtLib, EntryPoint = "virFree")]
    public static partial void virFree(IntPtr ptr);
}

// Structures

[StructLayout(LayoutKind.Sequential)]
public struct VirDomainInfo
{
    public byte State;
    public ulong MaxMem;  // KB
    public ulong Memory;  // KB
    public ushort NrVirtCpu;
    public ulong CpuTime; // nanoseconds
}

[StructLayout(LayoutKind.Sequential)]
public struct VirDomainBlockStats
{
    public long RdReq;
    public long RdBytes;
    public long WrReq;
    public long WrBytes;
    public long Errs;
}

[StructLayout(LayoutKind.Sequential)]
public struct VirDomainInterfaceStats
{
    public long RxBytes;
    public long RxPackets;
    public long RxErrs;
    public long RxDrop;
    public long TxBytes;
    public long TxPackets;
    public long TxErrs;
    public long TxDrop;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VirNodeInfo
{
    public fixed byte Model[32];
    public ulong Memory; // KB
    public uint Cpus;
    public uint Mhz;
    public uint Nodes;
    public uint Sockets;
    public uint Cores;
    public uint Threads;
}

[StructLayout(LayoutKind.Sequential)]
public struct VirStoragePoolInfo
{
    public int State;
    public ulong Capacity;
    public ulong Allocation;
    public ulong Available;
}

[StructLayout(LayoutKind.Sequential)]
public struct VirStorageVolInfo
{
    public int Type;
    public ulong Capacity;
    public ulong Allocation;
}
