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
