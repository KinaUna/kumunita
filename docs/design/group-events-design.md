---
id: design-group-events
title: "Group events (membership-scoped)"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0089-group-events-membership-lane.md
  - docs/adr/0013-group-posts-membership-lane.md
updated: 2026-11-02
---

# Group events — membership-scoped group events lane — design doc

> **Abstract:** the **group events** design — a **membership-scoped group events** lane: events addressed to a group through the group's membership (not the community audience), with the group's RSVP / reminder / ICS surface. A companion to ADR [0089](../adr/0089-group-events-membership-lane.md); invariants GE·1–GE·8, FACES GE1–GE13, the frozen C# of every seam, and the pinned tests. **Who is it for:** a developer touching the group-events lane; an agent that needs the membership-gate shape. **When to read the rest:** before touching the group-events surface, the membership gate, or the group-events tests.


Companion to **ADR [0089](../adr/0089-group-events-membership-lane.md)** ([`docs/adr/0089-group-events-membership-lane.md`](../adr/0089-group-events-membership-lane.md), the
authority on *why*) and the lane register
(`docs/plans-milestones/in-progress/group-events/plan-group-events.md`, the
authority on *what, in what order*). This doc is the authority on **exact
shape**: the invariants **GE·1–GE·8**, the FACES **GE1–GE13**, the frozen C#
of every seam, and the pinned seam-test names. If a unit's reads reveal a
mismatch between this doc and the code, the unit pauses (`## U<m> — Drift
pause`), per the group-posts U1 drift-guard.

## Context

M4 (ADR [0054](../adr/0054-events-rsvp-reminders.md)) shipped a full event surface — a `ComponentId` feed organizer,
an `Audience` boundary, drafts, RSVPs, day-before reminders, and user-added
translations — but **every event lives on the community surface.** A group
(ADR [0010](../adr/0010-private-groups.md)/0013) has a *posts* channel (a private channel, ADR [0013](../adr/0013-group-posts-membership-lane.md)) but no
*events* surface: a group's page shows posts and members, and the "create an
event" affordance only ever reaches the community feed. `how-it-works.md`
already promises "when membership changes, all your past posts reach exactly
the right people"; a group's events should carry the same membership
guarantee. ADR [0013](../adr/0013-group-posts-membership-lane.md)'s membership lane is the proven answer, and the M4 event
surface is the proven *target*. Group events = **the ADR [0013](../adr/0013-group-posts-membership-lane.md) membership
lane applied to the M4 event surface**, reusing the frozen group seams
verbatim (zero new authorization surface).

## Scope

**In:**
- `Event.GroupId` (`string`, default `string.Empty`) — one additive, ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1, the `Post.GroupId` precedent; **no new index** (the `Post.GroupId` precedent has none).
- The four group-event seams on `IEventService` (`ListGroupEventsAsync`, `GetGroupEventAsync`, `CreateGroupEventAsync`, `UpdateGroupEventAsync`) — ADR [0006](../adr/0006-module-boundary-contracts.md)-E compatible ADDs; **publish reuses the existing lane-neutral `PublishAsync`**, RSVP reuses `RsvpAsync`/`GetMyRsvpAsync`/`GetRsvpsAsync`, and the ADR [0059](../adr/0059-event-translations.md) `EventTranslation` seams are reused as-is (all keyed by `EventId`, no group branch).
- The **explicit** `GroupId == string.Empty` filter added to the existing `ListUpcomingAsync` / `ListInRangeAsync` candidate predicates (service-internal, no signature change — the one change that keeps group events off the community feed/calendar).
- `GroupEventDraft` / `GroupEventUpdate` records (types, not seams); `GroupEventFeedResult` record.
- Web: a group-page **events channel** — an events section on the group detail (feed + "New event"), and routes mirroring the group-post routes (composer, detail, edit, publish, RSVP) under `/groups/{id}/events`; a non-member gets a 404.
- `kw-l` strings in **all four** active languages (`en`, `de`, `fr`, `da`) — the ADR [0005](../adr/0005-multilingual-support.md) "all keys in every language" rule; no `?? "fallback"` escape hatch.
- The pinned seam tests (`Kumunita.Core.Tests/GroupEventServiceTests.cs`) + the Web controller 404 tests; the recorded acceptance gate.
- **ADR [0089](../adr/0089-group-events-membership-lane.md).**

**Out (deferred, each named — see ADR [0089](../adr/0089-group-events-membership-lane.md) Consequences):**
- Soft-delete on group events (the community lane's `DeleteAsync` is not mirrored on the group lane in this iteration).
- A group-scoped calendar (`EV-CAL` stays community-scoped).
- A cross-group "my group events" surface (`ListMineAsync` stays community-scoped).
- Notifications on *new* group events (M6-scope, like the group-post deferral).

**Explicitly NOT available (privacy-first — recorded in ADR [0089](../adr/0089-group-events-membership-lane.md), *not*
deferrals):** break-glass on group events; moderator peek; non-member
authoring; GlobalAdmin edit/publish override on group events. Changing any
requires an ADR [0089](../adr/0089-group-events-membership-lane.md) amendment.

## Invariants (pinned for group events)

- **GE·1 — membership is the sole decision (C4, strong consistency).** A group
  event's visibility = `Event.GroupId` membership, resolved by the frozen
  group-lane seams (`CanSeeGroupAsync` / `CanSeeGroupFeedAsync`, live
  `IUserInfoService.GetGroupIdsAsync` read). A membership add/remove re-scopes
  the next feed/detail. The **audience lane is never evaluated** for a group
  event; its `Audience` is written non-null **empty** (GE·8).
- **GE·2 — lane exclusivity.** One event is either a community feed entry
  **or** a group-channel event. A group event has `GroupId` non-empty,
  `ComponentId = string.Empty`, and is excluded from `ListUpcomingAsync` /
  `ListInRangeAsync` by the **explicit** `GroupId == string.Empty` candidate
  filter. A community event never carries a `GroupId`.
- **GE·3 — members-only authoring.** The create gate **is** the group-lane
  decision (Allow ⇒ may create; Deny ⇒ the gate's audit row is committed
  **before** `throw UnauthorizedAccessException` so it survives; Web renders
  **404**). The owner creates **as** a member — no owner-skip.
- **GE·4 — nobody peeks; author-only write lanes.** No break-glass, no
  moderator branch, no GlobalAdmin override (C5 / ADR [0003](../adr/0003-roles-and-moderator-scoping.md) / G·4 strictest
  reading). Edit and publish are **author-only** (the ADR [0016](../adr/0016-group-post-and-reply-edit-lane-author-only.md) / ADR [0037](../adr/0037-draft-mode-author-only.md)
  group-lane precedent): sole decision `AuthorId == actorId`; a non-author is
  denied (`UnauthorizedAccessException` → Web 404).
- **GE·5 — audit per lane, always on, in-transaction (C3).** Feed = **one
  aggregate** `AccessAudit` row via `CanSeeGroupFeedAsync` (TargetKind
  `"grouppost"` — the frozen seam's existing discriminator, reused — TargetId
  null, counts); detail = **one decision** row via `CanSeeGroupAsync`
  (TargetId = the event id); the create gate = one group-lane decision row
  (TargetId = the group id). **Allow and Deny** are both audited; the
  `IDocumentSession` overloads commit inside the caller's transaction. The
  write lanes (create/edit) additionally store their own `event.create`-style
  row via `EventService.StoreAuditRow` (TargetKind `"event"`, `Via Owner`) —
  mirroring the community create.
- **GE·6 — delegation is action-scoped (C2).** An in-scope `read` grant lets
  the delegate act with the **owner's** standing (they see the group's events
  iff the **owner** is a member), `Via = Delegation`; an out-of-scope grant
  still denies. Reused from the ADR [0013](../adr/0013-group-posts-membership-lane.md) `DecideGroupAsync` — **no new**
  authorization surface.
- **GE·7 — RSVP + reminders are membership-inherited, not re-derived.** The
  existing `RsvpAsync` / `GetMyRsvpAsync` / `GetRsvpsAsync` are reused as-is
  (a group event is a member, so their RSVPs are the truth); the §6.4
  reminder job already runs over `ReminderEnabled && !IsDraft && !IsDeleted`
  events and the Going-RSVP set, so group events are reminded **with no
  change** (the recipient set is membership-derived by construction). No new
  reminder surface, no new notification kind, no audit row (a side effect, not
  an access decision — ADR [0054](../adr/0054-events-rsvp-reminders.md) §3.6).
- **GE·8 — a group event's write shape is pinned.** `CreateGroupEventAsync`
  writes `GroupId` (the lane marker), `ComponentId = string.Empty`,
  `Audience = new Audience()` (non-null empty — grantable-by-nothing, GE·1).
  `EventService`'s constructor is **unchanged** (ADR [0006](../adr/0006-module-boundary-contracts.md)-D: it calls the
  frozen group seams and never reads `GroupMembership`/`DelegationGrant`
  itself).

## FACES (pinned, 13)

| F | Statement | Invariants |
|---|---|---|
| GE1 | a member sees the group events feed (aggregate row) | GE·1, GE·5 |
| GE2 | a non-member gets the empty feed + a Deny row | GE·1, GE·5 |
| GE3 | a member added after an event sees it on the next feed (C4) | GE·1, C4 |
| GE4 | a member removed after an event loses it on the next detail (C4) | GE·1, C4 |
| GE5 | a member creates a group event and sees it; `ComponentId`/`Audience` pinned | GE·3, GE·8 |
| GE6 | a non-member's create is denied (Deny row survives the throw; Web 404) | GE·3 |
| GE7 | a non-member moderator cannot see group events | GE·4 |
| GE8 | a non-member GlobalAdmin cannot see group events (break-glass N/A) | GE·4 |
| GE9 | a delegate with `read` in scope sees the owner's group events (Via Delegation) | GE·6, C2 |
| GE10 | a delegate without `read` in scope sees nothing | GE·6, C2 |
| GE11 | a non-author (member) cannot edit a group event (author-only) | GE·4 |
| GE12 | a group event never appears in the community `ListUpcomingAsync` | GE·2 |
| GE13 | a group event never appears in the community `ListInRangeAsync` (calendar) | GE·2 |

## Seams (exact C# — U04 freezes, U07 implements)

### `Event` POCO (one additive)

```csharp
/// <summary>
/// Group lane marker (ADR 0089 GE·2/GE·8): non-empty ⇒ a group-channel event —
/// visible to the group's current members only (the ADR 0013 membership lane),
/// with a pinned <see cref="ComponentId"/> of <see cref="string.Empty"/> and a
/// non-null empty <see cref="Audience"/>. Written only by
/// <c>EventService.CreateGroupEventAsync</c>. Community events leave it
/// <see cref="string.Empty"/>. No new index (the <c>Post.GroupId</c> precedent,
/// ADR 0089 Decision). ADR 0004 §B.1 additive (delta-detected, idempotent).
/// </summary>
public string GroupId { get; set; } = string.Empty;
```

### New records (types, not seams) — `Kumunita.Core.Events`

```csharp
/// <summary>The feed/result shape of a group events feed (mirrors Posts.FeedResult).</summary>
public sealed record GroupEventFeedResult(IReadOnlyList<Event> Visible, int HiddenCount, int Page, int Total);

/// <summary>
/// A group event composition (mirrors Posts.GroupPostDraft). Written only by
/// EventService.CreateGroupEventAsync (GE·8): pins GroupId, ComponentId =
/// string.Empty, Audience = new Audience(). Nullable defaults, not collection
/// expressions (CS1736).
/// </summary>
public sealed record GroupEventDraft(
    string GroupId,
    string Title,
    string Body,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Location = null,
    int? Capacity = null,
    string? Color = null,
    string? LanguageCode = null,
    bool? ReminderEnabled = null);

/// <summary>An author-only edit on a group event (reuses the M4 UpdateEventRequest shape; no lane fields).</summary>
public sealed record GroupEventUpdate(
    string Title,
    string Body,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? Location = null,
    int? Capacity = null,
    string? Color = null,
    string? LanguageCode = null,
    bool? ReminderEnabled = null);
```

### `IEventService` ADDs (frozen M4 signatures untouched — ADR [0006](../adr/0006-module-boundary-contracts.md)-E lane)

```csharp
// ── Group events lane (ADR 0089) — members see via the frozen group seams; non-member ⇒ null / empty ──

/// <summary>The group's upcoming published events, membership-scoped (GE·1/GE·5). 0 candidates ⇒ empty, no row.</summary>
Task<GroupEventFeedResult> ListGroupEventsAsync(string groupId, string actorId, int page, CancellationToken ct = default);

/// <summary>One group event, fail-closed (null for non-member / wrong group / draft-non-author); one CanSeeGroupAsync decision row (GE·1/GE·4/GE·5).</summary>
Task<Event?> GetGroupEventAsync(string groupId, string eventId, string actorId, CancellationToken ct = default);

/// <summary>Create gate = the membership decision (GE·3); the Deny row is committed before the throw (C3); pins the write shape (GE·8); caller's transaction.</summary>
Task<Event> CreateGroupEventAsync(GroupEventDraft draft, string actorId, IDocumentSession session, CancellationToken ct = default);

/// <summary>Author-only edit (GE·4); no audit row (the ADR 0016 precedent); GroupId/ComponentId/Audience/AuthorId/Created/IsDraft/IsDeleted untouched; caller's transaction.</summary>
Task<Event> UpdateGroupEventAsync(string eventId, string actorId, GroupEventUpdate update, IDocumentSession session, CancellationToken ct = default);
// Publish: REUSE the existing EventService.PublishAsync (already author-only, idempotent, lane-neutral).
// RSVP  : REUSE the existing RsvpAsync / GetMyRsvpAsync / GetRsvpsAsync (lane-neutral, keyed by EventId).
// Trans : REUSE the existing ADR 0059 EventTranslation seams (keyed by EventId, no group branch).
```

### `EventService` internal changes

```csharp
// ListUpcomingAsync / ListInRangeAsync candidate predicates GAIN:
//     ... && e.GroupId == string.Empty
// (GE·2: the one change that keeps group events off the community feed/calendar —
//  no signature change; a community event's GroupId is string.Empty.)

// New private group-lane methods (mirror PostService's group lanes; reuse _authorization,
// _userInfo-free — the group seams own every membership read):
//   GroupEventFeedResult ListGroupEventsAsync(...)     // candidates GroupId == groupId && !IsDeleted && !IsDraft; Start asc; 1 CanSeeGroupFeedAsync
//   Event? GetGroupEventAsync(...)                     // fail-closed; ADR 0037 draft gate BEFORE membership; 1 CanSeeGroupAsync
//   Event CreateGroupEventAsync(...)                   // gate in caller tx; Deny survives; pins GroupId/ComponentId/Audience; 1 StoreAuditRow("event.create")
//   Event UpdateGroupEventAsync(...)                   // author-only; no audit row; Modified stamped; lane fields untouched
```

## Pinned seam tests (`tests/Kumunita.Core.Tests/GroupEventServiceTests.cs`)

Mirror `GroupPostServiceTests.cs` (same harness shape; the group seams are the
same frozen methods, so the member/non-member/delegate cases are the same).
The Web 404 tests live in `Kumunita.Web.Tests` (the `GroupsController` group
actions).

1. `GE1_MemberSeesGroupEventsFeed`
2. `GE2_NonMemberFeedEmptyWithDenyRow`
3. `GE3_MembershipAddReScopesNextFeed`
4. `GE4_MembershipRemoveRevokesNextDetail`
5. `GE5_MemberCreatesGroupEventSeesIt`
6. `GE5_GroupEventComponentIdAndAudiencePinned`
7. `GE6_NonMemberCreateDenied_DenyRowSurvives`
8. `GE7_ModeratorNonMemberDenied`
9. `GE8_BreakGlassDoesNotApplyToGroupEvents`
10. `GE9_DelegateWithReadInScopeSeesOwnerGroupEvents`
11. `GE10_DelegateWithoutReadDenied`
12. `GE11_NonAuthorMemberCannotEditGroupEvent`
13. `GE12_GroupEventExcludedFromCommunityListUpcoming`
14. `GE13_GroupEventExcludedFromCommunityListInRange`
15. `Feed_AggregateAuditRowShape_GroupEvent`
16. `Detail_DecisionAuditRowShape_ViaGroup`
17. `Detail_DecisionAuditRowShape_ViaDelegation`
18. `Draft_NonAuthorSeesNothing_GetGroupEvent` (the ADR [0037](../adr/0037-draft-mode-author-only.md) draft gate, before membership)
19. `EventService_MakesNoModerateOrBreakGlassCallOnGroupEvents`

Acceptance gate: **closed loop** (a member creates a group event → it lands in
their group feed; aggregate row `visibleCount ≥ 1`); **handoff** (a member
added *after* the event sees it on the next feed — strong consistency; the
delegate-with-`read` branch is the handoff case); **part-vs-whole** (the 19
seam tests pass together with `Kumunita.Core.Tests`).

## Group events — Gate (recorded)

**Date:** 2026-09-25 · **Machine:** Windows (PowerShell terminal). Recorded
against the AGENTS.md reliable runner (`dotnet build Kumunita.slnx -c Debug`
then `dotnet exec <assembly>.dll` — **not** `dotnet test` / VS Test Explorer).

| # | Test | Shape (what it proves) | Pinned to |
|---|------|------------------------|-----------|
| 1 | **closed loop** | a member creates a group event → it appears in **their** group feed; the feed's aggregate `AccessAudit` row exists: `TargetKind = "grouppost"`, `TargetId = null`, `VisibleCount ≥ 1`, `HiddenCount = 0`, `Outcome = Allow` (GE·5) | `GroupEventServiceTests.GE5_MemberCreatesGroupEventSeesIt` (create-then-see round-trip: `ComponentId = string.Empty`, `GroupId = draft.GroupId`, the event present in the member's next `ListGroupEventsAsync`) + `GroupEventServiceTests.Feed_AggregateAuditRowShape_GroupEvent` (the feed's *single* aggregate row — the closed-loop's `VisibleCount ≥ 1` / `HiddenCount = 0` shape) |
| 2 | **handoff** | a member added **after** the event sees it on the **next** feed — strong consistency (C4); the in-scope delegated `read` branch is the handoff onto a delegate (the *same* pin) | `GroupEventServiceTests.GE3_MembershipAddReScopesNextFeed` (C4 — the `GroupMembership` row planted *after* the create re-scopes the *very next* `ListGroupEventsAsync` via the live `GetGroupIdsAsync` read; no projection lag) + `GroupEventServiceTests.GE9_DelegateWithReadInScopeSeesOwnerGroupEvents` (GE·6/C2 — the in-scope `read` grant acts with the *owner's* membership; decision row `Via = Delegation`, `EffectivePrincipalId = owner`) |
| 3 | **part-vs-whole** | the 19 §seams names are the **whole**; gates 1–2 are the **parts**; all must pass **together** in the same `Kumunita.Core.Tests` run as the inherited anchors | all 19 `GroupEventServiceTests` `[Fact]`s — `GE1_MemberSeesGroupEventsFeed` · `GE2_NonMemberFeedEmptyWithDenyRow` · `GE3_MembershipAddReScopesNextFeed` · `GE4_MembershipRemoveRevokesNextDetail` · `GE5_MemberCreatesGroupEventSeesIt` · `GE5_GroupEventComponentIdAndAudiencePinned` · `GE6_NonMemberCreateDenied_DenyRowSurvives` · `GE7_ModeratorNonMemberDenied` · `GE8_BreakGlassDoesNotApplyToGroupEvents` · `GE9_DelegateWithReadInScopeSeesOwnerGroupEvents` · `GE10_DelegateWithoutReadDenied` · `GE11_NonAuthorMemberCannotEditGroupEvent` · `GE12_GroupEventExcludedFromCommunityListUpcoming` · `GE13_GroupEventExcludedFromCommunityListInRange` · `Feed_AggregateAuditRowShape_GroupEvent` · `Detail_DecisionAuditRowShape_ViaGroup` · `Detail_DecisionAuditRowShape_ViaDelegation` · `Draft_NonAuthorSeesNothing_GetGroupEvent` · `EventService_MakesNoModerateOrBreakGlassCallOnGroupEvents` — green **in the same execution** as the M1/M2/M3/M3b/media/M4 inherited suites — **`Kumunita.Core.Tests` 851/851 passed, 0 failed** in this run; `Kumunita.Web.Tests` 466/466 in its own run (incl. the 13 `GroupsControllerGroupEventTests` 404 tests). |

## Group events — Closed (recorded)

**Date:** 2026-09-25 · **Single-agent run** (no per-unit handoff note — one
sealed plan register, `docs/archive/lanes/group-events/`). The gate
record above (`## Group events — Gate (recorded)`) is the milestone's last
line; this is its close marker. No new tests here — the checklist below is the
entry reads, each line re-verified at close.

### Consistency checklist (each line PASS/FAIL)

| Line | Check | Result |
|---|---|---|
| **seams** | `Event.GroupId` present (U04) — `src/Kumunita.Core/Events/Event.cs`, `public string GroupId { get; set; } = string.Empty;` · the four `IEventService` ADDs (U04) — `ListGroupEventsAsync` / `GetGroupEventAsync` / `CreateGroupEventAsync` / `UpdateGroupEventAsync`, implemented in `EventService.cs` (ctor unchanged) · `GroupEventFeedResult` / `GroupEventDraft` / `GroupEventUpdate` (U04) — `src/Kumunita.Core/Events/GroupEventDraft.cs` · the `ListUpcomingAsync` / `ListInRangeAsync` `GroupId == string.Empty` filter (U04) · every symbol grep'd, each named with its file, **all present** | **PASS** |
| **tests** | the 19 seam-test names all present in `tests/Kumunita.Core.Tests/GroupEventServiceTests.cs`, 19 `[Fact]`s, none missing / none extra vs §seams · the 13 `GroupsControllerGroupEventTests` 404 tests present in `tests/Kumunita.Web.Tests/GroupsControllerGroupEventTests.cs` · the U08 gate line (Core 851/851 + Web 466/466) still **PASS** per the recorded section above | **PASS** |
| **close docs** | [`README.md`](../../README.md) — the **Group events** Features bullet + the Roadmap **Group events (`GE`, ADR [0089](../adr/0089-group-events-membership-lane.md))** lane bullet (a shipped non-lettered lane lives in both; M4/M5/M6 Roadmap lines untouched) · [`docs/adr/README.md`](../adr/README.md) — the **0089** register row (`Accepted`) · `src/Kumunita.Web/Milestones.cs` **untouched** (a named lane, not a milestone — the ADR [0086](../adr/0086-goals-projects-lane.md) `PL` / ADR [0088](../adr/0088-projects-todos-boards-translations.md) precedent) | **PASS** |
| **kw-l** | the nine `groups.*` `kw-l` keys present in `en`/`de`/`fr`/`da` (the ADR [0005](../adr/0005-multilingual-support.md) "all keys in every language" rule) — `groups.back` · `groups.events_heading` · `groups.new_event` · `groups.events_empty_can` · `groups.events_empty` · `groups.new_event_title` · `groups.new_event_lead` · `groups.new_event_submit` · `groups.edit_event_title` | **PASS** |
| **folders** | [`docs/archive/lanes/group-events/plan-group-events.md`](../archive/lanes/group-events/plan-group-events.md) present (moved from `in-progress/`) · the design doc's gate + close sections recorded above | **PASS** |
| **drift** | no drift pause recorded — the 19 pinned names landed verbatim (master verified 1:1, no renames, count 19 ✓) — **clean** | **PASS** |

**Outcome: all six lines PASS — no hard failure. The group-events lane is
closed.** The `## Group events — Gate` line above is the recorded run
(2026-09-25, ALL THREE PASS); this section is its close marker.

---

## ADR [0005](../adr/0005-multilingual-support.md) — moved content (D11 trim, 2026-11-02)

## Revisit when

- A community actually needs MT of UGC — then decide C as a **new trust
  boundary**, not a feature flag, and update SECURITY.md §5/§6.
- Federation arrives (ADR [0001](../adr/0001-core-stack-and-identity-model.md)-B): the *platform* language catalog may move
  with the IdP, but per-instance languages and translations stay local.


## Amendments

### 2026-09-18 — bundled initial language pack (ADR [0042](../adr/0042-bundled-initial-language-pack.md))

§B's "every other language is community-provided" is amended: **`de` and
`fr` additionally ship as bundled initial values** — the pristine-boot
seeder (the `IsPristineAsync` gate, ADR [0015](../adr/0015-ui-view-localization-mechanics.md)) materializes the `de` / `fr`
catalog rows, the full UI-string baselines from the code's
`KnownTranslationKeys` registries, and the `de` / `fr` bodies of the seeded
`terms` / `help` system pages. They are **seeded once, then community-
owned** — an in-app edit (GlobalAdmin ∪ Translator, ADR [0021](../adr/0021-translator-role-delegated-translation-editing.md)) is never
overwritten by a later deploy. **`en` remains the only code-owned language**
(code-wins upsert + provider floor, ADR [0015](../adr/0015-ui-view-localization-mechanics.md) D1/D2, unchanged). See
**ADR [0042](../adr/0042-bundled-initial-language-pack.md)** for the full locked text (ownership semantics, the register
choice, the `about`-via-registry decision, the `about.*` key contract).

### 2026-09-18 — system pages shipped + discoverable (ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md))

§B's **static-page coverage** scope is widened: on a fresh instance the
seeded platform-page set is now the **five-surface set** — the `about`
product-story **view** (not a `Page` doc, the ADR [0039](../adr/0039-pages-hierarchy-audience-translations.md) U05 drift pin carried
forward) plus the four `Page`-backed surfaces **Terms**, **Help**, **Privacy
Policy**, and **Code of Conduct** (the last two new in ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md), D1) — each
of the four `Page` docs carrying `en` / `de` / `fr` bodies. The `de` / `fr`
bodies of the two new pages follow the **same** ADR [0042](../adr/0042-bundled-initial-language-pack.md) D1 ownership
semantics (seeded once, then community-owned; `en` code-owned). The two new
routes (`/privacy` / `/conduct`) join the hard-coded static set with the 404
floor (ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md) D2). See **ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md)** for the full locked text (the
five-surface set, the route contract, the ownership re-statement, the
discoverability decision, the content scope, and the unchanged standing
matrix).
### 2026-09-19 — UGC default variant selection (ADR [0049](../adr/0049-default-language-variant-selection.md))

§C's "never automatic" clause is **re-scoped**: the default-*visible*
variant on the post/reply/announcement detail surfaces and the
pinned-announcement banner is now the **viewer's current language**
(§B's resolution order, as amended by ADR [0046](../adr/0046-default-language-browser-accept-language-match.md)) **when a human-added
translation of the item exists in it** — otherwise the authored-in
variant remains default-visible (the ADR [0027](../adr/0027-post-reply-translation-display-and-swap.md) floor). This is a
display-only *default selection* of content that already exists in the
DOM; it is not, and does not enable, machine translation: the
deferred-MT clause, the no-third-party-send rule, and the "never
silently translated" guarantee for *generated* text are all unchanged.
See **ADR [0049](../adr/0049-default-language-variant-selection.md)** for the full locked text (the surface list, the
resolution chain it reuses, the ADR [0027](../adr/0027-post-reply-translation-display-and-swap.md) swap/return path that stays
touched, and the out-of-scope surfaces).
