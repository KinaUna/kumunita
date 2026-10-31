using Kumunita.Core.Portability;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// SEC-lane H1 (U4) — the <see cref="ImportOwnershipGuard"/> pure decision
/// tests. Each test exercises one or more of the rule branches pinned in
/// the design doc §2.2 H1 import policy (open veto 1):
/// <list type="bullet">
/// <item>ownership: every <c>OwnershipFields</c> value must equal the actor's subject id;</item>
/// <item><c>Conversation</c>: actor must be <c>ParticipantA</c> or <c>ParticipantB</c>;</item>
/// <item><c>Message</c>: <c>ConversationId</c> must be in the actor's conversation set;</item>
/// <item><c>GroupMembership</c>/<c>GroupInvitation</c>/<c>GroupJoinRequest</c>: always rejected (access-granting rows);</item>
/// <item><c>Group</c>: <c>OwnerId</c> must equal the actor;</item>
/// <item><c>Id</c> collision: <c>existingOwnerOf(row.Id)</c> returning a different owner rejects the row;</item>
/// <item>type outside the closed <see cref="UserScopeInventory"/> set: rejected.</item>
/// </list>
/// </summary>
/// <para>
/// Synthetic ids only (unit-series rule 7): actor is <c>user-a</c>,
/// a hostile actor is <c>user-b</c>; emails <c>a@example.test</c> /
/// <c>b@example.test</c>.
/// </para>
public sealed class ImportOwnershipGuardTests
{
    private const string Actor = "user-a";
    private const string Forged = "user-b";

    private static Func<string, string?> NoExistingOwner => _ => null;

    // — synthetic row fixtures (no binary fixtures, unit-series rule 8) —

    private static object OwnedPost() => new { Id = "post-1", AuthorId = Actor };
    private static object ForgedPost() => new { Id = "post-1", AuthorId = Forged };
    private static object OwnedBookmark() => new { Id = "bm-1", OwnerId = Actor };
    private static object ForgedBookmark() => new { Id = "bm-1", OwnerId = Forged };
    private static object OwnedRsvp() => new { Id = "rsvp-1", UserId = Actor };
    private static object ForgedRsvp() => new { Id = "rsvp-1", UserId = Forged };
    private static object OwnedGroup() => new { Id = "grp-1", OwnerId = Actor };
    private static object ForgedGroup() => new { Id = "grp-1", OwnerId = Forged };
    private static object ActorConversation() => new { Id = "conv-1", ParticipantA = Actor, ParticipantB = "user-c" };
    private static object ForgedConversation() => new { Id = "conv-1", ParticipantA = "user-b", ParticipantB = "user-c" };
    private static object MessageInActorConv() => new { Id = "msg-1", ConversationId = "conv-1", AuthorId = Actor };
    private static object MessageInForeignConv() => new { Id = "msg-1", ConversationId = "conv-9", AuthorId = Actor };
    private static object Membership() => new { Id = "gm-1", UserId = Actor, GroupId = "grp-1" };
    private static object Invitation() => new { Id = "gi-1", UserId = Actor, GroupId = "grp-1" };
    private static object JoinRequest() => new { Id = "gj-1", UserId = Actor, GroupId = "grp-1" };
    private static object OutOfScopeRow() => new { Id = "tok-1", Token = "secret" };

    [Fact]
    public void Allows_RowOwnedByActor()
    {
        var post = ImportOwnershipGuard.Check("Post", OwnedPost(), Actor, NoExistingOwner);
        var bookmark = ImportOwnershipGuard.Check("Bookmark", OwnedBookmark(), Actor, NoExistingOwner);
        var rsvp = ImportOwnershipGuard.Check("EventRsvp", OwnedRsvp(), Actor, NoExistingOwner);
        var group = ImportOwnershipGuard.Check("Group", OwnedGroup(), Actor, NoExistingOwner);
        var conv = ImportOwnershipGuard.Check("Conversation", ActorConversation(), Actor, NoExistingOwner);
        var msg = ImportOwnershipGuard.Check("Message", MessageInActorConv(), Actor, NoExistingOwner,
            new HashSet<string>(StringComparer.Ordinal) { "conv-1" });

        Assert.All(new[] { post, bookmark, rsvp, group, conv, msg }, v =>
        {
            Assert.True(v.Allowed);
            Assert.Null(v.RejectionReason);
        });
    }

    [Fact]
    public void Rejects_ForgedAuthorId()
    {
        var verdict = ImportOwnershipGuard.Check("Post", ForgedPost(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, verdict.RejectionReason);
    }

    [Fact]
    public void Rejects_ForgedOwnerId()
    {
        var verdict = ImportOwnershipGuard.Check("Bookmark", ForgedBookmark(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, verdict.RejectionReason);
    }

    [Fact]
    public void Rejects_ForgedUserId()
    {
        var verdict = ImportOwnershipGuard.Check("EventRsvp", ForgedRsvp(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, verdict.RejectionReason);
    }

    [Fact]
    public void Rejects_IdCollisionWithOtherOwner()
    {
        // The row itself is owned by the actor, but a foreign row already
        // holds the same Id — the import would overwrite their data.
        Func<string, string?> existingOwnerOf = id => id == "post-1" ? Forged : null;

        var verdict = ImportOwnershipGuard.Check("Post", OwnedPost(), Actor, existingOwnerOf);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonIdCollision, verdict.RejectionReason);
    }

    [Fact]
    public void Rejects_AccessGrantingTypes_Membership_Invitation_JoinRequest()
    {
        var membership = ImportOwnershipGuard.Check("GroupMembership", Membership(), Actor, NoExistingOwner);
        var invitation = ImportOwnershipGuard.Check("GroupInvitation", Invitation(), Actor, NoExistingOwner);
        var joinRequest = ImportOwnershipGuard.Check("GroupJoinRequest", JoinRequest(), Actor, NoExistingOwner);

        Assert.All(new[] { membership, invitation, joinRequest }, v =>
        {
            Assert.False(v.Allowed);
            Assert.Equal(ImportOwnershipGuard.ReasonAccessGranting, v.RejectionReason);
        });
    }

    [Fact]
    public void Group_RequiresActorAsOwner()
    {
        // A group the actor is merely a member of (OwnerId is someone else)
        // is rejected — only the OwnerId seat is importable.
        var verdict = ImportOwnershipGuard.Check("Group", ForgedGroup(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonNotOwned, verdict.RejectionReason);
    }

    [Fact]
    public void Message_RequiresActorConversation()
    {
        var verdict = ImportOwnershipGuard.Check(
            "Message", MessageInForeignConv(), Actor, NoExistingOwner,
            new HashSet<string>(StringComparer.Ordinal) { "conv-1" });

        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonConversationNotActor, verdict.RejectionReason);
    }

    [Fact]
    public void Conversation_ActorMustBeParticipant()
    {
        var verdict = ImportOwnershipGuard.Check("Conversation", ForgedConversation(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonConversationNotActor, verdict.RejectionReason);
    }

    [Fact]
    public void Rejects_TypeOutsideInventory()
    {
        var verdict = ImportOwnershipGuard.Check("IdentityToken", OutOfScopeRow(), Actor, NoExistingOwner);
        Assert.False(verdict.Allowed);
        Assert.Equal(ImportOwnershipGuard.ReasonTypeNotInScope, verdict.RejectionReason);
    }
}
