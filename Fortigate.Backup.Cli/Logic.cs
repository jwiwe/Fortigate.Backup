using Fortigate.Backup.Cli.Commands;
using Fortigate.Backup.Cli.Models;
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
            string hostname = AnsiConsole.Prompt(
                new TextPrompt<string>("IP Address/Hostname:")
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
                Hostname = hostname,
                Port = port,
                Apikey = CryptoService.Encrypt(apikey)
            };

            SqliteDataAccess.SaveGate(gate);

            AnsiConsole.MarkupLine($"Device added: [blue]{name}[/] with IP: [blue]{hostname}[/] on Port: [blue]{port}[/]");
            Log.Information("Added new device: {Name} ({Hostname}:{Port})", name, hostname, port);
            return Task.FromResult(0);
        }

        public static Task<int> HandleListCommand()
        {
            AnsiConsole.MarkupLine("[bold]All Fortigates:[/]");
            Table table = new Table();
            table.AddColumn("[bold]ID[/]");
            table.AddColumn("[bold]Name[/]");
            table.AddColumn("[bold]IP Address/Hostname[/]");
            table.AddColumn("[bold]Port[/]");

            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                table.AddRow(gate.Id.ToString(), gate.Name ?? string.Empty, gate.Hostname ?? string.Empty, gate.Port.ToString() ?? string.Empty);
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
            string? hostname = AnsiConsole.Prompt(
                new TextPrompt<string?>("IP Address/Hostname:")
                .DefaultValue(gate.Hostname)
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
            gate.Hostname = hostname;
            gate.Port = port;
            if (apikey != null && apikey != "") gate.Apikey = CryptoService.Encrypt(apikey);
            SqliteDataAccess.UpdateGate(gate);
            AnsiConsole.MarkupLine($"\nDevice updated");
            Log.Information("Updated device: {Name} ({Hostname}:{Port})", gate.Name, gate.Hostname, gate.Port);
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
                .Title($"Are you sure you want to delete this device '{gate.Name} ({gate.Hostname})'?")
                .AddChoices(new[] { "No", "Yes" }));

            if (selected == "Yes")
            {
                SqliteDataAccess.DeleteGate(gate.Id);
                AnsiConsole.MarkupLine($"\nDevice deleted");
                Log.Information("Deleted device: {Name} ({Hostname}:{Port})", gate.Name, gate.Hostname, gate.Port);
            }
            return Task.FromResult(0);
        }

        public static async Task<int> HandleBackupCommand(int id = 0, bool sendEmail = false)
        {
            List<int> selected;
            var report = new List<BackupResult>();
            if (id == 0)
            {
                var list = new Dictionary<int, string>();
                var gates = SqliteDataAccess.LoadGates();
                foreach (var gate in gates)
                {
                    list.Add(gate.Id, $"{gate.Name} ({gate.Hostname})");
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
                var gate = SqliteDataAccess.LoadGateById(item);
                if (gate == null)
                {
                    continue;
                }
                var result = new BackupResult { Name = gate.Name, Hostname = gate.Hostname };
                try
                {
                    string? content = null;
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Binary)
                        .StartAsync($"Backing up device: {gate.Name ?? string.Empty} ({gate.Hostname ?? string.Empty})...", async ctx =>
                        {
                            content = await BackupGate.Backup(gate);
                        }
                    );
                    if (content == null)
                    {
                        AnsiConsole.MarkupLine($"[red]000000 Unable to backup the device: {gate.Name} ({gate.Hostname})[/]");
                        Log.Error("Unable to backup the device: {Name} ({IpAddress})", gate.Name, gate.Hostname);
                        result.Success = false;
                        result.Message = "Unable to backup the device";
                        continue;
                    }
                    var path = Path.Combine("Backups", $"{gate.Name}", $"{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");

                    string pattern = @"#conf_file_ver=(?<version>\d+)\s+#buildno=(?<build>\d+)";
                    var match = Regex.Match(content, pattern);

                    if (match.Success)
                    {
                        if (match.Groups["version"].Value != gate.ConfVer || match.Groups["build"].Value != gate.BuildNo)
                        {
                            await SaveFileAsync(path, content);
                            gate.ConfVer = match.Groups["version"].Value;
                            gate.BuildNo = match.Groups["build"].Value;
                            SqliteDataAccess.UpdateGate(gate);
                            AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.Hostname})");
                            Log.Information("Backuped up device: {Name} ({Hostname})", gate.Name, gate.Hostname);
                            result.Success = true;
                            result.Message = "Backup successful";
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]000000 No changes detected for device: {gate.Name} ({gate.Hostname}), skipping backup.[/]");
                            Log.Information("No changes detected for device: {Name} ({Hostname}), skipping backup.", gate.Name, gate.Hostname);
                            result.Success = true;
                            result.Message = "No changes detected, backup skipped";
                        }
                    }
                    else
                    {
                        await SaveFileAsync(path, content);
                        gate.ConfVer = match.Groups["version"].Value;
                        gate.BuildNo = match.Groups["build"].Value;
                        SqliteDataAccess.UpdateGate(gate);
                        AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.Hostname})");
                        Log.Information("Backuped up device: {Name} ({Hostname})", gate.Name, gate.Hostname);
                        result.Success = true;
                        result.Message = "Backup successful";
                    }
                }
                catch (HttpRequestException ex)
                {
                    Log.Error("HTTP request error on {name}: {Message}", gate.Name, ex.StatusCode);
                    AnsiConsole.MarkupLine($"[red]000000 HTTP request error on {gate.Name}: {ex.StatusCode}[/]");
                    result.Success = false;
                    result.Message = $"HTTP request error: {ex.StatusCode}";
                }
                catch (Exception ex)
                {
                    Log.Error("Error backing up device {name}: {Message}", gate.Name, ex.Message);
                    AnsiConsole.MarkupLine($"[red]000000 Error backing up device {gate.Name}: {ex.Message}[/]");
                    result.Success = false;
                    result.Message = "Error backing up device";
                }
                report.Add(result);
            }
            AnsiConsole.MarkupLine("\nAll selected devices have been processed.");
            Log.Information("All selected devices have been processed.");
            if (sendEmail)
                await MailService.SendReportEmail(report);
            return 0;
        }

        public static async Task<int> HandleBackupAllCommand(bool force = false, bool sendEmail = false)
        {
            var report = new List<BackupResult>();
            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                var result = new BackupResult { Name = gate.Name, Hostname = gate.Hostname };
                try
                {
                    string? content = null;
                    await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Binary)
                        .StartAsync($"Backing up device: {gate.Name} ({gate.Hostname})...", async ctx =>
                        {
                            content = await BackupGate.Backup(gate);
                        }
                    );
                    if (content == null)
                    {
                        AnsiConsole.MarkupLine($"[red]000000 Unable to backup the device: {gate.Name} ({gate.Hostname})[/]");
                        Log.Error("Unable to backup the device: {Name} ({Hostname})", gate.Name, gate.Hostname);
                        result.Success = false;
                        result.Message = "Unable to backup the device";
                        continue;
                    }
                    var path = Path.Combine("Backups", $"{gate.Name}", $"{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");

                    string pattern = @"#conf_file_ver=(?<version>\d+)\s+#buildno=(?<build>\d+)";
                    var match = Regex.Match(content, pattern);

                    if (match.Success)
                    {
                        if (match.Groups["version"].Value != gate.ConfVer || match.Groups["build"].Value != gate.BuildNo || force)
                        {
                            await SaveFileAsync(path, content);
                            gate.ConfVer = match.Groups["version"].Value;
                            gate.BuildNo = match.Groups["build"].Value;
                            SqliteDataAccess.UpdateGate(gate);
                            AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.Hostname})");
                            Log.Information("Backuped up device: {Name} ({Hostname})", gate.Name, gate.Hostname);
                            result.Success = true;
                            result.Message = "Backup successful";
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[yellow]000000 No changes detected for device: {gate.Name} ({gate.Hostname}), skipping backup.[/]");
                            Log.Information("No changes detected for device: {Name} ({Hostname}), skipping backup.", gate.Name, gate.Hostname);
                            result.Success = true;
                            result.Message = "No changes detected, backup skipped";
                        }
                    }
                    else
                    {
                        await SaveFileAsync(path, content);
                        gate.ConfVer = match.Groups["version"].Value;
                        gate.BuildNo = match.Groups["build"].Value;
                        SqliteDataAccess.UpdateGate(gate);
                        AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.Hostname})");
                        Log.Information("Backuped up device: {Name} ({Hostname})", gate.Name, gate.Hostname);
                        result.Success = true;
                        result.Message = "Backup successful";
                    }
                }
                catch (HttpRequestException ex)
                {
                    AnsiConsole.MarkupLine($"[red]000000 HTTP request error on {gate.Name}: {ex.StatusCode}[/]");
                    Log.Error("HTTP request error on {name}: {Message}", gate.Name, ex.StatusCode);
                    result.Success = false;
                    result.Message = $"HTTP request error: {ex.StatusCode}";
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]000000 Error backing up device {gate.Name}: {ex.Message}[/]");
                    Log.Error("Error backing up device {name}: {Message}", gate.Name, ex.Message);
                    result.Success = false;
                    result.Message = "Error backing up device";
                }
                report.Add(result);
            }
            AnsiConsole.MarkupLine("\nAll devices have been processed.");
            Log.Information("All devices have been processed.");
            if (sendEmail)
                await MailService.SendReportEmail(report);
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
                list.Add(gate.Id, $"{gate.Name} ({gate.Hostname})");
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

        public static Task<int> HandleCleanupCommand(CleanupSettings settings)
        {
            var backupDir = "Backups";
            if (!Directory.Exists(backupDir))
            {
                AnsiConsole.MarkupLine("[yellow]No backups directory found.[/]");
                return Task.FromResult(0);
            }

            var deviceDirs = Directory.GetDirectories(backupDir);
            int totalDeleted = 0;

            foreach (var devDir in deviceDirs)
            {
                var files = new DirectoryInfo(devDir).GetFiles("*.conf")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                List<FileInfo> toDelete = new List<FileInfo>();

                if (settings.KeepCount.HasValue)
                {
                    toDelete = files.Skip(settings.KeepCount.Value).ToList();
                }
                else if (settings.KeepDays.HasValue)
                {
                    var cutoffDate = DateTime.Now.AddDays(-settings.KeepDays.Value);
                    toDelete = files.Where(f => f.CreationTime < cutoffDate).ToList();
                }

                foreach (var file in toDelete)
                {
                    file.Delete();
                    Log.Information("Deleted old backup file: {FileName}", file.FullName);
                }

                if (toDelete.Count > 0)
                {
                    AnsiConsole.MarkupLine($"Cleaned up {toDelete.Count} files in [blue]{new DirectoryInfo(devDir).Name}[/]");
                    totalDeleted += toDelete.Count;
                }
            }

            AnsiConsole.MarkupLine($"[green]Cleanup complete. Removed a total of {totalDeleted} file(s).[/]");
            return Task.FromResult(0);
        }
    }
}
