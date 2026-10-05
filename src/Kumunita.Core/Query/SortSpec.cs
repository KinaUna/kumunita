namespace Kumunita.Core.Query;

/// <summary>
/// A resolved sort request (C-SORT·1/3/7). <see cref="Key"/> is a
/// **lowercase** surface key (e.g. <c>"created"</c>) — the raw <c>?sort=</c>
/// value is mapped against the surface's **closed allowlist** (D-SORT·2)
/// before this is ever constructed, so a seam never sees a raw string;
/// <see cref="Descending"/> is the **resolved** direction (C-SORT·1). A
/// value object: no I/O, no HTTP (C-SORT·3/7).
/// </summary>
public sealed record SortSpec(string Key, bool Descending);

/// <summary>
/// The **pure** parser (C-SORT·1/3): maps a raw <c>?sort=</c>/<c>?dir=</c>
/// pair against a surface's **closed** allowlist. **No reflection, no
/// property lookup, no I/O** (no sort-injection, R1) — the raw string is
/// matched by value, never resolved to a member.
/// </summary>
public static class SortKeys
{
    public static SortSpec Parse(
        string? key, string? dir,
        IReadOnlySet<string> allowedKeys,
        string defaultKey, bool defaultDir)
    {
        // C-SORT·1 / F4: an unknown/blank key → the surface's default key.
        var k = (key ?? string.Empty).Trim().ToLowerInvariant();
        var resolvedKey = k.Length > 0 && allowedKeys.Contains(k)
            ? k
            : defaultKey;

        // C-SORT·1 / F5: "asc" → ascending, "desc" → descending,
        // null/other → the key's default direction. Never an error.
        var descending = dir switch
        {
            "asc" => false,
            "desc" => true,
            _ => defaultDir,
        };

        return new SortSpec(resolvedKey, descending);
    }
}
