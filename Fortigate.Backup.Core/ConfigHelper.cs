using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Fortigate.Backup.Core
{
    public class ConfigHelper
    {
        public static IConfiguration GetConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables() // God stil til f.eks. passwords
                .Build();
        }

        public static void SetSetting(string section, string key, string value)
        {
            string filePath = "appsettings.json";

            // 1. Læs den eksisterende fil (eller opret en tom hvis den mangler)
            string json = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";
            var root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();

            // 2. Find eller opret sektionen (f.exe. "UserSettings")
            if (!root.ContainsKey(section))
            {
                root[section] = new JsonObject();
            }

            // 3. Sæt værdien (f.eks. "IP": "KrypteretStreng")
            var sectionObject = root[section]?.AsObject();
            if (sectionObject != null)
            {
                sectionObject[key] = value;
            }

            // 4. Gem filen med "Pretty Printing" så den er læselig
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, root.ToJsonString(options));
        }
    }
}
