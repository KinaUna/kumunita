---
id: adr-0083
title: "Group membership notifications: notify the resident when they are added to a group or invited to join one (kinds `group.added` / `group.invite`), and the tabbed group detail page"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/group-join-request-lane.md]
supersedes: [adr-0007]
superseded_by: []
updated: 2026-11-02
---

# ADR 0083 — Group membership notifications: notify the resident when they are added to a group or invited to join one (kinds `group.added` / `group.invite`), and the tabbed group detail page

Status: Accepted | Date: 2026-09-20

> **Abstract:** two new `NotificationKinds`
> (`group.added` / `group.invite`) join the closed M6
> `NotificationKinds` vocabulary (ADR 0076 D2), **reusing verbatim** the
> `NotificationService.EmitAsync` seam, the frozen `IMailerStage.StageAsync`
> trio, and the ADR 0061 per-recipient language resolution — **no** new
> context, **no** new document, **no** new surface, **no** new service.
> The `UserInfoService` primary-ctor gains a **trailing, optional**
> `IServiceProvider? services = null` (the circular-dependency seam, the
> 181 positional `UserInfoService(store)` test call sites are **unchanged**
> — they get `services = null` → no emission, the pre-M6 behavior). The
> notification is a **personal read** on the recipient's own inbox (no
> authorization, no audit row — the *domain* write already emits its own
> `AccessAudit` row). Email is **best-effort**; the inbox is the durable
> record. The two new kinds join the closed `KnownTranslationKeys` registry
> (8 keys × 4 languages = 32 entries, the `KwLRegistryConsistencyTests`
> parity check enforces the count). The group detail page becomes **three
> tabs** (Feed, Members, Settings) — server-rendered, no new client JS.
> **For** a developer touching the `NotificationKinds` vocabulary, the
> `UserInfoService` emitters, the `KnownTranslationKeys` registry, the
> group detail view, or the `tests/*` group-notification families. The
> Amends ledger, the circular-dependency seam detail, the kw-l key table,
> and the tab layout detail are in the [handoff
> note](../design/adr-handoffs.md#adr-0083--group-membership-notifications-moved-detail).

## Context

The M6 notifications context (ADR 0076) shipped eleven kinds. ADR 0076 D2
reserved the `post.mention` kind and left the *membership-change* kinds
(`group.added` / `group.invite`) as a **follow-on lane** (ADR 0076's
"deferred to their own lanes" posture). This ADR is that follow-on: the
*membership-change* kinds (not the *content* kind — the `group.post` kind
was settled by ADR 0076 D2 + wired emitter). The *natural* shape is to
**add two kinds** to the closed `NotificationKinds` vocabulary,
**reuse verbatim** the `EmitAsync` seam, the `IMailerStage.StageAsync`
trio, and the ADR 0061 per-recipient language resolution — **not** to
invent a new context, a new document, a new surface, or a new service. The
group detail page's sections (Feed / Members / Settings) are already
shipped (ADR 0007/0008/0010/0013/0026); this ADR **restructures** them into
the Bootstrap `nav-tabs` idiom (the ADR 0062 `_ProjectsTabs.cshtml`
precedent) — a **view restructure**, not a new surface.

## Decision

- **D1 — Two new kinds join the closed `NotificationKinds` vocabulary.**
  `group.added` (the resident was *added* to a group) and
  `group.invite` (the resident was *invited* to join a group). The
  `EmitAsync` seam (the M6 D1/D4/D5/D7 frozen shape) is **reused
  verbatim** — the two emitters sit on the two `UserInfoService` write
  seams that ADR 0007's SoD gate protects (the membership upsert / the
  invitation write). The self-leave seam (`RemoveGroupMemberAsync`,
  ADR 0008) deliberately does **not** emit (a self-leave is not something
  to be notified *about*). See the handoff note for the Amends ledger.
- **D2 — The `UserInfoService` primary-ctor gains a trailing, optional
  `IServiceProvider? services = null`.** The two emitters resolve the
  `NotificationService` **at emission time** via
  `services?.GetService<Notifications.NotificationService>()` — deferred
  to runtime, which breaks the construction-time cycle (the
  `NotificationService` constructor takes `IUserInfoService`, so injecting
  a `NotificationService?` **directly** into `UserInfoService` would
  create a cycle). The `null` default preserves the **181 pre-ADR 0083
  direct-construction test call sites** (they get `services = null` → no
  emission, the pre-M6 behavior). See the handoff note for the full
  circular-dependency seam detail.
- **D3 — The notification is a *personal read* on the recipient's own
  inbox — no authorization, no audit.** A `group.added` / `group.invite`
  row is **the recipient's own data** (the `RecipientId` is the whole
  access story). **No** audit row is emitted for the notification itself —
  the *domain* write already emits its own `AccessAudit` row (the
  membership upsert / the invitation write, ADR 0007's SoD derivation).
  *Forbids:* a new `AccessAction`, a new `AccessVia`, a
  `NotificationToAuditableResource` adapter, or an audit row on the
  notification emission.
- **D4 — Email is best-effort; the inbox is the durable record.** The
  recipient **always** gets the inbox row. The email is staged
  **conditionally** on the recipient's `NotificationPreference` (the
  lean-default: no preference yet = all enabled) **and** on the recipient
  having a `Profile.Email` at all (a resident without an email still gets
  the inbox row; no `IMailerStage.StageAsync` call — the M4 precedent).
  A resident who disables the kind keeps the inbox record and loses only
  the email nudge.
- **D5 — The two new kinds join the closed `KnownTranslationKeys`
  registry, all four languages, four keys each.** Per kind:
  `notifications.kind.{kind}`, `notifications.preference.{kind}.label`,
  `notification.{kind}.subject`, `notification.{kind}.body`. That is 8
  new keys × 4 languages = 32 entries; the
  `KwLRegistryConsistencyTests` parity check (exact key-set equality
  across the four dictionaries) enforces the count. See the handoff note
  for the full key table.
- **D6 — The group detail page becomes three tabs: Feed, Members,
  Settings (owner ∪ GlobalAdmin only).** `Views/Groups/Detail.cshtml`
  restructures the existing sections into the Bootstrap `nav nav-tabs`
  idiom (the `_ProjectsTabs.cshtml` precedent, ADR 0062). The active tab
  is derived **server-side from the `?tab=` query string** (no client-side
  tab JS, no new dependency). See the handoff note for the full tab
  layout.
- **D7 — Reuse, don't reinvent.** The M6 `NotificationService.EmitAsync`
  seam, the M1 durable-email trio, the ADR 0061 per-recipient
  outbound-channel language, the closed `KnownTranslationKeys` registry,
  the Bootstrap `nav-tabs` idiom, and the ADR 0078 sample-account
  suppression gate (inherited by the two new kinds for free — the
  suppression check in `EmitAsync` is kind-agnostic) are all **frozen
  seams**. This ADR adds **two kind constants + two emitters (both in
  `UserInfoService`) + 32 `kw-l` keys + one view restructure + one new
  test file (7 tests) + one test-count update**. **No** new bounded
  context, **no** new document, **no** new `M*DocTypes` surface, **no**
  new `INotificationService` interface, **no** new `AccessAction`, **no**
  new `AccessVia`, **no** new `IAuthorizableResource` adapter, **no** new
  email mechanism, **no** new client dependency.

## Consequences

- `NotificationKinds` now has **thirteen** kinds (eleven +
  `group.added` + `group.invite`); the settings toggles render two more
  rows; the `NotificationsControllerTests` count assertion moves
  `11 → 13`; and the `KnownTranslationKeys` registry gains 32 entries
  (8 keys × 4 languages) whose exact key-set parity across en/de/fr/da is
  enforced at build time.
- `UserInfoService`'s primary-ctor gains one trailing, optional
  `IServiceProvider?` parameter; the 181 positional
  `UserInfoService(store)` test call sites are **unchanged** (they get
  `services = null` → no emission, the pre-M6 behavior). **No** existing
  Core or Web test changes its expectations — the only count that moves
  is the notifications count assertion, by design.
- A resident who is added to a group or invited to one now sees an inbox
  row (and, if they have an email and haven't disabled the kind, an email)
  in their own `EmailLanguage` (ADR 0061), with the group's name as the
  snippet (ADR 0018) — the "something just changed about me" signal M6
  promised for membership changes and that ADR 0076 left as a follow-on.
- The group detail page is now Feed / Members / Settings tabs: a member
  lands on the Feed; an owner ∪ GlobalAdmin additionally sees Members'
  management lanes and the Settings tab; deep links `?tab=…` land on the
  right tab.
- **Deferred, each named:** per-kind **sub-settings** (an email-only vs
  inbox-only split, ADR 0076's follow-on), **notification options on the
  group Settings tab**, the **`post.mention`** emitter (still reserved,
  ADR 0076 D2), **group/community to-do assignment** notifications
  (ADR 0073 / 0074), **push / PWA push** (M9), **digests**, and **live
  update** (WebSocket / SSE) — each a follow-on lane with its own ADR.
