using System.Net;
using System.Net.Sockets;
using Kumunita.Web.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.Tests;

/// <summary>
/// R02 (IMPROVE-UX, D2) — integration tests for the status-page re-execute
/// gate. The DoD pins both sides:
/// <list type="bullet">
/// <item>an HTML 404 re-executes to the status page (body contains the shared
/// nav and the 404 heading); the numeric status code is unchanged (404)</item>
/// <item>a fetch/<c>X-Requested-With</c> 404 body is unchanged (bare, empty)
/// and the status code is unchanged (404)</item>
/// </list>
/// The harness boots a minimal Kestrel host on a random port with the
/// <see cref="StatusPageGate.ReExecuteHtmlOnly"/> handler wired in via
/// <c>UseStatusCodePages</c> (registered before <c>UseRouting</c>, matching
/// the production pipeline), plus the probe endpoints the R02 card names.
/// It does <b>not</b> boot the full Kumunita app — the seam under test is
/// the gate + re-execute mechanism; the R01 <c>StatusController</c> /
/// <c>Views/Status/Index.cshtml</c> are pinned by <see cref="StatusControllerTests"/>.
/// </summary>
public sealed class StatusPageGateTests : IAsyncLifetime
{
    private WebApplication? _app;
    private HttpClient? _client;

    public async ValueTask InitializeAsync()
    {
        var port = GetAvailablePort();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        _app = builder.Build();

        // R02 gate — registered BEFORE UseRouting, as in Program.cs.
        _app.UseStatusCodePages(StatusPageGate.ReExecuteHtmlOnly);
        _app.UseRouting();

        // Probe endpoints (the R02 card's list).
        _app.MapGet("/boom", () => Results.NotFound());
        _app.MapGet("/forbid", () => Results.StatusCode(403));
        _app.MapGet("/api/boom", () => Results.NotFound());
        _app.MapGet("/cal.ics", () => Results.NotFound());
        // The status-page stand-in (the R01 page in the real app). Renders the
        // same shape Views/Status/Index.cshtml has: a shared nav + an h1 heading.
        _app.MapGet("/status/{code:int}", (int code) => Results.Content(
            $"<nav><a href='/'>Home</a><a href='/search'>Search</a></nav>" +
            $"<h1>{StatusHeading(code)}</h1>",
            "text/html"));

        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    // ── HTML 404 → status page (the DoD's primary pin) ─────────────────────

    [Fact]
    public async Task Html404_ReexecutesToStatusPage_WithNavAndHeading_AndUnchangedStatusCode()
    {
        var response = await SendHtmlAsync("/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);   // code unchanged
        Assert.Contains("<nav>", body);                                // shared nav
        Assert.Contains("<h1>", body);                                 // 404 heading
    }

    [Fact]
    public async Task Html403_ReexecutesToStatusPage_WithNavAndHeading_AndUnchangedStatusCode()
    {
        var response = await SendHtmlAsync("/forbid");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("<nav>", body);
        Assert.Contains("<h1>", body);
    }

    // ── XHR / fetch / JSON → bare status, unchanged body ───────────────────

    [Fact]
    public async Task Xhr404_BodyUnchanged_AndStatusCodeUnchanged()
    {
        var response = await SendAsync("/boom",
            headers: h => h.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Json404_BodyUnchanged_AndStatusCodeUnchanged()
    {
        var response = await SendAsync("/boom",
            headers: h => h.Headers.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(body);
    }

    // ── Excluded path prefixes → bare status ───────────────────────────────

    [Fact]
    public async Task Api404_BodyUnchanged_AndStatusCodeUnchanged()
    {
        var response = await SendHtmlAsync("/api/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Ics404_BodyUnchanged_AndStatusCodeUnchanged()
    {
        var response = await SendHtmlAsync("/cal.ics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(body);
    }

    // ── Direct navigation to /status/{code} still works ────────────────────

    [Fact]
    public async Task DirectStatusPageNavigation_Returns200()
    {
        var response = await SendHtmlAsync("/status/404");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<nav>", body);
        Assert.Contains("<h1>", body);
    }

    // ── Gate unit tests (no host needed — pure) ────────────────────────────

    [Theory]
    [InlineData("/boom", "text/html", null, true)]
    [InlineData("/boom", "application/json", null, false)]
    [InlineData("/boom", "text/html", "XMLHttpRequest", false)]
    [InlineData("/api/boom", "text/html", null, false)]
    [InlineData("/cal.ics", "text/html", null, false)]
    [InlineData("/media/image.png", "text/html", null, false)]
    [InlineData("/kiosk/slide/1", "text/html", null, false)]
    [InlineData("/settings/kiosk/items", "text/html", null, false)]
    [InlineData("/health", "text/html", null, false)]
    [InlineData("/status/404", "text/html", null, false)]
    public void IsHtmlPageRequest_GatesByPathAcceptAndXhr(
        string path, string accept, string? xhr, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Headers.Accept = accept;
        if (xhr is not null)
        {
            context.Request.Headers["X-Requested-With"] = xhr;
        }

        Assert.Equal(expected, StatusPageGate.IsHtmlPageRequest(context));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> SendHtmlAsync(string path)
        => SendAsync(path,
            headers: h => h.Headers.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html")));

    private async Task<HttpResponseMessage> SendAsync(
        string path, Action<HttpRequestMessage>? headers = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        headers?.Invoke(request);
        return await _client!.SendAsync(request);
    }

    private static string StatusHeading(int code) => code switch
    {
        404 => "Page not found",
        403 => "Access denied",
        400 => "Bad request",
        413 => "Payload too large",
        415 => "Unsupported media type",
        429 => "Too many requests",
        _ => "Something went wrong",
    };

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
