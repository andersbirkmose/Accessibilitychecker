using AccessibilityChecker.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AccessibilityChecker.Services;

public class EmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    /// <summary>
    /// Sender en e-mail med én eller flere filer vedhæftet.
    /// </summary>
    public async Task SendReportAsync(IEnumerable<string> filePaths, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("WCAG Checker", _settings.From));
        message.To.Add(MailboxAddress.Parse(_settings.To));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            TextBody = body
        };

        foreach (var path in filePaths)
        {
            if (File.Exists(path))
            {
                builder.Attachments.Add(path);
            }
            else
            {
                Console.WriteLine($"⚠️ Filen '{path}' blev ikke fundet – den bliver ikke vedhæftet.");
            }
        }

        message.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpServer, _settings.Port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine("📧 E-mail sendt med vedhæftede rapporter.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Kunne ikke sende e-mail: {ex.Message}");
        }
    }
}
