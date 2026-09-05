using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M3, plan U9 — the 18 <see cref="PostService"/> seam tests (design doc
/// <c>m3-posts-design.md</c> §2.4). The M3 analog of M2's 22-name seam list
/// (this assembly's <c>DirectoryServiceTests</c> / <c>DirectoryServiceTests_U6</c>
/// are the shape template — same <see cref="PostgresFixture"/>, same
/// <c>BootStoreAsync</c>, same service-composition pattern, fresh scratch
/// Postgres per test method).
/// <para>
/// Each test name is pinned verbatim by §2.4 (the §2.6 drift-guard makes
/// renaming them a drift event) and carries its FACES row (F1–F10, Part 1)
/// and invariant anchors (C-M3·1/2/3, ADR 0006 C1–C6, ADR 0001-B, C5).
/// The tests compose U6's <see cref="PostService"/> over the two frozen
/// seams (<c>IUserInfoService</c> + <c>IAuthorizationService</c>) through
/// U5's <see cref="PostToAuditableResource"/> — no new Core surface.
/// </para>
/// <para>
/// This unit **does not** run the §2.5 acceptance gate (U10) and authors no
/// e2e (U11). The pass/red status of these 18 is the data U10 consumes.
/// </para>
/// </summary>
public class PostServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string ComponentId = "c-u9-comp";

    // ── 1 — F1_FeedVisibleToAudienceMember (F1, C-M3·3 aggregate row) ─────
    //
    // An audience member reads the community feed: the post is in
    // `Visible`, the visit's **aggregate** audit row is present with
    // `VisibleCount >= 1`, `Action = "read"`, `TargetKind = "post"`,
    // `TargetId = null` (the C-M3·3 feed shape — an aggregate, not a
    // decision row), the per-item row carries the post's id and the
    // audience-lane `Via`.

    [Fact]
    public async Task F1_FeedVisibleToAudienceMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f1-owner";
        const string member = "u-u9-f1-member";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f1-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f1", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1);
        var post = Assert.Single(feed.Visible);
        Assert.Equal("f1-post", post.Id);
        Assert.Equal(0, feed.HiddenCount);
        Assert.Equal(1, feed.Total);

        var rows = await PostAudits(store, actor: member);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal("post", aggregate.TargetKind);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);

        var perItem = Assert.Single(rows, a => a.TargetId == "f1-post");
        Assert.Equal(AccessVia.Audience, perItem.Via);
        Assert.Equal(AccessOutcome.Allow, perItem.Outcome);
    }

    // ── 2 — F2_FeedHiddenFromNonMember (F2, C-M3·3, C1) ───────────────────
    //
    // A stranger reads the feed: the post is neither in `Visible` nor
    // rendered anywhere — and leaves **no trace** (F2): the member id and
    // the post id appear in the audit lane (the hidden count + the per-item
    // Deny), but the post's *author* never appears as actor nor effective
    // principal in any row (the audience's membership data is never logged).

    [Fact]
    public async Task F2_FeedHiddenFromNonMember()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f2-owner";
        const string member = "u-u9-f2-member";
        const string stranger = "u-u9-f2-stranger";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f2-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f2", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListFeedAsync(ComponentId, stranger, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var rows = await PostAudits(store, actor: stranger);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(0, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Deny, aggregate.Outcome);

        var perItem = Assert.Single(rows, a => a.TargetId == "f2-post");
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);

        // The post's author (and the audience member) appear nowhere in
        // the stranger's decision rows — C-M2·2's "never logged as an
        // access decision" pin, at the DB level (the M2 U5 precedent).
        Assert.All(rows, r =>
        {
            Assert.NotEqual(owner, r.ActorId);
            Assert.NotEqual(owner, r.EffectivePrincipalId);
            Assert.NotEqual(member, r.ActorId);
            Assert.NotEqual(member, r.EffectivePrincipalId);
        });
    }

    // ── 3 — F3_FeedDeniesModeratorOnAudiencePost (F3, C5 absence, C1) ─────
    //
    // C5's default posture pinned at the service level: the component's
    // <c>ModeratorAccess</c> flag is OFF (the C5 default — M3's product
    // surface never turns it ON), so M1's moderation branch ('branch #2'
    // in §4.4) does not fire. A moderator with an assignment row for this
    // component still reads the post's *audience* — and is outside it ⇒
    // Deny. The `Via = Moderator` lane is unavailable (the flag is OFF) and
    // the `Action` column in the M3 post lane always carries
    // <c>read</c> (the reserved <c>moderate</c> action id is never invoked
    // by M3's post surface — that is the F3/F8 *absence* pin, not a
    // moderation-branch deny pin: the branch is M1's, and if the flag were
    // ON the branch would §4.4-Allow the moderator regardless of
    // the action id they invoked — that path is M3b's lane, exercised by
    // the M3b "moderator peek" spec rather than by these names).

    [Fact]
    public async Task F3_FeedDeniesModeratorOnAudiencePost()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u9-f3-member";
        const string moderator = "u-u9-f3-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = false }); // C5 default-OFF
        await Plant(store, new ModeratorAssignment
        {
            Id = "f3-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u9-admin", At = DateTimeOffset.UtcNow
        });

        await Plant(store, new Post
        {
            Id = "f3-post", ComponentId = ComponentId, AuthorId = member,
            Body = "body f3", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListFeedAsync(ComponentId, moderator, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var rows = await PostAudits(store, actor: moderator);
        var perItem = Assert.Single(rows, a => a.TargetId == "f3-post");
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);
        Assert.Equal(AccessVia.Audience, perItem.Via); // actor's own standing

        // C5 absence in M3's post lane — the reserved `moderate` action id
        // is never invoked by M3's post service; every M3-post-lane row
        // carries `Action = "read"`.
        Assert.All(rows, r => Assert.Equal(AccessAction.Read.Id, r.Action));
    }

    // ── 4a — F4_EmptyAudiencePostAuthorSeesOwnDraft (F4, C1 owner branch) ─
    //
    // C1's owner-branch exception: an empty-audience post (the author's
    // bootstrap default) still allows its *author* — the feed visit is
    // `VisibleCount = 1`, `HiddenCount = 0`, and the per-item decision
    // row is `Allow / Via = Owner`.

    [Fact]
    public async Task F4_EmptyAudiencePostAuthorSeesOwnDraft()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u9-f4-author";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f4-own", ComponentId = ComponentId, AuthorId = author,
            Body = "body f4", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), // C1 — the author's bootstrap default
        });

        var feed = await svc.ListFeedAsync(ComponentId, author, page: 1);
        var post = Assert.Single(feed.Visible);
        Assert.Equal("f4-own", post.Id);
        Assert.Equal(0, feed.HiddenCount);

        var rows = await PostAudits(store, actor: author);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(0, aggregate.HiddenCount);
        var perItem = Assert.Single(rows, a => a.TargetId == "f4-own");
        Assert.Equal(AccessOutcome.Allow, perItem.Outcome);
        Assert.Equal(AccessVia.Owner, perItem.Via);
    }

    // ── 4b — F4_EmptyAudiencePostDeniesNonAuthor (F4, C1) ─────────────────
    //
    // The other half of the C1 exception: the *same* empty-audience post
    // denies everyone else. A second post in the feed (the member's own)
    // keeps the visit's aggregate `Allow`; only the empty author's post
    // lands on the aggregate's `HiddenCount` + a per-item `Deny`.

    [Fact]
    public async Task F4_EmptyAudiencePostDeniesNonAuthor()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u9-f4b-author";
        const string member = "u-u9-f4b-member";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f4b-post", ComponentId = ComponentId, AuthorId = author,
            Body = "body f4b", Created = DateTimeOffset.UtcNow,
            Audience = new Audience(), // empty — C1
        });
        // A visible post of the member keeps the visit's aggregate Allow.
        await Plant(store, new Post
        {
            Id = "f4b-own", ComponentId = ComponentId, AuthorId = member,
            Body = "body f4b own", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1);
        var visibleIds = feed.Visible.Select(p => p.Id).ToHashSet();
        Assert.Contains("f4b-own", visibleIds);
        Assert.DoesNotContain("f4b-post", visibleIds); // C1 — empty audience denies
        Assert.Equal(1, feed.HiddenCount); // exactly the other's empty post

        var rows = await PostAudits(store, actor: member);
        Assert.Single(rows, a => a.TargetId == "f4b-post" && a.Outcome == AccessOutcome.Deny);
        Assert.Single(rows, a => a.TargetId == "f4b-own"  && a.Outcome == AccessOutcome.Allow);
    }

    // ── 5 — F5_MembershipChangeReScopesNextRequest (F5, C4) ───────────────
    //
    // Strong consistency (C4) on the post lane: a group member is added
    // **after** the post exists; the *very next* feed render surfaces the
    // post. The membership add goes through the frozen
    // <c>IUserInfoService</c> write seam (the authorization path reads the
    // live <c>GroupMembership</c> row — no projection, no cache).

    [Fact]
    public async Task F5_MembershipChangeReScopesNextRequest()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-u9-f5-owner";
        const string member = "u-u9-f5-member";
        const string groupId = "g-u9-f5";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        // A real group under the exact id the post's audience names (the
        // post's audience references the group id literally; the
        // membership row is what MatchGroups reads — C4's live-row lane).
        await Plant(store, new Group
        {
            Id = groupId, Name = "F5 group", OwnerId = owner,
            Created = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "f5-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f5", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.Group, groupId),
        });

        // No membership yet — the group grant is in the audience but no
        // one is in the group ⇒ the feed visit denies (Deny-by-default).
        var before = await svc.ListFeedAsync(ComponentId, member, page: 1);
        Assert.Empty(before.Visible);
        Assert.Equal(1, before.HiddenCount);

        // Membership lands through the frozen write seam; the *very next*
        // render must reflect it (live row, C4).
        await userInfo.AddGroupMemberAsync(groupId, member, addedBy: owner);

        var after = await svc.ListFeedAsync(ComponentId, member, page: 1);
        Assert.Contains(after.Visible, p => p.Id == "f5-post");
        Assert.Equal(0, after.HiddenCount);
    }

    // ── 6 — F6_DelegateWithReadInScopeSeesAuthorPost (F6, C2) ─────────────
    //
    // C2 in-scope: the delegate borrows the owner's standing for a `read`
    // grant — the detail decision is `Allow` with `Via = Delegation` and
    // `EffectivePrincipalId = owner` (the owner branch fired; the delegate
    // is acting in the owner's shoes).

    [Fact]
    public async Task F6_DelegateWithReadInScopeSeesAuthorPost()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-u9-f6-owner";
        const string delegatee = "u-u9-f6-dev";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f6-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f6", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner), // owner-only audience
        });

        var grant = await userInfo.GrantDelegationAsync(
            ownerId: owner, delegateId: delegatee,
            scope: [AccessAction.Read.Id],
            from: DateTimeOffset.UtcNow.AddHours(-1), to: null);
        Assert.Contains(AccessAction.Read.Id, grant.Scope);

        var detail = await svc.GetPostAsync("f6-post", delegatee);
        Assert.NotNull(detail.Post);
        Assert.Equal("f6-post", detail.Post!.Id);

        var rows = await PostAudits(store, actor: delegatee);
        var row = Assert.Single(rows, a => a.TargetId == "f6-post");
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(AccessVia.Delegation, row.Via);
        Assert.Equal(owner, row.EffectivePrincipalId);
        Assert.Equal(delegatee, row.ActorId);
    }

    // ── 7 — F7_DelegateWithoutReadDenies (F7, C2) ─────────────────────────
    //
    // C2 out-of-scope: the delegate has a grant that does **not** include
    // `read` (scoped to the reserved M3b `moderate` action id). The
    // owner's standing is NOT borrowed for this action — the delegate acts
    // as self, is not in the owner-only audience ⇒ Deny, and the row
    // carries the acting identity (`Via = Delegation`,
    // `EffectivePrincipalId = delegatee`).

    [Fact]
    public async Task F7_DelegateWithoutReadDenies()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-u9-f7-owner";
        const string delegatee = "u-u9-f7-dev";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f7-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f7", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });

        var grant = await userInfo.GrantDelegationAsync(
            ownerId: owner, delegateId: delegatee,
            scope: [AccessAction.Moderate.Id], // `read` is out of scope
            from: DateTimeOffset.UtcNow.AddHours(-1), to: null);
        Assert.DoesNotContain(AccessAction.Read.Id, grant.Scope);

        var detail = await svc.GetPostAsync("f7-post", delegatee);
        Assert.Null(detail.Post);
        Assert.Empty(detail.Replies);

        var rows = await PostAudits(store, actor: delegatee);
        var row = Assert.Single(rows, a => a.TargetId == "f7-post");
        Assert.Equal(AccessOutcome.Deny, row.Outcome);
        Assert.Equal(AccessVia.Delegation, row.Via);   // acting identity carried (C2)
        Assert.Equal(delegatee, row.EffectivePrincipalId);
    }

    // ── 8 — F8_ComponentIsFilterNotAccessGate (F8, C-M3·2, C5 absence) ────
    //
    // C-M3·2 + C5's default posture at the service level: the component
    // the moderator governs is still a *feed organizer* and nothing more —
    // the moderator reads through the post's *audience*, not the
    // component's standing (C-M3·2's "not an access decision" pin), and
    // since the component's `ModeratorAccess` flag is OFF by default (C5)
    // the M1 branch (§4.4 branch #2) is not even entered for a `read`
    // invocation. A moderator who is not in the audience cannot see the
    // post — the pin is that the component is a *candidate filter* (the
    // product query), never a *gate* (an access decision), and the M3
    // post lane never calls the reserved `moderate` action id (the F3/F8
    // C5 absence pin).

    [Fact]
    public async Task F8_ComponentIsFilterNotAccessGate()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u9-f8-member";
        const string moderator = "u-u9-f8-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = false }); // C5 default-OFF
        await Plant(store, new ModeratorAssignment
        {
            Id = "f8-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u9-admin", At = DateTimeOffset.UtcNow
        });

        await Plant(store, new Post
        {
            Id = "f8-post", ComponentId = ComponentId, AuthorId = member,
            Body = "body f8", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var feed = await svc.ListFeedAsync(ComponentId, moderator, page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(1, feed.HiddenCount);

        var rows = await PostAudits(store, actor: moderator);
        var perItem = Assert.Single(rows, a => a.TargetId == "f8-post");
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);
        // C-M3·2's "not an access decision" + C5's default-OFF branch:
        // the denial is on the audience lane, not on the component's
        // standing lane (the component never gates a post's visibility).
        Assert.Equal(AccessVia.Audience, perItem.Via);
        Assert.All(rows, r => Assert.NotEqual(AccessVia.Moderator, r.Via));
    }

    // ── 9 — F9_CandidateFilterEmitsNoAuditRow (F9, C-M3·2) ────────────────
    //
    // C-M3·2 pin at the service — the candidate query (the
    // `componentId → candidate posts` filter) is **not** an `AccessAudit`
    // subject. Two probes in one fresh scratch DB:
    //   (a) a present component with **zero** posts — the candidate set
    //       surfaces as empty, no decision runs, and the entire
    //       `AccessAudit` table stays empty;
    //   (b) the read-side of the same pin — `GetComponentsAsync(bool)` —
    //       whose `enabledOnly` filter is a *read*, not a decision;
    //       neither call appends an `AccessAudit` row (U4's F9 unit test
    //       anchors this seam at the `UserInfoService` level; this is the
    //       service-level re-anchor).

    [Fact]
    public async Task F9_CandidateFilterEmitsNoAuditRow()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Component { Id = "c-u9-off", Name = "Safety-off", Enabled = false });

        // (a) Present component, zero candidate posts: the product query
        //     runs, the candidate set is empty, the decision lane is
        //     skipped — `AccessAudit` is empty in the fresh scratch DB.
        var feed = await svc.ListFeedAsync(ComponentId, "u-u9-f9", page: 1);
        Assert.Empty(feed.Visible);
        Assert.Equal(0, feed.HiddenCount);

        Assert.Empty(await AllAudits(store));

        // (b) `GetComponentsAsync` — the candidate read is not a decision.
        var enabledOnly = await userInfo.GetComponentsAsync(enabledOnly: true);
        var all = await userInfo.GetComponentsAsync(enabledOnly: false);
        Assert.Contains(enabledOnly, c => c.Id == ComponentId);
        Assert.DoesNotContain(enabledOnly, c => c.Id == "c-u9-off");
        Assert.Contains(all, c => c.Id == "c-u9-off");

        Assert.Empty(await AllAudits(store));
    }

    // ── 10a — F10_ReplyVisibleIffParentVisible (F10, C-M3·1) ──────────────
    //
    // C-M3·1 row 1: parent `Allow` ⇒ the reply is rendered. Crucially,
    // **no reply produces its own audit row** (C-M3·3: the row for the
    // visit is the *parent's* single decision; a reply has **no**
    // `Audience` of its own — `PostReply` has no `Audience` field at
    // all — so there is nothing to evaluate).

    [Fact]
    public async Task F10_ReplyVisibleIffParentVisible()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f10a-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f10a-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f10a", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });
        await Plant(store, new PostReply
        {
            Id = "f10a-reply", PostId = "f10a-post", AuthorId = "u-u9-f10a-repl1",
            Body = "reply f10a", Created = DateTimeOffset.UtcNow,
        });

        var detail = await svc.GetPostAsync("f10a-post", owner);
        Assert.NotNull(detail.Post);
        Assert.Equal("f10a-post", detail.Post!.Id);
        var reply = Assert.Single(detail.Replies);
        Assert.Equal("f10a-reply", reply.Id);

        var rows = await PostAudits(store, actor: owner);
        var decisionRow = Assert.Single(rows, a => a.TargetId == "f10a-post");
        Assert.Equal(AccessOutcome.Allow, decisionRow.Outcome);
        Assert.Equal(AccessVia.Owner, decisionRow.Via);
        Assert.DoesNotContain(rows, a => a.TargetId == "f10a-reply");
    }

    // ── 10b — F10_ReplyNotEvaluatedOnParentDeny (F10, C-M3·1, C3) ─────────
    //
    // C-M3·1 row 2: parent `Deny` ⇒ the reply is **not evaluated**
    // (short-circuits at the parent). No *reply* audit row at all; the
    // parent's row is `Deny`. The result surfaces `Post = null` (U6's
    // fail-closed shape) and `Replies = []`.

    [Fact]
    public async Task F10_ReplyNotEvaluatedOnParentDeny()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f10b-owner";
        const string member = "u-u9-f10b-member";
        const string stranger = "u-u9-f10b-stranger";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f10b-post", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f10b", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member), // stranger not in
        });
        await Plant(store, new PostReply
        {
            Id = "f10b-reply", PostId = "f10b-post", AuthorId = member,
            Body = "reply f10b", Created = DateTimeOffset.UtcNow,
        });

        var detail = await svc.GetPostAsync("f10b-post", stranger);
        Assert.Null(detail.Post);
        Assert.Empty(detail.Replies);

        var rows = await PostAudits(store, actor: stranger);
        var perItem = Assert.Single(rows, a => a.TargetId == "f10b-post");
        Assert.Equal(AccessOutcome.Deny, perItem.Outcome);
        Assert.DoesNotContain(rows, a => a.TargetId == "f10b-reply");
    }

    // ── 11 — Feed_AggregateAuditRowShape (C-M3·3, F1) ─────────────────────
    //
    // The C-M3·3 feed shape pinned: **one aggregate** `AccessAudit` row for
    // the visit — `TargetId = null`, `TargetKind = "post"`, `Action =
    // "read"`, `VisibleCount = 1`, `HiddenCount = 1`, `Outcome = Allow`,
    // and the aggregate `Via` is the first visible candidate's `Via`
    // (Owner here). The hidden post is its **own** per-item `Deny` row —
    // the aggregate does not double-count it as a second row.

    [Fact]
    public async Task Feed_AggregateAuditRowShape()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u9-f11-member";
        const string hiddenOwner = "u-u9-f11-hidden-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f11-own", ComponentId = ComponentId, AuthorId = member,
            Body = "body f11 own", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });
        await Plant(store, new Post
        {
            Id = "f11-hidden", ComponentId = ComponentId, AuthorId = hiddenOwner,
            Body = "body f11 hidden", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, hiddenOwner),
        });

        var feed = await svc.ListFeedAsync(ComponentId, member, page: 1);
        Assert.Contains(feed.Visible, p => p.Id == "f11-own");
        Assert.DoesNotContain(feed.Visible, p => p.Id == "f11-hidden");
        Assert.Equal(1, feed.HiddenCount);

        var rows = await PostAudits(store, actor: member);
        var aggregate = Assert.Single(rows, a => a.TargetId is null);
        Assert.Equal("post", aggregate.TargetKind);
        Assert.Equal(AccessAction.Read.Id, aggregate.Action);
        Assert.Equal(1, aggregate.VisibleCount);
        Assert.Equal(1, aggregate.HiddenCount);
        Assert.Equal(AccessOutcome.Allow, aggregate.Outcome);
        Assert.Equal(AccessVia.Owner, aggregate.Via); // first visible is the member's own

        var hidden = Assert.Single(rows, a => a.TargetId == "f11-hidden");
        Assert.Equal(AccessOutcome.Deny, hidden.Outcome);
        // C2 / M1 §4.4 branch 6 — the Deny row carries the *denied
        // actor's* standing (the acting identity, not the post author's):
        // the member was denied; they are the actor in the row.
        Assert.Equal(member, hidden.EffectivePrincipalId);
    }

    // ── 12 — Detail_DecisionAuditRowShape_ViaOwner (C-M3·3, C1) ───────────
    //
    // The C-M3·3 detail shape pinned for the `Via = Owner` case — the
    // visitor **is** the author, so the owner branch fires (before
    // MatchGroups). The single decision row is `Allow / Via = Owner` with
    // `EffectivePrincipalId = owner` (= actor).

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaOwner()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f12-owner";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f12-owner", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f12", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });

        var detail = await svc.GetPostAsync("f12-owner", owner);
        Assert.NotNull(detail.Post);

        var rows = await PostAudits(store, actor: owner);
        var row = Assert.Single(rows);
        Assert.Equal("f12-owner", row.TargetId);
        Assert.Equal("post", row.TargetKind);
        Assert.Equal(AccessAction.Read.Id, row.Action);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(owner, row.EffectivePrincipalId);
    }

    // ── 13 — Detail_DecisionAuditRowShape_ViaAudience (C-M3·3) ────────────
    //
    // The C-M3·3 detail row pinned for the `Via = Audience` case — the
    // visitor is **not** the owner (the owner branch does not fire), is
    // in the audience, and no delegation is planted in this test, so the
    // decision records the audience-lane match.

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaAudience()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string owner = "u-u9-f13-owner";
        const string member = "u-u9-f13-member";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f13-aud", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f13", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        var detail = await svc.GetPostAsync("f13-aud", member);
        Assert.NotNull(detail.Post);

        var rows = await PostAudits(store, actor: member);
        var row = Assert.Single(rows);
        Assert.Equal("f13-aud", row.TargetId);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(AccessVia.Audience, row.Via);
        Assert.Equal(member, row.EffectivePrincipalId);
    }

    // ── 14 — Detail_DecisionAuditRowShape_ViaDelegation (C-M3·3, C2) ──────
    //
    // The C-M3·3 detail row pinned for the `Via = Delegation` case — the
    // delegate borrows the owner's standing for `read` (C2), the owner
    // branch fires and the decision is Allowed; the row carries the acting
    // identity (`ActorId = delegatee`, `Via = Delegation`) and the
    // borrowed standing's principal id (`EffectivePrincipalId = owner`).

    [Fact]
    public async Task Detail_DecisionAuditRowShape_ViaDelegation()
    {
        var store = await BootStoreAsync();
        var (userInfo, _, svc) = Services(store);
        const string owner = "u-u9-f14-owner";
        const string delegatee = "u-u9-f14-dev";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });
        await Plant(store, new Post
        {
            Id = "f14-deleg", ComponentId = ComponentId, AuthorId = owner,
            Body = "body f14", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, owner),
        });

        var grant = await userInfo.GrantDelegationAsync(
            ownerId: owner, delegateId: delegatee,
            scope: [AccessAction.Read.Id],
            from: DateTimeOffset.UtcNow.AddHours(-1), to: null);
        Assert.Contains(AccessAction.Read.Id, grant.Scope);

        var detail = await svc.GetPostAsync("f14-deleg", delegatee);
        Assert.NotNull(detail.Post);

        var rows = await PostAudits(store, actor: delegatee);
        var row = Assert.Single(rows);
        Assert.Equal("f14-deleg", row.TargetId);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(AccessVia.Delegation, row.Via);
        Assert.Equal(delegatee, row.ActorId);
        Assert.Equal(owner, row.EffectivePrincipalId);
    }

    // ── 15 — AuthorAudienceWrittenVerbatim (ADR 0001-B) ───────────────────
    //
    // ADR 0001-B pin: the author's chosen `Audience` is written **verbatim**
    // into the `Post` row (the `PostDraft.Audience → Post.Audience` map
    // never mutates the shape). We pick a non-default audience (an
    // `All` mode with a mixed user + group grant) and assert the
    // round-tripped `Post.Audience` equals the input, both on the returned
    // entity and on the DB row.

    [Fact]
    public async Task AuthorAudienceWrittenVerbatim()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string author = "u-u9-f15-author";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true });

        // A non-default shape: `All` mode + two grant kinds (a user and a
        // group) so the round-trip proves the nested Audience document is
        // stored readably and its grants are bit-identical.
        var authorAudience = new Audience(
            AudienceMode.All,
            [
                new AudienceGrant(GrantKind.User, author),
                new AudienceGrant(GrantKind.Group, "g-u9-f15"),
            ]);

        var draft = new PostDraft(ComponentId, "F15", "body f15", authorAudience);
        var post = await RunInSession(store, async session =>
            await svc.CreatePostAsync(draft, author, session));

        // The returned entity carries the shape verbatim.
        Assert.Equal(author, post.AuthorId);
        Assert.Equal(ComponentId, post.ComponentId);
        Assert.Equal(authorAudience.Mode, post.Audience.Mode);
        Assert.Equal(GrantsOf(authorAudience), GrantsOf(post.Audience));

        // And the DB row carries the same shape.
        await using var r = store.QuerySession();
        var loaded = await r.LoadAsync<Post>(post.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.Equal(authorAudience.Mode, loaded!.Audience.Mode);
        Assert.Equal(GrantsOf(authorAudience), GrantsOf(loaded.Audience));
    }

    // ── 16 — PostService_MakesNoModerateCall (C5 absence, M3) ────────────
    //
    // C5 absence pinned at the service level: the M3 post lane never
    // invokes the reserved `AccessAction.Moderate` action id. Two
    // actors — the audience member (allowed) and a moderator who is not
    // in the audience (denied, on audience grounds — the `ModeratorAccess`
    // flag is OFF by default, so M1's branch #2 does not fire for a
    // `read` invocation) — drive the feed + detail visits. The M3
    // post lane's `Action` column always carries `read` and never the
    // reserved `moderate` action id (the pin is the absence of the
    // reserved action id in this lane, not the moderation branch itself —
    // that branch is M1's, and when its flag is ON it §4.4-Allows
    // regardless of action id, which is M3b's lane and out of these
    // names' pin).

    [Fact]
    public async Task PostService_MakesNoModerateCall()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u9-f16-member";
        const string moderator = "u-u9-f16-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = false }); // C5 default-OFF
        await Plant(store, new ModeratorAssignment
        {
            Id = "f16-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u9-admin", At = DateTimeOffset.UtcNow
        });

        await Plant(store, new Post
        {
            Id = "f16-post", ComponentId = ComponentId, AuthorId = member,
            Body = "body f16", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, member),
        });

        // Drive the M3 service lane (feed + detail) for both the member
        // (allowed by the audience) and the moderator (denied on audience
        // grounds — they're not in the audience, and the C5 default-OFF
        // branch is not entered for a `read` invocation, so no `moderate`
        // action id is reachable through M3's post lane).
        await svc.ListFeedAsync(ComponentId, member, page: 1);
        await svc.GetPostAsync("f16-post", member);
        await svc.ListFeedAsync(ComponentId, moderator, page: 1);
        await svc.GetPostAsync("f16-post", moderator);

        var allRows = await AllAudits(store);
        Assert.NotEmpty(allRows); // the visits did run
        Assert.All(allRows, r =>
        {
            Assert.NotEqual(AccessVia.Moderator, r.Via);
            Assert.NotEqual(AccessAction.Moderate.Id, r.Action);
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // M3b, plan U9 — the 5 ADDs (design doc m3b-moderation.md §2.5,
    // rows 9–13). These tests exercise U3's C-M3b·3 lanes (the
    // Moderate-gated write lanes F3/F4 — HidePostAsync /
    // RemovePostAsync) over the two frozen M1/M2 seams + the PostStatus
    // shape (§2.2.1). Pinned verbatim per §2.5 — the §2.7 drift-guard
    // makes renaming them a drift event. Do not reorder the M3 lanes
    // above (M3's own §2.6 drift-guard pins them).
    // ═══════════════════════════════════════════════════════════════════

    // ── M3b·9 — HidePostAsync_Moderator_WritesStatusHidden_ViaTagIsAdmin ─
    //
    // C-M3b·3 (F3) — the Moderate-gated write lane succeeds for a caller
    // with a standing moderator in this component (the ADR 0003 §SoD
    // split: Core trusts the caller's standing, the Web layer verifies
    // the role): post.Status flips to PostStatus.Hidden. The M1-seam's
    // audit record (Action = "moderate", TargetId = the post id,
    // Outcome = Allow) is the canonical write audit — same TargetKind
    // "post" as the M3 lane.

    [Fact]
    public async Task HidePostAsync_Moderator_WritesStatusHidden_ViaTagIsAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string moderator = "u-m3b-m-svc-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "m9-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u-m3b-m-svc-admin", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "m9-post", ComponentId = ComponentId, AuthorId = "u-m3b-m-svc-author",
            Body = "body m9", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-m-svc-member"),
            Status = PostStatus.Active,
        });

        await RunInSession(store, async session =>
            await svc.HidePostAsync("m9-post", moderator, session));

        // The post's Status flipped to the exact PostStatus.Hidden literal
        // (the C-M3b·3 F3 pin).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("m9-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Hidden, post!.Status);

        // The canonical write record from the M1-frozen seam: the audit
        // row for Action = AccessAction.Moderate.Id, TargetId = the
        // post id, ActorId = the acting caller, Outcome = Allow.
        var rows = await PostAudits(store, actor: moderator);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "m9-post"
                                   && a.Outcome == AccessOutcome.Allow);
    }

    // ── M3b·10 — HidePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState ─
    //
    // C-M3b·3 (F3, SoD) — a caller without a standing moderator in this
    // component is denied (M1-seam branch #2 does not fire): the post's
    // Status is NOT updated (it remains PostStatus.Active — the POCO
    // default), and the Deny audit row (Action = "moderate",
    // TargetId = the post id, Outcome = Deny) is committed. C3 — no
    // partial write; the audit row is the only trace.

    [Fact]
    public async Task HidePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string caller = "u-m3b-m-svc-callernon";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new Post
        {
            Id = "m10-post", ComponentId = ComponentId, AuthorId = "u-m3b-m-svc-author",
            Body = "body m10", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-m-svc-member"),
            Status = PostStatus.Active,
        });

        await RunInSession(store, async session =>
            await svc.HidePostAsync("m10-post", caller, session));

        // The post's Status must remain PostStatus.Active (no partial
        // write — the lane's "if (decision.Allowed)" gate did not fire).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("m10-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Active, post!.Status);

        // The Deny audit row for the write attempt is committed.
        var rows = await PostAudits(store, actor: caller);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "m10-post"
                                   && a.Outcome == AccessOutcome.Deny);
    }

    // ── M3b·11 — RemovePostAsync_Moderator_WritesStatusRemoved_ViaTagIsAdmin ─
    //
    // C-M3b·3 (F4) — same shape as the hide lane (M3b·9) but the
    // domain write is hard-remove: post.Status flips to
    // PostStatus.Removed (the §2.2.1 pin + the C-M3b·3 F4 anchor).

    [Fact]
    public async Task RemovePostAsync_Moderator_WritesStatusRemoved_ViaTagIsAdmin()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string moderator = "u-m3b-m-svc-moderator";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new ModeratorAssignment
        {
            Id = "m11-assign", UserId = moderator, ComponentId = ComponentId,
            GrantedBy = "u-m3b-m-svc-admin", At = DateTimeOffset.UtcNow
        });
        await Plant(store, new Post
        {
            Id = "m11-post", ComponentId = ComponentId, AuthorId = "u-m3b-m-svc-author",
            Body = "body m11", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-m-svc-member"),
            Status = PostStatus.Active,
        });

        await RunInSession(store, async session =>
            await svc.RemovePostAsync("m11-post", moderator, session));

        // The post's Status flipped to the exact PostStatus.Removed
        // literal (the C-M3b·3 F4 pin — hard-remove).
        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("m11-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Removed, post!.Status);

        var rows = await PostAudits(store, actor: moderator);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "m11-post"
                                   && a.Outcome == AccessOutcome.Allow);
    }

    // ── M3b·12 — RemovePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState ─
    //
    // C-M3b·3 (F4, SoD) — same shape as M3b·10: a caller without a
    // standing moderator in this component is denied; the post's
    // Status remains unchanged (PostStatus.Active) and the Deny audit
    // row is the only trace.

    [Fact]
    public async Task RemovePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string caller = "u-m3b-m-svc-callernon";

        await Plant(store, new Component { Id = ComponentId, Name = "Safety", Enabled = true,
                                           ModeratorAccess = true });
        await Plant(store, new Post
        {
            Id = "m12-post", ComponentId = ComponentId, AuthorId = "u-m3b-m-svc-author",
            Body = "body m12", Created = DateTimeOffset.UtcNow,
            Audience = Audience(GrantKind.User, "u-m3b-m-svc-member"),
            Status = PostStatus.Active,
        });

        await RunInSession(store, async session =>
            await svc.RemovePostAsync("m12-post", caller, session));

        await using var s2 = store.QuerySession();
        var post = await s2.LoadAsync<Post>("m12-post", TestContext.Current.CancellationToken);
        Assert.Equal(PostStatus.Active, post!.Status);

        var rows = await PostAudits(store, actor: caller);
        Assert.Contains(rows, a => a.Action == AccessAction.Moderate.Id
                                   && a.TargetId == "m12-post"
                                   && a.Outcome == AccessOutcome.Deny);
    }

    // ── M3b·13 — PostStatus_EnumHasExactlyThreeLiterals_ActiveHiddenRemoved ─
    //
    // Shape test for §2.2.1's pin: the PostStatus enum literal set is
    // exactly { Active, Hidden, Removed }. Set-equality (not order —
    // the pin is the **set** of literals, not their ordinal). No plant,
    // no drive — the test only reads the enum type.

    [Fact]
    public void PostStatus_EnumHasExactlyThreeLiterals_ActiveHiddenRemoved()
    {
        var actual = Enum.GetNames(typeof(PostStatus)).ToHashSet(StringComparer.Ordinal);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(PostStatus.Active),
            nameof(PostStatus.Hidden),
            nameof(PostStatus.Removed),
        };
        Assert.Equal(expected, actual);
    }


    // ── Shared helpers ─────────────────────────────────────────────────────

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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Compose the M3 service trio: <see cref="UserInfoService"/> +
    /// <see cref="AuthorizationService"/> + <see cref="PostService"/> (the
    /// same three-constructor shape U6's <c>AddTransient</c> registration
    /// uses, mirrored here directly against the scratch store — the M2
    /// <c>DirectoryServiceTests</c> precedent).</summary>
    private static (UserInfoService User, AuthorizationService Authz, PostService Posts)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var posts = new PostService(userInfo, authz, store);
        return (userInfo, authz, posts);
    }

    private static Audience Audience(GrantKind kind, string id)
        => new(AudienceMode.Any, [new AudienceGrant(kind, id)]);

    /// <summary>Plant a document row directly (test fixture seeding, not a
    /// service write seam — <c>AuthorAudienceWrittenVerbatim</c> is the
    /// one test that drives the service's own <c>CreatePostAsync</c>
    /// write lane).</summary>
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

    private static async Task RunInSession(IDocumentStore store, Func<IDocumentSession, Task> action)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        await action(session);
    }

    private static List<(GrantKind Kind, string Id)> GrantsOf(Audience a)
        => a.Grants.OrderBy(g => (g.Kind, g.Id)).Select(g => (g.Kind, g.Id)).ToList();

    private static async Task<IReadOnlyList<AccessAudit>> PostAudits(IDocumentStore store, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.TargetKind == "post");
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }
}
