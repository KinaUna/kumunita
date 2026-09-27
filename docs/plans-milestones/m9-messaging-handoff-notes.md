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
