using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Storage;

// hvsh storage device list
public class StorageDeviceSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class StorageDeviceCommand : AsyncCommand<StorageDeviceSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageDeviceSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var devices = await client.GetStorageDevicesAsync();
            if (devices == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve storage devices[/]");
                return 1;
            }

            if (!devices.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No storage devices found[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Type");
            table.AddColumn("Path");
            table.AddColumn("Mode");

            foreach (var device in devices)
            {
                table.AddRow(
                    device.Id.Length > 12 ? device.Id[..12] + "..." : device.Id,
                    device.DeviceType,
                    device.Path,
                    device.ReadOnly ? "[yellow]Read-Only[/]" : "[green]Read-Write[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[grey]Total: {devices.Count} device(s)[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

// hvsh storage datastore list
public class StorageDatastoreSettings : CommandSettings
{
    [CommandOption("-m|--min-gb")]
    [Description("Minimum free space in GB")]
    [DefaultValue(0)]
    public int MinGb { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class StorageDatastoreCommand : AsyncCommand<StorageDatastoreSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageDatastoreSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var locations = await client.GetStorageLocationsAsync(settings.MinGb);
            if (locations == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve storage locations[/]");
                return 1;
            }

            if (!locations.Any())
            {
                AnsiConsole.MarkupLine(settings.MinGb > 0
                    ? $"[yellow]No storage locations with at least {settings.MinGb} GB free space[/]"
                    : "[yellow]No storage locations found[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Path");
            table.AddColumn("Free Space");
            table.AddColumn("Total Space");
            table.AddColumn("Usage");

            foreach (var location in locations.OrderByDescending(l => l.FreeSpaceGB))
            {
                var usedPercent = location.TotalSpaceGB > 0
                    ? (double)(location.TotalSpaceGB - location.FreeSpaceGB) / location.TotalSpaceGB * 100
                    : 0;

                var usageColor = usedPercent > 90 ? "red" : usedPercent > 70 ? "yellow" : "green";

                table.AddRow(
                    location.Path,
                    $"{location.FreeSpaceGB} GB",
                    $"{location.TotalSpaceGB} GB",
                    $"[{usageColor}]{usedPercent:F1}%[/]"
                );
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

// hvsh storage disk list (physical disks from host)
public class StorageDiskSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class StorageDiskCommand : AsyncCommand<StorageDiskSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StorageDiskSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var hostMetrics = await client.GetHostMetricsAsync();
            if (hostMetrics == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve disk information[/]");
                return 1;
            }

            if (!hostMetrics.PhysicalDisks.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No physical disks found[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Disk");
            table.AddColumn("IOPS");
            table.AddColumn("Throughput");
            table.AddColumn("Latency");
            table.AddColumn("Queue");

            foreach (var disk in hostMetrics.PhysicalDisks)
            {
                var latencyColor = disk.LatencyMs > 20 ? "red" : disk.LatencyMs > 10 ? "yellow" : "green";

                table.AddRow(
                    disk.Name,
                    disk.Iops.ToString(),
                    $"{disk.ThroughputMBps:F1} MB/s",
                    $"[{latencyColor}]{disk.LatencyMs:F1} ms[/]",
                    disk.QueueLength.ToString()
                );
            }

            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
