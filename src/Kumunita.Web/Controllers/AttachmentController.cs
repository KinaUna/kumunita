using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// ATT U8 — the file-attachment upload route (C-ATT·1/3/6), mirroring
/// <c>ContentImageController</c>'s upload action. One action so far:
/// <c>POST /attachment</c>. The serve route (<c>GET /attachment/{id}</c>)
/// lands in U9 on **this same controller** (the constructor is deliberately
/// minimal — <see cref="IMediaStore"/> + <see cref="MediaOptions"/> — so U9
/// adds <c>Serve</c> and, if it needs owner-resolution services, widens the
/// constructor then).
///
/// The C-ATT·6 boundary, **verbatim** from the image upload lane (ADR 0011):
/// the **attachment** allowlist
/// (<see cref="MediaOptions.AttachmentAllowedContentTypes"/> — a **separate**
/// gate from the image lane's raster-only <see cref="MediaOptions.AllowedContentTypes"/>),
/// the same <see cref="MediaOptions.MaxBytes"/> cap (5 MiB default — reused,
/// not a second size cap), the same guards-before-write ordering — empty →
/// <b>400</b>, oversize → <b>413</b>, disallowed type → <b>415</b> — **no
/// file written on any guard** — then one <see cref="IMediaStore.PutAsync"/>
/// write. SVG is **excluded** from the default allowlist (C-ATT·6); the raster
/// image types are **included** so a photo can be attached *as a download*.
/// </summary>
public sealed class AttachmentController(
    IMediaStore media,
    IOptions<MediaOptions> mediaOpts,
    IAuthorizationService authz,
    PostService posts,
    IAnnouncementService announcements,
    Marten.IDocumentStore store) : Controller
{
    /// <summary>
    /// <c>POST /attachment</c> — the file-attachment upload lane (C-ATT·6 —
    /// ADR 0011's boundary, **verbatim**: the **attachment** allowlist
    /// <c>application/pdf|…|image/gif</c> (SVG excluded, raster included), the
    /// same <see cref="MediaOptions.MaxBytes"/> cap, the same guards-before-write
    /// ordering — empty → <b>400</b>, oversize → <b>413</b>, disallowed type →
    /// <b>415</b> — **no file written on any guard** — then one
    /// <see cref="IMediaStore.PutAsync"/> write. The <c>IFormFile</c> boundary is
    /// Web-only (C-ATT·4 — Core stays HTTP- and body-parse-free).
    /// <c>PutAsync</c> is the **existing** 4-arg seam (C-ATT·3 — unchanged; the
    /// trailing <c>CancellationToken</c> is left to its default). The write is to
    /// the **same** <see cref="IMediaStore"/> + volume as the image lane
    /// (C-ATT·1 — one store, one catalog).
    /// **No audit row**: the write is authenticated, not an audience-restricted
    /// read (the image lane makes the same choice — carried over).
    /// </summary>
    [HttpPost("/attachment")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null)
            return Unauthorized(); // defensive (the [Authorize] already gates)

        // The three guards run BEFORE any Put (C-ATT·6, F6 — a Put with a
        // disallowed type or an oversize payload would write a volume file that
        // must not exist):
        if (file is null || file.Length == 0)
            return BadRequest("Choose a file.");                             // empty → 400
        if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge);   // oversize → 413
        if (!mediaOpts.Value.IsAttachmentAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);    // disallowed type (incl. SVG) → 415

        // C-ATT·4: the IFormFile never crosses into Core — copy to bytes, then
        // store-first (orphan-safe order, C-MED·7) via the **existing** 4-arg
        // PutAsync (C-ATT·3 — the trailing CancellationToken defaults):
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var stored = await media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);

        // The id is a content hash, not secret; the serve route (U9) 404s until
        // a referencing doc exists (C-ATT·2/7). No audit row (the write is
        // authenticated, not an audience-restricted read).
        return Json(new { id = stored.Id });
    }

    /// <summary>
    /// ATT U9 — the file-attachment serving route (<c>GET /attachment/{id}</c>).
    /// Mirrors <c>ContentImageController.Serve</c>'s 5-step ordering
    /// (design doc §2.7, C-ATT·7) **with two deliberate differences** (the whole
    /// point of the lane):
    /// <ol>
    /// <li><b>Reply branch resolves the parent post</b> (C-ATT·8) and authorizes
    /// against it with one <see cref="IAuthorizationService.CanAsync"/>, instead
    /// of the image lane's flat 404 drift pause — a reply attachment is visible
    /// iff its <b>parent post</b> is (the single <c>Read</c> decision, C-M3·1).
    /// The image lane's reply branch (drift pause) is **not** copied (C-ATT·8/F4).</li>
    /// <li><b><c>Content-Disposition: attachment</c></b> (C-ATT·2 — a download,
    /// not an inline render): the stored <c>MediaObject.Filename</c> sanitized
    /// per RFC 6266 (fallback <c>{id}.bin</c>), plus
    /// <c>X-Content-Type-Options: nosniff</c> + the <b>stored</b>
    /// <c>Content-Type</c> (already validated at write — no re-check). The image
    /// lane omits <c>Content-Disposition</c> and renders inline.</li>
    /// </ol>
    /// 5-step ordering (frozen, C-ATT·7): (1) validate id → 400; (2)
    /// <see cref="IMediaStore.GetAsync"/> miss ⇒ 404 (zero audit rows);
    /// (3) reverse-lookup post → reply → announcement (no static-page branch
    /// — attachments are not on static pages this pass); all null ⇒ 404
    /// (orphan, zero audit rows); (4) per-owner decision
    /// — post: one <see cref="IAuthorizationService.CanAsync"/>, Deny ⇒ 404
    /// (not 403) + exactly one <c>Deny</c> audit row; reply: load the parent
    /// post → one <see cref="IAuthorizationService.CanAsync"/>, Deny ⇒ 404 + one
    /// <c>Deny</c> row (C-ATT·8); announcement: the flat scope gate
    /// (<see cref="IAnnouncementService.GetAsync"/>), null ⇒ 404, no
    /// <see cref="IAuthorizationService.CanAsync"/>, zero rows; (5)
    /// <see cref="IMediaStore.OpenReadAsync"/> →
    /// <c>File(stream, stored.ContentType)</c> + the two serve headers above.
    /// Audit contract (C-ATT·10): exactly <b>one</b> <c>Deny</c> row on a UGC
    /// (post/reply) Deny — emitted <b>by</b> the single
    /// <see cref="IAuthorizationService.CanAsync"/>, not by this action; zero
    /// rows on every other 404 path (invalid id, store miss, orphan,
    /// announcement scope-deny). No <c>[Authorize]</c> (the image lane's choice
    /// — the authorization is the per-owner <c>CanAsync</c>; an anonymous
    /// visitor to a public post's attachment must be served).
    /// The image lane (<see cref="ContentImageController"/>) is untouched
    /// (C-ATT·9) — this is a parallel action on this controller, not a copy of
    /// its reply branch.
    /// </summary>
    [HttpGet("/attachment/{id}")]
    public async Task<IActionResult> Serve([FromRoute] string id)
    {
        // ── Step 1: validate the id (1–128 lowercase hex) ──────────────
        if (!IsValidMediaId(id)) return BadRequest();

        // ── Step 2: store miss → 404 (zero audit rows) ────────────────
        var stored = await media.GetAsync(id);
        if (stored is null) return NotFound();

        // ── Step 3: reverse lookup in owner order (post → reply → announcement) ──
        var post = await posts.FindPostByAttachmentIdAsync(id);
        if (post is not null)
        {
            // ── Step 4 (UGC post): one Read decision (C-ATT·7) ─────────
            // Shared seam (Kumunita.Web.Security.PostReadDecision):
            // group-lane post (GroupId non-empty) → the membership lane
            // (ADR 0013 G·1/G·2/G·8); component post (GroupId empty) →
            // the audience lane (the M3 read decision). One audit row
            // (Allow or Deny) — emitted by the IAuthorizationService seam,
            // not this action.
            var actorId = KumunitaPrincipal.SubjectId(User) ?? "";
            var decision = await PostReadDecision.ResolveAsync(post, actorId, authz);
            if (!decision.Allowed) return NotFound(); // Deny → 404 (not 403) + one Deny row (by the decision)
            // ── Step 5: serve (C-ATT·2 download) ───────────────────────
            return await ServeFile(id, stored);
        }

        var reply = await posts.FindReplyByAttachmentIdAsync(id);
        if (reply is not null)
        {
            // ── Step 4 (UGC reply, C-ATT·8 — the deliberate difference): ──
            // resolve the parent post and authorize against IT (the image
            // lane's reply branch is the flat 404 drift pause — NOT copied).
            // The parent load is a raw document read (no CanAsync, no audit
            // row) — see the handoff note for the seam used. Fail-closed: a
            // missing parent is an orphan → 404, zero audit rows.
            Post? parent = null;
            await using (var s = store.QuerySession())
            {
                parent = await s.LoadAsync<Post>(reply.PostId);
            }
            if (parent is null) return NotFound(); // orphan reply → 404 (zero rows)

            var actorId = KumunitaPrincipal.SubjectId(User) ?? "";
            // Group-lane parent (GroupId non-empty) → the membership lane
            // (ADR 0013 G·1/G·2/G·8); component parent (GroupId empty) →
            // the audience lane (the M3 read decision). The shared seam is
            // PostReadDecision.ResolveAsync — one call, one audit row.
            var decision = await PostReadDecision.ResolveAsync(parent, actorId, authz);
            if (!decision.Allowed) return NotFound(); // Deny → 404 (not 403) + one Deny row (by the decision)
            // ── Step 5: serve (C-ATT·2 download) ───────────────────────
            return await ServeFile(id, stored);
        }

        var announcement = await announcements.FindByAttachmentIdAsync(id);
        if (announcement is not null)
        {
            // UGC announcement — flat scope/communities gate (announcements are
            // not audience-restricted, so no CanAsync + no AccessAudit row — the
            // same reasoning as the image lane's announcement branch). A null row
            // (not visible to the caller) maps to 404 — not 403 — the
            // announcement lane's non-leaky posture.
            var visible = await announcements.GetAsync(
                announcement.Id, KumunitaPrincipal.SubjectId(User), KumunitaPrincipal.RoleSet(User));
            if (visible is null) return NotFound(); // not visible → 404 (no-leak, zero rows)
            // ── Step 5: serve (C-ATT·2 download) ───────────────────────
            return await ServeFile(id, stored);
        }

        // ── All null → orphan → 404 (zero audit rows, C-ATT·7) ─────────
        return NotFound();
    }

    /// <summary>
    /// The shared serve block (C-ATT·2): opens the stored stream, sets the
    /// <b>download</b> headers (the one serve difference from the image lane,
    /// which omits <c>Content-Disposition</c> and renders inline), and returns
    /// the file with the **stored** <c>Content-Type</c> (already validated at
    /// write — no second allowlist check at read, C-ATT·3/9).
    /// </summary>
    private async Task<IActionResult> ServeFile(string id, MediaObject stored)
    {
        var stream = await media.OpenReadAsync(id);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var filename = SanitizeFilename(stored.Filename, id);
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);
        return File(stream, stored.ContentType);
    }

    /// <summary>
    /// RFC 6266 sanitization of the original <c>MediaObject.Filename</c>
    /// (the source for <c>Content-Disposition: attachment; filename=…</c>):
    /// null/whitespace → the content-hash fallback <c>{id}.bin</c>; otherwise
    /// strip the prohibited set (path separators <c>/</c> and <c>\</c>, the
    /// header terminators <c>";</c> / <c>"</c> / <c>\r</c> / <c>\n</c>, and
    /// other control chars) so a stored name can't inject a header or a path.
    /// No new library — a simple character filter (C-ATT·2).
    /// </summary>
    private static string SanitizeFilename(string? original, string id)
    {
        if (string.IsNullOrWhiteSpace(original))
            return id + ".bin";
        var cleaned = new System.Text.StringBuilder(original.Length);
        foreach (var c in original)
        {
            if (c < ' ' || c == '"' || c == '\\' || c == '/' || c == ';' || c == ':' || c == '<' || c == '>' || c == '?' || c == '|')
                continue;
            cleaned.Append(c);
        }
        return cleaned.Length == 0 ? id + ".bin" : cleaned.ToString();
    }

    /// <summary>
    /// Validates the route id: 1–128 lowercase hex chars (<c>[0-9a-f]</c>).
    /// The same character test the renderer's <c>IsSafeUrl</c> accepts for the
    /// route-shaped id; the two are independent layers by design (the unit
    /// plan's instruction: duplicate the 8-line check in this controller rather
    /// than making the image controller's copy public — C-ATT·9).
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
