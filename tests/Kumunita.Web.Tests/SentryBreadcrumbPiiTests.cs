using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-SEC **IS05** — the Sentry breadcrumb floor (prove, then fix).
/// <para>
/// At the pinned <c>Sentry.AspNetCore 6.12.1</c>, <c>UseSentry</c> registers the
/// Sentry logging integration (<c>SentryAspNetCoreLoggerProvider</c> /
/// <c>SentryAspNetCoreStructuredLoggerProvider</c>), which turns application log
/// lines into breadcrumbs on captured events. The <c>o =></c> receiver,
/// <c>SentryAspNetCoreOptions</c>, inherits
/// <c>MinimumBreadcrumbLevel</c> / <c>MinimumEventLevel</c>
/// (<see cref="LogLevel" />) from <c>SentryLoggingOptions</c>
/// (<c>Sentry.Extensions.Logging</c>). IS05 sets the floor to
/// <see cref="LogLevel.Warning" /> so the Information-level lines that carry
/// <c>{Email}</c> / <c>{Recipient}</c> in this codebase (E3) stop reaching the
/// third party as breadcrumbs, while the existing <see cref="SentryPiiScrubber"
/// /> still redacts request PII on the event.
/// </para>
/// <para>
/// The harness mirrors <c>Program.cs</c>'s conditional registration (the
/// <c>builder.WebHost.UseSentry(o => …)</c> shape, the 6.12.1
/// <c>IWebHostBuilder</c> overload) and the F10.U3 app-host idiom —
/// <c>UseSentry</c> defers its SDK + logging-provider registrations into a
/// <c>ConfigureLogging</c> delegate that only runs at host <c>Build()</c>. The
/// captured event is observed through <c>SetBeforeSend</c> (the event is the
/// public surface; its <see cref="SentryEvent.Breadcrumbs" /> is the breadcrumb
/// evidence) and an in-memory <see cref="ITransport" /> so nothing leaves the
/// process.
/// </para>
/// <para>
/// **No <c>src/**</c> behavior is touched beyond the two floor lines + the three
/// comment corrections in <c>Program.cs</c>; this unit is the sink-side pin for
/// P-3, complemented by the source-side redaction in IS06–IS08.
/// </para>
public sealed class SentryBreadcrumbPiiTests
{
    // A well-formed DSN shape (not a real DSN) — mirrors F10.U3's TestDsn.
    private const string TestDsn = "https://test@o0.ingest.sentry.io/0";

    /// <summary>
    /// With <c>MinimumBreadcrumbLevel = Warning</c>, an Information-level log
    /// line carrying <c>a@example.test</c> produces **no breadcrumb** and its
    /// value is not observable anywhere on the captured event.
    /// </summary>
    [Fact]
    public async Task InformationLog_IsNotABreadcrumb()
    {
        var result = await RunAppAsync(
            actions: logger => logger.LogInformation("resident signed in {Email}", "a@example.test"),
            triggerEvent: logger => logger.LogError("boom for {UserId}", "u-1"));

        // The captured event exists (the trigger), and it carries no breadcrumb
        // from the Information line.
        Assert.NotNull(result.Event);
        Assert.Empty(result.Event!.Breadcrumbs);
        // The Information line's value (the email) is not observable on the event.
        Assert.DoesNotContain("a@example.test", result.Event.ToString());
    }

    /// <summary>
    /// A Warning-level log line *does* become a breadcrumb (it clears the
    /// <c>Warning</c> floor), and that breadcrumb carries no request PII — its
    /// structured value is the non-PII placeholder, not an address.
    /// </summary>
    [Fact]
    public async Task WarningLog_IsABreadcrumb_WithoutRequestPii()
    {
        var result = await RunAppAsync(
            actions: logger => logger.LogWarning("quota near limit for {UserId}", "u-1"),
            triggerEvent: logger => logger.LogError("boom for {UserId}", "u-1"));

        Assert.NotNull(result.Event);
        Assert.NotEmpty(result.Event!.Breadcrumbs);

        var breadcrumb = result.Event.Breadcrumbs.First(b => b.Message is not null && b.Message.Contains("quota near limit"));
        Assert.Equal(BreadcrumbLevel.Warning, breadcrumb.Level);
        // No request PII: the address placeholder is not the value.
        Assert.DoesNotContain("a@example.test", breadcrumb.Message ?? string.Empty);
        Assert.DoesNotContain("u-1@example.test", breadcrumb.ToString());
    }

    /// <summary>
    /// The existing <see cref="SentryPiiScrubber" /> contract still redacts the
    /// PII-bearing request surfaces (headers, cookies, query-string params) on a
    /// synthetic event — the floor (above) and the scrubber (here) are
    /// independent, complementary controls. Reuses the F10.U3 leg-3 finding:
    /// the load-bearing contract is "the PII **value** is never observable";
    /// the exact redaction token differs by surface.
    /// </summary>
    [Fact]
    public void ScrubberStillRedactsHeadersCookiesQuery()
    {
        var req = new SentryRequest
        {
            Cookies = "Kumunita.Auth=secret-a; .AspNetCore.Identity.Cookie=secret-b",
            QueryString = "token=abc&id=42&sessionid=sid&csrf=c&__RequestVerificationToken=rv&page=2",
        };
        req.Headers["authorization"] = "Bearer super-secret";
        req.Headers["set-cookie"] = "Kumunita.Auth=secret-a; Path=/";
        req.Headers["cookie"] = "Kumunita.Auth=secret-a";

        var evt = new SentryEvent { Request = req };
        var scrubbed = SentryPiiScrubber.Scrub(evt);

        Assert.NotNull(scrubbed);
        var outReq = scrubbed!.Request!;
        // The PII values are never observable — the load-bearing contract.
        Assert.DoesNotContain("secret-a", outReq.Cookies);
        Assert.DoesNotContain("super-secret", outReq.Headers["authorization"]);
        Assert.DoesNotContain("abc", outReq.QueryString);
        Assert.DoesNotContain("42", outReq.QueryString);
        Assert.DoesNotContain("sid", outReq.QueryString);
        Assert.DoesNotContain("rv", outReq.QueryString);
        // A non-redacted param survives (the scrubber is targeted, not a wipe).
        Assert.Contains("page=2", outReq.QueryString);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private sealed record Capture(SentryEvent? Event);

    private sealed class SwallowingTransport : ITransport
    {
        public Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Builds a real app host exactly as <c>Program.cs</c> does (the F10.U3
    /// idiom): the Sentry logging integration is only registered at
    /// <c>Build()</c>. Logs the <c>actions</c> lines, then fires <c>triggerEvent</c>
    /// to capture an event whose <c>Breadcrumbs</c> we can inspect, and returns
    /// the captured event.
    /// </summary>
    private static async Task<Capture> RunAppAsync(Action<ILogger> actions, Action<ILogger> triggerEvent)
    {
        SentryEvent? captured = null;
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders(); // keep console output quiet; Sentry's providers are what we test
        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = TestDsn;
            o.SendDefaultPii = false;
            o.DisableSentryHttpMessageHandler = true;
            o.SetBeforeSend((e, _) => { captured = e; return e; });
            // IS05's floor — the two lines under test.
            o.MinimumBreadcrumbLevel = LogLevel.Warning;
            o.MinimumEventLevel = LogLevel.Error;
            o.Transport = new SwallowingTransport(); // nothing leaves the process
        });
        await using var app = builder.Build();

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IS05");
        actions(logger);
        triggerEvent(logger);
        return new Capture(captured);
    }
}
