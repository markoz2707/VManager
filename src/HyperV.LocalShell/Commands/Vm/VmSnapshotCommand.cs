using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using HyperV.LocalShell.Infrastructure;

namespace HyperV.LocalShell.Commands.Vm;

public class VmSnapshotSettings : CommandSettings
{
    [CommandArgument(0, "<vmname>")]
    [Description("Name of the virtual machine")]
    public string VmName { get; set; } = "";

    [CommandOption("-l|--list")]
    [Description("List all snapshots")]
    public bool List { get; set; }

    [CommandOption("-c|--create")]
    [Description("Create a new snapshot")]
    public bool Create { get; set; }

    [CommandOption("-d|--delete")]
    [Description("Delete a snapshot")]
    public bool Delete { get; set; }

    [CommandOption("-r|--revert")]
    [Description("Revert to a snapshot")]
    public bool Revert { get; set; }

    [CommandOption("-n|--name")]
    [Description("Snapshot name (for create/delete/revert)")]
    public string? SnapshotName { get; set; }

    [CommandOption("--description")]
    [Description("Snapshot description (for create)")]
    public string? Description { get; set; }

    [CommandOption("--id")]
    [Description("Snapshot ID (for delete/revert)")]
    public string? SnapshotId { get; set; }

    [CommandOption("-u|--url")]
    [Description("Agent API URL (default: https://localhost:8743)")]
    [DefaultValue("https://localhost:8743")]
    public string ApiUrl { get; set; } = "https://localhost:8743";

    public override ValidationResult Validate()
    {
        var actionCount = new[] { List, Create, Delete, Revert }.Count(x => x);
        if (actionCount == 0)
            return ValidationResult.Error("Specify an action: --list, --create, --delete, or --revert");
        if (actionCount > 1)
            return ValidationResult.Error("Only one action can be specified at a time");

        if (Create && string.IsNullOrEmpty(SnapshotName))
            return ValidationResult.Error("--name is required when creating a snapshot");

        if ((Delete || Revert) && string.IsNullOrEmpty(SnapshotId) && string.IsNullOrEmpty(SnapshotName))
            return ValidationResult.Error("--id or --name is required for delete/revert operations");

        return ValidationResult.Success();
    }
}

public class VmSnapshotCommand : AsyncCommand<VmSnapshotSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, VmSnapshotSettings settings)
    {
        using var client = new AgentApiClient(settings.ApiUrl);

        try
        {
            if (settings.List)
                return await ListSnapshots(client, settings);
            if (settings.Create)
                return await CreateSnapshot(client, settings);
            if (settings.Delete)
                return await DeleteSnapshot(client, settings);
            if (settings.Revert)
                return await RevertSnapshot(client, settings);

            return 1;
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

    private async Task<int> ListSnapshots(AgentApiClient client, VmSnapshotSettings settings)
    {
        var snapshots = await client.GetSnapshotsAsync(settings.VmName);
        if (snapshots == null)
        {
            AnsiConsole.MarkupLine($"[red]Failed to retrieve snapshots for VM '{settings.VmName}'[/]");
            return 1;
        }

        if (snapshots.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No snapshots found for VM '{settings.VmName}'[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Created");
        table.AddColumn("Type");

        foreach (var snapshot in snapshots.OrderByDescending(s => s.CreationTime))
        {
            table.AddRow(
                snapshot.Id,
                snapshot.Name,
                snapshot.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                snapshot.Type ?? "Standard"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[grey]Total: {snapshots.Count} snapshot(s)[/]");

        return 0;
    }

    private async Task<int> CreateSnapshot(AgentApiClient client, VmSnapshotSettings settings)
    {
        var success = await AnsiConsole.Status()
            .StartAsync($"Creating snapshot '{settings.SnapshotName}' for VM '{settings.VmName}'...", async ctx =>
            {
                return await client.CreateSnapshotAsync(settings.VmName, settings.SnapshotName!, settings.Description);
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Snapshot '{settings.SnapshotName}' created successfully[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to create snapshot '{settings.SnapshotName}'[/]");
            return 1;
        }
    }

    private async Task<int> DeleteSnapshot(AgentApiClient client, VmSnapshotSettings settings)
    {
        var snapshotId = settings.SnapshotId;

        // If name provided instead of ID, try to find it
        if (string.IsNullOrEmpty(snapshotId) && !string.IsNullOrEmpty(settings.SnapshotName))
        {
            var snapshots = await client.GetSnapshotsAsync(settings.VmName);
            var snapshot = snapshots?.FirstOrDefault(s =>
                s.Name.Equals(settings.SnapshotName, StringComparison.OrdinalIgnoreCase));

            if (snapshot == null)
            {
                AnsiConsole.MarkupLine($"[red]Snapshot '{settings.SnapshotName}' not found[/]");
                return 1;
            }
            snapshotId = snapshot.Id;
        }

        var success = await AnsiConsole.Status()
            .StartAsync($"Deleting snapshot '{snapshotId}'...", async ctx =>
            {
                return await client.DeleteSnapshotAsync(settings.VmName, snapshotId!);
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Snapshot deleted successfully[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to delete snapshot[/]");
            return 1;
        }
    }

    private async Task<int> RevertSnapshot(AgentApiClient client, VmSnapshotSettings settings)
    {
        var snapshotId = settings.SnapshotId;

        // If name provided instead of ID, try to find it
        if (string.IsNullOrEmpty(snapshotId) && !string.IsNullOrEmpty(settings.SnapshotName))
        {
            var snapshots = await client.GetSnapshotsAsync(settings.VmName);
            var snapshot = snapshots?.FirstOrDefault(s =>
                s.Name.Equals(settings.SnapshotName, StringComparison.OrdinalIgnoreCase));

            if (snapshot == null)
            {
                AnsiConsole.MarkupLine($"[red]Snapshot '{settings.SnapshotName}' not found[/]");
                return 1;
            }
            snapshotId = snapshot.Id;
        }

        // Confirm revert
        if (!AnsiConsole.Confirm($"Are you sure you want to revert VM '{settings.VmName}' to snapshot '{snapshotId}'?"))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled[/]");
            return 0;
        }

        var success = await AnsiConsole.Status()
            .StartAsync($"Reverting to snapshot '{snapshotId}'...", async ctx =>
            {
                return await client.RevertSnapshotAsync(settings.VmName, snapshotId!);
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]Successfully reverted to snapshot[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to revert to snapshot[/]");
            return 1;
        }
    }
}
