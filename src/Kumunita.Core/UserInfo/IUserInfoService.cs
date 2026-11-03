using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// The UserInfoModule's access-core surface (ADR 0006 §A frozen + ADR 0286 community lanes).
/// The 71 non-frozen methods split into four siblings (C07): <see cref="IGroupService"/>,
/// <see cref="ICommunityMembershipService"/>, <see cref="IGuardianService"/>,
/// <see cref="IProfileSettingsService"/>.
/// <para>
/// The M1 design's "part affected: <c>Kumunita.Core</c> new <c>UserInfo/</c> module"
/// — owns <see cref="Profile"/> (including the <c>visibility: Audience</c> storage M2
/// consumes and M1 stores), <see cref="Group"/>/member management, and
/// <see cref="DelegationGrant"/> (grant/revoke/documents). Strong consistency
/// (invariant C4): <see cref="GetGroupIdsAsync"/>'s read touches the live
/// membership rows directly (no projection lag), loaded once per request (D4).
/// </para>
/// </summary>
public interface IUserInfoService
{
    Task<Profile?> GetProfileAsync(string subjectId);

    /// <summary>
    /// Strong-consistency membership resolution (invariant C4): returns the set of
    /// <c>groupId</c> values the account is in *at this instant*, from the live
    /// membership rows — "a group whose membership changed after a post was granted"
    /// never leaks or hides access via a lagging projection. Called **once** per
    /// authorization request (D4).
    /// </summary>
    Task<HashSet<string>> GetGroupIdsAsync(string userId);

    /// <summary>
    /// The delegate's active grant, if any (the effective principle + scope the
    /// decision applies against — invariant C2). Null when the delegate has no
    /// active grant (they act as themselves).
    /// </summary>
    Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);

    /// <summary>
    /// Create a group (owner = the creator; membership starts as owner-only).
    /// <paramref name="isPrivate"/> (ADR 0010) sets <see cref="Group.IsPrivate"/>
    /// at creation — <c>false</c> (the default: a group is public unless the
    /// caller opts in) is a public group; <c>true</c> makes it private (hidden
    /// from the audience / grant pickers, still a working member group). The Web
    /// create form is the sole caller that passes <c>true</c>; every other caller
    /// (M1's seeders, tests, cross-context reads) gets the public default, so the
    /// new parameter is a compatible addition to the frozen surface
    /// (ADR 0006-A), not a break.
    /// </summary>
    Task<Group> CreateGroupAsync(string ownerId, string name, string? description, bool isPrivate = false);

    /// <summary>Add a user to a group (strong-consistency: the new membership is
    /// live on the next <see cref="GetGroupIdsAsync"/> call).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>; otherwise the Owner/Admin base applies.</summary>
    Task AddGroupMemberAsync(string groupId, string userId, string addedBy);

    /// <summary>Remove a user from a group (strong-consistency: the loss of access is
    /// live on the next <see cref="GetGroupIdsAsync"/> call — invariant C4).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>; otherwise the Owner/Admin base applies.</summary>
    Task RemoveGroupMemberAsync(string groupId, string userId, string removedBy);

    /// <summary>
    /// Grant a scoped delegation (invariant C2):
    /// <paramref name="delegateId"/> is <paramref name="ownerId"/> *only for* the actions
    /// named in <paramref name="scope"/>. <paramref name="from"/> is the effective
    /// start; <paramref name="to"/> (null = open-ended) the expiry. Appends an audit
    /// row (<c>via: Admin</c> or <c>via: Owner</c> — whoever grants, the
    /// <see cref="AccessVia"/> is the grantor's standing recorded at decision time).
    /// </summary>
    Task<DelegationGrant> GrantDelegationAsync(string ownerId, string delegateId,
        IReadOnlyList<string> scope, DateTimeOffset from, DateTimeOffset? to);

    /// <summary>Revoke a granted delegation (<see cref="DelegationGrant.RevokedBy"/> is
    /// the revoker; the grant is closed). Appends an audit row (<c>via: Admin</c>
    /// or <c>via: Owner</c>).</summary>
    Task RevokeDelegationAsync(string grantId, string revokedBy);

    // ── Media additions (ADR 0011; C-MED·8 — the *single* avatar write lane,
    // named; the ADR 0006-E compatible-addition idiom this file uses) ───────

    // ── Timezone addition (ADR 0019; the *single* user-override write lane,
    // named; the ADR 0006-E compatible-addition idiom this file uses) ────────

    // ── Page-size addition (the *single* user-override write lane for the
    // resident's items-per-page preference; the exact shape of
    // SetProfileTimezoneAsync, the ADR 0006-E compatible-addition idiom) ──

    // ── Date format addition (ADR 0020; the *single* user-override write
    // lane for the resident's personal date-time format — the exact shape of
    // SetProfileTimezoneAsync, the ADR 0006-E compatible-addition idiom) ──

    // ── Email & notification language addition (ADR 0061; the *single*
    // write lane for the resident's outbound-channel language — the exact
    // shape of SetProfileDateFormatAsync, the ADR 0006-E compatible-addition
    // idiom) ────────────────────────────────────────────────────────────────

    // ── Onboarding addition (ADR 0132; the *single* completion-stamp write
    // lane + its owner-scope read — the ADR 0006-E compatible-addition idiom
    // this file uses, the exact shape of SetProfileTimezoneAsync) ──────────

    // ── M9 amendment additions (per-resident + guardian messaging controls;
    // the ADR 0006-E compatible-addition idiom this file uses, mirroring the
    // Onboarding lanes above and the GU guardian lanes below) ──────────────

    // ── M3 additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    // ── M2 additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    // ── M1 lifecycle additions (ADR 0006-E compatible lane — added to the owning
    // module's public surface, named) ──────────────────────────────────────

    // ── Admin community management (add / edit / enable-disable, GlobalAdmin
    // surface — the <c>/admin</c> shell. ADR 0006-D: the Core is the single write
    // lane for the <see cref="Component"/> rows; the Web controller is a thin
    // wrapper over these three, never re-deriving identity/authz or opening
    // its own session.) ───────────────────────────────────────────────────

    // ── Community membership (the **posting right** — separate from
    // ModeratorAssignment, which is a moderator's *governing scope*.
    // ADR 0006-E compatible lane: appended to the owning module's public
    // surface; not a break on the ADR 0006-A frozen surface) ────────

    /// <summary>
    /// Strong-consistency membership resolution for the posting gate
    /// (invariant C4): returns the set of <c>componentId</c> values the
    /// account is a member of *at this instant* — the live
    /// <see cref="ComponentMembership"/> rows **∪** the enabled, mandatory
    /// communities (ADR 0012: <see cref="Component.Mandatory"/> membership is
    /// implicit — every verified resident is a member, and a disabled
    /// component grants none even when mandatory) — with no projection lag,
    /// no cache. This is the single "who is a member" definition: the
    /// posting gate (<see cref="Posts.PostService"/>), the composer's
    /// community picker, and the feed's directory all read through it, so a
    /// mandatory toggle is live on the very next read.
    /// Mirrors <see cref="GetGroupIdsAsync"/> in shape and audit
    /// behavior: the **write** lanes
    /// (<see cref="SetCommunityMembershipAsync"/> /
    /// <see cref="ClearCommunityMembershipAsync"/>) are the audited admin
    /// actions; this **read** lane is a candidate read and appends no
    /// <see cref="Authorization.AccessAudit"/> row itself (the caller —
    /// <see cref="Posts.PostService"/> / the Web surface — consults it and is
    /// the gate point).
    /// </summary>
    Task<IReadOnlyCollection<string>> GetCommunityIdsAsync(string userId);

    /// <summary>
    /// The actor's <b>effective</b> community set: the raw
    /// <see cref="GetCommunityIdsAsync"/> membership <b>minus</b> the
    /// actor's <see cref="Profile.BlockedCommunityIds"/> (a guardian's
    /// per-community block-and-hide ceiling over a supervised child, the
    /// <see cref="Profile.MessagingRestricted"/> precedent carried to
    /// communities). This is the seam the <b>child's own access surfaces</b>
    /// read through — the community directory/sidebar, the community feed
    /// gate, the composer's posting gate, and the
    /// <see cref="Authorization.AuthorizationService"/> community branch of
    /// <c>Decide</c> (audience visibility of community-scoped posts). It is
    /// why a <b>mandatory</b> community can be hidden for a child: mandatory
    /// membership is implicit and the removal lanes refuse / skip it
    /// (ADR 0012), so "removing the child" is impossible there, but
    /// <em>blocking</em> it is — the id simply drops out of this set.
    /// <para>
    /// Strong consistency (invariant C4): a
    /// <see cref="SetChildCommunityBlockAsync"/> write is live on the very
    /// next call. Like <see cref="GetCommunityIdsAsync"/>, this is a
    /// candidate read — no <see cref="Authorization.AccessAudit"/> row is
    /// appended here (the access decision + its audit live in the caller).
    /// The <b>raw</b> <see cref="GetCommunityIdsAsync"/> stays available to
    /// the surfaces that reason about the child's *actual* memberships
    /// regardless of a guardian's restriction — the guardian's own curation
    /// view (so the guardian sees the child's mandatory memberships and can
    /// toggle the block on each) and the /admin per-account community diff.
    /// </para>
    /// </summary>
    Task<IReadOnlyCollection<string>> GetEffectiveCommunityIdsAsync(string userId);

    /// <summary>
    /// Add a membership row (or refresh an existing one) so that
    /// <paramref name="userId"/> may post to <paramref name="componentId"/>.
    /// Idempotent on the <c>(componentId, userId)</c> pair: an existing row is
    /// re-stamped with <paramref name="actorId"/> and the new timestamp rather
    /// than duplicated (the business key is unique — the index in
    /// <see cref="CoreDocTypes"/> enforces it). Appends an
    /// <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add-member", targetKind "component", via Admin, outcome
    /// Allow) in the same transaction as the row (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="userId"/> is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityMembershipAsync(string componentId, string userId, string actorId);

    /// <summary>
    /// Remove a membership row (the <paramref name="userId"/> loses posting
    /// rights on <paramref name="componentId"/>). Strong consistency
    /// (invariant C4): the gate re-evaluates on the very next
    /// <see cref="GetCommunityIdsAsync"/> or
    /// <see cref="Posts.PostService.CreatePostAsync"/> call. A **no-op** when
    /// the pair has no row (does not throw) — admins editing a "set" of
    /// components per user are free to uncheck rows that were never there.
    /// <b>ADR 0012: a <see cref="Component.Mandatory"/> community is a
    /// no-op skip</b> (no row deleted, no audit appended — nothing changed):
    /// membership there is implicit (<see cref="GetCommunityIdsAsync"/> still
    /// returns it), and the <c>/admin</c> set form plus the self-leave route
    /// must be able to call this lane on a mandatory pair without a hard
    /// failure. The *moderator* removal lane
    /// (<see cref="RemoveCommunityMemberAsync"/>) refuses the same pair with
    /// an <see cref="InvalidOperationException"/> — the Web lane that wants
    /// the surfaced message.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.remove-member", targetKind "component", via Admin, outcome
    /// Allow) when a row is actually removed.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="userId"/> is null/whitespace.</exception>
    Task ClearCommunityMembershipAsync(string componentId, string userId, string actorId);

    // ── ADR 0012 — mandatory communities + moderator member-management
    // lanes (the community's <see cref="Identity.Roles.Moderator"/> scope ∪
    // <see cref="Identity.Roles.GlobalAdmin"/> standing; ADR 0006-E
    // compatible — appended to the owning module's public surface, additive)

    /// <summary>
    /// Set a community's <see cref="Component.Mandatory"/> flag (ADR 0012):
    /// <c>true</c> makes every verified resident an implicit member (nobody
    /// may be removed from it — <see cref="RemoveCommunityMemberAsync"/>
    /// refuses, <see cref="ClearCommunityMembershipAsync"/> skips — and
    /// nobody may leave it, the self-leave route is Web-gated on the flag);
    /// <c>false</c> restores ordinary optional membership (explicit rows alone
    /// then decide). **Standing gate — GlobalAdmin only** (thin token, decision
    /// in Core): whether a community is mandatory is an *admin* call even
    /// though the component's moderator governs its members (ADR 0012, product
    /// decision) — the actor's <c>actorRoles</c> must carry
    /// <see cref="Identity.Roles.GlobalAdmin"/>; a component-scoped moderator
    /// (and anyone else) gets an <see cref="UnauthorizedAccessException"/>
    /// (fail-closed). Strong consistency (invariant C4): the toggle is live
    /// on the very next <see cref="GetCommunityIdsAsync"/>.
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.set-mandatory" when switching on, "community.set-optional"
    /// when off; targetKind "component", via <b>Admin</b> — the sole admitted
    /// standing — outcome Allow) in the same transaction as the flag flip
    /// (invariant C3).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/> or
    /// <paramref name="actorId"/> is null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException"><paramref name="actorRoles"/>
    /// lacks <see cref="Identity.Roles.GlobalAdmin"/>.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task SetCommunityMandatoryAsync(string componentId, bool mandatory, string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// Add a resident to a community (the moderator ∪ GlobalAdmin side of
    /// ADR 0012's optional-membership management): the same idempotent
    /// <c>(componentId, userId)</c> upsert row as
    /// <see cref="SetCommunityMembershipAsync"/> — a row on an already
    /// mandatory community is a harmless no-op (the implicit union read
    /// already includes them; kept so the forms round-trip without special
    /// cases). **Standing gate** (thin token, decision in Core): the actor's
    /// <c>actorRoles</c> must carry the <see cref="Identity.Roles
    /// .ModeratorComponent(string)"/> scope claim for <paramref
    /// name="componentId"/> ∪ <see cref="Identity.Roles.GlobalAdmin"/> — a
    /// <see cref="UnauthorizedAccessException"/> otherwise. (Narrower than the
    /// set-mandatory lane, which is GlobalAdmin-only.)
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.add-member", targetKind "component", via <b>Moderator</b>
    /// when the actor holds the component's scope claim else Admin,
    /// outcome Allow) in the same transaction (invariant C3).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>, bypassing the GlobalAdmin/moderator gate; the
    /// existing standing applies otherwise.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/>,
    /// <paramref name="userId"/> or <paramref name="actorId"/> is
    /// null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor's role set
    /// carries neither standing.</exception>
    /// <exception cref="InvalidOperationException">No component with that id exists.</exception>
    Task AddCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// Remove a resident's membership from a community (the moderator ∪
    /// GlobalAdmin side of ADR 0012): deletes the <c>(componentId,
    /// userId)</c> row; a **no-op** when the pair has no row (does not
    /// throw — the set-form shape, consistent with the frozen admin lane).
    /// **Mandatory communities refuse** (ADR 0012's invariant: no one is a
    /// non-member of a mandatory community) — an <see
    /// cref="InvalidOperationException"/> the Web lane surfaces, not a
    /// silent skip (this lane is where the product message lives).
    /// **Standing gate** as <see cref="AddCommunityMemberAsync"/> (component
    /// scope ∪ GlobalAdmin — *not* the GlobalAdmin-only set-mandatory lane);
    /// **no** target-standing gate — a community's own moderator *can* be
    /// removed (unlike the ADR 0008 group owner's own row): moderator
    /// standing outlives membership (they govern and post on the
    /// <see cref="Identity.Roles.ModeratorComponent(string)"/> claim alone,
    /// so no orphan state results; the GlobalAdmin role lane re-adds scope
    /// when the membership matters again).
    /// Appends an <see cref="Authorization.AccessAudit"/> row (action
    /// "community.remove-member", targetKind "component", via <b>Moderator</b>
    /// when the actor holds the component's scope claim else Admin,
    /// outcome Allow) in the same transaction (invariant C3).
    /// GU (ADR 0028): the lane also admits the **guardian standing** — a
    /// guardian curating their child's membership (an active link over
    /// <paramref name="userId"/>) records the narrower standing
    /// <c>Via: Guardian</c>, bypassing the GlobalAdmin/moderator gate; the
    /// existing standing applies otherwise. The ADR 0012 mandatory-community
    /// refusal is **preserved** (it fires regardless of standing).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="componentId"/>,
    /// <paramref name="userId"/> or <paramref name="actorId"/> is
    /// null/whitespace.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor's role set
    /// carries neither standing.</exception>
    /// <exception cref="InvalidOperationException">No component with that id
    /// exists, or the community is <see cref="Component.Mandatory"/>.</exception>
    Task RemoveCommunityMemberAsync(string componentId, string userId, string actorId, IReadOnlySet<string> actorRoles);

    // ── ADR 0026 — group & community name/description translations ─────────
    // The "separate feature" ADR 0021's scope boundary deferred (and ADR 0022
    // shipped for posts/replies). Two documents in this context
    // (GroupTranslation / CommunityTranslation, registered in CoreDocTypes), an
    // add-only write lane each (a hand-written AccessAudit row, C3), a plain
    // read seam each (no decision, no audit — inherits the parent's reach),
    // and a public standing probe each the Web uses to decide whether to show
    // the "add a translation" form. ADR 0006-E compatible — additive to the
    // owning module's public surface.

    // ── M2b additions (ADR 0006-E compatible lane — owner-invited group
    // membership: invite → accept/decline, plus the owner's cancel lane.
    // docs/design/m2b-group-invitations.md; invariants C-M2b·1..3) ──────

    // ── ADR 0094 additions (resident self-initiated join request for public
    // groups — the reverse direction of the m2b owner-invited lane.
    // docs/adr/0094-group-join-request-lane.md; invariants JR·1..5) ────────

    // ── GU guardian lanes (ADR 0028) — additive, beside the membership lanes ──
    // Account-scope supervision of a child's account (ADR 0028 §C). The standing
    // is <c>AccessVia.Guardian</c> (the 9th value), resolved **live** off the
    // active <see cref="GuardianLink"/> row (G·2) and **deny-by-default** (G·3).
    // These lanes are **management** lanes on this surface — they are *not* on
    // a <c>CanAsync</c> / <c>CanSeeAsync</c> content-decision path (G·1, the
    // load-bearing honesty: a guardian has no standing to read the child's
    // private content). ADR 0006-E compatible — appended to the owning
    // module's public surface, additive; the frozen 4-method
    // <c>IAuthorizationService</c> surface is byte-identical.

    // ── GU community-approval lane (the GU extension — the sibling of the m2b
    // group-invitation lane, but for communities and with the guardian as the
    // resolver) ───────────────────────────────────────────────────────────────

    // ── M28 (ADR 0151, D4) — guardian time limits: the per-child allow/block
    // schedule that gates a child's whole-platform access (the inverse of the
    // M20 quiet lane, ADR 0121, on the GU surface, ADR 0028). Three additive
    // seams (the ADR 0006-E compatible-ADD lane this file uses, the
    // <see cref="SetChildMessagingRestrictionAsync"/> GU-scope shape): the
    // guardian's edit-page READ (no audit), the guardian's WRITE (one audit
    // row), and the ENFORCEMENT READ (child-keyed, no guardian gate, no
    // audit). Zero new authorization surface (C-M28·5): no
    // IAuthorizationService method, no AccessAction/AccessVia, no Decide()
    // branch. ──────────────────────────────────────────────────────────────────

}