using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="EventService"/> read-lane + standing-matrix seam tests (M4,
/// U03). The shape follows <see cref="PostServiceTests"/> verbatim — same
/// <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c>, same
/// <c>Plant</c> helper, same <c>Services</c> composition (the
/// <c>UserInfoService</c> + <c>AuthorizationService</c> + <c>EventService</c>
/// trio the U01 <c>AddTransient</c> registration mirrors), fresh scratch
/// Postgres per test method.
/// <para>
/// This unit pins the **U03** surface — the four read lanes
/// (<see cref="IEventService.ListUpcomingAsync"/> /
/// <see cref="IEventService.GetAsync"/> /
/// <see cref="IEventService.GetRsvpsAsync"/> /
/// <see cref="IEventService.GetMyRsvpAsync"/>) and the two **pure** standing
/// helpers (<see cref="EventService.CheckCreateStanding"/> /
/// <see cref="EventService.CheckEditStanding"/>) — against the **frozen**
/// <c>IAuthorizationService</c> (ADR 0006) through the
/// <see cref="EventToAuditableResource"/> adapter (U02).
/// </para>
/// <para>
/// The pins this lane owns (see the <see cref="EventService"/> members for the
/// full ADR 0054 rationale):
/// </para>
/// <list type="number">
/// <item><b>The feed is <c>CanSeeAsync(Read)</c>-filtered</b> — the
///       <see cref="Event"/> candidate set (non-draft, non-deleted, ordered by
///       <see cref="Event.Start"/> ascending) is the *candidate filter, never
///       the gate* (C-M3·2); the survivors pass the one shared
///       <c>CanSeeAsync</c> matching pass (C6) and produce the single
///       aggregate <see cref="AccessAudit"/> row with <c>TargetKind =
///       "event"</c> (C3).</item>
/// <item><b>The detail is a single <c>CanAsync(Read)</c> decision</b> with the
///       404-vs-403 split: a missing id and a <b>draft</b> seen by a
///       non-author are <see cref="KeyNotFoundException"/> (the Web 404, the
///       non-leaky pin); a denied read (an audience the actor is not in) is an
///       <see cref="UnauthorizedAccessException"/> (the Web 403).</item>
/// <item><b>The draft gate is author-only</b> (ADR 0037) — a
///       <see cref="Event.IsDraft"/> event bypasses the authorization decision
///       entirely: a pure <c>AuthorId == actorId</c> ordinal check, no
///       <c>CanAsync</c> call, no audit row. Feeds exclude drafts
///       unconditionally.</item>
/// <item><b>The standing matrix is a pure role-claim check</b> (ADR 0054
///       §3.4, the <see cref="PageService.CheckCreateStanding"/> /
///       <see cref="PageService.CheckEditStanding"/> shape): create = any
///       signed-in resident (they become the author); edit = author ∪
///       GlobalAdmin. A null event is a 404; a denied actor is a 403. No new
///       <c>AccessAction</c>, no new <c>AccessVia</c>, no branch in the frozen
///       <c>IAuthorizationService</c>.</item>
/// </list>
/// <para>
/// The write lanes (<see cref="IEventService.CreateAsync"/> /
/// <see cref="IEventService.UpdateAsync"/> /
/// <see cref="IEventService.PublishAsync"/> /
/// <see cref="IEventService.DeleteAsync"/> /
/// <see cref="IEventService.RsvpAsync"/>) are <b>U04</b> and remain
/// <see cref="NotImplementedException"/>; this unit does **not** drive them.
/// The full 23-name <c>M4_*</c> list (U09) is out of scope here — only the
/// read + standing names above are pinned.
/// </para>
/// </summary>
public class EventServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — M4_FeedVisibleToAudienceMember (feed, C6 / C3) ───────────────────
    //
    // An audience grantee reads the upcoming-events feed: their event is in
    // the visible set; a stranger's feed does not include it (the
    // MatchGroups branch, branch 6). The aggregate audit row (TargetKind
    // "event") is written for the grantee's visit.

    [Fact]
    public async Task M4_FeedVisibleToAudienceMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f1-author";
        const string grantee = "u-u03-f1-grantee";
        const string stranger = "u-u03-f1-stranger";

        await Plant(store, new Event
        {
            Id = "f1-ev", AuthorId = author,
            Title = "Cleanup day", Body = "body f1",
            Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 1, 13, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = Audience(GrantKind.User, grantee),
        });

        // The grantee sees the event in the feed (branch 6 MatchGroups).
        var granteeFeed = await svc.ListUpcomingAsync(null, grantee, 1);
        Assert.Contains("f1-ev", granteeFeed.Select(e => e.Id));

        // A stranger is denied the event (branch 7 Deny — the audience does
        // not match the stranger, and the stranger is not the owner).
        var strangerFeed = await svc.ListUpcomingAsync(null, stranger, 1);
        Assert.DoesNotContain("f1-ev", strangerFeed.Select(e => e.Id));
    }

    // ── 2 — M4_FeedPublicEventVisibleToResident (null Audience = public) ─────
    //
    // A null-<see cref="Event.Audience"/> event is **public** (the frozen
    // Decide() branch 5): any signed-in resident sees it in the feed. The
    // empty-audience-denies invariant (C1) applies only to a *non-null* empty
    // audience; a null audience is world-readable.

    [Fact]
    public async Task M4_FeedPublicEventVisibleToResident()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f2-author";
        const string resident = "u-u03-f2-resident";

        await Plant(store, new Event
        {
            Id = "f2-ev", AuthorId = author,
            Title = "Town hall", Body = "body f2",
            Start = new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = null, // public — branch 5
        });

        var feed = await svc.ListUpcomingAsync(null, resident, 1);
        Assert.Contains("f2-ev", feed.Select(e => e.Id));

        // Detail: the resident reads the public event via a single CanAsync
        // decision (branch 5) — no denial, no 403.
        var ev = await svc.GetAsync("f2-ev", resident);
        Assert.Equal("Town hall", ev.Title);
    }

    // ── 3 — M4_CommunityAudienceVisibleToMember (ADR 0036) ───────────────────
    //
    // A <see cref="Audience.Community"/> event on a component is visible to a
    // member of that component (branch 4) but not to a resident who is not a
    // member (branch 7 Deny). Seeded through the frozen
    // <see cref="IUserInfoService.SetCommunityMembershipAsync"/> seam (the
    // <c>communityIds</c> the Decide() Community branch reads).

    [Fact]
    public async Task M4_CommunityAudienceVisibleToMember()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-u03-f3-author";
        const string member = "u-u03-f3-member";
        const string nonMember = "u-u03-f3-nonmember";
        const string comp = "c-u03-f3";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await userInfo.SetCommunityMembershipAsync(comp, member, actorId: "u-u03-f3-admin");

        await Plant(store, new Event
        {
            Id = "f3-ev", AuthorId = author, ComponentId = comp,
            Title = "Safety drill", Body = "body f3",
            Start = new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = new Audience { Community = true }, // branch 4
        });

        // The community member sees the event (branch 4 — the live
        // communityIds contain the event's ComponentId).
        var memberFeed = await svc.ListUpcomingAsync(comp, member, 1);
        Assert.Contains("f3-ev", memberFeed.Select(e => e.Id));

        // A non-member resident is denied (branch 7 — the Community flag is
        // set but the actor's communityIds do not contain the component).
        var nonMemberFeed = await svc.ListUpcomingAsync(comp, nonMember, 1);
        Assert.DoesNotContain("f3-ev", nonMemberFeed.Select(e => e.Id));

        // Detail 404-vs-403 split: the member reads it (Allow); the
        // non-member is denied with a 403 (an audience the actor is not in).
        await svc.GetAsync("f3-ev", member);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetAsync("f3-ev", nonMember));
    }

    // ── 4 — M4_GrantAudienceOnlyGranteeSees (MatchGroups branch) ─────────────
    //
    // A group-grant event is visible to a group member (branch 6) but not to
    // a resident outside the group. The membership row is the live data
    // <c>MatchGroups</c> reads (C4 strong consistency).

    [Fact]
    public async Task M4_GrantAudienceOnlyGranteeSees()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-u03-f4-author";
        const string member = "u-u03-f4-member";
        const string outsider = "u-u03-f4-outsider";
        const string group = "g-u03-f4";

        // A real group under the exact id the event's audience names (the
        // audience references the group id literally; the GroupMembership row
        // is what MatchGroups reads — C4's live-row lane).
        await Plant(store, new Group
        {
            Id = group, Name = "Book club", OwnerId = author,
            Created = DateTimeOffset.UtcNow,
        });
        await userInfo.AddGroupMemberAsync(group, member, addedBy: author);

        await Plant(store, new Event
        {
            Id = "f4-ev", AuthorId = author,
            Title = "Book club", Body = "body f4",
            Start = new DateTimeOffset(2026, 3, 4, 18, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 4, 19, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = Audience(GrantKind.Group, group),
        });

        var memberFeed = await svc.ListUpcomingAsync(null, member, 1);
        Assert.Contains("f4-ev", memberFeed.Select(e => e.Id));

        var outsiderFeed = await svc.ListUpcomingAsync(null, outsider, 1);
        Assert.DoesNotContain("f4-ev", outsiderFeed.Select(e => e.Id));
    }

    // ── 5 — M4_DraftInvisibleToNonAuthor (ADR 0037 draft gate) ───────────────
    //
    // A draft event bypasses the authorization decision: it is visible to
    // **everyone except its author** — a pure <c>AuthorId == actorId</c>
    // ordinal check, no <c>CanAsync</c>, no audit row. A non-author (even a
    // GlobalAdmin) is denied with a 404 (the non-leaky pin). And drafts are
    // excluded from the feed **unconditionally** (not just for non-authors).

    [Fact]
    public async Task M4_DraftInvisibleToNonAuthor()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f5-author";
        const string stranger = "u-u03-f5-stranger";

        await Plant(store, new Event
        {
            Id = "f5-ev", AuthorId = author,
            Title = "Unpublished", Body = "draft body",
            Start = new DateTimeOffset(2026, 3, 5, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero),
            IsDraft = true,
            Audience = null, // would be public if it were published
        });

        // The author sees their own draft (the draft gate's Allow).
        var ev = await svc.GetAsync("f5-ev", author);
        Assert.Equal("Unpublished", ev.Title);

        // A non-author is denied with a 404 (KeyNotFoundException) — the
        // non-leaky pin, NOT a 403 (no "denied" signal that the event
        // exists).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetAsync("f5-ev", stranger));

        // The draft is excluded from the feed unconditionally (the candidate
        // set filters !IsDraft) — neither the author nor the stranger sees it
        // in the feed.
        Assert.DoesNotContain("f5-ev", (await svc.ListUpcomingAsync(null, author, 1)).Select(e => e.Id));
        Assert.DoesNotContain("f5-ev", (await svc.ListUpcomingAsync(null, stranger, 1)).Select(e => e.Id));
    }

    // ── 6 — M4_FeedOrderedStartAscending (feed ordering) ─────────────────────
    //
    // The feed is ordered by <see cref="Event.Start"/> ascending (the
    // upcoming-events shape, the <c>(ComponentId, Start)</c> index) — the
    // <see cref="M4DocTypes.Configure"/> ordering pin.

    [Fact]
    public async Task M4_FeedOrderedStartAscending()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f6-author";
        const string resident = "u-u03-f6-resident";

        var t = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);
        // Plant in an order deliberately different from the expected sort
        // order, so the test would fail if the query returned insertion
        // order instead of Start ascending.
        await Plant(store, new Event { Id = "mid", AuthorId = author, Title = "mid", Body = "m", Start = t, End = t, IsDraft = false, Audience = null });
        await Plant(store, new Event { Id = "late", AuthorId = author, Title = "late", Body = "l", Start = t.AddDays(1), End = t.AddDays(1), IsDraft = false, Audience = null });
        await Plant(store, new Event { Id = "early", AuthorId = author, Title = "early", Body = "e", Start = t.AddDays(-1), End = t.AddDays(-1), IsDraft = false, Audience = null });

        var feed = await svc.ListUpcomingAsync(null, resident, 1);
        // All three are public + published + non-deleted ⇒ all visible.
        Assert.Equal(3, feed.Count);
        // Start ascending: early → mid → late.
        Assert.Equal(new[] { "early", "mid", "late" }, feed.Select(e => e.Id).ToArray());
    }

    // ── 7 — M4_SoftDeletedExcludedFromFeedAndDetail (ADR 0024) ───────────────
    //
    // A soft-deleted event (<see cref="Event.IsDeleted"/> = true) is filtered
    // out of both the feed (the candidate set's <c>!IsDeleted</c> predicate)
    // and the detail (the <see cref="EventService.GetAsync"/> IsDeleted 404).
    // The record is kept, never hard-deleted.

    [Fact]
    public async Task M4_SoftDeletedExcludedFromFeedAndDetail()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f7-author";
        const string resident = "u-u03-f7-resident";

        await Plant(store, new Event
        {
            Id = "f7-live", AuthorId = author, Title = "Live", Body = "b",
            Start = new DateTimeOffset(2026, 3, 11, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 11, 10, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new Event
        {
            Id = "f7-deleted", AuthorId = author, Title = "Deleted", Body = "b",
            Start = new DateTimeOffset(2026, 3, 11, 11, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 11, 12, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = true, Audience = null,
        });

        var feed = await svc.ListUpcomingAsync(null, resident, 1);
        Assert.Contains("f7-live", feed.Select(e => e.Id));
        Assert.DoesNotContain("f7-deleted", feed.Select(e => e.Id));

        // Detail: the live event is readable; the deleted event is a 404
        // (the non-leaky pin — same "not found" as a missing id).
        await svc.GetAsync("f7-live", resident);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetAsync("f7-deleted", resident));

        // A missing id is also a 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetAsync("no-such-event", resident));
    }

    // ── 8 — M4_GetRsvps_ReturnsRsvps (owner list read) ───────────────────────
    //
    // <see cref="EventService.GetRsvpsAsync"/> is the owner-only RSVP list
    // (ADR 0054 §3.2). The <see cref="IEventService"/> seam carries <em>no</em>
    // actor (the owner gate is the Web controller's job, U05); this method
    // loads the event's RSVPs once the event is confirmed present and
    // non-deleted. A missing event is a 404.

    [Fact]
    public async Task M4_GetRsvps_ReturnsRsvps()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f8-author";
        const string rsvp1 = "u-u03-f8-r1";
        const string rsvp2 = "u-u03-f8-r2";

        await Plant(store, new Event
        {
            Id = "f8-ev", AuthorId = author, Title = "BBQ", Body = "b",
            Start = new DateTimeOffset(2026, 3, 12, 17, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 12, 21, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null,
        });
        await Plant(store, new EventRsvp
        {
            Id = "r1", EventId = "f8-ev", UserId = rsvp1,
            Status = RsvpStatus.Going, At = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
        });
        await Plant(store, new EventRsvp
        {
            Id = "r2", EventId = "f8-ev", UserId = rsvp2,
            Status = RsvpStatus.Maybe, At = new DateTimeOffset(2026, 3, 1, 11, 0, 0, TimeSpan.Zero),
        });

        var rsvps = await svc.GetRsvpsAsync("f8-ev");
        Assert.Equal(2, rsvps.Count);
        Assert.Contains(rsvps, r => r.UserId == rsvp1 && r.Status == RsvpStatus.Going);
        Assert.Contains(rsvps, r => r.UserId == rsvp2 && r.Status == RsvpStatus.Maybe);

        // A missing event is a 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetRsvpsAsync("no-such-event"));
    }

    // ── 9 — M4_GetMyRsvp_ReturnsOwn (last-write-wins read) ───────────────────
    //
    // <see cref="EventService.GetMyRsvpAsync"/> is the actor's **own** RSVP
    // (the last-write-wins read, §3.2): returns the resident's single
    // <c>(EventId, UserId)</c> row, or <c>null</c> when the resident has not
    // RSVPed. The event's <c>CanAsync(Read)</c> decision gates the read.

    [Fact]
    public async Task M4_GetMyRsvp_ReturnsOwn()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f9-author";
        const string me = "u-u03-f9-me";
        const string other = "u-u03-f9-other";

        await Plant(store, new Event
        {
            Id = "f9-ev", AuthorId = author, Title = "Walk", Body = "b",
            Start = new DateTimeOffset(2026, 3, 13, 7, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 13, 8, 0, 0, TimeSpan.Zero),
            IsDraft = false, IsDeleted = false, Audience = null, // public
        });
        await Plant(store, new EventRsvp
        {
            Id = "f9-r-me", EventId = "f9-ev", UserId = me,
            Status = RsvpStatus.Going, At = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero),
        });
        // Another resident's RSVP — must not be returned for `me`.
        await Plant(store, new EventRsvp
        {
            Id = "f9-r-other", EventId = "f9-ev", UserId = other,
            Status = RsvpStatus.No, At = new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.Zero),
        });

        // The actor's own RSVP is returned (not the other resident's).
        var mine = await svc.GetMyRsvpAsync("f9-ev", me);
        Assert.NotNull(mine);
        Assert.Equal(me, mine!.UserId);
        Assert.Equal(RsvpStatus.Going, mine.Status);

        // A resident who has not RSVPed gets null (not an error).
        var otherMine = await svc.GetMyRsvpAsync("f9-ev", other);
        Assert.NotNull(otherMine);
        Assert.Equal(RsvpStatus.No, otherMine!.Status);

        // A resident with no RSVP row gets null.
        var nobody = await svc.GetMyRsvpAsync("f9-ev", "u-u03-f9-nobody");
        Assert.Null(nobody);
    }

    // ── 10 — M4_GetMyRsvp_DraftNonAuthor_404 (draft gate, RSVP lane) ─────────
    //
    // The draft gate (ADR 0037) applies to the own-RSVP read too: a draft
    // event's RSVP is invisible to a non-author (404, the non-leaky pin) and
    // visible to the author (a pure ordinal check, no CanAsync).

    [Fact]
    public async Task M4_GetMyRsvp_DraftNonAuthor_404()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u03-f10-author";
        const string stranger = "u-u03-f10-stranger";

        await Plant(store, new Event
        {
            Id = "f10-ev", AuthorId = author, Title = "Draft BBQ", Body = "b",
            Start = new DateTimeOffset(2026, 3, 14, 17, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 3, 14, 21, 0, 0, TimeSpan.Zero),
            IsDraft = true, IsDeleted = false, Audience = null,
        });
        await Plant(store, new EventRsvp
        {
            Id = "f10-r", EventId = "f10-ev", UserId = stranger,
            Status = RsvpStatus.Going, At = new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero),
        });

        // The non-author's own RSVP of a draft event is a 404 (denied).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetMyRsvpAsync("f10-ev", stranger));

        // The author may read their own draft event's RSVP (the author lane).
        await Plant(store, new EventRsvp
        {
            Id = "f10-r-author", EventId = "f10-ev", UserId = author,
            Status = RsvpStatus.Going, At = new DateTimeOffset(2026, 3, 3, 9, 1, 0, TimeSpan.Zero),
        });
        var authorRsvp = await svc.GetMyRsvpAsync("f10-ev", author);
        Assert.NotNull(authorRsvp);
        Assert.Equal(author, authorRsvp!.UserId);
    }

    // ════════════════════════════════════════════════════════════════════════
    // Standing-matrix gate helpers (pure — no store, no async).
    // ADR 0054 §3.4: create = any signed-in resident; edit = author ∪
    // GlobalAdmin. The PageService.CheckCreateStanding / CheckEditStanding
    // shape; the ADR 0014 / 0016 / 0017 precedent.
    // ════════════════════════════════════════════════════════════════════════

    // ── 11 — M4_CheckCreateStanding_SignedIn_Resident_Allows ─────────────────
    //
    // Any signed-in resident may create an event (they become the author —
    // the Owner branch). The "signed-in" constraint is the actorId being
    // non-empty. A null event is a 404 (a data bug).

    [Fact]
    public void M4_CheckCreateStanding_SignedIn_Resident_Allows()
    {
        var @event = new Event { Id = "s1-ev", AuthorId = "u-s1", IsDraft = true };

        // A plain Member (any signed-in resident) may create.
        EventService.CheckCreateStanding("u-s1", new HashSet<string> { Roles.Member }, @event);
        // No exception ⇒ pass.

        // A GlobalAdmin may create too.
        EventService.CheckCreateStanding("u-s1", new HashSet<string> { Roles.Member, Roles.GlobalAdmin }, @event);
    }

    // ── 12 — M4_CheckCreateStanding_NoActor_Denies ───────────────────────────
    //
    // A null/empty actor (an anonymous caller — the Web [Authorize] would
    // have stopped them, this re-checks at the Core layer) is denied with a
    // 403.

    [Fact]
    public void M4_CheckCreateStanding_NoActor_Denies()
    {
        var @event = new Event { Id = "s2-ev", AuthorId = "u-s2", IsDraft = true };

        await_ThrowsUnauthorized(() => EventService.CheckCreateStanding("", new HashSet<string> { Roles.Member }, @event));
        await_ThrowsUnauthorized(() => EventService.CheckCreateStanding(null, new HashSet<string> { Roles.Member }, @event));
    }

    // ── 13 — M4_CheckCreateStanding_NullEvent_404 ────────────────────────────
    //
    // A null event is a data bug — a 404 (the Web layer's not-found).

    [Fact]
    public void M4_CheckCreateStanding_NullEvent_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => EventService.CheckCreateStanding("u-s3", new HashSet<string> { Roles.Member }, null));
    }

    // ── 14 — M4_CheckEditStanding_Author_Allows ──────────────────────────────
    //
    // The event's author may edit it (the Owner branch) — the standing
    // matrix's edit row.

    [Fact]
    public void M4_CheckEditStanding_Author_Allows()
    {
        var @event = new Event { Id = "s4-ev", AuthorId = "u-s4-author", IsDraft = false };

        EventService.CheckEditStanding("u-s4-author", new HashSet<string> { Roles.Member }, @event);
        // No exception ⇒ pass.
    }

    // ── 15 — M4_CheckEditStanding_GlobalAdmin_Allows ─────────────────────────
    //
    // A GlobalAdmin may edit any event (the ADR 0017 override shape —
    // contrast ADR 0037's publish lane, where a GlobalAdmin is <em>denied</em>).

    [Fact]
    public void M4_CheckEditStanding_GlobalAdmin_Allows()
    {
        var @event = new Event { Id = "s5-ev", AuthorId = "u-s5-author", IsDraft = false };

        // A GlobalAdmin who is NOT the author may still edit (the Admin
        // override).
        EventService.CheckEditStanding("u-s5-admin", new HashSet<string> { Roles.GlobalAdmin }, @event);
        // No exception ⇒ pass.
    }

    // ── 16 — M4_CheckEditStanding_Stranger_Denies ────────────────────────────
    //
    // A non-author, non-GlobalAdmin resident is denied with a 403.

    [Fact]
    public void M4_CheckEditStanding_Stranger_Denies()
    {
        var @event = new Event { Id = "s6-ev", AuthorId = "u-s6-author", IsDraft = false };

        await_ThrowsUnauthorized(() => EventService.CheckEditStanding("u-s6-stranger", new HashSet<string> { Roles.Member }, @event));
    }

    // ── 17 — M4_CheckEditStanding_NullEvent_404 ──────────────────────────────
    //
    // A null event is a data bug — a 404 (the Web layer's not-found), even
    // for a GlobalAdmin (the null-check precedes the role check).

    [Fact]
    public void M4_CheckEditStanding_NullEvent_404()
    {
        Assert.Throws<KeyNotFoundException>(
            () => EventService.CheckEditStanding("u-s7-admin", new HashSet<string> { Roles.GlobalAdmin }, null));
    }

    // ════════════════════════════════════════════════════════════════════════
    // U04 — the five write lanes (create / update / publish / delete / rsvp).
    // ADR 0054 §3.4 / §3.5: standing re-checked server-side (C3 single-source),
    // the AccessAudit row (TargetKind "event", the event.* action, Via Owner,
    // Outcome Allow) committed atomically with the write (C3), and the RSVP
    // lane's last-write-wins + no-audit-row pin (§3.2).
    // ════════════════════════════════════════════════════════════════════════

    // ── U04·1 — M4_CreateWritesEventAndAuditRow ─────────────────────────────
    // CreateAsync (a plain resident) writes the event verbatim (ADR 0001-B),
    // sets AuthorId = actor, mints a draft, and stores one AccessAudit row
    // (event.create, TargetKind "event", Via Owner, Outcome Allow).

    [Fact]
    public async Task M4_CreateWritesEventAndAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-c1-author";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Cleanup day",
            Body = "Bring gloves",
            Start = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            Audience = null,            // public (branch 5)
            IsDraft = true,             // ADR 0037 — a new event is a draft
        });

        Assert.False(string.IsNullOrEmpty(ev.Id));
        Assert.Equal(author, ev.AuthorId);            // the author becomes the standing owner.
        Assert.Equal("Cleanup day", ev.Title);
        Assert.Equal("Bring gloves", ev.Body);
        Assert.True(ev.IsDraft);
        Assert.Equal("en", ev.LanguageCode);          // ADR 0018 — the instance-default floor ("" → en).
        Assert.NotNull(ev.Created);

        var rows = await EventAuditRows(store);
        var create = Assert.Single(rows);
        Assert.Equal("event.create", create.Action);
        Assert.Equal("event", create.TargetKind);     // the exact string (C3 — the adapter's discriminator).
        Assert.Equal(ev.Id, create.TargetId);
        Assert.Equal(author, create.ActorId);
        Assert.Equal(author, create.EffectivePrincipalId);
        Assert.Equal(AccessVia.Owner, create.Via);
        Assert.Equal(AccessOutcome.Allow, create.Outcome);
    }

    // ── U04·2 — M4_CreateAudienceWrittenVerbatim (ADR 0001-B) ───────────────
    // The composer's audience choice is written verbatim (never mutated): a
    // user-grant event is visible to the grantee and denied to a stranger.

    [Fact]
    public async Task M4_CreateAudienceWrittenVerbatim()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-c2-author";
        const string grantee = "u-u04-c2-grantee";
        const string stranger = "u-u04-c2-stranger";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "Members only", Body = "b",
            Start = new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 2, 11, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, grantee),
            IsDraft = false,                            // published so the feed/decision path runs.
        });

        // The stored row carries the audience verbatim (bit-identical to the input).
        Assert.NotNull(ev.Audience);
        Assert.Equal(1, ev.Audience!.Grants.Count);
        Assert.Equal(GrantKind.User, ev.Audience.Grants[0].Kind);
        Assert.Equal(grantee, ev.Audience.Grants[0].Id);

        // The grantee sees it; the stranger is denied the detail (403).
        await svc.GetAsync(ev.Id, grantee);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetAsync(ev.Id, stranger));
    }

    // ── U04·3 — M4_CreateNoActor_Denies ─────────────────────────────────────
    // An empty actor (an anonymous caller) is denied with a 403 — the Web
    // [Authorize] would have stopped them; this re-checks at the Core layer.

    [Fact]
    public async Task M4_CreateNoActor_Denies()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        // An empty actor is a 403 — thrown before any store access (the
        // CheckCreateStanding denial). No event row and no audit row land.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateAsync("", new CreateEventRequest { Title = "t" }));
        var rows = await EventAuditRows(store);
        Assert.Empty(rows);
    }

    // ── U04·4 — M4_UpdateAuthorEditsAndPreservesAuthor (ADR 0014/0016/0017) ─
    // The author edits the event: Title/Body change, AuthorId + Created are
    // preserved untouched, Modified is stamped (a real change), and one
    // event.update audit row is stored.

    [Fact]
    public async Task M4_UpdateAuthorEditsAndPreservesAuthor()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-u1-author";
        var created = new DateTimeOffset(2026, 4, 3, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "old title", Body = "old body",
            Start = created, End = created, IsDraft = false,
        });
        var createdBefore = ev.Created;

        var updated = await svc.UpdateAsync(ev.Id, author, new UpdateEventRequest
        {
            Title = "new title", Body = "new body",
            Start = created, End = created,
        });

        Assert.Equal("new title", updated.Title);
        Assert.Equal("new body", updated.Body);
        Assert.Equal(author, updated.AuthorId);            // preserved untouched.
        Assert.Equal(createdBefore, updated.Created);      // preserved untouched.
        Assert.NotNull(updated.Modified);                  // a real change ⇒ stamped.

        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.update" && r.TargetId == ev.Id);
    }

    // ── U04·5 — M4_UpdateNoOpDoesNotStampModified (no-op re-save pin) ───────
    // A no-op re-save of an unchanged event does **not** stamp Modified (the
    // AnnouncementService.UpdateAsync shape) — the change-detection pin.

    [Fact]
    public async Task M4_UpdateNoOpDoesNotStampModified()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-u2-author";
        var created = new DateTimeOffset(2026, 4, 4, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "unchanged", Body = "body",
            Start = created, End = created, IsDraft = false,
        });
        Assert.Null(ev.Modified);   // a fresh create does not stamp Modified.

        // Re-save the exact same values — a no-op.
        var updated = await svc.UpdateAsync(ev.Id, author, new UpdateEventRequest
        {
            Title = "unchanged", Body = "body",
            Start = created, End = created,
        });
        Assert.Null(updated.Modified);   // no real change ⇒ the stamp stays untouched.
    }

    // ── U04·6 — M4_UpdateReparsesMediaIds (RC ADR 0025 / ATT ADR 0034) ──────
    // The edit lane persists the server-side-parsed content-image + attachment
    // ids (the client never sends a form field): a body referencing
    // /content-image/{id} and /attachment/{id} yields the matching ImageIds /
    // AttachmentIds lists on the stored row.

    [Fact]
    public async Task M4_UpdateReparsesMediaIds()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-u3-author";
        var created = new DateTimeOffset(2026, 4, 5, 9, 0, 0, TimeSpan.Zero);

        // The Web layer would parse these from the body (ContentImageIds /
        // AttachmentIds.Extract*); here we pass the parsed ids verbatim to the
        // request (the client never sends a *form field* — the parsed ids are
        // the server-side truth).
        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "t", Body = "see ![a](/content-image/aa11) and [f](/attachment/bb22)",
            Start = created, End = created,
            ImageIds = new[] { "aa11" },
            AttachmentIds = new[] { "bb22" },
        });
        Assert.Equal(new[] { "aa11" }, ev.ImageIds);
        Assert.Equal(new[] { "bb22" }, ev.AttachmentIds);

        // Edit to new media refs (replace-style — the re-parse is authoritative).
        var updated = await svc.UpdateAsync(ev.Id, author, new UpdateEventRequest
        {
            Title = "t", Body = "![b](/content-image/cc33) and [g](/attachment/dd44)",
            Start = created, End = created,
            ImageIds = new[] { "cc33" },
            AttachmentIds = new[] { "dd44" },
        });
        Assert.Equal(new[] { "cc33" }, updated.ImageIds);
        Assert.Equal(new[] { "dd44" }, updated.AttachmentIds);
    }

    // ── U04·7 — M4_UpdateStrangerDenied (C3 standing, server-side) ──────────
    // A non-author, non-GlobalAdmin resident editing an event is denied with a
    // 403 (the author branch of the standing matrix — the C3 single-source pin;
    // the Web [Authorize] is not the source of truth).

    [Fact]
    public async Task M4_UpdateStrangerDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-u4-author";
        const string stranger = "u-u04-u4-stranger";
        var created = new DateTimeOffset(2026, 4, 6, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "owned", Body = "b", Start = created, End = created, IsDraft = false,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.UpdateAsync(ev.Id, stranger, new UpdateEventRequest { Title = "hijack", Start = created, End = created }));
    }

    // ── U04·8 — M4_UpdateMissingEvent_404 ───────────────────────────────────
    // A missing event id is a 404 (KeyNotFoundException), not a 403 — the
    // non-leaky pin, the AnnouncementService edit-lane shape.

    [Fact]
    public async Task M4_UpdateMissingEvent_404()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.UpdateAsync("no-such-event", "u-u04-u5", new UpdateEventRequest { Title = "t" }));
    }

    // ── U04·9 — M4_PublishAuthorOnly (ADR 0037) ─────────────────────────────
    // Publishing flips IsDraft → false so the event enters the feed, is
    // readable via the normal decision path, and stores one event.publish
    // audit row. The author-only pin is enforced (the ADR 0037 shape).

    [Fact]
    public async Task M4_PublishAuthorOnly()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-p1-author";
        const string resident = "u-u04-p1-resident";
        var created = new DateTimeOffset(2026, 4, 7, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "draft", Body = "b", Start = created, End = created,
            IsDraft = true, Audience = null,
        });

        // A draft is excluded from the feed and invisible to a non-author.
        Assert.DoesNotContain(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetAsync(ev.Id, resident));

        var published = await svc.PublishAsync(ev.Id, author);
        Assert.False(published.IsDraft);

        // Now the feed shows it and a public reader can open it.
        Assert.Contains(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await svc.GetAsync(ev.Id, resident);

        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.publish" && r.TargetId == ev.Id);
    }

    // ── U04·10 — M4_PublishStrangerDenied (ADR 0037 author-only) ────────────
    // A non-author publishing is denied with a 403 — even at GlobalAdmin
    // (ADR 0037's author-only pin: publishing is the author's choice, not an
    // admin's lever). Here the stranger is a plain resident.

    [Fact]
    public async Task M4_PublishStrangerDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-p2-author";
        const string stranger = "u-u04-p2-stranger";
        var created = new DateTimeOffset(2026, 4, 8, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "draft", Body = "b", Start = created, End = created, IsDraft = true,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.PublishAsync(ev.Id, stranger));
        // The event is still a draft.
        var still = await svc.GetAsync(ev.Id, author);
        Assert.True(still.IsDraft);
    }

    // ── U04·11 — M4_PublishIdempotentNoDoubleStamp (no-op-re-save pin) ──────
    // A second publish on an already-live event is a no-op — it does not
    // re-stamp Modified.

    [Fact]
    public async Task M4_PublishIdempotentNoDoubleStamp()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-p3-author";
        var created = new DateTimeOffset(2026, 4, 9, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "d", Body = "b", Start = created, End = created, IsDraft = true,
        });
        var first = await svc.PublishAsync(ev.Id, author);
        var modifiedAfterFirst = first.Modified;
        Assert.NotNull(modifiedAfterFirst);

        var second = await svc.PublishAsync(ev.Id, author);
        Assert.False(second.IsDraft);
        Assert.Equal(modifiedAfterFirst, second.Modified);   // no re-stamp on the no-op.
    }

    // ── U04·12 — M4_SoftDeleteAuthorExcludesFromFeedAndDetail (ADR 0024) ────
    // Soft-delete (the author lane) sets IsDeleted; the event leaves the feed
    // and the detail is a 404 (the non-leaky pin — same as a missing id). One
    // event.delete audit row is stored.

    [Fact]
    public async Task M4_SoftDeleteAuthorExcludesFromFeedAndDetail()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-d1-author";
        const string resident = "u-u04-d1-resident";
        var created = new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "live", Body = "b", Start = created, End = created,
            IsDraft = false, Audience = null,
        });
        Assert.Contains(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await svc.GetAsync(ev.Id, resident);

        await svc.DeleteAsync(ev.Id, author);

        // Gone from the feed; the detail is a 404 (non-leaky pin).
        Assert.DoesNotContain(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetAsync(ev.Id, resident));

        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.delete" && r.TargetId == ev.Id);
    }

    // ── U04·13 — M4_SoftDeleteStrangerDenied ────────────────────────────────
    // A non-author, non-GlobalAdmin resident deleting an event is denied with
    // a 403 (the author branch of the standing matrix, the C3 pin).

    [Fact]
    public async Task M4_SoftDeleteStrangerDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-d2-author";
        const string stranger = "u-u04-d2-stranger";
        var created = new DateTimeOffset(2026, 4, 11, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "owned", Body = "b", Start = created, End = created, IsDraft = false,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteAsync(ev.Id, stranger));
        // The event is still live (not deleted).
        Assert.Contains(ev.Id, (await svc.ListUpcomingAsync(null, author, 1)).Select(e => e.Id));
    }

    // ── U04·14 — M4_RsvpLastWriteWins (ADR 0054 §3.2) ───────────────────────
    // RSVP is last-write-wins, keyed per (EventId, UserId): the same resident
    // RSVPing twice collapses to a single row carrying their latest status,
    // with the At stamp updated.

    [Fact]
    public async Task M4_RsvpLastWriteWins()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-r1-author";
        const string rsvp1 = "u-u04-r1-user";
        var created = new DateTimeOffset(2026, 4, 12, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "BBQ", Body = "b", Start = created, End = created, IsDraft = false, Audience = null,
        });

        var going = await svc.RsvpAsync(ev.Id, rsvp1, RsvpStatus.Going);
        Assert.Equal(RsvpStatus.Going, going.Status);

        var no = await svc.RsvpAsync(ev.Id, rsvp1, RsvpStatus.No);
        Assert.Equal(RsvpStatus.No, no.Status);          // last write wins.
        Assert.True(no.At >= going.At);                  // the At stamp advanced (or held).
        Assert.Equal(going.Id, no.Id);                   // the same row (the unique business key).

        // Exactly one RSVP row for this resident on this event.
        var all = await svc.GetRsvpsAsync(ev.Id);
        Assert.Single(all.Where(r => r.UserId == rsvp1));
        Assert.Equal(RsvpStatus.No, all.Single(r => r.UserId == rsvp1).Status);
    }

    // ── U04·15 — M4_RsvpDistinctResidentsCoexist ────────────────────────────
    // Two different residents each hold one row (the (EventId, UserId) unique
    // key is per-resident, not per-event) — the last-write-wins lane is keyed
    // per user.

    [Fact]
    public async Task M4_RsvpDistinctResidentsCoexist()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-r2-author";
        const string u1 = "u-u04-r2-u1";
        const string u2 = "u-u04-r2-u2";
        var created = new DateTimeOffset(2026, 4, 13, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "BBQ", Body = "b", Start = created, End = created, IsDraft = false, Audience = null,
        });

        await svc.RsvpAsync(ev.Id, u1, RsvpStatus.Going);
        await svc.RsvpAsync(ev.Id, u2, RsvpStatus.Maybe);

        var all = await svc.GetRsvpsAsync(ev.Id);
        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.UserId == u1 && r.Status == RsvpStatus.Going);
        Assert.Contains(all, r => r.UserId == u2 && r.Status == RsvpStatus.Maybe);
    }

    // ── U04·16 — M4_RsvpMissingEvent_404 ────────────────────────────────────
    // RSVP to a missing event is a 404 (KeyNotFoundException) — the non-leaky
    // pin, the GetRsvpsAsync shape.

    [Fact]
    public async Task M4_RsvpMissingEvent_404()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.RsvpAsync("no-such-event", "u-u04-r3", RsvpStatus.Going));
    }

    // ── U04·17 — M4_RsvpWritesNoAccessAuditRow (ADR 0054 §3.2) ──────────────
    // An RSVP stores **no** AccessAudit row (a routine resident action, not an
    // access decision — the same posture as a profile edit). Zero event.*
    // audit rows are created by the RSVP lane.

    [Fact]
    public async Task M4_RsvpWritesNoAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u04-r4-author";
        const string rsvp1 = "u-u04-r4-user";
        var created = new DateTimeOffset(2026, 4, 14, 9, 0, 0, TimeSpan.Zero);

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "BBQ", Body = "b", Start = created, End = created, IsDraft = false, Audience = null,
        });
        // The create lane wrote exactly one row (event.create).
        var before = await EventAuditRows(store);
        Assert.Equal(1, before.Count);
        Assert.Equal("event.create", before.Single().Action);

        // The RSVP lane must add **no** audit row.
        await svc.RsvpAsync(ev.Id, rsvp1, RsvpStatus.Going);

        var after = await EventAuditRows(store);
        Assert.Equal(1, after.Count);                          // still exactly one (the create).
        Assert.DoesNotContain(after, r => r.Action == "event.rsvp");   // no such action exists.
    }

    // ════════════════════════════════════════════════════════════════════════
    // U09 — the 8 §3.7 pinned names not yet named in this file
    // (T02 / T06 / T07 / T08 / T09 / T14 / T15 / T17 / T18).
    // Each mirrors the §3.7 master-list shape exactly; the existing U03/U04
    // tests above already cover most of the underlying behaviour — these
    // add the §3.7-named pins so the U11 gate (23 names) is complete.
    // ════════════════════════════════════════════════════════════════════════

    // ── T02 — M4_NullAudienceEventIsPublic (§3.3 — Decide() branch 5) ──────
    // A null-Audience event is public (world-readable): the frozen CanAsync
    // decision for an anonymous actor (empty actorId) is Allow via branch 5
    // (Audience is null). A signed-in resident also sees it. The §3.7 name
    // is "unauthenticated sees it" — branch 5, the public pin.

    [Fact]
    public async Task M4_NullAudienceEventIsPublic()
    {
        var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-u09-t02-author";
        const string resident = "u-u09-t02-resident";

        await Plant(store, new Event
        {
            Id = "t02-ev", AuthorId = author,
            Title = "Open to all", Body = "body t02",
            Start = new DateTimeOffset(2026, 4, 20, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 20, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = null,   // null = public (branch 5)
        });

        // The frozen CanAsync decision for the anonymous actor is Allow
        // (branch 5 — the §3.7 "unauthenticated sees it" pin).
        await using var q = store.QuerySession();
        var ev = await q.LoadAsync<Event>("t02-ev");
        var decision = await authz.CanAsync(string.Empty, AccessAction.Read,
            new EventToAuditableResource(ev!));
        Assert.True(decision.Allowed);

        // A signed-in resident (the "member" in the §3.7 name) also sees it.
        var residentFeed = await svc.ListUpcomingAsync(null, resident, 1);
        Assert.Contains("t02-ev", residentFeed.Select(e => e.Id));
        var ev2 = await svc.GetAsync("t02-ev", resident);
        Assert.Equal("Open to all", ev2.Title);
    }

    // ── T06 — M4_PlainMemberCreateAllowed (§3.4 — create standing) ─────────
    // A plain signed-in resident (no roles) may create an event — the
    // "create = any signed-in resident" pin (ADR 0054 §3.4). The author
    // becomes the standing owner; the audit row is stored.

    [Fact]
    public async Task M4_PlainMemberCreateAllowed()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string resident = "u-u09-t06-resident";

        var ev = await svc.CreateAsync(resident, new CreateEventRequest
        {
            Title = "Plain member event", Body = "body t06",
            Start = new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        Assert.Equal(resident, ev.AuthorId);    // the resident becomes the standing owner.
        Assert.Equal("Plain member event", ev.Title);

        // The create lane's audit row is stored (C3, TargetKind "event").
        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.create" && r.TargetKind == "event" && r.TargetId == ev.Id);
    }

    // ── T07 — M4_AuthorCanEditOwnEvent (§3.4 — owner edit) ─────────────────
    // The author of an event may edit it (ADR 0014/0016/0017): Title/Body
    // change, AuthorId/Created preserved, Modified stamped, audit row stored.

    [Fact]
    public async Task M4_AuthorCanEditOwnEvent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t07-author";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "original", Body = "body t07",
            Start = new DateTimeOffset(2026, 4, 22, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 22, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });
        var createdStamp = ev.Created;

        var updated = await svc.UpdateAsync(ev.Id, author, new UpdateEventRequest
        {
            Title = "edited", Body = "new body",
            Start = ev.Start, End = ev.End,
        });

        Assert.Equal("edited", updated.Title);
        Assert.Equal("new body", updated.Body);
        Assert.Equal(author, updated.AuthorId);         // preserved untouched.
        Assert.Equal(createdStamp, updated.Created);    // preserved untouched.
        Assert.NotNull(updated.Modified);               // stamped on a real change.

        // The edit lane's audit row is stored (C3, TargetKind "event").
        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.update" && r.TargetKind == "event" && r.TargetId == ev.Id);
    }

    // ── T08 — M4_PlainMemberEditDenied (§3.4 — non-author edit ⇒ 403) ─────
    // A non-author, non-GlobalAdmin resident editing someone else's event is
    // denied with a 403 (UnauthorizedAccessException) — the C3 standing pin.

    [Fact]
    public async Task M4_PlainMemberEditDenied()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t08-author";
        const string stranger = "u-u09-t08-stranger";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "owned", Body = "body t08",
            Start = new DateTimeOffset(2026, 4, 23, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 23, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.UpdateAsync(ev.Id, stranger, new UpdateEventRequest
            {
                Title = "hijack", Body = "body t08",
                Start = ev.Start, End = ev.End,
            }));

        // The event is unchanged (the denied write did not apply).
        var still = await svc.GetAsync(ev.Id, author);
        Assert.Equal("owned", still.Title);
    }

    // ── T09 — M4_GlobalAdminOverrideEdit (§3.4 — ADR 0017 shape) ──────────
    // A GlobalAdmin who is **not** the author may edit (the ADR 0017
    // override branch of CheckEditStanding). A plain member without the
    // GlobalAdmin role is denied — the contrast proves the override is the
    // role branch, not a default allow.

    [Fact]
    public void M4_GlobalAdminOverrideEdit()
    {
        var ev = new Event
        {
            Id = "t09-ev",
            AuthorId = "u-u09-t09-author",
            Title = "owned", Body = "body t09",
            Start = new DateTimeOffset(2026, 4, 24, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 24, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
        };

        // GlobalAdmin (not the author): the override branch passes.
        var adminRoles = new HashSet<string>(StringComparer.Ordinal) { Roles.GlobalAdmin };
        EventService.CheckEditStanding("u-u09-t09-admin", adminRoles, ev);

        // A plain member (no roles, not the author): denied.
        var plainRoles = new HashSet<string>();
        Assert.Throws<UnauthorizedAccessException>(
            () => EventService.CheckEditStanding("u-u09-t09-stranger", plainRoles, ev));
    }

    // ── T14 — M4_RsvpUniqueIndexOneRowPerUser (§3.2 — unique (EventId,UserId))
    // After a resident RSVPs twice (Going then No), the (EventId, UserId)
    // unique index holds exactly one row for that resident — last write
    // wins; a second resident holds their own row (the per-resident key).

    [Fact]
    public async Task M4_RsvpUniqueIndexOneRowPerUser()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t14-author";
        const string r1 = "u-u09-t14-r1";
        const string r2 = "u-u09-t14-r2";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "BBQ", Body = "body t14",
            Start = new DateTimeOffset(2026, 4, 25, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 25, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        // r1 RSVPs twice (Going then No) — last write wins, one row.
        await svc.RsvpAsync(ev.Id, r1, RsvpStatus.Going);
        await svc.RsvpAsync(ev.Id, r1, RsvpStatus.No);
        // r2 RSVPs once (Going) — their own row.
        await svc.RsvpAsync(ev.Id, r2, RsvpStatus.Going);

        var all = await svc.GetRsvpsAsync(ev.Id);
        Assert.Equal(2, all.Count);                                            // exactly 2 rows.
        Assert.Single(all.Where(r => r.UserId == r1));                         // r1: one row.
        Assert.Equal(RsvpStatus.No, all.Single(r => r.UserId == r1).Status);  // last write won.
        Assert.Single(all.Where(r => r.UserId == r2));                         // r2: one row.
        Assert.Equal(RsvpStatus.Going, all.Single(r => r.UserId == r2).Status);
    }

    // ── T15 — M4_RsvpListOwnerOnly (§3.2 — RSVP list read is owner-only) ──
    // The owner (author) reads their own event's RSVPs successfully; a
    // non-author is denied the event detail (403) — the owner-only gate's
    // Core-layer expression (the Web boundary enforces the actor check;
    // the Core layer denies a non-author the event detail). The §3.7 pin:
    // "the RSVP list read is owner-only."

    [Fact]
    public async Task M4_RsvpListOwnerOnly()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t15-author";
        const string r1 = "u-u09-t15-r1";
        const string stranger = "u-u09-t15-stranger";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "BBQ", Body = "body t15",
            Start = new DateTimeOffset(2026, 4, 26, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 26, 11, 0, 0, TimeSpan.Zero),
            Audience = Audience(GrantKind.User, author),  // grantee = author only
            IsDraft = false,
        });

        // A resident RSVPs (the RSVP list will hold their row).
        await svc.RsvpAsync(ev.Id, r1, RsvpStatus.Going);

        // The author (owner) reads the RSVP list successfully.
        var rsvps = await svc.GetRsvpsAsync(ev.Id);
        Assert.Contains(rsvps, r => r.UserId == r1 && r.Status == RsvpStatus.Going);

        // A non-author (not in the audience) is denied the event detail (403)
        // — the owner-only gate's Core-layer expression.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetAsync(ev.Id, stranger));
    }

    // ── T17 — M4_AuditRowShape_Create (§3.4 — the row shape) ───────────────
    // The create-lane AccessAudit row is exactly: TargetKind "event",
    // Action "event.create", Via Owner, Outcome Allow (the §3.7 pin).

    [Fact]
    public async Task M4_AuditRowShape_Create()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t17-author";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "shape", Body = "body t17",
            Start = new DateTimeOffset(2026, 4, 27, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 27, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        var rows = await EventAuditRows(store);
        var row = Assert.Single(rows);              // exactly one row (the create).
        Assert.Equal("event", row.TargetKind);      // the exact string.
        Assert.Equal("event.create", row.Action);   // the lane's action.
        Assert.Equal(AccessVia.Owner, row.Via);     // the author's own standing.
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(ev.Id, row.TargetId);          // the row points at the created event.
    }

    // ── T18 — M4_EventToAuditableResourceShape (§3.3 — 6-member projection)
    // The EventToAuditableResource adapter (U02) projects the 6 members
    // (Id / Name / OwnerId / Audience / ComponentId / TargetKind). Name is
    // the title, or the body truncated to 60 chars (57 + "...") when the
    // title is empty. TargetKind is the exact string "event".

    [Fact]
    public void M4_EventToAuditableResourceShape()
    {
        // An event with a title: Name = the title.
        var withTitle = new Event
        {
            Id = "t18-ev",
            Title = "Cleanup day",
            Body = "Bring gloves and boots",
            AuthorId = "u-u09-t18-author",
            ComponentId = "c-u09-t18",
            Start = new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = null,   // public (null)
        };
        var a1 = new EventToAuditableResource(withTitle);
        Assert.Equal("t18-ev", a1.Id);
        Assert.Equal("Cleanup day", a1.Name);           // title → Name.
        Assert.Equal("u-u09-t18-author", a1.OwnerId);   // author → OwnerId.
        Assert.Null(a1.Audience);                        // null → null (public).
        Assert.Equal("c-u09-t18", a1.ComponentId);       // component → ComponentId.
        Assert.Equal("event", a1.TargetKind);            // the exact string.

        // An event without a title (Title = null — the adapter's `??` fallback
        // triggers on null, not empty): Name = body truncated to 60 chars
        // (57 + "...") — the §3.3 pin.
        var body = new string('x', 65);   // 65 chars → truncated to 57 + "..." = 60.
        var noTitle = new Event
        {
            Id = "t18-ev2",
            Title = null!,                // null → the adapter falls back to the body.
            Body = body,
            AuthorId = "u-u09-t18-author2",
            Start = new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = null,
        };
        var a2 = new EventToAuditableResource(noTitle);
        Assert.Equal(body[..57] + "...", a2.Name);      // 57 + "..." = 60 chars total.
        Assert.Equal(60, a2.Name.Length);

        // A non-null Audience is projected as-is (the same reference).
        var withAud = new Event
        {
            Id = "t18-ev3",
            Title = "t",
            Body = "b",
            AuthorId = "u-u09-t18-author3",
            Start = new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = new Audience { Community = true },
        };
        var a3 = new EventToAuditableResource(withAud);
        Assert.NotNull(a3.Audience);
        Assert.True(a3.Audience!.Community);            // projected verbatim.
        Assert.Same(withAud.Audience, a3.Audience);     // the same reference (not a copy).
    }

    // ── T01 — M4_MemberSeesUpcomingEventFeed (§3.3 — the feed) ─────────────
    // A signed-in member (a grantee in the audience) sees the event in the
    // upcoming feed; the feed is ordered by Start ascending (the §3.7 pin:
    // "the feed (CanSeeAsync(Read) survivors, Start ordering)").

    [Fact]
    public async Task M4_MemberSeesUpcomingEventFeed()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t01-author";
        const string member = "u-u09-t01-member";

        await Plant(store, new Event
        {
            Id = "t01-ev", AuthorId = author,
            Title = "Feed event", Body = "body t01",
            Start = new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 29, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = Audience(GrantKind.User, member),   // the member is the grantee.
        });

        // The member (grantee) sees the event in the feed.
        var feed = await svc.ListUpcomingAsync(null, member, 1);
        Assert.Contains("t01-ev", feed.Select(e => e.Id));

        // A stranger (not in the audience) does not see it.
        var strangerFeed = await svc.ListUpcomingAsync(null, "u-u09-t01-stranger", 1);
        Assert.DoesNotContain("t01-ev", strangerFeed.Select(e => e.Id));
    }

    // ── T03 — M4_CommunityAudienceSeesFeed (§3.3 — ADR 0036 Community) ─────
    // A <c>Audience.Community</c> event on a component is visible to a member
    // of that component (branch 4) but not to a non-member (branch 7 Deny).

    [Fact]
    public async Task M4_CommunityAudienceSeesFeed()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-u09-t03-author";
        const string member = "u-u09-t03-member";
        const string nonMember = "u-u09-t03-nonmember";
        const string comp = "c-u09-t03";

        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await userInfo.SetCommunityMembershipAsync(comp, member, actorId: "u-u09-t03-admin");

        await Plant(store, new Event
        {
            Id = "t03-ev", AuthorId = author, ComponentId = comp,
            Title = "Community event", Body = "body t03",
            Start = new DateTimeOffset(2026, 4, 30, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 4, 30, 11, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = new Audience { Community = true },   // the Community branch.
        });

        // The community member sees the event (branch 4).
        var memberFeed = await svc.ListUpcomingAsync(comp, member, 1);
        Assert.Contains("t03-ev", memberFeed.Select(e => e.Id));

        // A non-member does not (branch 7 Deny).
        var nonMemberFeed = await svc.ListUpcomingAsync(comp, nonMember, 1);
        Assert.DoesNotContain("t03-ev", nonMemberFeed.Select(e => e.Id));
    }

    // ── T04 — M4_GrantsAudienceOnlyGranteeSees (§3.3 — the grant-list branch)
    // A group-grant event is visible to a group member (branch 6) but not to
    // an un-granted resident (branch 7 Deny).

    [Fact]
    public async Task M4_GrantsAudienceOnlyGranteeSees()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-u09-t04-author";
        const string member = "u-u09-t04-member";
        const string outsider = "u-u09-t04-outsider";
        const string group = "g-u09-t04";

        await Plant(store, new Group
        {
            Id = group, Name = "Book club", OwnerId = author,
            Created = DateTimeOffset.UtcNow,
        });
        await userInfo.AddGroupMemberAsync(group, member, addedBy: author);

        await Plant(store, new Event
        {
            Id = "t04-ev", AuthorId = author,
            Title = "Book club", Body = "body t04",
            Start = new DateTimeOffset(2026, 5, 1, 18, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 5, 1, 19, 0, 0, TimeSpan.Zero),
            IsDraft = false,
            Audience = Audience(GrantKind.Group, group),   // the grant-list branch.
        });

        // The group member sees the event (branch 6 MatchGroups).
        var memberFeed = await svc.ListUpcomingAsync(null, member, 1);
        Assert.Contains("t04-ev", memberFeed.Select(e => e.Id));

        // An un-granted resident does not (branch 7 Deny).
        var outsiderFeed = await svc.ListUpcomingAsync(null, outsider, 1);
        Assert.DoesNotContain("t04-ev", outsiderFeed.Select(e => e.Id));
    }

    // ── T11 — M4_SoftDeleteExcludesFromFeedAndDetail (§3.5 — ADR 0024) ────
    // After the author soft-deletes (IsDeleted = true), the event is
    // filtered from the feed AND the detail is a 404 (the non-leaky pin).

    [Fact]
    public async Task M4_SoftDeleteExcludesFromFeedAndDetail()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t11-author";
        const string resident = "u-u09-t11-resident";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "live", Body = "body t11",
            Start = new DateTimeOffset(2026, 5, 2, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 5, 2, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        // Before delete: the event is in the feed and readable.
        Assert.Contains(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await svc.GetAsync(ev.Id, resident);

        // The author soft-deletes (ADR 0024 — IsDeleted = true).
        await svc.DeleteAsync(ev.Id, author);

        // After delete: the event is filtered from the feed and the detail
        // is a 404 (the non-leaky pin — same as a missing id).
        Assert.DoesNotContain(ev.Id, (await svc.ListUpcomingAsync(null, resident, 1)).Select(e => e.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetAsync(ev.Id, resident));

        // The delete lane's audit row is stored (C3, TargetKind "event").
        var rows = await EventAuditRows(store);
        Assert.Contains(rows, r => r.Action == "event.delete" && r.TargetKind == "event" && r.TargetId == ev.Id);
    }

    // ── T12 — M4_AuthorSoftDeleteOwnEvent (§3.5 — the author delete lane) ──
    // The author (the standing owner) may soft-delete their own event
    // (ADR 0024 — the ADR 0014/0016/0017 author branch). A non-author is
    // denied with a 403 (the C3 standing pin).

    [Fact]
    public async Task M4_AuthorSoftDeleteOwnEvent()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u09-t12-author";
        const string stranger = "u-u09-t12-stranger";

        var ev = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "owned", Body = "body t12",
            Start = new DateTimeOffset(2026, 5, 3, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 5, 3, 11, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });

        // The author (the standing owner) soft-deletes their own event.
        await svc.DeleteAsync(ev.Id, author);

        // The event is now filtered from the feed (the read lanes filter it).
        Assert.DoesNotContain(ev.Id, (await svc.ListUpcomingAsync(null, author, 1)).Select(e => e.Id));

        // The detail is a 404 (the non-leaky pin).
        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.GetAsync(ev.Id, author));

        // A non-author (stranger) is denied the delete (403 — the C3 pin).
        var ev2 = await svc.CreateAsync(author, new CreateEventRequest
        {
            Title = "owned2", Body = "body t12b",
            Start = new DateTimeOffset(2026, 5, 3, 12, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 5, 3, 13, 0, 0, TimeSpan.Zero),
            Audience = null,
            IsDraft = false,
        });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteAsync(ev2.Id, stranger));
        // The second event is still live (the denied write did not apply).
        Assert.Contains(ev2.Id, (await svc.ListUpcomingAsync(null, author, 1)).Select(e => e.Id));
    }

    // ════════════════════════════════════════════════════════════════════════
    // Test plumbing (mirrors PostServiceTests).
    // ════════════════════════════════════════════════════════════════════════

    private static void await_ThrowsUnauthorized(Action action)
        => Assert.Throws<UnauthorizedAccessException>(action);

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
    /// same three-constructor shape U01's <c>AddTransient</c> registration
    /// uses, mirrored here directly against the scratch store — the
    /// <c>PostServiceTests</c> precedent).</summary>
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
    /// service write seam — the write lanes are U04's scope).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
