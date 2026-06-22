using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Gigahoo.Api.Services;

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string magicLink);
    Task SendContactNotificationAsync(string fromName, string fromEmail, string subject, string message);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendMagicLinkAsync(string toEmail, string magicLink)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Sign in to Gigahoo";

        var body = $"""
            <html>
            <body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto;">
                <h2>Sign in to Gigahoo</h2>
                <p>Click the button below to sign in to your account. This link expires in 15 minutes.</p>
                <p><a href="{magicLink}" style="display: inline-block; padding: 12px 24px; background: #2563eb; color: white; text-decoration: none; border-radius: 6px;">Sign In</a></p>
                <p style="color: #6b7280; font-size: 14px;">If you didn't request this, you can safely ignore this email.</p>
            </body>
            </html>
            """;

        message.Body = new TextPart("html") { Text = body };
        await SendAsync(message);
    }

    public async Task SendContactNotificationAsync(string fromName, string fromEmail, string subject, string message)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        email.To.Add(MailboxAddress.Parse("support@gigahoo.com"));
        email.Subject = $"[Contact] {subject}";

        var body = $"""
            <html>
            <body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto;">
                <h2>New Contact Form Submission</h2>
                <p><strong>From:</strong> {fromName} ({fromEmail})</p>
                <p><strong>Subject:</strong> {subject}</p>
                <hr />
                <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
            </body>
            </html>
            """;

        email.Body = new TextPart("html") { Text = body };
        await SendAsync(email);
    }

    private async Task SendAsync(MimeMessage message)
    {
        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                config["Email:SmtpHost"],
                int.Parse(config["Email:SmtpPort"] ?? "587"),
                SecureSocketOptions.StartTls);

            if (config["Email:SmtpUser"] is not null)
                await client.AuthenticateAsync(config["Email:SmtpUser"], config["Email:SmtpPassword"]);

            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {To}", message.To);
        }
    }
}
