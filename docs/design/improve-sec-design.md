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

## Part 2a — pins, fix shapes, test names (IS02)

### §2.1 — Re-verified findings register

Every *Where* reference of Part 1's register re-checked against the current
tree on 2026-11-07 (`development` @ `0936c327`), symbol-by-symbol with
`grep -n`, per the per-unit read discipline. **13 of 13 rows
re-verified; 12 confirmed as written, 1 corrected, 0 already fixed in the
tree** — no row is "n/a", so no unit retires on this basis.

| # | Register ref (as written at `1970cfc4`) | Status | Re-verified pin (2026-11-07) |
|---|---|---|---|
| E1 | `AdminPortabilityController` `GET admin/portability/export` → `PortabilityService.ExportAsync`; `PortabilityDocTypes` rows 41–42 | confirmed | `AdminPortabilityController.cs:57,71` (route + `ExportAsync(actor)` call); `IPortabilityService.ExportAsync` at `PortabilityService.cs:51` (impl `:120`, still scope-free); `PortabilityDocTypes.cs:153–154` (`Conversation` = 41, `Message` = 42) |
| E2 | `ResidentContentEraser.EraseAsync`; `IdentityService.AccountOps.DeleteCoreAsync`; F1 design §2C | confirmed | `ResidentContentEraser.cs:36,45` (class + `EraseAsync`; none of `SessionDoc`/`GroupMessage`/`ErrorReport`/`UsageEvent` appears in the file); `IdentityService.AccountOps.cs:298` (`DeleteCoreAsync`) |
| E3 | `IdentityService.cs:159,218,270`; `SmtpSender.cs:295`; `FirstBootSeeder.cs:230,295`; `SampleDataSeeder.cs:2579`; `Program.cs` `UseSentry` + `SentryPiiScrubber` | **corrected** (one line) | `IdentityService.cs:159,218,`**`271`** (plan's `:270` is the preceding argument line; the `{Email}` template is `:271`); `SmtpSender.cs:295`; `FirstBootSeeder.cs:230,295`; `SampleDataSeeder.cs:2579`; `Program.cs:455` (`UseSentry` block), `:1371` (`SentryPiiScrubber`), the "vacuous" notes at `:443` and `:1364` (IS05 corrects both) |
| E4 | `TodosController.TodoDetail` (≈ :260–285) | confirmed | `TodosController.cs:222` (action); the raw `session.Query<KanbanBoard>()` title lookup sits in `:268–285` (board/lane title dictionaries, no decision in-method) |
| E5 | `RollingFileSink.Retain` (boot only) | confirmed | `RollingFileSink.cs:38` (`public static int Retain(string dir, int retentionDays, DateTimeOffset now)`); its only production call site is `Program.cs` (boot); no purge job exists for the §E5 types |
| E6 | `UserScopeInventory.Entries` | confirmed | `UserScopeInventory.cs:66` (`Entries`, "29 doc families" per the doc comment `:63`); `PersonalNote`/`NoteFolder`/`BookmarkFolder`/`PageTranslation`/`GroupConversation`/`GroupMessage` are absent from the file — the E6 gap holds |
| E7 | `AdminController.Audit` (≈ :500), `ErrorReportAdminController`, `AdminGuestsController` | confirmed | `AdminController.cs:500` (`Audit`); `ErrorReportAdminController.cs:135` (`Index`), `:222` (`Detail`); `AdminGuestsController.cs:47` (`Index`); none writes an `AccessAudit` row |
| E8 | `docs/security/control-map.md`; `Program.cs:698` | confirmed | the control-map table (24 rows) is intact; `Program.cs:698–700` (`SecurityStampValidatorOptions`, `ValidationInterval = TimeSpan.FromDays(14)`) — still the inert seam the map points at |
| E9 | `Notes/Index` (2), `AdminHelp/Index` (2), `Bookmarks/Index`, `AdminSurfaceLabels/Index`, `AdminAnalytics/Index`, `Admin/Escalation/Tokens` | confirmed | the 8 real `on*=` hits: `Notes/Index.cshtml:157,253`; `AdminHelp/Index.cshtml:63,88`; `Bookmarks/Index.cshtml:145`; `AdminSurfaceLabels/Index.cshtml:168`; `AdminAnalytics/Index.cshtml:53` (`onchange`); `Admin/Escalation/Tokens.cshtml:100` (`onclick`); the 2 further raw hits are HTML comments at `_LayoutScripts.cshtml:19,22` |
| E10 | `docs/security/data-classes.md` | confirmed | the 5-class table is doc-only; no `[DataClass]` attribute or equivalent exists anywhere under `src/` |
| E11 | `decisions-open-items.md`, `trust-boundaries.md`, `WhatsNew.cs` 0.68 text | confirmed, **where-ref refined** | the "tokens hashed at rest" claim actually lives in `WhatsNew.cs:94–120` (0.68.0) and `STATUS.md:62` (both historical, untouched — the plan's `decisions-open-items.md` pin does not carry the claim; the IS41 correction must land against the open items, not rewrite the history); `trust-boundaries.md:32` (B4 lists class (a) only); the verify-lane "credential is the token row id" decision is the SEC lane's U23 in `docs/archive/lanes/sec/sec-handoff-notes.md` |
| E12 | archived handoffs `## Summary` | confirmed | `docs/archive/lanes/prod-hardening/hardening-handoff-notes.md:1186` (`## Summary`); `docs/archive/lanes/sec/sec-handoff-notes.md:378` (`## Summary`); no single ledger file exists yet |
| E13 | `decisions-open-items.md` | confirmed | the stale item is at `decisions-open-items.md:176`; both named tests exist in `tests/Kumunita.Core.Tests/AuthorizationServiceTests.cs` — `EvaluateAudience_EmptyAudience_AllMode_Denies` and `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` |

**Noticed (for IS03+):** E11's *Where* pin, as written in the plan, points at
`decisions-open-items.md` for the "hashed at rest" claim, but the claim lives
in the historical `WhatsNew.cs` 0.68.0 entry and `STATUS.md` (both out of
scope per the plan — history is not rewritten). IS09's B4 note and IS41's
doc-truth pass are the two landing spots; keep them there.

### §2.2 — Fix shapes and exact public signatures

The seven new public seams, exactly as the implementing units (IS05–IS42)
must ship them. Each is grounded in an entry read of this unit; the owning
module and the shape note are the contract.

- **`LogRedact.EmailDomain(string?)`** — `src/Kumunita.Core/Logging/LogRedact.cs`
  (new, IS06). A `public static` pure helper in the `Logging` module next to
  `LogLine` (no behavior, no PII retention). Returns the lower-cased domain
  of a syntactically valid email (`"a@example.test"` → `"example.test"`), or
  `"(none)"` for null/empty/malformed input. The same file carries
  `public static string Hash8(string? value)` — the first 8 hex characters
  of SHA-256 over the input, for correlating log lines without carrying the
  address (IS06's `Hash8_IsStableAndShort` pins the shape).
- **`BreakGlassProbe.HasActiveAsync(IQuerySession, string)`** —
  `src/Kumunita.Core/Authorization/BreakGlassProbe.cs` (new, IS26). Written
  out: `public static Task<bool> HasActiveAsync(IQuerySession session, string userId)`.
  The parameterised SQL moved verbatim from the private
  `AuthorizationService.HasBreakGlassAsync` (`AuthorizationService.cs:588–618`;
  `SELECT EXISTS` over `mt"."AdminOverride"` where
  `consumedAt IS NOT NULL AND expiresAt > @now`). `IDocumentSession` is an
  `IQuerySession`, so every existing call site (`:237`, `:347`) compiles
  unchanged; the private method then delegates to the probe — the frozen
  `IAuthorizationService` surface is untouched (ADR 0006).
- **`PortabilityExportScope { ContentFree, Full }`** —
  `src/Kumunita.Core/Portability/PortabilityExportScope.cs` (new, IS27). An
  `enum` in the `Portability` module; `ContentFree` is the GlobalAdmin
  default (structure, public content, the actor's own data — per ADR 0299,
  IS25), `Full` requires an active break-glass override (IS28's gate).
- **`IPortabilityService.ExportAsync(string actor, PortabilityExportScope scope, CancellationToken ct = default)`** — the new
  overload on the frozen surface (`PortabilityService.cs:51`, interface) with
  the implementation in `PortabilityService.ExportAsync` (`:120`). The old
  `ExportAsync(string actorId, CancellationToken ct = default)` stays and
  delegates `ContentFree` — so `AdminPortabilityController.cs:71`
  (`portability.ExportAsync(actor)`) and the `PortabilityRoundTripTests`
  callers compile unchanged until IS27 re-points the private-content
  assertions at `Full`.
- **`RetentionOptions`** — `src/Kumunita.Core/Privacy/RetentionOptions.cs`
  (new, IS33). An `IOptions<RetentionOptions>`-shaped POCO in the `Privacy`
  module (C10 self-registration; `Program.cs` untouched until IS34b's seed
  line). Bound from `Retention__*` keys, defaults from D6, `int` days,
  `0` = keep indefinitely:
  ```csharp
  public sealed record RetentionOptions
  {
      public int SessionDays { get; init; } = 30;          // Retention__SessionDays
      public int OutboxBodyDays { get; init; } = 7;        // Retention__OutboxBodyDays
      public int OutboxRowDays { get; init; } = 90;        // Retention__OutboxRowDays
      public int DeadLetterDays { get; init; } = 30;       // Retention__DeadLetterDays
      public int NotificationReadDays { get; init; } = 90; // Retention__NotificationReadDays
      public int NotificationUnreadDays { get; init; } = 365; // Retention__NotificationUnreadDays
      public int ErrorReportDays { get; init; } = 180;     // Retention__ErrorReportDays
      public int TokenDays { get; init; } = 7;             // Retention__TokenDays (consumed/expired IdentityToken + EscalationToken)
  }
  ```
- **`RetentionPolicy.CutoffFor(...)`** — `src/Kumunita.Core/Privacy/RetentionPolicy.cs`
  (new, IS33). Written out: `public static DateTimeOffset? CutoffFor(string key, RetentionOptions options, DateTimeOffset now)`.
  Pure, in the `Privacy` module (the `UsagePurgePolicy.ComputeCutoff`
  precedent at `UsagePurgePolicy.cs:44`, but multi-key and option-driven
  instead of a single platform constant). Resolves a retention key (the
  `RetentionKey` of `[DataClass]`, IS16) to `now − days`; returns `null`
  when the key is unknown or its day count is `0` (keep indefinitely — the
  `ZeroMeansKeep` test, IS33). No service, no session, no PII.
- **`RetentionTick`** — `src/Kumunita.Core/Privacy/RetentionTick.cs` (new,
  IS34a). Written out:
  `public sealed record RetentionTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));`
  — the 15-line record shape of `MediaLifecycleTick.cs:15` /
  `UsagePurgeTick.cs:14` verbatim (Core owns the message; Web hosts the
  `RetentionTickHandler`, IS34b). Core's `RetentionService` (IS34a) does the
  deletes/nulls and returns counts only; the handler reschedules.

### §2.3 — Final test names (IS05–IS42)

The seed list from each unit's plan spec, refined for consistency; a seed
name may be split or renamed within a unit but never dropped. **Every unit
IS05–IS42 has a list below, or a recorded "no test, because …".**

- **IS05** (Sentry floor): `InformationLog_IsNotABreadcrumb`,
  `WarningLog_IsABreadcrumb_WithoutRequestPii`,
  `ScrubberStillRedactsHeadersCookiesQuery`.
- **IS06** (`LogRedact`): `EmailDomain_ReturnsDomainOnly`,
  `EmailDomain_NullOrMalformed_IsNone`, `Hash8_IsStableAndShort`.
- **IS07** (identity + SMTP redaction): no new test — the change is four log
  templates with zero behavior change, and P-3 is pinned by IS05 (sink) and
  IS08 (gate). `IdentityServiceSignupPolicyTests` must stay green.
- **IS08** (seeders + log-PII gate): `NoLogTemplate_CarriesAPiiPlaceholder`
  (7 → 0).
- **IS09** (trust-boundary + log-hygiene docs): no test — docs only; the
  B7/B4 rows are doc-truth pinned by IS41 and the gates named here.
- **IS10** (`TodoDetail` leak): `TodoDetail_RestrictedBoardTitle_NotShown`,
  `TodoDetail_VisibleBoardTitle_Shown`,
  `TodoDetail_RestrictedBoardPlacement_NoLaneChip`.
- **IS11** (triage batch 1): no test — marker comments only (a leak pauses
  and becomes a new `IS10x` unit, never fixed here); the gate IS15 pins the
  outcome. Handoff carries the *method → verdict → evidence line* table.
- **IS12** (triage batch 2): no test — as IS11.
- **IS13** (triage batch 3): no test — as IS11; seeders / `LocalizationService.SeedBaselineAsync` / `SeededContentCatalog` are excluded by path in IS15's gate and not marked.
- **IS14** (triage batch 4 + component-list bypass): no test — as IS11; the
  `PostsController` ≈ `:743` verdict is recorded in the handoff (pause if it
  lists content).
- **IS15** (decision-free-read gate): `EveryAudienceBearingRead_IsDecidedOrMarked`
  (52 → 0 unmarked), `SecReadMarker_NamesAReason`.
- **IS16** (ADR 0298 + `[DataClass]` + catalog): `Catalog_ReadsAttribute`,
  `Keep_RequiresReason`, `Pseudonymize_RequiresPrincipalField`.
- **IS17–IS19** (annotation batches): no test — a one-line attribute per
  type, copied verbatim from §2.5; the coverage gate is IS20's
  `EveryRegisteredDocType_HasADataClass`.
- **IS20** (annotation batch 4 + coverage gate):
  `EveryRegisteredDocType_HasADataClass` (81/81, reflecting over every
  `Schema.For<T>` registration via each `*DocTypes.Configure`, not by
  regex).
- **IS21** (thread pseudonymization, D3):
  `DeleteAccount_DirectMessages_BodyErased_SenderTombstoned`,
  `DeleteAccount_OtherPartyThread_Survives`,
  `DeleteAccount_GroupMessages_Pseudonymized`,
  `DeleteAccount_RemovedFromGroupConversationParticipants`.
- **IS22** (identity-adjacent rows): `DeleteAccount_RemovesSessions`,
  `DeleteAccount_RemovesTokensAndGuestAccess`,
  `DeleteAccount_RemovesDelegations_BothSides`,
  `DeleteAccount_RemovesNotificationsAndOutbox`.
- **IS23** (guardian / community / error / escalation / usage):
  `DeleteAccount_ChildGuardianRows_Removed`,
  `DeleteAccount_ErrorReport_Pseudonymized_ContactEmailCleared`,
  `DeleteAccount_UsageEvents_Pseudonymized`.
- **IS24** (eraser/export agree with attributes):
  `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`,
  `EveryResidentExportType_IsInUserScopeInventory`,
  `NoHandledType_IsMarkedKeep`.
- **IS25** (ADR 0299): no test — an ADR; the behavior it encodes is pinned
  by IS28's tests.
- **IS26** (`BreakGlassProbe`): `Probe_ActiveOverride_True`,
  `Probe_Expired_False`, `Probe_NotConsumed_False`.
- **IS27** (`PortabilityExportScope` + filter):
  `ContentFree_HasNoConversationsOrMessages`,
  `ContentFree_HasNoRestrictedPosts_HasPublicOnes`,
  `ContentFree_BlanksProfileContactFields`, `Full_RoundTripsAsBefore`.
- **IS28** (controller gate + audit): `Export_Default_IsContentFree`,
  `FullExport_WithoutOverride_Forbidden`,
  `FullExport_WithOverride_AuditedBreakGlass`.
- **IS29** (resident transparency, D10): `FullExport_NotifiesEveryResident`,
  `ContentFreeExport_NotifiesNoOne`.
- **IS30** (export docs): no test — OPS runbook + M11 design note; the
  control-map row is IS39's.
- **IS31** (resident export: notes/folders/page translations):
  `Export_IncludesOwnNotesAndFolders`, `Export_ExcludesOthersNotes`,
  `Import_ForeignNote_Rejected`.
- **IS32** (resident export: group messaging):
  `Export_IncludesGroupThreadsActorIsIn`,
  `Import_GroupMessage_ForeignConversation_Rejected`,
  `Guard_GroupMessage_RequiresActorConversation`.
- **IS33** (`RetentionOptions` + policy): `Defaults_MatchD6`,
  `ZeroMeansKeep`, `Cutoff_IsNowMinusDays`.
- **IS34a** (`RetentionTick` + service, part 1):
  `Retention_DeletesOldRevokedSessions_KeepsLive`,
  `Retention_NullsOutboxBodyAfter7d_DeletesAfter90d`,
  `Retention_DeletesConsumedTokens`, `Retention_IsIdempotent`.
- **IS34b** (handler + seed): `Handler_CallsService_AndReschedules`.
- **IS35** (part 2: notifications, error reports, log files):
  `Retention_DeletesOldReadNotifications`,
  `Retention_DeletesResolvedErrorReportsAfter180d`,
  `Retention_RetainsLogFilesDaily`.
- **IS36** (admin-read audit, D9): `AuditLogView_IsItselfAudited`,
  `ErrorReportView_IsAudited`, `GuestListView_IsAudited`.
- **IS37** (confirm views → `data-confirm`): no test — the existing
  `confirm.ts` module is already exercised by the views it now serves; the
  discipline is pinned by IS38/IS38b's gate. The handoff records that the
  four destructive confirms now run.
- **IS38/IS38b** (autosubmit + copy + CSP gate):
  `NoView_HasAnInlineEventHandler` (8 → 0),
  `NoView_HasAnInlineScriptBlock`.
- **IS39** (control-map rewrite): no test — the doc rewrite itself; the
  traceability gate is IS40's.
- **IS40** (control-map gate): `EveryRow_HasASeamLink_ThatResolves`,
  `EveryCodeRow_NamesAPinningTestThatExists`,
  `EveryOpsRow_LinksARunbookThatExists` (M1: 9/24 → all).
- **IS41** (doc-truth + rules): no test — docs only (the `data-classes.md`
  pointer, the open-items corrections, the AGENTS.md rules); the truth it
  pins is already gate-pinned by IS08/IS15/IS24/IS38b, and `WhatsNew.cs`
  history is explicitly not rewritten.
- **IS42** (owner-ops ledger): no test — a record, not a control; its
  "last verified" column is what IS43's close run stamps.

### §2.4 — Drift-guard anchors per unit

The grep-able anchor a later gate or reviewer checks to prove a fix is still
in place. A rename that silently retires a fix breaks its own test (the test
name is the anchor) or one of these greps — either way the drift is caught.

| Unit | Anchor (grep-able) |
|---|---|
| IS05 | `Program.cs` — `o.MinimumBreadcrumbLevel = LogLevel.Warning;` inside the `UseSentry` block; `grep -n vacuous src/Kumunita.Web/Program.cs` → 0 hits |
| IS06 | `public static string EmailDomain(` and `public static string Hash8(` in `src/Kumunita.Core/Logging/LogRedact.cs` |
| IS07 | `grep -rn "{Email}" src/Kumunita.Core/Identity/` → 0 hits (the 4 templates now `{UserId}` + `{EmailDomain}` / `{RecipientHash}`) |
| IS08 | `grep -rn "{Email}" src/Kumunita.Core/Bootstrap/` → 0 hits; `NoLogTemplate_CarriesAPiiPlaceholder` |
| IS09 | `grep -n "^| B7" docs/security/trust-boundaries.md`; `B4` row carries "class (b)" (the snippet note) |
| IS10 | `TodosController.cs` — `ListBoardsForTodoAsync` call in the `TodoDetail` action body; `grep -n "session.Query<KanbanBoard>()" src/Kumunita.Web/Controllers/TodosController.cs` → 0 hits |
| IS11–IS14 | `// SEC-READ:` markers in `PostService.cs`, `PostService.Media.cs`, `DocumentService.cs`, `ModerationService.cs`, `EventService.cs`, `EventService.Group.cs`, `EventRsvpGuardianService.cs`, `TodoService.cs`, `PageService.cs`, `ContentTargetResolver.cs`, `PostsController.cs`, `GroupsController.cs`, `PageController.cs`; every marker line ends in a reason (IS15's `SecReadMarker_NamesAReason` enforces) |
| IS15 | `EveryAudienceBearingRead_IsDecidedOrMarked` + `SecReadMarker_NamesAReason` |
| IS16 | `[DataClass(` + `public enum DataClassKind` + `DataClassCatalog` in `src/Kumunita.Core/Privacy/DataClassAttribute.cs` |
| IS17–IS20 | `grep -rc "\[DataClass(" src --include="*.cs"` ≥ 81; `EveryRegisteredDocType_HasADataClass` |
| IS21 | `public static IReadOnlySet<Type> HandledTypes` on `ResidentThreadPseudonymizer` + `ResidentContentEraser`; `deleted:{` tombstone literal in `ResidentThreadPseudonymizer.cs` |
| IS22 | `ResidentAccountRowsEraser.cs` — `SessionDoc` / `IdentityToken` / `GuestAccess` / `DelegationGrant` / `OutboxEmail` in its `HandledTypes` set; the one call line in `DeleteCoreAsync` (`IdentityService.AccountOps.cs`) |
| IS23 | `ResidentAccountRowsEraser.HandledTypes` carries `GuardianTimeLimitSchedule`, `GuardianEventRequest`, `CommunityMembershipRequest`, `ErrorReport`, `EscalationToken`, `UsageEvent` |
| IS24 | `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` + `EveryResidentExportType_IsInUserScopeInventory` + `NoHandledType_IsMarkedKeep` |
| IS25 | `docs/adr/0299-*` (≤ 80 lines) + the ADR index row |
| IS26 | `public static Task<bool> HasActiveAsync(IQuerySession` in `BreakGlassProbe.cs`; the private `AuthorizationService.HasBreakGlassAsync` body is a single delegate call to the probe (no second SQL literal) |
| IS27 | `public enum PortabilityExportScope` + `Task<Stream> ExportAsync(string actorId, PortabilityExportScope scope, CancellationToken ct = default)` in `PortabilityService.cs` |
| IS28 | `BreakGlassProbe.HasActiveAsync` call in `AdminPortabilityController.cs`; audit actions `portability.export.content-free` / `portability.export.full` (string literals) |
| IS29 | the `instance.full-export` notification kind constant in `NotificationKinds` (or its file) |
| IS30 | the "full instance export" section heading in `docs/ops/` (runbook) |
| IS31 | `UserScopeInventory.Entries` carries `PersonalNote`, `NoteFolder`, `BookmarkFolder`, `PageTranslation` |
| IS32 | the `GroupMessage` case in `ImportOwnershipGuard` (mirrors the `Message` rule) |
| IS33 | `public static DateTimeOffset? CutoffFor(string key, RetentionOptions options, DateTimeOffset now)` in `RetentionPolicy.cs` |
| IS34a | `public sealed record RetentionTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));` in `RetentionTick.cs`; `RetentionService`'s body-null + delete-by-cutoff methods |
| IS34b | `RetentionTickHandler` in `src/Kumunita.Web/SideEffects/`; `RetentionTick` seed line in `Program.cs` |
| IS35 | `RetentionService` — the read/unread `Notification` split + the resolved-`ErrorReport` delete; the daily `RollingFileSink.Retain` call site (not `Program.cs` boot) |
| IS36 | the three `AccessAudit` `TargetKind` literals — `"audit-log"`, `"error-reports"`, `"guests"` — in the three controllers |
| IS37 | `data-confirm=` present in `Notes/Index.cshtml`, `AdminHelp/Index.cshtml`, `Bookmarks/Index.cshtml`, `AdminSurfaceLabels/Index.cshtml`; `onsubmit="return confirm(` absent from all four |
| IS38/IS38b | `data-autosubmit` + `data-copy-target` in `AdminAnalytics/Index.cshtml` + `Admin/Escalation/Tokens.cshtml`; the include line in `_LayoutScripts.cshtml`; `NoView_HasAnInlineEventHandler` + `NoView_HasAnInlineScriptBlock` |
| IS39 | the control-map table header carries the `Pinned by` and `ADR` columns; the stamp row points at `RevocationStampMiddleware` + `PrivilegedStampMiddleware` |
| IS40 | `EveryRow_HasASeamLink_ThatResolves` + `EveryCodeRow_NamesAPinningTestThatExists` + `EveryOpsRow_LinksARunbookThatExists` |
| IS41 | the four-rule sentence in `AGENTS.md` (or `docs/start/agent.md`); `data-classes.md` pointer to `[DataClass]` + §2.5; the U23 "credential = token row id" correction in `decisions-open-items.md` |
| IS42 | `docs/ops/owner-ops-ledger.md` — the `last verified` table column; linked from `docs/ops/hardening-checklist.md` + `docs/SECURITY.md` |

## Part 2b — the data-class policy table (IS03)

One policy row per **registered** doc type (81) plus the `ContentFree`
export-strip table. This is the table IS16–IS20 annotate **verbatim** onto
each type via `[DataClass]` (IS16), and IS24's agreement gate checks against.
The class labels follow
[`docs/security/data-classes.md`](../security/data-classes.md)
(a–e + operational/platform); the erasure and export values use the enum
members §2.2 names (`DataClassKind` / `Erasure` / `ExportScope`); the
retention-key names use §2.2's `RetentionOptions` record.

**Defaults applied (per the plan's decisions):**

- **D2** — erasure and export are declared on the type itself; a row here
  is the single fact IS16–IS20 copy verbatim.
- **D3** — resident-sent `Message` / `GroupMessage` are **pseudonymized**
  (body erased, sender → `deleted:{id}`), not deleted; the `Conversation`
  / `GroupConversation` rows are **kept** so the other participants' threads
  stay coherent.
- **D6** — retention keys are the §2.2 `RetentionOptions` fields; `0` means
  keep indefinitely; a type that is `Delete`-erased or a platform singleton
  needs no key.
- **Every F1 §2C "decision" row is decided here** (the rows named in
  F1 §2C — [`prod-hardening-f1-gdpr-deletion.md` §2C](prod-hardening-f1-gdpr-deletion.md))
  carry an explicit `Delete` / `Pseudonymize` / `Keep(reason)` value in the
  erasure column below; none is left as "excluded (default)".

### §2.5 — One row per registered doc type (81)

The 81 types are the deduplicated set of `Schema.For<T>` registrations
across every `*DocTypes.cs` under `src/Kumunita.Core` (27 files, IS20's
coverage gate reflects over these — the plan's `81` is confirmed by count).

| Type | Class | Erasure | Export | Retention key | Reason |
|---|---|---|---|---|---|
| `Profile` | a | Pseudonymize | Resident | — | F1 §2B: already handled by `DeleteCoreAsync` (step 8); the IS24 gate checks the agreement, not a second eraser |
| `SessionDoc` | a | Delete | Never | `Retention__SessionDays` | F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry |
| `OutboxEmail` | a | Delete (where `Recipient` is the resident) | Never | `Retention__OutboxBodyDays` (body) + `Retention__OutboxRowDays` (row) | F1 §2C decision (now decided); D6: body nulled 7 days after delivery, row deleted 90 days |
| `SuppressedAddress` | operational | Delete (where the address is the resident's) | Never | — | the resident opted out of notifications; the row is theirs; D2: declared on the type |
| `Conversation` | b | **Keep** (D3 — the resident's seat is tombstoned, the row stays) | Resident | — | D3: the other participant's thread must stay coherent; IS21's `ResidentThreadPseudonymizer` does the tombstone |
| `Message` | b | Pseudonymize (body erased, `SenderId` → `deleted:{id}` when the resident sent) | Resident | — | D3: 1:1 direct messages survive under the tombstone, not deleted (IS21) |
| `GroupConversation` | b | **Keep** (the resident is removed from `Participants`) | Resident | — | D3: group threads stay for the other participants (IS21) |
| `GroupMessage` | b | Pseudonymize (as `Message`, when the resident sent) | Resident | — | D3 (IS21); shipped after F1 — the E2 gap this lane closes |
| `GroupMessagingInvitation` | b | Delete (where the resident is the invitee) | Never | — | an invitation to a thread; deleting the account drops the pending invite (IS21) |
| `Post` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A: resident-authored content |
| `PostReply` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `PostTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0022 user-added translations |
| `ReplyTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0022 |
| `Announcement` | b | Delete (where `AuthorId == resident`; platform announcements `AuthorId == ""` are kept) | Resident | — | F1 §2A |
| `AnnouncementTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A |
| `AnnouncementComment` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A: the resident's comments |
| `Event` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `EventTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0059 |
| `EventRsvp` | b | Delete (where `UserId == resident`) | Resident | — | F1 §2A: the resident's **own** RSVPs (not the event's authorship) |
| `GuardianEventRequest` | c | Delete (where the resident is the child) | Never | — | IS23: a child's event request is the child's data |
| `Page` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A: the resident's blog pages |
| `PageTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; the E6 gap this lane closes (IS31) |
| `Project` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `ProjectGoal` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `ProjectTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0088 |
| `TodoItem` | b | Delete (where `AuthorId == resident`; **not** `AssigneeId`) | Resident | — | F1 §2A |
| `TodoComment` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `TodoTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0088 |
| `KanbanBoard` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A: the board the resident owns |
| `KanbanLane` | b | **Keep** (cascade — no principal field) | InstanceOnly | — | F1 §2A sub-entities: travels with `KanbanBoard`; the board row's Delete is what removes it, not a principal query |
| `BoardItemPlacement` | b | **Keep** (cascade — no principal field) | InstanceOnly | — | F1 §2A sub-entities: under `KanbanBoard` |
| `BoardTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A; ADR 0088 |
| `InventoryItem` | b | Delete (where `AuthorId == resident`) | Resident | — | F1 §2A |
| `InventoryCheckout` | b | Pseudonymize (the resident's identity is erased, the checkout state is kept for stock accuracy) | Never | — | a stock record; the checkout state (who/when) is PII, the item count is operational |
| `Document` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A: the resident's uploads |
| `DocumentFolder` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A |
| `Bookmark` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A |
| `BookmarkFolder` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A; the E6 gap this lane closes (IS31) |
| `PersonalNote` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A; M37 / ADR 0167; the E6 gap this lane closes (IS31) |
| `NoteFolder` | b | Delete (where `OwnerId == resident`) | Resident | — | F1 §2A; the E6 gap this lane closes (IS31) |
| `MediaObject` | e | Delete (where `CreatedById == resident`) | Never | — | F1 §2A: the catalog rows + their bytes (U4's set); the bytes live on a dedicated volume, not in the export |
| `Tag` | b | Delete (where `CreatedBy == resident`) | Resident | — | F1 §2A: the tags the resident created |
| `TagTranslation` | b | Delete (where `AuthorId == resident`, nullable) | Resident | — | F1 §2A: user-added tag translations |
| `Group` | b | **Keep** (ownership transfers per ADR 0142) | InstanceOnly | — | IS03 §2C decision (now decided): the resident is the **owner**; the group outlives the account (the E2 gap this lane closes) |
| `GroupMembership` | b | Delete (where `UserId == resident`) | InstanceOnly | — | F1 §2B: already handled by `DeleteCoreAsync` (step 6) |
| `GroupInvitation` | c | Delete (where `UserId == resident`) | Never | — | F1 §2C decision (now decided): an identity-graph edge received; the row is stale once the account is gone |
| `GroupJoinRequest` | c | Delete (where `UserId == resident`) | Never | — | F1 §2C decision (now decided): an identity-graph edge submitted; the row is stale once the account is gone |
| `DelegationGrant` | c | Delete (where the resident is `OwnerId` or `DelegateId`) | Never | — | F1 §2C decision (now decided): the delegation is meaningless once either side's account is gone (IS22) |
| `GuardianLink` | c | Delete (where the resident is `GuardianId` or `ChildId`) | Never | — | F1 §2B: already handled by `DeleteCoreAsync` (step 8b, dissolved) |
| `GuardianTimeLimitSchedule` | c | Delete (where the resident is the child) | Never | — | IS23: the child's schedule is the child's data |
| `GuestAccess` | c | Delete (where `SubjectId == resident`) | Never | — | F1 §2C decision (now decided): a credential; arguably should go on delete (IS22) |
| `Component` | platform | **Keep** (platform singleton) | InstanceOnly | — | a community component, not resident content; F1 §2C excluded |
| `ComponentMembership` | c | Delete (where `UserId == resident`) | InstanceOnly | — | F1 §2B: already handled by `DeleteCoreAsync` (step 7) |
| `ModeratorAssignment` | c | Delete (where `UserId == resident`) | InstanceOnly | — | F1 §2B: already handled by `DeleteCoreAsync` (step 7) |
| `CommunityMembershipRequest` | c | Delete (where the resident is the requester) | Never | — | IS23: the request is the resident's; deleting the account drops it |
| `CommunityTranslation` | b | **Keep** (cascade — no principal field) | InstanceOnly | — | F1 §2A sub-entities: under `Group`/`Component`; carries only `GroupId`/`ComponentId` + `LanguageCode`, no principal field |
| `GroupTranslation` | b | **Keep** (cascade — no principal field) | InstanceOnly | — | F1 §2A sub-entities: under `Group`; carries only `GroupId` + `LanguageCode`, no principal field (the `Group` row's `Keep(reason)` is what governs it) |
| `KioskPlaylist` | operational | Delete (where `Id == resident`) | Never | — | a per-resident display preference (Id = the resident's subject id, ADR 0164/0285); the resident's own playlist config; IS22's `ResidentAccountRowsEraser` adds the delete |
| `AccessAudit` | c | Pseudonymize (`ActorId` / `EffectivePrincipalId` → tombstone) | Never | — | F1 §2B: already pseudonymized by `DeleteCoreAsync` (step 5); data-classes.md §3.1: the audit log is a disclosure surface, retained per the tiered policy |
| `AuditPurgeSummary` | c | **Keep** (a summary of a past purge, not a resident row) | Never | — | an operational record of the purge itself; no resident principal |
| `Report` | c | Pseudonymize (`ReporterId` → tombstone) | Never | — | F1 §2C decision (now decided): moderation state, not authored content (IS23); M27 C-M27·3 excludes it from the delete set |
| `IdentityToken` | d | Delete (where `SubjectId == resident`) | Never | `Retention__TokenDays` | F1 §2C decision (now decided): a credential (verify/setup tokens) (IS22); D6: 7 days after consumption or expiry |
| `EscalationToken` | d | Pseudonymize (`CreatedBy` → tombstone) | Never | `Retention__TokenDays` | F1 §2C decision (now decided): an operator credential, but the `CreatedBy` is the resident's (IS23); D6: 7 days after revocation or expiry |
| `EscalationOutboundConfig` | d | **Keep** (a platform singleton, not a resident row) | Never | — | the outbound webhook config; the `Token` is an operator secret, not a resident PII field |
| `Notification` | operational | Delete (where `RecipientId == resident`) | Never | `Retention__NotificationUnreadDays` (unread) / `Retention__NotificationReadDays` (read) | F1 §2C decision (now decided): received per-resident state, not authored (IS22); D6: read 90 days, unread 365 days |
| `NotificationPreference` | operational | Delete (where `RecipientId == resident`) | Never | — | F1 §2C decision (now decided): the resident's own preference (IS22) |
| `NotificationSubscription` | operational | Delete (where `RecipientId == resident`) | Never | — | F1 §2C decision (now decided): the resident's own subscription (IS22) |
| `NotificationQuietSchedule` | operational | Delete (where `RecipientId == resident`) | Never | — | F1 §2C decision (now decided): the resident's own schedule (IS22) |
| `ErrorReport` | c | Pseudonymize (`SubjectId` → tombstone; `ContactEmail` → null) | Never | `Retention__ErrorReportDays` | F1 §2C decision (now decided): an operator-facing report the resident filed (IS23); D6: 180 days after resolution |
| `EmailDeadLetter` | operational | Delete (where the recipient is the resident) | Never | `Retention__DeadLetterDays` | D6: 30 days; the recipient is the resident's address (IS34a) |
| `UsageEvent` | operational | Pseudonymize (`ActorId` → tombstone) | Never | — | F1 §2C excluded (per-user storage row / usage history): derived metrics, not content; the resident's identity is erased, the metric stays (IS23) |
| `StorageMetricsSample` | operational | **Keep** (a platform aggregate, not a resident row) | Never | — | a sample of platform storage; no resident principal |
| `CommunityStorageSettings` | operational | **Keep** (a platform setting, not a resident row) | Never | — | the community's storage cap; no resident principal |
| `AdminOnboarding` | platform | **Keep** (a platform singleton, not a resident row) | Never | — | the platform's onboarding state; the `CompletedAt` is an operator timestamp |
| `SiteContent` | platform | **Keep** (a platform singleton, not a resident row) | InstanceOnly | — | F1 §2C excluded: platform singletons, not resident content |
| `SiteContentTranslation` | platform | **Keep** (a platform singleton, not a resident row) | InstanceOnly | — | F1 §2C excluded: the site's hero text per language |
| `SurfaceLabels` | platform | **Keep** (a platform singleton, not a resident row) | InstanceOnly | — | F1 §2C excluded: the admin's surface labels |
| `SurfaceLabelTranslation` | platform | **Keep** (a platform singleton, not a resident row) | InstanceOnly | — | F1 §2C excluded: the labels per language |
| `LanguageCatalog` | platform | **Keep** (a platform catalog, not a resident row) | InstanceOnly | — | the language list; no resident principal |
| `LocaleSettings` | platform | **Keep** (a platform singleton, not a resident row) | InstanceOnly | — | the community's locale defaults; no resident principal |
| `TranslationResource` | platform | **Keep** (a platform resource, not a resident row) | InstanceOnly | — | the UI translation strings; no resident principal |

**Per-class breakdown (81 rows):** a (PII) **3** · b (private content)
**41** · c (audit & moderation) **14** · d (secrets) **3** · e (media) **1** ·
operational **10** · platform **9**.

**Every principal-bearing type has a non-empty erasure value.** The 20
`Keep(reason)` rows fall into four shapes: **13 platform/operational
singletons or aggregates with no resident principal** (`Component`,
`AdminOnboarding`, `SiteContent`, `SiteContentTranslation`, `SurfaceLabels`,
`SurfaceLabelTranslation`, `LanguageCatalog`, `LocaleSettings`,
`TranslationResource`, `EscalationOutboundConfig`, `AuditPurgeSummary`,
`StorageMetricsSample`, `CommunityStorageSettings`), **4 cascade
sub-entities with no principal field** (`KanbanLane`, `BoardItemPlacement`,
`CommunityTranslation`, `GroupTranslation` — they travel with their parent's
row and are governed by the parent's erasure), **1 ownership-transfer
case** (`Group` — the group outlives the account, ADR 0142), and **2 D3
thread-coherence rows** (`Conversation`, `GroupConversation` — the rows
stay so the other participants' threads remain coherent; the resident's
seat is tombstoned, not deleted). The 9 `Pseudonymize` rows
(`Profile`, `Message`, `GroupMessage`, `InventoryCheckout`, `AccessAudit`,
`Report`, `ErrorReport`, `EscalationToken`, `UsageEvent`) name the
principal field erased and the field kept — the IS24 gate checks that the
eraser's `HandledTypes` set agrees.

### §2.6 — The `ContentFree` export-scope table

IS27's filter is driven by `DataClassCatalog`. The 44 M11 types are the
closed `PortabilityDocTypes.Entries` list (`PortabilityDocTypes.cs:91–158`;
`grep -c 'PortabilityDocEntry("'` confirms **44**). Three strip rules apply:

1. **`PrivateContent` → the type is omitted** from the archive entirely.
2. **Restrictive `Audience` → the row is omitted** (the type is present in
   the archive, the restricted rows are absent; rows with a null/empty
   `Audience` are public and stay).
3. **`Profile` PII fields → blanked** (`Email`, `Phone`, `Address` set to
   `null`; the `Profile` row itself stays — it is the resident's own data).

**The 44 M11 types, classified by strip rule:**

| Strip rule | M11 types | Count |
|---|---|---|
| Rule 1 — `PrivateContent` (class b), omitted entirely | `Announcement`, `AnnouncementComment`, `AnnouncementTranslation`, `BoardItemPlacement`, `BoardTranslation`, `CommunityTranslation`, `Conversation`, `Event`, `EventRsvp`, `EventTranslation`, `Group`, `GroupMembership`, `GroupTranslation`, `KanbanBoard`, `KanbanLane`, `Message`, `Page`, `PageTranslation`, `Post`, `PostReply`, `PostTranslation`, `Project`, `ProjectGoal`, `ProjectTranslation`, `ReplyTranslation`, `Tag`, `TagTranslation`, `TodoComment`, `TodoItem`, `TodoTranslation` | **30** |
| Rule 2 — audience-bearing (public rows stay, restricted rows omitted) | a subset of Rule 1's 30: `Post`, `Event`, `Page`, `Project`, `ProjectGoal`, `TodoItem`, `KanbanBoard` (the 7 of the 30 with a non-null `Audience` field; IS27 checks `Audience.Mode != Any` per row) | **(7 of the 30)** |
| Rule 3 — `Profile` PII fields blanked | `Profile` | **1** |
| Always present (structural / public, no strip) | `Component`, `TranslationResource` (platform singletons); `ComponentMembership`, `ModeratorAssignment` (governance); `MediaObject` (class (e) catalog row — the bytes are on the dedicated volume, out of the JSON archive by construction) | **5** |
| **`// TODO ❓`** (owner decision, not decided here) | `DelegationGrant`, `GroupInvitation`, `GroupJoinRequest`, `GuardianLink`, `Report` (class (c) — accountability records); `Notification`, `NotificationPreference`, `NotificationSubscription` (the recipient's inbox state) | **8** |
| | **Total** | **44** ✓ |

**`// TODO ❓` rows (owner decision; IS27 defaults to **skipped** if the
owner has not decided by the unit):**

| Type | Class | Question | IS27 default |
|---|---|---|---|
| `DelegationGrant` | c | the delegation graph (who delegated to whom, what scope) is an accountability record — is it structure or private? | **Skipped** |
| `GroupInvitation` | c | an identity-graph edge received; stale once the account is gone | **Skipped** |
| `GroupJoinRequest` | c | an identity-graph edge submitted; stale once the account is gone | **Skipped** |
| `GuardianLink` | c | the guardian/child relationship; both sides' privacy is implicated | **Skipped** |
| `Report` | c | a moderation record; "the resident was reported" is an accountability fact | **Skipped** |
| `Notification` | operational | the recipient's inbox; received per-resident state | **Skipped** |
| `NotificationPreference` | operational | the resident's own setting; not operator-facing | **Skipped** |
| `NotificationSubscription` | operational | the resident's own setting; not operator-facing | **Skipped** |

**What `Full` restores over `ContentFree`:** all 30 `PrivateContent` types
(including the 7 audience-bearing rows), the `Profile` PII fields
(`Email`/`Phone`/`Address` restored), and the 8 `// TODO ❓` rows (unless
the owner has promoted them to `ContentFree` before IS27 ships). `Full` is
the M11 round-trip set (all 44 types, no fields blanked). The `Full` scope
requires an active `AdminOverride` (IS28's gate, ADR 0299) and is audited
`Via: BreakGlass` (IS28).
