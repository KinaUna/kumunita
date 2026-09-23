using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>EV-CAL</c> lane's one seam — <see cref="IEventService.ListInRangeAsync"/>
/// (ADR 0063 D2) — pinned against the frozen <see cref="IAuthorizationService"/>
/// through the <see cref="EventToAuditableResource"/> adapter (the design doc
/// §5.3's 10 pinned names, verbatim). The shape follows
/// <see cref="EventServiceTests"/> verbatim — same <see cref="PostgresFixture"/>,
/// same <c>BootStoreAsync</c>, same <c>Plant</c> helper, same <c>Services</c>
/// composition (the <c>UserInfoService</c> + <c>AuthorizationService</c> +
/// <c>EventService</c> trio), fresh scratch Postgres per test method.
/// <para>
/// The pins this file owns (design §4 invariants):
/// </para>
/// <list type="bullet">
/// <item><b>C-EV·1</b> — the window shows <em>exactly</em> what
///       <see cref="IEventService.ListUpcomingAsync"/> would for the same
///       actor restricted to the window: same candidate filter
///       (<c>!IsDeleted &amp;&amp; !IsDraft</c>, optional component filter),
///       same single <c>CanSeeAsync(Read)</c> gate, same draft/deleted
///       semantics (a draft is visible to its author; deleted is invisible
///       to everyone).</item>
/// <item><b>C-EV·2</b> — one aggregate <see cref="AccessAudit"/> row per
///       render (<c>TargetKind = "event"</c>, <c>visibleCount</c> /
///       <c>hiddenCount</c>), from the single <c>CanSeeAsync</c> call.</item>
/// <item><b>C-EV·3</b> — the <c>componentId</c> query is a filter, never a
///       gate, and emits no row of its own.</item>
/// </list>
/// <para>
/// The window predicate (the §5.1 pin, verbatim):
/// <c>Start &gt;= windowStartUtc &amp;&amp; Start &lt; windowEndUtc</c> — an
/// event is in the window on the day it <b>starts</b>; the start bound is
/// inclusive, the end bound is exclusive.
/// </para>
/// </summary>
public class EventCalendarSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // The pinned test window — 2026-09-01T00:00Z (inclusive) …
    // 2026-10-01T00:00Z (exclusive). A 31-day span (the controller's 30-day
    // policy is not the seam's business — the seam is window-span-agnostic).
    private static readonly DateTimeOffset WindowStart = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    // ── 1 — EV_Range_IncludesEventStartingInWindow (§5.3 #1) ────────────────
    //
    // A published, non-deleted event whose <see cref="Event.Start"/> falls
    // strictly inside the window is in the actor's visible set (C-EV·1).
    // The boundary controls — one hour before the start bound and one hour
    // after the end bound — are excluded, so the assertion is the window
    // predicate, not "a list came back".

    [Fact]
    public async Task EV_Range_IncludesEventStartingInWindow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-1-author";
        const string resident = "u-evc-1-resident";

        await Plant(store, new Event
        {
            Id = "in-window", AuthorId = author,
            Title = "In window", Body = "b",
            Start = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null, // public
        });
        await Plant(store, new Event
        {
            Id = "before-window", AuthorId = author,
            Title = "Before window", Body = "b",
            Start = WindowStart.AddHours(-1),
            End = WindowStart.AddHours(-1),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "after-window", AuthorId = author,
            Title = "After window", Body = "b",
            Start = WindowEnd.AddHours(1),
            End = WindowEnd.AddHours(1),
            IsDraft = false, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, resident);
        Assert.Contains("in-window", result.Select(e => e.Id));
        Assert.DoesNotContain("before-window", result.Select(e => e.Id));
        Assert.DoesNotContain("after-window", result.Select(e => e.Id));
    }

    // ── 2 — EV_Range_ExcludesEventStartingBeforeWindow (§5.3 #2) ────────────
    //
    // The start bound is <b>inclusive</b> (an event starting exactly at
    // <c>windowStartUtc</c> is in the window) but a start <b>before</b> the
    // bound is out — the <c>Start &gt;= windowStartUtc</c> half of the
    // predicate (the §5.1 pin).

    [Fact]
    public async Task EV_Range_ExcludesEventStartingBeforeWindow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-2-author";
        const string resident = "u-evc-2-resident";

        await Plant(store, new Event
        {
            Id = "just-before", AuthorId = author,
            Title = "Just before", Body = "b",
            Start = WindowStart.AddMinutes(-1),
            End = WindowStart.AddMinutes(-1),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "on-start", AuthorId = author,
            Title = "On start", Body = "b",
            Start = WindowStart, // exactly the start bound — inclusive.
            End = WindowStart.AddHours(2),
            IsDraft = false, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, resident);
        Assert.Contains("on-start", result.Select(e => e.Id));
        Assert.DoesNotContain("just-before", result.Select(e => e.Id));
    }

    // ── 3 — EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive (§5.3 #3) ──
    //
    // The end bound is <b>exclusive</b>: a start exactly at <c>windowEndUtc</c>
    // is out (the <c>Start &lt; windowEndUtc</c> half of the predicate — the
    // §5.1 pin). A start one hour before the end bound stays in, so the
    // boundary is tight, not an off-by-one.

    [Fact]
    public async Task EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-3-author";
        const string resident = "u-evc-3-resident";

        await Plant(store, new Event
        {
            Id = "on-end", AuthorId = author,
            Title = "On end", Body = "b",
            Start = WindowEnd, // exactly the end bound — exclusive.
            End = WindowEnd.AddHours(2),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "just-before-end", AuthorId = author,
            Title = "Just before end", Body = "b",
            Start = WindowEnd.AddHours(-1),
            End = WindowEnd.AddHours(-1),
            IsDraft = false, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, resident);
        Assert.Contains("just-before-end", result.Select(e => e.Id));
        Assert.DoesNotContain("on-end", result.Select(e => e.Id));
    }

    // ── 4 — EV_Range_DraftInvisibleToNonAuthor (ADR 0037; §5.3 #4) ─────────
    //
    // A draft event's candidate set is empty for a non-author (the
    // candidate filter's <c>!IsDraft</c> excludes it from the list) — and
    // the exclusion is the same non-leaky shape the feed has (C-EV·1:
    // the calendar ≡ the feed restricted to the window).

    [Fact]
    public async Task EV_Range_DraftInvisibleToNonAuthor()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-4-author";
        const string stranger = "u-evc-4-stranger";

        await Plant(store, new Event
        {
            Id = "draft", AuthorId = author,
            Title = "Unpublished", Body = "b",
            Start = new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero),
            IsDraft = true, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, stranger);
        Assert.Empty(result);
    }

    // ── 5 — EV_Range_DraftVisibleToAuthor (ADR 0037; §5.3 #5) ──────────────
    //
    // **DRIFT NOTE:** The §5.3 pinned name "DraftVisibleToAuthor" implies
    // the author sees their draft in the calendar. However C-EV·1 (design
    // doc §4, verbatim) pins: "the calendar shows *exactly* what
    // <c>ListUpcomingAsync</c> would show for the same actor, restricted to
    // the window: the **same candidate filter** (<c>!IsDeleted &amp;&amp;
    // !IsDraft</c> …)". The M4 feed precedent (<c>M4_DraftInvisibleToNonAuthor</c>)
    // explicitly asserts that drafts are excluded from <em>both</em> the
    // author's and the stranger's feed. The U02 implementation correctly
    // mirrors this: <c>.Where(e => !e.IsDeleted && !e.IsDraft)</c>.
    // This test therefore asserts the <b>actual pinned behavior</b>: the
    // draft is excluded for the author too (consistent with C-EV·1 + the
    // M4 feed). The author's access to their own draft is via the detail
    // view (<c>GetAsync</c> with the ADR 0037 draft gate), not the list
    // surface. See "## U08 — Drift pause" in the handoff notes.

    [Fact]
    public async Task EV_Range_DraftVisibleToAuthor()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-5-author";

        await Plant(store, new Event
        {
            Id = "draft", AuthorId = author,
            Title = "Unpublished", Body = "b",
            Start = new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero),
            IsDraft = true, IsDeleted = false, Audience = null,
        });

        // C-EV·1 — the calendar's candidate filter is the same as the feed's
        // (the "M4_DraftInvisibleToNonAuthor" precedent): drafts are excluded
        // for everyone, including the author. The author accesses their draft
        // via the detail view (GetAsync, the ADR 0037 draft gate), not the
        // list surface.
        Assert.Empty(await svc.ListInRangeAsync(WindowStart, WindowEnd, null, author));
    }

    // ── 6 — EV_Range_DeletedExcludedForEveryone (ADR 0024; §5.3 #6) ────────
    //
    // A soft-deleted event is filtered out of the window for <b>everyone</b> —
    // the candidate filter's <c>!IsDeleted</c> (the feed's unconditional
    // exclusion, C-EV·1). Neither the author nor a stranger sees it.

    [Fact]
    public async Task EV_Range_DeletedExcludedForEveryone()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-6-author";
        const string stranger = "u-evc-6-stranger";

        await Plant(store, new Event
        {
            Id = "deleted", AuthorId = author,
            Title = "Deleted", Body = "b",
            Start = new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = true, Audience = null,
        });

        Assert.Empty(await svc.ListInRangeAsync(WindowStart, WindowEnd, null, author));
        Assert.Empty(await svc.ListInRangeAsync(WindowStart, WindowEnd, null, stranger));
    }

    // ── 7 — EV_Range_AudienceMemberSeesEvent (C6 branch 6; §5.3 #7) ────────
    //
    // An audience-restricted event in the window is in the grantee's visible
    // set (the <c>CanSeeAsync</c> gate's MatchGroups lane — the
    // <see cref="M4_FeedVisibleToAudienceMember"/> shape, windowed).

    [Fact]
    public async Task EV_Range_AudienceMemberSeesEvent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-7-author";
        const string grantee = "u-evc-7-grantee";

        await Plant(store, new Event
        {
            Id = "granted", AuthorId = author,
            Title = "Members only", Body = "b",
            Start = new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false,
            Audience = Audience(GrantKind.User, grantee),
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, grantee);
        Assert.Contains("granted", result.Select(e => e.Id));
    }

    // ── 8 — EV_Range_NonMemberDenied_NoLeak (C-EV·1 non-leak; §5.3 #8) ─────
    //
    // The same restricted event, read by a non-member: <b>empty</b> — no
    // trace of the event id in the returned set (the zero-leak pin; the
    // <see cref="M4_FeedVisibleToAudienceMember"/> stranger branch, windowed).

    [Fact]
    public async Task EV_Range_NonMemberDenied_NoLeak()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-8-author";
        const string grantee = "u-evc-8-grantee";
        const string stranger = "u-evc-8-stranger";

        await Plant(store, new Event
        {
            Id = "granted", AuthorId = author,
            Title = "Members only", Body = "b",
            Start = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false,
            Audience = Audience(GrantKind.User, grantee),
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, stranger);
        Assert.Empty(result);
    }

    // ── 9 — EV_Range_AggregateAuditRowShape_TargetKindEvent (C-EV·2; §5.3 #9)
    //
    // One render with a mix of visible (a public event) and hidden (a
    // user-grant event the actor is not in) candidates writes <b>exactly one
    // aggregate</b> <see cref="AccessAudit"/> row — <c>TargetId = null</c>,
    // <c>TargetKind = "event"</c> (the <see cref="EventToAuditableResource"/>
    // discriminator), <c>Action = "read"</c>, <c>VisibleCount</c> /
    // <c>HiddenCount</c> = (1, 1) — plus the one per-item Deny row for the
    // audience-restricted event (the <see cref="PostServiceTests"/> F1
    // aggregate shape, the C3 single-aggregate-row pin).

    [Fact]
    public async Task EV_Range_AggregateAuditRowShape_TargetKindEvent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-evc-9-author";
        const string otherGrantee = "u-evc-9-grantee";
        const string actor = "u-evc-9-actor";

        await Plant(store, new Event
        {
            Id = "public-ev", AuthorId = author,
            Title = "Public", Body = "b",
            Start = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null, // public
        });
        await Plant(store, new Event
        {
            Id = "restricted-ev", AuthorId = author,
            Title = "Restricted", Body = "b",
            Start = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false,
            Audience = Audience(GrantKind.User, otherGrantee),
        });

        var result = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, actor);
        Assert.Contains("public-ev", result.Select(e => e.Id));
        Assert.DoesNotContain("restricted-ev", result.Select(e => e.Id));

        var rows = await EventAuditRows(store);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("event", aggregate.TargetKind);      // the exact string (C-EV·2).
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);

        var perItem = Assert.Single(rows, a => a.TargetId == "restricted-ev");
        Assert.Equal("event", perItem.TargetKind);
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);
    }

    // ── 10 — EV_Range_ComponentFilterIsFilterNotGate (C-EV·3; §5.3 #10) ────
    //
    // The <c>componentId</c> query narrows the <b>candidate set</b> (a
    // non-matching event is simply not listed) but changes <b>no</b> access
    // decision: the member sees their audience-restricted event with or
    // without the filter; the non-member is denied with or without it
    // (C-M3·2 — a filter, never a gate; the filter emits no row of its own,
    // it rides the one aggregate row).

    [Fact]
    public async Task EV_Range_ComponentFilterIsFilterNotGate()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-evc-10-author";
        const string member = "u-evc-10-member";
        const string nonMember = "u-evc-10-nonmember";
        const string compA = "c-evc-10-a";
        const string compB = "c-evc-10-b";

        await Plant(store, new Component { Id = compA, Name = "Safety", Enabled = true });
        await Plant(store, new Component { Id = compB, Name = "Garden", Enabled = true });

        await Plant(store, new Event
        {
            Id = "ev-a", AuthorId = author, ComponentId = compA,
            Title = "On comp A", Body = "b",
            Start = new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false,
            Audience = Audience(GrantKind.User, member),
        });
        await Plant(store, new Event
        {
            Id = "ev-b", AuthorId = author, ComponentId = compB,
            Title = "On comp B", Body = "b",
            Start = new DateTimeOffset(2026, 9, 18, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null, // public
        });

        // The filter narrows the candidate set: unfiltered, the member sees
        // both (their restricted one + the public one); filtered to compA,
        // only compA's event is listed.
        var unfiltered = await svc.ListInRangeAsync(WindowStart, WindowEnd, null, member);
        Assert.Contains("ev-a", unfiltered.Select(e => e.Id));
        Assert.Contains("ev-b", unfiltered.Select(e => e.Id));

        var filtered = await svc.ListInRangeAsync(WindowStart, WindowEnd, compA, member);
        Assert.Contains("ev-a", filtered.Select(e => e.Id));
        Assert.DoesNotContain("ev-b", filtered.Select(e => e.Id));

        // The filter is never a gate: the non-member is denied the restricted
        // event with <em>and</em> without the filter (no leak either way).
        Assert.DoesNotContain(
            "ev-a", (await svc.ListInRangeAsync(WindowStart, WindowEnd, null, nonMember)).Select(e => e.Id));
        Assert.DoesNotContain(
            "ev-a", (await svc.ListInRangeAsync(WindowStart, WindowEnd, compA, nonMember)).Select(e => e.Id));
    }

    // ── EV-DWM — additive window pins (C-DWM·1; design §6.3) ───────────────
    //
    // The EV-DWM lane adds **no** seam: <see cref="IEventService
    // .ListInRangeAsync"/> is reused, unchanged. These two pins re-exercise
    // the *existing* seam with narrower (1-day / 7-day) windows than the
    // 31-day EV-CAL window above, proving C-DWM·1's "the seam is already
    // window-agnostic" claim — day/week/month windows are *caller policy* in
    // the controller, and the seam honors whatever span it is asked for.

    // EV_Range_OneDayWindow_OnlyThatDay (design §6.3) — a 1-day window
    // returns *only* the event that starts on that day; the day-before and
    // day-after events are excluded by the <c>Start &gt;= windowStartUtc
    // &amp;&amp; Start &lt; windowEndUtc</c> predicate. Proves the seam honors an
    // arbitrary 1-day window (C-DWM·1).
    [Fact]
    public async Task EV_Range_OneDayWindow_OnlyThatDay()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-dwm-1-author";
        const string resident = "u-dwm-1-resident";

        // Anchor day = 2026-09-15 (UTC-midnight bounds — the zone is the
        // controller's display concern, not the seam's; the seam is UTC).
        var dayStartUtc = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        var dayEndUtc = dayStartUtc.AddDays(1);

        await Plant(store, new Event
        {
            Id = "on-anchor-day", AuthorId = author,
            Title = "On anchor day", Body = "b",
            Start = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null, // public
        });
        await Plant(store, new Event
        {
            Id = "day-before", AuthorId = author,
            Title = "Day before", Body = "b",
            Start = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "day-after", AuthorId = author,
            Title = "Day after", Body = "b",
            Start = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(dayStartUtc, dayEndUtc, null, resident);
        var ids = result.Select(e => e.Id).ToList();
        Assert.Contains("on-anchor-day", ids);
        Assert.DoesNotContain("day-before", ids);
        Assert.DoesNotContain("day-after", ids);
        // Exactly the anchor-day event — the window is tight, not a superset.
        Assert.Equal(1, ids.Count);
    }

    // EV_Range_SevenDayWindow_OnlyThoseDays (design §6.3) — a 7-day window
    // returns *only* the events whose start falls inside it; events the day
    // before the start bound and the day after the end bound are excluded.
    // Proves the seam honors an arbitrary 7-day window (C-DWM·1) — the
    // controller's week policy, not the seam's.
    [Fact]
    public async Task EV_Range_SevenDayWindow_OnlyThoseDays()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-dwm-2-author";
        const string resident = "u-dwm-2-resident";

        // A 7-day window anchored on 2026-09-14 (a Monday) through
        // 2026-09-20 (inclusive), i.e. [2026-09-14T00:00Z, 2026-09-21T00:00Z).
        var weekStartUtc = new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
        var weekEndUtc = weekStartUtc.AddDays(7);

        await Plant(store, new Event
        {
            Id = "in-week-1", AuthorId = author,
            Title = "In week (day 1)", Body = "b",
            Start = new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "in-week-2", AuthorId = author,
            Title = "In week (last day)", Body = "b",
            Start = new DateTimeOffset(2026, 9, 20, 18, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 20, 20, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "day-before-week", AuthorId = author,
            Title = "Day before week", Body = "b",
            Start = new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "day-after-week", AuthorId = author,
            Title = "Day after week", Body = "b",
            Start = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 9, 21, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });

        var result = await svc.ListInRangeAsync(weekStartUtc, weekEndUtc, null, resident);
        var ids = result.Select(e => e.Id).ToList();
        Assert.Contains("in-week-1", ids);
        Assert.Contains("in-week-2", ids);
        Assert.DoesNotContain("day-before-week", ids);
        Assert.DoesNotContain("day-after-week", ids);
        Assert.Equal(2, ids.Count);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Test plumbing (mirrors EventServiceTests verbatim).
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>The <see cref="AccessAudit"/> rows for this test's scratch
    /// database (the fresh-postgres-per-test isolation means "all event rows"
    /// is unambiguous — <c>TargetKind == "event"</c>).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> EventAuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>()
            .Where(a => a.TargetKind == "event")
            .ToListAsync(ct);
    }

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

    /// <summary>Compose the M4 service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="EventService"/> (the
    /// same three-constructor shape the DI registration uses, mirrored here
    /// directly against the scratch store — the <c>EventServiceTests</c>
    /// precedent).</summary>
    private static (UserInfoService User, AuthorizationService Authz, EventService Events)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var events = new EventService(store, authz, userInfo);
        return (userInfo, authz, events);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
