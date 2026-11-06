using Marten;

namespace Kumunita.Core.Documents;

/// <summary>
/// The document-folder read/write seam (IMPROVE-CODE-2 C2-03 — the
/// <see cref="DocumentFolderService"/>'s cross-module surface, extracted
/// onto a narrow interface so the crossing <c>Web</c> layer (the
/// <see cref="Kumunita.Web.Controllers.DocumentFolderController"/>) codes
/// against a stable seam rather than the <c>DocumentFolderService</c>
/// concrete class — the C03
/// <see cref="Kumunita.Core.Notifications.INotificationEmitter"/> pattern,
/// the ADR 0006-D "route + authz + shape" rule).
/// <para>
/// The six methods are the cross-module surface the controller actually
/// calls (the part-vs-whole rule — the service is small and single-purpose
/// [folder create / list / move / rename / delete], so its whole surface is
/// the crossing surface; the DirectoryService / SessionService precedent).
/// The <see cref="CanManageFolder"/> standing check + the
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> rows stay in the
/// <see cref="DocumentFolderService"/> concrete — the interface adds no
/// behaviour.
/// </para>
/// </summary>
public interface IDocumentFolderService
{
    /// <summary>
    /// Create a new folder (the controller's <c>Create</c> write lane). The
    /// <see cref="DocumentFolderService"/> implementation is unchanged.
    /// </summary>
    Task<DocumentFolder> CreateAsync(string name, string? parentId, string actorId, IDocumentSession session);

    /// <summary>Session-owning overload (C2-04) — the Web controller no longer opens its own session; the service owns the write + the single <c>SaveChangesAsync</c>.</summary>
    Task<DocumentFolder> CreateAsync(string name, string? parentId, string actorId);

    /// <summary>
    /// List folders under a parent (the controller's <c>Index</c> read). The
    /// <see cref="DocumentFolderService"/> implementation is unchanged.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<DocumentFolder>> ListByParentAsync(string? parentId);

    /// <summary>
    /// Move a document to a different folder (the controller's
    /// <c>MoveDocument</c> write lane). The
    /// <see cref="DocumentFolderService"/> implementation is unchanged.
    /// </summary>
    Task<Document> MoveDocumentAsync(string documentId, string? newFolderId, string actorId, IDocumentSession session);

    /// <summary>Session-owning overload (C2-04) — the Web controller no longer opens its own session.</summary>
    Task<Document> MoveDocumentAsync(string documentId, string? newFolderId, string actorId);

    /// <summary>
    /// Rename a folder (the controller's <c>Rename</c> write lane). The
    /// <see cref="DocumentFolderService"/> implementation is unchanged.
    /// </summary>
    Task<DocumentFolder> RenameAsync(string folderId, string name, string actorId, System.Collections.Generic.IReadOnlySet<string> roles, IDocumentSession session);

    /// <summary>Session-owning overload (C2-04) — the Web controller no longer opens its own session.</summary>
    Task<DocumentFolder> RenameAsync(string folderId, string name, string actorId, System.Collections.Generic.IReadOnlySet<string> roles);

    /// <summary>
    /// Move a folder to a different parent (the controller's <c>Move</c>
    /// write lane). The <see cref="DocumentFolderService"/> implementation
    /// is unchanged.
    /// </summary>
    Task<DocumentFolder> MoveAsync(string folderId, string? newParentId, string actorId, System.Collections.Generic.IReadOnlySet<string> roles, IDocumentSession session);

    /// <summary>Session-owning overload (C2-04) — the Web controller no longer opens its own session.</summary>
    Task<DocumentFolder> MoveAsync(string folderId, string? newParentId, string actorId, System.Collections.Generic.IReadOnlySet<string> roles);

    /// <summary>
    /// Delete a folder (the controller's <c>Delete</c> write lane). The
    /// <see cref="DocumentFolderService"/> implementation is unchanged.
    /// </summary>
    Task DeleteAsync(string folderId, string actorId, System.Collections.Generic.IReadOnlySet<string> roles, IDocumentSession session);

    /// <summary>Session-owning overload (C2-04) — the Web controller no longer opens its own session.</summary>
    Task DeleteAsync(string folderId, string actorId, System.Collections.Generic.IReadOnlySet<string> roles);
}
