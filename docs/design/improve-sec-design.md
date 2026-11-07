---
id: design-improve-sec
title: "IMPROVE-SEC — design doc (context, invariants, findings register)"
tier: living
kind: design
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/plans-milestones/plan-improve-sec.md
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
updated: 2026-11-07
---

> **Abstract:** the IMPROVE-SEC design doc — Part 1 (IS01): the context, the
> eight invariants (P-1…P-8) the lane makes enforceable, and the 13-row
> findings register (E1–E13) transcribed from the
> [plan](../plans-milestones/plan-improve-sec.md). Part 2a (pins, fix shapes,
> test names — IS02) and Part 2b (the 81-row data-class table — IS03) follow
> in the same file. **Who is it for:** whoever picks up an `IS`-unit, and the
> reviewer of the close. **When to read the rest:** before starting any
> `IS`-unit — units name their own entry reads from here.

# IMPROVE-SEC — design doc

Part 1 of this doc is IS01 of the [IMPROVE-SEC
plan](../plans-milestones/plan-improve-sec.md). The plan is the contract;
this doc is the working reference the units build against.

## Context

The SEC remediation lane (H1–L8), the prod-hardening lane (F1–F22) and
IMPROVE's U08 audit closed the **known holes** in this repo. The `Decide()`
authorization matrix is standing-keyed with a deny test per `AccessVia`
(U08); the Sentry request scrubber redacts headers, cookies and query; the
H1 import guard fails on foreign content; anti-forgery is global; the thin
token plus per-request authorization model with its revocation and
privileged-stamp middlewares is sound. In the philosophy's words
([`docs/philosophy/in-code.md`](../philosophy/in-code.md)): *the parts are
cheap, the linkage is rare.*

**This lane goes after the linkage.** The control map promises that
"every threat must be covered", and the data-classes doc says to add a row
*before merge* — but both promises are kept by people reading docs, not by
the build. Every finding in the register below is a **seam with no gate**:
a security or privacy promise that lives in a doc, a runbook, or one
reviewer's memory, with no code seam and no test that would fail when the
next change drifts. The lane makes each promise *enforced and pinned*:
every fix ships with a gate test, and the gates follow the IMPROVE-CODE
precedent — they start from the measured baseline and **retire it to zero,
never freeze it**.

The four severity-S gaps (severity per [`docs/SECURITY.md`](../SECURITY.md);
the register below carries the detail):

- **E1** — the GlobalAdmin instance export carries every private message and
  piece of audience-restricted content, undoes the messaging D3 wall, and
  writes an audit row with nothing residents can see (no gate on the seam).
- **E2** — account deletion misses doc types that shipped after F1 (group
  messaging, `SessionDoc`, `ErrorReport`, `OutboxEmail`, notifications, …);
  25 of 65 principal-bearing types are not erased, with no recorded
  decision (the eraser has no policy to close against).
- **E3** — seven log templates carry PII, and Sentry's ILogger integration
  ships Information-level logs to a third party as breadcrumbs the scrubber
  cannot redact (a boundary with no row and no floor).
- **E4** — 52 methods read audience-bearing types with no decision in the
  same method, one of which is a confirmed leak (`TodoDetail` board
  titles); the rest are caller-gated but unmarked (the implicit contract).

**Scope correction.** This repo has **no tenant-by-slug model**:
[`ARCHITECTURE.md`](../ARCHITECTURE.md) says one instance per neighborhood,
with no multi-tenant data model. "Tenant isolation" therefore maps to
*community/component scoping*, which U08 verified (`AccessVia.Community`);
the `IS04` deny-matrix gate pins it.

**What this lane is not:** not a re-audit of what earlier lanes verified (it
does not redo the U08 `Decide()` matrix — it **turns U08's table into a
gate**, `IS04`); not a re-fix of H1–L8 or F1–F22; it does not reopen owner
decisions (U21, U23/U24, F6, F22 Option A); and it adds no new feature
surface beyond what the fixes require.

## Invariants

The eight invariants the lane makes enforceable. Ids and wording are the
plan's; each is pinned by a named gate (Part 2a, IS02, fixes the exact test
names).

- **P-1** a doc type cannot be registered without a `[DataClass]`.
  Closes E2/E10 (types with no recorded data-class policy); enforced by
  `DataClassCoverageTests` (IS16, IS20, gate IS24).
- **P-2** account deletion leaves no row whose policy says `Delete`, and no un-tombstoned principal where it says `Pseudonymize`.
  Closes E2 (incomplete erasure); enforced by the IS21–IS23 eraser tests
  and the IS24 agreement gate.
- **P-3** no log template carries a PII placeholder; nothing below Warning reaches a third party.
  Closes E3 (PII → Sentry breadcrumbs); enforced by
  `SentryBreadcrumbPiiTests` (IS05) and the log-PII gate
  `LogTemplatePiiGateTests` (IS08).
- **P-4** every read of an audience-bearing type is decided in-method or marked with its gate.
  Closes E4 (decision-free reads, incl. the `TodoDetail` leak); enforced by
  `TodoDetailBoardVisibilityTests` (IS10) and the
  `DecisionFreeReadGateTests` gate (IS15).
- **P-5** no GlobalAdmin-only path yields class (b) content without an operator-granted override.
  Closes E1 (instance export wall); enforced by
  `AdminPortabilityScopeTests` (IS28) under ADR 0299 (IS25).
- **P-6** every class (a)/(b)/(c) type has a retention key or a `Keep` reason.
  Closes E5 (no retention); enforced by the IS33–IS35 `RetentionService`
  tests and the IS24 `NoHandledType_IsMarkedKeep` gate.
- **P-7** every control-map row links a seam and names a pinning test.
  Closes E8 (control map with no traceability); enforced by
  `ControlMapTraceabilityTests` (IS40) over the rewritten map (IS39).
- **P-8** no inline script or handler in views.
  Closes E9 (inline handlers CSP blocks); enforced by
  `CspInlineDisciplineGateTests` (IS38/IS38b) after the confirm and
  autosubmit migrations (IS37–IS38).

## Findings register

Thirteen rows, E1–E13, transcribed as written from the plan's Evidence
table. *Sev* follows `docs/SECURITY.md`. *Where* carries file:line
references measured at `main` `1970cfc4` (2026-11-06) — **line numbers are
the plan's measurement at `1970cfc4`; IS02 re-verifies every one and
corrects or marks n/a.**

| # | Sev | Finding (one line) | Where (file:line) | Principle / anti-pattern | Units |
|---|---|---|---|---|---|
| E1 | **S** | Instance export includes all 44 M11 types — every `Conversation`/`Message`, audience-restricted posts, profile contact fields, reports; one audit row, nothing residents can see; contradicts the messaging D3 wall and ADR 0003 | `AdminPortabilityController` `GET admin/portability/export` → `PortabilityService.ExportAsync`; `PortabilityDocTypes` rows 41–42 | part-vs-whole (an operator surface undoes a resident guarantee); *the side door* | IS25–IS30 |
| E2 | **S** | Account deletion incomplete: 25 of 65 principal-bearing types not erased, most with no recorded decision; group messaging, `SessionDoc`, `ErrorReport`, `OutboxEmail`, notifications, `DelegationGrant`, guardian rows, `UsageEvent` survive | `ResidentContentEraser.EraseAsync`; `IdentityService.AccountOps.DeleteCoreAsync`; [`prod-hardening-f1-gdpr-deletion.md` §2C](prod-hardening-f1-gdpr-deletion.md) | closed-loop; *the orphaned part* | IS16–IS24 |
| E3 | **S** | PII reaches a third party via log breadcrumbs: 7 templates carry `{Email}`/`{Recipient}`; Sentry ships Information+ as breadcrumbs the scrubber cannot redact ("vacuous"); no Sentry trust-boundary row | `IdentityService.cs:159,218,270`, `SmtpSender.cs:295`, `FirstBootSeeder.cs:230,295`, `SampleDataSeeder.cs:2579`; `Program.cs` `UseSentry` block + `SentryPiiScrubber` | handoff test (data leaves via an unlisted boundary) | IS05–IS09 |
| E4 | **S** | 52 methods read an audience-bearing type with no decision in the same method; one confirmed leak — `TodoDetail` shows a restricted board's title; the rest caller-gated or owner-only but unmarked | `TodosController.TodoDetail` (≈ :260–285); the baseline M7 list | per-request authorization contract implicit; *the implicit contract* | IS10–IS15 |
| E5 | **M** | No retention for `SessionDoc`, `OutboxEmail`, `Notification`, `ErrorReport`, `EmailDeadLetter`, consumed/expired `IdentityToken`/`EscalationToken`; file-log retention runs only at boot | `RollingFileSink.Retain` (boot only); no purge for these types | Stable face (unbounded accumulation) | IS33–IS35 |
| E6 | **M** | Resident export (GDPR Art. 15/20) incomplete: misses `PersonalNote`, `NoteFolder`, `BookmarkFolder`, `PageTranslation`, `GroupConversation`, `GroupMessage` | `UserScopeInventory.Entries` | closed-loop | IS31–IS32 |
| E7 | **M** | GlobalAdmin reads of the audit log itself, error reports and guest lists write no audit row | `AdminController.Audit` (≈ :500), `ErrorReportAdminController`, `AdminGuestsController` | the U08 (b) rule applied to admin reads | IS36 |
| E8 | **M** | Control map: 10 of 24 rows link to code, 15 have no test naming their seam; the stamp row points at `SecurityStampValidatorOptions` (inert at 14 days); CSP pinned only by a source regex | `docs/security/control-map.md`; `Program.cs:698` | Coherent face; *documentation as the control* | IS39–IS40 |
| E9 | **M** | 8 inline `on*` handlers in 6 views that `script-src 'self'` silently blocks; the destructive confirms never run; no gate enforces the CSP code discipline | `Notes/Index` (2), `AdminHelp/Index` (2), `Bookmarks/Index`, `AdminSurfaceLabels/Index`, `AdminAnalytics/Index`, `Admin/Escalation/Tokens` | seam with no test | IS37–IS38 |
| E10 | **M** | Data classes exist only in a doc (5 classes, nothing in code); new PII types (`SessionDoc`, `OutboxEmail`, group messages, `ErrorReport.ContactEmail`, `UsageEvent`) are not in it | `docs/security/data-classes.md` | Coherent | IS16–IS20, IS41 |
| E11 | **M** | Docs say one thing and code another: the "tokens hashed at rest" claim for the verify lane (credential is the token row id, U23); DM/mention snippets cross B4, which lists only class (a) | `docs/security/decisions-open-items.md`, `docs/security/trust-boundaries.md`, `WhatsNew.cs` 0.68 text (historical, untouched) | Coherent | IS09, IS41 |
| E12 | **L** | Owner-ops open items spread over two archived handoffs with no single ledger and no "last verified" column | archived handoffs `## Summary` | handoff test (operator) | IS42 |
| E13 | **L** | The "named invariant tests" open item is stale: `EvaluateAudience_EmptyAudience_AllMode_Denies` and `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` both exist | `docs/security/decisions-open-items.md` | — | IS04, IS41 |

**What held up (do not "fix"):** the `Decide()` matrix (standing-keyed, a
deny test per `AccessVia`, U08), including guardian content isolation,
moderator scoping, delegation action scope, break-glass expiry; the thin
cookie plus per-request authorization with the revocation and
privileged-stamp middlewares; the H1 import guard; the GlobalAdmin 2FA
middleware; forwarded-header trust; anti-forgery globally; the Sentry
request scrubber; `SendDefaultPii = false`.
