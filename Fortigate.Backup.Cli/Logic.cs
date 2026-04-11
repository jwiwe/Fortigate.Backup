using Fortigate.Backup.Core;
using Fortigate.Backup.Core.Models;
using Serilog;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace Fortigate.Backup.Cli
{
    public class Logic
    {
        public static Task<int> HandleAddCommand()
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
            Log.Information("Added new device: {Name} ({IpAddress}:{Port})", name, ipAddress, port);
            return Task.FromResult(0);
        }

        public static Task<int> HandleListCommand()
        {
            AnsiConsole.MarkupLine("[bold]All Fortigates:[/]");
            Table table = new Table();
            table.AddColumn("[bold]ID[/]");
            table.AddColumn("[bold]Name[/]");
            table.AddColumn("[bold]IP Address[/]");
            table.AddColumn("[bold]Port[/]");

            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                table.AddRow(gate.Id.ToString(), gate.Name ?? string.Empty, gate.IpAddress ?? string.Empty, gate.Port.ToString() ?? string.Empty);
            }
            AnsiConsole.Write(table);
            return Task.FromResult(0);
        }

        public static Task<int> HandleEditCommand()
        {
            int id = ShowSelectionList("Select Fortigate to edit:");

            if (id <= 0) return Task.FromResult(0);

            var gate = SqliteDataAccess.LoadGateById(id);

            if (gate == null)
            {
                Console.WriteLine($"Device with ID {id} not found.");
                return Task.FromResult(1);
            }

            string? name = AnsiConsole.Prompt(
                new TextPrompt<string?>("Name:")
                .DefaultValue(gate.Name)
            );
            string? ipAddress = AnsiConsole.Prompt(
                new TextPrompt<string?>("IP Address:")
                .DefaultValue(gate.IpAddress)
            );
            int? port = AnsiConsole.Prompt(
                new TextPrompt<int?>("Port:")
                    .DefaultValue((int?)gate.Port)
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
            Log.Information("Updated device: {Name} ({IpAddress}:{Port})", gate.Name, gate.IpAddress, gate.Port);
            return Task.FromResult(0);
        }

        public static Task<int> HandleDeleteCommand()
        {
            int id = ShowSelectionList("Select Fortigate to delete:");

            if (id <= 0) return Task.FromResult(0);

            var gate = SqliteDataAccess.LoadGateById(id);

            if (gate == null)
            {
                Console.WriteLine($"Device with ID {id} not found.");
                return Task.FromResult(1);
            }

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title($"Are you sure you want to delete this device '{gate.Name} ({gate.IpAddress})'?")
                .AddChoices(new[] { "No", "Yes" }));

            if (selected == "Yes")
            {
                SqliteDataAccess.DeleteGate(gate.Id);
                AnsiConsole.MarkupLine($"\nDevice deleted");
                Log.Information("Deleted device: {Name} ({IpAddress}:{Port})", gate.Name, gate.IpAddress, gate.Port);
            }
            return Task.FromResult(0);
        }

        public static async Task<int> HandleBackupCommand(int id = 0)
        {
            List<int> selected;
            if (id == 0)
            {
                var list = new Dictionary<int, string>();
                var gates = SqliteDataAccess.LoadGates();
                foreach (var gate in gates)
                {
                    list.Add(gate.Id, $"{gate.Name} ({gate.IpAddress})");
                }

                selected = AnsiConsole.Prompt(
                    new MultiSelectionPrompt<int>()
                        .Title("Select Fortigates to backup:")
                        .UseConverter(key => list[key])
                        .AddChoices(list.Keys)
                        .PageSize(24));
            }
            else
            {
                selected = new List<int> { id };
            }


            foreach (var item in selected)
            {
                string? createText = null;
                var gate = SqliteDataAccess.LoadGateById(item);
                if (gate == null)
                {
                }
                else
                {
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Binary)
                        .StartAsync($"Backing up device: {gate.Name ?? string.Empty} ({gate.IpAddress ?? string.Empty})...", async ctx =>
                        {
                            createText = await BackupGate.Backup(gate);
                        }
                    );
                    if (createText == null)
                    {
                        AnsiConsole.MarkupLine($"[red]000000 Unable to backup the device: {gate.Name} ({gate.IpAddress})[/]");
                        Log.Error("Unable to backup the device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                        continue;
                    }
                    var path = Path.Combine("Backups", $"{gate.Name}", $"{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");

                    string pattern = @"#conf_file_ver=(?<version>\d+)\s+#buildno=(?<build>\d+)";
                    var match = Regex.Match(createText, pattern);

                    if (match.Success)
                    {
                        if (match.Groups["version"].Value != gate.ConfVer || match.Groups["build"].Value != gate.BuildNo)
                        {
                            await SaveFileAsync(path, createText);
                            gate.ConfVer = match.Groups["version"].Value;
                            gate.BuildNo = match.Groups["build"].Value;
                            SqliteDataAccess.UpdateGate(gate);
                            AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                            Log.Information("Backuped up device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]000000 No changes detected for device: {gate.Name} ({gate.IpAddress}), skipping backup.[/]");
                            Log.Information("No changes detected for device: {Name} ({IpAddress}), skipping backup.", gate.Name, gate.IpAddress);
                        }
                    }
                    else
                    {
                        await SaveFileAsync(path, createText);
                        gate.ConfVer = match.Groups["version"].Value;
                        gate.BuildNo = match.Groups["build"].Value;
                        SqliteDataAccess.UpdateGate(gate);
                        AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                        Log.Information("Backuped up device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                    }
                }
            }
            AnsiConsole.MarkupLine("\nAll selected devices have been processed.");
            Log.Information("All selected devices have been processed.");
            return 0;
        }

        public static async Task<int> HandleBackupAllCommand(bool force = false)
        {
            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                string? createText = null;
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
                    Log.Error("Unable to backup the device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                    continue;
                }
                var path = Path.Combine("Backups", $"{gate.Name}", $"{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");

                string pattern = @"#conf_file_ver=(?<version>\d+)\s+#buildno=(?<build>\d+)";
                var match = Regex.Match(createText, pattern);

                if (match.Success)
                {
                    if (match.Groups["version"].Value != gate.ConfVer || match.Groups["build"].Value != gate.BuildNo || force)
                    {
                        await SaveFileAsync(path, createText);
                        gate.ConfVer = match.Groups["version"].Value;
                        gate.BuildNo = match.Groups["build"].Value;
                        SqliteDataAccess.UpdateGate(gate);
                        AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                        Log.Information("Backuped up device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]000000 No changes detected for device: {gate.Name} ({gate.IpAddress}), skipping backup.[/]");
                        Log.Information("No changes detected for device: {Name} ({IpAddress}), skipping backup.", gate.Name, gate.IpAddress);
                    }
                }
                else
                {
                    await SaveFileAsync(path, createText);
                    gate.ConfVer = match.Groups["version"].Value;
                    gate.BuildNo = match.Groups["build"].Value;
                    SqliteDataAccess.UpdateGate(gate);
                    AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                    Log.Information("Backuped up device: {Name} ({IpAddress})", gate.Name, gate.IpAddress);
                }
            }
            AnsiConsole.MarkupLine("\nAll devices have been processed.");
            Log.Information("All devices have been processed.");
            return 0;
        }

        public async static Task SaveFileAsync(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            Log.Information("Saved backup file: {Path}", path);
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
