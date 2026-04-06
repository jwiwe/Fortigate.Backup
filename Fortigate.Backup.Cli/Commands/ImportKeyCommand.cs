using Fortigate.Backup.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fortigate.Backup.Cli.Commands
{
    public class ImportKeyCommand : AsyncCommand<ImportKeySettings>
    {
        protected async override Task<int> ExecuteAsync(CommandContext context, ImportKeySettings settings, CancellationToken cancellationToken)
        {
            // if the path is not given as a parameter, ask for it (Spectre style)
            string path = settings.Path ?? AnsiConsole.Prompt(new TextPrompt<string>("File path:"));
            string password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());

            CryptoService.ImportKey(path, password);
            AnsiConsole.MarkupLine($"Key imported from: [blue]{path}[/]");
            return 0;
        }
    }

    public class ImportKeySettings : CommandSettings
    {
        [CommandOption("-p|--path")]
        public string Path { get; set; }
    }
}