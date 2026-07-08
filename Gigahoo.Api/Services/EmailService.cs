using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Gigahoo.Api.Services;

public enum VerificationPurpose { SignIn, SignUp, EmailChange }

public interface IEmailService
{
    Task SendMagicLinkAsync(string toEmail, string magicLink, VerificationPurpose purpose = VerificationPurpose.SignIn);
    Task SendEmailChangeCodeAsync(string toEmail, string code);
    Task SendContactNotificationAsync(string fromName, string fromEmail, string subject, string message);
    Task SendPhoneNumberAssignedAsync(string toEmail, string businessName, string phoneNumber);
    Task SendMinutesExhaustedAsync(string toEmail, string businessName);
    Task SendCallSummaryAsync(string toEmail, string businessName, string? callerName, string callerPhone, int durationSeconds, string? summary);
    Task SendAdminAlertAsync(string subject, string message);
}

public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendMagicLinkAsync(string toEmail, string magicLink, VerificationPurpose purpose = VerificationPurpose.SignIn)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        // Copy differs by purpose so sign-up and sign-in emails don't read the same.
        var (subject, heading, intro, buttonLabel) = purpose switch
        {
            VerificationPurpose.SignUp => (
                "Welcome to Gigahoo — confirm your email", "Confirm your email",
                "Welcome to Gigahoo! Enter the code below to confirm your email and finish setting up your account.", "Confirm email"),
            _ => (
                "Sign in to Gigahoo", "Sign in to Gigahoo",
                "Enter the code below to sign in to your Gigahoo account.", "Sign in"),
        };
        message.Subject = subject;

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
                                        <img src="https://gigahoo.ai/gigahoo-logo.png" alt="Gigahoo" width="180" style="height: auto; max-width: 180px;" />
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style="padding: 24px 40px 32px;">
                                        <h1 style="margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #111827; text-align: center;">{{heading}}</h1>
                                        <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #4b5563; text-align: center;">
                                            {{intro}}
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
                                                    <a href="{{magicLink}}" style="display: inline-block; padding: 14px 32px; background-color: #2563eb; color: #ffffff; font-size: 16px; font-weight: 600; text-decoration: none; border-radius: 8px;">{{buttonLabel}}</a>
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

    public async Task SendEmailChangeCodeAsync(string toEmail, string code)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Confirm your new Gigahoo email address";

        var body = $$"""
            <!DOCTYPE html>
            <html>
            <head><meta charset="utf-8"></head>
            <body style="margin:0;padding:0;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background-color:#f9fafb;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f9fafb;padding:40px 0;">
                    <tr><td align="center">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background:white;border-radius:12px;box-shadow:0 1px 3px rgba(0,0,0,0.1);overflow:hidden;">
                            <tr><td style="padding:32px 40px 0;text-align:center;">
                                <img src="https://gigahoo.ai/gigahoo-logo.png" alt="Gigahoo" width="180" style="height:auto;max-width:180px;" />
                            </td></tr>
                            <tr><td style="padding:24px 40px 32px;">
                                <h1 style="margin:0 0 8px;font-size:24px;font-weight:700;color:#111827;text-align:center;">Confirm your new email</h1>
                                <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#4b5563;text-align:center;">
                                    Enter the code below in Settings to confirm this as the new email address for your Gigahoo account. If you didn't request this change, you can safely ignore this email.
                                </p>
                                <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr>
                                    <td align="center" style="padding-bottom:24px;">
                                        <div style="display:inline-block;background-color:#f3f4f6;border-radius:8px;padding:16px 24px;">
                                            <span style="font-family:'Courier New',monospace;font-size:32px;font-weight:700;letter-spacing:8px;color:#111827;user-select:all;">{{code}}</span>
                                        </div>
                                    </td>
                                </tr></table>
                                <p style="margin:0;font-size:13px;color:#9ca3af;text-align:center;">This code expires in 15 minutes and can only be used once.</p>
                            </td></tr>
                        </table>
                    </td></tr>
                </table>
            </body>
            </html>
            """;

        message.Body = new TextPart("html") { Text = body };
        await SendAsync(message);
    }

    public async Task SendAdminAlertAsync(string subject, string message)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        email.To.Add(MailboxAddress.Parse("admin@gigahoo.ai"));
        email.Subject = $"[Gigahoo Alert] {subject}";
        email.Body = new TextPart("plain") { Text = message };
        await SendAsync(email);
    }

    public async Task SendContactNotificationAsync(string fromName, string fromEmail, string subject, string message)
    {
        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        email.To.Add(MailboxAddress.Parse("contact@gigahoo.ai"));
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
        message.Subject = $"Welcome to Gigahoo! Your phone number is: {phoneNumber}";

        var body = $"""
            <html>
            <body style="font-family: -apple-system, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px 16px;">
                <div style="text-align: center; padding-bottom: 16px;">
                    <img src="https://gigahoo.ai/gigahoo-logo.png" alt="Gigahoo" width="405" style="height: auto; max-width: 405px;" />
                </div>
                <h2>Welcome to Gigahoo!</h2>
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
                <p style="color: #6b7280; font-size: 14px;">Need help? Contact us at contact@gigahoo.ai</p>
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
                                        <img src="https://gigahoo.ai/gigahoo-logo.png" alt="Gigahoo" width="180" style="height: auto; max-width: 180px;" />
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

    public async Task SendCallSummaryAsync(string toEmail, string businessName, string? callerName, string callerPhone, int durationSeconds, string? summary)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(config["Email:FromAddress"]!));
        message.To.Add(MailboxAddress.Parse(toEmail));

        var caller = string.IsNullOrWhiteSpace(callerName) ? callerPhone : callerName;
        message.Subject = $"New call summary — {caller}";

        var duration = $"{durationSeconds / 60} min {durationSeconds % 60} sec";
        var callerNameDisplay = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(callerName) ? "Unknown" : callerName);
        var callerPhoneDisplay = System.Net.WebUtility.HtmlEncode(callerPhone);
        var summaryDisplay = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(summary) ? "No summary available." : summary);

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
                                        <img src="https://gigahoo.ai/gigahoo-logo.png" alt="Gigahoo" width="180" style="height: auto; max-width: 180px;" />
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style="padding: 24px 40px 32px;">
                                        <h1 style="margin: 0 0 8px; font-size: 24px; font-weight: 700; color: #111827; text-align: center;">New call summary</h1>
                                        <p style="margin: 0 0 24px; font-size: 15px; line-height: 1.6; color: #4b5563; text-align: center;">
                                            Hi {{businessName}}, your AI receptionist just handled a call. Here are the details.
                                        </p>

                                        <!-- Call details -->
                                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color: #f3f4f6; border-radius: 8px; margin-bottom: 24px;">
                                            <tr>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #6b7280; width: 120px;">Caller</td>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #111827; font-weight: 600;">{{callerNameDisplay}}</td>
                                            </tr>
                                            <tr>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #6b7280; border-top: 1px solid #e5e7eb;">Phone</td>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #111827; font-weight: 600; border-top: 1px solid #e5e7eb;">{{callerPhoneDisplay}}</td>
                                            </tr>
                                            <tr>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #6b7280; border-top: 1px solid #e5e7eb;">Duration</td>
                                                <td style="padding: 12px 16px; font-size: 14px; color: #111827; font-weight: 600; border-top: 1px solid #e5e7eb;">{{duration}}</td>
                                            </tr>
                                        </table>

                                        <!-- Summary -->
                                        <h2 style="margin: 0 0 8px; font-size: 16px; font-weight: 700; color: #111827;">Summary</h2>
                                        <p style="margin: 0; font-size: 15px; line-height: 1.6; color: #4b5563; white-space: pre-wrap;">{{summaryDisplay}}</p>
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
