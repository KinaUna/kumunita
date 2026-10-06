using System.Reflection;

namespace Kumunita.Core.Portability;

/// <summary>
/// One D4 per-kind business-key field set (the design doc
/// <c>docs/design/m27-user-scoped-portability-design.md</c> §2.3 table
/// row, copied verbatim by M27 U02) — the closed field names that
/// constitute a specific entity's natural identity <em>by the same
/// author</em> (or the kind's locked unique key, where the design doc
/// pins one: <c>Tag</c>'s <c>Slug</c>, <c>Profile</c>'s
/// <c>SubjectId</c>, <c>Bookmark</c>'s <c>(OwnerId, TargetKind,
/// TargetId)</c>, <c>Group</c>'s <c>Name</c>). <see
/// cref="Fields"/> is the closed tuple, in the §2.3 order; a match
/// requires <em>every</em> field to be equal between the two
/// candidates.
/// </summary>
public sealed record UserBusinessKey(
    string Type,
    IReadOnlyList<string> Fields);

/// <summary>
/// The D4 per-kind business-key matchers (M27 U02) — the pure
/// <c>bool</c> <see cref="Matches"/> duplicate detector the U04
/// <c>ClassifyAsync</c> classifier drives off (a <c>duplicate</c> entity
/// is one whose business key matches an existing entity in the target;
/// the design doc §2.4 rule (b)).
/// <para>
/// **The copy source is the design doc §2.3, verbatim** (U00 locked
/// it; the U00 handoff §(c) restates the same table) — 14 kinds, the
/// closed set. The field names are the exact public property names on
/// the doc types (the <c>Post</c>.<c>Body</c> is compared as the field
/// value itself — the locked key, not a re-invented hash), so the
/// matcher reflects over the POCO set directly.
/// </para>
/// <para>
/// **No session, no write, no service registration:** pure over the
/// POCO set, referenced statically, like the M11
/// <see cref="PortabilityDocTypes"/> registry — the U09–U11 tests
/// (U11 ships the first M27 tests) witness it directly.
/// </para>
/// </summary>
public static class UserBusinessKeys
{
    /// <summary>
    /// The closed per-kind business-key field sets (the design doc §2.3
    /// table, verbatim — 14 kinds).
    /// </summary>
    public static IReadOnlyDictionary<string, UserBusinessKey> ByType { get; } =
        new Dictionary<string, UserBusinessKey>(StringComparer.Ordinal)
        {
            ["Post"]           = new UserBusinessKey("Post",           ["AuthorId", "Created", "Title", "Body"]),
            ["PostReply"]      = new UserBusinessKey("PostReply",      ["PostId", "AuthorId", "Created", "Body"]),
            ["Event"]          = new UserBusinessKey("Event",          ["AuthorId", "Created", "Title"]),
            ["TodoItem"]       = new UserBusinessKey("TodoItem",       ["AuthorId", "Created", "Title"]),
            ["KanbanBoard"]    = new UserBusinessKey("KanbanBoard",    ["AuthorId", "Created", "Title"]),
            ["Page"]           = new UserBusinessKey("Page",           ["AuthorId", "Created", "Title"]),
            ["Tag"]            = new UserBusinessKey("Tag",            ["Slug"]),
            ["Document"]       = new UserBusinessKey("Document",       ["OwnerId", "Created", "Title"]),
            ["Conversation"]   = new UserBusinessKey("Conversation",   ["ParticipantA", "ParticipantB"]),
            ["Message"]        = new UserBusinessKey("Message",        ["ConversationId", "SenderId", "Created"]),
            ["InventoryItem"]  = new UserBusinessKey("InventoryItem",  ["AuthorId", "Created", "Name"]),
            ["Bookmark"]       = new UserBusinessKey("Bookmark",       ["OwnerId", "TargetKind", "TargetId"]),
            ["Profile"]        = new UserBusinessKey("Profile",        ["SubjectId"]),
            ["Group"]          = new UserBusinessKey("Group",          ["Name"]),
        };

    /// <summary>
    /// The pure duplicate matcher (the plan's locked shape,
    /// <c>bool Matches(string type, object a, object b)</c>) —
    /// <c>true</c> iff <paramref name="a"/> and <paramref name="b"/>
    /// are non-null POCOs of the <paramref name="type"/> doc kind and
    /// <em>every</em> field in the kind's locked business-key set is
    /// equal (case-sensitive <see cref="object.Equals(object)"/>, the
    /// locked key — no case-folding, no normalization).
    /// <para>
    /// **The fail-closed default (C-M27·5):** <c>false</c> for any
    /// <paramref name="type"/> outside the closed set, for a null
    /// argument, or for an argument whose runtime type has no public
    /// property of the locked field name (a malformed / out-of-scope
    /// candidate is <em>never</em> a duplicate — the U04 classifier
    /// treats the absence as "no match", and the entity's
    /// clean/conflict classification proceeds on that basis).
    /// </para>
    /// </summary>
    /// <param name="type">The doc type name (the closed §2.3 set).</param>
    /// <param name="a">The candidate entity (the archive's row).</param>
    /// <param name="b">The existing entity in the target (the match
    ///     against which <paramref name="a"/> is compared).</param>
    public static bool Matches(string type, object? a, object? b)
    {
        if (a is null || b is null)
            return false;
        if (!ByType.TryGetValue(type, out var key))
            return false;

        var aType = a.GetType();
        var bType = b.GetType();
        foreach (var field in key.Fields)
        {
            var pa = aType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            var pb = bType.GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (pa is null || pb is null)
                return false;
            if (!Equals(pa.GetValue(a), pb.GetValue(b)))
                return false;
        }
        return true;
    }
}