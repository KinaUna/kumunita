using System.Collections.Generic;

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
    bool CanUpload,
    // ADR 0239 D1 — the uploader's display name for each visible document
    // (key = the document's <c>Id</c>, value = the <c>Profile.DisplayName</c>
    // resolved through the <c>IUserInfoService.GetProfileAsync</c> read lane
    // — the <c>PostsController</c> feed shape). A <c>document.Id</c> that is
    // not in the dictionary degrades to the raw owner id in the view (the
    // <c>profile?.DisplayName ?? post.AuthorId</c> shape). Empty when the
    // <c>IUserInfoService</c> seam is absent (a test-construction site — the
    // view falls back to the raw owner id for every row).
    IDictionary<string, string> OwnerNames)
{
    /// <summary>M26 U13 (D-SORT·5) — the one shared sort control (the U10
    /// _Sort reference, reused verbatim — C-SORT·1): the closed documents
    /// allowlist (U2 §2.2 row 12 — created/modified/title/size, `size` being
    /// this surface's own extra key, F9). A null <c>Sort</c> renders nothing
    /// (the no-sort pin, C-SORT·2).</summary>
    public SortViewModel? Sort { get; init; }
}
