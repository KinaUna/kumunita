using System;
using Microsoft.AspNetCore.Http;
using System.Threading.RateLimiting;

namespace Kumunita.Web.Security;

/// <summary>
/// SEC L8 (design doc §2.2/§2.3, U27) — the single source of truth for the
/// <c>login</c> rate-limit policy partition.
/// <para>
/// The login limiter partitions on the **resolved client IP only**
/// (<see cref="HttpContext.Connection.RemoteIpAddress"/>, which
/// <c>UseForwardedHeaders</c> resolves for a trusted proxy — SEC H3/U10). It
/// **never reads the request body** and carries **no sample-account
/// exemption**: the closed sample-data set was historically exempted so an
/// operator could switch between test logins without throttling, but that
/// branch read <c>Request.Form["Email"]</c> *and* let an attacker post the
/// sample e-mail to bypass the limit entirely. Brute-force on any account —
/// sample or not — stays bounded by the account-level ASP.NET Identity lockout,
/// which is independent of this IP limit, so the exemption is removed.
/// </para>
/// <para>
/// Extracted as a <c>public</c> helper (mirroring
/// <see cref="ForwardedHeadersSetup"/>) so <c>Program.cs</c> wires the exact
/// same logic and the pinned U27 tests
/// (<c>LoginLimiterPartitionTests</c>) can drive it in isolation without
/// booting the host. <c>Program.cs</c> top-level members are
/// <c>internal</c> with no <c>InternalsVisibleTo</c>, so a private local here
/// would be untestable — hence this type.
/// </para>
/// </summary>
public static class LoginRateLimitPolicy
{
    /// <summary>Login attempts a single client IP may make within <see cref="Window"/>.</summary>
    public const int PermitLimit = 5;

    /// <summary>The fixed window a client IP gets.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The partition key for a login attempt: the **resolved client IP**, or
    /// <c>"unknown"</c> when no remote IP is available (loopback-less dev).
    /// <para>
    /// Exposed as a pure function so the pinned U27 tests
    /// (<c>LoginLimiterPartitionTests</c>) can assert IP-only partitioning
    /// directly: the key depends on <c>Connection.RemoteIpAddress</c> and on
    /// **nothing else** (no posted e-mail, no sample-account exemption). The
    /// old <c>GetNoLimiter("sample-exempt")</c> branch is gone.
    /// </para>
    /// </summary>
    public static string KeyFor(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// The rate-limit partition for a login attempt. Keyed on the resolved
    /// client IP (<see cref="KeyFor"/>); no body read, no sample-account
    /// exemption. A real fixed-window limiter (5 / 15 min) is always attached.
    /// </summary>
    /// <remarks>
    /// <c>RateLimitPartition</c> is a static factory class in .NET 10; the
    /// concrete partition type is the generic <c>RateLimitPartition&lt;T&gt;</c>
    /// keyed on the partition key type — here <c>string</c> (the client IP's
    /// string form), matching the shape <c>Program.cs</c>'s other policies
    /// (e.g. the <c>AddWindow</c> helpers) already produce.
    /// </remarks>
    public static RateLimitPartition<string> Partition(HttpContext context)
        => RateLimitPartition.GetFixedWindowLimiter(
            KeyFor(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = Window,
                AutoReplenishment = true
            });
}
