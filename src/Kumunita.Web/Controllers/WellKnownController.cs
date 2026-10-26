using Kumunita.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The RFC 9116 <c>/.well-known/security.txt</c> route (OPS-05 / F13).
/// <para>
/// This is a **tiny additive surface with no bounded context** (the F13 plan
/// row): one anonymous, dependency-light GET that returns the machine-readable
/// security.txt (RFC 9116) companion to the VDP prose in
/// <c>docs/SECURITY-VDP.md</c>. The prose is the canonical human-readable
/// policy; this route is the machine-readable one (the contract the
/// <c>SecurityTxt_ReturnsValidFields</c> test pins — F13.U2).
/// </para>
/// <para>
/// **Routing mechanism (F13.U1 judgment call, pinned here):** a **controller
/// action** (the repo idiom — the <c>StaticPagesController</c> absolute-route
/// precedent, e.g. <c>[HttpGet("/terms")]</c>), not <c>app.MapGet</c>. The body
/// is **built inline** (no file read at request time): the VDP doc
/// (<c>SECURITY-VDP.md</c>) is the canonical prose for humans / the repo, and
/// this route carries the RFC 9116 <em>fields</em> — a single source of truth
/// per artifact, no startup-file-read dependency, and no <c>wwwroot</c> file
/// that a deployer could accidentally shadow (a <c>wwwroot/.well-known/</c>
/// file is not in the repo today, so there is no shadowing risk either way,
/// but the inline body keeps the route self-contained and trivially testable).
/// </para>
/// <para>
/// **Contact (F13.U1 judgment call, pinned here):** the
/// <see cref="CommunityOptions.SupportEmail"/> (the instance's configured,
/// **monitored** support mailbox — the same channel F12 routes alerts to),
/// emitted as an RFC 9116 <c>mailto:</c> value. It is config-driven so the
/// field is always the live, owner-monitored address in every deployment; a
/// stable, non-empty floor (<c>security@localhost</c>) is used only when the
/// instance has not set a support email (a fresh / dev instance), so the field
/// is **never** blank (RFC 9116 requires <c>Contact</c>).
/// </para>
/// <para>
/// **No auth, no new seam:** anonymous by default (the app has no global
/// authorization filter — <c>Program.cs</c> registers no
/// <c>RequireAuthenticatedUser</c>, the <c>StaticPagesController</c> routes are
/// likewise unauthenticated readers). One action, no DI dependency beyond
/// <see cref="CommunityOptions"/> (a Core type — ADR 0006, Web depends on Core).
/// </para>
/// </summary>
public sealed class WellKnownController : Controller
{
    private readonly IOptions<CommunityOptions> _community;

    public WellKnownController(IOptions<CommunityOptions> community)
    {
        _community = community;
    }

    /// <summary>
    /// <c>GET /.well-known/security.txt</c> (RFC 9116, OPS-05) — the
    /// machine-readable security.txt companion to the VDP
    /// (<c>docs/SECURITY-VDP.md</c>). Returns **200** with
    /// <c>text/plain</c> and the RFC 9116 fields (<c>Contact</c>,
    /// <c>Expires</c> — a future date, at least 12 months out,
    /// <c>Preferred-Languages</c>, <c>Policy</c>, <c>Hints</c>).
    /// </summary>
    [HttpGet("/.well-known/security.txt")]
    public ContentResult SecurityTxt()
    {
        // Contact — the instance's monitored support mailbox (the F12 channel),
        // emitted as an RFC 9116 mailto value. Config-driven so it is always the
        // live owner-monitored address; a stable floor keeps the field non-blank
        // when an unconfigured (fresh / dev) instance has no support email.
        var support = _community.Value.SupportEmail;
        var contact = string.IsNullOrWhiteSpace(support)
            ? "mailto:security@localhost"
            : $"mailto:{support}";

        // Policy — the canonical URL of the VDP. There is no separate served
        // VDP page, so the Policy points at this same security.txt route
        // (the F13 plan: "pointing at this same security.txt or a dedicated
        // page"); built from the request origin so it is a full, correct URL.
        var origin = $"{Request.Scheme}://{Request.Host}";
        var policy = $"{origin}/.well-known/security.txt";

        // Expires — a future date at least 12 months out (today is 2026-10-24;
        // 2027-12-01 is ~13 months out). RFC 9116 requires a future date.
        const string expires = "2027-12-01";

        var hints =
            "Resident- and operator-facing web surface of this self-hosted " +
            "single-community instance: identity/login/verification/MFA and " +
            "session handling; content, post/reply, group, event, project, " +
            "announcement and page stores; media upload and serving; messages, " +
            "notifications and the mail-out path. See the VDP (Policy above) " +
            "for full scope and the safe-harbor / no-reprisal terms.";

        // RFC 9116 is a line-based `Field: value` format — one field per line.
        var body = string.Join("\n",
            $"Contact: {contact}",
            $"Expires: {expires}",
            "Preferred-Languages: en",
            $"Policy: {policy}",
            $"Hints: {hints}",
            "");

        return Content(body, "text/plain");
    }
}
