using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// <see cref="DirectoryService.ListAsync"/>'s result: the residents the viewer may see
/// (their source <see cref="Profile"/> documents, projected 1:1 — never re-read, no field
/// invented). The directory lists *every* non-blocked resident to any signed-in viewer —
/// the platform is invitation-only and limited to residents, so "who is here" is not a
/// gated surface; the <c>CanSeeAsync</c> pass is not run on the list (there is no
/// hidden-count to count). Blocked residents (a GlobalAdmin's suspension) are still
/// excluded, since a suspended account has no public presence. No <see cref="AccessAudit"/>
/// row is written for the list render (an anonymised "directory.list-viewed" row is a
/// separate concern, deferred to a future privacy-abuse lane).
/// </summary>
public sealed record DirectoryList(IReadOnlyList<Profile> Visible);

/// <summary>
/// <see cref="DirectoryService.DetailAsync"/>'s result: <see cref="ShowContactBlock"/> is
/// the single gate evaluated through <c>IAuthorizationService.CanAsync</c> — the
/// <see cref="Profile.ContactVisibility"/> audience (the §2.4 rule, now a *single*
/// decision): a null / absent audience short-circuits to <c>false</c> with no contact
/// decision (no audit row); a non-null audience runs the one <c>CanAsync</c>, one
/// <see cref="AccessAudit"/> row. A <b>self-view</b> (viewer is the profile's owner)
/// unconditionally renders the full contact block — the owner's audience gates *others*,
/// never themselves — with no decision and no audit row.
/// fail-closed empty shape — the target profile does not exist, or the resident is
/// <see cref="Profile.Blocked"/> (a suspended account whose profile has no public
/// presence at all) — in which case no decision ran, no audit row.
/// <para>
/// The profile-level <see cref="Profile.Visibility"/> audience no longer hides the whole
/// profile from the directory (see <see cref="DirectoryService"/> doc comment — the
/// directory shows every non-blocked resident). It remains on the data model + editor for
/// the audience that would gate *detailed* non-contact fields once such fields exist;
/// the contact block (Email/Phone) is the only profile field currently gated by an
/// audience, and that gate is <see cref="Profile.ContactVisibility"/>.
/// </para>
/// </summary>
public sealed record DirectoryDetail(bool ShowContactBlock, Profile? Profile);

/// <summary>
/// <see cref="DirectoryService.PreviewAsAsync"/>'s result (F6 — the read-only "view as"
/// preview): the same single-gate shape as <see cref="DirectoryDetail"/>, applied to the
/// *author's* saved profile as if a chosen resident (<c>asSubjectId</c>) were the viewer.
/// No write path (M2's scope pin: preview is a composition read, never an editor field);
/// the contact-block decision still commits its own <see cref="AccessAudit"/> row (C3 —
/// a preview is an evaluation, not an exemption from the audit lane).
/// </summary>
public sealed record PreviewRow(bool ShowContactBlock, Profile? Profile);

/// <summary>
/// The directory-side composition root (M2, plan U5). A pure caller of the two frozen
/// modules — <see cref="IUserInfoService"/> (candidate set + single-row read) and
/// <see cref="IAuthorizationService"/> (the single decision path, ADR 0006-D) — never
/// reading <c>GroupMembership</c>/<c>DelegationGrant</c> for its own access decisions
/// (the same "feature modules never re-derive access" ADR 0006-D boundary that pins M1's
/// modules).
/// <para>
/// **Product rule (invocation-only, invitation-only platform):** the directory lists
/// every non-blocked resident to every signed-in viewer — "who is here" is not a gated
/// surface. <see cref="Profile.Blocked"/> is the only account-level exclusion (a
/// suspended account has no public presence). <see cref="ListAsync"/> therefore no longer
/// runs <c>CanSeeAsync</c> at all: it is a pure catalog read; no <see cref="AccessAudit"/>
/// row is written for the list render. (If a "directory.list-viewed" privacy-abuse audit
/// row is needed later, it is a separate addition — not part of this rule.)
/// </para>
/// <para>
/// **Contact-block opt-in (M2 §2.4, invariant C-M2·1):** the email/phone block on the
/// <b>detail</b> (<see cref="DetailAsync"/>) and the read-only <b>preview</b>
/// (<see cref="PreviewAsAsync"/>) is gated by <b>one</b> audience decision —
/// <see cref="Profile.ContactVisibility"/> — through the frozen
/// <see cref="IAuthorizationService"/>'s <c>CanAsync</c>. A null audience short-circuits
/// to "no contact block" with no decision and no audit row (the C-M2·1/C3 pin that is
/// preserved from the two-gate M2 design: the second decision is *not evaluated*, not
/// a Deny). <c>CanAsync</c> is the single shared decision path (C6) — the two public
/// read methods behind this rule (<c>DetailAsync</c> and <c>PreviewAsAsync</c>) share
/// the same <see cref="EvaluateContactGateAsync"/> call so they cannot drift.
/// </para>
/// <para>
/// The profile-level <see cref="Profile.Visibility"/> audience remains on the data model
/// and the editor (an author-controlled, opt-in audience), but the directory and detail
/// surfaces no longer use it to hide a whole profile — the "show everyone, filter the
/// contact block" rule above supersedes it at the presentation layer. It takes effect
/// once *detailed* non-contact profile fields exist and the product wires them through
/// a <c>Visibility</c> gate (M2 scope pin: the visibility gate stays frozen in the data
/// model and editor so later fields can adopt it without a migration).
/// </para>
/// </summary>
public sealed class DirectoryService
{
    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;

    public DirectoryService(IUserInfoService userInfo, IAuthorizationService authz)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
    }

    /// <summary>
    /// The directory listing (F1/F8/F11/F15): every non-blocked resident, for any
    /// signed-in viewer. The platform is invitation-only and limited to residents, so
    /// "who is here" is not a gated surface — <see cref="Profile.Visibility"/> no longer
    /// hides a profile from the directory. <see cref="Profile.Blocked"/> remains the only
    /// account-level exclusion (a suspended account has no public presence).
    /// <para>
    /// F8 boundary: a <c>null</c>/empty <paramref name="viewerSubjectId"/> (unauthenticated)
    /// short-circuits to an empty <see cref="DirectoryList"/> — the list is
    /// sign-in-gated at the Web layer ([Authorize] on the DirectoryController); this
    /// method is the Core-side pin that the list is never exposed to a no-principal caller
    /// even if a caller bypasses the Web gate.
    /// </para>
    /// <para>
    /// <see cref="ListAsync"/> does not run <see cref="IAuthorizationService"/> at all:
    /// it is a pure catalog read, no <see cref="AccessAudit"/> row. (A future
    /// anonymised "directory.list-viewed" privacy-abuse lane is a separate addition,
    /// not part of this pin.)
    /// </para>
    /// </summary>
    public async Task<DirectoryList> ListAsync(string viewerSubjectId)
    {
        if (string.IsNullOrEmpty(viewerSubjectId))
            return new DirectoryList(Visible: Array.Empty<Profile>());

        var all = await _userInfo.GetProfilesAsync(verifiedOnly: false).ConfigureAwait(false);
        var visible = all.Where(p => !p.Blocked).ToList();
        return new DirectoryList(Visible: visible);
    }

    /// <summary>
    /// The directory detail (F3/F4): the <see cref="Profile.ContactVisibility"/> decision
    /// for <paramref name="viewerSubjectId"/> → <paramref name="targetSubjectId"/>, the
    /// <b>single</b> audience gate on the detail surface (§2.4, invariant C-M2·1 — now a
    /// one-decision rule): <c>null</c> <see cref="Profile.ContactVisibility"/> short-circuits
    /// to <c>ShowContactBlock = false</c> with no <c>CanAsync</c> call and no
    /// <see cref="AccessAudit"/> row; a non-null audience runs one <c>CanAsync</c> and one
    /// audit row. A <b>self-view</b> (viewer subject equals the profile owner) short-circuits
    /// to <c>ShowContactBlock = true</c> with neither — the owner always sees their own
    /// contact block, whatever their saved audience says.
    /// the only thing the detail renders *always* — <see cref="Profile.Visibility"/> no
    /// longer hides the whole profile (the platform is invitation-only and limited to
    /// residents, so "who is here" is not gated). A missing target profile, or a
    /// <see cref="Profile.Blocked"/> resident (suspended, no public presence), is
    /// fail-closed: <c>Profile = null</c>, no decision, no audit row.
    /// <see cref="PreviewAsAsync"/> shares this exact single-gate shape (F6) via
    /// <see cref="EvaluateContactGateAsync"/> — C6's no-drift property.
    /// </summary>
    public Task<DirectoryDetail> DetailAsync(string viewerSubjectId, string targetSubjectId)
        => EvaluateContactGateAsync(viewerSubjectId, targetSubjectId);

    /// <summary>
    /// The read-only "view-as" preview (F6): evaluates <paramref name="authorSubjectId"/>'s
    /// saved <see cref="Profile"/> through exactly the same single gate as
    /// <see cref="DetailAsync"/>, with <paramref name="asSubjectId"/> standing in as the
    /// viewer. Read-only — no write path, no state change (M2's scope pin: the preview is
    /// a composition read, not an editor field). The contact-block decision still commits
    /// its own <see cref="AccessAudit"/> row (C3) — a preview is an evaluation, not an
    /// exemption.
    /// </summary>
    public async Task<PreviewRow> PreviewAsAsync(string authorSubjectId, string asSubjectId)
    {
        if (string.IsNullOrEmpty(authorSubjectId) || string.IsNullOrEmpty(asSubjectId))
            return new PreviewRow(ShowContactBlock: false, Profile: null);

        var detail = await EvaluateContactGateAsync(
                viewerSubjectId: asSubjectId, profileSubjectId: authorSubjectId)
            .ConfigureAwait(false);
        return new PreviewRow(detail.ShowContactBlock, detail.Profile);
    }

    /// <summary>
    /// The shared <see cref="Profile.ContactVisibility"/> gate evaluation — the one code
    /// path behind <see cref="DetailAsync"/> and <see cref="PreviewAsAsync"/> (C6's
    /// no-drift property applied to this service's two public read methods: they *cannot*
    /// disagree on the gate or the audit-row shape, since they are the same call).
    /// </summary>
    private async Task<DirectoryDetail> EvaluateContactGateAsync(
        string viewerSubjectId, string profileSubjectId)
    {
        var profile = await _userInfo.GetProfileAsync(profileSubjectId).ConfigureAwait(false);

        // Fail-closed shape: no row, or a suspended resident (a <see cref="Profile.Blocked"/>
        // account has no public presence — the profile never surfaces, no decision runs,
        // no audit row).
        if (profile is null || profile.Blocked)
            return new DirectoryDetail(ShowContactBlock: false, Profile: null);

        // Self-view: a resident's own profile is never gated — the contact audience is an
        // author control on what *others* see, not a way to hide one's own data from
        // oneself. Short-circuits like the `null`-audience case: the viewer is the owner,
        // so the decision is trivially allowed — no <c>CanAsync</c> call, no
        // <see cref="AccessAudit"/> row.
        if (!string.IsNullOrEmpty(viewerSubjectId) && viewerSubjectId == profileSubjectId)
            return new DirectoryDetail(ShowContactBlock: true, Profile: profile);

        // §2.4 / C-M2·1 short-circuit: `null` ContactVisibility ⇒ no contact decision, no
        // <see cref="AccessAudit"/> row. Basic info still renders; only the contact block
        // is gated, and a `null` gate is the "not opted in" shape (not a Deny, not an
        // evaluation).
        if (profile.ContactVisibility is null)
            return new DirectoryDetail(ShowContactBlock: false, Profile: profile);

        // One audience decision (C6 shared matching pass; C3 one audit row) on the
        // profile's contact audience.
        var contactDecision = await _authz.CanAsync(
                viewerSubjectId, AccessAction.Read, new ContactVisibilityResource(profile))
            .ConfigureAwait(false);

        return new DirectoryDetail(
            ShowContactBlock: contactDecision.Allowed,
            Profile: profile);
    }

    /// <summary>
    /// Presents one <see cref="Profile"/>'s *contact* audience
    /// (<see cref="Profile.ContactVisibility"/>) — not its profile-level
    /// <see cref="Profile.Visibility"/> — to <c>IAuthorizationService</c>'s
    /// <c>CanAsync</c> as the §2.4 second decision's <see cref="IAuditableResource"/>.
    /// Everything else (Id/Name/OwnerId/ComponentId/TargetKind) is identical to
    /// <see cref="ProfileToAuditableResource"/>s mapping, so the two decisions land on
    /// the same "directory" resource shape (same TargetId/TargetKind) in the
    /// <see cref="AccessAudit"/> lane — the "one resource, two decisions, two audit rows"
    /// reading C-M2·1/C6 lean on.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>private</b>: <see cref="DirectoryService"/> already publishes three
    /// return-type records (<see cref="DirectoryList"/>/<see cref="DirectoryDetail"/>/
    /// <see cref="PreviewRow"/>) alongside its three public methods; a fourth *public* type
    /// here would add surface the §2.2 freeze ("3 public methods, nothing else") doesn't
    /// name. This is an internal implementation detail — the same shape as the public,
    /// U4-shipped <see cref="ProfileToAuditableResource"/>, with one field
    /// (<see cref="IAuditableResource.Audience"/>) swapped.
    /// </remarks>
    private sealed class ContactVisibilityResource(Profile profile) : IAuditableResource
    {
        public string Id => profile.SubjectId;
        public string Name => profile.DisplayName;
        public string? OwnerId => profile.SubjectId;
        public Audience? Audience => profile.ContactVisibility;
        public string? ComponentId => null;
        public string TargetKind => "directory";
    }
}
