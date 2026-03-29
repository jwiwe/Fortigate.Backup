using Dapper;
using Fortigate.Backup.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SQLite;

namespace Fortigate.Backup.Core
{
    public class SqliteDataAccess
    {
        public static void InitializeDatabase()
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                cnn.ExecuteAsync("CREATE TABLE IF NOT EXISTS systemSettings (key TEXT PRIMARY KEY, value TEXT NOT NULL);");
                cnn.ExecuteAsync("CREATE TABLE IF NOT EXISTS gates (id INTEGER NOT NULL UNIQUE, name TEXT NOT NULL, ipAddress TEXT NOT NULL, apikey TEXT NOT NULL, PRIMARY KEY( id AUTOINCREMENT));");
            }
        }

        public static List<GateModel> LoadGates()
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                var output = cnn.Query<GateModel>("SELECT id, name, ipAddress, apikey FROM gates", new DynamicParameters());
                return output.ToList();
            }
        }

        public static GateModel LoadGateById(int id)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                var output = cnn.QuerySingleOrDefault<GateModel>("SELECT id, name, ipAddress, apikey FROM gates WHERE id = @Id", new { Id = id });
                return output;
            }
        }

        public static void SaveGate(GateModel gate)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                cnn.Execute("INSERT INTO gates (name, ipAddress, apikey) VALUES (@Name, @IpAddress, @Apikey)", gate);
            }
        }

        public static void UpdateGate(GateModel gate)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                cnn.Execute("UPDATE gates SET name = @Name, ipAddress = @IpAddress, apikey = @Apikey WHERE id = @Id", gate);
            }
        }

        public static void DeleteGate(int id)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                cnn.Execute("DELETE FROM gates WHERE id = @Id", new { Id = id });
            }
        }

        public static string? LoadSetting(string key)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                return cnn.QueryFirstOrDefault<string>(
                    "SELECT value FROM systemSettings WHERE key = @Key",
                    new { Key = key });
            }
        }

        public static void SaveSetting(string key, string value)
        {
            var connectionString = LoadConnectionString();
            using (IDbConnection cnn = new SQLiteConnection(connectionString))
            {
                cnn.Execute(@"
                    INSERT INTO systemSettings (key, value) 
                    VALUES (@Key, @Value) 
                    ON CONFLICT(Key) DO UPDATE SET value = @Value",
                    new { Key = key, Value = value });
            }
        }

        private static string LoadConnectionString()
        {
            var config = ConfigHelper.GetConfig();
            var connectionString = config.GetConnectionString("DefaultConnection");
            return connectionString;
        }
    }
}
