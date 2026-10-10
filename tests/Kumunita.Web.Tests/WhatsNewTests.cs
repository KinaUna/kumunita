using Kumunita.Core.Localization;
using Kumunita.Web;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the <see cref="WhatsNew"/> version registry (the <c>VN</c> lane, ADR
/// 0110) — the same "source-controlled static data, pin it" discipline as
/// <see cref="MilestonesTests"/> for <see cref="Milestones"/>.
///
/// Invariants a resident-facing "What's new" section + toast depend on:
///   - the list is non-empty and ordered newest-first (so <see cref="WhatsNew.Latest"/>
///     is the head and the toast announces the right version);
///   - versions are unique (no duplicate release rows);
///   - every row is well-formed (non-blank version + ISO date + at least one
///     change, none of them blank) — the About section renders these verbatim;
///   - the "What's new" UI chrome keys are all registered (the <c>kw-l</c>
///     floor is the registry, so a missing key would render as the raw key).
///
/// <para>
/// <b>Static, no Testcontainers:</b> a pure in-memory check of the shipped
/// registry — no host, no view resolution, no Postgres.
/// </para>
/// </summary>
public sealed class WhatsNewTests
{
    [Fact(DisplayName = "WhatsNew.All is non-empty")]
    public void All_Is_NonEmpty()
    {
        Assert.True(WhatsNew.All.Count > 0,
            "WhatsNew.All must have at least one release.");
    }

    [Fact(DisplayName = "WhatsNew.All is ordered newest-first (highest version first)")]
    public void All_Is_Ordered_NewestFirst()
    {
        var versions = WhatsNew.All.Select(v => ParseVersion(v.Version)).ToList();
        var sorted = versions.OrderByDescending(x => x).ToList();
        Assert.Equal(sorted.Count, versions.Count);
        for (var i = 0; i < versions.Count; i++)
        {
            Assert.True(sorted[i] == versions[i],
                $"Position {i}: expected {sorted[i].Maj}.{sorted[i].Min}.{sorted[i].Patch}, got {versions[i].Maj}.{versions[i].Min}.{versions[i].Patch}.");
        }
    }

    [Fact(DisplayName = "WhatsNew.Latest is the head of WhatsNew.All")]
    public void Latest_Is_The_Head()
    {
        Assert.Same(WhatsNew.All[0], WhatsNew.Latest);
        Assert.Equal(WhatsNew.All[0].Version, WhatsNew.Latest.Version);
    }

    [Fact(DisplayName = "Versions in WhatsNew.All are unique")]
    public void Versions_Are_Unique()
    {
        var versions = WhatsNew.All.Select(v => v.Version).ToList();
        var dupes = versions.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0,
            "Duplicate versions found: " + string.Join(", ", dupes));
    }

    [Fact(DisplayName = "The newest-first head is the M33 0.49.0 entry (M32 0.48.0 + M31 0.47.0 one/two rows back)")]
    public void The_Improve_Lane_Reduction_Entry_Is_Shipped()
    {
        // Pin the version-registry head (the required sixth close-flip member,
        // ADR 0110 / AGENTS.md) so a copy-paste that drops or re-orders an
        // entry is caught by the build instead of silently losing the
        // "What's new" announcement of the close. The head is newest-first:
        // when a milestone ships it becomes the head and the previous head
        // slides down. M33 (storage metrics history) is the newest shipped
        // milestone (the 0.49.0 entry, this M33 close flip), so it is now the
        // head; the M32 (issue submission & escalation) 0.48.0 entry slides
        // one row back, and the M31 (production error handling) 0.47.0 entry
        // must remain, two rows back.
        var head = WhatsNew.All[0];
        Assert.True(
            head.Version == "0.49.0" && head.Date == "2026-10-10",
            "The M33 0.49.0 (2026-10-10) entry must be the head of the registry; got "
                + head.Version + " / " + head.Date + ".");
        Assert.True(
            head.Changes.Any(c => c.Contains("storage metrics history", StringComparison.OrdinalIgnoreCase)),
            "The 0.49.0 head entry must name M33's storage metrics history capability.");

        // The M32 issue-submission & escalation entry slides one row back from the head.
        var m32 = WhatsNew.All.Single(v => v.Version == "0.48.0");
        Assert.True(
            m32.Changes.Any(c => c.Contains("issue submission", StringComparison.OrdinalIgnoreCase)),
            "The 0.48.0 entry must name M32's issue submission & escalation capability.");

        // The M31 production-error-handling entry is still shipped (not dropped by
        // the M33 close) — two rows back from the head.
        var m31 = WhatsNew.All.Single(v => v.Version == "0.47.0");
        Assert.True(
            m31.Changes.Any(c => c.Contains("error handling", StringComparison.OrdinalIgnoreCase)),
            "The M31 0.47.0 entry must name M31's production error handling capability.");

        // The M30 admin-onboarding entry is still shipped (not dropped by
        // the M32 close) — two rows back from the head.
        var m30 = WhatsNew.All.Single(v => v.Version == "0.46.0");
        Assert.True(
            m30.Changes.Any(c => c.Contains("admin onboarding", StringComparison.OrdinalIgnoreCase)),
            "The M30 0.46.0 entry must name M30's admin onboarding capability.");
    }

    [Fact(DisplayName = "Every WhatsNew row is well-formed (version, ISO date, ≥1 non-blank change)")]
    public void Every_Row_Is_WellFormed()
    {
        foreach (var v in WhatsNew.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Version),
                "A version row has a blank Version: " + v.Version);

            // The About section renders the date verbatim as a fixed,
            // timezone-independent fact — keep it a clean yyyy-MM-dd so the
            // <time datetime="…"> machine value is stable.
            Assert.True(IsIsoDate(v.Date),
                $"Version {v.Version}: Date '{v.Date}' should be ISO yyyy-MM-dd.");

            Assert.True(v.Changes.Count > 0,
                $"Version {v.Version} has no changes listed.");

            foreach (var c in v.Changes)
            {
                Assert.False(string.IsNullOrWhiteSpace(c),
                    $"Version {v.Version} has a blank change entry.");
            }
        }
    }

    [Fact(DisplayName = "The 'What's new' UI chrome keys are all registered")]
    public void WhatsNew_Keys_Are_Registered()
    {
        string[] keys =
        {
            "whatsnew.eyebrow",
            "whatsnew.heading",
            "whatsnew.lead",
            "whatsnew.version",
            "whatsnew.toast_label",
            "whatsnew.toast_see",
        };

        foreach (var key in keys)
        {
            Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key),
                "The 'what's new' UI key '" + key + "' is not in the KnownTranslationKeys " +
                "en registry — a resident would see the raw key instead of the label.");
            Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]),
                "The 'what's new' UI key '" + key + "' has a blank en value.");
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static bool IsIsoDate(string s)
    {
        if (s.Length != 10) return false;
        if (s[4] != '-' || s[7] != '-') return false;
        for (var i = 0; i < 4; i++) if (!char.IsDigit(s[i])) return false;
        for (var i = 5; i < 7; i++) if (!char.IsDigit(s[i])) return false;
        for (var i = 8; i < 10; i++) if (!char.IsDigit(s[i])) return false;
        return true;
    }

    private static VersionTuple ParseVersion(string v)
    {
        var parts = v.Split('.');
        return new VersionTuple(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            parts.Length > 2 ? int.Parse(parts[2]) : 0);
    }

    private readonly record struct VersionTuple(int Maj, int Min, int Patch)
        : IComparable<VersionTuple>
    {
        public int CompareTo(VersionTuple other)
        {
            var c = Maj.CompareTo(other.Maj);
            if (c != 0) return c;
            c = Min.CompareTo(other.Min);
            if (c != 0) return c;
            return Patch.CompareTo(other.Patch);
        }
    }
}
