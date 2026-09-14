using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// RC U03 — the content-image serving route (R·4). One action:
/// <c>GET /content-image/{id}</c>. The 5-step ordering (pinned in R·4):
/// <ol>
/// <li>Validate the id: 1–128 lowercase hex chars. Reject (400) anything else.</li>
/// <li><see cref="IMediaStore.GetAsync"/> → miss ⇒ <b>404</b> (zero audit rows).</li>
/// <li>Reverse lookup: post → reply → announcement → page. All null ⇒ <b>404</b>
/// (orphan is inert — R·4; no GlobalAdmin branch).</li>
/// <li>Branch by owner:
/// <ul>
/// <li>UGC post → exactly one <see cref="IAuthorizationService.CanAsync"/>
/// (Allow ⇒ serve; Deny ⇒ <b>404</b>, not 403 — the avatar precedent).</li>
/// <li>UGC reply → <b>404</b> (drift pause: no <c>PostReplyToAuditableResource</c>;
/// C-M3·1 — the reply's decision IS the parent post's, but resolving the parent
/// is outside U03's scope).</li>
/// <li>UGC announcement → <b>404</b> (drift pause: no
/// <c>AnnouncementToAuditableResource</c>; announcements use a flat role/scope
/// gate, not <c>CanAsync</c>).</li>
/// <li>Platform page → serve directly (public by construction — zero
/// <c>CanAsync</c>, zero audit rows).</li>
/// </ul></li>
/// <li><see cref="IMediaStore.OpenReadAsync"/> →
/// <c>File(stream, stored.ContentType)</c> + <c>X-Content-Type-Options: nosniff</c>
/// (the avatar idiom, verbatim).</li>
/// </ol>
/// No <c>[Authorize]</c> on the route (the authorization is the per-owner
/// <c>CanAsync</c>, the avatar idiom — an anonymous visitor to a public
/// post's image must be served; a non-member to a restricted post's image
/// gets 404 + one Deny audit row, R·3/R·4).
/// </summary>
public sealed class ContentImageController(
    IMediaStore media,
    IAuthorizationService authz,
    PostService posts,
    IAnnouncementService announcements,
    ITranslationProvider pages) : Controller
{
    /// <summary>
    /// <c>GET /content-image/{id}</c> — the single serving action. The id
    /// is a <c>MediaObject.Id</c> (lowercase-hex SHA-256 of the payload).
    /// The 5-step ordering is pinned in R·4; see the class doc-comment.
    /// </summary>
    [HttpGet("/content-image/{id}")]
    public async Task<IActionResult> Serve([FromRoute] string id)
    {
        // ── Step 1: validate the id (1–128 lowercase hex) ──────────────
        if (!IsValidMediaId(id)) return BadRequest();

        // ── Step 2: store miss → 404 (zero audit rows) ────────────────
        var stored = await media.GetAsync(id);
        if (stored is null) return NotFound();

        // ── Step 3: reverse lookup in owner order (post → reply → announcement → page) ──
        var post = await posts.FindPostByImageIdAsync(id);
        if (post is not null)
        {
            // ── Step 4 (UGC post): one CanAsync (R·4) ──────────────────
            var actorId = KumunitaPrincipal.SubjectId(User) ?? "";
            var decision = await authz.CanAsync(
                actorId, AccessAction.Read, new PostToAuditableResource(post));
            if (!decision.Allowed) return NotFound(); // Deny → 404 (not 403)
            // ── Step 5: serve ─────────────────────────────────────────
            var stream = await media.OpenReadAsync(id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, stored.ContentType);
        }

        var reply = await posts.FindReplyByImageIdAsync(id);
        if (reply is not null)
        {
            // Drift pause: no PostReplyToAuditableResource exists (C-M3·1 —
            // the reply's decision IS the parent post's, but resolving the
            // parent is outside U03's scope). Fail-closed 404, zero audit
            // rows (no CanAsync called).
            return NotFound();
        }

        var announcement = await announcements.FindByImageIdAsync(id);
        if (announcement is not null)
        {
            // Drift pause: no AnnouncementToAuditableResource exists
            // (announcements use a flat role/scope gate, not CanAsync).
            // Fail-closed 404, zero audit rows (no CanAsync called).
            return NotFound();
        }

        var page = await pages.FindPageByImageIdAsync(id);
        if (page is not null)
        {
            // ── Step 4 (platform page): public by construction (R·4) ───
            // ── Step 5: serve (zero CanAsync, zero audit rows) ─────────
            var stream = await media.OpenReadAsync(id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, stored.ContentType);
        }

        // ── All null → orphan → 404 (zero audit rows, R·4) ─────────────
        return NotFound();
    }

    /// <summary>
    /// Validates the route id: 1–128 lowercase hex chars (<c>[0-9a-f]</c>).
    /// The same character test the renderer's <c>IsSafeImageSrc</c> accepts
    /// (RC R·3 — the route-shaped id); the two are independent layers by
    /// design (the plan's entry read 5: "duplicate the 8-line check in the
    /// controller rather than making it public").
    /// </summary>
    private static bool IsValidMediaId(string id)
    {
        if (id.Length is < 1 or > 128) return false;
        foreach (var c in id)
        {
            if (c is >= '0' and <= '9' or >= 'a' and <= 'f') continue;
            return false;
        }
        return true;
    }
}
