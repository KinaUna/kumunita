# IP · U07 — Close: handoff `## Summary` + all IP artifacts → `done/`

> **You are the U07 agent.** Read **this file + your entry reads** and you
> can execute. IP is **not a milestone** (no `Milestones.cs` entry, no
> `MilestonesTests` re-pin, no `STATUS.md` update, no `README.md` roadmap
> update, no ADR) — so this close is **lighter** than M13's U07. It has
> two deliverables: append the `## Summary` section to the handoff notes,
> and move all IP artifacts flat to `done/`.

## Goal

Close the IP lane:

1. **Append the `## Summary`** section to the handoff notes
   (`ip-integration-polish-handoff-notes.md`) — a table of U00–U06 with
   their one-liner goal + test count + deviations + the deferred-lane list
   (the god-service split, the `BookmarkService` seam abstraction, the
   `KnownTranslationKeys` split, the `INotificationService` interface —
   each deliberately *not* in IP, each needing its own ADR).
2. **Move all IP artifacts** flat to `docs/plans-milestones/done/` (the
   M13–M21 convention — **not** a `done/ip/` subfolder).

## Entry reads (4)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (the unit map, the FACES, the deferred-lane list).
2. `docs/plans-milestones/in-progress/ip-integration-polish-handoff-notes.md`
   — the rolling handoff notes (U00 created it; each unit appended a
   `## U##` section). **If this file does not exist** (U00 was skipped or
   the notes were lost), create it with a `# IP — Integration Polish —
   Handoff Notes` header and a `## U07` section noting the gap.
3. `docs/plans-milestones/done/m13/m13-u07.md` — the **M13 close unit** as the
   format reference (the `## Summary` table shape, the move instructions).
4. `docs/plans-milestones/done/` — the **done/ folder** listing (confirm
   the flat convention: `m13-u00.md`, `plan-m13-logging-analytics.md`,
   `m13-logging-analytics-handoff-notes.md` are all at the `done/` root).

## Deliverables (1 edit + 10 moves)

### 1. Append `## Summary` to the handoff notes

In `docs/plans-milestones/in-progress/ip-integration-polish-handoff-notes.md`
(or the `done/` location if U06 already moved it — check both), **append**
this section at the end:

```markdown
## Summary (U07 — close)

| Unit | Goal (one-liner) | Test assembly | Deviations |
| --- | --- | --- | --- |
| U00 | Docs parity: ADR range → 0001–0123, value-chain table → M18–M23, handoff notes created. | (no build/test — docs only) | — |
| U01 | `AccessAuditFactory.SingleTarget` + delete the three `StoreAuditRow` copies. | Core.Tests | — |
| U02 | `StandingMatrix.AuditVia` + delete the four `*AuditViaFor` copies. | Core.Tests | — |
| U03 | Two rate-limit policies (`message` 20/15min, `write` 30/15min) + three `[EnableRateLimiting]` placements. | Web.Tests | — |
| U04 | `ILogger<T>` injection + `catch (Exception ex) when (ex is not UnauthorizedAccessException)` in `HomeController` + `MessagesController`. | Web.Tests | — |
| U05 | `BlockedAccountMiddlewareTests` + `PrivilegedStampMiddlewareTests`. | Web.Tests | — |
| U06 | Login view `?error=` code → 3-row `kw-l` table + 3 keys × 4 languages. | Core.Tests + Web.Tests (named exception) | — |

**Deferred lanes (not in IP, each needs its own ADR):**
- God-service split (`ProjectService` at 4,128 lines).
- `BookmarkService` seam abstraction.
- `KnownTranslationKeys` split (the 6,000+ line single file).
- `INotificationService` interface (forbidden by ADRs 0076/0077/0083/0084/
  0117/0118).

**IP is closed.** All artifacts moved to `done/`. The handoff notes are
the lane's observable record.
```

> Adjust the "Deviations" column if any unit's `## U##` section in the
> handoff notes records a deviation from the register's D#. The table is a
> **summary** — the detail is in each unit's `## U##` section above it.

### 2. Move all IP artifacts flat to `done/`

Move these **10 files** from their current locations to
`docs/plans-milestones/done/`:

| # | From | To |
| --- | --- | --- |
| 1 | `docs/plans-milestones/plan-ip-integration-polish.md` | `docs/plans-milestones/done/ip/plan-ip-integration-polish.md` |
| 2 | `docs/plans-milestones/in-progress/ip-u00.md` | `docs/plans-milestones/done/ip/ip-u00.md` |
| 3 | `docs/plans-milestones/in-progress/ip-u01.md` | `docs/plans-milestones/done/ip/ip-u01.md` |
| 4 | `docs/plans-milestones/in-progress/ip-u02.md` | `docs/plans-milestones/done/ip/ip-u02.md` |
| 5 | `docs/plans-milestones/in-progress/ip-u03.md` | `docs/plans-milestones/done/ip/ip-u03.md` |
| 6 | `docs/plans-milestones/in-progress/ip-u04.md` | `docs/plans-milestones/done/ip/ip-u04.md` |
| 7 | `docs/plans-milestones/in-progress/ip-u05.md` | `docs/plans-milestones/done/ip/ip-u05.md` |
| 8 | `docs/plans-milestones/in-progress/ip-u06.md` | `docs/plans-milestones/done/ip/ip-u06.md` |
| 9 | `docs/plans-milestones/in-progress/ip-u07.md` | `docs/plans-milestones/done/ip/ip-u07.md` |
| 10 | `docs/plans-milestones/in-progress/ip-integration-polish-handoff-notes.md` | `docs/plans-milestones/done/ip/ip-integration-polish-handoff-notes.md` |

> **If the handoff notes file does not exist** (U00 was skipped), move the
> 9 files that do exist and create a stub handoff notes file in `done/`
> with the `## Summary` section from Deliverable 1.
>
> **If any unit plan file does not exist** (the unit was not executed),
> skip that move and note the gap in the `## Summary` table's Deviations
> column.

## Exit

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Both must be green (U07 is a **no-code-change** unit — the build + test are
the exit gate to confirm no earlier unit left the tree in a broken state).
Additionally:

- `Get-ChildItem docs\plans-milestones\done\ip-*.md` returns **8** files
  (`ip-u00.md` through `ip-u07.md`).
- `Get-ChildItem docs\plans-milestones\done\plan-ip-integration-polish.md`
  returns **1** file.
- `Get-ChildItem docs\plans-milestones\done\ip-integration-polish-handoff-notes.md`
  returns **1** file.
- `Get-ChildItem docs\plans-milestones\in-progress\ip-*.md` returns
  **0** files (all moved).
- `Get-ChildItem docs\plans-milestones\plan-ip-integration-polish.md`
  returns **0** files (moved to `done/`).
- The handoff notes file has a `## Summary` section at the end.
