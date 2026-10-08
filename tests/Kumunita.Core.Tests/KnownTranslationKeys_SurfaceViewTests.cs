using System;
using System.Linq;
using Kumunita.Core.Localization;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// U04 (IMPROVE lane) — the <see cref="KnownTranslationKeys"/> per-surface view
/// parity pins.
///
/// <para>
/// The per-surface properties (<see cref="KnownTranslationKeys.AdminGuests"/>,
/// <see cref="KnownTranslationKeys.SettingsQuiet"/>, <see cref="KnownTranslationKeys.GuardianTimeLimit"/>,
/// <see cref="KnownTranslationKeys.ProjectsBoard"/>, <see cref="KnownTranslationKeys.PostsDetail"/>,
/// <see cref="KnownTranslationKeys.Announcements"/>, <see cref="KnownTranslationKeys.Pages"/>,
/// <see cref="KnownTranslationKeys.Tags"/>, <see cref="KnownTranslationKeys.Events"/>,
/// <see cref="KnownTranslationKeys.Groups"/>, <see cref="KnownTranslationKeys.Account"/>,
/// <see cref="KnownTranslationKeys.Common"/>) are derived views into
/// <see cref="KnownTranslationKeys.EnValues"/>. This test pins three invariants:
/// </para>
/// <ol>
/// <li><b>Subset</b> — every key in a per-surface view is a key in
/// <see cref="KnownTranslationKeys.EnValues"/>.</li>
/// <li><b>Byte-identical values</b> — for every key in a per-surface view, the
/// value equals <see cref="KnownTranslationKeys.EnValues"/>[key] exactly.</li>
/// <li><b>Complete partition</b> — the union of all per-surface views' keys
/// equals <see cref="KnownTranslationKeys.EnValues"/>.Keys exactly (no key
/// lost, no key duplicated across views).</li>
/// </ol>
/// <para>
/// No database, no Testcontainers — a pure registry-shape test.
/// </para>
/// </summary>
public class KnownTranslationKeys_SurfaceViewTests
{
    private static readonly (string Name, IReadOnlyDictionary<string, string> View)[] Views =
    {
        ("AdminGuests",      KnownTranslationKeys.AdminGuests),
        ("SettingsQuiet",    KnownTranslationKeys.SettingsQuiet),
        ("GuardianTimeLimit",KnownTranslationKeys.GuardianTimeLimit),
        ("ProjectsBoard",    KnownTranslationKeys.ProjectsBoard),
        ("PostsDetail",      KnownTranslationKeys.PostsDetail),
        ("Announcements",    KnownTranslationKeys.Announcements),
        ("Pages",            KnownTranslationKeys.Pages),
        ("Tags",             KnownTranslationKeys.Tags),
        ("Events",           KnownTranslationKeys.Events),
        ("Groups",           KnownTranslationKeys.Groups),
        ("Account",          KnownTranslationKeys.Account),
        ("Common",           KnownTranslationKeys.Common),
    };

    [Fact(DisplayName = "Every per-surface view is a subset of EnValues (keys + byte-identical values)")]
    public void Every_View_Is_Subset_Of_EnValues_With_ByteIdentical_Values()
    {
        foreach (var (name, view) in Views)
        {
            foreach (var (key, value) in view)
            {
                Assert.True(
                    KnownTranslationKeys.EnValues.ContainsKey(key),
                    $"{name} contains key '{key}' that is not in EnValues");

                Assert.True(
                    string.Equals(KnownTranslationKeys.EnValues[key], value, StringComparison.Ordinal),
                    $"{name}[\"{key}\"] does not match EnValues[\"{key}\"]: " +
                    $"expected '{KnownTranslationKeys.EnValues[key]}', got '{value}'");
            }
        }
    }

    [Fact(DisplayName = "The per-surface views form a complete partition of EnValues (no key lost, no key duplicated)")]
    public void Views_Form_Complete_Partition_Of_EnValues()
    {
        // Collect all keys across all views
        var allViewKeys = new List<string>();
        foreach (var (_, view) in Views)
        {
            allViewKeys.AddRange(view.Keys);
        }

        // No key appears in more than one view (pairwise disjoint)
        Assert.True(
            allViewKeys.Count == allViewKeys.Distinct().Count(),
            "A key appears in more than one per-surface view — the views must be pairwise disjoint");

        // The union of all views' keys == EnValues keys (complete, no key lost)
        var enKeys = KnownTranslationKeys.EnValues.Keys.OrderBy(k => k).ToList();
        var viewKeys = allViewKeys.Distinct().OrderBy(k => k).ToList();
        Assert.True(
            enKeys.SequenceEqual(viewKeys),
            "The union of per-surface view keys does not match EnValues keys — " +
            $"EnValues has {enKeys.Count} keys, views have {viewKeys.Count} keys");
    }

    [Fact(DisplayName = "AllKeys is unchanged and equals EnValues keys (the public surface is intact)")]
    public void AllKeys_Equals_EnValues_Keys()
    {
        Assert.True(
            KnownTranslationKeys.EnValues.Keys.OrderBy(k => k)
              .SequenceEqual(KnownTranslationKeys.AllKeys.OrderBy(k => k)),
            "AllKeys does not match EnValues keys");
    }
}
