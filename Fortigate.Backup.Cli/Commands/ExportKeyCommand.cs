using Fortigate.Backup.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using Serilog;

namespace Fortigate.Backup.Cli.Commands
{
    public class ExportKeyCommand : AsyncCommand<ExportKeySettings>
    {
        protected async override Task<int> ExecuteAsync(CommandContext context, ExportKeySettings settings, CancellationToken cancellationToken)
        {
            // if the path is not given as a parameter, ask for it (Spectre style)
            string path = settings.Path ?? AnsiConsole.Prompt(new TextPrompt<string>("File path:"));
            string password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());

            CryptoService.ExportKey(path, password);
            AnsiConsole.MarkupLine($"Key exported to: [blue]{path}[/]");
            Log.Warning("Exported key to {Path}", path);
            return 0;
        }
    }

    public class ExportKeySettings : CommandSettings
    {
        [CommandOption("-p|--path")]
        public string Path { get; set; }
    }
}
