using System.Text.RegularExpressions;

namespace Kumunita.Web.Tests.SecGate;

/// <summary>
/// IS15 (IMPROVE-SEC) — the P-4 **decision-free-read gate** (E4). Retires the
/// M7 baseline (52 unmarked → 0) and makes the defect class unrepeatable: a
/// method on the <b>per-request surface</b> that reads an audience-bearing
/// type with no decision in-method and no <c>// SEC-READ:</c> marker is a
/// **red build**, not a review comment (the lane's doctrine: *the parts are
/// cheap, the linkage is rare* — this is the linkage).
///
/// <b>What counts as a per-request read (the detection contract).</b> A
/// <b>public</b> method that references an audience-bearing (class (b),
/// design §2.5) type as a <b>genuine read</b> — a <c>LoadAsync&lt;T&gt;</c> /
/// <c>Query&lt;T&gt;</c> generic argument that is <i>not</i> a strategy
/// delegate (<c>=&gt; … Load&lt;T&gt;</c>, e.g. a <c>TranslationLane</c>
/// descriptor) and <i>not</i> a <c>new T(…)</c> write — <b>and</b> that
/// operates for a caller: it takes an actor-identity parameter, or it
/// references the request principal (<c>SubjectId(</c> / <c>RoleSet(</c> /
/// <c>KumunitaPrincipal</c> / <c>User</c>).
///
/// <b>Why the actor/request-principal scope.</b> A public method with no
/// actor and no request principal <i>cannot</i> make a per-request decision —
/// it does not know who is requesting — so it is by definition a
/// caller-gated data-access seam (the codebase's documented "a read, not a
/// decision" shape: <c>GroupService.GetGroupAsync</c>,
/// <c>AnnouncementService.FindByImageIdAsync</c>, …) or a background job
/// (<c>EventReminderService.SendRemindersAsync</c>). Those are <b>outside</b>
/// the per-request contract and are not defects. Private helpers are likewise
/// composition seams decided by their caller. Scoping the gate to the
/// per-request surface is what separates the M7 defect (a read that
/// <i>could</i> decide but doesn't) from a legitimate seam.
///
/// <b>What satisfies the gate.</b> Any <see cref="DecisionIdioms"/> signal in
/// the body (the <c>IAuthorizationService</c> <c>Can*</c> standing family, the
/// <c>*Standing</c> policy family, the owner/author/self ordinal, the
/// self-scoped query, the public/private scope filter, the messaging
/// instance toggle, …) <b>or</b> a <c>// SEC-READ:</c> marker (the IS11–13
/// pattern for seams whose decision is in the caller).
///
/// <b>Path excludes</b> (the plan names these): seeders, portability, the
/// eraser, purge jobs and migrations read audience-bearing types **by design**
/// (export / erasure / seed) and are outside the per-request contract.
///
/// <b>Soundness, verified in IS15.</b> (a) the current code reports
/// 0 violations; (b) a synthetic public method that reads an audience-bearing
/// type for an actor with no decision and no marker is flagged (the gate is
/// not vacuous); (c) the IS10 <c>TodoDetail</c> board-title leak shape — a
/// public per-request read with no decision — is flagged (the gate would have
/// caught the confirmed S-leak it retired); (d) the six IS14 in-method
/// <c>Edit</c> actions and the 64 existing markers are not flagged.
/// </summary>
public class DecisionFreeReadGateTests
{
    /// <summary>
    /// The audience-bearing (class (b)) type set — the 41 rows of design §2.5
    /// where the "Class" column is <c>b</c> (private content /
    /// audience-restricted). Pinned here, **not** reconstructed from memory;
    /// if §2.5 gains a class-(b) row the gate must grow with it (that is the
    /// point of the gate).
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
    /// A Marten <b>read idiom</b> naming a type as a generic argument — the
    /// reference-detection contract. Group 1 is the type name.
    /// </summary>
    private static readonly Regex ReadIdiom = new(
        @"\b(?:LoadAsync|LoadManyAsync|Load|Query|QuerySingle|FetchAsync|Fetch)\s*<\s*([A-Za-z_][A-Za-z0-9_]*)\s*>",
        RegexOptions.Compiled);

    /// <summary>
    /// The "decided in-method" <b>named-guard</b> signals (the IS11–14
    /// taxonomy, calibrated against the real code in this repo). Each entry is
    /// a specific identifier that names a decision the method itself performs
    /// over the rows it reads. Deliberately <b>not</b> the bare English words
    /// "guardian" / "veto" — a comment mentioning them is not a decision; only
    /// a real guard call (<c>GuardianVeto…</c>) is. <c>Standing</c> is matched
    /// as a substring (it is a suffix — <c>CheckTodoStanding</c> /
    /// <c>HasEditStanding</c> / <c>ResolveMoveDeleteStanding</c> — so a
    /// word-boundary match would miss it); it is specific enough that a bare
    /// occurrence is a standing decision, not noise.
    /// </summary>
    private static readonly string[] DecisionIdioms =
    {
        // the *Standing policy family (CheckEditStanding / CheckCreateStanding /
        // CheckTodoStanding / ResolveMoveDeleteStanding / HasEditStanding / …)
        "Standing",
        // the Owner/Admin standing derivation (a decision, not a read)
        "AccessVia",
        // permission guards
        "EnsureEditPermission", "EnsurePermission",
        // the messaging instance toggle + guardian veto (a per-actor gate)
        "EnsureEnabledAsync", "IsMessagingAllowed", "GuardianVeto",
        // membership / participation checks
        "Participants.Contains", "IsParticipant", "IsMember", "Members.Contains", "Contains(actor",
        // the frozen Read-decision delegates
        "ResolveAsync", "ResolveForListAsync", "ResolveReadVisibility", "LoadActorReadableContent",
        // the public/private scope filter (a self-contained visibility decision)
        ".IsPublic", ".IsPrivate",
    };

    /// <summary>
    /// An <b>actor-identity parameter</b> in the method's signature — the
    /// marker of a per-request surface that can (and must) make a decision.
    /// </summary>
    private static readonly Regex ActorParam = new(
        @"\b(actorId|actor|roles|principal|residentId|selfId|ownerId|userId|addedBy)\b",
        RegexOptions.Compiled);

    /// <summary>
    /// A <b>request-principal</b> reference in the body — a controller action
    /// that derives the actor from <c>HttpContext</c> / <c>ClaimsPrincipal</c>
    /// rather than a parameter is still a per-request surface.
    /// </summary>
    private static readonly Regex RequestPrincipal = new(
        @"\b(SubjectId\s*\(|RoleSet\s*\(|KumunitaPrincipal)\b|\bUser\b",
        RegexOptions.Compiled);

    /// <summary>
    /// A top-level method declaration line: an accessibility / <c>static</c>
    /// modifier prefix, then an identifier, then a parameter list.
    /// (Constructors and field initializers that carry a modifier also match;
    /// the public-only scope + genuine-read + actor/request-principal rules
    /// below are what exclude them from the per-request contract — see the
    /// class doc.)
    /// </summary>
    private static readonly Regex MethodDecl = new(
        @"^\s*(?:public|private|protected|internal|static)\b.*?\b\w+\s*\(",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Path-exclude tokens (the plan): a file whose path contains any of these
    /// is skipped wholesale (seeder / export / erasure / purge / schema).
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
    /// </summary>
    private static readonly string[] TaxonomyWords =
    {
        "caller-gated", "owner-only", "no disclosure", "report-gated",
    };

    /// <summary>
    /// The principal-field set — the identity fields an owner/author/self
    /// ordinal is keyed on.
    /// </summary>
    private const string PrincipalFields = "AuthorId|OwnerId|UserId|CreatedBy|RecipientId|SubjectId|SenderId|InvitedId";

    /// <summary>
    /// An actor-like token — the identity an ordinal is compared against.
    /// </summary>
    private const string ActorTokens = "actor|owner|author|principal|updatedBy|resident|self|userId|ownerId|actorId";

    /// <summary>
    /// Decision patterns (regex) that generalize the <c>IAuthorizationService</c>
    /// <c>Can*</c> standing family (<c>CanSeeAsync</c> / <c>CanSeeGroupAsync</c> /
    /// <c>CanSeeGroupFeedAsync</c> / <c>CanPost</c> / <c>CanRead</c> / …), the
    /// <c>Gate*</c> decision helpers, and the <b>owner/author/self ordinal</b>
    /// (a principal field compared to an actor token, in the <c>x.OwnerId ==
    /// actor</c>, <c>actor == x.OwnerId</c>, and <c>string.Equals(x.OwnerId,
    /// actor)</c> forms, or a self-scoped query) — whole families in one
    /// pattern each, so a new standing seam or a new principal field cannot
    /// drift past the gate the way a flat name list would.
    /// </summary>
    private static readonly Regex[] DecisionPatterns =
    {
        new(@"\bCan[A-Z]\w*(?:Async)?\s*\(", RegexOptions.Compiled),
        new(@"\bGate\w*\s*\(", RegexOptions.Compiled),
        new(@"\.Decide\w*\s*\(|\bDecide\w*\s*\(", RegexOptions.Compiled),
        new($@"\.(?:{PrincipalFields})\s*[=!]="),
        new($@"(?:{PrincipalFields})\s*[=!]=\s*(?:{ActorTokens})\b"),
        new($@"(?:{ActorTokens})\s*[=!]=\s*\w*\.(?:{PrincipalFields})\b"),
        new($@"Equals\s*\(\s*\w+\.(?:{PrincipalFields})\b"),
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
                // (1) the per-request surface: public only.
                string declLine = src[decl.Index..(decl.Index + decl.Length)];
                if (!Regex.IsMatch(declLine, @"\bpublic\b"))
                    continue;

                int declIndex = decl.Index;
                int declLineNo = LineOf(src, declIndex);

                // (2) a genuine read of an audience-bearing type in the body.
                ExtractBody(src, declIndex, out string body);
                string? referencedType = FirstReferencedType(body);
                if (referencedType is null)
                    continue; // no audience-bearing read in this method

                // (3) a per-request caller: actor param, or the request principal.
                string signature = SignatureSpan(src, declIndex);
                bool perRequest = ActorParam.IsMatch(signature) || RequestPrincipal.IsMatch(body);
                if (!perRequest)
                    continue; // structurally caller-gated / background — not a decision surface

                // (4) decided in-method or marked.
                if (IsDecided(body) || body.Contains("// SEC-READ:"))
                    continue;

                violations.Add($"{rel}:{declLineNo}  reads <{referencedType}> — no decision in-method, no SEC-READ marker");
            }
        }

        violations = violations.Distinct().ToList();
        Assert.True(
            violations.Count == 0,
            "Method(s) that read an audience-bearing type on the per-request surface with no "
            + "decision in-method and no // SEC-READ marker. Decide in-method (CanAsync / "
            + "CanSeeAsync / the *Standing family / the owner-only ordinal / the self-scoped "
            + "query) OR add the // SEC-READ marker (the IS11–14 pattern). A genuine leak is a "
            + "PAUSE (a new IS10x), not a mark.\n  "
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
                int sep = FirstIndexOfAny(rest, "—(|");
                string reason = sep >= 0 ? rest[(sep + 1)..].Trim() : rest.Trim();

                bool ok = hasTaxonomy && reason.Length > 0
                          && !reason.All(char.IsPunctuation)
                          && reason != rest.Trim(); // there IS a reason beyond the taxonomy word

                if (!ok)
                    bad.Add($"{rel}:{line}  '{rest}'");
            }
        }

        Assert.True(count >= 1, "The SEC-READ marker set is empty — the gate is vacuous.");
        Assert.True(bad.Count == 0,
            $"Marker(s) that do not open with a taxonomy word and end in a reason "
            + $"({count} markers scanned):\n  " + string.Join("\n  ", bad.Distinct()));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The first class-(b) type referenced via a <b>genuine read</b> in
    /// <paramref name="body"/> — excluding a strategy delegate
    /// (<c>=&gt; … Load&lt;T&gt;</c>) and a <c>new T(…)</c> write.
    /// </summary>
    private static string? FirstReferencedType(string body)
    {
        foreach (Match m in ReadIdiom.Matches(body))
        {
            string t = m.Groups[1].Value;
            if (!AudienceBearingTypes.Contains(t))
                continue;

            int abs = m.Index;
            string pre = body.Substring(Math.Max(0, abs - 24), Math.Min(24, abs));
            if (pre.Contains("=>"))
                continue; // strategy delegate (a TranslationLane descriptor, not a call)
            if (Regex.IsMatch(pre, @"\bnew\s+\S*\s*$"))
                continue; // new T(...) — a write, not a read

            return t;
        }

        return null;
    }

    private static bool IsDecided(string body)
        => DecisionIdioms.Any(idiom => body.Contains(idiom))
           || DecisionPatterns.Any(pattern => pattern.IsMatch(body));

    /// <summary>The text from a declaration's start to its body's <c>{</c> / <c>=&gt;</c> (the parameter list).</summary>
    private static string SignatureSpan(string src, int declIndex)
    {
        int i = declIndex;
        while (i < src.Length && src[i] != '{' && !(src[i] == '=' && i + 1 < src.Length && src[i + 1] == '>'))
            i++;
        return src[declIndex..i];
    }

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
    /// From a declaration's start, extracts the method body: from the first
    /// <c>{</c> (or <c>=&gt;</c> for an expression body) at or after it,
    /// brace-matched. Writes the body text to <paramref name="body"/>.
    /// </summary>
    private static void ExtractBody(string src, int declIndex, out string body)
    {
        body = string.Empty;
        int i = declIndex;
        while (i < src.Length && src[i] != '{' && !(src[i] == '=' && i + 1 < src.Length && src[i + 1] == '>'))
            i++;
        if (i >= src.Length)
            return;

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
                        return;
                    }
                }
            }

            // unbalanced — take the rest
            body = src[(start + 1)..];
            return;
        }

        // expression body: to the terminating ';'
        int semi = src.IndexOf(';', i);
        if (semi < 0)
            semi = src.Length - 1;
        body = src[(i + 2)..semi];
    }
}
