using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Group posts milestone, unit U9 — the 19 <see cref="PostService"/> group-lane
/// seam tests (design doc <c>group-posts-design.md</c> Part 2 §2.5). Each test
/// name is pinned verbatim by §2.5 (the §2.7 drift-guard makes renaming them a
/// drift event) and carries its FACES row (G1–G13, Part 1) and invariant
/// anchors (G·1–G·8, C2/C4, C-M3·1/3 analogs).
/// <para>
/// Mirrors the M3 <c>PostServiceTests</c> scaffolding: same
/// <see cref="PostgresFixture"/> (fresh scratch Postgres per test method),
/// same <c>BootStoreAsync</c> shape, same service trio
/// (<c>UserInfoService</c> + <c>AuthorizationService</c> + <c>PostService</c>)
/// — the group lane is U5's <c>AuthorizationService</c> ADDs
/// (<c>CanSeeGroupAsync</c> pair + <c>CanSeeGroupFeedAsync</c> pair,
/// <c>AccessVia.Group</c>) consumed by U6's <c>PostService</c> group surface
/// (<c>ListGroupFeedAsync</c> / <c>GetGroupPostAsync</c> /
/// <c>CreateGroupPostAsync</c>). No new Core surface.
/// </para>
/// <para>
/// This unit **does not** run the §2.4 acceptance gate (U10); the pass/red
/// status of these 19 is the data U10 consumes.
/// </para>
/// </summary>
public class GroupPostServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — G1_MemberSeesGroupFeed (G1 FACES; G·1, G·5 aggregate Allow) ────
    //
    // A member reads the group channel: the post is in `Visible`, and the
    // visit's **single aggregate** row is present — `TargetKind = "grouppost"`,
    // `TargetId = null`, `VisibleCount = 1`, `HiddenCount = 0`,
    // `Action = "read"`, `Via = Group`, `Allow` (the C-M3·3 analog on the
    // membership lane; the channel is all-or-nothing).

    [Fact]
    public async Task G1_MemberSeesGroupFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g1-owner";

        var group = await userInfo.CreateGroupAsync(owner, "G1 family", null);
        await Plant(store, GroupPost("g1-post", group.Id, owner));

        var feed = await svc.ListGroupFeedAsync(group.Id, owner, page: 1);
        var post = Assert.Single(feed.Visible);
        Assert.Equal("g1-post", post.Id);
        Assert.Equal(0, feed.HiddenCount);
        Assert.Equal(1, feed.Total);

        var aggregate = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("grouppost", aggregate.TargetKind);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        Assert.Equal(AccessVia.Group, aggregate.Via);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
    }

    // ── 2 — G2_NonMemberFeedEmptyWithDenyRow (G2 FACES; G·1, G·5) ──────────
    //
    // A non-member reads the channel: `Visible` empty, `HiddenCount = 1`, and
    // the visit's aggregate **Deny** row (`Via = Group`, counts set, TargetId
    // null) — the denial is the audit evidence (G·5: Allow *and* Deny).

    [Fact]
    public async Task G2_NonMemberFeedEmptyWithDenyRow()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g2-owner";
        const string stranger = "u-gp-g2-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "G2 family", null);
        await Plant(store, GroupPost("g2-post", group.Id, owner));

        var feed = await svc.ListGroupFeedAsync(group.Id, stranger, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);
        Assert.Equal(0, feed.Total);

        var aggregate = Assert.Single(await GroupPostAudits(store, actor: stranger),
            a => a.TargetId is null);
        Assert.Equal(0, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessVia.Group, aggregate.Via);
        Assert.Equal(AccessOutcome.Deny, aggregate.Outcome);
    }

    // ── 3 — G3_MembershipAddReScopesNextFeed (G3 FACES; C4, G·1) ───────────
    //
    // A post exists **before** the membership add; the very **next** feed read
    // by the new member is Allow — the live `GetGroupIdsAsync` read is the
    // truth (C4 strong consistency, no projection lag).

    [Fact]
    public async Task G3_MembershipAddReScopesNextFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g3-owner";
        const string newcomer = "u-gp-g3-newcomer";

        var group = await userInfo.CreateGroupAsync(owner, "G3 family", null);
        await Plant(store, GroupPost("g3-post", group.Id, owner));

        // The post precedes the add; the member is not in it yet.
        var before = await svc.ListGroupFeedAsync(group.Id, newcomer, page: 1);
        Assert.Empty(before.Visible);

        await userInfo.AddGroupMemberAsync(group.Id, newcomer, addedBy: owner);
        var after = await svc.ListGroupFeedAsync(group.Id, newcomer, page: 1);
        var post = Assert.Single(after.Visible);
        Assert.Equal("g3-post", post.Id);
        Assert.Equal(0, after.HiddenCount);
    }

    // ── 4 — G4_MembershipRemoveRevokesNextDetail (G4 FACES; C4, G·1) ───────
    //
    // The **author** (also the owner) is removed from the group; the very
    // **next** detail load is Deny — no owner-skip, no author branch on the
    // group lane (the §2.5 anchor: "an author who is removed is denied — no
    // owner-skip"). The Deny row names the post id.

    [Fact]
    public async Task G4_MembershipRemoveRevokesNextDetail()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string author = "u-gp-g4-author";

        var group = await userInfo.CreateGroupAsync(author, "G4 family", null);
        await Plant(store, GroupPost("g4-post", group.Id, author));

        // While a member, the author sees it (the author is allowed *because*
        // a member — G·1).
        var whileMember = await svc.GetGroupPostAsync(group.Id, "g4-post", author);
        Assert.NotNull(whileMember.Post);

        // Removed — live on the next read (C4); the author branch does not
        // exist on this lane (G·4's nobody-peeks, C5).
        await userInfo.RemoveGroupMemberAsync(group.Id, author, removedBy: "u-gp-g4-admin");
        var afterRemove = await svc.GetGroupPostAsync(group.Id, "g4-post", author);
        Assert.Null(afterRemove.Post);
        Assert.Empty(afterRemove.Replies);

        var denyRow = Assert.Single(await GroupPostAudits(store, actor: author),
            a => a.Outcome == AccessOutcome.Deny);
        Assert.Equal("g4-post", denyRow.TargetId);
        Assert.Equal(AccessVia.Group, denyRow.Via);
    }

    // ── 5 — G5_MemberCreatesGroupPostSeesIt (G5 FACES; G·3, G·5) ───────────
    //
    // A member creates a group post through the service's group surface; it is
    // stored and lands in the group feed. The create gate's Allow row (the
    // gate **is** the group-lane decision, G·3) is present.

    [Fact]
    public async Task G5_MemberCreatesGroupPostSeesIt()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g5-owner";

        var group = await userInfo.CreateGroupAsync(owner, "G5 family", null);

        var post = await RunInSession(store, s => svc.CreateGroupPostAsync(
            new GroupPostDraft(group.Id, "hello", "body g5"), owner, s));

        Assert.NotNull(post);
        Assert.Equal(group.Id, post.GroupId);

        var feed = await svc.ListGroupFeedAsync(group.Id, owner, page: 1);
        Assert.Contains(feed.Visible, p => p.Id == post.Id);

        var gateRow = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.Outcome == AccessOutcome.Allow && a.TargetId == group.Id);
        Assert.Equal(AccessVia.Group, gateRow.Via);
    }

    // ── 6 — G5_GroupPostAudienceWrittenEmpty (G5 FACES; G·8) ───────────────
    //
    // The service **writes** the stored `Post.Audience` non-null and **empty**
    // (G·8 — audience grants never apply to group posts; the audience lane is
    // never evaluated) with `ComponentId` empty (G·2 lane exclusivity) and the
    // non-empty `GroupId` marking the lane.

    [Fact]
    public async Task G5_GroupPostAudienceWrittenEmpty()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g5b-owner";

        var group = await userInfo.CreateGroupAsync(owner, "G5b family", null);

        var post = await RunInSession(store, s => svc.CreateGroupPostAsync(
            new GroupPostDraft(group.Id, null, "body g5b"), owner, s));

        Assert.NotNull(post.Audience);          // non-null
        Assert.Empty(post.Audience.Grants);     // and empty
        Assert.True(post.Audience.IsEmpty);
        Assert.Equal(string.Empty, post.ComponentId); // G·2 — lane exclusivity
        Assert.Equal(group.Id, post.GroupId);
    }

    // ── 7 — G6_NonMemberCreateDenied (G6 FACES; G·3) ───────────────────────
    //
    // A non-member's create is denied with the exact
    // <c>UnauthorizedAccessException</c> (U6's pinned message) **and** the
    // gate's Deny row is **persisted** (the service commits it via
    // <c>SaveChangesAsync</c> before the throw — the row survives the
    // rejection; G6 FACES).

    [Fact]
    public async Task G6_NonMemberCreateDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g6-owner";
        const string stranger = "u-gp-g6-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "G6 family", null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => svc.CreateGroupPostAsync(
                new GroupPostDraft(group.Id, null, "body g6"), stranger, s)));

        Assert.Contains("not a member of the group", ex.Message);

        // The gate's Deny row **survived** the throw (C3, G6 FACES): the
        // create-gate shape — TargetId = the group id (the channel as the
        // gate's target), counts null, Via Group.
        var gateRow = Assert.Single(await GroupPostAudits(store, actor: stranger),
            a => a.TargetId == group.Id);
        Assert.Equal(AccessOutcome.Deny, gateRow.Outcome);
        Assert.Equal(AccessVia.Group, gateRow.Via);
        Assert.Null(gateRow.VisibleCount);
        Assert.Null(gateRow.HiddenCount);
    }

    // ── 8 — G7_ModeratorNonMemberDenied (G7 FACES; G·4) ────────────────────
    //
    // *Fixture*: a <c>ModeratorAssignment</c> row present (for a **different**
    // component — the component lane's standing does not extend to a group
    // lane). The group lane is membership-only: the moderator, being a
    // non-member, is denied. No `Via = Moderator` row.

    [Fact]
    public async Task G7_ModeratorNonMemberDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g7-owner";
        const string moderator = "u-gp-g7-moderator";

        var group = await userInfo.CreateGroupAsync(owner, "G7 family", null);
        await Plant(store, GroupPost("g7-post", group.Id, owner));
        await Plant(store, new Component { Id = "c-g7", Name = "Safety", Enabled = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "g7-assign", UserId = moderator, ComponentId = "c-g7",
            GrantedBy = "u-gp-g7-admin", At = DateTimeOffset.UtcNow
        });

        var feed = await svc.ListGroupFeedAsync(group.Id, moderator, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var detail = await svc.GetGroupPostAsync(group.Id, "g7-post", moderator);
        Assert.Null(detail.Post);

        var rows = await GroupPostAudits(store, actor: moderator);
        Assert.All(rows, r =>
        {
            Assert.Equal(AccessOutcome.Deny, r.Outcome);
            Assert.Equal(AccessVia.Group, r.Via); // never Moderator (G·4)
        });
    }

    // ── 9 — G8_BreakGlassDoesNotApplyToGroupPosts (G8 FACES; G·4) ──────────
    //
    // *Fixture*: a consumed, unexpired <c>AdminOverride</c> (break-glass
    // elevation active on the **audience** lane — the
    // <c>AuthorizationServiceTests</c> seed shape) for a **non-member**
    // GlobalAdmin. The group lane never reads break-glass (G·4 — the
    // strictest privacy lane: *no break-glass on group posts, full stop*),
    // so the elevation is inert here: Deny.

    [Fact]
    public async Task G8_BreakGlassDoesNotApplyToGroupPosts()
    {
        var (store, conn) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g8-owner";
        const string admin = "u-gp-g8-globaladmin";
        var now = DateTimeOffset.UtcNow;

        var group = await userInfo.CreateGroupAsync(owner, "G8 family", null);
        await Plant(store, GroupPost("g8-post", group.Id, owner));

        // Operator-written, consumed and unexpired (the usable shape).
        await SeedAdminOverrideAsync(conn, admin, "tok-gp-g8",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        var feed = await svc.ListGroupFeedAsync(group.Id, admin, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var detail = await svc.GetGroupPostAsync(group.Id, "g8-post", admin);
        Assert.Null(detail.Post);

        var rows = await GroupPostAudits(store, actor: admin);
        Assert.All(rows, r =>
        {
            Assert.Equal(AccessOutcome.Deny, r.Outcome);
            Assert.Equal(AccessVia.Group, r.Via); // never BreakGlass (G·4)
        });
    }

    // ── 10 — G9_DelegateWithReadInScopeSeesOwnerGroupPosts ─────────────────
    //
    // G·6 / C2: an **in-scope `read`** grant acts with the **owner's**
    // standing — the owner is a member ⇒ the delegate's feed is Allow, and
    // the aggregate row records `Via = Delegation`,
    // `EffectivePrincipalId = owner`.

    [Fact]
    public async Task G9_DelegateWithReadInScopeSeesOwnerGroupPosts()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g9-owner";
        const string delegate_ = "u-gp-g9-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "G9 family", null);
        await Plant(store, GroupPost("g9-post", group.Id, owner));
        await Plant(store, ReadGrant(owner, delegate_, "g9-grant"));

        var feed = await svc.ListGroupFeedAsync(group.Id, delegate_, page: 1);
        var post = Assert.Single(feed.Visible);
        Assert.Equal("g9-post", post.Id);

        var aggregate = Assert.Single(await GroupPostAudits(store, actor: delegate_),
            a => a.TargetId is null);
        Assert.Equal(AccessVia.Delegation, aggregate.Via);
        Assert.Equal(owner, aggregate.EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
    }

    // ── 11 — G10_DelegateWithoutReadDenied (G10 FACES; G·6, C2) ────────────
    //
    // An **out-of-scope** grant (`write` only) acts as the delegate
    // themself; the delegate is **not** a member ⇒ Deny, and the row is
    // still `Via = Delegation` (M1's acting-identity rule — the scope guard
    // is real, the standing tag is the grant's).

    [Fact]
    public async Task G10_DelegateWithoutReadDenied()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g10-owner";
        const string delegate_ = "u-gp-g10-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "G10 family", null);
        await Plant(store, GroupPost("g10-post", group.Id, owner));
        await Plant(store, new DelegationGrant
        {
            Id = "g10-grant",
            OwnerId = owner,
            DelegateId = delegate_,
            Scope = ["write"], // no "read" — out of scope (scope entries are action ids)
            From = DateTimeOffset.UtcNow.AddDays(-1)
        });

        var feed = await svc.ListGroupFeedAsync(group.Id, delegate_, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var aggregate = Assert.Single(await GroupPostAudits(store, actor: delegate_),
            a => a.TargetId is null);
        Assert.Equal(AccessOutcome.Deny, aggregate.Outcome);
        Assert.Equal(AccessVia.Delegation, aggregate.Via);
        Assert.Equal(delegate_, aggregate.EffectivePrincipalId); // themself
    }

    // ── 12 — G11_ReplyInheritsParentGroupLane (G11 FACES; G·7, C-M3·1) ─────
    //
    // Parent's Allow ⇒ the reply list is returned **as-is** under the parent's
    // single group-lane decision: one decision row (the parent's), **no**
    // per-reply row, no second evaluation.

    [Fact]
    public async Task G11_ReplyInheritsParentGroupLane()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g11-owner";

        var group = await userInfo.CreateGroupAsync(owner, "G11 family", null);
        await Plant(store, GroupPost("g11-post", group.Id, owner));
        var reply = new PostReply
        {
            Id = "g11-reply", PostId = "g11-post", AuthorId = owner,
            Body = "reply body", Created = DateTimeOffset.UtcNow
        };
        await Plant(store, reply);

        var detail = await svc.GetGroupPostAsync(group.Id, "g11-post", owner);
        Assert.NotNull(detail.Post);
        var seenReply = Assert.Single(detail.Replies);
        Assert.Equal("g11-reply", seenReply.Id);

        var rows = await GroupPostAudits(store, actor: owner);
        // Exactly the parent's decision row — the reply carries none (G·7).
        var parentRow = Assert.Single(rows, a => a.TargetId == "g11-post");
        Assert.Equal(AccessOutcome.Allow, parentRow.Outcome);
        Assert.Equal(AccessVia.Group, parentRow.Via);
        Assert.DoesNotContain(rows, a => a.TargetId == "g11-reply");
    }

    // ── 13 — G11_ReplyNotEvaluatedOnParentDeny (G11 FACES; G·7) ────────────
    //
    // Parent's Deny ⇒ the reply is **not evaluated** at all: `Post = null`,
    // no replies, and exactly **one** audit row for the visitor (the parent's
    // Deny — no second audience/lane pass, no reply row).

    [Fact]
    public async Task G11_ReplyNotEvaluatedOnParentDeny()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-g11b-owner";
        const string stranger = "u-gp-g11b-stranger";

        var group = await userInfo.CreateGroupAsync(owner, "G11b family", null);
        await Plant(store, GroupPost("g11b-post", group.Id, owner));
        await Plant(store, new PostReply
        {
            Id = "g11b-reply", PostId = "g11b-post", AuthorId = owner,
            Body = "reply body", Created = DateTimeOffset.UtcNow
        });

        var detail = await svc.GetGroupPostAsync(group.Id, "g11b-post", stranger);
        Assert.Null(detail.Post);
        Assert.Empty(detail.Replies);

        // One row — the parent's Deny. No second evaluation, no reply row.
        var denyRow = Assert.Single(await GroupPostAudits(store, actor: stranger),
            a => a.TargetId == "g11b-post");
        Assert.Equal(AccessOutcome.Deny, denyRow.Outcome);
    }

    // ── 14 — G12_GroupPostExcludedFromComponentFeed (G12 FACES; G·2) ───────
    //
    // Structural exclusion (design §2.3(a)): the group post's empty
    // `ComponentId` keeps it out of the M3 component feed's candidate set —
    // it is not a *hidden* candidate (it is not evaluated), so no
    // `"grouppost"`-kind row exists for the visit.

    [Fact]
    public async Task G12_GroupPostExcludedFromComponentFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string actor = "u-gp-g12-actor";
        const string comp = "c-g12";

        var group = await userInfo.CreateGroupAsync(actor, "G12 family", null);
        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, GroupPost("g12-group", group.Id, actor));
        await Plant(store, new Post
        {
            Id = "g12-comp", ComponentId = comp, AuthorId = actor,
            Body = "component post", Created = DateTimeOffset.UtcNow,
            Audience = AudienceOf(actor),
        });

        var feed = await svc.ListFeedAsync(comp, actor, page: 1);
        var ids = feed.Visible.Select(p => p.Id).ToHashSet();
        Assert.Contains("g12-comp", ids);
        Assert.DoesNotContain("g12-group", ids);

        // The group post is not a candidate ⇒ no group-lane row at all.
        Assert.Empty(await GroupPostAudits(store, actor: actor));
    }

    // ── 15 — G13_GroupPostExcludedFromAllFeed (G13 FACES; G·2) ─────────────
    //
    // Same structural exclusion through the "all sections" lane
    // (<c>ListAllFeedAsync</c>) — the group post is absent from `Visible`
    // and not counted as hidden.

    [Fact]
    public async Task G13_GroupPostExcludedFromAllFeed()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string actor = "u-gp-g13-actor";
        const string comp = "c-g13";

        var group = await userInfo.CreateGroupAsync(actor, "G13 family", null);
        await Plant(store, new Component { Id = comp, Name = "Safety", Enabled = true });
        await Plant(store, GroupPost("g13-group", group.Id, actor));
        await Plant(store, new Post
        {
            Id = "g13-comp", ComponentId = comp, AuthorId = actor,
            Body = "component post", Created = DateTimeOffset.UtcNow,
            Audience = AudienceOf(actor),
        });

        var feed = await svc.ListAllFeedAsync(new[] { comp }, actor, page: 1);
        var ids = feed.Visible.Select(p => p.Id).ToHashSet();
        Assert.Contains("g13-comp", ids);
        Assert.DoesNotContain("g13-group", ids);
        Assert.Equal(0, feed.HiddenCount); // not a candidate — nothing hidden

        Assert.Empty(await GroupPostAudits(store, actor: actor));
    }

    // ── 16 — Feed_AggregateAuditRowShape_GroupPost (G·5; §2.1 aggregate) ───
    //
    // The **full** aggregate-row shape on a member's feed visit:
    // TargetKind `"grouppost"`, `TargetId` null, counts set, `Action`
    // `"read"`, both identity columns populated.

    [Fact]
    public async Task Feed_AggregateAuditRowShape_GroupPost()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-fshape-owner";

        var group = await userInfo.CreateGroupAsync(owner, "F shape", null);
        await Plant(store, GroupPost("fshape-post", group.Id, owner));

        var before = DateTimeOffset.UtcNow;
        await svc.ListGroupFeedAsync(group.Id, owner, page: 1);

        var row = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.TargetId is null);
        Assert.NotEmpty(row.Id);
        Assert.True(row.At >= before);
        Assert.Equal(owner, row.ActorId);
        Assert.Equal(owner, row.EffectivePrincipalId);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Null(row.TargetId);
        Assert.Equal(1, row.VisibleCount);
        Assert.Equal(0, row.HiddenCount);
        Assert.Equal(AccessVia.Group, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 17 — Detail_DecisionAuditRowShape_ViaGroup (G·5; §2.1 decision) ────
    //
    // The **full** decision-row shape on a member's detail visit:
    // `TargetId = postId`, counts null, `Via = Group`, `Action` `"read"`.

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaGroup()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-dshape-owner";

        var group = await userInfo.CreateGroupAsync(owner, "D shape", null);
        await Plant(store, GroupPost("dshape-post", group.Id, owner));

        await svc.GetGroupPostAsync(group.Id, "dshape-post", owner);

        var row = Assert.Single(await GroupPostAudits(store, actor: owner),
            a => a.TargetId == "dshape-post");
        Assert.NotEmpty(row.Id);
        Assert.Equal(owner, row.ActorId);
        Assert.Equal(owner, row.EffectivePrincipalId);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Equal("dshape-post", row.TargetId);
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
        Assert.Equal(AccessVia.Group, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 18 — Detail_DecisionAuditRowShape_ViaDelegation (G·5, C2) ──────────
    //
    // The delegate branch of the same decision row: `Via = Delegation`,
    // `EffectivePrincipalId =` the **owner** (in-scope `read` ⇒ the owner's
    // standing), `TargetId =` the post id.

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaDelegation()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-gp-ddel-owner";
        const string delegate_ = "u-gp-ddel-delegate";

        var group = await userInfo.CreateGroupAsync(owner, "D del", null);
        await Plant(store, GroupPost("ddel-post", group.Id, owner));
        await Plant(store, ReadGrant(owner, delegate_, "ddel-grant"));

        await svc.GetGroupPostAsync(group.Id, "ddel-post", delegate_);

        var row = Assert.Single(await GroupPostAudits(store, actor: delegate_),
            a => a.TargetId == "ddel-post");
        Assert.Equal(delegate_, row.ActorId);
        Assert.Equal(owner, row.EffectivePrincipalId);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal("grouppost", row.TargetKind);
        Assert.Equal("ddel-post", row.TargetId);
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
        Assert.Equal(AccessVia.Delegation, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 19 — PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts ───────
    //
    // G·4's **seam-level** absence: over the whole group surface (feed +
    // detail + create allow + create deny), the `PostService` issues **only**
    // group-lane authorization calls — no audience-lane
    // <c>CanAsync</c>/<c>CanSeeAsync</c> of any action, and
    // <c>AccessAction.Moderate</c> is never invoked (the break-glass path
    // is unreachable by construction: the lane never reads
    // <c>AdminOverride</c>/<c>ModeratorAssignment</c> — the G7/G8 fixtures
    // proved it at the decision level; this pins it at the call level).

    [Fact]
    public async Task PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts()
    {
        var (store, _) = await BootStoreAsync();
        var (userInfo, authz, _) = Services(store);
        const string owner = "u-gp-spy-owner";
        const string stranger = "u-gp-spy-stranger";

        var recorder = new RecordingAuthz(authz);
        var spy = new PostService(userInfo, recorder, store);

        var group = await userInfo.CreateGroupAsync(owner, "Spy family", null);
        await Plant(store, GroupPost("spy-post", group.Id, owner));

        // The whole group surface: read (feed + detail), write allow,
        // write deny (caught — the gate's row must still persist).
        await spy.ListGroupFeedAsync(group.Id, owner, page: 1);
        await spy.GetGroupPostAsync(group.Id, "spy-post", owner);
        await RunInSession(store, s => spy.CreateGroupPostAsync(
            new GroupPostDraft(group.Id, null, "body spy"), owner, s));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => RunInSession(store, s => spy.CreateGroupPostAsync(
                new GroupPostDraft(group.Id, null, "body spy2"), stranger, s)));

        // Only group-lane calls reached the authorization seam …
        Assert.True(recorder.GroupLaneCalls >= 4,
            $"expected ≥ 4 group-lane calls, got {recorder.GroupLaneCalls}");
        // … and **no** audience-lane call of any kind …
        Assert.Equal(0, recorder.AudienceLaneCalls);
        // … and no Moderate action anywhere (G·4 at the seam level).
        Assert.Equal(0, recorder.ModerateCalls);
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return (store, conn);
    }

    /// <summary>The three-constructor service trio over the scratch store
    /// (the M3 <c>PostServiceTests.Services</c> precedent, verbatim).</summary>
    private static (UserInfoService User, AuthorizationService Authz, PostService Posts)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var posts = new PostService(userInfo, authz, store);
        return (userInfo, authz, posts);
    }

    /// <summary>A planted group post: non-empty <c>GroupId</c>, empty
    /// <c>ComponentId</c>, audience non-null empty (the G·2/G·8 shape the
    /// service's own writer pins).</summary>
    private static Post GroupPost(string id, string groupId, string author) => new()
    {
        Id = id,
        ComponentId = string.Empty,
        GroupId = groupId,
        AuthorId = author,
        Body = "body " + id,
        Audience = new Audience(),
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

    private static Audience AudienceOf(string userId)
        => new(AudienceMode.Any, [new AudienceGrant(GrantKind.User, userId)]);

    /// <summary>Plant a document row directly (fixture seeding, not a
    /// service write seam — the M3 <c>Plant</c> precedent).</summary>
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
    /// `"grouppost"` — never the M2 `"group"` management rows the
    /// add/remove member lanes append).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> GroupPostAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "grouppost");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    /// <summary>Seed a consumed/unexpired <c>AdminOverride</c> row (the
    /// <c>AuthorizationServiceTests.SeedAdminOverrideAsync</c> precedent —
    /// the hand-rolled <c>mt</c> table, operator-written via psql; the test
    /// mirrors that path with a raw Npgsql insert).</summary>
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
    /// counts audience-lane calls, group-lane calls, and
    /// <c>AccessAction.Moderate</c> invocations. Test #19's spy.</summary>
    private sealed class RecordingAuthz(IAuthorizationService inner) : IAuthorizationService
    {
        public int AudienceLaneCalls { get; private set; }
        public int GroupLaneCalls { get; private set; }
        public int ModerateCalls { get; private set; }

        private void NoteModerate(AccessAction action)
        {
            if (action == AccessAction.Moderate) ModerateCalls++;
        }

        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target)
        {
            AudienceLaneCalls++;
            NoteModerate(action);
            return inner.CanAsync(actorId, action, target);
        }

        public Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session)
        {
            AudienceLaneCalls++;
            NoteModerate(action);
            return inner.CanAsync(actorId, action, target, session);
        }

        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates)
        {
            AudienceLaneCalls++;
            NoteModerate(action);
            return inner.CanSeeAsync(actorId, action, candidates);
        }

        public Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session)
        {
            AudienceLaneCalls++;
            NoteModerate(action);
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
