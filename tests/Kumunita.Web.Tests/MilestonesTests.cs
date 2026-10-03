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
            new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "M28" },
            Ids.ToList());
    }

    [Fact]
    public void Shipped_Milestones_Are_Marked_Done()
    {
        foreach (string id in new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void M25_Is_The_Single_InProgress_Milestone()
    {
        // M24 (storage metrics) is now DONE — closed in the U8 close unit
        // (ADR 0134); M25 (upload limits) is promoted to the single
        // in-progress milestone, the order unchanged (M20, M21, M23, M22, M24, M25);
        // M23 (extended profiles) + M22 (onboarding) + M21 (document management) are still done.
        var next = Milestones.All.Where(m => m.Status == Milestones.StatusNext).ToList();
        Assert.Single(next);
        Assert.Equal("M25", next[0].Id);
        // the done list has grown to M24 (M25 is not done)
        Assert.Equal(Milestones.StatusDone, Milestones.All.Single(x => x.Id == "M24").Status);
        Assert.Equal(Milestones.StatusDone, Milestones.All.Single(x => x.Id == "M22").Status);
        Assert.Equal(Milestones.StatusDone, Milestones.All.Single(x => x.Id == "M23").Status);
        Assert.Equal(Milestones.StatusDone, Milestones.All.Single(x => x.Id == "M21").Status);
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
