using Kumunita.Core.Localization;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U16 (F12) — the closed <see cref="KnownTranslationKeys"/>
/// <c>sort.*</c> set the shared <c>Views/Shared/_Sort.cshtml</c> partial emits
/// (<c>&lt;kw-l key="sort.{key}"&gt;</c>) is registered, present, non-empty, in
/// **all four** languages (en / de / fr / da) — the four-language parity
/// closure the <c>documents.*</c> / <c>a11y.*</c> closed-set lane established
/// (the <c>KwLRegistryConsistencyTests</c> + Core.Tests'
/// <c>KnownTranslationKeys_ParityTests</c> pins extend automatically from
/// the registry side; this pin names the exact 8-key closed set and asserts
/// each resolves — no blank, no missing-key degradation on the closed set).
/// </summary>
/// <remarks>
/// The closed set is exactly the eight sort keys the U10–U15 surfaces'
/// closed allowlists offer:
/// <list type="bullet">
/// <item><c>created</c> / <c>modified</c> / <c>title</c> — posts, events,
/// projects/goals/boards, announcements, documents, tags, search;</item>
/// <item><c>size</c> — documents (<c>DocumentFeedAllowedKeys</c>);</item>
/// <item><c>name</c> — inventory (<c>InventoryFeedAllowedKeys</c>) +
/// people-find (<c>PeopleFindAllowedKeys</c>);</item>
/// <item><c>start</c> — events (upcoming/past + group events);</item>
/// <item><c>due</c> / <c>status</c> — todos (<c>TodoFeedAllowedKeys</c>).</item>
/// </list>
///
/// <b>Deliberately absent (drift pauses, not omissions):</b>
/// <list type="bullet">
/// <item><c>sort.priority</c> — U12's drift pause: <c>TodoItem</c> has no
/// <c>Priority</c> property, so no surface offers it (the frozen Part-2
/// allowlist wins over the U12 brief's <c>due/priority/status</c> wording).</item>
/// <item><c>sort.label</c> / <c>sort.asc</c> / <c>sort.desc</c> — the
/// U16 brief's <i>e.g.</i> list names them, but the shipped <c>_Sort</c>
/// partial renders per-option links (no direction toggles) and its group
/// <c>aria-label</c> stays hardcoded (the value-free-attribute kw-l
/// exclusion, the <c>a11y.*</c> precedent). Registering keys no view
/// emits would make the registry a lie (the registry's own doc).</item>
/// </list>
///
/// No database, no Testcontainers — a pure registry-shape test.
/// </remarks>
public class SortKwL_En_De_Fr_Da
{
    /// <summary>
    /// The closed M26 <c>sort.*</c> set (F12) — exactly 8 keys: the union
    /// of the U10–U15 surfaces' closed allowlists (each allowlist is a
    /// subset of these eight; no surface offers a ninth).
    /// </summary>
    private static readonly string[] ClosedSet =
    {
        "sort.created",
        "sort.modified",
        "sort.title",
        "sort.size",
        "sort.name",
        "sort.start",
        "sort.due",
        "sort.status",
    };

    [Fact(DisplayName = "F12 — the closed sort.* set is exactly 8 keys (no priority/label/asc/desc)")]
    public void ClosedSet_Has_Exactly_8_Keys()
    {
        Assert.Equal(8, ClosedSet.Length);
        Assert.Equal(ClosedSet.Length, new HashSet<string>(ClosedSet).Count);

        // The drift pauses are pinned as absence: no sort.priority (U12) and
        // no sort.label/asc/desc (the partial renders no such strings) may be
        // registered — the closed set is exactly what the _Sort partial emits.
        var sortKeysInRegistry = KnownTranslationKeys.AllKeys
            .Where(k => k.StartsWith("sort.", System.StringComparison.Ordinal))
            .ToList();
        Assert.DoesNotContain("sort.priority", sortKeysInRegistry);
        Assert.DoesNotContain("sort.label", sortKeysInRegistry);
        Assert.DoesNotContain("sort.asc", sortKeysInRegistry);
        Assert.DoesNotContain("sort.desc", sortKeysInRegistry);
        Assert.Equal(ClosedSet.OrderBy(k => k).ToList(),
            sortKeysInRegistry.OrderBy(k => k).ToList());
    }

    [Fact(DisplayName = "F12 — every closed sort.* label resolves in en/de/fr/da (no blank, no missing key)")]
    public void SortKwL_Resolves_En_De_Fr_Da()
    {
        var registries = new (string Name, IReadOnlyDictionary<string, string> Dict)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        foreach (var (name, dict) in registries)
        {
            foreach (var key in ClosedSet)
            {
                Assert.True(dict.ContainsKey(key),
                    $"'{name}' registry missing '{key}' — a resident with a " +
                    $"{name} preference would see the raw key instead of a label.");
                Assert.False(string.IsNullOrWhiteSpace(dict[key]),
                    $"The '{key}' value in '{name}' must be non-empty (M·1: " +
                    "a resident never sees a blank label).");
            }
        }

        // The English floor: the en text is the provider floor (ADR 0015 D1)
        // — a de/fr/da row, once edited or removed by an admin, degrades to
        // this en text, never to nothing.
        foreach (var key in ClosedSet)
        {
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                $"the en floor for '{key}' must be non-empty (the ML-UI floor rule).");
        }
    }
}
