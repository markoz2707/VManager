using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Network;

// hvsh network list
public class NetworkListSettings : CommandSettings
{
    [CommandOption("--hcn")]
    [Description("Show only HCN networks")]
    public bool HcnOnly { get; set; }

    [CommandOption("--wmi")]
    [Description("Show only WMI virtual switches")]
    public bool WmiOnly { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class NetworkListCommand : AsyncCommand<NetworkListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NetworkListSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var networks = await client.GetNetworksAsync();
            if (networks == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve network list[/]");
                return 1;
            }

            // HCN Networks
            if (!settings.WmiOnly && networks.HcnNetworks.Any())
            {
                AnsiConsole.MarkupLine("[bold underline]HCN Networks[/]");
                var hcnTable = new Table();
                hcnTable.Border(TableBorder.Rounded);
                hcnTable.AddColumn("Name");
                hcnTable.AddColumn("ID");
                hcnTable.AddColumn("Type");
                hcnTable.AddColumn("Subnet");

                foreach (var net in networks.HcnNetworks)
                {
                    hcnTable.AddRow(
                        net.Name,
                        net.Id.Length > 8 ? net.Id[..8] + "..." : net.Id,
                        net.Type,
                        net.Subnet ?? "-"
                    );
                }
                AnsiConsole.Write(hcnTable);
                AnsiConsole.WriteLine();
            }

            // WMI Switches
            if (!settings.HcnOnly && networks.WmiSwitches.Any())
            {
                AnsiConsole.MarkupLine("[bold underline]Virtual Switches (WMI)[/]");
                var wmiTable = new Table();
                wmiTable.Border(TableBorder.Rounded);
                wmiTable.AddColumn("Name");
                wmiTable.AddColumn("ID");
                wmiTable.AddColumn("Type");

                foreach (var sw in networks.WmiSwitches)
                {
                    var typeColor = sw.Type.ToLower() switch
                    {
                        "external" => "green",
                        "internal" => "yellow",
                        "private" => "grey",
                        _ => "white"
                    };
                    wmiTable.AddRow(
                        sw.Name,
                        sw.Id.Length > 8 ? sw.Id[..8] + "..." : sw.Id,
                        $"[{typeColor}]{sw.Type}[/]"
                    );
                }
                AnsiConsole.Write(wmiTable);
            }

            var total = (settings.HcnOnly ? 0 : networks.HcnNetworks.Count) +
                        (settings.WmiOnly ? 0 : networks.WmiSwitches.Count);

            if (total == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No networks found[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"\n[grey]Total: {total} network(s)[/]");
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
}

// hvsh network adapter list
public class NetworkAdapterSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class NetworkAdapterCommand : AsyncCommand<NetworkAdapterSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NetworkAdapterSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var hostMetrics = await client.GetHostMetricsAsync();
            if (hostMetrics == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve network adapters[/]");
                return 1;
            }

            if (!hostMetrics.NetworkAdapters.Any())
            {
                AnsiConsole.MarkupLine("[yellow]No network adapters found[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("Speed");
            table.AddColumn("TX Rate");
            table.AddColumn("RX Rate");

            foreach (var adapter in hostMetrics.NetworkAdapters)
            {
                table.AddRow(
                    adapter.Name,
                    $"{adapter.SpeedMbps} Mbps",
                    FormatBytes(adapter.BytesSentPerSec) + "/s",
                    FormatBytes(adapter.BytesReceivedPerSec) + "/s"
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

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
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
