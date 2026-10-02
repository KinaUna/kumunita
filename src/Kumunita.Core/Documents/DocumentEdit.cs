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
    bool FileReplaced);
