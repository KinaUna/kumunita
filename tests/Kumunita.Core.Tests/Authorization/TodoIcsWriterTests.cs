using Kumunita.Core.Events;
using Kumunita.Core.Projects;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The M14 <see cref="TodoIcsWriter"/> **pinned pure tests** (ADR 0115 D4,
/// the design doc <c>§pinned tests</c> names — U05 unit of
/// <c>plan-m14-events-projects.md</c>). The <see cref="TodoIcsWriter"/> is
/// **pure over POCOs** (C-M14·5): no store, no clock (the caller passes
/// <c>nowUtc</c>), no <c>IAuthorizationService</c> — so these pins run in
/// milliseconds with **no Testcontainers**. They prove the emitter's
/// **purity + closed-subset pinning + DUE/DTSTART conditionality +
/// undated-skip + escaping + 75-octet folding + CRLF endings + UID
/// stability + never-emitted list + empty-calendar validity**
/// (C-M14·5 / C-M14·6).
/// </summary>
public class TodoIcsWriterTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static TodoItem MakeTodo(
        string id,
        string title = "Bring the ladder",
        string? body = null,
        DateTimeOffset? startAt = null,
        DateTimeOffset? dueAt = null,
        bool isDeleted = false) => new()
    {
        Id = id,
        Title = title,
        Body = body,
        StartAt = startAt,
        DueAt = dueAt,
        IsDeleted = isDeleted,
    };

    private static string? FindLine(string ics, string prefix)
    {
        foreach (var raw in ics.Split('\n'))
        {
            var line = raw.TrimEnd('\r').TrimStart(' ');
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return line;
        }
        return null;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    // ── 1 — the pinned VTODO subset (C-M14·6) ──────────────────────────────
    //
    // A dated to-do (both dates + a body) emits **exactly** the §vtodo
    // property set, in the locked order — and nothing outside that set.

    [Fact]
    public void BuildTodos_EmitsThePinnedVTodoSubset_ForADatedTodo()
    {
        var todo = MakeTodo(
            "t1",
            title: "Bring the ladder",
            body: "The tall one from the garage.",
            startAt: new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero),
            dueAt: new DateTimeOffset(2026, 10, 3, 18, 0, 0, TimeSpan.Zero));
        var ics = TodoIcsWriter.BuildTodos([todo], NowUtc);

        // The VCALENDAR envelope — the ADR 0112 pin verbatim (one PRODID
        // for the instance, VEVENT and VTODO files alike).
        Assert.Equal("BEGIN:VCALENDAR", ics.Split('\n')[0].TrimEnd('\r'));
        Assert.Contains("VERSION:2.0", ics);
        Assert.Contains("PRODID:-//Kumunita//community calendar//EN", ics);
        Assert.Contains("CALSCALE:GREGORIAN", ics);
        Assert.Contains("METHOD:PUBLISH", ics);

        // The VTODO property set — in the pinned order (§vtodo): UID /
        // DTSTAMP / DUE / DTSTART / SUMMARY / DESCRIPTION (no STATUS —
        // the to-do is not deleted).
        var vtodo = ics[ics.IndexOf("BEGIN:VTODO", StringComparison.Ordinal)
            ..ics.IndexOf("END:VTODO", StringComparison.Ordinal)];
        var vtodoProps = vtodo.Split('\n')
            .Where(l => !l.TrimEnd('\r').TrimStart(' ').StartsWith("BEGIN:VTODO")
                && !l.TrimEnd('\r').TrimStart(' ').StartsWith("END:VTODO"))
            .Select(l => l.TrimEnd('\r').TrimStart(' '))
            .Where(l => l.Length > 0)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "UID:kw-todo-t1@kumunita",
                "DTSTAMP:20260928T120000Z",
                "DUE:20261003T180000Z",
                "DTSTART:20261002T080000Z",
                "SUMMARY:Bring the ladder",
                "DESCRIPTION:The tall one from the garage.",
            },
            vtodoProps);

        // The "never" list (C-M14·5 / C-M14·6) — no property with that name
        // anywhere in the file. Checked as a property name (the part before
        // the first ':' of each physical line), so a banned name can never
        // be confused with a value that merely contains it.
        var propertyNames = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart(' ').Split(':', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var banned in new[]
        {
            "ORGANIZER", "CREATED-BY", "ATTENDEE",
            "RRULE", "RECURRENCE-ID",
            "AUDIENCE", "STATUS", "SEQUENCE", "PRIORITY",
            "CLASS", "CREATED", "LAST-MODIFIED", "REQUEST-ID",
        })
        {
            Assert.DoesNotContain(banned, propertyNames);
        }
        Assert.All(propertyNames, n => Assert.False(n.StartsWith("X-", StringComparison.Ordinal)));
    }

    // ── 2 — DUE / DTSTART conditionality + the undated-skip rule ───────────
    //
    // A `DUE`-only to-do emits no `DTSTART`; a `DTSTART`-only to-do emits
    // no `DUE`; a to-do with **neither** is skipped entirely (no
    // `VTODO` block) — the feed's skip rule (§vtodo).

    [Fact]
    public void BuildTodos_EmitsDueOnly_DtstartOnly_SkipsUndated()
    {
        var dueOnly = MakeTodo("t1", dueAt: new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var startOnly = MakeTodo("t2", startAt: new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));
        var undated = MakeTodo("t3");
        var ics = TodoIcsWriter.BuildTodos([dueOnly, startOnly, undated], NowUtc);

        // Exactly two VTODO blocks — the undated to-do is skipped.
        Assert.Equal(2, CountOccurrences(ics, "BEGIN:VTODO"));
        Assert.DoesNotContain("kw-todo-t3@", ics, StringComparison.Ordinal);

        var block1 = ics[ics.IndexOf("BEGIN:VTODO", StringComparison.Ordinal)
            ..ics.IndexOf("END:VTODO", StringComparison.Ordinal)];
        var block2 = ics[ics.LastIndexOf("BEGIN:VTODO", StringComparison.Ordinal)..];

        // Block 1 — DUE present, DTSTART absent.
        Assert.Contains("DUE:20261001T120000Z", block1, StringComparison.Ordinal);
        Assert.DoesNotContain("DTSTART:", block1, StringComparison.Ordinal);
        Assert.DoesNotContain("UID:kw-todo-t2@kumunita", block1, StringComparison.Ordinal);

        // Block 2 — DTSTART present, DUE absent.
        Assert.Contains("DTSTART:20261002T090000Z", block2, StringComparison.Ordinal);
        Assert.DoesNotContain("DUE:", block2, StringComparison.Ordinal);
        Assert.Contains("UID:kw-todo-t2@kumunita", block2, StringComparison.Ordinal);
    }

    // ── 3 — fold + escape on a long / special-char title+body ──────────────
    //
    // `\` → `\\`, `;` → `\;`, `,` → `\,`, CR/LF → `\n` in SUMMARY /
    // DESCRIPTION; a > 75-octet content line folds to ≥ 2 physical lines,
    // each ≤ 75 octets, continuation lines begin with a single space, and
    // unfolding reconstructs the original logical line (the C-M14·6
    // format pin).

    [Fact]
    public void BuildTodos_FoldsAndEscapesALongTitleAndBody()
    {
        var title = "a\\b;c,d\re\nf";
        var body = new string('a', 120); // "DESCRIPTION:" + 120 octets = 132 — well over 75
        var todo = MakeTodo("t1", title: title, body: body,
            dueAt: new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var ics = TodoIcsWriter.BuildTodos([todo], NowUtc);

        // Escaping — the §vtodo escape pin.
        Assert.Equal("SUMMARY:a\\\\b\\;c\\,d\\ne\\nf", FindLine(ics, "SUMMARY:"));

        // Folding — collect the DESCRIPTION's folded block.
        var physical = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var first = Array.FindIndex(physical, l => l.StartsWith("DESCRIPTION:", StringComparison.Ordinal));
        Assert.True(first >= 0, "expected a DESCRIPTION line");
        var descLines = new List<string>();
        for (var i = first; i < physical.Length; i++)
        {
            if (i > first && !physical[i].StartsWith(" ", StringComparison.Ordinal))
                break; // a non-continuation line ends the folded block
            descLines.Add(physical[i]);
        }

        Assert.True(descLines.Count >= 2, "expected a folded DESCRIPTION across >= 2 physical lines");
        Assert.All(physical, l => Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(l) <= 75,
            $"line exceeds 75 octets: '{l}'"));
        Assert.True(descLines[1].StartsWith(" ", StringComparison.Ordinal)
            && !descLines[1].StartsWith("  ", StringComparison.Ordinal),
            "continuation line must begin with exactly one space");

        // Unfold: strip the single leading space on continuations, concatenate.
        var unfolded = string.Concat(descLines.Select(l => l.TrimStart(' ')));
        Assert.Equal($"DESCRIPTION:{body}", unfolded);

        // The folded SUMMARY (short here, but folded or not it must round-trip).
        var summaryLines = new List<string>();
        var summaryFirst = Array.FindIndex(physical, l => l.StartsWith("SUMMARY:", StringComparison.Ordinal));
        for (var i = summaryFirst; i < physical.Length; i++)
        {
            if (i > summaryFirst && !physical[i].StartsWith(" ", StringComparison.Ordinal))
                break;
            summaryLines.Add(physical[i]);
        }
        Assert.Equal("SUMMARY:a\\\\b\\;c\\,d\\ne\\nf",
            string.Concat(summaryLines.Select(l => l.TrimStart(' '))));
    }

    // ── 4 — no dated to-dos ⇒ a valid empty VCALENDAR ──────────────────────
    //
    // A set with no dated to-dos (an empty set, or only undated to-dos) ⇒
    // the file is exactly the `VCALENDAR` envelope with **zero**
    // `VTODO` blocks — RFC-legal, no crash (C-M14·5/6, the §vtodo
    // empty-calendar pin).

    [Fact]
    public void BuildTodos_ReturnsAValidEmptyCalendar_WhenNoDatedTodos()
    {
        const string envelope =
            "BEGIN:VCALENDAR\r\n" +
            "VERSION:2.0\r\n" +
            "PRODID:-//Kumunita//community calendar//EN\r\n" +
            "CALSCALE:GREGORIAN\r\n" +
            "METHOD:PUBLISH\r\n" +
            "END:VCALENDAR\r\n";

        // An empty set.
        Assert.Equal(envelope, TodoIcsWriter.BuildTodos(Array.Empty<TodoItem>(), NowUtc));

        // A non-empty set of **undated** to-dos — the feed's skip rule
        // drops them all (the per-item degenerate form is the U06
        // lane-1 pin, drift-guard entry 4 — not this feed rule).
        var onlyUndated = TodoIcsWriter.BuildTodos(
            [MakeTodo("t1"), MakeTodo("t2", body: "still undated")], NowUtc);
        Assert.Equal(envelope, onlyUndated);
        Assert.DoesNotContain("VTODO", onlyUndated, StringComparison.Ordinal);
    }
}
