# ADR 0139 — Per-resident messaging controls (opt-in + guardian's ceiling on the M9 lane)

Status: Accepted
Date: 2026-10-04

The M9 messaging surface (ADR 0105) ships an **instance-level** toggle
(`LocaleSettings.MessagingEnabled`, the GlobalAdmin's `/admin/messaging`
lane): off ⇒ no one may message, on ⇒ every signed-in resident may
message. This ADR adds the **per-resident** half of the control, on top of
the instance master gate:

- **The resident's own opt-in** (`Profile.MessagingOptIn`, default `false`
  — the ADR 0105 privacy-sensitive opt-in convention carried to the per-actor
  half): a checkbox on a new `/settings/messaging` tab (the ADR 0080 settings
  surface, the ADR 0019 time-zone / ADR 0020 date-format / ADR 0121 quiet-hours
  section idiom verbatim). The resident decides for themselves whether they
  participate in 1:1 messaging, under the instance's umbrella.
- **The guardian's ceiling** (`Profile.MessagingRestricted`, default
  `false`; written by a guardian with an active `GuardianLink` over the
  child): a hard veto over the child's own opt-in (the normal parental-
  restriction model — restrict, but the child can opt in up to the
  guardian's cap). Two buttons on the existing `/me/children/{childId}`
  curation view (the ADR 0028 Suspend/Unsuspend idiom verbatim — two
  POSTs, two routes, two data-confirms; the standing gate + the audit row
  are minted in the Core seam, not the Web layer).

The composition rule (the user's chosen model) is the load-bearing
invariant:

| Instance on | Resident opted in | Guardian restricted | Result |
|:---:|:---:|:---:|:---:|
| off   | off  | off  | **no** (master gate off) |
| off   | on   | off  | **no** (master gate off — the per-user opt-in is under the instance's umbrella, not an override) |
| on    | off  | off  | **no** (opt-in off) |
| on    | on   | off  | **yes** |
| on    | on   | on   | **no** (the ceiling wins over the child's own opt-in) |
| on    | off  | on   | **no** (the ceiling + the off opt-in agree) |

This ADR rides **frozen** seams, extending none of them:

- **The ADR 0105 instance-level toggle** — `IMessagingService
  .IsMessagingEnabledAsync` / `SetMessagingEnabledAsync` are
  **untouched**; the per-actor gate is a *read* seam (`IsMessagingAllowedForAsync`)
  composed **on top of** the master gate, not a replacement (C-1).
- **The ADR 0105 service contract** — the `OpenConversationAsync` /
  `SendAsync` / `GetConversationAsync` / `ListConversationsAsync` /
  `MarkReadAsync` seams are **untouched**; the per-actor gate is enforced
  at the **Web boundary** (the `MessagesController` actions + the account
  nav), not inside the service (the C-M9·2 "enforced in the service, never
  only the view" pin is *reinterpreted* for the per-actor half: the service
  is the internal composition seam, not an exposed endpoint; the Web
  controller is the app's real request surface — the same architectural
  split that already governs the instance-toggle enforcement).
- **The ADR 0028 guardian-controls standing** — the `SetChildMessaging-
  RestrictionAsync` write lane reuses the `GuardActiveLinkAsync` standing
  gate (G·2/G·3 deny-by-default) verbatim; the `guardian.messaging_restrict`
  audit row is the `guardian.suspend` / `guardian.unsuspend` shape carried
  to the messaging lane (the GU G·2 pin).
- **The ADR 0004 §B.1 additive-surface discipline** — both new fields
  (`Profile.MessagingOptIn`, `Profile.MessagingRestricted`) are *additive*
  to the existing `Profile` document; **zero migrations**, no re-seed, no
  new `*DocTypes` surface (C-2).
- **The ADR 0080 settings-surface-split** — the new `/settings/messaging`
  tab follows the ADR 0019 / ADR 0020 / ADR 0121 section idiom: a GET
  action that renders a section of a shared settings page, a POST save
  lane that redirects back to the section (C-3).

## Context

ADR 0105 (M9) already ships the instance-level messaging toggle (the
GlobalAdmin's `/admin/messaging` lane) + the 1:1 conversation / message
seams (the F1 idempotent pair-open, the D6 nudge, the D7 cap, the D8
read-state lane) + the C-M9·2 "enforced in the service, never only the
view" pin. The ADR 0028 GU surface already ships the guardian's standing
over a supervised child (the `GuardianLink` row, the G·2 suspension /
G·3 deny-by-default, the G·5 independence lane). What the M9 surface does
*not* have is the **per-resident** control: a way for a resident to opt
out of messaging without asking an admin to turn the instance off (which
would cut off *every* resident), and a way for a guardian to restrict a
child's messaging without the child's standing being the only voice.

The constraint that shapes the decision is the same one that shaped ADR
0105 + ADR 0028: **compose the frozen seams, never extend them.** The
per-actor gate is a *read* seam (the `IsMessagingAllowedForAsync`
composite) composed on top of the instance master gate (D1, D4); the
resident's opt-in is an *additive* `Profile` field + a *single-write*
owner-scope lane (the `CompleteOnboardingAsync` shape, D2, D3); the
guardian's ceiling is an *additive* `Profile` field + a *single-write*
guardian-scope lane (the `SuspendChildAsync` shape, D5, D6). The one rule
that keeps this safe (C-1, C-4): **the instance toggle is the master
gate; the per-actor gate is a *read* seam, enforced at the Web boundary,
never a new authorization surface** — the ADR 0105 service contract, the
ADR 0028 guardian standing, and the frozen `IMessagingService` are
byte-identical after this ADR.

## Decision

**D1 — The per-actor gate is a *read* seam, not a *write* seam.** The new
`IMessagingService.IsMessagingAllowedForAsync(string actorId)` is a
**read-only** composite of three existing states: (a) the instance master
gate (`IsMessagingEnabledAsync`), (b) the resident's own opt-in
(`Profile.MessagingOptIn`, via the frozen `IUserInfoService
.GetProfileAsync` read), and (c) the guardian's ceiling
(`Profile.MessagingRestricted`, same read). A missing profile (or a
harness without the directory seam) **fails closed** to "not allowed"
(the floor is "no messaging", the ADR 0105 opt-in default-`false`
convention carried to the per-actor half). *Forbids:* a write seam that
changes the instance toggle or the profile on a read; a default-open
drift (the floor is "off"); a new `AccessAction` or `IAuthorizationService`
call (the gate is a *read*, not an access decision — the C-M9·5
"no `IAuthorizationService`" pin carried to the per-actor half).

**D2 — The resident's own opt-in is an *additive* `Profile` field + a
*single-write* owner-scope lane.** The new `Profile.MessagingOptIn`
(bool, default `false`) is an additive field on the existing `Profile`
document (ADR 0004 §B.1 — delta-detected, idempotent, no re-seed, no EF
migration, no new `*DocTypes` surface). The write lane is the new
`IUserInfoService.SetMessagingOptInAsync(string subjectId, bool optIn,
string actorBy)` — the `CompleteOnboardingAsync` shape verbatim (the
`SetProfileTimezoneAsync` owner-scope single-write idiom: the self-scope
check is at the Web boundary, the owner is the actor, one
`SaveChangesAsync`, **no** `AccessAudit` row — "not an access decision").
Strong consistency (C4): the value is live on the very next
`GetProfileAsync` read. *Forbids:* load-or-create on a missing profile
(the `CompleteOnboardingAsync` fail-closed pin); an audit row (a
profile-field write is not an access decision); a new `Profile` document
or a `MessagingOptIn` table (the additive-field discipline).

**D3 — The resident's opt-in surface is a new `/settings/messaging` tab
(the ADR 0080 settings-surface-split idiom).** The new
`MessagingSettingsController` (a fresh controller, not an extension of
`LocaleController` — the `LocaleController` is pinned to its
`ILocalizationService` + `IUserInfoService` + `IDocumentStore` + optional
translation-provider/notifications constructor shape by
`PublicLocaleAndAboutTests`, `LocaleControllerQuietSectionTests`, and
`SettingsSectionSplitTests`, all of which construct it positionally, and
growing the constructor would ripple through those pins) owns the two
routes:
`GET /settings/messaging` (renders the toggle + the instance-off notice
+ the guardian-restricted notice) and `POST /settings/messaging` (writes
the opt-in, refuses when the ceiling is on). The `_SettingsTabs.cshtml`
strip gains a new tab row (the active-detection is controller-aware, the
`("MessagingSettings", "Index")` shape). *Forbids:* adding the toggle to
the `ProfileEditViewModel` (the `Has_Exactly_Nine_FormFields` pin
forbids a 10th field); a new `LocaleController` action (the test-
construction pins above); a new `*DocTypes` surface (the additive-field
discipline).

**D4 — The per-actor gate is enforced at the *Web boundary*, not inside
the service.** The `MessagesController`'s `Index` / `Open` / `Thread` /
`Send` actions each read `IsMessagingAllowedForAsync(actorId)` immediately
after the existing `IsMessagingEnabledAsync()` read (the F5 instance-toggle
gate is **preserved** — the existing `MessagesControllerTests` pins the
`IsMessagingEnabledAsync()` text in the controller + the nav, and the
`DidNotReceiveWithAnyArgs` assertions on the service calls); the nav
(`_AccountNav.cshtml`) renders the `/messages` link only when **both** the
instance toggle is on **and** the signed-in resident is per-actor allowed
(the existing `Messages_Nav_ToggleOff_EntryHidden` / `Messages_Nav_Toggle-
On_EntryPresent` view-source pins assert the `IsMessagingEnabledAsync()`
text is present, so the new gate is *added alongside*, not in place of).
The service's `OpenConversationAsync` / `SendAsync` keep their existing
`EnsureEnabledAsync()` backstop (the instance-toggle throw) but do **not**
add a per-actor gate inside the service (the C-M9·2 pin is *reinterpreted*
for the per-actor half: the service is the internal composition seam, not
an exposed endpoint; the Web controller is the app's real request surface
— the same architectural split that already governs the instance-toggle
enforcement). *Forbids:* a per-actor gate inside the service that fails
closed on a missing profile (the existing `MessagingServiceTests` pins
"no profiles planted" for the `alice`/`bob` stub users — the per-actor
gate would fail those 36+ tests); a new `AccessAction` or
`IAuthorizationService` call (the gate is a *read*, not an access
decision); a new `IMessagingService` method that *writes* the per-actor
state (the resident's opt-in is a `Profile` field, written by
`IUserInfoService`, not by `IMessagingService`).

**D5 — The guardian's ceiling is an *additive* `Profile` field + a
*single-write* guardian-scope lane.** The new `Profile
.MessagingRestricted` (bool, default `false`) is an additive field on the
existing `Profile` document (ADR 0004 §B.1 — same discipline as
`Profile.Blocked` — the GU G·2 "suspend sets the same flag" enforcement
parity). The write lane is the new
`IUserInfoService.SetChildMessagingRestrictionAsync(string childId, bool
restricted, string guardianId)` — the `SuspendChildAsync` shape verbatim
(the `GuardActiveLinkAsync` standing gate first, then the flag write, then
one `AccessAudit` row in the same session / one `SaveChangesAsync`). The
audit row is `guardian.messaging_restrict` / `TargetKind = "profile"` /
`TargetId = childId` / `Via = Guardian` / `Outcome = Allow` (the
`guardian.suspend` / `guardian.unsuspend` shape). The ceiling is a
*state*, not a *decision*: both directions — `restricted = true`
(restrict) and `restricted = false` (allow) — commit the **same**
`guardian.messaging_restrict` audit verb with the `restricted` value as
the state carried in the row (the GU G·2 "suspend/unsuspend" idiom, where
one audit lane covers both directions of the flag). *Forbids:* a ceiling
write on a (guardian, child) pair with no active link (the G·3 deny-by-
default pin); two separate audit verbs for the two directions
(`guardian.messaging_restrict` + `guardian.messaging_allow`) — the ceiling
is a *state* flip, one seam, one verb, the `SuspendChildAsync` /
`UnsuspendChildAsync` shape; a new `GuardianLink` field or a
`MessagingRestriction` table (the additive-field discipline — the ceiling
is on the *child's* profile, not on the *link*).

**D6 — The guardian's ceiling surface is two buttons on the existing
`/me/children/{childId}` curation view.** The new
`GuardianController.SetChildMessaging` POST action (the route
`me/children/{childId}/messaging`, the `[ValidateAntiForgeryToken]`
shape, the `Suspend` / `Unsuspend` idiom verbatim) reads the
`ActiveLinkAsync` standing gate first (a non-guardian is a 404, the ADR
0028 shape), then calls the D5 write lane. The `Detail.cshtml` view gains
a new section (the child's `MessagingRestricted` flag + the child's own
`MessagingOptIn` state, exposed on `ViewData` — the
`MembershipEditorModel` is a pinned 5-field record, the
`GuardianViewModelsTests.MembershipEditorModel_Is_Exact_Five_Field-
_Projection` pin forbids a 6th field; the repo's `_AudienceEditor` /
`SeedGrantPickerOptionsAsync` precedent for non-model view data) with two
conditional buttons (the `Allow messaging` / `Restrict messaging` pair,
the `Suspend` / `Unsuspend` two-button idiom verbatim, each with its own
`data-confirm`). *Forbids:* a checkbox (the Suspend/Unsuspend two-button
idiom is the existing pattern; a checkbox would require a hidden-field
trick to round-trip the boolean, the repo's `Quiet.cshtml` /
`Timezone.cshtml` / `DateFormat.cshtml` all use explicit two-state
controls); a new `MembershipEditorModel` field (the pin above); a
`MessagingRestricted` write on the *child's* own settings page (the
child's page is the *opt-in* lane, the *guardian's* page is the *ceiling*
lane — the two are distinct, the D2/D5 split).

**D7 — The instance-off notice + the guardian-restricted notice are
*presentation*, not *enforcement*.** When the instance master gate is off
(`IsMessagingEnabledAsync` returns `false`), the `/settings/messaging`
page still renders (so a resident can opt in in advance — the opt-in is
a meaningful choice to make, the master gate is the *presentation* of
"messaging is off right now, your opt-in will be respected when it's
back on"), and an informational `alert` surfaces the state. When the
guardian's ceiling is on (`MessagingRestricted` is `true`), the checkbox
is rendered `disabled` and a warning `alert` surfaces the state; the POST
is refused at the Web boundary (the D6 `SetChildMessaging` lane is the
*guardian's* lane, the *resident's* `Save` lane refuses the write when
the ceiling is on, the `TempData["error"]` + redirect shape, the
Suspend/Unsuspend user-presentable-error idiom). *Forbids:* hiding the
`/settings/messaging` page when the instance is off (the resident needs
to be able to reach the page to *set* the opt-in — the enforcement is at
the *gate read*, the D4 Web boundary, not at the *page render*); a
server-side 404 on the `Save` POST when the ceiling is on (the *user-
presentable error* is the shape, the `TempData["error"]` + redirect, the
Suspend/Unsuspend idiom — a 404 would leak "you're restricted" to a
non-guardian, the ADR 0028 G·3 "a non-guardian learns nothing" pin
carried to the messaging lane).

## Consequences

- **C-1 — The instance toggle is the master gate; the per-actor gate is
  a *read* seam, enforced at the Web boundary.** The ADR 0105 service
  contract (`OpenConversationAsync` / `SendAsync` / `GetConversationAsync`
  / `ListConversationsAsync` / `MarkReadAsync`), the ADR 0028 guardian
  standing, and the frozen `IMessagingService` are **byte-identical**
  after this ADR. The per-actor gate is a *read* of three existing states
  (the instance toggle + the two new `Profile` fields), composed at the
  Web boundary (the `MessagesController` actions + the account nav), not
  a new authorization surface. *Test:*
  `Kumunita.Core.Tests.MessagingPerActorGateTests` (the 10 composition
  pins — the master-gate veto, the opt-in floor, the ceiling veto, the
  allowance-lifts-the-veto, the fail-closed floor, the write-lane
  round-trips, the no-audit-row + no-stand pins);
  `Kumunita.Web.Tests.MessagesControllerTests` (the 5 existing F5 pins
  are **preserved** — the `IsMessagingEnabledAsync()` text is still in
  the controller + the nav; the 5 new per-actor pins — the disabled
  render, the 404, the disabled render, the nav view-source pin);
  `Kumunita.Web.Tests.MessagingSettingsControllerTests` (the 5
  `/settings/messaging` pins — the model shape, the opt-in write, the
  ceiling refuse); `Kumunita.Web.Tests.GuardianMessagingRestrictionTests`
  (the 3 `/me/children/{childId}/messaging` pins — the standing gate,
  the ceiling write + audit row, the round-trip).
- **C-2 — Two additive `Profile` fields, zero migrations.** The new
  `MessagingOptIn` (default `false`) + `MessagingRestricted` (default
  `false`) are additive to the existing `Profile` document (ADR 0004
  §B.1 — delta-detected, idempotent, no re-seed, no EF migration, no new
  `*DocTypes` surface). A fresh instance has both at the `false` floor:
  the opt-in is off (the ADR 0105 privacy-sensitive convention), the
  ceiling is off (no guardian to set it). *Test:* the
  `Kumunita.Core.Tests.MessagingPerActorGateTests`
  `SetMessagingOptIn_FlipsFlag_NoAuditRow_LiveOnNextRead` +
  `SetChildMessagingRestriction_FlipsCeiling_AuditedViaGuardian` pins
  (the round-trip + the floor).
- **C-3 — One new controller, one new settings tab, no new
  `*DocTypes` surface.** The new `MessagingSettingsController` (a fresh
  controller, not an extension of `LocaleController` — the
  test-construction pins above) + the `/settings/messaging` route + the
  `_SettingsTabs.cshtml` tab row are the only new Web surface. The new
  `GuardianController.SetChildMessaging` action is an additive method on
  an existing controller (the `Suspend` / `Unsuspend` idiom — the
  existing `GuardianAssignmentTests` pins are **preserved**). *Test:*
  the `Kumunita.Web.Tests.MessagesControllerTests`
  `Messages_Nav_PerActorGate_Wraps_Messages_Link` pin (the view-source
  shape); the `Kumunita.Web.Tests.SettingsSectionSplitTests` pins
  (the existing settings-surface shape is **preserved** — the new tab is
  additive, not a reflow).
- **C-4 — The ceiling is a *state*, not a *decision*.** The
  `SetChildMessagingRestrictionAsync` seam is the `SuspendChildAsync`
  shape verbatim (the standing gate + the flag write + one audit row in
  the same session / one `SaveChangesAsync`). The audit row is
  `guardian.messaging_restrict` / `Via: Guardian` (the
  `guardian.suspend` / `guardian.unsuspend` shape). The *allow* write is
  the *same* seam, the *same* audit verb — the ceiling is a *state* (the
  `restricted` value), not a *decision* (the *verb* is the same for
  both directions, the GU G·2 "suspend/unsuspend" idiom carried to the
  messaging lane). *Test:* the
  `Kumunita.Core.Tests.MessagingPerActorGateTests`
  `SetChildMessagingRestriction_FlipsCeiling_AuditedViaGuardian` pin
  (the audit row shape); the
  `Kumunita.Web.Tests.GuardianMessagingRestrictionTests`
  `SetChildMessaging_Guardian_RestrictedTrue_CeilingLive_AuditedViaGuardian`
  pin (the Web-boundary shape — the redirect, the ceiling live on the
  next read, the audit row).
- **C-5 — The existing `MessagesControllerTests` + the existing
  `MessagingServiceTests` + the existing `GuardianAssignmentTests` + the
  existing `SettingsSectionSplitTests` + the existing
  `GuardianViewModelsTests` pins are all **preserved** — no test was
  modified to accommodate the new behavior (except the
  `BuildMessaging` harness in `MessagesControllerTests`, which adds a
  default `IsMessagingAllowedForAsync().Returns(true)` stub so the
  existing F5 pins continue to exercise the instance-toggle gate
  without the new per-actor gate interfering). The new behavior is
  pinned in new test files (the 4 new test classes above).

## Drift guard

The one rule that keeps this amendment safe, in the ADR 0105 / ADR 0028
voice:

> **The instance toggle is the master gate; the per-actor gate is a
> *read* seam, enforced at the Web boundary; the ceiling is a *state*,
> not a *decision*; the two new `Profile` fields are additive; the
> frozen `IMessagingService` + the ADR 0028 guardian standing are
> byte-identical after this ADR.**

A drift pause fires if any of:

- the per-actor gate is moved *inside* the service (the
  `MessagingServiceTests` "no profiles planted" pins would fail — the
  `alice`/`bob` stub users have no profile, the fail-closed gate would
  refuse them);
- the instance toggle is removed as the master gate (the
  `MessagesControllerTests` F5 pins + the `MessagingServiceTests`
  `IsMessagingEnabled_FreshInstance_FloorsToFalse` pin would fail — the
  master gate is the load-bearing invariant, the per-actor gate is the
  *individual choice* under the instance's umbrella);
- the ceiling is written on the *link* instead of the *child's profile*
  (the `GuardianViewModelsTests` `MembershipEditorModel_Is_Exact_Five_
  Field_Projection` pin + the `ProfileEditViewModel_Has_Exactly_Nine_
  FormFields` pin would fail — the additive-field discipline);
- the `SetChildMessagingRestrictionAsync` seam writes on a (guardian,
  child) pair with no active link (the `GuardianAssignmentTests`
  `Assign_NonGuardian_Returns404` pin + the ADR 0028 G·3 "a non-
  guardian learns nothing" invariant would fail — the standing gate is
  the load-bearing invariant);
- a new `AccessAction` or `IAuthorizationService` call is added for the
  per-actor gate (the C-M9·5 "no `IAuthorizationService`" pin + the
  D1 "the gate is a *read*, not an access decision" invariant would
  fail — the gate is a *composition* of three existing states, not a
  new authorization surface);
- the `/settings/messaging` page is hidden when the instance is off
  (the `MessagingSettingsControllerTests` `Index_Renders_Model_With-
  InstanceEnabledAndOptIn` pin + the D7 "the page is always reachable,
  the enforcement is at the *gate read*" invariant would fail — the
  resident needs to be able to reach the page to *set* the opt-in);
- the ceiling is enforced by a server-side 404 on the `Save` POST (the
  `MessagingSettingsControllerTests` `Save_Restricted_Refuses_NoWrite`
  pin + the D7 "the *user-presentable error* is the shape" invariant
  would fail — a 404 would leak "you're restricted" to a non-guardian,
  the ADR 0028 G·3 pin).
