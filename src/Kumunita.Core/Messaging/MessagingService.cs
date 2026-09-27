using Kumunita.Core.Localization;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Messaging;

/// <summary>
/// Composition service for the <see cref="Kumunita.Core.Messaging"/> bounded
/// context (M9, ADR 0105 — direct 1:1 signed-in resident messaging). U02
/// ships the two admin-toggle seams (design doc §D2): a read-with-floor over
/// the <see cref="LocaleSettings"/> singleton (the <c>false</c> floor — a
/// missing row reads as **off**, the deliberate inverse of the ADR 0101
/// <c>true</c> floor) and the admin write that persists the flag plus its
/// single <c>messaging.toggle</c> <see cref="Authorization.AccessAudit"/> row
/// in the same session (C-M9·3, the ADR 0101
/// <c>announcementcomments.set-enabled</c> write-row shape). U03 appends the
/// conversation / message seams (the ctor will then gain the optional
/// <c>NotificationService?</c> nudge seam, the ADR 0077 optional-nudge-param
/// idiom).
/// </summary>
public sealed class MessagingService : IMessagingService
{
    private readonly IDocumentStore _store;

    public MessagingService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<bool> IsMessagingEnabledAsync()
    {
        using var session = _store.QuerySession();
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        // The <c>false</c> floor: a missing singleton reads as "messaging off"
        // (the deliberate inverse of the AnnouncementCommentsEnabled
        // <c>true</c> floor — a privacy-sensitive opt-in, design doc §D2).
        return settings?.MessagingEnabled == true;
    }

    public async Task SetMessagingEnabledAsync(bool enabled, string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to change the messaging gate.");

        await using var session = _store.OpenSession(new SessionOptions());
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        if (settings is null)
            settings = new LocaleSettings { MessagingEnabled = enabled };
        else
            settings.MessagingEnabled = enabled;

        // The singleton-toggle audit row (the signup.set-open /
        // announcementcomments.set-enabled shape — ADR 0004 §B.1 additive
        // field, one row per flip, C-M9·3).
        session.Store(settings);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "messaging.toggle",
            TargetKind = "messaging.toggle",
            TargetId = "messaging.toggle",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
