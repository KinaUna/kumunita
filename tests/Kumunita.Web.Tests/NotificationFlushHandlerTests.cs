using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Kumunita.Web.SideEffects;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M20 (ADR 0121, D5) — the <see cref="NotificationFlushHandler"/> (U05) Web
/// host pin tests. Three pins per the register / plan:
/// <list type="number">
/// <item><b>Handler flushes + re-schedules at the resolved cadence</b> — a
///       held (<c>EmailDeferred == true</c>) row for a recipient with <i>no</i>
///       quiet schedule (the C-M20·3 floor) is released: the mailer records
///       the staged email under the deferred key, and the returned
///       <see cref="NotificationFlushTick"/> carries the resolved admin cadence
///       (30 min, set via the <see cref="ILocalizationService.GetQuietCheckMinutesAsync"/>
///       stand-in).</item>
/// <item><b>No-op flush still re-schedules</b> — no deferred rows: the
///       handler returns exactly one <see cref="NotificationFlushTick"/> with
///       the resolved cadence (45 min); the mailer records nothing.</item>
/// <item><b><c>Program.cs</c> seed line present</b> — the source contains
///       <c>PublishAsync(new NotificationFlushTick())</c> (the first-boot
///       seed).</item>
/// </list>
/// </summary>
public class NotificationFlushHandlerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Pin 1 — handler flushes + re-schedules at the resolved cadence ─────

    [Fact]
    public async Task Handle_With_Deferred_Row_Flushes_And_Reschedules_At_Resolved_Cadence()
    {
        var store = await BootStoreAsync();
        var (mailer, staged) = RecordingMailer();
        var userInfo = StubUserInfo();
        var localization = LocalizationWithCadence(30);
        var translationProvider = Substitute.For<ITranslationProvider>();

        const string recipient = "u-u05-pin1";
        const string sourceId = "reply-u05";
        const string email = "pin1@kumunita";

        // Plant a held (deferred) row for a recipient with NO quiet schedule
        // (the C-M20·3 floor: never quiet → the release path).
        await PlantNotification(store, recipient, sourceId, emailDeferred: true);
        userInfo.GetProfileAsync(recipient).Returns(new Profile
        {
            SubjectId = recipient,
            DisplayName = recipient,
            Verified = true,
            Email = email,
        });

        var tick = new NotificationFlushTick();
        var result = await NotificationFlushHandler.Handle(
            tick, store, mailer, userInfo, localization, translationProvider);

        // Exactly one re-schedule tick, at the RESOLVED cadence (30 min).
        Assert.Single(result);
        var re = Assert.IsType<NotificationFlushTick>(result.Single());
        Assert.Equal(TimeSpan.FromMinutes(30), re.DelayTime);

        // The held email was staged exactly once, under the deferred key.
        Assert.Single(staged);
        var expectedKey = NotificationService.DeferredKey(
            NotificationKinds.PostReply, sourceId);
        Assert.Equal(expectedKey, staged[0].Key);
        Assert.Equal("notification:post.reply:reply-u05:deferred", expectedKey);
        Assert.Equal(email, staged[0].Recipient);

        // The row's deferred flag is flipped off (released).
        var after = await LoadNotification(store, recipient);
        Assert.NotNull(after);
        Assert.False(after!.EmailDeferred);
    }

    // ── Pin 2 — no-op flush still re-schedules ─────────────────────────────

    [Fact]
    public async Task Handle_No_Deferred_Rows_Still_Reschedules()
    {
        var store = await BootStoreAsync();
        var (mailer, staged) = RecordingMailer();
        var userInfo = StubUserInfo();
        var localization = LocalizationWithCadence(45);
        var translationProvider = Substitute.For<ITranslationProvider>();

        // No deferred rows planted (or any rows at all).

        var tick = new NotificationFlushTick();
        var result = await NotificationFlushHandler.Handle(
            tick, store, mailer, userInfo, localization, translationProvider);

        // The job continues: exactly one re-schedule tick at the resolved
        // cadence (45 min).
        Assert.Single(result);
        var re = Assert.IsType<NotificationFlushTick>(result.Single());
        Assert.Equal(TimeSpan.FromMinutes(45), re.DelayTime);

        // Nothing was staged (no held rows to release).
        Assert.Empty(staged);
    }

    // ── Pin 3 — Program.cs seed line present ───────────────────────────────

    [Fact]
    public void Program_Cs_Contains_NotificationFlushTick_Seed()
    {
        var repoRoot = FindRepoRoot();
        var programPath = Path.Combine(repoRoot, "src", "Kumunita.Web", "Program.cs");
        Assert.True(File.Exists(programPath), $"Program.cs not found at {programPath}");

        var source = File.ReadAllText(programPath);
        Assert.Contains("PublishAsync(new NotificationFlushTick())", source);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
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
            null, null, ct);
        return store;
    }

    private static (IMailerStage mailer, List<(string Key, string Recipient, string Subject, string Body)> staged)
        RecordingMailer()
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
                staged.Add((
                    (string)callInfo[1],
                    (string)callInfo[2],
                    (string)callInfo[3],
                    (string)callInfo[4]));
                return Task.CompletedTask;
            });
        return (mailer, staged);
    }

    private static IUserInfoService StubUserInfo()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);
        return userInfo;
    }

    private static ILocalizationService LocalizationWithCadence(int minutes)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("UTC");
        localization.GetQuietCheckMinutesAsync().Returns(minutes);
        return localization;
    }

    private static async Task PlantNotification(
        IDocumentStore store, string recipientId, string sourceId, bool emailDeferred)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Notification
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientId = recipientId,
            Kind = NotificationKinds.PostReply,
            IdempotencyKey = $"notification:{NotificationKinds.PostReply}:{sourceId}",
            SourceId = sourceId,
            Subject = "Reply to your post",
            Body = "Someone replied.",
            Created = DateTimeOffset.UtcNow,
            EmailDeferred = emailDeferred,
        });
        await w.SaveChangesAsync(ct);
    }

    private static async Task<Notification?> LoadNotification(
        IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId)
            .FirstOrDefaultAsync(ct);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("Kumunita.slnx").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not find repo root (Kumunita.slnx not found).");
    }
}
