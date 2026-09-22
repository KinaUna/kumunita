using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="EventReminderService"/> (§6.4 job) business-logic harness
/// (M4 U07; ADR 0054 §3.6). The shape follows
/// <see cref="SideEffectHarnessTests"/> (the <see cref="AuditPurgeService"/>
/// tiering tests) verbatim — the <b>Wolverine-free</b> static service is
/// exercised against a live <see cref="IDocumentStore"/> (fresh scratch
/// Postgres per test via <see cref="PostgresFixture"/>) with a
/// <b>frozen</b> <see cref="IMailerStage"/> stand-in that <em>records</em> the
/// staged <c>OutboxEmail</c> rows (key + recipient) rather than dispatching
/// over SMTP — the §6.2 durable handler is a Web-host concern (U08) and is
/// not exercised here.
/// <para>
/// The pins this lane owns (the 5 names T19–T23 from the §3.7 master list):
/// </para>
/// <list type="number">
/// <item><b>Window</b> — an event <c>25 h</c> out (outside <c>now &lt; Start ≤
///       now+24 h</c>) is not reminded; one <c>23 h</c> out (inside) is.</item>
/// <item><b>Going-only</b> — a <c>Maybe</c> / <c>No</c> RSVP is not reminded;
///       a <c>Going</c> RSVP is.</item>
/// <item><b>Author always</b> — the author is reminded even without a
///       <c>Going</c> RSVP (even when the author's own RSVP is <c>No</c>).</item>
/// <item><b>Key shape</b> — exactly one staged email per (event, recipient),
///       keyed <c>remind:{eventId}:{userId}</c>.</item>
/// <item><b>No audit row</b> — zero <see cref="AccessAudit"/> rows after
///       <c>SendRemindersAsync</c> (a side effect, not an access decision —
///       the verification-email posture).</item>
/// </list>
/// </summary>
public class EventReminderServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── T19 — M4_ReminderWindowFiltersOutsideEvents ───────────────────────
    //
    // Two in-scope events: one 25 h out (outside the 24 h window), one 23 h
    // out (inside). Only the 23 h event's recipients are reminded.

    [Fact]
    public async Task M4_ReminderWindowFiltersOutsideEvents()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string authorInside = "u-u07-w-in";
        const string authorOutside = "u-u07-w-out";

        await PlantProfile(store, authorInside, "in@kumunita");
        await PlantProfile(store, authorOutside, "out@kumunita");
        await PlantEvent(store, "w-in", authorInside, now.AddHours(23));
        await PlantEvent(store, "w-out", authorOutside, now.AddHours(25));

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        // 23 h out (inside the window) → the author is reminded.
        Assert.Contains(staged, s => s.Key == $"remind:w-in:{authorInside}");
        // 25 h out (outside the window) → nothing is staged for it.
        Assert.DoesNotContain(staged, s => s.Key == $"remind:w-out:{authorOutside}");
        Assert.DoesNotContain(staged, s => s.Key.StartsWith("remind:w-out:"));
    }

    // ── T20 — M4_ReminderGoingRsvpsOnly ───────────────────────────────────
    //
    // One in-window event. The author (no RSVP), a Going RSVP, a Maybe RSVP,
    // and a No RSVP. Only the author + the Going RSVP are reminded; the
    // Maybe / No residents are not.

    [Fact]
    public async Task M4_ReminderGoingRsvpsOnly()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string author = "u-u07-g-author";
        const string goer = "u-u07-g-go";
        const string maybe = "u-u07-g-maybe";
        const string no = "u-u07-g-no";

        foreach (var (id, email) in new[] { (author, "a@kumunita"), (goer, "g@kumunita"), (maybe, "m@kumunita"), (no, "n@kumunita") })
            await PlantProfile(store, id, email);

        await PlantEvent(store, "g-ev", author, now.AddHours(12));
        await PlantRsvp(store, "g-ev", goer, RsvpStatus.Going);
        await PlantRsvp(store, "g-ev", maybe, RsvpStatus.Maybe);
        await PlantRsvp(store, "g-ev", no, RsvpStatus.No);

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        Assert.Contains(staged, s => s.Key == $"remind:g-ev:{goer}");          // Going → reminded
        Assert.Contains(staged, s => s.Key == $"remind:g-ev:{author}");        // author always (T21)
        Assert.DoesNotContain(staged, s => s.Key == $"remind:g-ev:{maybe}");   // Maybe → not
        Assert.DoesNotContain(staged, s => s.Key == $"remind:g-ev:{no}");      // No → not
    }

    // ── T21 — M4_ReminderAuthorAlwaysIncluded ─────────────────────────────
    //
    // The author's own RSVP is a <c>No</c> (a non-Going status) — the author
    // is still reminded (the "author, always" rule is independent of the
    // Going filter).

    [Fact]
    public async Task M4_ReminderAuthorAlwaysIncluded()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string author = "u-u07-a-author";
        await PlantProfile(store, author, "author@kumunita");

        await PlantEvent(store, "a-ev", author, now.AddHours(10));
        // The author RSVPed "No" — but the author is still a reminder recipient.
        await PlantRsvp(store, "a-ev", author, RsvpStatus.No);

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        Assert.Contains(staged, s => s.Key == $"remind:a-ev:{author}");
        // Exactly one row for the author (no double-send via the Going path).
        Assert.Single(staged, s => s.Key == $"remind:a-ev:{author}");
    }

    // ── T22 — M4_ReminderIdempotencyKeyShape ──────────────────────────────
    //
    // One in-window event, author + one Going RSVP (both with emails). Exactly
    // two emails are staged, one per (event, recipient), keyed exactly
    // <c>remind:{eventId}:{userId}</c>.

    [Fact]
    public async Task M4_ReminderIdempotencyKeyShape()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string author = "u-u07-k-author";
        const string goer = "u-u07-k-goer";
        await PlantProfile(store, author, "ka@kumunita");
        await PlantProfile(store, goer, "kg@kumunita");

        await PlantEvent(store, "k-ev", author, now.AddHours(5));
        await PlantRsvp(store, "k-ev", goer, RsvpStatus.Going);

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        // Exactly one staged email per (event, recipient); the key is the
        // §6.2 per-email shape remind:{eventId}:{userId}.
        Assert.Equal(2, staged.Count);
        Assert.Contains(staged, s => s.Key == "remind:k-ev:u-u07-k-author");
        Assert.Contains(staged, s => s.Key == "remind:k-ev:u-u07-k-goer");
        // The recipients resolve to the planted profile emails.
        Assert.Contains(staged, s => s.Recipient == "ka@kumunita");
        Assert.Contains(staged, s => s.Recipient == "kg@kumunita");
    }

    // ── T23 — M4_ReminderWritesNoAccessAuditRow ───────────────────────────
    //
    // After a reminder run, zero <see cref="AccessAudit"/> rows — the
    // reminder is a side effect, not an access decision (ADR 0054 §3.6).

    [Fact]
    public async Task M4_ReminderWritesNoAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string author = "u-u07-n-author";
        const string goer = "u-u07-n-goer";
        await PlantProfile(store, author, "na@kumunita");
        await PlantProfile(store, goer, "ng@kumunita");

        await PlantEvent(store, "n-ev", author, now.AddHours(8));
        await PlantRsvp(store, "n-ev", goer, RsvpStatus.Going);

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        // The reminder actually staged emails (the test is not vacuous).
        Assert.Equal(2, staged.Count);

        // ...and wrote NO AccessAudit row (the no-audit-row pin).
        var audit = await AuditRows(store);
        Assert.Empty(audit);
    }
    // ── ADR 0019 / 0020 follow-on pin (not a renumber of the frozen M4 master\r\n    //    list — T24 is `M4_GlobalAdminOverrideEditEndToEnd` in EventServiceTests) ──\r\n    //     M4_ReminderWhenUsesRecipientTimezoneAndFormat ───────────────────
    //
    // The reminder's "when" renders in the *recipient's* own time zone +
    // date-time format (ADR 0019 / 0020, the kw-dt resolution order). A
    // recipient with a Europe/Paris override sees the event instant shifted
    // to Paris wall-clock; a recipient with no override resolves the platform
    // default (the UTC zone + the FloorFormat floor, here). In both cases the
    // literal "'UTC'" label the pre-ADR-0019 code emitted is gone.

    [Fact]
    public async Task M4_ReminderWhenUsesRecipientTimezoneAndFormat()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();

        const string paris = "u-u07-pz-paris";
        const string plain = "u-u07-pz-plain";
        await PlantProfile(store, paris, "pz-paris@kumunita",
            timeZone: "Europe/Paris", dateFormat: "yyyy-MM-dd HH:mm");
        await PlantProfile(store, plain, "pz-plain@kumunita");

        var start = now.AddMinutes(30);   // in-window; the "when" instant
        await PlantEvent(store, "pz-ev", paris, start);
        await PlantRsvp(store, "pz-ev", plain, RsvpStatus.Going);

        await EventReminderService.SendRemindersAsync(store, new EventReminderOptions(), now, mailer, Localization());

        // The UTC wall time of the event instant = the plain recipient's view
        // (the platform default here is the UTC zone); the Paris recipient's
        // view is that instant in Europe/Paris wall-clock.
        var utc = start.UtcDateTime;
        var utcTime = utc.ToString("HH:mm");
        var parisTime = (utc + TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris").GetUtcOffset(utc)).ToString("HH:mm");

        // Paris recipient (Europe/Paris override): sees Paris wall-clock,
        // never the UTC wall time, never a "'UTC'" label.
        var parisBody = staged.Single(s => s.Key == $"remind:pz-ev:{paris}").Body;
        Assert.Contains(parisTime, parisBody);
        Assert.DoesNotContain(utcTime, parisBody);
        Assert.DoesNotContain("UTC", parisBody);

        // Plain recipient (no override): the platform default (UTC zone) → the
        // UTC wall time; still no "'UTC'" label.
        var plainBody = staged.Single(s => s.Key == $"remind:pz-ev:{plain}").Body;
        Assert.Contains(utcTime, plainBody);
        Assert.DoesNotContain("UTC", plainBody);
    }
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot the scratch store with the M1/M3/M4 surfaces (the
    /// <see cref="EventServiceTests"/> shape) so the <see cref="Event"/> /
    /// <see cref="EventRsvp"/> / <see cref="Profile"/> / <see cref="OutboxEmail"/> /
    /// <see cref="AccessAudit"/> tables exist.</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>A <b>frozen</b> <see cref="IMailerStage"/> stand-in that records the
    /// staged (idempotency key, recipient) pairs. <c>IMailerStage</c> itself is
    /// untouched — this is a test double for its one method.</summary>
    private static (IMailerStage mailer, List<(string Key, string Recipient, string Body)> staged) RecordingMailer()
    {
        var staged = new List<(string Key, string Recipient, string Body)>();
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[4]));
                return Task.CompletedTask;
            });
        return (mailer, staged);
    }

    private static async Task PlantProfile(IDocumentStore store, string subjectId, string email,
        string? timeZone = null, string? dateFormat = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Profile
        {
            SubjectId = subjectId,
            DisplayName = subjectId,
            Verified = true,
            Email = email,
            TimeZone = timeZone,
            DateFormat = dateFormat,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>A <see cref="ILocalizationService"/> stand-in returning the
    /// ADR 0019 / 0020 floors (the UTC zone id + the <see cref="DateFormat
    /// .FloorFormat"/> floor) — the platform defaults the reminder's "when"
    /// falls back to when the recipient has no personal override.</summary>
    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("UTC");
        localization.GetDefaultDateFormatAsync().Returns(DateFormat.FloorFormat);
        return localization;
    }

    private static async Task PlantEvent(IDocumentStore store, string id, string authorId, DateTimeOffset start)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Event
        {
            Id = id,
            AuthorId = authorId,
            Title = "Cleanup day",
            Body = "body",
            Start = start,
            End = start.AddHours(4),
            ReminderEnabled = true,
            IsDraft = false,
            IsDeleted = false,
        });
        await w.SaveChangesAsync(ct);
    }

    private static async Task PlantRsvp(IDocumentStore store, string eventId, string userId, RsvpStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new EventRsvp { Id = $"rsvp-{eventId}-{userId}", EventId = eventId, UserId = userId, Status = status, At = DateTimeOffset.UtcNow });
        await w.SaveChangesAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>().ToListAsync(ct);
    }
}
