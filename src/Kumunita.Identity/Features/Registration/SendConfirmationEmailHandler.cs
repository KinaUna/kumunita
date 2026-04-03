using System.Text;
using Kumunita.Identity.Domain;
using Kumunita.Identity.Domain.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kumunita.Identity.Features.Registration;

public static class SendConfirmationEmailHandler
{
    public static async Task Handle(
        UserRegistered @event,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct)
    {
        AppUser? user = await userManager.FindByIdAsync(@event.UserId.Value.ToString());
        if (user is null)
        {
            logger.LogWarning("SendConfirmationEmail: user {UserId} not found", @event.UserId);
            return;
        }

        if (user.EmailConfirmed)
            return;

        string token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        string baseUrl = ResolveBaseUrl(configuration);
        string confirmationLink = $"{baseUrl}/Account/ConfirmEmail?userId={user.Id}&token={encodedToken}";

        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink);

        logger.LogInformation(
            "Confirmation email sent to {Email} for user {UserId}",
            user.Email, @event.UserId);
    }

    private static string ResolveBaseUrl(IConfiguration configuration)
    {
        string? configured = configuration["Kumunita:BaseUrl"];
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');

        string? aspnetUrls = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrEmpty(aspnetUrls))
        {
            string? https = aspnetUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            if (https is not null) return https.TrimEnd('/');

            string? http = aspnetUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (http is not null) return http.TrimEnd('/');
        }

        return "https://localhost:7577";
    }
}
