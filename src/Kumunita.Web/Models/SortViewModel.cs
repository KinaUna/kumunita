namespace Kumunita.Web.Models;

/// <summary>
/// The one shared sort-control view model (M26, D-SORT·5).
/// <see cref="Options"/> is the surface's **closed** allowlist — the
/// control offers **exactly** those keys (F9, no dead options);
/// <see cref="CurrentKey"/> / <see cref="CurrentDir"/> echo the request's
/// resolved values (a null pair = the viewer chose no sort);
/// <see cref="CarriedParams"/> is the query-pair set the control's links
/// preserve (the `_Pager` `FilterParams` shape, M7 D7) — `page` + the
/// surface's existing filters.
/// </summary>
public sealed record SortViewModel(
    string BaseUrl,
    string? CurrentKey,
    string? CurrentDir,
    IReadOnlyList<(string Key, string Dir)> Options,
    IReadOnlyDictionary<string, string> CarriedParams)
{
    /// <summary>
    /// Build a <see cref="SortViewModel"/> for a route: <paramref name="options"/>
    /// is the surface's closed allowlist with each key's default direction;
    /// <paramref name="carriedParams"/> defaults to an empty dictionary
    /// (a surface with no filters carries only the sort/dir pair on its links).
    /// </summary>
    public static SortViewModel ForRoute(
        string baseUrl, string? currentKey, string? currentDir,
        IReadOnlyList<(string Key, string Dir)> options,
        IReadOnlyDictionary<string, string>? carriedParams = null)
        => new(baseUrl, currentKey, currentDir, options,
            carriedParams ?? new Dictionary<string, string>());
}
