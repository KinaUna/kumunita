using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// F17 / MAIL-03 — the pinned gate test <c>SuppressedAddress_NotMailed</c>
/// (pin: <c>docs/design/prod-hardening-f17-bounce.md</c> §4.1–§4.2, UNRENAMED).
/// <para>
/// Drives the **gate's behavior** (the read-only <c>LoadAsync&lt;SuppressedAddress&gt;</c>
/// + the <c>return notification</c> short-circuit at pin §2.1), not the Web ingest
/// route (pin §4.3 — a Core test because the gate is in
/// <see cref="NotificationService.EmitAsync"/>, Core). The three legs (pin
/// §4.2):
/// <ol>
/// <li>The email is **NOT** staged (<c>StageAsyncCount == 0</c>) — the gate's
///     <c>LoadAsync</c> returned the planted <see cref="SuppressedAddress"/>
///     row → the <c>return notification</c> short-circuited **before** the
///     <c>StageAsync</c> call.</li>
/// <li>The inbox row **IS** stored (query by <c>IdempotencyKey</c>) — D7, the
///     inbox is the durable record; the gate skips the email, not the row.</li>
/// <li>No <see cref="AccessAudit"/> row is written (pin §2.3 — the gate is a
///     pure read + return; the ADR 0078 / ADR 0084 gates write no audit row
///     either, and the F17 gate follows the same shape).</li>
/// </ol>
/// The pin §4.2 note: the fail-open / no-row path (a recipient with **no**
/// <c>SuppressedAddress</c> row → the email **is** sent) is the default and is
/// implicitly covered by every other <see cref="NotificationService"/> test;
/// it is not asserted here.
/// </para>
/// <para>
/// Harness (pin §4.2, grounded in the <see cref="NotificationServiceTests"/>
/// recording-mailer precedent): a real scratch-Postgres <see cref="IDocumentStore"/>
/// with <c>CoreDocTypes</c> + <c>PostDocTypes</c> + <c>NotificationDocTypes</c>
/// configured (the <see cref="SuppressedAddress"/> doc is on the
/// <c>NotificationDocTypes</c> surface — pin §1.1; the F16.U3 GOTCHA-1
/// precedent: a doc on a surface the boot store omits → unmanaged-table error),
/// a **recording** <see cref="IMailerStage"/> stand-in (counts calls, stores
/// nothing), a real (sealed) <see cref="NotificationService"/> over an NSubstitute
/// <c>IUserInfoService</c> (returns the planted <see cref="Profile"/>) + an
/// NSubstitute <c>ITranslationProvider</c> (fixed template strings), with
/// <c>SuppressForSampleAccountsInProduction = false</c> (the ADR 0078
/// sample-suppression gate is **disabled** — the test drives the **F17** gate,
/// not the ADR 0078 gate). A <see cref="SuppressedAddress"/> row is planted
/// for the recipient's e-mail.
/// </para>
/// </summary>
public class F17U3_SuppressionGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── SuppressedAddress_NotMailed — the pin's UNRENAMED gate test ─────────
    //
    // A recipient with a planted SuppressedAddress row: EmitAsync stores the
    // inbox row (D7) and returns before the StageAsync call — the email is
    // skipped. Three legs: (1) no StageAsync call; (2) the inbox row is
    // stored (queried by IdempotencyKey); (3) no AccessAudit row is written.

    [Fact]
    public async Task SuppressedAddress_NotMailed()
    {
        var (store, svc, mailer) = await BootAsync();

        const string recipient = "user-suppressed";
        const string email = "suppressed@example.com";
        await PlantProfile(store, recipient, email);

        // Plant a SuppressedAddress row for the recipient's e-mail (the
        // pin §4.2 harness step 2). The gate's LoadAsync(id) returns this →
        // the return-notification short-circuit fires before StageAsync.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new SuppressedAddress
            {
                Id = email,
                Reason = "bounce",
                Created = DateTimeOffset.UtcNow,
                Source = "test",
            });
            await w.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        const string key = "notification:group.post:test-suppressed";
        var row = await svc.EmitAsync(session, recipient,
            NotificationKinds.GroupPost, key, "A test post.",
            TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Leg 1 — the email is NOT staged (the gate fired before StageAsync).
        Assert.Equal(0, mailer.StageAsyncCount);

        // Leg 2 — the inbox row IS stored (D7; queried by IdempotencyKey —
        // LoadAsync loads by the document Id (a Guid), not the key).
        await using var q = store.QuerySession();
        var inbox = await q.Query<Notification>()
            .Where(n => n.IdempotencyKey == key)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(inbox);

        // Leg 3 — no AccessAudit row is written (pin §2.3 — the gate is a
        // pure read + return; the gate never writes an audit row).
        await using var qAudit = store.QuerySession();
        var suppressRows = await qAudit.Query<AccessAudit>()
            .Where(a => a.Action.Contains("suppress"))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(suppressRows);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Boots the scratch store with the M1 + M3 + M6 surfaces (the
    /// <see cref="NotificationServiceTests"/> shape — <c>NotificationDocTypes</c>
    /// carries the <see cref="SuppressedAddress"/> doc, pin §1.1) and builds
    /// a real <see cref="NotificationService"/> over the frozen seams with the
    /// ADR 0078 sample-suppression gate **disabled** (the test drives the F17
    /// gate, not the ADR 0078 gate). Returns the store, the service, and the
    /// recording mailer (for the <c>StageAsyncCount</c> leg).
    /// </summary>
    private async Task<(IDocumentStore store, NotificationService svc, RecordingMailer mailer)> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult($"tpl-{(string)ci[0]}"));

        var mailer = new RecordingMailer();

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(ci => PlantProfileReadAsync(store, (string)ci[0]));

        // SuppressForSampleAccountsInProduction = false (pin §4.2 — the ADR
        // 0078 gate is disabled; this test drives the F17 gate, not ADR 0078).
        var svc = new NotificationService(
            store, userInfo, translator, mailer,
            Options.Create(new NotificationOptions { SuppressForSampleAccountsInProduction = false }));
        return (store, svc, mailer);
    }

    /// <summary>
    /// A **recording** <see cref="IMailerStage"/> stand-in: counts the
    /// <c>StageAsync</c> calls (the leg-1 assert) and stores nothing — no
    /// outbox row, no envelope. The pin §4.2 "recording IMailerStage stub
    /// asserts 0 calls" shape.
    /// </summary>
    private sealed class RecordingMailer : IMailerStage
    {
        public int StageAsyncCount { get; private set; }

        public Task StageAsync(
            IDocumentSession session,
            string idempotencyKey,
            string recipient,
            string subject,
            string body,
            CancellationToken ct = default)
        {
            StageAsyncCount++;
            return Task.CompletedTask;
        }
    }

    private static async Task<Profile?> PlantProfileReadAsync(IDocumentStore store, string subjectId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.LoadAsync<Profile>(subjectId, ct);
    }

    private static async Task PlantProfile(IDocumentStore store, string subjectId, string email)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Profile
        {
            SubjectId = subjectId,
            DisplayName = subjectId,
            Verified = true,
            Email = email,
        });
        await w.SaveChangesAsync(ct);
    }
}
