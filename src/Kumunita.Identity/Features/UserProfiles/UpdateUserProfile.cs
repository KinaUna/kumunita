using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Identity.Features.UserProfiles;

public record SetPreferredLanguage(UserId UserId, string LanguageCode);

public static class SetPreferredLanguageHandler
{
    public static async Task Handle(
        SetPreferredLanguage cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        UserProfile? profile = await session.LoadAsync<UserProfile>(cmd.UserId.Value, ct);
        if (profile is null) return;

        profile.Update(
            displayName: null,
            firstName: null,
            lastName: null,
            bio: null,
            preferredLanguage: new LanguageCode(cmd.LanguageCode));

        session.Store(profile);
    }
}