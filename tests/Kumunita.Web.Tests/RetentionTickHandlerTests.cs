using Kumunita.Core;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Privacy;
using Kumunita.Core.Sessions;
using Kumunita.Web.SideEffects;
using Marten;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// IS34b seam test for <see cref="RetentionTickHandler"/> (IMPROVE-SEC D6 / E5
/// — the Web-side recurring leg of the retention sweep IS33/IS34a made pure
/// + I/O in Core). A **store** unit (the <see cref="Kumunita.Core.Tests.
/// RetentionServiceTests"/> boot shape — a real Marten <c>DocumentStore</c>
/// over the <see cref="PostgresFixture"/>), proving the handler is a thin
/// adapter: it calls IS34a's <see cref="RetentionService.RunAsync"/> seam
/// (no re-derived cutoff, no inline delete SQL here) and re-yields a fresh
/// <see cref="RetentionTick"/> so the recurring schedule carries forward.
/// <para>
/// Synthetic fixture ids only (<c>user-a</c>) — no real PII (rule 8 / the IS08
/// gate).
/// </para>
/// </summary>
public sealed class RetentionTickHandlerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Handler_CallsService_AndReschedules")]
    public async Task Handler_CallsService_AndReschedules()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        // Cutoff = now − 30 d (the default Sessions key). A row revoked 31 d
        // ago is past it; a live session (RevokedAt null) is kept.
        var revokedId = Guid.NewGuid().ToString("n");

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new SessionDoc
            {
                Id = revokedId, UserId = "user-a", UserAgent = "test-agent",
                Ip = "10.0.0.1",
                CreatedAt = now.UtcDateTime.AddDays(-40),
                LastActive = now.UtcDateTime.AddDays(-31),
                RevokedAt = now.UtcDateTime.AddDays(-31)
            });
            await session.SaveChangesAsync(ct);
        }

        // Drive the handler (not the service directly) — the seam IS34b
        // delivers: the handler injects a live store + options into the
        // IS34a-frozen RetentionService and re-yields a fresh RetentionTick.
        var results = await RetentionTickHandler.Handle(
            new RetentionTick(), store, Options.Create(new RetentionOptions()));

        // (1) The handler returned a fresh RetentionTick (the re-schedule).
        Assert.IsType<RetentionTick>(Assert.Single(results));

        // (2) The service ran: the revoked session was deleted.
        await using (var q = store.QuerySession())
            Assert.Null(await q.LoadAsync<SessionDoc>(revokedId, ct));

        // A second drive is a no-op (no row to delete) but still re-yields —
        // the schedule self-perpetuates.
        var results2 = await RetentionTickHandler.Handle(
            new RetentionTick(), store, Options.Create(new RetentionOptions()));
        Assert.IsType<RetentionTick>(Assert.Single(results2));
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
