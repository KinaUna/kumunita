using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC H3 — the pinned tests for <see cref="ForwardedHeadersSetup"/>
/// (design doc §2.3, U9; invariants S-3 "client IP and scheme come only from a
/// configured trusted proxy" and S-8 "every fix has a named regression test").
/// <para>
/// Each test builds the options in isolation via
/// <see cref="ForwardedHeadersSetup.BuildOptions"/> from an in-memory
/// <see cref="IConfiguration"/>, then drives a <see cref="DefaultHttpContext"/>
/// through the real <see cref="ForwardedHeadersMiddleware"/> (the
/// <c>DefaultHttpContext</c> + in-memory-response-body idiom the house
/// middleware tests use) and asserts what the request resolved to. Synthetic
/// peers only — no PII.
/// </para>
/// <para>
/// <b>API note:</b> in the installed .NET 10 shared framework the options type
/// is <c>Microsoft.AspNetCore.Builder.ForwardedHeadersOptions</c> and trust is
/// set via the get-only <c>KnownProxies</c> / <c>KnownIPNetworks</c> lists
/// (there is no <c>TrustedNetworks</c> property and <c>KnownNetworks</c> is
/// obsolete) — verified by reflection against
/// <c>shared/Microsoft.AspNetCore.App/10.0.13</c>, not memory.
/// </para>
/// </summary>
public sealed class ForwardedHeadersSetupTests
{
    // ── The four pinned names (design doc §2.3, verbatim) ─────────────

    /// <summary>
    /// A peer inside a configured trusted CIDR, sending
    /// <c>X-Forwarded-For</c> + <c>X-Forwarded-Proto</c>, has both honored:
    /// the resolved remote IP is the forwarded client IP and
    /// <see cref="HttpRequest.IsHttps"/> is true.
    /// </summary>
    [Fact]
    public void TrustedProxy_ClientIpAndSchemeHonored()
    {
        var config = Config(
            ("ReverseProxy:KnownNetworks:0", "172.18.0.0/16"));
        var options = ForwardedHeadersSetup.BuildOptions(config);

        var ctx = Drive(options, peerIp: "172.18.0.5", xff: "203.0.113.7", xproto: "https");

        Assert.Equal(IPAddress.Parse("203.0.113.7"), ctx.Connection.RemoteIpAddress);
        Assert.True(ctx.Request.IsHttps, "a trusted proxy's X-Forwarded-Proto must set the scheme");
    }

    /// <summary>
    /// A peer OUTSIDE any trusted range sending the same headers has them
    /// ignored: the remote IP stays the peer and the scheme stays clear.
    /// </summary>
    [Fact]
    public void UntrustedPeer_HeadersIgnored()
    {
        var config = Config(
            ("ReverseProxy:KnownNetworks:0", "10.0.0.0/8"));
        var options = ForwardedHeadersSetup.BuildOptions(config);

        var ctx = Drive(options, peerIp: "172.18.0.5", xff: "203.0.113.7", xproto: "https");

        Assert.Equal(IPAddress.Parse("172.18.0.5"), ctx.Connection.RemoteIpAddress);
        Assert.False(ctx.Request.IsHttps, "an untrusted peer must not be able to set the scheme");
    }

    /// <summary>
    /// With no configuration the defaults (loopback + RFC1918) apply — they are
    /// exactly the private ranges, and a public peer is still NOT trusted (never
    /// "trust everyone").
    /// </summary>
    [Fact]
    public void EmptyConfig_DefaultsToPrivateRangesNotTrustAll()
    {
        var options = ForwardedHeadersSetup.BuildOptions(Config());

        // Defaults are exactly the private ranges (order-independent).
        var defaults = new HashSet<string>(options.KnownIPNetworks.Select(n => n.ToString()));
        Assert.Equal(new HashSet<string>
        {
            "127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
        }, defaults);

        // A public peer sending forged headers is NOT honored (not trust-all).
        var ctx = Drive(options, peerIp: "203.0.113.7", xff: "8.8.8.8", xproto: "https");
        Assert.Equal(IPAddress.Parse("203.0.113.7"), ctx.Connection.RemoteIpAddress);
        Assert.False(ctx.Request.IsHttps);
    }

    /// <summary>
    /// An unparsable CIDR (and an unparsable proxy IP) makes the build throw
    /// <see cref="InvalidOperationException"/> — a config typo fails the boot,
    /// it does not silently open the door.
    /// </summary>
    [Fact]
    public void InvalidCidr_FailsBoot()
    {
        // Bad prefix length / bad address / garbage all fail the build.
        foreach (var bad in new[] { "172.18.0.0/99", "not-an-ip/16", "172.18.0.0/xyz" })
        {
            var cidrConfig = Config(("ReverseProxy:KnownNetworks:0", bad));
            var ex = Assert.Throws<InvalidOperationException>(
                () => ForwardedHeadersSetup.BuildOptions(cidrConfig));
            Assert.Contains(bad, ex.Message);
        }

        // A non-IP proxy entry fails the build too.
        var config = Config(("ReverseProxy:KnownProxies:0", "not-an-ip"));
        var proxyEx = Assert.Throws<InvalidOperationException>(
            () => ForwardedHeadersSetup.BuildOptions(config));
        Assert.Contains("not-an-ip", proxyEx.Message);
    }

    // ── Harness ────────────────────────────────────────────────────────

    private static IConfiguration Config(params (string key, string value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    /// <summary>
    /// Drives one request through the real <see cref="ForwardedHeadersMiddleware"/>
    /// with the given options, peer IP and optional <c>X-Forwarded-For</c> /
    /// <c>X-Forwarded-Proto</c> headers, and returns the resolved
    /// <see cref="HttpContext"/>.
    /// </summary>
    private static DefaultHttpContext Drive(
        ForwardedHeadersOptions options, string peerIp, string? xff, string? xproto)
    {
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IOptions<ForwardedHeadersOptions>>(Options.Create(options));
        var provider = services.BuildServiceProvider();

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new ForwardedHeadersMiddleware(
            next,
            provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>(),
            provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>());

        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(peerIp);
        ctx.Request.Method = "GET";
        ctx.Request.Path = "/";
        // The DefaultHttpContext response body is a non-writable empty stream —
        // back it with a real in-memory stream (house middleware-test idiom).
        ctx.Response.Body = new MemoryStream();
        if (!string.IsNullOrEmpty(xff)) ctx.Request.Headers["X-Forwarded-For"] = xff;
        if (!string.IsNullOrEmpty(xproto)) ctx.Request.Headers["X-Forwarded-Proto"] = xproto;

        middleware.Invoke(ctx);
        return ctx;
    }
}
