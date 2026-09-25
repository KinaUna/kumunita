# ADR 0083 — Group membership notifications: notify the resident when they are added to a group or invited to join one (kinds `group.added` / `group.invite`), and the tabbed group detail page

Status: Accepted
Date: 2026-09-25
Amends: **0007** (the group-management lane — owner ∪ GlobalAdmin is the standing
that *adds* / *invites*; the two emitters sit exactly on the two write seams that
ADR 0007's SoD gate protects, and the recipient is always the *other* resident,
never the actor), **0008** (the self-leave lane — its write seam
`RemoveGroupMemberAsync` deliberately does **not** emit: a self-leave is not
something to be notified *about*), **0010** (private groups — the Privacy tab
this ADR re-homes; a private group's add/invite carries the same notification as
a public one, so membership reach is not a signal that "a private group is
happening"), **0013** (the group-post lane — its "notifications on new group
posts" deferral was settled by ADR 0076's `group.post` kind + wired emitter;
this ADR is the sibling: the *membership-change* kinds, not the *content* kind),
**0026** (group name/description translations — the Translations tab this ADR
re-homes, unchanged in behavior), **0076** (the M6 notifications context — this
ADR **adds two kinds** to the closed `NotificationKinds` vocabulary D2 froze,
**reuses verbatim** the `NotificationService.EmitAsync` seam D1/D4/D5/D7 pinned,
the frozen `IMailerStage.StageAsync` trio, and the ADR 0061 per-recipient
language resolution; **no new context, no new document, no new surface, no new
service**, the settings toggle list simply grows by two), **0077** (the
admin-lane kinds — the precedent for "a kind whose recipient is not the acting
resident"; here the recipient *is* the affected resident, so it is the resident
lane, not the admin lane), and **0078** (sample-account suppression in
production — inherited by the two new kinds for free: the `EmitAsync`
suppression gate is kind-agnostic).

## Context

The M6 notifications surface (ADR 0076) shipped the **shared awareness** arrow:
an inbox (the durable "things that happened to me" list) + the best-effort email
nudge, a closed kind vocabulary the code's emitters write into, and a settings
page where a resident toggles which kinds earn an email. Every kind ADR 0076
wired is about *content or state the resident already has a relationship to*: a
reply on my post, a new post in a group I'm in, an RSVP on my event, a report on
my content, a to-do assigned to me.

But two of the most common "something just changed about *me*" moments on a
neighborhood platform were still **silent**:

- **I was added to a group.** A group owner (or a GlobalAdmin) added a resident
  directly to a group the resident did not choose to join. The resident finds
  out — if they notice at all — only by re-visiting the group list and seeing a
  new group they don't recognize. There is no "you've been added to the
  Neighborhood Garden Club" signal anywhere.
- **I was invited to a group.** An owner invited a resident to join. The invite
  is a pending row (`GroupInvitation`, ADR 0007's invitation lane) that only
  surfaces on the group's detail page (the owner's view) — the *invitee* has no
  notification that an invitation is waiting for them.

These are exactly the "reach out to the resident where they are" moments the
M6 arrow promises, and both are **owned by the `UserInfo` bounded context**
(the `GroupMembership` upsert in `AddGroupMemberAsync` and the
`GroupInvitation` write in `InviteGroupMemberAsync`), not by the content
lanes that own the other kinds. The M6 design deliberately scoped its emitters
to the content/state lanes it was executing against and left membership-change
awareness as a follow-on. **This ADR executes that follow-on** — the
group-membership lane's own notifications — and, while re-homing that lane's
surface, the group detail page itself.

**The second half of the work is the group detail page.** The page had grown:
the membership-scoped feed (ADR 0013), the member list + the owner's invite/add
lanes (M2b / ADR 0007), the self-leave lane (ADR 0008), the privacy toggle
(ADR 0010), and the name/description translations (ADR 0026) — all stacked
vertically on one long scroll, the feed on top and every management control
below it. For a non-owner member the page is "a feed with a wall of forms I
can't use below it"; for an owner it is "a feed, then a wall of settings". The
natural shape is the one the platform already uses for multi-section surfaces
(the projects `_ProjectsTabs.cshtml` nav-tabs, ADR 0062): **tabs** — Feed,
Members, and Settings (owner ∪ GlobalAdmin only), each a distinct concern rather
than one scroll. This ADR folds that reorganization in because the Settings tab
is *the* home for the privacy + description-edit + translations controls, and
getting the tab split right is what makes "we might later add notification
options and more" to the Settings tab an additive step rather than a rewrite.

**Roadmap order (recorded here, per the sign-off gate):** no roadmap letter
moves. Group-membership notifications are not a milestone — they are a lane
within the M6 notifications surface (the same shape as ADR 0013's `group.post`
kind and ADR 0077's admin-lane kinds). `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` are **untouched**.

## Decision

**D1 — Two new kinds on the closed vocabulary; the closed set grows 11 → 13.**
`NotificationKinds` gains two `public const string` members, placed in `Known`
adjacent to the other group kind (`GroupPost`) so the settings toggle order is
topical (a group resident sees the group kinds together):
`GroupAdded` = `"group.added"` and `GroupInvite` = `"group.invite"`. Both are
**wired** (an emitter exists for each, D2) — neither is reserved. The
`Known` list goes from eleven to thirteen entries, and every downstream shape
that reads it (the settings toggle list, the per-kind `kw-l` key table, the
Web test that pins the count) follows: the
`NotificationsControllerTests` count assertion moves `11 → 13`, and the
`KnownTranslationKeys` parity test (enforced by
`KwLRegistryConsistencyTests`) now requires the four new keys per kind, in all
four languages. *Forbids:* a resident-authored kind string, an enum-backed
kind, a kind that lacks all four `kw-l` keys in all four languages, or a change
to the closed set that the parity test does not pin.

**D2 — The emitters sit on the `UserInfoService` write seams, in the caller's
transaction, recipient = the affected resident.** `AddGroupMemberAsync` emits
`group.added` and `InviteGroupMemberAsync` emits `group.invite`, **both in the
same session** as the membership / invitation write (so the inbox row + the
outbox row commit atomically with the domain write — the C3 single-commit
invariant, the M6 D5 pin). The **recipient is always the *other* resident**
(the newly-added resident in the add lane, the invitee in the invite lane) —
never the actor (`addedBy`), and never the owner. The **idempotency key** is
content-derived and stable, of the M6 D4 shape `notification:{kind}:{id}` with
the group and recipient as the stable source:
`notification:group.added:{groupId}:{userId}` and
`notification:group.invite:{groupId}:{userId}` — so a re-stamp of the same
(group, user) membership re-emits with the **same** key and collapses to a
no-op on both channels (a second inbox row / a second email for a duplicate add
is the exact failure M6 D4 forbids, and the test
`AddGroupMember_Readd_EmitsOnlyOneRow_IdempotencyKeyDedups` pins it). The
**body** passed to `EmitAsync` is the **group's display name** (the UGC
snippet, appended after one space to the localized body template — the
"You've been added to the group {0}" shape, ADR 0018's authored-language
snippet rule). *Forbids:* an emitter in a Web controller, an emitter on a
read lane, a recipient that is the actor, a non-content-derived idempotency
key, or a body that is not the group name.

**D3 — The circular-dependency seam: `UserInfoService` resolves the
`NotificationService` lazily from an optional `IServiceProvider`.** The
`NotificationService` constructor takes `IUserInfoService` (for the recipient's
`Profile.EmailLanguage` + `Email`, ADR 0061), so injecting a
`NotificationService?` **directly** into `UserInfoService` would create a
construction-time cycle (`IUserInfoService → UserInfoService →
NotificationService → IUserInfoService`). The `IdentityService` emitters
(ADR 0077) only avoid this because `IIdentityService` is a *different*
registration than the `IUserInfoService` the `NotificationService` consumes.
The fix here: `UserInfoService`'s primary-ctor gains a **trailing, optional**
`IServiceProvider? services = null`, and the two emitters resolve the
`NotificationService` **at emission time** via
`services?.GetService<Notifications.NotificationService>()` — deferred to
runtime, which breaks the construction-time cycle (the service graph is only
assembled when an emission actually happens, by which point both ends of the
would-be cycle are already constructed). The `null` default preserves the
**181 pre-ADR 0083 direct-construction test call sites** that build
`UserInfoService(store)` positionally: they get `services = null`, the
`GetService` call is a no-op, and the emission is **silently skipped** (the
write still commits; no notification row) — exactly the pre-M6 behavior, so no
existing test's expectations change and no harness is forced to wire the
`NotificationService`. The DI container (which *does* register the
`NotificationService`) resolves `IServiceProvider` and supplies it, so the
production emitters are live. *Forbids:* a direct `NotificationService?`
parameter on `UserInfoService`, a mandatory (non-optional) `IServiceProvider`,
a `Func<NotificationService>` that is eagerly invoked at construction, or a
change that breaks the 181 positional `UserInfoService(store)` call sites.

**D4 — The notification is a *personal read* on the recipient's own inbox,
exactly as M6 D3 pinned — no authorization, no audit.** A
`group.added` / `group.invite` row is **the recipient's own data** (the
`RecipientId` is the whole access story); the inbox read lane does not call
`IAuthorizationService` (there is no audience to evaluate). **No audit row is
emitted for the notification itself** — the *domain* write (the membership
upsert / the invitation write) already emits its own `AccessAudit` row
(`group.add-member` / the invitation action, ADR 0007's SoD derivation), which
is the auditable record; the notification is the *consequence* of that
audited write, not a new decision. *Forbids:* a new `AccessAction`, a new
`AccessVia`, a `NotificationToAuditableResource` adapter, or an audit row on
the notification emission.

**D5 — Email is best-effort; the inbox is the durable record — the M6 D5/D7
invariants apply to the two new kinds unchanged.** The recipient **always**
gets the inbox row (the durable "you were added / invited" record, which they
see when they next sign in — or when they later add an email address). The
email is staged **conditionally** on the recipient's
`NotificationPreference` (the lean-default: no preference yet = all enabled)
**and** on the recipient having a `Profile.Email` at all (a resident without
an email still gets the inbox row; no `IMailerStage.StageAsync` call — the
M4 precedent, the "no address to deliver to" path, pinned by the test
`AddGroupMember_ResidentWithoutEmail_StillStoresInboxRow_NoEmail`). A
resident who disables the kind keeps the inbox record and loses only the email
nudge. *Forbids:* an inbox-row rollback on send failure, suppressing the inbox
row on a disabled kind, a new email mechanism, or a sender-side override of the
recipient's preference.

**D6 — The two new kinds join the closed `KnownTranslationKeys` registry, all
four languages, four keys each — the ADR 0015 parity test enforces the
delta.** Per kind the registry gains exactly the four keys the parity test
pins, in **en / de / fr / da**: `notifications.kind.{kind}` (the settings +
inbox label), `notifications.preference.{kind}.label` (the preferences toggle),
`notification.{kind}.subject` (the email subject), `notification.{kind}.body`
(the email body template — the UGC snippet, the group name, is appended after
one space, so the templates carry a trailing space). That is 8 new keys × 4
languages = 32 entries added to `KnownTranslationKeys`, and the
`KwLRegistryConsistencyTests` parity check (exact key-set equality across the
four dictionaries) is what enforces the count — a missing key in *any*
language fails the build, not a flake. *Forbids:* a key present in fewer than
four languages, a subject/body template that omits the trailing-space
snippet slot, or a kind that appears in `NotificationKinds.Known` without its
four keys registered.

**D7 — The group detail page becomes three tabs: Feed, Members, Settings
(owner ∪ GlobalAdmin only), server-rendered, no new client JS.**
`Views/Groups/Detail.cshtml` restructures the existing sections into the
Bootstrap `nav nav-tabs` idiom (the `_ProjectsTabs.cshtml` precedent, ADR
0062) with the header (back link, `<h1>` group name, "Owned by …" + the
owner/private badges, the **description read**) above the tab bar:

- **Feed** — the membership-scoped posts (ADR 0013): the "New post" button
  (when `Model.CanPost`), the empty state, and the posts `list-group`. This is
  the default tab and the one every member lands on.
- **Members** — the member list (with the per-row **Remove** for
  `canManage` / **Leave** on the actor's own row, ADR 0007/0008), the
  owner's **pending invitations** list, the **invite** form (owner), and the
  **add-member** form (`canManage`).
- **Settings** — rendered **only when `canManage`**
  (`Model.IsOwner || GlobalAdmin`, the C-M2·3 owner ∪ GlobalAdmin standing the
  controller's `TryResolveOwnerSurface` gates already enforce): the **About**
  (description *edit*) form, the **Privacy** toggle (ADR 0010, `isPrivate`),
  and the **Translations** block (ADR 0026, the chips + per-translation
  `<details>` + the add-translation forms when `Model.CanTranslate`).

The **active tab is derived server-side from the `?tab=` query string**
(`?tab=feed|members|settings`), so a deep link `/groups/{id}?tab=settings`
lands on Settings and the "back to the group" links from a post's detail page
drop the query and fall back to the default (Feed) — no client-side tab JS, no
new dependency (the ADR 0031 tsc-only / no-dependency pin). The description
**read** stays in the header (a group's blurb sits above the tabs, like a
profile's bio); only the description **edit** form moves into Settings. *Forbids:*
a client-side tab-switching script, a Settings tab offered to a plain member,
a member who types `?tab=settings` receiving a 404 (they fall back to Feed —
the read never 404s; the route's SoD gate is the real wall, the tab just isn't
offered), or moving the description read out of the header.

**D8 — Reuse, don't reinvent.** The M6 `NotificationService.EmitAsync` seam
(D1/D4/D5/D7), the M1 durable-email trio (`IMailerStage.StageAsync` +
`OutboxEmailStager` + `OutboxEmailHandler` + `EmailDeadLetterWriter`), the
ADR 0061 per-recipient outbound-channel language, the closed
`KnownTranslationKeys` registry (ADR 0015), the Bootstrap `nav-tabs` idiom
(ADR 0062), and the ADR 0078 sample-account suppression gate (inherited by the
two new kinds for free — the suppression check in `EmitAsync` is
kind-agnostic) are all **frozen seams**. This ADR adds **two kind constants +
two emitters (both in `UserInfoService`) + 32 `kw-l` keys + one view
restructure + one new test file (7 tests) + one test-count update
(`NotificationsControllerTests` `11 → 13`)**, not a branch. **No new
bounded context, no new document, no new `M*DocTypes` surface, no new
`INotificationService` interface, no new `AccessAction`, no new `AccessVia`,
no new `IAuthorizableResource` adapter, no new email mechanism, no new client
dependency.** *Forbids:* a new authorization path, a new email mechanism, a
new UI dependency, or a notification mechanism outside M6's `EmitAsync`.

## Consequences

- `NotificationKinds` now has **thirteen** kinds (eleven + `group.added` +
  `group.invite`); the settings toggles render two more rows; the
  `NotificationsControllerTests` count assertion moves `11 → 13`; and the
  `KnownTranslationKeys` registry gains 32 entries (8 keys × 4 languages)
  whose exact key-set parity across en/de/fr/da is enforced at build time.
  That is the whole kind-vocabulary delta.
- `UserInfoService`'s primary-ctor gains one trailing, optional
  `IServiceProvider?` parameter; the two write seams gain an emission each,
  resolved lazily at emit time. The 181 positional
  `UserInfoService(store)` test call sites are **unchanged** (they get
  `services = null` → no emission, the pre-M6 behavior) — the new emitter
  coverage is a dedicated test file
  (`UserInfoServiceGroupNotificationTests`) that wires the
  `NotificationService` through a `ServiceCollection` and pins: the row +
  idempotency key + the staged email, the re-add/re-invite dedup
  (one row, one email), the no-email-still-gets-inbox path, the two kinds
  landing for two different recipients, and the `services = null`
  no-emission path. **No existing Core or Web test changes its
  expectations** — the only count that moves is the notifications count
  assertion, by design.
- A resident who is added to a group or invited to one now sees an inbox row
  (and, if they have an email and haven't disabled the kind, an email) in
  their own `EmailLanguage` (ADR 0061), with the group's name as the snippet
  (ADR 0018) — the "something just changed about me" signal M6 promised for
  membership changes and that ADR 0076 left as a follow-on.
- The group detail page is now Feed / Members / Settings tabs: a member lands
  on the Feed; an owner ∪ GlobalAdmin additionally sees Members' management
  lanes and the Settings tab (About + Privacy + Translations); deep links
  `?tab=…` land on the right tab; and the Settings tab is the home the
  roadmap's "we might later add notification options and more" points at.
- **Deferred, each named:** per-kind **sub-settings** (an email-only vs
  inbox-only split, ADR 0076's follow-on), **notification options on the
  group Settings tab** (this ADR names the lane the user asked for — "later
  we might add notification options" — as the follow-on this Settings tab
  makes additive), the **`post.mention`** emitter (still reserved, ADR 0076
  D2), **group/community to-do assignment** notifications (ADR 0073 / 0074),
  **push / PWA push** (M9), **digests**, and **live update** (WebSocket / SSE)
  — each a follow-on lane with its own ADR.
