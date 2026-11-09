using Kumunita.Core.Privacy;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IS33 unit test for the <see cref="RetentionPolicy"/> +
/// <see cref="RetentionOptions"/> decision surface (IMPROVE-SEC D6 / E5 — the
/// retention policy that none of these types had). A **pure** unit — no
/// <c>PostgresFixture</c>, no <c>Marten.IDocumentStore</c>, no
/// <c>*DocTypes.Configure</c>: just <c>DateTimeOffset</c> values + a default-
/// or hand-set <see cref="RetentionOptions"/> (the
/// <see cref="UsagePurgePolicyTests"/> shape). Pins the D6 defaults, the
/// <c>0</c> = keep fail-safe, and the deterministic cutoff (<c>now − days</c>).
/// The I/O legs (the <c>RetentionTick</c> record, the
/// <c>RetentionService</c> deletes, the handler, the <c>Program.cs</c> seed)
/// are IS34a / IS34b — they must call <see cref="RetentionPolicy.CutoffFor"/>,
/// never re-derive the cutoff.
/// </summary>
public class RetentionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Defaults_MatchD6()
    {
        RetentionOptions opts = new();

        Assert.Equal(30, opts.Sessions);                 // 30 days after revoke or expiry
        Assert.Equal(7, opts.OutboxBodyNullDays);        // body nulled 7 days after delivery
        Assert.Equal(90, opts.OutboxDays);               // row kept 90 days
        Assert.Equal(30, opts.DeadLetters);              // dead letters 30 days
        Assert.Equal(90, opts.NotificationsRead);        // read notifications 90 days
        Assert.Equal(365, opts.NotificationsUnread);     // unread notifications 365 days
        Assert.Equal(180, opts.ErrorReports);            // resolved error reports 180 days
        Assert.Equal(7, opts.Tokens);                    // consumed / expired tokens 7 days
    }

    [Fact]
    public void ZeroMeansKeep()
    {
        RetentionOptions opts = new() { Sessions = 0 };

        // A 0-valued key yields no cutoff — the row is kept (the safe side).
        Assert.Null(RetentionPolicy.CutoffFor(opts, "Sessions", Now));
        Assert.True(RetentionPolicy.Keep(opts, "Sessions"));

        // An unknown / absent key is also keep (the fail-safe), not a purge.
        Assert.Null(RetentionPolicy.CutoffFor(opts, "NotAKey", Now));
        Assert.True(RetentionPolicy.Keep(opts, "NotAKey"));
    }

    [Fact]
    public void Cutoff_IsNowMinusDays()
    {
        RetentionOptions opts = new() { Tokens = 7 };

        // A positive key gives now − days, deterministically (now is injected).
        Assert.Equal(Now.AddDays(-7), RetentionPolicy.CutoffFor(opts, "Tokens", Now));
        Assert.False(RetentionPolicy.Keep(opts, "Tokens"));
    }
}
