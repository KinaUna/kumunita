using Kumunita.Web;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the public roadmap shown on the home page so a copy-paste reorder or a
/// forgotten status bump (e.g. M3 starting but still showing M2 as "In
/// progress") is caught by the build instead of silently lying to visitors.
/// </summary>
public class MilestonesTests
{
    private static readonly IEnumerable<string> Ids =
        Milestones.All.Select(m => m.Id);

    [Fact]
    public void Roadmap_Covers_M0_Through_M37_Plus_Named_Lanes_In_Order()
    {
        Assert.Equal(
            new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "SITE-2", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "ESC", "M33", "M34", "M35", "M36", "M37", "IMPROVE-3" },
            Ids.ToList());
    }

    [Fact]
    public void Shipped_Milestones_Are_Marked_Done()
    {
        // M37 (personal notes) is the **last** milestone on the roadmap, now
        // **done** (the M37 close, U06). The shipped set is **every** id
        // through M37 — the "last milestone, no successor" pin (the M36·10 /
        // M35·11 framing, re-applied).
        foreach (string id in new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "SITE-2", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "ESC", "M33", "M34", "M35", "M36", "M37", "IMPROVE-3" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void Roadmap_Has_No_InProgress_Entry_And_The_Last_Entry_Is_Shipped()
    {
        // The roadmap is **fully shipped** (no entry is <c>StatusNext</c>) —
        // the "no in-progress milestone" invariant the M37 close (U06)
        // established. The last entry is the most-recently-shipped named
        // lane: <c>IMPROVE-3</c> (the IMPROVE-CODE round-3 code-quality
        // reduction, ADRs 0292–0297) — the C3-08 close flip (the D6
        // six-member pin). The prior last entry was <c>M37</c> (personal
        // notes); the IMPROVE-3 entry is a code-quality lane (a shipped
        // platform change, not a resident feature), consistent with the
        // <c>IMPROVE</c> (round 1) and <c>ESC</c> named-lane precedent.
        var inProgress = Milestones.All.Where(m => m.Status == Milestones.StatusNext).ToList();
        Assert.Empty(inProgress);
        // The **last** entry in the roadmap is IMPROVE-3 (the most recent
        // shipped named lane — no M38, no successor milestone).
        Assert.Equal("IMPROVE-3", Milestones.All.Last().Id);
        // And it is <c>done</c> (the close flip — the D6 six-member pin).
        Assert.Equal(Milestones.StatusDone, Milestones.All.Last().Status);
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
