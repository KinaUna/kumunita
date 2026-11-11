using Kumunita.Core.Identity;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/invitations</c> list view's model (IMPROVE-UX A09, ADR
/// 0304) — the GlobalAdmin's control plane over the invitation lane:
/// invite one or more emails, list sent/accepted/expired/revoked, revoke,
/// and (re)send. <see cref="Items"/> is the <see cref
/// "IInvitationService.ListAsync"/> read projection (the
/// <see cref="InvitationListItem"/> list, newest-first, with the
/// virtual-Expired <c>DisplayStatus</c> computed in the Core read). The
/// invite / revoke / resend outcomes are flashed through
/// <c>TempData</c> (the <c>_FlashToast</c> layout strip — the
/// <see cref="ControllerFlashExtensions"/> shape), not carried on the
/// model. <see cref="DraftEmails"/> seeds the invite textarea (the
/// create shape).
/// </summary>
public sealed class InvitationsAdminViewModel
{
    /// <summary>The inviter's invitations (newest-first; empty = the
    /// friendly empty state).</summary>
    public IReadOnlyList<InvitationListItem> Items { get; set; } =
        System.Array.Empty<InvitationListItem>();

    /// <summary>True when there are no invitations (the friendly empty
    /// state — the <c>admin.invitations.empty</c> shape).</summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>The invite textarea's current value (seeded for the
    /// create shape — a re-render does not lose the actor's input).</summary>
    public string DraftEmails { get; set; } = string.Empty;

    /// <summary>
    /// IMPROVE-UX A12 (ADR 0304 D6 c) — the **approval queue**: the
    /// <see cref="EmailInvitationStatus.PendingApproval"/> rows across
    /// all inviters (newest-first, each carrying the inviter's id — the
    /// admin sees *who* invited). The section is **visible** when the
    /// A11 setting is <see cref="ResidentInviteSetting.OnWithApproval"/>
    /// (the resident surface can create <see cref
    /// "EmailInvitationStatus.PendingApproval"/> rows) and **hidden**
    /// when the setting is <see cref="ResidentInviteSetting.Off"/> (no
    /// resident invitations can exist) or <see cref
    /// "ResidentInviteSetting.On"/> (the resident's invitations land
    /// <see cref="EmailInvitationStatus.Pending"/> directly — the
    /// existing A09 list surface is unchanged, the A12 card: "The
    /// <c>On</c> mode has **no** approval queue").
    /// </summary>
    public IReadOnlyList<InvitationListItem> PendingApprovalItems { get; set; } =
        System.Array.Empty<InvitationListItem>();

    /// <summary>IMPROVE-UX A12 — true when the approval queue section
    /// should render (the A11 setting is <see cref
    /// "ResidentInviteSetting.OnWithApproval"/> — the resident surface
    /// can create <see cref
    /// "EmailInvitationStatus.PendingApproval"/> rows). False when the
    /// setting is <see cref="ResidentInviteSetting.Off"/> (no resident
    /// invitations can exist) or <see cref
    /// "ResidentInviteSetting.On"/> (the resident's invitations land
    /// <see cref="EmailInvitationStatus.Pending"/> directly).</summary>
    public bool ApprovalQueueVisible { get; init; } = false;
}
