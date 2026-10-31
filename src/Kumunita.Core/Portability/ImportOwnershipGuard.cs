using System.Reflection;

namespace Kumunita.Core.Portability;

/// <summary>
/// The SEC-lane <b>import ownership guard</b> (H1 — the static audit's
/// "resident import stores archive rows with no ownership or
/// <c>Id</c>-collision check"). One pure, reflection-at-field-name-level
/// decision: may this archive row be imported <em>for this actor</em>?
/// <para>
/// The rule set is the design doc
/// <c>docs/design/sec-remediation-design.md</c> §2.2 H1 import policy
/// (open veto 1, now authoritative):
/// <list type="bullet">
/// <item><c>GroupMembership</c> / <c>GroupInvitation</c> /
///     <c>GroupJoinRequest</c> → always <see cref="ImportRowVerdict"/>
///     rejected with <c>ReasonAccessGranting</c> — these rows grant
///     access, so importing them is the attack, not the data.</item>
/// <item><c>Group</c> → allowed only when the row's <c>OwnerId</c>
///     equals the actor (owner import; the member seat is not importable)
///     and the <c>Id</c> is not already owned by another row.</item>
/// <item><c>Conversation</c> → allowed only when the actor holds
///     <c>ParticipantA</c> or <c>ParticipantB</c>.</item>
/// <item><c>Message</c> → allowed only when its <c>ConversationId</c>
///     is in the caller-supplied <c>actorConversationIds</c> (the
///     conversations the actor is already a participant of).</item>
/// <item>Every other <see cref="UserScopeInventory.Entries"/> type: each
///     <c>OwnershipFields</c> value must equal the actor's subject
///     id (a translation row's nullable <c>AuthorId</c> — null means the
///     row is an operator-managed translation, not the actor's
///     user-added one — is therefore <em>not</em> owned and is
///     rejected), and the <c>Id</c> must not be owned by another row
///     (<c>existingOwnerOf</c> returning a non-null owner ≠ actor →
///     <c>ReasonIdCollision</c>).</item>
/// <item>Any type outside the closed <see cref="UserScopeInventory"/>
///     → <c>ReasonTypeNotInScope</c> (the closed-set fail-closed
///     default, C-M27·5 at the guard level).</item>
/// </list>
/// </para>
/// <para>
/// The reason strings (<c>Reason*</c> constants) become <b>user-visible
/// codes</b>: U5 surfaces them as the per-entity
/// <c>RejectedReason</c> on the import plan and U6 records them as
/// <c>import.rejected:&lt;reason&gt;</c> in the apply result's message
/// list — so they are pinned here, in this file, and U5/U6 record them
/// in the handoff note rather than re-deriving them.
/// </para>
/// <para>
/// **Pure** — no service, no session, no document: the caller (U5's
/// classify loop, U6's resolve loop) supplies the
/// <c>existingOwnerOf</c> probe and the actor's conversation id set.
/// No PII: the guard sees subject ids and row field values only.
/// </para>
/// </summary>
public static class ImportOwnershipGuard
{
    /// <summary>Row type grants access (membership / invitation / join-request) — never importable.</summary>
    public const string ReasonAccessGranting = "access-granting row";

    /// <summary>The row's <c>Id</c> is already owned by another row (an overwrite, not an import).</summary>
    public const string ReasonIdCollision = "id collision";

    /// <summary>An <c>OwnershipFields</c> value does not equal the actor's subject id.</summary>
    public const string ReasonNotOwned = "not-owned";

    /// <summary>A <c>Message</c>'s <c>ConversationId</c> is not a conversation the actor participates in (or <c>Conversation</c> the actor is not a participant of).</summary>
    public const string ReasonConversationNotActor = "conversation-not-actor";

    /// <summary>The doc type is not in the closed <see cref="UserScopeInventory.Entries"/> set.</summary>
    public const string ReasonTypeNotInScope = "type-not-in-scope";

    private const string GroupType = "Group";
    private const string ConversationType = "Conversation";
    private const string MessageType = "Message";

    private static readonly HashSet<string> AccessGrantingTypes =
        new(StringComparer.Ordinal) { "GroupMembership", "GroupInvitation", "GroupJoinRequest" };

    /// <summary>
    /// May this archive row be imported for <paramref name="actorSubjectId"/>?
    /// <param name="type">The doc type name (a <see cref="UserScopeInventory.Entries"/> key, or any other name — the guard is closed-set).</param>
    /// <param name="row">The deserialized archive row. Ownership-field values and the <c>Id</c> are read by property name (the <see cref="UserScopeEntry.OwnershipFields"/> names, verbatim — the same names the M11 reference map and the documents use).</param>
    /// <param name="actorSubjectId">The importing resident's subject id (the actor; the row is imported into <em>their</em> scope).</param>
    /// <param name="existingOwnerOf">Probe: given a row <c>Id</c>, return the owner subject id of the row already stored with that <c>Id</c> (or null when no such row exists). A non-null owner ≠ the actor means the import would <em>overwrite</em> another resident's row.</param>
    /// <param name="actorConversationIds">The <c>Conversation</c> ids the actor already participates in (the caller has verified this — e.g. from the actor's own <c>Conversation</c> rows). Required for <c>Message</c> rows; <c>null</c>/empty means no conversation is accepted, so every <c>Message</c> is rejected (fail-closed).</param>
    /// </summary>
    public static ImportRowVerdict Check(
        string type,
        object row,
        string actorSubjectId,
        Func<string, string?> existingOwnerOf,
        IReadOnlySet<string>? actorConversationIds = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(row);
        if (string.IsNullOrWhiteSpace(actorSubjectId))
            throw new ArgumentException("actorSubjectId must be the actor's subject id.", nameof(actorSubjectId));
        ArgumentNullException.ThrowIfNull(existingOwnerOf);

        if (!UserScopeInventory.Entries.TryGetValue(type, out UserScopeEntry? entry))
            return Rejected(ReasonTypeNotInScope);

        if (AccessGrantingTypes.Contains(type))
            return Rejected(ReasonAccessGranting);

        if (type is not (GroupType or ConversationType or MessageType))
        {
            foreach (string field in entry.OwnershipFields)
            {
                string? value = Str(row, field);
                if (value is null || !string.Equals(value, actorSubjectId, StringComparison.Ordinal))
                    return Rejected(ReasonNotOwned);
            }
        }
        else if (type == GroupType)
        {
            // veto 1: Group importable only when OwnerId == the actor (the
            // member seat is not importable — the actor's own membership is
            // their own, and the group's OwnerId row is the one that must
            // belong to them).
            if (!string.Equals(Str(row, "OwnerId"), actorSubjectId, StringComparison.Ordinal))
                return Rejected(ReasonNotOwned);
        }
        else if (type == ConversationType)
        {
            bool a = string.Equals(Str(row, "ParticipantA"), actorSubjectId, StringComparison.Ordinal);
            bool b = string.Equals(Str(row, "ParticipantB"), actorSubjectId, StringComparison.Ordinal);
            if (!a && !b)
                return Rejected(ReasonConversationNotActor);
        }
        else // Message
        {
            string? convId = Str(row, "ConversationId");
            if (convId is null
                || actorConversationIds is null
                || !actorConversationIds.Contains(convId))
                return Rejected(ReasonConversationNotActor);
        }

        // Id-collision check: a row whose Id is already owned by someone
        // else is an overwrite of foreign data, not an import.
        if (IdOf(row) is { } id && existingOwnerOf(id) is { } existingOwner
            && !string.Equals(existingOwner, actorSubjectId, StringComparison.Ordinal))
        {
            return Rejected(ReasonIdCollision);
        }

        return new ImportRowVerdict(true, null);
    }

    private static ImportRowVerdict Rejected(string reason) =>
        new ImportRowVerdict(false, reason);

    /// <summary>Read a row's string property by name (the ownership-field / <c>Id</c> / <c>ConversationId</c> names). Returns null when the property is absent or its value is null.</summary>
    private static string? Str(object row, string name)
    {
        object? value = row.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(row);
        return value?.ToString();
    }

    /// <summary>The row's <c>Id</c> as a string (or null when absent).</summary>
    private static string? IdOf(object row) => Str(row, "Id");
}

/// <summary>
/// The <see cref="ImportOwnershipGuard"/> verdict for one archive row.
/// <see cref="Allowed"/> is true only when every rule passes;
/// <see cref="RejectionReason"/> carries the user-visible reason code
/// (an <c>ImportOwnershipGuard.Reason*</c> constant) when rejected.
/// </summary>
public sealed record ImportRowVerdict(bool Allowed, string? RejectionReason);
