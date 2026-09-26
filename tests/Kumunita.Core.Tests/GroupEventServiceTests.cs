using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0089 (the <b>GE</b> group-events lane) — the 19 <see cref="EventService"/>
/// group-lane seam tests (design doc <c>group-events-design.md</c>, "Pinned seam
/// tests"). Each test name is pinned verbatim by that list (renaming them is a
/// drift event) and carries its invariant anchors (GE·1–GE·8, the C3/C4 analogs).
/// <para>
/// Mirrors <see cref="GroupPostServiceTests"/> (the ADR 0013 membership lane):
/// same <see cref="PostgresFixture"/> (fresh scratch Postgres per test), same
/// <c>BootStoreAsync</c> shape, the same frozen <c>IAuthorizationService</c>
/// group seams (<c>CanSeeGroupAsync</c> pair + <c>CanSeeGroupFeedAsync</c>) —
/// the difference is the <see cref="Event"/> document and the group-event
/// surface (<c>ListGroupEventsAsync</c> / <c>GetGroupEventAsync</c> /
/// <c>CreateGroupEventAsync</c> / <c>UpdateGroupEventAsync</c>) added by U04.
/// The community lanes (<c>ListUpcomingAsync</c> / <c>ListInRangeAsync</c>) are
/// reused to pin GE·2 lane exclusivity (a group event never reaches them).
/// </para>
/// </summary>
public class GroupEventServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — GE1_MemberSeesGroupEventsFeed (GE·1, GE·5 aggregate Allow) ─────
    //
    // A member reads the group channel: the published event is in `Visible`,
    // and the visit's **single aggregate** row is present — TargetKind
    // "grouppost", TargetId null, VisibleCount 1, HiddenCount 0, Via Group,
    // Allow (the channel is all-or-nothing).

    [Fact]
    public async Task GE1_MemberSeesGroupEventsFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g1-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GE1 family", null);
        await Plant(store, GroupEvent("ge1-ev", group.Id, owner));

        var feed = await svc.ListGroupEventsAsync(group.Id, owner, page: 1);
        var ev = Assert.Single(feed.Visible);
        Assert.Equal("ge1-ev", ev.Id);
        Assert.Equal(0, feed.HiddenCount);
        Assert.Equal(1, feed.Total);

        var aggregate = Assert.Single(await GroupAudits(store, actor: owner), a => a.TargetId is null);
        Assert.Equal("grouppost", aggregate.TargetKind);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        Assert.Equal(AccessVia.Group, aggregate.Via);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
    }

    // ── 2 — GE2_NonMemberFeedEmptyWithDenyRow (GE·1, GE·5) ─────────────────
    //
    // A non-member reads the channel: `Visible` empty, `HiddenCount = 1`, and
    // the visit's aggregate **Deny** row (Via Group, counts set, TargetId null)
    // — the denial is the audit evidence.

    [Fact]
    public async Task GE2_NonMemberFeedEmptyWithDenyRow()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g2-owner";
        const string stranger = "u-ge-g2-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "GE2 family", null);
        await Plant(store, GroupEvent("ge2-ev", group.Id, owner));

        var feed = await svc.ListGroupEventsAsync(group.Id, stranger, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);
        Assert.Equal(0, feed.Total);

        var aggregate = Assert.Single(await GroupAudits(store, actor: stranger), a => a.TargetId is null);
        Assert.Equal(0, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessVia.Group, aggregate.Via);
        Assert.Equal(AccessOutcome.Deny, aggregate.Outcome);
    }

    // ── 3 — GE3_MembershipAddReScopesNextFeed (C4 strong consistency) ──────
    //
    // The event exists **before** the membership add; the very **next** feed
    // read by the new member is Allow — the live membership read is the truth.

    [Fact]
    public async Task GE3_MembershipAddReScopesNextFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g3-owner";
        const string newcomer = "u-ge-g3-newcomer";

        var group = await userInfo.CreateGroupAsync(owner, "GE3 family", null);
        await Plant(store, GroupEvent("ge3-ev", group.Id, owner));

        var before = await svc.ListGroupEventsAsync(group.Id, newcomer, page: 1);
        Assert.Empty(before.Visible);

        await userInfo.AddGroupMemberAsync(group.Id, newcomer, addedBy: owner);
        var after = await svc.ListGroupEventsAsync(group.Id, newcomer, page: 1);
        var ev = Assert.Single(after.Visible);
        Assert.Equal("ge3-ev", ev.Id);
        Assert.Equal(0, after.HiddenCount);
    }

    // ── 4 — GE4_MembershipRemoveRevokesNextDetail (C4, no owner-skip) ──────
    //
    // The **author** (also the owner) is removed from the group; the very
    // **next** detail load is null — no owner-skip on the group lane. The
    // Deny row names the event id.

    [Fact]
    public async Task GE4_MembershipRemoveRevokesNextDetail()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-ge-g4-author";

        var group = await userInfo.CreateGroupAsync(author, "GE4 family", null);
        await Plant(store, GroupEvent("ge4-ev", group.Id, author));

        var whileMember = await svc.GetGroupEventAsync(group.Id, "ge4-ev", author);
        Assert.NotNull(whileMember);

        await userInfo.RemoveGroupMemberAsync(group.Id, author, removedBy: "u-ge-g4-admin");
        var afterRemove = await svc.GetGroupEventAsync(group.Id, "ge4-ev", author);
        Assert.Null(afterRemove);

        var denyRow = Assert.Single(await GroupAudits(store, actor: author),
            a => a.Outcome == AccessOutcome.Deny && a.TargetId == "ge4-ev");
        Assert.Equal(AccessVia.Group, denyRow.Via);
    }

    // ── 5 — GE5_MemberCreatesGroupEventSeesIt (GE·3, GE·5) ─────────────────
    //
    // A member creates a group event through the service's group surface; it is
    // stored (a draft — invisible until publish) and the create gate's Allow
    // row (the gate **is** the group-lane decision) is present.

    [Fact]
    public async Task GE5_MemberCreatesGroupEventSeesIt()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g5-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GE5 family", null);

        var created = await RunInSession(store, s => svc.CreateGroupEventAsync(
            new GroupEventDraft(group.Id, "Meeting", "body ge5",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), owner, s));

        Assert.NotNull(created);
        Assert.Equal(group.Id, created.GroupId);
        Assert.True(created.IsDraft); // ADR 0037 — a group event is a draft at create.

        var authorDetail = await svc.GetGroupEventAsync(group.Id, created.Id, owner);
        Assert.NotNull(authorDetail); // the author's draft gate (ADR 0037) returns it.

        var gateRow = Assert.Single(await GroupAudits(store, actor: owner),
            a => a.Outcome == AccessOutcome.Allow && a.TargetId == group.Id);
        Assert.Equal(AccessVia.Group, gateRow.Via);
    }

    // ── 6 — GE5_GroupEventComponentIdAndAudiencePinned (GE·2, GE·8) ────────
    //
    // The service **writes** the stored shape pinned: non-empty `GroupId`,
    // `ComponentId = string.Empty` (lane exclusivity), `Audience` non-null and
    // **empty** (audience never applies to a group event), `IsDraft = true`.

    [Fact]
    public async Task GE5_GroupEventComponentIdAndAudiencePinned()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g5b-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GE5b family", null);

        var ev = await RunInSession(store, s => svc.CreateGroupEventAsync(
            new GroupEventDraft(group.Id, "Pinned shape", "body ge5b",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), owner, s));

        Assert.Equal(group.Id, ev.GroupId);
        Assert.Equal(string.Empty, ev.ComponentId); // GE·2 — lane exclusivity.
        Assert.NotNull(ev.Audience);                  // GE·8 — non-null
        Assert.Empty(ev.Audience.Grants);             //        …and empty
        Assert.True(ev.Audience.IsEmpty);
        Assert.True(ev.IsDraft);                       // ADR 0037 pin.
    }

    // ── 7 — GE6_NonMemberCreateDenied_DenyRowSurvives (GE·3) ───────────────
    //
    // A non-member create is denied with <see cref="UnauthorizedAccessException"/>
    // (the Web 404), and the gate's **Deny** row survives (committed by the
    // SaveChangesAsync before the throw).

    [Fact]
    public async Task GE6_NonMemberCreateDenied_DenyRowSurvives()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g6-owner";
        const string stranger = "u-ge-g6-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "GE6 family", null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.CreateGroupEventAsync(
                new GroupEventDraft(group.Id, "no", "body ge6",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), stranger, s)));

        // The Deny row survived the throw (the gate's audit evidence — GE·5).
        var gateRow = Assert.Single(await GroupAudits(store, actor: stranger),
            a => a.Outcome == AccessOutcome.Deny && a.TargetId == group.Id);
        Assert.Equal(AccessVia.Group, gateRow.Via);
    }

    // ── 8 — GE7_ModeratorNonMemberDenied (GE·4 — no moderator branch) ──────
    //
    // A moderator (assigned to a **different** component) who is **not** a
    // member of the group is denied — the group lane is membership-only; no
    // `Via = Moderator` row.

    [Fact]
    public async Task GE7_ModeratorNonMemberDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g7-owner";
        const string moderator = "u-ge-g7-moderator";

        var group = await userInfo.CreateGroupAsync(owner, "GE7 family", null);
        await Plant(store, GroupEvent("ge7-ev", group.Id, owner));
        await Plant(store, new Component { Id = "c-ge7", Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "ge7-assign", UserId = moderator, ComponentId = "c-ge7",
            GrantedBy = "u-ge-g7-admin", At = DateTimeOffset.UtcNow
        });

        var feed = await svc.ListGroupEventsAsync(group.Id, moderator, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        Assert.Null(await svc.GetGroupEventAsync(group.Id, "ge7-ev", moderator));

        var rows = await GroupAudits(store, actor: moderator);
        Assert.All(rows, r =>
        {
            Assert.Equal(AccessOutcome.Deny, r.Outcome);
            Assert.Equal(AccessVia.Group, r.Via); // never Moderator (GE·4).
        });
    }

    // ── 9 — GE8_BreakGlassDoesNotApplyToGroupEvents (GE·4) ─────────────────
    //
    // A consumed, unexpired <c>AdminOverride</c> (break-glass elevation active
    // on the audience lane) for a **non-member** GlobalAdmin. The group lane
    // never reads break-glass (GE·4 — the strictest privacy lane), so the
    // elevation is inert here: Deny, and no `Via = BreakGlass` row.

    [Fact]
    public async Task GE8_BreakGlassDoesNotApplyToGroupEvents()
    {
        var (store, conn) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g8-owner";
        const string admin = "u-ge-g8-globaladmin";
        var now = DateTimeOffset.UtcNow;

        var group = await userInfo.CreateGroupAsync(owner, "GE8 family", null);
        await Plant(store, GroupEvent("ge8-ev", group.Id, owner));

        await SeedAdminOverrideAsync(conn, admin, "tok-ge-g8",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        var feed = await svc.ListGroupEventsAsync(group.Id, admin, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        Assert.Null(await svc.GetGroupEventAsync(group.Id, "ge8-ev", admin));

        var rows = await GroupAudits(store, actor: admin);
        Assert.All(rows, r =>
        {
            Assert.Equal(AccessOutcome.Deny, r.Outcome);
            Assert.Equal(AccessVia.Group, r.Via); // never BreakGlass (GE·4).
        });
    }

    // ── 10 — GE9_DelegateWithReadInScopeSeesOwnerGroupEvents (GE·6, C2) ────
    //
    // An **in-scope `read`** grant acts with the **owner's** standing — the
    // owner is a member ⇒ the delegate's feed is Allow, and the aggregate row
    // records `Via = Delegation`, `EffectivePrincipalId = owner`.

    [Fact]
    public async Task GE9_DelegateWithReadInScopeSeesOwnerGroupEvents()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g9-owner";
        const string delegate_ = "u-ge-g9-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "GE9 family", null);
        await Plant(store, GroupEvent("ge9-ev", group.Id, owner));
        await Plant(store, ReadGrant(owner, delegate_, "ge9-grant"));

        var feed = await svc.ListGroupEventsAsync(group.Id, delegate_, page: 1);
        var ev = Assert.Single(feed.Visible);
        Assert.Equal("ge9-ev", ev.Id);

        var aggregate = Assert.Single(await GroupAudits(store, actor: delegate_), a => a.TargetId is null);
        Assert.Equal(AccessVia.Delegation, aggregate.Via);
        Assert.Equal(owner, aggregate.EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
    }

    // ── 11 — GE10_DelegateWithoutReadDenied (GE·6, C2) ─────────────────────
    //
    // An **out-of-scope** grant (`write` only) acts as the delegate themself;
    // the delegate is **not** a member ⇒ Deny, and the row is still
    // `Via = Delegation` (the acting-identity rule; the scope guard is real).

    [Fact]
    public async Task GE10_DelegateWithoutReadDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g10-owner";
        const string delegate_ = "u-ge-g10-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "GE10 family", null);
        await Plant(store, GroupEvent("ge10-ev", group.Id, owner));
        await Plant(store, new DelegationGrant
        {
            Id = "ge10-grant",
            OwnerId = owner,
            DelegateId = delegate_,
            Scope = ["write"], // no "read" — out of scope.
            From = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var feed = await svc.ListGroupEventsAsync(group.Id, delegate_, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var aggregate = Assert.Single(await GroupAudits(store, actor: delegate_), a => a.TargetId is null);
        Assert.Equal(AccessOutcome.Deny, aggregate.Outcome);
        Assert.Equal(AccessVia.Delegation, aggregate.Via);
        Assert.Equal(delegate_, aggregate.EffectivePrincipalId); // themself.
    }

    // ── 12 — GE11_NonAuthorMemberCannotEditGroupEvent (GE·4) ───────────────
    //
    // A **member** (not the author) cannot edit a group event: the edit lane
    // is **author-only** (the ADR 0016 / 0037 precedent) — a non-author, even a
    // GlobalAdmin, is denied with <see cref="UnauthorizedAccessException"/>
    // (the Web 404). The author's edit is allowed and stamps the editable
    // surface without touching the lane markers.

    [Fact]
    public async Task GE11_NonAuthorMemberCannotEditGroupEvent()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-ge-g11-author";
        const string member = "u-ge-g11-member";

        var group = await userInfo.CreateGroupAsync(author, "GE11 family", null);
        await Plant(store, GroupEvent("ge11-ev", group.Id, author));
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: author);

        // The non-author member is denied.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.UpdateGroupEventAsync("ge11-ev", member,
                new GroupEventUpdate("t", "b", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), s)));

        // The author edits: the editable surface updates, the lane markers
        // (GroupId / ComponentId / AuthorId / IsDraft) are untouched.
        var updated = await RunInSession(store, s => svc.UpdateGroupEventAsync("ge11-ev", author,
            new GroupEventUpdate("New title", "New body",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2)), s));
        Assert.Equal("New title", updated.Title);
        Assert.Equal(group.Id, updated.GroupId);
        Assert.Equal(string.Empty, updated.ComponentId);
        Assert.Equal(author, updated.AuthorId);
        Assert.False(updated.IsDraft); // the lane marker is untouched (it started published).
    }

    // ── 13 — GE12_GroupEventExcludedFromCommunityListUpcoming (GE·2) ───────
    //
    // A group event (non-empty GroupId) is **never** in the community
    // <c>ListUpcomingAsync</c> feed — even for its author and even though the
    // author "owns" it. Lane exclusivity is enforced by the candidate filter
    // (`GroupId == string.Empty`), not by a decision.

    [Fact]
    public async Task GE12_GroupEventExcludedFromCommunityListUpcoming()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g12-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GE12 family", null);
        await Plant(store, GroupEvent("ge12-group", group.Id, owner));

        // The community feed never contains the group event (the author is the
        // strongest possible claimant, so if even they can't see it the lane
        // is exclusive by construction).
        var upcoming = await svc.ListUpcomingAsync(null, owner, page: 1);
        Assert.DoesNotContain(upcoming, e => e.Id == "ge12-group");
    }

    // ── 14 — GE13_GroupEventExcludedFromCommunityListInRange (GE·2) ────────
    //
    // Same pin for the <c>ListInRangeAsync</c> (calendar-window) feed — the
    // group event is within the window but is excluded by the lane filter.

    [Fact]
    public async Task GE13_GroupEventExcludedFromCommunityListInRange()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-g13-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GE13 family", null);
        var start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        await Plant(store, new Event
        {
            Id = "ge13-group", AuthorId = owner, GroupId = group.Id,
            ComponentId = string.Empty, Audience = new Audience(),
            Title = "Group event", Body = "body ge13",
            Start = start, End = start.AddHours(1), IsDraft = false,
            Created = DateTimeOffset.UtcNow
        });

        var window = await svc.ListInRangeAsync(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
            null, owner);
        Assert.DoesNotContain(window, e => e.Id == "ge13-group");
    }

    // ── 15 — Feed_AggregateAuditRowShape_GroupEvent (GE·5 aggregate shape) ─
    //
    // The feed's single aggregate row for a member visit — the exact field
    // shape (action / TargetKind / TargetId / counts / Via / Outcome) the
    // GE·5 "audit per lane" invariant pins.

    [Fact]
    public async Task Feed_AggregateAuditRowShape_GroupEvent()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-feedshape-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GEfeed family", null);
        await Plant(store, GroupEvent("feedshape-ev", group.Id, owner));

        await svc.ListGroupEventsAsync(group.Id, owner, page: 1);

        var row = Assert.Single(await GroupAudits(store, actor: owner), a => a.TargetId is null);
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal(1, row.VisibleCount);
        Assert.Equal(0, row.HiddenCount);
        Assert.Equal(AccessVia.Group, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 16 — Detail_DecisionAuditRowShape_ViaGroup (GE·5 detail shape) ─────
    //
    // A member's detail visit writes **one** decision row naming the event id
    // (TargetId = the event id), `Via = Group`, Allow.

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaGroup()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-dshape-owner";

        var group = await userInfo.CreateGroupAsync(owner, "GEdetail family", null);
        await Plant(store, GroupEvent("dshape-ev", group.Id, owner));

        var ev = await svc.GetGroupEventAsync(group.Id, "dshape-ev", owner);
        Assert.NotNull(ev);

        var row = Assert.Single(await GroupAudits(store, actor: owner), a => a.TargetId == "dshape-ev");
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal(AccessVia.Group, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 17 — Detail_DecisionAuditRowShape_ViaDelegation (GE·6) ─────────────
    //
    // A delegate's detail visit (in-scope `read`) writes the same **one**
    // decision row, but `Via = Delegation` and `EffectivePrincipalId = owner`.

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaDelegation()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-ge-ddel-owner";
        const string delegate_ = "u-ge-ddel-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "GEddel family", null);
        await Plant(store, GroupEvent("ddel-ev", group.Id, owner));
        await Plant(store, ReadGrant(owner, delegate_, "ddel-grant"));

        var ev = await svc.GetGroupEventAsync(group.Id, "ddel-ev", delegate_);
        Assert.NotNull(ev);

        var row = Assert.Single(await GroupAudits(store, actor: delegate_), a => a.TargetId == "ddel-ev");
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal(AccessVia.Delegation, row.Via);
        Assert.Equal(owner, row.EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 18 — Draft_NonAuthorSeesNothing_GetGroupEvent (ADR 0037) ───────────
    //
    // The **ADR 0037 draft gate** runs **before** the membership decision: a
    // draft is invisible to *every* member (including other members of the
    // group) and to any moderator/admin except its author — a pure
    // `AuthorId == actorId` ordinal check, **no** <c>CanSeeGroupAsync</c>,
    // **no** audit row.

    [Fact]
    public async Task Draft_NonAuthorSeesNothing_GetGroupEvent()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-ge-draft-author";
        const string member = "u-ge-draft-member";

        var group = await userInfo.CreateGroupAsync(author, "GEdraft family", null);
        // A group event authored as a **draft** (IsDraft = true).
        await Plant(store, new Event
        {
            Id = "draft-ev", AuthorId = author, GroupId = group.Id,
            ComponentId = string.Empty, Audience = new Audience(),
            Title = "Draft", Body = "body draft",
            Start = DateTimeOffset.UtcNow, End = DateTimeOffset.UtcNow.AddHours(1),
            IsDraft = true, Created = DateTimeOffset.UtcNow
        });
        await userInfo.AddGroupMemberAsync(group.Id, member, addedBy: author);

        // A member (not the author) sees nothing.
        Assert.Null(await svc.GetGroupEventAsync(group.Id, "draft-ev", member));

        // The author (via the draft gate, before membership) sees it.
        Assert.NotNull(await svc.GetGroupEventAsync(group.Id, "draft-ev", author));

        // The draft gate writes **no** decision row for the member (the ADR
        // 0037 ordinal check is decision-free); the only row is the author's
        // own, if any — but there is none for the member.
        Assert.DoesNotContain(await GroupAudits(store, actor: member), a => a.TargetId == "draft-ev");
    }

    // ── 19 — EventService_MakesNoModerateOrBreakGlassCallOnGroupEvents (GE·4)
    //
    // At the **call** level: the group-event surface never reaches the
    // audience lane (<c>CanSeeAsync</c>) or a <c>Moderate</c> action. Only
    // group-lane calls (<c>CanSeeGroupAsync</c> / <c>CanSeeGroupFeedAsync</c>)
    // are observed (GE·4 at the seam level, complementing GE7/GE8's
    // decision-level proof).

    [Fact]
    public async Task EventService_MakesNoModerateOrBreakGlassCallOnGroupEvents()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, _) = Services(store);
        const string owner = "u-ge-spy-owner";
        const string stranger = "u-ge-spy-stranger";

        var recorder = new RecordingAuthz(authz);
        var spy = new EventService(store, recorder, userInfo);

        var group = await userInfo.CreateGroupAsync(owner, "Spy family", null);
        await Plant(store, GroupEvent("spy-ev", group.Id, owner));

        // The whole group surface: read (feed + detail), write allow, write
        // deny (caught — the gate's row must still persist).
        await spy.ListGroupEventsAsync(group.Id, owner, page: 1);
        await spy.GetGroupEventAsync(group.Id, "spy-ev", owner);
        await RunInSession(store, s => spy.CreateGroupEventAsync(
            new GroupEventDraft(group.Id, "t", "body spy",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), owner, s));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => spy.CreateGroupEventAsync(
                new GroupEventDraft(group.Id, "t", "body spy2",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)), stranger, s)));

        Assert.True(recorder.GroupLaneCalls >= 4, $"expected ≥ 4 group-lane calls, got {recorder.GroupLaneCalls}");
        Assert.Equal(0, recorder.AudienceLaneCalls);
        Assert.Equal(0, recorder.ModerateCalls);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private async Task<(IDocumentStore store, string conn)> BootStoreAsync()
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
        return (store, conn);
    }

    /// <summary>The service trio over the scratch store
    /// (<see cref="GroupPostServiceTests.Services"/> precedent).</summary>
    private static (UserInfoService User, AuthorizationService Authz, EventService Events)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var events = new EventService(store, authz, userInfo);
        return (userInfo, authz, events);
    }

    /// <summary>A planted **published** group event: non-empty <c>GroupId</c>,
    /// empty <c>ComponentId</c>, audience non-null empty, <c>IsDraft = false</c>
    /// (the GE·2/GE·8 shape the service's own writer pins).</summary>
    private static Event GroupEvent(string id, string groupId, string author) => new()
    {
        Id = id,
        GroupId = groupId,
        ComponentId = string.Empty,
        AuthorId = author,
        Title = id,
        Body = "body " + id,
        Audience = new Audience(),
        Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 3, 1, 13, 0, 0, TimeSpan.Zero),
        IsDraft = false,
        Created = DateTimeOffset.UtcNow
    };

    /// <summary>An in-scope <c>read</c> delegation grant (active window,
    /// not revoked) for the named delegate over the named owner.</summary>
    private static DelegationGrant ReadGrant(string owner, string delegate_, string id) => new()
    {
        Id = id,
        OwnerId = owner,
        DelegateId = delegate_,
        Scope = [AccessAction.Read.Id],
        From = DateTimeOffset.UtcNow.AddDays(-1)
    };

    /// <summary>Plant a document row directly (fixture seeding, not a service
    /// write seam).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task<T> RunInSession<T>(IDocumentStore store, Func<IDocumentSession, Task<T>> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        return await action(session);
    }

    /// <summary>The group lane's audit rows only (TargetKind
    /// `"grouppost"` — the frozen seam's existing discriminator; never the
    /// M2 `"group"` management rows or the `"event"` write-lane rows).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> GroupAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "grouppost");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    /// <summary>Seed a consumed/unexpired <c>AdminOverride</c> row (the
    /// <see cref="GroupPostServiceTests.SeedAdminOverrideAsync"/> shape,
    /// hand-rolled over the <c>mt</c> table via a raw Npgsql insert).</summary>
    private static async Task SeedAdminOverrideAsync(
        string connString, string userId, string token,
        DateTimeOffset grantedAt, DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt)
    {
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"mt\".\"AdminOverride\" " +
            "  (\"id\", \"userId\", \"token\", \"grantedAt\", \"expiresAt\", \"consumedAt\") " +
            "VALUES (@id, @userId, @token, @grantedAt, @expiresAt, @consumedAt)";

        static void Add(NpgsqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        Add(cmd, "@id", Guid.NewGuid().ToString("N"));
        Add(cmd, "@userId", userId);
        Add(cmd, "@token", token);
        Add(cmd, "@grantedAt", grantedAt);
        Add(cmd, "@expiresAt", expiresAt);
        if (consumedAt is null)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@consumedAt";
            p.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.TimestampTz;
            p.Value = DBNull.Value;
            cmd.Parameters.Add(p);
        }
        else
        {
            Add(cmd, "@consumedAt", consumedAt);
        }

        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Test double **over the real** <see cref="AuthorizationService"/>
    /// (decision behavior unchanged — only the *call surface* is recorded):
    /// counts group-lane calls, audience-lane calls, and
    /// <c>AccessAction.Moderate</c> invocations. Test #19's spy (the
    /// <see cref="GroupPostServiceTests.RecordingAuthz"/> shape).</summary>
    private sealed class RecordingAuthz(IAuthorizationService inner) : IAuthorizationService
    {
        public int GroupLaneCalls;
        public int AudienceLaneCalls;
        public int ModerateCalls;

        private void NoteModerate(AccessAction action)
        {
            if (action == AccessAction.Moderate) ModerateCalls++;
        }

        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target)
        {
            AudienceLaneCalls++; NoteModerate(action);
            return inner.CanAsync(actorId, action, target);
        }
        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session)
        {
            AudienceLaneCalls++; NoteModerate(action);
            return inner.CanAsync(actorId, action, target, session);
        }
        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates)
        {
            AudienceLaneCalls++; NoteModerate(action);
            return inner.CanSeeAsync(actorId, action, candidates);
        }
        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session)
        {
            AudienceLaneCalls++; NoteModerate(action);
            return inner.CanSeeAsync(actorId, action, candidates, session);
        }
        public Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId)
        {
            GroupLaneCalls++;
            return inner.CanSeeGroupAsync(actorId, groupId, targetPostId);
        }
        public Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId, IDocumentSession session)
        {
            GroupLaneCalls++;
            return inner.CanSeeGroupAsync(actorId, groupId, targetPostId, session);
        }
        public Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount)
        {
            GroupLaneCalls++;
            return inner.CanSeeGroupFeedAsync(actorId, groupId, candidateCount);
        }
        public Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount, IDocumentSession session)
        {
            GroupLaneCalls++;
            return inner.CanSeeGroupFeedAsync(actorId, groupId, candidateCount, session);
        }
    }
}
