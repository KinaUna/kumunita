using Microsoft.Extensions.Logging;

namespace Kumunita.Core.Logging;

/// <summary>
/// The pure JSON-lines writer (ADR 0114 D6). One line per log entry.
/// The escaping rules are pinned verbatim in the design doc §sink:
/// <c>\</c> → <c>\\</c>, <c>"</c> → <c>\"</c>, newline → <c>\n</c>,
/// tab → <c>\t</c>; the <c>timestamp</c> field in ISO-8601
/// <c>"o"</c> format; the <c>level</c> field as the
/// <see cref="LogLevel"/> enum name in <b>lowercase</b>; the
/// <c>category</c> field verbatim (escaped); the <c>message</c> field
/// escaped; the <c>exception</c> field as
/// <c>exception?.ToString()</c> escaped, <b>omitted when null</b>.
/// BCL-only (no <c>System.Text.Json</c> dependency on the Core project
/// — the writer is hand-rolled, the <c>IcsWriter</c> M12 precedent).
/// </summary>
public static class LogLine
{
    public static void Write(
        System.IO.TextWriter writer,
        LogLevel level,
        string category,
        string message,
        Exception? exception,
        DateTimeOffset timestamp)
    {
        System.IO.TextWriter? w = writer;
        ArgumentNullException.ThrowIfNull(w);

        w.Write("{ \"timestamp\":\"");
        w.Write(Escape(timestamp.ToString("o")));
        w.Write("\", \"level\":\"");
        w.Write(level.ToString().ToLowerInvariant());
        w.Write("\", \"category\":\"");
        w.Write(Escape(category));
        w.Write("\", \"message\":\"");
        w.Write(Escape(message));
        w.Write("\"");
        if (exception is not null)
        {
            w.Write(", \"exception\":\"");
            w.Write(Escape(exception.ToString() ?? string.Empty));
            w.Write("\"");
        }
        w.Write(" }\n");
        w.Flush();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n");  break;
                case '\t': sb.Append("\\t");  break;
                default:   sb.Append(c);      break;
            }
        }
        return sb.ToString();
    }
}
