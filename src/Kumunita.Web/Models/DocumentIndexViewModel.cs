namespace Kumunita.Web.Models;

/// <summary>
/// M21 (ADR 0122, U03) — the document feed (index) view model. The
/// <see cref="Visible"/> list holds the actor-visible
/// <see cref="Kumunita.Core.Documents.Document"/> documents (U02's
/// <c>DocumentListResult.Visible</c>); <see cref="HasMore"/> is the sole paging
/// signal. The aggregate audit row's hidden count (C-M21·3) is deliberately
/// **absent** — the feed never leaks "how many you cannot see" (F1).
/// </summary>
public sealed record DocumentIndexViewModel(
    IReadOnlyList<Kumunita.Core.Documents.Document> Visible,
    bool HasMore,
    int Page,
    bool CanUpload);
