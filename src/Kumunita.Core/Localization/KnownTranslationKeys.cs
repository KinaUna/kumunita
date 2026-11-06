namespace Kumunita.Core.Localization;

/// <summary>
/// The canonical, closed set of platform UI-string keys and their <c>en</c>
/// source text (ADR 0005; the <c>ML-UI</c> U1 deliverable — D2, the curated
/// bounded registry).
/// <para>
/// <b>Single source of truth.</b> Three readers, one shape: the <b>seeder</b>
/// (<c>FirstBootSeeder</c>) materializes each key as an <c>en</c>
/// <see cref="TranslationResource"/> row on first boot; the <b>TagHelper</b>
/// (<c>kw-l</c>) resolves a key through <see cref="ITranslationProvider"/> with
/// the <c>en</c> floor (ADR 0015 D1); the <b>admin editor</b> lists
/// <see cref="AllKeys"/> as the closed set with each key's <c>en</c> value as
/// the reference text.
/// </para>
/// <para>
/// <b>Data, not code (ADR 0289, C02).</b> The key → value data is embedded
/// resources, not C# literals: <c>Localization/Keys/{en,de,fr,da}/&lt;surface&gt;.json</c>,
/// one file per surface (the key's first dot-segment) per language. This class
/// is a thin loader; the <see cref="EnValues"/> / <see cref="DeValues"/> /
/// <see cref="FrValues"/> / <see cref="DaValues"/> / <see cref="AllKeys"/>
/// public surface is unchanged, so the ADR 0015 <c>kw-l</c> provider floor and
/// the M·12 completeness view are unaffected. A surface file absent for a
/// language (or a key missing from it) simply yields no entry for that language
/// — never a null reference (the <c>en</c> floor stays a graceful lookup).
/// </para>
/// </summary>
public static class KnownTranslationKeys
{
    /// <summary>The closed, curated key → <c>en</c> source-text set (the M·9 floor).</summary>
    public static IReadOnlyDictionary<string, string> EnValues { get; } = Load("en");

    /// <summary>The curated <c>de</c> baseline (LS U02, ADR 0042 D2/D5).</summary>
    public static IReadOnlyDictionary<string, string> DeValues { get; } = Load("de");

    /// <summary>The curated <c>fr</c> baseline (LS U03, ADR 0042 D2/D5).</summary>
    public static IReadOnlyDictionary<string, string> FrValues { get; } = Load("fr");

    /// <summary>The curated <c>da</c> baseline (ADR 0042 D2/D5).</summary>
    public static IReadOnlyDictionary<string, string> DaValues { get; } = Load("da");

    /// <summary>The closed key set — <c>EnValues.Keys</c>, in a stable read order.</summary>
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys.ToList();

    // The assembly's own embedded resources (this class is in Kumunita.Core).
    private const string Root = "Kumunita.Core.Localization.Keys.";

    /// <summary>
    /// Load one language's registry: every surface file for that language merged
    /// into a single key → value map. A surface absent for the language simply
    /// contributes no keys. A key present in two surface files is a data error
    /// (a key belongs to exactly one surface — its first dot-segment) and fails
    /// loud at run time; the C02 parity tests pin this.
    /// </summary>
    private static Dictionary<string, string> Load(string lang)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var prefix = Root + lang + ".";

        foreach (var name in SurfacesFor(lang))
        {
            var stream = typeof(KnownTranslationKeys).Assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException(
                    $"Translation-key resource '{name}' (surface '{name[prefix.Length..]}', " +
                    $"language '{lang}') is missing from the Kumunita.Core assembly — " +
                    "check the Localization/Keys embedded resources.");
            using (stream)
            {
                var values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
                    ?? throw new InvalidOperationException($"Translation-key resource '{name}' failed to deserialize.");
                foreach (var (key, value) in values)
                    if (!dict.TryAdd(key, value))
                        throw new InvalidOperationException(
                            $"Translation-key '{key}' (language '{lang}') is defined in more than one " +
                            "surface file — a key must belong to exactly one surface (its first dot-segment).");
            }
        }
        return dict;
    }

    // The surface resource names for a language, case-insensitively (the SDK may
    // normalize embedded-resource name casing), in ordinal order for a stable AllKeys.
    private static IEnumerable<string> SurfacesFor(string lang)
    {
        var prefix = Root + lang + ".";
        return typeof(KnownTranslationKeys).Assembly.GetManifestResourceNames()
            .Where(n => n.Length > prefix.Length && n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal);
    }
}
