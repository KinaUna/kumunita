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
        // C11 — the envelope + the escape / fold / stamp primitives are the
        // shared <see cref="IcsFormat"/> (the one calendar serializer, ADR
        // 0112 D1 / ADR 0115 D4); this method keeps the VEVENT property set
        // (the ADR 0112 §ics pin) and its CATEGORIES map.
        var lines = new List<string>(8 + events.Count * 8);
        IcsFormat.AppendEnvelope(lines);

        foreach (var ev in events)
        {
            lines.Add("BEGIN:VEVENT");
            lines.Add($"UID:kw-eve-{ev.Id}@kumunita");
            lines.Add($"DTSTAMP:{IcsFormat.FormatUtc(nowUtc)}");
            lines.Add($"DTSTART:{IcsFormat.FormatUtc(ev.Start)}");
            lines.Add($"DTEND:{IcsFormat.FormatUtc(ev.End)}");
            lines.Add($"SUMMARY:{IcsFormat.Escape(ev.Title)}");
            if (!string.IsNullOrEmpty(ev.Body))
                lines.Add($"DESCRIPTION:{IcsFormat.Escape(ev.Body)}");
            if (!string.IsNullOrEmpty(ev.Location))
                lines.Add($"LOCATION:{IcsFormat.Escape(ev.Location)}");
            if (categoriesByEventId is not null
                && categoriesByEventId.TryGetValue(ev.Id, out var names)
                && names is not null
                && names.Count > 0)
                lines.Add("CATEGORIES:" + string.Join(",", names.Select(IcsFormat.Escape)));
            if (ev.IsDeleted)
                lines.Add("STATUS:CANCELLED");
            lines.Add("END:VEVENT");
        }

        lines.Add("END:VCALENDAR");

        return string.Join("\r\n", lines.SelectMany(IcsFormat.Fold)) + "\r\n";
    }

    // The escape / fold / stamp primitives (the ADR 0112 §ics format pin) and
    // the VCALENDAR envelope live in the shared <see cref="IcsFormat"/> since
    // C11 — no private copies here (the C10/C11 de-duplication of the
    // IcsWriter ↔ TodoIcsWriter clone, the IMPROVE-CODE E7 seam).
}
