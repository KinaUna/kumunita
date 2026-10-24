using Kumunita.Core.Notifications;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace Kumunita.Web.Controllers;

/// <summary>
/// F17 / MAIL-03 — the provider-callback **ingest** route (pin:
/// <c>docs/design/prod-hardening-f17-bounce.md</c> §3.1/§3.3).
/// <para>
/// <c>POST /api/mail/suppress</c> — the mail provider's bounce/complaint
/// webhook posts the JSON envelope <c>{ email, reason: "bounce"|"complaint",
/// source: &lt;provider&gt; }</c>. The controller validates the body, checks the
/// shared-secret <c>X-Suppression-Token</c> header against the
/// <c>MailSuppression:IngestToken</c> config value, then delegates to the
/// Core <see cref="ISuppressionService.SuppressAsync"/> write lane (pin §3.2)
/// and returns <c>204</c>.
/// </para>
/// <para>
/// **Web-only boundary (ADR 0006-D, pin §5·1):** the <c>IActionResult</c> /
/// <c>Request</c> / <c>IConfiguration</c> live **here**, in
/// <c>Kumunita.Web</c> — never in Core. <see cref="ISuppressionService"/> /
/// <see cref="SuppressionService"/> carry no HTTP types.
/// </para>
/// <para>
/// **Shared-secret auth (pin §3.1):** the <c>X-Suppression-Token</c> header is
/// a per-instance shared secret (the <c>EscalationToken</c> / <c>GuestAccess</c>
/// token-seam precedent — a server-to-server secret, not an ASP.NET Identity
/// credential). Config key <c>MailSuppression:IngestToken</c> (the
/// <c>MailSuppression__IngestToken</c> appsettings / env form). A missing or
/// mismatched token is <c>403</c> (fail-closed — never a permissive default:
/// an unconfigured instance has no token set, so every request is refused).
/// </para>
/// <para>
/// **No CORS** (server-to-server, not a browser call); **no rate limit** (the
/// provider webhook is not a user-facing lane — the F8 <c>upload</c> policy
/// does not apply, pin §3.1). **No auth attribute** (the shared-secret header
/// **is** the auth; <c>[Authorize]</c> would require an Identity principal).
/// </para>
/// </summary>
public sealed class MailSuppressionController : Controller
{
    private readonly ISuppressionService _suppression;
    private readonly IConfiguration _config;

    public MailSuppressionController(ISuppressionService suppression, IConfiguration config)
    {
        _suppression = suppression ?? throw new ArgumentNullException(nameof(suppression));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// <c>POST /api/mail/suppress</c> (pin §3.1/§3.3) — the provider's
    /// bounce/complaint ingest.
    /// <para>
    /// Ordering (pin §3.3): (1) validate the body (<c>email</c> non-empty,
    /// <c>reason</c> ∈ {<c>"bounce"</c>, <c>"complaint"</c>}, <c>source</c>
    /// non-empty) → <c>400</c>; (2) check the <c>X-Suppression-Token</c> header
    /// against <c>MailSuppression:IngestToken</c> → <c>403</c> on mismatch (or
    /// a missing/absent configured token); (3)
    /// <see cref="ISuppressionService.SuppressAsync"/> (one
    /// <c>SaveChangesAsync</c>, invariant C3); (4) <c>204</c>.
    /// </para>
    /// </summary>
    [HttpPost("api/mail/suppress")]
    public async Task<IActionResult> Suppress([FromBody] SuppressionIngestRequest? req, CancellationToken ct)
    {
        // (1) Validate the body (pin §3.3 step 1) — before the auth check, so a
        //     malformed probe is a 400 (shape) not a 403 (secret).
        if (req is null
            || string.IsNullOrWhiteSpace(req.Email)
            || (req.Reason != "bounce" && req.Reason != "complaint")
            || string.IsNullOrWhiteSpace(req.Source))
            return BadRequest();

        // (2) Shared-secret auth (pin §3.1) — the X-Suppression-Token header
        //     must equal the configured MailSuppression:IngestToken value.
        //     Fail-closed: an unconfigured instance (no token bound) has an
        //     empty expected value, so a non-empty presented token never
        //     matches → 403 (never a permissive default that would accept a
        //     blank header). Constant-time comparison (CryptographicOperations)
        //     so a timing oracle cannot recover the secret.
        var expected = _config["MailSuppression:IngestToken"] ?? string.Empty;
        var presented = (Request?.Headers["X-Suppression-Token"].FirstOrDefault())?.Trim() ?? string.Empty;
        if (expected.Length == 0
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(expected),
                System.Text.Encoding.UTF8.GetBytes(presented)))
            return StatusCode(StatusCodes.Status403Forbidden);

        // (3) The Core write lane (pin §3.2) — one SaveChangesAsync (C3).
        await _suppression.SuppressAsync(req.Email, req.Reason, req.Source, ct);

        // (4) 204 No Content (pin §3.1 — the SuppressedAddress row is stored).
        return NoContent();
    }

    /// <summary>
    /// The provider envelope (pin §3.1) — mapped to the
    /// <see cref="SuppressionService.SuppressAsync"/> arguments. <c>public</c>
    /// (a public action's parameter type must not be less accessible than the
    /// action — CS0051).
    /// </summary>
    public sealed class SuppressionIngestRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;   // "bounce" | "complaint"
        public string Source { get; set; } = string.Empty;   // the provider name
    }
}
