using System;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// SEC H3 (design doc §2.2, U9) — the config-driven replacement for the bare
/// no-options <c>app.UseForwardedHeaders()</c> at <c>Program.cs:961</c>.
/// <para>
/// <b>API note (verified against the installed .NET 10 shared framework, not
/// memory):</b> the options type the middleware consumes is
/// <c>Microsoft.AspNetCore.Builder.ForwardedHeadersOptions</c>; trust is set
/// through the get-only lists <c>KnownProxies</c> (<c>IList&lt;IPAddress&gt;</c>)
/// and <c>KnownIPNetworks</c> (<c>IList&lt;System.Net.IPNetwork&gt;</c>). There
/// is <b>no</b> <c>TrustedNetworks</c> property, and <c>KnownNetworks</c> is
/// obsolete in .NET 10 — this class uses the non-obsolete <c>KnownIPNetworks</c>.
/// </para>
/// <para>
/// Trust model (open veto 3, now a decision): trusted proxies come from
/// configuration — <c>ReverseProxy:KnownNetworks</c> (CIDR ranges, e.g.
/// <c>172.18.0.0/16</c>) and <c>ReverseProxy:KnownProxies</c> (individual IPs).
/// Unset/empty configuration falls back to <see cref="DefaultTrustedNetworks"/>
/// (loopback + RFC1918 — the Docker network). It <b>never</b> means "trust
/// everyone": an out-of-range peer's <c>X-Forwarded-For</c> /
/// <c>X-Forwarded-Proto</c> are ignored.
/// </para>
/// <para>
/// An unparsable CIDR or IP value throws <see cref="InvalidOperationException"/>
/// from <see cref="BuildOptions"/> (called at <c>builder.Services</c> time) so a
/// config typo fails the boot instead of silently opening the door.
/// </para>
/// <para>
/// U9 adds this class and its tests only; wiring it into <c>Program.cs</c> and
/// the <c>Secure</c> cookie policy is U10.
/// </para>
/// </summary>
public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Registers a fully-built <see cref="ForwardedHeadersOptions"/> (X-Forwarded-For
    /// | X-Forwarded-Proto, <c>ForwardLimit = 1</c>, trusted networks from
    /// <c>ReverseProxy:KnownNetworks</c> / <c>ReverseProxy:KnownProxies</c>,
    /// defaulting to <see cref="DefaultTrustedNetworks"/> when both are unset/empty).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A configured value is neither a parsable IP address nor a parsable CIDR
    /// (fail the boot, not open).
    /// </exception>
    public static void AddKumunitaForwardedHeaders(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure(options => Configure(options, config));
    }

    /// <summary>
    /// Builds a fully-configured <see cref="ForwardedHeadersOptions"/>.
    /// Exposed so the pinned tests (U9) can build the options in isolation and
    /// drive a <c>DefaultHttpContext</c> through
    /// <c>ForwardedHeadersMiddleware</c>, and so <c>Program.cs</c> (U10) can
    /// reuse the exact same logic.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A configured value is neither a parsable IP address nor a parsable CIDR.
    /// </exception>
    public static ForwardedHeadersOptions BuildOptions(IConfiguration config)
    {
        var options = new ForwardedHeadersOptions();
        Configure(options, config);
        return options;
    }

    /// <summary>
    /// Applies the forwarded-header policy to an existing
    /// <see cref="ForwardedHeadersOptions"/> instance. The single source of
    /// truth for the header set, the forward limit and the trusted networks;
    /// <see cref="AddKumunitaForwardedHeaders"/> and <see cref="BuildOptions"/>
    /// both delegate here.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A configured value is neither a parsable IP address nor a parsable CIDR.
    /// </exception>
    public static void Configure(ForwardedHeadersOptions options, IConfiguration config)
    {
        var reverseProxy = config.GetSection("ReverseProxy");
        var networks = reverseProxy.GetSection("KnownNetworks").Get<string[]>() ?? Array.Empty<string>();
        var proxies = reverseProxy.GetSection("KnownProxies").Get<string[]>() ?? Array.Empty<string>();

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        if (networks.Length == 0 && proxies.Length == 0)
        {
            // Unset/empty = the safe defaults (loopback + RFC1918), never trust-all.
            foreach (var network in DefaultTrustedNetworks)
            {
                options.KnownIPNetworks.Add(network);
            }

            return;
        }

        foreach (var network in networks)
        {
            options.KnownIPNetworks.Add(ParseNetwork(network, "ReverseProxy:KnownNetworks"));
        }

        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(ParseProxy(proxy, "ReverseProxy:KnownProxies"));
        }
    }

    /// <summary>
    /// The default trusted networks (open veto 3): loopback (v4 + v6) and the
    /// RFC1918 ranges — the Docker network — never the world.
    /// </summary>
    public static IReadOnlyList<System.Net.IPNetwork> DefaultTrustedNetworks { get; } = new[]
    {
        new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8),
        new System.Net.IPNetwork(IPAddress.Parse("::1"), 128),
        new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8),
        new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12),
        new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16),
    };

    /// <summary>
    /// Parses one <c>ReverseProxy:KnownNetworks</c> entry as a CIDR range
    /// (<c>a.b.c.d/prefix</c>). Anything that is not a valid CIDR — an empty
    /// entry, a bad address, a non-numeric or out-of-range prefix — throws
    /// <see cref="InvalidOperationException"/> with the offending value and key.
    /// </summary>
    private static System.Net.IPNetwork ParseNetwork(string value, string configKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{configKey} contains an empty entry. Remove it or give it a CIDR range (e.g. 172.18.0.0/16).");
        }

        var parts = value.Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var network)
            || !int.TryParse(parts[1], out var prefix))
        {
            throw new InvalidOperationException(
                $"{configKey} entry '{value}' is not a valid CIDR range (expected e.g. 172.18.0.0/16).");
        }

        try
        {
            return new System.Net.IPNetwork(network, prefix);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidOperationException(
                $"{configKey} entry '{value}' has an invalid prefix length '{prefix}'.", ex);
        }
    }

    /// <summary>
    /// Parses one <c>ReverseProxy:KnownProxies</c> entry as an IP address.
    /// An empty or unparsable value throws <see cref="InvalidOperationException"/>.
    /// </summary>
    private static IPAddress ParseProxy(string value, string configKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{configKey} contains an empty entry. Remove it or give it an IP address (e.g. 172.18.0.5).");
        }

        if (!IPAddress.TryParse(value, out var ip))
        {
            throw new InvalidOperationException(
                $"{configKey} entry '{value}' is not a valid IP address.");
        }

        return ip;
    }
}
