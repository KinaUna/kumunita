# Plan: M9 — Messaging

> **In progress.** Unit register (secondary tier). Living handoff note:
> `docs/plans-milestones/m9-messaging-handoff-notes.md` (scratch tier — one
> `## U#` section per unit, appended, never rewritten). The authoritative
> design (primary tier) — `docs/design/m9-messaging-design.md` (the
> `m8-search-design.md` naming) — is authored by **U00** and locked before
> any code unit runs; the decision record is **ADR 0105**.
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/`
> (`messaging-u00.md` … `messaging-u07.md`). When a unit is done, its plan
> file moves to `docs/plans-milestones/done/`. A unit agent reads **its own
> plan file + its entry reads** — it does not need to re-derive this
> register, which is why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the
> invariant/FACES contract; the decisions below are the [PROPOSED] set U00
> locks (or the user vetoes before U00 runs — this register is the last
> cheap place to change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — ≤ 5 files, ≤ ~400 LOC of
> change, a short entry-reads list (3–6 files), and an Exit check that fits
> in one build + test run. A unit's full context (its unit-plan file + its
> entry reads + its deliverables) fits in one 32K window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests beyond
> the pinned list; no new seams on frozen interfaces.**
>
> M9 is **greenfield**: there is no messaging surface, no `Conversation` /
> `Message` document, and no `Kumunita.Core.Messaging` context anywhere yet
> (grep-confirmed). What M9 builds on: M6's `Notification` lane (ADR 0076 —
> the "new message" bell/email nudge), ADR 0101's admin-toggle shape
> (`LocaleSettings` bool + read-with-floor + `Set…Async(actorId)` write seam
> + one `Via = Admin` audit row + a dedicated GlobalAdmin controller), the
> M7/M8 paging discipline (`HasMore` + `_Pager`, ADR 0090), and ADR 0004
> §B.1's Marten-native document-surface rule. M9 starts from that locked
> text.

---

## Understanding

Kumunita has public and group-scoped conversation surfaces (posts,
announcements, todo/comments, group posts) but **no direct 1:1 channel
between two residents**. M9 adds exactly that: a signed-in resident opens a
conversation with another resident, exchanges plain-text messages, and both
participants see read state. A GlobalAdmin can enable or disable the feature
instance-wide (the ADR 0101 shape). The feature is **off by default** on a
fresh instance (D2 — privacy-first opt-in: a neighborhood opts *in* to
direct messaging rather than inheriting it).

Access is deliberately minimal: a conversation is visible **only to its two
participants** (D3/D4 — the ADR 0076 D3 "the id is the whole access story"
shape; no `Audience`, no `IAuthorizationService`, no `GlobalAdmin`
break-glass). Messages are plain text, length-capped, immutable once sent
(D7 — rich content is a follow-on lane). A new message nudges the recipient
through the existing M6 `Notification` lane (D6).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0105 in U00]

> **Open veto.** These are the decisions the user can still change cheaply —
> **before U00 runs**. After U00 they are locked by
> `docs/design/m9-messaging-design.md` + ADR 0105 and changeable only via
> the drift guard.

- **D1 · Scope — 1:1 signed-in resident messaging only.** A conversation is
  always between exactly two residents (the `ParticipantIds` pair). **No**
  group messaging, no community-wide messaging, no self-conversations.
  Alternative considered and rejected: group inbox — that is a follow-on
  lane (its access story is the ADR 0013 membership lane, not M9's
  participant-by-id shape).
- **D2 · Admin toggle, default OFF.** A `LocaleSettings.MessagingEnabled`
  additive bool (`false` floor — a missing row reads as **off**; the
  deliberate inverse of ADR 0101's `true` floor, because messaging is a
  privacy-sensitive opt-in, not a public-surface default). The GlobalAdmin
  flips it at `/admin/messaging` (the `AdminAnnouncementCommentsController`
  shape). When off: **no surface renders, and every service seam refuses**
  (403) — the toggle is enforced in the service, never only in the view.
- **D3 · Participant-only access; no `GlobalAdmin` break-glass.** A
  conversation is visible to exactly the two `ParticipantIds`. A
  GlobalAdmin who is not a participant sees **nothing** (404, not 403 —
  the non-leaky shape). This is the ADR 0028 GU precedent: the platform
  does not grant standing to read private content, not even to its
  operator.
- **D4 · No `Audience`, no `IAuthorizationService` in the messaging read
  path.** Participant check by id comparison in the service (the ADR 0076
  D3 / ADR 0100/0101 "standing is carried by the parent's shape, not a
  second authz call" discipline). Zero new `AccessAction`, `AccessVia`, or
  `IAuditableResource` adapter.
- **D5 · Audit: one `AccessAudit` row per write (open/send/toggle),
  in-transaction, same session as the domain write.** Write rows:
  `TargetKind = "message"` (open + send), `Via = Owner`, `Outcome = Allow`
  (the ADR 0101 `Set…Async` write-row shape). The admin toggle row:
  `TargetKind = "messaging.toggle"`, `Via = Admin`, `Outcome = Allow`
  (the ADR 0101 `announcementcomments.set-enabled` shape verbatim).
  **Reads emit no audit row** (the ADR 0076 F11 personal-read shape —
  participant-by-id is the access story; a 404 leak is the only read
  consequence). The exact `TargetKind` strings and `Via` values are locked
  in U00 against the actual `AccessAudit` doc + the ADR 0101 write-row
  test.
- **D6 · New-message nudge through the M6 lane.** On send, `MessagingService`
  calls the existing `NotificationService.EmitAsync` (ADR 0076) for the
  *other* participant: kind `message.new` (a new closed-set entry in
  `NotificationKinds`), idempotency key `notification:message.new:{messageId}`
  (stable, content-derived — the D4/F10 dedup anchor), `LinkPath =
  /messages/{conversationId}`. The recipient's existing inbox bell +
  preference machinery carries the rest — **zero** new outbound channel.
- **D7 · Plain-text, length-capped, immutable.** `Message.Body` is plain
  text (not Markdown, no images, no attachments) capped at **2000 chars**
  (the design doc may adjust the number, not the rule). A sent message is
  **immutable in M9** — no edit, no delete (a follow-on lane; the ADR 0024
  soft-delete shape is its entry). Alternative considered and rejected:
  reusing the ADR 0025/0031 rich editor — messaging's value is lightness;
  rich content is a lane.
- **D8 · Read state is per-recipient.** `Message.ReadBy` (the id of the
  recipient who has read it, `null` = unread) — one field, not a join
  document (the ADR 0076 `Notification.ReadAt` "personal read marker"
  shape, adapted to the two-participant model). `MarkReadAsync` sets it for
  the caller only. Unread count per conversation = messages where
  `SenderId != caller && ReadBy is null`.

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M9·1 · Participant-only, no operator peek.** A conversation's
  messages are visible to exactly its two participants. A non-participant
  (including a GlobalAdmin) gets a **non-leaky 404**, never 403 or data
  (D3/D4; the ADR 0028 GU precedent).
- **C-M9·2 · The toggle is a hard service gate.** When off (or the settings
  row is missing), every seam — open, send, list, thread-read, mark-read —
  refuses (403); the admin toggle seam itself always works (it is how the
  instance opens the surface) (D2).
- **C-M9·3 · Audit is always-on, in-transaction, one row per write (D5).**
  Open / send / toggle each commit their row in the same session as the
  domain write; reads emit no row (C-M9·4).
- **C-M9·4 · Reads never audit.** List / thread-read / mark-read emit zero
  `AccessAudit` rows (the ADR 0076 F11 personal-read shape) (D4/D5).
- **C-M9·5 · Zero new authorization surface.** No new `AccessAction`,
  `AccessVia`, `IAuthorizationService` call, or `IAuditableResource`
  adapter anywhere in the messaging path (D4; ADR 0090/0091
  "zero new authorization surface" discipline).
- **C-M9·6 · Messages are plain-text, capped, immutable in M9 (D7).**
  `Body` ≤ cap (400 on over-length); no rendering pipeline, no attachments,
  no edit/delete lane.
- **C-M9·7 · Marten-native documents, one new surface (ADR 0004 §B.1).**
  `Conversation` + `Message` POCOs in `M9DocTypes.Configure(StoreOptions)`,
  delta-detected, idempotent, wired into **both** boot paths
  (`Kumunita.Web/Program.cs` dev loop + `Kumunita.Core/Bootstrap/SchemaBootstrap.cs`);
  no hand-rolled `FeatureSchemaBase`.

## FACES — [PROPOSED, U00 locks]

- **F1 · Open a conversation.** A signed-in resident picks another resident
  → one conversation for that unordered pair exists after the call (a
  second "open" of the same pair returns the existing one — idempotent)
  (C-M9·2/3).
- **F2 · Send a message.** Both participants see it on their next read; the
  *other* participant gets exactly one `message.new` notification (deduped
  by idempotency key); one `message` audit row (C-M9·3/6).
- **F3 · Read state is personal.** Unread count + the "all read for me"
  marker never change for the other participant (D8).
- **F4 · Non-participants see nothing.** A third resident (or a GlobalAdmin
  not in the pair) gets 404 on the thread and no conversation in their
  list — no 403, no existence leak (C-M9·1).
- **F5 · The surface respects the toggle.** When off: the nav entry is
  hidden, `/messages` and `/messages/{id}` 404 (or redirect to home), and
  the service seams refuse (C-M9·2).
- **F6 · The admin toggle is a single audit-trailed action.** GlobalAdmin
  flips on/off at `/admin/messaging`; one `messaging.toggle` audit row per
  flip (C-M9·3).
- **F7 · Everything resident-facing is localized** (`message.*` `kw-l` keys,
  all four languages — `KnownTranslationKeys_ParityTests` enforces).

## Approach

- **Track A — Core (U00–U03):** the design doc + ADR (U00); documents +
  `M9DocTypes` + boot wiring (U01); the toggle seams (U02);
  `IMessagingService` + `MessagingService` + pinned Core tests (U03).
- **Track B — Web (U04–U05):** the resident surface (U04); the admin toggle
  surface (U05).
- **Track C — Web tests (U06):** NSubstitute over `IMessagingService` (no
  Postgres).
- **Close (U07):** `Milestones.cs` / `README.md` / `MilestonesTests.cs`
  flip, `ARCHITECTURE.md` shape-of-code, handoff `## Summary`, unit-plan
  files → `done/`.

## Workflow (three-tier, per-unit)

Same contract as the M8 register: primary tier =
`docs/design/m9-messaging-design.md` (authored by U00; the only authority
after it lands); secondary = this register; scratch = the handoff note (one
`## U#` section per unit: entry state / what ran / drift / open items).
Per unit: Goal → Entry reads (3–6 files) → Deliverables (≤ 5 files) → Exit
(build green + handoff entry). **Unit-series rule: never touch files
outside your own Deliverables; never rewrite the design doc outside the
drift-guard note; no tests beyond the pinned list; no new seams on frozen
interfaces.**

Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
and `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
(the VS Test Explorer / `dotnet test` discovery path is known-broken on this
machine — use `dotnet exec` of the built assembly).

---

## U00 — Lock the design: `m9-messaging-design.md` + ADR 0105

**Goal.** Author the primary tier and the decision record. Lock D1–D8 (or
record vetoes), the C-M9 invariants, F1–F7, the exact seam signature (the
`IMessagingService` block in this register), the audit `TargetKind` values
+ `Via` values (D5 — verified against `AccessAudit` + `Decision.cs`), the
cap numbers (D7: 2000), the pinned test names (U03/U06), the `kw-l` key
list (U04), and the drift log. ADR 0105 records: decisions + alternatives
considered (group messaging; rich content; default-on; GlobalAdmin
break-glass) + the Consequences hand-off (rich-content lane, edit/delete
lane, read-receipts).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md`;
`docs/design/m8-search-design.md` (the house style);
`docs/adr/0101-announcement-comments-lane.md` (the admin-toggle +
write-row shape to mirror); `docs/adr/0076-notifications-inbox-and-recipient-email.md`
(D3/F10/F11 — the personal-read + nudge shape); `src/Kumunita.Core/Authorization/AccessAudit.cs`
(the audit doc shape D5 must fit); `src/Kumunita.Core/Localization/LanguageCatalog.cs`
(the `LocaleSettings` singleton D2 extends).

**Deliverables (3).** `docs/design/m9-messaging-design.md`;
`docs/adr/0105-messaging.md`; `docs/adr/README.md` (one index row).

**Exit.** `dotnet build` still green (docs only). Handoff entry: decisions
locked/vetoed, any D-item text changed, the exact seam signature as written
in the design doc (U01–U03 copy it verbatim), the locked audit-row shape.

---

## U01 — Documents: `Conversation` + `Message` + `M9DocTypes` + boot wiring

**Goal.** The two POCOs (namespace `Kumunita.Core.Messaging`), the
`M9DocTypes.Configure(StoreOptions)` surface (a `(ConversationId, Created)`
index on `Message` for thread reads), and the wiring into **both** boot
paths (the dev loop in `Kumunita.Web/Program.cs` +
`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — the M3 U3 precedent).

**Entry reads (5).** `docs/design/m9-messaging-design.md` (§seams /
§documents — the locked shapes); `src/Kumunita.Core/M3DocTypes.cs` (the
registration shape to mirror — the actual current file);
`src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the boot wiring point);
`src/Kumunita.Web/Program.cs` (the dev-loop boot path);
`docs/adr/0004-data-persistence-and-schema-evolution.md` (§B.1 only).

**Deliverables (5).** `src/Kumunita.Core/Messaging/Conversation.cs`;
`src/Kumunita.Core/Messaging/Message.cs`; `src/Kumunita.Core/M9DocTypes.cs`;
`src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (one call added);
`src/Kumunita.Web/Program.cs` (one call added).

**Exit.** Build green. `M9DocTypes` compiles; both boot paths reference it.
**No test in this unit** (U03 owns the first M9 test). Handoff entry: the
exact `M9DocTypes` lines, the two boot-path insertion points, any schema
shape that drifted from the design doc.

---

## U02 — The admin toggle seams on the messaging service

**Goal.** D2's toggle: the `LocaleSettings.MessagingEnabled` additive bool
(`false` floor) + the two toggle seams (`IsMessagingEnabledAsync()` /
`SetMessagingEnabledAsync(bool, actorId)`) + the one `messaging.toggle`
audit row (C-M9·3, the ADR 0101 write-row shape).

**Where the seams live:** on **`IMessagingService`** (U03's interface —
U02 creates the interface file with the two toggle seams first, U03
appends the rest; the `AnnouncementService` toggle-on-own-service precedent
from ADR 0101, not the `IIdentityService`-hosting variant of
`IsSignupOpen`). If U02's drift reads say the pair belongs on
`IIdentityService` instead, record the `## U02 — Drift pause` and stop.

**Entry reads (6).** `docs/design/m9-messaging-design.md` (§toggle — the
locked shape); `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the
`LocaleSettings` doc + the `AnnouncementCommentsEnabled` field D2 mirrors);
`src/Kumunita.Core/Announcements/AnnouncementService.cs`
(`AreAnnouncementCommentsEnabledAsync` / `SetAnnouncementCommentsEnabledAsync`
— the floor + audit-row shape to copy); `src/Kumunita.Core/Authorization/AccessAudit.cs`;
`tests/Kumunita.Core.Tests/AnnouncementServiceTests.cs` (the toggle
pinned-test pair, ~line 1906); `docs/adr/0101-announcement-comments-lane.md`
(§toggle).

**Deliverables (4).** `src/Kumunita.Core/Localization/LanguageCatalog.cs`
(one additive `MessagingEnabled` bool); `src/Kumunita.Core/Messaging/IMessagingService.cs`
(the interface — toggle seams first); `src/Kumunita.Core/Messaging/MessagingService.cs`
(the two toggle methods + ctor over `IDocumentStore` only);
`src/Kumunita.Core/DependencyInjection.cs` (the `AddTransient<IMessagingService>`
registration).

**Pinned tests (2, in `tests/Kumunita.Core.Tests/MessagingServiceTests.cs`
— this file is U02's to create, U03 extends):**
`IsMessagingEnabled_FreshInstance_FloorsToFalse` ·
`SetMessagingEnabled_Toggle_StoresFlagAndAuditRow`.

**Exit.** Build green; the 2 pins pass. Handoff entry: the seam signatures
as implemented, the audit-row field values, any floor-text drift.

---

## U03 — The Core seams: open / send / list / thread / read

**Goal.** The D1–D8 read/write behavior of messaging: `OpenConversationAsync`
(idempotent on the unordered pair), `SendAsync` (cap check, append,
`NotificationService.EmitAsync` nudge, audit row), `ListConversationsAsync`
(actor's conversations, newest-activity-first, `HasMore` paging on the M7
discipline), `GetConversationAsync` (participant-gated, non-leaky 404),
`MarkReadAsync` (caller-only `ReadBy` set).

**Entry reads (6).** `docs/design/m9-messaging-design.md` (§seams,
§invariants — the locked text); `src/Kumunita.Core/Notifications/NotificationService.cs`
(`EmitAsync` overloads, ~line 107/154 — the nudge seam to call);
`src/Kumunita.Core/Notifications/NotificationKinds.cs` (the closed-set kind
registry D6 extends); `src/Kumunita.Core/Posts/FeedResult.cs` (the `HasMore`
record shape); `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the harness);
`tests/Kumunita.Core.Tests/MessagingServiceTests.cs` (U02's 2 pins — extend,
do not rewrite).

**Deliverables (3, ≤ 5 files).** `src/Kumunita.Core/Messaging/IMessagingService.cs`
(the remaining seams appended); `src/Kumunita.Core/Messaging/MessagingService.cs`
(the implementations — ctor gains the optional
`NotificationService?` seam, the ADR 0077 optional-nudge-param idiom, so the
U02 tests keep compiling); `tests/Kumunita.Core.Tests/MessagingServiceTests.cs`
(extend with the pins below).

**Pinned tests (12 — the design doc may rename, not rescope):**
`OpenConversation_SamePairTwice_ReturnsSameConversation` ·
`OpenConversation_SamePairEitherOrder_ReturnsSameConversation` ·
`OpenConversation_BothParticipants_SignedIn_Only` ·
`Send_StoresMessage_BothParticipantsSeeIt` ·
`Send_OtherParticipant_GetsMessageNewNotification_Once` ·
`Send_BlankBody_ArgumentException` ·
`Send_OverCap_ArgumentException` ·
`GetConversation_NonParticipant_404_Not403` ·
`GetConversation_GlobalAdminNonParticipant_404` ·
`ListConversations_OnlyOwn_NoLeak` ·
`MarkRead_SetsReadByForCallerOnly` ·
`ToggleOff_AllSeamsRefuse_403`.

**Exit.** Build green; the full `MessagingServiceTests` class passes
(2 + 12 = 14). Handoff entry: seam signatures as implemented, the
notification idempotency-key string as written, the audit-row shape
(observed), any drift (e.g. a `HasMore` record field the design doc didn't
name).

---

## U04 — Web surface: resident messaging

**Goal.** F1–F5/F7 rendered: the `/messages` conversation list +
`/messages/{id}` thread (read + compose), the nav entry (hidden when the
toggle is off — the `IsMessagingEnabledAsync` read), and the `message.*`
`kw-l` keys (all four languages).

**Entry reads (5).** `docs/design/m9-messaging-design.md` (§web, §FACES,
§keys); `src/Kumunita.Web/Controllers/AnnouncementController.cs` (a
`[Authorize]`-signed-in controller + the toggle-read gate pattern at ~line
308); `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (the nav block —
where the entry goes; the `IsSignupOpenAsync` conditional at ~line 275 to
mirror); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
closed-key registry); `src/Kumunita.Web/Views/Shared/_Pager.cshtml` (the
shared pager, M7 discipline).

**Deliverables (5).** `src/Kumunita.Web/Controllers/MessagesController.cs`;
`src/Kumunita.Web/Views/Messages/Index.cshtml` (the list + "new
conversation" picker); `src/Kumunita.Web/Views/Messages/Thread.cshtml`
(messages + composer + unread markers); `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml`
(nav entry only — no reflow); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(new keys — **all four languages**, `KnownTranslationKeys_ParityTests`
enforces).

**New keys (provisional — the design doc is authoritative):**
`message.nav` · `message.title` · `message.new` · `message.thread.empty` ·
`message.compose.placeholder` · `message.compose.send` · `message.unread` ·
`message.disabled` · `message.other` (the "other participant" label).

**Exit.** Build green; app smoke (`dotnet run` + browser): with the toggle
on, `/messages` renders the list + a thread renders messages + the nav
entry is present; with it off, the nav entry is hidden and `/messages`
404s/redirects. Handoff entry: keys added, nav placement, the
controller→view-model shapes, any drift.

---

## U05 — Web surface: admin toggle

**Goal.** F6 rendered: `/admin/messaging` (GlobalAdmin — the
`AdminAnnouncementCommentsController` shape verbatim: `[Route("admin/…")]` +
`[Authorize(Roles = Roles.GlobalAdmin)]` on a **dedicated** controller, a thin
wrapper over the two toggle seams, the audit row is the **service's**) with
the on/off control.

**Entry reads (4).** `docs/design/m9-messaging-design.md` (§toggle, §web);
`src/Kumunita.Web/Controllers/AdminAnnouncementCommentsController.cs` (the
controller shape to copy — route + attribute + GET/POST pair + its nested
view-model class); `src/Kumunita.Web/Views/AdminAnnouncementComments/Index.cshtml`
(the sibling's view layout — the new view lives in its **own** folder,
`Views/AdminMessaging/Index.cshtml`, not under `Views/Admin/`);
`src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (U04's nav — the
resident-side link to mirror in any admin link placement).

**Deliverables (3).** `src/Kumunita.Web/Controllers/AdminMessagingController.cs`
(route `admin/messaging`); `src/Kumunita.Web/Views/AdminMessaging/Index.cshtml`;
the admin nav link (one line in the file that holds the
`announcementcomments` admin link — U05 identifies it in its entry reads; if
the sibling is reached only by direct URL, follow that and add no link).

**Exit.** Build green; app smoke: as a GlobalAdmin, `/admin/messaging`
flips the toggle (verified by the resident surface appearing/disappearing);
as a non-admin, 403. Handoff entry: the link placement, any drift.

---

## U06 — Web tests: `MessagesControllerTests` (NSubstitute, no Postgres)

**Goal.** Pin the Web shape over the substituted `IMessagingService`
(the M8 U03 / `AnnouncementControllerTests` harness).

**Entry reads (4).** `docs/design/m9-messaging-design.md` (§web, §FACES);
`tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the
NSubstitute-over-service controller harness — copy its setup);
`src/Kumunita.Web/Controllers/MessagesController.cs` (U04's code);
`src/Kumunita.Web/Controllers/AdminMessagingController.cs` (U05's code).

**Deliverables (1).** `tests/Kumunita.Web.Tests/MessagesControllerTests.cs`.

**Pinned tests (10):** `Messages_Index_ToggleOff_RendersDisabled_NoListCall` ·
`Messages_Index_ToggleOn_RendersConversationList` ·
`Messages_Thread_NonParticipant_404_NoView` ·
`Messages_Thread_RendersMessages_WithUnreadMarkers` ·
`Messages_Send_PostsBody_ToService` ·
`Messages_Send_BlankBody_RendersError_NoServiceCall` ·
`Messages_Nav_ToggleOff_EntryHidden` ·
`Messages_Nav_ToggleOn_EntryPresent` ·
`AdminMessaging_Get_RendersCurrentState` ·
`AdminMessaging_Post_FlipsToggle_AuditedByService`.

**Exit.** Build green; the class passes. Handoff entry.

---

## U07 — Close the milestone

**Goal.** Flip the roadmap state and land the doc parity (the AGENTS.md
contract — README ↔ `Milestones.cs` ↔ `MilestonesTests.cs` move together).

**Entry reads (4).** `docs/plans-milestones/m9-messaging-handoff-notes.md`
(the full unit log); `README.md` (Roadmap — the M9 entry, already
**In progress**); `src/Kumunita.Web/Milestones.cs` (+
`tests/Kumunita.Web.Tests/MilestonesTests.cs` — the pins);
`docs/adr/0105-messaging.md` (the citation for the flip).

**Deliverables (6).** `src/Kumunita.Web/Milestones.cs` (M9 →
`StatusDone`; M10 PWA → `StatusNext` — the single-in-progress invariant
passes to the next milestone);
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M10 sole-in-progress
pin, M11–M14 planned); `README.md` (Roadmap: M9 → `**Done.**` citing
ADR 0105; M10 → `**In progress.**`); `docs/STATUS.md` (the "next is"
sentence moves to M10); `docs/ARCHITECTURE.md` ("shape of the code":
the `Messaging/` bounded context + the seam + the audit `TargetKind`s);
the unit-plan files `in-progress/messaging-u*.md` → `done/`.

**Exit.** **Everything green** (`dotnet build Kumunita.slnx -c Debug`;
both test assemblies pass in full). Handoff `## Summary` (the M8 U04
shape: the capability, the seams, the invariants, the deferred lanes —
rich content, edit/delete, read-receipts).

---

## Exit (all green)

- `dotnet build Kumunita.slnx -c Debug` — 0 errors.
- `Kumunita.Core.Tests.MessagingServiceTests` — 14/14.
- `Kumunita.Web.Tests.MessagesControllerTests` — 10/10.
- `MilestonesTests` — green (M9 done, M10 sole in-progress).
- The handoff note ends with `## Summary`.
