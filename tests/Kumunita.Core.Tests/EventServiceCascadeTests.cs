using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// GATE-4 (ADR 0119, U04) — the <see cref="IEventService.UpdateAsync"/>
/// head-edit cascade (D4): editing the head row of a series cascades
/// text / display / audience / language fields to all non-deleted
/// siblings in the same transaction (F4), and re-materializes the
/// series (soft-delete old siblings, re-expand, insert new set with
/// fresh Ids) when the rule itself or the head's <c>Start</c> /
/// <c>End</c> changes. The head's <c>Id</c> is always stable (D4 —
/// never reassigned). Standing is unchanged (C-M18·4 / C-M18·5 —
/// author ∪ GlobalAdmin via the existing <c>CheckEditStanding</c>
/// path; no new <c>AccessAction</c> / <c>AccessVia</c> / adapter).
/// <para>
/// The two names below are the §gate GATE-4 pin; a unit that does
/// not land them is not done.
/// </para>
/// </summary>
public class EventServiceCascadeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IReadOnlySet<string> EmptyRoles =
        new HashSet<string>(StringComparer.Ordinal);

    // ── GATE-4 (1) — text-field cascade to all non-deleted siblings ─────

    [Fact]
    public async Task Head_Edit_Cascades_Text_Fields_To_All_NonDeleted_Occurrences()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate4a-author";
        var ct = TestContext.Current.CancellationToken;

        // Build a 5-row weekly series via U02's create lane (Weekly/1/5):
        // head (RecurrenceRule non-null, RecurrenceHeadId null) + 4 siblings
        // (RecurrenceHeadId == head.Id, RecurrenceRule null). Anchor a week out
        // (a clean UTC whole-hour, microsecond-clean value) so every sibling
        // stays in the future regardless of the clock (the D4 now-floor drops
        // non-head occurrences whose Start is in the past — the fixed 2026-10-01
        // dates went stale), the Postgres timestamptz (µs-precision) round-trip
        // is exact (a sub-µs UtcNow would make a timestamp-equality assertion
        // flaky), and UTC keeps stepping away from any local DST boundary.
        var headStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(7), TimeSpan.Zero);
        var headEnd = headStart.AddHours(1);

        var head = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Weekly standup",
            Body = "body",
            Start = headStart,
            End = headEnd,
            IsDraft = false,
            Recurrence = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 5,
            },
        });

        // Verify the pre-edit state: exactly 5 non-deleted rows (head + 4 siblings).
        await using (var q0 = store.QuerySession())
        {
            var all0 = await q0.Query<Event>()
                .Where(e => e.Id == head.Id || e.RecurrenceHeadId == head.Id)
                .Where(e => e.IsDeleted == false)
                .ToListAsync(ct);
            Assert.Equal(5, all0.Count);
            Assert.Equal("Weekly standup", all0[0].Title);
        }

        // Edit the head: new Title + Location, same Start / End, same rule
        // (Weekly/1/5 — no rule change, no Start/End change → no re-materialization).
        var updated = await svc.UpdateAsync(head.Id, author, EmptyRoles, new UpdateEventRequest
        {
            Title = "Renamed standup",
            Body = "body",
            Start = headStart,
            End = headEnd,
            Location = "Room B",
            Recurrence = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 5,
            },
        });

        // The returned head: new Title / Location, rule unchanged (Weekly/1/5),
        // Start / End unchanged, Id stable.
        Assert.Equal(head.Id, updated.Id);
        Assert.Equal("Renamed standup", updated.Title);
        Assert.Equal("Room B", updated.Location);
        Assert.Equal(headStart.UtcDateTime, updated.Start.UtcDateTime);
        Assert.Equal(headEnd.UtcDateTime, updated.End.UtcDateTime);
        Assert.NotNull(updated.RecurrenceRule);
        Assert.Equal(Recurrence.Weekly, updated.RecurrenceRule!.Recurrence);
        Assert.Equal(1, updated.RecurrenceRule.Interval);
        Assert.Equal(5, updated.RecurrenceRule.Count);

        // All 5 rows (head + 4 siblings) carry the new Title + Location.
        // No row's Start / End changed. Still exactly 5 non-deleted rows.
        await using (var q = store.QuerySession())
        {
            var all = (await q.Query<Event>()
                .Where(e => e.Id == head.Id || e.RecurrenceHeadId == head.Id)
                .Where(e => e.IsDeleted == false)
                .OrderBy(e => e.Start)
                .ToListAsync(ct)).ToList();

            Assert.Equal(5, all.Count);

            // Expected Starts: head + 4 weekly siblings (interval 1), all
            // derived from the now-relative headStart so the suite stays
            // green as the clock advances.
            var expectedStarts = new[]
            {
                headStart,
                headStart.AddDays(7),
                headStart.AddDays(14),
                headStart.AddDays(21),
                headStart.AddDays(28),
            };

            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(expectedStarts[i].UtcDateTime, all[i].Start.UtcDateTime);
                Assert.Equal(expectedStarts[i].AddHours(1).UtcDateTime, all[i].End.UtcDateTime);  // 1-hour duration preserved.
                Assert.Equal("Renamed standup", all[i].Title);           // cascaded to all rows.
                Assert.Equal("Room B", all[i].Location);                 // cascaded to all rows.
            }

            // The head (row 0) has the rule; the siblings (rows 1-4) do not.
            Assert.NotNull(all[0].RecurrenceRule);
            Assert.Null(all[0].RecurrenceHeadId);
            for (var i = 1; i < 5; i++)
            {
                Assert.Null(all[i].RecurrenceRule);
                Assert.Equal(head.Id, all[i].RecurrenceHeadId);
            }
        }
    }

    // ── GATE-4 (2) — rule change re-materializes the series ──────────────

    [Fact]
    public async Task Head_Edit_Rule_Change_Rematerializes_The_Series()
    {
        var store = await BootStoreAsync();
        var svc = Services(store);
        const string author = "m18-gate4b-author";
        var ct = TestContext.Current.CancellationToken;

        // Build a 5-row weekly series (Weekly/1/5): head + 4 weekly
        // siblings. Anchor a week out (a clean UTC whole-hour, microsecond-
        // clean value) so every sibling stays in the future regardless of the
        // clock (the D4 now-floor drops non-head occurrences whose Start is in
        // the past — the fixed 2026-10-01 dates went stale), the Postgres
        // timestamptz round-trip is exact, and UTC keeps stepping away from any
        // local DST boundary.
        var headStart = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(7), TimeSpan.Zero);
        var headEnd = headStart.AddHours(1);

        var head = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Weekly standup",
            Body = "body",
            Start = headStart,
            End = headEnd,
            IsDraft = false,
            Recurrence = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 5,
            },
        });

        var headIdBefore = head.Id;

        // Pre-edit: exactly 5 non-deleted rows, the 5th (last) sibling is at
        // 2026-10-29 (the 4th sibling, index 3).
        await using (var qPre = store.QuerySession())
        {
            var pre = (await qPre.Query<Event>()
                .Where(e => e.Id == head.Id || e.RecurrenceHeadId == head.Id)
                .Where(e => e.IsDeleted == false)
                .OrderBy(e => e.Start)
                .ToListAsync(ct)).ToList();
            Assert.Equal(5, pre.Count);
            var fifthOccurrenceStart = headStart.AddDays(28);
            Assert.Equal(fifthOccurrenceStart.UtcDateTime, pre[4].Start.UtcDateTime);
        }

        // Edit the head: change the rule from Weekly/1/5 to Weekly/1/3
        // (Count 5 → 3 — a rule change → re-materialization, D4).
        var updated = await svc.UpdateAsync(head.Id, author, EmptyRoles, new UpdateEventRequest
        {
            Title = "Weekly standup",
            Body = "body",
            Start = headStart,
            End = headEnd,
            Recurrence = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 3,
            },
        });

        // The head's Id is stable (D4 — never reassigned).
        Assert.Equal(headIdBefore, updated.Id);

        // The head's RecurrenceRule is now Weekly/1/3.
        Assert.NotNull(updated.RecurrenceRule);
        Assert.Equal(Recurrence.Weekly, updated.RecurrenceRule!.Recurrence);
        Assert.Equal(1, updated.RecurrenceRule.Interval);
        Assert.Equal(3, updated.RecurrenceRule.Count);

        // Exactly 2 non-deleted siblings remain (the 3-occurrence set minus
        // the head), each with Start at 7-day steps from the head.
        await using (var q = store.QuerySession())
        {
            var liveSiblings = (await q.Query<Event>()
                .Where(e => e.RecurrenceHeadId == head.Id)
                .Where(e => e.IsDeleted == false)
                .OrderBy(e => e.Start)
                .ToListAsync(ct)).ToList();

            Assert.Equal(2, liveSiblings.Count);
            Assert.Equal(headStart.AddDays(7).UtcDateTime, liveSiblings[0].Start.UtcDateTime);
            Assert.Equal(headStart.AddDays(14).UtcDateTime, liveSiblings[1].Start.UtcDateTime);
            // End = Start + 1-hour duration preserved.
            Assert.Equal(liveSiblings[0].Start.AddHours(1).UtcDateTime, liveSiblings[0].End.UtcDateTime);
            Assert.Equal(liveSiblings[1].Start.AddHours(1).UtcDateTime, liveSiblings[1].End.UtcDateTime);
            // Siblings have null rule, link to head.
            Assert.All(liveSiblings, s =>
            {
                Assert.Null(s.RecurrenceRule);
                Assert.Equal(head.Id, s.RecurrenceHeadId);
            });
        }

        // The old 5th-occurrence row (2026-10-29) is soft-deleted
        // (IsDeleted == true), not hard-deleted — it is still queryable
        // in the store (C-M18·8 — reversible soft-delete).
        await using (var qOld = store.QuerySession())
        {
            var oldFifth = await qOld.Query<Event>()
                .Where(e => e.RecurrenceHeadId == head.Id)
                .Where(e => e.Start == headStart.AddDays(28))
                .ToListAsync(ct);
            // There may be multiple rows at this Start (old deleted + no new
            // sibling at this date since the new series only goes to 2026-10-15).
            // The old row at 2026-10-29 should all be deleted.
            Assert.NotEmpty(oldFifth);
            Assert.All(oldFifth, e => Assert.True(e.IsDeleted));
        }
    }

    // ── helpers (mirroring the U02 / U03 test pattern) ──────────────────

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
