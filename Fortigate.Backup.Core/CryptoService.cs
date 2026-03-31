using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;

namespace Fortigate.Backup.Core
{
    public static class CryptoService
    {
        private static byte[]? _cachedKey;

        private static byte[] GetKey()
        {
            // Cache key in memory during runtime, so we don't have to read from disk all the time
            if (_cachedKey != null) return _cachedKey;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Load the key from the Windows DPAPI vault (or create it if it doesn't exist)
                _cachedKey = GetWindowsKey();
            }
            else
            {
                // Load the key from the Linux file system (or create it if it doesn't exist)
                _cachedKey = GetLinuxKey();
            }

            return _cachedKey;
        }

        private static byte[] GetWindowsKey()
        {
            string path = GetKeyPath();

            if (!File.Exists(path))
            {
                byte[] rawKey = RandomNumberGenerator.GetBytes(32);
                // DPAPI: Encrypt the key specifically for the current Windows user 
                byte[] encrypted = ProtectedData.Protect(rawKey, null, DataProtectionScope.CurrentUser);

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, encrypted);
                return rawKey;
            }

            byte[] encryptedData = File.ReadAllBytes(path);
            return ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
        }

        private static byte[] GetLinuxKey()
        {
            string path = GetKeyPath();

            if (!File.Exists(path))
            {
                byte[] rawKey = RandomNumberGenerator.GetBytes(32);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, rawKey);

                // Set permissions to 600 (only owner can read/write) via shell
                try
                {
                    System.Diagnostics.Process.Start("chmod", $"600 \"{path}\"");
                }
                catch { }

                return rawKey;
            }

            return File.ReadAllBytes(path);
        }

        private static string GetKeyPath()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "FortigateBackup", "master.bin");
        }

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";

            byte[] key = GetKey();
            using var aes = new AesGcm(key, 16);

            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];

            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            byte[] combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(combined);
        }

        public static string Decrypt(string combinedBase64)
        {
            if (string.IsNullOrWhiteSpace(combinedBase64)) return "";

            try
            {
                byte[] combined = Convert.FromBase64String(combinedBase64);
                byte[] key = GetKey();
                using var aes = new AesGcm(key, 16);

                byte[] nonce = combined[..12];
                byte[] tag = combined[12..28];
                byte[] ciphertext = combined[28..];
                byte[] decryptedBytes = new byte[ciphertext.Length];

                aes.Decrypt(nonce, ciphertext, tag, decryptedBytes);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return "[Decryption error - Check access rights]";
            }
        }

        public static void ExportKey(string destinationPath, string password)
        {
            byte[] keyToExport = GetKey(); // Load the current "locked" key

            // Make a key from the password (PBKDF2)
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] encryptionKey = deriveBytes.GetBytes(32);

            // Encrypt the master key using AES-GCM with the derived key and a random nonce
            using var aes = new AesGcm(encryptionKey, 16);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[keyToExport.Length];

            aes.Encrypt(nonce, keyToExport, ciphertext, tag);

            // Build backup file: [Salt][Nonce][Tag][Ciphertext]
            byte[] backup = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(salt, 0, backup, 0, 16);
            Buffer.BlockCopy(nonce, 0, backup, 16, 12);
            Buffer.BlockCopy(tag, 0, backup, 28, 16);
            Buffer.BlockCopy(ciphertext, 0, backup, 44, ciphertext.Length);

            File.WriteAllBytes(destinationPath, backup);
        }

        public static void ImportKey(string sourcePath, string password)
        {
            byte[] backup = File.ReadAllBytes(sourcePath);

            // Unoack the backup
            byte[] salt = backup[..16];
            byte[] nonce = backup[16..28];
            byte[] tag = backup[28..44];
            byte[] ciphertext = backup[44..];


            // restore the encryption key from the password using PBKDF2
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            byte[] encryptionKey = deriveBytes.GetBytes(32);

            // Dectyrpt the master key using AES-GCM with the derived key and the nonce from the backup
            using var aes = new AesGcm(encryptionKey, 16);
            byte[] decryptedKey = new byte[ciphertext.Length];
            aes.Decrypt(nonce, ciphertext, tag, decryptedKey);


            // Save the decrypted key in the new computer's OS vault
            _cachedKey = decryptedKey;
            SaveKeyToOSVault(decryptedKey);
        }

        private static void SaveKeyToOSVault(byte[] rawKey)
        {
            string path = GetKeyPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                byte[] encrypted = ProtectedData.Protect(rawKey, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(path, encrypted);
            }
            else
            {
                File.WriteAllBytes(path, rawKey);
                try { System.Diagnostics.Process.Start("chmod", $"600 \"{path}\""); } catch { }
            }
        }
    }
}