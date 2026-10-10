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
    public void Roadmap_Covers_M0_Through_M34_Plus_Named_Lanes_In_Order()
    {
        Assert.Equal(
            new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "M33", "M34" },
            Ids.ToList());
    }

    [Fact]
    public void Shipped_Milestones_Are_Marked_Done()
    {
        // M33 (storage metrics history) is now the last shipped milestone
        // (closed in this M33 close unit, ADR 0156); M34 (analytics history)
        // is the single in-progress milestone, so the asserted set is every
        // id except M34.
        foreach (string id in new[] { "M0", "M1", "M2", "M3", "GP", "ML", "ML-UI", "LS", "SP", "TZ", "DF", "TR", "RC", "GU", "GA", "RE", "TG", "PG", "UG", "M4", "EV-CAL", "EV-DWM", "EV-NW", "M5", "M6", "M7", "M8", "M9", "M10", "M11", "M12", "M13", "M14", "M15", "M16", "M17", "M18", "M19", "M20", "M21", "M23", "M22", "M24", "M25", "M26", "M27", "SITE", "M28", "IMPROVE", "M29", "M30", "M31", "M32", "M33" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void M34_Is_The_Single_InProgress_Milestone()
    {
        // M33 (storage metrics history) is now the last shipped milestone
        // (closed in this M33 close unit, ADR 0156); M34 (analytics history)
        // is the single in-progress milestone promoted to StatusNext. The
        // roadmap order is unchanged ("…M32","M33","M34" — the "named lane,
        // not a renumber" precedent).
        var next = Assert.Single(Milestones.All.Where(m => m.Status == Milestones.StatusNext));
        Assert.Equal("M34", next.Id);
        Assert.All(Milestones.All.Where(m => m.Id is not "M34"),
            m => Assert.Equal(Milestones.StatusDone, m.Status));
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
