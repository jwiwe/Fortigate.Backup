using CommandLine;

namespace Fortigate.Backup.Cli
{
    public class CommandLineOptions
    {
        // Class for 'add' commands
        [Verb("add", HelpText = "Add a new Fortigate to the database.")]
        public class AddOptions
        {
            [Option('n', "name", Required = true)]
            public string Name { get; set; }

            [Option('i', "ip", Required = true)]
            public string IpAddress { get; set; }
            [Option('p', "port", Required = false)]
            public int Port { get; set; } = 443;
            [Option('k', "apikey", Required = true)]
            public string Apikey { get; set; }
        }

        // Class for 'edit' commands
        [Verb("edit", HelpText = "Edit an existing Fortigate in the database")]
        public class EditOptions
        {
            [Option("id", Required = true)]
            public int Id { get; set; }

            [Option('n', "name", Required = false)]
            public string? Name { get; set; }

            [Option('i', "ip", Required = false)]
            public string? IpAddress { get; set; }
            [Option('p', "port", Required = false)]
            public int? Port { get; set; }

            [Option('k', "apikey", Required = false)]
            public string? Apikey { get; set; }
        }

        // Class for 'list' commands
        [Verb("list", HelpText = "List all Fortigates in the database.")]
        public class ListOptions
        {
        }

        // Class for 'delete' commands
        [Verb("delete", HelpText = "Remove a Fortigate from the database.")]
        public class DeleteOptions
        {
            [Option("id", Required = false)]
            public int Id { get; set; }
        }

        // Class for 'backup' commands
        [Verb("backup", HelpText = "Make a backup of a Fortigate or all Fortigates in the database.")]
        public class BackupOptions
        {
            [Option("id", Required = false)]
            public int Id { get; set; }
        }
    }
}
