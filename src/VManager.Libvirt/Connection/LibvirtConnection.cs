using System.Runtime.InteropServices;
using VManager.Libvirt.Native;

namespace VManager.Libvirt.Connection;

/// <summary>
/// Managed wrapper around a libvirt connection
/// </summary>
public class LibvirtConnection : IDisposable
{
    private IntPtr _conn;
    private bool _disposed;

    public IntPtr Handle => _conn;
    public bool IsOpen => _conn != IntPtr.Zero;

    public LibvirtConnection(string? uri = null)
    {
        _conn = LibvirtNative.virConnectOpen(uri ?? "qemu:///system");
        if (_conn == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to connect to libvirt at '{uri ?? "qemu:///system"}'");
    }

    public string GetHostname()
    {
        var ptr = LibvirtNative.virConnectGetHostname(_conn);
        if (ptr == IntPtr.Zero) return "unknown";
        var hostname = Marshal.PtrToStringUTF8(ptr) ?? "unknown";
        Marshal.FreeHGlobal(ptr);
        return hostname;
    }

    public ulong GetVersion()
    {
        LibvirtNative.virConnectGetVersion(_conn, out var version);
        return version;
    }

    public VirNodeInfo GetNodeInfo()
    {
        LibvirtNative.virNodeGetInfo(_conn, out var info);
        return info;
    }

    public ulong GetFreeMemory()
    {
        return LibvirtNative.virNodeGetFreeMemory(_conn);
    }

    public IntPtr LookupDomainByName(string name)
    {
        return LibvirtNative.virDomainLookupByName(_conn, name);
    }

    public IntPtr LookupDomainByUuid(string uuid)
    {
        return LibvirtNative.virDomainLookupByUUIDString(_conn, uuid);
    }

    public IntPtr DefineDomain(string xml)
    {
        return LibvirtNative.virDomainDefineXML(_conn, xml);
    }

    public IntPtr DefineNetwork(string xml)
    {
        return LibvirtNative.virNetworkDefineXML(_conn, xml);
    }

    public IntPtr LookupStoragePoolByName(string name)
    {
        return LibvirtNative.virStoragePoolLookupByName(_conn, name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_conn != IntPtr.Zero)
        {
            LibvirtNative.virConnectClose(_conn);
            _conn = IntPtr.Zero;
        }
    }
}
