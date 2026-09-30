using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Notifications;

/// <summary>
/// M20 (ADR 0121, D5) — the §6.4 <c>NotificationFlush</c> job's business logic
/// (the <see cref="Kumunita.Core.Events.EventReminderService"/> precedent
/// verbatim — a <b>Wolverine-free</b> static class that the Web host's thin
/// adapter (<c>NotificationFlushHandler</c>, U05) calls with a live
/// <see cref="IDocumentStore"/>). Per run: (1) load the bounded set of
/// <see cref="Notification"/> rows with <c>EmailDeferred == true</c> (ordered by
/// <c>Created</c>), (2) for each, re-resolve the ADR 0019 effective zone
/// (<see cref="Profile.TimeZone"/> override →
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync()"/> platform
/// default → <see cref="TimeZoneInfo.Utc"/> floor) + re-evaluate the pure
/// <see cref="QuietScheduleEvaluator.IsQuietNow"/> (D3) at the **run** instant,
/// (3) for each that is **no longer quiet**, stage the held email via the
/// **frozen** <see cref="IMailerStage"/> with the deferred idempotency key
/// (<see cref="NotificationService.DeferredKey"/>, D4) and flip that row's
/// <c>EmailDeferred = false</c>, (4) leave each still-quiet row
/// <c>EmailDeferred = true</c> (re-checked next run). It commits **once per
/// run** (C3).
/// <para>
/// **The flush does NOT re-run <see cref="NotificationService.EmitAsync"/>
/// (D5's *Forbids*)** — re-emitting would double-store the inbox row
/// (ADR 0076 D7, C-M20·1). It **only stages the held email** for rows whose
/// quiet has lifted, reusing the row's stored <see cref="Notification.Subject"/>
/// / <see cref="Notification.Body"/> (the localized display values the emit
/// path already composed — the <see cref="Notification"/> row is the durable
/// record, D7). A recipient with **no** schedule or a disabled schedule is
/// **no longer quiet** (the floor, C-M20·3) → their held email is staged (the
/// flush is the *release* path). A recipient with **no** <see
/// cref="Profile.Email"/> is still cleared (resolved, not re-tried forever —
/// the C-M20·4 note, the <see cref="Kumunita.Core.Events.EventReminderService"/>
/// "recipient without a Profile email is skipped" precedent applied to a
/// *deferred* row).
/// </para>
/// <para>
/// **Re-runnable / idempotent (C-M20·4):** the flip to <c>EmailDeferred =
/// false</c> is committed in the same run, so a second run does not re-query
/// the already-cleared row (the §6.2 "existing-row check" guard) — and even if
/// it did, the <see cref="NotificationService.DeferredKey"/> outbox dedup
/// (F10) makes a second stage of the same key a no-op. A still-quiet row stays
/// <c>EmailDeferred = true</c> across consecutive runs.
/// </para>
/// <para>
/// **No <c>AccessAudit</c> row** — a flush is a side effect (releasing a held
/// email), not an access decision (the <see
/// cref="Kumunita.Core.Events.EventReminderService"/> / M1 verification-email
/// posture; C-M20·5 — zero new authorization surface).
/// </para>
/// </summary>
public static class NotificationFlushService
{
    /// <summary>
    /// Run one flush tick. <paramref name="now"/> is the injection point for
    /// tests (pin "now" so the quiet verdict is deterministic — the
    /// <see cref="Kumunita.Core.Events.EventReminderService"/> shape). Returns
    /// the number of emails delivered this run (for the U05 handler's log
    /// line).
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same
    /// <c>IDocumentStore</c> the domain services use — one Postgres, no
    /// cross-store window).</param>
    /// <param name="now">The run instant the quiet verdict is evaluated at
    /// (ADR 0019 — the run instant, not the emit instant: quiet may have
    /// lifted since the row was deferred).</param>
    /// <param name="mailer">The <b>frozen</b> <see cref="IMailerStage"/> (the
    /// host resolves the real <c>OutboxEmailStager</c> in production; a
    /// recording fake in the harness).</param>
    /// <param name="userInfo">The <b>frozen</b>
    /// <see cref="IUserInfoService"/> <c>GetProfileAsync</c> read lane (the
    /// recipient's <c>Profile.Email</c> delivery address +
    /// <c>Profile.TimeZone</c> ADR 0019 override).</param>
    /// <param name="localization">The <b>frozen</b>
    /// <see cref="ILocalizationService"/> platform-default zone read (the ADR
    /// 0019 chain's fallback tier) + the D6 cadence read the U05 handler uses
    /// to re-schedule.</param>
    /// <param name="translationProvider">Optional (ADR 0061) — accepted to keep
    /// the U05 adapter's call-site compiling verbatim (design doc §7); unused
    /// here because the held email's subject/body are the row's already-localized
    /// <see cref="Notification.Subject"/> / <see cref="Notification.Body"/>
    /// values (the emit path resolved them in the recipient's language, ADR
    /// 0061).</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task<int> FlushDeferredAsync(
        IDocumentStore store,
        DateTimeOffset now,
        IMailerStage mailer,
        IUserInfoService userInfo,
        ILocalizationService localization,
        ITranslationProvider? translationProvider = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(mailer);
        ArgumentNullException.ThrowIfNull(userInfo);
        ArgumentNullException.ThrowIfNull(localization);

        // The ADR 0019 platform-default zone, resolved ONCE per run (the
        // EventReminderService "two platform reads shared across recipients"
        // shape — a recipient's own Profile.TimeZone override wins over this).
        var defaultZoneId = await localization.GetDefaultTimezoneAsync().ConfigureAwait(false);

        // (1) The bounded set of held (deferred) rows, oldest-first (the
        //     ListInboxAsync feed-ordering shape, by Created ascending —
        //     oldest held email delivered first). A plain read; no writes yet.
        List<Notification> pending;
        await using (var q = store.QuerySession())
        {
            pending = (await q.Query<Notification>()
                .Where(n => n.EmailDeferred)
                .OrderBy(n => n.Created)
                .ToListAsync(ct).ConfigureAwait(false)).ToList();
        }

        if (pending.Count == 0)
            return 0;   // nothing held — a no-op run (no write, no commit)

        // (2) Per row: re-resolve the ADR 0019 effective zone + re-evaluate
        //     the PURE IsQuietNow (D3) at the RUN instant. A missing schedule
        //     or a disabled schedule → NOT quiet (the C-M20·3 floor) → the
        //     release path. All reads complete BEFORE any write below (the
        //     EventReminderService reads-then-writes discipline — no query
        //     after a write in the write session).
        var toRelease = new List<(Notification Row, Profile? Profile)>();
        foreach (var row in pending)
        {
            var profile = await userInfo.GetProfileAsync(row.RecipientId).ConfigureAwait(false);
            var zoneId = profile?.TimeZone;
            if (string.IsNullOrWhiteSpace(zoneId))
                zoneId = defaultZoneId;
            var zone = TryResolveZone(zoneId) ?? TimeZoneInfo.Utc;

            await using var q2 = store.QuerySession();
            var schedule = await q2
                .LoadAsync<NotificationQuietSchedule>(row.RecipientId, ct)
                .ConfigureAwait(false);

            // Still quiet at the run instant → leave EmailDeferred = true
            // (re-checked next run; C-M20·4 — a still-quiet row is untouched
            // across consecutive flush runs).
            if (QuietScheduleEvaluator.IsQuietNow(schedule, now, zone))
                continue;

            toRelease.Add((row, profile));
        }

        if (toRelease.Count == 0)
            return 0;   // every held row is still quiet — a no-op run

        // (3) Stage each now-clear held email via the frozen IMailerStage under
        //     the DEFERRED idempotency key (U03's DeferredKey — distinct from
        //     the emit-time key, so the F10 outbox dedup does not collide and a
        //     cleared email is delivered exactly once, C-M20·4) and flip the
        //     row EmailDeferred = false. One SaveChangesAsync per run (C3) —
        //     the held emails' envelopes + the flips commit atomically.
        //     A recipient with no Profile.Email is still cleared (resolved, not
        //     re-tried forever — the C-M20·4 note).
        var delivered = 0;
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        foreach (var (row, profile) in toRelease)
        {
            if (profile is not null && !string.IsNullOrWhiteSpace(profile.Email))
            {
                var key = NotificationService.DeferredKey(row.Kind, row.SourceId ?? string.Empty);
                await mailer.StageAsync(
                    session,
                    key,
                    profile.Email!,
                    row.Subject ?? string.Empty,
                    row.Body ?? string.Empty,
                    ct).ConfigureAwait(false);
                delivered++;
            }

            // The flip happens regardless of the stage (a no-address recipient
            // is still cleared — not left "pending" forever).
            row.EmailDeferred = false;
            session.Store(row);   // re-track the detached row so the flip persists
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return delivered;
    }

    /// <summary>
    /// An IANA zone id → a <see cref="TimeZoneInfo"/>, or <c>null</c> when the
    /// id is blank / not present on the OS (the
    /// <see cref="Kumunita.Core.Events.EventReminderService"/> /
    /// <see cref="NotificationService"/> "TryResolveZone fall through" rule —
    /// never a throw). The <see cref="TimeZoneInfo.Utc"/> floor is the caller's
    /// responsibility.
    /// </summary>
    private static TimeZoneInfo? TryResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }
}
