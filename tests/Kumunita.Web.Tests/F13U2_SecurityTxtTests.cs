using Kumunita.Core;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F13 (OPS-05: <c>/.well-known/security.txt</c> + VDP) — **U2**, the LAST
/// unit of F13. The single pinned test <c>SecurityTxt_ReturnsValidFields</c>
/// (F13.U1 pin, <c>WellKnownController</c> doc-comment + handoff note
/// F13.U1 "Pinned test name" section).
/// <para>
/// F13.U1 landed the <see cref="WellKnownController"/> route (the RFC 9116
/// <c>security.txt</c> body built inline from <see cref="CommunityOptions"/>)
/// + the VDP prose (<c>docs/SECURITY-VDP.md</c>). This unit is **test-only**
/// (≤1 file, Web.Tests): it pins the **contract** — the action returns a
/// <see cref="ContentResult"/> with **200** + <c>text/plain</c> and the five
/// RFC 9116 fields (<c>Contact</c> non-empty <c>mailto:</c>, <c>Expires</c> a
/// future date ≥ 12 months out, <c>Preferred-Languages: en</c>, <c>Policy</c>
/// a full URL ending <c>/.well-known/security.txt</c>, <c>Hints</c>
/// non-empty). The pin fixes the **contract**, not the **access path** —
/// per the F13.U1 handoff note the harness is the direct-action-call idiom
/// (the F3.U5 / <c>KioskControllerTests</c> <c>ActionContext</c>/stubbed-
/// <c>Request</c> precedent), **not** a <c>TestServer</c>.
/// </para>
/// <para>
/// <b>Legs (per the F13.U1 pin, verbatim):</b>
/// <list type="number">
/// <item><b>leg-1</b> — the result is a <see cref="ContentResult"/> with
/// <see cref="ContentResult.StatusCode"/> <c>== 200</c> and a
/// <see cref="ContentResult.ContentType"/> containing <c>text/plain</c>.</item>
/// <item><b>leg-2</b> — the body carries a <c>Contact:</c> line whose value
/// starts <c>mailto:</c>; for the stubbed config it equals
/// <c>mailto:support@examplium.com</c>.</item>
/// <item><b>leg-3 (load-bearing — not a string-equality assert, so the
/// pinned date can roll without breaking the test)</b> — the <c>Expires:</c>
/// value parses to a date <b>&gt; 12 months from <see cref="DateTime.UtcNow"
/// /></b>.</item>
/// <item><b>leg-4</b> — the <c>Policy:</c> value is a full URL ending
/// <c>/.well-known/security.txt</c> (the stubbed <c>Request</c> origin
/// <c>http://localhost</c>).</item>
/// <item><b>leg-5</b> — <c>Preferred-Languages: en</c> is present.</item>
/// <item><b>leg-6</b> — the <c>Hints:</c> value is non-empty.</item>
/// </list>
/// </para>
/// <para>
/// <b>No <c>src/**</c> change, no <c>PackageReference</c>, no migration,
/// no <c>.slnx</c> change</b> (a test-only unit; <c>WellKnownController</c> +
/// <c>SECURITY-VDP.md</c> from F13.U1 are **untouched**).
/// </para>
/// </summary>
public sealed class F13U2_SecurityTxtTests
{
    private const string SupportEmail = "support@examplium.com";
    private const string PolicySuffix = "/.well-known/security.txt";

    /// <summary>
    /// The pinned <c>SecurityTxt_ReturnsValidFields</c> — the action returns
    /// **200** + <c>text/plain</c> with the five RFC 9116 fields (the six legs
    /// above). Harness: a hand-rolled <see cref="WellKnownController"/> with a
    /// stubbed <see cref="IOptions{CommunityOptions}"/> (<c>SupportEmail</c>
    /// set — leg-2's exact-match config) and a stubbed
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> (the
    /// <c>KioskControllerTests</c> <c>DefaultHttpContext</c>
    /// <c>Scheme</c>/<c>Host</c> precedent — so the <c>Policy</c> origin
    /// builds), asserting on the <see cref="ContentResult"/> directly.
    /// </summary>
    [Fact]
    public void SecurityTxt_ReturnsValidFields()
    {
        var controller = Build();

        var result = controller.SecurityTxt();

        // ── leg-1: 200 + text/plain ────────────────────────────────────────
        var content = Assert.IsType<ContentResult>(result);
        // The <c>Content(body, "text/plain")</c> helper produces a
        // <see cref="ContentResult"/> whose <c>StatusCode</c> is the ASP.NET
        // Core "use the framework default" sentinel (<c>null</c>) — which the
        // <c>ContentResultExecutor</c> resolves to **200** (the route is a
        // 200; the helper does not stamp an explicit <c>StatusCode</c>). Pin
        // the semantic contract (200 OK = null-or-200), not the literal 200:
        Assert.True(
            content.StatusCode is null || content.StatusCode == 200,
            $"the security.txt route must be a 200 OK (StatusCode '{(content.StatusCode?.ToString() ?? "(null = framework default)")}'); expected null (the framework default) or 200.");
        Assert.NotNull(content.ContentType);
        Assert.Contains("text/plain", content.ContentType);

        // The action shape (the <c>Content(body, "text/plain")</c> contract):
        Assert.NotNull(content.Content);
        var body = content.Content;
        Assert.False(string.IsNullOrWhiteSpace(body), "the security.txt body must not be blank (RFC 9116).");

        // ── leg-2: Contact: a non-empty mailto: (exact match for the config) ─
        var contactLine = Field(body, "Contact");
        Assert.StartsWith("mailto:", contactLine);
        Assert.Equal($"mailto:{SupportEmail}", contactLine);

        // ── leg-3 (load-bearing): Expires: > 12 months out (parse, don't
        //      string-compare — the pinned date may roll) ──────────────────
        var expiresLine = Field(body, "Expires");
        Assert.True(
            DateTime.TryParse(expiresLine, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var expires),
            $"the Expires value '{expiresLine}' must parse as a date (RFC 9116).");
        Assert.True(
            expires > DateTime.UtcNow.AddMonths(12),
            $"the Expires value '{expiresLine}' must be at least 12 months in the future (RFC 9116).");

        // ── leg-4: Policy: a full URL ending /.well-known/security.txt ────
        var policyLine = Field(body, "Policy");
        // the Policy is built from the request origin (the stubbed http://localhost):
        Assert.StartsWith("http://localhost", policyLine);
        Assert.EndsWith(PolicySuffix, policyLine);

        // ── leg-5: Preferred-Languages: en ─────────────────────────────────
        Assert.Equal("en", Field(body, "Preferred-Languages"));

        // ── leg-6: Hints: non-empty ────────────────────────────────────────
        var hintsLine = Field(body, "Hints");
        Assert.False(string.IsNullOrWhiteSpace(hintsLine), "the Hints field must be non-empty.");
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the <see cref="WellKnownController"/> with a hand-rolled
    /// <see cref="IOptions{CommunityOptions}"/> (<c>SupportEmail</c> set to
    /// the leg-2 stub) and a stubbed <see cref="HttpContext"/> (Scheme
    /// <c>http</c> + Host <c>localhost</c> — the
    /// <c>KioskControllerTests</c> <c>DefaultHttpContext</c> precedent — so
    /// the action's <c>Policy</c> origin builds).
    /// </summary>
    private static WellKnownController Build()
    {
        var controller = new WellKnownController(Options.Create(new CommunityOptions
        {
            SupportEmail = SupportEmail,
        }));

        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Scheme = "http",
                Host   = new HostString("localhost"),
            },
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    /// <summary>
    /// Extract the value of an RFC 9116 <c>Field: value</c> line from the
    /// body. Fails the test (via <see cref="Assert.False"/>) if the field is
    /// absent — the five pinned fields are all mandatory here.
    /// </summary>
    private static string Field(string body, string name)
    {
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith($"{name}: ", StringComparison.Ordinal))
                return trimmed.Substring(name.Length + 2).Trim();
        }
        Assert.Fail($"the RFC 9116 '{name}' field is missing from the security.txt body.");
        return string.Empty; // unreachable — Assert.Fail throws
    }
}
