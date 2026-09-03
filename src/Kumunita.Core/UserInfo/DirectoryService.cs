using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// <see cref="DirectoryService.ListAsync"/>'s result: the profiles the viewer may see
/// (their source <see cref="Profile"/> documents, projected 1:1 from the
/// <c>CanSeeAsync</c> visible set — never re-read, no field invented) plus how many of
/// the *candidate set* were hidden. Invariant C-M2·2: <see cref="HiddenCount"/> counts
/// only the candidates <c>CanSeeAsync</c> actually evaluated; a profile dropped by the
/// §2.3 candidate filter (e.g. a verified resident, seen by an unverified viewer) is not
/// "hidden" here — it was excluded before any decision ran, and no <see cref="AccessAudit"/>
/// row names it.
/// </summary>
public sealed record DirectoryList(IReadOnlyList<Profile> Visible, int HiddenCount);

/// <summary>
/// <see cref="DirectoryService.DetailAsync"/>'s result: <see cref="IsVisible"/> is the
/// first gate (<see cref="Profile.Visibility"/>, via <c>IAuthorizationService.CanAsync</c>);
/// <see cref="ShowContactBlock"/> is the second gate (<see cref="Profile.ContactVisibility"/>,
/// §2.4, invariant C-M2·1 — evaluated *only* after <see cref="IsVisible"/> is true, so a
/// hidden profile never produces a contact-block audit row: the §9 pin).
/// <see cref="Profile"/> is null only in the fail-closed "target profile does not exist"
/// case (no decision ran, no audit row).
/// </summary>
public sealed record DirectoryDetail(bool IsVisible, bool ShowContactBlock, Profile? Profile);

/// <summary>
/// <see cref="DirectoryService.PreviewAsAsync"/>'s result (F6 — the read-only "view as"
/// preview): the same two-gate shape as <see cref="DirectoryDetail"/>, applied to the
/// *author's* saved profile as if a chosen resident (<c>asSubjectId</c>) were the viewer.
/// No write path (M2's scope pin: preview is a composition read, never an editor field);
/// the two decisions still commit their own <see cref="AccessAudit"/> rows (C3 — a
/// preview is an evaluation, not an exemption from the audit lane).
/// </summary>
public sealed record PreviewRow(bool IsVisible, bool ShowContactBlock, Profile? Profile);

/// <summary>
/// The directory-side composition root (M2, plan U5). A pure caller of the two frozen
/// modules — <see cref="IUserInfoService"/> (candidate set + single-row read) and
/// <see cref="IAuthorizationService"/> (the single decision path, ADR 0006-D) — never
/// reading <c>GroupMembership</c>/<c>DelegationGrant</c> for its own access decisions
/// (the same "feature modules never re-derive access" ADR 0006-D boundary that pins M1's
/// modules). Owns M2's two product rules: the §4.3 candidate filter (invariant C-M2·2 —
/// a *product rule*, applied *before* any <see cref="IAuthorizationService"/> call, never
/// an <see cref="AccessAction"/> subject or an <see cref="AccessAudit"/> row) and the §2.4
/// <c>ContactVisibility</c> gating (invariant C-M2·1 — a *composition* rule over the same
/// two frozen methods: Visibility first, then ContactVisibility, as two separate
/// decisions through the same shared matching pass, C6).
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
    /// The directory listing (F1/F8/F11/F15): the §2.3 candidate filter, then one
    /// <c>IAuthorizationService</c> <c>CanSeeAsync</c> call over the survivor set.
    /// <paramref name="viewerSubjectId"/>/<paramref name="viewerVerified"/> is the
    /// caller-state pair the Web layer already knows (from the principal); this service —
    /// not the Web controller — applies the §2.3 table to it (C-M2·2 names this service
    /// as the filter's owner): <c>null</c>/empty <paramref name="viewerSubjectId"/>
    /// (unauthenticated) short-circuits to an empty <see cref="DirectoryList"/> with no
    /// <c>CanSeeAsync</c> call and no aggregate audit row (F8's boundary row);
    /// <paramref name="viewerVerified"/> true — the <c>verifiedOnly:true</c> profile set;
    /// false — exactly the viewer's own <see cref="Profile"/>, if any ("missing profile
    /// ⇒ empty, fail closed" — §2.3's last row, shared with the no-principal case).
    /// </summary>
    public async Task<DirectoryList> ListAsync(string viewerSubjectId, bool viewerVerified)
    {
        if (string.IsNullOrEmpty(viewerSubjectId))
            return new DirectoryList(Visible: Array.Empty<Profile>(), HiddenCount: 0);

        IReadOnlyList<Profile> candidates;
        if (viewerVerified)
        {
            candidates = await _userInfo.GetProfilesAsync(verifiedOnly: true).ConfigureAwait(false);
        }
        else
        {
            // Unverified-resident self-only (F8): exactly one candidate — the viewer
            // themself — or none (their profile row missing: fail closed, no decision).
            var all = await _userInfo.GetProfilesAsync(verifiedOnly: false).ConfigureAwait(false);
            var self = all.FirstOrDefault(p => p.SubjectId == viewerSubjectId);
            candidates = self is null ? Array.Empty<Profile>() : new[] { self };
        }

        if (candidates.Count == 0)
            return new DirectoryList(Visible: candidates, HiddenCount: 0);

        // C6 — one shared matching pass over the whole candidate set; C3 — one aggregate
        // audit row (VisibleCount/HiddenCount) from that single call. Standalone form
        // (no IDocumentSession overload): this service has no in-flight caller transaction
        // (a plain read, not a command handler's write path), so the standalone method's
        // own commit is the correct C3 lane here.
        var visibleSet = await _authz.CanSeeAsync(
                viewerSubjectId, AccessAction.Read,
                candidates.Select(p => new ProfileToAuditableResource(p)))
            .ConfigureAwait(false);

        // F1 / the "Profile enumeration vs privacy" risk line: return only the source
        // documents whose id the visible set surfaced — never a hidden row's fields.
        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        var visible = candidates
            .Where(p => visibleIds.Contains(p.SubjectId))
            .ToList();

        return new DirectoryList(Visible: visible, HiddenCount: visibleSet.HiddenCount);
    }

    /// <summary>
    /// The directory detail (F3/F4): the <see cref="Profile.Visibility"/> decision for
    /// <paramref name="viewerSubjectId"/> → <paramref name="targetSubjectId"/>, and —
    /// *only if that allowed* (C-M2·1, §2.4) — the <see cref="Profile.ContactVisibility"/>
    /// decision for the same pair. A missing target profile is fail-closed (no decision,
    /// no audit row — the §2.3 "missing profile ⇒ empty" row, extended to the single-row
    /// case). <see cref="PreviewAsAsync"/> shares this exact two-gate shape (F6).
    /// </summary>
    public Task<DirectoryDetail> DetailAsync(string viewerSubjectId, string targetSubjectId)
        => EvaluateTwoGatesAsync(viewerSubjectId, targetSubjectId);

    /// <summary>
    /// The read-only "view-as" preview (F6): evaluates <paramref name="authorSubjectId"/>'s
    /// saved <see cref="Profile"/> through exactly the same two gates as
    /// <see cref="DetailAsync"/>, with <paramref name="asSubjectId"/> standing in as the
    /// viewer. Read-only — no write path, no state change (M2's scope pin: the preview is
    /// a composition read, not an editor field). The two decisions still commit their own
    /// <see cref="AccessAudit"/> rows (C3) — a preview is an evaluation, not an exemption.
    /// </summary>
    public async Task<PreviewRow> PreviewAsAsync(string authorSubjectId, string asSubjectId)
    {
        if (string.IsNullOrEmpty(authorSubjectId) || string.IsNullOrEmpty(asSubjectId))
            return new PreviewRow(IsVisible: false, ShowContactBlock: false, Profile: null);

        var detail = await EvaluateTwoGatesAsync(
                viewerSubjectId: asSubjectId, profileSubjectId: authorSubjectId)
            .ConfigureAwait(false);
        return new PreviewRow(detail.IsVisible, detail.ShowContactBlock, detail.Profile);
    }

    /// <summary>
    /// The shared Visibility → ContactVisibility two-gate evaluation — the one code path
    /// behind <see cref="DetailAsync"/> and <see cref="PreviewAsAsync"/> (C6's no-drift
    /// property applied to this service's own two public read methods: they *cannot*
    /// disagree on the order or the shape of the two decisions, since they are the same
    /// calls in the same order).
    /// </summary>
    private async Task<DirectoryDetail> EvaluateTwoGatesAsync(
        string viewerSubjectId, string profileSubjectId)
    {
        var profile = await _userInfo.GetProfileAsync(profileSubjectId).ConfigureAwait(false);
        if (profile is null)
            return new DirectoryDetail(IsVisible: false, ShowContactBlock: false, Profile: null);

        var visibilityDecision = await _authz.CanAsync(
                viewerSubjectId, AccessAction.Read, new ProfileToAuditableResource(profile))
            .ConfigureAwait(false);

        if (!visibilityDecision.Allowed)
            // C-M2·1 / F4 — a Visibility Deny never reaches the contact decision: no second
            // CanAsync call, no second audit row, no contact field render. This is the §9
            // pin ("contact block never on a hidden profile"), made concrete by this early
            // return.
            return new DirectoryDetail(IsVisible: false, ShowContactBlock: false, Profile: profile);

        if (profile.ContactVisibility is null)
            // §2.4, row 1 — `null` short-circuits: the contact decision is *not evaluated*
            // (no call, no audit row), exactly as the design doc's literal "not evaluated"
            // wording reads.
            return new DirectoryDetail(IsVisible: true, ShowContactBlock: false, Profile: profile);

        // §2.4, rows 2–4 (+ Any/All with non-empty grants): a *separate* second decision,
        // on the same profile, through the same shared matching pass (C6) — deliberately
        // not folded into one merged compound audience, so the two decisions can never
        // drift and each audits its own row (C3).
        var contactDecision = await _authz.CanAsync(
                viewerSubjectId, AccessAction.Read, new ContactVisibilityResource(profile))
            .ConfigureAwait(false);

        return new DirectoryDetail(
            IsVisible: true,
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
