using System.Text;

namespace VManager.Libvirt.Xml;

public static class VolumeXmlBuilder
{
    public static string Build(string name, long capacityBytes, string format = "qcow2")
    {
        var sb = new StringBuilder();
        sb.AppendLine("<volume>");
        sb.AppendLine($"  <name>{name}</name>");
        sb.AppendLine($"  <capacity unit='bytes'>{capacityBytes}</capacity>");
        sb.AppendLine("  <target>");
        sb.AppendLine($"    <format type='{format}'/>");
        sb.AppendLine("  </target>");
        sb.AppendLine("</volume>");
        return sb.ToString();
    }
}
