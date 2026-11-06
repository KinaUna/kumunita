namespace Kumunita.Core.Targets;

/// <summary>
/// The single content-target resolution seam (IMPROVE-CODE C05 — the
/// Bookmark/PersonalNote target resolvers, ADR 0118 D3/D5 + ADR 0167 D1/D7).
/// Maps a <c>(TargetKind, TargetId, actorId, surface)</c> to a display row —
/// <c>(Visible, Title, Link)</c> for the write lane (the D3 create-gate
/// check, which re-runs the target's own frozen read) and <c>(Title, Link)</c>
/// for the list lane (the D5 zero-audit shape, no <c>CanAsync(Read)</c>).
/// <para>
/// **Why an interface:** the D6 boundary rule allows a module (Bookmarks,
/// PersonalNotes) to reference only an interface / record / enum / static
/// <c>*Policy</c> / <c>*Keys</c> / <c>*Kinds</c> from another module — a
/// concrete <c>ContentTargetResolver</c> referenced cross-module would be a
/// boundary violation. Exposing the seam behind this interface keeps the
/// D6 gate green while the two modules share one implementation.
/// </para>
/// <para>
/// **Byte-identical behaviour** is the contract: the same kinds, the same
/// <c>CanAsync(Read)</c> decisions, the same <c>Title</c> / <c>Link</c> /
/// <c>Visible</c> shape, the same audit rows as the private clones this
/// replaces (the <c>BookmarkService.ResolveTargetAsync</c> /
/// <c>ResolveForListAsync</c> and the <c>PersonalNoteService</c> twins).
/// The two surfaces' divergences (the <c>project</c> link, the
/// <c>announcement</c> list draft gate, the <c>grouppost</c> /
/// <c>document</c> / <c>page-*</c> kinds) are recorded in
/// <see cref="ContentTargetKinds"/> (a legal D6 target), not re-derived
/// per surface.
/// </para>
/// </summary>
public interface IContentTargetResolver
{
    /// <summary>
    /// Resolves a target to (<c>visible</c>, <c>title</c>, <c>link</c>) for
    /// the **write lane** (the D3 create-gate check) — the same call the
    /// target's own detail page uses, so the target's own <c>AccessAudit</c>
    /// row commits. <c>visible</c> is <c>false</c> (and title/link are
    /// <c>null</c>) when the target is absent, soft-deleted, a non-author
    /// draft, denied to the actor, or outside the closed kind set for the
    /// requesting surface (ADR 0118 D3; ADR 0167 D1).
    /// </summary>
    /// <param name="targetKind">
    /// The <c>TargetKind</c> string (the M5 <c>KanbanStatuses</c> shape).
    /// </param>
    /// <param name="targetId">The target's document id.</param>
    /// <param name="actorId">The acting user's id (thin principal).</param>
    /// <param name="surface">
    /// The requesting surface — <see cref="ContentTargetKinds.Bookmarks"/>
    /// or <see cref="ContentTargetKinds.PersonalNotes"/>. A few kinds
    /// (the <c>project</c> link) resolve slightly differently per surface;
    /// the table records that divergence.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="targetKind"/>, <paramref name="targetId"/>,
    /// <paramref name="actorId"/> or <paramref name="surface"/> is null.
    /// </exception>
    Task<(bool Visible, string? Title, string? Link)> ResolveAsync(
        string targetKind, string targetId, string actorId, string surface);

    /// <summary>
    /// Resolves a list row to (<c>title</c>, <c>link</c>) for the **list
    /// lane** (the D5 personal read, zero-audit — ADR 0118 D5, the
    /// C-M17·2 pin). A pure read: commits **no** <c>AccessAudit</c> row and
    /// makes **no** <c>CanAsync(Read)</c> call. Returns <c>(null, null)</c>
    /// when the target is absent, soft-deleted, a non-author draft, or
    /// outside the closed kind set for the requesting surface (the D5
    /// degrade).
    /// </summary>
    Task<(string? Title, string? Link)> ResolveForListAsync(
        string targetKind, string targetId, string actorId, string surface);
}
