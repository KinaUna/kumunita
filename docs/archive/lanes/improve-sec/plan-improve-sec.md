---
id: plan-improve-sec
title: "Plan: IMPROVE-SEC — security & privacy integrated, not bolted on"
tier: living
kind: plan
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/SECURITY.md
  - docs/security/control-map.md
  - docs/security/data-classes.md
  - docs/security/trust-boundaries.md
  - docs/security/decisions-open-items.md
  - docs/archive/lanes/sec/plan-sec-remediation.md
  - docs/archive/lanes/prod-hardening/hardening-handoff-notes.md
  - docs/archive/lanes/improve/improve-audit.md
  - docs/philosophy/in-code.md
lane: improve-sec
updated: 2026-11-06
---

> **Abstract:** the IMPROVE-SEC lane asks whether security and privacy are
> *integrated* (every control has a seam in the code and a test that pins it,
> and every data class has a policy the code enforces) or *bolted on* (the
> control lives in a doc, in a runbook, or in one reviewer's memory). The
> earlier lanes closed the known holes: SEC remediation fixed H1–L8,
> prod-hardening fixed F1–F22, and IMPROVE U08 found the `Decide()` matrix
> clean. This lane goes after the **linkage** instead. It found four
> severity-S gaps. (1) The GlobalAdmin instance export carries every private
> message. (2) Account deletion misses doc types that shipped after F1.
> (3) Email addresses in logs reach Sentry as breadcrumbs. (4) 52 reads of
> audience-bearing data run with no decision in the same method, one of
> which leaks. It also found a medium tier: no retention, an incomplete GDPR
> export, unaudited admin reads, inline handlers that CSP blocks, and a
> control map with no traceability. Each gap gets a fix **and a gate**, so
> the next doc type, log line or read cannot drift the same way. The units
> are sealed, sized for a 32–64K-token agent.
> **Who is it for:** whoever picks up an `IS`-unit, and the reviewer of the
> close. **When to read the rest:** before starting any `IS`-unit; units
> name their own entry reads.

# Plan: IMPROVE-SEC — security & privacy integrated, not bolted on

Measured on `main` @ **`1970cfc4`** (2026-11-06) with
[`tools/sec/sec-measure.py`](../../../tools/sec/sec-measure.py), a static
reader with no build and no network. The script is the lane's starting gate:
`IS04` ports its load-bearing checks into xUnit, and `IS43` deletes it.

## Why now

The control map promises that **"every threat must be covered"**, and the
data-classes doc says to add a row *before merge*. Both promises are kept by
people reading docs, not by the build:

- **Group messaging** shipped on 2026-10-30, five days after F1's complete
  GDPR deletion. Nobody updated the eraser, so a deleted resident's group
  messages survive under their `SenderId`.
- The same drift left `SessionDoc` (IP + user agent, one per login)
  and `ErrorReport` (contact email) without an erasure or retention
  policy.
- The F10 Sentry scrubber calls breadcrumb redaction "vacuous", but the
  logging integration ships Information-level logs (with `{Email}`) as
  breadcrumbs.

None of these is a lapse in the code that was written. Each is a seam with
no gate. In the philosophy's words: *the parts are cheap, the linkage is
rare.*

## What this lane is (and is not)

- **Is:** making each security/privacy promise *enforced and pinned*. Every
  fix comes with a gate test that fails when the next change drifts. The
  gates follow the IMPROVE-CODE precedent: start from the measured baseline
  and **retire** it to zero; never freeze it.
- **Is not:** a re-audit of what earlier lanes verified. It does not redo
  the `AccessVia` deny matrix (U08); it **turns U08's table into a gate**
  (`IS04`). It does not re-fix H1–L8 or F1–F22, and it does not reopen
  owner decisions (U21, U23/U24, F6, F22 Option A). It also adds no new
  feature surface beyond what the fixes require.
- **Scope correction to the lane prompt:** there is **no tenant-by-slug
  model** in this repo. [`ARCHITECTURE.md`](../../ARCHITECTURE.md) says
  one instance per neighborhood, with no multi-tenant data model.
  "Tenant isolation" therefore maps to *community/component scoping*,
  which U08 verified (`AccessVia.Community`). The deny-matrix gate pins it.

## Decisions

| # | Decision | Chosen (owner, 2026-11-06) | Alternatives rejected |
|---|---|---|---|
| D1 | **S1** — the GlobalAdmin instance export carries all private content | **Operator-gated.** A GlobalAdmin alone gets a **content-free** export: structure, public content, and their own data. A **full** export requires an operator-granted break-glass `AdminOverride` (the existing ADR 0003 path; no in-app endpoint can grant it), is audited `Via: BreakGlass`, and leaves a transparency entry that residents can see. New ADR **0299**. | Strip private content entirely (loses migration; pushes the operator to raw `pg_dump`); accept and disclose (keeps a GlobalAdmin content read that contradicts the messaging D3 wall) |
| D2 | **S2 root cause** — where each doc type's data-class policy lives | **`[DataClass(...)]` attribute on each document type.** It declares the class (a–e plus operational/platform), erasure (`Delete` / `Pseudonymize` / `Keep(reason)`), export (`Resident` / `InstanceOnly` / `Never`) and a retention key. A reflection gate fails when a registered type has no attribute, or when the eraser or export inventory disagrees with it. New ADR **0298**. | Central registry table (further from the type); a docs table plus script (weaker) |
| D3 | **S2 shared threads** — a deleted resident's messages | **Pseudonymize 1:1 and group alike.** The body is erased and the sender becomes the tombstone (`deleted:{id}`, the `AccessAudit` precedent). The rows stay so the other participants' threads remain coherent. 1:1 changes from *delete conversation* (which also destroyed the other party's copy) to this rule. | Delete both; delete 1:1 and tombstone groups |
| D4 | **S3** — PII in logs, Sentry breadcrumbs | **Fix both source and sink.** A `LogRedact` helper and the subject id replace emails at all 7 sites, and a gate fails on PII placeholders. Sentry `MinimumBreadcrumbLevel = Warning`, proven by a test before the change. Sentry becomes trust boundary **B7**. | Source only; disable Sentry's logging integration (file logs keep emails) |
| D5 | **S4** — decision-free reads of audience-bearing types | **Marker plus ratchet.** Triage all 52. Leaks are fixed through `CanSeeAsync`; legitimate reads get `// SEC-READ: <reason> (gated by <caller>)`. A gate fails on any unmarked read, taking the count from 52 to **0** for good. | Push `actorId` into every service seam (changes ADR 0006 frozen signatures); fix leaks only (no gate) |
| D6 | Retention defaults | **Moderate.** Sessions: 30 days after revoke or expiry. Outbox: body nulled 7 days after delivery, row kept 90 days. Dead letters: 30 days. Notifications: read 90 days, unread 365. Resolved error reports: 180 days. Consumed or expired tokens: 7 days. Each is a config key; one `RetentionTick` runs them all. File-log `Retain` runs daily, not only at boot. | Strict; configurable-but-off |
| D7 | Gate form | **xUnit `SecGate` tests in the existing CI run**, enforced from the unit that lands each check, plus a thin `tools/sec/sec-report.ps1` for the numbers. The Python script is deleted at the close. | A PowerShell gate in report-only mode; keep Python |
| D8 | Unit shape | **Sealed units for a 32–64K window**: ≤ 5 entry reads (ranges for the large files), ≤ 4 deliverable files, about 600 LOC or less, own tests only, and a handoff note *before* finishing. The SEC remediation protocol and its rules 1–9 apply verbatim ([plan-sec-remediation §Workflow](../../archive/lanes/sec/plan-sec-remediation.md)). | One unit per finding (too large for the window) |
| D9 | Admin reads of class (c)/PII (audit log, error reports, guests) | `// TODO ❓` **at `IS36`** — default: **one aggregate `AccessAudit` row per view** (`Via: Admin`, `TargetKind` `audit-log` / `error-reports` / `guests`, `VisibleCount`). | per-row audit (noisy); accept unaudited |
| D10 | Where residents see a full-export transparency entry | `// TODO ❓` **at `IS29`** — default: **a notification to every resident** (the existing inbox; no new surface), plus the audit row. | a new `/transparency` page; an `About` footer line |

## Evidence / starting numbers (measured 2026-11-06, `main` `1970cfc4`)

Severity follows `docs/SECURITY.md`: a privacy leak or a non-auditable access path is **S**.

| # | Sev | Finding | Where | Principle / test / anti-pattern | Units |
|---|---|---|---|---|---|
| E1 | **S** | The instance export includes all 44 M11 types: every `Conversation`/`Message`, audience-restricted posts, profile contact fields and reports. It writes one audit row, with no second party and nothing residents can see. This contradicts the messaging D3 wall ("a GlobalAdmin never gets a read of any conversation") and ADR 0003's privacy default. | `AdminPortabilityController` `GET admin/portability/export` → `PortabilityService.ExportAsync`; `PortabilityDocTypes` rows 41–42 | part-vs-whole (an operator surface undoes a resident guarantee); anti-pattern *the side door* | IS25–IS30 |
| E2 | **S** | Account deletion is incomplete. Of 65 principal-bearing doc types, **25 are not erased** and most have no recorded decision. Private content and PII survive: `GroupMessage`, `GroupConversation`, `GroupMessagingInvitation` (shipped after F1), `SessionDoc` (IP + user agent), `ErrorReport` (contact email), `OutboxEmail` (recipient + body), notifications, `DelegationGrant`, guardian rows, and `UsageEvent` (not pseudonymized). F1's §2C "decision" rows were never decided. | `ResidentContentEraser.EraseAsync`; `IdentityService.AccountOps.DeleteCoreAsync`; `docs/design/prod-hardening-f1-gdpr-deletion.md` §2C | closed-loop (the erasure promise does not close); anti-pattern *the orphaned part*: a new part without a link | IS16–IS24 |
| E3 | **S** | PII reaches a third party through log breadcrumbs. Seven log templates carry `{Email}`/`{Recipient}`, and Sentry's ILogger integration sends Information and above as breadcrumbs that the scrubber cannot redact. The F10 comment calls this "vacuous". Sentry also has no trust-boundary row. | `IdentityService.cs:159,218,270`, `SmtpSender.cs:295`, `FirstBootSeeder.cs:230,295`, `SampleDataSeeder.cs:2579`; `Program.cs` `UseSentry` block + `SentryPiiScrubber` | handoff test (data leaves via an unlisted boundary) | IS05–IS09 |
| E4 | **S** | 52 methods read an audience-bearing type (`Post`, `KanbanBoard`, `Event`, `Page`, `Document`, …) with no decision in the same method. **One is a confirmed leak:** `TodoDetail` shows the title of a restricted board that the to-do sits on, with no decision and no audit. The rest are caller-gated or owner-only, but unmarked. | `TodosController.TodoDetail` (≈ line 260–285); list in the baseline output (M7) | per-request authorization contract implicit; anti-pattern *the implicit contract* | IS10–IS15 |
| E5 | M | No retention for `SessionDoc`, `OutboxEmail` (full bodies, including verify links and 200-character UGC/DM snippets), `Notification`, `ErrorReport`, `EmailDeadLetter`, or consumed/expired `IdentityToken`/`EscalationToken`. File-log retention runs only at boot. | `RollingFileSink.Retain` (boot only); no purge for these types | Stable face (unbounded accumulation) | IS33–IS35 |
| E6 | M | The resident export (GDPR Art. 15/20) is incomplete: it misses `PersonalNote`, `NoteFolder`, `BookmarkFolder`, `PageTranslation`, `GroupConversation` and `GroupMessage`. | `UserScopeInventory.Entries` | closed-loop | IS31–IS32 |
| E7 | M | GlobalAdmin reads of the **audit log itself** (class c, "a disclosure surface"), error reports and guest lists write no audit row. | `AdminController.Audit` (≈ :500), `ErrorReportAdminController`, `AdminGuestsController` | the U08 (b) rule applied to admin reads | IS36 |
| E8 | M | Control map: of **24 rows, 10 link to code and 15 have no test naming their seam.** The "security-stamp rotation" row points at `SecurityStampValidatorOptions`, whose `ValidationInterval` is set to 14 days so it never fires; the real seams are `RevocationStampMiddleware` and `PrivilegedStampMiddleware`. CSP is pinned only by a source regex. | `docs/security/control-map.md`; `Program.cs:698` | Coherent face; *documentation as the control* | IS39–IS40 |
| E9 | M | **8 inline `on*` handlers in 6 views**, which `script-src 'self'` silently blocks. The 2 further raw hits are HTML comments in `_LayoutScripts`. The destructive "Delete note/folder?", "Delete bookmark folder?" and "Reset help pages?" confirms never run, even though `client/lib/confirm.ts` (`form[data-confirm]`) already exists and is loaded globally. No gate enforces the CSP code discipline. | `Notes/Index` (2), `AdminHelp/Index` (2), `Bookmarks/Index`, `AdminSurfaceLabels/Index`, `AdminAnalytics/Index`, `Admin/Escalation/Tokens` | seam with no test | IS37–IS38 |
| E10 | M | Data classes exist only in a doc, with 5 classes and nothing in code. New PII types (`SessionDoc`, `OutboxEmail`, group messages, `ErrorReport.ContactEmail`, `UsageEvent`) are not in it. | `docs/security/data-classes.md` | Coherent | IS16–IS20, IS41 |
| E11 | M | Docs say one thing and code another: "tokens hashed at rest" (F2, WhatsNew 0.68) is claimed for the verify lane, whose link credential is the token row id (owner decision U23 = n/a). DM and mention snippets cross B4 (SMTP), but B4 lists only class (a). | `decisions-open-items.md`, `trust-boundaries.md`, `WhatsNew.cs` 0.68 text (historical, untouched) | Coherent | IS09, IS41 |
| E12 | L | Owner-ops open items are spread over two archived handoffs (prod-hardening F4/F10/F12–F14/F16–F18/F20–F22; SEC `KnownNetworks`, `AllowedHosts`, `Health__Token`, revocation cut-off, `node:22` pin), with no single ledger and no "last verified" column. | archived handoffs `## Summary` | handoff test (operator) | IS42 |
| E13 | L | The "named invariant tests" open item is stale: `EvaluateAudience_EmptyAudience_AllMode_Denies` and `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` exist. | `decisions-open-items.md` | — | IS04, IS41 |

**What held up (do not "fix"):** the `Decide()` matrix, which is standing-keyed and has a deny test per `AccessVia` (U08). It covers guardian content isolation (no content read), moderator scoping (C5, off by default), delegation action scope, and break-glass expiry. Also sound: thin cookie with per-request authorization plus the revocation and privileged-stamp middlewares; the H1 import guard; the GlobalAdmin 2FA middleware; forwarded-header trust; anti-forgery globally; the Sentry request scrubber (headers, cookies, query); `SendDefaultPii = false`.

**Raw numbers (the gate baselines that the lane retires):** M1 control-map rows with a pinned seam **9/24** · M2 principal-bearing types with no erasure **25/65**, neither exported nor named excluded **19** · M4 PII log placeholders **7** (the script's M4 regex under-counts; IS08's gate is authoritative) · M7 unmarked decision-free reads **52** · inline handlers **8** · unaudited admin reads of class (c)/PII **3**.

## The units

IDs are `IS00` … `IS45`. A letter suffix (`IS17a`, `IS27b`, `IS34a`, `IS38b`) is a split that keeps a unit within 4 files; any unit may split the same way, and that is not drift. Each unit is one commit. **Per-unit contract (D8):** read only the entry reads (use `grep -n` then `sed -n 'A,Bp'` on any file over 600 lines; never read `Program.cs`, `IdentityService*.cs`, `UserPortabilityService.cs`, `PostsController.cs`, `PageService.cs` or `EventService.cs` whole); edit only the deliverables; run only the named tests with the runner form recorded in [SEC U3](../../archive/lanes/sec/sec-handoff-notes.md) (xunit.v3 `-class`, not `dotnet test --filter`); append a 3–8 line `## IS<n> — <title>` section to `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` covering files, public names, tests and pass count, next-unit notes, and anything noticed. **Rules 1–9 of the SEC protocol apply.** In particular: no PII in code, tests or notes (`user-a`, `a@example.test`); pause on drift; never edit around a failing test.

### Phase 0 — Foundations

#### IS00 — This plan *(delivered as `improve-sec-00-plan.patch`)*
Plan + `tools/sec/sec-measure.py` + the `docs/plans-milestones/README.md` pointer. No product code.

#### IS01 — Design doc Part 1 (context, invariants, register)
- **Goal:** write `docs/design/improve-sec-design.md` Part 1. **No code, no build.**
- **Entry reads:** this plan (Decisions + Evidence), `docs/philosophy/templates/design-doc.md`, `docs/security/control-map.md`.
- **Deliverables (1 new file, ~180 lines):** `## Context`; `## Invariants`, using these exact ids:
  - **P-1** a doc type cannot be registered without a `[DataClass]`.
  - **P-2** account deletion leaves no row whose policy says `Delete`, and no un-tombstoned principal where it says `Pseudonymize`.
  - **P-3** no log template carries a PII placeholder; nothing below Warning reaches a third party.
  - **P-4** every read of an audience-bearing type is decided in-method or marked with its gate.
  - **P-5** no GlobalAdmin-only path yields class (b) content without an operator-granted override.
  - **P-6** every class (a)/(b)/(c) type has a retention key or a `Keep` reason.
  - **P-7** every control-map row links a seam and names a pinning test.
  - **P-8** no inline script or handler in views.

  Also `## Findings register` (E1–E13 with file:line, *re-verify in IS02*).
- **Exit:** the file renders. Handoff: invariant ids and register row count (13).

#### IS02 — Design doc Part 2a (pins for phases A, B, D, E)
- **Needs:** IS01.
- **Entry reads:** design Part 1; `grep -n "UseSentry" -A25 src/Kumunita.Web/Program.cs`; `grep -n "HasBreakGlassAsync" -A30 src/Kumunita.Core/Authorization/AuthorizationService.cs`; `sed -n 1,60p src/Kumunita.Core/Portability/PortabilityService.cs`; `ls src/Kumunita.Core/Usage/*Tick.cs src/Kumunita.Core/Media/MediaLifecycleTick.cs`.
- **Deliverables (modify the design doc):** Part 2a contains:
  - §2.1, the re-verified file:line for every register row.
  - §2.2, the fix shapes and exact new public signatures:
    - `LogRedact.EmailDomain(string?)`
    - `BreakGlassProbe.HasActiveAsync(IQuerySession, string)`
    - `PortabilityExportScope { ContentFree, Full }`
    - `IPortabilityService.ExportAsync(string actor, PortabilityExportScope scope, CancellationToken ct = default)`, as an overload: the old signature delegates `ContentFree`
    - `RetentionOptions` (keys under `Retention__*`, the D6 numbers)
    - `RetentionPolicy.CutoffFor(...)`
    - `RetentionTick`
  - §2.3, final test names (seed list in each unit below).
  - §2.4, the drift-guard anchors per unit.
- **Exit:** every unit IS05–IS42 has a test list or "no test, because …". Handoff: corrected refs and any row already fixed (that unit becomes "n/a").

#### IS03 — Design doc Part 2b (the data-class policy table)
- **Needs:** IS02.
- **Entry reads:** design Part 1; `python3 tools/sec/sec-measure.py --json` (M2 rows only, through `jq '.m2'`); `docs/design/prod-hardening-f1-gdpr-deletion.md` §2A–§2C (lines 57–135); `docs/security/data-classes.md`.
- **Deliverables (modify the design doc):** §2.5, **one row per registered doc type (81)** with type, class, erasure, export, retention key and reason. The defaults follow D2, D3 and D6, and every F1 §2C "decision" row is decided here:
  - Notifications ×4, `DelegationGrant`, `GuestAccess`, `IdentityToken`, `SessionDoc` → `Delete`.
  - `Report`, `ErrorReport`, `EscalationToken`, `UsageEvent` → `Pseudonymize`.
  - A resident-owned `Group` → `Keep`, with the reason "ownership transfers per ADR 0142".

  §2.6 holds the export-scope table, i.e. which M11 types and fields are stripped in `ContentFree`. Rows that need the owner stay `// TODO ❓`.
- **Exit:** 81 rows; every principal-bearing type has a non-empty erasure value. Handoff: the count per class and the open `❓` rows.

#### IS04 — Gate harness + the `AccessVia` deny-matrix gate
- **Needs:** IS02.
- **Entry reads:** `tests/Kumunita.Web.Tests/F7F8_HardeningGateTests.cs` lines 1–40 (the repo-root pattern); `src/Kumunita.Core/Authorization/Decision.cs` (`AccessVia`); the U08 table in `docs/archive/lanes/improve/improve-audit.md` (`grep -n "AccessVia" …`, the ±30-line table).
- **Deliverables (≤ 4 files, new):**
  - `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs`: repo-root locator, `ReadSrc`, `AllSrcFiles`, and `TestMethodExists(name)` (a source scan of `tests/**`).
  - `tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs`: a static map from each `AccessVia` member to its U08 deny-test name; it asserts every enum member is mapped and every named test exists.
  - `tools/sec/sec-report.ps1`, which prints the six baseline numbers.
  - The handoff-notes file, with the `## IS04` section recording the baseline.
- **Tests:** `EveryAccessVia_HasANamedDenyTest`, `DenyMatrix_NamedTestsExist`, `EmptyAudienceDenies_AndDelegationScope_ArePinned`.
- **Exit:** build plus the new class green.

### Phase A — S3: PII in logs, Sentry breadcrumbs

#### IS05 — Sentry breadcrumb floor (prove, then fix)
- **Needs:** IS02 (§2.2 Sentry pin).
- **Entry reads:** `grep -n "UseSentry" -A25 src/Kumunita.Web/Program.cs`; `tests/Kumunita.Web.Tests/F10U3_ErrorTrackerTests.cs` lines 1–80 and 170–200.
- **Deliverables (≤ 2):**
  - `Program.cs`, the Sentry block only: `o.MinimumBreadcrumbLevel = LogLevel.Warning; o.MinimumEventLevel = LogLevel.Error;`. First verify the member names by reflection on `SentryAspNetCoreOptions` in 6.12.1; if they are absent, write a drift pause.
  - **Both** comments that call breadcrumbs "vacuous" are corrected: the `UseSentry` drift note (≈ :440) and the `SentryPiiScrubber` header comment (`grep -n vacuous src/Kumunita.Web/Program.cs`).
  - `tests/Kumunita.Web.Tests/SentryBreadcrumbPiiTests.cs`, using an in-memory `ITransport`, an ILogger Information line with `a@example.test`, and a captured exception.
- **Tests:** `InformationLog_IsNotABreadcrumb`, `WarningLog_IsABreadcrumb_WithoutRequestPii`, `ScrubberStillRedactsHeadersCookiesQuery`.
- **Exit:** new tests + `F10U3_ErrorTrackerTests` green.

#### IS06 — `LogRedact` helper
- **Needs:** IS02.
- **Entry reads:** `src/Kumunita.Core/Logging/LogLine.cs`; one pure Core test for style.
- **Deliverables (2, new):** `src/Kumunita.Core/Logging/LogRedact.cs` (`EmailDomain`, which returns `"example.test"` or `"(none)"`; `Hash8`, the first 8 hex characters of SHA-256 for correlating without the address); `tests/Kumunita.Core.Tests/LogRedactTests.cs`.
- **Tests:** `EmailDomain_ReturnsDomainOnly`, `EmailDomain_NullOrMalformed_IsNone`, `Hash8_IsStableAndShort`.

#### IS07 — Redact the identity and SMTP log sites
- **Needs:** IS06.
- **Entry reads:** `sed -n '150,165p;210,222p;262,275p' src/Kumunita.Core/Identity/IdentityService.cs`; `sed -n 285,300p src/Kumunita.Core/Identity/SmtpSender.cs`.
- **Deliverables (2):** the 4 templates switch to `{UserId}` plus `{EmailDomain}`/`{RecipientHash}`. Templates only; no behaviour change.
- **Exit:** build + `IdentityServiceSignupPolicyTests` green (re-point any log assertion only if §2.3 names it).

#### IS08 — Redact the seeders, and the log-PII gate
- **Needs:** IS07.
- **Entry reads:** `sed -n 2572,2585p src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs`; `grep -n "{Email}" src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (≈ :230 and :295, ±3 each); `SecGateSource.cs`.
- **Deliverables (3):** the two seeder files (templates only) and `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs`. The gate ports M4 over `src/**/*.cs` log calls, including multi-line calls, and finds `{…Email…|…Ip…|…Phone…|…Address…|…Recipient…|…Body…}` placeholders. Placeholders ending in `Id` (`{RecipientId}`, `{UserId}`) are ids, not PII, and are excluded by rule. The allowlist is empty.
- **Tests:** `NoLogTemplate_CarriesAPiiPlaceholder`.
- **Exit:** gate green (7 → 0).

#### IS09 — Trust-boundary and log-hygiene docs
- **Needs:** IS05, IS08.
- **Entry reads:** `docs/security/trust-boundaries.md`; `docs/security/decisions-open-items.md` lines 150–180.
- **Deliverables (2 docs):**
  - **B7** Sentry (third party, DSN-gated, Warning floor, scrubber, no PII templates).
  - The B4 note: notification emails carry ≤ 200-character UGC/DM snippets, so class (b) crosses B4.
  - The log-hygiene open item: the PII half is **decided and closed by IS08**; log-forging (L8c) stays open and says so.
- **Exit:** no build.

### Phase B — S4: decision-free reads

#### IS10 — Fix the `TodoDetail` board-title leak
- **Needs:** IS04.
- **Entry reads:** `grep -n "TodoDetail" src/Kumunita.Web/Controllers/TodosController.cs`, then that action ±60 lines; `sed -n 70,80p src/Kumunita.Core/Projects/IBoardService.cs` (`ListBoardsForTodoAsync(todoItemId, actorId)`, which already filters through `CanSeeAsync`: `BoardService.cs` ≈ :168–207); `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` setup (`grep -n "new TodosController("`, ≤ 60 lines).
- **Deliverables (≤ 2):** `TodosController.cs` replaces the raw `session.Query<KanbanBoard>()` title lookup with the injected `IBoardService.ListBoardsForTodoAsync(id, actor)`, and drops placements and lanes whose board is not in the visible set. **No constructor change** (the controller has no `IAuthorizationService`; do not add one). Plus `tests/Kumunita.Web.Tests/TodoDetailBoardVisibilityTests.cs`.
- **Tests:** `TodoDetail_RestrictedBoardTitle_NotShown`, `TodoDetail_VisibleBoardTitle_Shown`, `TodoDetail_RestrictedBoardPlacement_NoLaneChip`.

#### IS11 — Triage batch 1: Posts, Documents, Moderation (13 methods)
- **Needs:** IS04.
- **Entry reads:** the baseline M7 list, filtered to these files; for each method, the method range only (`grep -n` the name, then about 40 lines).
- **Deliverables (≤ 4 files: `PostService.cs`, `PostService.Media.cs`, `DocumentService.cs`, `ModerationService.cs`):** each listed method gets either a one-line `// SEC-READ: <owner-only | caller-gated by <Type.Method> | report-gated (ADR 0003) | no disclosure: <why>>`, or **a pause** if it is a leak. A leak becomes a new `IS10x` unit and is never fixed in the triage unit.
- **Exit:** build green; handoff table *method → verdict → evidence line*.

#### IS12 — Triage batch 2: Events, Projects (12)
As IS11 for `EventService.cs`, `EventService.Group.cs`, `EventRsvpGuardianService.cs`, `TodoService.cs` (≤ 4 files; the constructor hits are field initializers, verify and mark).

#### IS13 — Triage batch 3: Pages, Targets (16)
As IS11 for `PageService.cs` and `ContentTargetResolver.cs`. `BuildPagePathAsync` builds a slug path for a resolved target: decide whether the target was already decided by the caller, and record it. Seeders, `LocalizationService.SeedBaselineAsync` and `SeededContentCatalog` are **excluded by path** in the gate (IS15) and not marked.

#### IS14 — Triage batch 4: Web controllers (6) + the component-list bypass
As IS11 for `PostsController.cs` (the two `Edit` actions, ranges only), `GroupsController.cs` (`EditGroupPost`), `PageController.cs` (the two `Edit` actions). Also verify that `PostsController` ≈ :743 ("GlobalAdmin bypasses the membership check") lists component *names* only and never content. Record the verdict; if it lists content, pause.

#### IS15 — The decision-free-read gate
- **Needs:** IS03 (§2.5), IS10–IS14.
- **Entry reads:** `tools/sec/sec-measure.py` (`m7_method_bypass` only); `SecGateSource.cs`.
- **Deliverables (1 new):** `tests/Kumunita.Web.Tests/SecGate/DecisionFreeReadGateTests.cs`. It is a C# port of M7: a per-method scan; the audience-bearing type list is read from IS03 §2.5 class (b) types; path excludes cover seeders, portability, the eraser, purge jobs and migrations.
- **Tests:** `EveryAudienceBearingRead_IsDecidedOrMarked` (52 → **0** unmarked), `SecReadMarker_NamesAReason`.

### Phase C — S2: data classes in code + complete erasure

#### IS16 — ADR 0298 + `[DataClass]` + catalog reader
- **Needs:** IS03.
- **Entry reads:** design §2.5 (header and 3 sample rows); `docs/adr/README.md` (the last 5 rows, for the index format); `src/Kumunita.Core/Portability/UserScopeInventory.cs` lines 1–30.
- **Deliverables (≤ 4):**
  - `docs/adr/0298-data-classes-in-code.md`, ≤ 80 lines.
  - The ADR index row.
  - `src/Kumunita.Core/Privacy/DataClassAttribute.cs`, containing:
    - the enums `DataClassKind { Pii, PrivateContent, AuditModeration, Secret, Media, Operational, Platform }`, `Erasure { Delete, Pseudonymize, Keep }` and `ExportScope { Resident, InstanceOnly, Never }`;
    - an attribute carrying `Reason` and `RetentionKey?`;
    - `DataClassCatalog.For(Type)` / `.All(Assembly)`.
  - `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs`.
- **Tests:** `Catalog_ReadsAttribute`, `Keep_RequiresReason`, `Pseudonymize_RequiresPrincipalField`.

#### IS17–IS20 — Annotate the 81 types (≈ 20 per unit, a one-line attribute each, copied from §2.5)

| Unit | Types (module) |
|---|---|
| IS17 | Identity / UserInfo / Authorization / Localization (`Profile`, `Group*`, `DelegationGrant`, `GuardianLink`, `GuardianTimeLimitSchedule`, `Component*`, `ModeratorAssignment`, `IdentityToken`, `GuestAccess`, `AccessAudit`, `AuditPurgeSummary`, `AdminOverride`, `LocaleSettings`, `LanguageCatalog`, `TranslationResource`, `OutboxEmail`, `EmailDeadLetter`, `SuppressedAddress`) |
| IS18 | Posts / Announcements / Events / Pages / Tags / Moderation (`Post*`, `Reply*`, `Announcement*`, `Event*`, `GuardianEventRequest`, `Page*`, `Tag*`, `Report`) |
| IS19 | Projects / Documents / Bookmarks / Inventory / PersonalNotes / Media / Kiosk (`Project*`, `Todo*`, `Kanban*`, `Board*`, `Document*`, `Bookmark*`, `Inventory*`, `PersonalNote`, `NoteFolder`, `MediaObject`, `KioskPlaylist`) |
| IS20 | Messaging / GroupMessaging / Notifications / Sessions / Usage / ErrorReports / SiteContent / SurfaceLabels / Escalation / Onboarding / Community*, **plus** `DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` (it reflects over every `Schema.For<T>` registration via a `StoreOptions` built from each `*DocTypes.Configure`, not by regex) |

- **Entry reads per unit:** design §2.5 rows for the batch; `grep -n "class <Type>"` for each type (the attribute goes on the class line, plus a `using`).
- **Deliverables:** only the POCO files of the batch. If a batch touches more than 4 files, split it at the module boundary into `IS17a`/`IS17b`; the plan allows letter suffixes.
- **Exit:** build green; IS20's coverage test green (81/81).

#### IS21 — Erasure: pseudonymize 1:1 and group threads (D3)
- **Needs:** IS20.
- **Entry reads:** `src/Kumunita.Core/Identity/ResidentContentEraser.cs` (the Conversation/Message block, `grep -n "Conversation"` ±10); `grep -n "class GroupMessage\|class GroupConversation\|class GroupMessagingInvitation" -A25 -r src/Kumunita.Core/GroupMessaging`; `IdentityService.AccountOps.cs` `PseudonymizeAuditRowsAsync` (the tombstone shape).
- **Deliverables (≤ 3):**
  - `src/Kumunita.Core/Identity/ResidentThreadPseudonymizer.cs` (new). `Message`/`GroupMessage` where the sender is the resident get `Body = ""` and `SenderId = deleted:{id}`; a `Conversation` seat is tombstoned rather than the conversation deleted; the resident is removed from `GroupConversation.Participants`; `GroupMessagingInvitation` rows where the resident is the invitee are deleted.
  - `ResidentContentEraser.cs`: the delete of `Conversation`/`Message` is replaced with a call to the pseudonymizer.
  - `tests/Kumunita.Core.Tests/DeleteAccountThreadTests.cs`.
- **Tests:** `DeleteAccount_DirectMessages_BodyErased_SenderTombstoned`, `DeleteAccount_OtherPartyThread_Survives`, `DeleteAccount_GroupMessages_Pseudonymized`, `DeleteAccount_RemovedFromGroupConversationParticipants`.
- **Exit:** new tests + `F1` deletion tests green. `DeleteAccount_KeepsOthersContentIntact` must still pass; if an old test asserts *conversation deleted*, pause (rule 9) unless §2.3 names it.

#### IS22 — Erasure: identity-adjacent rows
- **Needs:** IS21.
- **Entry reads:** `ResidentContentEraser.cs` (the tail, `grep -n "MediaObject"` ±5); the 4 notification doc classes (`grep -n "RecipientId"`); `src/Kumunita.Core/Sessions/SessionDoc.cs`; `src/Kumunita.Core/Identity/IdentityToken.cs`, `src/Kumunita.Core/Identity/GuestAccess.cs`, `DelegationGrant` in `src/Kumunita.Core/UserInfo/Group.cs` (`grep -n "class DelegationGrant" -A20`; field names only); in `DeleteCoreAsync`, the `User target` already carries `Email`, so pass `target.Email`.
- **Deliverables (≤ 3):**
  - `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs` (new) deletes `SessionDoc`, `IdentityToken`, `GuestAccess`, `DelegationGrant` (owner or delegate), the 4 notification types, and `OutboxEmail` where `Recipient` is the resident's email. The email is passed in by the composer; it is never logged.
  - One call line in `DeleteCoreAsync` (`IdentityService.AccountOps.cs`, an anchored edit).
  - `tests/Kumunita.Core.Tests/DeleteAccountRowsTests.cs`.
- **Tests:** `DeleteAccount_RemovesSessions`, `DeleteAccount_RemovesTokensAndGuestAccess`, `DeleteAccount_RemovesDelegations_BothSides`, `DeleteAccount_RemovesNotificationsAndOutbox`.

#### IS23 — Erasure: guardian, community, error, escalation, usage
- **Needs:** IS22.
- **Deliverables (≤ 3):** extend `ResidentAccountRowsEraser.cs`:
  - `GuardianTimeLimitSchedule` and `GuardianEventRequest`, where the resident is the child → delete.
  - `CommunityMembershipRequest` → delete.
  - `ErrorReport`: `SubjectId` → tombstone and `ContactEmail` → null.
  - `EscalationToken.CreatedBy` → tombstone.
  - `UsageEvent.ActorId` → tombstone.

  Plus the test file `DeleteAccountRowsTests.cs` (append).
- **Tests:** `DeleteAccount_ChildGuardianRows_Removed`, `DeleteAccount_ErrorReport_Pseudonymized_ContactEmailCleared`, `DeleteAccount_UsageEvents_Pseudonymized`.

#### IS24 — Erasure and export agree with the attributes (gate)
- **Needs:** IS23.
- **Deliverables (≤ 3):** a `public static IReadOnlySet<Type> HandledTypes` on `ResidentContentEraser`, `ResidentThreadPseudonymizer` and `ResidentAccountRowsEraser` (declared next to each query, so one fact lives in one place); and `tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs` (append).
- **Tests:** `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`, `EveryResidentExportType_IsInUserScopeInventory`, `NoHandledType_IsMarkedKeep`.
- **Exit:** gate green (E2: 25 → 0 undecided, 0 drift).

### Phase D — S1: the operator-gated instance export

#### IS25 — ADR 0299: the instance export is operator-gated
- **Needs:** IS03 §2.6.
- **Entry reads:** `docs/adr/0003-roles-and-moderator-scoping.md` lines 40–70; `docs/design/m11-portability-design.md` lines 148–170 and the C-M11·6 constraint (`grep -n "C-M11·6"`); the messaging D3 constraint (`grep -n "D3" docs/design/m9-messaging-design.md | head -5`).
- **Deliverables (2 docs):** the ADR (≤ 80 lines: content-free by default, full only under an active `AdminOverride`, audited `Via: BreakGlass` with the scope recorded, resident transparency per D10, and it supersedes the C-M11·6 "quiet surface" line for the full scope) and the ADR index row.

#### IS26 — `BreakGlassProbe` (extract, don't widen the frozen seam)
- **Needs:** IS02.
- **Entry reads:** `grep -n "HasBreakGlassAsync" -A30 src/Kumunita.Core/Authorization/AuthorizationService.cs`; the existing break-glass tests (`grep -ln "BreakGlass_Expired" tests/Kumunita.Core.Tests/*.cs`, setup ≤ 60 lines).
- **Deliverables (≤ 3):** `src/Kumunita.Core/Authorization/BreakGlassProbe.cs` (`public static Task<bool> HasActiveAsync(IQuerySession, string userId)`, the exact SQL moved from the private method; `IDocumentSession` is an `IQuerySession`, so the existing call sites compile unchanged); `AuthorizationService.cs` (the private method delegates to it; **no `IAuthorizationService` change**, per ADR 0006); `tests/Kumunita.Core.Tests/BreakGlassProbeTests.cs`.
- **Tests:** `Probe_ActiveOverride_True`, `Probe_Expired_False`, `Probe_NotConsumed_False`.
- **Exit:** new tests + the existing `BreakGlass_*` tests green.

#### IS27 — `PortabilityExportScope` + the content-free filter
- **Needs:** IS24, IS26.
- **Entry reads:** `src/Kumunita.Core/Portability/PortabilityExportDocuments.cs` lines 130–175 (the export loop); `IPortabilityService` (`grep -rn "interface IPortabilityService" -A20 src`); design §2.6.
- **Known touches:** the existing `ExportAsync(actor, ct)` callers in `PortabilityRoundTripTests.cs` (≈ :608/:779/:887) and `AdminPortabilityControllerTests.cs` (≈ :128) keep compiling because the old overload stays. It now means `ContentFree`, so a round-trip test that expects private rows must call the `Full` overload. If that edit is needed, it is **IS27b** (tests only), not a fifth file here. Keep the `CancellationToken` parameter on the new overload.
- **Deliverables (≤ 4):** `PortabilityExportScope.cs` (new); `PortabilityExportDocuments.cs`, where `ContentFree` skips the types §2.6 marks (driven by `DataClassCatalog`: `PrivateContent` types and rows with a non-null restrictive `Audience`) and blanks the §2.6 PII fields on `Profile`; the `IPortabilityService`/`PortabilityService` overload; `tests/Kumunita.Core.Tests/PortabilityExportScopeTests.cs`.
- **Tests:** `ContentFree_HasNoConversationsOrMessages`, `ContentFree_HasNoRestrictedPosts_HasPublicOnes`, `ContentFree_BlanksProfileContactFields`, `Full_RoundTripsAsBefore`.
- **Exit:** new + `PortabilityRoundTripTests` green (round-trip uses `Full`).

#### IS28 — Controller: scope, override gate, audit row
- **Needs:** IS27.
- **Entry reads:** `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (whole file, under 200 lines); `grep -n "portability.export" -B5 -A15 src/Kumunita.Core/Portability/PortabilityService.cs`; the admin portability view (`ls src/Kumunita.Web/Views/AdminPortability`).
- **Deliverables (≤ 4):**
  - Controller: `GET export?scope=full` returns `Forbid` unless `BreakGlassProbe.HasActiveAsync`.
  - Service: the audit row records `Action = portability.export.{content-free|full}` and `Via = Admin|BreakGlass`.
  - View: two buttons, with "full" explaining the operator step.
  - `tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs`.
- **Tests:** `Export_Default_IsContentFree`, `FullExport_WithoutOverride_Forbidden`, `FullExport_WithOverride_AuditedBreakGlass`.

#### IS29 — Resident transparency entry (D10)
- **Needs:** IS28. `// TODO ❓ D10`. Default: an `INotificationEmitter` notice to every non-guest resident, with kind `instance.full-export`, stating the date and that the operator authorised it, and with no actor name. Deliverables ≤ 3: the emitter call in the full branch, the `NotificationKinds` constant + translation key (appended), and the test `FullExport_NotifiesEveryResident`, `ContentFreeExport_NotifiesNoOne`.

#### IS30 — Export docs
- **Needs:** IS29. **Deliverables (≤ 3 docs):** an OPS break-glass runbook section ("full instance export"); a note in the M11 design doc linking ADR 0299. The control-map row is **IS39's** (it lands in the new format there).

### Phase E — Medium findings

#### IS31 — Resident export: notes, folders, page translations
- **Needs:** IS24.
- **Entry reads:** `UserScopeInventory.cs` lines 60–100; `ImportOwnershipGuard.cs` (the type-policy switch, `grep -n "case \"\|=> \"" `); `grep -n "UserScopeInventory.Entries" src/Kumunita.Core/Portability/UserPortabilityService*.cs` (the export type map, ±15).
- **Deliverables (≤ 4):** the inventory rows (`PersonalNote`/`NoteFolder`/`BookmarkFolder` by `OwnerId`, `PageTranslation` by `AuthorId`); the type map in the export; the guard needs no change if the generic owner rule covers these types (verify; otherwise add the rule); `tests/Kumunita.Core.Tests/UserPortabilityNotesExportTests.cs`.
- **Tests:** `Export_IncludesOwnNotesAndFolders`, `Export_ExcludesOthersNotes`, `Import_ForeignNote_Rejected`.

#### IS32 — Resident export: group messaging (indirect scope)
- As IS31 for `GroupConversation` (resident ∈ `Participants`) and `GroupMessage` (via `ConversationId`, the `Message` indirect rule). The **guard gets a `GroupMessage` rule** mirroring `Message`: the caller supplies the actor's group-conversation ids, and an empty set means fail-closed.
- **Tests:** `Export_IncludesGroupThreadsActorIsIn`, `Import_GroupMessage_ForeignConversation_Rejected`, `Guard_GroupMessage_RequiresActorConversation`.

#### IS33 — `RetentionOptions` + `RetentionPolicy` (pure)
- **Needs:** IS02 §2.2.
- **Entry reads:** `src/Kumunita.Core/Usage/UsagePurgePolicy.cs` (the pure-policy precedent); `src/Kumunita.Core/Media/MediaLifecycleTick.cs` (a 15-line record).
- **Deliverables (≤ 3):** `src/Kumunita.Core/Privacy/RetentionOptions.cs` (the D6 keys and defaults); `RetentionPolicy.cs` (`CutoffFor(string key, DateTimeOffset now)`, where `0` means keep); `tests/Kumunita.Core.Tests/RetentionPolicyTests.cs`.
- **Tests:** `Defaults_MatchD6`, `ZeroMeansKeep`, `Cutoff_IsNowMinusDays`.

#### IS34a — `RetentionTick` record + Core service, part 1: sessions, tokens, outbox, dead letters
- **Needs:** IS33.
- **Entry reads:** `src/Kumunita.Core/Media/MediaLifecycleTick.cs` (the record); `src/Kumunita.Web/SideEffects/MediaLifecycleHandler.cs` (the handler shape: Core does the work, Web hosts the Wolverine handler); `src/Kumunita.Core/Usage/UsagePurgeService.cs` lines 40–80 (the delete-by-cutoff idiom); the field names in `src/Kumunita.Core/Sessions/SessionDoc.cs` and `src/Kumunita.Core/Identity/EmailMessages.cs`.
- **Deliverables (≤ 3):**
  - `src/Kumunita.Core/Privacy/RetentionTick.cs`, the record.
  - `src/Kumunita.Core/Privacy/RetentionService.cs`. It deletes `SessionDoc` revoked or expired beyond the cutoff, consumed or expired `IdentityToken`/`EscalationToken`, and `EmailDeadLetter`. For `OutboxEmail` it **nulls `Body` after 7 days** and deletes after 90. It returns counts only, with no PII (the IS08 gate applies).
  - `tests/Kumunita.Core.Tests/RetentionServiceTests.cs`, plus the DI line in the module's `DependencyInjection.cs` if the module has one (a 4th file); otherwise register in IS34b.
- **Tests:** `Retention_DeletesOldRevokedSessions_KeepsLive`, `Retention_NullsOutboxBodyAfter7d_DeletesAfter90d`, `Retention_DeletesConsumedTokens`, `Retention_IsIdempotent`.

#### IS34b — `RetentionTickHandler` + the seed
- **Needs:** IS34a.
- **Entry reads:** `MediaLifecycleHandler.cs` (whole); `grep -n "MediaLifecycleTick" src/Kumunita.Web/Program.cs` (the `bus.PublishAsync` seed at ≈ :1331, ±10).
- **Deliverables (≤ 3):** `src/Kumunita.Web/SideEffects/RetentionTickHandler.cs` (it mirrors `MediaLifecycleHandler`: calls `RetentionService`, logs counts, reschedules); `Program.cs` (**one** seed line next to the `MediaLifecycleTick` seed — the lane's second and last `Program.cs` touch); `tests/Kumunita.Web.Tests/RetentionTickHandlerTests.cs`.
- **Tests:** `Handler_CallsService_AndReschedules`.

#### IS35 — `RetentionTick` part 2: notifications, error reports, log files
- **Needs:** IS34b. Extend `RetentionService` to `Notification` (read 90 days, unread 365) and resolved `ErrorReport` (180 days), and call `RollingFileSink.Retain` daily.
- **Deliverables (≤ 3):** `RetentionService.cs`, `docs/ops/configuration-reference.md` (the `Retention__*` keys), and the test file (append).
- **Tests:** `Retention_DeletesOldReadNotifications`, `Retention_DeletesResolvedErrorReportsAfter180d`, `Retention_RetainsLogFilesDaily`.

#### IS36 — Audit the unaudited admin reads (D9)
- **Needs:** IS02. `// TODO ❓ D9` (default: one aggregate row per view).
- **Entry reads:** `AdminController.cs` `Audit` action (`grep -n "public async Task<IActionResult> Audit"` ±40); `ErrorReportAdminController.cs` list/detail actions (`grep -n "Http"` ±20); `AdminGuestsController.cs` the list action; `AccessAuditFactory.cs`.
- **Deliverables (≤ 4):** the three controllers, each writing one `AccessAudit` per view (`Via: Admin`, `TargetKind` `audit-log`/`error-reports`/`guests`, `VisibleCount`); `tests/Kumunita.Web.Tests/AdminReadAuditTests.cs`.
- **Tests:** `AuditLogView_IsItselfAudited`, `ErrorReportView_IsAudited`, `GuestListView_IsAudited`.

#### IS37 — The confirm views move to the existing `data-confirm` module
- **Needs:** IS04.
- **Entry reads:** `src/Kumunita.Web/client/lib/confirm.ts` (whole; the existing `form[data-confirm]` contract, already loaded globally in `Views/Shared/_LayoutScripts.cshtml`); `tsconfig.json`; `grep -rnE "\son[a-z]+=\"" src/Kumunita.Web/Views` (8 real hits; the 2 in `_LayoutScripts` are HTML comments).
- **Deliverables (≤ 4):** `Notes/Index.cshtml` (2), `Bookmarks/Index.cshtml` (1) and `AdminSurfaceLabels/Index.cshtml` (1): each `onsubmit="return confirm('…')"` becomes `data-confirm="…"`, using the existing module with **no new TS**. Also `AdminHelp/Index.cshtml` (2), the same treatment.
- **Exit:** build green; record that the four destructive confirms now run.

#### IS38 — Autosubmit + copy, and the CSP-discipline gate
- **Needs:** IS37.
- **Deliverables (≤ 4):**
  - `client/lib/autosubmit.ts` (`[data-autosubmit]` change → `form.requestSubmit()`) and `client/lib/copy-text.ts` (`[data-copy-target]` click → clipboard). Or put both in one `client/lib/inline-free.ts`, to stay within 4 files.
  - The `_LayoutScripts.cshtml` include line.
  - `AdminAnalytics/Index.cshtml` + `Admin/Escalation/Tokens.cshtml` (one handler each).
  - If the gate test would make a 5th file, it moves to **IS38b**: `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`, which ignores Razor and HTML comments.
- **Tests (IS38/IS38b):** `NoView_HasAnInlineEventHandler` (8 → 0), `NoView_HasAnInlineScriptBlock`.
- **Exit:** build + `tsc` green.

#### IS39 — Control-map rewrite (traceability)
- **Needs:** IS24, IS30, IS36, IS38.
- **Entry reads:** `docs/security/control-map.md`; the `## IS` handoff headers (`grep -n "^## IS"`); `ls tests/Kumunita.Web.Tests/SecGate`.
- **Deliverables (1 doc):** the table gets the columns **Control · Answers · Seam (code link or `ops:` runbook link) · Pinned by (test name) · ADR**.
  - The stamp row moves to `RevocationStampMiddleware` + `PrivilegedStampMiddleware`, and records that `SecurityStampValidatorOptions` is deliberately inert at 14 days.
  - New rows: data classes (ADR 0298), erasure, retention, operator-gated export (ADR 0299), decision-free-read markers, log redaction + Sentry floor, CSP discipline, admin-read audit.
  - Every code row names an existing test.

#### IS40 — The control-map traceability gate
- **Needs:** IS39. **Deliverables (1 new):** `tests/Kumunita.Web.Tests/SecGate/ControlMapTraceabilityTests.cs`.
- **Tests:** `EveryRow_HasASeamLink_ThatResolves`, `EveryCodeRow_NamesAPinningTestThatExists`, `EveryOpsRow_LinksARunbookThatExists` (M1: 9/24 → all).

#### IS41 — Doc-truth pass + the lessons as rules
- **Needs:** IS24, IS40.
- **Deliverables (≤ 4 docs):**
  - `docs/security/data-classes.md`: the five-class prose stays; the table becomes a pointer to `[DataClass]` + design §2.5; it adds the missing classes (sessions, outbox, group messages).
  - `decisions-open-items.md`:
    - correct the token-at-rest claim for the verify lane (credential = token row id, owner decision U23, 2026-10-31);
    - close the stale "named invariant tests" item with links;
    - record the retention decision (D6).
  - `AGENTS.md` (or `docs/start/agent.md` after IMPROVE-DOCS), the rules section: *"A new document type carries `[DataClass]`; a new read of an audience-bearing type is decided in-method or marked `// SEC-READ:`; log templates never carry PII; views never carry inline handlers. The SecGate tests enforce each."* WhatsNew history is **not** rewritten.

#### IS42 — Owner-ops ledger
- **Deliverables (≤ 3 docs):** `docs/ops/owner-ops-ledger.md` (new, with front-matter and abstract). One table: item · source (F-row / SEC unit / IS unit) · what proves it · **last verified** (blank = not verified). It merges the prod-hardening `## Summary` open items, the SEC operator checks, and the new ones (`Retention__*` reviewed, the `SENTRY_DSN` decision now safe after IS05/IS08, full-export runbook rehearsed). It is linked from `docs/ops/hardening-checklist.md` and `docs/SECURITY.md`. No values are flipped; the ledger is a record.

### Phase F — Close

#### IS43 — Run and record
- **Needs:** IS04–IS42.
- **Do:** full build, both full suites, `tools/sec/sec-report.ps1`. Record in the design doc `### Run result` the before → after numbers for the six baselines (control-map pinned 9/24 → 100%; undecided erasure 25 → 0; PII log placeholders 7 → 0; unmarked decision-free reads 52 → 0; inline handlers 8 → 0; unaudited admin reads 3 → 0) and the totals against the IS04 baseline. Delete `tools/sec/sec-measure.py`, whose checks now live in `SecGate`. **If a gate is red, stop and list it.**

#### IS44 — Independent verification (cold read)
- A **fresh agent with no lane context** gets only the DoD below, the repo, and three probes:
  1. Add a throwaway doc type with a `UserId` and run the tests. `DataClassCoverageTests` must fail.
  2. Add `logger.LogInformation("{Email}", e)` and run the tests. The log gate must fail.
  3. As a GlobalAdmin with no override, request `admin/portability/export?scope=full`. Expect 403, and a content-free archive by default containing no `Message.json`.

  It reports each DoD line PASS/FAIL with evidence and reverts the probes. Its report is appended verbatim to the handoff note.

#### IS45 — Close flip + archive
- **Needs:** IS44 all-PASS. The named-lane close flip per `AGENTS.md`: `WhatsNew.cs` entry + `WhatsNewTests` head-pin; `README.md` roadmap named-lane row; `STATUS.md`; `ARCHITECTURE.md` (a line on data classes in code); ADR index verified; `MilestonesTests` untouched. Move the lane files to `docs/archive/lanes/improve-sec/`. If IS44 failed any line, **do not flip**: record it and hand off to a successor.

## Run order

`IS00 → IS01 → IS02 → IS03 → IS04`, then the phases. A, B and C are independent of each other after IS04 (parallel branches are fine, merged one at a time). D needs C (IS24) and IS26. E: IS31–IS32 need IS24; IS33 → IS34a → IS34b → IS35; IS36 and IS37 → IS38 are independent; IS39 → IS40 come after D, IS36 and IS38; IS41–IS42 come last. Then `IS43 → IS44 → IS45`. **`Program.cs` is touched only by IS05 (the Sentry block) and IS34b (one seed line)** in this lane. Merge those two one at a time.

## Definition of Done

1. **P-1:** every registered doc type carries `[DataClass]` (81/81); `DataClassCoverageTests` green.
2. **P-2:** account deletion handles every `Delete`/`Pseudonymize` type; the IS21–IS23 tests are green; 1:1 and group threads are pseudonymized (D3); F1 tests still green.
3. **P-3:** 0 PII log placeholders (gate); Information-level logs are not Sentry breadcrumbs (test); B7 is documented.
4. **P-4:** 0 unmarked decision-free reads (gate); the `TodoDetail` leak is fixed and tested; every triage verdict is recorded.
5. **P-5:** the instance export is content-free by default; full requires an active `AdminOverride`, is audited `Via: BreakGlass`, and notifies residents (D10); ADR 0299 Accepted.
6. **P-6:** `RetentionTick` covers every type §2.5 gives a retention key; log files are retained daily.
7. **P-7:** every control-map row has a resolving seam link and a pinning test (gate).
8. **P-8:** 0 inline handlers or scripts in views (gate); the destructive confirms work again.
9. The resident export includes notes, folders, page translations and group threads, and the H1 guard covers each new type.
10. The audit log, error-report and guest views are themselves audited (D9).
11. `AccessViaDenyMatrixTests` green (11/11 `AccessVia` pinned).
12. The owner-ops ledger exists and is linked; the doc-truth corrections have landed; the four lessons are rules in `AGENTS.md`.
13. Full suites show no failure outside the IS04 baseline; the IS44 cold read is all PASS; the close flip has landed.

## Prior work this lane builds on

- **SEC remediation** (`archive/lanes/sec/`): the sealed-unit protocol, the test-runner form (U3), the H1 import guard (extended in IS32), and the owner decisions U21 and U23/U24, which are not reopened.
- **Prod-hardening** (`archive/lanes/prod-hardening/`): F1 deletion (extended, not replaced), F10 Sentry (fixed at the floor), F15 lifecycle and the `UsagePurge` pattern (reused by the retention tick), and the owner-ops summary (folded into IS42).
- **IMPROVE U08** (`archive/lanes/improve/improve-audit.md`): the deny matrix is turned into a gate; the guardian/translation/portability passes are not redone.
- **IMPROVE-CODE C10** (module self-registration): new jobs register in their module.