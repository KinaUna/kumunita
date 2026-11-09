---
id: code-hygiene-design
title: "CODE-HYGIENE design — warning triage + ratchet re-baseline"
tier: living
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/archive/lanes/code-hygiene/plan-code-hygiene.md
  - docs/archive/lanes/code-hygiene/code-hygiene-handoff-notes.md
  - docs/dev/testing-and-ci.md
  - docs/adr/0298-data-classes-in-code.md
  - docs/adr/0299-instance-export-operator-gated.md
  - docs/DOCS-CONVENTIONS.md
lane: code-hygiene
updated: 2026-11-09
---

> **Abstract:** the CODE-HYGIENE lane's triage — the single source of truth
> CH02–CH14 reference. One row per live warning (code × file) with its
> decision (fix / re-baseline / successor-scope) and owning unit, the
> ratchet re-baseline plan, the 3 known-reds (2 successor-scope, named
> verbatim), and the unit map. **Who is it for:** the agent picking up any
> `CH`-unit and the close reviewer. **When to read the rest:** before
> starting a warning-fix or re-baseline unit — the "owning unit" and site
> counts here are what the unit's exit criteria must reconcile against.
>
> **Count basis (authoritative):** **100 unique warning sites** at
> `development` @ `f8423743`. See §"Count basis" — the register's "165" and
> the raw log's "216" are both *not* the count of distinct problems.

# CODE-HYGIENE design — warning triage + ratchet re-baseline

## Context

The `improve-sec` lane (IS00–IS45) shipped on 2026-11-09 and left the
codebase in a **known-red, drift-heavy** state: the ratchet gate is red,
build warnings are visible, one route is shadowed, and 3 tests are red (1
auto-clears, 2 are successor-scope). None of these are regressions — they are
the *expected* drift after a lane that added cross-context reads, new eraser
methods, and new retention lambdas. The CODE-HYGIENE lane **clears that
drift** so the next lane starts green. It adds **no feature**, re-opens **no**
`improve-sec` decision (D1–D10), and fixes **none** of the 2 successor-scope
red tests. This doc is the triage that makes every one of those decisions
concrete and machine-checkable.

Measured 2026-11-09 on `development` @ **`f8423743`** (the exact commit the
register measured; `git status` shows only uncommitted lane docs, so `src/` is
byte-identical to that commit) via a full `dotnet build Kumunita.slnx
--no-incremental`, `pwsh -NoProfile -File tools/code/code-check.ps1`, and the
`improve-sec` IS43 close record.

## Scope

**In (this lane):**

- The **1 route-conflict fix** (ASP0023 — `Dissolve` shadows
  `ApproveEventRsvp`).
- The **4 method splits** (the 4 methods over the 80-line / 15-cc ceiling).
- The **100 warning fixes** (94 nullability CS8xxx + 6 mechanical + the 2
  ASP0023 sites).
- The **ratchet re-baseline** (51 → 0) + **3 Marten allowlist entries**.
- **1 auto-clearing gate** (`CodeCheck_Gate_Passes`) — clears by the
  re-baseline, not by editing the gate.

**Out (successor-scope — stay red, do NOT fix here):** the 2 M2 agreement
reds, named verbatim in §"Known-reds."

## Count basis (authoritative) — read this before the tables

Three numbers float around this lane; **only one is the count of distinct
problems:**

| Basis | Value | What it is |
|---|---|---|
| **Unique warning sites** | **100** | **The count to use.** Distinct `file:line:code` problems, deduplicated across the whole solution build. |
| Raw build-log lines | 216 | The `--no-incremental` solution build compiles some projects more than once; 92 sites print ×2 and 8 sites print ×4 → 92·2 + 8·4 = 216. A **build-orchestration artifact**, not 216 problems. |
| Register's "165" | 165 | Reproduced by **neither** command at this commit. The register's own entry-read command (`grep -oE "warning [A-Z0-9]+:" \| sort \| uniq -c`) sums to **216**, not 165. Its per-file numbers (GroupsController "22", TodosController "10", PostService "8") are the **raw/doubled** .cs counts (11·2, 5·2, 4·2). Treat the 165 as a pre-lane estimate that doesn't reproduce; **the live 100 is authoritative.** |

**Decision (CH01, 2026-11-09):** every per-unit site count below, and the
Close protocol's "0 warnings" bar, are keyed to the **unique-site** basis.
When a unit runs its exit `grep`, compare against the **unique** set for its
files (a file's unique-site count is its raw count ÷ its print multiplier —
the unit's own `grep` of a single file returns the raw lines, so a "0 lines"
result is what to aim for, and that is unaffected by the doubling).

Per-code histogram (unique sites): CS8602 52 · CS8767 10 · CS8622 8 · CS4014 4
· CS8601 4 · CS8625 3 · CS8620 3 · CS8604 3 · CS9113 2 · CS8619 2 · CS8321 2 ·
CS0108 2 · ASP0023 2 · CS8600 1 · CS0219 1 · CS0105 1. **Σ = 100.**

## Triage table

One row per **warning code × file**, with the decision and owning unit.
Counts are **unique sites** (per §"Count basis"). Every row maps to a real,
named unit — none point at a unit that doesn't exist.

### U2 — route conflict (ASP0023) — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `Controllers/GuardianController.cs` | ASP0023 | 1 | `Dissolve` route `{childId}/eventrsvp/{eventId}/approve` collides with `ApproveEventRsvp`; `Dissolve` takes only `childId`, so the `{eventId}` segment is wrong → route to `{childId}/dissolve`. |
| `Controllers/GuardianController.RsvpCurate.cs` | ASP0023 | 1 | The other half of the same route pair (`ApproveEventRsvp`, the correct route). Cleared when `Dissolve`'s route is fixed. |

**U2 total: 2 sites** (one collision; both endpoints report it).

### U3 — test mechanical (CS4014/CS0105/CS0219/CS0108) — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` | CS4014 | 4 | 4 unawaited calls → add `await` (+ `async` on the method). |
| `tests/Kumunita.Web.Tests/ProjectsBaseControllerTests.cs` | CS0108 | 2 | 2 hiding-inherited members → add `new` or rename. |
| `tests/Kumunita.Web.Tests/HealthControllerTests.cs` | CS0105 | 1 | 1 duplicate `using`. |
| `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` | CS0219 | 1 | 1 unused local → remove or `_`-prefix. |

**U3 total: 8 sites** (matches the register's "8 mechanical in 4 test files").

### U4 — src dead code / unread params (CS8321/CS9113) — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `Views/Boards/BoardDetail.cshtml` | CS8321 | 2 | 2 unused local functions (`StatusGlyph`/`StatusLabelKey`) → remove, or record as a false positive if referenced. |
| `Core/UserInfo/ProfileSettingsService.cs` | CS9113 | 1 | 1 unread `services` param → `_`-prefix. |
| `Controllers/ReportTargetsController.cs` | CS9113 | 1 | 1 unread `posts` param → `_`-prefix. |

**U4 total: 4 sites** (matches the register's "4 in 3 files").

### U5 — stub `IAuthenticationService` nullability — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` | CS8767 | 5 | `StubAuthService` non-nullable `scheme`/`properties` vs the nullable interface → add `?`. |
| `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` | CS8620 | 2 | Same stub, same `?` fix. |
| `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` | CS8767 | 5 | Same stub, same fix. |
| `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` | CS8620 | 1 | Same stub, same `?` fix. |
| `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` | CS8619 | 1 | Same stub, same `?` fix. |

**U5 total: 14 sites.** The register's "52 (CS8767/CS8622)" does not reproduce
(live: 10 CS8767 + 3 CS8620 + 1 CS8619 across the 2 stub files, unique). The
*files* match the register; the *count* is corrected to 14. The stub files'
CS4014 (4 sites) stays with **U3** (mechanical), so U3 and U5 don't double-claim
the same sites — the split is by **code**, not by file.

### U10 — nullability batch 1 (Groups/Event/Boards named files) — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `Controllers/GroupsController.cs` | CS8602 | 11 | Mechanical null dereference annotations. |
| `Views/Groups/Detail.cshtml` | CS8604 | 1 | Null arg passed to non-null param. |
| `Views/Event/Calendar.cshtml` | CS8602 | 1 | Null dereference. |

**U10 total: 13 sites** (the register's "24" is the raw/doubled count of the
same 3 files: 11·2 + 1·1 + 1·1 ≈ 24).

### U11 — nullability batch 2 (Posts/Portability/Todos named files) — decision: **fix**

| File | Code | Sites | Rationale |
|---|---|---|---|
| `Web/Controllers/TodosController.cs` | CS8602 | 5 | Mechanical null annotations. |
| `Core/Posts/PostService.cs` | CS8601 | 4 | Possible null reference assignment. |
| `Core/Portability/UserPortabilityService.cs` | CS8602 | 3 | Null dereference (do **not** re-touch CH09's split). |
| `Core/Portability/UserPortabilityService.cs` | CS8604 | 1 | Null arg to non-null param. |

**U11 total: 13 sites** (the register's "26" is the raw/doubled count of the
same 3 files).

### U12 — nullability batch 3 (**remaining / catch-all**) — decision: **fix**

The register defines U12 as the "remaining" unit with a **0-warnings** exit.
The live build has nullability sites in **30 files** that U10/U11 do not name;
all of them are U12's to clear (this is the register's own "U12 = remaining"
intent, with the file list corrected to the live build).

**Scope note for the CH12 agent:** the CH12 prompt's "≤ 4 files" hint is
**superseded** by (a) the register's U12 exit — "`dotnet build` should report
**0 warnings** (or ≤5, all documented as false positives)" — and (b) this file
list. CH12's own prompt already says "Run the histogram one final time to
confirm which warnings remain, and fix the ones in the named files" and ends
with a 0-warnings bar; use this list as the "named files." Do **not** treat the
"≤ 4 files" hint as a cap.

| File | Code | Sites | | File | Code | Sites |
|---|---|---|---|---|---|---|
| `Web/Controllers/ProjectsController.cs` | CS8602 | 3 | | `Core/Portability/PortabilityValidate.cs` | CS8602 | 2 |
| `Web/Controllers/EventController.cs` | CS8602 | 3 | | `Core/Projects/BoardLaneService.cs` | CS8602 | 2 |
| `Web/Controllers/BoardsController.Translations.cs` | CS8602 | 3 | | `Web/Controllers/BoardsController.cs` | CS8602 | 1 |
| `Core/Announcements/AnnouncementService.cs` | CS8622 | 2 | | `Web/Security/UploadGateHandler.cs` | CS8604 | 1 |
| `Core/Pages/PageService.cs` | CS8622 | 2 | | `Web/Views/Admin/Manage.cshtml` | CS8602 | 1 |
| `Web/Controllers/NotificationsController.cs` | CS8602 | 2 | | `Web/Views/Announcement/New.cshtml` | CS8602 | 1 |
| `Core/Events/EventService.cs` | CS8622 | 2 | | `Web/Views/Announcement/Edit.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Document/New.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Document/Edit.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Boards/BoardNew.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Boards/BoardEdit.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Event/Create.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Event/Edit.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Shared/_BoardCardEditModal.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Shared/_AccountNav.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Inventory/Detail.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Todos/Create.cshtml` | CS8602 | 1 |
| | | | | `Web/Views/Todos/Edit.cshtml` | CS8602 | 1 |
| `Core.Tests/BoardLaneServiceSeamTests.cs` | CS8602 | 2 | | `Web.Tests/KioskControllerTests.cs` | CS8619 | 1 |
| `Core.Tests/TranslationDescriptorTests.cs` | CS8622 | 2 | | `Web.Tests/UploadSignatureSnifferTests.cs` | CS8625 | 1 |
| `Web.Tests/CommunityControllerTranslationTests.cs` | CS8625 | 2 | | `Core.Tests/C03_NotificationEmitterContractTests.cs` | CS8600 | 1 |

**U12 total: 46 sites** (the remaining 100 − 2 − 8 − 4 − 14 − 13 − 13).

> The register's U12 named "remaining test files" `DocumentServiceTests`,
> `DocumentControllerTests`, `KioskItemCatalogTests`, `BreakGlassProbeTests`
> are all **0-warning** in the live build — the live test-file nullability is
> in the 6 files listed above instead. This is a file-list correction, not a
> scope change: U12 still owns "the remaining."

### Warning grand total

**2 (U2) + 8 (U3) + 4 (U4) + 14 (U5) + 13 (U10) + 13 (U11) + 46 (U12) = 100
unique sites.** When all seven units are done, `dotnet build` reports 0
warnings.

## Ratchet re-baseline plan — decision: **re-baseline (U13)**

All 51 violations are the *legitimate consequence* of the `improve-sec`
decisions (account deletion, retention, portability, logging cross contexts by
design — ADR [0298](../adr/0298-data-classes-in-code.md) /
[0299](../adr/0299-instance-export-operator-gated.md)). Re-baselining with an
ADR annotation is the sanctioned path (the
[`testing-and-ci.md`](../dev/testing-and-ci.md) "re-baseline + annotate" rule).
Do **not** refactor the boundaries away, do **not** raise a ceiling, do **not**
delete the gate.

**Verified 51 violations (live, `code-check.ps1`, all match the register):**

- **1 file grew:** `Bootstrap/SampleDataSeeder.cs` (2060 → 2061) — re-baseline to 2061.
- **2 stale method baselines (method gone):** `ImportOwnershipGuard.Check` (old 5-arg) and `UserPortabilityService.ScopeRowHoldsSeat` (old 5-arg) — remove after the CH08/CH09 splits land.
- **4 new methods over ceiling** — *cleared by the splits, NOT re-baselined:* `EraseAsync` (142 L / cc 21 → CH06), `RunAsync` (118 L / cc 38 → CH07), `Check` (cc 32 → CH08), `ScopeRowHoldsSeat` (cc 33 → CH09). After the split each is ≤80 L / cc ≤15, so `--tighten` baselines them at their new (lower) numbers.
- **3 boundary edges grew:** `Portability→UserInfo:Profile` (2→4 files), `Web→Authorization:AccessAudit` (2→4), `Web→Logging:FileSinkOptions` (1→2) — re-baseline.
- **~38 new boundary violations:** `Bootstrap→Logging`, `Identity→{Logging,ErrorReports,Events,Notifications,Sessions,Usage,UserInfo,GroupMessaging}`, `Portability→{Authorization,Privacy,Bookmarks,GroupMessaging,PersonalNotes}`, `Privacy→{Messaging,ErrorReports,Identity,Logging,Notifications,Sessions}`, `Web→{Authorization,BreakGlassProbe,Privacy}` — add.
- **3 controllers on Marten without an allowlist entry:** `AdminGuestsController`, `AdminPortabilityController`, `ErrorReportAdminController` — add to the Marten allowlist.

**U13 execution order (matters — the splits come first):** CH06–CH09 (splits) →
CH13 (`CodeMetrics --tighten` then verify: the 2 stale entries gone, the 4 split
methods baselined at their new numbers, the 3 grown edges re-baselined, the ~38
new boundaries present) → add the 3 allowlist entries → annotate ADR
0298/0299 (one line each: "boundary edges + method baselines re-baselined to the
current count on 2026-11-09 by CODE-HYGIENE U13") → `code-check.ps1` exits 0.
The `CodeCheck_Gate_Passes` red **auto-clears** at this step.

## Known-reds (successor-scope)

Three tests are red at this HEAD (verified against the `improve-sec` IS43 close
record, which names them verbatim). **Two are successor-scope and stay red —
this lane does NOT fix them.** One auto-clears by U13.

**Successor-scope — OUT OF SCOPE (stay red), named verbatim:**

- `SecGate.DataClassCoverageTests.EveryResidentExportType_IsInUserScopeInventory`
  — `improve-sec` owners **IS31/IS32** (the E6 export-scope gap; 10 `Export.Resident`
  types absent from `UserScopeInventory.Entries`).
- `SecGate.DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`
  — `improve-sec` owners **IS21–IS23** (13 `Delete`/`Pseudonymize` types in no
  `HandledTypes` set — the eraser-handling gap).

**Auto-clears (NOT successor-scope):**

- `ImproveHarnessTests.CodeCheck_Gate_Passes` — the ratchet-gate harness test.
  Clears when U13 makes `code-check.ps1` exit 0. **Not** a successor-scope red.

## Unit map

One line per unit; the "clears" column is the **unique-site** count this doc
assigns (per §"Count basis").

| Unit | Name | Files / target | Clears |
|---|---|---|---|
| **CH01** | Design doc / triage (this file) | `docs/design/code-hygiene-design.md` (docs only, no build) | — (authors the triage) |
| **CH02** | Route conflict fix (ASP0023) | `GuardianController.cs` + posting view + 1 route test | 2 sites (1 collision) |
| **CH03** | Test mechanical warnings | 4 test files | 8 sites |
| **CH04** | Src dead code / unread params | 3 files | 4 sites |
| **CH05** | Stub `IAuthenticationService` nullability | 2 stub test files | 14 sites |
| **CH06** | Split `ResidentAccountRowsEraser.EraseAsync` | 1 src file (≤2 with tests) | 2 ratchet ceiling violations |
| **CH07** | Split `RetentionService.RunAsync` | 1 src file (≤2 with tests) | 2 ratchet ceiling violations |
| **CH08** | Split `ImportOwnershipGuard.Check` | 1 src file (≤2 with tests) | 1 ratchet cc violation + 1 stale baseline |
| **CH09** | Split `UserPortabilityService.ScopeRowHoldsSeat` | 1 src file (≤2 with tests) | 1 ratchet cc violation + 1 stale baseline |
| **CH10** | Nullability batch 1 (named files) | `GroupsController.cs`, `Groups/Detail.cshtml`, `Event/Calendar.cshtml` | 13 sites |
| **CH11** | Nullability batch 2 (named files) | `PostService.cs`, `UserPortabilityService.cs`, `TodosController.cs` | 13 sites |
| **CH12** | Nullability batch 3 (**remaining/catch-all**) | the 30 files in the U12 table | 46 sites |
| **CH13** | Ratchet re-baseline + allowlist | `code-baseline.json`, allowlist, ADR 0298/0299 | 51 → 0 violations; `CodeCheck_Gate_Passes` green |
| **CH14** | Close: gate verify + handoff + archive | full suite run, handoff note, archive move | confirms 0 warnings / 0 violations / only the 2 successor reds fail |

**Ordering note:** CH06–CH09 (splits) must land **before** CH13 (re-baseline)
so the tightened baseline reflects the split methods; CH10–CH12 (warnings) are
independent and can land in any order; CH14 is last.

## Drift log (CH01, 2026-11-09)

- **PAUSED then RESOLVED.** The first CH01 pass recorded a Drift pause: the
  register's "165 warnings" doesn't reproduce from its own entry-read command
  (which yields 216 raw / 100 unique), and the register's U5/U10/U11/U12
  per-file counts disagree with the live build.
- **Resolution (user-authorized, this pass):** adopt the **100 unique-site**
  basis as authoritative; map every live warning to its owning unit (U2/U3/U4/U5
  by the register's own per-unit definition; U10/U11 to their named files; U12
  as the register's "remaining" catch-all with the corrected 30-file list).
  Confirmed-unchanged against the register: the 51 ratchet violations, the 1
  route conflict, the 4 over-ceiling methods, and the 3 known-reds (incl. the
  2 successor-scope names). Corrected against the register: the warning total
  (165 → 100 unique) and the U5/U10/U11/U12 per-file counts.
- **No unit boundary was invented or re-scoped** — only the register's own
  "U12 = remaining / 0 warnings" intent was applied with a corrected file list,
  and the U12 "≤ 4 files" hint was flagged as superseded by its 0-warnings exit.
