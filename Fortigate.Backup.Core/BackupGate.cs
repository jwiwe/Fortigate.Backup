using Fortigate.Backup.Core.Models;

namespace Fortigate.Backup.Core
{
    public class BackupGate
    {
        public static async Task<string> Backup(GateModel gate)
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {CryptoService.Decrypt(gate.Apikey)}");
            var response = http.GetAsync($"https://{gate.IpAddress}/api/v2/monitor/system/config/backup?scope=global").Result;
            if (response != null && response.IsSuccessStatusCode)
            {
                var content = response.Content.ReadAsStringAsync().Result;
                // Process the backup content as needed
                return content;
            }
            return null;
        }
    }
}
