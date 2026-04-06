using Spectre.Console;
using Spectre.Console.Cli;

namespace Fortigate.Backup.Cli.Commands
{
    internal class InteractiveCommand : AsyncCommand
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            var menu = new Dictionary<string, string>
            {
                { "list", "List all Fortigates in the database." },
                { "add", "Add a new Fortigate to the database." },
                { "edit", "Edit an existing Fortigate in the database" },
                { "delete", "Delete a Fortigate from the database" },
                { "backup", "Backup a Fortigate configuration" },
                { "backupAll", "Backup all Fortigates in the database" },
                { "exit", "Exit the program" }
            };

            while (true)
            {
                Console.Clear();
                AnsiConsole.Write(new FigletText("Fortigate Backup").Color(Color.Green));

                var selected = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Vælg opgave:")
                        .UseConverter(key => menu[key])
                        .AddChoices(menu.Keys));

                if (selected == "exit") return 0;

                // Her kalder du dine eksisterende metoder fra Program.cs (gør dem public static)
                switch (selected)
                {
                    case "add": await Logic.HandleAddCommand(); break;
                    case "list": await Logic.HandleListCommand(); break;
                    case "edit": await Logic.HandleEditCommand(); break;
                    case "delete": await Logic.HandleDeleteCommand(); break;
                    case "backup": await Logic.HandleBackupCommand(); break;
                    case "backupAll": await Logic.HandleBackupAllCommand(); break;
                }

                AnsiConsole.MarkupLine("\n[grey]Press any key to continue...[/]");
                Console.ReadKey();
            }
        }
    }
}
