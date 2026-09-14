using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// One <b>child account row</b> on the <c>/me/children</c> list (GU, ADR 0028).
/// <para>
/// **Exact 3-field projection (U07 pin)** — <see cref="ChildId"/>,
/// <see cref="DisplayName"/>, <see cref="Blocked"/>. <see cref="ChildId"/> is the
/// route's <c>{childId}</c> (the supervised child's
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>);
/// <see cref="DisplayName"/> is resolved from the child's
/// <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> (the raw id as
/// fail-safe); <see cref="Blocked"/> drives the suspend/un-suspend button state —
/// the <see cref="Kumunita.Core.UserInfo.Profile.Blocked"/> flag the existing
/// <c>BlockedAccountMiddleware</c> + directory already read (enforcement parity;
/// U04's suspension lane sets the same flag). No other field: the child's posts,
/// profile body, or any audience-restricted content never reach this row (G·1).
/// </para>
/// </summary>
public sealed record ChildAccountItem(string ChildId, string DisplayName, bool Blocked);

/// <summary>
/// One <b>pending group invitation row</b> on the child's <c>Detail</c> curation
/// view (GU, ADR 0028). <see cref="GroupId"/> is the route's <c>{groupId}</c> the
/// approve POST posts to; <see cref="GroupName"/> is the display label (resolved
/// through the single-group read — a curation fact, not content); <see
/// cref="InvitedAt"/> is the row's
/// <see cref="Kumunita.Core.UserInfo.GroupInvitation.InvitedAt"/> (ISO-8601). The
/// row's <c>Status</c> / resolution stamps never reach the model — the list shows
/// only <em>pending</em> rows, and "who resolved it" is an
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> lane fact, not a UI fact
/// (G·1).
/// </summary>
public sealed record PendingInvitationItem(string GroupId, string GroupName, string InvitedAt);

/// <summary>
/// The <b>per-child curation</b> view model for <c>/me/children/{childId}</c> (GU,
/// ADR 0028). The three curation sets — <see cref="GroupIds"/> (the child's group
/// membership ids), <see cref="CommunityIds"/> (the child's community membership
/// ids), and <see cref="PendingInvitations"/> (the child's pending group
/// invitations) — are **ids/names only** (G·1): a curation read, never the child's
/// posts, profile body, or any audience-restricted content. <see cref="ChildId"/>
/// is the route's <c>{childId}</c>.
/// </summary>
public sealed record MembershipEditorModel(
    string ChildId,
    IReadOnlyList<string> GroupIds,
    IReadOnlyList<string> CommunityIds,
    IReadOnlyList<PendingInvitationItem> PendingInvitations);

/// <summary>
/// The <b>add-a-child</b> form model (GU, ADR 0028) bound via <c>[FromForm]</c> on
/// <c>GuardianController.AddChild</c>. The <b>guardian</b> is never form-bound — it
/// is minted by the Web layer from <c>KumunitaPrincipal.SubjectId(User)</c> (the
/// single identity source) and passed as the <c>guardianId</c> argument to the Core
/// seam <see cref="Kumunita.Core.UserInfo.IUserInfoService.CreateGuardianLinkAsync"/>
/// (one commit, G·4). <see cref="DisplayName"/> / <see cref="Email"/> /
/// <see cref="Password"/> feed the usual <c>RegisterAsync</c> signup lane — the
/// verification email is M1's, the form does not bypass it.
/// </summary>
public sealed class AddChildForm
{
    [Required, MaxLength(100)]
    [Display(Name = "Display name")]
    public string? DisplayName { get; set; }

    [Required, EmailAddress, MaxLength(255)]
    [Display(Name = "Email address")]
    public string? Email { get; set; }

    [Required, DataType(DataType.Password), MinLength(8)]
    [Display(Name = "Password")]
    public string? Password { get; set; }
}
