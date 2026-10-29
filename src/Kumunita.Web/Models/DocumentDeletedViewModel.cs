namespace Kumunita.Web.Models;

/// <summary>
/// ADR 0241 — the "Deleted documents" page (<c>GET /documents/deleted</c>): the soft-deleted
/// documents the viewer may restore (their own, or all of them for a GlobalAdmin).
/// </summary>
/// <param name="Rows">Newest deletion first.</param>
/// <param name="IsAdminView">True when the viewer sees every deleted document (GlobalAdmin).</param>
public sealed record DocumentDeletedViewModel(
    IReadOnlyList<DocumentDeletedRow> Rows,
    bool IsAdminView);

/// <summary>One row of <see cref="DocumentDeletedViewModel"/>: the document and its resolved uploader name.</summary>
public sealed record DocumentDeletedRow(
    Kumunita.Core.Documents.Document Document,
    string OwnerName);
