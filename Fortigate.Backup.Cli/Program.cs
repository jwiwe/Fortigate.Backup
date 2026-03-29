using CommandLine;
using Fortigate.Backup.Core;
using Fortigate.Backup.Core.Models;
using Microsoft.Extensions.Configuration;
using static Fortigate.Backup.Cli.CommandLineOptions;

namespace Fortigate.Backup.Cli
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            SqliteDataAccess.InitializeDatabase();
            IConfiguration configuration = ConfigHelper.GetConfig();
            ValidateKey.EnsureKeyExists();
            if (!ValidateKey.EnsureKeyIsValid())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("!!! SECURITY ERROR !!!");
                Console.WriteLine("Your encryption key (environment variable) does not match the database.");
                Console.WriteLine("The program is being terminated to protect your data.");
                Console.ResetColor();
                return; // Stop programmet
            }
            await Parser.Default.ParseArguments<AddOptions, ListOptions, EditOptions, DeleteOptions, BackupOptions>(args)
                .MapResult(
                        (AddOptions opts) => HandleAddCommand(opts),
                        (ListOptions opts) => HandleListCommand(opts),
                        (EditOptions opts) => HandleEditCommand(opts),
                        (DeleteOptions opts) => HandleDeleteCommand(opts),
                        async (BackupOptions opts) => await HandleBackupCommand(opts),
                        errs => Task.FromResult(1)
                    );
        }

        private static Task<int> HandleAddCommand(AddOptions opts)
        {
            // Implement your logic for the 'add' command here
            var gate = new GateModel
            {
                Name = opts.Name,
                IpAddress = opts.IpAddress,
                Apikey = CryptoService.Encrypt(opts.Apikey)
            };

            SqliteDataAccess.SaveGate(gate);

            Console.WriteLine($"Adding device: {opts.Name} with IP: {opts.IpAddress}");
            return Task.FromResult(0);
        }

        private static Task<int> HandleListCommand(ListOptions opts)
        {
            // Implement your logic for the 'list' command here
            Console.WriteLine("All Fortigates:");
            var gates = SqliteDataAccess.LoadGates();
            foreach (var gate in gates)
            {
                Console.WriteLine($"{gate.Id}\t{gate.Name}\t{gate.IpAddress}");
            }
            return Task.FromResult(0);
        }

        private static Task<int> HandleEditCommand(EditOptions opts)
        {
            // Implement your logic for the 'list' command here
            var gate = SqliteDataAccess.LoadGateById(opts.Id);
            if (gate == null)
            {
                Console.WriteLine($"Device with ID {opts.Id} not found.");
                return Task.FromResult(1);
            }
            if (opts.Name != null) gate.Name = opts.Name;
            if (opts.IpAddress != null) gate.IpAddress = opts.IpAddress;
            if (opts.Apikey != null) gate.Apikey = CryptoService.Encrypt(opts.Apikey);
            SqliteDataAccess.UpdateGate(gate);

            return Task.FromResult(0);
        }

        private static Task<int> HandleDeleteCommand(DeleteOptions opts)
        {
            // Implement your logic for the 'delete' command here
            var gate = SqliteDataAccess.LoadGateById(opts.Id);
            if (gate == null)
            {
                Console.WriteLine($"Device with ID {opts.Id} not found.");
                return Task.FromResult(1);
            }
            SqliteDataAccess.DeleteGate(gate.Id);
            return Task.FromResult(0);
        }

        private static async Task<int> HandleBackupCommand(BackupOptions opts)
        {
            var createText = string.Empty;
            if (opts.Id <= 0)
            {
                var gates = SqliteDataAccess.LoadGates();
                foreach (var item in gates)
                {
                    createText = await BackupGate.Backup(item);
                    if (createText == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Unable to backup the device with ID {item.Id}");
                        Console.ResetColor();
                        continue;
                    }
                    File.WriteAllText($"Backups\\{item.Name}_{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.txt", createText);
                    Console.WriteLine($"Backuped device with ID: {item.Id}");
                }
                return 0;
            }
            var gate = SqliteDataAccess.LoadGateById(opts.Id);
            if (gate == null)
            {
                Console.WriteLine($"Device with ID {opts.Id} not found.");
                return 1;
            }
            createText = await BackupGate.Backup(gate);
            if (createText == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unable to backup the device with ID {gate.Id}");
                Console.ResetColor();
                return 1;
            }
            File.WriteAllText($"Backups\\{gate.Name}_{DateTime.Now.ToString("dd-MM-yyyy_HHmm")}.txt", createText);
            return 0;
        }
    }
}
