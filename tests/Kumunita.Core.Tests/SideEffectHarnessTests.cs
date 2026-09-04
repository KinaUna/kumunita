using System.Linq;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Plan M1 step 7 — the email-handler failure/retry/dead-letter harness (design
/// doc's "Part tests") + the <see cref="AuditPurgeService"/> tiering, both written
/// against the <em>Wolverine-free</em> business logic in <c>Kumunita.Core</c>.
/// <para>
/// The durable retry *policy* (6 attempts / ~24h via <c>RetryWithCooldown</c>) and the
/// <c>Fault&lt;OutboxEmail&gt;</c> → dead-letter handoff live in the Web host's
/// Wolverine handler (<c>Kumunita.Web/SideEffects/</c>). These tests pin the two
/// guarantees that policy delegates to, without a live message host:
/// </para>
/// <list type="number">
/// <item><see cref="EmailDeadLetterWriter"/> emits the exact <see cref="EmailDeadLetter"/>
/// row shape (§5: recipient, idempotency key, last error, attempt count) and commits it
/// in the session's transaction.</item>
/// <item>A failed send can <em>never</em> roll back a domain write that already
/// committed — the handoff test (IMailerStage.cs / ARCHITECTURE.md §6.1: "a failed SMTP
/// connection can never roll back or delay a domain write"). The write-path (stager) and
/// the read-path (handler) are separate transactions by construction; the test proves
/// the committed row survives the send failure.</item>
/// <item><see cref="AuditPurgeService"/> expires only the routine tier past the cutoff,
/// keeps the standing tier (moderator/admin/break-glass) indefinitely, and records one
/// <see cref="AuditPurgeSummary"/> audit-of-audit per run (§5).</item>
/// </list>
/// </summary>
public class SideEffectHarnessTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Dead-letter row shape + same-transaction commit ──────────────────

    [Fact]
    public async Task Write_EmailDeadLetter_StoresRowWithKeyRecipientAttemptsAndTimestamps()
    {
        var store = await BootStoreAsync();
        await using var session = store.OpenSession(new SessionOptions());

        var origin = new OutboxEmail
        {
            Id = "omsg-1",
            IdempotencyKey = "verify:u-1:1",
            Recipient = "resident@kumunita",
            Subject = "Verify your account",
            Body = "link",
            QueuedAt = DateTimeOffset.UtcNow
        };
        var before = DateTimeOffset.UtcNow;

        var row = EmailDeadLetterWriter.Write(session, origin, lastError: "SMTP 550: relay unavailable", attempts: EmailDeadLetterWriter.MaxAttempts, ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        var saved = await session.LoadAsync<EmailDeadLetter>(row.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal("verify:u-1:1", saved.IdempotencyKey);
        Assert.Equal("resident@kumunita", saved.Recipient);
        Assert.Equal("Verify your account", saved.Subject);
        Assert.Equal("SMTP 550: relay unavailable", saved.LastError);
        Assert.Equal(6, saved.Attempts);
        Assert.True(saved.CreatedAt >= before, "CreatedAt must reflect the failure time");
        Assert.True(saved.DeadAt >= before, "DeadAt must reflect the failure time");
    }

    [Fact]
    public async Task Write_EmailDeadLetter_CommitsAtomicallyWithACoStagedDocument()
    {
        var store = await BootStoreAsync();
        await using var session = store.OpenSession(new SessionOptions());

        // The dead-letter row and an unrelated staged document commit in ONE SaveChangesAsync
        // (the "same transaction" the design doc's same-transaction guarantee relies on).
        session.Store(new Profile
        {
            SubjectId = "u-co",
            DisplayName = "Co Stored",
            Email = "co@kumunita",
            Verified = true,
            Visibility = new Audience()
        });
        var origin = new OutboxEmail { Id = "omsg-2", IdempotencyKey = "k-2", Recipient = "r@k", Subject = "s", Body = "b" };
        var row = EmailDeadLetterWriter.Write(session, origin, null, EmailDeadLetterWriter.MaxAttempts, ct: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(await session.LoadAsync<Profile>("u-co", TestContext.Current.CancellationToken));
        Assert.NotNull(await session.LoadAsync<EmailDeadLetter>(row.Id, TestContext.Current.CancellationToken));
    }

    // ── THE handoff test: a failed send never rolls back a committed domain write ─

    [Fact]
    public async Task FailedSend_NeverRollsBackTheCommittedDomainWrite_TheHandoff()
    {
        var store = await BootStoreAsync();
        var subjectId = "u-resident-1";
        var email = "resident@kumunita";

        // 1. Write-path (step-6 stager contract): the domain write + the staged
        //    OutboxEmail commit atomically in the caller's session (invariant C3).
        await using (var staging = store.OpenSession(new SessionOptions()))
        {
            staging.Store(new Profile
            {
                SubjectId = subjectId,
                DisplayName = "Resident",
                Email = email,
                Verified = false,          // unverified: this verification email is the handoff
                Visibility = new Audience()
            });
            staging.Store(new OutboxEmail
            {
                Id = "omsg-verify-1",
                IdempotencyKey = $"verify:{subjectId}:1",
                Recipient = email,
                Subject = "Verify your Kumunita account",
                Body = "link",
                QueuedAt = DateTimeOffset.UtcNow
            });
            await staging.SaveChangesAsync(TestContext.Current.CancellationToken);   // committed — the domain write is live
        }

        // 2. Read-path (step-7 handler): a delivery attempt fails. SmtpSender with no
        //    configured host throws — exactly the "SMTP down" signal the §6 dead-letter
        //    path is designed to surface (a real relay reset would give SmtpException
        //    instead; the handler's retry policy is indifferent to which).
        var sender = new SmtpSender(
            Options.Create(new SmtpOptions()),      // Host unset → unconfigured
            NullLogger<SmtpSender>.Instance);
        var stagedAgain = new OutboxEmail
        {
            Id = "omsg-verify-1",
            IdempotencyKey = $"verify:{subjectId}:1",
            Recipient = email,
            Subject = "Verify your Kumunita account",
            Body = "link"
        };
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(stagedAgain, TestContext.Current.CancellationToken));

        // 3. THE guarantee: the failed send is a *separate, later* transaction and cannot
        //    roll back step 1's commit. The profile and the staged outbox row are intact.
        await using var check = store.OpenSession(new SessionOptions());
        Assert.NotNull(await check.LoadAsync<Profile>(subjectId, TestContext.Current.CancellationToken));         // domain write survived the failed send
        var committedMsg = await check.LoadAsync<OutboxEmail>("omsg-verify-1", TestContext.Current.CancellationToken);
        Assert.NotNull(committedMsg);                                      // durable outbox row survived the failed send

        // 4. On final failure the handler writes the dead-letter (a further separate tx)
        //    — the operator's /health degraded signal — while the domain write stays the truth.
        var dead = await EmailDeadLetterWriter.WriteAndCommitAsync(check, committedMsg, failure.Message, EmailDeadLetterWriter.MaxAttempts, ct: TestContext.Current.CancellationToken);
        Assert.Equal($"verify:{subjectId}:1", dead.IdempotencyKey);
        Assert.NotNull(await check.LoadAsync<Profile>(subjectId, TestContext.Current.CancellationToken));        // domain write STILL present after dead-lettering
    }

    // ── SmtpSender contract (no Postgres needed) ─────────────────────────

    [Fact]
    public async Task SendAsync_UnconfiguredHost_ThrowsRatherThanSilentlyDropping()
    {
        var sender = new SmtpSender(Options.Create(new SmtpOptions()), NullLogger<SmtpSender>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new OutboxEmail { Recipient = "a@b.c", Subject = "s", Body = "b" }, TestContext.Current.CancellationToken));

        // Actionable pointer (OPS.md §2): no silent "send to nowhere" that would hide the
        // dead-letter signal /health is designed to surface.
        Assert.Contains("SMTP__Host", ex.Message);
    }

    [Fact]
    public async Task SendAsync_EmptyRecipient_Throws()
    {
        var sender = new SmtpSender(Options.Create(new SmtpOptions { Host = "mail.example" }), NullLogger<SmtpSender>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(
            () => sender.SendAsync(new OutboxEmail { Recipient = "", Subject = "s", Body = "b" }, TestContext.Current.CancellationToken));
    }

    // ── AuditPurge tiering (§5 retention) ────────────────────────────────

    [Fact]
    public async Task AuditPurge_ExpiresOnlyRoutineRowsPastCutoff_KeepsStanding_WritesSummary()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;
        var expectedCutoff = now.AddDays(-90);

        await using (var s = store.OpenSession(new SessionOptions()))
        {
            // Routine tier, OLD (past the ~90d cutoff) — MUST be purged.
            s.Store(AuditRow("routine-old-1", AccessVia.Owner, now.AddDays(-120)));
            s.Store(AuditRow("routine-old-2", AccessVia.Delegation, now.AddDays(-91)));
            // Routine tier, NEW — kept.
            s.Store(AuditRow("routine-new-1", AccessVia.Audience, now.AddDays(-10)));
            // Standing tier (moderator / admin / break-glass), OLD — kept indefinitely.
            s.Store(AuditRow("admin-old-1", AccessVia.Admin, now.AddDays(-400)));
            s.Store(AuditRow("mod-old-1", AccessVia.Moderator, now.AddDays(-300)));
            s.Store(AuditRow("breakglass-old-1", AccessVia.BreakGlass, now.AddDays(-500)));
            await s.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var summary = await AuditPurgeService.PurgeAsync(store, new AuditPurgeOptions { RoutineDays = 90 }, now, TestContext.Current.CancellationToken);

        // Only the two routine-old rows are deleted; standing + recent survive.
        Assert.Equal(2L, summary.Count);
        Assert.Equal(expectedCutoff, summary.Cutoff);

        await using var check = store.OpenSession(new SessionOptions());
        Assert.Null(await check.LoadAsync<AccessAudit>("routine-old-1", TestContext.Current.CancellationToken));
        Assert.Null(await check.LoadAsync<AccessAudit>("routine-old-2", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AccessAudit>("routine-new-1", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AccessAudit>("admin-old-1", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AccessAudit>("mod-old-1", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AccessAudit>("breakglass-old-1", TestContext.Current.CancellationToken));

        // Audit-of-audit: the summary row itself committed (the "deletion is logged" half).
        var saved = await check.LoadAsync<AuditPurgeSummary>(summary.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(2L, saved.Count);
        Assert.Equal(expectedCutoff, saved.Cutoff);
    }

    [Fact]
    public async Task AuditPurge_NothingToExpire_StillWritesASummaryWithZeroCount()
    {
        var store = await BootStoreAsync();
        var now = DateTimeOffset.UtcNow;

        await using (var s = store.OpenSession(new SessionOptions()))
        {
            // Only standing rows and a recent routine row — nothing qualifies.
            s.Store(AuditRow("admin-old-1", AccessVia.Admin, now.AddDays(-400)));
            s.Store(AuditRow("routine-new-1", AccessVia.Owner, now.AddDays(-5)));
            await s.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var summary = await AuditPurgeService.PurgeAsync(store, new AuditPurgeOptions { RoutineDays = 90 }, now, TestContext.Current.CancellationToken);

        Assert.Equal(0L, summary.Count);

        await using var check = store.OpenSession(new SessionOptions());
        Assert.NotNull(await check.LoadAsync<AccessAudit>("admin-old-1", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AccessAudit>("routine-new-1", TestContext.Current.CancellationToken));
        Assert.NotNull(await check.LoadAsync<AuditPurgeSummary>(summary.Id, TestContext.Current.CancellationToken));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static AccessAudit AuditRow(string id, AccessVia via, DateTimeOffset at) =>
        new()
        {
            Id = id,
            At = at,
            ActorId = "actor-1",
            Action = "view",
            TargetKind = "post",
            Via = via,
            Outcome = AccessOutcome.Allow
        };

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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
