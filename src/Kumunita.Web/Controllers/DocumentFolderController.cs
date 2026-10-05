using Kumunita.Core.Documents;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The "documents organization" lane's folder CRUD + document-move routes:
/// the folder list (<c>GET /documents/folders</c>), the folder create
/// (<c>POST /documents/folders</c>), the folder rename
/// (<c>POST /documents/folders/{id}/rename</c>), the folder move
/// (<c>POST /documents/folders/{id}/move</c>), the folder delete
/// (<c>POST /documents/folders/{id}/delete</c>), and the document move
/// (<c>POST /documents/{id}/move</c>).
/// <para>
/// The standing gates are the same elevated standing as the upload lane
/// (<c>GlobalAdmin ∪ Moderator</c> — the ADR 0122 D5 precedent for
/// folder create), the owner ∪ GlobalAdmin standing (the ADR 0125
/// owner-reading carried to folders — the ADR 0044 elevated-standing
/// precedent) for rename / move / delete, and the owner-only standing
/// (the ADR 0125 owner-reading) for the document move. A non-eligible
/// actor gets **404, not 403** (the ADR 0122 D7 posture — the form's
/// existence is not leaked).
/// </para>
/// </summary>
public sealed class DocumentFolderController(
    DocumentFolderService folders,
    IDocumentStore store,
    Kumunita.Core.Localization.ILocalizationService? localization = null,
    Kumunita.Core.Localization.ITranslationProvider? translationProvider = null) : Controller
{
    private async Task<string> T(string key)
    {
        if (translationProvider is null || localization is null)
            return key;
        var lang = await Kumunita.Web.Security.EffectiveLanguageCode.ResolveAsync(
            HttpContext?.Request, localization, translationProvider);
        return await translationProvider.GetAsync(key, lang);
    }

    private static bool IsElevated(System.Security.Claims.ClaimsPrincipal? user)
        => Kumunita.Web.Security.KumunitaPrincipal.IsGlobalAdmin(user!)
           || Kumunita.Web.Security.KumunitaPrincipal.IsModerator(user!);

    // ── GET /documents/folders — the folder tree (the picker's data source) ─
    [HttpGet("/documents/folders")]
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var actorId = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (actorId is null) return Unauthorized();

        var roots = await folders.ListByParentAsync(null);

        // Flatten into a single level list (the picker is a flat list, not a
        // tree — the "Unfiled" root + all folders, ordered by name). The
        // depth is not rendered in the picker (the picker is a
        // <select> element; a nested <optgroup> would be over-engineering
        // for this surface — the ADR 0039 Pages "folder" is a pure
        // organizational aid, not a navigation tree).
        var all = new List<(string Id, string Name, string? ParentId)>();
        foreach (var root in roots)
        {
            all.Add((root.Id, root.Name, null));
            var children = await folders.ListByParentAsync(root.Id);
            foreach (var child in children)
                all.Add((child.Id, child.Name, root.Id));
        }

        return Json(new
        {
            folders = all.Select(f => new { f.Id, f.Name, f.ParentId }).ToList(),
        });
    }

    // ── POST /documents/folders — create a folder (GlobalAdmin ∪ Moderator) ─
    [HttpPost("/documents/folders")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] DocumentFolderCreateViewModel form)
    {
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        // The standing gate (the ADR 0122 D5 precedent — the same elevated
        // standing as the upload lane): GlobalAdmin ∪ Moderator.
        if (!IsElevated(User)) return NotFound();

        if (string.IsNullOrWhiteSpace(form.Name))
            return BadRequest("A folder name is required.");

        await using var session = store.LightweightSession();
        try
        {
            var folder = await folders.CreateAsync(form.Name, form.ParentId, subject, session);
            TempData["info"] = await T("documents.folder_flash_created");
            return Redirect("/documents");
        }
        catch (KeyNotFoundException)
        {
            // A missing parent folder (the Web layer's 404 posture, ADR 0122 D7).
            return NotFound();
        }
    }

    // ── POST /documents/folders/{id}/rename — rename (owner ∪ GlobalAdmin) ─
    [HttpPost("/documents/folders/{id}/rename")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(string id, [FromForm] DocumentFolderRenameViewModel form)
    {
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        var roles = Kumunita.Web.Security.KumunitaPrincipal.RoleSet(User)
            ;

        if (string.IsNullOrWhiteSpace(form.Name))
            return BadRequest("A folder name is required.");

        await using var session = store.LightweightSession();
        try
        {
            await folders.RenameAsync(id, form.Name, subject, roles, session);
            TempData["info"] = await T("documents.folder_flash_renamed");
            return Redirect("/documents");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // A non-owner non-GlobalAdmin — 404 (the ADR 0122 D7 posture:
            // the folder's existence is not leaked).
            return NotFound();
        }
    }

    // ── POST /documents/folders/{id}/move — move (owner ∪ GlobalAdmin) ────
    [HttpPost("/documents/folders/{id}/move")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(string id, [FromForm] DocumentFolderMoveViewModel form)
    {
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        var roles = Kumunita.Web.Security.KumunitaPrincipal.RoleSet(User)
            ;

        await using var session = store.LightweightSession();
        try
        {
            await folders.MoveAsync(id, form.NewParentId, subject, roles, session);
            TempData["info"] = await T("documents.folder_flash_moved");
            return Redirect("/documents");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            // A cycle (the move would create a cycle — the write-lane guard,
            // the ADR 0044 tag DeriveSlug shape). A 400 (a form error, the
            // M3 "a form is a shape" precedent).
            return BadRequest();
        }
    }

    // ── POST /documents/folders/{id}/delete — delete (owner ∪ GlobalAdmin) ─
    [HttpPost("/documents/folders/{id}/delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        var roles = Kumunita.Web.Security.KumunitaPrincipal.RoleSet(User)
            ;

        await using var session = store.LightweightSession();
        try
        {
            await folders.DeleteAsync(id, subject, roles, session);
            TempData["info"] = await T("documents.folder_flash_deleted");
            return Redirect("/documents");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            // A folder with documents or subfolders cannot be deleted (the
            // Web layer's form error, the M3 "a form is a shape" precedent).
            return BadRequest();
        }
    }

    // ── POST /documents/{id}/move — move a document to a folder (owner-only) ─
    [HttpPost("/documents/{id}/move")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveDocument(string id, [FromForm] DocumentMoveViewModel form)
    {
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        // The owner-only gate (the ADR 0125 owner-reading — the same standing
        // that governs the edit lane). The Core lane re-checks it.
        await using var session = store.LightweightSession();
        try
        {
            await folders.MoveDocumentAsync(id, form.NewFolderId, subject, session);
            TempData["info"] = await T("documents.flash_moved");
            return Redirect($"/documents/{id}");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }
}

// ── Form-bound view models (the "documents organization" lane) ─────────────

/// <summary>The folder create form (<c>POST /documents/folders</c>).</summary>
public sealed class DocumentFolderCreateViewModel
{
    /// <summary>The folder's display name (a label, never a gate).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The parent folder id (<c>null</c> = a root).</summary>
    public string? ParentId { get; set; }
}

/// <summary>The folder rename form (<c>POST /documents/folders/{id}/rename</c>).</summary>
public sealed class DocumentFolderRenameViewModel
{
    /// <summary>The folder's new display name (a label, never a gate).</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>The folder move form (<c>POST /documents/folders/{id}/move</c>).</summary>
public sealed class DocumentFolderMoveViewModel
{
    /// <summary>The new parent folder id (<c>null</c> = promote to a root).</summary>
    public string? NewParentId { get; set; }
}

/// <summary>The document move form (<c>POST /documents/{id}/move</c>).</summary>
public sealed class DocumentMoveViewModel
{
    /// <summary>The new folder id (<c>null</c> = "Unfiled" — the root).</summary>
    public string? NewFolderId { get; set; }
}
