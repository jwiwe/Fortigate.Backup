using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Fortigate.Backup.Core
{
    public static class CryptoService
    {
        private static readonly string secretKeyFromConfig = ValidateKey.EnsureKeyExists();
        private static readonly byte[] Key = Encoding.UTF8.GetBytes($"{secretKeyFromConfig}"); // Skal være 32 bytes

        public static string Encrypt(string plainText)
        {
            using var aes = new AesGcm(Key, 16);

            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ciphertext = new byte[plaintextBytes.Length];

            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Vi samler det hele i én pakke: [12 bytes Nonce][16 bytes Tag][Resten er Ciphertext]
            byte[] combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
            Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

            return Convert.ToBase64String(combined);
        }

        public static string Decrypt(string combinedBase64)
        {
            if (string.IsNullOrWhiteSpace(combinedBase64))
                return "[No data]";
            try
            {
                byte[] combined = Convert.FromBase64String(combinedBase64);
                using var aes = new AesGcm(Key, 16);

                // Pak pakken ud igen
                byte[] nonce = combined[..12];
                byte[] tag = combined[12..28];
                byte[] ciphertext = combined[28..];
                byte[] decryptedBytes = new byte[ciphertext.Length];

                aes.Decrypt(nonce, ciphertext, tag, decryptedBytes);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (FormatException)
            {
                return "[Error: Data is not in correct Base64 format]";
            }
            catch (CryptographicException)
            {
                return "[Error: Unable to decrypt. Incorrect key or corrupt data]";
            }
            catch (Exception ex)
            {
                return $"[Unexpected error: {ex.Message}]";
            }
        }
    }
}
