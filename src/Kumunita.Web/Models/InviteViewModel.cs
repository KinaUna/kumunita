namespace Kumunita.Web.Models;

/// <summary>
/// The resident "Invite a neighbour" surface's model (IMPROVE-UX A12,
/// ADR 0304 D6 c) — the A11 rule's posture (the
/// <see cref="Kumunita.Core.Identity.ResidentInvitePosture"/> read by
/// <see cref="Kumunita.Core.Identity.IInvitationService
/// .GetResidentInvitePostureAsync"/>) surfaced for the view. The
/// <see cref="MayInvite"/> flag is the **gate**: when
/// <c>false</c> (the <see cref
/// "Kumunita.Core.Identity.ResidentInviteSetting.Off"/> floor) the
/// account-menu entry is hidden and the surface's seam refuses (the
/// A11 rule's <see cref
/// "Kumunita.Core.Identity.ResidentInvitePosture.Refused"/> posture).
/// The <see cref="RequiresApproval"/> flag is the **ledger**: when
/// <c>true</c> (the <see cref
/// "Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval"/> mode)
/// the resident's created invitation lands
/// <see cref="Kumunita.Core.Identity.EmailInvitationStatus
/// .PendingApproval"/> (gated behind the admin approval queue) instead
/// of <see cref
/// "Kumunita.Core.Identity.EmailInvitationStatus.Pending"/> (immediately
/// usable — the <see cref
/// "Kumunita.Core.Identity.ResidentInviteSetting.On"/> mode). The
/// <see cref="Kumunita.Core.Identity.IInvitationService
/// .CreateAsync"/> / <see cref="Kumunita.Core.Identity.IInvitationService
/// .InviteAsync"/> seams own the ADR 0304 D4 invariants (single-use,
/// expiring, revocable) — the mode controls *who may create*, not *what
/// a row can do*.
/// </summary>
public sealed class InviteViewModel
{
    /// <summary>The A11 rule's gate: <c>true</c> when the resident may
    /// create an invitation (the <see cref
    /// "Kumunita.Core.Identity.ResidentInviteSetting.On"/> /
    /// <see cref
    /// "Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval"/>
    /// modes), <c>false</c> when the surface is hidden (the
    /// <see cref="Kumunita.Core.Identity.ResidentInviteSetting.Off"/>
    /// floor). The view renders the form only when <c>true</c> (the
    /// <c>Off</c> surface is absent, not a 403 — the account-menu entry
    /// is the primary visibility gate, this flag is the belt-and-suspenders
    /// on the surface itself).</summary>
    public bool MayInvite { get; init; } = false;

    /// <summary>The A11 rule's ledger: <c>true</c> when the resident's
    /// created invitation is gated behind the admin approval queue (the
    /// <see cref
    /// "Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval"/>
    /// mode — the row lands
    /// <see cref="Kumunita.Core.Identity.EmailInvitationStatus
    /// .PendingApproval"/>), <c>false</c> when the invitation is
    /// immediately usable (the <see cref
    /// "Kumunita.Core.Identity.ResidentInviteSetting.On"/> mode — the
    /// row lands <see cref
    /// "Kumunita.Core.Identity.EmailInvitationStatus.Pending"/>). The
    /// view renders the "pending approval" notice when <c>true</c> (the
    /// ADR 0304 D6 c shape — the resident knows their invitation is not
    /// yet usable, the admin approval queue is the A12 admin lane).</summary>
    public bool RequiresApproval { get; init; } = false;

    /// <summary>The neighbour's email address (the form's
    /// <c>email</c> field — the ADR 0304 D1 "lowercased, trimmed at
    /// creation" shape; the <see cref
    /// "Kumunita.Core.Identity.IInvitationService.InviteAsync"/> seam
    /// lowercases + trims before storage). The
    /// <see cref
    /// "Kumunita.Web.Controllers.AdminInvitationsController
    /// .IsPlausibleEmail"/> shape check (a <c>local@domain</c> with a
    /// dot in the domain) is the A09 card's "invalid emails are
    /// reported, not silently dropped" pin.</summary>
    public string Email { get; set; } = string.Empty;
}
