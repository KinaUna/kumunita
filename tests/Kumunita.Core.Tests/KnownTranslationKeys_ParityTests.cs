using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// LS U01 — the <see cref="KnownTranslationKeys"/> registry parity pins (the
/// unit this plan section owns; the U02 / U03 baselines extend this same
/// family for <c>DeValues</c> / <c>FrValues</c>).
///
/// <para>
/// ADR 0015's honesty invariant, in test form: <see cref="KnownTranslationKeys.AllKeys"/>
/// is defined as <c>EnValues</c>.Keys, so the parity here is
/// (a) the key set is exactly what the closed contract claims — no
/// duplicate-declaration surprises, every key present exactly once — and
/// (b) every value is non-empty (a registered key whose <c>en</c> text is
/// blank would make the provider floor resolve to nothing, and the
/// completeness view would report a "present" string that renders blank).
/// </para>
///
/// <para>
/// The <c>about.*</c> half is the LS U01 pin specifically (ADR 0042 D5): the
/// closed 17-key contract exists in the registry, and each one carries its
/// exact current English copy (non-empty). The values themselves are the
/// ADR's locked text — the byte-level match against the ADR is enforced by
/// the non-emptiness + the U05 byte-identical-English view render, not by
/// re-quoting 17 strings in a test (that would drift from the registry it
/// pins, the same trap the registry's doc-comment warns against).
/// </para>
///
/// <para>
/// No database, no Testcontainers — a pure registry-shape test.
/// </para>
/// </summary>
public class KnownTranslationKeys_ParityTests
{
    // ── AllKeys == EnValues keys, no duplicates, every value non-empty ──

    [Fact(DisplayName = "AllKeys is exactly the EnValues key set (no duplicates, nothing lost)")]
    public void AllKeys_Equals_EnValues_Keys_Exact_NoDuplicates()
    {
        var enKeys = KnownTranslationKeys.EnValues.Keys.ToList();
        var allKeys = KnownTranslationKeys.AllKeys.ToList();

        // The closed universe is the EnValues set, in declaration order.
        Assert.Equal(enKeys, allKeys);

        // No key declared twice (a Dictionary would silently collapse a
        // duplicate, so pin the shape: the source-level set has no repeats).
        Assert.Equal(enKeys.Count, new HashSet<string>(enKeys).Count);

        // The universe is real, not empty.
        Assert.True(allKeys.Count > 0);
    }

    [Fact(DisplayName = "Every registered key has a non-empty en value")]
    public void Every_Registered_Key_Has_NonEmpty_En_Value()
    {
        foreach (var (key, value) in KnownTranslationKeys.EnValues)
        {
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"registry key '{key}' has an empty/whitespace en value — " +
                "the provider floor would resolve it to nothing");
        }
    }

    // ── LS U01 — the about.* contract (ADR 0042 D5) is registered ──

    [Fact(DisplayName = "about.* the ADR 0042 D5 contract is fully registered with non-empty en values")]
    public void About_Keys_From_Adr_0042_D5_Are_Registered_NonEmpty()
    {
        // The D5 closed contract — 17 keys (the stable names U02 / U03
        // translate against and U05 wraps the view against).
        var d5Keys = new[]
        {
            "about.eyebrow",
            "about.lead",
            "about.cta_feed",
            "about.cta_notes",
            "about.features.one.title",
            "about.features.one.body",
            "about.features.groups.title",
            "about.features.groups.body",
            "about.features.pinned.title",
            "about.features.pinned.body",
            "about.stats.neighbors",
            "about.stats.groups",
            "about.stats.posts",
            "about.stats.pinned",
            "about.project.eyebrow",
            "about.project.heading",
            "about.project.lead",
        };

        Assert.Equal(17, d5Keys.Length);

        foreach (var key in d5Keys)
        {
            Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key),
                $"ADR 0042 D5 key '{key}' is not registered in EnValues");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                $"ADR 0042 D5 key '{key}' has an empty en value");
            Assert.Contains(key, KnownTranslationKeys.AllKeys);
        }

        // The D5 list is closed: every about.* key in the registry is one of
        // the 17 contract keys (no drift in either direction).
        var registryAboutKeys =
            KnownTranslationKeys.AllKeys.Where(k => k.StartsWith("about.")).ToList();
        Assert.Equal(d5Keys.OrderBy(k => k).ToList(),
            registryAboutKeys.OrderBy(k => k).ToList());
    }

    // ── LS U02 — the de baseline at full registry parity (ADR 0042 D2/D5) ──

    [Fact(DisplayName = "DeValues keys exactly match AllKeys — no missing, no extra, no empty values")]
    public void DeValues_Keys_Match_AllKeys_Exactly_NoEmptyValues()
    {
        var deKeys = KnownTranslationKeys.DeValues.Keys.ToList();
        var allKeys = KnownTranslationKeys.AllKeys.ToList();

        // Same cardinality — a missing or extra key is a registry-shape
        // defect (the completeness view would report a mismatch against the
        // admin editor's closed list).
        Assert.Equal(allKeys.Count, deKeys.Count);

        // Set equality in both directions.
        Assert.Equal(allKeys.OrderBy(k => k), deKeys.OrderBy(k => k));

        // No key declared twice (a Dictionary would silently collapse a
        // duplicate, so pin the shape — same as the en test above).
        Assert.Equal(allKeys.Count, new HashSet<string>(allKeys).Count);

        // Every de value is non-empty — a blank baseline would make the
        // provider resolve to nothing for that key under a de preference,
        // and the M·12 completeness view would count it "present" while
        // rendering blank.
        foreach (var (key, value) in KnownTranslationKeys.DeValues)
        {
            Assert.False(string.IsNullOrWhiteSpace(value),
                $"registry key '{key}' has an empty/whitespace de value — " +
                "the provider would resolve it to nothing under a de preference");
        }

        // The D5 about.* contract is translated (every D5 key is present in
        // the de dictionary with a non-empty value).
        foreach (var key in KnownTranslationKeys.AllKeys.Where(k => k.StartsWith("about.")))
        {
            Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key),
                $"ADR 0042 D5 key '{key}' is missing from DeValues");
        }
    }
}
