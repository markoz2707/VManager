using Spectre.Console;

AnsiConsole.MarkupLine("[bold]HyperV Local Shell[/] (Server Core friendly)");
while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Co chcesz zrobić?")
            .AddChoices("Konfiguracja sieci (HCN)", "Włącz zdalne zarządzanie (WinRM)", "Shell", "Wyjście"));

    switch (choice)
    {
        case "Konfiguracja sieci (HCN)":
            AnsiConsole.MarkupLine("Tu wywołamy polecenia HCN (placeholder).");
            break;
        case "Włącz zdalne zarządzanie (WinRM)":
            AnsiConsole.MarkupLine("winrm quickconfig (placeholder) – uruchom przez PowerShell w implementacji.");
            break;
        case "Shell":
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=true});
            return;
        default: return;
    }
}