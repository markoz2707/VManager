using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Vm;

public class VmPowerSettings : CommandSettings
{
    [CommandArgument(0, "<vmname>")]
    [Description("Name of the virtual machine")]
    public string VmName { get; set; } = "";

    [CommandOption("--on")]
    [Description("Power on the VM")]
    public bool PowerOn { get; set; }

    [CommandOption("--off")]
    [Description("Force power off the VM")]
    public bool PowerOff { get; set; }

    [CommandOption("--shutdown")]
    [Description("Graceful shutdown (requires integration services)")]
    public bool Shutdown { get; set; }

    [CommandOption("--pause")]
    [Description("Pause the VM")]
    public bool Pause { get; set; }

    [CommandOption("--resume")]
    [Description("Resume a paused VM")]
    public bool Resume { get; set; }

    [CommandOption("--save")]
    [Description("Save VM state to disk")]
    public bool Save { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";

    public override ValidationResult Validate()
    {
        var actionCount = new[] { PowerOn, PowerOff, Shutdown, Pause, Resume, Save }.Count(x => x);
        if (actionCount == 0)
            return ValidationResult.Error("Specify an action: --on, --off, --shutdown, --pause, --resume, or --save");
        if (actionCount > 1)
            return ValidationResult.Error("Only one action can be specified at a time");
        return ValidationResult.Success();
    }
}

public class VmPowerCommand : AsyncCommand<VmPowerSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VmPowerSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            string action;
            Func<Task<bool>> operation;

            if (settings.PowerOn)
            {
                action = "Starting";
                operation = () => client.StartVmAsync(settings.VmName);
            }
            else if (settings.PowerOff)
            {
                action = "Stopping";
                operation = () => client.StopVmAsync(settings.VmName);
            }
            else if (settings.Shutdown)
            {
                action = "Shutting down";
                operation = () => client.ShutdownVmAsync(settings.VmName);
            }
            else if (settings.Pause)
            {
                action = "Pausing";
                operation = () => client.PauseVmAsync(settings.VmName);
            }
            else if (settings.Resume)
            {
                action = "Resuming";
                operation = () => client.ResumeVmAsync(settings.VmName);
            }
            else if (settings.Save)
            {
                action = "Saving";
                operation = () => client.SaveVmAsync(settings.VmName);
            }
            else
            {
                return 1;
            }

            var success = await AnsiConsole.Status()
                .StartAsync($"{action} VM '{settings.VmName}'...", async ctx =>
                {
                    return await operation();
                });

            if (success)
            {
                AnsiConsole.MarkupLine($"[green]Successfully completed: {action.ToLower()} '{settings.VmName}'[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to {action.ToLower()} VM '{settings.VmName}'[/]");
                return 1;
            }
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
