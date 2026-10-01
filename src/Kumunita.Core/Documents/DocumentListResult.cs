namespace Kumunita.Core.Documents;

/// <summary>
/// <see cref="DocumentService.ListAsync"/>'s result (M21, ADR 0122; design
/// doc §5, the C-M21·3 aggregate shape). <see cref="Visible"/> holds the source
/// <see cref="Document"/> documents the single <c>CanSeeAsync</c> call over the
/// candidate set actually allowed — never a hidden document's fields (F1/F2).
/// <see cref="HiddenCount"/> counts only the candidates that call evaluated.
/// One aggregate <c>AccessAudit</c> row (<c>TargetKind = "document"</c>, via the
/// <see cref="DocumentToAuditableResource"/> adapter) is the row for this visit
/// (C-M21·3). <see cref="Total"/> is the **candidate-set count** (pre-decision);
/// <see cref="HasMore"/> is the sole paging signal: <c>true</c> iff the page's
/// candidate set filled the page.
/// </summary>
public sealed record DocumentListResult(
    IReadOnlyList<Document> Visible,
    int HiddenCount,
    int Page,
    int Total,
    bool HasMore);
