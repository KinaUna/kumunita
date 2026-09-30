using System.Security.Claims;
using Kumunita.Core.Bookmarks;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/bookmarks</c> surface (M17 — ADR 0118: the personal pin surface
/// for the five closed <see cref="Bookmark.TargetKind"/>s — <c>post</c> /
/// <c>event</c> / <c>todo</c> / <c>announcement</c> / <c>page</c>). A *thin*
/// HTTP layer (ADR 0006-D: routes + authz + shape): every access decision
/// comes from the frozen <see cref="IBookmarkService"/> seam (the single
/// decision path — C-M17·1: the seam composes only frozen seams, no new
/// decision of its own). The M16 <see cref="InventoryController"/> precedent
/// for the route / authz / 404-vs-403 split, with the M17 D2 personal-read
/// shape (a bookmark is a personal-by-id record, never an audience decision).
/// <list type="bullet">
/// <item><c>GET /bookmarks</c> — the owner's own list (the D2 personal read,
/// C-M17·2: zero <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c> passes —
/// the ADR 0105 / M6-inbox "operator has no read standing" precedent).
/// The caller's <see cref="KumunitaPrincipal.SubjectId"/> is the
/// <c>ownerId</c>; a caller with no read standing (no <c>SubjectId</c>)
/// gets a non-leaky 404 (C-M17·2 — the "operator has no read standing"
/// gate; that 404 commits no <c>AccessAudit</c> row). The seam's
/// <c>ListAsync(ownerId)</c> is the sole reader (C-M17·1 — the controller
/// never re-derives access).</item>
/// <item><c>POST /bookmarks/{targetKind}/{targetId}/remove</c> — the
/// per-row unbookmark control (the D5 pin: unbookmark on a **degraded** row
/// still works — the row is keyed on the owner's own <c>Bookmark</c> row,
/// not the target's id, so the unbookmark needs no target read at all —
/// D5 / F4). The seam's <c>RemoveAsync</c> is the sole writer; the
/// controller commits in the caller's <c>IDocumentSession</c> (the C3
/// same-transaction lane — <see cref="BookmarkService"/> never commits
/// internally; the U02 <c>RunInSession</c> harness witnesses the
/// caller-commits shape), then redirects back to <c>/bookmarks</c> (the
/// "redirect after write" precedent).</item>
/// </list>
/// <para>
/// **D2 / C-M17·2:** a bookmark is a personal-by-id record — a
/// <c>GlobalAdmin</c> included — gets a non-leaky 404 (the Web layer's
/// owner-check), and the owner's own read is a **personal read** that
/// **never audits**: zero <c>AccessAudit</c> rows, zero
/// <c>CanSeeAsync</c> passes (C-M17·2 — the design doc §2.2 verbatim).
/// The <see cref="AuthorizeAttribute"/> is a convenience pre-gate only —
/// never the source of truth (the <c>SubjectId</c> check is the gate;
/// the seam's <c>ListAsync</c> is the sole reader).
/// </para>
/// <para>
/// **D6 / C-M17·6:** M17 is a standing core surface (no off-by-default
/// toggle) — the views are always available to the community.
/// </para>
/// </summary>
[Authorize]
public sealed class BookmarksController(
    IBookmarkService bookmarks,
    IDocumentStore store) : Controller
{
    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /bookmarks</c> — the owner's own list (the D2 personal read,
    /// C-M17·2: zero <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c>
    /// passes — the ADR 0105 / M6-inbox "operator has no read standing"
    /// precedent). The caller's <see cref="KumunitaPrincipal.SubjectId"/>
    /// is the <c>ownerId</c>; a caller with no read standing (no
    /// <c>SubjectId</c>) gets a non-leaky 404 (C-M17·2 — the "operator
    /// has no read standing" gate; that 404 commits no
    /// <c>AccessAudit</c> row). The seam's <c>ListAsync(ownerId)</c> is
    /// the sole reader (C-M17·1 — the controller never re-derives access);
    /// the view receives the <see cref="BookmarkListResult"/> verbatim
    /// (the grouped rows, the D5 degraded flag).
    /// </summary>
    [HttpGet("/bookmarks")]
    public async Task<IActionResult> Index()
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // C-M17·2 — the "operator has no read standing" gate (the
            // ADR 0105 precedent): a caller with no <c>SubjectId</c> has
            // no read standing over any bookmark list. A non-leaky 404 —
            // **no** <c>AccessAudit</c> row is committed, and the seam's
            // <c>ListAsync</c> is **not** called (the sole-reader pin —
            // C-M17·1: the controller never re-derives access, and a
            // caller with no read standing has no read to delegate).
            return NotFound();
        }

        // C-M17·1 / C-M17·2 — the seam is the sole reader (the personal
        // read, zero audit rows). The controller never re-derives access;
        // the seam's <c>ListAsync</c> loads the owner's own rows by the
        // identity predicate <c>OwnerId == ownerId</c>.
        var result = await bookmarks.ListAsync(actorId);
        return View(result);
    }

    /// <summary>
    /// <c>POST /bookmarks/{targetKind}/{targetId}/remove</c> — the
    /// per-row unbookmark control (the D5 pin: unbookmark on a **degraded**
    /// row still works — the row is keyed on the owner's own
    /// <see cref="Bookmark"/> row, not the target's id, so the unbookmark
    /// needs no target read at all — D5 / F4). The seam's
    /// <see cref="IBookmarkService.RemoveAsync"/> is the sole writer; the
    /// controller commits in the caller's <see cref="IDocumentSession"/>
    /// (the C3 same-transaction lane — <see cref="BookmarkService"/> never
    /// commits internally; the U02 <c>RunInSession</c> harness witnesses
    /// the caller-commits shape), then redirects back to
    /// <c>/bookmarks</c> (the "redirect after write" precedent — the M16
    /// <c>InventoryController</c> / M5 <c>PageController</c> shape).
    /// </summary>
    [HttpPost("/bookmarks/{targetKind}/{targetId}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string targetKind, string targetId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // C-M17·2 — the "operator has no read standing" gate (the
            // ADR 0105 precedent): a caller with no <c>SubjectId</c> has
            // no standing to remove any bookmark row. A non-leaky 404 —
            // **no** <c>AccessAudit</c> row is committed, and the seam's
            // <c>RemoveAsync</c> is **not** called (the sole-writer pin —
            // C-M17·1: the controller never re-derives access).
            return NotFound();
        }

        // C3 same-transaction lane: the service's <c>Store</c> /
        // <c>Delete</c> writes into the caller's in-flight session
        // (the U02 <c>RunInSession</c> shape); the controller commits
        // (the <c>BookmarkService</c> never calls
        // <c>SaveChangesAsync</c> internally — the caller owns the
        // single write).
        await using var session = store.LightweightSession();
        await bookmarks.RemoveAsync(actorId, targetKind, targetId, session);
        await session.SaveChangesAsync();
        return Redirect("/bookmarks");
    }
}
