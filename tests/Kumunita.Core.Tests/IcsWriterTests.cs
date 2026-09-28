using Kumunita.Core.Events;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The M12 <see cref="IcsWriter"/> **pinned pure tests** (ADR 0112, D7 —
/// the design doc <c>§pinned tests</c> names, U01 unit of
/// <c>plan-m12-ical.md</c>). The <see cref="IcsWriter"/> is **pure over
/// POCOs** (C-M12·5): no store, no clock (the caller passes
/// <c>nowUtc</c>), no <c>ITagService</c> — so these pins run in
/// milliseconds with **no Testcontainers**. They prove the emitter's
/// **purity + subset pinning + escaping + 75-octet folding + CRLF endings
/// + UID stability + DTSTART/DTEND UTC shape + STATUS-CANCELLED
/// capability + CATEGORIES + empty-feed validity** (C-M12·2 / C-M12·4).
/// <para>
/// The one composition pin
/// (<c>IcsFeedTests.IcsFeed_ContainsExactlyTheVisibleUpcomingSet</c>,
/// <c>PostgresFixture</c>) is <b>not</b> here — U03 owns it.
/// </para>
/// </summary>
public class IcsWriterTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static Event MakeEvent(
        string id,
        string title = "Community cleanup day",
        string body = "",
        string? location = null,
        bool isDeleted = false) => new()
    {
        Id = id,
        Title = title,
        Body = body,
        Location = location,
        Start = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 10, 3, 12, 30, 0, TimeSpan.Zero),
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

    // ── 1 — subset pin (C-M12·4) ─────────────────────────────────────────────
    //
    // The emitted text is **exactly** the D1 property set (the §ics closed
    // list): the VCALENDAR four + the VEVENT always five + the four
    // conditionals — and **nothing** outside that set.

    [Fact]
    public void IcsWriter_Emits_Only_The_Pinned_Subset()
    {
        var ev = MakeEvent("e1", body: "Bring gloves.", location: "Green corner");
        var ics = IcsWriter.Build([ev], NowUtc,
            new Dictionary<string, IReadOnlyList<string>> { ["e1"] = ["Cleanup", "Outdoor"] });

        // The VCALENDAR envelope — exactly the four pinned properties.
        Assert.Equal("BEGIN:VCALENDAR", ics.Split('\n')[0].TrimEnd('\r'));
        Assert.Contains("VERSION:2.0", ics);
        Assert.Contains("PRODID:-//Kumunita//community calendar//EN", ics);
        Assert.Contains("CALSCALE:GREGORIAN", ics);
        Assert.Contains("METHOD:PUBLISH", ics);

        // The VEVENT always set — in the pinned order.
        var vevent = ics[ics.IndexOf("BEGIN:VEVENT", StringComparison.Ordinal)
            ..ics.IndexOf("END:VEVENT", StringComparison.Ordinal)];
        var veventProps = vevent.Split('\n')
            .Where(l => !l.TrimEnd('\r').TrimStart(' ').StartsWith("BEGIN:VEVENT")
                && !l.TrimEnd('\r').TrimStart(' ').StartsWith("END:VEVENT"))
            .Select(l => l.TrimEnd('\r').TrimStart(' '))
            .Where(l => l.Length > 0)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "UID:kw-eve-e1@kumunita",
                "DTSTAMP:20260928T120000Z",
                "DTSTART:20261003T090000Z",
                "DTEND:20261003T123000Z",
                "SUMMARY:Community cleanup day",
                "DESCRIPTION:Bring gloves.",
                "LOCATION:Green corner",
                "CATEGORIES:Cleanup,Outdoor",
            },
            veventProps);

        // The "never" list (D4) — no property with that name anywhere in the
        // file (C-M12·2). Checked as a property name (the part before the
        // first ':' of each physical line), so a banned name can never be
        // confused with a value that merely contains it.
        var propertyNames = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart(' ').Split(':', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
        foreach (var banned in new[]
        {
            "ORGANIZER", "ATTENDEE", "RSVP", "RRULE", "VTODO",
            "COLOR", "GEO", "URL", "SEQUENCE", "PRIORITY",
            "CLASS", "CREATED", "LAST-MODIFIED", "REQUEST-ID",
        })
        {
            Assert.DoesNotContain(banned, propertyNames);
        }
        Assert.All(propertyNames, n => Assert.False(n.StartsWith("X-", StringComparison.Ordinal)));
    }

    // ── 2 — escaping pin ─────────────────────────────────────────────────────
    //
    // Each of `\` → `\\`, `;` → `\;`, `,` → `\,`, CR/LF → `\n` in
    // SUMMARY / DESCRIPTION / LOCATION / CATEGORIES.

    [Fact]
    public void IcsWriter_Escapes_Backslash_Semicolon_Comma_Newline()
    {
        var ev = MakeEvent(
            "e1",
            title: "a\\b;c,d\re\nf",
            body: "line1\nline2",
            location: "Room 10; floor 2, back hall");
        var ics = IcsWriter.Build([ev], NowUtc,
            new Dictionary<string, IReadOnlyList<string>> { ["e1"] = ["a\\b;c,d"] });

        Assert.Equal("SUMMARY:a\\\\b\\;c\\,d\\ne\\nf", FindLine(ics, "SUMMARY:"));
        Assert.Equal("DESCRIPTION:line1\\nline2", FindLine(ics, "DESCRIPTION:"));
        Assert.Equal("LOCATION:Room 10\\; floor 2\\, back hall", FindLine(ics, "LOCATION:"));
        Assert.Equal("CATEGORIES:a\\\\b\\;c\\,d", FindLine(ics, "CATEGORIES:"));
    }

    // ── 3 — 75-octet fold pin ────────────────────────────────────────────────
    //
    // A > 75-octet content line folds to ≥ 2 physical lines, each ≤ 75
    // octets, continuation lines begin with a single space, and unfolding
    // reconstructs the original; a ≤ 75-octet line is **not** folded.

    [Fact]
    public void IcsWriter_Folds_A_Line_Longer_Than_75_Octets()
    {
        // "DESCRIPTION:" is 12 octets; 70 more octets (82 total) crosses 75.
        var body = new string('a', 70);
        var ev = MakeEvent("e1", body: body);
        var ics = IcsWriter.Build([ev], NowUtc);

        var physical = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var first = Array.FindIndex(physical, l => l.StartsWith("DESCRIPTION:", StringComparison.Ordinal));
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

        // A ≤ 75-octet content line is **not** folded.
        var shortIcs = IcsWriter.Build([MakeEvent("e1", title: "short")], NowUtc);
        Assert.Contains("SUMMARY:short", shortIcs, StringComparison.Ordinal);
        Assert.DoesNotContain(" SUMMARY", shortIcs, StringComparison.Ordinal);
    }

    // ── 4 — CRLF pin ─────────────────────────────────────────────────────────
    //
    // Every line ends in `\r\n` (no bare `\n`), including the final
    // `END:VCALENDAR`.

    [Fact]
    public void IcsWriter_Uses_CRLF_Line_Endings()
    {
        var ics = IcsWriter.Build([MakeEvent("e1")], NowUtc);

        Assert.Contains("\r\n", ics, StringComparison.Ordinal);
        Assert.Equal(0, ics.Split('\n').Where(l => l.Length > 0).Count(l => !l.EndsWith("\r", StringComparison.Ordinal)));
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
    }

    // ── 5 — UID stability pin ────────────────────────────────────────────────
    //
    // The same `Event.Id` ⇒ the same `UID` (`kw-eve-{Id}@kumunita`) across
    // two `Build` calls; two different ids ⇒ two different `UID`s.

    [Fact]
    public void IcsWriter_Keeps_UID_Stable_Per_Event_Id()
    {
        var ics1 = IcsWriter.Build([MakeEvent("abc")], new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var ics2 = IcsWriter.Build([MakeEvent("abc")], new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(FindLine(ics1, "UID:"), FindLine(ics2, "UID:"));
        Assert.Equal("UID:kw-eve-abc@kumunita", FindLine(ics1, "UID:"));

        var ics3 = IcsWriter.Build([MakeEvent("abc"), MakeEvent("def")], NowUtc);
        Assert.Contains("UID:kw-eve-abc@kumunita", ics3, StringComparison.Ordinal);
        Assert.Contains("UID:kw-eve-def@kumunita", ics3, StringComparison.Ordinal);
    }

    // ── 6 — DTSTART/DTEND UTC shape pin ─────────────────────────────────────
    //
    // `DTSTART` / `DTEND` are `yyyyMMddTHHmmssZ` (the stored UTC instant,
    // `Z`-suffixed, no offset).

    [Fact]
    public void IcsWriter_Writes_DTSTART_DTEND_As_UTC_Z_Suffixed()
    {
        var ics = IcsWriter.Build([MakeEvent("e1")], NowUtc);

        Assert.Equal("DTSTART:20261003T090000Z", FindLine(ics, "DTSTART:"));
        Assert.Equal("DTEND:20261003T123000Z", FindLine(ics, "DTEND:"));
        Assert.Equal("DTSTAMP:20260928T120000Z", FindLine(ics, "DTSTAMP:"));
    }

    // ── 7 — DESCRIPTION carries Markdown source, not HTML ───────────────────
    //
    // `DESCRIPTION` is the **Markdown source** of `Body` (escaped), **not**
    // rendered HTML (D4 — HTML in DESCRIPTION is non-portable).

    [Fact]
    public void IcsWriter_DESCRIPTION_Carries_Markdown_Source_Not_Html()
    {
        var ev = MakeEvent("e1", body: "Bring **gloves** and [a rake](/attachments/rake.md)");
        var ics = IcsWriter.Build([ev], NowUtc);

        Assert.Equal("DESCRIPTION:Bring **gloves** and [a rake](/attachments/rake.md)",
            FindLine(ics, "DESCRIPTION:"));
        Assert.DoesNotContain("<strong>", ics, StringComparison.Ordinal);
        Assert.DoesNotContain("<a ", ics, StringComparison.Ordinal);

        // Body empty ⇒ no DESCRIPTION line at all (the conditional pin).
        var ics2 = IcsWriter.Build([MakeEvent("e2")], NowUtc);
        Assert.DoesNotContain("DESCRIPTION:", ics2, StringComparison.Ordinal);
    }

    // ── 8 — STATUS:CANCELLED capability pin ─────────────────────────────────
    //
    // An `IsDeleted` event carries `STATUS:CANCELLED` (the capability the
    // pure emitter supports — the §ics note on the frozen-seam
    // reachability: drift-guard entry 1).

    [Fact]
    public void IcsWriter_Emits_STATUS_CANCELLED_On_A_Deleted_Event()
    {
        var deleted = IcsWriter.Build([MakeEvent("e1", isDeleted: true)], NowUtc);
        Assert.Contains("STATUS:CANCELLED", deleted, StringComparison.Ordinal);

        var live = IcsWriter.Build([MakeEvent("e1")], NowUtc);
        Assert.DoesNotContain("STATUS:", live, StringComparison.Ordinal);
    }

    // ── 9 — CATEGORIES from resolved tags ────────────────────────────────────
    //
    // The caller-passed `CATEGORIES` names are emitted (escaped,
    // comma-joined, in the pinned order); no `CATEGORIES` when the map has
    // no entry for the event.

    [Fact]
    public void IcsWriter_Categories_From_Resolved_Tags()
    {
        var map = new Dictionary<string, IReadOnlyList<string>>
        {
            ["e1"] = ["Cleanup", "Outdoor, evening"],
        };
        var ics = IcsWriter.Build([MakeEvent("e1"), MakeEvent("e2")], NowUtc, map);

        // Escaped individually, then joined with the literal list-separator comma.
        Assert.Contains("CATEGORIES:Cleanup,Outdoor\\, evening", ics, StringComparison.Ordinal);

        // No map entry for e2 ⇒ no CATEGORIES for it (exactly one in the file).
        Assert.Equal(1, CountOccurrences(ics, "CATEGORIES:"));
        var vevent2 = ics[ics.LastIndexOf("BEGIN:VEVENT", StringComparison.Ordinal)..];
        Assert.DoesNotContain("CATEGORIES:", vevent2, StringComparison.Ordinal);
    }

    // ── 10 — empty feed is a valid empty calendar ───────────────────────────
    //
    // `events` empty ⇒ the file is exactly the `VCALENDAR` envelope (the
    // four envelope properties) with **zero** `VEVENT` blocks — RFC-legal,
    // no crash (C-M12·4, the empty-feed-valid pin).

    [Fact]
    public void IcsWriter_Empty_Feed_Is_A_Valid_Empty_Calendar()
    {
        var ics = IcsWriter.Build(Array.Empty<Event>(), NowUtc);

        Assert.Equal(
            "BEGIN:VCALENDAR\r\n" +
            "VERSION:2.0\r\n" +
            "PRODID:-//Kumunita//community calendar//EN\r\n" +
            "CALSCALE:GREGORIAN\r\n" +
            "METHOD:PUBLISH\r\n" +
            "END:VCALENDAR\r\n",
            ics);
        Assert.DoesNotContain("VEVENT", ics, StringComparison.Ordinal);
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
}
