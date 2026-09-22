namespace Kumunita.Core.Identity;

/// <summary>
/// <c>/health</c> diagnostic payload gate (M3). Bound from
/// <c>Health__Token</c> env / <c>appsettings</c>.
/// <para>
/// When <see cref="Token"/> is set (non-empty), the full diagnostic payload
/// (SMTP relay detail, dead-letter count, build SHA) requires either the
/// <c>X-Health-Token</c> header to match this token, or an authenticated
/// GlobalAdmin session. Without a token, the full payload is always visible
/// (back-compat for existing deployments that don't set it).
/// </para>
/// <para>
/// The minimal liveness probe (<c>status</c>, <c>database</c>, <c>app</c>,
/// <c>elapsedMs</c>) is <em>always</em> available to anonymous callers —
/// the Coolify / edge-proxy health check relies on it.
/// </para>
/// </summary>
public sealed class HealthOptions
{
    public const string SectionName = "Health";

    /// <summary>
    /// Shared-secret for the full <c>/health</c> diagnostic payload.
    /// When set, the full payload requires <c>X-Health-Token</c> header match
    /// or a GlobalAdmin session. Empty/null = full payload always visible.
    /// </summary>
    public string? Token { get; set; }
}
