using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Events;

/// <summary>
/// The §6.4 <c>EventReminders</c> job's window config (ADR 0054 §3.6, the
/// <see cref="Kumunita.Core.Authorization.AuditPurgeOptions"/> precedent — a config
/// POCO, not improvised; bound by the host, e.g. <c>EventReminder__WindowHours</c>).
/// </summary>
public sealed class EventReminderOptions
{
    /// <summary>The "remind the day before" window in hours (the §3.6 default 24).</summary>
    public int WindowHours { get; set; } = 24;
}

/// <summary>
/// The §6.4 <c>EventReminders</c> job's business logic (ADR 0054 §3.6, the
/// <see cref="Kumunita.Core.Authorization.AuditPurgeService"/> precedent — a
/// <b>Wolverine-free</b> static class that the Web host's thin adapter
/// (<c>EventReminderHandler</c>, U08) calls with a live <see cref="IDocumentStore"/>).
/// <para>
/// <b>Window (the "remind the day before" semantics):</b> an <see cref="Event"/> is a
/// candidate when <c>now &lt; Start ≤ now + WindowHours</c> (default 24 h). A
/// <see cref="Event.Start"/> in the past is never reminded; one beyond the window is not
/// yet. <see cref="Event.ReminderEnabled"/> is the per-event opt-out (skip when
/// <c>false</c>); drafts (<see cref="Event.IsDraft"/>) and soft-deleted
/// (<see cref="Event.IsDeleted"/>) events are excluded (they are not live).
/// </para>
/// <para>
/// <b>Recipients (§3.6):</b> the event's <see cref="EventRsvp"/> rows with
/// <see cref="RsvpStatus.Going"/> <b>plus the author, always</b> — even if the author did
/// not RSVP. A <see cref="RsvpStatus.Maybe"/> / <see cref="RsvpStatus.No"/> RSVP is not
/// reminded. A recipient with no <see cref="Profile"/> email (unseeded / suspended) is
/// skipped — there is no address to deliver to.
/// </para>
/// <para>
/// <b>Time zone &amp; date-time format (ADR 0019 / 0020):</b> the reminder's
/// "when" is rendered in <b>each recipient's</b> effective time zone and
/// effective date-time format — the exact resolution order the <c>kw-dt</c>
/// TagHelper uses: the recipient's <see cref="Profile.TimeZone"/> /
/// <see cref="Profile.DateFormat"/> override → the platform default
/// (<see cref="ILocalizationService.GetDefaultTimezoneAsync()"/> /
/// <see cref="ILocalizationService.GetDefaultDateFormatAsync()"/>) → the
/// <c>UTC</c> / <see cref="DateFormat.FloorFormat"/> floor. The instant is
/// converted to the zone's wall-clock <i>first</i>, then the format is applied
/// (invariant culture). A background job has no request principal, so the
/// recipient's <see cref="Profile"/> is the "actor" here; the two platform
/// defaults are resolved once per tick and shared across recipients.
/// </para>
/// <para>
/// <b>Email (the frozen trio, untouched):</b> each recipient is staged via the
/// <b>frozen</b> <see cref="IMailerStage.StageAsync"/> with the §6.2 per-email idempotency
/// key <c>remind:{eventId}:{userId}</c>. The <see cref="IMailerStage"/> seam itself is
/// <b>not</b> re-shaped (no new method); the <see cref="OutboxEmail"/> row and the
/// durable <c>OutboxEmailHandler</c> are <b>untouched</b> — this service only *stages*
/// rows, and the §6.2 durable handler dispatches over SMTP (best-effort, dead-letter on
/// final failure).
/// </para>
/// <para>
/// <b>No double-send across ticks:</b> before staging, the existing
/// <see cref="OutboxEmail"/> key set is read; a key already present (from an earlier tick
/// or the same run) is not staged again — the §3.6 "existing-row check" guard. This is
/// the re-runnable (idempotent) guarantee the §6.2 best-effort table relies on.
/// </para>
/// <para>
/// <b>No <c>AccessAudit</c> row:</b> the reminder is a side effect, not an access
/// decision — the same posture as the M1 verification email. Nothing here touches the
/// authorization path or the frozen <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>.
/// </para>
/// </summary>
public static class EventReminderService
{
    /// <summary>
    /// Run one reminder tick.
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same
    /// <c>IDocumentStore</c> the domain services use — one Postgres, no cross-store window).</param>
    /// <param name="options">The reminder window config (per-instance, §6.4 — not improvised).</param>
    /// <param name="now">Injection point for tests (pin "now" so the window boundary is deterministic).</param>
    /// <param name="mailer">The <b>frozen</b> <see cref="IMailerStage"/> (the C3 envelope — the host
    /// resolves the real <c>OutboxEmailStager</c> in production; a recording fake in the harness).</param>
    /// <param name="localization">The platform-default time zone + date-time format resolver (ADR 0019 / 0020).</param>
    /// <param name="translationProvider">Optional (ADR 0061) — resolves the reminder's subject/body in
    /// each recipient's <see cref="Profile.EmailLanguage"/> (→ platform default → <c>en</c> floor).
    /// When <c>null</c> (e.g. the test harness) the English subject/body are kept verbatim, so the
    /// frozen English fallback is preserved and the 7 direct call sites in
    /// <c>EventReminderServiceTests</c> keep compiling unchanged.</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task SendRemindersAsync(
        IDocumentStore store,
        EventReminderOptions options,
        DateTimeOffset now,
        IMailerStage mailer,
        ILocalizationService localization,
        ITranslationProvider? translationProvider = null,
        CancellationToken ct = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (mailer is null) throw new ArgumentNullException(nameof(mailer));
        if (localization is null) throw new ArgumentNullException(nameof(localization));

        // The platform-default time zone + date-time format (ADR 0019 / 0020) —
        // resolved ONCE per tick (the two platform reads are shared across
        // recipients; a recipient's own Profile override wins over these, per
        // the kw-dt resolution order).
        var defaultZoneId = await localization.GetDefaultTimezoneAsync();
        var defaultFormat = await localization.GetDefaultDateFormatAsync();

        var windowEnd = now.AddHours(Math.Max(24, options.WindowHours));
        var windowStart = now;

        // (a) Live events inside the "remind the day before" window (drafts + deleted
        //     excluded — they are not live; ReminderEnabled=false is the per-event opt-out).
        await using (var q = store.QuerySession())
        {
            var candidates = await q.Query<Event>()
                .Where(e => e.ReminderEnabled &&
                            !e.IsDraft &&
                            !e.IsDeleted &&
                            e.Start > windowStart &&
                            e.Start <= windowEnd)
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return;

            var eventIds = candidates.Select(e => e.Id).ToList();

            // (b) The event's Going RSVPs (the recipient set).
            await using var q2 = store.QuerySession();
            var rsvps = await q2.Query<EventRsvp>()
                .Where(r => r.Status == RsvpStatus.Going && eventIds.Contains(r.EventId))
                .ToListAsync(ct);

            // The author is always a recipient (even without an RSVP), per event.
            var recipientsByEvent = eventIds.ToDictionary(eid => eid, eid =>
            {
                var users = new HashSet<string>();
                var ev = candidates.First(e => e.Id == eid);
                if (!string.IsNullOrWhiteSpace(ev.AuthorId))
                    users.Add(ev.AuthorId);                       // the author, always
                foreach (var r in rsvps.Where(r => r.EventId == eid))
                    users.Add(r.UserId);                           // the Going set
                return users;
            });

            // (c) Existing OutboxEmail keys for this tick — the no-double-send guard
            //     (§3.6: "the key's existing-row check is the no-double-send guard across
            //     ticks"). A key already present from an earlier run is not re-staged.
            var candidateKeys = recipientsByEvent
                .SelectMany(kvp => kvp.Value.Select(uid => $"remind:{kvp.Key}:{uid}"))
                .ToList();
            await using var q3 = store.QuerySession();
            var existingKeys = candidateKeys.Count == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : (await q3.Query<OutboxEmail>()
                        .Where(m => candidateKeys.Contains(m.IdempotencyKey))
                        .Select(m => m.IdempotencyKey)
                        .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

            // Resolve each recipient's Profile — the delivery address AND the
            // recipient's effective time zone + date-time format override
            // (Profile.TimeZone / Profile.DateFormat; ADR 0019 / 0020).
            var allUsers = recipientsByEvent.Values.SelectMany(v => v).Distinct().ToList();
            await using var q4 = store.QuerySession();
            var profiles = allUsers.Count == 0
                ? new Dictionary<string, Profile>(StringComparer.Ordinal)
                : (await q4.Query<Profile>()
                        .Where(p => allUsers.Contains(p.SubjectId))
                        .ToListAsync(ct))
                    .ToDictionary(p => p.SubjectId, StringComparer.Ordinal);

            // (d) Stage one OutboxEmail per (event, recipient) — the frozen trio (untouched).
            //     A recipient without a known email is skipped (no address to deliver to).
            //     The body's "when" is rendered in the recipient's own time zone +
            //     date-time format (ADR 0019 / 0020, the kw-dt resolution order).
            //     NO AccessAudit row is written — a reminder is a side effect, not an access
            //     decision (the M1 verification-email posture; ADR 0054 §3.6).
            await using var session = store.OpenSession(new Marten.Services.SessionOptions());
            var staged = 0;
            foreach (var ev in candidates)
            {
                foreach (var userId in recipientsByEvent[ev.Id])
                {
                    var key = $"remind:{ev.Id}:{userId}";
                    if (existingKeys.Contains(key))
                        continue;                                  // already sent this tick
                    if (!profiles.TryGetValue(userId, out var profile) || string.IsNullOrWhiteSpace(profile?.Email))
                        continue;                                  // no delivery address
                    // ADR 0061: the subject/body are resolved in the recipient's
                    // EmailLanguage (→ platform default → en floor) when a provider is wired in;
                    // otherwise the frozen English fallback is used verbatim (the test-harness path).
                    var (subject, body) = translationProvider is null
                        ? (($"Reminder: {ev.Title}"),
                           BuildReminderBody(ev, profile, defaultZoneId, defaultFormat))
                        : await BuildLocalizedReminder(ev, profile, defaultZoneId, defaultFormat, translationProvider);
                    await mailer.StageAsync(
                        session,
                        idempotencyKey: key,
                        recipient: profile!.Email!,
                        subject: subject,
                        body: body,
                        ct: ct);
                    staged++;
                }
            }

            if (staged > 0)
                await session.SaveChangesAsync(ct);
        }
    }

    /// <summary>The reminder email body. The "when" is rendered in
    /// <paramref name="recipient"/>'s effective time zone + date-time format
    /// (ADR 0019 / 0020); the rest is the event's title + location + body
    /// (the §6.1 "rendered body" shape).</summary>
    private static string BuildReminderBody(Event ev, Profile? recipient, string defaultZoneId, string defaultFormat)
    {
        var when = FormatEventInstant(
            ev.Start,
            recipient?.TimeZone, defaultZoneId,
            recipient?.DateFormat, defaultFormat);
        var where = string.IsNullOrWhiteSpace(ev.Location) ? string.Empty : $", {ev.Location}";
        return $"**{ev.Title}** is coming up: {when}{where}. " +
               (string.IsNullOrWhiteSpace(ev.Body) ? string.Empty : ev.Body);
    }

    /// <summary>
    /// The ADR 0061 localized reminder: the subject + the fixed "coming up" prefix are resolved
    /// through <paramref name="provider"/> in the recipient's <see cref="Profile.EmailLanguage"/>
    /// (→ platform default → <c>en</c> floor), then <see cref="System.String.Format"/> with the
    /// registry placeholders (<c>{0}</c>=title, <c>{1}</c>=when, <c>{2}</c>=where). The event's own
    /// UGC body (authored in its own language, ADR 0018) is appended after the localized prefix —
    /// exactly the shape <see cref="BuildReminderBody"/> produces for the English fallback, so the
    /// two paths read identically apart from the surrounding words.
    /// </summary>
    private static async Task<(string Subject, string Body)> BuildLocalizedReminder(
        Event ev, Profile? recipient, string defaultZoneId, string defaultFormat, ITranslationProvider provider)
    {
        string? lang = recipient?.EmailLanguage;
        string when = FormatEventInstant(
            ev.Start,
            recipient?.TimeZone, defaultZoneId,
            recipient?.DateFormat, defaultFormat);
        var where = string.IsNullOrWhiteSpace(ev.Location) ? string.Empty : $", {ev.Location}";

        string subject = System.String.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            await provider.GetAsync("email.reminder_subject", lang),
            ev.Title);

        string prefix = System.String.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            await provider.GetAsync("email.reminder_body", lang),
            ev.Title, when, where);

        string body = string.IsNullOrWhiteSpace(ev.Body)
            ? prefix
            : prefix + " " + ev.Body;

        return (subject, body);
    }

    /// <summary>
    /// Renders <paramref name="instant"/> in the recipient's effective time
    /// zone + date-time format — the exact resolution order the <c>kw-dt</c>
    /// TagHelper uses (ADR 0019 / 0020): the recipient's <c>Profile.TimeZone</c>
    /// / <c>Profile.DateFormat</c> override → the platform default → the
    /// <c>UTC</c> / <see cref="DateFormat.FloorFormat"/> floor. The instant is
    /// converted to the zone's wall-clock <i>first</i>, then the format is
    /// applied with the invariant culture (the zone/format, not the host
    /// locale, are what the resident sees). Never throws — an unknown zone id
    /// or an unusable format degrades to the next tier.
    /// </summary>
    private static string FormatEventInstant(
        DateTimeOffset instant,
        string? profileZoneId, string defaultZoneId,
        string? profileFormat, string defaultFormat)
    {
        var zone = TryResolveZone(profileZoneId)
                  ?? TryResolveZone(defaultZoneId)
                  ?? FallbackUtc();

        string fmt;
        if (DateFormat.IsValid(profileFormat)) fmt = profileFormat!;
        else if (DateFormat.IsValid(defaultFormat)) fmt = defaultFormat;
        else fmt = DateFormat.FloorFormat;

        var utc = instant.UtcDateTime;
        var wallTime = utc + zone.GetUtcOffset(utc);
        return wallTime.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Convert an IANA id to a <see cref="TimeZoneInfo"/>, or
    /// <c>null</c> when the id is blank / not present on the OS (the
    /// <c>EffectiveTimezoneResolver.TryConvert</c> "fall through" rule —
    /// never a throw).</summary>
    private static TimeZoneInfo? TryResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return System.TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch
        {
            return null;
        }
    }

    private static TimeZoneInfo FallbackUtc() =>
        System.TimeZoneInfo.FindSystemTimeZoneById("UTC");
}
