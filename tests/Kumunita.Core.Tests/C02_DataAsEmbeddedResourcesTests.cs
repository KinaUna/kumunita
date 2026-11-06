using System.Text.Json;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C02 (ADR 0289) — the data-move parity tests (the permanent guard).
/// <para>
/// The C# registry and the seeded guide / default-page data moved out of C#
/// literals into embedded resources: <c>Localization/Keys/{en,de,fr,da}/&lt;surface&gt;.json</c>
/// for the UI-string keys (one file per surface = the key's first dot-segment),
/// <c>Bootstrap/GuidePages/{lang}/&lt;slug&gt;.md</c> + <c>order.json</c> +
/// <c>parent-map.json</c> for the resident guides, and
/// <c>Bootstrap/DefaultPages/{lang}/&lt;slug&gt;.md</c> + <c>index.json</c> for the
/// four-surface set. Because resources now fail at run time rather than at compile
/// time, this file is the ongoing guard: it pins the closed-registry invariants the
/// ADR 0015 honesty rule and the admin editor's closed set depend on —
/// cross-language parity (every <c>en</c> key is present in de / fr / da, or is
/// listed as intentionally missing), no key living in two surface files, no orphan
/// key in a non-<c>en</c> language, and the guide nesting referencing real slugs.
/// </para>
/// <para>
/// <b>Why there is no byte-for-byte value snapshot here.</b> The byte-identity of
/// the move (loader values == the pre-move C# literals) was verified during C02
/// against a one-off snapshot and is recorded in the lane handoff; that snapshot was
/// then deleted, per the plan ("one commit, then the snapshot is deleted"). A
/// permanent value snapshot would defeat ADR 0289's own purpose — a new key is a
/// JSON entry, not a C# dictionary entry — so the ongoing guard is the structural
/// parity above, plus the existing value assertions scattered across the suites.
/// </para>
/// </summary>
public sealed class C02_DataAsEmbeddedResourcesTests
{
    // A key present in `en` but absent in a non-`en` language is a deliberate gap,
    // not a bug. Today the de / fr / da baselines are full-registry (the ADR 0015
    // honesty invariant extended to all three — the pre-C02 C# already required it),
    // so these lists are empty. The contract is the "present-or-listed-here" rule:
    // a future intentionally-partial baseline is pinned here, not silently dropped.
    private static readonly string[] IntentionallyMissingInDe = [];
    private static readonly string[] IntentionallyMissingInFr = [];
    private static readonly string[] IntentionallyMissingInDa = [];

    // ── Parity: every `en` key exists in de / fr / da (or is listed missing) ───────

    [Theory]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("da")]
    public void EveryEnKeyExistsInLanguage_OrIsListedIntentionallyMissing(string lang)
    {
        var en = KnownTranslationKeys.EnValues;
        var values = Accessor.GetValues(lang);
        var allowed = AllowedFor(lang);

        var missing = en.Keys.Where(k => !values.ContainsKey(k) && !allowed.Contains(k)).OrderBy(k => k).ToList();
        Assert.True(
            missing.Count == 0,
            $"Language '{lang}' is missing {missing.Count} `en` key(s) not in the intentionally-missing list: " +
            $"{string.Join(", ", missing.Take(10))}" + (missing.Count > 10 ? $" (…+{missing.Count - 10} more)" : ""));
    }

    // The `en` set is the closed universe (the M·9 floor / the admin editor's closed
    // set). A non-`en` language must not carry a key the `en` registry does not know
    // (an orphan key would let the editor's closed set and the language drift apart).
    [Theory]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("da")]
    public void NonEnLanguagesHaveNoOrphanKeys(string lang)
    {
        var en = KnownTranslationKeys.EnValues;
        var values = Accessor.GetValues(lang);
        var orphans = values.Keys.Where(k => !en.ContainsKey(k)).OrderBy(k => k).ToList();
        Assert.True(orphans.Count == 0,
            $"Language '{lang}' carries {orphans.Count} key(s) absent from the `en` registry: {string.Join(", ", orphans.Take(10))}");
    }

    // ── No duplicate keys across a language's surface files ────────────────────────
    // A key belongs to exactly one surface (its first dot-segment). The loader already
    // fails loud on a cross-file duplicate; this pins the invariant independently and
    // would name the offending key even if the loader's guard regressed.
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("da")]
    public void NoKeyIsDefinedInMoreThanOneSurfaceFile(string lang)
    {
        var prefix = "Kumunita.Core.Localization.Keys." + lang + ".";
        var asm = typeof(KnownTranslationKeys).Assembly;
        var files = asm.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var seen = new Dictionary<string, string>(StringComparer.Ordinal); // key → first surface file
        var dupes = new List<string>();
        foreach (var name in files)
        {
            using var stream = asm.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"resource {name} not found");
            var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
            foreach (var key in values.Keys)
                if (!seen.TryAdd(key, name))
                    dupes.Add(key);
        }

        Assert.True(dupes.Count == 0,
            $"Language '{lang}' defines {dupes.Count} key(s) in more than one surface file: {string.Join(", ", dupes.Take(10))}");
        // The loader's merged surface must equal the union of the files (nothing lost).
        Assert.True(Accessor.GetValues(lang).Count == seen.Count,
            $"Language '{lang}': loader serves {Accessor.GetValues(lang).Count} keys but the surface files carry {seen.Count} distinct keys.");
    }

    // ── Guides: cross-language parity + a well-formed nesting map ──────────────────
    [Theory]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("da")]
    public void EveryEnGuideHasACounterpartInLanguage(string lang)
    {
        var enGuides = SeededContentCatalog.GuidePages().Select(g => g.Slug).ToHashSet(StringComparer.Ordinal);
        var langGuides = Accessor.GuideSet(lang).Select(g => g.Slug).ToHashSet(StringComparer.Ordinal);

        var missing = enGuides.Where(s => !langGuides.Contains(s)).OrderBy(s => s).ToList();
        Assert.True(missing.Count == 0,
            $"Language '{lang}' is missing {missing.Count} guide(s) present in `en`: {string.Join(", ", missing.Take(10))}");
    }

    [Fact(DisplayName = "C02: every nested-guide parent is itself a seeded guide slug")]
    public void GuideParentMapReferencesOnlySeededGuideSlugs()
    {
        var parentMap = SeededContentCatalog.GuideParentMap();
        var slugs = SeededContentCatalog.GuidePages().Select(g => g.Slug).ToHashSet(StringComparer.Ordinal);

        // Every child in the map is a real guide; every parent is a real guide (so
        // SeedUserGuidesAsync's parent resolution always finds a seeded parent).
        var badChildren = parentMap.Keys.Where(s => !slugs.Contains(s)).OrderBy(s => s).ToList();
        var badParents = parentMap.Values.Where(s => !slugs.Contains(s)).OrderBy(s => s).ToList();
        Assert.True(badChildren.Count == 0, $"Parent map references {badChildren.Count} child slug(s) not a seeded guide: {string.Join(", ", badChildren)}");
        Assert.True(badParents.Count == 0, $"Parent map references {badParents.Count} parent slug(s) not a seeded guide: {string.Join(", ", badParents)}");
    }

    // ── The en floor stays a graceful lookup: a missing key resolves to itself ─────
    [Fact(DisplayName = "C02: an unregistered key is simply absent (the en floor never NREs)")]
    public void AnUnregisteredKeyIsAbsentNotNull()
    {
        // A data move must not turn the provider's graceful `en` floor (ADR 0015 D1:
        // an unknown key renders as itself) into a null reference. The loader's
        // dictionary has no entry for an unknown key — no exception, no null value.
        Assert.False(KnownTranslationKeys.EnValues.ContainsKey("does.not.exist.anywhere"));
        // And a known key still serves its value (a stable, long-standing key).
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey("nav.home"));
    }

    private static HashSet<string> AllowedFor(string lang) => (lang switch
    {
        "de" => IntentionallyMissingInDe,
        "fr" => IntentionallyMissingInFr,
        _ => IntentionallyMissingInDa,
    }).ToHashSet(StringComparer.Ordinal);

    // A test-only accessor for the language → registry / guide-set mapping (the public
    // surface is unchanged per ADR 0289 — these are the fixed members, just addressed
    // by code).
    private static class Accessor
    {
        public static IReadOnlyDictionary<string, string> GetValues(string lang) => lang switch
        {
            "en" => KnownTranslationKeys.EnValues,
            "de" => KnownTranslationKeys.DeValues,
            "fr" => KnownTranslationKeys.FrValues,
            "da" => KnownTranslationKeys.DaValues,
            _ => throw new ArgumentException($"unknown language '{lang}'", nameof(lang)),
        };

        public static (string Slug, string Title, string Body)[] GuideSet(string lang) => lang switch
        {
            "en" => SeededContentCatalog.GuidePages(),
            "de" => SeededContentCatalog.DeGuidePages(),
            "fr" => SeededContentCatalog.FrGuidePages(),
            "da" => SeededContentCatalog.DaGuidePages(),
            _ => throw new ArgumentException($"unknown language '{lang}'", nameof(lang)),
        };
    }
}
