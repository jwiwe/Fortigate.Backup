using Spectre.Console.Cli;
using System.ComponentModel;

namespace Fortigate.Backup.Cli.Commands
{
    public class BackupCommand : AsyncCommand<BackupSettings>
    {
        protected async override Task<int> ExecuteAsync(CommandContext context, BackupSettings settings, CancellationToken cancellationToken)
        {
            int id = settings.Id ?? 0;
            if (id > 0)
            {
                await Logic.HandleBackupCommand(id, true);
                return 0;
            }
            await Logic.HandleBackupAllCommand(settings.Force, true);
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
