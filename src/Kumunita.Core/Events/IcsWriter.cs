using System.Text;

namespace Kumunita.Core.Events;

/// <summary>
/// The M12 iCal emitter (ADR 0112, D1/D6) — a **pure** function over
/// already-authorized <see cref="Event"/> rows. Maps the POCOs it is handed
/// onto RFC 5545 text and produces the **entire** file: the one
/// <c>VCALENDAR</c> envelope + one <c>VEVENT</c> per event, in list order,
/// CRLF-terminated, 75-octet-folded, fully escaped, on the **closed pinned
/// subset** only.
/// <para>
/// Pinned verbatim by <c>docs/design/m12-ical-design.md</c> §ics / §field-map
/// (the LOCKED primary tier, U01 unit of <c>plan-m12-ical.md</c>). **Exactly**
/// one public method (<see cref="Build"/>) — the locked D6 surface. The
/// emitter is <b>pure</b> (C-M12·5): no store, no <c>IDocumentSession</c>,
/// no <c>IAuthorizationService</c>, no <c>ITagService</c>, no clock (the
/// caller passes <c>nowUtc</c>), no reflection, no library (D1 — BCL-only).
/// The authorization was done *before* the rows reach here; the decision is
/// never re-derived and never leaked (C-M12·2).
/// </para>
/// <para>
/// **The closed property set (C-M12·4 — the ceiling):**
/// <c>VCALENDAR</c> = <c>VERSION:2.0</c> /
/// <c>PRODID:-//Kumunita//community calendar//EN</c> /
/// <c>CALSCALE:GREGORIAN</c> / <c>METHOD:PUBLISH</c>;
/// <c>VEVENT</c> always = <c>UID</c> (<c>kw-eve-{Event.Id}@kumunita</c>,
/// stable — the calendar app's update-vs-add key) / <c>DTSTAMP</c> (the
/// caller-passed <c>nowUtc</c> — the RFC's "date-time of creation" refresh
/// marker, **not** <see cref="Event.Modified"/>) / <c>DTSTART</c> /
/// <c>DTEND</c> (stored UTC, <c>yyyyMMddTHHmmssZ</c>) / <c>SUMMARY</c>
/// (<see cref="Event.Title"/>); conditionally <c>DESCRIPTION</c>
/// (<see cref="Event.Body"/> — the **Markdown source**, not rendered HTML —
/// only when non-empty) / <c>LOCATION</c> (only when set / non-empty) /
/// <c>CATEGORIES</c> (caller-resolved tag display names, only when the map
/// carries a non-empty list for the event) / <c>STATUS:CANCELLED</c> (only
/// when <see cref="Event.IsDeleted"/> — the RFC update path; an emitter
/// **capability**, never reached through the frozen read seams, drift-guard
/// entry 1).
/// </para>
/// <para>
/// **Never emitted (the D4 "never" list):** <c>ORGANIZER</c> (no author
/// identity in a file that travels — the ADR 0028 posture), <c>ATTENDEE</c>
/// / <c>RSVP</c> (the author-only RSVP list — privacy decision, own lane),
/// <c>RRULE</c> (no recurrence concept in the <see cref="Event"/> doc; M14's
/// home), <c>VTODO</c> (a different surface), <see cref="Event.Color"/>
/// (app-internal display metadata), <see cref="Event.Audience"/> / grant /
/// membership internals (the decision is already *applied* to the set —
/// C-M12·2), <see cref="Event.AuthorId"/> beyond the never-emitted
/// <c>ORGANIZER</c>, and **any** vendor-specific property (the <c>X-…</c> /
/// <c>COLOR</c> / <c>GEO</c> / <c>URL</c> / <c>SEQUENCE</c> /
/// <c>PRIORITY</c> / <c>CLASS</c> / <c>CREATED</c> / <c>LAST-MODIFIED</c> /
/// <c>REQUEST-ID</c> set — none of it).
/// </para>
/// <para>
/// **Formatting (C-M12·4):** CRLF endings on every line (including the
/// final <c>END:VCALENDAR</c>); a content line whose UTF-8 octet length
/// exceeds 75 is folded at a ≤ 75-octet boundary that does not break a
/// multi-byte UTF-8 sequence, with a single leading space on each
/// continuation line; escaping — backslash <c>\</c> → <c>\\</c> (first),
/// semicolon → <c>\;</c>, comma → <c>\,</c>, CR/LF → <c>\n</c> (a CRLF or a
/// lone LF becomes the two-char backslash-<c>n</c> sequence).
/// </para>
/// </summary>
public static class IcsWriter
{
    /// <summary>
    /// The one public method (the locked D6 surface, verbatim). Renders the
    /// **entire** ICS file — the <c>VCALENDAR</c> envelope + one
    /// <c>VEVENT</c> per event, in list order — on the closed pinned subset
    /// (§ics).
    /// </summary>
    /// <param name="events">
    /// The **already-authorized** rows the caller resolved (the frozen
    /// <c>IEventService</c> read seam's output — C-M12·5). Empty ⇒ a valid
    /// empty <c>VCALENDAR</c> (the four envelope properties, zero
    /// <c>VEVENT</c> blocks — RFC-legal, no crash, C-M12·4).
    /// </param>
    /// <param name="nowUtc">
    /// The caller's fetch instant — the <c>DTSTAMP</c> refresh marker. The
    /// emitter **never reads a clock**, so it is deterministic + testable;
    /// the caller passes <see cref="DateTimeOffset.UtcNow"/>.
    /// </param>
    /// <param name="categoriesByEventId">
    /// An **optional** per-event <c>Id</c> → <c>CATEGORIES</c> display names
    /// map. The caller resolves it from <see cref="Event.TagIds"/> via the
    /// ADR 0044 display-name idiom (design doc §field-map — the Web layer's
    /// job, drift-guard entry 2); <c>null</c> ⇒ no <c>CATEGORIES</c> on any
    /// <c>VEVENT</c>. Names are escaped individually, then joined with the
    /// literal (unescaped) RFC 5545 list separator comma — so a name
    /// containing a comma survives the round-trip as an escaped comma within
    /// the list item.
    /// </param>
    /// <returns>
    /// The entire ICS file text — CRLF-terminated, 75-octet-folded, fully
    /// escaped, on the pinned subset only.
    /// </returns>
    public static string Build(
        IReadOnlyList<Event> events,
        DateTimeOffset nowUtc,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? categoriesByEventId = null)
    {
        var lines = new List<string>(8 + events.Count * 8);
        lines.Add("BEGIN:VCALENDAR");
        lines.Add("VERSION:2.0");
        lines.Add("PRODID:-//Kumunita//community calendar//EN");
        lines.Add("CALSCALE:GREGORIAN");
        lines.Add("METHOD:PUBLISH");

        foreach (var ev in events)
        {
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:kw-eve-{ev.Id}@kumunita");
            lines.Add($"DTSTAMP:{FormatUtc(nowUtc)}");
            lines.Add($"DTSTART:{FormatUtc(ev.Start)}");
            lines.Add($"DTEND:{FormatUtc(ev.End)}");
            lines.Add($"SUMMARY:{Escape(ev.Title)}");
            if (!string.IsNullOrEmpty(ev.Body))
                lines.Add($"DESCRIPTION:{Escape(ev.Body)}");
            if (!string.IsNullOrEmpty(ev.Location))
                lines.Add($"LOCATION:{Escape(ev.Location)}");
            if (categoriesByEventId is not null
                && categoriesByEventId.TryGetValue(ev.Id, out var names)
                && names is not null
                && names.Count > 0)
                lines.Add("CATEGORIES:" + string.Join(",", names.Select(Escape)));
            if (ev.IsDeleted)
                lines.Add("STATUS:CANCELLED");
            lines.Add("END:VEVENT");
        }

        lines.Add("END:VCALENDAR");

        return string.Join("\r\n", lines.SelectMany(Fold)) + "\r\n";
    }

    /// <summary>
    /// Format a UTC instant as <c>yyyyMMddTHHmmssZ</c> (no offset).
    /// <c>DTSTART</c> / <c>DTEND</c> / <c>DTSTAMP</c> are **formatted, not
    /// text** — never escaped (§ics field pin).
    /// </summary>
    private static string FormatUtc(DateTimeOffset utc) => utc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>
    /// Escape an ICS TEXT value (§ics escape pin): backslash → <c>\\</c>
    /// (first), semicolon → <c>\;</c>, comma → <c>\,</c>, CR/LF → <c>\n</c>
    /// (a CRLF or a lone LF becomes the two-char backslash-<c>n</c>
    /// sequence). All other characters pass through.
    /// </summary>
    private static string Escape(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case ';': sb.Append("\\;"); break;
                case ',': sb.Append("\\,"); break;
                case '\r': case '\n': sb.Append("\\n"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Fold one logical content line to physical lines of ≤ 75 **octets**
    /// (UTF-8 bytes). A line ≤ 75 octets is **not** folded. Splits are made
    /// on UTF-16 character boundaries, so a multi-byte UTF-8 sequence is
    /// never broken; each continuation line carries a single leading space.
    /// Unfolding (strip the CRLF + one leading space, concatenate)
    /// reconstructs the original logical line exactly.
    /// </summary>
    private static IEnumerable<string> Fold(string logicalLine)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(logicalLine) <= 75)
            return new[] { logicalLine };

        var result = new List<string>();
        var sb = new StringBuilder();
        var octets = 0;
        const int limit = 75;

        foreach (var c in logicalLine)
        {
            var cOctets = System.Text.Encoding.UTF8.GetByteCount(c.ToString());
            if (octets + cOctets > limit && sb.Length > 0)
            {
                // The first physical line carries no leading space; every
                // subsequent (continuation) line carries exactly one.
                result.Add(result.Count == 0 ? sb.ToString() : " " + sb.ToString());
                sb.Clear();
                octets = 0;
            }
            sb.Append(c);
            octets += cOctets;
        }
        if (sb.Length > 0)
            result.Add(result.Count == 0 ? sb.ToString() : " " + sb.ToString());
        return result;
    }
}
