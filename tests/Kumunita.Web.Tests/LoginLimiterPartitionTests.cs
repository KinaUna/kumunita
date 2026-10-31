using System.Net;
using Kumunita.Core.Bootstrap;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC L8 (design doc §2.3, U27) — the pinned regression tests for the
/// <c>login</c> rate-limit policy: it partitions on the **resolved client IP
/// only** and carries **no sample-account exemption**.
/// <para>
/// The tests drive the pure <see cref="LoginRateLimitPolicy.KeyFor"/> helper
/// (the partition-key core, extracted so it is testable in isolation — the
/// house idiom U9 uses for <c>ForwardedHeadersSetup</c>) and assert that the
/// key depends on the client IP and on **nothing else**: not the posted
/// e-mail, not a sample-account membership. .NET 10's <c>RateLimitPartition</c>
/// is opaque (no public <c>Key</c>/<c>Name</c>/<c>Limiter</c>), so the
/// partition-key core is the honest observable surface — a change that
/// re-introduces the <c>GetNoLimiter("sample-exempt")</c> branch or a form-body
/// read would make the key depend on something other than the IP and these
/// tests fail.
/// </para>
/// <para>
/// Synthetic IPs and the code-owned sample set (via
/// <see cref="SampleDataSeeder.SampleAccountEmails"/>) — no PII, no hardcoded
/// sample e-mails.
/// </para>
/// </summary>
public sealed class LoginLimiterPartitionTests
{
    /// <summary>
    /// The partition key is the client IP, not the posted e-mail: the same IP
    /// yields the same key regardless of the e-mail (sample or not), and a
    /// different IP yields a different key (separate bucket).
    /// </summary>
    [Fact]
    public void LoginLimiter_PartitionsOnIpOnly()
    {
        var ipA = IPAddress.Parse("10.0.0.1");
        var ipB = IPAddress.Parse("10.0.0.2");

        // Same IP, sample e-mail vs a different e-mail → identical key.
        var sampleKey = LoginRateLimitPolicy.KeyFor(Ctx(ipA, SampleEmail()));
        var otherKey = LoginRateLimitPolicy.KeyFor(Ctx(ipA, "other@example.test"));
        Assert.True(sampleKey == otherKey,
            $"the posted e-mail must not feed the login partition key (sample='{sampleKey}', other='{otherKey}')");

        // Different IP, same e-mail → different key.
        var differentIpKey = LoginRateLimitPolicy.KeyFor(Ctx(ipB, SampleEmail()));
        Assert.True(sampleKey != differentIpKey,
            $"a different client IP must get its own partition (bucket) (ipA='{sampleKey}', ipB='{differentIpKey}')");

        // The key IS the IP's string form (no secret salt or transform).
        Assert.True(sampleKey == ipA.ToString(),
            $"the partition key is the resolved client IP verbatim (got '{sampleKey}', expected '{ipA}')");
    }

    /// <summary>
    /// A sample/test account e-mail no longer bypasses the limiter: the
    /// partition key for a sample-account request is exactly the same as for a
    /// non-sample request from the same IP — i.e. no <c>GetNoLimiter("sample-exempt")</c>
    /// branch, no form-body read, no per-e-mail carve-out. (The old code
    /// returned <c>RateLimitPartition.GetNoLimiter("sample-exempt")</c> when
    /// the posted e-mail was in <see cref="SampleDataSeeder.SampleAccountEmails"/>;
    /// the new code returns the same IP-keyed fixed-window partition for every
    /// e-mail.)
    /// </summary>
    [Fact]
    public void LoginLimiter_NoSampleAccountExemption()
    {
        var ip = IPAddress.Parse("10.0.0.7");
        var sampleEmail = SampleEmail();

        // The sample-account e-mail must not appear anywhere in the key.
        var sampleKey = LoginRateLimitPolicy.KeyFor(Ctx(ip, sampleEmail));
        Assert.True(!(sampleEmail.Contains(sampleKey) || sampleKey.Contains(sampleEmail)),
            $"the partition key must not embed the posted e-mail (key='{sampleKey}') — that was the old 'sample-exempt' branch");

        // The key is identical for the sample and a non-sample e-mail from the
        // same IP — the old code diverged here (sample → "sample-exempt",
        // other → the IP). Now both are the IP.
        var nonSampleKey = LoginRateLimitPolicy.KeyFor(Ctx(ip, "other@example.test"));
        Assert.True(sampleKey == nonSampleKey,
            $"a sample-account e-mail must no longer get a distinct (exempt) partition (sample='{sampleKey}', other='{nonSampleKey}') — the old 'sample-exempt' no-op is gone");

        // And the key is the IP, verbatim.
        Assert.True(sampleKey == ip.ToString(),
            $"the partition key is the resolved client IP verbatim (got '{sampleKey}', expected '{ip}') — no sample-account carve-out");
    }

    // ── Harness ────────────────────────────────────────────────────────

    private static string SampleEmail() => SampleDataSeeder.SampleAccountEmails.First();

    /// <summary>
    /// Builds a <see cref="DefaultHttpContext"/> with the given resolved client
    /// IP and a form carrying an <c>Email</c> value (the old exemption read
    /// this; the new policy must ignore it).
    /// </summary>
    private static DefaultHttpContext Ctx(IPAddress ip, string email)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = ip;
        ctx.Request.Method = "POST";
        ctx.Request.ContentType = "application/x-www-form-urlencoded";
        ctx.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["Email"] = new Microsoft.Extensions.Primitives.StringValues(email),
        });
        return ctx;
    }
}
