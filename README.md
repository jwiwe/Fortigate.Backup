# Fortigate Backup Tool

This simple app makes it easy to perform backups of several Fortigate firewalls. It reads a list of Fortigates from a SQLite database file, performs a backup of each one and saves the backup file in a local directory.

It securely stores your device details (including API keys) using local encryption, and allows you to easily perform on-demand or scheduled backups in an interactive CLI.

## Features

- **Interactive CLI:** Built with Spectre.Console for a beautiful, easy-to-use terminal interface.
- **Secure Storage:** Device API keys are securely encrypted before being stored in a local SQLite database.
- **Key Management:** Export and import encryption keys to safely move your configuration across systems or recover them.
- **Manage Devices:** Add, list, edit, and delete multiple FortiGate devices from the internal database.
- **Bulk or Single Backups:** Create configuration backups for all registered devices at once, or target specific firewalls.
- **Email Notifications:** Optionally send an email report containing the results of the backup operation via SMTP.

## Requirements
- [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or newer
- A Fortigate REST API key with the `super_admin` access profile (required to download the full configuration backup).

## Usage

When you run the application with no arguments, it starts in an interactive mode allowing you to navigate through a menu to manage your devices.

### Commands

* `backup` - Perform a backup of all FortiGates or a single FortiGate.
* `cleanup` - Clean up the backup directories, keeping only a specific amount of files or days. Options: `--keep-count X`, `--keep-days X`
* `export-key` - Export the local encryption key from the system. Securely store this to prevent data loss.
* `import-key` - Import a previously exported encryption key to the system.

### Interactive Menu Options

* **Add a new Fortigate:** Prompts you for the Device Name, IP Address, Port, and REST API Key.
* **List all Fortigates:** Displays a table showing the ID, Name, IP Address, and Port of all registered devices.
* **Edit Fortigate:** Select an existing setup to quickly change its IP, port, or update its API key.
* **Delete Fortigate:** Remove a device from your configuration.

## Setup & Security

The application generates an encryption key upon first execution and stores it securely. This key ensures that your API keys saved in the local SQLite database cannot be accessed in plaintext.

> **Important**: If you move the application to another machine, or reinstall your OS, you will lose access to the encrypted data unless you export your encryption key and import it on the new system using the `export-key` and `import-key` commands respectively.

### Email Notifications

Configure email notifications by modifying the `appsettings.json` file. Ensure `EnableEmailNotifications` is set to `true`:

```json
{
  "EmailSettings": {
    "EnableEmailNotifications": false,
    "SmtpServer": "domain.com",
    "Port": 587,
    "Encryption": "Auto",
    "SenderName": "Fortigate Backup",
    "SenderEmail": "backup@domain.com",
    "ReceiverEmail": "admin@domain.com",
    "Username": "backup@domain.com",
    "Password": "YourPasswordHere"
  }
}
```