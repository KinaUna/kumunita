# M6 — Notifications — sealed unit register

> **In progress.** This is the **lane register** (the secondary register
> tier) for the **M6** milestone — the **shared awareness** arrow on the
> value chain: *"the platform reaches out to the resident where they are"*
> (the `docs/ARCHITECTURE.md` §0 row, already pinned). This lane settles the
> notification surface that **every prior ADR deferred to M6** — ADR 0013
> (notifications on new group posts), ADR 0054 (notifications-as-a-lane),
> ADR 0063/0064 (no new notifications), ADR 0067 (notifications on
> assignment), the GU/GA lanes (no email notification to the assigned
> guardian — **still deferred** after M6, recorded as a follow-on lane in
> the deferral list), and M3/M3b (report-file / report-assign /
> report-resolve).
>
> **What this is:** a **new bounded context** `Kumunita.Core.Notifications`
> (the `Notification` and `NotificationPreference` documents), **one new
> document surface** (`M6DocTypes`, the `M5DocTypes` pattern verbatim),
> **one new service** (`NotificationService`, no `INotificationService`
> interface — the `EventService` / `ProjectService` precedent), **no new
> `AccessAction`**, **no new `AccessVia`**, **no new
> `IAuditableResource` adapter** (a notification is *personal data* — the
> recipient reads their own rows; there is no audience to evaluate),
> **no new email mechanism** (the M1 durable-email trio is reused verbatim
> — `IMailerStage.StageAsync` + `OutboxEmailStager` + `OutboxEmailHandler`),
> **one new Web controller** (`NotificationsController`) + views + one
> layout bell, **one new plain-TS module** (`client/lib/notifications-bell.ts`)
> for the bell's unread poller + the settings toggles, and **the `kw-l`
> keys × 4 languages** (en/de/fr/da) for the UI strings.
>
> **The one thing every unit must respect:** this milestone is **additive
> and reusing.** It reuses the **M1 durable-email trio** (the
> `IMailerStage.StageAsync` seam, the `OutboxEmailStager` implementation,
> the `OutboxEmailHandler` durable consumer, and the
> `EmailDeadLetterWriter` — ADR 0001 step 7; the ARCHITECTURE.md §6.2
> dead-letter + `/health` degraded gate), the **ADR 0061 per-recipient
> outbound-channel language** (`Profile.EmailLanguage` →
> `LocaleSettings.DefaultLanguageCode` → `en` registry floor — the
> `ITranslationProvider` already does the resolution), the **`kw-dt`
> TagHelper** (ADR 0019 / 0020) for every inbox timestamp, the **closed
> `KnownTranslationKeys` registry** (ADR 0015) for the notification
> body / subject templates, and the **plain `client/lib` tsc-only TS**
> (ADR 0031) for the bell + settings. **No new `AccessAction`**, **no new
> `AccessVia`**, **no new `IAuditableResource` adapter**, **no new email
> mechanism**, **no new UI dependency**, **zero migrations for existing
> surfaces** (ADR 0004 §B.1 additive — the two docs are new; the surface
> is additive). **M6 is the single `StatusNext` on the roadmap** (M5 is
> `StatusDone` — ADR 0067; U01 confirms, not moves); **M7 stays
> `StatusPlanned`**.
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a
> time, each with its own closed exit criteria, in the `M4` / `M5` /
> `EV-DWM` style. **U00 is the sign-off gate** — it authors the design
> doc, locks the **[PROPOSED]** decisions into **ADR 0076**, and the
> roadmap trio is confirmed by **U01** (M5 `StatusDone`, M6 `StatusNext`,
> M7 `StatusPlanned` — already the case on `Milestones.cs` + README);
> every later unit codes against the *locked* text. **Sequencing
> invariant:** the two documents + `M6DocTypes` (U02) land before the
> service (U03–U04); the service (U03–U04) lands before the Web
> controller (U05) and the settings (U07); the bell + poller (U06, U08)
> land after the controller they call; the pinned tests (U09) run before
> the gate (U10); the close (U10) is last so the doc index + README +
> ARCHITECTURE.md are honest at ship time.

## Understanding (one paragraph)

M3 shipped **signal** (posts, replies, announcements, group posts,
moderation); M4 shipped **coordination** (events, RSVPs, reminders); M5
shipped **outcome** (to-dos, boards). Every one of them has a moment
where *a resident's action should reach another resident* — a reply lands
on your post, someone RSVPs to your event, a neighbor posts in the group
you're in, a to-do is assigned to you, a report is filed against content
you wrote, a report is assigned to you as a moderator. Today, **none of
those moments produce a notification.** The only outbound channel is the
event-reminder email (M4 §6.4) and the verification email (M1). A
resident has no way to *see* what happened to the things they own
without re-visiting every surface.

M6 is the **shared awareness** arrow: the platform reaches out to the
resident *where they are* — in the inbox (a first-class, always-available
list of "things that happened to you") and in their email (the durable,
out-of-band channel for the time-sensitive kinds). This is the *shortest
honest path from a resident's action to a signal we can act on*
(`docs/philosophy/in-code.md` §4, feedback loops as a feature; the
human-system doc's "a notification that flows to a next action" vs
"another thing I have to check"). The one rule the platform already
encodes (ADR 0061, `Profile.EmailLanguage`) — *the outbound channel
language is a persistent profile fact, resolved per recipient, with an
`en` floor* — is the **exact** shape M6's email templates use, so the
per-recipient language pin is *inherited*, not re-invented.

The **inbox** is a `Notification` document: a personal row the recipient
owns (no audience, no `AccessAction`, no `AccessVia`, no adapter — the
recipient's own `SubjectId` is the whole access story). The **email**
channel is the M1 durable-email trio reused verbatim — the same
`IMailerStage.StageAsync` seam, the same `OutboxEmailHandler` durable
consumer, the same dead-letter + `/health` degraded gate — with a
**`notification:{idempotency-key}`** prefix on the idempotency key so a
re-emission of the same logical event is a **no-op** (the C3
atomicity + idempotency pin). **Email is best-effort** (dead-letter is
fine — the inbox is the primary channel, per
ARCHITECTURE.md §6.2's "audience notification: best-effort" row); the
inbox row is the **durable, guaranteed** delivery. The **preferences**
document is a per-resident set of enabled kinds (`KindsEnabled`, a list
of the closed kind vocabulary — `null` / empty = all enabled, the
lean-default), edited on `/notifications/preferences` and read by the
emitter *at emit time* (the recipient's choice is the authority; the
sender has no standing to override it).

## Assumptions

- **Scope (per user, settled by ADR 0076):** the **two documents**
  (`Notification`, `NotificationPreference`), the **one surface**
  (`M6DocTypes`), the **one service** (`NotificationService` — the
  `EmitAsync` writer, the `ListInboxAsync` / `CountUnreadAsync` /
  `MarkAllReadAsync` read + state lanes, the
  `GetPreferencesAsync` / `SetPreferencesAsync` preference lanes), the
  **one Web controller** (`NotificationsController` — the `/notifications`
  inbox + `/notifications/preferences` editor), the **one layout bell**
  (the unread count badge + the dropdown + the poller), the **one plain
  TS module** (`client/lib/notifications-bell.ts`), the **`kw-l` keys ×
  4 languages** (the inbox labels, the bell label, the settings toggles,
  the empty state, the "mark all read" action, the per-kind body /
  subject templates), the **pinned tests** (12 FACES over the service +
  the Web controller pins), and the **three-test acceptance gate**
  (closed-loop / handoff / part-vs-whole, recorded by U10). **Out
  (deferred, each named):** **push / PWA push notifications** (M9 owns
  PWA — a follow-on lane, own ADR; M6 ships email + the in-app inbox
  only), **daily / weekly digests** (a follow-on lane, own ADR — M6 is
  per-event, not aggregated), **per-kind sub-filters on the inbox** (the
  inbox is a single flat list; filtering by kind is a follow-on lane),
  **notification on assignment to a *group* or *community*** (M5's
  `TodoItem` group / community claim lane, ADR 0073 / 0074 — a follow-on
  lane; M6's `todo.assign` kind is *person-only*), **notification to the
  assigned guardian** (GU / GA lanes — **still deferred** after M6; the
  GU/GA ADRs named this deferral and it is *not* lifted by M6),
  **moderation-queue surfacing on the inbox** (the inbox shows the
  report-file / report-assign / report-resolve *to the resident*; the
  moderator's queue is the M3b moderation surface, unchanged), **read
  receipts / delivery confirmation** (the inbox is the delivery record; a
  read-receipt lane is a follow-on lane), **cross-neighborhood
  notification** (M6 is per-instance; federation is a future lane).
- **The kind vocabulary is a closed string set (the M5 `Status` /
  `KanbanStatuses` string shape, not an enum).** `Kumunita.Core.Notifications
  .NotificationKinds` is a **static class** with **nine** `public const
  string` members (a *code-owned*, closed set — the M5
  `KanbanStatuses` pattern; a resident *cannot* choose their own kind
  string — only the code's emitters can). The **nine kinds** (each a
  `string` constant, each with a **body** + a **subject** `kw-l` key):
  - `NotificationKinds.PostReply` — `"post.reply"` — a reply was added to a post the resident authored
  - `NotificationKinds.PostMention` — `"post.mention"` — a reply on a post the resident is in (a member of the audience) mentions them *(a follow-on lane — M6 ships the kind constant + the `kw-l` keys but the **emitter** is not wired; the constant is reserved for forward compatibility)*
  - `NotificationKinds.GroupPost` — `"group.post"` — a new post was added to a group the resident is a member of
  - `NotificationKinds.EventRsvp` — `"event.rsvp"` — a resident RSVP'd to an event the resident authored
  - `NotificationKinds.EventReminder` — `"event.reminder"` — the M4 §6.4 day-before reminder *(already implemented in M4; M6 adds the **inbox row** for it — the email is M4's, the inbox row is M6's)*
  - `NotificationKinds.ReportFiled` — `"report.filed"` — a report was filed against content the resident authored
  - `NotificationKinds.ReportAssigned` — `"report.assigned"` — a report was assigned to the resident (a moderator)
  - `NotificationKinds.ReportResolved` — `"report.resolved"` — a report the resident filed (or was assigned to) was resolved
  - `NotificationKinds.TodoAssign` — `"todo.assign"` — a to-do was assigned to the resident (person-only; group / community assign is a follow-on lane)
  - **`PostMention` is reserved, not wired** (the constant + the `kw-l` keys exist; no emitter calls it in M6 — the mention-detection logic is a follow-on lane). The **other eight kinds** each have a **wired emitter** in M6 (U04 + the Web-side hook).
- **The idempotency key is the **emitter's** responsibility.** Each
  emission supplies a **stable, content-derived** idempotency key
  (the C3-envelope fix precedent — the `verify:{userId}:{attempt}` /
  `setup:{userId}` shape from the M1 step-7 plan, now with a
  `notification:` prefix). The **key shape** is
  **`notification:{kind}:{stable-source-id}`** — e.g.
  `notification:post.reply:{replyId}` (a reply is a unique source; a
  re-emit of the same reply is a no-op),
  `notification:group.post:{postId}`,
  `notification:event.rsvp:{rsvpId}`,
  `notification:event.reminder:{eventId}:{date}`,
  `notification:report.filed:{reportId}`,
  `notification:report.assigned:{reportId}`,
  `notification:report.resolved:{reportId}`,
  `notification:todo.assign:{todoId}`. **The `IMailerStage.StageAsync`
  idempotency guarantee** (the `OutboxEmailStager` contract — a
  duplicate key is a no-op, not a second email) is the **sole** dedup
  mechanism; the service does not re-check. **A re-emission of the same
  logical event is a no-op** (the C3 pin — the inbox row is stored once,
  the email is staged once, the envelope is published once).
- **The inbox is a **personal read**, not an `AccessAction` decision.**
  A `Notification` row is **the recipient's own data** — the
  `RecipientId` is the whole access story; the inbox read lane
  (`ListInboxAsync`, `CountUnreadAsync`, `MarkAllReadAsync`) does
  **not** call `IAuthorizationService` (there is no audience to
  evaluate, no `AccessAction` to check — the recipient is the
  recipient). **No `NotificationToAuditableResource` adapter exists**
  (C-M6·3 — a notification is not an *auditable resource* in the
  ADR 0006 sense; it is a personal row). **No audit row is emitted for
  an inbox read** (the recipient reading their own notifications is not
  an *access decision* — the ADR 0006 C3 audit is for *audience*
  decisions; a personal read is a different shape, the M1
  `GetProfileAsync` owner-branch precedent).
- **Email is **best-effort**; the inbox is the **durable** record.**
  The `IMailerStage.StageAsync` seam is called **in the same
  transaction** as the `Notification` row store (the C3 pin — the
  domain write + the outbox row + the envelope commit atomically). A
  failed send **never** rolls back the inbox row (the
  `EmailDeadLetterWriter` + the `/health` degraded gate — the
  ARCHITECTURE.md §6.2 posture). **The inbox is the primary channel;
  the email is the out-of-band nudge** (the M4 §6.4 event-reminder
  precedent — the reminder email is the nudge, the event detail is the
  content). **A resident who has disabled a kind in their
  preferences still gets the inbox row** (the inbox is the record; the
  email is the nudge — the preference governs the *email*, not the
  *inbox*; the C-M6·7 pin).
- **The per-recipient email language is the ADR 0061 resolution,
  verbatim.** The `NotificationService` resolves the recipient's
  `Profile.EmailLanguage` (via the frozen
  `IUserInfoService.GetProfileAsync` read seam) and passes it to the
  `ITranslationProvider` (the ADR 0061 seam — the provider does the
  `EmailLanguage` → `DefaultLanguageCode` → `en` chain). **The sender's
  language is irrelevant** (the recipient's choice is the authority —
  ADR 0061 §Context: "a resident may keep the UI in English but want
  reminders in German, or vice versa"). **The UGC snippet in the email
  body** (a reply's body, a to-do's title) is **rendered in the
  sender's authored language** (ADR 0018 — the authored-in language is
  the content's own language, not the recipient's). **The template
  subject / body** is in the **recipient's** `EmailLanguage`.
- **The inbox is a flat list, capped at 50.** `ListInboxAsync` returns
  the **most recent 50** `Notification` rows for the recipient
  (newest-first, `Created` descending). **No pagination in M6** (a
  neighborhood is small — 50 is the "everything that's happened to me
  recently" shape; a full archive is a follow-on lane, the M7
  pagination milestone). **No per-kind filter in M6** (the flat list is
  the shape; filtering is a follow-on lane). **The "mark all read"
  action** sets `ReadAt = now` on **all** the recipient's unread rows
  (one `Update` — no per-row loop; the M3b bulk-update precedent).
- **The preferences document is **lean**.** `NotificationPreference`
  has **one** field: `KindsEnabled` (an `IReadOnlyList<string>`, the
  subset of the nine kind constants the resident has **enabled**;
  `null` / empty = **all enabled** — the lean-default, the M5
  `KanbanLane.MaxItems?` nullable-string precedent: `null` means "no
  limit", here `null` / empty means "all on"). **A resident edits the
  toggles on `/notifications/preferences`** (the settings surface); the
  emitter reads the preference **at emit time** (the recipient's choice
  is the authority; the sender has no standing to override it). **No
  per-kind email-only vs inbox-only split** (the preference governs the
  *email*; the inbox is always recorded — C-M6·7).
- **The layout bell is a **poller**, not a push.** The
  `client/lib/notifications-bell.ts` module polls
  `GET /notifications/unread-count` at a **30-second interval**
  (a `setInterval`, the `client/lib` plain-TS precedent — no
  WebSocket, no SSE, no push; the ADR 0031 tsc-only / no-dependency
  pin). **The badge shows the unread count** (a Bootstrap
  `badge text-bg-danger` on the bell icon in the `_AccountNav`
  partial); **the dropdown is a plain `fetch` + DOM render** (the
  `client/lib/api.ts` CSRF-aware fetch shape). **No live update in
  M6** (a WebSocket / SSE lane is a follow-on lane; the 30-second
  poll is the "good enough" shape for a neighborhood-scale
  platform).
- **Test model (unchanged).** xunit.v3, run via `dotnet exec …dll` per
  AGENTS.md (**not** `dotnet test` / VS Test Explorer on this
  machine); `Kumunita.Core.Tests` = `PostgresFixture`;
  `Kumunita.Web.Tests` = NSubstitute (no Postgres). The pinned seam
  tests (Core: the 12 FACES over the service) + the Web controller pins
  are named in the design doc Part 2 (U00); the three-test acceptance
  gate (closed-loop / handoff / part-vs-whole) is recorded by U10.

## Approach

Four tracks, sequenced — exactly like M5. **Track A (Core model, U02):**
the two POCOs in `Kumunita.Core.Notifications`, the `M6DocTypes`
registration + boot wiring, and the `NotificationKinds` static class.
**Track B (Core service, U03–U04):** U03 declares the
**`NotificationService`** (the `EmitAsync` writer + the
`ListInboxAsync` / `CountUnreadAsync` / `MarkAllReadAsync` read + state
lanes + the `GetPreferencesAsync` / `SetPreferencesAsync` preference
lanes, all implemented — the M5 `ProjectService` shape, not the M4
`IProjectService` interface-first shape, because the service is small
enough to land in one unit); U04 wires the **eight emitters** (the
Web-side hooks that call `NotificationService.EmitAsync` on
post-reply, group-post, event-rsvp, report-file / -assign / -resolve,
and todo-assign). **Track C (Web, U05–U08):** U05 the
`NotificationsController` (the `/notifications` inbox + the
`/notifications/unread-count` poller endpoint + the
`/notifications/mark-all-read` POST + the
`/notifications/preferences` GET + POST) + the view models; U06 the
Razor views (the inbox list + the empty state + the "mark all read"
button) + the layout bell (the `_AccountNav` badge + the dropdown);
U07 the preferences editor view + the `kw-l` keys × 4 languages;
U08 the `client/lib/notifications-bell.ts` module + the `site.css`
rules. **Track D (Tests + gate, U09–U10):** U09 the pinned seam tests
(Core: the 12 FACES over the service; Web: the controller pins); U10
the acceptance gate + the close (roadmap trio, ARCHITECTURE.md
§3 + §5 sync, README, handoff summary, and moving this lane's unit
files from `in-progress/notifications/` →
`done/notifications/`).

Every unit ends with **build green** (and `tsc` green from U08). The
last unit (U10) appends the final handoff section + moves the lane's
unit files into `done/notifications/` so the roadmap / ADR /
ARCHITECTURE.md pin is honest at ship time.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U00–U10 below),
one unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/m6-notifications-design.md`,
  authored U00–U01): pins the exact C# shapes of every document / service
  method every unit codes against, the invariant table, the pinned test
  names, and the acceptance-gate shape. U00–U01 are the **sign-off
  gate** — **ADR 0076** (Accepted) is the decisions source; the design
  doc turns it into exact shapes.
- **Secondary — this file**
  (`docs/plans-milestones/plan-m6-notifications.md`) — the lane
  register: understanding, assumptions, invariants, FACES, and the unit
  index (each pointing at its unit file).
- **Unit files — `docs/plans-milestones/in-progress/notifications/U0#.md`**
  — one file per unit with the full **Goal / Entry reads / Deliverables
  / Exit**. **On a unit's completion its file is moved
  `in-progress/notifications/U0#.md` → `done/notifications/U0#.md`**
  (the per-lane subfolder follows the existing `done/m4/` / `done/ev-dwm/`
  convention and avoids cross-lane `U0#` collisions). **The lane register
  stays at `docs/plans-milestones/plan-m6-notifications.md`** (it does
  not move with the units — the lane folder it registers is
  `in-progress/notifications/`, which becomes `done/notifications/` when
  the lane closes). When the lane closes (U10) the whole
  `in-progress/notifications/` unit-file set moves to
  `done/notifications/`.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/notifications/notifications-handoff-notes.md`).
  One `## U#` section per unit, appended (never rewritten). Each unit
  writes exactly one short section before it exits; the next unit reads
  only that section + its own entry-read list. Moves with the lane folder
  at close.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, ≤ 5 files <~300 lines each, no full-repo scan; the
design-doc section cited is named); **Deliverables** (a closed set of
new/modified files, ≤ ~4 files / ~600 LOC, no misc cleanups); **Exit**
(build green for the touched projects + `tsc` green from U08;
handoff-note entry appended *before* any follow-up action; the unit file
moved `in-progress/notifications/` → `done/notifications/`).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the
§drift-guard unit (U10); (3) never introduces a test whose exact name is
not in the design doc's pinned list (U09); (4) **the
`NotificationService` is frozen in U03** (all public method signatures,
verbatim from the design doc) — U04 **wires the emitters** (the
Web-side hooks that call the service), it does **not** re-shape the
service's public surface; (5) never re-shapes a document
(`Notification`, `NotificationPreference`) outside the design-doc §2
pin; (6) never adds a seam on `IAuthorizationService` /
`IUserInfoService` / `IIdentityService` beyond the **reuse** of the
frozen `GetProfileAsync` read seam for the recipient's
`EmailLanguage` lookup (there is **no** ADD in this lane — the
notification is a personal read, not an `AccessAction` decision);
(7) never introduces a UI library or a bundler (plain `client/lib` TS,
tsc-only — the ADR 0031 pin); (8) never re-derives an access decision
in the Web (the `NotificationService` is the single path — a personal
read, not an `AccessAction`); (9) if entry reads reveal the design doc
is out of date, the unit pauses and records `## U<m> — Drift pause` in
the handoff note.

---

## The invariants (pinned for `M6`)

- **C-M6·1 — One bounded context, two documents, one surface, additive.**
  `Kumunita.Core.Notifications` with `Notification`,
  `NotificationPreference`; one new surface `M6DocTypes` (the
  `M5DocTypes` pattern verbatim); Marten-native POCOs, conventional
  `string` `Id`, delta-detected + idempotent, no seeding, no EF, zero
  migrations for existing surfaces (ADR 0004 §B.1).
  *(ADR 0076 D1.)*
- **C-M6·2 — The kind vocabulary is a closed string set, code-owned.**
  `NotificationKinds` is a **static class** with **nine** `public const
  string` members (a code-owned, closed set — the M5 `KanbanStatuses`
  pattern; a resident *cannot* choose their own kind string — only the
  code's emitters can). **`PostMention` is reserved, not wired** (the
  constant + the `kw-l` keys exist; no emitter calls it in M6 — the
  mention-detection logic is a follow-on lane). The **other eight
  kinds** each have a **wired emitter** in M6 (U04 + the Web-side hook).
  *(D2.)*
- **C-M6·3 — A notification is a **personal read**, not an
  `AccessAction` decision.** A `Notification` row is **the recipient's
  own data** — the `RecipientId` is the whole access story; the inbox
  read lane does **not** call `IAuthorizationService` (there is no
  audience to evaluate, no `AccessAction` to check). **No
  `NotificationToAuditableResource` adapter exists** (a notification is
  not an *auditable resource* in the ADR 0006 sense). **No audit row is
  emitted for an inbox read** (the recipient reading their own
  notifications is not an *access decision* — the M1
  `GetProfileAsync` owner-branch precedent). *(D3.)*
- **C-M6·4 — The idempotency key is the **emitter's** responsibility;
  a re-emission is a no-op.** Each emission supplies a
  **`notification:{kind}:{stable-source-id}`** idempotency key (the
  C3-envelope fix precedent — the `verify:{userId}:{attempt}` /
  `setup:{userId}` shape from the M1 step-7 plan, now with a
  `notification:` prefix). **The `IMailerStage.StageAsync` idempotency
  guarantee** (a duplicate key is a no-op, not a second email) is the
  **sole** dedup mechanism; the service does not re-check. **A
  re-emission of the same logical event is a no-op** (the C3 pin — the
  inbox row is stored once, the email is staged once, the envelope is
  published once). *(D4.)*
- **C-M6·5 — Email is **best-effort**; the inbox is the **durable**
  record.** The `IMailerStage.StageAsync` seam is called **in the same
  transaction** as the `Notification` row store (the C3 pin — the
  domain write + the outbox row + the envelope commit atomically). A
  failed send **never** rolls back the inbox row (the
  `EmailDeadLetterWriter` + the `/health` degraded gate — the
  ARCHITECTURE.md §6.2 posture). **The inbox is the primary channel;
  the email is the out-of-band nudge** (the M4 §6.4 event-reminder
  precedent). *(D5.)*
- **C-M6·6 — The per-recipient email language is the ADR 0061
  resolution, verbatim.** The `NotificationService` resolves the
  recipient's `Profile.EmailLanguage` (via the frozen
  `IUserInfoService.GetProfileAsync` read seam) and passes it to the
  `ITranslationProvider` (the ADR 0061 seam). **The sender's language
  is irrelevant** (the recipient's choice is the authority — ADR 0061
  §Context). **The UGC snippet in the email body** (a reply's body, a
  to-do's title) is **rendered in the sender's authored language**
  (ADR 0018 — the content's own language, not the recipient's).
  *(D6.)*
- **C-M6·7 — A resident who has disabled a kind in their preferences
  still gets the inbox row** (the inbox is the record; the email is the
  nudge — the preference governs the *email*, not the *inbox*). The
  `NotificationService.EmitAsync` **always** stores the
  `Notification` row; it **conditionally** calls
  `IMailerStage.StageAsync` (only if the recipient's
  `NotificationPreference.KindsEnabled` includes the kind, or is
  `null` / empty = all enabled). **No per-kind email-only vs
  inbox-only split** (the preference is a single list; the split is a
  follow-on lane). *(D7.)*
- **C-M6·8 — The inbox is a flat list, capped at 50; no pagination,
  no per-kind filter in M6.** `ListInboxAsync` returns the **most
  recent 50** `Notification` rows for the recipient (newest-first,
  `Created` descending). **No pagination in M6** (a neighborhood is
  small — 50 is the shape; a full archive is M7). **No per-kind filter
  in M6** (the flat list is the shape; filtering is a follow-on lane).
  **The "mark all read" action** sets `ReadAt = now` on **all** the
  recipient's unread rows (one `Update` — no per-row loop). *(D8.)*
- **C-M6·9 — The preferences document is **lean**.**
  `NotificationPreference` has **one** field: `KindsEnabled` (an
  `IReadOnlyList<string>`, the subset of the nine kind constants the
  resident has **enabled**; `null` / empty = **all enabled** — the
  lean-default). **A resident edits the toggles on
  `/notifications/preferences`** (the settings surface); the emitter
  reads the preference **at emit time**. **No per-kind sub-settings in
  M6** (the single list is the shape; sub-settings are a follow-on
  lane). *(D9.)*
- **C-M6·10 — The layout bell is a **poller**, not a push.** The
  `client/lib/notifications-bell.ts` module polls
  `GET /notifications/unread-count` at a **30-second interval**
  (a `setInterval`, the `client/lib` plain-TS precedent — no
  WebSocket, no SSE, no push; the ADR 0031 tsc-only / no-dependency
  pin). **The badge shows the unread count**; **the dropdown is a
  plain `fetch` + DOM render** (the `client/lib/api.ts` CSRF-aware
  fetch shape). **No live update in M6** (a WebSocket / SSE lane is a
  follow-on lane; the 30-second poll is the shape). *(D10.)*
- **C-M6·11 — Reuse, don't reinvent.** The M1 durable-email trio
  (`IMailerStage.StageAsync` + `OutboxEmailStager` +
  `OutboxEmailHandler` + `EmailDeadLetterWriter`), the ADR 0061
  per-recipient outbound-channel language, the `kw-dt` TagHelper
  (ADR 0019 / 0020), the closed `KnownTranslationKeys` registry (ADR
  0015), the plain `client/lib` tsc-only TS (ADR 0031) are all
  **frozen seams** — M6 adds **two documents + one surface + one
  service + one controller + one view set + one TS module**, not a
  branch. **No new `AccessAction`**, **no new `AccessVia`**, **no new
  `IAuditableResource` adapter**, **no new email mechanism**, **no new
  UI dependency**. *(D11.)*

## FACES (pinned, 12)

- **F1** a reply on a post the resident authored notifies the author
  (inbox row + email) — C-M6·2, C-M6·7
- **F2** a new post in a group the resident is a member of notifies the
  member (inbox row + email) — C-M6·2, C-M6·7
- **F3** an RSVP on an event the resident authored notifies the author
  (inbox row + email) — C-M6·2, C-M6·7
- **F4** the M4 §6.4 day-before reminder now also stores an inbox row
  (the email is M4's; the inbox row is M6's) — C-M6·2, C-M6·5
- **F5** a report filed against content the resident authored notifies
  the author (inbox row + email) — C-M6·2, C-M6·7
- **F6** a report assigned to the resident (a moderator) notifies them
  (inbox row + email) — C-M6·2, C-M6·7
- **F7** a report the resident filed (or was assigned to) is resolved
  — they are notified (inbox row + email) — C-M6·2, C-M6·7
- **F8** a to-do assigned to the resident (person-only) notifies them
  (inbox row + email) — C-M6·2, C-M6·7
- **F9** a resident who has disabled a kind in their preferences **still
  gets the inbox row** but **not the email** — C-M6·7
- **F10** a re-emission of the same logical event (same idempotency key)
  is a **no-op** (no second inbox row, no second email) — C-M6·4
- **F11** the inbox read (`ListInboxAsync`, `CountUnreadAsync`) does
  **not** emit an audit row (a personal read, not an `AccessAction`
  decision) — C-M6·3
- **F12** the email is in the **recipient's** `Profile.EmailLanguage`
  (the ADR 0061 resolution), the UGC snippet is in the **sender's**
  authored language (ADR 0018) — C-M6·6

## Units (11 total)

The unit files live in `docs/plans-milestones/in-progress/notifications/`
(one `U0#.md` per unit). **On a unit's completion its file is moved
`in-progress/notifications/U0#.md` → `done/notifications/U0#.md`** (the
per-lane subfolder follows the existing `done/m4/` / `done/ev-dwm/`
convention). The lane register (this file) stays at
`docs/plans-milestones/plan-m6-notifications.md`. When the lane closes
(U10) the whole `in-progress/notifications/` unit-file set moves to
`done/notifications/`.

- **U00** — Design doc Part 1 (context, scope, decisions, invariants,
  FACES) — `U00.md`
- **U01** — Design doc Part 2 (seams, contracts, test list, gate,
  drift-guard) + ADR 0076 sign-off + the roadmap trio confirmation —
  `U01.md`
- **U02** — The two documents + `M6DocTypes` registration + boot wiring
  + the `NotificationKinds` static class — `U02.md`
- **U03** — The `NotificationService` (the `EmitAsync` writer + the
  read / state / preference lanes) — `U03.md`
- **U04** — The **eight emitters** (the Web-side hooks that call
  `NotificationService.EmitAsync` on post-reply, group-post, event-rsvp,
  report-file / -assign / -resolve, and todo-assign) — `U04.md`
- **U05** — The `NotificationsController` (the `/notifications` inbox +
  the `/notifications/unread-count` poller endpoint + the
  `/notifications/mark-all-read` POST + the
  `/notifications/preferences` GET + POST) + the view models — `U05.md`
- **U06** — The Razor views (the inbox list + the empty state + the
  "mark all read" button) + the layout bell (the `_AccountNav` badge +
  the dropdown) — `U06.md`
- **U07** — The preferences editor view + the `kw-l` keys × 4 languages
  (the inbox labels, the bell label, the settings toggles, the empty
  state, the "mark all read" action, the per-kind body / subject
  templates) — `U07.md`
- **U08** — The `client/lib/notifications-bell.ts` module (the bell's
  unread poller + the dropdown render + the settings toggles) + the
  `site.css` rules — `U08.md`
- **U09** — The pinned seam tests (Core: the 12 FACES over the service;
  Web: the controller pins) — `U09.md`
- **U10** — The acceptance gate + the close (roadmap trio,
  ARCHITECTURE.md §3 + §5 sync, README, handoff summary, and moving
  this lane's unit files from `in-progress/notifications/` →
  `done/notifications/`) — `U10.md`
