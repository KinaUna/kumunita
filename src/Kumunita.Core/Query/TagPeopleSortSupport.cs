namespace Kumunita.Core.Query;

/// <summary>
/// M26 U8 — the shared ordering helpers for the four tag/people seams
/// (<see cref="Tags.TagService.ListPostsByTagPagedAsync"/>,
/// <see cref="Tags.TagService.ListPagesByTagPagedAsync"/>,
/// <see cref="UserInfo.ProfileFindService.FindPeopleByTagAsync"/>,
/// <see cref="UserInfo.ProfileFindService.FindPeopleByBioAsync"/>).
/// <b>Unlike U4–U7 these seams order in-memory candidate lists
/// (LINQ-to-objects, not Marten <c>IAsyncQueryable</c>)</b>, so the U4–U7
/// Marten drifts do not apply: the frozen §2.2
/// <c>?? DateTimeOffset.MinValue</c> sentinel on the nullable
/// <c>modified</c> key compiles and sorts normally, and
/// <c>ThenBy</c> is the plain <c>Enumerable</c> form (no CS0411, no
/// <c>BadLinqExpressionException</c>). Each surface applies its
/// <b>closed</b> allowlist (design doc §2.2 rows 14–17 —
/// tag→posts <c>created</c>/<c>modified</c>/<c>title</c>; tag→pages
/// <c>created</c>/<c>modified</c>/<c>title</c>; people
/// <c>name</c> → <see cref="UserInfo.Profile.DisplayName"/> only —
/// correction C-2, <c>Profile</c> has no <c>Created</c>) + the
/// <c>.ThenBy(…Id)</c> tie-breaker (C-SORT·5; people's identity member is
/// <see cref="UserInfo.Profile.SubjectId"/> — <c>Profile</c> has no
/// <c>Id</c> member). <c>null</c> leaves the list untouched: the pinned
/// <c>.OrderBy(p => p.Created)</c> (ascending — correction C-1) stays
/// byte-for-byte on the tag feeds, and the people lists stay in their
/// current (unsorted) storage order (C-SORT·2).
/// </summary>
public static class TagPeopleSortSupport
{
    /// <summary>The generic (non-string) ordering — the shared shape for the
    /// <c>created</c> (non-null) / <c>modified</c> (nullable,
    /// <c>?? MinValue</c> sentinel — in-memory, so the frozen comparator
    /// works as pinned) keys + the <c>ThenBy</c> tie-breaker (C-SORT·5).</summary>
    private static IEnumerable<T> By<T, K>(IEnumerable<T> q, Func<T, K> sel,
        bool descending, Func<T, string> id)
        => descending
            ? q.OrderByDescending(sel).ThenBy(id)
            : q.OrderBy(sel).ThenBy(id);

    /// <summary>The string-key ordering — the shared shape for the
    /// <c>title</c> / <c>name</c> keys: <c>OrdinalIgnoreCase</c>
    /// (tag→posts' <c>title</c> is nullable → <c>?? ""</c>;
    /// tag→pages' <c>title</c> and people's <c>DisplayName</c> are
    /// non-null, the sentinel is a no-op) + the <c>ThenBy</c> tie-breaker
    /// (C-SORT·5).</summary>
    private static IEnumerable<T> ByString<T>(IEnumerable<T> q, Func<T, string?> sel,
        bool descending, Func<T, string> id)
        => descending
            ? q.OrderByDescending(x => sel(x) ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(id)
            : q.OrderBy(x => sel(x) ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(id);

    /// <summary>The tag-feed ordering (rows 14/15) — the shared shape for
    /// <c>Post</c> / <c>Page</c> over their
    /// <c>created</c>/<c>modified</c>/<c>title</c> keys; the
    /// <c>allowedKeys</c> guard makes the switch <b>closed</b> per surface
    /// (D-SORT·2). <c>null</c> returns the sequence untouched (C-SORT·2);
    /// a key outside the allowlist falls to the pinned default branch
    /// (<c>created</c> — the surface's current order; C-SORT·1, unreachable
    /// in practice: <c>SortKeys.Parse</c> already fell back). The
    /// <paramref name="defaultDir"/> is the surface's pinned default
    /// direction (for these rows: <b>asc</b> — corrections C-1) and is
    /// applied on the default branch (the spec's own direction is
    /// meaningless for an out-of-allowlist key — the same C-SORT·1
    /// fallback as the Web's <c>Parse</c> default).</summary>
    public static IEnumerable<T> OrderByTagFeedSort<T>(
        IEnumerable<T> q, SortSpec? sort,
        IReadOnlySet<string> allowedKeys,
        bool defaultDir,
        Func<T, DateTimeOffset> created,
        Func<T, DateTimeOffset?> modified,
        Func<T, string?> title,
        Func<T, string> id)
    {
        if (sort is null)
            return q; // ← the pinned .OrderBy(p => p.Created) stays byte-for-byte (C-SORT·2)

        return sort.Key switch
        {
            _ when allowedKeys.Contains("created") && sort.Key == "created"
                => By(q, created, sort.Descending, id),
            _ when allowedKeys.Contains("modified") && sort.Key == "modified"
                => By(q, x => modified(x) ?? DateTimeOffset.MinValue, sort.Descending, id),
            _ when allowedKeys.Contains("title") && sort.Key == "title"
                => ByString(q, title, sort.Descending, id),
            _ => By(q, created, defaultDir, id), // C-SORT·1 — unreachable (Parse already fell back); the pinned default is applied anyway
        };
    }

    /// <summary>The people ordering (rows 16/17, correction C-2) — the
    /// <c>name</c> key over <see cref="UserInfo.Profile.DisplayName"/>
    /// (non-null, <c>OrdinalIgnoreCase</c>) + the
    /// <see cref="UserInfo.Profile.SubjectId"/> tie-breaker (C-SORT·5 —
    /// the call site passes <c>p => p.SubjectId</c> as the
    /// <paramref name="id"/> selector). <c>null</c> returns the list
    /// untouched (C-SORT·2 — the current unsorted storage order); a key
    /// outside the allowlist falls to the <c>name</c> default branch
    /// (C-SORT·1, unreachable in practice — <c>SortKeys.Parse</c> already
    /// fell back).</summary>
    public static IEnumerable<T> OrderByPeopleSort<T>(
        IEnumerable<T> q, SortSpec? sort,
        IReadOnlySet<string> allowedKeys,
        bool defaultDir,
        Func<T, string> name,
        Func<T, string> id)
    {
        if (sort is null)
            return q; // ← the list stays in its current order (C-SORT·2)

        return sort.Key switch
        {
            _ when allowedKeys.Contains("name") && sort.Key == "name"
                => ByString(q, name, sort.Descending, id),
            _ => ByString(q, name, defaultDir, id), // C-SORT·1 — unreachable (Parse already fell back); the pinned default is applied anyway
        };
    }
}
