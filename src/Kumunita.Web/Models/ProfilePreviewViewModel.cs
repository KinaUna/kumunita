namespace Kumunita.Web.Models;

/// <summary>
/// M2, plan U11 — the read-only "view as" preview for the profile editor (F6).
/// <see cref="ProfileController.Preview"/> evaluates <b>the signed-in author's</b> saved
/// profile as if <see cref="AsDisplayName"/> (or the author themselves) were the viewer,
/// through the frozen <c>DirectoryService.PreviewAsAsync</c> (U5) — a *composition read*,
/// never a write path (the M2 scope pin: "the preview is a composition read, not an editor
/// field").
/// <para>
/// Privacy pin (parallel to U8's <see cref="DirectoryViewModel.Detail"/>): <see
/// cref="Email"/>/<see cref="Phone"/> are a *subset* of <see
/// cref="Kumunita.Core.UserInfo.Profile"/> and are surfaced <b>only</b> when
/// <see cref="ShowContactBlock"/> is true — otherwise they are null, so the view has no
/// channel to render a contact method the author's two-gate evaluation did not allow
/// (C-M2·1 / §2.4: the contact decision is *never* evaluated on a profile
/// <see cref="Kumunita.Core.UserInfo.Profile.Visibility"/> denied).
/// </para>
/// <para>
/// The row carries <b>no</b> <c>PredictedAudience</c>/<c>PredictedGrants</c> and no raw
/// subject id — the §2.x decision the author cares about ("who could see my contact block?")
/// is <b>never</b> computed: the §2.4/§9 pin is that contact visibility is only *evaluated*,
/// not *predicted*, and a "who-else-could-see-my-contacts" oracle is a Web-layer peek surface
/// (F12's "a resident cannot use the profile editor to peek contact visibility" line) the
/// preview must not open.
/// </para>
/// </summary>
public sealed record ProfilePreviewViewModel(
    string AsDisplayName,
    bool IsVisible,
    bool ShowContactBlock,
    string? Email,
    string? Phone);

/// <summary>
/// One selectable "view as" resident for the profile editor's preview selector (F6).
/// The selectable set is exactly: the author themselves (the "how I appear" self-view)
/// plus the distinct <b>User-kind</b> grant targets of the author's saved
/// <see cref="Kumunita.Core.UserInfo.Profile.Visibility"/> /
/// <see cref="Kumunita.Core.UserInfo.Profile.ContactVisibility"/> audiences ("residents
/// the author can resolve" — M2's assumption line). <c>Group</c> grants are <b>excluded
/// from the selector</b>: a group grant targets the group's *members* (an open set),
/// not a single stand-in subject, and standing in for "a member of that group" is the
/// Directory/detail surface's job (U7/U8), not this editor's.
/// <para>
/// A shape twin of U7's <see cref="VisibleProfile"/>: <see cref="SubjectId"/> is
/// <c>string</c> (the frozen <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>
/// opaque-subject shape — not a <see cref="Guid"/>), <see cref="DisplayName"/> is a
/// display fallback to the raw id when the target has no profile row (fail-safe), and
/// <see cref="Verified"/> is the row's presentation badge. No contact/audience fields —
/// the same hidden-channel pin as U7/U8: the selector row never reaches Profile's own
/// email/phone/visibility fields.
/// </para>
/// </summary>
public sealed record ViewerProfile(string SubjectId, string DisplayName, bool Verified);
