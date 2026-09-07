using System.Diagnostics;
using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Mvc;
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

    public HealthController(IDocumentStore store, IEmailDeadLetterCounter deadLetters, ISmtpHealthCheck smtpHealth)
    {
        _store = store;
        _deadLetters = deadLetters;
        _smtpHealth = smtpHealth;
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
            return StatusCode(503, new { status = "degraded", database = "unreachable" });
        }

        // Count dead-lettered email rows (OPS §8): a non-empty set means the durable
        // email outbox is backing up and the operator needs to intervene (OPS §7),
        // so surface "degraded" alongside the live count.
        var deadLetterCount = await _deadLetters.GetCountAsync(ct).ConfigureAwait(false);

        // Probe the SMTP relay with a minimal handshake (OPS §8): the app stays
        // live (200) when the relay is down — email is still durable via the
        // OutboxEmail retry / dead-letter path — but the operator gets the
        // "degraded" signal plus a "mail" field showing the relay is unreachable.
        var mailReachable = await _smtpHealth.IsReachableAsync(ct).ConfigureAwait(false);

        sw.Stop();

        return Ok(new
        {
            status = (deadLetterCount > 0 || !mailReachable) ? "degraded" : "ok",
            app = "Kumunita",
            build = Environment.GetEnvironmentVariable("SOURCE_COMMIT") ?? "local",
            database = "ok",
            mail = mailReachable ? "ok" : "unreachable",
            emailDeadLetters = deadLetterCount,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }
}
