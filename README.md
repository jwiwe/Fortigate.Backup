# Fortigate Backup Tool

Fortigate Backup Tool is a .NET CLI application for storing FortiGate devices locally and creating configuration backups on demand or as part of a scheduled task. Device API keys are encrypted before they are written to the local SQLite database.

## Features

- **Interactive CLI:** Manage devices and run backups through a Spectre.Console menu.
- **Command mode:** Run backups, cleanup, and key import/export directly from the command line.
- **Secure local storage:** API keys are encrypted before being stored in SQLite.
- **Key migration:** Export and import the encryption key when moving to another machine.
- **Bulk or single-device backups:** Back up all devices or target a single device by ID.
- **Optional email reports:** Send a summary email after backup runs.

## Requirements

- [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or newer
- A FortiGate REST API key with the `super_admin` access profile

## Quick start

Build the solution:

```powershell
dotnet build .\Fortigate.Backup.slnx
```

Start the interactive menu:

```powershell
dotnet run --project .\Fortigate.Backup.Cli
```

Run a backup from the command line:

```powershell
dotnet run --project .\Fortigate.Backup.Cli -- backup
```

## How the project is organized

| Project | Purpose |
|---|---|
| `Fortigate.Backup.Cli` | Interactive menu, CLI commands, backup orchestration, cleanup, and email reporting |
| `Fortigate.Backup.Core` | SQLite access, configuration loading, encryption/key handling, and the FortiGate API call |

## First run

On first startup the application:

1. Creates the SQLite database if it does not already exist
2. Generates a local encryption key if one does not already exist
3. Stores a validation value so future runs can verify that the key still matches the encrypted data

If the key no longer matches the database, the application stops with a security error until the correct key is imported.

## Usage

Running the application without arguments opens the interactive menu. This is the easiest way to add, edit, delete, and back up devices manually.

```powershell
dotnet run --project .\Fortigate.Backup.Cli
```

### Interactive menu options

- **List all Fortigates**
- **Add a new Fortigate**
- **Edit an existing Fortigate**
- **Delete a Fortigate**
- **Backup a Fortigate configuration**
- **Backup all Fortigates in the database**
- **Exit**

### Commands

#### `backup`

Back up all devices, or a single device by ID.

```powershell
dotnet run --project .\Fortigate.Backup.Cli -- backup
dotnet run --project .\Fortigate.Backup.Cli -- backup --id 2
dotnet run --project .\Fortigate.Backup.Cli -- backup --force
```

Options:

- `--id`, `-i`: Back up one device by database ID
- `--force`, `-f`: Save a backup even if no configuration change is detected

#### `cleanup`

Delete old backup files by count or age.

```powershell
dotnet run --project .\Fortigate.Backup.Cli -- cleanup --keep-count 10
dotnet run --project .\Fortigate.Backup.Cli -- cleanup --keep-days 30
```

Options:

- `--keep-count`, `-c`: Keep the newest `X` backup files per device
- `--keep-days`, `-d`: Keep files from the last `X` days per device

Only one cleanup option can be used at a time.

#### `export-key`

Export the local encryption key to a file protected with a password.

```powershell
dotnet run --project .\Fortigate.Backup.Cli -- export-key --path .\backup-key.bin
```

Option:

- `--path`, `-p`: Destination file path. If omitted, the app prompts for it.

#### `import-key`

Import a previously exported encryption key.

```powershell
dotnet run --project .\Fortigate.Backup.Cli -- import-key --path .\backup-key.bin
```

Option:

- `--path`, `-p`: Source file path. If omitted, the app prompts for it.

## Backup behavior

Each backup uses the configured FortiGate hostname/IP address, port, and decrypted API key to request the configuration from:

```text
https://<hostname>:<port>/api/v2/monitor/system/config/backup?scope=global
```

Backups are written to:

```text
Backups\<DeviceName>\<dd-MM-yyyy_HHmm>.conf
```

The application stores the last seen `confVer` and `buildNo` for each device. If the downloaded config reports the same values as the last successful run, the backup is skipped unless `--force` is used.

## Data and file locations

- **Database:** `Fortigate.db`
- **Application config:** `Fortigate.Backup.Core\appsettings.json`
- **Backup files:** `Backups\<DeviceName>\`
- **Logs:** `logs\backup-log-*.txt`

## Email notifications

Backup commands can send a report email when email notifications are enabled in `appsettings.json`.

```json
{
  "EmailSettings": {
    "EnableEmailNotifications": true,
    "SmtpServer": "domain.com",
    "Port": 587,
    "Encryption": "Auto",
    "SenderName": "Fortigate Backup",
    "SenderEmail": "backup@domain.com",
    "Receivers": [
      "admin1@domain.com",
      "admin2@domain.com"
    ],
    "Username": "backup@domain.com",
    "Password": "YourPasswordHere"
  }
}
```

`Encryption` supports `Auto`, `Ssl`, `Tls`, `StartTls`, and `None`.

## Key management and migration

The encryption key is created automatically and stored locally on the machine. If you move the database to another computer without also moving the key, existing API keys in the database cannot be decrypted.

Recommended migration flow:

1. Run `export-key` on the current machine and save the file securely
2. Copy `Fortigate.db` to the new machine
3. Run `import-key` on the new machine using the exported key file
4. Start the application and verify that existing devices can be read normally

## Security notes

- API keys are encrypted before being stored in SQLite
- On Windows, the master key is protected per user with DPAPI
- On Linux, the master key is stored in the user's application data folder
- The current HTTP backup implementation accepts any TLS certificate presented by the FortiGate device. This is convenient for self-signed certificates, but it also means certificate trust is not being validated
