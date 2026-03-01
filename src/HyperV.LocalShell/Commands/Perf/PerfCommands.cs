using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Perf;

// hvsh perf top - interactive performance monitor (esxtop equivalent)
public class PerfTopSettings : CommandSettings
{
    [CommandOption("-i|--interval")]
    [Description("Refresh interval in seconds (default: 2)")]
    [DefaultValue(2)]
    public int Interval { get; set; } = 2;

    [CommandOption("-c|--count")]
    [Description("Number of iterations (default: unlimited)")]
    [DefaultValue(0)]
    public int Count { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class PerfTopCommand : AsyncCommand<PerfTopSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PerfTopSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);
        var iteration = 0;

        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to exit[/]");
        AnsiConsole.WriteLine();

        try
        {
            while (settings.Count == 0 || iteration < settings.Count)
            {
                iteration++;

                var hostMetrics = await client.GetHostMetricsAsync();
                var vms = await client.GetVmsAsync();

                if (hostMetrics == null)
                {
                    AnsiConsole.MarkupLine("[red]Failed to retrieve metrics[/]");
                    await Task.Delay(settings.Interval * 1000);
                    continue;
                }

                // Clear console and render
                AnsiConsole.Clear();
                RenderHeader(hostMetrics);
                RenderHostMetrics(hostMetrics);

                if (vms != null)
                {
                    await RenderVmMetrics(client, vms);
                }

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[grey]Iteration {iteration} | Refresh: {settings.Interval}s | Press Ctrl+C to exit[/]");

                await Task.Delay(settings.Interval * 1000);
            }

            return 0;
        }
        catch (TaskCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private void RenderHeader(HostUsageSummary metrics)
    {
        var now = DateTime.Now;
        AnsiConsole.MarkupLine($"[bold]hvsh perf top[/] - {now:yyyy-MM-dd HH:mm:ss}");
        AnsiConsole.WriteLine();
    }

    private void RenderHostMetrics(HostUsageSummary metrics)
    {
        // CPU and Memory summary
        var cpuColor = metrics.Cpu.UsagePercent > 90 ? "red" : metrics.Cpu.UsagePercent > 70 ? "yellow" : "green";
        var memColor = metrics.Memory.UsagePercent > 90 ? "red" : metrics.Memory.UsagePercent > 70 ? "yellow" : "green";

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[bold]CPU:[/] [{cpuColor}]{metrics.Cpu.UsagePercent,5:F1}%[/] ({metrics.Cpu.Cores} cores, {metrics.Cpu.LogicalProcessors} logical)",
            $"[bold]MEM:[/] [{memColor}]{metrics.Memory.UsagePercent,5:F1}%[/] ({metrics.Memory.UsedMB / 1024.0:F1} / {metrics.Memory.TotalPhysicalMB / 1024.0:F1} GB)"
        );

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        // Physical Disks
        if (metrics.PhysicalDisks.Any())
        {
            AnsiConsole.MarkupLine("[bold underline]Physical Disks[/]");
            var diskTable = new Table();
            diskTable.Border(TableBorder.Simple);
            diskTable.AddColumn("Disk");
            diskTable.AddColumn(new TableColumn("IOPS").RightAligned());
            diskTable.AddColumn(new TableColumn("MB/s").RightAligned());
            diskTable.AddColumn(new TableColumn("Latency").RightAligned());
            diskTable.AddColumn(new TableColumn("Queue").RightAligned());

            foreach (var disk in metrics.PhysicalDisks.Take(5))
            {
                var latencyColor = disk.LatencyMs > 20 ? "red" : disk.LatencyMs > 10 ? "yellow" : "green";
                diskTable.AddRow(
                    disk.Name.Length > 20 ? disk.Name[..20] + "..." : disk.Name,
                    disk.Iops.ToString(),
                    $"{disk.ThroughputMBps:F1}",
                    $"[{latencyColor}]{disk.LatencyMs:F1}[/]",
                    disk.QueueLength.ToString()
                );
            }
            AnsiConsole.Write(diskTable);
            AnsiConsole.WriteLine();
        }

        // Network Adapters
        if (metrics.NetworkAdapters.Any())
        {
            AnsiConsole.MarkupLine("[bold underline]Network Adapters[/]");
            var netTable = new Table();
            netTable.Border(TableBorder.Simple);
            netTable.AddColumn("Adapter");
            netTable.AddColumn(new TableColumn("Speed").RightAligned());
            netTable.AddColumn(new TableColumn("TX/s").RightAligned());
            netTable.AddColumn(new TableColumn("RX/s").RightAligned());

            foreach (var adapter in metrics.NetworkAdapters.Take(5))
            {
                netTable.AddRow(
                    adapter.Name.Length > 30 ? adapter.Name[..30] + "..." : adapter.Name,
                    $"{adapter.SpeedMbps} Mbps",
                    FormatBytes(adapter.BytesSentPerSec),
                    FormatBytes(adapter.BytesReceivedPerSec)
                );
            }
            AnsiConsole.Write(netTable);
            AnsiConsole.WriteLine();
        }
    }

    private async Task RenderVmMetrics(AgentApiClient client, VmListResponse vms)
    {
        var runningVms = vms.HcsVms.Concat(vms.WmiVms)
            .Where(v => v.State.Equals("Running", StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        if (!runningVms.Any())
            return;

        AnsiConsole.MarkupLine("[bold underline]Running VMs[/]");
        var vmTable = new Table();
        vmTable.Border(TableBorder.Simple);
        vmTable.AddColumn("VM Name");
        vmTable.AddColumn(new TableColumn("CPU").RightAligned());
        vmTable.AddColumn(new TableColumn("Memory").RightAligned());
        vmTable.AddColumn(new TableColumn("Status").Centered());

        foreach (var vm in runningVms)
        {
            var metrics = await client.GetVmMetricsAsync(vm.Name);
            if (metrics != null)
            {
                var cpuColor = metrics.Cpu.UsagePercent > 90 ? "red" : metrics.Cpu.UsagePercent > 70 ? "yellow" : "green";
                var memColor = metrics.Memory.UsagePercent > 90 ? "red" : metrics.Memory.UsagePercent > 70 ? "yellow" : "green";

                vmTable.AddRow(
                    vm.Name.Length > 25 ? vm.Name[..25] + "..." : vm.Name,
                    $"[{cpuColor}]{metrics.Cpu.UsagePercent:F1}%[/]",
                    $"[{memColor}]{metrics.Memory.UsagePercent:F1}%[/] ({metrics.Memory.DemandMB}MB)",
                    $"[green]{vm.State}[/]"
                );
            }
            else
            {
                vmTable.AddRow(
                    vm.Name.Length > 25 ? vm.Name[..25] + "..." : vm.Name,
                    "-",
                    $"{vm.MemoryMB} MB",
                    $"[green]{vm.State}[/]"
                );
            }
        }

        AnsiConsole.Write(vmTable);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }
}

// hvsh perf stats <vmname> - show VM performance statistics
public class PerfStatsSettings : CommandSettings
{
    [CommandArgument(0, "<vmname>")]
    [Description("Name of the virtual machine")]
    public string VmName { get; set; } = "";

    [CommandOption("-w|--watch")]
    [Description("Continuously watch (refresh every 2 seconds)")]
    public bool Watch { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class PerfStatsCommand : AsyncCommand<PerfStatsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PerfStatsSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            do
            {
                if (settings.Watch)
                    AnsiConsole.Clear();

                var metrics = await client.GetVmMetricsAsync(settings.VmName);
                if (metrics == null)
                {
                    AnsiConsole.MarkupLine($"[red]Failed to retrieve metrics for VM '{settings.VmName}'[/]");
                    if (!settings.Watch) return 1;
                    await Task.Delay(2000);
                    continue;
                }

                RenderVmStats(settings.VmName, metrics);

                if (settings.Watch)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[grey]Press Ctrl+C to exit[/]");
                    await Task.Delay(2000);
                }

            } while (settings.Watch);

            return 0;
        }
        catch (TaskCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }

    private void RenderVmStats(string vmName, VmUsageSummary metrics)
    {
        AnsiConsole.MarkupLine($"[bold]Performance Statistics: {vmName}[/]");
        AnsiConsole.MarkupLine($"[grey]{DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");
        AnsiConsole.WriteLine();

        // CPU
        var cpuColor = metrics.Cpu.UsagePercent > 90 ? "red" : metrics.Cpu.UsagePercent > 70 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold]CPU Usage:[/] [{cpuColor}]{metrics.Cpu.UsagePercent:F1}%[/]");
        AnsiConsole.MarkupLine($"[bold]Guest CPU:[/] {metrics.Cpu.GuestAverageUsage:F1}%");

        // Memory
        var memColor = metrics.Memory.UsagePercent > 90 ? "red" : metrics.Memory.UsagePercent > 70 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[bold]Memory:[/] [{memColor}]{metrics.Memory.UsagePercent:F1}%[/]");
        AnsiConsole.MarkupLine($"[bold]Demand/Assigned:[/] {metrics.Memory.DemandMB} / {metrics.Memory.AssignedMB} MB");
        AnsiConsole.MarkupLine($"[bold]Status:[/] {metrics.Memory.Status}");

        // Disks
        if (metrics.Disks.Any())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold underline]Disk I/O[/]");
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
            AnsiConsole.MarkupLine("[bold underline]Network I/O[/]");
            var netTable = new Table();
            netTable.Border(TableBorder.Simple);
            netTable.AddColumn("Adapter");
            netTable.AddColumn("TX");
            netTable.AddColumn("RX");
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
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }
}
