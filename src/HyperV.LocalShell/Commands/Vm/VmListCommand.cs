using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Vm;

public class VmListSettings : CommandSettings
{
    [CommandOption("-r|--running")]
    [Description("Show only running VMs")]
    public bool RunningOnly { get; set; }

    [CommandOption("-s|--stopped")]
    [Description("Show only stopped VMs")]
    public bool StoppedOnly { get; set; }

    [CommandOption("--hcs")]
    [Description("Show only HCS VMs")]
    public bool HcsOnly { get; set; }

    [CommandOption("--wmi")]
    [Description("Show only WMI VMs")]
    public bool WmiOnly { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class VmListCommand : AsyncCommand<VmListSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VmListSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var vms = await client.GetVmsAsync();
            if (vms == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve VM list from agent[/]");
                return 1;
            }

            var allVms = new List<(VmInfo Vm, string Source)>();

            if (!settings.WmiOnly)
            {
                foreach (var vm in vms.HcsVms)
                    allVms.Add((vm, "HCS"));
            }

            if (!settings.HcsOnly)
            {
                foreach (var vm in vms.WmiVms)
                    allVms.Add((vm, "WMI"));
            }

            // Apply filters
            if (settings.RunningOnly)
                allVms = allVms.Where(v => v.Vm.State.Equals("Running", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (settings.StoppedOnly)
                allVms = allVms.Where(v => v.Vm.State.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
                                          v.Vm.State.Equals("Stopped", StringComparison.OrdinalIgnoreCase)).ToList();

            if (allVms.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No VMs found matching criteria[/]");
                return 0;
            }

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("Name");
            table.AddColumn("State");
            table.AddColumn("CPU");
            table.AddColumn("Memory (MB)");
            table.AddColumn("Source");

            foreach (var (vm, source) in allVms)
            {
                var stateColor = vm.State.ToLower() switch
                {
                    "running" => "green",
                    "off" or "stopped" => "red",
                    "paused" => "yellow",
                    "saved" => "blue",
                    _ => "grey"
                };

                table.AddRow(
                    vm.Name,
                    $"[{stateColor}]{vm.State}[/]",
                    vm.CpuCount.ToString(),
                    vm.MemoryMB.ToString(),
                    source
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[grey]Total: {allVms.Count} VM(s)[/]");

            return 0;
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection error: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Make sure the HyperV Agent is running on the specified URL[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}
