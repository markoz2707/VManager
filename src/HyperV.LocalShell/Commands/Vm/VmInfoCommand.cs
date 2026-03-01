using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Vm;

public class VmInfoSettings : CommandSettings
{
    [CommandArgument(0, "<vmname>")]
    [Description("Name of the virtual machine")]
    public string VmName { get; set; } = "";

    [CommandOption("-m|--metrics")]
    [Description("Include performance metrics")]
    public bool IncludeMetrics { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class VmInfoCommand : AsyncCommand<VmInfoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VmInfoSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var vmInfo = await client.GetVmInfoAsync(settings.VmName);
            if (vmInfo == null)
            {
                AnsiConsole.MarkupLine($"[red]VM '{settings.VmName}' not found[/]");
                return 1;
            }

            // VM Properties Panel
            var panel = new Panel(new Rows(
                new Markup($"[bold]Name:[/] {vmInfo.Name}"),
                new Markup($"[bold]ID:[/] {vmInfo.Id}"),
                new Markup($"[bold]State:[/] {GetStateMarkup(vmInfo.State)}"),
                new Markup($"[bold]CPUs:[/] {vmInfo.CpuCount}"),
                new Markup($"[bold]Memory:[/] {vmInfo.MemoryMB} MB"),
                new Markup($"[bold]Dynamic Memory:[/] {(vmInfo.EnableDynamicMemory ? "[green]Enabled[/]" : "[grey]Disabled[/]")}"),
                vmInfo.EnableDynamicMemory
                    ? new Markup($"[bold]Memory Range:[/] {vmInfo.MinMemoryMB ?? 0} - {vmInfo.MaxMemoryMB ?? vmInfo.MemoryMB} MB")
                    : Text.Empty
            ))
            {
                Header = new PanelHeader($"Virtual Machine: {settings.VmName}"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);

            // Performance metrics if requested
            if (settings.IncludeMetrics)
            {
                AnsiConsole.WriteLine();
                var metrics = await client.GetVmMetricsAsync(settings.VmName);
                if (metrics != null)
                {
                    RenderMetrics(metrics);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Metrics not available for this VM[/]");
                }
            }

            return 0;
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection error: {ex.Message}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private static string GetStateMarkup(string state)
    {
        var color = state.ToLower() switch
        {
            "running" => "green",
            "off" or "stopped" => "red",
            "paused" => "yellow",
            "saved" => "blue",
            "starting" or "stopping" => "orange1",
            _ => "grey"
        };
        return $"[{color}]{state}[/]";
    }

    private static void RenderMetrics(VmUsageSummary metrics)
    {
        AnsiConsole.MarkupLine("[bold underline]Performance Metrics[/]");
        AnsiConsole.WriteLine();

        // CPU
        var cpuColor = metrics.Cpu.UsagePercent > 90 ? "red" : metrics.Cpu.UsagePercent > 70 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold]CPU Usage:[/] [{cpuColor}]{metrics.Cpu.UsagePercent:F1}%[/]");
        AnsiConsole.MarkupLine($"[bold]Guest CPU:[/] {metrics.Cpu.GuestAverageUsage:F1}%");

        // Memory
        var memColor = metrics.Memory.UsagePercent > 90 ? "red" : metrics.Memory.UsagePercent > 70 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold]Memory:[/] [{memColor}]{metrics.Memory.UsagePercent:F1}%[/] ({metrics.Memory.DemandMB} / {metrics.Memory.AssignedMB} MB)");
        AnsiConsole.MarkupLine($"[bold]Memory Status:[/] {metrics.Memory.Status}");

        // Disks
        if (metrics.Disks.Any())
        {
            AnsiConsole.WriteLine();
            var diskTable = new Table();
            diskTable.Border(TableBorder.Simple);
            diskTable.AddColumn("Disk");
            diskTable.AddColumn("Read IOPS");
            diskTable.AddColumn("Write IOPS");
            diskTable.AddColumn("Latency");
            diskTable.AddColumn("Throughput");

            foreach (var disk in metrics.Disks)
            {
                diskTable.AddRow(
                    disk.Name,
                    disk.ReadIops.ToString(),
                    disk.WriteIops.ToString(),
                    $"{disk.LatencyMs:F2} ms",
                    FormatBytes(disk.ThroughputBytesPerSec) + "/s"
                );
            }
            AnsiConsole.Write(diskTable);
        }

        // Network
        if (metrics.Networks.Any())
        {
            AnsiConsole.WriteLine();
            var netTable = new Table();
            netTable.Border(TableBorder.Simple);
            netTable.AddColumn("Adapter");
            netTable.AddColumn("Sent");
            netTable.AddColumn("Received");
            netTable.AddColumn("Dropped");

            foreach (var net in metrics.Networks)
            {
                netTable.AddRow(
                    net.AdapterName,
                    FormatBytes(net.BytesSentPerSec) + "/s",
                    FormatBytes(net.BytesReceivedPerSec) + "/s",
                    net.PacketsDropped.ToString()
                );
            }
            AnsiConsole.Write(netTable);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }
}
