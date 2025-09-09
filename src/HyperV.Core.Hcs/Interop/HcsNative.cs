using System;
using System.Runtime.InteropServices;

namespace HyperV.Core.Hcs.Interop;

/// <summary>Interop z Host Compute System (HCS).</summary>
internal static class HcsNative
{
    [DllImport("computecore.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr HcsCreateOperation(IntPtr context, IntPtr callback);

    [DllImport("computecore.dll", CharSet = CharSet.Unicode)]
    internal static extern int HcsCreateComputeSystem(string id, string configuration, IntPtr operation, IntPtr securityDescriptor, out IntPtr computeSystem);

    [DllImport("computecore.dll")] internal static extern int HcsStartComputeSystem(IntPtr system, IntPtr operation, string options);
    [DllImport("computecore.dll")] internal static extern int HcsGetComputeSystemProperties(IntPtr system, IntPtr operation, string query);
    [DllImport("computecore.dll")] internal static extern int HcsModifyComputeSystem(IntPtr system, IntPtr operation, string configuration, IntPtr identity);
    [DllImport("computecore.dll")] internal static extern int HcsOpenComputeSystem(string id, out IntPtr system, ref Guid riid);
    [DllImport("computecore.dll")] internal static extern int HcsShutDownComputeSystem(IntPtr system, IntPtr operation, string options);
    [DllImport("computecore.dll")] internal static extern int HcsTerminateComputeSystem(IntPtr system, IntPtr operation);
    [DllImport("computecore.dll")] internal static extern int HcsSaveComputeSystem(IntPtr system, IntPtr operation, string options);
    [DllImport("computecore.dll")] internal static extern int HcsWaitForOperationResult(IntPtr operation, uint timeoutMs, out IntPtr resultJson);
    [DllImport("computecore.dll")] internal static extern void HcsCloseOperation(IntPtr op);
    [DllImport("computecore.dll")] internal static extern void HcsCloseComputeSystem(IntPtr sys);
    [DllImport("computecore.dll")] internal static extern int HcsPauseComputeSystem(IntPtr system, IntPtr operation, string options);
    [DllImport("computecore.dll")] internal static extern int HcsResumeComputeSystem(IntPtr system, IntPtr operation, string options);
}
