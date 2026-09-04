namespace Kumunita.Core.Authorization;

/// <summary>
/// An always-on audit row (invariant C3). Appended for every decision on
/// audience-restricted content — Allow *and* Deny — in the *same transaction* as the
/// domain write, so access can never commit un-audited.
/// <para>
/// Rows come in two shapes (both stored together): a single-target decision carries
/// <see cref="TargetId"/> (and counts stay null); an aggregate row from a bulk decision
/// (`targetKind` "component"/"directory" or a component/board id) carries
/// <see cref="VisibleCount"/>/<see cref="HiddenCount"/> instead (targetId null).
/// </para>
/// <para>
/// Purged only by the <see cref="AuditPurgeJob"/> writer, which appends an
/// <see cref="AuditPurgeSummary"/> row (the only writer that deletes; §5).
/// </para>
/// </summary>
public sealed class AccessAudit
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    /// <summary>The acting account (not the effective principal — see <see cref="EffectivePrincipalId"/>)
    /// for break-glass-attached actions.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>The effective principal (owner when acting under a delegation, otherwise the actor).</summary>
    public string? EffectivePrincipalId { get; set; }

    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// The kind of resource that is the decision's focus (e.g. component, post, directory,
    /// or the aggregate "list" kind).
    /// </summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>For single-target rows. Null for aggregate rows (bulk list views).</summary>
    public string? TargetId { get; set; }

    /// <summary>For aggregate rows only (bulk list decisions).</summary>
    public int? VisibleCount { get; set; }

    /// <summary>For aggregate rows only (bulk list decisions).</summary>
    public int? HiddenCount { get; set; }

    public AccessVia Via { get; set; }

    public AccessOutcome Outcome { get; set; }
}

/// <summary>
/// A summary row written only by the scheduled purge job: count, cutoff, at. It is the
/// audit-of-audit for the deletion — the only deletion writer is the purge job (§5).
/// </summary>
public sealed class AuditPurgeSummary
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    /// <summary>Rows deleted by this purge.</summary>
    public long Count { get; set; }

    /// <summary>The cutoff applied (tiered expiry — §5).</summary>
    public DateTimeOffset Cutoff { get; set; }
}

/// <summary>
/// Scheduled-purge writer (Wolverine, §6.4): expires <see cref="AccessAudit"/> rows by
/// tier — routine restricted-content Allow/Deny after ~90 days; report- and
/// moderator/admin-access rows kept until the report resolves (+90 days). Cutoff is
/// per-instance config, not improvised (§5).
/// </summary>
public sealed record AuditPurgeJob(
    DateTimeOffset Cutoff,
    int RoutineDays,
    int UnresolvedReportDays);
