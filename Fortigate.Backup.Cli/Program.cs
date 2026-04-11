using Fortigate.Backup.Cli.Commands;
using Fortigate.Backup.Core;
using Microsoft.Extensions.Configuration;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fortigate.Backup.Cli
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.Clear();

            IConfiguration configuration = ConfigHelper.GetConfig();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .CreateLogger();
            try
            {
                Log.Information("Starting application...");

                SqliteDataAccess.InitializeDatabase();

                bool isImporting = args.Length > 0 && args[0].Equals("import-key", StringComparison.OrdinalIgnoreCase);

                if (!isImporting && !ValidateKey.EnsureKeyIsValid())
                {
                    AnsiConsole.MarkupLine("[red bold]!!! SECURITY ERROR !!![/]");
                    AnsiConsole.MarkupLine("[red]The encryption key does not match the database.[/]");
                    Log.Error("Encryption key validation failed. The key does not match the database.");
                    return -1;
                }

                var app = new CommandApp<InteractiveCommand>();

                app.Configure(config =>
                {
                    config.SetApplicationName("Fortigate Backup CLI");
                    config.AddCommand<ExportKeyCommand>("export-key").WithDescription("Export the encryption key from system.");
                    config.AddCommand<ImportKeyCommand>("import-key").WithDescription("Import an encryption key to system.");
                    config.AddCommand<BackupCommand>("backup").WithDescription("Perform a backup of all Fortigates or a single Fortigate.");
                });

                return await app.RunAsync(args);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "An unhandled exception occurred.");
                return -1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
