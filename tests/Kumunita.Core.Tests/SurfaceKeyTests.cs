using System.Linq;
using System.Reflection;
using Kumunita.Core.Usage;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The 2 D2 / C-M13·2 pins (ADR 0114 D2; design doc §pinned tests).
/// BCL-only, no Testcontainers — <see cref="SurfaceKey.Map"/> is a pure
/// string-map and the <see cref="UsageEvent"/> shape pin is a reflection
/// assertion over the POCO's public properties.
/// </summary>
public class SurfaceKeyTests
{
    /// <summary>
    /// The D2 closed list: each of the 26 pinned top-level segments maps to
    /// its surface key (§surface-key in the design doc, verbatim); an
    /// unknown segment maps to <c>"other"</c> (the D2 closed list, the
    /// C-M13·4 boundary).
    /// </summary>
    [Fact]
    public void SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set()
    {
        // The 26 pinned top-level segments → surface keys (design doc §surface-key, verbatim).
        var pinned = new (string RouteTemplate, string Expected)[]
        {
            ("GET /posts/{id}",        "posts"),
            ("GET /events/{id}",       "events"),
            ("GET /groups/{id}",       "groups"),
            ("GET /admin/audit",       "admin"),
            ("GET /messages/{id}",     "messages"),
            ("GET /todos",             "todos"),
            ("GET /boards/{id}",       "boards"),
            ("GET /projects/{id}",     "projects"),
            ("GET /search",            "search"),
            ("GET /about",             "about"),
            ("GET /terms",             "terms"),
            ("GET /help",              "help"),
            ("GET /privacy",           "privacy"),
            ("GET /conduct",           "conduct"),
            ("GET /language",          "language"),
            ("GET /settings",          "settings"),
            ("GET /account",           "account"),
            ("GET /my/posts",          "my"),
            ("GET /pages/about",       "pages"),
            ("GET /community",         "community"),
            ("GET /attachments/{id}",  "attachments"),
            ("GET /content-image/{id}","content-image"),
            ("GET /notifications",     "notifications"),
            ("GET /calendar",          "calendar"),
            ("GET /whats-new",         "whats-new"),
            ("GET /health",            "health"),
        };

        foreach (var (rt, expected) in pinned)
        {
            var actual = SurfaceKey.Map(rt);
            Assert.True(expected == actual, $"RouteTemplate '{rt}' mapped to '{actual}', expected '{expected}'");
        }

        // Unknown segment → "other" (the D2 fallback).
        Assert.Equal("other", SurfaceKey.Map("GET /no/such/route"));

        // Bare "/" → "other" (no segment to extract).
        Assert.Equal("other", SurfaceKey.Map("GET /"));

        // Null → "other" (defensive, one bucket, never a crash).
        Assert.Equal("other", SurfaceKey.Map(null));

        // Segment without a METHOD prefix (defensive — the middleware always
        // prefixes, but SurfaceKey.Map is pure and handles both shapes).
        Assert.Equal("posts", SurfaceKey.Map("/posts/{id}"));
    }

    /// <summary>
    /// The C-M13·2 boundary (reflection pin): the <see cref="UsageEvent"/>
    /// POCO has <b>exactly</b> the four public properties
    /// <c>Id</c> / <c>At</c> / <c>ActorId</c> / <c>RouteTemplate</c>.
    /// A future lane that adds a field to <c>UsageEvent</c> moves this pin
    /// <b>together</b> (the drift-guard rule).
    /// </summary>
    [Fact]
    public void UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip()
    {
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "Id", "At", "ActorId", "RouteTemplate"
        };

        // GetProperties(Public | Instance) excludes static — no further filter needed.
        var actual = typeof(UsageEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Exactly four properties — no more, no fewer.
        Assert.Equal(4, actual.Count);
        Assert.Equal(expected, actual);

        // No forbidden field types: no email, no body, no UA, no IP, no status.
        Assert.DoesNotContain("Email", actual);
        Assert.DoesNotContain("Body", actual);
        Assert.DoesNotContain("UserAgent", actual);
        Assert.DoesNotContain("Ua", actual);
        Assert.DoesNotContain("Ip", actual);
        Assert.DoesNotContain("Status", actual);
        Assert.DoesNotContain("StatusCode", actual);
    }
}
