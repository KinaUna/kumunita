using System.Text;
using Kumunita.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace Kumunita.Host.Pages.Account;

public sealed class ConfirmEmailModel(
    UserManager<AppUser> userManager) : PageModel
{
    public bool Succeeded { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery] Guid? userId,
        [FromQuery] string? token)
    {
        if (userId is null || string.IsNullOrEmpty(token))
        {
            ErrorMessage = "Invalid confirmation link.";
            return Page();
        }

        AppUser? user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null)
        {
            ErrorMessage = "Invalid confirmation link.";
            return Page();
        }

        string decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        IdentityResult result = await userManager.ConfirmEmailAsync(user, decodedToken);

        Succeeded = result.Succeeded;
        if (!result.Succeeded)
            ErrorMessage = "Invalid or expired confirmation link.";

        return Page();
    }
}
