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

    [Fact(DisplayName = "The newest-first head is the 0.82.0 entry (group messaging GM — ADR 0278) — 0.81.0 (directory sort & filter) slides one row back")]
    public void The_ONB_OnboardingWALK_Entry_Is_Shipped()
    {
        // Pin the version-registry head (the required sixth close-flip member,
        // ADR 0110 / AGENTS.md) so a copy-paste that drops or re-orders an
        // entry is caught by the build instead of silently losing the
        // "What's new" announcement of the close. The head is newest-first:
        // when a milestone ships it becomes the head and the previous head
        // slides down. The group-messaging lane (`GM`, ADR 0278, 2026-10-30
        // — multi-participant chat threads, the N:1 generalization of M9's
        // 1:1 surface: a new `Kumunita.Core.GroupMessaging` bounded context,
        // the multi-recipient picker, the any-participant-can-add membership
        // rule, the per-participant read marker, the two new
        // `group.message.new` / `group.message.mention` nudge kinds, and the
        // `/group-messages` account-nav surface riding the same M9 master
        // gate) is now the newest shipped (the 0.82.0 entry), so it is the
        // head; the directory sort & filter lane (ADR 0277, 2026-10-30)
        // 0.81.0 entry slides one row back; the portability-restore locale +
        // escalation-config lane (ADR 0276, 2026-10-30) 0.80.0 entry slides
        // two rows back; the portability-import principal-merge lane (ADR
        // 0275, 2026-10-30) 0.79.0 entry slides three rows back; the
        // portability-import tag-merge lane (ADR 0274, 2026-10-30) 0.78.0
        // entry slides four rows back; the localization-of-the-remaining-
        // surface lane (ADRs 0261–0273, 2026-10-30) 0.77.0 entry slides five
        // rows back; the Documents-uploader-attribution + delete-lane lane
        // (ADR 0239, 2026-10-29) 0.76.0 entry slides six rows back; and so
        // on.
        var head = WhatsNew.All[0];
        Assert.True(
            head.Version == "0.82.0" && head.Date == "2026-10-30",
            "The group messaging (GM) 0.82.0 (2026-10-30) entry must be the head of the registry; got "
                + head.Version + " / " + head.Date + ".");
        Assert.True(
            head.Changes.Any(c => c.Contains("Group messages", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("multi-participant", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0278", StringComparison.OrdinalIgnoreCase)),
            "The 0.82.0 head entry must name the group-messaging (GM) lane + ADR 0278.");
        // The previous head (the directory sort & filter lane, 0.81.0)
        // slides one row back — pin it too so a copy-paste that drops it is
        // caught.
        var second = WhatsNew.All[1];
        Assert.True(
            second.Version == "0.81.0" && second.Date == "2026-10-30",
            "The directory sort & filter 0.81.0 (2026-10-30) entry must be the second entry; got "
                + second.Version + " / " + second.Date + ".");
        Assert.True(
            second.Changes.Any(c => c.Contains("directory", StringComparison.OrdinalIgnoreCase)
                                   && c.Contains("Verified", StringComparison.OrdinalIgnoreCase)
                                   && c.Contains("ADR 0277", StringComparison.OrdinalIgnoreCase)),
            "The 0.81.0 entry must name the directory sort/filter lane + ADR 0277.");
        // The locale-restore + escalation lane (0.80.0) slides two rows back — pin it too.
        var third = WhatsNew.All[2];
        Assert.True(
            third.Version == "0.80.0" && third.Date == "2026-10-30",
            "The portability-restore locale + escalation 0.80.0 (2026-10-30) entry must be the third entry; got "
                + third.Version + " / " + third.Date + ".");
        Assert.True(
            third.Changes.Any(c => c.Contains("time zone", StringComparison.OrdinalIgnoreCase)
                                  && c.Contains("escalation", StringComparison.OrdinalIgnoreCase)
                                  && c.Contains("ADR 0276", StringComparison.OrdinalIgnoreCase)),
            "The 0.80.0 entry must name the locale-restore + escalation lane + ADR 0276.");
        // The principal-merge lane (0.79.0) slides three rows back — pin it too.
        var fourth = WhatsNew.All[3];
        Assert.True(
            fourth.Version == "0.79.0" && fourth.Date == "2026-10-30",
            "The portability-import principal-merge 0.79.0 (2026-10-30) entry must be the fourth entry; got "
                + fourth.Version + " / " + fourth.Date + ".");
        Assert.True(
            fourth.Changes.Any(c => c.Contains("directory", StringComparison.OrdinalIgnoreCase)
                                   && c.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                                   && c.Contains("ADR 0275", StringComparison.OrdinalIgnoreCase)),
            "The 0.79.0 entry must name the principal-merge lane + ADR 0275.");
        // The tag-merge lane (0.78.0) slides four rows back.
        var fifth = WhatsNew.All[4];
        Assert.True(
            fifth.Version == "0.78.0" && fifth.Date == "2026-10-30",
            "The portability-import tag-merge 0.78.0 (2026-10-30) entry must be the fifth entry; got "
                + fifth.Version + " / " + fifth.Date + ".");
        Assert.True(
            fifth.Changes.Any(c => c.Contains("tag", StringComparison.OrdinalIgnoreCase)
                                  && c.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                                  && c.Contains("ADR 0274", StringComparison.OrdinalIgnoreCase)),
            "The 0.78.0 entry must name the tag-merge lane + ADR 0274.");

        // The Documents-uploader-attribution + delete-lane entry (0.76.0) slides one row back.
        var documentsLane = WhatsNew.All.Single(v => v.Version == "0.76.0");
        Assert.True(
            documentsLane.Changes.Any(c => c.Contains("Documents", StringComparison.OrdinalIgnoreCase)
                                          && c.Contains("Uploaded by", StringComparison.OrdinalIgnoreCase)
                                          && c.Contains("ADR 0239", StringComparison.OrdinalIgnoreCase)),
            "The 0.76.0 entry must name the Documents-uploader-attribution + delete-lane + the ADR 0239.");

        // The Kanban-board-card-edit-modal entry (0.75.0) slides two rows back.
        var kanbanBoardModal = WhatsNew.All.Single(v => v.Version == "0.75.0");
        Assert.True(
            kanbanBoardModal.Changes.Any(c => c.Contains("board", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("modal", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0195", StringComparison.OrdinalIgnoreCase)),
            "The 0.75.0 entry must name the Kanban-board-card-edit-modal lane + the ADR 0195.");

        // The Sample-data-MFA-lock entry (0.74.0) slides two rows back.
        var sampleMfaLock = WhatsNew.All.Single(v => v.Version == "0.74.0");
        Assert.True(
            sampleMfaLock.Changes.Any(c => c.Contains("MFA", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("sample", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("lock", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0194", StringComparison.OrdinalIgnoreCase)),
            "The 0.74.0 entry must name the Sample-data-MFA-lock lane + the ADR 0194.");

        // The Portability-import-modes entry (0.73.0) slides three rows back.
        var portabilityImport = WhatsNew.All.Single(v => v.Version == "0.73.0");
        Assert.True(
            portabilityImport.Changes.Any(c => c.Contains("portability import modes", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("keep existing", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0193", StringComparison.OrdinalIgnoreCase)),
            "The 0.73.0 entry must name the Portability-import-modes lane + the ADR 0193.");

        // The Security-tab + MFA-pairing-QR entry (0.72.0) slides four rows back.
        var securityTab = WhatsNew.All.Single(v => v.Version == "0.72.0");
        Assert.True(
            securityTab.Changes.Any(c => c.Contains("Security", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("QR", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0174", StringComparison.OrdinalIgnoreCase)),
            "The 0.72.0 entry must name the Security-tab + MFA-pairing-QR lane + the ADR 0174.");

        // The Report-targets entry (0.71.0) slides two rows back.
        var reportTargets = WhatsNew.All.Single(v => v.Version == "0.71.0");
        Assert.True(
            reportTargets.Changes.Any(c => c.Contains("report", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0173", StringComparison.OrdinalIgnoreCase)),
            "The 0.71.0 entry must name the Report-targets lane + the ADR 0173.");

        // The Kiosk conditional-refresh entry (0.70.0) slides two rows back.
        var kioskRefresh = WhatsNew.All.Single(v => v.Version == "0.70.0");
        Assert.True(
            kioskRefresh.Changes.Any(c => c.Contains("kiosk auto-refresh", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("ADR 0164 amendment", StringComparison.OrdinalIgnoreCase)),
            "The 0.70.0 entry must name the Kiosk conditional-refresh lane + the ADR 0164 amendment.");

        // The Page-level notes entry (0.69.0) slides two rows back.
        var pageNotes = WhatsNew.All.Single(v => v.Version == "0.69.0");
        Assert.True(
            pageNotes.Changes.Any(c => c.Contains("page-level notes", StringComparison.OrdinalIgnoreCase)
                                      && c.Contains("ADR 0167 amendment", StringComparison.OrdinalIgnoreCase)),
            "The 0.69.0 entry must name the Page-level notes lane + the ADR 0167 amendment.");

        // The Prod-hardening entry (0.68.0) slides two rows back.
        var prodHardening = WhatsNew.All.Single(v => v.Version == "0.68.0");
        Assert.True(
            prodHardening.Changes.Any(c => c.Contains("prod-hardening", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("least-privilege db role", StringComparison.OrdinalIgnoreCase)),
            "The 0.68.0 entry must name the Prod-hardening lane close + the F22 two-role split (ADR 0171).");

        // The Bookmark-and-note-inventory-and-directory entry (0.67.0) slides one row back.
        var inventoryDirectory = WhatsNew.All.Single(v => v.Version == "0.67.0");
        Assert.True(
            inventoryDirectory.Changes.Any(c => c.Contains("inventory", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("directory", StringComparison.OrdinalIgnoreCase)
                                 && c.Contains("bookmark", StringComparison.OrdinalIgnoreCase)),
            "The 0.67.0 entry must name the inventory + directory bookmark + note capability.");

        // The Bookmark-projects-boards entry (0.66.0) slides two rows back.
        var bookmarkProjectsBoards = WhatsNew.All.Single(v => v.Version == "0.66.0");
        Assert.True(
            bookmarkProjectsBoards.Changes.Any(c => c.Contains("bookmark", StringComparison.OrdinalIgnoreCase)
                                 && (c.Contains("project", StringComparison.OrdinalIgnoreCase)
                                     || c.Contains("board", StringComparison.OrdinalIgnoreCase))),
            "The 0.66.0 entry must name the bookmark-projects-boards capability.");

        // The first-day-of-week entry (0.65.0) slides one row back.
        var firstDayOfWeek = WhatsNew.All.Single(v => v.Version == "0.65.0");
        Assert.True(
            firstDayOfWeek.Changes.Any(c => c.Contains("first day of week", StringComparison.OrdinalIgnoreCase)),
            "The 0.65.0 entry must name the first-day-of-week capability.");

        // The Kiosk display-mode entry (0.64.0) slides two rows back.
        var kioskDisplayMode = WhatsNew.All.Single(v => v.Version == "0.64.0");
        Assert.True(
            kioskDisplayMode.Changes.Any(c => c.Contains("display mode", StringComparison.OrdinalIgnoreCase)
                                              && c.Contains("kiosk", StringComparison.OrdinalIgnoreCase)),
            "The 0.64.0 entry must name the Kiosk display-mode (full page vs. full screen) capability.");

        // The Kiosk per-resident entry (0.63.0) slides two rows back.
        var kioskPerResident = WhatsNew.All.Single(v => v.Version == "0.63.0");
        Assert.True(
            kioskPerResident.Changes.Any(c => c.Contains("kiosk", StringComparison.OrdinalIgnoreCase)
                                              && c.Contains("per-resident", StringComparison.OrdinalIgnoreCase)),
            "The 0.63.0 entry must name the Kiosk per-resident capability.");

        // The Bookmark-folders entry (0.62.0) slides one row back.
        var bookmarkFolders = WhatsNew.All.Single(v => v.Version == "0.62.0");
        Assert.True(
            bookmarkFolders.Changes.Any(c => c.Contains("bookmark", StringComparison.OrdinalIgnoreCase)),
            "The 0.62.0 entry must name the Bookmark-folders capability.");

        // The M37 Personal-notes entry (0.61.0) slides two rows back.
        var personalNotes = WhatsNew.All.Single(v => v.Version == "0.61.0");
        Assert.True(
            personalNotes.Changes.Any(c => c.Contains("personal notes", StringComparison.OrdinalIgnoreCase)),
            "The 0.61.0 entry must name the M37 Personal-notes capability.");

        // The Events↔boards lane entry (0.60.0) slides two rows back.
        var eventsBoards = WhatsNew.All.Single(v => v.Version == "0.60.0");
        Assert.True(
            eventsBoards.Changes.Any(c => c.Contains("board", StringComparison.OrdinalIgnoreCase)
                                         && c.Contains("event", StringComparison.OrdinalIgnoreCase)),
            "The 0.60.0 entry must name the Events ↔ boards linkage capability.");

        // The ONB onboarding walk-through entry slides three rows back.
        var onb = WhatsNew.All.Single(v => v.Version == "0.59.0");
        Assert.True(
            onb.Changes.Any(c => c.Contains("onboarding", StringComparison.OrdinalIgnoreCase)),
            "The 0.59.0 entry must name the onboarding walk-through capability.");

        // The M36 @mentions entry (ADR 0165) slides two rows back.
        var m36 = WhatsNew.All.Single(v => v.Version == "0.58.0");
        Assert.True(
            m36.Changes.Any(c => c.Contains("mention", StringComparison.OrdinalIgnoreCase)),
            "The 0.58.0 entry must name the M36 @mentions capability.");

        // The M35 kiosk-mode entry (ADR 0164) slides two rows back.
        var kiosk = WhatsNew.All.Single(v => v.Version == "0.57.0");
        Assert.True(
            kiosk.Changes.Any(c => c.Contains("kiosk", StringComparison.OrdinalIgnoreCase)),
            "The 0.57.0 entry must name the M35 kiosk mode capability.");

        // The home upcoming events & to-dos entry (ADR 0163) slides one row back.
        var upcoming = WhatsNew.All.Single(v => v.Version == "0.56.0");
        Assert.True(
            upcoming.Changes.Any(c => c.Contains("upcoming", StringComparison.OrdinalIgnoreCase)),
            "The 0.56.0 entry must name the home upcoming events & to-dos capability.");

        // The M34 (analytics history) entry slides two rows back.
        var analytics = WhatsNew.All.Single(v => v.Version == "0.55.0");
        Assert.True(
            analytics.Changes.Any(c => c.Contains("analytics history", StringComparison.OrdinalIgnoreCase)),
            "The 0.55.0 entry must name M34's analytics history capability.");

        // The ADR 0161 page-translation-standing entry slides two rows back.
        var page = WhatsNew.All.Single(v => v.Version == "0.54.0");
        Assert.True(
            page.Changes.Any(c => c.Contains("page translations", StringComparison.OrdinalIgnoreCase)),
            "The 0.54.0 entry must name the ADR 0161 page-translations-for-editors capability.");

        // The NESTED nested-help-guides entry (ADR 0160) slides two rows back.
        var nested = WhatsNew.All.Single(v => v.Version == "0.53.0");
        Assert.True(
            nested.Changes.Any(c => c.Contains("nested help guides", StringComparison.OrdinalIgnoreCase)),
            "The 0.53.0 entry must name the NESTED nested help guides capability.");

        // The ESC escalation-authorization entry (ADR 0159) slides three rows back.
        var esc = WhatsNew.All.Single(v => v.Version == "0.52.0");
        Assert.True(
            esc.Changes.Any(c => c.Contains("escalation authorization", StringComparison.OrdinalIgnoreCase)),
            "The 0.52.0 entry must name the ESC escalation authorization capability.");

        // The LBL-2 surface-label-translation entry slides four rows back.
        var lbl2 = WhatsNew.All.Single(v => v.Version == "0.51.0");
        Assert.True(
            lbl2.Changes.Any(c => c.Contains("surface label translations", StringComparison.OrdinalIgnoreCase)),
            "The 0.51.0 entry must name the LBL-2 surface label translations capability.");

        // The SITE-2 site-content hero-translation entry slides five rows back.
        var site2 = WhatsNew.All.Single(v => v.Version == "0.50.0");
        Assert.True(
            site2.Changes.Any(c => c.Contains("site content hero translations", StringComparison.OrdinalIgnoreCase)),
            "The 0.50.0 entry must name the SITE-2 site content hero translations capability.");

        // The M33 storage-metrics-history entry slides six rows back.
        var m33 = WhatsNew.All.Single(v => v.Version == "0.49.0");
        Assert.True(
            m33.Changes.Any(c => c.Contains("storage metrics history", StringComparison.OrdinalIgnoreCase)),
            "The 0.49.0 entry must name M33's storage metrics history capability.");

        // The M32 issue-submission & escalation entry slides seven rows back.
        var m32 = WhatsNew.All.Single(v => v.Version == "0.48.0");
        Assert.True(
            m32.Changes.Any(c => c.Contains("issue submission", StringComparison.OrdinalIgnoreCase)),
            "The 0.48.0 entry must name M32's issue submission & escalation capability.");

        // The M31 production-error-handling entry is still shipped — eight rows
        // back from the head.
        var m31 = WhatsNew.All.Single(v => v.Version == "0.47.0");
        Assert.True(
            m31.Changes.Any(c => c.Contains("error handling", StringComparison.OrdinalIgnoreCase)),
            "The M31 0.47.0 entry must name M31's production error handling capability.");

        // The M30 admin-onboarding entry is still shipped — nine rows back
        // from the head.
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
