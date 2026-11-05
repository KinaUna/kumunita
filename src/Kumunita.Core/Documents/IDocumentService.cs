using Kumunita.Core.Query;
using Marten;

namespace Kumunita.Core.Documents;

/// <summary>
/// The document read/write seam (IMPROVE-CODE-2 C2-03 — the
/// <see cref="DocumentService"/>'s cross-module surface, extracted onto a
/// narrow interface so the crossing <c>Web</c> layer (the
/// <see cref="Kumunita.Web.Controllers.DocumentController"/>) codes against a
/// stable seam rather than the <c>DocumentService</c> concrete class — the
/// C03 <see cref="Kumunita.Core.Notifications.INotificationEmitter"/> pattern,
/// the ADR 0006-D "route + authz + shape" rule).
/// <para>
/// The seven methods are the cross-module surface the controller actually
/// calls (the part-vs-whole rule — the service is small and single-purpose
/// [document list / read / write / trash], so its whole surface is the
/// crossing surface; there is no fat/tin split to avoid — the DirectoryService
/// / SessionService precedent). The standing decision, the owner-scope rule,
/// and the <see cref="Kumunita.Core.Authorization.AccessAudit"/> rows all stay
/// in the <see cref="DocumentService"/> concrete — the interface adds no
/// behaviour.
/// </para>
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// The document list read (the <see
    /// cref="Kumunita.Web.Controllers.DocumentController"/>'s <c>Index</c>).
    /// The <see cref="DocumentService"/> implementation is unchanged.
    /// </summary>
    Task<DocumentListResult> ListAsync(string actorId, int page, SortSpec? sort = null, int? pageSize = null);

    /// <summary>
    /// A single document's read (the controller's <c>Detail</c>). The
    /// <see cref="DocumentService"/> implementation is unchanged (the
    /// <c>owner</c> / <c>audience</c> standing + the <c>AccessAudit</c> row
    /// stay in the concrete).
    /// </summary>
    Task<DocumentDetailResult> GetAsync(string documentId, string actorId);

    /// <summary>
    /// Upload a new document (the controller's <c>Upload</c> write lane). The
    /// <see cref="DocumentService"/> implementation is unchanged (the
    /// <c>IDocumentSession</c> is the controller's, the C3 same-transaction
    /// lane).
    /// </summary>
    Task<Document> UploadAsync(DocumentUpload draft, string actorId, IDocumentSession session);

    /// <summary>
    /// Update a document (the controller's <c>Edit</c> write lane). The
    /// <see cref="DocumentService"/> implementation is unchanged.
    /// </summary>
    Task<Document> UpdateAsync(string documentId, DocumentEdit edit, string actorId, IDocumentSession session);

    /// <summary>
    /// Delete a document to the trash (the controller's <c>Delete</c> write
    /// lane). The <see cref="DocumentService"/> implementation is unchanged.
    /// </summary>
    Task DeleteAsync(string documentId, string actorId, IDocumentSession session);

    /// <summary>
    /// List the trashed documents (the controller's <c>Trash</c> read). The
    /// <see cref="DocumentService"/> implementation is unchanged.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<Document>> ListDeletedAsync(string actorId, bool actorIsGlobalAdmin, int max = 200);

    /// <summary>
    /// Restore a trashed document (the controller's <c>Restore</c> write
    /// lane). The <see cref="DocumentService"/> implementation is unchanged.
    /// </summary>
    Task RestoreAsync(string documentId, string actorId, bool actorIsGlobalAdmin, IDocumentSession session);
}
