using CommandLine;
using Fortigate.Backup.Core;
using Fortigate.Backup.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using static Fortigate.Backup.Cli.CommandLineOptions;

namespace Fortigate.Backup.Cli
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            IConfiguration configuration = ConfigHelper.GetConfig();
            if (configuration.GetSection("Fortigate_Backup")["SecretKey"] is null)
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#%&/()=?";
                var random = new Random();
                string key = new string(Enumerable.Repeat(chars, 32)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
                ConfigHelper.SetSetting("Fortigate_Backup", "SecretKey", key);
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
                        Console.WriteLine($"Unable to backup the device with ID {item.Id}");
                        continue;
                    }
                    File.WriteAllText($"Backups\\{item.Name}-{DateTime.Now.ToString("dd-MM-yyyy")}.txt", createText);
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
                Console.WriteLine($"Unable to backup the device with ID {gate.Id}");
                return 1;
            }
            File.WriteAllText($"Backups\\{gate.Name}-{DateTime.Now.ToString("dd-MM-yyyy")}.txt", createText);
            return 0;
        }
    }
}
