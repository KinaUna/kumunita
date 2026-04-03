using Kumunita.Identity.Domain;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kumunita.Identity.Infrastructure;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender<AppUser>
{
    public async Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        string subject = "Confirm your Kumunita account";
        string body = $"""
            <html>
            <body style="font-family: system-ui, sans-serif; background: #f5f5f5; padding: 2rem;">
              <div style="max-width: 480px; margin: 0 auto; background: #fff; border-radius: 8px; box-shadow: 0 2px 16px rgba(0,0,0,.1); padding: 2rem;">
                <h2 style="margin-top: 0;">Welcome to Kumunita</h2>
                <p>Please confirm your email address by clicking the link below:</p>
                <p style="margin: 1.5rem 0;">
                  <a href="{confirmationLink}"
                     style="display: inline-block; padding: .65rem 1.5rem; background: #1976d2; color: #fff; text-decoration: none; border-radius: 4px; font-weight: 600;">
                    Confirm email
                  </a>
                </p>
                <p style="font-size: .875rem; color: #666;">If you did not create an account, you can safely ignore this email.</p>
              </div>
            </body>
            </html>
            """;

        await SendAsync(email, subject, body);
    }

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
        => Task.CompletedTask;

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
        => Task.CompletedTask;

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        SmtpOptions opts = options.Value;

        MimeMessage message = new();
        message.From.Add(new MailboxAddress(opts.SenderName, opts.SenderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using SmtpClient client = new();

        try
        {
            await client.ConnectAsync(
                opts.Host,
                opts.Port,
                opts.UseSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.None);

            if (!string.IsNullOrEmpty(opts.Username))
                await client.AuthenticateAsync(opts.Username, opts.Password);

            await client.SendAsync(message);
            logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
            throw;
        }
        finally
        {
            await client.DisconnectAsync(quit: true);
        }
    }
}
