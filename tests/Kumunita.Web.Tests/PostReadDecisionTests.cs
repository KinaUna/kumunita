using Kumunita.Core.Authorization;
using Kumunita.Core.Posts;
using Kumunita.Web.Security;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the <see cref="PostReadDecision.ResolveAsync"/> routing — the
/// single shared seam that both serve routes
/// (<see cref="Kumunita.Web.Controllers.ContentImageController.Serve"/>,
/// <see cref="Kumunita.Web.Controllers.AttachmentController.Serve"/>) and
/// the reply-parent branch of each delegate to.
/// <para>
/// <b>What is pinned:</b> the <em>lane choice</em> — a group-lane post
/// (ADR 0013 G·2 — <c>GroupId</c> non-empty) routes to the <b>membership</b>
/// lane (<see cref="IAuthorizationService.CanSeeGroupAsync"/>, G·1); a
/// component post (ADR 0001/0006 — <c>GroupId</c> empty) routes to the
/// <b>audience</b> lane (<see cref="IAuthorizationService.CanAsync"/> with
/// <see cref="PostToAuditableResource"/>, the M3 read decision). The
/// <b>decision value</b> (Allow/Deny) is the lane's contract, not this
/// helper's — the helper passes it through unchanged.
/// </para>
/// <para>
/// <b>Why this test is here (not Core.Tests):</b> the routing is a
/// Web-layer concern (it calls <c>IAuthorizationService</c>, whose audit
/// row is the route's); the helper lives in
/// <c>Kumunita.Web.Security</c> (the same home as
/// <see cref="AttachmentIds"/> / <see cref="ContentImageIds"/> /
/// <see cref="MarkdownRenderer"/>). This test is a <em>pure routing pin</em>
/// — it needs no store, no Postgres, no Testcontainers — just an
/// <c>NSubstitute</c> <see cref="IAuthorizationService"/> that records
/// which lane was called. It does <b>not</b> lift the drift-paused
/// <see cref="AttachmentServingTests"/> / <see cref="ContentImageServingTests"/>
/// (those need a drivable <see cref="PostService"/> reverse-lookup seam —
/// a separate infra decision).
/// </para>
/// </summary>
public class PostReadDecisionTests
{
    private static IAuthorizationService FakeAuthz(Decision groupResult, Decision componentResult)
    {
        var authz = Substitute.For<IAuthorizationService>();
        authz.CanSeeGroupAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>())
            .Returns(groupResult);
        authz.CanAsync(
                Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(componentResult);
        return authz;
    }

    private static Post GroupPost(string id = "gp-1", string groupId = "grp-1", string author = "u-gp")
        => new()
        {
            Id = id,
            GroupId = groupId,       // G·2 — non-empty ⇒ group lane
            ComponentId = string.Empty, // G·2 — lane exclusivity
            AuthorId = author,
            Audience = new Audience(), // G·8 — non-null empty
            Body = "hello",
            Created = DateTimeOffset.UtcNow,
        };

    private static Post ComponentPost(string id = "cp-1", string componentId = "comp-1", string author = "u-cp")
        => new()
        {
            Id = id,
            GroupId = string.Empty,   // empty ⇒ component lane
            ComponentId = componentId,
            AuthorId = author,
            Audience = new(AudienceMode.Any, [new AudienceGrant(GrantKind.User, author)]),
            Body = "hello",
            Created = DateTimeOffset.UtcNow,
        };

    // ── 1 — GroupPost_RoutesToMembershipLane ─────────────────────────────
    //
    // A group-lane post (GroupId non-empty, ADR 0013 G·2) must route to the
    // membership lane (CanSeeGroupAsync, G·1). The audience lane (CanAsync)
    // must NOT be called — the post's empty audience (G·8) would deny
    // everyone except the author under the audience lane's MatchGroups
    // branch (the empty-audience-denies invariant, ADR 0006-C1).

    [Fact]
    public async Task GroupPost_RoutesToMembershipLane_NotAudienceLane()
    {
        var allow = new Decision(true, AccessVia.Group, "u-member");
        var unused = new Decision(false, AccessVia.Audience, "unused");
        var authz = FakeAuthz(groupResult: allow, componentResult: unused);
        var post = GroupPost();

        var decision = await PostReadDecision.ResolveAsync(post, "u-member", authz);

        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Group, decision.Via);
        // Membership lane was called (with the post's GroupId and the post's Id as TargetId):
        await authz.Received(1).CanSeeGroupAsync("u-member", post.GroupId, post.Id);
        // Audience lane was NOT called:
        await authz.DidNotReceive().CanAsync(
            Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
    }

    // ── 2 — ComponentPost_RoutesToAudienceLane ───────────────────────────
    //
    // A component post (GroupId empty, ADR 0001/0006) must route to the
    // audience lane (CanAsync with PostToAuditableResource, the M3 read
    // decision). The membership lane (CanSeeGroupAsync) must NOT be called.

    [Fact]
    public async Task ComponentPost_RoutesToAudienceLane_NotMembershipLane()
    {
        var allow = new Decision(true, AccessVia.Owner, "u-cp");
        var unused = new Decision(false, AccessVia.Group, "unused");
        var authz = FakeAuthz(groupResult: unused, componentResult: allow);
        var post = ComponentPost();

        var decision = await PostReadDecision.ResolveAsync(post, "u-cp", authz);

        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Owner, decision.Via);
        // Audience lane was called (with AccessAction.Read + the post's adapter):
        await authz.Received(1).CanAsync(
            "u-cp", AccessAction.Read, Arg.Any<IAuditableResource>());
        // Membership lane was NOT called:
        await authz.DidNotReceive().CanSeeGroupAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>());
    }

    // ── 3 — GroupPost_Deny_PassesThrough ─────────────────────────────────
    //
    // The helper does NOT re-derive the decision — it passes the lane's
    // result through unchanged. A Deny from the membership lane (a
    // non-member to a group post) must be returned as-is (Allowed == false,
    // Via == Group), and the route maps that to a 404 (not 403).

    [Fact]
    public async Task GroupPost_Deny_PassesThrough_Unmodified()
    {
        var deny = new Decision(false, AccessVia.Group, "u-nonmember");
        var unused = new Decision(false, AccessVia.Audience, "unused");
        var authz = FakeAuthz(groupResult: deny, componentResult: unused);
        var post = GroupPost();

        var decision = await PostReadDecision.ResolveAsync(post, "u-nonmember", authz);

        Assert.False(decision.Allowed);
        Assert.Equal(AccessVia.Group, decision.Via);
        await authz.Received(1).CanSeeGroupAsync("u-nonmember", post.GroupId, post.Id);
    }

    // ── 4 — ComponentPost_Deny_PassesThrough ─────────────────────────────
    //
    // Same contract on the audience lane: a Deny (a non-audience-member to
    // a component post) is passed through unchanged (Allowed == false,
    // Via == Audience), and the route maps it to a 404.

    [Fact]
    public async Task ComponentPost_Deny_PassesThrough_Unmodified()
    {
        var deny = new Decision(false, AccessVia.Audience, "u-stranger");
        var unused = new Decision(false, AccessVia.Group, "unused");
        var authz = FakeAuthz(groupResult: unused, componentResult: deny);
        var post = ComponentPost();

        var decision = await PostReadDecision.ResolveAsync(post, "u-stranger", authz);

        Assert.False(decision.Allowed);
        Assert.Equal(AccessVia.Audience, decision.Via);
        await authz.Received(1).CanAsync(
            "u-stranger", AccessAction.Read, Arg.Any<IAuditableResource>());
    }

    // ── 5 — NullPost_Throws ───────────────────────────────────────────────
    //
    // The helper's contract is non-null by construction (the serve route
    // 404s before calling when the reverse lookup is null). A null post is
    // a programming error — fail fast with ArgumentNullException.

    [Fact]
    public void NullPost_Throws_ArgumentNullException()
    {
        var unused = new Decision(false, AccessVia.Audience, "unused");
        var authz = FakeAuthz(unused, unused);
        Assert.ThrowsAny<ArgumentNullException>(
            () => PostReadDecision.ResolveAsync(null!, "u-1", authz).GetAwaiter().GetResult());
    }
}
