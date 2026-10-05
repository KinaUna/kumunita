namespace Kumunita.Core.Documents;

/// <summary>
/// The edit draft for <see cref="DocumentService.UpdateAsync"/> (ADR 0125,
/// D1/D2/D3). The owner's **replace** of a document: the new
/// <see cref="Audience"/> (the "who can access it" — C-M21·1, written verbatim
/// and never auto-augmented), the optional new title/summary (display-only
/// labels — ADR 0125 D2 leaves them editable alongside the two named fields),
/// and the **optional** new file (ADR 0125 D3 — an empty file is a no-op on the
/// byte surface: <see cref="MediaId"/> / <see cref="Filename"/> /
/// <see cref="ContentType"/> / <see cref="SizeBytes"/> stay at their stored
/// values and <see cref="FileReplaced"/> is false; a file present populates the
/// fresh <c>IMediaStore.PutAsync</c> result + <see cref="FileReplaced"/> true).
/// </summary>
/// <remarks>
/// <see cref="MediaId"/> / <see cref="Filename"/> / <see cref="ContentType"/> /
/// <see cref="SizeBytes"/> are populated **server-side by the Web layer** from
/// the frozen ADR 0011 <c>IMediaStore.PutAsync</c> result — the Core service
/// never touches the bytes (ADR 0122 D3: the media lane is untouched, a new
/// consumer). <see cref="FileReplaced"/> is the Web layer's marker for "a new
/// file was chosen on this edit", so the Core lane can distinguish the
/// no-file (keep the stored blob) case from the file-replaced (write the new
/// blob reference) case without re-deriving it from a nullable id.
/// </remarks>
public sealed record DocumentEdit(
    string Title,
    string? Summary,
    string MediaId,
    string? Filename,
    string ContentType,
    long SizeBytes,
    Authorization.Audience Audience,
    bool FileReplaced,
    // ── Organization (the "documents organization" lane) ────────────────────
    // The folder this document is being moved to (the ADR 0039 Pages
    // <c>ParentId</c> forest carried to Documents). <c>null</c> = "Unfiled"
    // (the root). The Web layer resolves the folder id (a validated,
    // actor-visible id); a shape-violating folder is a form error at the Web
    // boundary, never here. The Core write lane (DocumentService.UpdateAsync)
    // writes <c>FolderId</c> verbatim onto <c>Document.FolderId</c>
    // (the ADR 0125 D2 "editable alongside" shape, the owner-only lane).
    string? FolderId,
    // The TG-lane tag slugs the owner typed (the client posts a JSON array of
    // label strings; the Web parses via TagSlugs.Parse — trim / dedup /
    // drop-blank). <c>null</c> = "leave the document's existing tags"
    // (the M3/M7 default-empty idiom, the U8b register patch's detach
    // semantics: a **present** field — even an empty <c>[]</c> when the
    // owner removed every chip — is authoritative ⇒ empty detaches all,
    // non-empty attaches). The Core write lane resolves them to Tag ids
    // through ITagService.AttachToDocumentAsync (the AttachToPostAsync /
    // AttachToPageAsync precedent) and stores the resolved ids on
    // Document.TagIds (replacing the POCO's default-empty list).
    IReadOnlyList<string>? TagSlugs);
