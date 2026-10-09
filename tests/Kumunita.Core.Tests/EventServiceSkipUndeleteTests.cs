using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GATE-3 (ADR 0119, U03) — the <see cref="IEventService.SkipOccurrenceAsync"/>
/// / <see cref="IEventService.UndeleteOccurrenceAsync"/> author-only seams:
/// <see cref="IEventService.SkipOccurrenceAsync"/> is a **soft-delete of one row**
/// (<c>IsDeleted = true</c>) — never a hard-delete, never a cascade, never a
/// touch of the head's <c>RecurrenceRule</c> or any sibling (C-M18·8);
/// <see cref="IEventService.UndeleteOccurrenceAsync"/> is its inverse
/// (<c>IsDeleted = false</c>, always reversible). The standing is **author ∪
/// GlobalAdmin** (C-M18·4); a non-author, non-GlobalAdmin actor gets a
/// <c>KeyNotFoundException</c> (404, the frozen seam's "absent" shape — no
/// leak, no 403, C-M18·4 / D5). No new <c>AccessAction</c> / <c>AccessVia</c>
/// / adapter (C-M18·5); no <c>AccessAudit</c> row (a write side-effect, not an
/// access decision).
/// <para>
/// The two names below are the §gate GATE-3 pin; a unit that does not land
/// them is not done.
/// </para>
/// </summary>
public class EventServiceSkipUndeleteTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── GATE-3 (1) — skip is a soft-delete of one row (reversible) ─────────

    [Fact]
    public async Task Skip_Occurrence_Sets_IsDeleted_On_That_Row_Only()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate3-author";
        var ct = TestContext.Current.CancellationToken;

        // Build a 3-occurrence weekly series via U02's create lane: the head
        // (RecurrenceRule non-null, RecurrenceHeadId null) + 2 siblings
        // (RecurrenceHeadId == head.Id, RecurrenceRule null). Anchor a week out
        // (a clean UTC whole-hour, microsecond-clean value) so every sibling
        // stays in the future regardless of the clock (the D4 now-floor drops
        // non-head occurrences whose Start is in the past — the fixed 2026-10-01
        // dates went stale) and the Postgres timestamptz round-trip is exact.
        var headStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(7), TimeSpan.Zero);
        var head = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Weekly sync",
            Body = "body",
            Start = headStart,
            End = headStart.AddHours(1),
            IsDraft = false,
            Recurrence = new EventRecurrenceRule { Recurrence = Recurrence.Weekly, Interval = 1, Count = 3 },
        });

        await using (var q = store.QuerySession())
        {
            var siblings = (await q.Query<Event>()
                .Where(e => e.RecurrenceHeadId == head.Id)
                .OrderBy(e => e.Start)
                .ToListAsync(ct)).ToList();
            Assert.Equal(2, siblings.Count);

            var middle = siblings[0];                 // occurrence #2 (the middle of the 3-row series)
            var third = siblings[1];                  // occurrence #3
            Assert.Null(middle.RecurrenceRule);
            Assert.Equal(head.Id, middle.RecurrenceHeadId);

            // Skip the MIDDLE row. Only that row is soft-deleted.
            await svc.SkipOccurrenceAsync(middle.Id, author, ct);

            var m = await q.LoadAsync<Event>(middle.Id, ct);
            var h = await q.LoadAsync<Event>(head.Id, ct);
            var t = await q.LoadAsync<Event>(third.Id, ct);

            Assert.True(m!.IsDeleted);
            Assert.False(h!.IsDeleted);
            Assert.False(t!.IsDeleted);

            // The head's rule is untouched (C-M18·8) and the sibling link holds.
            Assert.NotNull(h.RecurrenceRule);
            Assert.Equal(Recurrence.Weekly, h.RecurrenceRule!.Recurrence);
            Assert.Equal(3, h.RecurrenceRule.Count);
            Assert.Equal(head.Id, m.RecurrenceHeadId); // sibling link intact (C-M18·8 — untouched)

            // Reversible (C-M18·8): undelete the middle row.
            await svc.UndeleteOccurrenceAsync(middle.Id, author, ct);
            var m2 = await q.LoadAsync<Event>(middle.Id, ct);
            Assert.False(m2!.IsDeleted);
        }
    }

    // ── GATE-3 (2) — a non-author gets a 404 (no leak), no write occurred ──

    [Fact]
    public async Task Skip_By_NonAuthor_Returns_404()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate3b-author";
        const string stranger = "m18-gate3b-user-B";
        var ct = TestContext.Current.CancellationToken;

        // Build a 2-row weekly series owned by author A (head + 1 sibling).
        // Anchor a week out (a clean UTC whole-hour, microsecond-clean value)
        // so the single sibling stays in the future regardless of the clock
        // (the fixed 2026-11-01 date went stale) and the Postgres timestamptz
        // round-trip is exact.
        var headStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(7), TimeSpan.Zero);
        var head = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Weekly club",
            Body = "body",
            Start = headStart,
            End = headStart.AddHours(1),
            IsDraft = false,
            Recurrence = new EventRecurrenceRule { Recurrence = Recurrence.Weekly, Interval = 1, Count = 2 },
        });

        await using var q = store.QuerySession();
        var sibling = (await q.Query<Event>()
            .Where(e => e.RecurrenceHeadId == head.Id)
            .ToListAsync(ct)).Single();
        Assert.Equal(author, sibling.AuthorId);

        // A non-author, non-GlobalAdmin actor is denied with a 404
        // (KeyNotFoundException — the frozen seam's "absent" shape, NOT a 403,
        // C-M18·4 / D5), and no write occurred.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SkipOccurrenceAsync(sibling.Id, stranger, ct));

        var after = await q.LoadAsync<Event>(sibling.Id, ct);
        Assert.False(after!.IsDeleted);
        Assert.Equal(author, after.AuthorId);
    }

    // ── helpers (mirroring the EventServiceCreateRecurrenceTests pattern) ───

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

    private static EventService Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        return new EventService(store, authz, userInfo);
    }
}
