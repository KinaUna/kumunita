using System.Text;

namespace Kumunita.Core.Events;

/// <summary>
/// The shared RFC 5545 **line-level primitives** for the calendar emitters
/// (IMPROVE-CODE C11 — the one calendar serializer): <see cref="FormatUtc"/>
/// (the <c>yyyyMMddTHHmmssZ</c> stamp), <see cref="Escape"/> (the ICS TEXT
/// value pin), <see cref="Fold"/> (the ≤ 75-octet fold) and
/// <see cref="AppendEnvelope"/> (the <c>VCALENDAR</c> envelope, one PRODID
/// for the instance, <c>VEVENT</c> and <c>VTODO</c> files alike).
/// <para>
/// Before C11 each of <see cref="IcsWriter"/> (ADR 0112, M12) and
/// <see cref="TodoIcsWriter"/> (ADR 0115 D4, M14) carried its **own** copy of
/// the three private methods below — a silent-coupling clone (the
/// <c>anti-patterns.md</c> seam the IMPROVE-CODE E7 row names:
/// <c>IcsWriter</c> ↔ <c>TodoIcsWriter</c>). The primitives are identical
/// (same escape order, same 75-octet boundary, same stamp format) and the
/// envelope is byte-for-byte the ADR 0112 five lines. They now live here,
/// exactly once; the two writers keep their public surfaces
/// (<see cref="IcsWriter.Build"/> / <see cref="TodoIcsWriter.BuildTodos"/>)
/// and their per-component property sets (the ADR pins), but render their
/// shared bits through this one type.
/// </para>
/// <para>
/// **Pure + BCL-only** (the ADR 0112 D1 / ADR 0115 D4 discipline carried to
/// the shared part): no store, no clock, no reflection, no library. The
/// escape / fold / stamp semantics are the ADR 0112 §ics / ADR 0115 §vtodo
/// pins verbatim — a change here changes both emitters' bytes, so it is
/// pinned by <c>tests/Kumunita.Core.Tests/IcsFormatTests.cs</c> (part-vs-whole:
/// this type is named by its own unit test, not only its two callers).
/// </para>
/// </summary>
public static class IcsFormat
{
    /// <summary>
    /// The <c>VCALENDAR</c> envelope — the five pinned lines (the ADR 0112
    /// D1 envelope verbatim, one PRODID for the instance, <c>VEVENT</c> and
    /// <c>VTODO</c> files alike). Appended in order to <paramref name="lines"/>
    /// so a caller keeps control of the component body that follows.
    /// </summary>
    public static void AppendEnvelope(List<string> lines)
    {
        lines.Add("BEGIN:VCALENDAR");
        lines.Add("VERSION:2.0");
        lines.Add("PRODID:-//Kumunita//community calendar//EN");
        lines.Add("CALSCALE:GREGORIAN");
        lines.Add("METHOD:PUBLISH");
    }

    /// <summary>
    /// Format a UTC instant as <c>yyyyMMddTHHmmssZ</c> (no offset).
    /// <c>DTSTART</c> / <c>DTEND</c> / <c>DUE</c> / <c>DTSTAMP</c> are
    /// **formatted, not text** — never escaped (the §ics / §vtodo field pin).
    /// </summary>
    public static string FormatUtc(DateTimeOffset utc) => utc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");

    /// <summary>
    /// Escape an ICS TEXT value (the §ics / §vtodo escape pin): backslash →
    /// <c>\\</c> (first), semicolon → <c>\;</c>, comma → <c>\,</c>, CR/LF →
    /// <c>\n</c> (a CRLF or a lone LF becomes the two-char backslash-<c>n</c>
    /// sequence). All other characters pass through.
    /// </summary>
    public static string Escape(string value)
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
    public static IEnumerable<string> Fold(string logicalLine)
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
