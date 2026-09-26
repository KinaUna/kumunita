# ADR 0084 — Per-target notification subscriptions: opt-in/opt-out per (recipient, kind, target) lane on the frozen M6 emitter, a settings-page list, and the per-page subscribe toggle

Status: Accepted
Date: 2026-09-25
Amends: **0076** (the M6 notifications context — this ADR **adds a new
document** `NotificationSubscription` to the `M6DocTypes` surface, **adds
three new kinds** to the closed `NotificationKinds` vocabulary D2 froze,
**amends the frozen `EmitAsync` surface** with a 7-arg overload + a gate
step that consults the new subscription lane *first* (the ADR 0078
sample-suppression precedent — an additive gate, no signature change to the
existing 6-arg overload), and **adds two new public lanes** on the frozen
`NotificationService` (`GetSubscriptionsAsync` / `SetSubscriptionAsync` /
`IsSubscriptionEnabledForAsync`) — the same frozen-surface-amendment
precedent ADR 0078 set for `EmitAsync`'s return widening), and **0077** (the
admin-lane kinds — the precedent for "a kind whose recipient is not fixed
by the event" — here the recipient *is* the resident, so it is the
resident lane, not the admin lane), **0078** (sample-account suppression —
the frozen-surface amendment precedent this ADR follows: an additive gate
consulted at the head of `EmitAsync`, the 6-arg signature unchanged, the
return `Task<Notification?>` unchanged, the suppression check kind-agnostic
so the three new kinds inherit it for free), **0015** (the closed
`KnownTranslationKeys` registry — the three new kinds + the settings-page
labels + the page's subscribe button labels each add `kw-l` keys in all
four languages; the parity test enforces the delta), **0005** (the
per-recipient outbound-channel language — unchanged, the new kinds' email
bodies resolve in the recipient's `Profile.EmailLanguage` exactly as the
other kinds do), and **0039 / 0040** (the page lane — the per-page
subscribe button sits on the page's detail header, the `page.child`
emitter sits on the `PageService.CreateAsync` write seam, and the
`PagePaths.Href` / `PagePaths.Derive` seam is reused for the redirect-back
shape).

## Context

The M6 notifications surface (ADR 0076) shipped the **shared awareness**
arrow: an inbox (the durable "things that happened to me" list) + the
best-effort email nudge, a closed kind vocabulary the code's emitters write
into, and a settings page where a resident toggles which *kinds* earn an
email. Every kind ADR 0076 wired is **kind-level** — the resident's
preference is one bit per kind, shared across every possible target of
that kind. ADR 0077 added two admin-lane kinds (still kind-level: the
recipient is every GlobalAdmin, not a target the resident could scope).
ADR 0083 added two group-membership kinds (still kind-level: the recipient
is the affected resident, not a target the resident could scope).

But four of the most common "I want to hear about *this specific thing*"
moments on a neighborhood platform have a **per-target** shape that a
kind-level toggle cannot express — and in three of the four, the content
lane the signal belongs to has been in the platform for a while:

- **A new announcement in a community I'm in** (or a flat/public
  announcement). The announcement *feature* (the `Announcement` doc, the
  publish/detail surface) has been in the platform since M3b — but the
  M6 kind vocabulary never gained an `announcement` kind, and the
  `AnnouncementService` write seam never emitted one. This ADR completes
  that wiring (recipient universe: the target community's members, or
  every verified resident for a flat/public announcement), and the
  per-target scope (a resident in five communities may want announcements
  from only two) is what makes it expressible at all.
- **A new post in a community I'm in.** The community-post lane has been
  in the platform since M3 — but, like the announcement lane, the M6
  kind vocabulary has no `community.post` kind, and the
  `PostService.CreatePostAsync` seam never emitted one. This ADR
  completes that wiring (recipient universe: the community's members,
  excluding the author), again with the per-target scope that lets a
  resident in five communities opt out of three.
- **A new post in a group I'm in.** The `group.post` kind already exists
  (ADR 0013, wired in ADR 0076) — this ADR does *not* add it; it
  **re-gates** it on the per-target shape (target = the group id), so a
  resident in eight groups may want posts from only three. The
  opt-OUT default is unchanged, so existing residents see no behavior
  change.
- **A new page added under a parent page I care about.** The page lane
  (ADR 0039 / 0040) has no notification kind at all — the "a new page
  appeared under the section I follow" signal is missing. And the
  recipient universe is not "every resident" (a page's children are
  audience-scoped, not broadcast) but "the residents who opted in to
  this parent page's children" — a per-target scope with no kind-level
  fallback.

These are exactly the "reach out to the resident where they are" moments
the M6 arrow promises, and all four are **owned by the content lane that
fires the event** (the `AnnouncementService` / `PostService` /
`PageService` write seams), not by a new bounded context. The M6 design
deliberately scoped its emitters to the kind-level shape it was executing
against and left per-target scoping as a follow-on. **This ADR executes
that follow-on** — the per-target subscription lane: for the two lanes
that already existed but were not yet wired into the notifications
surface (announcements, community posts), it *completes the wiring*;
for the one that was wired but only kind-level (group posts), it
*re-gates* it per-target; and for the one that has never existed (page
children), it *adds* the wiring. While doing so, it adds the settings-page
list the resident uses to manage it and the per-page subscribe toggle
(the one target where the "subscribe" affordance belongs on the target
itself, not only in settings).

**Roadmap order (recorded here, per the sign-off gate):** no roadmap
letter moves. Per-target notification subscriptions are not a milestone —
they are a lane within the M6 notifications surface (the same shape as
ADR 0083's group-membership kinds and ADR 0077's admin-lane kinds).
`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are
**untouched**.

## Decision

**D1 — Three new kinds on the closed vocabulary; the closed set grows
13 → 16.** `NotificationKinds` gains three `public const string`
members, placed in `Known` adjacent to the other ADR 0084 kinds so the
settings toggle order is topical: `Announcement` = `"announcement"`,
`CommunityPost` = `"community.post"`, `PageChild` = `"page.child"`. All
three are **wired** (an emitter exists for each, D2) — none is reserved.
The `Known` list goes from thirteen to sixteen entries, and every
downstream shape that reads it (the settings toggle list, the per-kind
`kw-l` key table, the Web test that pins the count) follows: the
`NotificationsControllerTests` count assertion moves `13 → 16`, and the
`KnownTranslationKeys` parity test (enforced by
`KwLRegistryConsistencyTests`) now requires the new keys in all four
languages. *Forbids:* a resident-authored kind string, an enum-backed
kind, a kind that lacks its `kw-l` keys in all four languages, or a change
to the closed set that the parity test does not pin.

**D2 — The emitters sit on the content-lane write seams, in the caller's
transaction, recipient = the affected resident, target = the scope the
resident subscribed to.** Three emitters, each on the lane that already
owns the event (the announcements / community-post / page lanes have all
three been in the platform; this ADR is what finally wires two of them
into the notifications surface and re-gates the third):

- **`announcement`** — the `AnnouncementService` write seam emits to the
  target community's members (for a community-scoped announcement) or to
  every verified resident (for a flat/public announcement, target = the
  `"announcements"` sentinel, D4). Idempotency key:
  `notification:announcement:{announcementId}:{memberId}`.
- **`community.post`** — the `PostService.CreatePostAsync` write seam
  emits to the community's members, excluding the author. Target = the
  community id. Idempotency key:
  `notification:community.post:{postId}:{memberId}`.
- **`page.child`** — the `PageService.CreateAsync` write seam emits to
  the parent page's subscribers (the `NotificationSubscription` rows for
  the parent id), excluding the author. Target = the parent page's id.
  Idempotency key: `notification:page.child:{newPageId}:{subscriberId}`.

All three are **in the same session** as the domain write (so the inbox
row + the outbox row commit atomically with the domain write — the C3
single-commit invariant, the M6 D5 pin). The **recipient is always the
resident** (never the actor for `community.post` / `page.child`; the
author is excluded). The **body** passed to `EmitAsync` is the UGC
snippet (the announcement title / the post title / the new page's title,
appended after one space to the localized body template — the ADR 0018
authored-language snippet rule). *Forbids:* an emitter in a Web
controller, an emitter on a read lane, a recipient that is the author (for
`community.post` / `page.child`), a non-content-derived idempotency key,
or a body that is not the UGC snippet.

**D3 — The frozen-surface amendment: a 7-arg `EmitAsync` overload + a
gate step consulted *first*.** The existing 6-arg
`EmitAsync(session, recipientId, kind, idempotencyKey, body, ct)` is
**unchanged in signature and behavior** (the ADR 0078 precedent — the
return is already `Task<Notification?>` from the sample-suppression
gate; the 6-arg overload forwards to the new 7-arg overload with
`targetId: null`, which skips the gate). The new 7-arg overload
`EmitAsync(session, recipientId, kind, idempotencyKey, body, targetId,
ct)` gains a **gate step (0)** consulted **before** dedup, before the
profile read, before the sample-suppression gate: when `targetId` is
non-empty, the recipient's effective
`IsSubscriptionEnabledForAsync(session, recipientId, kind, targetId)`
choice is consulted, and a `false` result **short-circuits with
`return null`** (no inbox row, no email, no side effects of any kind —
the ADR 0078 sample-suppression shape, but for a per-target
subscription). When `targetId` is null/empty (legacy emitters, or kinds
with no per-target scope), the gate is **skipped** — the kind's existing
behavior is unchanged, so the 13 pre-ADR 0084 legacy emitters are
**byte-for-byte** the same as before. The **return type** is unchanged
(`Task<Notification?>`). *Forbids:* a change to the 6-arg signature, a
gate that runs *after* dedup (a disabled recipient must not leave a
dedup row behind), a gate that consults the preference lane instead of
the subscription lane (the two are distinct: the preference lane decides
"do I get an email for this kind at all", the subscription lane decides
"do I get *anything* for this (kind, target) at all"), a new
`INotificationService` interface, or a gate that is not consulted first.

**D4 — The `"announcements"` target sentinel for flat/public
announcements.** A community-scoped announcement has a natural target:
the community id. A **flat/public** announcement has no community — the
recipient universe is every verified resident, and the subscription
target must be a stable, code-owned string that is not a community id
(collision risk: a community id is a `Component.Id`, and a flat
announcement's target must not be *interpreted* as a community id by any
downstream code). The sentinel is the **string literal
`"announcements"`** (lowercase, no dot — distinct from any
`Component.Id` the codebase uses), defined as
`NotificationsController.AnnouncementsTargetSentinel` (the Web-layer
display constant) and the same literal at the Core emitter call site
(the `AnnouncementService` flat/public lane). The (recipient, kind,
target) triple for a flat announcement is therefore
`(residentId, "announcement", "announcements")`. *Forbids:* a flat
announcement that uses a community id as its target (collision), a
sentinel that is not the exact literal `"announcements"` (the test
`S1_GET_Subscriptions_Returns_200_And_Effective_Rows` pins the sentinel
string), or a per-community announcement that uses the sentinel (a
community-scoped announcement's target is the community id, not the
sentinel).

**D5 — The opt-in / opt-out table: `NotificationKinds.OptInKinds`.** The
three new kinds are **split by default**: `Announcement` and `PageChild`
are **opt-IN** (disabled until the resident explicitly enables them —
the recipient universe is broad — every community member for
`announcement`, every parent-page subscriber for `page.child` — and
default-on would be noisy for a resident in five communities or
subscribed to ten parent pages). `CommunityPost` is **opt-OUT**
(enabled until the resident explicitly disables it — the recipient
universe is the community's members, a narrower set, and the shape
mirrors the existing `GroupPost` kind's opt-OUT default). The existing
`GroupPost` kind (ADR 0013 / 0076) is **re-gated** on the per-target
shape (target = the group id) but keeps its opt-OUT default — the
13 pre-ADR 0084 residents who already have `group.post` enabled see no
behavior change (the gate consults the stored row's `Enabled` when
present, else the kind's default, and the default for `group.post` is
still enabled). The table is the **closed, code-owned**
`NotificationKinds.OptInKinds` set:
`{ Announcement, PageChild }` — the complement (all other kinds,
including `CommunityPost` and `GroupPost`) is opt-OUT. *Forbids:* a
resident-authored opt-in/opt-out choice (the table is code-owned), a
kind in `OptInKinds` that is not one of the three new ADR 0084 kinds, a
kind not in `OptInKinds` that defaults to disabled (the complement is
opt-OUT by definition), or a change to the table that is not pinned by
the `NotificationServiceTests.F13–F20` gate tests.

**D6 — The `NotificationSubscription` document: the (recipient, kind,
target) business key, upsert-only, never deleted.** A new
`Kumunita.Core.Notifications.NotificationSubscription` doc on the
`M6DocTypes` surface: `Id` (a `Guid` string, the Marten doc id),
`RecipientId` (the resident's subject id — the whole access story, D3 /
F11), `Kind` (the code-owned `NotificationKinds` constant), `TargetId`
(the community id / group id / parent-page id / `"announcements"`
sentinel), `Enabled` (the resident's explicit choice — `true` or
`false`), and `Updated` (the `DateTimeOffset` of the last upsert). The
**business key** is `(RecipientId, Kind, TargetId)` — a unique index in
the `M6DocTypes` surface (a second row for the same triple is the exact
failure the upsert forbids). The **upsert** semantics:
`SetSubscriptionAsync` **never deletes** a row — an opt-in toggle-off
stores an `Enabled = false` row (the gate consults the row's value, not
its presence, so the row is the record of the resident's last explicit
choice — the same convention as the `SetPreferencesAsync` lane, which
stores the whole preference rather than deleting it on an opt-out). A
fresh install (no row) falls back to the kind's default from
`OptInKinds`. *Forbids:* a `SetSubscriptionAsync` that deletes a row, a
row whose `RecipientId` is not the actor (a resident cannot toggle
another resident's subscription), a row whose `Kind` is not one of the
four per-target kinds (the Web-layer `SubscriptionKinds` set rejects
other kinds at the route level — a client cannot mint a subscription the
emitters don't consult), or a row whose `TargetId` is empty (the
business key requires all three parts).

**D7 — The settings-page list: `GET /notifications/subscriptions` +
`POST /notifications/subscriptions`.** The `NotificationsController`
gains two routes:

- **`GET /notifications/subscriptions`** — the settings list: every
  (kind, target) pair the resident can subscribe to, with its effective
  state (the stored row's `Enabled` when one exists, else the kind's
  default from `OptInKinds` — opt-IN kinds default to disabled, opt-OUT
  kinds to enabled; the same rule the emit gate uses, so the toggle the
  resident sees is exactly what the emitters consult). The rows are
  assembled from the resident's **member communities** (the
  `IUserInfoService.GetCommunityIdsAsync` read seam — the membership is
  the single source, no audit row), the **flat/public announcement
  sentinel** (always present, one row), the **member communities** again
  (for `community.post`), the **resident's groups** (the
  `IUserInfoService.GetGroupsForUserAsync` read seam — owner ∪ member,
  the single "my groups" read), and the **parent pages** the resident
  has a `page.child` subscription row for (the opt-IN kind has no
  default-on rows to surface; the list is driven by the resident's
  explicit rows, named from the live page forest via
  `IPageService.GetTreeAsync`). A **personal read** — the `[Authorize]`
  gate + the actor being the recipient is the whole decision; no audit
  row (D3 / F11). *Forbids:* a list that surfaces a community the
  resident is not a member of, a list that surfaces a group the resident
  does not own ∪ is not a member of, a list that surfaces a parent page
  the resident has no `page.child` row for (the opt-IN kind has no
  default-on rows), or an audit row on the read.
- **`POST /notifications/subscriptions`** — the toggle write: upserts
  the caller's `NotificationSubscription` row for the named (kind,
  target) (the `SetSubscriptionAsync` lane — the row is the record of the
  resident's last explicit choice, never deleted). A hand-crafted POST
  naming a kind outside the four per-target kinds, or an empty target,
  is **rejected (400)** — a client cannot mint a subscription the
  emitters don't consult. A **state lane** — no audit row (D3). *Forbids:*
  a POST that accepts a kind outside the four per-target kinds, a POST
  that accepts an empty target, a POST that deletes a row (the upsert is
  the only write), or an audit row on the write.

The two new view models (`SubscriptionRow` +
`NotificationSubscriptionsViewModel`) are added to
`Kumunita.Web.Models.NotificationViewModels`. The view
`Notifications/Subscriptions.cshtml` groups the rows by kind (one
`<h2>` per kind, one `<form>` per row with the `kind` / `targetId`
hidden inputs + the `enabled` checkbox), and the `_AccountNav.cshtml`
settings menu gains a link to the new surface. *Forbids:* a view that
does not group by kind, a form that does not carry the `kind` +
`targetId` hidden inputs, or a checkbox that does not post the `enabled`
value.

**D8 — The per-page subscribe toggle: `POST /pages/{id}/subscribe` +
the `PageShowViewModel.Subscribed` flag.** The `PageController` gains
one route + one view-model field:

- **`POST /pages/{id}/subscribe`** — the page's subscribe toggle: flips
  the caller's `NotificationSubscription` row for (kind `page.child`,
  target = **this page's id** — subscribing to a page means "notify me
  when a new page is added under it") to the opposite of its current
  effective state (a stored row's `Enabled` value when one exists, else
  the kind's opt-IN default — disabled — so the first toggle is always
  an explicit subscribe). The `SetSubscriptionAsync` upserts the row
  (the row is the record of the resident's last explicit choice, never
  deleted). A `[Authorize]`-gated **personal write** — the RecipientId
  is the whole access story; no audit row (D3 / F11). A hand-crafted POST
  on an absent page still flips the row (a subscription is the resident's
  own choice, not a page property — the toggle on a live page is the
  normal path; the redirect degrades to the page's `Index` when the
  page is not found). *Forbids:* a toggle that is not a personal write,
  a toggle that deletes a row, a toggle that does not redirect back to
  the page (the Show redirect shape — the page's path, not its id), or
  an audit row on the write.
- **`PageShowViewModel.Subscribed`** — a new positional `bool` on the
  `PageShowViewModel` record: the viewer's effective `page.child` choice
  for the page's own id (the `IsSubscriptionEnabledForAsync` read on the
  caller's session; defaults to `false` when the seam or the actor is
  absent). The `Views/Page/Show.cshtml` header gains a **Subscribe /
  Unsubscribe** button (the label + the button class are derived from
  the flag — `pages.subscribe` / `pages.unsubscribe` `kw-l` keys, the
  `btn-primary` / `btn-outline-primary` Bootstrap classes) in a
  `@if (User.Identity?.IsAuthenticated ?? false)` guard (the toggle is
  a personal write — an anonymous viewer does not see it). *Forbids:* a
  button that is visible to an anonymous viewer, a button that does not
  post to `POST /pages/{id}/subscribe`, a button that does not carry the
  anti-forgery token, or a label that is not one of the two `kw-l` keys.

**D9 — The `NotificationService` gains three public lanes (the frozen
surface is amended, not extended).** The `NotificationService` is a
**concrete sealed** class (no interface — the M5 `ProjectService`
precedent, the M6 design doc's frozen-surface rule). This ADR adds
three public methods to it (the ADR 0078 precedent for amending the
frozen surface with additive members — the 6-arg `EmitAsync` signature
is unchanged, the return is unchanged, the three new methods are
additive):

- **`GetSubscriptionsAsync(recipientId, ct)`** — the settings-page read:
  the recipient's `NotificationSubscription` rows, in `(Kind, TargetId)`
  order (the stable display order). A personal read — no audit row (D3).
- **`SetSubscriptionAsync(recipientId, kind, targetId, enabled, ct)`** —
  the upsert lane (D6). A state lane — no audit row (D3).
- **`IsSubscriptionEnabledForAsync(session, recipientId, kind, targetId,
  ct)`** — the gate (D3 step 0): resolves the recipient's effective
  choice for the named (kind, target) by combining the stored
  `NotificationSubscription` row (if any) with the kind's default from
  `NotificationKinds.OptInKinds`. A pure read — no write, no commit (the
  caller's transaction is unaffected). *Forbids:* a new
  `INotificationService` interface, a change to the 6-arg `EmitAsync`
  signature, a lane that is not a personal read (no
  `IAuthorizationService` call, no audit row), or a gate that is not
  consulted on the caller's session (the gate must see the same rows the
  emitter sees, in the same transaction).

**D10 — The three new kinds join the closed `KnownTranslationKeys`
registry, all four languages, the ADR 0015 parity test enforces the
delta.** Per kind the registry gains the email subject + body templates
(`notification.{kind}.subject` / `notification.{kind}.body` — the UGC
snippet is appended after one space, so the templates carry a trailing
space), the settings label (`notifications.kind.{kind}`), and the
preference toggle label (`notifications.preference.{kind}.label`). In
addition, the settings-page list adds the page-title + intro keys
(`notifications.subscriptions.title` / `notifications.subscriptions.intro`),
the per-kind section labels on the list
(`notifications.subscription.{kind}.label` for the three new kinds + the
existing `group.post`), and the per-page toggle labels
(`pages.subscribe` / `pages.unsubscribe`). That is **21 new keys × 4
languages = 84 entries** added to `KnownTranslationKeys`, and the
`KwLRegistryConsistencyTests` parity check (exact key-set equality across
the four dictionaries) is what enforces the count — a missing key in
*any* language fails the build, not a flake. *Forbids:* a key present
in fewer than four languages, a subject/body template that omits the
trailing-space snippet slot, a kind that appears in
`NotificationKinds.Known` without its keys registered, or a settings-page
label that is not one of the registered `kw-l` keys.

**D11 — Reuse, don't reinvent.** The M6 `NotificationService.EmitAsync`
seam (D1/D4/D5/D7), the M1 durable-email trio
(`IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler`
+ `EmailDeadLetterWriter`), the ADR 0061 per-recipient outbound-channel
language, the closed `KnownTranslationKeys` registry (ADR 0015), the
ADR 0078 sample-account suppression gate (inherited by the three new
kinds for free — the suppression check in `EmitAsync` is kind-agnostic),
and the `PagePaths.Href` / `PagePaths.Derive` seam (ADR 0039) are all
**frozen seams**. This ADR adds **three kind constants + three emitters
(both in the content lanes) + one new document (`NotificationSubscription`
on the `M6DocTypes` surface) + three new public lanes on the frozen
`NotificationService` + one 7-arg `EmitAsync` overload (the 6-arg is
unchanged) + two new Web routes (the settings list GET/POST) + one new
Web route (the per-page toggle) + one new view-model field
(`PageShowViewModel.Subscribed`) + one new view
(`Notifications/Subscriptions.cshtml`) + one new view-model
(`NotificationSubscriptionsViewModel`) + one `_AccountNav` link + one
`Page/Show.cshtml` button + 84 `kw-l` keys + one Core test file
(`NotificationServiceTests` F13–F23, 11 tests) + three Web test files
(`NotificationsControllerTests` S1–S3, 3 tests; `PageControllerTests`
`Subscribe_Post_Flips_Row_And_Redirects_To_Page`, 1 test; the
`NotificationsControllerTests` count assertion `13 → 16`) + one
`_AccountNav` link**, not a branch. **No new bounded context, no new
`M*DocTypes` surface (the `M6DocTypes` surface is amended, not extended),
no new `INotificationService` interface, no new `AccessAction`, no new
`AccessVia`, no new `IAuthorizableResource` adapter, no new email
mechanism, no new client dependency.** *Forbids:* a new authorization
path, a new email mechanism, a new UI dependency, a notification
mechanism outside M6's `EmitAsync`, a new bounded context, or a new
`M*DocTypes` surface.

## Consequences

- `NotificationKinds` now has **sixteen** kinds (thirteen + `announcement`
  + `community.post` + `page.child`); the settings toggles render three
  more rows; the `NotificationsControllerTests` count assertion moves
  `13 → 16`; and the `KnownTranslationKeys` registry gains 84 entries
  (21 keys × 4 languages) whose exact key-set parity across en/de/fr/da
  is enforced at build time. That is the whole kind-vocabulary delta.
- The `M6DocTypes` surface gains the `NotificationSubscription` document
  (the `(RecipientId, Kind, TargetId)` business key, a unique index). The
  `NotificationService` gains three public lanes
  (`GetSubscriptionsAsync` / `SetSubscriptionAsync` /
  `IsSubscriptionEnabledForAsync`) + a 7-arg `EmitAsync` overload (the
  6-arg is unchanged, the return is unchanged). The three new emitters
  sit on the `AnnouncementService` / `PostService` / `PageService` write
  seams, in the caller's transaction, recipient = the affected resident,
  target = the scope the resident subscribed to.
- A resident who is a member of a community now sees an inbox row (and,
  if they have an email and haven't disabled the kind, an email) for a
  new announcement or a new post in that community — **if they opted in**
  (for `announcement`) or **have not opted out** (for `community.post`).
  A resident who is a member of a group sees the same for a new post in
  that group (the `group.post` kind, re-gated on the per-target shape —
  the opt-OUT default is unchanged, so existing residents see no
  behavior change). A resident who subscribed to a parent page sees an
  inbox row for a new page under it (the `page.child` kind, opt-IN
  default — they must have explicitly subscribed).
- The settings page gains a new surface (`GET /notifications/subscriptions`
  + `POST /notifications/subscriptions`) listing every (kind, target)
  pair the resident can subscribe to, with the effective state (the
  stored row's `Enabled` when present, else the kind's default) — the
  same rule the emit gate uses, so the toggle the resident sees is
  exactly what the emitters consult. The per-page toggle
  (`POST /pages/{id}/subscribe`) is the one target where the "subscribe"
  affordance belongs on the target itself, not only in settings.
- The `NotificationsController` ctor gains two optional parameters
  (`IUserInfoService?` + `IPageService?`) for the display-name resolution
  on the settings list (the CS1736 idiom — the existing test-construction
  sites keep compiling; the subscriptions routes degrade to nameless rows
  when a seam is absent, a display gap, never a decision). The
  `PageController` ctor gains one optional parameter
  (`NotificationService?`) for the per-page toggle (the same CS1736
  idiom; DI always supplies the live service in the app).
- **Deferred, each named:** per-kind **sub-settings** (an email-only vs
  inbox-only split, ADR 0076's follow-on), **digests** (a daily / weekly
  summary of the resident's subscribed targets), **push / PWA push** (M9),
  **live update** (WebSocket / SSE), and **per-target preferences** (a
  resident-specific override of the kind's default, distinct from the
  per-target subscription) — each a follow-on lane with its own ADR.