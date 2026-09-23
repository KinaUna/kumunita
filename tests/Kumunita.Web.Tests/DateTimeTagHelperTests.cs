using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.TagHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NSubstitute;
using System.Text.Encodings.Web;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the <see cref="DateTimeTagHelper"/> (<c>kw-dt</c>, ADR 0019 zone /
/// ADR 0020 format) <b>rendering contract</b> — the branch the
/// <see cref="DateTimeTagHelper.TimeOnly">TimeOnly</see> flag selects.
/// <para>
/// <b>Why this harness.</b> The <c>/events/calendar</c> grid (Day / Week /
/// Month) renders each event's time-of-day through
/// <c>&lt;kw-dt dt="..." TimeOnly="true"/&gt;</c> — the grid already supplies
/// the day, so a date would be redundant. The whole point of the flag is that
/// when it is <c>true</c> the TagHelper renders the <b>time of day only</b>
/// (<c>HH:mm</c>, the ADR 0020 24-hour platform convention) and when it is
/// <c>false</c> (the default) it renders the <b>effective format</b> (which is
/// a dated format — the override → platform-default → floor always yields a
/// date). The two outcomes are observably different: one contains a date, the
/// other must not.
/// </para>
/// <para>
/// <b>The regression this owns.</b> The calendar ships with date+time where
/// the day column already says the day. That symptom is only possible if the
/// <see cref="DateTimeTagHelper.TimeOnly">TimeOnly</see> branch did not win
/// (falling through to the dated effective-format path). This test pins the
/// branch directly so a future rewording of the view markup or the resolver
/// order cannot silently put the date back into a time-only cell.
/// </para>
/// <para>
/// <b>Zone is UTC.</b> The zone <em>resolution order</em> (profile override →
/// platform default → UTC floor) is pinned by the resolver's own tests; here
/// the zone is a given. A fixed <c>UTC</c> floor (anonymous principal → the
/// platform default we set to <c>UTC</c>) makes the wall-clock arithmetic
/// deterministic — no DST, no host-locale dependence — so the assertion is
/// the branch's <em>shape</em> (time-only vs dated), not the offset math.
/// </para>
/// </summary>
public class DateTimeTagHelperTests
{
    // 2026-03-10 14:25:00Z. Under the UTC floor the wall time is identical —
    // so the time-only render is exactly "14:25" and the dated render is
    // "2026-03-10 14:25" (or the floor's dated form) — distinct, and
    // DST/locale-free.
    private static readonly DateTimeOffset Instant =
        new(2026, 3, 10, 14, 25, 0, TimeSpan.Zero);

    /// <summary>
    /// An anonymous-context pair of resolvers: no principal (so no
    /// <c>GetProfileAsync</c> call — the <c>IUserInfoService</c> is a bare
    /// substitute), the platform-default zone <c>UTC</c> and a dated platform
    /// format — leaving <c>TimeOnly</c> as the only variable under test.
    /// </summary>
    private static DateTimeTagHelper BuildHelper(bool timeOnly, DateTimeOffset? dt)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("UTC");
        localization.GetDefaultDateFormatAsync().Returns("yyyy-MM-dd HH:mm");

        var zone = new EffectiveTimezoneResolver(userInfo, localization, new HttpContextAccessor());
        var format = new EffectiveDateFormatResolver(userInfo, localization, new HttpContextAccessor());

        return new DateTimeTagHelper(zone, format)
        {
            Dt = dt,
            TimeOnly = timeOnly,
        };
    }

    private static string Run(DateTimeTagHelper helper)
    {
        var output = new TagHelperOutput(
            "kw-dt",
            new TagHelperAttributeList(),
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var context = new TagHelperContext(
            "kw-dt",
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            uniqueId: "test");

        helper.ProcessAsync(context, output).GetAwaiter().GetResult();

        // GetContent(HtmlEncoder) returns the fully-rendered inner HTML as a
        // string — the encoder is a no-op for our plain-text time/date output.
        return output.Content.GetContent(HtmlEncoder.Default);
    }

    [Fact(DisplayName = "TimeOnly=true renders the time of day only (HH:mm), no date — the calendar cell contract")]
    public void TimeOnly_True_RendersTimeOnly()
    {
        var rendered = Run(BuildHelper(timeOnly: true, dt: Instant));

        // Exactly the ADR 0020 24-hour time-of-day — no date component.
        Assert.Equal("14:25", rendered);
        Assert.DoesNotContain("2026", rendered);
    }

    [Fact(DisplayName = "TimeOnly=false (the default) renders the effective (dated) format — the branch the calendar must NOT take")]
    public void TimeOnly_False_RendersDatedFormat()
    {
        var rendered = Run(BuildHelper(timeOnly: false, dt: Instant));

        // The effective format always carries a date (the resolver's floor
        // guarantees a dated string); so the dated path is observably distinct
        // from the time-only path.
        Assert.Contains("2026", rendered);
        Assert.NotEqual("14:25", rendered);
    }

    [Fact(DisplayName = "A null instant renders nothing (the 'no value → no text' floor)")]
    public void NullDt_RendersEmpty()
    {
        var rendered = Run(BuildHelper(timeOnly: true, dt: null));

        Assert.Equal(string.Empty, rendered);
    }
}
