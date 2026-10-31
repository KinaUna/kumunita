namespace Kumunita.Core.Identity;

/// <summary>
/// <c>/health</c> diagnostic payload gate (M3). Bound from
/// <c>Health__Token</c> env / <c>appsettings</c>.
/// <para>
/// The full diagnostic payload (SMTP relay detail, dead-letter count, build
/// SHA) is <em>never</em> visible to anonymous callers by default. It requires
/// either the <c>X-Health-Token</c> header to match <see cref="Token"/> (when
/// <see cref="Token"/> is set), or an authenticated GlobalAdmin session. When
/// <see cref="Token"/> is unset, only the GlobalAdmin route grants the full
/// payload — anonymous callers always get the minimal liveness probe.
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
    /// When set, the full payload requires an <c>X-Health-Token</c> header
    /// match (or a GlobalAdmin session). Empty/null = the full payload is
    /// reachable only via an authenticated GlobalAdmin session; anonymous
    /// callers always get the minimal liveness probe.
    /// </summary>
    public string? Token { get; set; }
}
