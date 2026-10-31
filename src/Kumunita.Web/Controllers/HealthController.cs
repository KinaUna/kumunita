using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// Liveness probe (OPS.md §8). Reports the app is up, the live Postgres is
/// reachable, the configured SMTP relay answers a handshake, and the durable
/// email outbox is not backing up (a non-empty <c>EmailDeadLetter</c> set drives
/// the "degraded" status per OPS §8 and ARCHITECTURE.md §5/§6.2).
/// <para>
/// M4 (SEC U16): the full diagnostic payload (mail detail, dead-letter count,
/// build SHA) is visible **only** to a caller presenting the <c>X-Health-Token</c>
/// header matching <c>Health__Token</c>, or an authenticated GlobalAdmin.
/// **When the token is unset, anonymous callers always get the minimal liveness
/// probe** (status/database/app/elapsedMs) — the Coolify / edge-proxy health
/// check relies on that shape. The token compare is constant-time
/// (<see cref="CryptographicOperations.FixedTimeEquals"/>); the SMTP relay
/// handshake is cached ~60 s so anonymous probing does not do a handshake per
/// request.
/// </para>
/// </summary>
[Route("health")]
public sealed class HealthController : Controller
{
    /// <summary>
    /// M4 (SEC U16) — the SMTP relay handshake is expensive (connect + banner +
    /// EHLO + AUTH per call), so its result is cached for a short window.
    /// Transient controllers do not share instance state across requests, hence
    /// the static holder (one process = one relay). A single lock guards the
    /// read-and-refresh so concurrent probes do not stampede the relay.
    /// </summary>
    private static readonly object _smtpLock = new();
    private static readonly TimeSpan SmtpCacheTtl = TimeSpan.FromSeconds(60);
    private static DateTime _smtpCachedAtUtc;
    private static SmtpHealthResult? _smtpCachedResult;

    /// <summary>
    /// Test seam (M4 / SEC U16) — clear the SMTP-probe cache so a test starts
    /// from a cold cache. Production code never calls this; it exists so the
    /// unit tests (which run against different <see cref="ISmtpHealthCheck"/>
    /// stubs in the same process) do not observe one stub's cached result in
    /// another test.
    /// </summary>
    public static void ResetSmtpCacheForTesting()
    {
        lock (_smtpLock)
        {
            _smtpCachedResult = null;
            _smtpCachedAtUtc = default;
        }
    }
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

        // M4 (SEC U16, veto 4) — decide once, up front, whether the caller is
        // allowed the full detailed payload. The full payload requires the
        // matching X-Health-Token header (constant-time compare) OR an
        // authenticated GlobalAdmin. **When Health__Token is unset, neither
        // token nor role alone grants the full payload to an anonymous caller**
        // (the audit's default was public-by-default; M4 closes it) — only an
        // authenticated GlobalAdmin sees the detail, and only a matching token
        // does when the token is set.
        bool presentedTokenMatches = IsHealthTokenPresented();
        // ControllerContext may be null in a unit test (no HttpContext); guard.
        var principal = ControllerContext?.HttpContext?.User;
        bool isGlobalAdmin = principal?.Identity?.IsAuthenticated == true
            && principal.Claims.Any(c => c.Type == Kumunita.Core.Identity.ClaimTypes.Role
                                         && c.Value == Kumunita.Core.Identity.Roles.GlobalAdmin);
        bool fullPayloadAllowed = presentedTokenMatches || isGlobalAdmin;

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

        // M4 (SEC U16) — probe the SMTP relay with a minimal handshake (OPS §8),
        // cached ~60 s (the handshake is expensive and the full payload is
        // operator-only now, so repeated operator/monitor probes must not each
        // do a connect+banner+EHLO+AUTH). The app stays live (200) when the
        // relay is down — email is still durable via the OutboxEmail retry /
        // dead-letter path — but the operator gets the "degraded" signal plus a
        // "mail" field showing the relay is unreachable.
        var mail = await GetCachedSmtpResultAsync(ct).ConfigureAwait(false);

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

    /// <summary>
    /// M4 (SEC U16) — whether the caller presented the <c>Health__Token</c> as
    /// the <c>X-Health-Token</c> header, compared **constant-time** on UTF-8
    /// bytes via <see cref="CryptographicOperations.FixedTimeEquals"/>. Length is
    /// handled first (a differing length is a mismatch, returned before the
    /// fixed-time compare — the lengths themselves are configuration-derived,
    /// not an oracle). No token configured, or no header, is a mismatch; the
    /// caller then relies on the GlobalAdmin path.
    /// </summary>
    private bool IsHealthTokenPresented()
    {
        if (string.IsNullOrWhiteSpace(_healthOptions.Token) || Request is null)
        {
            return false;
        }
        if (!Request.Headers.TryGetValue("X-Health-Token", out var presented))
        {
            return false;
        }
        var expected = Encoding.UTF8.GetBytes(_healthOptions.Token);
        var actual = Encoding.UTF8.GetBytes(presented.ToString());
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>
    /// M4 (SEC U16) — the SMTP relay result, cached for ~60 s (see
    /// <see cref="_smtpCachedAtUtc"/>). A fresh probe refreshes the entry; a
    /// cached (fresh) entry is returned without touching the relay.
    /// </summary>
    private async Task<SmtpHealthResult> GetCachedSmtpResultAsync(CancellationToken ct)
    {
        lock (_smtpLock)
        {
            if (_smtpCachedResult is { } fresh && DateTime.UtcNow - _smtpCachedAtUtc < SmtpCacheTtl)
            {
                return fresh;
            }
        }

        var result = await _smtpHealth.CheckAsync(ct).ConfigureAwait(false);
        lock (_smtpLock)
        {
            _smtpCachedResult = result;
            _smtpCachedAtUtc = DateTime.UtcNow;
        }
        return result;
    }
}
