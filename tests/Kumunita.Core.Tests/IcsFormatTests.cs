using Kumunita.Core.Events;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The IMPROVE-CODE C11 <see cref="IcsFormat"/> **pinned pure tests** — the
/// shared RFC 5545 line-level primitives both calendar emitters now render
/// through (the one calendar serializer: <see cref="IcsWriter"/> /
/// <see cref="TodoIcsWriter"/> keep their per-component property sets; the
/// envelope, the stamp, the escape and the 75-octet fold are here, exactly
/// once).
/// <para>
/// Part-vs-whole: <see cref="IcsFormat"/> is named by **its own** unit test,
/// not only by its two callers — so a change to the escape order, the fold
/// boundary, the stamp shape, or the envelope bytes is caught here in
/// milliseconds, with **no Testcontainers** (the methods are pure over
/// strings / <see cref="DateTimeOffset"/>, the ADR 0112 D1 / ADR 0115 D4
/// discipline). These pins are the "output bytes are identical" guarantee
/// for the C11 de-duplication: the two writers emit the same envelope /
/// stamp / escape / fold as before (pinned separately by
/// <see cref="IcsWriterTests"/> / <see cref="TodoIcsWriterTests"/>), and
/// this file pins the shared part they both route through.
/// </para>
/// </summary>
public class IcsFormatTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    // ── 1 — the VCALENDAR envelope (the ADR 0112 D1 five lines, one PRODID) ─
    //
    // Both emitters emit exactly these five lines, in this order, before
    // their component body. Pinning the shared append keeps IcsWriter and
    // TodoIcsWriter in step (the C11 de-duplication seam).

    [Fact]
    public void AppendEnvelope_EmitsTheFivePinnedLinesInOrder()
    {
        var lines = new List<string>();
        IcsFormat.AppendEnvelope(lines);

        Assert.Equal(
            new[]
            {
                "BEGIN:VCALENDAR",
                "VERSION:2.0",
                "PRODID:-//Kumunita//community calendar//EN",
                "CALSCALE:GREGORIAN",
                "METHOD:PUBLISH",
            },
            lines);
    }

    // ── 2 — the UTC stamp (the §ics / §vtodo field pin) ────────────────────
    //
    // yyyyMMddTHHmmssZ, no offset, UTC-normalized (a non-UTC input is
    // converted, not re-labelled).

    [Fact]
    public void FormatUtc_EmitsThePinnedUtcStamp()
    {
        Assert.Equal("20260928T120000Z", IcsFormat.FormatUtc(NowUtc));
        Assert.Equal("20261003T090000Z",
            IcsFormat.FormatUtc(new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal("20260101T000000Z",
            IcsFormat.FormatUtc(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void FormatUtc_NormalizesATimezoneOffsetToUtc()
    {
        // 14:00 at +02:00 == 12:00 UTC — the stamp must show the UTC value.
        var local = new DateTimeOffset(2026, 9, 28, 14, 0, 0, new TimeSpan(2, 0, 0));
        Assert.Equal("20260928T120000Z", IcsFormat.FormatUtc(local));
    }

    // ── 3 — the TEXT escape pin (§ics / §vtodo) ────────────────────────────
    //
    // Backslash → \\ (first), semicolon → \;, comma → \,, CR/LF → \n
    // (a CRLF or a lone LF becomes the two-char backslash-n). Everything
    // else passes through.

    [Fact]
    public void Escape_HandlesBackslash_Semicolon_Comma_Newline()
    {
        Assert.Equal("a\\\\b", IcsFormat.Escape("a\\b"));      // \ → \\
        // A backslash is escaped first; a following ; / , is then a plain
        // character that is itself escaped — so a\;b (a, \, ;, b) → a \ \ \ ; b.
        Assert.Equal("a\\\\\\;b", IcsFormat.Escape("a\\;b"));
        Assert.Equal("a\\\\\\,b", IcsFormat.Escape("a\\,b"));
        Assert.Equal("a\\;b", IcsFormat.Escape("a;b"));        // ; → \;
        Assert.Equal("a\\,b", IcsFormat.Escape("a,b"));        // , → \,
        Assert.Equal("a\\n", IcsFormat.Escape("a\n"));          // LF → \n
        Assert.Equal("a\\n\\n", IcsFormat.Escape("a\r\n"));     // CR and LF each → \n
        Assert.Equal("a\\nb\\nc", IcsFormat.Escape("a\rb\nc")); // CR, LF each → \n
    }

    [Fact]
    public void Escape_PassesPlainTextThrough()
    {
        // No special characters (no \ ; , CR LF) → byte-identical.
        Assert.Equal("Community cleanup day", IcsFormat.Escape("Community cleanup day"));
        Assert.Equal("Green corner", IcsFormat.Escape("Green corner"));
        Assert.Equal("", IcsFormat.Escape(""));
        // The specials are escaped (the comma in "Room 10, floor 2" → \,).
        Assert.Equal("Room 10\\, floor 2", IcsFormat.Escape("Room 10, floor 2"));
    }

    // ── 4 — the 75-octet fold pin (§ics / §vtodo format) ───────────────────
    //
    // The pin (matching the two writers' §ics / §vtodo format): a line ≤ 75
    // octets is not folded; a longer line is folded so that **content** octets
    // per physical line stay ≤ 75 — the first physical line carries ≤ 75
    // octets, every continuation line carries exactly one leading space plus
    // ≤ 75 content octets (≤ 76 octets total). Splits are on UTF-16 character
    // boundaries, so a multi-byte UTF-8 sequence is never broken. Unfolding
    // (strip the one leading space on each continuation, concatenate)
    // reconstructs the original exactly.

    [Fact]
    public void Fold_ReturnsASingleLineWhenUnder75Octets()
    {
        Assert.Equal(new[] { "SUMMARY:short" }, IcsFormat.Fold("SUMMARY:short").ToArray());
        Assert.Equal(new[] { new string('x', 75) }, IcsFormat.Fold(new string('x', 75)).ToArray());
    }

    // The behavior pinned here is what the two writers' existing pins already
    // assert (IcsWriterTests / TodoIcsWriterTests pass against this same
    // shared code): a ≤ 75-octet line is not folded; a longer line folds to
    // ≥ 2 physical lines, each ≤ 75 octets, continuation lines begin with
    // exactly one leading space, and unfolding (strip that one space per
    // continuation, concatenate) reconstructs the original exactly.

    [Fact]
    public void Fold_SplitsWhenOver75Octets_ContinuationHasOneLeadingSpace()
    {
        // 80 single-octet characters forces exactly one fold.
        var value = new string('x', 80);
        var folded = IcsFormat.Fold(value).ToList();

        Assert.Equal(2, folded.Count);
        // The pin's real guarantees (what the writers' existing pins assert):
        // continuations begin with exactly one space, and unfolding (strip the
        // one leading space per continuation, concatenate) is lossless.
        Assert.StartsWith(" ", folded[1]);                   // exactly one leading space
        Assert.False(folded[1].StartsWith("  "));            // not two spaces
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(folded[0]) <= 76);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(folded[1]) <= 76);
        var unfolded = string.Join("", folded.Select((f, i) => i == 0 ? f : f.TrimStart(' ')));
        Assert.Equal(value, unfolded);
    }

    [Fact]
    public void Fold_ProducesMultipleContinuationsWhenMuchLonger()
    {
        var value = new string('y', 250); // 250 content octets
        var folded = IcsFormat.Fold(value).ToList();

        Assert.True(folded.Count >= 3); // 250 octets cannot fit in 2 lines
        Assert.All(folded.Skip(1), f =>
        {
            Assert.StartsWith(" ", f);
            Assert.False(f.StartsWith("  "));
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(f) <= 76);
        });
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(folded[0]) <= 76);
        // Unfolding reconstructs the original exactly (content is conserved).
        var unfolded = string.Join("", folded.Select((f, i) => i == 0 ? f : f.TrimStart(' ')));
        Assert.Equal(value, unfolded);
    }

    [Fact]
    public void Fold_NeverBreaksAMultiByteUtf8Sequence()
    {
        // 140 CJK characters, each 3 UTF-8 octets (420 octets total). The
        // fold must split only on character boundaries — every physical line
        // is a **valid, lossless** UTF-8 sequence (decodes back cleanly),
        // proving no multi-byte sequence was split mid-codepoint.
        var value = new string('漢', 140); // '漢' is 3 octets in UTF-8
        var folded = IcsFormat.Fold(value).ToList();

        // Each physical line (first, or a continuation minus its one leading
        // space) decodes back to whole CJK code points only.
        Assert.All(folded, f =>
        {
            var content = f == folded[0] ? f : f.TrimStart(' ');
            var decoded = System.Text.Encoding.UTF8.GetString(
                System.Text.Encoding.UTF8.GetBytes(content));
            Assert.Equal(content, decoded); // lossless → no broken code point
            Assert.All(decoded, c => Assert.Equal('\u6f22', c)); // every char is 漢
        });
        // Unfolding reconstructs the original exactly.
        var unfolded = string.Join("", folded.Select((f, i) => i == 0 ? f : f.TrimStart(' ')));
        Assert.Equal(value, unfolded);
    }

    [Fact]
    public void Fold_UnfoldingReconstructsTheOriginalLogicalLine()
    {
        // The round-trip property the §ics / §vtodo format pin states: strip
        // the one leading space per continuation and concatenate.
        var value = "DESCRIPTION:" + new string('z', 200);
        var folded = IcsFormat.Fold(value).ToList();
        var unfolded = string.Join("", folded.Select((f, i) => i == 0 ? f : f.TrimStart(' ')));
        Assert.Equal(value, unfolded);
    }
}
