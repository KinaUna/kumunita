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
    public void Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order()
    {
        Assert.Equal(
            new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "M28", "IMPROVE" },
            Ids.ToList());
    }

    [Fact]
    public void Shipped_Milestones_Are_Marked_Done()
    {
        // M28 (guardian time limits) is now DONE — closed in the U09 close unit
        // (ADR 0151). M28 is the LAST milestone on the roadmap, so every
        // milestone is now done; the asserted set is the full `Ids` set.
        foreach (string id in new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "M28", "IMPROVE" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void Roadmap_Is_Fully_Shipped_No_InProgress_Milestone()
    {
        // M28 (guardian time limits) is the LAST milestone on the roadmap and is
        // now DONE — closed in the U09 close unit (ADR 0151). The "single
        // in-progress" premise no longer holds: the roadmap is complete, so
        // there is no `StatusNext` milestone and every row is `StatusDone`.
        // (A semantic reframe of `M28_Is_The_Single_InProgress_Milestone`, not
        // a rename — M28 was the last, so the premise it asserted is gone.)
        // The order is unchanged (…, M26, M27, SITE, M28).
        Assert.Empty(Milestones.All.Where(m => m.Status == Milestones.StatusNext));
        Assert.All(Milestones.All, m => Assert.Equal(Milestones.StatusDone, m.Status));
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
