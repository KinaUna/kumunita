namespace Kumunita.Core.Search;

/// <summary>
/// The closed set of search surface keys (IMPROVE-CODE-2 C2-03 — the
/// <c>*Surface</c> string keys the Web layer names to pick a surface from a
/// <see cref="SearchService"/> read). Extracted from the
/// <see cref="SearchService"/> as a pure, session-free <c>*Keys</c> static
/// (the D6 boundary-allowlist shape — a cross-module target the Web may reach
/// without coding against the concrete <see cref="SearchService"/>).
/// <para>
/// The string values are unchanged (the M7 / ADR 0124 surface keys — the
/// four original resident surfaces + the six extended M8 surfaces); the
/// <see cref="SearchService"/> implementation is unchanged and still reads
/// these values (either via <c>SearchService.</c> — the consts stay there as
/// well — or via <see cref="SearchKeys"/>, either way the same literal).
/// </para>
/// <para>
/// ADR 0124 (the six extended surfaces): <see cref="ProjectsSurface"/> /
/// <see cref="BoardsSurface"/> / <see cref="TodosSurface"/> /
/// <see cref="InventorySurface"/> / <see cref="DocumentsSurface"/> /
/// <see cref="PeopleSurface"/>. The four original surfaces (D2):
/// <see cref="PostsSurface"/> / <see cref="EventsSurface"/> /
/// <see cref="PagesSurface"/> / <see cref="AnnouncementsSurface"/>.
/// </para>
/// </summary>
public static class SearchKeys
{
    /// <summary>The four original search surfaces (D2 — the four resident content surfaces).</summary>
    public const string PostsSurface = "posts";
    public const string EventsSurface = "events";
    public const string PagesSurface = "pages";
    public const string AnnouncementsSurface = "announcements";

    /// <summary>The six extended surfaces (ADR 0124 — the M8 D2 "out" list, realized).</summary>
    public const string ProjectsSurface = "projects";
    public const string BoardsSurface = "boards";
    public const string TodosSurface = "todos";
    public const string InventorySurface = "inventory";
    public const string DocumentsSurface = "documents";
    public const string PeopleSurface = "people";
}
