using Fortigate.Backup.Core;
using Fortigate.Backup.Core.Models;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Extensions;
using File = System.IO.File;

namespace Fortigate.Backup.Cli
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Clear();
            SqliteDataAccess.InitializeDatabase();
            IConfiguration configuration = ConfigHelper.GetConfig();
            if (!ValidateKey.EnsureKeyExists())
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#%&/()=?";
                var random = new Random();
                string newKey = new string(Enumerable.Range(1, 32).Select(_ => chars[random.Next(chars.Length)]).ToArray());
                AnsiConsole.MarkupLine("[red bold]!!! SECURITY ERROR !!![/]");
                AnsiConsole.MarkupLine("[red]No encryption key (environment variable) found.\n" +
                    "Please set the environment variable 'Fortigate_Backup__SecretKey' to a secure 32 byte string value before running the program.\n" +
                    "The program is being terminated to protect your data.[/]\n");
                AnsiConsole.MarkupLine($"Suggested key: [blue]{newKey}[/]\n");
                AnsiConsole.MarkupLine("[bold]Windows:[/]");
                AnsiConsole.MarkupLine($"setx Fortigate_Backup__SecretKey \"[blue]{newKey}[/]\"\n" +
                    $"RefreshEnv\n");
                AnsiConsole.MarkupLine("[bold]Linux/macOS:[/]");
                AnsiConsole.MarkupLine($"export Fortigate_Backup__SecretKey=\"[blue]{newKey}[/]\"\n" +
                    $"RefreshEnv\n");
                Console.ReadKey();
                return; // Stop programmet
            }
            if (!ValidateKey.EnsureKeyIsValid())
            {
                AnsiConsole.MarkupLine("[red bold]!!! SECURITY ERROR !!![/]");
                AnsiConsole.MarkupLine("[red]Your encryption key (environment variable) does not match the database.\n" +
                    "The program is being terminated to protect your data.[/]\n");
                Console.ReadKey();
                return; // Stop programmet
            }

            if(args.Length > 0)
            {
                string command = args[0].ToLower();
                switch (command)
                {
                    case "backup":
                        await HandleBackupCommand();
                        return;
                    case "backupall":
                        await HandleBackupAllCommand();
                        return;
                    default:
                        AnsiConsole.MarkupLine($"[red]Unknown command: {command}[/]");
                        return;
                }
            }

            var environments = new Dictionary<string, string>
            {
                { "list", "List all Fortigates in the database." },
                { "add", "Add a new Fortigate to the database." },
                { "edit", "Edit an existing Fortigate in the database" },
                { "delete", "Delete a Fortigate from the database" },
                { "backup", "Backup a Fortigate configuration" },
                { "backupAll", "Backup all Fortigates in the database" },
                { "exit", "Exit the program" }
            };

            do {
                Console.Clear();
                FigletText title = new FigletText("Fortigate Backup")
                .Centered()
                .Color(Color.Green);

                AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("green dim")));
                AnsiConsole.Write(title);
                AnsiConsole.Write(new Rule().RuleStyle(Style.Parse("green dim")));

                var selected = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a task:")
                        .UseConverter(key => environments[key])
                        .AddChoices(environments.Keys));

                Console.Clear();

                switch (selected)
                {
                    case "add":
                        await HandleAddCommand();
                        break;
                    case "list":
                        await HandleListCommand();
                        break;
                    case "edit":
                        await HandleEditCommand();
                        break;
                    case "delete":
                        await HandleDeleteCommand();
                        break;
                    case "backup":
                        await HandleBackupCommand();
                        Console.ReadKey();
                        break;
                    case "backupAll":
                        await HandleBackupAllCommand();
                        Console.ReadKey();
                        break;
                    default:
                        return;
                }
            } while (true);
        }

        private static Task<int> HandleAddCommand()
        {
            AnsiConsole.MarkupLine("[bold]Add a new Fortigate:[/]");

            string name = AnsiConsole.Prompt(
                new TextPrompt<string>("Name:")
            );
            string ipAddress = AnsiConsole.Prompt(
                new TextPrompt<string>("IP Address:")
            );
            int port = AnsiConsole.Prompt(
                new TextPrompt<int>("Port:")
                    .DefaultValue(443)
            );
            string apikey = AnsiConsole.Prompt(
                new TextPrompt<string>("API Key:")
                    .Secret()
            );
            var gate = new GateModel
            {
                Name = name,
                IpAddress = ipAddress,
                Port = port,
                Apikey = CryptoService.Encrypt(apikey)
            };

            SqliteDataAccess.SaveGate(gate);

            AnsiConsole.MarkupLine($"Device added: [blue]{name}[/] with IP: [blue]{ipAddress}[/] on Port: [blue]{port}[/]");
            return Task.FromResult(0);
        }

        private static Task<int> HandleListCommand()
        {
            // Implement your logic for the 'list' command here
            AnsiConsole.MarkupLine("[bold]All Fortigates:[/]");
            Table table = new Table();
            table.AddColumn("[bold]ID[/]");
            table.AddColumn("[bold]Name[/]");
            table.AddColumn("[bold]IP Address[/]");
            table.AddColumn("[bold]Port[/]");

            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                table.AddRow(gate.Id.ToString(), gate.Name, gate.IpAddress, gate.Port.ToString());
            }
            AnsiConsole.Write(table);
            Console.ReadKey();
            return Task.FromResult(0);
        }

        private static Task<int> HandleEditCommand()
        {
            int id = ShowSelectionList("Select Fortigate to edit:");

            if(id <= 0) return Task.FromResult(0);

            var gate = SqliteDataAccess.LoadGateById(id);

            if (gate == null)
            {
                Console.WriteLine($"Device with ID {id} not found.");
                Console.ReadKey();
                return Task.FromResult(1);
            }

            string name = AnsiConsole.Prompt(
                new TextPrompt<string>("Name:")
                .DefaultValue(gate.Name)
            );
            string ipAddress = AnsiConsole.Prompt(
                new TextPrompt<string>("IP Address:")
                .DefaultValue(gate.IpAddress)
            );
            int port = AnsiConsole.Prompt(
                new TextPrompt<int>("Port:")
                    .DefaultValue((int)gate.Port)
            );
            string apikey = AnsiConsole.Prompt(
                new TextPrompt<string>("API Key:")
                    .Secret()
                    .AllowEmpty()
            );
            
            gate.Name = name;
            gate.IpAddress = ipAddress;
            gate.Port = port;
            if (apikey != null && apikey != "") gate.Apikey = CryptoService.Encrypt(apikey);
            SqliteDataAccess.UpdateGate(gate);
            AnsiConsole.MarkupLine($"\nDevice updated");
            Console.ReadKey();
            return Task.FromResult(0);
        }

        private static Task<int> HandleDeleteCommand()
        {
            int id = ShowSelectionList("Select Fortigate to delete:");

            if (id <= 0) return Task.FromResult(0);

            var gate = SqliteDataAccess.LoadGateById(id);

            if (gate == null)
            {
                Console.WriteLine($"Device with ID {id} not found.");
                Console.ReadKey();
                return Task.FromResult(1);
            }

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title($"Are you sure you want to delete this device '{gate.Name} ({gate.IpAddress})'?")
                .AddChoices(new[] { "No", "Yes" }));

            if(selected == "Yes")
            {
                SqliteDataAccess.DeleteGate(gate.Id);
                AnsiConsole.MarkupLine($"\nDevice deleted");
                Console.ReadKey();
            }
            return Task.FromResult(0);
        }

        private static async Task<int> HandleBackupCommand()
        {
            var list = new Dictionary<int, string>();
            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                list.Add(gate.Id, $"{gate.Name} ({gate.IpAddress})");
            }

            var selected = AnsiConsole.Prompt(
                new MultiSelectionPrompt<int>()
                    .Title("Select Fortigates to backup:")
                    .UseConverter(key => list[key])
                    .AddChoices(list.Keys)
                    .PageSize(24));
                

            foreach (var item in selected)
            {
                string createText = null;
                var gate = SqliteDataAccess.LoadGateById(item);
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Binary)
                    .StartAsync($"Backing up device: {gate.Name} ({gate.IpAddress})...", async ctx =>
                    {
                        createText = await BackupGate.Backup(gate);
                    }
                );
                if (createText == null)
                {
                    AnsiConsole.MarkupLine($"[red]000000 Unable to backup the device: {gate.Name} ({gate.IpAddress})[/]");
                    continue;
                }
                var path = Path.Combine("Backups", $"{gate.Name}_{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");
                File.WriteAllText(path, createText);

                AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
            }
            AnsiConsole.MarkupLine("\nAll selected devices have been processed.");
            return 0;
        }

        private static async Task<int> HandleBackupAllCommand()
        {
            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                string createText = null;
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Binary)
                    .StartAsync($"Backing up device: {gate.Name} ({gate.IpAddress})...", async ctx =>
                    {
                        createText = await BackupGate.Backup(gate);
                    }
                );
                if (createText == null)
                {
                    AnsiConsole.MarkupLine($"[red]000000 Unable to backup the device: {gate.Name} ({gate.IpAddress})[/]");
                    continue;
                }
                var path = Path.Combine("Backups", $"{gate.Name}_{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");
                File.WriteAllText(path, createText);
                AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
            }
            AnsiConsole.MarkupLine("\nAll devices have been processed.");
            return 0;
        }

        private static int ShowSelectionList(string title = "Select Fortigate:")
        {
            var list = new Dictionary<int, string>();
            var gates = SqliteDataAccess.LoadGates();
            list.Add(0, "[red]Exit[/]");
            foreach (var gate in gates)
            {
                list.Add(gate.Id, $"{gate.Name} ({gate.IpAddress})");
            }
            list.Add(-1, "[red]Exit[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<int>()
                    .Title(title)
                    .UseConverter(key => list[key])
                    .AddChoices(list.Keys)
                    .PageSize(24));

            return selected;
        }
    }
}
