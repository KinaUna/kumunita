# M9 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the design-doc
/ ADR section it maps to), anything that drifted from the plan's text, and the
next unit's entry reads.

The register is `docs/plans-milestones/plan-m9-messaging.md`. Each unit ships
its own self-contained plan in `in-progress/messaging-uNN.md`; when a unit is
done its plan file moves to `done/`. The **next unit's** agent reads its own
unit plan + this file's most recent `## U#` section + its entry reads — it does
not re-derive the register.

## Kickoff — M9 inserted as the in-progress milestone

- **What this is.** M9 (Messaging) was inserted as the next milestone by
  shifting the existing tail: PWA and responsive design M9 → **M10**,
  Portability M10 → M11, iCal M11 → M12, Logging and analytics M12 → M13,
  Integration of Events and Projects M13 → **M14**. M9 Messaging is the
  **single** `StatusNext` milestone.
- **Files touched (the doc-parity trio, moved together):**
  - `src/Kumunita.Web/Milestones.cs` — M9 Messaging `StatusNext`; M10–M14
    `StatusPlanned`.
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — order pin now runs
    `…M8, M9, M10, M11, M12, M13, M14`; the in-progress test renamed to
    `M9_Is_The_Single_InProgress_Milestone_And_M10_Through_M14_Are_Planned`
    (planned list `M10–M14`).
  - `README.md` — Status line → "M9 in progress (Messaging…)"; Roadmap M9
    entry `**In progress.**` (ADR 0105), tail renumbered to M10–M14.
  - `docs/STATUS.md` — "next is M9 — Messaging … Then M10–M14 (…)".
  - `docs/ARCHITECTURE.md` — value-chain table: M9 messaging row inserted;
    M9–M13 → M10–M14.
- **Verified:** `dotnet build Kumunita.slnx -c Debug` green;
  `Kumunita.Web.Tests` 511/511 (the `MilestonesTests` re-pin included).
- **Not touched (historical records, per the AGENTS.md "don't edit done
  records" discipline):** the M6 bullet's "push / PWA push (M9)" deferral note
  (a done-milestone record), every `done/` handoff note referencing the old
  M9–M13 tail, and ADRs that name "M9" as PWA (ADR 0076/0083/0084 deferrals).
- **ADR number for M9:** **0105** (the index ran 0001–0104; `0105` was free).
  U00 will author `docs/adr/0105-messaging.md` + the `docs/adr/README.md` row.
- **Next:** U00 (see `in-progress/messaging-u00.md`).

## U00 — design doc + ADR 0105

- **Files written:** `docs/design/m9-messaging-design.md` (the primary
  tier, LOCKED), `docs/adr/0105-messaging.md` (Accepted, 2026-09-27),
  `docs/adr/README.md` (one index row, 0105).
- **Decisions:** **all of D1–D8 locked as proposed; no veto was
  recorded** in the open window. The D-item texts were carried into the
  design doc verbatim in substance; the refinements below are
  clarifications, not changes of rule.
- **Refinements recorded (drift-guard entries, all in the design doc's
  locked text — no unit plan contradicts them):**
  1. **D5 audit `Action` strings locked** (the plan text named only the
     `TargetKind`s): `message.open` / `message.send` /
     `messaging.toggle` — the `announcementcomments.set-enabled`
     verb-shape (verified against `AnnouncementService.cs`, the actual
     rows) applied to `message.*` / `messaging.toggle`.
  2. **D5 toggle row `TargetId` = `"messaging.toggle"`** (the flat
     sentinel, mirroring ADR 0101's `TargetId = "announcementcomments"`,
     verified in the actual `SetAnnouncementCommentsEnabledAsync`) — the
     plan text did not name `TargetId` for the toggle row.
  3. **D6 `targetId = null`** on the `EmitAsync` call — the ADR 0084
     per-target subscription gate is skipped (messaging has no
     per-target scope in M9); `message.new` is **not** in
     `OptInKinds` (opt-OUT default = enabled, the resident-facing
     posture, verified against `NotificationKinds.OptInKinds`).
  4. **D6 emission runs before the send's single `SaveChangesAsync`** —
     domain write + nudge + audit row commit atomically (the ADR 0076
     D5 shape); a failed email never rolls back the message.
  5. **The `Conversation` pair is stored sorted** (`ParticipantA ≤
     ParticipantB`) + a **unique `(ParticipantA, ParticipantB)` index**
     in `M9DocTypes` — the F1 idempotency witness; the plan text named
     the index only for `Message`.
  6. **`Message.LanguageCode` added** (ADR 0018 authored-in tag,
     default `"en"`) — the display/translation lane's entry field,
     consistent with every UGC document; the plan text did not name it.
     (The display/translation lane itself stays deferred.)
  7. **`MaxBodyChars = 2000`, `PageSize = 20`** locked as
     `MessagingService` constants (the ADR 0076 D8 / ADR 0090 D4
     neighborhood-scale shape); the number is amendable in the design
     doc §Drift-guard, the cap rule is not.
  8. **`OtherDisplayName` resolved via the frozen
     `IUserInfoService.GetProfileAsync`** (null-safe to the raw subject
     id) — the plan's record shape named the field; the seam that
     fills it is locked here (the `DirectoryService.cs:180` usage is
     the verified precedent).
- **Seam signature as locked (design doc §2.2 — U01–U03 copy verbatim):**
  the §2.2 block of `docs/design/m9-messaging-design.md`, unchanged from
  the plan text: `IsMessagingEnabledAsync()` ·
  `SetMessagingEnabledAsync(bool, string)` ·
  `OpenConversationAsync(string, string) → Task<ConversationRef>` ·
  `GetConversationAsync(string, string, int) → Task<ConversationDetail>` ·
  `ListConversationsAsync(string, int) → Task<ConversationList>` ·
  `SendAsync(string, string, string)` · `MarkReadAsync(string, string)`.
  Records: `ConversationRef(Id, OtherParticipantId, OtherDisplayName,
  LastMessageBody, LastMessageAt, UnreadCount)` ·
  `ConversationList(Items, HasMore)` · `ConversationDetail(Conversation,
  Messages, HasMore)`. `MessagingService` composes `IDocumentStore` +
  optional `NotificationService` (the ADR 0077/0083 idiom — the U02
  tests keep compiling) + `IUserInfoService.GetProfileAsync`;
  `AddTransient<IMessagingService>` in `DependencyInjection.cs`.
- **Audit-row shape locked (design doc §2.3):** single-target rows only
  (`TargetId` set, counts null — verified against `AccessAudit.cs`);
  `Via` = the existing `AccessVia.Owner` / `AccessVia.Admin` values
  (verified against `Decision.cs` — **no enum append needed**, the
  plan's "verify the enum names" instruction: `Owner` and `Admin`
  exist exactly as named); `Outcome = Allow`; `ActorId =
  EffectivePrincipalId = actorId`; reads emit **no** row.
- **Floor verified against the actual file:**
  `LocaleSettings.AnnouncementCommentsEnabled { get; set; } = true`
  (`LanguageCatalog.cs`) is the field D2 mirrors — M9's
  `MessagingEnabled` is the same shape with the **`false` floor
  inverse** (`settings?.MessagingEnabled == true`), a deliberate
  exception to the codebase `true`-floor convention, recorded in ADR
  0105 D2 + the design doc §Drift-guard pin 5.
- **ADR number confirmed free:** `0105` (the index ran 0001–0104; the
  kickoff entry above recorded the same).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (docs only);
  the 14 + 10 pinned test names are locked in the design doc §2.5
  (U02/U03/U06 copy them by name).
- **Next:** U01 (see `in-progress/messaging-u01.md`).

## U01 — documents + M9DocTypes + boot wiring

- **Entry state:** design doc §2.1/§2.2 LOCKED (U00 locked D1–D8, no veto);
  no `Messaging/` context, no `M9DocTypes`, and no `Conversation` / `Message`
  anywhere yet (grep-confirmed greenfield). `M3DocTypes.cs` read in full as
  the registration shape to mirror; `Program.cs` `AddMarten` lambda read as
  the boot-wiring site; `SchemaBootstrap.cs` read in full.
- **Files written (all three new):**
  - `src/Kumunita.Core/Messaging/Conversation.cs` — the POCO, **verbatim**
    from design doc §2.1: `Id` / `ParticipantA` / `ParticipantB` /
    `Created` / `LastMessageAt?`. `sealed`, `string Id = string.Empty`
    surrogate, `DateTimeOffset Created`, `DateTimeOffset? LastMessageAt`.
  - `src/Kumunita.Core/Messaging/Message.cs` — the POCO, **verbatim** from
    design doc §2.1: `Id` / `ConversationId` / `SenderId` / `Body` /
    `Created` / `ReadBy?` / `LanguageCode` (default `"en"` — the ADR 0018
    authored-in tag, the U00 drift-guard pin 6). `sealed`, `Body =
    string.Empty` (the 2000-char cap is U03's service rule, not the doc).
  - `src/Kumunita.Core/M9DocTypes.cs` — the registration surface (the
    `M6DocTypes` shape — a `public static class` with one
    `Configure(StoreOptions opts)` method; no hand-rolled
    `FeatureSchemaBase`, ADR 0004 §B.1).
- **Modified (one line + comment block):** `src/Kumunita.Web/Program.cs`
  — `M9DocTypes.Configure(opts);` added **after** the
  `M6DocTypes.Configure(opts);` call (line 135 pre-edit) inside the
  `AddMarten(opts => { … })` lambda. Neighbor line it sat next to: the
  `M6DocTypes.Configure(opts);` call (the M6 U02 precedent location, the
  last doc-surface registration before the lambda closes).
- **(a) `M9DocTypes` `.Schema.For` lines + the two index definitions, exactly
  as written:**
  ```csharp
  opts.Schema.For<Conversation>()
         .UniqueIndex("convo_uidx_pair",
                     c => c.ParticipantA, c => c.ParticipantB);

  opts.Schema.For<Message>()
         .Index(m => new { m.ConversationId, m.Created });
  ```
  The `Conversation` index is the **unique** business-key index — the F1
  idempotency witness (a second open of the same unordered pair returns the
  existing conversation). The `Message` index is a **regular** composite
  thread-ordering index (the M6 `Notification` `(RecipientId, Created)`
  feed-ordering shape, not an integrity constraint). Both use conventional
  `string` ids (Marten's default) — **no** `.Identity(...)` pin needed
  (the `M6DocTypes` `NotificationPreference` `Identity` pin is specific to
  that doc's no-separate-`Id` shape and does not apply here).
- **(b) Boot-path insertion point (single registration — the M3/M4/M6
  precedent):** one line in one file, `src/Kumunita.Web/Program.cs`
  (the `AddMarten` lambda, after `M6DocTypes.Configure(opts)`). **The plan
  register's "two boot paths" instruction — add a line to
  `SchemaBootstrap.cs` too — is a misdescription, verified against the
  actual code and every prior milestone's handoff note:**
  `SchemaBootstrap.ApplyAsync` calls
  `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`, which
  *consumes* every `Schema.For<T>` / `Storage.Add<T>` registration that the
  host (i.e. `Program.cs`) registered on the `IDocumentStore`'s
  `StoreOptions` — it carries **no** doc-type registration cluster of its
  own (grep-confirmed: zero `…DocTypes.Configure` calls in
  `src/Kumunita.Core/Bootstrap/`). The M3 U3 handoff note records the exact
  same precedent and explicitly records this as the M3/U3 deviation; the
  M4 U01 and M6 U02 handoff notes both record the same "1 line in 1 file,
  `SchemaBootstrap.cs` was **not** touched" shape. **Not a drift-pause** —
  the frozen §2.1 pin is the `M9DocTypes` type + the `Configure(StoreOptions)`
  signature, both preserved verbatim; the *file* the registration call
  belongs in is determined by the actual codebase shape, which the entry
  reads are the authority for (the design doc §2.1 text says "the U01 entry
  reads are the authority for the exact current lines").
- **(c) `ParticipantA < ParticipantB` normalization rule, as written:** the
  design doc §2.1 comment on `Conversation.ParticipantA` /
  `ParticipantB` says **`min(actorId, otherId)` / `max(actorId, otherId)`**
  (string compare) — U03's `OpenConversationAsync` **must** sort the pair
  before storing so the unordered pair has exactly one canonical form, or
  the `convo_uidx_pair` unique index will reject a second open of the same
  pair in the other order (F1). The index enforces it at the DB layer;
  the sort is U03's service-side obligation, not the doc's.
- **(d) Drift vs. the design doc:** none against the §2.1 field sets or the
  §2.1 index list (both indexes are exactly the two §2.1 names — the unique
  `(ParticipantA, ParticipantB)` on `Conversation` and the
  `(ConversationId, Created)` on `Message`). One **naming** choice the
  design doc left open: the design doc names the unique index by its
  columns only ("a unique index on `Conversation` `(ParticipantA,
  ParticipantB)`") without a literal index name. This implementation gives
  it an explicit short name, `convo_uidx_pair`, mirroring the
  `M3DocTypes` `ann_tr_uidx_ann_lang` explicit-name precedent (the
  auto-derived `mt_doc_conversation_uidx_participant_aparticipant_b`
  is 46 chars — within Postgres's 64-char NAMEDATALEN limit, so it is not
  *required* here, but it is the codebase's consistent shape for a
  business-key unique index on a new surface, and it keeps the name short
  and greppable). The `Message` index keeps the auto-derived name (the
  M6 `Notification` `(RecipientId, Created)` feed index does too).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (5 projects,
  zero errors, zero warnings on the new files). No test in this unit —
  U03 owns the first M9 test. `M9DocTypes` compiles; both the dev-loop
  (`Program.cs` `AddMarten` lambda) and the all-env apply path
  (`SchemaBootstrap.ApplyAsync` → `ApplyAllConfiguredChangesToDatabaseAsync`)
  pick the surface up automatically (the M3/M4/M5/M6 precedent).
- **Next:** U02 (see `in-progress/messaging-u02.md`) — the `LocaleSettings`
  `MessagingEnabled` additive bool + the two toggle seams + the one
  `messaging.toggle` audit row.

## U02 — toggle seams

- **Entry state:** design doc §D2/§2.2/§2.3 LOCKED (U00); `Conversation` /
  `Message` + `M9DocTypes` landed (U01); no `IMessagingService` /
  `MessagingService` anywhere yet (grep-confirmed). `AnnouncementService.cs`
  `AreAnnouncementCommentsEnabledAsync` / `SetAnnouncementCommentsEnabledAsync`
  read in full as the floor + write-row shape; `LanguageCatalog.cs`
  `LocaleSettings` read in full; `AccessAudit.cs` + `Decision.cs` read in
  full (enum members verified); `AnnouncementServiceTests.cs` toggle pins +
  harness helpers read; ADR 0101 §toggle read. **No drift** — the seam
  placement (on the messaging service, not `IIdentityService`) matches the
  design doc's locked §2.2 interface block, so no drift pause.
- **Files written (all deliverables, 4 + the test file):**
  - `src/Kumunita.Core/Localization/LanguageCatalog.cs` — one additive
    `public bool MessagingEnabled { get; set; } = false;` on
    `LocaleSettings`, **after** `AnnouncementCommentsEnabled` (the
    `false`-floor text as written: "Defaults to `false` — the deliberate
    **inverse** of the codebase `true`-floor convention … a missing
    settings row reads as **off** (the `false` floor, design doc §D2)").
  - `src/Kumunita.Core/Messaging/IMessagingService.cs` — the interface with
    the **two toggle seams only** (U03 appends the rest), verbatim from the
    design doc §2.2 locked block:
    `Task<bool> IsMessagingEnabledAsync();` ·
    `Task SetMessagingEnabledAsync(bool enabled, string actorId);`
  - `src/Kumunita.Core/Messaging/MessagingService.cs` — ctor over
    `IDocumentStore` only (U03 will gain the optional `NotificationService?`
    seam). `IsMessagingEnabledAsync`: `QuerySession` +
    `LoadAsync<LocaleSettings>(SingletonId)` → `settings?.MessagingEnabled
    == true` (the `false` floor). `SetMessagingEnabledAsync`: empty
    `actorId` → `UnauthorizedAccessException` (the ADR 0101 shape verbatim);
    load-or-mint the singleton, set the flag, store settings + audit row,
    one `SaveChangesAsync`.
  - `src/Kumunita.Core/DependencyInjection.cs` —
    `AddTransient<Messaging.IMessagingService>(sp => new
    Messaging.MessagingService(sp.GetRequiredService<Marten.IDocumentStore
    >()))`, added directly after the `IAnnouncementService` registration
    (the same factory shape).
- **Audit-row field values (as implemented, verified against the design
  doc §2.3 frozen text):** `Action = "messaging.toggle"`,
  `TargetKind = "messaging.toggle"`, `TargetId = "messaging.toggle"` (the
  flat sentinel), `Via = Authorization.AccessVia.Admin`, `Outcome =
  Authorization.AccessOutcome.Allow` (both enum members exist in
  `Decision.cs` — no append needed), `ActorId = EffectivePrincipalId =
  actorId`, `VisibleCount = HiddenCount = null` (the single-target shape).
- **Pinned tests (2):**
  - `IsMessagingEnabled_FreshInstance_FloorsToFalse` — fresh store, no
    `LocaleSettings` row → `false`; `Assert.Empty` on the `AccessAudit`
    lane (the `IsSignupOpen_FreshInstance_FloorsToTrue_NoAuditRow` shape,
    the `false` floor).
  - `SetMessagingEnabled_Toggle_StoresFlagAndAuditRow` — `Set(true,
    "u-admin")` → flag reads `true`; exactly one `messaging.toggle` row,
    `Via = Admin`, `Outcome = Allow`, `TargetKind = TargetId =
    "messaging.toggle"`, `ActorId = "u-admin"`, `VisibleCount` /
    `HiddenCount` null (the ADR 0101
    `SetAnnouncementCommentsEnabled_Closed_StoresFlagAndAuditRow` shape).
  - Harness notes: `BootStoreAsync` mirrors `AnnouncementServiceTests`
    (fresh scratch Postgres per test) but registers `M9DocTypes` instead of
    `M3DocTypes` (the `LocaleSettings` + `AccessAudit` rows come from
    `M1DocTypes` in both); the `AuditRows` helper is copied verbatim.
- **Drift:** none. The design doc's locked seam block, the audit-row
  table, the `false`-floor text, and the ADR 0101 precedent all fit the
  actual code exactly; the one plan-text nuance ("`IIdentityService`
  hosting variant" as a possible drift) was checked and rejected — the
  design doc is authoritative and names the messaging service.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (5 projects, zero
  errors). `Kumunita.Core.Tests.MessagingServiceTests` 2/2 via
  `dotnet exec … -class "Kumunita.Core.Tests.MessagingServiceTests"`
  (8.1s, Docker Postgres up/down clean).
- **Next:** U03 (see `in-progress/messaging-u03.md`) — the open / send /
  list / thread / read seams + the 12 behavior pins, extending this
  `MessagingServiceTests.cs` class.

## U03 — core seams

- **Entry state:** design doc §D1–D8/§2.2/§2.3 LOCKED (U00); `Conversation` /
  `Message` + `M9DocTypes` landed (U01); the two toggle seams +
  `IMessagingService` landed (U02). `NotificationService.EmitAsync` (9-arg
  overload) read in full (the nudge seam — **concrete class, not an
  interface**, optional `IOptions<NotificationOptions>?` +
  `IOptions<VerificationOptions>?` trailing params); `NotificationKinds.cs`
  read in full (the closed-set registry — `OptInKinds` is `{ announcement,
  page.child }`, the rest opt-OUT); `NotificationServiceTests.cs` harness
  read in full (the `RecordingTranslator` / `RecordingMailer` /
  `PlantProfileReadAsync` shape the nudge pin mirrors);
  `MessagingServiceTests.cs` (U02's 2 pins) read in full.
- **Files written (all four deliverables + the test file):**
  - `src/Kumunita.Core/Messaging/IMessagingService.cs` — the **five
    remaining seams** appended (the design doc §2.2 block, verbatim), plus
    the three record types the seams return:
    - `Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);`
    - `Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);`
    - `Task<ConversationList> ListConversationsAsync(string actorId, int page);`
    - `Task SendAsync(string conversationId, string actorId, string body);`
    - `Task MarkReadAsync(string conversationId, string actorId);`
    - `record ConversationRef(string Id, string OtherParticipantId, string? OtherDisplayName, string? LastMessageBody, DateTimeOffset? LastMessageAt, int UnreadCount);`
    - `record ConversationList(IReadOnlyList<ConversationRef> Items, bool HasMore);`
    - `record ConversationDetail(ConversationRef Conversation, IReadOnlyList<Message> Messages, bool HasMore);`
  - `src/Kumunita.Core/Messaging/MessagingService.cs` — the implementations;
    the ctor **gains two optional trailing params** (the ADR 0077
    optional-nudge-param idiom — CS1736, so U02's 2 tests keep compiling
    unchanged): `IUserInfoService? userInfo = null` (the
    `OtherDisplayName` resolution lane, refinement 8) +
    `NotificationService? notifications = null` (the D6 nudge lane).
    - `OpenConversationAsync`: blank `actorId`/`otherId` → `ArgumentException`
      (400); `actorId == otherId` → `ArgumentException` (D1 — no
      self-conversations); `EnsureEnabledAsync` (D2 hard gate); the pair is
      **sorted** (`string.CompareOrdinal`) so `ParticipantA < ParticipantB`
      (the F1 canonical form); find-or-create on the
      `(ParticipantA, ParticipantB)` pair (the `convo_uidx_pair` unique
      index is the DB-level witness); one `message.open` audit row
      (`Via = Owner`, `TargetKind = "message"`, `TargetId = convoId`,
      `Outcome = Allow`) in the same session, one `SaveChangesAsync`.
    - `GetConversationAsync`: `EnsureEnabledAsync`; loads the conversation
      — **non-leaky 404** (`KeyNotFoundException`) if missing **or** the
      actor is not a participant (D3/D4, C-M9·1); page floors to 1; the
      message window is newest-first `Skip/Take(PageSize)`; `HasMore =
      messages.Count == PageSize` (ADR 0090 D1).
    - `ListConversationsAsync`: `EnsureEnabledAsync`; queries
      `Where(c => c.ParticipantA == actorId || c.ParticipantB == actorId)`
      (F4 — only the caller's own conversations); sorts by
      `LastMessageAt ?? Created` desc; paged (ADR 0090); per-conversation
      last-message body + unread count.
    - `SendAsync`: blank body → `ArgumentException`; `body.Length >
      MaxBodyChars` (2000) → `ArgumentException` (D7); `EnsureEnabledAsync`;
      non-leaky 404; stores the `Message` (D8 `ReadBy = null`,
      `LanguageCode = "en"` the ADR 0018 authored-in tag); updates
      `Conversation.LastMessageAt`; **the D6 nudge** —
      `NotificationService.EmitAsync` (9-arg overload) for the *other*
      participant, **before** the single `SaveChangesAsync` (the ADR 0076
      D5 atomic shape — domain write + nudge + audit row commit
      atomically); one `message.send` audit row (`Via = Owner`,
      `TargetKind = "message"`, `TargetId = convoId`, `Outcome = Allow`).
    - `MarkReadAsync`: `EnsureEnabledAsync`; non-leaky 404; sets
      `ReadBy = actorId` on the messages where `SenderId == other &&
      ReadBy == null` (D8 — caller-only); **no audit row** (C-M9·4 —
      reads / read-state never audit).
    - Constants: `MaxBodyChars = 2000`, `PageSize = 20` (the U00 drift-guard
      pins 7 — the ADR 0076 D8 / ADR 0090 D4 neighborhood-scale shape).
  - `src/Kumunita.Core/Notifications/NotificationKinds.cs` — the
    **`message.new` kind added** (D6 — the closed code-owned set grows):
    `public const string MessageNew = "message.new";` **not** in
    `OptInKinds` (opt-OUT default = enabled — the resident-facing posture,
    the ADR 0105 D6 shape). **Also added to the `Known` list** (the
    17th entry) — the settings-page toggle set the design doc §2.5
    "the settings page renders the `NotificationKinds.Known` toggles"
    names (the ADR 0083/0084 append precedent — `group.added` /
    `group.invite` / `announcement` / `community.post` / `page.child` are
    all in `Known`).
  - **`tests/Kumunita.Core.Tests/MessagingServiceTests.cs`** — extended
    with the **12 pinned behavior pins** (U02's 2 toggle pins are preserved
    verbatim — not rewritten). A new harness helper
    `BootStoreWithNotificationsAsync` (the `NotificationServiceTests`
    shape) registers `M6DocTypes` (so the `Notification` table exists for
    the nudge pin) and constructs the `NotificationService` over the frozen
    seams: a real `IDocumentStore`, an `IUserInfoService` substitute
    (returns `null` for every `GetProfileAsync` — the `OtherDisplayName`
    is `null` in these tests, which is the expected no-directory behavior),
    a `RecordingTranslator` (key-derived markers, the same shape as
    `NotificationServiceTests`), and a `RecordingMailer` (records the staged
    tuples, never dispatches). Three query helpers (`QueryConversations` /
    `QueryMessages` / `QueryNotifications`) are added alongside the
    existing `AuditRows`.
- **Pinned tests (12) — the design doc §2.5 names, verbatim:**
  1. `OpenConversation_SamePairTwice_ReturnsSameConversation`
  2. `OpenConversation_SamePairEitherOrder_ReturnsSameConversation`
  3. `OpenConversation_BothParticipants_SignedIn_Only`
  4. `Send_StoresMessage_BothParticipantsSeeIt`
  5. `Send_OtherParticipant_GetsMessageNewNotification_Once`
  6. `Send_BlankBody_ArgumentException`
  7. `Send_OverCap_ArgumentException`
  8. `GetConversation_NonParticipant_404_Not403`
  9. `GetConversation_GlobalAdminNonParticipant_404`
  10. `ListConversations_OnlyOwn_NoLeak`
  11. `MarkRead_SetsReadByForCallerOnly`
  12. `ToggleOff_AllSeamsRefuse_403`
- **Audit-row field values (as implemented, verified against `Decision.cs`
  and `AccessAudit.cs`):** `Action = "message.open"` / `"message.send"`,
  `TargetKind = "message"`, `TargetId = conversationId`, `Via =
  Authorization.AccessVia.Owner`, `Outcome =
  Authorization.AccessOutcome.Allow`, `ActorId = EffectivePrincipalId =
  actorId`, `VisibleCount = HiddenCount = null` (the single-target shape).
  The `messaging.toggle` row (U02) is `Via = Admin` (the admin-toggle
  lane). Reads / mark-read emit **no** `AccessAudit` row (C-M9·4).
- **`HasMore` record shape (ADR 0090):** `ConversationList(Items, HasMore)`
  + `ConversationDetail(Conversation, Messages, HasMore)` — `HasMore` is
  the sole paging signal; `page` floors to 1; `HasMore = page filled` (a
  full page of 20 implies there may be more). No `Total` / `Count` field
  (the M7/M8 `FeedResult` shape).
- **`message.new` nudge idempotency key (as written):**
  `notification:message.new:{messageId}` — the `messageId` is the
  32-char hex `Message.Id` (the codebase's `Guid.NewGuid().ToString("N")`
  idiom, the M6 D4 shape). The `EmitAsync` call passes
  `targetId: null` (the ADR 0084 per-target subscription gate is skipped —
  messaging has no per-target scope), `linkPath: /messages/{conversationId}`,
  `body: message.Body` (the UGC snippet, ADR 0018 — the recipient's
  `EmailLanguage` governs the template around it, the emitter does not
  pre-localize). Exactly **one** notification per send (dedup by key —
  a re-emission of the same message is a no-op).
- **Drift (two, both recorded, neither a drift-pause):**
  1. **`MessagingService` ctor gains TWO optional params** (not one):
     the plan's text said "gains an optional `NotificationService?` param"
     (the ADR 0077 idiom); the actual implementation also needs
     `IUserInfoService?` (the `OtherDisplayName` resolution, U00 drift-guard
     pin 8). Both are **optional trailing** (`= null`) so U02's 2 tests
     keep compiling unchanged (the ADR 0077 CS1736 idiom).
  2. **`NotificationKinds.MessageNew` added to the `Known` list** — the
     plan's text said "add `message.new` to `NotificationKinds`" without
     specifying the `Known` list. The design doc §2.5 (the settings page
     renders the `Known` toggles) + the ADR 0083/0084 append precedent
     (every resident-facing kind is in `Known`) make this the right call.
     **One consequence:** `Known` grows 16 → 17, which will break the
     **Web** `NotificationsControllerTests.AllKinds.Count == 16` pin —
     that is U06's concern (U06 owns the Web tests; the design doc §2.5
     says "the settings page renders the `Known` toggles," so the count
     pin must follow the `Known` list). U03's Core exit is green; the Web
     count pin is out of U03's scope.
  3. **`KnownTranslationKeys.cs` — the `message.new` keys added in all
     four languages** (en/de/fr/da): `notifications.kind.message.new`,
     `notifications.preference.message.new.label`,
     `notification.message.new.subject`,
     `notification.message.new.body`. The `NotificationService.EmitAsync`
     reads these keys (the `notification.{kind}.subject` / `.body` shape)
     before storing the `Notification` row — without them, the nudge pin
     would see a null `Subject` / `Body`. The
     `KnownTranslationKeys_ParityTests` (U06's Web-test concern) enforces
     the four-language closure.
  4. **`DependencyInjection.cs` — the `IMessagingService` registration
     updated** to pass the two optional params (the ADR 0077 idiom):
     `sp.GetRequiredService<IUserInfoService>()` +
     `sp.GetRequiredService<Notifications.NotificationService>()`.
     U02's registration passed only the `IDocumentStore`; U03's
     implementations need the directory + nudge seams.
  5. **`IQuerySession` vs `IDocumentSession` in
     `CountUnreadForAsync`** — the plan's text (and the design doc §D8)

## U04 — resident surface

- **Files written (the 5 deliverables, exactly):**
  1. `src/Kumunita.Web/Controllers/MessagesController.cs` (new).
  2. `src/Kumunita.Web/Views/Messages/Index.cshtml` (new).
  3. `src/Kumunita.Web/Views/Messages/Thread.cshtml` (new).
  4. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (edited — the
     gated nav entry).
  5. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (edited —
     9 keys × 4 languages).
- **Controller shape (what U06 pins against):**
  - `public sealed class MessagesController(IMessagingService messaging,
    IUserInfoService userInfo) : Controller`, `[Authorize]` on the class.
    (A stray unused `ITranslationProvider` param was removed — CS9113.)
  - **`GET /messages`** `Index(int? page)`: actor null → `NotFound()`;
    toggle off → `View(new MessagesIndexViewModel { Disabled = true })`
    with **no `ListConversationsAsync` call** (the F5 pin); else
    `ListConversationsAsync(actorId, pageNum)`; picker candidates =
    `GetProfilesAsync(verifiedOnly: false)` minus the actor and blocked
    residents, projected to `record PickerCandidate(string SubjectId,
    string DisplayName)`, ordered by display name; `Pager =
    PagedViewModel.ForRoute("/messages", pageNum, MessagingService.PageSize,
    list.HasMore)` only when `HasMore || pageNum > 1`.
  - **`POST /messages/open`** `Open([FromForm] string? otherId)`: toggle off
    → `View("Index", { Disabled = true })`; success →
    `Redirect($"/messages/{conversation.Id}")`; `ArgumentException` →
    `View("Index", { Error = true })`; `KeyNotFoundException` →
    `NotFound()`; `UnauthorizedAccessException` → disabled view.
  - **`GET /messages/{id}`** `Thread(string id, int? page)`: toggle off →
    `NotFound()`; `GetConversationAsync` — `KeyNotFoundException` →
    `NotFound()` (the non-leaky 404, C-M9·1), `UnauthorizedAccessException`
    → `StatusCode(403)`; best-effort `MarkReadAsync` on entry (catching
    KNF/UAEE so marking never breaks the render); `ActorDisplayName` via
    `GetProfileAsync(actorId)` (null-tolerant); pager on the
    `/messages/{id}` route.
  - **`POST /messages/{id}/send`** `Send(string id, [FromForm] string?
    body)`: the **blank-body check is in the controller, before any service
    call** (`string.IsNullOrWhiteSpace(body)` → `View("Thread", { Error =
    true })` — the `Messages_Send_BlankBody_RendersError_NoServiceCall`
    pin); `SendAsync` — `ArgumentException` → error view, KNF → 404,
    UAEE → 403; success → `TempData["info"] = "Sent."` + redirect to the
    thread.
  - Nested types: `MessagesIndexViewModel` (Disabled, Error,
    `Conversations IReadOnlyList<ConversationRef>`, `Candidates
    IReadOnlyList<PickerCandidate>`, Page, `Pager PagedViewModel?`),
    `MessagesThreadViewModel` (Conversation `ConversationRef?`, Messages
    `IReadOnlyList<Message>`, ActorId, ActorDisplayName `string?`, Page,
    Pager, Error, Disabled), `PickerCandidate`.
- **Views:**
  - `Index.cshtml` — the `Disabled || Error` state renders the
    `message.disabled` alert and returns (no list column, no picker);
    otherwise the two-column layout: conversation list (empty state →
    `message.thread.empty`; row = other-person name with the
    `message.other` fallback, unread badge `title="@unreadWord"`
    (`message.unread`), truncated `LastMessageBody`, `<kw-dt>` timestamp)
    + the new-conversation picker (select `name="otherId"` → POST
    `/messages/open` with anti-forgery token, button `message.new`);
    `<partial name="_Pager" model="Model.Pager" />` when non-null (the M8
    search pattern, reused exactly).
  - `Thread.cshtml` — header (back link + other person's name, falling
    back to `message.other`), `Disabled`/`Error` alert states, the message
    list (row = sender label — own messages labeled with
    `ActorDisplayName` — timestamp `<kw-dt>`, unread badge when
    `!mine && m.ReadBy is null`, unread rows get a
    `border-start border-primary border-4` accent), `_Pager` when
    non-null, and the composer (POST `/messages/{id}/send`, textarea
    `name="body"` `required` `maxlength` =
    `MessagingService.MaxBodyChars` (the public const), placeholder
    `message.compose.placeholder`, submit `message.compose.send`).
  - Both views resolve placeholder/attribute strings through
    `EffectiveLanguageCode.ResolveAsync` + `ITranslationProvider`
    (the ADR 0072 pattern; `@using Kumunita.Web.Security` required).
- **Nav entry (`_AccountNav.cshtml`):** injected
  `IMessagingService`; inside the signed-in account dropdown, after
  "Notification subscriptions" and before the `@if (isGlobalAdmin)`
  block: `@if (await Messaging.IsMessagingEnabledAsync()) { <li>…
  <a href="/messages"><kw-l key="message.nav">Messages</kw-l></a>…</li> }`
  — the `IsMessagingEnabledAsync()` gate is the whole toggle story on the
  nav (no list call, C-M9·1).
- **9 new keys × 4 languages (en/de/fr/da)** in
  `KnownTranslationKeys.cs`, anchored after `notification.message.new.body`:
  `message.nav`, `message.title`, `message.new`,
  `message.thread.empty`, `message.compose.placeholder`,
  `message.compose.send`, `message.unread`, `message.disabled`,
  `message.other` (the en/de/fr/da values are in the file;
  `message.title` is the Index page heading) — all enforced by the
  parity tests.
- **Verified:** `dotnet build Kumunita.slnx -c Debug` green;
  `Kumunita.Web.Tests` 511/511 (incl. `KwLRegistryConsistencyTests` —
  every view `kw-l` key is registered); `KnownTranslationKeys_ParityTests`
  7/7.
- **Browser smoke (all pass):** toggle **off** → `/messages` renders the
  `message.disabled` notice with no list, the nav entry is hidden; toggle
  **on** → list + picker render (picker shows the 6 other residents, the
  actor excluded), nav entry present in the right position; Open →
  redirect to the thread; thread renders (header, empty state, composer);
  Send → "Sent." toast + message row with correct sender/timestamp/body;
  **two-account pass** — signed in as Ben Nowak, the same thread shows
  Anna's message with the **unread badge** (Ben hadn't read it), replied
  through the composer, thread shows both messages in order.
- **Drift / caveats (record, don't pause):**
  1. **The toggle smoke was driven by a scratch helper**,
     `.tmp/smoke_toggle/` (a small console app against the dev DB:
     `on` / `off` / `state` modes, calling `SetMessagingEnabledAsync`
     directly) — U05's admin toggle surface doesn't exist yet, so the
     browser toggle flip was done out-of-band. This left dev-DB residue:
     `messaging.toggle` `AccessAudit` rows with actor `"smoke-test"`
     (plus the earlier toggle states), and the toggle was **left ON** in
     the dev DB (the smoke left it that way).
  2. **Dev-DB conversation residue:** conversation
     `ab366b3ee0694ec9804e1b43a4ae68b9` now holds two messages (Anna →
     Ben, Ben → Anna) from the two-account smoke — expected test residue
     in the throwaway dev DB.
  3. **Stray console 404 on page loads** is the pre-existing avatar
     fallback 404 (the avatar-missing pattern from earlier milestones),
     **not** a Messages regression — the pages render correctly.
- **Not touched:** no service-layer files, no `IAuthorizationService`
  (C-M9·1 — U03's non-leaky 404 is the whole access story), no tests
  (U06 owns them), no files outside the 5 deliverables.
- **Next:** U05 (the admin toggle surface — the first thing to replace the
  `.tmp/smoke_toggle` helper with a real page), then U06 (Web tests,
  including re-pinning any `Known`-count pin to 17 per U03's drift note 2).
     don't name the session type. The two read paths (`GetConversationAsync`
     / `ListConversationsAsync`) use `IQuerySession` (read-only); the
     write paths (`OpenConversationAsync` / `SendAsync` /
     `MarkReadAsync`) use `IDocumentSession`. The helper's param is
     typed `IQuerySession` (the more general of the two — both compile).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (zero errors,
  zero warnings). `Kumunita.Core.Tests.MessagingServiceTests` **14/14**
  (U02's 2 + U03's 12) via
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  -class "Kumunita.Core.Tests.MessagingServiceTests"` (13.6s, Docker
  Postgres up/down clean).
- **Next:** U04 (see `in-progress/messaging-u04.md`) — the Web surface
  (the `/messages` list + thread + nav entry + the `message.*` `kw-l` keys).

## U05 — admin toggle surface

- **Files written (the deliverables, exactly):**
  1. `src/Kumunita.Web/Controllers/AdminMessagingController.cs` (new).
  2. `src/Kumunita.Web/Views/AdminMessaging/Index.cshtml` (new).
  3. `src/Kumunita.Web/Views/Admin/Index.cshtml` (edited — the admin-link
     card, one block, after the `Announcement comments` sibling card).
  4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (edited —
     5 keys × 4 languages: `admin.messaging_title`, `admin.messaging_lede`,
     `admin.messaging_on`, `admin.messaging_off`; the Save button reuses the
     sibling's `admin.anncomments_save` key — see Drift note 1).
- **Controller shape (what U06 pins against):**
  - `public sealed class AdminMessagingController(IMessagingService
    messaging) : Controller`, `[Route("admin/messaging")]` +
    `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]` — the
    `AdminAnnouncementCommentsController` shape verbatim (confirmed against
    the sibling: same route attribute, same authorization attribute, same
    `KumunitaPrincipal.SubjectId(User)` actorId mapping).
  - **`GET /admin/messaging`** `Index()`: reads
    `IsMessagingEnabledAsync()` into the nested
    `MessagingAdminViewModel { Enabled }` (default `false`, the D2 floor) —
    the controller adds **no** audit row (C-M9·3 — the service owns it).
  - **`POST /admin/messaging`** `Save(bool enabled)`:
    `[ValidateAntiForgeryToken]`, maps `actorId` via
    `KumunitaPrincipal.SubjectId(User) ?? string.Empty`, calls
    `SetMessagingEnabledAsync(enabled, actor)`, sets
    `TempData["info"]` (open/closed text), redirects to `Index`. The
    `messaging.toggle` audit row is committed by the **service** — the
    controller adds none.
- **View (`Views/AdminMessaging/Index.cshtml`):** the
  `AdminAnnouncementComments/Index.cshtml` layout copied (back-link to
  `/admin`, `<h1>` title, `<p class="text-muted">` lede, the
  `row g-2 align-items-end` form with the `<select name="enabled">` True/
  False options + Save button, anti-forgery token, `action="/admin/messaging"`).
  The five `admin.messaging_*` keys drive the title/lede/on/off; the Save
  button reuses `admin.anncomments_save`.
- **Admin link:** `src/Kumunita.Web/Views/Admin/Index.cshtml` — the sibling
  `announcementcomments` card link lives in the `row g-3` section-card grid
  (not in `_AccountNav` — that is the resident-side nav). Added the
  `Direct messaging` card (`href="/admin/messaging"`) immediately after the
  `Announcement comments` card. Verified rendering in the admin dashboard
  during smoke (e103/e104).
- **Verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors, the
  1 pre-existing warning is U03's `MessagingServiceTests.cs` xUnit2013).
  App smoke (all pass, toggle left **ON** in the dev DB after):
  - **Non-admin (Ben Nowak)** → `GET /admin/messaging` → Access denied
    page (the `[Authorize(Roles=GlobalAdmin)]` shape — 403 path, same as
    the sibling).
  - **GlobalAdmin (Alex Admin, `admin@examplium.com`) GET** → renders
    current state (ON, `selected` on the Open option — U04 left it ON).
  - **GlobalAdmin POST → Closed** → "Messaging is now closed" toast, form
    re-renders with Closed selected, **and the resident `Messages` nav
    entry is gone** from the account dropdown (the F5 gate — the
    `IsMessagingEnabledAsync()` read in `_AccountNav`).
  - **GlobalAdmin POST → Open** → "Messaging is now open" toast, **and the
    `Messages` nav entry is back** — full round-trip of the toggle driving
    the resident surface.
  - **Admin dashboard** → the `Direct messaging` card renders and links to
    `/admin/messaging`.
- **Drift / caveats (record, don't pause):**
  1. **The Save button reuses `admin.anncomments_save`** rather than a new
     `admin.messaging_save` key — the sibling's lede/title/on/off got their
     own `admin.anncomments_*` keys (ADR 0101 precedent), so F7's "sibling
     keys if the sibling added any" reads as: follow the sibling's
     *pattern* of dedicated keys (done: `admin.messaging_title` / `_lede` /
     `_on` / `_off`). The `Save` verb, however, is a generic action button
     whose sibling value is the identical word in all four languages
     (Save/Speichern/Enregistrer/Gem) — registering a 5th key for the same
     string is pure duplication, so the view points at the existing
     `admin.anncomments_save` key. If U06/U07 want strict per-surface key
     ownership, that's a one-line key rename in the view + one registry
     entry × 4 languages; nothing in the controller changes.
  2. **The `dotnet run --launch-profile http` exit-code-1 quirk** from the
     context block was reproduced this session: the profile's
     `ASPNETCORE_ENVIRONMENT=Development` is set, but
     `WebApplication.CreateBuilder` does **not** auto-load
     `appsettings.Development.Local.json` (the host's config chain is
     `appsettings.json` + `appsettings.{env}.json` only), so
     `ConnectionStrings:Kumunita` is missing and the app throws at
     `Program.cs:59`. The working launch command for smoke on this machine
     is to run the built DLL from `src/Kumunita.Web` with the env var the
     error message itself names:
     `ConnectionStrings__Kumunita='Host=localhost;Port=5433;Database=kumunita;Username=kumunita;Password=kumunita'`.
     That is a pre-existing launch-config gap (the sibling admin surfaces
     would hit it identically) — **not** a U05 regression, and fixing the
     config chain is out of U05's scope.
  3. **The non-admin smoke hit the AccessDenied page, not a bare 403** — the
     route returns a 403 status with the app's AccessDenied view (the
     sibling's `[Authorize(Roles=GlobalAdmin)]` shape routes unauthorized
     calls through the `AccessDenied` page; the HTTP status is 403, the
     body is the branded denied page). U06 should pin the **status code**
     (403), not the body, for the non-admin case — matching how the sibling
     is tested.
  4. **Dev-DB residue:** two more `messaging.toggle` `AccessAudit` rows
     (actor = Alex Admin's subject id, `Via = Admin`,
     `TargetKind = "messaging.toggle"`) from the ON→OFF→ON round-trip, on
     top of U04's `"smoke-test"` actor rows. Expected throwaway-dev-DB
     residue; the toggle is **left ON** (the U04 state, restored by the
     final POST→Open).
- **Not touched:** no service-layer files, no `IMessagingService` seams
  (U02/U03's `IsMessagingEnabledAsync` / `SetMessagingEnabledAsync`
  consumed as-is, no new seams on the frozen interface), no
  `IAuthorizationService` (C-M9·5), no tests (U06 owns them), no files
  outside the 4 deliverables.
- **Next:** U06 (Web tests — `MessagesControllerTests` + the admin
  Get/Post pair; re-pin any `Known`-count pin to 17 per U03's drift note
  2). The plan is at `in-progress/messaging-u06.md`.

## U06 — Web tests

- **Files written (the 1 deliverable, exactly):**
  1. `tests/Kumunita.Web.Tests/MessagesControllerTests.cs` (new).
- **Harness pattern (the `AnnouncementControllerTests` setup as applied):**
  NSubstitute stand-ins for `IMessagingService` + `IUserInfoService`;
  `controller.ControllerContext = new ControllerContext { HttpContext }`
  with a signed-in `ClaimsPrincipal` carrying the
  `Kumunita.Core.Identity.ClaimTypes.Subject` claim (the
  `KumunitaPrincipal.SubjectId` read — note `ClaimTypes` is ambiguous
  against `System.Security.Claims.ClaimTypes`, so the tests fully-qualify
  it); `controller.TempData = new TempDataDictionary(http,
  new NoOpTempDataProvider())` (the
  `AccountControllerSignupGateTests` pattern — `Send` / `Save` set
  `TempData["info"]` on success and would NRE without it). No Postgres, no
  Marten store, no `IDocumentStore` (neither controller consumes one).
  `BuildMessaging(enabled)` / `BuildAdmin(enabled)` helpers set the
  `IsMessagingEnabledAsync` stub up front. One NSubstitute gotcha hit: this
  version has no `ThrowsAsync` on `Task<T>` — the non-participant pin
  stubs with `.Returns(Task.FromException<ConversationDetail>(new
  KeyNotFoundException(...)))`.
- **The 10 test names (as written — zero renames from the plan):**
  `Messages_Index_ToggleOff_RendersDisabled_NoListCall` ·
  `Messages_Index_ToggleOn_RendersConversationList` ·
  `Messages_Thread_NonParticipant_404_NoView` ·
  `Messages_Thread_RendersMessages_WithUnreadMarkers` ·
  `Messages_Send_PostsBody_ToService` ·
  `Messages_Send_BlankBody_RendersError_NoServiceCall` ·
  `Messages_Nav_ToggleOff_EntryHidden` · `Messages_Nav_ToggleOn_EntryPresent` ·
  `AdminMessaging_Get_RendersCurrentState` ·
  `AdminMessaging_Post_FlipsToggle_AuditedByService`.
- **Key shapes as pinned against the shipped controllers (U04/U05
  consumed as-is, no controller edits):**
  - F5: `Index` toggle-off → `ViewResult` with
    `MessagesIndexViewModel.Disabled == true`, empty `Conversations` +
    `Candidates`, **`ListConversationsAsync` + `GetProfilesAsync` both
    `DidNotReceive`** (the no-list-call pin — the picker read is also
    gated by the toggle, stronger than the plan's minimum).
  - F5 (on): `Index` toggle-on → `ListConversationsAsync(Actor, 1)`
    received once; candidates exclude the actor (`OtherId` only) and
    blocked profiles (the `GetProfilesAsync(verifiedOnly: false)` seam,
    the controller's `!p.Blocked && p.SubjectId != actorId` filter);
    `Pager == null` when `HasMore == false` (the ADR 0090 null-pager
    one-page shape).
  - C-M9·1: `Thread` with `GetConversationAsync` throwing
    `KeyNotFoundException` → `NotFoundResult` (not a `ViewResult`, not a
    403) and **`MarkReadAsync` `DidNotReceive`** (no read-state write for
    a non-participant).
  - D8/F3: `Thread` happy path → model carries the `ConversationRef`,
    both `Message`s, `ActorId`, `ActorDisplayName` (the
    `GetProfileAsync` best-effort read), `Pager == null`;
    `MarkReadAsync(ConvoId, Actor)` received exactly once; the
    unread-marker shape (`!mine && m.ReadBy is null` + the
    `message.unread` badge) pinned against `Views/Messages/Thread.cshtml`
    source (the `SearchControllerTests.ReadViewSource` pattern).
  - Send: happy path → `RedirectResult` to `/messages/{id}` +
    `SendAsync(ConvoId, Actor, body)` received once; blank body →
    `Thread` view with `Error == true` and **`SendAsync` `DidNotReceive`**
    (the check is in the controller, before the service).
  - Nav: the two `Messages_Nav_*` pins follow the
    `Search_NavBox_Rendered_*` precedent — the
    `_AccountNav.cshtml` partial cannot be driven through a controller
    substitute (it renders in the layout), so both pin the view source:
    the `Messaging.IsMessagingEnabledAsync()` `@if` gate wraps the
    `href="/messages"` link + the `message.nav` key, and `message.nav`
    is registered non-empty in `KnownTranslationKeys.EnValues` (the F7
    shape; `KnownTranslationKeys_ParityTests` enforces the four-language
    closure). No rename of the pinned behavior — the plan's
    "hidden/present" is pinned as "gate wraps the entry" vs "entry +
    key present under the gate".
  - F6: `AdminMessaging_Get_RendersCurrentState` → `ViewResult` with
    `MessagingAdminViewModel.Enabled == true` + `IsMessagingEnabledAsync`
    received once; `AdminMessaging_Post_FlipsToggle_AuditedByService` →
    `RedirectToAction` to `Index` + `SetMessagingEnabledAsync(false,
    Actor)` received exactly once (the form's value + the actor's
    subject id — the audit row is the service's, pinned in U02's Core
    tests, not re-pinned here).
- **Drift (three, all record-don't-pause):**
  1. **The sanctioned `AllKinds.Count` re-pin was NOT applied — U03's
     drift note 2 premise is factually wrong.** U03's handoff (and the
     U03 commit message, `ec1f795`) claim "Known grows 16 → 17". Verified
     against the shipped code: `NotificationKinds.Known` still ends at
     `Announcement, CommunityPost, PageChild` (16 entries) — the `ec1f795`
     diff for `NotificationKinds.cs` adds only the `MessageNew` constant
     (+ its doc comment), and does **not** append it to the `Known` list.
     The `NotificationsControllerTests` `AllKinds.Count == 16` pin is
     therefore **currently passing** (it is inside the 521/521 green run
     below), and re-pinning it to 17 would turn a green pin red and
     contradict the shipped registry. The design doc (§D6) mandates only
     "one new constant on the closed set, not in `OptInKinds`" —
     `Known`-list membership was U03's own drift decision, and it was
     recorded but never shipped. No `message.new` toggle exists on the
     settings page today, consistent with the 16-entry pin. **Left
     untouched; recorded here so U07 does not re-attempt the re-pin.**
     (If a future lane does add `message.new` to `Known`, the pin
     follows in that lane's own unit.)
  2. **NSubstitute `ThrowsAsync` is unavailable** in this NSubstitute
     version for `Task<T>` return stubs — stubbed with
     `Task.FromException` + `.Returns(...)` instead (see harness
     pattern). Pure test-side idiom; no production impact.
  3. **`Messages_Index_ToggleOff_RendersDisabled_NoListCall` pins more
     than the plan's text** — the shipped controller also skips the
     picker's `GetProfilesAsync` read on the disabled path (it returns
     the disabled view immediately after the toggle read), so the pin
     asserts `DidNotReceive` on both the list and the picker. This is
     the shipped behavior pinned, not a rescope: the plan's pin is
     "no list call", and the extra assertion only makes the F5 gate
     stronger.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (0 errors, 0
  warnings). `Kumunita.Web.Tests.MessagesControllerTests` **10/10** via
  `dotnet exec … -class "Kumunita.Web.Tests.MessagesControllerTests"`
  (0.6s, no Docker). **Full `Kumunita.Web.Tests` assembly 521/521**
  (13.99s, Docker up/down clean) — proves the
  `NotificationsControllerTests` `AllKinds.Count == 16` pin stays green
  per drift note 1 above.
- **Not touched:** no production files (U04's `MessagesController` +
  U05's `AdminMessagingController` consumed as-is — every pinned test
  passed against the shipped code on the first green build, so no
  `## U06 — Drift pause` was needed); no `NotificationKinds.cs` (drift
  note 1 — the 16 pin is correct as shipped); no service-layer files; no
  `IAuthorizationService`; no files outside the one deliverable.
- **Next:** U07 (close the milestone — the roadmap flip + doc parity).
  The plan is at `in-progress/messaging-u07.md`. **Carry the drift note 1
  correction into the U07 close-out: do not re-pin `AllKinds.Count` to
  17 — it is 16 in the shipped code and its pin is green.**

## U07 — close the milestone

- **Files touched (the doc-parity trio, moved together + the two doc
  updates — exactly the deliverables, six groups):**
  1. `src/Kumunita.Web/Milestones.cs` — M9 Messaging
     `StatusNext` → `StatusDone`; M10 (PWA and responsive design)
     `StatusPlanned` → `StatusNext`; M11–M14 stay `StatusPlanned`
     (the single-in-progress invariant now points at M10).
  2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — `M9` added to the
     shipped-done list; the in-progress pin renamed to
     `M10_Is_The_Single_InProgress_Milestone_And_M11_Through_M14_Are_Planned`
     (single `StatusNext` = `M10`; planned list retargeted to
     `M11–M14`); the exact-order pin (`M0…M14` + the named lanes) was
     already in step and needed no change.
  3. `README.md` — Status line → "**M10 in progress** (PWA and responsive
     design); M1–M9 and all named lanes are done … messaging (`M9`),
     on one server-rendered stack over a single Postgres."; Roadmap M9
     entry `**In progress**` (ADR 0105) → `**Done.**` (ADR 0105); M10
     entry gains `**In progress.**`.
  4. `docs/STATUS.md` — the "next is M9" sentence → "**M9 is done** —
     messaging (1:1 resident messaging, admin-toggleable, off by default;
     ADR 0105); **next is M10** — PWA and responsive design. Then M11–M14
     (portability, iCal, logging & analytics, Events+Projects
     integration …)".
  5. `docs/ARCHITECTURE.md` (shape-of-code, three additive insertions):
     the `Messaging/` context row added to the solution tree (after the
     `Search/` row — the M9 ✓ ADR 0105 shape: the two docs + the
     `M9DocTypes` surface + the `IMessagingService` / `MessagingService`
     seams + the participant-by-id access story + the write audit
     `TargetKind`s `message` / `messaging.toggle` + the M6-lane
     `message.new` nudge); the §3 feature-modules list gains
     "Messaging (M9 ✓ — ADR 0105)"; the §2 "shape of the code" paragraph
     gains the `Messaging/` (M9) is-now-live block (seam list, zero-new
     authorization-surface statement, the audit `TargetKind`s, the nudge
     kind) alongside the M5/M6/M8 blocks.
  6. **U06's carry-in honored:** the `NotificationsControllerTests`
     `AllKinds.Count == 16` pin was **not** re-pinned (U06's drift note 1
     is correct as shipped — `message.new` never joined the `Known`
     list; the 16 pin is green in the 521/521 run below).
- **Exit — everything green (2026-09-27):**
  `dotnet build Kumunita.slnx -c Debug` — 0 errors (the single warning
  is U03's pre-existing `MessagingServiceTests.cs` xUnit2013, recorded in
  the U05 entry). `Kumunita.Core.Tests` **956/956** (0 failed, 92.9s —
  incl. `MessagingServiceTests` 14/14). `Kumunita.Web.Tests` **521/521**
  (0 failed, 14.2s — incl. `MilestonesTests` green with M10 sole
  in-progress and `MessagesControllerTests` 10/10).
- **Not touched:** no production code, no tests beyond the retargeted
  `MilestonesTests` pins (no new tests — the unit plan's own rule), no
  files outside the six deliverable groups.
- **Plan file moved:** `in-progress/messaging-u07.md` → `done/` (this
  unit is the last one — `in-progress/` is now empty; `done/` holds all
  eight `messaging-u*.md` plans, U00–U07).

## Summary

- **Capability shipped (M9, ADR 0105):** 1:1 resident messaging — a
  signed-in resident opens a conversation with another resident,
  exchanges plain-text messages (≤ 2000 chars, immutable in M9), and both
  participants see per-recipient read state. The feature is
  **admin-toggleable and OFF by default** (`LocaleSettings.MessagingEnabled`
  `false` floor — the deliberate inverse of the codebase `true`-floor
  convention); the GlobalAdmin flips it at `/admin/messaging`
  (`AdminMessagingController`, the ADR 0101 shape). Participant-only
  access — a non-participant (GlobalAdmin included) gets a **non-leaky
  404**; no `Audience`, no `IAuthorizationService` call, zero new
  `AccessAction` / `AccessVia` / `IAuditableResource` adapter. New
  messages nudge the recipient through the existing M6 `Notification`
  lane (kind `message.new`).
- **The seams (frozen as of M9 close):** `IMessagingService` —
  `IsMessagingEnabledAsync()` · `SetMessagingEnabledAsync(bool, actorId)`
  · `OpenConversationAsync(actorId, otherId) → ConversationRef` ·
  `GetConversationAsync(conversationId, actorId, page) →
  ConversationDetail` · `ListConversationsAsync(actorId, page) →
  ConversationList` · `SendAsync(conversationId, actorId, body)` ·
  `MarkReadAsync(conversationId, actorId)`; the `ConversationRef` /
  `ConversationList` / `ConversationDetail` records on the ADR 0090
  `HasMore` discipline. `MessagingService` composes the
  `IDocumentStore` + optional `IUserInfoService` / `NotificationService`
  (the ADR 0077 idiom). `M9DocTypes` carries the two docs
  (`Conversation` + `Message`), the unique `(ParticipantA, ParticipantB)`
  pair index (the F1 idempotency witness) + the
  `(ConversationId, Created)` thread index.
- **The invariants, as landed (C-M9·1–7):**
  1. **C-M9·1** participant-only, no operator peek — non-leaky 404,
     pinned (`GetConversation_NonParticipant_404_Not403`,
     `GetConversation_GlobalAdminNonParticipant_404`,
     `Messages_Thread_NonParticipant_404_NoView`).
  2. **C-M9·2** the toggle is a hard service gate — every seam refuses
     (403) when off, pinned (`ToggleOff_AllSeamsRefuse_403`,
     `Messages_Index_ToggleOff_RendersDisabled_NoListCall`).
  3. **C-M9·3** audit always-on, in-transaction, one row per write —
     `message.open` / `message.send` (`Via = Owner`, `TargetKind =
     "message"`), `messaging.toggle` (`Via = Admin`, `TargetId =
     "messaging.toggle"`), pinned (`SetMessagingEnabled_Toggle_StoresFlagAndAuditRow`).
  4. **C-M9·4** reads never audit — list / thread / mark-read emit zero
     rows.
  5. **C-M9·5** zero new authorization surface — verified at every unit's
     entry-reads against `Decision.cs` / `AccessAudit.cs` (no enum
     append was needed).
  6. **C-M9·6** plain-text, capped (2000), immutable —
     `Send_BlankBody_ArgumentException` / `Send_OverCap_ArgumentException`.
  7. **C-M9·7** Marten-native documents, one new surface —
     `M9DocTypes` delta-detected, idempotent, wired into the dev-loop
     boot path (the `SchemaBootstrap.ApplyAsync` apply path consumes it
     via `ApplyAllConfiguredChangesToDatabaseAsync`).
- **The test floor as closed:** `MessagingServiceTests` **14/14**
  (U02's 2 toggle pins + U03's 12 behavior pins);
  `MessagesControllerTests` **10/10** (U06); `MilestonesTests` green
  (M9 done, M10 sole in-progress, M11–M14 planned); full assemblies
  green — Core **956/956**, Web **521/521**.
- **The deferred lanes (the follow-on entries, own ADRs):**
  **rich content** on messages (the ADR 0025/0031/0034 machinery is the
  entry, not M9's value); **edit / delete** of a sent message (the ADR
  0024 soft-delete shape is the entry — M9's `Message` is deliberately
  immutable); **read receipts / delivery confirmation** (the M6
  deferral, unchanged); **group messaging** (the ADR 0013 membership
  lane is the entry — a follow-on surface, not a widening of M9's
  participant-by-id shape). ~~**`message.new` on the notification
  settings page**~~ — **shipped post-close** (see the
  "## Post-close" section below): `MessageNew` now joins
  `NotificationKinds.Known` (17 entries) and the `AllKinds.Count` pin is
  re-pinned 16 → 17 — the resident owns the per-kind mail toggle.
- **Milestone state:** M9 is **done**; the single-in-progress pointer is
  **M10 (PWA and responsive design)**; M11–M14 remain planned. The
  doc-parity trio (README ↔ `Milestones.cs` ↔ `MilestonesTests.cs`) moved
  together in this unit. **This `## Summary` is the last line the handoff
  note receives — the M9 milestone is closed.**

## Post-close — `message.new` joins `Known` (the U06 drift note 1 lane, shipped)

- **Entry state:** U06's drift note 1 recorded that `NotificationKinds
  .MessageNew` (ADR 0105 D6) was defined but **not** appended to the
  `Known` closed set, and that U07 must not re-pin `AllKinds.Count`
  (16) — the settings page had no `message.new` toggle.
- **Why the lane was pulled forward (the seam audit):** the verification
  pass over the closed M9 surface found the consequence U06 had only
  recorded: a recipient who stores *any* per-kind preference gets a
  non-empty `KindsEnabled` list, and `EmailEnabledForAsync`
  (`NotificationService.cs`) suppresses email for any kind not in that
  list — so `message.new` mail was **silently suppressed for exactly the
  residents who had customized their notifications**, with no settings
  toggle to fix it. That is a broken feedback loop (the philosophy's
  "a signal with no owner and no response decays into noise"), not a
  missing feature.
- **What shipped (2 lines + 1 pin):**
  1. `src/Kumunita.Core/Notifications/NotificationKinds.cs` — `MessageNew`
     appended to `Known` (17 entries). It remains **outside
     `OptInKinds`**, so the default stays opt-OUT / enabled — matching
     the design doc §D6 and the `MessageNew` constant's own doc comment
     ("not in `OptInKinds`").
  2. `tests/Kumunita.Web.Tests/NotificationsControllerTests.cs` — the
     `AllKinds.Count` pin re-pinned 16 → 17 (the sanctioned re-pin U06's
     drift note 1 held in escrow for "the lane that adds it").
- **What did not ship:** no new code path, no new key (the
  `notifications.kind.message.new` + `notification.message.new.*` keys
  already landed in U03/U04), no view change (the preferences view
  renders `NotificationKinds.Known` generically — the toggle appears
  automatically with the label "New message" / "Neue Nachricht" /
  "Nouveau message" / "Ny besked").
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Web.Tests` **521/521** (the re-pinned count passes; no other
  test references `Known` membership); Core assembly unaffected (nothing
  there pins `Known` membership — `MessagingServiceTests` 14/14 within
  the 956/956 run of the closed surface).
- **Net effect:** the resident now has the per-kind mail toggle for
  `message.new` on `/notifications/preferences`, and a stored preference
  list can no longer silently suppress message mail — the loop is closed
  and the resident owns the signal.
