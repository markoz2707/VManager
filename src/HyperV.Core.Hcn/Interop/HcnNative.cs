using System;
using System.Runtime.InteropServices;

namespace HyperV.Core.Hcn.Interop;

/// <summary>Interop z Host Compute Network (HCN).</summary>
internal static class HcnNative
{
    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnCreateNetwork(Guid id, string settingsJson, out IntPtr network, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnDeleteNetwork(Guid id, out IntPtr errorRecord);

    [DllImport("computenetwork.dll")] internal static extern void HcnCloseNetwork(IntPtr network);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnOpenNetwork(Guid id, out IntPtr network, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnCreateEndpoint(Guid networkId, Guid endpointId, string settingsJson, out IntPtr endpoint, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnDeleteEndpoint(Guid endpointId, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnOpenEndpoint(Guid endpointId, out IntPtr endpoint, out IntPtr errorRecord);

    [DllImport("computenetwork.dll")] internal static extern void HcnCloseEndpoint(IntPtr endpoint);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnQueryEndpointProperties(IntPtr endpoint, string query, out string result, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnQueryNetworkProperties(IntPtr network, string query, out string result, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnEnumerateNetworks(string query, out string networks, out IntPtr errorRecord);

    [DllImport("computenetwork.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcnEnumerateEndpoints(string query, out string endpoints, out IntPtr errorRecord);
}
