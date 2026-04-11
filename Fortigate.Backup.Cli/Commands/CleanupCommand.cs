using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace Fortigate.Backup.Cli.Commands
{
    public class CleanupCommand : AsyncCommand<CleanupSettings>
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, CleanupSettings settings, CancellationToken cancellationToken)
        {
            if (settings.KeepCount == null && settings.KeepDays == null)
            {
                AnsiConsole.MarkupLine("[red]You must specify either --keep-count or --keep-days.[/]");
                return -1;
            }

            if (settings.KeepCount != null && settings.KeepDays != null)
            {
                AnsiConsole.MarkupLine("[red]You can only specify one of --keep-count or --keep-days.[/]");
                return -1;
            }

            return await Logic.HandleCleanupCommand(settings);
        }
    }

    public class CleanupSettings : CommandSettings
    {
        [CommandOption("-c|--keep-count")]
        [Description("Keep the newest X backup files per device.")]
        public int? KeepCount { get; set; }

        [CommandOption("-d|--keep-days")]
        [Description("Keep backup files from the last X days per device.")]
        public int? KeepDays { get; set; }
    }
}
