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
            new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "SITE-2", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "ESC", "M33", "M34", "M35", "M36", "M37" },
            Ids.ToList());
    }

    [Fact]
    public void Shipped_Milestones_Are_Marked_Done()
    {
        // M37 (personal notes) is the **last** milestone on the roadmap, now
        // **done** (the M37 close, U06). The shipped set is **every** id
        // through M37 — the "last milestone, no successor" pin (the M36·10 /
        // M35·11 framing, re-applied).
        foreach (string id in new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "SITE-2", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "ESC", "M33", "M34", "M35", "M36", "M37" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void M37_Is_The_Last_Milestone_With_No_Successor()
    {
        // M37 (personal notes) is the **last** milestone on the roadmap —
        // there is no <c>M38</c> to promote (the M36·10 / M35·11 "last
        // milestone, no successor" framing, re-applied). The M37 close
        // (U06) flipped M37 → done and replaced the prior
        // "M37_Is_The_Single_InProgress_Milestone_And_The_Last" pin with
        // this "no in-progress milestone" assertion: the roadmap is fully
        // shipped (every entry is <c>StatusDone</c>) and the last entry is
        // <c>M37</c> (no successor).
        var inProgress = Milestones.All.Where(m => m.Status == Milestones.StatusNext).ToList();
        Assert.Empty(inProgress);
        // M37 is the **last** entry in the roadmap (the M37 close pin — no
        // <c>M38</c>).
        Assert.Equal("M37", Milestones.All.Last().Id);
        // And it is <c>done</c> (the close flip — the D9 six-member pin).
        Assert.Equal(Milestones.StatusDone, Milestones.All.Last().Status);
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
