# ADR 0140 — Guardian community block-and-hide (the per-child ceiling on the ADR 0028 surface)

Status: Accepted
Date: 2026-10-04

The ADR 0028 GU surface ("manage a child account", `/me/children/{childId}`)
ships a guardian's supervision of a child. Two of its community lanes were
product mistakes, fixed here:

- **The "add a community" / "add a group" forms** made no sense — joining a
  community is decided by an admin (or is *implicit*, for a mandatory
  community), and joining a group is decided by the group's owner (an
  invitation) or an admin. A guardian does not mint group membership. Both
  forms are **removed** from the curation view (and the `CurateCommunity`
  add/remove community lane is retired).
- **"Removing a child from a community" was a silent no-op for a mandatory
  community.** ADR 0012's mandatory communities grant *implicit* membership
  to every resident, and the removal lanes (`RemoveCommunityMemberAsync`
  refuses, `ClearCommunityMembershipAsync` skips) will not remove it — so a
  guardian's "remove" did nothing exactly where it was wanted. The guardian's
  real control over a mandatory community is not to *remove* the child but to
  **block access to, and hide, the community from the child**.

This ADR adds that control — a **per-child community block** — the
`Profile.MessagingRestricted` guardian-ceiling precedent (ADR 0139) carried
from a single flag to a per-community set. It rides frozen seams, extending
none:

- **The ADR 0028 guardian standing** — the new
  `SetChildCommunityBlockAsync` write lane reuses the `GuardActiveLinkAsync`
  standing gate (G·2/G·3 deny-by-default) verbatim; the `guardian.
  community_block` / `guardian.community_unblock` audit rows are the
  `guardian.suspend` / `guardian.messaging_restrict` shape.
- **The ADR 0012 mandatory-membership invariant** — a mandatory community's
  *membership* is untouched (the child remains an implicit member; the raw
  `GetCommunityIdsAsync` read and the `/admin` per-account diff are
  byte-identical). The block is a *restriction on the child's own access*,
  not a membership change — which is precisely why it works where "remove"
  cannot.
- **The ADR 0004 §B.1 additive-surface discipline** — the new
  `Profile.BlockedCommunityIds` field is *additive* to the existing
  `Profile` document; **zero migrations**, no re-seed, no new `*DocTypes`
  surface.

## Context

ADR 0012 (mandatory communities) makes every verified resident an implicit
member of the mandatory set; nobody may be removed (the removal lanes refuse
/skip it) and nobody may leave (the self-leave route is gated on the flag).
The ADR 0028 GU surface layered "membership curation" on top — a guardian
add/removing the child to/from a community — which collided with ADR 0012
for the mandatory set: the guardian's remove was a silent no-op. The product
correction is to stop offering "remove" for a mandatory community and instead
give the guardian a way to **hide** the community from the child (block its
access), which is a different kind of control that ADR 0012 does not forbid.

The ADR 0139 messaging ceiling is the closest precedent: an additive
`Profile` field, a single guardian-scope write lane gated on an active
`GuardianLink`, one audit row, and a read seam the enforcement surfaces read
through. This ADR follows that shape exactly, with one difference — the
ceiling is a **per-community set** (`IReadOnlyList<string>`, like
`Profile.TagIds`) rather than a single bool, because a guardian may block
some communities and leave others open.

The access surfaces that must honor the block are the **child's own**:
the community directory / sidebar (the composer's `AccessibleComponentsAsync`),
the single-community feed (`GET /community/{componentId}`), the composer's
posting gate (`PostService.CreatePostAsync`), the audience-visibility of
community-scoped posts (the `AuthorizationService` community branch of
`Decide`), and community-scoped announcements
(`AnnouncementService`'s read-visibility resolver). The **admin** and the
**guardian's own curation view** keep reading the *raw* membership — a
guardian must still see the child's mandatory memberships in order to decide
which to block.

## Decision

**D1 — The block is an *additive* `Profile` field.** The new
`Profile.BlockedCommunityIds` (`IReadOnlyList<string>`, default `[]`) is an
additive field on the existing `Profile` document (ADR 0004 §B.1 —
delta-detected, idempotent, no re-seed, no EF migration, no new `*DocTypes`
surface — the `Profile.TagIds` / `Profile.MessagingRestricted` precedent).
For an unsupervised resident it stays empty, so every read below reduces to
the ordinary membership. *Forbids:* a new `CommunityBlock` table or a new
`GuardianLink` field (the restriction is on the *child's* profile, not on
the link — the ADR 0139 "ceiling is on the child, not the link" pin).

**D2 — The write lane is a *single-write* guardian-scope seam.** The new
`IUserInfoService.SetChildCommunityBlockAsync(string childId, string
communityId, bool blocked, string guardianId)` is the
`SetChildMessagingRestrictionAsync` shape verbatim: the
`GuardActiveLinkAsync` standing gate first (G·2/G·3 deny-by-default — a
non-guardian / dissolved link is `UnauthorizedAccessException` → the Web's
404), then the list toggle (idempotent — re-block / re-unblock never
duplicates or mis-removes), then one `AccessAudit` row, all in one session /
one `SaveChangesAsync` (C3). The component must exist (a block on a missing
community is `InvalidOperationException` → the Web's `TempData["error"]`,
the `ClearCommunityMembershipAsync` "must exist" pin). The audit row is
`guardian.community_block` (when blocking) / `guardian.
community_unblock` (when lifting), `TargetKind = "community"`,
`TargetId = communityId`, `Via = Guardian`, `Outcome = Allow`. Strong
consistency (C4): the toggle is live on the very next read. *Forbids:* a
block write on a (guardian, child) pair with no active link (the G·3
deny-by-default pin); touching the child's `ComponentMembership` row at all
(the block is a restriction, not a membership change — the ADR 0012
"cannot remove" invariant is preserved, not bypassed at the membership
layer).

**D3 — The enforcement read seam is `GetEffectiveCommunityIdsAsync`.** The
new `IUserInfoService.GetEffectiveCommunityIdsAsync(string userId)` returns
the raw `GetCommunityIdsAsync` membership **minus** the actor's
`Profile.BlockedCommunityIds`. It is the seam the **child's own access
surfaces** read through: the composer's `AccessibleComponentsAsync`
(directory/sidebar + posting-reach), the `PostService.CreatePostAsync`
posting gate, the `AuthorizationService` community branch of `Decide`
(audience visibility of community-scoped posts), the
`AnnouncementService` community read-visibility resolver, and the Web
single-community feed gate (D5). The **raw** `GetCommunityIdsAsync` is kept
for the surfaces that reason about the child's *actual* memberships
regardless of a guardian's restriction: the guardian's own curation view
(`GuardianController.Detail` still reads `Model.CommunityIds` from the raw
read, so a mandatory community still appears for the guardian to block) and
the `/admin` per-account community diff. A missing profile degrades to the
raw membership (the ADR 0139 `MessagingRestricted` null-safe floor).
*Forbids:* a default-open drift (a community the guardian blocked must not
leak into the child's effective set on any surface); a `Profile` read on a
non-community decision path (the effective-set read happens only where
community standing is actually consulted — D5's Web feed gate is an
exception, see its note); a membership mutation disguised as a read (the
effective set is a *read*, never a write).

**D4 — The "add" forms are removed; the "remove-from-community" lane is
retired.** The `Detail.cshtml` "add a group" and "add a community"
text-input forms are deleted (joining is decided by the group's owner or an
admin — the group **invitation** approve lane and the group-membership
**remove** lane are the guardian's legitimate gates and are unchanged). The
`GuardianController.CurateCommunity` POST route
(`me/children/{childId}/memberships/community`) is deleted (its only caller
was the removed form + the per-community "Remove" button). The group's
`CurateGroup` remove lane (and its per-group "Remove" button) are kept —
removing a child from a *group* is meaningful (groups have no implicit
membership). *Forbids:* a new "add a community" surface; re-adding a
"remove child from a mandatory community" button (a silent no-op under
ADR 0012 — the block is the correct control).

**D5 — The community surface is a per-community block/unblock toggle on the
existing `/me/children/{childId}` curation view.** The new
`GuardianController.SetChildCommunityBlock` POST action (the route
`me/children/{childId}/communities/{communityId}`, `[ValidateAntiForgeryToken]`,
the `SetChildMessaging` idiom verbatim) reads the `ActiveLinkAsync` standing
gate first (a non-guardian is a 404, the ADR 0028 shape), then calls the D2
write lane. The `Detail.cshtml` community section is rewritten from the
"list + Remove + Add form" to a **list of the child's communities** (the
raw `Model.CommunityIds`) each with a **state-picked button**: not-blocked
→ `Block access & hide` (a `blocked = true` POST); blocked → a
`Blocked & hidden` badge + `Unblock & show` (a `blocked = false` POST). The
child's current block set is exposed on `ViewData["BlockedCommunityIds"]`
(the `MembershipEditorModel` is a pinned 5-field record —
`GuardianViewModelsTests.MembershipEditorModel_Is_Exact_Five_Field_
Projection` forbids a 6th field — so the block state rides `ViewData`, the
ADR 0139 `MessagingRestricted` / `_AudienceEditor` precedent for non-model
view data). The **single-community feed** (`PostsController.Index` on
`/community/{componentId}`) gains a pre-feed 404 gate: a non-admin,
non-moderator viewer whose effective set does not include the community sees
a 404 — the same shape as the missing/disabled-component 404, so a blocked
community is invisible to the child (admin/moderator standing is unaffected
— they still manage it). *Forbids:* a block/unblock surface that removes the
child's membership (the block is a restriction, not a membership write); a
6th field on `MembershipEditorModel` (the pin); a new view-model for the
block state (it rides `ViewData`).

**D6 — The localization contract is extended in all four languages.** The
four new UI keys (`guardian.community_block_note`, `guardian.community_
blocked`, `guardian.community_unblock`, `guardian.community_block`) are
registered in `KnownTranslationKeys` in `EnValues` **and** `DeValues` /
`FrValues` / `DaValues` (the "no English-only key" parity rule + the
`KwLRegistryConsistencyTests` "every used key is registered" pin). The now-
dead `guardian.community_id_label` key is retained in the registry (a
registry key is a closed contract; the key simply goes unused) to avoid
perturbing the parity tests' closed-set assertions.

## Consequences

- A guardian can hide a mandatory community from a child — the exact case
  the old "remove" lane could not handle — without touching the child's
  membership (ADR 0012's invariant intact; `/admin` and the guardian's own
  curation list are unchanged).
- The child's effective community set (directory, feed, posting, community
  post/announcement visibility) excludes a blocked community; admin and the
  guardian's curation view still see it (they act on it).
- The `ResolveActorAsync` community set now reads `Profile` on community-
  scoped decisions (a `Profile` read the decision path did not previously
  perform) — every test harness that drives `AuthorizationService` through
  a store that does not register `Profile` (the `InventoryServiceTests`
  minimal `M16DocTypes`-only store) must register `M1DocTypes`, the same as
  its `PostServiceTests` / `ProjectServiceTests` siblings.
- Frozen seams (`GetCommunityIdsAsync`, `ComponentMembership`, the ADR 0012
  mandatory lanes) are **byte-identical**; the new surface is three seams
  (one field, one write, one read) + one Web action + a rewritten view
  section, all following the ADR 0139 / ADR 0028 idiom.

## Pinned seams / tests

- `UserInfoServiceTests`: `GetEffectiveCommunityIds_ExcludesGuardian-
  BlockedCommunity`, `GetEffectiveCommunityIds_HidesMandatoryCommunityFor-
  BlockedChild`, `SetChildCommunityBlock_TogglesEffectiveSet_AndAuditsVia-
  Guardian`, `SetChildCommunityBlock_NonGuardian_Unauthorized` (the D1/D2/D3
  + the mandatory-community pin + the G·3 deny-by-default pin).
- `GuardianCommunityBlockTests` (Web): `SetChildCommunityBlock_NonGuardian-
  _Returns404_NoWrite`, `SetChildCommunityBlock_Guardian_BlockedTrue_Block-
  Live_AuditedViaGuardian`, `SetChildCommunityBlock_BlockedFalse_LiftsBlock`
  (the D5 Web surface + the ADR 0028 deny-by-default shape, mirroring
  `GuardianMessagingRestrictionTests`).
- `GuardianViewModelsTests`: `MembershipEditorModel_Is_Exact_Five_Field_
  Projection` (unchanged — the block state rides `ViewData`, not the model).
- `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`: the
  four new keys present in all four dictionaries (D6).
- `InventoryServiceTests`: unchanged behavior; the harness now registers
  `M1DocTypes` so the community-branch `Profile` read resolves (see
  Consequences).
