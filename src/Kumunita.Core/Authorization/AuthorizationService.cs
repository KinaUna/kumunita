using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Authorization;

/// <summary>
/// Concrete <see cref="IAuthorizationService"/> (M1 step 5).
/// <para>
/// The decision algorithm (ADR 0006 §A) reduces to a fixed branch
/// order, Deny-by-default, on audience-restricted resources until a
/// policy opts in (ADR 0006-E):
/// </para>
/// <list type="number">
/// <item><b>Owner</b> — the effective principal *is* the resource owner
/// (the "in-scope delegation" case is folded here: the delegate borrows
/// the owner's standing only for actions in the grant's scope).</item>
/// <item><b>Moderation</b> — the component's <c>ModeratorAccess</c> flag
/// is <c>true</c> <em>and</em> the effective principal holds a
/// <see cref="ModeratorAssignment"/> for the component. <see
/// cref="Component.ModeratorAccess"/> defaults to <c>false</c>
/// (Invariant C5).</item>
/// <item><b>Break-glass</b> — the actor has a consumed, non-expired
/// <see cref="AdminOverride"/> row. Read inline on every decision
/// (ADR 0003, no job, no projection lag).</item>
/// <item><b>MatchGroups</b> — the shared audience pass (<see
/// cref="EvaluateAudience"/>). An empty audience <em>always</em> denies
/// (Invariant 1 — the vacuous-truth guard that keeps an empty
/// <c>All</c> resource from becoming world-readable).</item>
/// <item><b>Deny</b> — no branch matched.</item>
/// </list>
/// <para>
/// <see cref="EvaluateAudience"/> is the single matching pass shared
/// by both call surfaces (Invariant 6 / C6, the no-drift property).
/// It is pure (no DB access) and therefore directly testable against
/// the design-doc "truth table" — the <c>Any</c>/<c>All</c> × grant
/// kinds × delegation-mode matrix the seam-test block calls out.
/// </para>
/// <para>
/// Transaction shape (Invariant 3 / C3, ADR 0006-E): the standalone
/// <c>CanAsync</c>/<c>CanSeeAsync</c> overloads (the frozen §A
/// signatures) open their own document session, perform the checks,
/// <c>Store</c> the <see cref="AccessAudit"/> row(s), and commit in one
/// transaction — "no silent, unaudited access". The
/// <c>IDocumentSession</c> overloads are the ADR 0006-E *compatible
/// lane*: they <c>Store</c> the audit row into the caller's in-flight
/// transaction and let the caller commit, so a command handler's
/// domain write and the access decision against it commit or roll back
/// together.
/// </para>
/// <para>
/// Delegation (Invariant 2 / C2): a delegated actor borrows the owner's
/// standing <em>only for</em> actions inside the grant's scope. An
/// out-of-scope action Denies even though the effective principal
/// *would have been* the owner. The audit row records
/// <see cref="AccessVia.Delegation"/> on the Deny so the acting
/// identity is traceable.
/// </para>
/// </summary>
public sealed class AuthorizationService(IDocumentStore store, IUserInfoService userInfoService)
    : IAuthorizationService
{
    // ── Frozen §A signatures (the public contract) ───────────────────────

    /// <inheritdoc />
    public async Task<Decision> CanAsync(
        string actorId, AccessAction action, IAuditableResource target)
    {
        await using var session = store.OpenSession(new SessionOptions());
        var decision = await DecideAsync(session, actorId, action, target, state: null)
            .ConfigureAwait(false);
        // Standalone path (the default caller contract): commit ourselves.
        await session.SaveChangesAsync().ConfigureAwait(false);
        return decision;
    }

    /// <inheritdoc />
    public async Task<VisibleSet> CanSeeAsync(
        string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates)
    {
        await using var session = store.OpenSession(new SessionOptions());
        var result = await CanSeeInternalAsync(session, actorId, action, candidates)
            .ConfigureAwait(false);
        // Standalone path (the default caller contract): commit ourselves.
        await session.SaveChangesAsync().ConfigureAwait(false);
        return result;
    }

    // ── ADR 0006-E compatible lane (added methods; frozen signatures above are
    //    untouched) ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Decision> CanAsync(
        string actorId, AccessAction action, IAuditableResource target, IDocumentSession session)
        => DecideAsync(session, actorId, action, target, state: null);

    /// <inheritdoc />
    public Task<VisibleSet> CanSeeAsync(
        string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates,
        IDocumentSession session)
        => CanSeeInternalAsync(session, actorId, action, candidates);

    // ── Bulk decision (one group-load, one shared passing pass, one
    //    aggregate audit row plus one row per visible audience-restricted
    //    candidate — invariant C3) ───────────────────────────────────────────

    private async Task<VisibleSet> CanSeeInternalAsync(
        IDocumentSession session,
        string actorId,
        AccessAction action,
        IEnumerable<IAuditableResource> candidates)
    {
        var now = DateTimeOffset.UtcNow;

        // D4 — one group-load per AuthorizationModule call; C4 — strong
        // consistency (the live membership rows, not a projection).
        var state = await ResolveActorAsync(actorId, action).ConfigureAwait(false);
        var hasBreakGlass = await HasBreakGlassAsync(session, actorId).ConfigureAwait(false);

        // Moderation branch data — the actor's components they moderate.
        // Loaded once per call (D4 spirit; per-candidate load would hit
        // the same row N times for a homogeneous list — at M1's admin-facing
        // scale the load is cheap and the pre-load keeps the path fast.)
        var moderatingAssignments = await session
            .Query<ModeratorAssignment>()
            .Where(a => a.UserId == actorId || a.UserId == state.EffectivePrincipalId)
            .ToListAsync()
            .ConfigureAwait(false);
        var moderationComponentsOn = new HashSet<string>();
        foreach (var a in moderatingAssignments)
        {
            var comp = await session.LoadAsync<Component>(a.ComponentId).ConfigureAwait(false);
            if (comp is not null && comp.ModeratorAccess)
                moderationComponentsOn.Add(comp.Id);
        }

        var visible = new List<(string Id, AccessVia Via)>();
        // Per-candidate audit payloads for the audience-restricted items
        // (one row each, Allow or Deny — invariant C3's "Allow and Deny"
        // wording for restricted content; public items are covered by the
        // aggregate row only).
        var perItemRows = new List<(IAuditableResource Resource, Decision Decision)>();
        var hiddenCount = 0;

        foreach (var candidate in candidates)
        {
            var decision = Decide(
                candidate,
                actorId,
                state.EffectivePrincipalId, state.GroupIds,
                state.IsDelegated,
                hasBreakGlass,
                moderationComponentsOn);

            if (decision.Allowed)
                visible.Add((candidate.Id, decision.Via));
            else
                hiddenCount++;

            if (candidate.Audience is not null)
                perItemRows.Add((candidate, decision));
        }

        var firstTargetKind = candidates.FirstOrDefault()?.TargetKind ?? string.Empty;
        var aggregateVia = visible.Count > 0
            ? visible[0].Via
            : (state.IsDelegated ? AccessVia.Delegation : AccessVia.Audience);

        // Aggregate audit row (C3 — one per bulk decision, visibleCount/
        // hiddenCount instead of TargetId).
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = state.EffectivePrincipalId,
            Action = action.Id,
            TargetKind = firstTargetKind,
            TargetId = null,
            VisibleCount = visible.Count,
            HiddenCount = hiddenCount,
            Via = aggregateVia,
            Outcome = visible.Count > 0 ? AccessOutcome.Allow : AccessOutcome.Deny
        });

        // One row per audience-restricted candidate — its own Via, its own
        // outcome (Allow or Deny). Public candidates are not individually
        // audited (the aggregate row already records them; a public row adds
        // no "who / via" information).
        foreach (var (resource, decision) in perItemRows)
        {
            session.Store(new AccessAudit
            {
                Id = Guid.NewGuid().ToString("N"),
                At = now,
                ActorId = actorId,
                EffectivePrincipalId = decision.EffectivePrincipalId,
                Action = action.Id,
                TargetKind = resource.TargetKind,
                TargetId = resource.Id,
                VisibleCount = null,
                HiddenCount = null,
                Via = decision.Via,
                Outcome = decision.Allowed ? AccessOutcome.Allow : AccessOutcome.Deny
            });
        }

        // Commit ownership is the caller's responsibility:
        // — standalone public overload commits (after calling this helper),
        // — ADR 0006-E compatible lane (IDocumentSession overload): the
        //   caller is the one committing (their transaction already in
        //   flight — the decision's audit row lands with the domain write).
        return new VisibleSet([.. visible], hiddenCount);
    }

    // ── Single-target decision (one candidate, one audit row) ──────────────

    private async Task<Decision> DecideAsync(
        IDocumentSession session,
        string actorId,
        AccessAction action,
        IAuditableResource target,
        ActorContext? state)
    {
        var now = DateTimeOffset.UtcNow;
        state ??= await ResolveActorAsync(actorId, action).ConfigureAwait(false);

        var hasBreakGlass = await HasBreakGlassAsync(session, actorId).ConfigureAwait(false);

        // Moderation branch data (C5 — OFF by default, ON only if both
        // Component.ModeratorAccess = true and the actor holds a
        // ModeratorAssignment for the component).
        var moderationComponentsOn = new HashSet<string>();
        if (target.ComponentId is not null)
        {
            var comps = await session.Query<ModeratorAssignment>()
                .Where(a => a.UserId == actorId || a.UserId == state.EffectivePrincipalId)
                .Where(a => a.ComponentId == target.ComponentId)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var c in comps)
            {
                var comp = await session.LoadAsync<Component>(c.ComponentId).ConfigureAwait(false);
                if (comp is not null && comp.ModeratorAccess)
                    moderationComponentsOn.Add(comp.Id);
            }
        }

        var decision = Decide(
            target,
            actorId,
            state.EffectivePrincipalId, state.GroupIds,
            state.IsDelegated,
            hasBreakGlass,
            moderationComponentsOn);

        // Always-on audit row (C3 — Allow and Deny, the row is part of the
        // caller's transaction or the standalone commit).
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = decision.EffectivePrincipalId,
            Action = action.Id,
            TargetKind = target.TargetKind,
            TargetId = target.Id,
            Via = decision.Via,
            Outcome = decision.Allowed ? AccessOutcome.Allow : AccessOutcome.Deny
        });

        return decision;
    }

    // ── Decide — pure, shared between both call surfaces (Invariant 6 / C6) ─

    private static Decision Decide(
        IAuditableResource target,
        string actorId,
        string effectivePrincipalId,
        IReadOnlySet<string> groupIds,
        bool isDelegated,
        bool hasBreakGlass,
        IReadOnlySet<string> moderationComponentsOn)
    {
        var denyVia = isDelegated ? AccessVia.Delegation : AccessVia.Audience;

        // 1. Owner branch — effective principal covers the owner in the
        //    delegation case (the delegate borrows the owner's standing
        //    for in-scope actions — invariant C2).
        if (target.OwnerId is not null && target.OwnerId == effectivePrincipalId)
        {
            var via = (isDelegated && target.OwnerId != actorId)
                ? AccessVia.Delegation
                : AccessVia.Owner;
            return new Decision(true, via, effectivePrincipalId);
        }

        // 2. Moderation branch — component must be in the pre-load (C5).
        if (target.ComponentId is not null && moderationComponentsOn.Contains(target.ComponentId))
            return new Decision(true, AccessVia.Moderator, effectivePrincipalId);

        // 3. Break-glass — actor-level (the consumed AdminOverride row).
        if (hasBreakGlass)
            return new Decision(true, AccessVia.BreakGlass, actorId);

        // 4. Public resource (Audience null — not audience-restricted).
        if (target.Audience is null)
        {
            var via = isDelegated ? AccessVia.Delegation : denyVia;
            return new Decision(true, via, effectivePrincipalId);
        }

        // 5. MatchGroups (C6 shared single pass — pure).
        if (EvaluateAudience(target.Audience, effectivePrincipalId, groupIds))
        {
            var via = isDelegated ? AccessVia.Delegation : denyVia;
            return new Decision(true, via, effectivePrincipalId);
        }

        // 6. Deny — no branch matched.
        return new Decision(false, denyVia, actorId);
    }

    // ── Public test seam — the shared audience matcher (Invariant 6 / C6).
    //
    // The design-doc part-test "MatchGroups truth table (Any/All × grant
    // kinds × moderatorAccess flag × delegation scope)" targets this method.
    // Both CanAsync and CanSeeAsync reduce to it — no drift.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluates one candidate's <see cref="Audience"/> against the actor's
    /// effective principal and live group set. <b>Pure:</b> no DB access, no
    /// shared state — the input triple (audience, effective principal, group
    /// set) fully determines the result.
    /// </summary>
    /// <param name="audience">The resource's audience, or <c>null</c> when the
    /// resource is public (not audience-restricted — allow all).</param>
    /// <param name="effectivePrincipalId">The principal the decision runs
    /// under: the actor, or the owner when acting under an in-scope
    /// delegation (invariant C2).</param>
    /// <param name="groupIds">The actor's live group set (C4 — strong
    /// consistency).</param>
    /// <returns><c>true</c> when the actor's standing covers at least one
    /// (in <c>Any</c> mode) or all (in <c>All</c> mode) of the audience
    /// grants; <c>false</c> when the audience is empty (invariant 1 — the
    /// vacuous-truth guard), or when no grant matches.</returns>
    public static bool EvaluateAudience(
        Audience? audience, string effectivePrincipalId, IReadOnlySet<string> groupIds)
    {
        // Public resource — not audience-restricted; allow.
        if (audience is null)
            return true;

        // Invariant 1 — explicit guard. In mode `All`, `Grants.All(...)`
        // over an empty list is vacuously true and would make an empty
        // `All` resource world-readable. An empty audience always denies,
        // in either mode.
        if (audience.IsEmpty)
            return false;

        return audience.Mode switch
        {
            AudienceMode.Any => audience.Grants.Any(g => GrantMatches(g, effectivePrincipalId, groupIds)),
            AudienceMode.All => audience.Grants.All(g => GrantMatches(g, effectivePrincipalId, groupIds)),
            _ => false
        };
    }

    private static bool GrantMatches(
        AudienceGrant grant, string effectivePrincipalId, IReadOnlySet<string> groupIds)
        => grant.Kind switch
        {
            GrantKind.User  => grant.Id == effectivePrincipalId,
            GrantKind.Group => groupIds.Contains(grant.Id),
            _ => false
        };

    // ── Actor + delegation + break-glass resolution ────────────────────────

    private async Task<ActorContext> ResolveActorAsync(string actorId, AccessAction action)
    {
        // D4 — one group-load per AuthorizationModule call.
        // C4 — strong consistency (live membership rows, no projection lag).
        var groupIds = await userInfoService.GetGroupIdsAsync(actorId).ConfigureAwait(false);

        var grant = await userInfoService.GetActiveGrantAsync(actorId).ConfigureAwait(false);
        if (grant is null)
            return new ActorContext(actorId, groupIds, IsDelegated: false);

        if (grant.Scope.Contains(action.Id))
        {
            // Action in scope — the delegate borrows the owner's standing.
            return new ActorContext(grant.OwnerId, groupIds, IsDelegated: true);
        }

        // Action out of scope — the delegate acts as self; isDelegated stays
        // true so the Deny row carries Via = Delegation (invariant C2, the
        // "acting identity" requirement).
        return new ActorContext(actorId, groupIds, IsDelegated: true);
    }

    private async Task<bool> HasBreakGlassAsync(IDocumentSession session, string userId)
    {
        // AdminOverride is hand-rolled (AuthorizationFeature / ADR 0004 §B.1)
        // — operator-written via psql; the app only reads it. It is NOT a
        // Marten document, so the read is a parameterised SQL query on the
        // <em>same document session</em> — this keeps the read inside the
        // caller's transaction when one is in flight, and the ADO.NET
        // command executes against the same physical connection the session
        // holds (Marten 9 IQuerySession surfaces a Connection property for
        // exactly this raw-SQL seam — see M1Feature's raw-SQL reads in DDL
        // tests for the same pattern).
        var conn = ((Npgsql.NpgsqlConnection)session.Connection!);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT EXISTS (" +
            "  SELECT 1 FROM \"mt\".\"AdminOverride\" " +
            "  WHERE \"userId\" = @userId " +
            "    AND \"consumedAt\" IS NOT NULL " +
            "    AND \"expiresAt\" > @now" +
            ")";

        var pUser = cmd.CreateParameter();
        pUser.ParameterName = "@userId";
        pUser.Value = userId;
        cmd.Parameters.Add(pUser);

        var pNow = cmd.CreateParameter();
        pNow.ParameterName = "@now";
        pNow.Value = DateTimeOffset.UtcNow;
        cmd.Parameters.Add(pNow);

        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return result is bool b && b;
    }

    // ── Actor context (the shared state the decision consumes) ─────────────

    private sealed record ActorContext(
        string EffectivePrincipalId,
        IReadOnlySet<string> GroupIds,
        bool IsDelegated);
}
