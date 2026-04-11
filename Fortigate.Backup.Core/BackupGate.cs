using Fortigate.Backup.Core.Models;

namespace Fortigate.Backup.Core
{
    public class BackupGate
    {
        public static async Task<string?> Backup(GateModel gate)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };

            using (var http = new HttpClient(handler))
            {
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {CryptoService.Decrypt(gate.Apikey ?? string.Empty)}");

                try
                {
                    var response = await http.GetAsync($"https://{gate.IpAddress}:{gate.Port}/api/v2/monitor/system/config/backup?scope=global");

                    if (response != null && response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return content;
                    }
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
            return null;
        }
    }
}
