using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M20 (ADR 0121, D5) — the <see cref="NotificationFlushService"/> (§6.4
/// <c>NotificationFlush</c> job) business-logic harness. The shape follows
/// <see cref="EventReminderServiceTests"/> verbatim — the
/// <b>Wolverine-free</b> static flush service is exercised against a live
/// <see cref="IDocumentStore"/> (fresh scratch Postgres per test via
/// <see cref="PostgresFixture"/>) with a <b>frozen</b>
/// <see cref="IMailerStage"/> stand-in that <em>records</em> the staged
/// (idempotency key, recipient) pairs rather than dispatching over SMTP — the
/// §6.4 durable handler (<c>NotificationFlushHandler</c>, U05) is a Web-host
/// concern and is not exercised here.
/// <para>
/// The two authoritative pins this unit owns (the GATE-4 set):
/// </para>
/// <list type="number">
/// <item><b>Cleared row → staged exactly once, then idempotent</b> — a held
///       (<see cref="Notification.EmailDeferred"/>) row for a recipient with
///       <i>no</i> quiet schedule (the C-M20·3 floor — never quiet) is released:
///       the flush stages the held email under the
///       <see cref="NotificationService.DeferredKey"/> <i>once</i> and flips the
///       row's <c>EmailDeferred = false</c>. A <b>second</b> flush run stages
///       nothing (the row is no longer in the pending set) — the email is
///       delivered exactly once (C-M20·4).</item>
/// <item><b>Still-quiet row stays deferred across runs</b> — a held row for a
///       recipient whose <see cref="QuietScheduleMode.Blocked"/> schedule is
///       quiet at <i>every</i> instant (empty <c>Hours</c> = all hours, empty
///       <c>DaysOfWeek</c> = all days) is <b>not</b> released: the flush stages
///       nothing, and the row stays <c>EmailDeferred = true</c> across two
///       consecutive runs (the re-check-next-run contract, C-M20·4).</item>
/// </list>
/// <para>
/// Neither pin asserts an <see cref="AccessAudit"/> row — a flush is a side
/// effect (releasing a held email), not an access decision (the
/// <see cref="Kumunita.Core.Events.EventReminderService"/> posture;
/// C-M20·5 — zero new authorization surface).
/// </para>
/// </summary>
public class NotificationFlushServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── GATE-4 / pin 1 — Flush_Cleared_Row_Stages_One_Email_Flips_Deferred ──
    //
    // A held row for a recipient with NO quiet schedule (the floor: never
    // quiet). The flush releases it: stages the held email under the deferred
    // idempotency key exactly ONCE and flips EmailDeferred = false. A second
    // run is a no-op (the row is cleared) — the email is delivered exactly
    // once.

    [Fact]
    public async Task Flush_Cleared_Row_Stages_One_Email_Flips_Deferred()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();
        var userInfo = StubUserInfo();
        var localization = Localization();

        const string recipient = "u-u04-cleared";
        const string sourceId = "reply-g4";
        // No NotificationQuietSchedule planted → the C-M20·3 floor (never
        // quiet) → the release path.
        await PlantNotification(store, recipient,
            kind: NotificationKinds.PostReply,
            sourceId: sourceId,
            subject: "Reply to your post",
            body: "Someone replied.",
            emailDeferred: true);
        // The recipient's delivery address (read through the frozen seam).
        userInfo.GetProfileAsync(recipient).Returns(new Profile
        {
            SubjectId = recipient,
            DisplayName = recipient,
            Verified = true,
            Email = "cleared@kumunita",
        });

        // ── First flush run: the row is cleared (no longer quiet) → released.
        var delivered = await NotificationFlushService.FlushDeferredAsync(
            store, now, mailer, userInfo, localization,
            translationProvider: null,
            ct: TestContext.Current.CancellationToken);

        // Exactly one email, to the planted address, under the DEFERRED key
        // (U03's DeferredKey — distinct from the emit-time key).
        Assert.Equal(1, delivered);
        Assert.Single(staged);
        var key = NotificationService.DeferredKey(NotificationKinds.PostReply, sourceId);
        Assert.Equal(key, staged[0].Key);
        Assert.Equal("notification:post.reply:reply-g4:deferred", key);
        Assert.Equal("cleared@kumunita", staged[0].Recipient);
        Assert.Equal("Reply to your post", staged[0].Subject);
        Assert.Equal("Someone replied.", staged[0].Body);

        // The row's deferred flag is flipped off (cleared — the durable record
        // is now "email delivered").
        var afterFirst = await LoadNotification(store, recipient);
        Assert.NotNull(afterFirst);
        Assert.False(afterFirst!.EmailDeferred);

        // ── Second flush run: nothing is pending → a no-op, nothing restaged.
        var deliveredSecond = await NotificationFlushService.FlushDeferredAsync(
            store, now, mailer, userInfo, localization,
            translationProvider: null,
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(0, deliveredSecond);
        // Still exactly one email total (idempotent — no double-delivery).
        Assert.Single(staged);
    }

    // ── GATE-4 / pin 2 — Flush_Still_Quiet_Row_Stays_Deferred_Across_Two_Runs ─
    //
    // A held row for a recipient whose Blocked schedule is quiet at EVERY
    // instant (empty Hours = all hours; empty DaysOfWeek = all days). The
    // flush does NOT release it: stages nothing, and the row stays
    // EmailDeferred = true across two consecutive runs (re-checked next run).

    [Fact]
    public async Task Flush_Still_Quiet_Row_Stays_Deferred_Across_Two_Runs()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var (mailer, staged) = RecordingMailer();
        var userInfo = StubUserInfo();
        var localization = Localization();

        const string recipient = "u-u04-quiet";
        const string sourceId = "reply-g5";
        await PlantNotification(store, recipient,
            kind: NotificationKinds.PostReply,
            sourceId: sourceId,
            subject: "Reply to your post",
            body: "Someone replied.",
            emailDeferred: true);
        // The recipient's delivery address (read through the frozen seam).
        userInfo.GetProfileAsync(recipient).Returns(new Profile
        {
            SubjectId = recipient,
            DisplayName = recipient,
            Verified = true,
            Email = "quiet@kumunita",
        });
        // A Blocked schedule that is quiet at EVERY instant (empty Hours = all
        // hours; empty DaysOfWeek = all days; Enabled = on). Quiet at any run
        // instant → never released by the flush.
        await PlantAlwaysQuietSchedule(store, recipient);

        // ── First flush run: still quiet → NOT released.
        var deliveredFirst = await NotificationFlushService.FlushDeferredAsync(
            store, now, mailer, userInfo, localization,
            translationProvider: null,
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(0, deliveredFirst);
        Assert.Empty(staged);

        var afterFirst = await LoadNotification(store, recipient);
        Assert.NotNull(afterFirst);
        Assert.True(afterFirst!.EmailDeferred);   // stays deferred

        // ── Second flush run: still quiet → still NOT released.
        var deliveredSecond = await NotificationFlushService.FlushDeferredAsync(
            store, now, mailer, userInfo, localization,
            translationProvider: null,
            ct: TestContext.Current.CancellationToken);
        Assert.Equal(0, deliveredSecond);
        Assert.Empty(staged);

        var afterSecond = await LoadNotification(store, recipient);
        Assert.NotNull(afterSecond);
        Assert.True(afterSecond!.EmailDeferred);   // still deferred across runs
    }
    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot the scratch store with the M1/M3/M6 surfaces so the
    /// <see cref="Notification"/> / <see cref="NotificationQuietSchedule"/> /
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
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>A <b>frozen</b> <see cref="IMailerStage"/> stand-in that records
    /// the staged (idempotency key, recipient, subject, body) tuples.</summary>
    private static (IMailerStage mailer, List<(string Key, string Recipient, string Subject, string Body)> staged) RecordingMailer()
    {
        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
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
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });
        return (mailer, staged);
    }

    /// <summary>A <b>frozen</b> <see cref="IUserInfoService"/> stand-in; tests
    /// stub the <c>GetProfileAsync</c> return per recipient (the delivery
    /// address the flush reads for the held email).</summary>
    private static IUserInfoService StubUserInfo()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        return userInfo;
    }

    /// <summary>A <b>frozen</b> <see cref="ILocalizationService"/> stand-in
    /// returning the ADR 0019 floor (the UTC zone id) — the platform-default
    /// tier the ADR 0019 zone chain falls back to when the recipient has no
    /// personal <c>Profile.TimeZone</c> override.</summary>
    private static ILocalizationService Localization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("UTC");
        return localization;
    }

    /// <summary>Plant a <see cref="Notification"/> row directly (test fixture
    /// seeding — not a service write seam).</summary>
    private static async Task PlantNotification(
        IDocumentStore store, string recipientId,
        string kind, string? sourceId, string? subject, string? body, bool emailDeferred)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Notification
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientId = recipientId,
            Kind = kind,
            IdempotencyKey = $"notification:{kind}:{sourceId}",
            SourceId = sourceId,
            Subject = subject,
            Body = body,
            Created = DateTimeOffset.UtcNow,
            EmailDeferred = emailDeferred,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Plant a <see cref="NotificationQuietSchedule"/> row for the
    /// recipient: an "always quiet" Blocked schedule (empty <c>Hours</c> = all
    /// hours, empty <c>DaysOfWeek</c> = all days; <c>Enabled</c>=on) — quiet at
    /// any run instant, so the flush never releases a held row for this
    /// recipient.</summary>
    private static async Task PlantAlwaysQuietSchedule(IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new NotificationQuietSchedule
        {
            RecipientId = recipientId,
            Enabled = true,
            Mode = QuietScheduleMode.Blocked,
            Hours = [],
            DaysOfWeek = [],
            Updated = DateTimeOffset.UtcNow,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Load the recipient's <see cref="Notification"/> row (one held
    /// row per (recipient, source) in these tests).</summary>
    private static async Task<Notification?> LoadNotification(IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId)
            .FirstOrDefaultAsync(ct);
    }
}
