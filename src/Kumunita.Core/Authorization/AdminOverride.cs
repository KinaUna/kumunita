namespace Kumunita.Core.Authorization;

/// <summary>
/// Break-glass elevation (§4.5, ADR 0003).
/// <para>
/// Written ONLY by the host operator directly into Postgres (psql, OPS §9) —
/// never by any in-app endpoint. The target account consumes the token once to become
/// GlobalAdmin until <see cref="ExpiresAt"/>; consumption + elevation are audited
/// (via: BreakGlass).
/// </para>
/// </summary>
/// <remarks>
/// The `mt` schema needs a non-null indexed read on `(actorId, consumedAt)` for the
/// hot break-glass check at every authorization decision (rare document on a hot path
/// — the cost of NOT checking inline (a job's lag window) is worse than the index).
/// </remarks>
public sealed class AdminOverride
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The account being elevated.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Strong one-time token; consumed and invalidated on first use.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>Elevation lapses at this instant (checked inline at decision time).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set when the account first consumes the token; the row stays put.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    // Weasel: the index is defined on the `mt` schema via the companion
    // versioned storage feature (see <c>Kumunita.AuthorizationFeature</c>).

    /// <summary>
    /// Valid for break-glass purposes if consumed, not expired, and the token is the
    /// target's current one-shot (single-use — consumption is the point of this shape).
    /// </summary>
    public bool IsUsableAt(DateTimeOffset now) =>
        ConsumedAt is not null && now < ExpiresAt;
}
