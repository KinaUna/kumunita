using Kumunita.Core.UserInfo;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="GuardianTimeLimitEvaluator"/> (M28 — ADR 0151, D3) **pure**
/// harness: the F1–F4 pure-evaluator faces (the design doc §2.6 names,
/// verbatim). A **pure** unit — no <see cref="Marten.IDocumentStore"/>, no
/// <c>PostgresFixture</c>, no <c>IUserInfoService</c>: just the
/// <see cref="GuardianTimeLimitSchedule"/> POCO + <see cref="TimeZoneInfo"/> +
/// <see cref="DateTimeOffset"/>. The **load-bearing difference** from M20's
/// <c>QuietScheduleEvaluator</c> is the inverted polarity: here
/// <c>true</c> = the child MAY use the platform now (<em>allowed</em> is the
/// permission, not the restriction), so the floor (<c>null</c> / disabled)
/// returns <c>true</c> (always allowed) — the opposite of M20's floor
/// (<c>false</c> = never quiet).
/// </summary>
public class GuardianTimeLimitEvaluatorTests
{
    private static readonly DateTimeOffset AnyInstant =
        new(2026, 1, 15, 23, 0, 0, TimeSpan.Zero);

    // C-M28·3 — the floor: a null schedule is always allowed (the child is
    // never restricted).
    [Fact]
    public void F3_Missing_Schedule_Is_Always_Allowed()
    {
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(null, AnyInstant, TimeZoneInfo.Utc));
    }

    // C-M28·3 — the floor: a stored-but-disabled schedule is always allowed,
    // regardless of the window (the D2 master on/off).
    [Fact]
    public void F3_Disabled_Schedule_Is_Always_Allowed()
    {
        var disabled = new GuardianTimeLimitSchedule
        {
            ChildId = "c-disabled",
            Enabled = false,
            Mode = TimeLimitMode.Blocked,
            Hours = new[] { 23 },
        };
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(disabled, AnyInstant, TimeZoneInfo.Utc));
    }

    // F1 — Blocked mode (the lean default): the child is restricted DURING the
    // listed window. 22:30 local is inside Hours=[22] → restricted (not allowed).
    [Fact]
    public void F1_Blocked_InWindow_Is_Restricted()
    {
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId = "c-b1",
            Enabled = true,
            Mode = TimeLimitMode.Blocked,
            Hours = new[] { 22 },
        };
        var instant = new DateTimeOffset(2026, 1, 15, 22, 30, 0, TimeSpan.Zero);
        Assert.False(GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, TimeZoneInfo.Utc));
    }

    // F1 — Blocked mode: outside the listed window → allowed. 14:00 local is
    // outside Hours=[22] → allowed.
    [Fact]
    public void F1_Blocked_OutOfWindow_Is_Allowed()
    {
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId = "c-b2",
            Enabled = true,
            Mode = TimeLimitMode.Blocked,
            Hours = new[] { 22 },
        };
        var instant = new DateTimeOffset(2026, 1, 15, 14, 0, 0, TimeSpan.Zero);
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, TimeZoneInfo.Utc));
    }

    // F2 — Allowed mode (the allow-list): the child may use the platform only
    // DURING the listed window. 10:00 local is inside Hours=[9,10,11] → allowed.
    [Fact]
    public void F2_Allowed_InWindow_Is_Allowed()
    {
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId = "c-a1",
            Enabled = true,
            Mode = TimeLimitMode.Allowed,
            Hours = new[] { 9, 10, 11 },
        };
        var instant = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        Assert.True(GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, TimeZoneInfo.Utc));
    }

    // F2 — Allowed mode: outside the listed window → restricted (not allowed).
    // 22:00 local is outside Hours=[9,10,11] → restricted.
    [Fact]
    public void F2_Allowed_OutOfWindow_Is_Restricted()
    {
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId = "c-a2",
            Enabled = true,
            Mode = TimeLimitMode.Allowed,
            Hours = new[] { 9, 10, 11 },
        };
        var instant = new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero);
        Assert.False(GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, TimeZoneInfo.Utc));
    }

    // C-M28·6 — the verdict is timezone-aware (the M20
    // IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC analog): the same Blocked
    // schedule + the same instant yields a **different** verdict in UTC+2 than
    // in UTC, because the wall-clock hour differs (the point of ADR 0019).
    [Fact]
    public void F4_Verdict_Differs_Between_Zones()
    {
        // A fixed UTC+2 zone (no DST, deterministic across platforms — the
        // CreateCustomTimeZone shape avoids the IANA/Windows zone-id + DST
        // ambiguity of a named zone like CET, which is only UTC+1 in winter).
        var utc2 = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var utc  = TimeZoneInfo.Utc;

        // Blocked mode, window Hours=[22,23] (the M20 {22,23} analog); empty
        // DaysOfWeek = all days.
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId = "c-f4",
            Enabled = true,
            Mode = TimeLimitMode.Blocked,
            Hours = new[] { 22, 23 },
            DaysOfWeek = [],
        };

        // Pick a UTC instant whose UTC+2 wall-clock hour is 23 (inside the
        // window) while the UTC wall-clock hour is 21 (outside the window) — the
        // verdict must flip. 21:30 UTC == 23:30 UTC+2 (no DST ambiguity at this
        // fixed offset).
        var instant = new DateTimeOffset(2026, 1, 15, 21, 30, 0, TimeSpan.Zero);

        var inUtc2 = GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, utc2);
        var inUtc  = GuardianTimeLimitEvaluator.IsAllowedNow(schedule, instant, utc);

        // Blocked → restricted DURING the window → allowed iff OUTSIDE.
        Assert.False(inUtc2);    // 23:30 local → within {22,23} → restricted
        Assert.True(inUtc);     // 21:30 UTC  → outside {22,23} → allowed
        Assert.NotEqual(inUtc2, inUtc);   // the verdict differs (C-M28·6)
    }
}
