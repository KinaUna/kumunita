using System.Reflection;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// GU, plan U10 — the <b>guardian-controls Web VM projection pins</b> (ADR 0028
/// §C — the five supervisory actions the four view models render).
/// <para>
/// These four view models are the U07 Web pin (the <c>/me/children</c> list row,
/// the per-child curation view, the pending-invitation row, and the add-a-child
/// form). They are <b>frozen contract</b>: the "drift-guard: no fields beyond the
/// pin" discipline that the other lanes' VM tests apply (the M2
/// <see cref="GroupViewModel"/> "exact N-field projection" precedent,
/// <see cref="GroupsViewModelTests"/>). Each test asserts the VM carries
/// <b>exactly</b> the pinned field set and nothing else — a "we just added a
/// content field to the child row" regression (G·1) is caught here, not in prod.
/// </para>
/// <para>
/// The assertion idiom matches <see cref="GroupsViewModelTests"/>: pin the
/// <b>field names</b> (the public instance property set, sorted), not the types
/// — the name set is the drift-guard, and a name added or removed is the signal.
/// No 5th test exists (the drift-guard: no fields beyond the pin).
/// </para>
/// </summary>
public sealed class GuardianViewModelsTests
{
    [Fact]
    public void ChildAccountItem_Is_Exact_Four_Field_Projection()
    {
        var fields = typeof(ChildAccountItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // M28 (ADR 0151 D6) — the 4-field pin (the U06 re-pin of the 3-field
        // GU pin): ChildId + DisplayName + Blocked + HasTimeLimits — and
        // *nothing else*. No posts, no profile body, no audience-restricted
        // content (G·1). HasTimeLimits (the M28 badge, the Blocked flag
        // precedent) drives the "time-limits set?" badge on the GU Index card.
        Assert.Equal(new[] { "Blocked", "ChildId", "DisplayName", "HasTimeLimits" }, fields.ToArray());
    }

    [Fact]
    public void MembershipEditorModel_Is_Exact_Six_Field_Projection()
    {
        var fields = typeof(MembershipEditorModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // GU pin: ChildId + GroupIds + CommunityIds + PendingInvitations.
        // GA (ADR 0038) U04 added the 5th: GuardianItems (the "other
        // guardians" list — the assigned guardian's display name; G-A·3
        // identical-in-kind pin; G·1 held: ids/names only, never the
        // child's posts, profile body, or any audience-restricted content).
        // GU community-approval lane (ADR 0141) added the 6th:
        // PendingCommunityRequests (the child's pending
        // CommunityMembershipRequest rows — the approve/reject buttons'
        // source data; G·1 held: ids/names only).
        Assert.Equal(
            new[] { "ChildId", "CommunityIds", "GroupIds", "GuardianItems", "PendingCommunityRequests", "PendingInvitations" },
            fields.ToArray());
    }

    [Fact]
    public void PendingInvitationItem_Is_Exact_Three_Field_Projection()
    {
        var fields = typeof(PendingInvitationItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The U07 pin: GroupId + GroupName + InvitedAt — and *nothing else*.
        // The row's Status / resolution stamps never reach the model (G·1).
        Assert.Equal(new[] { "GroupId", "GroupName", "InvitedAt" }, fields.ToArray());
    }

    [Fact]
    public void PendingCommunityRequestItem_Is_Exact_Three_Field_Projection()
    {
        var fields = typeof(PendingCommunityRequestItem)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // GU community-approval lane (ADR 0141) pin: CommunityId +
        // CommunityName + RequestedAt — and *nothing else*. The row's
        // Status / resolution stamps never reach the model (G·1, the
        // PendingInvitationItem precedent).
        Assert.Equal(new[] { "CommunityId", "CommunityName", "RequestedAt" }, fields.ToArray());
    }

    [Fact]
    public void AddChildForm_Is_Form_Model_With_Three_Fields()
    {
        var fields = typeof(AddChildForm)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // ADR 0146: DisplayName + Email — the two form-bound fields — plus the
        // guardian consent checkbox (GuardianConsent — the guardian's confirmation
        // of the child-account terms before creation). **No Password field** (the
        // guardian does not set the child's credential — the child sets their own
        // at the confirmation link, the ADR 0028 "supervision rides the link, not
        // the password" shape). The guardian is *never* a form-bound field (minted
        // from the signed-in principal); all three carry [Required] (see the VM
        // doc-comments).
        Assert.Equal(new[] { "DisplayName", "Email", "GuardianConsent" }, fields.ToArray());
    }
}
