using Fortigate.Backup.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Fortigate.Backup.Cli.Commands
{
    public class BackupCommand : AsyncCommand<BackupSettings>
    {
        protected async override Task<int> ExecuteAsync(CommandContext context, BackupSettings settings, CancellationToken cancellationToken)
        {
            int id = settings.Id ?? 0;
            if (id > 0)
            {
                string? createText = null;
                var gate = SqliteDataAccess.LoadGateById(id);
                if (gate == null)
                {
                    AnsiConsole.MarkupLine($"[red]000000 Device with ID {id} not found.[/]");
                    return -1;
                }
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
                }
                var path = Path.Combine("Backups", $"{gate.Name}", $"{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.conf");

                string pattern = @"#conf_file_ver=(?<version>\d+)\s+#buildno=(?<build>\d+)";
                var match = Regex.Match(createText ?? string.Empty, pattern);

                if (match.Success)
                {
                    if (match.Groups["version"].Value != gate.ConfVer || match.Groups["build"].Value != gate.BuildNo || settings.Force)
                    {
                        await Logic.SaveFileAsync(path, createText ?? string.Empty);
                        gate.ConfVer = match.Groups["version"].Value;
                        gate.BuildNo = match.Groups["build"].Value;
                        SqliteDataAccess.UpdateGate(gate);
                AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]000000 No changes detected for device: {gate.Name} ({gate.IpAddress}), skipping backup.[/]");
                    }
                }
                else
                {
                    await Logic.SaveFileAsync(path, createText ?? string.Empty);
                    gate.ConfVer = match.Groups["version"].Value;
                    gate.BuildNo = match.Groups["build"].Value;
                    SqliteDataAccess.UpdateGate(gate);
                    AnsiConsole.MarkupLine($"111111 Backuped up device: {gate.Name} ({gate.IpAddress})");
                }
                return 0;
            }
            await Logic.HandleBackupAllCommand();
            return 0;
        }
    }

    public class BackupSettings : CommandSettings
    {
        [CommandOption("-i|--id")]
        [Description("ID of the device to backup. If not provided, all devices will be backed up.")]
        public int? Id { get; set; }
        [CommandOption("-f|--force")]
        [Description("Force backup even if no changes are detected.")]
        [DefaultValue(false)]
        public bool Force { get; set; } = false;
    }
}
