using Kumunita.Core.Bootstrap;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.Options;

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
    private readonly bool _suppressSampleAccounts;
    // The instance's public base URL (the VerificationOptions.BaseUrl
    // absolute-link precedent — the M1 verification email builds its link the
    // same way). Used to turn the same-origin relative LinkPath into the
    // absolute link the email carries. Absence (null options, e.g. test
    // harnesses) → empty (the email carries the relative path, a dev-only
    // shape — a human can still copy it into the browser bar).
    private readonly string _baseUrl;

    public NotificationService(
        IDocumentStore store,
        IUserInfoService userInfo,
        ITranslationProvider translator,
        IMailerStage mailer,
        IOptions<NotificationOptions>? options = null,
        // The instance base URL for the email's absolute link. Optional
        // (CS1736 trailing-param idiom) so the existing test-construction
        // sites that pass only the four frozen seams keep compiling unchanged.
        IOptions<Identity.VerificationOptions>? baseUrlOptions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _mailer = mailer ?? throw new ArgumentNullException(nameof(mailer));
        // ADR 0078 — the host binds SuppressForSampleAccountsInProduction to
        // !IsDevelopment() in Program.cs; in Development (Mailpit) sample
        // accounts behave like real residents; in Production/Staging they are
        // silently skipped. Absence (null options, e.g. test harnesses) → false.
        _suppressSampleAccounts = options?.Value.SuppressForSampleAccountsInProduction ?? false;
        _baseUrl = baseUrlOptions?.Value.BaseUrl ?? string.Empty;
    }

    /// <summary>
    /// Turn the same-origin relative <paramref name="linkPath"/> into the
    /// absolute link the email carries, using the instance's public
    /// <c>BaseUrl</c> (the <see cref="Identity.VerificationOptions.BaseUrl"/>
    /// absolute-link precedent — the M1 verification email builds its
    /// one-time link the same way). When <c>BaseUrl</c> is unset (a dev-only
    /// shape) the relative path is returned as-is — a human reading the mail
    /// can still copy it into the browser's bar (the same fallback the
    /// verification link uses). An empty/whitespace <paramref name="linkPath"/>
    /// returns <see cref="string.Empty"/> (no link to append).
    /// </summary>
    private static string AbsoluteLink(string baseUrl, string? linkPath)
    {
        if (string.IsNullOrWhiteSpace(linkPath)) return string.Empty;
        return (baseUrl is string root and not "" ? root.TrimEnd('/') : string.Empty) + linkPath;
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
    /// Returns the stored (or pre-existing) row, or <c>null</c> when the
    /// recipient is a sample account in a production environment (ADR 0078 —
    /// the suppression gate: no inbox row, no email, no side effects).
    /// </summary>
    public async Task<Notification?> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        CancellationToken ct = default)
        => await EmitAsync(session, recipientId, kind, idempotencyKey, body, targetId: null, linkPath: null, ct).ConfigureAwait(false);

    /// <summary>
    /// ADR 0084 — the writer with the per-target subscription gate. Same
    /// contract as <see cref="EmitAsync(IDocumentSession, string, string, string, string, CancellationToken)"/>,
    /// plus: when <paramref name="kind"/> is in
    /// <see cref="NotificationKinds.OptInKinds"/> and <paramref name="targetId"/>
    /// is provided, the recipient's effective
    /// <see cref="IsSubscriptionEnabledForAsync"/> choice is consulted **
    /// first** (before dedup, before the profile read, before staging — the
    /// gate short-circuits with a <c>null</c> return, the ADR 0078
    /// sample-suppression shape): a disabled recipient stores no inbox row
    /// and no email, exactly as if the event never happened. <c>null</c> /
    /// empty <paramref name="targetId"/> (legacy emitters, or kinds with no
    /// per-target scope) skips the gate — the kind's existing behavior is
    /// unchanged. <paramref name="linkPath"/> is the same-origin relative
    /// path to the item this notification is about (e.g.
    /// <c>/posts/{id}#reply-{replyId}</c>): it is stored on the
    /// <see cref="Notification.LinkPath"/> field (the inbox renders it as a
    /// clickable link) and appended to the **email** body as an absolute
    /// link via the instance <c>BaseUrl</c> (the
    /// <see cref="Identity.VerificationOptions.BaseUrl"/> precedent).
    /// <c>null</c> / empty <paramref name="linkPath"/> (the non-content
    /// kinds: reports, to-dos, group add/invite, account lanes) stores no
    /// link and appends nothing.
    /// </summary>
    public async Task<Notification?> EmitAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string idempotencyKey,
        string? body,
        string? targetId = null,
        string? linkPath = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("A kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));

        // (0) ADR 0084 — the per-target subscription gate. Consulted **
        //     first** (before dedup, before the profile read, before the
        //     sample-suppression gate — a disabled recipient short-circuits
        //     with no side effects of any kind). Skipped for a kind with no
        //     per-target scope (targetId absent — the gate has nothing to
        //     resolve against): legacy emitters that don't pass targetId are
        //     unaffected. The default (opt-in vs opt-out) is resolved inside
        //     IsSubscriptionEnabledForAsync from the
        //     NotificationKinds.OptInKinds table: opt-IN kinds (the 3 new
        //     ADR 0084 kinds) default to disabled, opt-OUT kinds (group.post
        //     and the other 10 legacy kinds) default to enabled.
        if (!string.IsNullOrWhiteSpace(targetId)
            && !await IsSubscriptionEnabledForAsync(session, recipientId, kind, targetId, ct).ConfigureAwait(false))
        {
            return null;   // ADR 0084 — recipient disabled this (kind, target): suppress
        }

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

        // (2a) ADR 0078 — sample-account suppression gate (production only).
        //      The host binds this to !IsDevelopment(); in Development the
        //      flag is false so sample accounts behave like real residents
        //      (Mailpit collects their mail). In Production / Staging the
        //      flag is true and any recipient whose profile e-mail matches a
        //      code-owned sample address (SampleDataSeeder.SampleAccountEmails)
        //      is a no-op: no inbox row stored, no email staged, no
        //      translation lookup. The gate is a pure return-null — the
        //      caller's transaction is unaffected (the caller still runs its
        //      own SaveChangesAsync for the domain write it is performing).
        if (_suppressSampleAccounts
            && profile is not null
            && !string.IsNullOrWhiteSpace(profile.Email)
            && SampleDataSeeder.SampleAccountEmails.Contains(profile.Email.Trim()))
        {
            return null;   // ADR 0078 — sample account in production: suppress
        }

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
            // The same-origin relative link to the item (stored as-is for the
            // inbox; the email below appends the instance BaseUrl to make it
            // absolute). `null` when the emitter didn't provide one.
            LinkPath = string.IsNullOrWhiteSpace(linkPath) ? null : linkPath,
            Created = DateTimeOffset.UtcNow,
        };
        session.Store(notification);

        // (4) The D7 email gate — resolved on the caller's session **before**
        //     deciding to stage: a non-null, non-empty <c>KindsEnabled</c>
        //     that omits <paramref name="kind"/> suppresses the email (F9);
        //     <c>null</c> / empty = all enabled (the lean-default).
        if (!await EmailEnabledForAsync(session, recipientId, kind, ct).ConfigureAwait(false))
            return notification;

        // (4a) The <c>event.reminder</c> kind's email is **M4's** — staged
        //      directly by <c>EventReminderService</c> with the frozen
        //      <c>remind:{eventId}:{userId}</c> key (ADR 0054, the M4 surface).
        //      M6's role for this kind is the inbox row only — the F4 pin:
        //      "the email is M4's; the inbox row is M6's". Staging here too
        //      would double-send: the two keys differ, so the outbox dedup
        //      can't catch the second (F10 — the idempotency-key contract).
        if (kind == NotificationKinds.EventReminder)
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

        // (5a) The item link (the content/reply notification surface): when
        //      the emitter supplied a <c>LinkPath</c>, the email carries it
        //      as an **absolute** link (the instance <c>BaseUrl</c> + the
        //      same-origin relative path — the
        //      <see cref="Identity.VerificationOptions.BaseUrl"/> precedent,
        //      the M1 verification email builds its one-time link the same
        //      way). A short localized "view" prefix makes the link read
        //      naturally (the <c>notifications.view</c> key, the en floor
        //      "View" / de "Ansehen" / fr "Voir" / da "Se"). The link is
        //      appended **only to the email** — the stored <see
        //      cref="Notification.Body"/> stays the inbox's localized text +
        //      UGC snippet (the inbox renders the <c>LinkPath</c> as its own
        //      clickable link in the view). A kind with no <c>LinkPath</c>
        //      (the non-content lanes: reports, to-dos, group add/invite,
        //      account lanes) appends nothing.
        var emailBody = composedBody;
        if (!string.IsNullOrWhiteSpace(notification.LinkPath))
        {
            var viewPrefix = await _translator.GetAsync("notifications.view", lang).ConfigureAwait(false);
            emailBody += "\n\n" + viewPrefix + ": " + AbsoluteLink(_baseUrl, notification.LinkPath);
        }

        await _mailer.StageAsync(session, idempotencyKey, profile.Email!, subject, emailBody, ct).ConfigureAwait(false);
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
    /// (the subset of the <see cref="NotificationKinds.Known"/> closed set the
    /// resident has enabled; <c>null</c> / empty = all enabled — the
    /// lean-default) and sets <c>Updated = now</c>. A state lane — no audit
    /// row (D3).
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

    // --- ADR 0084 — per-target subscription lanes (the §6.1
    //     NotificationSubscription shape; the same personal-read / no-audit
    //     convention as the NotificationPreference lanes, D3 / F11) ──────

    /// <summary>
    /// ADR 0084 — the subscription read (the settings page's list): the
    /// recipient's <see cref="NotificationSubscription"/> rows, in
    /// (Kind, TargetId) order (the stable display order). A personal read
    /// — no audit row (D3).
    /// </summary>
    public async Task<IReadOnlyList<NotificationSubscription>> GetSubscriptionsAsync(
        string recipientId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));
        await using var session = _store.QuerySession();
        return await session.Query<NotificationSubscription>()
            .Where(s => s.RecipientId == recipientId)
            .OrderBy(s => s.Kind).ThenBy(s => s.TargetId)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// ADR 0084 — the subscription write lane (upsert on the
    /// (RecipientId, Kind, TargetId) business key): stores the recipient's
    /// explicit <paramref name="enabled"/> choice for the named (kind, target)
    /// and sets <c>Updated = now</c>. The row is **never deleted** — an
    /// opt-in toggle-off stores an <c>Enabled = false</c> row (the gate
    /// consults the row's value, not its presence, so the row is the record
    /// of the resident's last explicit choice — the same convention as the
    /// <see cref="SetPreferencesAsync"/> lane, which stores the whole
    /// preference rather than deleting it on an opt-out). A state lane — no
    /// audit row (D3).
    /// </summary>
    public async Task SetSubscriptionAsync(
        string recipientId,
        string kind,
        string targetId,
        bool enabled,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("A kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("A target id is required.", nameof(targetId));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.Query<NotificationSubscription>()
            .Where(s => s.RecipientId == recipientId && s.Kind == kind && s.TargetId == targetId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var sub = existing ?? new NotificationSubscription
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientId = recipientId,
            Kind = kind,
            TargetId = targetId,
        };
        sub.Enabled = enabled;
        sub.Updated = DateTimeOffset.UtcNow;
        session.Store(sub);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// ADR 0084 — the per-target subscription gate, on the caller's session
    /// (the <see cref="EmailEnabledForAsync"/> / preference-gate pattern, but
    /// resolved per-(recipient, kind, target) rather than per-(recipient,
    /// kind)). Resolves the recipient's effective choice for the named (kind,
    /// target) by combining the stored
    /// <see cref="NotificationSubscription"/> row (if any) with the kind's
    /// default from <see cref="NotificationKinds.OptInKinds"/>:
    /// <list type="bullet">
    /// <item>an **opt-IN** kind (in <see cref="NotificationKinds.OptInKinds"/>)
    ///   defaults to <b>disabled</b> — an absent row is treated as
    ///   <c>false</c>, a stored row's <c>Enabled</c> value is returned
    ///   verbatim.</item>
    /// <item>an **opt-OUT** kind (not in <see cref="NotificationKinds.OptInKinds"/>)
    ///   defaults to <b>enabled</b> — an absent row is treated as
    ///   <c>true</c>, a stored row's <c>Enabled</c> value is returned
    ///   verbatim (an <c>Enabled = false</c> row disables).</item>
    /// </list>
    /// **Personal read, no audit row** (D3 / F11 carried): the
    /// <c>RecipientId</c> is the whole access story. A pure read — no write,
    /// no commit (the caller's transaction is unaffected).
    /// </summary>
    public async Task<bool> IsSubscriptionEnabledForAsync(
        IDocumentSession session,
        string recipientId,
        string kind,
        string targetId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(recipientId)) throw new ArgumentException("A recipient id is required.", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("A kind is required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(targetId)) throw new ArgumentException("A target id is required.", nameof(targetId));

        var row = await session.Query<NotificationSubscription>()
            .Where(s => s.RecipientId == recipientId && s.Kind == kind && s.TargetId == targetId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (row is not null)
            return row.Enabled;

        // No row stored — fall back to the kind's default (the
        // <see cref="NotificationKinds.OptInKinds"/> table: opt-IN kinds
        // default to disabled, everything else defaults to enabled — the
        // ADR 0084 "fresh install behaves exactly like before" pin).
        return !NotificationKinds.OptInKinds.Contains(kind, StringComparer.Ordinal);
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
