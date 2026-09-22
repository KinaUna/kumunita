using Kumunita.Web.Localization;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Kumunita.Web.TagHelpers;

/// <summary>
/// The <c>&lt;kw-dt&gt;</c> view-localization TagHelper (ADR 0019) — the
/// per-request timestamp renderer. It takes a <see cref="DateTimeOffset"/>
/// and a .NET format string, and emits that instant **converted to the
/// effective time zone** resolved by
/// <see cref="EffectiveTimezoneResolver"/> (the signed-in actor's
/// <c>Profile.TimeZone</c> override → the instance default
/// (<c>LocaleSettings.DefaultTimezone</c>) → the <c>UTC</c> floor — never
/// null, never a throw).
/// <para>
/// <b>Why a TagHelper.</b> The codebase already has exactly one precedent
/// for "a per-request value rendered across many views": the <c>kw-l</c>
/// localization TagHelper (ADR 0015). Timezone is the same shape — a
 /// per-request value, resolved from the request's principal, consumed by
/// many views — and a TagHelper is the convention-consistent way to expose
/// it without threading a <see cref="TimeZoneInfo"/> through every view
/// model in the tree (which would touch the pinned <c>PostDetailViewModel</c>
/// / <c>PostListItem</c> / <c>Announcement</c> / <c>Moderation</c> shapes
/// and their <c>GetProperties</c> tests).
/// </para>
/// <para>
/// <b>Format contract.</b> The <c>format</c> attribute is a standard .NET
/// custom format string (the same <c>"g"</c> / <c>"d"</c> / <c>"ddd, MMM d
/// yyyy"</c> the views were already passing to <c>ToString</c>); the
/// conversion is applied **first** to the instant, then the format is applied
/// to the resulting <see cref="DateTimeOffset"/>.
/// </para>
/// <para>
/// <b>Graceful floor.</b> An empty <c>dt</c> renders as the empty string
/// (a <c>null</c> or blank value, e.g. a <c>Modified</c> the author has not
/// set, renders nothing — the same "no value → no text" shape the views
/// already use when the <c>Modified</c> check gates the edit line). A
/// missing context (no signed-in principal) degrades to the instance default
/// (the resolver's own rule) — never a throw.
/// </para>
/// </summary>
[HtmlTargetElement("kw-dt")]
public sealed class DateTimeTagHelper : TagHelper
{
    private readonly EffectiveTimezoneResolver _resolver;
    private readonly EffectiveDateFormatResolver _formatResolver;

    /// <param name="resolver">The per-request effective-time-zone resolver
    /// (DI — scoped, one instance per request, the resolver caches its
    /// first <c>GetAsync</c> result for the request's lifetime).</param>
    /// <param name="formatResolver">The per-request effective date-time format
    /// resolver (ADR 0020 — scoped, one instance per request, the resolver
    /// caches its first <c>GetAsync</c> result for the request's lifetime). The
    /// TagHelper applies the **effective format** (override → platform
    /// default → the <see cref="Kumunita.Core.Localization.DateFormat.FloorFormat"/>
    /// floor); the <c>format</c> attribute is the fallback when no setting
    /// resolves (it always does in practice, so the attribute is superseded).</param>
    public DateTimeTagHelper(
        EffectiveTimezoneResolver resolver,
        EffectiveDateFormatResolver formatResolver)
    {
        _resolver = resolver;
        _formatResolver = formatResolver;
    }

    /// <summary>The instant to render (a <see cref="DateTimeOffset"/> — the
    /// views pass <c>Post.Created</c> / <c>Post.Modified</c> /
    /// <c>AccessAudit.At</c> / <c>Announcement.Created</c>, all
    /// <see cref="DateTimeOffset"/>).</summary>
    [HtmlAttributeName]
    public DateTimeOffset? Dt { get; set; }

    /// <summary>The .NET format string to apply to the converted instant
    /// (the same custom-format-string contract the views were already using
    /// with <c>ToString</c> — e.g. <c>"g"</c>, <c>"d"</c>, or
    /// <c>"ddd, MMM d yyyy"</c>).</summary>
    [HtmlAttributeName]
    public string Format { get; set; } = "g";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // A null/blank instant renders nothing (a Modified the author has not
        // set — the same "no value → no text" shape the views already use when
        // the Modified check gates the edit line).
        if (Dt is null)
        {
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Content.SetContent(string.Empty);
            return;
        }

        var tz = await _resolver.GetAsync();
        // ADR 0020 — the **effective format** (override → platform default →
        // the DateFormat.FloorFormat floor) is what renders; the `format`
        // attribute is the documented fallback when nothing resolves (it
        // always does in practice, so the attribute is superseded by the
        // setting). Resolved per request (cached by the resolver) so a page's
        // many timestamps are one profile read + one default read, not N of
        // each — the same shape as the zone resolution above.
        var fmt = await _formatResolver.GetAsync();
        // The conversion is applied **first** (UTC instant → the effective
        // zone's wall-clock time), then the effective format is applied to the
        // result. The zone and the format are independent choices (ADR 0019
        // zone, ADR 0020 format) and both are resident-controlled — neither is
        // the culture (render with the invariant culture, so the zone/format,
        // not the host's locale, are what the resident sees).
        var utc = Dt.Value.UtcDateTime;
        var wallTime = utc + tz.GetUtcOffset(utc);
        var rendered = wallTime.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);

        // SetContent auto-escapes (the safer choice — the kw-l TagHelper uses
        // the same idiom); the element's inner reference is the pre-conversion
        // fallback (never shown — SetContent replaces the element's content).
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetContent(rendered);
    }
}
