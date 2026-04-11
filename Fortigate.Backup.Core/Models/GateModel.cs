namespace Fortigate.Backup.Core.Models
{
    public class GateModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Hostname { get; set; }
        public int? Port { get; set; } = 443;
        public string? Apikey { get; set; }
        public string? ConfVer { get; set; }
        public string? BuildNo { get; set; }
    }
}
