using Kumunita.Identity.Domain;
using Kumunita.Identity.Domain.Events;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Features.Registration;

public record RegisterUser(
    string Email,
    string Password,
    string DisplayName,
    LanguageCode PreferredLanguage);

public record UserRegistrationResult(UserId UserId, bool RequiresEmailConfirmation);

public static class RegisterUserHandler
{
    public static async Task<(UserRegistrationResult, UserRegistered)> Handle(
        RegisterUser cmd,
        UserManager<AppUser> userManager,  // ASP.NET Core Identity
        IDocumentSession session,           // Marten — triggers AutoApplyTransactions
        CancellationToken ct)
    {
        // Step 1 — create the ASP.NET Core Identity user
        var appUser = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = cmd.Email,
            UserName = cmd.Email,
        };

        var result = await userManager.CreateAsync(appUser, cmd.Password);
        if (!result.Succeeded)
            throw new RegistrationException(result.Errors);

        // Step 2 — assign default Member role
        await userManager.AddToRoleAsync(appUser, AppRole.SystemRoles.Member);

        var userId = appUser.DomainId;

        // Step 3 — create Marten UserProfile
        var profile = UserProfile.Create(userId, cmd.DisplayName, cmd.PreferredLanguage);
        session.Store(profile);

        // Step 4 — create Marten DirectoryEntry
        var directory = DirectoryEntry.Create(userId, cmd.DisplayName, userId);
        session.Store(directory);

        // Step 5 — return result and event
        // Wolverine publishes UserRegistered after Marten commits
        var registrationResult = new UserRegistrationResult(
            userId,
            RequiresEmailConfirmation: !appUser.EmailConfirmed);

        return (registrationResult, new UserRegistered(
            userId,
            cmd.Email,
            cmd.DisplayName,
            cmd.PreferredLanguage));
    }
}
