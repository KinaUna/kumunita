using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Notifications;

/// <summary>
/// The <c>M6</c> (Notifications) composition service (ADR 0076). A
/// store-composing service over the frozen seams:
/// <see cref="IDocumentStore"/> (reads open their own
/// <c>QuerySession</c>), <see cref="IUserInfoService"/> (the
/// frozen <c>GetProfileAsync</c> read lane for the recipient's
/// <c>Profile.EmailLanguage</c> + <c>Email</c> — ADR 0061),
/// <see cref="ITranslationProvider"/> (the ADR 0061
/// <c>preferredLanguageCode</c> → <c>DefaultLanguageCode</c> → <c>en</c>
/// chain), and <see cref="IMailerStage"/> (the M1 durable-email
/// seam — the <c>StageAsync</c> idempotency guarantee is the **sole
/// email-side** dedup mechanism; the inbox-side dedup is the
/// <c>IdempotencyKey</c> look-up in <c>EmitAsync</c>, D4 / F10).
/// <para>
/// **Personal read, not an <c>AccessAction</c> decision (D3):** no
/// <c>Authorization.IAuthorizationService</c> in the constructor —
/// the <c>RecipientId</c> is the whole access story; no audit row is
/// emitted for an inbox read (F11).
/// </para>
/// </summary>
public sealed class NotificationService
{
    /// <summary>The inbox cap (D8) — the most recent 50 rows, newest-first.</summary>
    public const int InboxCap = 50;

    private readonly IDocumentStore _store;
    private readonly IUserInfoService _userInfo;
    private readonly ITranslationProvider _translator;
    private readonly IMailerStage _mailer;

    public NotificationService(
        IDocumentStore store,
        IUserInfoService userInfo,
        ITranslationProvider translator,
        IMailerStage mailer)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _mailer = mailer ?? throw new ArgumentNullException(nameof(mailer));
    }

    /// <summary>
    /// The writer (D4, D5, D7): stores the <see cref="Notification"/> row on
    /// <paramref name="session"/> (the inbox is the durable record) and
    /// **conditionally** calls <see cref="IMailerStage.StageAsync"/> (only if
    /// the recipient's preference enables the kind, D7), in the same
    /// transaction (C3 — the domain write + the outbox row + the envelope
    /// commit atomically). **Dedup (D4, F10):** if a <see cref="Notification"/>
    /// row with the same <paramref name="idempotencyKey"/> already exists
    /// (looked up on <paramref name="session"/>, the
    /// <c>IdempotencyKey</c> index in <c>M6DocTypes</c>), the method returns
    /// that existing row **without** storing a second row and **without**
    /// calling <c>StageAsync</c> — a re-emission of the same logical event is
    /// a no-op (no second inbox row, no second email). The key itself is the
    /// **emitter's** responsibility (D4) — a stable, content-derived
    /// <c>notification:{kind}:{stable-source-id}</c> string (the §6.3 table).
    /// Returns the stored (or pre-existing) row.
    /// </summary>
    public async Task<Notification> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("A kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));

        // (1) Inbox-side dedup (D4 / F10): a same-key row already stored (on
        //     this session or in a prior commit — the <c>IdempotencyKey</c>
        //     index in <c>M6DocTypes</c>) makes this emission a no-op: no
        //     second row, no <c>StageAsync</c> call.
        // (U09 fix: `FirstAsync` throws on an empty sequence — i.e. on every
        //  first emission, which is the normal path. The intent (and the null
        //  check below) is "no row yet" → proceed. `FirstOrDefaultAsync`.)
        var existing = await session.Query<Notification>()
            .Where(n => n.IdempotencyKey == idempotencyKey)
            .OrderBy(n => n.Created)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (existing is not null)
            return existing;

        // (2) Resolve the recipient's <c>Profile.EmailLanguage</c> (ADR 0061)
        //     and the per-kind subject + body templates (C-M6·6) **before**
        //     storing the row — the <c>Notification.Subject</c> / <c>Body</c>
        //     fields are the localized display values (recipient's language)
        //     with the UGC snippet (sender's authored language, ADR 0018).
        //     The M4 <c>BuildLocalizedReminder</c> precedent: the service
        //     resolves the templates via <see cref="ITranslationProvider"/>
        //     and appends the UGC content.
        var profile = await _userInfo.GetProfileAsync(recipientId).ConfigureAwait(false);
        var lang = profile?.EmailLanguage;                    // the ADR 0061 per-recipient resolution input
        var subject = await _translator.GetAsync($"notification.{kind}.subject", lang).ConfigureAwait(false);
        var bodyTemplate = await _translator.GetAsync($"notification.{kind}.body", lang).ConfigureAwait(false);
        var composedBody = string.IsNullOrWhiteSpace(body)
            ? bodyTemplate
            : bodyTemplate + " " + body;                      // localized template + UGC snippet (sender's language, ADR 0018)

        // (3) The inbox row is stored **unconditionally** (D7 — the inbox is
        //     the durable record; the email is the best-effort nudge).
        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString("N"),            // the codebase idiom (PostService / EventService — externally-assigned string key; Marten does not auto-generate string Ids)
            RecipientId = recipientId,
            Kind = kind,
            IdempotencyKey = idempotencyKey,
            SourceId = SourceIdFromKey(kind, idempotencyKey),
            Subject = subject,
            Body = composedBody,
            Created = DateTimeOffset.UtcNow,
        };
        session.Store(notification);

        // (4) The D7 email gate — resolved on the caller's session **before**
        //     deciding to stage: a non-null, non-empty <c>KindsEnabled</c>
        //     that omits <paramref name="kind"/> suppresses the email (F9);
        //     <c>null</c> / empty = all enabled (the lean-default).
        if (!await EmailEnabledForAsync(session, recipientId, kind, ct).ConfigureAwait(false))
            return notification;

        // (5) The email nudge — the <see cref="IMailerStage"/> idempotency
        //     guarantee is the **sole** email-side dedup (D4). Staged on the
        //     caller's session — one caller <c>SaveChangesAsync</c> = the row
        //     + the outbox row + the envelope commit atomically (C3);
        //     <c>EmitAsync</c> does not commit (D5). A recipient without a
        //     known email is skipped (no address to deliver to — the M4
        //     precedent; the inbox row is the record, D5).
        if (profile is null || string.IsNullOrWhiteSpace(profile.Email))
            return notification;
        await _mailer.StageAsync(session, idempotencyKey, profile.Email!, subject, composedBody, ct).ConfigureAwait(false);
        return notification;
    }

    /// <summary>
    /// The inbox read (D8): the most recent <see cref="InboxCap"/> (50)
    /// rows for the recipient, newest-first (<c>Created</c> descending).
    /// **No pagination, no per-kind filter in M6** (D8). A personal read —
    /// no <c>IAuthorizationService</c> call, no audit row (D3, F11).
    /// </summary>
    public async Task<IReadOnlyList<Notification>> ListInboxAsync(
        string recipientId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));

        await using var session = _store.QuerySession();
        return await session.Query<Notification>()
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.Created)
            .Take(InboxCap)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The unread count (D8): the number of the recipient's rows with
    /// <c>ReadAt</c> null. A personal read — no audit row (D3, F11).
    /// </summary>
    public async Task<int> CountUnreadAsync(string recipientId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));

        await using var session = _store.QuerySession();
        return await session.Query<Notification>()
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null)
            .CountAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The "mark all read" state lane (D8): sets <c>ReadAt = now</c> on
    /// **all** the recipient's unread rows (one load + one store + one
    /// <c>SaveChangesAsync</c> — the codebase's universal write idiom;
    /// Marten 9.31's <c>IDocumentSession</c> exposes no expression-based
    /// bulk <c>Update</c>, so the "one update" is a single session commit
    /// over the matched set, not a per-row round-trip loop). A state lane,
    /// not a read — no audit row (D3).
    /// </summary>
    public async Task MarkAllReadAsync(string recipientId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var unread = await session.Query<Notification>()
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null)
            .ToListAsync(ct).ConfigureAwait(false);
        if (unread.Count == 0)
            return;                                              // nothing to mark — a no-op (no round-trip, no commit)
        var now = DateTimeOffset.UtcNow;
        foreach (var n in unread)
            n.ReadAt = now;
        session.Store(unread.ToArray());
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The preference read (D9): the recipient's
    /// <see cref="NotificationPreference"/>; returns a **default** instance
    /// (<c>KindsEnabled = null</c> = all enabled) when none exists — the
    /// lean-default, never a <see cref="KeyNotFoundException"/>. A personal
    /// read — no audit row (D3).
    /// </summary>
    public async Task<NotificationPreference> GetPreferencesAsync(
        string recipientId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));

        await using var session = _store.QuerySession();
        return await session.LoadAsync<NotificationPreference>(recipientId, ct).ConfigureAwait(false)
            ?? new NotificationPreference { RecipientId = recipientId };
    }

    /// <summary>
    /// The preference write lane (D9): upserts the recipient's
    /// <see cref="NotificationPreference"/> with <paramref name="kindsEnabled"/>
    /// (the subset of the nine kind constants the resident has enabled;
    /// <c>null</c> / empty = all enabled — the lean-default) and sets
    /// <c>Updated = now</c>. A state lane — no audit row (D3).
    /// </summary>
    public async Task SetPreferencesAsync(
        string recipientId,
        IReadOnlyList<string>? kindsEnabled,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<NotificationPreference>(recipientId, ct).ConfigureAwait(false);
        var preference = existing ?? new NotificationPreference { RecipientId = recipientId };
        preference.KindsEnabled = kindsEnabled;
        preference.Updated = DateTimeOffset.UtcNow;
        session.Store(preference);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // --- Internals ------------------------------------------------------------

    /// <summary>
    /// The D7 gate, on the caller's session: <c>true</c> when the recipient's
    /// <see cref="NotificationPreference"/> is absent, or <c>KindsEnabled</c>
    /// is <c>null</c> / empty (the lean-default — all enabled), or the list
    /// contains <paramref name="kind"/>.
    /// </summary>
    private static async Task<bool> EmailEnabledForAsync(IDocumentSession session, string recipientId, string kind, CancellationToken ct)
    {
        var preference = await session.LoadAsync<NotificationPreference>(recipientId, ct).ConfigureAwait(false);
        var kinds = preference?.KindsEnabled;
        return kinds is null || kinds.Count == 0 || kinds.Contains(kind, StringComparer.Ordinal);
    }

    /// <summary>
    /// The <see cref="Notification.SourceId"/> for the row (display/debug,
    /// never a gate — D4): the stable source id trailing the
    /// <c>notification:{kind}:</c> prefix (the §6.3 table —
    /// <c>notification:post.reply:{replyId}</c> → <c>{replyId}</c>).
    /// </summary>
    private static string? SourceIdFromKey(string kind, string idempotencyKey)
    {
        var prefix = $"notification:{kind}:";
        if (!idempotencyKey.StartsWith(prefix, StringComparison.Ordinal))
            return null;                                       // a non-conforming key (the emitter's responsibility, D4) — no derivable source id
        var source = idempotencyKey[prefix.Length..].TrimEnd();
        return string.IsNullOrEmpty(source) ? null : source;
    }
}
