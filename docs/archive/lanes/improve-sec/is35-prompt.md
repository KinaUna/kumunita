---
id: is35-prompt
title: "IS35 prompt — RetentionService part 2: notifications, error reports, log files (E5, D6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS35 — `RetentionService` part 2: notifications, error reports, log files

## What you are doing

You are the **thirty-sixth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS34b are committed.
IS34a built the **work** (`Kumunita.Core.Privacy.RetentionService`, a `static`
class with `RunAsync(store, options, now, ct) → Task<RetentionSweepResult>` —
the part-1 types: sessions, tokens, dead letters, outbox) and the **message**
(the 1-day `RetentionTick`); IS34b wired the **`RetentionTickHandler`** (a thin
`static` adapter that calls `RunAsync` and re-yields the tick) + the two
`Program.cs` retention touches (the `RetentionOptions` bind + the seed).

**Finding E5 (severity M), part 2:** the plan's **IS35** section
(`docs/plans-milestones/plan-improve-sec.md`, see `#### IS35`):

> Extend `RetentionService` to `Notification` (read 90 days, unread 365) and
> resolved `ErrorReport` (180 days), and call `RollingFileSink.Retain` daily.

That is the whole unit. The D6 defaults these three rows use are **already on
IS33's `RetentionOptions`** — `NotificationsRead` (90), `NotificationsUnread`
(365), `ErrorReports` (180) — so **no new option keys are needed** for the two
Marten types. The daily log-file leg is the one piece that is *not* a D6 row:
it reuses the existing `FileSinkOptions` (`Directory`, `RetentionDays`,
defaulted to `14`) — the D6 decision says *"File-log `Retain` runs daily, not
only at boot."* The spec is the **IS35** section + the **D6** decision row.

## The one thing to get right

**Extend the *existing* `RetentionService.RunAsync` seam — do not add a new
job, a new handler, or a new tick.** The handler, the `RetentionTick`, and the
`Program.cs` wiring are IS34b's and already in place; this unit only widens the
sweep `RunAsync` performs, so the recurring leg IS34b wired now also covers
part 2. Mirror the part-1 style exactly: every cutoff is
`RetentionPolicy.CutoffFor(options, "<key>", now)` (a `null` cutoff from a
`0`-valued/absent key **skips** that type); batched id-collection +
`Delete<T>(id)` + one `SaveChangesAsync` per type; append the new counts to
`RetentionSweepResult` (no PII — counts only, per the IS08 gate).

- **`Notification`** (class `Operational`, `Retention__*` via
  `NotificationsRead`/`NotificationsUnread`): delete a row when
  `ReadAt != null && ReadAt < (now − NotificationsRead)` **or**
  `ReadAt == null && Created < (now − NotificationsUnread)`. Read = 90 d,
  unread = 365 d (D6). Use `Notification.Created` for the age anchor and
  `ReadAt` (nullable; `null` = unread) to split the two cohorts.
- **resolved `ErrorReport`** (class `AuditModeration`, `ErrorReports`): delete
  when `ResolvedAt != null && ResolvedAt < (now − ErrorReports)`. Unresolved
  reports (`ResolvedAt` null) are **always kept** — never delete an unresolved
  report.
- **log files:** call `RollingFileSink.Retain(dir, retentionDays, now)` once
  per run (it already deletes `app-*.log` older than `now − retentionDays`,
  per-file `try/catch`, returns a count — the D6 "no per-row crash" shape).

## The design decision to resolve (the one real call this unit makes)

The two Marten types are straightforward, but the **log-file leg** is a seam
you must close cleanly, because `FileSinkOptions` (the log `Directory` +
`RetentionDays`) is **not** DI-registered — it is a local POCO read in
`Program.cs` (`GetSection(FileSinkOptions.SectionName)`), and it is **separate
from** `RetentionOptions`. The IS34b handler currently calls
`RetentionService.RunAsync(store, options.Value, now)` with no log config. So
you must get the log `dir` + `retentionDays` to `RunAsync`. Cleanest options —
pick one and note it in the handoff:

1. **Recommended:** give `RunAsync` two optional params (defaulting to "skip
   the log leg", e.g. `string? logDir = null, int logRetentionDays = 0`) so the
   IS34b caller is **unchanged** when no log config is supplied; then have the
   IS34b `RetentionTickHandler` pass `FileSinkOptions`'s `Directory` +
   `RetentionDays` (it can take an `IOptions<FileSinkOptions>`-style param or
   read the same section). This is a **justified touch to the IS34b handler** —
   it is the single caller and the only way the frozen "Core does the work"
   seam receives the log config. Record that touch in the handoff.
2. Alternative: bind `IOptions<FileSinkOptions>` in `Program.cs` (one extra
   bind line) and inject it through the handler. More `Program.cs` surface —
   prefer option 1 unless the handler shape resists it.

Whichever you pick: `RollingFileSink.Retain` stays pure (BCL-only, temp-dir
testable); the sweep just calls it. The `RetentionSweepResult` gains a
`LogFilesDeleted` count.

## Constraints that keep existing tests green

- **`RetentionTick` + the two `Program.cs` retention lines are IS34b's and
  frozen** unless option 1 above justifies the single handler-param touch. Do
  **not** add a second tick, a second handler, or a second seed.
- **`RetentionOptions` (IS33) is frozen** — it already carries the three D6
  keys you need; do not add keys. Read them via the existing
  `RetentionPolicy.CutoffFor(options, "<key>", now)` (the key strings are
  `NotificationsRead` / `NotificationsUnread` / `ErrorReports` — verify they
  resolve in `RetentionOptions.DaysFor` before relying on them; if a key is
  missing, pause per rule 9).
  - **Key-name drift to watch (real, already in the codebase):** the
    `[DataClass(..., retentionKey: "...")]` strings on the POCOs do **not**
    match the `RetentionOptions.DaysFor` key strings. `Notification` says
    `retentionKey: "NotificationUnreadDays"`, but the machine key the service
    must key off is `NotificationsRead`/`NotificationsUnread`. Same family of
    drift on the part-1 types (`SessionDoc` `retentionKey:"SessionDays"` vs
    the service's `"Sessions"` key; `ErrorReport` `"ErrorReportDays"` vs
    `"ErrorReports"`; `IdentityToken`/`EscalationToken` `"TokenDays"` vs
    `"Tokens"`). **IS34a's `RetentionService` correctly uses the
    `DaysFor` key strings** (not the `[DataClass]` retentionKey) — so use
    `NotificationsRead` / `NotificationsUnread` / `ErrorReports` for
    `CutoffFor`, and do **not** re-derive the key from the `[DataClass]`
    attribute. (This drift is a real gap the IS41 doc-truth pass may want to
    reconcile; note it in your handoff but do **not** fix it here — that is a
    different seam.)
- **`PortabilityDocTypes` (M11) is untouched** (C-M11·5 closed-set pin) — this
  unit deletes rows of existing types; it adds no doc type and no doc-type
  surface.
- **PII rule:** the test uses synthetic fixture ids (`user-a`),
  `a@example.test`-style addresses, and temp-dir log file names only — no real
  PII anywhere (rule 8 / the IS08 gate). `ErrorReport.ContactEmail` must never
  be logged or asserted on value (counts only).

## Deliverables (≈ 3–4 files)

1. **`src/Kumunita.Core/Privacy/RetentionService.cs`** — extend `RunAsync` for
   `Notification` (two cutoffs) + resolved `ErrorReport` (one cutoff) + the one
   `RollingFileSink.Retain(dir, retentionDays, now)` call; append the new
   count(s) to `RetentionSweepResult`. Keep the part-1 blocks unchanged.
2. **`docs/ops/configuration-reference.md`** — add the three `Retention__*`
   keys (`Retention__NotificationsRead` = 90, `Retention__NotificationsUnread`
   = 365, `Retention__ErrorReports` = 180) next to the other `Retention__*`
   rows (IS34b/IS33's bind), and a one-line note that the file-log `Retain`
   now also runs daily via the retention tick (D6).
3. **`tests/Kumunita.Core.Tests/RetentionServiceTests.cs`** — append the three
   tests below (the existing store-harness `BootAsync` idiom; register
   `NotificationDocTypes` + `ErrorReportDocTypes` on the scratch store if not
   already present — see `tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs`
   `≈:390` for the `ErrorReportDocTypes.Configure(opts)` line).
4. **(If option 1 above)** the single `RetentionTickHandler.Handle` param touch
   + a one-line `FileSinkOptions` read in the handler — recorded in the handoff.

## Entry reads (only these — no whole-file reads)

- `sed -n '1,40p' src/Kumunita.Core/Privacy/RetentionService.cs` and its
  `RetentionSweepResult` record (the IS34a surface you extend — read
  `RunAsync`'s exact part-1 blocks to mirror them; the record is at the file
  end).
- `sed -n '1,60p' src/Kumunita.Core/Privacy/RetentionOptions.cs` (confirm the
  `NotificationsRead` / `NotificationsUnread` / `ErrorReports` keys +
  `DaysFor`; IS33, frozen).
- `sed -n '1,40p' src/Kumunita.Core/Privacy/RetentionPolicy.cs` (the
  `CutoffFor` seam — IS33, frozen).
- `cat src/Kumunita.Core/Notifications/Notification.cs` (fields: `Created`,
  `ReadAt` nullable — `null` = unread; the `[DataClass]` retentionKey note).
- `sed -n '15,80p' src/Kumunita.Core/ErrorReports/ErrorReport.cs` (fields:
  `ResolvedAt` nullable — `null` = unresolved; `SubjectId`,
  `ContactEmail`, `TriageStatus`).
- `cat src/Kumunita.Core/Logging/RollingFileSink.cs` (the pure
  `Retain(dir, retentionDays, now)` — call it, don't re-implement it) and
  `src/Kumunita.Core/Logging/FileSinkOptions.cs` (`Directory` +
  `RetentionDays`, the `SectionName` `"Logging__File"`).
- `sed -n '1,60p' tests/Kumunita.Core.Tests/RetentionServiceTests.cs` (the
  `BootAsync` scratch-store idiom + the `PostgresFixture` + the
  `CoreDocTypes`/`SessionDocTypes`/`EscalationDocTypes` registration you extend
  with `NotificationDocTypes` / `ErrorReportDocTypes`).
- `sed -n '380,395p' tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs`
  (the `ErrorReportDocTypes.Configure(opts)` + `EscalationDocTypes.Configure(opts)`
  registration lines to mirror in the Core.Tests boot).
- `sed -n '1,40p' src/Kumunita.Web/SideEffects/RetentionTickHandler.cs` (the
  IS34b handler you may touch for the log-config param — read its exact
  `Handle` signature).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the **IS35** section + the **D6** decision row) +
  `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (append to `RetentionServiceTests`)

- `Retention_DeletesOldReadNotifications` — seed one **read** notification
  (`ReadAt = now − 100 d`, `Created` earlier) + one **unread** notification
  (`ReadAt = null`, `Created = now − 100 d`); with defaults (read 90 /
  unread 365), the **read** one is deleted (100 d > 90 d) and the **unread**
  one is **kept** (100 d < 365 d). Assert the counts + that only the read row
  is gone.
- `Retention_DeletesResolvedErrorReportsAfter180d` — seed one
  **resolved** report (`ResolvedAt = now − 200 d`) + one **unresolved** report
  (`ResolvedAt = null`, `ResolvedBy` null); with the default 180 d key, the
  resolved one is deleted, the unresolved one is **always kept**. Assert the
  count + that the unresolved row survives.
- `Retention_RetainsLogFilesDaily` — create a temp log dir with one
  `app-<old-date>.log` whose mtime is forced older than the cutoff and one
  recent one; run the sweep with the log leg enabled (pass the temp dir + a
  small retention); assert the old file is deleted, the recent one kept, and
  the `LogFilesDeleted` count is correct. Clean up the temp dir in a `finally`
  (the `RollingFileSink` test precedent). Use synthetic file names only.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] The 3 new tests green **and** IS34a's `RetentionServiceTests` (the 4
      part-1 tests) **and** IS33's `RetentionPolicyTests` **still** green — run
      all three classes in one command: `dotnet exec
      tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class
      "Kumunita.Core.Tests.RetentionServiceTests" -class
      "Kumunita.Core.Tests.RetentionPolicyTests"`.
- [ ] If you touched the IS34b handler, re-run
      `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/
      Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.RetentionTickHandlerTests"`
      → still 1/1 green.
- [ ] Every new cutoff is `RetentionPolicy.CutoffFor` (no re-derived
      `AddDays` in the service); a `0`-valued key skips its type (the fail-safe).
- [ ] Unresolved `ErrorReport`s are never deleted; unread notifications keep
      the 365-d rule. No PII in code, tests, or notes (counts only).
- [ ] `RetentionOptions` / `RetentionTick` / `PortabilityDocTypes` (M11)
      unchanged.
- [ ] `docs/ops/configuration-reference.md` documents the three `Retention__*`
      keys + the daily log-`Retain` note.
- [ ] One `## IS35` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the new `RetentionSweepResult` fields, the log-config seam decision
      + any handler touch, the test + pass count, next-unit notes).

## Definition of done (IS35)

- [ ] `RetentionService.RunAsync` now sweeps `Notification` (read 90 /
      unread 365), resolved `ErrorReport` (180), and runs the daily
      `RollingFileSink.Retain` leg; `RetentionSweepResult` carries the new
      counts; no PII.
- [ ] The log-config seam is resolved cleanly (recorded in the handoff) with
      the **least** `Program.cs` / handler surface.
- [ ] The 3 new tests green + IS34a/IS33 test classes still green + (if touched)
      the IS34b handler test still green; build green.
- [ ] `RetentionOptions` / `RetentionTick` / `PortabilityDocTypes` unchanged;
      no second tick/handler/seed.
- [ ] `docs/ops/configuration-reference.md` updated; one commit, one handoff
      section.
- [ ] **Next unit: IS36** (audit the unaudited admin reads — `AdminController.
      Audit`, `ErrorReportAdminController`, `AdminGuestsController` — one
      aggregate `AccessAudit` row per view, `Via: Admin`, D9).
