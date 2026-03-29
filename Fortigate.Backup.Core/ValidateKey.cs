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
                Console.WriteLine("Ingen eksisterende nøgle fundet. Initialiserer database...");
                string newCanary = CryptoService.Encrypt(ExpectedPlaintext);
                SqliteDataAccess.SaveSetting(CanaryKey, newCanary);
                return true;
            }

            // Validering: Forsøg at dekryptere
            try
            {
                string decrypted = CryptoService.Decrypt(encryptedCanary);

                if (decrypted == ExpectedPlaintext)
                {
                    return true; // Alt er OK!
                }
            }
            catch
            {
                // Hvis dekryptering fejler (f.eks. pga. forkert nøgle i miljøvariabler)
            }

            return false;
        }

        public static bool EnsureKeyExists()
        {
            string? key = GetSecretKey();

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            return true;
        }

        public static string GetSecretKey()
        {
            const string KeyName = "Fortigate_Backup__SecretKey";

            // 1. Tjek først den nuværende proces (hvis vi lige har sat den i denne kørsel)
            string? key = Environment.GetEnvironmentVariable(KeyName, EnvironmentVariableTarget.Process);

            // 2. Hvis den er tom, så tving programmet til at læse direkte fra Windows Registry (User)
            if (string.IsNullOrEmpty(key))
            {
                key = Environment.GetEnvironmentVariable(KeyName, EnvironmentVariableTarget.User);
            }

            return key;
        }
    }
}
