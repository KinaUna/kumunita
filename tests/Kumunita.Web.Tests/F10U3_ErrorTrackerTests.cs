using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Sentry.AspNetCore;

namespace Kumunita.Web.Tests;

/// <summary>
/// F10 (OBS-02: error tracker — Sentry) — **U3**, the third and final unit of F10.
/// The pinned test <c>Error_TrackerRegisteredWhenDsnSet</c> (design
/// <c>prod-hardening-f10-errtrack.md</c> §4, §4a leg-1/leg-2/leg-3, §4b harness
/// gotchas).
/// <para>
/// F10.U2 landed the <c>Sentry.AspNetCore 6.12.1</c> package + the
/// <c>UseSentry</c> registration + the <c>SentryPiiScrubber</c>. This unit
/// drives that seam. Two pin-vs-6.12.1 facts (same drift class F10.U2
/// documented — the pin's <i>description of the Sentry SDK API</i> is wrong for
/// the pinned version, NOT a codebase drift) shape the access path, per the pin's
/// own delegation ("the pin fixes the **contract**, not the **access path**";
/// "the exact <c>SentryOptions</c> access idiom is a **F10.U3 judgment call**"):
/// </para>
/// <list type="bullet">
/// <item><b>leg-1/leg-2 target type</b> — the pin names <c>ISentrySdk</c> /
/// <c>ISentryEventSink</c>. Neither exists in <c>Sentry</c> 6.12.1 (verified
/// against the 6.12.1 NUGET assembly XML docs: 0 hits). The real registered
/// SDK interface is <see cref="ISentryClient"/> (the <c>AddSentry()</c> path
/// registers it <c>TryAddTransient&lt;ISentryClient&gt;(c =&gt;
/// c.GetRequiredService&lt;IHub&gt;())</c>). So the resolvable assertion is
/// against <see cref="ISentryClient"/>.</item>
/// <item><b>leg-1/leg-2 must build an <b>application host</b>, not a bare
/// <c>ServiceCollection</c></b> — in 6.12.1 <c>UseSentry(Action&lt;…&gt;)</c>
/// defers its SDK registrations into a <c>ConfigureLogging</c> delegate that
/// only runs at host <c>Build()</c> (not at the <c>UseSentry</c> call). A bare
/// <c>ServiceCollection.BuildServiceProvider()</c> would never materialize
/// <see cref="ISentryClient"/>. So this test faithfully mirrors
/// <c>Program.cs</c> — <c>WebApplication.CreateBuilder()</c> →
/// <c>builder.WebHost.UseSentry(…)</c> → <c>builder.Build()</c> (a
/// <c>WebApplication</c>, which is what <c>Program.cs</c> itself builds;
/// <c>builder.WebHost</c> is the <c>IWebHostBuilder</c> receiver, so this is the
/// same registration, just built to completion). The pin's "a
/// <c>ServiceCollection</c> that registers the one conditional registration" is
/// the <i>concept</i>; the faithful 6.12.1 realization is an app-host build.</item>
/// <item><b>leg-3 (load-bearing)</b> — <see cref="SentryPiiScrubber.Scrub"/>
/// invoked directly on a synthetic <c>SentryEvent</c> (the scrubber is
/// <c>public static</c>, callable from this assembly). Two 6.12.1 facts (a
/// **fourth** pin-vs-SDK drift — the same class F10.U2 documented, extending it
/// to the header surface): (a) <c>SentryRequest.Headers</c> is **getter-only**
/// (backed by an internal redacting dictionary) — headers are populated through
/// the getter's returned dictionary (exactly as the scrubber mutates it), while
/// <c>Cookies</c> / <c>QueryString</c> are settable <c>string?</c> properties;
/// (b) that internal dictionary is <c>RedactedHeaders</c>, which **pre-filters
/// on write** — <c>Authorization</c> / <c>Proxy-Authorization</c> values are
/// stored as <c>"[Filtered]"</c> no matter what is assigned. So the scrubber's
/// <c>"REDACTED"</c> token is observable for <c>set-cookie</c> / <c>cookie</c>
/// (not in Sentry's sensitive list) but <c>authorization</c> reads back
/// <c>"[Filtered]"</c> (the SDK's own write-time redaction wins over the
/// scrubber's write). The load-bearing contract — the PII **value** is never
/// observable — holds for all three; the exact redaction token differs by
/// surface.</item>
/// </list>
/// <para>
/// **No <c>src/**</c> production file is touched** (a test unit only). The
/// <c>Sentry</c> types resolve via the transitive graph
/// (<c>Sentry.AspNetCore 6.12.1</c> → <c>Sentry 6.12.1</c>, already added to
/// <c>Kumunita.Web.csproj</c> in F10.U2) — no <c>Sentry</c> <c>PackageReference</c>
/// is added to this test project (pin §4b gotcha 2 did not materialize).
/// </para>
public sealed class F10U3_ErrorTrackerTests
{
    // A well-formed DSN shape (not a real DSN) — pin §4a leg-1.
    private const string TestDsn = "https://test@o0.ingest.sentry.io/0";

    /// <summary>
    /// The pinned <c>Error_TrackerRegisteredWhenDsnSet</c> — all three legs
    /// (pin §4a): leg-1 the Sentry SDK DI surface is registered when the DSN is
    /// set; leg-2 it is NOT registered when the DSN is unset (the no-op path);
    /// leg-3 the <see cref="SentryPiiScrubber"/> contract redacts the §3c
    /// PII-bearing request surfaces on a synthetic event and does not drop a
    /// well-formed event.
    /// </summary>
    [Fact]
    public async Task Error_TrackerRegisteredWhenDsnSet()
    {
        // ── leg-1 (DSN set): the conditional UseSentry registration runs, the
        //    app host builds, and the Sentry SDK DI surface is resolvable.
        //    (Drift re-anchor: the pin's ISentrySdk/ISentryEventSink → the real
        //    6.12.1 ISentryClient.)
        await using (var app = BuildApp(dsn: TestDsn))
        {
            Assert.NotNull(app.Services.GetRequiredService<ISentryClient>());
        }

        // ── leg-2 (DSN unset): the conditional registration is skipped
        //    (Program.cs's `if (!string.IsNullOrWhiteSpace(dsn))` is false), so
        //    the SDK DI surface is NOT registered. (GetService, nullable — NOT
        //    GetRequiredService, per pin §4a leg-2.)
        await using (var app = BuildApp(dsn: null))
        {
            Assert.Null(app.Services.GetService<ISentryClient>());
        }

        // ── leg-3 (scrubber contract, load-bearing): §3c redaction on a
        //    synthetic SentryEvent. Headers are getter-only → populated through
        //    the getter's returned dictionary (the same surface the scrubber
        //    mutates); Cookies / QueryString are settable string? properties.
        var req = new SentryRequest
        {
            Cookies = "Kumunita.Auth=secret-a; .AspNetCore.Identity.Cookie=secret-b",
            QueryString = "token=abc&id=42&sessionid=sid&csrf=c&__RequestVerificationToken=rv&page=2",
        };
        req.Headers["authorization"] = "Bearer super-secret";
        req.Headers["set-cookie"]    = "Kumunita.Auth=secret-a; Path=/";
        req.Headers["cookie"]        = "Kumunita.Auth=secret-a";
        req.Headers["content-type"]  = "text/html; charset=utf-8";

        var evt = new SentryEvent { Request = req };

        // The scrubber is the F10.U2 registration's BeforeSend callback target —
        // assert its contract directly (the pin §4a leg-3 idiom: a synthetic
        // event → the redacted values are "REDACTED").
        var scrubbed = SentryPiiScrubber.Scrub(evt);

        // A well-formed event is scrubbed in place, NOT dropped (fail-closed
        // only fires on throw).
        Assert.NotNull(scrubbed);
        Assert.Same(evt, scrubbed);
        var outReq = scrubbed.Request!;

        // §3c target 1 — the PII-bearing headers are redacted; a non-PII header
        // is intact. (Drift re-anchor: `authorization` reads back the SDK's own
        // write-time redaction token "[Filtered]" — RedactedHeaders pre-filters
        // Authorization/Proxy-Authorization on write, so the scrubber's "REDACTED"
        // is unobservable for that key; the load-bearing point is the PII value is
        // gone. `set-cookie`/`cookie` are not in Sentry's sensitive list, so the
        // scrubber's "REDACTED" token is what reads back.)
        Assert.Equal("[Filtered]", outReq.Headers["authorization"]);
        Assert.Equal("REDACTED", outReq.Headers["set-cookie"]);
        Assert.Equal("REDACTED", outReq.Headers["cookie"]);
        Assert.Equal("text/html; charset=utf-8", outReq.Headers["content-type"]);

        // §3c target 2 — the cookie values are PII; the whole wire string is
        // redacted.
        Assert.Equal("REDACTED", outReq.Cookies);

        // §3c target 3 — the PII-bearing query params are redacted; the
        // non-PII param (page) is intact; the original PII values are gone.
        var qs = outReq.QueryString!;
        Assert.Contains("token=REDACTED", qs);
        Assert.Contains("id=REDACTED", qs);
        Assert.Contains("sessionid=REDACTED", qs);
        Assert.Contains("csrf=REDACTED", qs);
        Assert.Contains("__RequestVerificationToken=REDACTED", qs);
        Assert.Contains("page=2", qs);
        Assert.DoesNotContain("secret-a", outReq.Cookies);
        Assert.DoesNotContain("super-secret", outReq.Headers["authorization"]);
    }

    /// <summary>
    /// Faithfully mirrors <c>Program.cs</c>'s F10.U2 conditional registration:
    /// when <c>dsn</c> is non-blank the <c>builder.WebHost.UseSentry(…)</c>
    /// block runs (pin §2a, the exact F10.U2 body — including the drift-corrected
    /// <c>DisableSentryHttpMessageHandler</c> + <c>SetBeforeSend</c>); when it is
    /// blank the block is skipped (the no-op path). Building a full
    /// <c>WebApplication</c> is what materializes the deferred
    /// <c>ConfigureLogging</c> → <c>AddSentry()</c> registrations (incl.
    /// <see cref="ISentryClient"/>).
    /// </summary>
    private static WebApplication BuildApp(string? dsn)
    {
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        if (!string.IsNullOrWhiteSpace(dsn))
        {
            builder.WebHost.UseSentry(o =>
            {
                o.Dsn = dsn;
                // PII-scrubbing contract (pin §3a): default PII off.
                o.SendDefaultPii = false;
                // Pin §3a "AddNetworkLogBreadcrumbs = false" — no such member in
                // 6.12.1; DisableSentryHttpMessageHandler is the real lever.
                o.DisableSentryHttpMessageHandler = true;
                // Pin §3b/§3c: the BeforeSend callback redacts the PII-bearing
                // request surfaces; fail-closed on throw.
                o.SetBeforeSend((e, _) => SentryPiiScrubber.Scrub(e));
                // Pin §2d: the Sentry stack-trace in-app filter.
                o.AddInAppExclude("Kumunita.Web");
            });
        }
        return builder.Build();
    }
}
