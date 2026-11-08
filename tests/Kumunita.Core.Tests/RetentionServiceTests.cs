using Kumunita.Core;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Identity;
using Kumunita.Core.Privacy;
using Kumunita.Core.Sessions;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IS34a seam test for <see cref="RetentionService"/> (IMPROVE-SEC D6 / E5 —
/// the I/O leg of the retention policy IS33 made pure). A **store** unit (the
/// <see cref="UsagePurgeServiceTests"/> / <see cref="MediaLifecycleTests"/>
/// idiom — a real Marten <c>DocumentStore</c> over the
/// <c>PostgresFixture</c>), pinning the I/O behaviour: old rows deleted, live
/// rows kept, outbox body nulled before the row is deleted, and idempotency.
/// The pure cutoff math is IS33's <see cref="RetentionPolicyTests"/> (not
/// re-tested here); the single-source-of-rule contract is that every cutoff in
/// the service is <see cref="RetentionPolicy.CutoffFor"/> (a <c>null</c> cutoff
/// from a <c>0</c>-valued / absent key skips the type).
/// <para>
/// Synthetic fixture ids only (<c>user-a</c> / <c>user-b</c>,
/// <c>a@example.test</c>) — no real PII (rule 8 / the IS08 gate).
/// </para>
/// </summary>
public sealed class RetentionServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Retention_DeletesOldRevokedSessions_KeepsLive()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        // Cutoff = now − 30 d (the default Sessions key). A row revoked 31 d
        // ago is past it; a live session (RevokedAt null) is kept.
        var revokedId = Guid.NewGuid().ToString("n");
        var liveId = Guid.NewGuid().ToString("n");

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new SessionDoc
            {
                Id = revokedId, UserId = "user-a", UserAgent = "test-agent",
                Ip = "10.0.0.1", CreatedAt = now.UtcDateTime.AddDays(-40), LastActive = now.UtcDateTime.AddDays(-31),
                RevokedAt = now.UtcDateTime.AddDays(-31)
            });
            session.Store(new SessionDoc
            {
                Id = liveId, UserId = "user-a", UserAgent = "test-agent",
                Ip = "10.0.0.1", CreatedAt = now.UtcDateTime, LastActive = now.UtcDateTime,
                RevokedAt = null
            });
            await session.SaveChangesAsync(ct);
        }

        var result = await RetentionService.RunAsync(store, new RetentionOptions(), now, ct);

        // Only the revoked session is deleted; the live one is kept.
        Assert.Equal(1, result.SessionsDeleted);
        await using (var q = store.QuerySession())
            Assert.Null(await q.LoadAsync<SessionDoc>(revokedId, ct));
        await using (var q2 = store.QuerySession())
            Assert.NotNull(await q2.LoadAsync<SessionDoc>(liveId, ct));
    }

    [Fact]
    public async Task Retention_NullsOutboxBodyAfter7d_DeletesAfter90d()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        // Body-null cutoff = now − 7 d; row-delete cutoff = now − 90 d. A row
        // queued 10 d ago is past the body cutoff (nulled, kept); one queued
        // 100 d ago is past the row cutoff (deleted).
        var nulledId = Guid.NewGuid().ToString("n");
        var deletedId = Guid.NewGuid().ToString("n");

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new OutboxEmail
            {
                Id = nulledId, IdempotencyKey = "verify:user-a:1",
                Recipient = "a@example.test", Subject = "verify", Body = "SECRET-BODY-nulled",
                QueuedAt = now.AddDays(-10)
            });
            session.Store(new OutboxEmail
            {
                Id = deletedId, IdempotencyKey = "verify:user-a:2",
                Recipient = "a@example.test", Subject = "verify", Body = "SECRET-BODY-deleted",
                QueuedAt = now.AddDays(-100)
            });
            await session.SaveChangesAsync(ct);
        }

        var result = await RetentionService.RunAsync(store, new RetentionOptions(), now, ct);

        // The -100 d row is deleted (past the 90 d OutboxDays cutoff). The
        // -10 d row is kept (row cutoff not crossed) but its body is nulled.
        // Both rows are past the 7 d body-null cutoff, so both have their
        // body nulled this run (the -100 d one is then deleted — nulling a
        // row about to be deleted is harmless and correct).
        Assert.Equal(1, result.OutboxDeleted);
        Assert.Equal(2, result.OutboxBodyNulled);
        await using (var q = store.QuerySession())
            Assert.Null(await q.LoadAsync<OutboxEmail>(deletedId, ct));
        await using (var q2 = store.QuerySession())
        {
            var nulled = await q2.LoadAsync<OutboxEmail>(nulledId, ct);
            Assert.NotNull(nulled);
            Assert.Equal(string.Empty, nulled!.Body);
        }
    }

    [Fact]
    public async Task Retention_DeletesConsumedTokens()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        // Tokens cutoff = now − 7 d. Consumed or expired past it → deleted;
        // live (not consumed, not expired) → kept.
        var consumedId = Guid.NewGuid().ToString("n");
        var expiredId = Guid.NewGuid().ToString("n");
        var liveId = Guid.NewGuid().ToString("n");
        var revokedEscId = Guid.NewGuid().ToString("n");
        var liveEscId = Guid.NewGuid().ToString("n");
        var deadId = Guid.NewGuid().ToString("n");

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new IdentityToken
            {
                Id = consumedId, Kind = IdentityToken.KindVerify, UserId = "user-a",
                Token = "secret-a", Attempt = 1,
                CreatedAt = now.AddDays(-20), ExpiresAt = now.AddDays(-5),
                ConsumedAt = now.AddDays(-10)
            });
            session.Store(new IdentityToken
            {
                Id = expiredId, Kind = IdentityToken.KindVerify, UserId = "user-b",
                Token = "secret-b", Attempt = 1,
                CreatedAt = now.AddDays(-20), ExpiresAt = now.AddDays(-8),
                ConsumedAt = null
            });
            session.Store(new IdentityToken
            {
                Id = liveId, Kind = IdentityToken.KindVerify, UserId = "user-a",
                Token = "secret-live", Attempt = 1,
                CreatedAt = now, ExpiresAt = now.AddDays(1),
                ConsumedAt = null
            });

            // An EscalationToken revoked past the cutoff is deleted; a live one kept.
            session.Store(new EscalationToken
            {
                Id = revokedEscId, Label = "origin-a", TokenHash = "hash-a", TokenPrefix = "kesc_a",
                Created = now.AddDays(-20), CreatedBy = "user-a",
                RevokedAt = now.AddDays(-10), RevokedBy = "user-b"
            });
            session.Store(new EscalationToken
            {
                Id = liveEscId, Label = "origin-b", TokenHash = "hash-b", TokenPrefix = "kesc_b",
                Created = now, CreatedBy = "user-b",
                RevokedAt = null, RevokedBy = null
            });

            // An EmailDeadLetter past the cutoff is deleted.
            session.Store(new EmailDeadLetter
            {
                Id = deadId, IdempotencyKey = "verify:user-a:1", Recipient = "a@example.test",
                Subject = "verify", LastError = "smtp down", Attempts = 6,
                CreatedAt = now.AddDays(-40), DeadAt = now.AddDays(-35)
            });

            await session.SaveChangesAsync(ct);
        }

        var result = await RetentionService.RunAsync(store, new RetentionOptions(), now, ct);

        // Consumed + expired tokens deleted; live token kept.
        Assert.Equal(2, result.IdentityTokensDeleted);
        await using (var q = store.QuerySession())
        {
            Assert.Null(await q.LoadAsync<IdentityToken>(consumedId, ct));
            Assert.Null(await q.LoadAsync<IdentityToken>(expiredId, ct));
            Assert.NotNull(await q.LoadAsync<IdentityToken>(liveId, ct));
        }

        // Revoked escalation token deleted; live one kept.
        Assert.Equal(1, result.EscalationTokensDeleted);
        await using (var q2 = store.QuerySession())
        {
            Assert.Null(await q2.LoadAsync<EscalationToken>(revokedEscId, ct));
            Assert.NotNull(await q2.LoadAsync<EscalationToken>(liveEscId, ct));
        }

        // The old dead letter is deleted.
        Assert.Equal(1, result.DeadLettersDeleted);
        await using (var q3 = store.QuerySession())
            Assert.Null(await q3.LoadAsync<EmailDeadLetter>(deadId, ct));
    }

    [Fact]
    public async Task Retention_IsIdempotent()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new SessionDoc
            {
                Id = Guid.NewGuid().ToString("n"), UserId = "user-a", UserAgent = "test-agent",
                Ip = "10.0.0.1", CreatedAt = now.UtcDateTime.AddDays(-40), LastActive = now.UtcDateTime.AddDays(-31),
                RevokedAt = now.UtcDateTime.AddDays(-31)
            });
            await session.SaveChangesAsync(ct);
        }

        // First run deletes the one old session.
        var first = await RetentionService.RunAsync(store, new RetentionOptions(), now, ct);
        Assert.Equal(1, first.SessionsDeleted);

        // Second run is a no-op: nothing left to delete (idempotent — the
        // UsagePurgeService "natural no-op" shape).
        var second = await RetentionService.RunAsync(store, new RetentionOptions(), now, ct);
        Assert.Equal(0, second.SessionsDeleted);
        Assert.Equal(0, second.IdentityTokensDeleted);
        Assert.Equal(0, second.EscalationTokensDeleted);
        Assert.Equal(0, second.DeadLettersDeleted);
        Assert.Equal(0, second.OutboxBodyNulled);
        Assert.Equal(0, second.OutboxDeleted);

        // A key set to 0 (keep) skips its type entirely even with an old row.
        await using (var s2 = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s2.Store(new SessionDoc
            {
                Id = Guid.NewGuid().ToString("n"), UserId = "user-b", UserAgent = "test-agent",
                Ip = "10.0.0.2", CreatedAt = now.UtcDateTime.AddDays(-40), LastActive = now.UtcDateTime.AddDays(-31),
                RevokedAt = now.UtcDateTime.AddDays(-31)
            });
            await s2.SaveChangesAsync(ct);
        }
        var keepOptions = new RetentionOptions { Sessions = 0 };
        var kept = await RetentionService.RunAsync(store, keepOptions, now, ct);
        Assert.Equal(0, kept.SessionsDeleted); // Sessions=0 → null cutoff → skipped
    }

    private async Task<IDocumentStore> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);          // IdentityToken, OutboxEmail, EmailDeadLetter
            SessionDocTypes.Configure(opts);       // SessionDoc
            EscalationDocTypes.Configure(opts);    // EscalationToken
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }
}
