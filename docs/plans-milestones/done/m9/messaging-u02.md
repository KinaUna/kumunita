# M9 Messaging — U02 · The admin toggle seams on `IMessagingService`

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + the 2 pinned tests.
> **Unit-series rule:** never touch files outside your own Deliverables; no
> tests beyond the pinned list; no new seams on frozen interfaces.
>
> **This unit creates `IMessagingService` + `MessagingService` with the two
> toggle seams first; U03 appends the rest.** Do not add the open/send/list
> seams here — that is U03's, and its entry reads assume U02's shape.

## Goal

D2's toggle: the `LocaleSettings.MessagingEnabled` additive bool (`false`
floor) + the two toggle seams (`IsMessagingEnabledAsync()` /
`SetMessagingEnabledAsync(bool, actorId)`) + the one `messaging.toggle`
audit row (C-M9·3, the ADR 0101 write-row shape).

## Context (the [PROPOSED] toggle shape U00 locked)

- **D2 · Admin toggle, default OFF.** A `LocaleSettings.MessagingEnabled`
  additive bool. A missing settings row reads as **off** (`false` floor —
  the deliberate inverse of ADR 0101's `true` floor, because messaging is a
  privacy-sensitive opt-in). When off, **every** other messaging seam
  refuses (403) — the toggle is enforced in the service, not only the view.
- **D5 · Toggle audit row.** `SetMessagingEnabledAsync` commits one
  `AccessAudit` row in the same session: `TargetKind = "messaging.toggle"`,
  `Via = Admin`, `Outcome = Allow` (the ADR 0101
  `announcementcomments.set-enabled` shape verbatim — **verify the exact
  `AccessVia`/`AccessOutcome` enum member names against the actual
  `AccessAudit.cs` + `Decision.cs`, not this text**). An empty `actorId` is
  403 (`UnauthorizedAccessException`).
- **Seam placement.** On **`IMessagingService`** (the ADR 0101
  toggle-on-own-service precedent — `AreAnnouncementCommentsEnabledAsync`
  lives on `IAnnouncementService`, not `IIdentityService`). If your entry
  reads contradict this, stop: write `## U02 — Drift pause` in the handoff
  note and do not implement.

## Entry reads (6)

1. `docs/design/m9-messaging-design.md` §toggle (the locked shape).
2. `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the `LocaleSettings`
   doc + the `AnnouncementCommentsEnabled` field D2 mirrors — add
   `MessagingEnabled` next to it).
3. `src/Kumunita.Core/Announcements/AnnouncementService.cs`
   (`AreAnnouncementCommentsEnabledAsync` /
   `SetAnnouncementCommentsEnabledAsync` — the floor + audit-row shape to
   copy, ~line 1152/1163).
4. `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit doc — the
   field names + the `AccessVia`/`AccessAction`/`AccessOutcome` values D5
   must use).
5. `tests/Kumunita.Core.Tests/AnnouncementServiceTests.cs` (~line 1906 — the
   toggle pinned-test pair to mirror: floor + toggle-sets-flag-and-audit).
6. `docs/adr/0101-announcement-comments-lane.md` §toggle.

## Deliverables (4)

- `src/Kumunita.Core/Localization/LanguageCatalog.cs` — one additive
  `public bool MessagingEnabled { get; set; } = false;` (the `false` floor,
  the inverse of the `true`-floor convention — the design doc explains why).
- `src/Kumunita.Core/Messaging/IMessagingService.cs` — the interface, with
  the **two toggle seams only** for now (U03 appends the rest):
  ```csharp
  Task<bool> IsMessagingEnabledAsync();
  Task SetMessagingEnabledAsync(bool enabled, string actorId);
  ```
- `src/Kumunita.Core/Messaging/MessagingService.cs` — ctor over
  `IDocumentStore` only (U03 will gain the optional `NotificationService?`
  seam); the two toggle methods (read-with-floor; write + audit row, one
  `SaveChangesAsync`).
- `src/Kumunita.Core/DependencyInjection.cs` — the `AddTransient<IMessagingService>`
  registration (the `AddTransient<I…Service>` factory shape).

## Pinned tests (2 — `tests/Kumunita.Core.Tests/MessagingServiceTests.cs`,
this file is **yours to create**; U03 extends it, you do not write its other
tests):

- `IsMessagingEnabled_FreshInstance_FloorsToFalse` — a fresh store (no
  `LocaleSettings` row) → `IsMessagingEnabledAsync()` returns `false` and
  emits **no** audit row (the floor is a read, the ADR 0101
  `IsSignupOpen_FreshInstance_FloorsToTrue_NoAuditRow` shape with the
  `false` floor).
- `SetMessagingEnabled_Toggle_StoresFlagAndAuditRow` —
  `SetMessagingEnabledAsync(true, "u-admin")` → the flag is `true`, and
  exactly one `AccessAudit` row with `TargetKind = "messaging.toggle"`,
  `Via = Admin`, `Outcome = Allow` (the ADR 0101
  `SetAnnouncementCommentsEnabled_Closed_StoresFlagAndAuditRow` shape).

## Exit

`dotnet build Kumunita.slnx -c Debug` green; the 2 pins pass
(`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
-class "Kumunita.Core.Tests.MessagingServiceTests"`).

Append a `## U02 — toggle seams` section to the handoff note: the seam
signatures as implemented, the `AccessAudit` field values observed (name the
`AccessVia`/`AccessOutcome` enum members you used), the `false`-floor text as
written, any drift (e.g. the seam placement contradicted the design doc).

**Last action:** once the Exit above is satisfied and the `## U02` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u02.md` to
`docs/plans-milestones/done/m9/messaging-u02.md`. Each unit moves only its own
file as it completes — U03's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
