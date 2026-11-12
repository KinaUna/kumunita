using Kumunita.Core.Identity;
using Kumunita.Core.InstanceStatus;
using Kumunita.Core.Usage;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests.InstanceStatus;

/// <summary>
/// Unit tests for the <see cref="InstanceStatusService"/> (IMPROVE-UX O01,
/// the operator's read-only instance-status seam). The seams are
/// NSubstituted (the <see cref="Kumunita.Web.Tests.HealthControllerTests"/> /
/// <see cref="Kumunita.Core.Tests.AdminOnboardingServiceTests"/> "missing
/// store" idiom); no <c>PostgresFixture</c> is needed because the
/// <see cref="InstanceStatusService"/> is a read-only composer and its
/// degradation logic (a failing sub-read degrades that section, not the whole
/// read) is the load-bearing behavior under test.
/// <para>
/// Pins:
/// <list type="bullet">
/// <item>(a) the happy read returns the composed POCO (DB ok, SMTP ok,
/// mail-alarm via the reader seam, storage snapshot, version/uptime) and
/// calls each reused seam at most once (the "reuse, don't re-derive" pin);</item>
/// <item>(b) a sub-read failure (mail-alarm reader throws / SMTP throws /
/// storage throws / DB down) degrades that section to a fact, the read still
/// returns (the "resilient degradation" DoD);</item>
/// <item>(c) the returned POCO carries no secret — a set credential/token
/// input never appears in the result (the "no secret values" DoD).</item>
/// </list>
/// </para>
/// <para>
/// Note: <c>UserManager&lt;User&gt;</c> is a concrete class with no parameterless
/// ctor; NSubstitute cannot proxy it, so the tests pass <c>null!</c> — the
/// service's <c>ReadAdminExistsAsync</c> try/catch degrades to
/// <c>false</c> (no admin exists), which is the correct shape for a test
/// construction with no usable userManager (the
/// <see cref="Kumunita.Core.Tests.AdminOnboardingServiceTests"/> "missing
/// store" idiom). The SeedAdmin warning rule requires both
/// <c>SeedAdminCredentialsSet</c> AND <c>AdminExists</c>, so the admin-exists
/// degradation is the only observable effect.
/// </para>
/// </summary>
public class InstanceStatusServiceTests
{
    private const string SecretSeedAdminEmail = "admin@secret.example";
    private const string SecretSeedAdminToken = "super-secret-seed-token-0123456789abcdef";
    private const string SecretHealthToken = "super-secret-health-token-0123456789abcdef";

    // ── (a) happy read: composed POCO + each seam called at most once ────

    [Fact]
    public async Task GetStatus_HappyRead_ReturnsComposedPoco_EachSeamCalledOnce()
    {
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        mailAlarmReader.ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new MailAlarmCounts(Bounces: 0, Complaints: 0, DeadLetters: 3, Firing: false));

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(SmtpHealthResult.Ok);

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes: 1_000, TotalVolumeBytes: 10_000,
                FreeVolumeBytes: 9_000, UserContentUsedBytes: 500,
                TotalUniqueFiles: 42, TotalDistinctUsers: 7,
                AsOf: DateTimeOffset.UtcNow));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: null, seedAdminToken: null,
            sampleDataEnabled: false,
            isProduction: true,
            healthToken: "some-token",
            dataProtectionPersisted: true);

        var status = await svc.GetStatusAsync(CancellationToken.None);

        // DB reachable, mail ok, mail-alarm ok, storage ok, version set, uptime non-negative.
        Assert.True(status.DatabaseReachable);
        Assert.True(status.Mail.Available);
        Assert.True(status.Mail.Reachable);
        Assert.Null(status.Mail.Reason);
        Assert.True(status.MailAlarm.Available);
        Assert.Equal(3, status.MailAlarm.DeadLetters);
        Assert.False(status.MailAlarm.Firing);
        Assert.True(status.Storage.Available);
        Assert.Equal(1_000, status.Storage.TotalUsedBytes);
        Assert.Equal(9_000, status.Storage.FreeVolumeBytes);
        Assert.False(string.IsNullOrWhiteSpace(status.Version));
        Assert.True(status.Uptime >= TimeSpan.Zero);

        // "Reuse, don't re-derive": each reused seam is called at most once.
        await mailAlarmReader.Received(1).ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await smtp.Received(1).CheckAsync(Arg.Any<CancellationToken>());
        await storage.Received(1).GetSnapshotAsync(Arg.Any<CancellationToken>());
    }

    // ── (b) degraded read: a failing sub-read degrades that section ──────

    [Fact]
    public async Task GetStatus_MailAlarmReaderThrows_MailAlarmDegrades_ReadStillReturns()
    {
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        mailAlarmReader.ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MailAlarmCounts>(new InvalidOperationException("no DB")));

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes: 1, TotalVolumeBytes: 2, FreeVolumeBytes: 1,
                UserContentUsedBytes: 0, TotalUniqueFiles: 0,
                TotalDistinctUsers: 0, AsOf: DateTimeOffset.UtcNow));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: null, seedAdminToken: null,
            sampleDataEnabled: false,
            isProduction: true,
            healthToken: "some-token",
            dataProtectionPersisted: true);

        // The read must still return (not throw).
        var status = await svc.GetStatusAsync(CancellationToken.None);

        // The mail-alarm reader threw → the mail-alarm section degrades to
        // Unavailable (a fact, not a throw).
        Assert.False(status.MailAlarm.Available);
        // The other sections are still available.
        Assert.True(status.DatabaseReachable);
        Assert.True(status.Mail.Available);
        Assert.True(status.Storage.Available);
    }

    [Fact]
    public async Task GetStatus_SmtpHealthThrows_MailDegrades_ReadStillReturns()
    {
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        mailAlarmReader.ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new MailAlarmCounts(Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false));

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SmtpHealthResult>(new InvalidOperationException("no relay")));

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes: 1, TotalVolumeBytes: 2, FreeVolumeBytes: 1,
                UserContentUsedBytes: 0, TotalUniqueFiles: 0,
                TotalDistinctUsers: 0, AsOf: DateTimeOffset.UtcNow));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: null, seedAdminToken: null,
            sampleDataEnabled: false,
            isProduction: true,
            healthToken: "some-token",
            dataProtectionPersisted: true);

        var status = await svc.GetStatusAsync(CancellationToken.None);

        // SMTP threw → the mail section degrades to Unavailable.
        Assert.False(status.Mail.Available);
        // The other sections are still available.
        Assert.True(status.DatabaseReachable);
        Assert.True(status.MailAlarm.Available);
        Assert.True(status.Storage.Available);
    }

    [Fact]
    public async Task GetStatus_StorageSnapshotThrows_StorageDegrades_ReadStillReturns()
    {
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        mailAlarmReader.ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new MailAlarmCounts(Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false));

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StorageMetricsSnapshot>(new InvalidOperationException("no store")));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: null, seedAdminToken: null,
            sampleDataEnabled: false,
            isProduction: true,
            healthToken: "some-token",
            dataProtectionPersisted: true);

        var status = await svc.GetStatusAsync(CancellationToken.None);

        // Storage threw → the storage section degrades to Unavailable.
        Assert.False(status.Storage.Available);
        // The other sections are still available.
        Assert.True(status.DatabaseReachable);
        Assert.True(status.Mail.Available);
        Assert.True(status.MailAlarm.Available);
    }

    [Fact]
    public async Task GetStatus_DbDown_DegradedDbSections_ReadStillReturns()
    {
        // The DB itself is down (AssertConnectivityAsync throws). The
        // DB-backed sections (mail-alarm, GlobalAdmin count) degrade in one
        // branch instead of each attempting a connection and each throwing.
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("no DB")));

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        // The mail-alarm reader is NOT called when the DB is down (the
        // service gates the DB-backed reads on dbReachable).

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes: 1, TotalVolumeBytes: 2, FreeVolumeBytes: 1,
                UserContentUsedBytes: 0, TotalUniqueFiles: 0,
                TotalDistinctUsers: 0, AsOf: DateTimeOffset.UtcNow));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: null, seedAdminToken: null,
            sampleDataEnabled: false,
            isProduction: true,
            healthToken: "some-token",
            dataProtectionPersisted: true);

        // The read must still return (not throw).
        var status = await svc.GetStatusAsync(CancellationToken.None);

        // DB down → the DB-backed sections degrade.
        Assert.False(status.DatabaseReachable);
        Assert.False(status.MailAlarm.Available);
        // The independent sections (SMTP, storage) are still available.
        Assert.True(status.Mail.Available);
        Assert.True(status.Storage.Available);
        // The mail-alarm reader is NOT called when the DB is down.
        await mailAlarmReader.DidNotReceive().ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    // ── (c) no-secret invariant ───────────────────────────────────────────

    [Fact]
    public async Task GetStatus_SecretsSet_ResultCarriesNoSecretValue()
    {
        // Even when the host has set a SeedAdmin email + token AND a Health
        // token, the result POCO carries only the boolean posture (the
        // ConfigWarning list), never the credential / token values.
        var store = Substitute.For<IDocumentStore>();
        store.Storage!.Database!.AssertConnectivityAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mailAlarmReader = Substitute.For<IMailAlarmStateReader>();
        mailAlarmReader.ReadAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new MailAlarmCounts(Bounces: 0, Complaints: 0, DeadLetters: 0, Firing: false));

        var smtp = Substitute.For<ISmtpHealthCheck>();
        smtp.CheckAsync(Arg.Any<CancellationToken>()).Returns(SmtpHealthResult.Ok);

        var storage = Substitute.For<IStorageMetricsService>();
        storage.GetSnapshotAsync(Arg.Any<CancellationToken>())
            .Returns(new StorageMetricsSnapshot(
                TotalUsedBytes: 1, TotalVolumeBytes: 2, FreeVolumeBytes: 1,
                UserContentUsedBytes: 0, TotalUniqueFiles: 0,
                TotalDistinctUsers: 0, AsOf: DateTimeOffset.UtcNow));

        var svc = BuildService(store, mailAlarmReader, smtp, storage, userManager: null!,
            seedAdminEmail: SecretSeedAdminEmail,
            seedAdminToken: SecretSeedAdminToken,
            sampleDataEnabled: true,
            isProduction: true,
            healthToken: SecretHealthToken,
            dataProtectionPersisted: false);

        var status = await svc.GetStatusAsync(CancellationToken.None);

        // The result must not carry the secret values anywhere.
        var serialized = System.Text.Json.JsonSerializer.Serialize(status);
        Assert.DoesNotContain(SecretSeedAdminEmail, serialized);
        Assert.DoesNotContain(SecretSeedAdminToken, serialized);
        Assert.DoesNotContain(SecretHealthToken, serialized);

        // The warnings fire: SampleData + DataProtection (2 — the SeedAdmin
        // rule does NOT fire because the userManager is null, so AdminExists
        // degrades to false, and the SeedAdmin rule requires both
        // SeedAdminSet AND AdminExists). Health token IS set so rule 3 does
        // not fire.
        Assert.Equal(2, status.Warnings.Count);
        Assert.True(status.Warnings.Any(w => w.Key == ConfigWarning.Keys.SampleDataInProduction));
        Assert.True(status.Warnings.Any(w => w.Key == ConfigWarning.Keys.DataProtectionNotPersisted));
        Assert.False(status.Warnings.Any(w => w.Key == ConfigWarning.Keys.SeedAdminStillSet));
        Assert.False(status.Warnings.Any(w => w.Key == ConfigWarning.Keys.HealthTokenUnset));
    }

    // ── helper ────────────────────────────────────────────────────────────

    private static InstanceStatusService BuildService(
        IDocumentStore store,
        IMailAlarmStateReader mailAlarmReader,
        ISmtpHealthCheck smtp,
        IStorageMetricsService storage,
        UserManager<User>? userManager,
        string? seedAdminEmail,
        string? seedAdminToken,
        bool sampleDataEnabled,
        bool isProduction,
        string? healthToken,
        bool dataProtectionPersisted)
    {
        return new InstanceStatusService(
            store: store,
            mailAlarmReader: mailAlarmReader,
            smtpHealth: smtp,
            storageMetrics: storage,
            userManager: userManager!,
            seedAdmin: Options.Create(new SeedAdminOptions
            {
                Email = seedAdminEmail,
                Token = seedAdminToken
            }),
            sampleData: Options.Create(new SampleDataOptions
            {
                Enabled = sampleDataEnabled
            }),
            health: Options.Create(new HealthOptions
            {
                Token = healthToken
            }),
            instanceStatus: Options.Create(new InstanceStatusOptions
            {
                IsProduction = isProduction,
                DataProtectionKeysPersisted = dataProtectionPersisted
            }));
    }
}
