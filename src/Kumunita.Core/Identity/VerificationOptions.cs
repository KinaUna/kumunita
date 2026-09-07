namespace Kumunita.Core.Identity;

/// <summary>
/// Signup verification token policy (the design's "World seams" — the single
/// verification email is the one outward-facing handoff). Bound from
/// <c>appsettings.Development.json</c> (dev) / <c>Verification__*</c> env (OPS §2).
/// <para>
/// <see cref="TtlDays"/> — the per-attempt token's lifetime from mint. A re-verify
/// (a new attempt, a new token) is a *new* window, not an extension (§6.2: a new
/// attempt is a new row, a new idempotency key — the old token stays put, still
/// consumable until its own expiry).
/// </para>
/// <para>
/// <see cref="MaxVerifyAttempts"/> — per-account bound. A resident who
/// dead-letters three attempts gets no more verify links; the admin manual-verify
/// valve (the unverified-signup pile-up signal, OPS §7) is the path forward.
/// This is a *seam guard* for the dead-letter path (the durable email handler's
/// failure / retry / dead-letter harness in M1 step 7 is the real signal); it does
/// not prevent the valve from working, and it does *not* prevent a resident from
/// re-signing-up with a different email (that's a new account, a new idempotency
/// namespace).
/// </para>
/// </summary>
public sealed class VerificationOptions
{
    public const string SectionName = "Verification";

    public int TtlDays { get; set; } = 14;

    public int MaxVerifyAttempts { get; set; } = 3;

    /// <summary>
    /// The public base URL of this platform instance (no trailing slash, e.g.
    /// <c>https://maplewood.kumunita.example</c>), used to build the absolute
    /// verification link in the confirmation email — an email reader cannot
    /// resolve a relative <c>/account/verify</c> path on its own. Empty/unset =
    /// the email carries the relative path (dev convenience only — the dev
    /// shapes set it in <c>appsettings.Development.json</c> /
    /// <c>docker-compose.yml</c>; production sets it to the instance's public
    /// domain per <c>docs/COOLIFY.md</c> §5/§5.3).
    /// </summary>
    public string? BaseUrl { get; set; }
}
