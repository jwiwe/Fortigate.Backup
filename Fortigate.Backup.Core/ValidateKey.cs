namespace Fortigate.Backup.Core
{
    public class ValidateKey
    {
        public static bool EnsureKeyIsValid()
        {
            const string CanaryKey = "ValidationCanary";
            const string ExpectedPlaintext = "KEY_WORKS_OK";

            string? encryptedCanary = SqliteDataAccess.LoadSetting(CanaryKey);

            // Første kørsel: Opret kanariefuglen
            if (encryptedCanary == null)
            {
                Console.WriteLine("No existing key found. Initializing database...");
                string newCanary = CryptoService.Encrypt(ExpectedPlaintext);
                SqliteDataAccess.SaveSetting(CanaryKey, newCanary);
                return true;
            }

            // Validation: Attempt to decrypt
            try
            {
                string decrypted = CryptoService.Decrypt(encryptedCanary);

                if (decrypted == ExpectedPlaintext)
                {
                    return true; // Everything is OK!
                }
            }
            catch
            {
                // If the decryption fails (e.g., due to an incorrect key in OS Vault)
            }

            return false;
        }
    }
}