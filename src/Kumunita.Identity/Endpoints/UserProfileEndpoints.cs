using Kumunita.Identity.Domain;
using Kumunita.Identity.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using Wolverine.Http;

namespace Kumunita.Identity.Endpoints;

public static class UserProfileEndpoints
{
    public record SetPreferredLanguageRequest(string LanguageCode);

    [WolverinePut("/users/me/preferred-language")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public static async Task<IResult> SetPreferredLanguage(
        SetPreferredLanguageRequest body,
        ClaimsPrincipal user,
        IDocumentSession session,
        CancellationToken ct)
    {
        UserId userId = user.GetUserId();

        UserProfile? profile = await session.LoadAsync<UserProfile>(userId.Value, ct);
        if (profile is null)
            return Results.NotFound();

        profile.Update(
            displayName: null,
            firstName: null,
            lastName: null,
            bio: null,
            preferredLanguage: new LanguageCode(body.LanguageCode));

        session.Store(profile);

        return Results.NoContent();
    }
}