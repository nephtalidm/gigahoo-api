using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Gigahoo.Api.Services;

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string magicLink);
    Task SendContactNotificationAsync(string fromName, string fromEmail, string subject, string message);
    Task SendPhoneNumberAssignedAsync(string toEmail, string businessName, string phoneNumber);
    Task SendMinutesExhaustedAsync(string toEmail, string businessName);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendMagicLinkAsync(string toEmail, string magicLink)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your Gigahoo verification code";

        // Extract the 6-digit code from the URL
        var code = "------";
        try
        {
            var uri = new Uri(magicLink);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("code", out var codeValues))
                code = codeValues.FirstOrDefault() ?? "------";
        }
        catch { }

        var body = $$"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"></head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f9fafb;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color: #f9fafb; padding: 40px 0;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background: white; border-radius: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;">
                                <!-- Header -->
                                <tr>
                                    <td style="padding: 32px 40px 0; text-align: center;">
                                        <div style="display: inline-block; width: 48px; height: 48px; line-height: 48px; border-radius: 10px; background-color: #2563eb; text-align: center;">
                                            <span style="color: white; font-size: 24px; font-weight: bold;">G</span>
                                        </div>
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style="padding: 24px 40px 32px;">
                                        <h1 style="margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #111827; text-align: center;">Verify your email</h1>
                                        <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #4b5563; text-align: center;">
                                            Use the code below to verify your email and continue setting up your account.
                                        </p>

                                        <!-- Code Display -->
                                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                            <tr>
                                                <td align="center" style="padding-bottom: 24px;">
                                                    <div style="display: inline-block; background-color: #f3f4f6; border-radius: 8px; padding: 16px 24px;">
                                                        <span style="font-family: 'Courier New', monospace; font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #111827; user-select: all;">{{code}}</span>
                                                    </div>
                                                </td>
                                            </tr>
                                        </table>

                                        <!-- CTA Button -->
                                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                            <tr>
                                                <td align="center" style="padding-bottom: 16px;">
                                                    <a href="{{magicLink}}" style="display: inline-block; padding: 14px 32px; background-color: #2563eb; color: #ffffff; font-size: 16px; font-weight: 600; text-decoration: none; border-radius: 8px;">Verify Email</a>
                                                </td>
                                            </tr>
                                        </table>

                                        <!-- Fallback link -->
                                        <p style="margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #6b7280; text-align: center;">
                                            Or copy and paste this link into your browser:
                                        </p>
                                        <p style="margin: 0 0 24px; padding: 12px; background-color: #f3f4f6; border-radius: 6px; font-size: 12px; color: #374151; word-break: break-all; text-align: center; font-family: monospace;">
                                            {{magicLink}}
                                        </p>

                                        <!-- Expiry notice -->
                                        <p style="margin: 0 0 8px; font-size: 13px; color: #9ca3af; text-align: center;">
                                            This code expires in 15 minutes and can only be used once.
                                        </p>
                                        <p style="margin: 0; font-size: 13px; color: #9ca3af; text-align: center;">
                                            If you didn't request this, you can safely ignore this email.
                                        </p>
                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style="padding: 24px 40px; background-color: #f9fafb; border-top: 1px solid #e5e7eb; text-align: center;">
                                        <p style="margin: 0; font-size: 12px; color: #9ca3af;">
                                            &copy; {DateTime.UtcNow.Year} Gigahoo. All rights reserved.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
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

    public async Task SendPhoneNumberAssignedAsync(string toEmail, string businessName, string phoneNumber)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = $"Your Gigahoo phone number is ready: {phoneNumber}";

        var body = $"""
            <html>
            <body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto;">
                <h2>Your AI Receptionist Phone Number</h2>
                <p>Hi {businessName},</p>
                <p>Your dedicated phone number has been provisioned and is ready to receive calls:</p>
                <div style="background: #f3f4f6; padding: 16px; border-radius: 8px; margin: 16px 0; text-align: center;">
                    <p style="font-size: 24px; font-weight: bold; margin: 0; color: #111827;">{phoneNumber}</p>
                </div>
                <p><strong>Next steps:</strong></p>
                <ol>
                    <li>Forward your existing business calls to this number</li>
                    <li>Test the AI receptionist by calling the number yourself</li>
                    <li>Configure your business details in the dashboard</li>
                </ol>
                <p style="color: #6b7280; font-size: 14px;">Need help? Contact us at support@gigahoo.com</p>
            </body>
            </html>
            """;

        message.Body = new TextPart("html") { Text = body };
        await SendAsync(message);
    }

    public async Task SendMinutesExhaustedAsync(string toEmail, string businessName)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "You've used all your included Gigahoo minutes";

        var upgradeUrl = $"{config["Frontend:Url"] ?? "https://gigahoo.ai"}/dashboard/billing";

        var body = $$"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"></head>
            <body style="margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background-color: #f9fafb;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color: #f9fafb; padding: 40px 0;">
                    <tr>
                        <td align="center">
                            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background: white; border-radius: 12px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); overflow: hidden;">
                                <!-- Header -->
                                <tr>
                                    <td style="padding: 32px 40px 0; text-align: center;">
                                        <div style="display: inline-block; width: 48px; height: 48px; line-height: 48px; border-radius: 10px; background-color: #2563eb; text-align: center;">
                                            <span style="color: white; font-size: 24px; font-weight: bold;">G</span>
                                        </div>
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style="padding: 24px 40px 32px;">
                                        <h1 style="margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #111827; text-align: center;">You're out of minutes</h1>
                                        <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #4b5563; text-align: center;">
                                            Hi {{businessName}}, your AI receptionist has used all the calling minutes included in your current plan for this billing period.
                                            New incoming calls won't be answered until your minutes reset or you upgrade.
                                        </p>

                                        <!-- CTA Button -->
                                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                                            <tr>
                                                <td align="center" style="padding-bottom: 16px;">
                                                    <a href="{{upgradeUrl}}" style="display: inline-block; padding: 14px 32px; background-color: #2563eb; color: #ffffff; font-size: 16px; font-weight: 600; text-decoration: none; border-radius: 8px;">Upgrade your plan</a>
                                                </td>
                                            </tr>
                                        </table>

                                        <p style="margin: 16px 0 0; font-size: 13px; color: #9ca3af; text-align: center;">
                                            Your minutes will automatically reset at the start of your next billing period.
                                        </p>
                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style="padding: 24px 40px; background-color: #f9fafb; border-top: 1px solid #e5e7eb; text-align: center;">
                                        <p style="margin: 0; font-size: 12px; color: #9ca3af;">
                                            &copy; {DateTime.UtcNow.Year} Gigahoo. All rights reserved.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        message.Body = new TextPart("html") { Text = body };
        await SendAsync(message);
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
