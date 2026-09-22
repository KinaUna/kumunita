using System.Diagnostics;
using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// Liveness probe (OPS.md §8). Reports the app is up, the live Postgres is
/// reachable, the configured SMTP relay answers a handshake, and the durable
/// email outbox is not backing up (a non-empty <c>EmailDeadLetter</c> set drives
/// the "degraded" status per OPS §8 and ARCHITECTURE.md §5/§6.2).
/// </summary>
[Route("health")]
public sealed class HealthController : Controller
{
    private readonly IDocumentStore _store;
    private readonly IEmailDeadLetterCounter _deadLetters;
    private readonly ISmtpHealthCheck _smtpHealth;
    private readonly HealthOptions _healthOptions;

    public HealthController(
        IDocumentStore store,
        IEmailDeadLetterCounter deadLetters,
        ISmtpHealthCheck smtpHealth,
        IOptions<HealthOptions> healthOptions)
    {
        _store = store;
        _deadLetters = deadLetters;
        _smtpHealth = smtpHealth;
        _healthOptions = healthOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // AssertConnectivityAsync opens a connection to the default database and runs a
            // trivial query; it throws if the database is unreachable.
            await _store.Storage.Database.AssertConnectivityAsync(ct);
        }
        catch (Exception)
        {
            // M3 — the 503 liveness response is the minimal probe shape (status,
            // database, app, elapsedMs) — no mail / build / dead-letter detail,
            // even for an operator who would otherwise be allowed the full payload.
            // (The DB is down, so the detailed diagnostic would be incomplete anyway.)
            sw.Stop();
            return StatusCode(503, new
            {
                status = "degraded",
                database = "unreachable",
                app = "Kumunita",
                elapsedMs = sw.ElapsedMilliseconds
            });
        }

        // M3 — decide once, up front, whether the caller is allowed the full
        // detailed payload. When Health__Token is unset, the full payload is
        // always visible (back-compat for existing deployments). When set, the
        // caller must present the matching X-Health-Token header OR be an
        // authenticated GlobalAdmin.
        bool tokenSet = !string.IsNullOrWhiteSpace(_healthOptions.Token);
        // Request may be null when the controller is constructed directly in a
        // unit test (no ControllerContext / HttpContext); guard both accesses.
        bool presentedTokenMatches = false;
        if (tokenSet && Request is not null)
        {
            presentedTokenMatches = Request.Headers.TryGetValue("X-Health-Token", out var presented)
                && string.Equals(presented.ToString(), _healthOptions.Token, StringComparison.Ordinal);
        }
        // ControllerContext may be null in a unit test (no HttpContext); guard.
        var principal = ControllerContext?.HttpContext?.User;
        bool isGlobalAdmin = principal?.Identity?.IsAuthenticated == true
            && principal.Claims.Any(c => c.Type == Kumunita.Core.Identity.ClaimTypes.Role
                                         && c.Value == Kumunita.Core.Identity.Roles.GlobalAdmin);
        bool fullPayloadAllowed = !tokenSet || presentedTokenMatches || isGlobalAdmin;

        if (!fullPayloadAllowed)
        {
            // M3 — minimal liveness probe: the Coolify / edge-proxy health check
            // relies on this shape. We already confirmed the DB is reachable
            // (passed the catch block above), so the liveness signal is "ok".
            // No mail state, no build SHA, no dead-letter count — none of which
            // should be visible to an anonymous caller.
            sw.Stop();
            return Ok(new
            {
                status = "ok",
                database = "ok",
                app = "Kumunita",
                elapsedMs = sw.ElapsedMilliseconds
            });
        }

        // Count dead-lettered email rows (OPS §8): a non-empty set means the durable
        // email outbox is backing up and the operator needs to intervene (OPS §7),
        // so surface "degraded" alongside the live count.
        var deadLetterCount = await _deadLetters.GetCountAsync(ct).ConfigureAwait(false);

        // Probe the SMTP relay with a minimal handshake (OPS §8): the app stays
        // live (200) when the relay is down — email is still durable via the
        // OutboxEmail retry / dead-letter path — but the operator gets the
        // "degraded" signal plus a "mail" field showing the relay is unreachable.
        var mail = await _smtpHealth.CheckAsync(ct).ConfigureAwait(false);

        sw.Stop();

        return Ok(new
        {
            status = (deadLetterCount > 0 || !mail.Reachable) ? "degraded" : "ok",
            app = "Kumunita",
            build = Environment.GetEnvironmentVariable("SOURCE_COMMIT") ?? "local",
            database = "ok",
            mail = mail.Reachable ? "ok" : "unreachable",
            // Operator diagnostic (OPS §7): the exact step the handshake failed at
            // (connect/DNS, banner, EHLO, AUTH, timeout) plus the relay's own
            // reply — null when mail is healthy.
            mailDetail = mail.Reachable ? null : mail.Reason,
            emailDeadLetters = deadLetterCount,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }
}
