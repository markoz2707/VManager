using System.ComponentModel;
using System.ServiceProcess;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Host;

// hvsh system info
public class SystemInfoSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class SystemInfoCommand : AsyncCommand<SystemInfoSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SystemInfoSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var hardware = await client.GetHostHardwareAsync();
            var system = await client.GetHostSystemAsync();

            if (hardware == null && system == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to retrieve system information[/]");
                return 1;
            }

            // Hardware Info
            if (hardware != null)
            {
                var hardwarePanel = new Panel(new Rows(
                    new Markup($"[bold]Manufacturer:[/] {hardware.Manufacturer}"),
                    new Markup($"[bold]Model:[/] {hardware.Model}"),
                    new Markup($"[bold]Serial:[/] {hardware.SerialNumber}"),
                    new Markup($"[bold]BIOS:[/] {hardware.BiosVersion}"),
                    new Markup($"[bold]CPU:[/] {hardware.CpuName}"),
                    new Markup($"[bold]Cores:[/] {hardware.CpuCores} ({hardware.CpuLogicalProcessors} logical processors)"),
                    new Markup($"[bold]Memory:[/] {hardware.TotalMemoryMB / 1024.0:F1} GB")
                ))
                {
                    Header = new PanelHeader("Hardware Information"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(hardwarePanel);
                AnsiConsole.WriteLine();
            }

            // System Info
            if (system != null)
            {
                var uptime = DateTime.Now - system.LastBootTime;
                var uptimeStr = uptime.Days > 0
                    ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                    : $"{uptime.Hours}h {uptime.Minutes}m";

                var systemPanel = new Panel(new Rows(
                    new Markup($"[bold]OS:[/] {system.OsName}"),
                    new Markup($"[bold]Version:[/] {system.OsVersion}"),
                    new Markup($"[bold]Build:[/] {system.BuildNumber}"),
                    new Markup($"[bold]UUID:[/] {system.SystemUuid}"),
                    new Markup($"[bold]Last Boot:[/] {system.LastBootTime:yyyy-MM-dd HH:mm:ss}"),
                    new Markup($"[bold]Uptime:[/] [green]{uptimeStr}[/]")
                ))
                {
                    Header = new PanelHeader("System Information"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(systemPanel);
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

// hvsh system health
public class SystemHealthSettings : CommandSettings
{
    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";
}

public class SystemHealthCommand : AsyncCommand<SystemHealthSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SystemHealthSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            var health = await client.GetHealthAsync();
            if (health == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to connect to agent - service may be offline[/]");
                return 1;
            }

            var statusColor = health.Status.ToLower() == "healthy" ? "green" : "red";
            AnsiConsole.MarkupLine($"[bold]Service Status:[/] [{statusColor}]{health.Status}[/]");
            AnsiConsole.MarkupLine($"[bold]Timestamp:[/] {health.Timestamp:yyyy-MM-dd HH:mm:ss}");

            if (health.Components.Any())
            {
                AnsiConsole.WriteLine();
                var table = new Table();
                table.Border(TableBorder.Simple);
                table.AddColumn("Component");
                table.AddColumn("Status");

                foreach (var component in health.Components)
                {
                    var compColor = component.Value.ToLower() == "healthy" ? "green" : "red";
                    table.AddRow(component.Key, $"[{compColor}]{component.Value}[/]");
                }

                AnsiConsole.Write(table);
            }

            return health.Status.ToLower() == "healthy" ? 0 : 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

// hvsh system service (manage Windows services)
public class SystemServiceSettings : CommandSettings
{
    [CommandArgument(0, "[service]")]
    [Description("Service name (e.g., vmcompute, vmms)")]
    public string? ServiceName { get; set; }

    [CommandOption("-l|--list")]
    [Description("List Hyper-V related services")]
    public bool List { get; set; }

    [CommandOption("--start")]
    [Description("Start the service")]
    public bool Start { get; set; }

    [CommandOption("--stop")]
    [Description("Stop the service")]
    public bool Stop { get; set; }

    [CommandOption("--restart")]
    [Description("Restart the service")]
    public bool Restart { get; set; }
}

public class SystemServiceCommand : Command<SystemServiceSettings>
{
    public override int Execute(CommandContext context, SystemServiceSettings settings)
    {
        if (settings.List)
        {
            return ListServices();
        }

        if (string.IsNullOrEmpty(settings.ServiceName))
        {
            AnsiConsole.MarkupLine("[red]Service name required. Use --list to see available services.[/]");
            return 1;
        }

        var actionCount = new[] { settings.Start, settings.Stop, settings.Restart }.Count(x => x);
        if (actionCount == 0)
        {
            // Just show status
            return ShowServiceStatus(settings.ServiceName);
        }

        if (actionCount > 1)
        {
            AnsiConsole.MarkupLine("[red]Only one action (--start, --stop, --restart) can be specified[/]");
            return 1;
        }

        if (settings.Start) return ControlService(settings.ServiceName, "start");
        if (settings.Stop) return ControlService(settings.ServiceName, "stop");
        if (settings.Restart) return ControlService(settings.ServiceName, "restart");

        return 1;
    }

    private int ListServices()
    {
        var hypervServices = new[]
        {
            ("vmcompute", "Hyper-V Host Compute Service"),
            ("vmms", "Hyper-V Virtual Machine Management"),
            ("vmicheartbeat", "Hyper-V Heartbeat Service"),
            ("vmickvpexchange", "Hyper-V Data Exchange Service"),
            ("vmicrdv", "Hyper-V Remote Desktop Virtualization"),
            ("vmicshutdown", "Hyper-V Guest Shutdown Service"),
            ("vmictimesync", "Hyper-V Time Synchronization Service"),
            ("vmicvss", "Hyper-V Volume Shadow Copy Requestor"),
            ("HvHost", "HV Host Service"),
            ("nvspwmi", "Hyper-V Network Provider")
        };

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Service");
        table.AddColumn("Description");
        table.AddColumn("Status");

        foreach (var (name, description) in hypervServices)
        {
            try
            {
                using var sc = new ServiceController(name);
                var statusColor = sc.Status == ServiceControllerStatus.Running ? "green" : "red";
                table.AddRow(name, description, $"[{statusColor}]{sc.Status}[/]");
            }
            catch
            {
                table.AddRow(name, description, "[grey]Not installed[/]");
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private int ShowServiceStatus(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            var statusColor = sc.Status == ServiceControllerStatus.Running ? "green" : "red";
            AnsiConsole.MarkupLine($"[bold]Service:[/] {sc.ServiceName}");
            AnsiConsole.MarkupLine($"[bold]Display Name:[/] {sc.DisplayName}");
            AnsiConsole.MarkupLine($"[bold]Status:[/] [{statusColor}]{sc.Status}[/]");
            return 0;
        }
        catch (InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Service '{serviceName}' not found[/]");
            return 1;
        }
    }

    private int ControlService(string serviceName, string action)
    {
        try
        {
            using var sc = new ServiceController(serviceName);

            AnsiConsole.Status().Start($"{action} service '{serviceName}'...", ctx =>
            {
                switch (action)
                {
                    case "start":
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;
                    case "stop":
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        break;
                    case "restart":
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;
                }
            });

            AnsiConsole.MarkupLine($"[green]Service '{serviceName}' {action} completed[/]");
            return 0;
        }
        catch (InvalidOperationException)
        {
            AnsiConsole.MarkupLine($"[red]Service '{serviceName}' not found[/]");
            return 1;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            AnsiConsole.MarkupLine($"[red]Timeout waiting for service '{serviceName}' to {action}[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[grey]Note: Service control may require administrator privileges[/]");
            return 1;
        }
    }
}
