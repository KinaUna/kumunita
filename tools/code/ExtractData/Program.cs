using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;

namespace ExtractData;

/// <summary>
/// C02 one-off extractor (ADR 0289): reads the current C# data literals from the
/// compiled Kumunita.Core assembly and writes them to the embedded-resource files:
/// per-language / per-surface translation-key JSON, and the guide / default-page
/// sets as per-slug Markdown + a small ordered index. By reading the values the C#
/// currently produces, the generated files are byte-identical to what the code ships
/// today — the loader swap is therefore a pure data move. Kept under tools/code/ per
/// the plan (a one-off script; delete after the lane if the data never changes shape).
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static string Root =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));

    private static string CoreSrc => Path.Combine(Root, "src", "Kumunita.Core");

    private static void Main()
    {
        WriteTranslationKeys();
        WritePageSets();
        WriteSnapshot();
        Console.WriteLine("ExtractData: done.");
    }

    // ── Translation keys: Keys/{lang}/{surface}.json ────────────────────────────────
    private static void WriteTranslationKeys()
    {
        var langs = new (string, IReadOnlyDictionary<string, string>)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        // Surface = the key's first dot-segment (ADR 0289 "surface = the key prefix").
        var surfaces = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (_, values) in langs)
            foreach (var key in values.Keys)
                surfaces.Add(SurfaceOf(key));

        var keysRoot = Path.Combine(CoreSrc, "Localization", "Keys");
        foreach (var (lang, values) in langs)
        {
            var dir = Path.Combine(keysRoot, lang);
            Directory.CreateDirectory(dir);
            foreach (var surface in surfaces)
            {
                var subset = values
                    .Where(kv => SurfaceOf(kv.Key) == surface)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
                if (subset.Count == 0) continue; // a surface absent in this language is simply not emitted
                File.WriteAllText(Path.Combine(dir, $"{surface}.json"),
                    JsonSerializer.Serialize(subset, JsonOpts) + "\n");
            }
        }
        Console.WriteLine($"  Keys: {surfaces.Count} surfaces × 4 languages → Localization/Keys/");
    }

    private static string SurfaceOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot <= 0 ? key : key[..dot];
    }

    // ── Guides + default pages ───────────────────────────────────────────────────────
    // Guides: ADR 0289 "read by slug" → one <slug>.md per page (the body, stored
    // byte-for-byte). The page's Title is invariantly its body's first "## " heading
    // (verified 176/176 across en/de/fr/da), so the loader derives it from the .md —
    // no sidecar, and the (slug, title, body)[] surface is reproduced exactly.
    // Default pages: the four-surface set's Title is NOT always its heading (terms:
    // 'Terms' vs 'Terms of use'; help: 'Help' vs 'Getting started'), so each <slug>.md
    // is paired with a small ordered index.json carrying {slug, title}. Order is the
    // registry order (the public arrays preserve it).
    private sealed record IndexEntry(string Slug, string Title);

    private static void WritePageSets()
    {
        WriteGuides("en", FirstBootSeeder.GuidePages());
        WriteGuides("de", FirstBootSeeder.DeGuidePages());
        WriteGuides("fr", FirstBootSeeder.FrGuidePages());
        WriteGuides("da", FirstBootSeeder.DaGuidePages());

        WriteDefaultPages("en", FirstBootSeeder.EnDefaultPages());
        WriteDefaultPages("de", FirstBootSeeder.DeDefaultPages());
        WriteDefaultPages("fr", FirstBootSeeder.FrDefaultPages());
        WriteDefaultPages("da", FirstBootSeeder.DaDefaultPages());

        var parentMap = FirstBootSeeder.GuideParentMap();
        var gDir = Path.Combine(CoreSrc, "Bootstrap", "GuidePages");
        File.WriteAllText(Path.Combine(gDir, "parent-map.json"),
            JsonSerializer.Serialize(parentMap, JsonOpts) + "\n");
        Console.WriteLine("  Page sets: guides (per-slug .md) + default pages (.md + index) + parent map.");
    }

    private static void WriteGuides(string lang, (string Slug, string Title, string Body)[] pages)
    {
        var dir = Path.Combine(CoreSrc, "Bootstrap", "GuidePages", lang);
        Directory.CreateDirectory(dir);
        var order = new List<string>(pages.Length);
        foreach (var (slug, title, body) in pages)
        {
            // Fail loud at generation time if the title==first-heading invariant is ever
            // broken — the loader derives the title from the heading, so a silent drift
            // here would change rendered text.
            if (FirstHeading(body) != title)
                throw new InvalidOperationException($"Guide title invariant broken: {lang}/{slug} '{title}' vs '{FirstHeading(body)}'");
            File.WriteAllText(Path.Combine(dir, $"{slug}.md"), body);
            order.Add(slug);
        }
        // Seed-order index: SeedUserGuidesAsync must visit top-level guides before their
        // nested children (the parent-map fallback), so the loader iterates slugs in this
        // order, not alphabetically. The bodies stay in the per-slug .md files.
        File.WriteAllText(Path.Combine(dir, "order.json"), JsonSerializer.Serialize(order, JsonOpts) + "\n");
    }

    private static void WriteDefaultPages(string lang, (string Slug, string Title, string Body)[] pages)
    {
        var dir = Path.Combine(CoreSrc, "Bootstrap", "DefaultPages", lang);
        Directory.CreateDirectory(dir);
        var index = new List<IndexEntry>(pages.Length);
        foreach (var (slug, title, body) in pages)
        {
            File.WriteAllText(Path.Combine(dir, $"{slug}.md"), body);
            index.Add(new IndexEntry(slug, title));
        }
        File.WriteAllText(Path.Combine(dir, "index.json"), JsonSerializer.Serialize(index, JsonOpts) + "\n");
    }

    private static string FirstHeading(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var t = raw.TrimStart();
            if (t.StartsWith("## ")) return t[3..].Trim();
        }
        return string.Empty;
    }

    // ── The temporary byte-identity snapshot (the C# values, captured pre-move) ─────
    private sealed record SnapSet(string Slug, string Title, string Body);

    private static void WriteSnapshot()
    {
        var snap = new
        {
            en = Dict(KnownTranslationKeys.EnValues),
            de = Dict(KnownTranslationKeys.DeValues),
            fr = Dict(KnownTranslationKeys.FrValues),
            da = Dict(KnownTranslationKeys.DaValues),
            guides = PageSnap(FirstBootSeeder.GuidePages()),
            deGuides = PageSnap(FirstBootSeeder.DeGuidePages()),
            frGuides = PageSnap(FirstBootSeeder.FrGuidePages()),
            daGuides = PageSnap(FirstBootSeeder.DaGuidePages()),
            defPages = PageSnap(FirstBootSeeder.EnDefaultPages()),
            deDefPages = PageSnap(FirstBootSeeder.DeDefaultPages()),
            frDefPages = PageSnap(FirstBootSeeder.FrDefaultPages()),
            daDefPages = PageSnap(FirstBootSeeder.DaDefaultPages()),
            parentMap = FirstBootSeeder.GuideParentMap(),
        };
        var dir = Path.Combine(Root, ".tmp");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "c02-values-snapshot.json"),
            JsonSerializer.Serialize(snap, JsonOpts) + "\n");
        Console.WriteLine("  Snapshot → .tmp/c02-values-snapshot.json");
    }

    private static Dictionary<string, string> Dict(IReadOnlyDictionary<string, string> d) =>
        d.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

    private static List<SnapSet> PageSnap((string Slug, string Title, string Body)[] pages) =>
        pages.Select(p => new SnapSet(p.Slug, p.Title, p.Body)).ToList();
}
