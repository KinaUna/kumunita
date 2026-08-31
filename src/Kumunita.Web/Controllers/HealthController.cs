using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Marten;

namespace Kumunita.Web.Controllers;

/// <summary>
/// Liveness probe (OPS.md §8). At M0 this reports the app is up and the live Postgres is
/// reachable; the "degraded when <c>mt.email_dead_letters</c> is non-empty" half lands in M1+
/// once the durable email outbox exists.
/// </summary>
[Route("health")]
public sealed class HealthController : Controller
{
    private readonly IDocumentStore _store;

    public HealthController(IDocumentStore store)
    {
        _store = store;
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

        sw.Stop();

        return Ok(new
        {
            status = "ok",
            app = "Kumunita",
            build = Environment.GetEnvironmentVariable("SOURCE_COMMIT") ?? "local",
            database = "ok",
            elapsedMs = sw.ElapsedMilliseconds
        });
    }
}
