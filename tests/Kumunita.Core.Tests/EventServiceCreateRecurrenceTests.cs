using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GATE-2 (ADR 0119, U02) — the <see cref="IEventService.CreateAsync"/>
/// materialization lane: a <c>CreateAsync</c> call carrying a recurrence rule
/// (a non-<c>None</c> <see cref="EventRecurrenceRule"/>) materializes the full
/// series (head + siblings) in one transaction; a <c>CreateAsync</c> call with
/// no rule (or <c>Recurrence.None</c>) is byte-for-byte unchanged (one row,
/// <c>RecurrenceHeadId = null</c>, <c>RecurrenceRule = null</c>).
/// <para>
/// These tests pin D1 (materialized occurrences), D3 (the head is
/// occurrence #1, siblings carry <c>RecurrenceHeadId = head.Id</c>),
/// C-M18·1 (reads see concrete rows — create produces them), and C-M18·2
/// (the expansion is the pure U01 expander — <c>CreateAsync</c> only *calls*
/// it and *persists* the rows, it does not re-implement stepping).
/// </para>
/// <para>
/// The two names below are the §gate GATE-2 pin; a unit that does not land
/// them is not done.
/// </para>
/// </summary>
public class EventServiceCreateRecurrenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── GATE-2 (1) — a weekly rule materializes the full series ─────────────

    [Fact]
    public async Task Create_With_A_Weekly_Rule_Materializes_All_Occurrences()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate2-author";

        var head = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Weekly standup",
            Body = "body",
            Start = new DateTimeOffset(2026, 10, 1, 18, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 19, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Recurrence = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 4,
            },
        });

        // The returned head: rule non-null (Weekly/1/4/null), no series link, not a draft.
        Assert.NotNull(head.RecurrenceRule);
        Assert.Equal(Recurrence.Weekly, head.RecurrenceRule!.Recurrence);
        Assert.Equal(1, head.RecurrenceRule.Interval);
        Assert.Equal(4, head.RecurrenceRule.Count);
        Assert.Null(head.RecurrenceRule.Ends);
        Assert.Null(head.RecurrenceHeadId);
        Assert.False(head.IsDraft);
        Assert.Equal(author, head.AuthorId);
        Assert.Equal("Weekly standup", head.Title);

        // The store holds exactly 3 siblings with RecurrenceHeadId == head.Id,
        // each with the expected Start / End, a null rule, and IsDraft = false.
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        var siblings = (await q.Query<Event>()
            .Where(e => e.RecurrenceHeadId == head.Id)
            .OrderBy(e => e.Start)
            .ToListAsync(ct))
            .ToList();

        Assert.Equal(3, siblings.Count);

        var expectedStarts = new[]
        {
            new DateTimeOffset(2026, 10, 8, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 22, 18, 0, 0, TimeSpan.Zero),
        };

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(expectedStarts[i], siblings[i].Start);
            // End = Start + (head.End - head.Start) — the 1-hour duration preserved.
            Assert.Equal(expectedStarts[i].AddHours(1), siblings[i].End);
            Assert.Null(siblings[i].RecurrenceRule);
            Assert.Equal(head.Id, siblings[i].RecurrenceHeadId);
            Assert.False(siblings[i].IsDraft);
            Assert.Equal(author, siblings[i].AuthorId);
            Assert.Equal("Weekly standup", siblings[i].Title);
        }

        // None of the 4 rows is a draft beyond the head's IsDraft (a non-draft
        // create publishes the whole series).
        var allRows = await q.Query<Event>()
            .Where(e => e.Id == head.Id || e.RecurrenceHeadId == head.Id)
            .ToListAsync(ct);
        Assert.Equal(4, allRows.Count);
        Assert.All(allRows, e => Assert.False(e.IsDraft));
    }

    // ── GATE-2 (2) — the zero-change branch (no rule / Recurrence.None) ─────

    [Fact]
    public async Task Create_With_No_Rule_Behaves_Exactly_As_Today()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate2b-author";
        var ct = TestContext.Current.CancellationToken;

        // Case 1: Recurrence = null (the common case — the author picks no rule).
        var ev1 = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "One-off meetup",
            Body = "body1",
            Start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Recurrence = null,
        });

        Assert.Null(ev1.RecurrenceRule);
        Assert.Null(ev1.RecurrenceHeadId);
        Assert.Equal("One-off meetup", ev1.Title);

        await using var q1 = store.QuerySession();
        var all1 = await q1.Query<Event>().ToListAsync(ct);
        Assert.Single(all1, e => e.Id == ev1.Id || e.RecurrenceHeadId == ev1.Id);

        // Case 2: Recurrence = { Recurrence = None } (the composer's explicit "None").
        var ev2 = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "One-off workshop",
            Body = "body2",
            Start = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Recurrence = new EventRecurrenceRule { Recurrence = Recurrence.None, Interval = 1 },
        });

        Assert.Null(ev2.RecurrenceRule);
        Assert.Null(ev2.RecurrenceHeadId);
        Assert.Equal("One-off workshop", ev2.Title);

        await using var q2 = store.QuerySession();
        var all2 = await q2.Query<Event>().ToListAsync(ct);
        Assert.Single(all2, e => e.Id == ev2.Id || e.RecurrenceHeadId == ev2.Id);
    }

    // ── helpers (mirroring the EventServiceTests pattern) ──────────────────

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
