using System.Text.RegularExpressions;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS15 (IMPROVE-SEC) — the P-4 **decision-free-read gate** (E4). Retires the
/// M7 baseline (52 unmarked → 0) and makes the defect class unrepeatable: a
/// method that reads an audience-bearing type with no decision in-method and
/// no <c>// SEC-READ:</c> marker is a **red build**, not a review comment
/// (the lane's doctrine: *the parts are cheap, the linkage is rare* — this is
/// the linkage).
///
/// <b>Pre-written scaffold.</b> The two tests are wired up and compile; the
/// genuinely hard logic (per-method extraction + read-reference detection) is
/// implemented but is a **source heuristic, not a full C# parse** — the same
/// posture as <see cref="SecGateSource.TestMethodExists"/> ("a full C# parse
/// is overkill for a gate"). Before IS15 flips this from scaffold to done, the
/// calibration points below MUST be verified against a red/green run (the
/// <c>// CALIBRATION</c> notes mark them).
///
/// <b>What counts as a "read" (the detection contract).</b> A method
/// references an audience-bearing type when one of the four class-(b) read
/// idioms names a type from <see cref="AudienceBearingTypes"/> as a generic
/// argument — <c>LoadAsync&lt;T&gt;</c>, <c>Query&lt;T&gt;</c>, and their
/// siblings — or returns a bare <c>T</c>. A type name inside a comment / XML
/// doc is **not** a reference.
///
/// <b>What satisfies the gate (the "decided in-method" branch).</b> Any of
/// <see cref="DecisionIdioms"/> in the method body, OR a
/// <c>// SEC-READ:</c> marker in the body (the IS11–IS13 pattern). A method
/// with neither is a violation.
///
/// <b>Path excludes</b> (the plan names these): seeders, portability, the
/// eraser, purge jobs and migrations read audience-bearing types **by design**
/// (export / erasure / seed) and are outside the per-request contract.
/// </summary>
public class DecisionFreeReadGateTests
{
    /// <summary>
    /// The audience-bearing (class (b)) type set — the 41 rows of design §2.5
    /// where the "Class" column is <c>b</c> (the private-content /
    /// audience-restricted classes). Pinned here, **not** reconstructed from
    /// memory; if §2.5 gains a class-(b) row the gate must grow with it (that
    /// is the point of the gate).
    /// </summary>
    private static readonly HashSet<string> AudienceBearingTypes = new(StringComparer.Ordinal)
    {
        "Post", "PostReply", "PostTranslation", "ReplyTranslation",
        "Announcement", "AnnouncementComment", "AnnouncementTranslation",
        "Event", "EventRsvp", "EventTranslation",
        "KanbanBoard", "KanbanLane", "BoardItemPlacement", "BoardTranslation",
        "Page", "PageTranslation",
        "Project", "ProjectGoal", "ProjectTranslation",
        "TodoItem", "TodoComment", "TodoTranslation",
        "Conversation", "Message", "GroupConversation", "GroupMessage",
        "GroupMessagingInvitation", "Group", "GroupMembership", "GroupTranslation",
        "CommunityTranslation",
        "Tag", "TagTranslation",
        "Document", "DocumentFolder", "Bookmark", "BookmarkFolder",
        "PersonalNote", "NoteFolder",
        "InventoryItem", "InventoryCheckout",
    };

    /// <summary>
    /// A Marten **read idiom** naming a type as a generic argument — the
    /// reference-detection contract. The codebase uses <c>LoadAsync&lt;T&gt;</c>
    /// (371 sites) and <c>Query&lt;T&gt;</c> (442 sites); the rest are included
    /// for robustness (the gate is the *linkage*, not a narrow probe).
    /// Group 1 is the type name.
    /// </summary>
    private static readonly Regex ReadIdiom = new(
        @"\b(?:LoadAsync|LoadManyAsync|Load|Query|QuerySingle|FetchAsync|Fetch)\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>",
        RegexOptions.Compiled);

    /// <summary>
    /// The author-only ordinal via <c>string.Equals(&lt;x&gt;.AuthorId, actor…)</c>
    /// — the one decision idiom that is a call, not a bare keyword (the IS14
    /// Web-Edit posture, and much of IS11/12/13's Core).
    /// </summary>
    private static readonly Regex AuthorOrdinalEquals = new(
        @"string\.Equals\s*\(\s*[A-Za-z0-9_]+\s*\.\s*AuthorId\s*,\s*actor",
        RegexOptions.Compiled);

    /// <summary>
    /// The "decided in-method" idiom set (the IS11–14 taxonomy). A method body
    /// containing any of these **over the rows it reads** is considered
    /// decided and needs no marker. // CALIBRATION: verify against a run —
    /// this list is the gate's precision; a missing idiom here yields a false
    /// positive, an over-broad one masks a real leak.
    /// </summary>
    private static readonly string[] DecisionIdioms =
    {
        "CanAsync(", "CanSeeAsync(",
        "PostReadDecision.ResolveAsync",
        "CheckEditStanding", "CheckCreateStanding", "CheckTodoStanding", "CheckStanding",
        "ResolveMoveDeleteStanding",
        "AuthorId != actor", "OwnerId != actorId", "OwnerId == actorId",
    };

    /// <summary>
    /// A top-level method declaration line: an accessibility /
    /// <c>static</c> modifier prefix, then an identifier, then a parameter
    /// list. Anchoring on the modifier is the discriminator between a
    /// declaration and a call site (the latter never starts with
    /// <c>public</c>/<c>private</c>/…). // CALIBRATION: local functions and
    /// expression-bodied members in unusual positions are the weak spot.
    /// </summary>
    private static readonly Regex MethodDecl = new(
        @"^\s*(?:public|private|protected|internal|static)\b.*?\b\w+\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Path-exclude tokens (the plan): a file whose path contains any of these
    /// is skipped wholesale.
    /// </summary>
    private static readonly string[] PathExcludes =
    {
        "Seeder", "Portability", "Eraser", "Purge", "Migration", "DocTypes", "SeededContentCatalog",
    };

    /// <summary>
    /// The <c>// SEC-READ:</c> marker, captured so the taxonomy + reason can
    /// be checked. Matches the IS11–13 line form.
    /// </summary>
    private static readonly Regex Marker = new(
        @"//\s*SEC-READ:\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// The four taxonomy words a marker must open with (the IS11–13 style).
    /// // CALIBRATION: the IS11–13 markers use exactly these four.
    /// </summary>
    private static readonly string[] TaxonomyWords =
    {
        "caller-gated", "owner-only", "no disclosure", "report-gated",
    };

    // ── Test 1: the 52 → 0 retirement ──────────────────────────────────────

    [Fact(DisplayName = "Every audience-bearing read is decided in-method or marked (M7: 52 -> 0)")]
    public void EveryAudienceBearingRead_IsDecidedOrMarked()
    {
        string rootPrefix = SecGateSource.RepoRoot.Replace('\\', '/') + "/";
        var violations = new List<string>();

        foreach (string file in SecGateSource.AllSrcFiles("*.cs"))
        {
            string rel = file.Replace('\\', '/').Replace(rootPrefix, "");
            if (!rel.StartsWith("src/", StringComparison.Ordinal))
                continue;
            if (PathExcludes.Any(rel.Contains))
                continue;

            string src = SecGateSource.ReadSrc(rel);

            foreach (Match decl in MethodDecl.Matches(src))
            {
                int declIndex = decl.Index;
                int declLine = LineOf(src, declIndex);

                // The body is from the first '{' (or '=>' for expression-bodied)
                // at or after the declaration line, brace-matched.
                int bodyEnd = ExtractBodyEnd(src, declIndex, out string body);
                if (string.IsNullOrEmpty(body))
                    continue; // abstract / interface / partial-without-body

                // 1. Does this method reference an audience-bearing type via a read idiom?
                string? referencedType = FirstReferencedType(body);
                if (referencedType is null)
                    continue; // no audience-bearing read in this method

                // 2. Is it decided in-method or marked?
                if (IsDecided(body) || body.Contains("// SEC-READ:"))
                    continue;

                violations.Add($"{rel}:{declLine}  reads <{referencedType}> — no decision in-method, no SEC-READ marker");
            }
        }

        violations = violations.Distinct().ToList();
        Assert.True(
            violations.Count == 0,
            "Method(s) that read an audience-bearing type with no decision in-method and no "
            + "// SEC-READ marker. Decide in-method (CanAsync / CanSeeAsync / the standing-policy "
            + "family / the author-only ordinal) OR add the // SEC-READ marker (the IS11–14 pattern). "
            + "A genuine leak is a PAUSE (a new IS10x), not a mark.\n  "
            + string.Join("\n  ", violations));
    }

    // ── Test 2: every marker ends in a reason ──────────────────────────────

    [Fact(DisplayName = "Every SEC-READ marker names a reason")]
    public void SecReadMarker_NamesAReason()
    {
        string rootPrefix = SecGateSource.RepoRoot.Replace('\\', '/') + "/";
        var bad = new List<string>();
        int count = 0;

        foreach (string file in SecGateSource.AllSrcFiles("*.cs"))
        {
            string rel = file.Replace('\\', '/').Replace(rootPrefix, "");
            if (!rel.StartsWith("src/", StringComparison.Ordinal))
                continue;
            string src = SecGateSource.ReadSrc(rel);

            foreach (Match m in Marker.Matches(src))
            {
                count++;
                int line = LineOf(src, m.Index);
                string rest = m.Groups[1].Value.Trim();

                bool hasTaxonomy = TaxonomyWords.Any(t => rest.StartsWith(t, StringComparison.Ordinal));
                // The reason is whatever follows the taxonomy + a separator
                // ('—', '(', ' | '). Everything after the first such separator.
                int sep = FirstIndexOfAny(rest, "—(|");
                string reason = sep >= 0 ? rest[(sep + 1)..].Trim() : rest.Trim();

                bool ok = hasTaxonomy && reason.Length > 0
                          && !reason.All(char.IsPunctuation)
                          && reason != rest.Trim(); // there IS a reason beyond the taxonomy word

                if (!ok)
                    bad.Add($"{rel}:{line}  '{rest}'");
            }
        }

        Assert.True(bad.Count == 0,
            $"Marker(s) that do not open with a taxonomy word and end in a reason "
            + $"({count} markers scanned):\n  " + string.Join("\n  ", bad.Distinct()));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>The first class-(b) type referenced via a read idiom in <paramref name="body"/>.</summary>
    private static string? FirstReferencedType(string body)
    {
        foreach (Match m in ReadIdiom.Matches(body))
        {
            string t = m.Groups[1].Value;
            if (AudienceBearingTypes.Contains(t))
                return t;
        }
        return null;
    }

    private static bool IsDecided(string body)
        => DecisionIdioms.Any(idiom => body.Contains(idiom)) || AuthorOrdinalEquals.IsMatch(body);

    private static int LineOf(string src, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < src.Length; i++)
            if (src[i] == '\n')
                line++;
        return line;
    }

    private static int FirstIndexOfAny(string s, string chars)
    {
        foreach (char c in chars)
        {
            int i = s.IndexOf(c);
            if (i >= 0)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// From a declaration's start, finds the first <c>{</c> or <c>=&gt;</c> at
    /// or after it and returns the end index of the method body (after the
    /// matching <c>}</c>, or the end of the <c>;</c>-terminated expression
    /// body), and writes the body text to <paramref name="body"/>. Returns
    /// -1 and an empty body when the member has no body (abstract / interface).
    /// </summary>
    private static int ExtractBodyEnd(string src, int declIndex, out string body)
    {
        body = string.Empty;
        int i = declIndex;
        while (i < src.Length && src[i] != '{' && !(src[i] == '=' && i + 1 < src.Length && src[i + 1] == '>'))
            i++;
        if (i >= src.Length)
            return -1;

        if (src[i] == '{')
        {
            int depth = 0;
            int start = i;
            for (int j = i; j < src.Length; j++)
            {
                char c = src[j];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        body = src[(start + 1)..j];
                        return j;
                    }
                }
            }
            // unbalanced — take the rest
            body = src[(start + 1)..];
            return src.Length - 1;
        }

        // expression body: to the terminating ';' (string-aware enough for the scaffold)
        int semi = src.IndexOf(';', i);
        if (semi < 0)
            semi = src.Length - 1;
        body = src[(i + 2)..semi];
        return semi;
    }
}
