using Kumunita.Core.Media;
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
    IOptions<MediaOptions> mediaOpts) : Controller
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
}
