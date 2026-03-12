using Kumunita.Identity.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Kumunita.Host.Pages.Account;

public sealed class LoginModel(
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        // Clear any leftover external cookie so a fresh login is attempted
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // PasswordSignInAsync matches on UserName, not Email — resolve it first.
        AppUser? user = await userManager.FindByEmailAsync(Input.Email);

        Microsoft.AspNetCore.Identity.SignInResult result = user is not null
            ? await signInManager.PasswordSignInAsync(
                user.UserName!,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true)
            : Microsoft.AspNetCore.Identity.SignInResult.Failed;

        if (result.Succeeded)
        {
            logger.LogInformation("User '{Email}' signed in.", Input.Email);
            return LocalRedirect(ReturnUrl ?? "/");
        }

        ErrorMessage = result switch
        {
            { IsLockedOut: true }  => "Your account is locked. Please try again later.",
            { IsNotAllowed: true } => "Sign in is not allowed. Please confirm your email address first.",
            _                      => "Invalid email or password."
        };

        if (result.IsLockedOut)
            logger.LogWarning("User '{Email}' account locked out.", Input.Email);

        return Page();
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}