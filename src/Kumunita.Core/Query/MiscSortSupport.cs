using System.Linq.Expressions;

namespace Kumunita.Core.Query;

/// <summary>
/// M26 U7 — the shared ordering helper for the three misc-list seams
/// (<see cref="Announcements.AnnouncementService.ListVisiblePagedAsync"/>,
/// <see cref="Documents.DocumentService.ListAsync"/>,
/// <see cref="Inventory.InventoryService.ListItemsAsync"/>): each applies
/// its **closed** allowlist (design doc §2.2 rows 11–13 —
/// announcements <c>created</c>/<c>modified</c>/<c>title</c>; documents
/// <c>created</c>/<c>modified</c>/<c>title</c>/<c>size</c>; inventory
/// <c>created</c>/<c>modified</c>/<c>name</c>) + the
/// <c>.ThenBy(x =&gt; x.Id)</c> tie-breaker (C-SORT·5). The three surfaces
/// differ only in their per-key **selector** and **allowlist** (the
/// <paramref name="allowedKeys"/> closed-set guard, D-SORT·2): documents'
/// <c>size</c> → <see cref="Documents.Document.SizeBytes"/>, a non-null
/// <c>long</c>; inventory's <c>name</c> →
/// <see cref="Inventory.InventoryItem.Name"/>, non-null. One generic helper
/// covers all three (the U6 selector technique — the models have no shared
/// base type). <c>null</c> leaves the query untouched so the pinned
/// <c>OrderByDescending(Created)</c> stays byte-for-byte (C-SORT·2).
/// <para>
/// <b>Marten 9.31.2 drift (carried from U4/U5/U6):</b> the frozen §2.2
/// comparator pins a <c>?? MinValue</c> sentinel on the nullable
/// <c>modified</c> key, but Marten's Linq parser rejects any non-member
/// OrderBy expression (<c>BadLinqExpressionException</c>); the key orders
/// on the **raw nullable column** and Postgres supplies the
/// null-ordering (nulls-last in asc, nulls-first in desc) — a documented
/// deviation, forced by the persistence provider, not a design choice
/// (pinned by the <c>MiscListSortTests</c> <c>SortModified*</c> tests).
/// The <c>title</c>/<c>name</c> keys keep the
/// <c>OrdinalIgnoreCase</c> comparator (non-null in all three models).
/// The <c>ThenBy</c> is invoked fully-qualified as
/// <c>Queryable.ThenBy(…)</c> — on Marten's <c>IAsyncQueryable</c> the
/// unqualified form is ambiguous (CS0411, confirmed U4/U5/U6).
/// </para>
/// </summary>
public static class MiscSortSupport
{
    /// <summary>The generic (non-string) ordering — the shared shape for
    /// the <c>created</c> / <c>modified</c> (nullable raw column) /
    /// <c>size</c> (numeric) keys (the <c>Queryable.OrderBy</c> generic
    /// overload + the <c>Queryable.ThenBy</c> tie-breaker, C-SORT·5).</summary>
    private static IQueryable<T> By<T, K>(IQueryable<T> q, Expression<Func<T, K>> sel,
        bool descending, Expression<Func<T, string>> id)
        => descending
            ? Queryable.ThenBy(q.OrderByDescending(sel), id)
            : Queryable.ThenBy(q.OrderBy(sel), id);

    /// <summary>The string-key ordering — the shared shape for the
    /// <c>title</c> / <c>name</c> keys: <c>OrdinalIgnoreCase</c> on the
    /// non-null comparison (both properties non-null in all three models)
    /// + the <c>ThenBy(Id)</c> tie-breaker (C-SORT·5).</summary>
    private static IQueryable<T> ByString<T>(IQueryable<T> q, Expression<Func<T, string>> sel,
        bool descending, Expression<Func<T, string>> id)
        => descending
            ? Queryable.ThenBy(q.OrderByDescending(sel, StringComparer.OrdinalIgnoreCase), id)
            : Queryable.ThenBy(q.OrderBy(sel, StringComparer.OrdinalIgnoreCase), id);

    public static IQueryable<T> OrderByMiscSort<T, TCreated, TModified, TSize>(
        IQueryable<T> q, SortSpec? sort,
        IReadOnlySet<string> allowedKeys,
        Expression<Func<T, TCreated>> created,
        Expression<Func<T, TModified>> modified,
        Expression<Func<T, string>> title,
        string sizeKey, Expression<Func<T, TSize>> size,
        string nameKey, Expression<Func<T, string>> name,
        Expression<Func<T, string>> id)
    {
        if (sort is null)
            return q; // ← the pinned line stays byte-for-byte (C-SORT·2)

        // C-SORT·5 — the .ThenBy(x => x.Id) tie-breaker on every non-null
        // sort path. Each branch delegates to the per-key-type static
        // helper (one generic lambda each — the U6 shape). The
        // <c>allowedKeys.Contains(…)</c> guards make the switch **closed**
        // per surface (D-SORT·2): a key absent from the surface's
        // allowlist (e.g. "title" on an inventory call, or "size"/"name"
        // on a surface that omits them) falls to the default branch, not
        // its own (C-SORT·1 — unreachable in practice: Parse already fell
        // back; the default is pinned anyway).
        return sort.Key switch
        {
            _ when allowedKeys.Contains("created") && sort.Key == "created"
                => By(q, created, sort.Descending, id),
            _ when allowedKeys.Contains("modified") && sort.Key == "modified"
                => By(q, modified, sort.Descending, id),
            _ when allowedKeys.Contains("title") && sort.Key == "title"
                => ByString(q, title, sort.Descending, id),
            _ when allowedKeys.Contains(sizeKey) && sort.Key == sizeKey
                => By(q, size, sort.Descending, id),
            _ when allowedKeys.Contains(nameKey) && sort.Key == nameKey
                => ByString(q, name, sort.Descending, id),
            _ => By(q, created, sort.Descending, id), // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
        };
    }
}
