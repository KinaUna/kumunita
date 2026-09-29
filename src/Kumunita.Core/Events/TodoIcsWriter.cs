using System.Text;
using Kumunita.Core.Projects;

namespace Kumunita.Core.Events;

/// <summary>
/// The M14 <c>VTODO</c> iCal emitter (ADR 0115 D4, §vtodo) — a **pure**
/// function over already-authorized <see cref="Projects.TodoItem"/> rows.
/// Maps the POCOs it is handed onto RFC 5545 text and produces the
/// **entire** file: the one <c>VCALENDAR</c> envelope + one <c>VTODO</c>
/// per **dated** to-do (a to-do with neither <see cref="Projects.TodoItem.DueAt"/>
/// nor <see cref="Projects.TodoItem.StartAt"/> is **skipped** — a
/// <c>VTODO</c> with neither <c>DUE</c> nor <c>DTSTART</c> is a valid-but-
/// pointless component; the "only include what's meaningful"
/// <see cref="IcsWriter"/> idiom), in list order, CRLF-terminated,
/// 75-octet-folded, fully escaped, on the **closed pinned subset** only.
/// <para>
/// Pinned verbatim by <c>docs/design/m14-events-projects-design.md</c>
/// §vtodo / §seams contract 5 (the LOCKED primary tier, U05 unit of
/// <c>plan-m14-events-projects.md</c>). **Exactly** one public method
/// (<see cref="BuildTodos"/>) — the locked D4 surface. The emitter is
/// <b>pure</b> (C-M14·5): no store, no <c>IDocumentSession</c>, no
/// <c>IAuthorizationService</c>, no clock (the caller passes
/// <c>nowUtc</c>), no reflection, no library (D4 — BCL-only). The
/// authorization was done *before* the rows reach here (the frozen
/// <c>IProjectService</c> read seam's output is the input); the decision
/// is never re-derived and never leaked (C-M14·5).
/// </para>
/// <para>
/// **The closed property set (C-M14·6 — the ceiling):**
/// <c>VCALENDAR</c> = <c>VERSION:2.0</c> /
/// <c>PRODID:-//Kumunita//community calendar//EN</c> /
/// <c>CALSCALE:GREGORIAN</c> / <c>METHOD:PUBLISH</c> (the ADR 0112
/// envelope verbatim — one PRODID for the instance, <c>VEVENT</c> and
/// <c>VTODO</c> files alike);
/// <c>VTODO</c> always = <c>UID</c> (<c>kw-todo-{TodoItem.Id}@kumunita</c>,
/// stable — the calendar app's update-vs-add key) / <c>DTSTAMP</c> (the
/// caller-passed <c>nowUtc</c> — the RFC's "date-time of creation"
/// refresh marker, **not** <see cref="Projects.TodoItem.Modified"/>) /
/// conditionally <c>DUE</c> (only when <see cref="Projects.TodoItem.DueAt"/>
/// is set) / <c>DTSTART</c> (only when
/// <see cref="Projects.TodoItem.StartAt"/> is set) — both stored UTC,
/// <c>yyyyMMddTHHmmssZ</c> — / <c>SUMMARY</c>
/// (<see cref="Projects.TodoItem.Title"/>) / conditionally
/// <c>DESCRIPTION</c> (<see cref="Projects.TodoItem.Body"/> — the
/// **Markdown source**, not rendered HTML — only when non-empty) /
/// conditionally <c>STATUS:CANCELLED</c> (only when
/// <see cref="Projects.TodoItem.IsDeleted"/> — the RFC update path; an
/// emitter **capability**, never reached through the frozen read seams).
/// </para>
/// <para>
/// **Never emitted (the C-M14·5 / C-M14·6 "never" list — the ADR 0028
/// posture + the ADR 0112 D1 "never" list carried to the to-do surface):**
/// <c>ORGANIZER</c> (no author identity in a file that travels),
/// <c>CREATED-BY</c>, <c>ATTENDEE</c> (no assignee identity),
/// <c>RRULE</c> / <c>RECURRENCE-ID</c> (C-M14·6 / D6 — no recurrence of
/// any kind in M14; M18's home), <see cref="Projects.TodoItem.Audience"/>
/// / grant / membership internals (the decision is already *applied* to
/// the set — C-M14·5), <see cref="Projects.TodoItem.AuthorId"/> /
/// <see cref="Projects.TodoItem.AssigneeId"/> beyond the never-emitted
/// properties, <see cref="Projects.TodoItem.ComponentId"/> /
/// <see cref="Projects.TodoItem.ProjectId"/> /
/// <see cref="Projects.TodoItem.EventId"/> /
/// <see cref="Projects.TodoItem.ParentId"/> /
/// <see cref="Projects.TodoItem.BlockedByTodoId"/> /
/// <see cref="Projects.TodoItem.Status"/> /
/// <see cref="Projects.TodoItem.LanguageCode"/> /
/// <see cref="Projects.TodoItem.TagIds"/> /
/// <see cref="Projects.TodoItem.ImageIds"/> /
/// <see cref="Projects.TodoItem.AttachmentIds"/> /
/// <see cref="Projects.TodoItem.Created"/> (app-internal state — none of
/// it), and **any** vendor-specific property (the <c>X-…</c> set — none
/// of it).
/// </para>
/// <para>
/// **Formatting (the §vtodo format pin, copied from
/// <see cref="IcsWriter"/>):** CRLF endings on every line (including the
/// final <c>END:VCALENDAR</c>); a content line whose UTF-8 octet length
/// exceeds 75 is folded at a ≤ 75-octet boundary that does not break a
/// multi-byte UTF-8 sequence, with a single leading space on each
/// continuation line; escaping — backslash <c>\</c> → <c>\\</c> (first),
/// semicolon → <c>\;</c>, comma → <c>\,</c>, CR/LF → <c>\n</c> (a CRLF or
/// a lone LF becomes the two-char backslash-<c>n</c> sequence).
/// </para>
/// </summary>
public static class TodoIcsWriter
{
    /// <summary>
    /// The one public method (the locked D4 surface, verbatim —
    /// <c>§seams contract 5</c>). Renders the **entire** ICS file — the
    /// <c>VCALENDAR</c> envelope + one <c>VTODO</c> per **dated** to-do
    /// (<c>DueAt != null || StartAt != null</c>; a to-do with **neither**
    /// is **skipped** — the feed's skip rule), in list order — on the
    /// closed pinned subset (§vtodo).
    /// </summary>
    /// <param name="todos">
    /// The **already-authorized** rows the caller resolved (the frozen
    /// <c>IProjectService</c> read seam's output — C-M14·5). A set with no
    /// dated to-dos ⇒ a valid empty <c>VCALENDAR</c> (the five envelope
    /// lines, zero <c>VTODO</c> blocks — RFC-legal, no crash,
    /// C-M14·5/6).
    /// </param>
    /// <param name="nowUtc">
    /// The caller's fetch instant — the <c>DTSTAMP</c> refresh marker. The
    /// emitter **never reads a clock**, so it is deterministic + testable;
    /// the caller passes <see cref="DateTimeOffset.UtcNow"/>.
    /// </param>
    /// <returns>
    /// The entire ICS file text — CRLF-terminated, 75-octet-folded, fully
    /// escaped, on the pinned subset only.
    /// </returns>
    public static string BuildTodos(IReadOnlyList<TodoItem> todos, DateTimeOffset nowUtc)
    {
        var dated = todos.Where(t => t.DueAt is not null || t.StartAt is not null).ToList();

        var lines = new List<string>(8 + dated.Count * 8);
        lines.Add("BEGIN:VCALENDAR");
        lines.Add("VERSION:2.0");
        lines.Add("PRODID:-//Kumunita//community calendar//EN");
        lines.Add("CALSCALE:GREGORIAN");
        lines.Add("METHOD:PUBLISH");

        foreach (var todo in dated)
        {
            lines.Add("BEGIN:VTODO");
            lines.Add($"UID:kw-todo-{todo.Id}@kumunita");
            lines.Add($"DTSTAMP:{FormatUtc(nowUtc)}");
            if (todo.DueAt is not null)
                lines.Add($"DUE:{FormatUtc(todo.DueAt.Value)}");
            if (todo.StartAt is not null)
                lines.Add($"DTSTART:{FormatUtc(todo.StartAt.Value)}");
            lines.Add($"SUMMARY:{Escape(todo.Title)}");
            if (!string.IsNullOrEmpty(todo.Body))
                lines.Add($"DESCRIPTION:{Escape(todo.Body)}");
            if (todo.IsDeleted)
                lines.Add("STATUS:CANCELLED");
            lines.Add("END:VTODO");
        }

        lines.Add("END:VCALENDAR");

        return string.Join("\r\n", lines.SelectMany(Fold)) + "\r\n";
    }

    /// <summary>
    /// Format a UTC instant as <c>yyyyMMddTHHmmssZ</c> (no offset).
    /// <c>DUE</c> / <c>DTSTART</c> / <c>DTSTAMP</c> are **formatted, not
    /// text** — never escaped (§vtodo field pin).
    /// </summary>
    private static string FormatUtc(DateTimeOffset utc) => utc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>
    /// Escape an ICS TEXT value (§vtodo escape pin): backslash → <c>\\</c>
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
        if (Encoding.UTF8.GetByteCount(logicalLine) <= 75)
            return new[] { logicalLine };

        var result = new List<string>();
        var sb = new StringBuilder();
        var octets = 0;
        const int limit = 75;

        foreach (var c in logicalLine)
        {
            var cOctets = Encoding.UTF8.GetByteCount(c.ToString());
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
