using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M18 U07 — the <see cref="KnownTranslationKeys"/> M18 closure pins (ADR 0119
/// D9): the 14-key recurring-events set (the <c>events.recurrence.*</c>
/// composer-picker half landed in <c>EnValues</c> with U05 and the
/// <c>events.series.*</c> detail-page half with U06; U07 adds the same 14 keys
/// to <c>DeValues</c> / <c>FrValues</c> / <c>DaValues</c>).
///
/// <para>
/// Two pins, both pure registry-shape tests (no database, no Testcontainers):
/// </para>
/// <list type="number">
///   <item>
///     <b>All 14 keys present in all four dictionaries</b> — the D9
///     "no English-only key" rule: every key must appear in
///     <c>EnValues</c> ∧ <c>DeValues</c> ∧ <c>FrValues</c> ∧ <c>DaValues</c>.
///     The reverse guard (every <c>events.recurrence.*</c> /
///     <c>events.series.*</c> key in any dictionary is in the 14-key set)
///     pins the closure in the other direction — a stray key under the wrong
///     shape fails here.
///   </item>
///   <item>
///     <b>No leaked term</b> — no value carries a non-localized "RRULE" /
///     "iCal" / "RFC 5545" / "EXRULE" substring (case-insensitive) in any of
///     the four dictionaries. These are the D9 "no leaked term" rule.
///   </item>
/// </list>
///
/// <para>
/// This file is the standing guard: any future unit that adds an
/// <c>events.recurrence.*</c> / <c>events.series.*</c> key to one dictionary
/// and not the others — or one that lands a leaked term — fails this test.
/// The keys are frozen (D9); only the per-dictionary values differ.
/// </para>
/// </summary>
public class KnownTranslationKeysClosureTests
{
    // The D9 closed set — the 14 recurring-events keys, verbatim (the M18 U07
    // unit plan's table is the single source of truth). All under the existing
    // `events.*` namespace; no new `event.*` / `recurrence.*` / `series.*`
    // namespace.
    private static readonly string[] M18Keys =
    {
        "events.recurrence.none",
        "events.recurrence.daily",
        "events.recurrence.weekly",
        "events.recurrence.monthly",
        "events.recurrence.yearly",
        "events.recurrence.interval",
        "events.recurrence.ends_after",
        "events.recurrence.ends_on",
        "events.recurrence.count",
        "events.recurrence.until",
        "events.series.repeats",
        "events.series.skip",
        "events.series.restore",
        "events.series.part_of",
    };

    // The D9 "no leaked term" rule — the non-localized terms a user-visible
    // string must not carry (case-insensitive substring check).
    private static readonly string[] LeakedTerms =
    {
        "RRULE",
        "iCal",
        "RFC 5545",
        "EXRULE",
    };

    [Fact(DisplayName = "All 14 M18 keys present in all four dictionaries (no English-only key)")]
    public void All_14_M18_Keys_Present_In_All_Four_Dictionaries()
    {
        Assert.Equal(14, M18Keys.Length);
        // The key set is unique (no duplicate in the D9 table).
        Assert.Equal(M18Keys.Length, new HashSet<string>(M18Keys).Count);

        var dictionaries = new (string Name, IReadOnlyDictionary<string, string> Values)[]
        {
            ("EnValues", KnownTranslationKeys.EnValues),
            ("DeValues", KnownTranslationKeys.DeValues),
            ("FrValues", KnownTranslationKeys.FrValues),
            ("DaValues", KnownTranslationKeys.DaValues),
        };

        // Forward guard: every one of the 14 keys is present in all four
        // dictionaries (no English-only key — a key in EnValues but missing
        // from De/Fr/Da fails here).
        foreach (var key in M18Keys)
        {
            foreach (var (name, values) in dictionaries)
            {
                Assert.True(values.ContainsKey(key),
                    $"M18 D9 key '{key}' is missing from {name}");
                Assert.False(string.IsNullOrWhiteSpace(values[key]),
                    $"M18 D9 key '{key}' has an empty value in {name}");
            }
        }

        // Reverse guard: every key under `events.recurrence.*` or
        // `events.series.*` in any dictionary is in the 14-key set (a stray
        // key under the wrong shape fails here).
        foreach (var (name, values) in dictionaries)
        {
            foreach (var key in values.Keys)
            {
                var isM18Namespace =
                    key.StartsWith("events.recurrence.", StringComparison.Ordinal) ||
                    key.StartsWith("events.series.", StringComparison.Ordinal);
                if (!isM18Namespace)
                    continue;

                Assert.True(M18Keys.Contains(key),
                    $"M18 D9 closure: '{name}' carries '{key}' under the " +
                    "events.recurrence.* / events.series.* namespace, which is " +
                    "not in the 14-key closed set");
            }
        }
    }

    [Fact(DisplayName = "No M18 key leaks a non-localized term (RRULE / iCal / RFC 5545 / EXRULE)")]
    public void No_M18_Key_Leaks_A_NonLocalized_Term()
    {
        var dictionaries = new (string Name, IReadOnlyDictionary<string, string> Values)[]
        {
            ("EnValues", KnownTranslationKeys.EnValues),
            ("DeValues", KnownTranslationKeys.DeValues),
            ("FrValues", KnownTranslationKeys.FrValues),
            ("DaValues", KnownTranslationKeys.DaValues),
        };

        foreach (var key in M18Keys)
        {
            foreach (var (name, values) in dictionaries)
            {
                Assert.True(values.ContainsKey(key),
                    $"M18 D9 key '{key}' is missing from {name}");
                var value = values[key];

                foreach (var term in LeakedTerms)
                {
                    Assert.False(
                        value.Contains(term, StringComparison.OrdinalIgnoreCase),
                        $"M18 D9 key '{key}' in {name} leaks the non-localized " +
                        $"term '{term}': '{value}'");
                }
            }
        }
    }
}
