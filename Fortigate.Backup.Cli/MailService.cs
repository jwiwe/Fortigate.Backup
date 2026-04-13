using Fortigate.Backup.Cli.Models;
using Fortigate.Backup.Core;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Serilog;
using System.Text;

namespace Fortigate.Backup.Cli
{
    public static class MailService
    {
        public static async Task SendReportEmail(List<BackupResult> results)
        {
            var config = ConfigHelper.GetConfig();
            var emailConfig = config.GetSection("EmailSettings");

            if (emailConfig.GetValue<bool>("EnableEmailNotifications"))
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(emailConfig["SenderName"], emailConfig["SenderEmail"]));

                var receivers = emailConfig.GetSection("Receivers").Get<List<string>>();

                if (receivers != null && receivers.Any())
                {
                    foreach (var email in receivers)
                    {
                        message.To.Add(new MailboxAddress("", email.Trim()));
                    }
                }
                else
                {
                    // Fallback if the array is empty (optional)
                    Log.Warning("No recipients found in the configuration.");
                    return;
                }

                message.Subject = $"Fortigate Backup Report - {DateTime.Now:dd/MM/yyyy}";

                var bodyBuilder = new BodyBuilder();

                // Build HTML email body
                var sb = new StringBuilder();
                sb.Append("<h2 style='font-family: Arial;'>Status from Fortigate Backup</h2>");
                sb.Append("<table border='1' cellpadding='8' style='border-collapse: collapse; font-family: Arial; width: 100%;'>");
                sb.Append("<tr style='background-color: #333; color: white;'><th>Name</th><th>Hostname</th><th>Status</th><th>Message</th></tr>");

                var sortedResults = results.OrderBy(r => r.Success).ThenBy(r => r.Name).ToList();

                foreach (var res in sortedResults)
                {
                    string bgColor = res.Success ? "#d4edda" : "#f8d7da";
                    string statusText = res.Success ? "SUCCESS" : "FAILED";

                    sb.Append($"<tr style='background-color: {bgColor};'>");
                    sb.Append($"<td><b>{res.Name}</b></td>");
                    sb.Append($"<td>{res.Hostname}</td>");
                    sb.Append($"<td>{statusText}</td>");
                    sb.Append($"<td>{res.Message}</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
                sb.Append("<p style='color: grey; font-size: 12px;'>Sent automatically by Fortigate Backup Tool</p>");

                bodyBuilder.HtmlBody = sb.ToString();
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                try
                {
                    var options = GetSecureSocketOptions(config["Encryption"]);

                    // Connect to the server
                    await client.ConnectAsync(
                        emailConfig["SmtpServer"],
                        int.Parse(emailConfig["Port"] ?? "587"),
                        options);

                    // Log in
                    await client.AuthenticateAsync(emailConfig["Username"], emailConfig["Password"]);

                    // Send
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }
                catch (Exception ex)
                {
                    // Log the error with Serilog if the email fails
                    Serilog.Log.Error(ex, "Could not send backup report via email");
                }
            }
        }

        private static SecureSocketOptions GetSecureSocketOptions(string? encryption)
        {
            return encryption?.ToLower() switch
            {
                "ssl" => SecureSocketOptions.SslOnConnect,
                "tls" => SecureSocketOptions.StartTls,
                "starttls" => SecureSocketOptions.StartTls,
                "none" => SecureSocketOptions.None,
                _ => SecureSocketOptions.Auto // Standard hvis feltet er tomt eller forkert udfyldt
            };
        }
    }
}
