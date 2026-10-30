using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// RC U03 — the content-image serving route (R·4). One action:
/// <c>GET /content-image/{id}</c>. The 5-step ordering (pinned in R·4):
/// <ol>
/// <li>Validate the id: 1–128 lowercase hex chars. Reject (400) anything else.</li>
/// <li><see cref="IMediaStore.GetAsync"/> → miss ⇒ <b>404</b> (zero audit rows).</li>
/// <li>Reverse lookup: post → reply → announcement. All null ⇒ <b>404</b>
/// (orphan is inert — R·4; no GlobalAdmin branch).</li>
/// <li>Branch by owner:
/// <ul>
/// <li>UGC post → exactly one <see cref="IAuthorizationService.CanAsync"/>
/// (Allow ⇒ serve; Deny ⇒ <b>404</b>, not 403 — the avatar precedent).</li>
/// <li>UGC reply → resolve the parent post and authorize against it with one
/// <see cref="IAuthorizationService.CanAsync"/> (the reply's decision IS the parent
/// post's, C-M3·1); Deny ⇒ <b>404</b> (not 403) + one <c>Deny</c> audit row.</li>
/// <li>UGC announcement → serve when the caller passes the announcement's flat
/// scope/communities read gate (<see cref="IAnnouncementService.GetAsync"/>),
/// else <b>404</b> (no <c>CanAsync</c> + no <c>AccessAudit</c> row — announcements
/// are not audience-restricted; the flat gate is the whole decision).</li>
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
    IOptions<MediaOptions> mediaOpts,
    Marten.IDocumentStore store,
    // F16 / UPL-02 — the serve-lane virus-scan seam (the "widen the ctor as
    // the lane needs it" shape). The serve action runs the gate (short-circuit
    // / scan / quarantine + 404); the upload lane is untouched. Optional
    // (default null) so the existing test-construction sites keep compiling;
    // a null scanner is the fail-open floor (pin §2.1) — the serve proceeds
    // unchanged (DI always supplies the live ClamAvScanner in the app).
    IVirusScanner? scanner = null) : Controller
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

        // ── F16 / UPL-02: the quarantine gate (serve lane is the
        // enforcement point, pin §4/§5). Before the reverse-lookup /
        // Read decision — a malware byte is refused for everyone, 404, zero
        // audit rows (the no-leak posture; the store-miss above is the same). ──
        if (await IsQuarantinedOrInfectedAsync(id, stored, CancellationToken.None))
            return NotFound();

        // ── Step 3: reverse lookup in owner order (post → reply → announcement → page) ──
        var post = await posts.FindPostByImageIdAsync(id);
        if (post is not null)
        {
            // ── Step 4 (UGC post): one Read decision (R·4) ─────────────
            // Shared seam (Kumunita.Web.Security.PostReadDecision):
            // group-lane post (GroupId non-empty) → the membership lane
            // (ADR 0013 G·1/G·2/G·8); component post (GroupId empty) →
            // the audience lane (the M3 read decision). One audit row
            // (Allow or Deny) — emitted by the IAuthorizationService seam,
            // not this action.
            var actorId = KumunitaPrincipal.SubjectId(User) ?? "";
            var decision = await PostReadDecision.ResolveAsync(post, actorId, authz);
            if (!decision.Allowed) return NotFound(); // Deny → 404 (not 403)
            // ── Step 5: serve ─────────────────────────────────────────
            var stream = await media.OpenReadAsync(id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, stored.ContentType);
        }

        var reply = await posts.FindReplyByImageIdAsync(id);
        if (reply is not null)
        {
            // ── Step 4 (UGC reply, C-M3·1): resolve the parent post and
            // authorize against IT (the reply's decision IS the parent post's).
            // The parent load is a raw document read (no CanAsync, no audit
            // row). Fail-closed: a missing parent is an orphan → 404, zero
            // audit rows.
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
            // ── Step 5: serve ─────────────────────────────────────────
            var stream = await media.OpenReadAsync(id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, stored.ContentType);
        }

        var announcement = await announcements.FindByImageIdAsync(id);
        if (announcement is not null)
        {
            // UGC announcement — flat scope/communities gate (announcements are
            // not audience-restricted, so no CanAsync + no AccessAudit row — the
            // same "no audit row" reasoning as ListVisibleAsync / GetAsync). The
            // announcement read gate (GetAsync) is the sole serve/deny decision:
            // public scope always; community scope when signed in; a
            // community-targeted row when the caller is its GlobalAdmin, a member
            // of that community, or its moderator. Anonymous → public-scope only.
            // A null row (not visible to the caller) maps to 404 — not 403 — the
            // announcement lane's non-leaky posture.
            var visible = await announcements.GetAsync(
                announcement.Id, KumunitaPrincipal.SubjectId(User), KumunitaPrincipal.RoleSet(User));
            if (visible is null) return NotFound(); // not visible → 404 (no-leak)
            // ── Step 5: serve ─────────────────────────────────────────
            var stream = await media.OpenReadAsync(id);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(stream, stored.ContentType);
        }

        // ── All null → orphan → 404 (zero audit rows, R·4) ─────────────
        return NotFound();
    }

    /// <summary>
    /// <c>POST /content-image</c> — the content-image upload lane (RC R·6 —
    /// ADR 0011's boundary, **verbatim**: the same allowlist
    /// <c>image/jpeg|png|webp|gif</c> (SVG excluded), the same
    /// <see cref="MediaOptions.MaxBytes"/> cap (10 MiB default), the same
    /// guards-before-write ordering — empty → <b>400</b>, oversize →
    /// <b>413</b>, disallowed type → <b>415</b> — **no file written on any
    /// guard** — then one <see cref="IMediaStore.PutAsync"/> write. The
    /// <c>IFormFile</c> boundary is Web-only (R·5 — Core stays HTTP-free).
    /// **No audit call**: the write is authenticated, not an
    /// audience-restricted read (the avatar lane makes the same choice).
    /// </summary>
    [HttpPost("/content-image")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [EnableRateLimiting("upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null)
            return Unauthorized(); // defensive (the [Authorize] already gates)

        // The guards run BEFORE any Put (R·6, verbatim from the avatar lane —
        // a Put with a disallowed type or an oversize payload would write a
        // volume file that must not exist; M25 C-UP·2 guards-before-write):
        if (file is null || file.Length == 0)
            return BadRequest(await this.FlashAsync("validation.choose_image", "Choose an image."));                          // empty → 400 (untouched)
        // M25 (U8) — the size/over-quota guard is now the Web-only
        // IUploadGate (C-UP·3): the single 413 producer, running the pure Core
        // StorageLimits.Decide over the admin doc + the subject's usage. Runs
        // BEFORE PutAsync, so a reject writes no byte (C-UP·2). Allowlist +
        // empty-file checks untouched, same positions.
        var requestServices = HttpContext.RequestServices;
        var settingsSvc = requestServices.GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>();
        var uploadGate  = requestServices.GetRequiredService<IUploadGate>();
        var settings = await settingsSvc.GetOrCreateAsync(CancellationToken.None);
        var reject = await uploadGate.CheckUpload(file.Length, subject, settings, mediaOpts.Value.MaxBytes, mediaOpts.Value.MaxPlatformBytes);
        if (reject is not null) return reject;                              // oversize/over-quota → 413 (the gate)
        if (!mediaOpts.Value.IsAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);   // disallowed type (incl. SVG) → 415 (untouched)

        // R·5: the IFormFile never crosses into Core — copy to bytes, then
        // store-first (orphan-safe order, C-MED·7):
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var stored = await media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);

        // The id is a content hash, not secret; the route 404s until a
        // referencing doc exists (R·4). No audit row (the write is
        // authenticated, not an audience-restricted read).
        return Json(new { id = stored.Id });
    }

    /// <summary>
    /// F16 / UPL-02 — the serve-lane quarantine gate (pin §4/§5). Returns
    /// <c>true</c> when the byte must be <b>refused with 404</b>: either it is
    /// already quarantined (idempotent short-circuit — no re-scan, no re-write,
    /// zero audit rows), or the scanner flags the payload <see
    /// cref="VirusScanStatus.Infected"/> (set <see cref="MediaObject.Quarantined"/>
    /// + one <c>SaveChangesAsync</c>, C3 single-write-lane). A
    /// <see cref="VirusScanStatus.Clean"/> verdict (incl. the fail-open
    /// unavailable-daemon default, pin §2.1) returns <c>false</c> — the serve
    /// proceeds unchanged. The scanner re-uses the serve lane's own
    /// <see cref="IMediaStore.OpenReadAsync"/> read seam (no new byte-read seam).
    /// </summary>
    private async Task<bool> IsQuarantinedOrInfectedAsync(string id, MediaObject stored, CancellationToken ct)
    {
        if (stored.Quarantined) return true;               // idempotent short-circuit (no re-scan, §3)

        // Fail-open floor (pin §2.1): a box / test-construction site without a
        // scanner degrades to Clean — the serve proceeds unchanged (never a
        // crash, never a false-positive quarantine).
        if (scanner is null) return false;

        VirusScanStatus verdict = VirusScanStatus.Clean;
        try
        {
            await using var scanStream = await media.OpenReadAsync(id, ct);
            verdict = await scanner!.ScanAsync(scanStream, ct);
        }
        catch { /* fail-open: a scanner fault degrades to Clean (§2.1), never a crash */ }

        if (verdict is VirusScanStatus.Infected)
        {
            stored.Quarantined = true;                     // quarantine (persist, §3)
            await using var s = store.LightweightSession();
            s.Store(stored);
            await s.SaveChangesAsync(ct);                  // one commit (C3 single-write-lane, ADR 0006)
            return true;                                   // refuse with 404 (no-leak, §4)
        }
        return false;                                       // Clean → serve proceeds unchanged
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
