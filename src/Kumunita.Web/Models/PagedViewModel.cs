using System.Collections.Generic;

namespace Kumunita.Web.Models;

/// <summary>
/// The one shared pager view model (ADR 0090 D5). <see cref="HasPrevious"/>
/// = <c>page &gt; 1</c>; <see cref="HasNext"/> = the seam's <c>HasMore</c>
/// (D1 — the sole paging signal; there is no <c>TotalPages</c>).
/// <see cref="FilterParams"/> are the current filter values the pager's
/// links carry as query pairs (D7 — the pager preserves the filter);
/// empty by default. A null of this type on a section's VM field means
/// "one page" — the <c>_Pager</c> partial renders nothing (F2).
/// </summary>
public sealed record PagedViewModel(
    int CurrentPage,
    int PageSize,
    bool HasPrevious,
    bool HasNext,
    string BaseUrl,
    IReadOnlyDictionary<string, string> FilterParams)
{
    /// <summary>
    /// Build a <see cref="PagedViewModel"/> for a route: <c>HasPrevious</c>
    /// is <c>page &gt; 1</c> (D5); <c>HasNext</c> is the seam's <c>hasMore</c>
    /// (D1); <paramref name="filterParams"/> defaults to an empty
    /// dictionary (D7 — a surface with no filter form simply carries
    /// <c>?page=N</c> on its links).
    /// </summary>
    public static PagedViewModel ForRoute(
        string baseUrl, int page, int pageSize, bool hasMore,
        IReadOnlyDictionary<string, string>? filterParams = null)
        => new(
            CurrentPage: page,
            PageSize: pageSize,
            HasPrevious: page > 1,
            HasNext: hasMore,
            BaseUrl: baseUrl,
            FilterParams: filterParams ?? new Dictionary<string, string>());
}
