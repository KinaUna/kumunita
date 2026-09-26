using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Plan M1 step 5 — <see cref="AuthorizationService"/> tests.
/// <para>
/// The seam-test list in <c>docs/design/m1-identity-access.md</c>
/// ("Feedback loops") pins an invariant anchor for each test:
/// C1/C2/C4/C5/C6 plus break-glass inline check and always-on audit.
/// The pure <see cref="AuthorizationService.EvaluateAudience"/> matcher
/// is unit-tested exhaustively without Postgres (the design-doc
/// "MatchGroups truth table" part-test); the DB-backed seam tests run
/// against a fresh scratch DB (per <see cref="PostgresFixture"/>) so
/// the full branch order (owner → moderation → break-glass → audience →
/// deny) is exercised end-to-end.
/// </para>
/// <para>
/// The decision algorithm's branch order is the ADR 0006 §A contract,
/// and the tests exercise it in that order. A regression that flips the
/// branch order (e.g. moderation before owner) is caught by the
/// "moderation does not override owner" assertion.
/// </para>
/// </summary>
public class AuthorizationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Pure matcher truth table (no Postgres) ───────────────────────────
    //
    // The design-doc part-test: `MatchGroups` truth table over Any/All ×
    // user-kind grants × group-kind grants. The delegation + moderator
    // modes are exercised in the DB-backed seam tests below (they call
    // through the full `CanAsync` path with a grant seeded).

    [Fact]
    public void EvaluateAudience_EmptyAudience_AnyMode_Denies()
    {
        // Invariant 1 — the explicit guard:
        var empty = new Audience(AudienceMode.Any, new List<AudienceGrant>());
        Assert.True(empty.IsEmpty, "expected empty audience to be flagged");
        Assert.False(AuthorizationService.EvaluateAudience(
            empty, "u-anyone", new HashSet<string> { "g-1" }));
    }

    [Fact]
    public void EvaluateAudience_EmptyAudience_AllMode_Denies()
    {
        // Invariant 1 — the vacuous-truth pitfall that the guard closes:
        // in mode `All`, `Grants.All(...)` over an empty list is vacuously
        // true and would make an empty `All` resource world-readable.
        var empty = new Audience(AudienceMode.All, new List<AudienceGrant>());
        Assert.True(empty.IsEmpty, "expected empty audience to be flagged");
        Assert.False(AuthorizationService.EvaluateAudience(
            empty, "u-anyone", new HashSet<string> { "g-1" }));
    }

    [Fact]
    public void EvaluateAudience_NullAudience_Public_Allows()
    {
        // Audience null — not audience-restricted; allow all.
        Assert.True(AuthorizationService.EvaluateAudience(
            null, "u-anyone", new HashSet<string>()));
        Assert.True(AuthorizationService.EvaluateAudience(
            null, "u-anyone", new HashSet<string> { "g-only" }));
    }

    [Theory]
    [InlineData(true,  true)]    // user grant  matches   actor
    [InlineData(false, false)]   // user grant  is a different user
    public void EvaluateAudience_AnyMode_SingleUserGrant(bool userMatch, bool expected)
    {
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, userMatch ? "u-actor" : "u-other")]);
        Assert.Equal(expected, AuthorizationService.EvaluateAudience(
            audience, "u-actor", new HashSet<string>()));
    }

    [Theory]
    [InlineData(true,  true)]    // group grant matches an actor's group
    [InlineData(false, false)]   // group grant is an unrelated group
    public void EvaluateAudience_AnyMode_SingleGroupGrant(bool groupMatch, bool expected)
    {
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.Group, groupMatch ? "g-1" : "g-2")]);
        var groupIds = new HashSet<string> { "g-1" };
        Assert.Equal(expected, AuthorizationService.EvaluateAudience(
            audience, "u-actor", groupIds));
    }

    [Theory]
    [InlineData(AudienceMode.Any, "granted-user-yes", "granted-group-yes", true)]
    [InlineData(AudienceMode.Any, "granted-user-yes", "granted-group-no", true)]
    [InlineData(AudienceMode.Any, "granted-user-no",  "granted-group-yes", true)]
    [InlineData(AudienceMode.Any, "granted-user-no",  "granted-group-no", false)]
    [InlineData(AudienceMode.All, "granted-user-yes", "granted-group-yes", true)]
    [InlineData(AudienceMode.All, "granted-user-yes", "granted-group-no", false)]
    [InlineData(AudienceMode.All, "granted-user-no",  "granted-group-yes", false)]
    [InlineData(AudienceMode.All, "granted-user-no",  "granted-group-no", false)]
    public void EvaluateAudience_MixedUserAndGroup_TruthTable(
        AudienceMode mode, string userGrant, string groupGrant, bool expected)
    {
        var audience = new Audience(mode,
        [
            new AudienceGrant(GrantKind.User,
                userGrant  == "granted-user-yes"  ? "u-actor"  : "u-other"),
            new AudienceGrant(GrantKind.Group,
                groupGrant == "granted-group-yes" ? "g-inSet"  : "g-outSet")
        ]);
        var groupIds = new HashSet<string> { "g-inSet" };
        Assert.Equal(expected, AuthorizationService.EvaluateAudience(
            audience, "u-actor", groupIds));
    }

    // ── Invariant C2 — delegation is action-scoped ──────────────────────

    [Fact]
    public async Task C2_Delegate_InScope_BorrowsOwnersStanding_AllowsViaOwnerBranch()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string owner = "u-owner-c2";
        const string delegatee = "u-deleg-c2";
        var now = DateTimeOffset.UtcNow;

        // Grant: scope includes Read. Delegate is not the owner.
        await userInfo.GrantDelegationAsync(owner, delegatee,
            scope: [AccessAction.Read.Id], from: now.AddHours(-1), to: now.AddHours(1));

        // The resource is owner-restricted to `owner`. The owner branch
        // fires because the effective principal (in-scope delegation)
        // is `owner`.
        var target = new TestResource
        {
            Id = "post-c2",
            TargetKind = "post",
            OwnerId = owner,
            Audience = null,  // public by shape; the owner branch is
                              // the first and only Allow branch that
                              // matches.
        };

        var decision = await auth.CanAsync(delegatee, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        // The acting path is recorded as a delegation (C2).
        Assert.Equal(AccessVia.Delegation, decision.Via);
        // Effective principal is the owner (the owner-branch path).
        Assert.Equal(owner, decision.EffectivePrincipalId);
    }

    [Fact]
    public async Task C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-deleg-c2b";
        var now = DateTimeOffset.UtcNow;

        // Grant covers Read only. Actor performs Moderate — out of scope.
        await userInfo.GrantDelegationAsync("u-owner-c2b", actor,
            scope: [AccessAction.Read.Id],
            from: now.AddHours(-1), to: now.AddHours(1));

        // Resource's audience is restricted to another user. The actor has
        // no owner-branch match, no moderator standing, no break-glass —
        // so the audience branch is the only place to allow, and it fails.
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-allowed-other")]);
        var target = new TestResource
        {
            Id = "post-c2b",
            TargetKind = "post",
            OwnerId = "u-original-owner",
            Audience = audience
        };

        var decision = await auth.CanAsync(actor, AccessAction.Moderate, target);
        Assert.False(decision.Allowed);

        // Invariant C2 — the Deny row still records the acting identity
        // (the actor, not the owner) with Via = Delegation. The effective
        // principal in a Deny decision is the actor (the owner's standing
        // was NOT borrowed because the action was out of scope).
        Assert.Equal(AccessVia.Delegation, decision.Via);
        Assert.Equal(actor, decision.EffectivePrincipalId);

        // The audit row in the same standalone session carries the same fields.
        await using var session = store.QuerySession();
        var audit = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Moderate.Id && a.TargetId == "post-c2b")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(audit);
        Assert.Equal(AccessVia.Delegation, audit!.Via);
        Assert.Equal(AccessOutcome.Deny, audit.Outcome);
        Assert.Equal(actor, audit.EffectivePrincipalId);
        Assert.Equal(actor, audit.ActorId);
    }

    // ── Invariant C4 — membership change is live on the next request ────

    [Fact]
    public async Task C4_MembershipChange_IsLiveOnTheNextDecision()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-actor-c4";

        // Create a group and add the actor.
        var group = await userInfo.CreateGroupAsync("u-someone-else", "C4 group", null);
        await userInfo.AddGroupMemberAsync(group.Id, actor, "u-someone-else");

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.Group, group.Id)]);
        var target = new TestResource
        {
            Id = "post-c4",
            TargetKind = "post",
            OwnerId = "u-original-owner",
            Audience = audience
        };

        // First decision: actor is in the group → Allow.
        var allow = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(allow.Allowed);
        Assert.Equal(AccessVia.Audience, allow.Via);

        // Remove actor from the group — the C4 "membership change".
        await userInfo.RemoveGroupMemberAsync(group.Id, actor, "u-someone-else");

        // The very next decision must see the live membership loss.
        var deny = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.False(deny.Allowed);

        // The Deny row landed; the Allow row landed earlier (same actor +
        // target + action). Together they show the live transition.
        await using var session = store.QuerySession();
        var denyRows = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Read.Id && a.TargetId == "post-c4")
            .Where(a => a.Outcome == AccessOutcome.Deny)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(denyRows);
    }

    // ── Invariant C5 — moderator access OFF by default ──────────────────

    [Fact]
    public async Task C5_ModeratorAccess_OffByDefault_ModeratorCannotSee()
    {
        var (store, _conn, _userInfo, auth) = await BootAsync();
        const string moderator = "u-moderator-c5";
        const string componentId = "comp-c5";

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new Component
            {
                Id = componentId,
                Name = "Moderation Component",
                // ModeratorAccess defaults to false (C5) — do NOT set it.
            });
            session.Store(new ModeratorAssignment
            {
                Id = "ma-c5",
                UserId = moderator,
                ComponentId = componentId,
                GrantedBy = "u-root",
                At = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var target = new TestResource
        {
            Id = "post-c5-off",
            TargetKind = "post",
            OwnerId = "u-original-owner",
            ComponentId = componentId,
            Audience = audience
        };

        // OFF by default — the moderator cannot see the resource.
        var decision = await auth.CanAsync(moderator, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task C5_ModeratorAccess_OnWithAssignment_ModeratorCanSee()
    {
        var (store, _conn, _userInfo, auth) = await BootAsync();
        const string moderator = "u-moderator-c5b";
        const string componentId = "comp-c5b";

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new Component
            {
                Id = componentId,
                Name = "Moderation Component (flag on)",
                ModeratorAccess = true   // Standing-moderator path (C5).
            });
            session.Store(new ModeratorAssignment
            {
                Id = "ma-c5b",
                UserId = moderator,
                ComponentId = componentId,
                GrantedBy = "u-root",
                At = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var target = new TestResource
        {
            Id = "post-c5b",
            TargetKind = "post",
            OwnerId = "u-original-owner",
            ComponentId = componentId,
            Audience = audience
        };

        var decision = await auth.CanAsync(moderator, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Moderator, decision.Via);
    }

    // ── ADR 0036 — Community-visible branch ──────────────────────────────
    //
    // The 4th decision branch (after owner / moderation / break-glass,
    // before the public / grant-match branches): a resource whose
    // Audience.Community is true AND whose ComponentId the actor is a
    // member of is visible, via AccessVia.Community. This is the default
    // for new community posts (the Web composer seeds it true); existing
    // posts have it false (the old owner-only behavior). Exercised
    // through the DB-backed seam (the branch lives in the private Decide,
    // not the public static EvaluateAudience test seam).

    private async Task<string> SeedCommunityWithMemberAsync(
        IDocumentStore store, UserInfoService userInfo,
        string componentId, string member)
    {
        // Enabled, non-mandatory component; the member is added via the
        // explicit ComponentMembership lane (the strong-consistency write
        // seam GetCommunityIdsAsync reads).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new Component
            {
                Id = componentId,
                Name = "Community 0036",
                Enabled = true,
                Mandatory = false,
            });
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await userInfo.SetCommunityMembershipAsync(componentId, member, "u-root-0036");
        return componentId;
    }

    [Fact]
    public async Task A0036_CommunityFlagAndMember_Allows_ViaCommunity()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-member-0036";
        const string componentId = "comp-0036-member";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, actor);

        // Community flag on, empty grants (the "all community members"
        // shape), component set — the actor is a member → Allow, via
        // Community (not Audience: the grants list is empty, so the
        // MatchGroups branch would deny; the Community branch short-
        // circuits before it).
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-member",
            TargetKind = "post",
            OwnerId = "u-other-0036",
            ComponentId = componentId,
            Audience = audience,
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Community, decision.Via);
    }

    [Fact]
    public async Task A0036_CommunityFlagButNotMember_Denies()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string nonMember = "u-nonmember-0036";
        const string member = "u-member-0036b";
        const string componentId = "comp-0036-nonmember";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, member);

        // The actor is NOT a member of the component — the Community
        // branch fails (communityIds lacks the component), and the
        // grants are empty so MatchGroups denies too.
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-nonmember",
            TargetKind = "post",
            OwnerId = "u-other-0036b",
            ComponentId = componentId,
            Audience = audience,
        };

        var decision = await auth.CanAsync(nonMember, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    // ADR 0102 — the pre-ADR-0102 "inert" consequence is reversed. When the
    // Community flag is on AND the target's community scope is "all
    // communities" (a null/empty ComponentId), the flag is the whole
    // decision: ANY signed-in actor sees it (the resident-only standing,
    // the same as the AllResidents branch 4.5), regardless of which specific
    // communities they are a member of. Anonymous (empty actorId) is denied
    // (the public branch is the world-readable shape). The old behavior
    // (this shape was inert ⇒ owner-only) is gone.

    [Fact]
    public async Task A0036_CommunityFlagNullComponent_SignedInActor_Allows_ViaCommunity()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-member-0036c";
        const string componentId = "comp-0036-null";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, actor);

        // Community flag on but the target has NO ComponentId (the
        // Event/Project "All communities" scope: null ComponentId +
        // non-null audience). Under ADR 0102 this is the "all residents"
        // shape — the actor's membership of *some* specific community is
        // irrelevant; ANY signed-in actor sees it.
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-nullcomp",
            TargetKind = "post",
            OwnerId = "u-other-0036c",
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Community, decision.Via);
    }

    [Fact]
    public async Task A0036_CommunityFlagNullComponent_SignedInNonMember_Allows()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-standalone-0036c2";
        const string componentId = "comp-0036-null2";
        // Seed a community + a *different* member so the store has live
        // community data; the actor is NOT a member of any community.
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, "u-other-member-0036c2");

        // Community flag on + null ComponentId = "all communities" scope
        // (ADR 0102). A signed-in actor with NO community membership at all
        // still sees it — the flag is the whole decision, not a member
        // match.
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-nullcomp-nonmember",
            TargetKind = "post",
            OwnerId = "u-other-0036c2",
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Community, decision.Via);
    }

    [Fact]
    public async Task A0036_CommunityFlagNullComponent_AnonymousActor_Denies()
    {
        var (store, _conn, _userInfo, auth) = await BootAsync();

        // Community flag on + null ComponentId = "all communities" scope,
        // but the actor is anonymous (empty actorId). The Community branch
        // (4) requires a signed-in actor in this shape; the public branch
        // (5) does not fire because the audience is non-null. → Deny.
        var audience = new Audience(AudienceMode.Any, []) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-nullcomp-anon",
            TargetKind = "post",
            OwnerId = "u-other-0036c3",
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync("", AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task A0036_CommunityFlagFalse_EmptyGrants_OwnerOnly()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string member = "u-member-0036d";
        const string owner = "u-owner-0036d";
        const string componentId = "comp-0036-off";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, member);

        // The pre-ADR-0036 shape: Community false, empty grants. A
        // community member who is NOT the owner gets no Community branch
        // (flag false) and no grant match (empty) → Deny. Only the owner
        // (owner branch) can see it — the old owner-only behavior,
        // preserved for existing posts.
        var audience = new Audience(AudienceMode.Any, []); // Community defaults false
        var target = new TestResource
        {
            Id = "post-0036-off",
            TargetKind = "post",
            OwnerId = owner,
            ComponentId = componentId,
            Audience = audience,
        };

        var memberDecision = await auth.CanAsync(member, AccessAction.Read, target);
        Assert.False(memberDecision.Allowed);

        var ownerDecision = await auth.CanAsync(owner, AccessAction.Read, target);
        Assert.True(ownerDecision.Allowed);
        Assert.Equal(AccessVia.Owner, ownerDecision.Via);
    }

    [Fact]
    public async Task A0036_CommunityFlagPlusExplicitGrant_BothVisible()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string member = "u-member-0036e";
        const string grantedUser = "u-granted-0036e";
        const string componentId = "comp-0036-both";
        await SeedCommunityWithMemberAsync(store, userInfo, componentId, member);

        // Community on AND an explicit user grant. The community member
        // sees it via Community; the explicitly granted user (not a
        // community member) sees it via the MatchGroups branch.
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, grantedUser)]) { Community = true };
        var target = new TestResource
        {
            Id = "post-0036-both",
            TargetKind = "post",
            OwnerId = "u-other-0036e",
            ComponentId = componentId,
            Audience = audience,
        };

        var memberDecision = await auth.CanAsync(member, AccessAction.Read, target);
        Assert.True(memberDecision.Allowed);
        Assert.Equal(AccessVia.Community, memberDecision.Via);

        var grantedDecision = await auth.CanAsync(grantedUser, AccessAction.Read, target);
        Assert.True(grantedDecision.Allowed);
        Assert.Equal(AccessVia.Audience, grantedDecision.Via);
    }

    // ── ADR 0041 — All-residents branch ──────────────────────────────
    //
    // The 4.5 decision branch (after the community branch, before the
    // public branch): a resource whose Audience.AllResidents is true is
    // visible to ANY signed-in actor (a non-empty actorId), regardless of
    // community membership or grants. Anonymous (empty actorId) is denied
    // (the public branch is the world-readable shape; this is resident-only).
    // The owner branch (1) still wins — an owner of an AllResidents resource
    // is allowed via Owner, not via Resident (the branch ordering is the
    // pin: the more specific branch fires first).

    [Fact]
    public async Task A0041_AllResidentsFlag_SignedInActor_Allows_ViaResident()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-resident-0041";
        const string owner = "u-owner-0041";

        // AllResidents flag on, empty grants, NO ComponentId (the flag is
        // the whole decision — no community membership required). A signed-in
        // actor who is NOT the owner and NOT a member of any community sees
        // it via the new Resident branch (not Audience: the grants list is
        // empty, so the MatchGroups branch would deny).
        var audience = new Audience(AudienceMode.Any, []) { AllResidents = true };
        var target = new TestResource
        {
            Id = "post-0041-resident",
            TargetKind = "post",
            OwnerId = owner,
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Resident, decision.Via);
    }

    [Fact]
    public async Task A0041_AllResidentsFlag_AnonymousActor_Denies()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();

        // Anonymous (empty actorId) is denied — the AllResidents branch
        // requires a signed-in actor. The public branch (audience = null)
        // is the world-readable shape; a non-null audience with
        // AllResidents = true is resident-only.
        var audience = new Audience(AudienceMode.Any, []) { AllResidents = true };
        var target = new TestResource
        {
            Id = "post-0041-anon",
            TargetKind = "post",
            OwnerId = "u-other-0041",
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync("", AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task A0041_AllResidentsFlag_OwnerStillWins_ViaOwner()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string owner = "u-owner-0041b";

        // The owner branch (1) fires before the AllResidents branch (4.5) —
        // the owner is allowed via Owner, not via Resident (the branch
        // ordering is the pin: the more specific branch wins).
        var audience = new Audience(AudienceMode.Any, []) { AllResidents = true };
        var target = new TestResource
        {
            Id = "post-0041-owner",
            TargetKind = "post",
            OwnerId = owner,
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync(owner, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        Assert.Equal(AccessVia.Owner, decision.Via);
    }

    [Fact]
    public async Task A0041_AllResidentsFlagFalse_EmptyGrants_Denies()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-nonmember-0041c";

        // AllResidents flag OFF, empty grants, no ComponentId — the pre-ADR-0041
        // shape. A signed-in actor who is NOT the owner gets no branch match
        // (owner fails, community fails, AllResidents fails, public fails
        // because the audience is non-null, MatchGroups denies on empty
        // grants) → Deny.
        var audience = new Audience(AudienceMode.Any, []); // AllResidents defaults false
        var target = new TestResource
        {
            Id = "post-0041-off",
            TargetKind = "post",
            OwnerId = "u-other-0041c",
            ComponentId = null,
            Audience = audience,
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    // ── Invariant C6 — bulk equals per-CanAsync aggregate ───────────────

    [Fact]
    public async Task C6_BulkMatches_PerCanAsync_AggregateOverSameCandidates()
    {
        var (store, _conn, userInfo, auth) = await BootAsync();
        const string actor = "u-actor-c6";

        // Three candidates:
        //   c1 — audience user = actor (audience-Allow)
        //   c2 — audience group = "g-unrelated", actor is NOT in it (Deny)
        //   c3 — owner = actor (owner branch Allow)
        var c1 = new TestResource
        {
            Id = "c6-1", TargetKind = "post", OwnerId = "u-other-c6",
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, actor)])
        };
        var c2 = new TestResource
        {
            Id = "c6-2", TargetKind = "post", OwnerId = "u-other-c6",
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.Group, "g-unrelated-c6")])
        };
        var c3 = new TestResource
        {
            Id = "c6-3", TargetKind = "post", OwnerId = actor,
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.Group, "g-unrelated-c6b")])
        };
        var candidates = new[] { c1, c2, c3 };

        // Per-CanAsync aggregate: the union of per-item decisions.
        var perItem = new Dictionary<string, bool>();
        foreach (var r in candidates)
        {
            var d = await auth.CanAsync(actor, AccessAction.Read, r);
            perItem[r.Id] = d.Allowed;
        }

        var expectedVisibleIds = candidates.Where(r => perItem[r.Id]).Select(r => r.Id).ToHashSet();
        var expectedHiddenCount = candidates.Count(r => !perItem[r.Id]);
        Assert.Equal(2, expectedVisibleIds.Count);
        Assert.Equal(1, expectedHiddenCount);

        // Bulk.
        var bulk = await auth.CanSeeAsync(actor, AccessAction.Read, candidates);
        Assert.Equal(expectedVisibleIds, bulk.Visible.Select(v => v.Id).ToHashSet());
        Assert.Equal(expectedHiddenCount, bulk.HiddenCount);

        // Aggregate audit row: visibleCount/hiddenCount match the per-item
        // aggregate (invariant C3 / C6).
        await using var session = store.QuerySession();
        var aggregateRow = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Read.Id && a.VisibleCount > 0)
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(aggregateRow);
        Assert.Equal(expectedVisibleIds.Count, aggregateRow!.VisibleCount);
        Assert.Equal(expectedHiddenCount, aggregateRow.HiddenCount);
    }

    // ── Invariant C3 — audit row commits in the same transaction ────────

    [Fact]
    public async Task C3_AuditRow_CommitsWithTheDecision_AllowAndDeny()
    {
        var (store, _conn, _userInfo, auth) = await BootAsync();
        const string actor = "u-actor-c3";

        var allowTarget = new TestResource
        {
            Id = "post-c3-allow",
            TargetKind = "post",
            OwnerId = actor,  // owner branch Allow
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, "u-allowed-other")])
        };
        var denyTarget = new TestResource
        {
            Id = "post-c3-deny",
            TargetKind = "post",
            OwnerId = "u-other-owner",
            Audience = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.Group, "g-unrelated")])
        };

        var allow = await auth.CanAsync(actor, AccessAction.Read, allowTarget);
        var deny  = await auth.CanAsync(actor, AccessAction.Read, denyTarget);
        Assert.True(allow.Allowed);
        Assert.False(deny.Allowed);

        // Both rows are persisted — the standalone method committed itself.
        await using var session = store.QuerySession();

        var allowRow = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Read.Id
                && a.TargetId == "post-c3-allow")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(allowRow);
        Assert.Equal(AccessOutcome.Allow, allowRow!.Outcome);
        Assert.Equal(AccessVia.Owner, allowRow.Via);

        var denyRow = await session.Query<AccessAudit>()
            .Where(a => a.Action == AccessAction.Read.Id
                && a.TargetId == "post-c3-deny")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(denyRow);
        Assert.Equal(AccessOutcome.Deny, denyRow!.Outcome);
    }

    // ── Break-glass inline check (ADR 0003 / §4.5) ──────────────────────

    [Fact]
    public async Task BreakGlass_ConsumedAndUnexpired_Elevates()
    {
        var (store, conn, _userInfo, auth) = await BootAsync();
        const string actor = "u-actor-bg";
        var now = DateTimeOffset.UtcNow;

        // Operator-written, consumed and unexpired.
        await SeedAdminOverrideAsync(conn, actor, "tok-bg-1",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var target = new TestResource
        {
            Id = "post-bg", TargetKind = "post",
            OwnerId = "u-other-bg",
            Audience = audience
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.True(decision.Allowed);
        // Break-glass is the "exception" — via is BreakGlass.
        Assert.Equal(AccessVia.BreakGlass, decision.Via);
    }

    [Fact]
    public async Task BreakGlass_NotConsumed_DoesNotElevate()
    {
        var (store, conn, _userInfo, auth) = await BootAsync();
        const string actor = "u-actor-bg2";
        var now = DateTimeOffset.UtcNow;

        // Unconsumed — the inline check requires ConsumedAt to be set
        // (a grant that has not yet been consumed is not an active
        // elevation).
        await SeedAdminOverrideAsync(conn, actor, "tok-bg-2",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: null);

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var target = new TestResource
        {
            Id = "post-bg2", TargetKind = "post",
            OwnerId = "u-other-bg",
            Audience = audience
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public async Task BreakGlass_Expired_DoesNotElevate()
    {
        var (store, conn, _userInfo, auth) = await BootAsync();
        const string actor = "u-actor-bg3";
        var now = DateTimeOffset.UtcNow;

        // Consumed but past expiry — the inline check requires
        // ExpiresAt > now.
        await SeedAdminOverrideAsync(conn, actor, "tok-bg-3",
            grantedAt: now.AddDays(-30), expiresAt: now.AddDays(-1),
            consumedAt: now.AddDays(-29));

        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-someone-else")]);
        var target = new TestResource
        {
            Id = "post-bg3", TargetKind = "post",
            OwnerId = "u-other-bg",
            Audience = audience
        };

        var decision = await auth.CanAsync(actor, AccessAction.Read, target);
        Assert.False(decision.Allowed);
    }

    // ── Test resource (a minimal IAuditableResource for the decision path) ─

    private sealed class TestResource : IAuditableResource
    {
        public string Id { get; set; } = string.Empty;
        public string Name => Id;
        public string? OwnerId { get; set; }
        public Audience? Audience { get; set; }
        public string? ComponentId { get; set; }
        public string TargetKind { get; set; } = string.Empty;
    }

    // ── Shared bootstrap: store + connection string + services ──────────

    private async Task<(IDocumentStore, string, UserInfoService, AuthorizationService)>
        BootAsync()
    {
        var connString = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(connString);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var userInfo = new UserInfoService(store);
        var auth = new AuthorizationService(store, userInfo);
        return (store, connString, userInfo, auth);
    }

    private static async Task SeedAdminOverrideAsync(
        string connString, string userId, string token,
        DateTimeOffset grantedAt, DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt)
    {
        // The AdminOverride table is hand-rolled (AuthorizationFeature /
        // ADR 0004 §B.1) — the operator writes it through psql (OPS §9),
        // so the test mirrors that path with a raw Npgsql insert on a
        // fresh connection (no session required — no document to track).
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
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

        await cmd.ExecuteNonQueryAsync();
    }
}
