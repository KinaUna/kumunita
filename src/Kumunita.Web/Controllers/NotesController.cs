using System.Linq;
using System.Security.Claims;
using Kumunita.Core.Localization;
using Kumunita.Core.PersonalNotes;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/notes</c> Web surface, part 1 (M37 — ADR 0167: the personal-note
/// surface — a private body of the resident's own writing attached to a
/// target, author-only). A *thin* HTTP layer (ADR 0006-D: routes + authz +
/// shape): every access decision comes from the frozen
/// <see cref="IPersonalNoteService"/> seam (the single decision path —
/// C-M37·1: the seam composes only frozen seams, no new decision of its own).
/// The M17 <see cref="BookmarksController"/> precedent for the route / authz /
/// 404-vs-403 split, with the M37 D2 personal-read shape (a note is a
/// personal-by-id record, never an audience decision).
/// <list type="bullet">
/// <item><c>GET /notes</c> — the owner's own note list (the D2 personal
/// read, C-M37·2: zero <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c>
/// passes — the ADR 0105 "operator has no read standing" precedent). The
/// caller's <c>SubjectId</c> is the <c>ownerId</c>; a caller with no read
/// standing (no <c>SubjectId</c> — a <c>GlobalAdmin</c> included) gets a
/// non-leaky **404** (C-M37·2 — that 404 commits no <c>AccessAudit</c>
/// row). The seam's <c>ListNotesAsync(ownerId)</c> is the sole reader
/// (C-M37·1 — the controller never re-derives access); the view receives
/// the <c>NoteListResult</c> verbatim (the groups keyed by the closed
/// <c>TargetKind</c> set, the owner's folder tree, the D7 degraded rows —
/// design doc §2.2 / §2.4).</item>
/// <item><c>POST /notes/attach</c> — the D3 write-lane (the
/// <see cref="IPersonalNoteService.CreateNoteAsync"/> seam re-runs the
/// target's own frozen <c>CanAsync(Read)</c> before the row is created;
/// the controller maps the seam's <c>KeyNotFoundException</c> — thrown when
/// the target is invisible, absent, or soft-deleted — to a non-leaky **404**
/// (never 403 — the M16 create-gate posture: the Deny row does not survive,
/// and no note row is committed). A successful call returns **200** (the
/// frozen <c>Task&lt;NoteRow&gt;</c> return does not distinguish
/// "freshly created" from "already noted" — the F1 no-op and the F1 fresh
/// create both produce a <c>NoteRow</c>; the drift is recorded in the U04
/// handoff note). The controller commits in its own fresh
/// <see cref="IDocumentSession"/> (the C3 same-transaction lane —
/// <see cref="PersonalNoteService"/> never commits internally; the caller
/// owns the single write — the M17 <c>BookmarkService</c> shape), then
/// redirects back to the <c>returnUrl</c> form field (the surface's own
/// detail URL) when present, falling back to <c>/notes</c> when it is
/// absent (the "redirect after write" precedent — the M16
/// <c>InventoryController</c> / M17 <c>BookmarksController</c> shape).</item>
/// <item><c>GET /notes/has/{targetKind}/{targetId}</c> — the F1 witness
/// (the "noted" state on reload): the controller calls
/// <see cref="IPersonalNoteService.HasNoteAsync"/> with the actor's
/// <c>SubjectId</c> as the <c>ownerId</c>. A caller with no read standing
/// (no <c>SubjectId</c> — a <c>GlobalAdmin</c> included) gets a non-leaky
/// **404** (the C-M37·2 pin — the "operator has no read standing" gate;
/// that 404 commits no <c>AccessAudit</c> row, and the seam's
/// <c>HasNoteAsync</c> is **not** called — the sole-reader pin, C-M37·1).
/// A caller with a <c>SubjectId</c> gets a <c>200</c> JSON
/// <c>{"has": true|false}</c> — the <c>HasNoteAsync</c> call is a pure
/// existence check over the owner's own row (C-M37·2: zero
/// <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c> passes, no target
/// read — the ADR 0105 "operator has no read standing" precedent).</item>
/// </list>
/// <para>
/// **D2 / C-M37·2:** a personal note is a personal-by-id record — a
/// <c>GlobalAdmin</c> included — gets a non-leaky 404 (the Web layer's
/// owner-check: no <c>SubjectId</c> ⇒ no read standing), and the owner's
/// own read is a **personal read** that **never audits**: zero
/// <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c> passes. The
/// <see cref="AuthorizeAttribute"/> is a convenience pre-gate only — never
/// the source of truth (the <c>SubjectId</c> check is the gate; the seam's
/// <c>HasNoteAsync</c> / <c>CreateNoteAsync</c> are the sole readers /
/// writers).
/// </para>
/// <para>
/// **D8 / C-M37·7:** M37 is a standing core surface (no off-by-default
/// toggle) — the surface is always available to the community.
/// </para>
/// </summary>
[Authorize]
public sealed class NotesController(
    IPersonalNoteService notes,
    IDocumentStore store,
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolves a <c>pn.note.*</c> kw-l key to the operator's
    /// effective language (the house <c>EffectiveLanguageCode.ResolveAsync</c>
    /// + <c>ITranslationProvider.GetAsync</c> seam — the same as the view's
    /// resolution, so the flash toast renders in the operator's language).
    /// Falls back to the raw key when the translation seam is absent
    /// (the test-construction floor — the tests assert the raw key).
    /// </summary>
    private async Task<string> T(string key)
    {
        if (translationProvider is null || localization is null)
            return key; // the test floor (no context — the raw key)
        var lang = await EffectiveLanguageCode.ResolveAsync(
            HttpContext?.Request, localization, translationProvider);
        return await translationProvider.GetAsync(key, lang);
    }

    /// <summary>
    /// <c>GET /notes</c> — the owner's own note list (the D2 personal read,
    /// C-M37·2: zero <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c>
    /// passes — the ADR 0105 / M6-inbox "operator has no read standing"
    /// precedent). The caller's <see cref="KumunitaPrincipal.SubjectId"/> is
    /// the <c>ownerId</c>; a caller with no read standing (no
    /// <c>SubjectId</c>) gets a non-leaky 404 (C-M37·2 — the "operator has
    /// no read standing" gate; that 404 commits no <c>AccessAudit</c> row).
    /// The seam's <c>ListNotesAsync(ownerId)</c> is the sole reader
    /// (C-M37·1 — the controller never re-derives access); the view
    /// receives the <see cref="NoteListResult"/> verbatim (the groups
    /// keyed by the closed <c>TargetKind</c> set, the owner's folder tree,
    /// the D7 degraded rows — design doc §2.2 / §2.4).
    /// <para>
    /// **D2 / C-M37·2:** the <see cref="AuthorizeAttribute"/> on the class
    /// is a convenience pre-gate only — never the source of truth (the
    /// <c>SubjectId</c> check is the gate; the seam's <c>ListNotesAsync</c>
    /// is the sole reader).
    /// </para>
    /// </summary>
    [HttpGet("/notes")]
    public async Task<IActionResult> Index()
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // C-M37·2 — the "operator has no read standing" gate (the ADR
            // 0105 precedent): a caller with no <c>SubjectId</c> has no
            // read standing over any note list. A non-leaky 404 — **no**
            // <c>AccessAudit</c> row is committed, and the seam's
            // <c>ListNotesAsync</c> is **not** called (the sole-reader pin —
            // C-M37·1: the controller never re-derives access, and a caller
            // with no read standing has no read to delegate).
            return NotFound();
        }

        // C-M37·1 / C-M37·2 — the seam is the sole reader (the personal
        // read, zero audit rows). The controller never re-derives access;
        // the seam's <c>ListNotesAsync</c> loads the owner's own rows by
        // the identity predicate <c>OwnerId == ownerId</c> and resolves the
        // D7 degraded rows via a pure, non-auditing doc load. The view
        // receives the <see cref="NoteListResult"/> verbatim (the
        // grouped-by-<c>TargetKind</c> rows + the owner's folder tree).
        var result = await notes.ListNotesAsync(actorId);
        return View(result);
    }

    /// <summary>
    /// <c>POST /notes/attach</c> — the D3 write-lane (the
    /// <see cref="IPersonalNoteService.CreateNoteAsync"/> seam re-runs the
    /// target's own frozen <c>CanAsync(Read)</c> before the row is created;
    /// the controller maps the seam's <c>KeyNotFoundException</c> — thrown
    /// when the target is invisible, absent, or soft-deleted — to a
    /// non-leaky **404** (never 403 — the M16 create-gate posture: the Deny
    /// row does not survive, and no note row is committed). A successful
    /// call returns **200** (the frozen <c>Task&lt;NoteRow&gt;</c> return
    /// does not distinguish "freshly created" from "already noted" — the
    /// F1 no-op and the F1 fresh create both produce a <c>NoteRow</c>; the
    /// drift is recorded in the U04 handoff note — the plan intended a
    /// 200/201 split but the frozen interface does not carry that
    /// distinction). The controller commits in its own fresh
    /// <see cref="IDocumentSession"/> (the C3 same-transaction lane —
    /// <see cref="PersonalNoteService"/> never commits internally; the
    /// caller owns the single write — the M17 <c>BookmarkService</c> shape),
    /// then redirects back to the <c>returnUrl</c> form field (the surface's
    /// own detail URL) when present, falling back to <c>/notes</c> when it
    /// is absent (the "redirect after write" precedent — the M16
    /// <c>InventoryController</c> / M17 <c>BookmarksController</c> shape).
    /// </summary>
    [HttpPost("/notes/attach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Attach(
        string targetKind,
        string targetId,
        string? body = null,
        string? returnUrl = null)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // C-M37·2 — the "operator has no read standing" gate (the ADR
            // 0105 precedent): a caller with no <c>SubjectId</c> has no
            // standing to create a note row. A non-leaky 404 — **no**
            // <c>AccessAudit</c> row is committed, and the seam's
            // <c>CreateNoteAsync</c> is **not** called (the sole-writer pin —
            // C-M37·1: the controller never re-derives access).
            return NotFound();
        }

        // C3 same-transaction lane: the seam's store write goes into the
        // caller's in-flight session (the M17 <c>BookmarkService</c> shape);
        // the controller commits (the <c>PersonalNoteService</c> never
        // calls <c>SaveChangesAsync</c> internally — the caller owns the
        // single write).
        await using var session = store.LightweightSession();

        // D3 (design doc §2.3) — the seam re-runs the target's own frozen
        // <c>CanAsync(Read)</c> BEFORE the row is created. Deny / absent /
        // soft-deleted ⇒ the seam throws <c>KeyNotFoundException</c> (the
        // M16 create-gate posture: no row survives, no M37 Deny row).
        // The controller maps it to a non-leaky 404 (never 403).
        try
        {
            await notes.CreateNoteAsync(
                actorId, targetKind, targetId, body ?? string.Empty, session);
        }
        catch (KeyNotFoundException)
        {
            // D3 write-lane visibility check failed — the target is not
            // visible to the owner (Deny / absent / soft-deleted). The
            // "no row survives" pin means the seam already rolled back any
            // partial write; we return a non-leaky 404 (never 403 — the M16
            // create-gate posture: the Deny row does not survive).
            return NotFound();
        }

        // The seam wrote into the session; the caller commits (C3).
        // Both the F1 fresh-create and the F1 no-op (already noted) reach
        // here — the unique-index witness guarantees at-most-one row per
        // (owner, target), and the frozen <c>Task&lt;NoteRow&gt;</c> return
        // does not carry a status distinction (the plan's 200/201 split
        // cannot be expressed against the frozen shape — recorded as a
        // drift pause in the U04 handoff note; the redirect-after-write
        // + flash toast is the M17 <c>BookmarksController</c> precedent
        // and works for both branches identically).
        await session.SaveChangesAsync();
        TempData["info"] = await T("pn.note.attached");
        return Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/notes" : returnUrl);
    }

    /// <summary>
    /// <c>GET /notes/has/{targetKind}/{targetId}</c> — the F1 witness
    /// (the "noted" state on reload): the controller calls
    /// <see cref="IPersonalNoteService.HasNoteAsync"/> with the actor's
    /// <c>SubjectId</c> as the <c>ownerId</c>. A caller with no read
    /// standing (no <c>SubjectId</c> — a <c>GlobalAdmin</c> included) gets a
    /// non-leaky **404** (the C-M37·2 pin — the "operator has no read
    /// standing" gate; that 404 commits no <c>AccessAudit</c> row, and the
    /// seam's <c>HasNoteAsync</c> is **not** called — the sole-reader pin,
    /// C-M37·1). A caller with a <c>SubjectId</c> gets a <c>200</c> JSON
    /// <c>{"has": true|false}</c> — the <c>HasNoteAsync</c> call is a pure
    /// existence check over the owner's own row (C-M37·2: zero
    /// <c>AccessAudit</c> rows, zero <c>CanSeeAsync</c> passes, no target
    /// read — the ADR 0105 "operator has no read standing" precedent).
    /// </summary>
    [HttpGet("/notes/has/{targetKind}/{targetId}")]
    public async Task<IActionResult> HasNote(string targetKind, string targetId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // C-M37·2 — the "operator has no read standing" gate (the ADR
            // 0105 precedent): a caller with no <c>SubjectId</c> has no
            // read standing over any note. A non-leaky 404 — **no**
            // <c>AccessAudit</c> row is committed, and the seam's
            // <c>HasNoteAsync</c> is **not** called (the sole-reader pin —
            // C-M37·1: the controller never re-derives access, and a
            // caller with no read standing has no read to delegate).
            return NotFound();
        }

        // C-M37·1 / C-M37·2 — the seam is the sole reader (the personal
        // read, zero audit rows). The controller never re-derives access;
        // the seam's <c>HasNoteAsync</c> loads the owner's own row by the
        // identity predicate <c>OwnerId == ownerId</c>.
        var has = await notes.HasNoteAsync(actorId, targetKind, targetId);
        return Ok(new { has });
    }

    // ── JSON API endpoints (the note-modal AJAX surface) ────────────────
    //
    // These endpoints are used by the Bootstrap modal in _NoteButton.cshtml
    // (the "add / edit / delete note without leaving the page" UX). They
    // follow the same D2 personal-by-id + C-M37·2 zero-audit shape as the
    // existing routes; the only difference is JSON response (200/404)
    // instead of a redirect, so the modal can update its UI in-place.

    /// <summary>
    /// <c>GET /notes/api/{targetKind}/{targetId}</c> — the owner's note for
    /// this target, returned as JSON. 404 when no note exists (the modal
    /// opens in "create" mode) or when the caller has no read standing
    /// (C-M37·2 non-leaky 404).
    /// </summary>
    [HttpGet("/notes/api/{targetKind}/{targetId}")]
    public async Task<IActionResult> GetNoteForTarget(string targetKind, string targetId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        var note = await notes.GetNoteByTargetAsync(actorId, targetKind, targetId);
        if (note is null)
            return NotFound();

        return Ok(new
        {
            note.Id,
            note.Body,
            note.Created,
            note.Modified,
            note.Tags,
            note.Degraded,
            note.Title,
        });
    }

    /// <summary>
    /// <c>POST /notes/api/attach</c> — create or update the note for this
    /// target (F1 idempotent: the unique index guarantees one row per
    /// (owner, kind, id)). JSON body: <c>{ targetKind, targetId, body }</c>.
    /// 404 when the target is invisible (D3 write-lane check) or the caller
    /// has no read standing.
    /// </summary>
    [HttpPost("/notes/api/attach")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiAttach([FromBody] NoteAttachRequest req)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        if (string.IsNullOrWhiteSpace(req?.TargetKind) ||
            string.IsNullOrWhiteSpace(req?.TargetId))
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            var row = await notes.CreateNoteAsync(
                actorId, req.TargetKind, req.TargetId, req.Body ?? string.Empty, session);
            await session.SaveChangesAsync();
            return Ok(new { row.Id, row.Body, row.Created, row.Modified, row.Tags });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>PUT /notes/api/{noteId}</c> — update the note's body (D4: the
    /// body's new revision, the <c>Modified</c> timestamp is the witness).
    /// JSON body: <c>{ body }</c>. 404 when the note does not exist or the
    /// caller is not the owner (C-M37·2 non-leaky 404).
    /// </summary>
    [HttpPut("/notes/api/{noteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiUpdate(string noteId, [FromBody] NoteUpdateRequest req)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            var row = await notes.UpdateNoteBodyAsync(
                actorId, noteId, req?.Body ?? string.Empty, session);
            await session.SaveChangesAsync();
            return Ok(new { row.Id, row.Body, row.Created, row.Modified, row.Tags });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>DELETE /notes/api/{noteId}</c> — remove the note by id. 404 when
    /// the note does not exist or the caller is not the owner (C-M37·2
    /// non-leaky 404; D7/F4 — a dangling note is still removable).
    /// </summary>
    [HttpDelete("/notes/api/{noteId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApiDelete(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return NotFound();

        await using var session = store.LightweightSession();
        var status = await notes.DeleteNoteAsync(actorId, noteId, session);
        if (status == NoteDeleteStatus.NotNoted)
            return NotFound();

        await session.SaveChangesAsync();
        return Ok(new { deleted = true });
    }

    // ── Per-row note controls (the /notes list's edit / move / tag / delete) ──
    //
    // These actions implement the per-row controls rendered in
    // Views/Notes/Index.cshtml (edit / move / add-tag / remove-tag / delete).
    // Each follows the same C-M37·2 non-leaky-404 + zero-audit shape as the
    // existing routes: the controller never re-derives access, the frozen
    // IPersonalNoteService seam is the sole reader/writer, and a non-owner
    // (a GlobalAdmin included) gets a non-leaky 404 (the ADR 0105 "operator
    // has no read standing" precedent). The controller commits in its own
    // fresh IDocumentSession (the C3 same-transaction lane —
    // PersonalNoteService never commits internally; the caller owns the
    // single write).

    /// <summary>
    /// <c>GET /notes/{noteId}/edit</c> — the note body edit form (D4: the
    /// body's new revision, the <c>Modified</c> timestamp is the witness).
    /// 404 when the note does not exist or the caller is not the owner
    /// (C-M37·2 non-leaky 404).
    /// </summary>
    [HttpGet("/notes/{noteId}/edit")]
    public async Task<IActionResult> NoteEdit(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        try
        {
            var note = await notes.GetNoteAsync(actorId, noteId);
            return View(note);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/{noteId}/edit</c> — save the note body (D4).
    /// 404 when the note does not exist or the caller is not the owner.
    /// </summary>
    [HttpPost("/notes/{noteId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoteEditSave(string noteId, string? body)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        await using var session = store.LightweightSession();
        try
        {
            await notes.UpdateNoteBodyAsync(actorId, noteId, body ?? string.Empty, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>GET /notes/{noteId}/move</c> — the note move form (D6: the
    /// folder tree is a personal tree, bounded in depth). 404 when the
    /// note does not exist or the caller is not the owner.
    /// </summary>
    [HttpGet("/notes/{noteId}/move")]
    public async Task<IActionResult> NoteMove(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        try
        {
            var note = await notes.GetNoteAsync(actorId, noteId);
            var list = await notes.ListNotesAsync(actorId);
            return View(new NoteMoveForm(note, list.Folders));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/{noteId}/move</c> — move the note to (or from) a
    /// folder (D6). 404 when the note or folder does not exist or the
    /// caller is not the owner.
    /// </summary>
    [HttpPost("/notes/{noteId}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoteMoveSave(string noteId, string? folderId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        await using var session = store.LightweightSession();
        try
        {
            await notes.MoveNoteToFolderAsync(actorId, noteId, folderId, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>GET /notes/{noteId}/tags</c> — the add-tag form (D5: tags are
    /// author-set, free-form, per-note; normalized on write). 404 when the
    /// note does not exist or the caller is not the owner.
    /// </summary>
    [HttpGet("/notes/{noteId}/tags")]
    public async Task<IActionResult> NoteAddTag(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        try
        {
            var note = await notes.GetNoteAsync(actorId, noteId);
            return View(note);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/{noteId}/tags</c> — add a tag to the note (D5).
    /// 404 when the note does not exist or the caller is not the owner.
    /// </summary>
    [HttpPost("/notes/{noteId}/tags")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoteAddTagSave(string noteId, string? tag)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        if (string.IsNullOrWhiteSpace(tag))
            return Redirect($"/notes/{noteId}/tags");

        await using var session = store.LightweightSession();
        try
        {
            await notes.AddTagAsync(actorId, noteId, tag, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>GET /notes/{noteId}/tags/remove</c> — the remove-tag form (D5).
    /// 404 when the note does not exist or the caller is not the owner.
    /// </summary>
    [HttpGet("/notes/{noteId}/tags/remove")]
    public async Task<IActionResult> NoteRemoveTag(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        try
        {
            var note = await notes.GetNoteAsync(actorId, noteId);
            return View(note);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/{noteId}/tags/remove</c> — remove a tag from the
    /// note (D5). 404 when the note does not exist or the caller is not
    /// the owner.
    /// </summary>
    [HttpPost("/notes/{noteId}/tags/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoteRemoveTagSave(string noteId, string? tag)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        if (string.IsNullOrWhiteSpace(tag))
            return Redirect($"/notes/{noteId}/tags/remove");

        await using var session = store.LightweightSession();
        try
        {
            await notes.RemoveTagAsync(actorId, noteId, tag, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/{noteId}/delete</c> — delete the note (D7 / F4: a
    /// dangling note is still removable). 404 when the note does not exist
    /// or the caller is not the owner.
    /// </summary>
    [HttpPost("/notes/{noteId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoteDelete(string noteId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        await using var session = store.LightweightSession();
        var status = await notes.DeleteNoteAsync(actorId, noteId, session);
        if (status == NoteDeleteStatus.NotNoted)
            return NotFound();
        await session.SaveChangesAsync();
        TempData["info"] = await T("common.done");
        return Redirect("/notes");
    }

    // ── Folder-tree controls (the /notes list's new / rename / delete) ──

    /// <summary>
    /// <c>GET /notes/folders/new</c> — the new-folder form (D6: the folder
    /// tree is a personal tree, bounded in depth ≤ 2). 404 when the caller
    /// has no read standing.
    /// </summary>
    [HttpGet("/notes/folders/new")]
    public IActionResult FolderNew()
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        return View();
    }

    /// <summary>
    /// <c>POST /notes/folders/new</c> — create a folder (D6). 404 when the
    /// caller has no read standing. Redirects back to the form when the
    /// name is empty.
    /// </summary>
    [HttpPost("/notes/folders/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FolderNewSave(string? name)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        if (string.IsNullOrWhiteSpace(name))
            return Redirect("/notes/folders/new");

        await using var session = store.LightweightSession();
        try
        {
            await notes.CreateFolderAsync(actorId, name.Trim(), null, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // D6 depth violation or name conflict — 400.
            return BadRequest();
        }
    }

    /// <summary>
    /// <c>GET /notes/folders/{folderId}/rename</c> — the rename-folder form
    /// (D6). 404 when the folder does not exist or the caller is not the
    /// owner.
    /// </summary>
    [HttpGet("/notes/folders/{folderId}/rename")]
    public async Task<IActionResult> FolderRename(string folderId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        try
        {
            var list = await notes.ListNotesAsync(actorId);
            var folder = list.Folders.FirstOrDefault(f => f.Id == folderId);
            if (folder is null) return NotFound();
            return View(folder);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// <c>POST /notes/folders/{folderId}/rename</c> — rename a folder (D6).
    /// 404 when the folder does not exist or the caller is not the owner.
    /// </summary>
    [HttpPost("/notes/folders/{folderId}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FolderRenameSave(string folderId, string? name)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        if (string.IsNullOrWhiteSpace(name))
            return Redirect($"/notes/folders/{folderId}/rename");

        await using var session = store.LightweightSession();
        try
        {
            await notes.RenameFolderAsync(actorId, folderId, name.Trim(), session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // D6 name conflict — 400.
            return BadRequest();
        }
    }

    /// <summary>
    /// <c>POST /notes/folders/{folderId}/delete</c> — delete a folder
    /// (D6: the folder's notes are moved to the root; the note bodies are
    /// never lost). 404 when the folder does not exist or the caller is
    /// not the owner.
    /// </summary>
    [HttpPost("/notes/folders/{folderId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FolderDelete(string folderId)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId)) return NotFound();
        await using var session = store.LightweightSession();
        try
        {
            await notes.DeleteFolderAsync(actorId, folderId, session);
            await session.SaveChangesAsync();
            TempData["info"] = await T("common.done");
            return Redirect("/notes");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

/// <summary>
/// The JSON request body for <c>POST /notes/api/attach</c>.
/// </summary>
public sealed class NoteAttachRequest
{
    public string TargetKind { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string? Body { get; init; }
}

/// <summary>
/// The JSON request body for <c>PUT /notes/api/{noteId}</c>.
/// </summary>
public sealed class NoteUpdateRequest
{
    public string? Body { get; init; }
}

/// <summary>
/// The form model for <c>GET /notes/{noteId}/move</c> (the folder picker):
/// the note (for the current-folder pre-select + the 404 witness) + the
/// owner's folder tree (the D6 personal tree, bounded in depth ≤ 2).
/// </summary>
public sealed record NoteMoveForm(NoteRow Note, IReadOnlyList<FolderRow> Folders);
