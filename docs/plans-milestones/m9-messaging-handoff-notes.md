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
