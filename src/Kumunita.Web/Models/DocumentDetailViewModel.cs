namespace Kumunita.Web.Models;

/// <summary>
/// M21 (ADR 0122, U03) — the document detail view model. <see
/// cref="Document"/> is the already-authorized document (U02's
/// <c>DocumentDetailResult.Document</c>); the controller 404s (D7) when it is
/// null — the missing and the Deny cases collapse to the **same** response
/// (feed and detail agree, C-M21·5). <see cref="CanDownload"/> is true iff the
/// actor is allowed (redundant with the controller's own decision here, but the
/// view (U04) uses it to render the download link only when visible).
/// <see cref="DownloadUrl"/> is the stable route the download link points at.
/// <see cref="CanEdit"/> is true iff the actor is the document's <b>owner</b>
/// (the uploader — ADR 0125 D1); the view renders the Edit link only then, so a
/// non-owner never sees the affordance (the ADR 0122 D7 posture: the form's
/// existence is not leaked — a non-owner gets a 404 on the edit route, not a
/// 403).
/// </summary>
public sealed record DocumentDetailViewModel(
    Kumunita.Core.Documents.Document Document,
    bool CanDownload,
    string DownloadUrl,
    bool CanEdit,
    string EditUrl,
    // ── Organization (the "documents organization" lane) ─────────────────────
    // The document's tags resolved to (slug, display-name) rows (the
    // PostsController's tag-rows shape — the display-name is resolved to the
    // viewer's language through the ITagService read seam; a label, never a
    // gate — C-TG·1). Empty when the document carries no tags (the M3/M7
    // default-empty idiom).
    System.Collections.Generic.IReadOnlyList<DocumentTagRow> Tags,
    // The document's folder name (the display label, never a gate). <c>null</c>
    // = "Unfiled" (the root).
    string? FolderName,
    // The document's folder id (used by the view to link to the folder).
    string? FolderId);

/// <summary>
/// A tag row on a document's detail view (the "documents organization" lane):
/// the tag's <see cref="Slug"/> (the business key — the <c>/tags/{slug}</c>
/// route link target) and the <see cref="DisplayedName"/> (the label resolved
/// to the viewer's language through the <c>ITagService</c> read seam — the
/// ADR 0005 preference order, D5).
/// </summary>
public sealed record DocumentTagRow(string Slug, string DisplayedName);
