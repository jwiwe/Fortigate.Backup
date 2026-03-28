using Dapper;
using Fortigate.Backup.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SQLite;

namespace Fortigate.Backup.Core
{
    public class SqliteDataAccess
    {
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

        private static string LoadConnectionString()
        {
            var config = ConfigHelper.GetConfig();
            var connectionString = config.GetConnectionString("DefaultConnection");
            return connectionString;
        }
    }
}
