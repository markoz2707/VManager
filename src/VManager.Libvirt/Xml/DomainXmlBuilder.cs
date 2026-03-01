using System.Text;
using HyperV.Contracts.Models.Common;

namespace VManager.Libvirt.Xml;

/// <summary>
/// Builds libvirt domain XML from a CreateVmSpec
/// </summary>
public static class DomainXmlBuilder
{
    public static string Build(CreateVmSpec spec, string diskPath, string? networkBridge = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<domain type='kvm'>");
        sb.AppendLine($"  <name>{EscapeXml(spec.Name)}</name>");
        sb.AppendLine($"  <memory unit='MiB'>{spec.MemoryMB}</memory>");
        sb.AppendLine($"  <currentMemory unit='MiB'>{spec.MemoryMB}</currentMemory>");
        sb.AppendLine($"  <vcpu placement='static'>{spec.CpuCount}</vcpu>");
        sb.AppendLine("  <os>");
        sb.AppendLine("    <type arch='x86_64' machine='pc-q35-9.0'>hvm</type>");
        sb.AppendLine("    <boot dev='hd'/>");

        if (!string.IsNullOrEmpty(spec.IsoPath))
        {
            sb.AppendLine("    <boot dev='cdrom'/>");
        }

        sb.AppendLine("  </os>");
        sb.AppendLine("  <features>");
        sb.AppendLine("    <acpi/>");
        sb.AppendLine("    <apic/>");
        sb.AppendLine("  </features>");
        sb.AppendLine("  <devices>");

        // Disk
        sb.AppendLine("    <disk type='file' device='disk'>");
        sb.AppendLine($"      <driver name='qemu' type='qcow2'/>");
        sb.AppendLine($"      <source file='{EscapeXml(diskPath)}'/>");
        sb.AppendLine("      <target dev='vda' bus='virtio'/>");
        sb.AppendLine("    </disk>");

        // CD-ROM
        if (!string.IsNullOrEmpty(spec.IsoPath))
        {
            sb.AppendLine("    <disk type='file' device='cdrom'>");
            sb.AppendLine("      <driver name='qemu' type='raw'/>");
            sb.AppendLine($"      <source file='{EscapeXml(spec.IsoPath)}'/>");
            sb.AppendLine("      <target dev='sda' bus='sata'/>");
            sb.AppendLine("      <readonly/>");
            sb.AppendLine("    </disk>");
        }

        // Network
        if (!string.IsNullOrEmpty(networkBridge))
        {
            sb.AppendLine("    <interface type='bridge'>");
            sb.AppendLine($"      <source bridge='{EscapeXml(networkBridge)}'/>");
            sb.AppendLine("      <model type='virtio'/>");
            sb.AppendLine("    </interface>");
        }
        else
        {
            sb.AppendLine("    <interface type='network'>");
            sb.AppendLine("      <source network='default'/>");
            sb.AppendLine("      <model type='virtio'/>");
            sb.AppendLine("    </interface>");
        }

        // Graphics (VNC)
        sb.AppendLine("    <graphics type='vnc' port='-1' autoport='yes' listen='0.0.0.0'>");
        sb.AppendLine("      <listen type='address' address='0.0.0.0'/>");
        sb.AppendLine("    </graphics>");

        // Serial console
        sb.AppendLine("    <serial type='pty'>");
        sb.AppendLine("      <target port='0'/>");
        sb.AppendLine("    </serial>");
        sb.AppendLine("    <console type='pty'>");
        sb.AppendLine("      <target type='serial' port='0'/>");
        sb.AppendLine("    </console>");

        sb.AppendLine("  </devices>");
        sb.AppendLine("</domain>");

        return sb.ToString();
    }

    public static string BuildSnapshotXml(string name)
    {
        return $"<domainsnapshot><name>{EscapeXml(name)}</name></domainsnapshot>";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
