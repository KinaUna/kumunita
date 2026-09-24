# M6 Notifications — rolling handoff note

One `## U#` section per unit, **appended** (never
rewritten). Each unit writes exactly one short section
before it exits; the next unit reads only that section
+ its own entry-read list. Moves with the lane folder at
close (U10: `in-progress/notifications/` →
`done/notifications/`).

**Convention:** each section is ≤ 10 lines; it states
(1) what the unit *shipped* (the closed deliverable
set, the *one-line* shape), (2) the *one thing* the
next agent must not change (the frozen seam, the
pinned name, the *exact* shape), (3) any *drift*
recorded (the register's unit-series rule 9 — the
*sole* mechanism for a mid-lane doc fix). **No
prose beyond those three bullets** (the M4 / M5
handoff-note precedent — the *lean* shape).

---

## U00

- **Shipped:** `docs/design/m6-notifications-design.md` Part 1 (§1–§5 complete; §6.1–§6.6 placeholder headers for U01). Eleven invariants C-M6·1…11 (verbatim from the register), twelve FACES F1–F12 (verbatim), eleven decisions D1–D11 in [PROPOSED] state, one-to-one D#↔C-M6·# mapping. No code, no ADR.
- **One thing the next agent must not change:** the §5 D#↔C-M6·# mapping is 1:1 and complete (eleven each); U01 locks them into ADR 0076 as-is — do not renumber, merge, or split any D#
- **Drift:** none recorded.

## U01

- **Shipped:** design doc Part 2 (§6.1–§6.6 complete: exact C# for `Notification` / `NotificationPreference` / `NotificationKinds` (nine constants) / `M6DocTypes` / the six-method `NotificationService` surface; the 8-key idempotency table; the 12 pinned Core test names + 5 Web route pins; the 3 gate-test names; the drift-guard rule). `docs/adr/0076-notifications-inbox-and-recipient-email.md` authored **Accepted**, D1–D11 1:1 with C-M6·1…11 (locked as-is, no renumber/merge/split). Roadmap trio **confirmed, no edit**: `Milestones.cs` (M5 `StatusDone` / M6 `StatusNext` / M7 `StatusPlanned`), README Roadmap (M5 "Done" / M6 "In progress"), `MilestonesTests.cs` pins the exact trio.
- **One thing the next agent must not change:** the §6.2 `EmitAsync` signature — `Task<Notification> EmitAsync(IDocumentSession session, string recipientId, string kind, string idempotencyKey, string? body, CancellationToken ct = default)` — and the dedup pin: `EmitAsync` dedups **itself** via an `IdempotencyKey` look-up on the caller's session (F10: a same-key re-emission returns the existing row, no second row, no second `StageAsync` call). U02 codes the docs against §6.1 verbatim; U03 the service against §6.2 verbatim.
- **Drift:** none recorded. (Note: §6.3 names the dedup as **two layers** — the service-side `IdempotencyKey` look-up (inbox) + the `StageAsync` guarantee (email) — which is how F10's "no second inbox row" is testable against the service alone; read D4/C-M6·4 as the emitter-supplies-a-stable-key + service-enforces-the-no-op split.)

## U02

- **Shipped:** `src/Kumunita.Core/Notifications/NotificationKinds.cs` (nine `public const string` members + the `Known` ordered list, the `KanbanStatuses` shape), `Notification.cs` (8 fields: `Id` / `RecipientId` / `Kind` / `IdempotencyKey` / `SourceId?` / `Subject?` / `Body?` / `Created` / `ReadAt?`), `NotificationPreference.cs` (3 fields: `RecipientId` doc-id / `KindsEnabled?` nullable list / `Updated?`), `M6DocTypes.cs` ((RecipientId, Created) feed index + IdempotencyKey index; `NotificationPreference` bare), and the boot-wiring line `M6DocTypes.Configure(opts)` after `M5DocTypes` in `src/Kumunita.Web/Program.cs` (the true M5 precedent location — the U02 entry-read named `DependencyInjection.cs`, which holds no surface registration). All four Core files are **verbatim** from design doc §6.1. `dotnet build Kumunita.slnx -c Debug` green (one pre-existing unrelated `BoardDetail.cshtml` CS8600 warning).
- **One thing the next agent must not change:** the §6.1 field sets, esp. `Notification.IdempotencyKey` (non-nullable, `string.Empty` default — the F10 dedup anchor U03's `EmitAsync` looks up on the caller's session) and `NotificationPreference.RecipientId` being the **document id** (one row per recipient — U03's `GetPreferencesAsync` / `SetPreferencesAsync` load/upsert by it, no separate `Id` field).
- **Drift:** none against the frozen text; the boot-wiring file location follows the true `M5DocTypes` precedent (`Program.cs`), recorded here per rule 9.

## U03

- **Shipped:** `src/Kumunita.Core/Notifications/NotificationService.cs` (sealed, **no interface** — the design doc §6.2 pin; ctor `NotificationService(IDocumentStore, IUserInfoService, ITranslationProvider, IMailerStage)`; six public methods **verbatim** from §6.2: `EmitAsync(IDocumentSession, recipientId, kind, idempotencyKey, body?, ct)`, `ListInboxAsync`, `CountUnreadAsync`, `MarkAllReadAsync`, `GetPreferencesAsync`, `SetPreferencesAsync`; `const int InboxCap = 50`) + the `AddTransient<Notifications.NotificationService>(...)` line in `src/Kumunita.Core/DependencyInjection.cs` (the M4 / M5 "AddTransient with the store injected" shape). No `IAuthorizationService` anywhere (C-M6·3); `EmitAsync` stores the row **unconditionally** and **conditionally** calls `IMailerStage.StageAsync` (C-M6·7); inbox-side dedup via the `IdempotencyKey` look-up on the caller's session before storing (F10, C-M6·4); subject / body resolved in the recipient's `Profile.EmailLanguage` via `ITranslationProvider.GetAsync($"notification.{kind}.subject" / ".body", lang)` with the UGC snippet (`body`) appended — the M4 `BuildLocalizedReminder` shape (C-M6·6, F12).
- **One thing the next agent must not change:** the **six public method signatures** (parameter order / types / nullability / return types) are the frozen surface U04 / U05 / U06 / U07 / U09 code against; the **key shape** the subject / body templates use is `notification.{kind}.subject` / `notification.{kind}.body` (U07's `kw-l` keys must land as exactly these two per kind — a mismatch breaks F12 and the F1–F8 / F9 email assertions); `EmitAsync` **does not call `SaveChangesAsync` itself** (the caller's commit is the single commit — the C3 pin, U04 / U05 must own the commit).
- **Drift (recorded per rule 9 — API-surface, not design-doc):** the entry-read named a "M3b bulk-update precedent" for `MarkAllReadAsync`, but **Marten 9.31.2's `IDocumentSession` has no expression-based `Update<T>(where, apply)` and no synchronous `Load<T>()`** (verified via reflection on the actual assembly: the session surface is `LoadAsync<T>` / `Update(T[])` / `Update(IEnumerable<T>)` only). U03 therefore implements `MarkAllReadAsync` with the codebase's universal write idiom (query the unread set → mutate `ReadAt = now` on each in memory → one `Store(unread)` → one `SaveChangesAsync`), and the D7 email gate uses `LoadAsync<NotificationPreference>` on the caller's session. This is **not** a re-derivation of the design — the §6.2 shape (the "one Update, no per-row loop" intent, the lean-default gate) is preserved; only the *Marten call* is the codebase idiom (the M5 `ProjectService` / M4 `EventService` / M1 `IdentityService` write-lane shape). If U09's F9 / F10 / gate tests hit a mismatch, this is the line to check first.

<!-- U04 appends `## U04` here on completion. -->
<!-- U05 appends `## U05` here on completion. -->
<!-- U06 appends `## U06` here on completion. -->
<!-- U07 appends `## U07` here on completion. -->
<!-- U08 appends `## U08` here on completion. -->
<!-- U09 appends `## U09` here on completion. -->
<!-- U10 appends `## U10` (the close) here on completion. -->
