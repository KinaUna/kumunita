namespace Kumunita.Core.Documents;

/// <summary>
/// M21 (ADR 0122, D1) — an official document in the community's shared
/// repository (contracts, minutes, notices). The bytes are **not** stored here:
/// <see cref="MediaId"/> is the content-addressed id of the file on the frozen
/// ADR 0011 <c>IMediaStore</c> (D3 — the media lane is untouched, a new
/// consumer). <see cref="Audience"/> is the per-document access control,
/// written verbatim and projected as-is to the frozen
/// <see cref="Authorization.IAuthorizationService"/> Read path (C-M21·1, C-M21·3).
/// </summary>
public sealed class Document
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Authorization.Audience Audience { get; set; } = new();
    public string OwnerId { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // ── Organization (the "documents organization" lane) ─────────────────────
    /// <summary>
    /// The <c>TG</c>-lane tag ids attached to this document (the
    /// <see cref="Posts.Post.TagIds"/> / <see cref="Pages.Page.TagIds"/> shape —
    /// a global shared id-doc list, the ADR 0011 shared-id-doc convention).
    /// Default <see cref="IReadOnlyList{T}">empty</see> — existing documents
    /// read back with no tags (the ADR 0004 §B.1 additive no-reseed pin; the
    /// M3/M7 default-empty idiom). A tag is a label, never a gate (C-TG·1) —
    /// access is still governed solely by <see cref="Audience"/>.
    /// </summary>
    public IReadOnlyList<string> TagIds { get; set; } = [];

    /// <summary>
    /// The folder this document lives in (the "documents organization" lane —
    /// the ADR 0039 Pages <c>ParentId</c> forest carried to Documents).
    /// <c>null</c> = "Unfiled" (the root — no folder). The folder is a pure
    /// organization unit (a label, never a gate): access is governed solely
    /// by <see cref="Audience"/>. <see cref="DocumentFolderService.MoveAsync"/>
    /// rewrites this column to move a document between folders.
    /// </summary>
    public string? FolderId { get; set; }
}
