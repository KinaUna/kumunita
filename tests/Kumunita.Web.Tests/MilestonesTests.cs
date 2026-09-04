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
    public void Roadmap_Covers_M0_Through_M6_In_Order()
    {
        Assert.Equal(
            new[] { "M0", "M1", "M2", "M3", "M4", "M5", "M6" },
            Ids.ToList());
    }

    [Fact]
    public void M0_M1_M2_Are_Marked_Done()
    {
        foreach (string id in new[] { "M0", "M1", "M2" })
        {
            var m = Milestones.All.Single(x => x.Id == id);
            Assert.Equal(Milestones.StatusDone, m.Status);
        }
    }

    [Fact]
    public void M3_Is_The_Single_InProgress_Milestone()
    {
        var next = Milestones.All.Where(m => m.Status == Milestones.StatusNext).ToList();
        Assert.Single(next);
        Assert.Equal("M3", next[0].Id);
    }

    [Fact]
    public void No_Milestone_Has_Blank_Title()
    {
        Assert.All(Milestones.All, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }
}
