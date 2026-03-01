using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Hardware;

// hvsh hardware info
public class HardwareInfoSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class HardwareInfoCommand : AsyncCommand<HardwareInfoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HardwareInfoSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var hardware = await client.GetHostHardwareAsync();
            if (hardware == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve hardware information[/]");
                return 1;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.HideHeaders();
            table.AddColumn("Property");
            table.AddColumn("Value");

            table.AddRow("[bold]Manufacturer[/]", hardware.Manufacturer);
            table.AddRow("[bold]Model[/]", hardware.Model);
            table.AddRow("[bold]Serial Number[/]", hardware.SerialNumber);
            table.AddRow("[bold]BIOS Version[/]", hardware.BiosVersion);
            table.AddRow("[bold]CPU[/]", hardware.CpuName);
            table.AddRow("[bold]Physical Cores[/]", hardware.CpuCores.ToString());
            table.AddRow("[bold]Logical Processors[/]", hardware.CpuLogicalProcessors.ToString());
            table.AddRow("[bold]Total Memory[/]", $"{hardware.TotalMemoryMB / 1024.0:F1} GB ({hardware.TotalMemoryMB} MB)");

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

// hvsh hardware cpu
public class HardwareCpuSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class HardwareCpuCommand : AsyncCommand<HardwareCpuSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HardwareCpuSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var hardware = await client.GetHostHardwareAsync();
            var metrics = await client.GetHostMetricsAsync();

            if (hardware == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve CPU information[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[bold]CPU:[/] {hardware.CpuName}");
            AnsiConsole.MarkupLine($"[bold]Physical Cores:[/] {hardware.CpuCores}");
            AnsiConsole.MarkupLine($"[bold]Logical Processors:[/] {hardware.CpuLogicalProcessors}");

            if (metrics != null)
            {
                var usageColor = metrics.Cpu.UsagePercent > 90 ? "red" :
                                 metrics.Cpu.UsagePercent > 70 ? "yellow" : "green";

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[bold]Current Usage:[/] [{usageColor}]{metrics.Cpu.UsagePercent:F1}%[/]");

                // Show a simple bar chart
                var barWidth = 40;
                var filledWidth = (int)(metrics.Cpu.UsagePercent / 100 * barWidth);
                var bar = new string('█', filledWidth) + new string('░', barWidth - filledWidth);
                AnsiConsole.MarkupLine($"[{usageColor}]{bar}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

// hvsh hardware memory
public class HardwareMemorySettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class HardwareMemoryCommand : AsyncCommand<HardwareMemorySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, HardwareMemorySettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var metrics = await client.GetHostMetricsAsync();
            if (metrics == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve memory information[/]");
                return 1;
            }

            var usageColor = metrics.Memory.UsagePercent > 90 ? "red" :
                             metrics.Memory.UsagePercent > 70 ? "yellow" : "green";

            AnsiConsole.MarkupLine($"[bold]Total Memory:[/] {metrics.Memory.TotalPhysicalMB / 1024.0:F1} GB");
            AnsiConsole.MarkupLine($"[bold]Used:[/] {metrics.Memory.UsedMB / 1024.0:F1} GB");
            AnsiConsole.MarkupLine($"[bold]Available:[/] {metrics.Memory.AvailableMB / 1024.0:F1} GB");
            AnsiConsole.MarkupLine($"[bold]Usage:[/] [{usageColor}]{metrics.Memory.UsagePercent:F1}%[/]");

            AnsiConsole.WriteLine();

            // Show a bar chart
            var chart = new BarChart()
                .Width(50)
                .Label("[bold]Memory Usage[/]")
                .AddItem("Used", metrics.Memory.UsedMB / 1024.0, Color.Red)
                .AddItem("Available", metrics.Memory.AvailableMB / 1024.0, Color.Green);

            AnsiConsole.Write(chart);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
