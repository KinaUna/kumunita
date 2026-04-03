using System.Text;
using Kumunita.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;

namespace Kumunita.Host.Pages.Account;

public sealed class ResendConfirmationModel(
    UserManager<AppUser> userManager,
    IEmailSender<AppUser> emailSender,
    IConfiguration configuration,
    ILogger<ResendConfirmationModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        AppUser? user = await userManager.FindByEmailAsync(Input.Email);

        if (user is not null && !user.EmailConfirmed)
        {
            string token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            string baseUrl = ResolveBaseUrl();
            string confirmationLink = $"{baseUrl}/Account/ConfirmEmail?userId={user.Id}&token={encodedToken}";

            await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmationLink);

            logger.LogInformation("Resent confirmation email to {Email}", Input.Email);
        }

        // Always show success to prevent user enumeration
        EmailSent = true;
        return Page();
    }

    private string ResolveBaseUrl()
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

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
