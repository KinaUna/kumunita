using Kumunita.Core.Usage;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C2-06 unit test for the <see cref="UsagePurgePolicy"/> decision surface
/// (M13 usage-row retention, ADR 0114 D5 — the DoD #7 "the two untested
/// services covered" line, the pure decision extracted out of
/// <see cref="UsagePurgeService.PurgeAsync"/>'s I/O). A **pure** unit — no
/// <c>PostgresFixture</c>, no <see cref="Marten.IDocumentStore"/>: just
/// <c>DateTimeOffset</c> values + the
/// <see cref="UsagePurgeService.RetentionDays"/> platform constant (the
/// <see cref="Kumunita.Core.Logging.RollingFileSink.Retain"/> cutoff
/// precedent, unit-tested the same way in <c>LoggingTests</c>).
/// Pins the cutoff computation (<c>now − RetentionDays</c>) and the
/// keep/purge boundary (a row exactly at the cutoff is **kept**; one older is
/// **purged**; one newer is **kept**). The I/O legs (the batched
/// <c>Query</c>/<c>Delete</c>/one-<c>SaveChangesAsync</c> commit) stay
/// covered by <see cref="UsagePurgeServiceTests"/> (Testcontainers).
/// </summary>
public class UsagePurgePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ComputeCutoff_IsNowMinusRetentionDays()
    {
        var cutoff = UsagePurgePolicy.ComputeCutoff(Now);
        Assert.Equal(Now.AddDays(-UsagePurgeService.RetentionDays), cutoff);
    }

    [Fact]
    public void ComputeCutoff_UsesCustomRetentionWhenSupplied()
    {
        Assert.Equal(Now.AddDays(-30), UsagePurgePolicy.ComputeCutoff(Now, 30));
    }

    [Fact]
    public void ShouldPurge_RowOlderThanCutoff_Purges()
    {
        var cutoff = UsagePurgePolicy.ComputeCutoff(Now);
        Assert.True(UsagePurgePolicy.ShouldPurge(cutoff.AddSeconds(-1), cutoff));
    }

    [Fact]
    public void ShouldPurge_RowExactlyAtCutoff_Keeps()
    {
        // The rule is strictly-older (At < cutoff): a row exactly at the
        // cutoff is kept (the "≥ 365 days" boundary — 365 days old is the
        // first purged row only when strictly before the cutoff instant).
        var cutoff = UsagePurgePolicy.ComputeCutoff(Now);
        Assert.False(UsagePurgePolicy.ShouldPurge(cutoff, cutoff));
    }

    [Fact]
    public void ShouldPurge_RowNewerThanCutoff_Keeps()
    {
        var cutoff = UsagePurgePolicy.ComputeCutoff(Now);
        Assert.False(UsagePurgePolicy.ShouldPurge(Now, cutoff));
    }

    [Fact]
    public void ShouldPurge_RowOneDayPastRetention_Purges()
    {
        // The UsagePurgeServiceTests plant: now − (RetentionDays + 1) is purged.
        var cutoff = UsagePurgePolicy.ComputeCutoff(Now);
        Assert.True(UsagePurgePolicy.ShouldPurge(
            Now.AddDays(-(UsagePurgeService.RetentionDays + 1)), cutoff));
    }
}
