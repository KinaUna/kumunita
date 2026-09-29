namespace Kumunita.Core.Usage;

/// <summary>
/// One captured usage row (M13, ADR 0114 D1). Carries <b>exactly</b>
/// <see cref="Id"/> / <see cref="At"/> / <see cref="ActorId"/> /
/// <see cref="RouteTemplate"/> — no email, no profile field, no request
/// body, no user-agent, no IP, no status code (C-M13·2 — the status code
/// would leak the access decision, which the <c>AccessAudit</c> lane
/// already owns). <see cref="ActorId"/> is the
/// <c>ClaimTypes.Subject</c> value, <see cref="string.Empty"/> when
/// anonymous. <see cref="RouteTemplate"/> is the <b>route template</b>
/// (e.g. <c>GET /posts/{id}</c>), never a concrete path (C-M13·4).
/// Purged only by the <see cref="UsagePurgeService"/> writer (D5,
/// the 365-day platform constant).
/// </summary>
public sealed class UsageEvent
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The capture instant, UTC (the middleware's request instant).</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>The acting account's <c>ClaimTypes.Subject</c> value; <see cref="string.Empty"/> when anonymous.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>The <b>route template</b> (e.g. <c>GET /posts/{id}</c>), never a concrete path (C-M13·4).</summary>
    public string RouteTemplate { get; set; } = string.Empty;
}
