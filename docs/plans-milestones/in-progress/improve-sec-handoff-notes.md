---
id: improve-sec-handoff-notes
title: "IMPROVE-SEC — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
lane: improve-sec
related:
  - docs/plans-milestones/plan-improve-sec.md
  - docs/design/improve-sec-design.md
updated: 2026-11-07
---

> **Abstract:** the running handoff log for the IMPROVE-SEC lane — each unit
> appends a `## IS<n>` section (files, public names, tests, pass count,
> next-unit notes).

## IS01 — Design doc Part 1

- Files created: `docs/design/improve-sec-design.md` (≈ 180 lines; sections
  `## Context` → `## Invariants` → `## Findings register`, in that order) and
  this file.
- Invariants: P-1…P-8 verbatim from the plan, each with its closing finding
  and enforcing unit/gate.
- Findings register: **13 rows** (E1–E13), transcribed as written from the
  plan's Evidence table; *Where* refs are the plan's measurement at
  `main` `1970cfc4` (2026-11-06) — **IS02 re-verifies every one and
  corrects or marks n/a**.
- Nothing ambiguous in the plan rows; no code touched, no build run
  (IS01 is design-only).

## IS02 — Design doc Part 2a

- Appended to `docs/design/improve-sec-design.md`: `## Part 2a` with
  §2.1 (re-verified register), §2.2 (the seven fix-shape signatures), §2.3
  (test names for IS05–IS42), §2.4 (drift-guard anchors per unit).
- **13/13 register rows re-verified** against `development` @ `0936c327`:
  12 confirmed as written, **1 corrected** — E3's `IdentityService.cs:270`
  is `:271` (the plan's `:270` is the argument line; the `{Email}` template
  is `:271`). No row is "n/a (fixed)" — nothing in the tree pre-fixes any
  unit.
- Noticed: E11's "hashed at rest" claim actually lives in `WhatsNew.cs:94–120`
  (0.68.0) + `STATUS.md:62`, not `decisions-open-items.md` as the plan's
  *Where* pin says; recorded in §2.1 so IS09/IS41 land the correction in the
  right file (history stays untouched).
- `improve-check.ps1` ALL GATES PASS. No code touched, no build run
  (design-only unit).
- IS03 builds the §2.5 table on top of §2.2's `[DataClass]`/retention keys
  and §2.3's test-name contract; the signature text in §2.2 is the
  implementing contract for IS05–IS42.

## IS03 — Design doc Part 2b

- Appended to `docs/design/improve-sec-design.md`: `## Part 2b` with
  §2.5 (**81 rows**, one per `Schema.For<T>` registration across the 27
  `*DocTypes.cs` files — the plan's `81` is confirmed by count) and
  §2.6 (the `ContentFree` strip table over the 44 M11 types).
- Per-class breakdown of the 81: a **3** · b **41** · c **14** · d **3**
  · e **1** · operational **10** · platform **9**. Every principal-bearing
  type has a non-empty erasure value; 20 `Keep(reason)` rows are platform
  singletons / cascade sub-entities / `Group` (ADR 0142) / D3 thread
  coherence (`Conversation`, `GroupConversation`).
- F1 §2C "decision" rows all decided per D2/D3/D6: Notifications ×4,
  `DelegationGrant`, `GuestAccess`, `IdentityToken`, `SessionDoc` →
  `Delete`; `Report`, `ErrorReport`, `EscalationToken`, `UsageEvent` →
  `Pseudonymize`; resident-owned `Group` → `Keep` (ADR 0142).
- §2.6 leaves **8 `// TODO ❓` rows** (owner decision, IS27 defaults to
  skip): `DelegationGrant`, `GroupInvitation`, `GroupJoinRequest`,
  `GuardianLink`, `Report` (class c); `Notification`, `NotificationPreference`,
  `NotificationSubscription` (recipient inbox). 27 `PrivateContent` types
  omitted, 7 of those audience-bearing (public rows stay), `Profile` PII
  fields (`Email`/`Phone`/`Address`) blanked, 8 always-present
  (structural/public).
- Next: **IS04** builds the deny-matrix gate harness; **IS16–IS20**
  annotate the 81 types with `[DataClass]` **verbatim from §2.5** (the
  table is the single fact IS16's `DataClassAttribute` and IS24's
  agreement gate check against).
- `improve-check.ps1` ALL GATES PASS. No code touched, no build run
  (design-only unit).

## IS04 — Gate harness + deny-matrix gate

- Files created: `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (repo-root
  walk-up, `ReadSrc`, `AllSrcFiles`, `TestMethodExists`),
  `tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs`,
  `tools/sec/sec-report.ps1`, this handoff append.
- The deny matrix maps all **13** `AccessVia` members (the U08 table's 11 plus
  `Anonymous` (ADR 0154) and `Escalation` (M31·9/ESC) — the enum has grown
  past U08's "all 11" line; the gate is written against the live enum, so the
  next append without a pin fails `EveryAccessVia_HasANamedDenyTest`). Each maps to a deny-side pin
  that exists in `tests/**` (e.g.
  `Escalation → ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow`).
  The two E13 invariant tests (`EvaluateAudience_EmptyAudience_AllMode_Denies`,
  `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded`) are pinned by
  `EmptyAudienceDenies_AndDelegationScope_ArePinned`.
- `dotnet build` 0 errors; the 3 `SecGate` tests pass, and the full
  `Kumunita.Web.Tests` suite (1296) is green (runner form: `dotnet exec
  tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`, the
  `docs/dev/testing-and-ci.md` in-process path — `dotnet test -- -class`
  reports "Zero tests ran / exit 5" on this machine).
- `pwsh tools/sec/sec-report.ps1` prints the six baselines (9/24 · 25/65 · 7 ·
  52 · 8 · 3).
- Noticed: `Directory.EnumerateFiles` does not take `**` glob patterns (it
  takes a filename pattern + `EnumerationOptions.RecurseSubdirectories`) —
  `SecGateSource.AllSrcFiles` is documented to that effect for IS15/IS24/IS38b/
  IS40.
- Next: **IS05** (Sentry breadcrumb floor) is the first Phase A fix;
  IS15/IS24/IS38b/IS40 reuse `SecGateSource`.


## IS05 — Sentry breadcrumb floor (prove, then fix)

- **First verification (the "drift pause" I almost filed) was wrong** — I
  recorded it and corrected it. The members the plan names **do exist on the
  6.12.1 `o =>` receiver**: `SentryAspNetCoreOptions` inherits
  `MinimumBreadcrumbLevel` / `MinimumEventLevel` (`LogLevel`) from
  `SentryLoggingOptions` in `Sentry.Extensions.Logging.dll` — a *different
  assembly* than the `Sentry.dll` I initially reflected, so the whole-assembly
  search missed them. The `Sentry.AspNetCore.xml` docs list only *declared*
  members, not inherited ones. **Lesson:** check the base type of the receiver,
  not just the assembly the receiver is declared in.
- **The E3 premise is reachable at 6.12.1** — `UseSentry` registers the Sentry
  logging integration (`SentryAspNetCoreLoggerProvider`,
  `SentryAspNetCoreStructuredLoggerProvider`) in the runtime DI (verified by
  enumeration of `app.Services`), so Information-level log lines **do** become
  breadcrumbs on captured events when `SENTRY_DSN` is set. The two floor lines
  are the right fix, not a no-op.
- **Files touched:** `Program.cs` (the `UseSentry` block: added
  `o.MinimumBreadcrumbLevel = LogLevel.Warning;` +
  `o.MinimumEventLevel = LogLevel.Error;`, plus the three "vacuous" comment
  corrections at `:443`, `:1372`, `:1449`); `SentryBreadcrumbPiiTests.cs` (new,
  3 tests); this handoff append. `src/**` behavior unchanged beyond the floor.
- **Verified:** the seam compiles against 6.12.1 (the `o =>` receiver
  accepts both floor lines); the harness mirrors `Program.cs` (F10.U3 app-host
  idiom) and observes the captured event through `SetBeforeSend` (the event's
  `Breadcrumbs` is the public surface) + an in-memory `ITransport` (swallows,
  nothing leaves the process).
- **3/3 `SentryBreadcrumbPiiTests` pass** (the Information line with
  `a@example.test` produces **no** breadcrumb and its value is not observable
  on the captured event; the Warning line **does** become a breadcrumb with no
  PII; `SentryPiiScrubber.Scrub` still redacts the header/cookie/query PII).
  **F10.U3** still green (1/1). `dotnet build` 0 errors. `grep -n vacuous
  src/Kumunita.Web/Program.cs` → **0 hits**.
- Next: **IS06** (`LogRedact` helper) — the source-side fix for P-3, the
  durable complement to this sink-side floor.


## IS06 — LogRedact helper

- **Files touched:** `src/Kumunita.Core/Logging/LogRedact.cs` (new, pure
  BCL-only static class next to `LogLine`) and
  `tests/Kumunita.Core.Tests/LogRedactTests.cs` (new, 3 tests). This handoff
  append. The Web project is **untouched** (IS06 is pure Core) — no Web test
  run required.
- **Two members (design §2.2):** `EmailDomain(string?)` → lower-cased domain
  after the `@`, `"(none)"` for null/empty/no-`@`/empty-local/empty-domain,
  local part never returned. `Hash8(string?)` → first 8 lowercase-hex chars of
  SHA-256 (UTF-8 bytes), `"(none)"` for null/empty (the pinned sentinel, made
  total). Nothing retained; both return a total `string`.
- **3/3 `LogRedactTests` pass** (`EmailDomain_ReturnsDomainOnly`,
  `EmailDomain_NullOrMalformed_IsNone`, `Hash8_IsStableAndShort`). `dotnet
  build` 0 errors. §2.4 anchors present; helper carries no PII literal and no
  `System.Text.Json` (the doctest examples were reworded so the DoD grep is
  clean).
- **One correction during the unit:** the `Hash8` hex-check assertion was
  first written with a malformed boolean expression — replaced with a clean
  per-char `('0'-'9' | 'a'-'f')` guard before it could mis-pin.
- Next: **IS07** — the consumer side, redacting the 4 `IdentityService` /
  `SmtpSender` templates to `{UserId}` + `{EmailDomain}` / `{RecipientHash}`.

## IS07 — Redact the identity + SMTP log templates

- **Files touched:** `src/Kumunita.Core/Identity/IdentityService.cs` (3 sites)
  and `src/Kumunita.Core/Identity/SmtpSender.cs` (1 site), template + argument
  only, zero behaviour change. `using Kumunita.Core.Logging;` added to both
  (missing before). No test added — §2.3 records IS07 as "no new test"
  (P-3 pinned by IS05 sink floor + IS08 gate).
- **The 4 sites:** `IdentityService.cs:161` (disposable-email rejection —
  `{EmailDomain}`, `LogRedact.EmailDomain(email)`; `{Domain}` already carried
  the domain), `:219` (registered unverified resident), `:273` (re-issued
  verification) — all `LogRedact.EmailDomain(email)`. `SmtpSender.cs:296`
  (delivered email — `{RecipientHash}`, `LogRedact.Hash8(email.Recipient)`).
- **Verification:** `dotnet build` 0 errors. `grep -rn "{Email}"
  src/Kumunita.Core/Identity/` → **0 hits**; raw `{Recipient}` → **0 hits**;
  `LogRedact.*` present at all 4 sites (§2.4 anchor). **3/3
  `IdentityServiceSignupPolicyTests` pass** (in-process runner, ~8 s).
- Next: **IS08** — the seeder redaction (`FirstBootSeeder`,
  `SampleDataSeeder`) + the log-PII gate over `src/**/*.cs`.

## IS08 — Redact the seeders + the log-PII gate (M4: 7 → 0)

- **Files touched:** `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`
  (2 sites) and `SampleDataSeeder.cs` (1 site) — template label + address
  argument only, zero behaviour change; `using Kumunita.Core.Logging;` added to
  both (missing before). Plus `tests/Kumunita.Web.Tests/SecGate/
  LogTemplatePiiGateTests.cs` (new, the gate) and this append.
- **The 3 sites:** `FirstBootSeeder.cs:231` + `:297` (`{Email}` →
  `{EmailDomain}`, arg → `LogRedact.EmailDomain(email)`; the `:297` site's
  `{UserId}` is an id — left), `SampleDataSeeder.cs:2580` (`{Email}` →
  `{EmailDomain}`; `{Name}` left). All carry the domain, never the address.
- **The gate:** `NoLogTemplate_CarriesAPiiPlaceholder` walks `src/**/*.cs`
  only (per the spec — `tests/` legitimately carries the sink probes, e.g.
  `SentryBreadcrumbPiiTests`, and is out of scope). For each structured-log
  call it walks to the matching close paren **respecting string literals and
  line comments** (the line-wrap immunity: a template split across 2–3 lines
  stays one span), then flags any placeholder that is **exactly a PII word**
  (`Email`/`Ip`/`Phone`/`Address`/`Recipient`/`Body`, case-insensitive).
  Id-shaped placeholders are excluded by construction — `{EmailDomain}`,
  `{RecipientId}`, `{Idp}`, `{IdempotencyKey}`, `{UserId}`, `{MaxBodyChars}`
  never equal a bare word. Allowlist is **empty** (a site that can't be
  redacted is a recorded pause, not a silent exception).
- **Verification:** `dotnet build` 0 errors. `grep "{Email}" Bootstrap/` →
  **0**; `LogRedact.EmailDomain` in `Bootstrap/` → **3** (§2.4 anchor). Gate
  **1/1 pass**; `AccessViaDenyMatrixTests` still **3/3** (harness intact).
  **Bite-checked:** reverting `FirstBootSeeder.cs:231` to `'{Email}', email`
  made the gate FAIL naming exactly `FirstBootSeeder.cs:231  {Email}` with a
  redact-at-the-source message, then restored → green.
- **One fix during the unit:** the call-span scanner first pre-seeded its paren
  depth *and* counted the opening paren, so it swept to the next unrelated
  string and swept up `{email}` from email-body *content* strings (not log
  templates) — re-armed at the real `(` (depth 1 from `openParen+1`) and scoped
  the scan to `src/`. The gate is now red exactly when a log template carries a
  bare PII word.
- Next: **IS09** — the trust-boundary + log-hygiene docs (B7 Sentry row, the B4
  snippet note, and closing the PII half of the log-hygiene open item as
  "decided and closed by IS08"; log-forging L8c stays open).

## IS09 — Trust-boundary + log-hygiene docs (B7, the B4 class-(b) note, the L8c split)

- **Docs only — no code, no build, no test** (§2.3 records IS09 as "no test —
  docs only; the B7/B4 rows are doc-truth pinned by IS41").
- **Files touched:** `docs/security/trust-boundaries.md` (abstract 6→7, the B4
  class-(b) snippet note, the new B7 Sentry row, `updated:` → 2026-11-07) and
  `docs/security/decisions-open-items.md` (the log-hygiene row split into its
  closed PII half and its open log-forging half, `updated:` → 2026-11-07). This
  append. **No `src/**` change.**
- **B7 row (Sentry):** DSN-gated (`SENTRY_DSN` unset → the `UseSentry` seam is
  not executed, byte-identical to before), `MinimumBreadcrumbLevel = Warning`
  (the IS05 floor), `SendDefaultPii = false`, the request scrubber
  redacts headers/cookies/query, and log templates carry no PII (the IS08
  gate). All facts copied from `Program.cs` ≈ :449–:481 + `:1383`; third-party
  cell bolded like B5.
- **B4 note:** kept the `[class (a)](data-classes.md)` link; added that
  notification/DM/mention emails also carry a ≤ 200-char UGC/DM snippet
  (`UgcSnippets.Truncate`, `src/Kumunita.Core/Notifications/UgcSnippets.cs:23`),
  so **class (b)** crosses B4 too.
- **L8c split:** one ambiguous "still open" row → two. PII half **closed** by
  IS08 (links `LogTemplatePiiGateTests` + the IS05 floor); log-forging half
  (SEC L8c) **still open**, no owner decision. L8c was **not** closed.
- **Not this unit (IS39's / IS41's / IS42's):** the B7 **control-map row** lands
  in **IS39** (the traceability rewrite), the `data-classes.md` / token-at-rest
  / AGENTS.md-rules doc-truth is **IS41**, and the owner-ops ledger (e.g. the
  `SENTRY_DSN` "now safe" note, D6 retention) is **IS42**.
- **Verification:** `grep -n "^| B7" docs/security/trust-boundaries.md` → 1
  hit (DSN-gated + Warning floor + no-PII-templates); B4 row carries class (b)
  **and** keeps the class (a) link; abstract reads **seven (B1–B7)**; both
  `updated:` = 2026-11-07; the open-items table reads as PII-closed /
  log-forging-open. `git status` → only the 2 docs + this append.

## IS10 — Fix the `TodoDetail` board-title leak (E4, the one confirmed S leak)
- **Files (3, the 3rd authorized by the owner mid-unit):**
  `src/Kumunita.Web/Controllers/TodosController.cs` (the `TodoDetail` F2
  block **only** — no constructor change, no `IAuthorizationService` added, no
  other action); `tests/Kumunita.Web.Tests/TodoDetailBoardVisibilityTests.cs`
  (new, 3 tests); `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (one
  harness stub, see "noticed").
- **The F2-block change:** the raw `session.Query<KanbanBoard>()` title lookup
  is gone — it is now `boards.ListBoardsForTodoAsync(id, actorId,
  HttpContext.RequestAborted)` (the `CanSeeAsync`-filtered seam, IS10 E4),
  with `placements` and `lanes` kept only for boards in the visible set
  (`visibleBoardIds`); `boardTitle` is rebuilt from the visible boards. The
  old local `IReadOnlyList<KanbanBoard> boards` was dropped, so `boards`
  resolves to the injected `IBoardService` (no shadowing).
- **Tests (3/3 green):** `TodoDetail_RestrictedBoardTitle_NotShown`,
  `TodoDetail_VisibleBoardTitle_Shown`,
  `TodoDetail_RestrictedBoardPlacement_NoLaneChip`. Real scratch-Postgres
  store for the placements/lanes reads (Marten 9 `ToListAsync()` is not
  NSubstitutable — the `ProjectsControllerTests` precedent); the seam is
  NSubstituted to drive the two visibility outcomes.
- **Notice (rule 9 — the pre-existing test):** `ProjectsControllerTests.Todo_Detail_SubtasksRendered`
  regressed (empty `vm.Placements`) because its `IBoardService` substitute
  never stubbed `ListBoardsForTodoAsync`, so the new visible-set filter dropped
  the placement. That board is **public** (no `Audience`), so this is a
  harness-stub gap, **not** the leak the test pinned. Added one stub
  (`projects_B.ListBoardsForTodoAsync(…) → [board-detail]`); the test's own
  assertions are unchanged. All 55 (3 new + 52 regression across
  `ProjectsControllerTests` + `M14InterlockTests`) green; `dotnet build` 0
  errors.
- **Verification anchors:** `grep "session.Query<KanbanBoard>()"
  TodosController.cs` → **0**; `grep "ListBoardsForTodoAsync"
  TodosController.cs` → the single call in `TodoDetail` (plus its doc-comment
  ref); `git status` → controller + new test + the `ProjectsControllerTests`
  stub + this append (the two pre-existing untracked `*-prompt.md` files are
  not part of this unit).
- **Next unit (IS11):** triage batch 1 (Posts / Documents / Moderation, 13
  methods) — `// SEC-READ:` markers or a pause on a leak; the 52 → 0 gate is
  IS15's.
- **Noticed:** the `Audience` type uses `Mode` + `List<AudienceGrant>`
  (not a `Kind`/`Value` pair) — relevant for any test that seeds an
  audience-restricted board (the IS10 fixture uses
  `Grants = [new AudienceGrant(GrantKind.Group, "grp-…")]`).

## IS11 — Triage batch 1 (Posts / Documents / Moderation, SEC-READ markers)

- **Deliverables (4 marker files + this append):** `src/Kumunita.Core/Posts/PostService.cs`, `src/Kumunita.Core/Posts/PostService.Media.cs`, `src/Kumunita.Core/Documents/DocumentService.cs`, `src/Kumunita.Core/Moderation/ModerationService.cs`. Comment-only edits (no method body, signature, `using`, or test changed). `dotnet build` 0 errors; the affected Core (`PostServiceTests`, `ModerationServiceTests`, `PostReplyCountTests`) and Web (`DocumentControllerTests`, `ModerationControllerTests`, `PostReadDecisionTests`) classes are all green (76 + 44, no failures).
- **Count of markers added: 22** (11 PostService, 4 PostService.Media, 4 DocumentService, 3 ModerationService). Every marker line ends in a reason (IS15's `SecReadMarker_NamesAReason` enforces).
- **`method → verdict → evidence line` table** (one row per marked method):

  | Method (file) | Taxonomy line used | Evidence (file:line, makes it safe) |
  |---|---|---|
  | `PostService.CreatePostAsync` (PostService.cs:446) | `owner-only` — the read is the actor's own effective community set | PostService.cs:481–497: `GetEffectiveCommunityIdsAsync(actorId)` scopes to the actor's own memberships; the write is the actor's own post |
  | `PostService.UpdatePostAsync` (PostService.cs:673) | `owner-only` — author-only edit lane (ADR 0014) | PostService.cs:711–713: `post.AuthorId == actorId` else `UnauthorizedAccessException` |
  | `PostService.CreateReplyAsync` (PostService.cs:808) | `no disclosure` — the parent-post load is to route the reply-mention notification to the parent author | PostService.cs:839–852: `parentPost.AuthorId` is used only to choose the notification recipient, not a read surface |
  | `PostService.UpdateReplyAsync` (PostService.cs:976) | `owner-only` — author-only reply-edit lane (ADR 0016) | PostService.cs:990–992: `reply.AuthorId == actorId` else `UnauthorizedAccessException` |
  | `PostService.DeletePostAsync` (PostService.cs:1043) | `owner-only` — author-only soft-delete lane (ADR 0024) | PostService.cs:1055–1057: `post.AuthorId == actorId` else `UnauthorizedAccessException` |
  | `PostService.DeleteReplyAsync` (PostService.cs:1085) | `owner-only` — author-only reply-soft-delete lane (ADR 0024) | PostService.cs:1096–1097: `reply.AuthorId == actorId` else `UnauthorizedAccessException` |
  | `PostService.PublishPostAsync` (PostService.cs:1140) | `owner-only` — author-only draft publish lane (ADR 0037) | PostService.cs:1152–1154: `post.AuthorId == actorId` else `UnauthorizedAccessException` |
  | `PostService.ListMyDraftsAsync` (PostService.cs:1193) | `owner-only` — the read is scoped to the actor's own drafts | PostService.cs:1204: `p.IsDraft && p.AuthorId == actorId && p.DeletedAt == null` |
  | `PostService.GetPostTranslationsAsync` (PostService.cs:1352) | `caller-gated by PostsController.Detail / GroupsController.GroupPostDetail / HomeController.Index` (C-M3·1) | PostService.cs:1345–1351: "the caller has already made the Read decision (the Web reads this only after `GetPostAsync` returned the post)" |
  | `PostService.CountRepliesAsync` (PostService.cs:1378) | `no disclosure` — returns only post-id → reply-count | PostService.cs:1397–1405: the projection is `Select(r => r.PostId)` + a count dictionary; no audience-bearing field is exposed |
  | `PostService.GetReplyTranslationsAsync` (PostService.cs:1409) | `caller-gated by PostsController.Detail / GroupsController.GroupPostDetail` (C-M3·1) | PostService.cs:1401–1407: "the reply read inherits the parent post's single Read decision, already made by the caller" |
  | `PostService.FindPostByImageIdAsync` (PostService.Media.cs:32) | `caller-gated by ContentImageController.Image` (`PostReadDecision.ResolveAsync`) | ContentImageController.cs:80–93: the finder result is fed into `PostReadDecision.ResolveAsync(post, actorId, authz)` and Deny → 404 |
  | `PostService.FindReplyByImageIdAsync` (PostService.Media.cs:53) | `caller-gated by ContentImageController.Image` (against the parent post, C-M3·1) | ContentImageController.cs:99–118: the reply's parent is loaded and `PostReadDecision.ResolveAsync(parent, …)` decides |
  | `PostService.FindPostByAttachmentIdAsync` (PostService.Media.cs:77) | `caller-gated by AttachmentController.Download` (`PostReadDecision.ResolveAsync`) | AttachmentController.cs:180 (same seam as the image route, the C-ATT·7 pin) |
  | `PostService.FindReplyByAttachmentIdAsync` (PostService.Media.cs:98) | `caller-gated by AttachmentController.Download` (against the parent post, C-M3·1) | AttachmentController.cs:197 (the reply → parent → decision shape, the C-ATT·7 pin) |
  | `DocumentService.UpdateAsync` (DocumentService.cs:257) | `owner-only` — the load is followed by the owner-only gate (ADR 0125 D1) | DocumentService.cs:265–267: `doc.OwnerId == actorId` else the lane's 403/404 shape |
  | `DocumentService.DeleteAsync` (DocumentService.cs:350) | `owner-only` — the load is followed by the owner-only gate (ADR 0122/0125) | DocumentService.cs:356 (the load) + the ADR 0122 D7 posture (missing + denied + deleted all collapse to 404) |
  | `DocumentService.ListDeletedAsync` (DocumentService.cs:392) | `owner-only` — non-admin callers are scoped to their own rows; GlobalAdmin sees the restore-surface list of titles | DocumentService.cs:399–401: `d.DeletedAt != null` + `d.OwnerId == actorId` for non-admins |
  | `DocumentService.RestoreAsync` (DocumentService.cs:418) | `owner-only` — the load is followed by the owner/global-admin restore gate (ADR 0241) | DocumentService.cs:428–430: `!actorIsGlobalAdmin && doc.OwnerId != actorId` → `UnauthorizedAccessException` |
  | `ModerationService.GetQueueAsync` (ModerationService.cs:281) | `report-gated (ADR 0003)` — the queue self-filters | ModerationService.cs:305–335: the standing-moderator branch requires `ModeratorAssignment` row + `Component.ModeratorAccess == true`; a plain resident sees none |
  | `ModerationService.GetDetailAsync` (ModerationService.cs:427) | `report-gated (ADR 0003)` — ModerationController.Resolve scopes the read | ModerationController.cs:266–268: `!isGlobalAdmin && !(isStanding && IsInActorScopeAsync(…))` → `NotFound()` |
  | `ModerationService.GetStandingModeratorsAsync` (ModerationService.cs:555) | `report-gated (ADR 0003)` — surfaced only from GetDetailAsync (report-gated) | ModerationService.cs:525: the single non-test caller is GetDetailAsync (the report-gated seam) |

- **Methods left as *already decided* (no marker):**
  - `PostService.ListFeedAsync` (PostService.cs:217) — `CanSeeAsync` in-method over the feed rows (PostService.cs:260).
  - `PostService.ListAllFeedAsync` (PostService.cs:297) — `CanSeeAsync` in-method (PostService.cs:339).
  - `PostService.GetPostAsync` (PostService.cs:362) — `CanAsync` in-method (PostService.cs:395).
  - `PostService.HidePostAsync` (PostService.cs:1221) — `CanAsync(Moderate)` in-method (PostService.cs:1233).
  - `PostService.RemovePostAsync` (PostService.cs:1258) — `CanAsync(Moderate)` in-method (PostService.cs:1268).
  - `DocumentService.ListAsync` (DocumentService.cs:74) — `CanSeeAsync` in-method (DocumentService.cs:128).
  - `DocumentService.GetAsync` (DocumentService.cs:150) — `CanAsync(Read)` in-method (DocumentService.cs:165).
  - `ModerationService.IsStandingModeratorOnComponentAsync` (ModerationService.cs:263) — a pure probe, returns `bool`; no audience-bearing field is exposed.
  - `ModerationService.IsInActorScopeAsync` (ModerationService.cs:580) — a pure probe, returns `bool`; the `Component.ModeratorAccess` bool is a flag, not a content field.
  - `DocumentService.UploadAsync` (DocumentService.cs:186) — a write lane (no audience-bearing read; the caller's standing is decided at the Web boundary, D5).
- **No leaks found; no pause.** Every method either self-decides in-method (`CanAsync`/`CanSeeAsync`/`CanAsync(Moderate)`), is owner-scoped (`AuthorId == actorId` / `OwnerId == actorId` / `AuthorId` on the drafts filter), returns no audience-bearing field (counts, ids, shapes with the content stripped), or is a read seam whose caller already ran the decision (the Web controllers' `PostReadDecision.ResolveAsync` / `ModerationController.Resolve` / `PostsController.Detail`).
- **Noticed:** `ModerationService.GetStandingModeratorsAsync` has only one non-test caller (`ModerationService.GetDetailAsync` itself, ModerationService.cs:525). The taxonomy line records that the seam is only surfaced from the report-gated seam, so the report-gate applies.
- **Noticed:** `DocumentService.ListDeletedAsync`'s GlobalAdmin branch lists every soft-deleted document's title. The doc-comment (DocumentService.cs:387–391) already pins this as "a list of titles and names for restoring, not a read of the content: no download, no audience decision". The marker records the same posture.
- **Noticed:** `PostService.CreatePostAsync` reads the actor's own effective community set via `GetEffectiveCommunityIdsAsync`. That is a UserInfo read, not an audience-bearing read (the actor's own rows). The posting-right gate is in-method (the ADR 0003 posting-right rule). The marker records the owner-only posture.
- **Next unit (IS12):** triage batch 2 (Events / Projects, 12 methods) — the same `// SEC-READ:` marker or a pause-on-leak; the 52 → 0 gate is still IS15's.

## IS12 — Triage batch 2 (Events / Projects, SEC-READ markers)

- **Deliverables (4 marker files + this append):** `src/Kumunita.Core/Events/EventService.cs`, `src/Kumunita.Core/Events/EventService.Group.cs`, `src/Kumunita.Core/Events/EventRsvpGuardianService.cs`, `src/Kumunita.Core/Projects/TodoService.cs`. Comment-only edits (no method body, signature, `using`, or test changed). `dotnet build` 0 errors; `git diff --stat` on the four marker files is **20 insertions, 0 deletions** (the IS11 guard-clause trap was avoided — one slip on `DeleteAsync` was caught by `diff --stat` and corrected before it landed). The affected Core classes are all green in-process (xunit.v3 `-class` form, the `docs/dev/testing-and-ci.md` path): `EventServiceTests` + `GroupEventServiceTests` + `GuardianEventRsvpGateTests` + `GuardianApprovalLaneTests` = **125 pass / 0 fail**; `TodoCommentServiceSeamTests` + `TodoStandingPolicyTests` + `M7PaginationSeamTests` + `Projects.ProjectsTranslationLaneTests` + `EventServiceSkipUndeleteTests` + `EventServiceCreateRecurrenceTests` = **34 pass / 0 fail**.
- **Count of markers added: 20** (10 EventService, 1 EventService.Group, 6 EventRsvpGuardianService, 3 TodoService). Every marker line ends in a reason (IS15's `SecReadMarker_NamesAReason` enforces). The plan said "the ~12 such reads"; the batch-2 file set actually carried 20 decision-free reads + 10 already-decided methods — the count was a floor, not a pin (the gate IS15 retires the count to 0, it does not pin a number).
- **`method → verdict → evidence line` table** (one row per marked method, in file order):

  | Method (file) | Taxonomy line used | Evidence (file:line, makes it safe) |
  |---|---|---|
  | `EventService.GetRsvpsAsync` (EventService.cs:393) | `caller-gated by EventController.Detail` (author-scoped) | EventController.cs:968-971: the `isAuthor` branch is the only non-test caller that reads the RSVP list (the KioskItemCatalog.cs:291 caller reads it *after* `events.GetAsync` already ran the Read decision — `ResolveEventAsync`'s fail-closed shape) |
  | `EventService.ListMineAsync` (EventService.cs:477) | `owner-only` — the candidate set is scoped to the actor's own rows | EventService.cs:489/495: `r.UserId == actorId` (own RSVPs) ∪ `e.AuthorId == actorId` (own authored); the doc pins "an RSVP row can only exist on an event the actor may already read" |
  | `EventService.ListDraftsAsync` (EventService.cs:514) | `owner-only` — the query is scoped to the actor's own drafts | EventService.cs:524: `e.IsDraft && e.AuthorId == actorId && !e.IsDeleted` |
  | `EventService.CreateAsync` (EventService.cs:654) | `no disclosure` — a write lane with no audience-bearing read | EventService.cs:693/698: the only store read is the `LocaleSettings` singleton (the authored-in code floor); standing is in-method (`CheckCreateStanding` — any signed-in resident) |
  | `EventService.UpdateAsync` (EventService.cs:787) | `owner-only` — the load is followed by the in-method standing gate | EventService.cs:810: `CheckEditStanding(actorId, actorRoles, existing)` (author ∪ GlobalAdmin), then the write |
  | `EventService.PublishAsync` (EventService.cs:981) | `owner-only` — the load is followed by the in-method author gate | EventService.cs:999-1000: `AuthorId == actorId` else `UnauthorizedAccessException` (ADR 0037, GlobalAdmin explicitly denied) |
  | `EventService.DeleteAsync` (EventService.cs:1036) | `owner-only` — the load is followed by the in-method standing gate | EventService.cs:1052: `CheckEditStanding(actorId, actorRoles, existing)` (author ∪ GlobalAdmin), then the write |
  | `EventService.SkipOccurrenceAsync` (EventService.cs:1090) | `owner-only` — the load is followed by the in-method author gate | EventService.cs:1113-1115: `AuthorId == actorId` else the no-leak 404 (ADR 0037 shape, C-M18·4) |
  | `EventService.UndeleteOccurrenceAsync` (EventService.cs:1137) | `owner-only` — the load is followed by the in-method author gate | EventService.cs:1159: `AuthorId == actorId` else the no-leak 404 (the SkipOccurrenceAsync shape) |
  | `EventService.GetEventTranslationsAsync` (EventService.cs:1191) | `caller-gated by EventController.Detail / the feed reads` (C-M3·1) | EventController.cs:995 / HomeController.cs:641: the read is reached only after `GetAsync`'s `CanAsync(Read)` (or the feed's `CanSeeAsync`) already ran — the doc-comment pins "the caller has already made the Read decision" |
  | `EventService.UpdateGroupEventAsync` (EventService.Group.cs:256) | `owner-only` — the load is followed by the in-method author gate | EventService.Group.cs:275-276: `AuthorId == actorId` else `UnauthorizedAccessException` (GE·4, GlobalAdmin explicitly denied) |
  | `EventRsvpGuardianService.RsvpAsync` (EventRsvpGuardianService.cs:89) | `no disclosure` — a write lane with no audience-bearing read | EventRsvpGuardianService.cs:60-74 doc: standing is in-method (any signed-in resident; the `M4_RsvpWritesNoAccessAuditRow` pin — the lane deliberately does not route standing through `IAuthorizationService`); the only store reads are the event's presence + the actor's own `GuardianLink`/`Profile` rows |
  | `EventRsvpGuardianService.ApproveEventRsvpAsync` (EventRsvpGuardianService.cs:327) | `owner-only` — the standing gate is in-method before the reads | EventRsvpGuardianService.cs:340: `GuardianEventLinkAsync(session, guardianId, childId, ct)` (the active-link check) runs before the event/request loads; the lane is the guardian's own scope (a write, not a content read — ADR 0028 G·1) |
  | `EventRsvpGuardianService.DenyEventRsvpAsync` (EventRsvpGuardianService.cs:417) | `owner-only` — the standing gate is in-method before the reads | EventRsvpGuardianService.cs:431: `GuardianEventLinkAsync` (the active-link check) runs before the event/request loads; the lane is the guardian's own scope (ADR 0028 G·1) |
  | `EventRsvpGuardianService.VetoEventRsvpAsync` (EventRsvpGuardianService.cs:482) | `owner-only` — the standing gate is in-method before the reads | EventRsvpGuardianService.cs:497: `GuardianEventLinkAsync` (the active-link check) runs before the event/RSVP loads; the lane is the guardian's own scope (ADR 0028 G·1) |
  | `EventRsvpGuardianService.GetPendingEventRsvpRequestsAsync` (EventRsvpGuardianService.cs:534) | `caller-gated by GuardianController.Detail` (active-link standing) | GuardianController.cs:129: the `Detail` action runs `ActiveLinkAsync(subject, childId)` before it reads this seam (GuardianController.cs:240); a non-guardian learns nothing |
  | `EventRsvpGuardianService.GetChildRsvpsAsync` (EventRsvpGuardianService.cs:557) | `caller-gated by GuardianController.Detail` (active-link standing) | GuardianController.cs:129: the `Detail` action runs `ActiveLinkAsync(subject, childId)` before it reads this seam (GuardianController.cs:247); a non-guardian learns nothing |
  | `TodoService.CreateTodoAsync` (TodoService.cs:460) | `no disclosure` — a write lane whose only audience-type read is the blocker's presence check | TodoService.cs:509: `session.LoadAsync<TodoItem>(todo.BlockedByTodoId)` is the existence / not-soft-deleted guard (TodoService.cs:510 — no field is read or exposed); standing is in-method (the create lane's `CheckTodoStanding` shape) |
  | `TodoService.UpdateTodoAsync` (TodoService.cs:544) | `owner-only` — the load is followed by the in-method standing gate | TodoService.cs:566: `CheckTodoStanding(actorId, actorRoles, todo)` (creator ∪ assignee ∪ GlobalAdmin, C-M5·6), then the write |
  | `TodoService.GetTodoTranslationsAsync` (TodoService.cs:812) | `caller-gated by TodosController.Detail / the feed reads` (C-M3·1) | TodosController.cs:374 / ProjectsBaseController.cs:253: the read is reached only after `GetTodoAsync`'s `CanAsync(Read)` (or the feed's `CanSeeAsync`) already ran — the C-M3·1 shape (a translation inherits the parent's decision) |

- **Methods left as *already decided* (no marker — they run `CanAsync` / `CanSeeAsync` / `CanSeeGroupAsync` / `CanSeeGroupFeedAsync` in-method over the rows they read):**
  | `EventService.ListUpcomingAsync` (EventService.cs:193) — `CanSeeAsync(Read)` in-method (EventService.cs:224). |
  | `EventService.ListPastAsync` (EventService.cs:246) — `CanSeeAsync(Read)` in-method (EventService.cs:274). |
  | `EventService.ListInRangeAsync` (EventService.cs:298) — `CanSeeAsync(Read)` in-method (EventService.cs:321). |
  | `EventService.GetAsync` (EventService.cs:345) — `CanAsync(Read)` in-method (EventService.cs:373); the draft gate is the author-only ordinal (EventService.cs:359). |
  | `EventService.GetMyRsvpAsync` (EventService.cs:420) — `CanAsync(Read)` in-method (EventService.cs:447); the draft branch is the author-only ordinal. |
  - `EventService.ListMineAsync` / `ListDraftsAsync` — owner-scoped (see the marked rows above).
  | `EventService.Group.ListGroupEventsAsync` (EventService.Group.cs:48) — `CanSeeGroupFeedAsync` in-method (EventService.Group.cs:75). |
  - `EventService.Group.GetGroupEventAsync` (EventService.Group.cs:104) — `CanSeeGroupAsync` in-method (EventService.Group.cs:138); the draft branch is the author-only ordinal.
  - `EventService.Group.CreateGroupEventAsync` (EventService.Group.cs:171) — `CanSeeGroupAsync` in-method (EventService.Group.cs:186, in the caller's in-flight session; GE·3: the lane's membership is the sole decision). **Not marked** — it runs an `IAuthorizationService` decision in-method, which is the plan's "decided in-method" posture.
  - `TodoService.ListTodosAsync` (TodoService.cs:242) — `CanSeeAsync(Read)` in-method (TodoService.cs:284).
  - `TodoService.ListTodosForEventAsync` (TodoService.cs:291) — `CanSeeAsync(Read)` in-method (TodoService.cs:322).
  - `TodoService.GetTodoAsync` (TodoService.cs:329) — `CanAsync(Read)` in-method (TodoService.cs:345) + the subtask/blocker rows each run their own `CanAsync(Read)` (TodoService.cs:364/388 — the C-M5·7 / C-TBD·4 shape).
  - `TodoService.ListPickerTodosAsync` (TodoService.cs:427) — `CanSeeAsync(Read)` in-method (TodoService.cs:453).
  - `TodoService.CreateTodoCommentAsync` / `DeleteTodoCommentAsync` (TodoService.cs:416/422) — thin forwarders to `TodoCommentService`, whose lanes run `CanAsync(Read)` (create, TodoCommentService.cs:66) and the author-only ordinal (delete, TodoCommentService.cs:165-167) in-method. **Not marked** — the decision is in the delegate's method body, not this seam's; the forwarder is a one-line `=> _comments.?(…)` (the IS11 `_groupPosts` pattern).
- **No leaks found; no pause.** Every method either self-decides in-method (`CanAsync` / `CanSeeAsync` / `CanSeeGroupAsync` / `CanSeeGroupFeedAsync` / `CheckEditStanding` / `CheckTodoStanding` / `CheckCreateStanding` / the author-only ordinal), is owner-scoped (`AuthorId == actorId` / `UserId == actorId` on the candidate set), returns no audience-bearing field (the write lanes read only the presence / their own rows), or is a read seam whose caller already ran the decision (`EventController.Detail`'s `isAuthor` branch, `GuardianController.Detail`'s `ActiveLinkAsync`, the feed/detail reads that run `CanSeeAsync`/`CanAsync` before `GetEventTranslationsAsync` / `GetTodoTranslationsAsync`).
- **Noticed:** `EventService.GetRsvpsAsync` has a second non-test caller — `KioskItemCatalog.ResolveEventAsync` (KioskItemCatalog.cs:291) — which reads the RSVP list *after* `events.GetAsync(id, actorId)` already ran the Read decision (the kiosk's fail-closed shape: a denied event is `KioskResolvedItem.Hidden` before the RSVP read is reached). The marker names the `EventController.Detail` caller as the primary seam; the kiosk caller is safe by the same "the parent event's decision already ran" posture.
- **Noticed:** `EventRsvpGuardianService`'s two read seams (`GetPendingEventRsvpRequestsAsync` / `GetChildRsvpsAsync`) are documented in their own `<summary>` as "a read, not a decision — the `EventService.GetMyRsvpAsync` posture: no `IAuthorizationService` call, no audit row. The standing gate (an active `GuardianLink` over the child) is the Web layer's (the Detail GET's `ActiveLinkAsync` check)". The marker records exactly that posture (caller-gated by `GuardianController.Detail`).
- **Noticed:** `TodoService.CreateTodoCommentAsync` / `DeleteTodoCommentAsync` are the IS11 `_groupPosts` forwarder pattern (one-line `=> _comments.?(…)` delegation). The decision is in the delegate's method body (`TodoCommentService.CreateTodoCommentAsync` runs `CanAsync(Read)` at TodoCommentService.cs:66; `DeleteTodoCommentAsync` runs the author-only ordinal at TodoCommentService.cs:160). **Not marked** — per the plan's "forwarder / seam" posture, the decision is in the delegate's method body, not this seam's.
- **Noticed:** `EventService.Group.CreateGroupEventAsync` marker was drafted, then withdrawn — it runs `CanSeeGroupAsync` (an `IAuthorizationService` decision) in-method (EventService.Group.cs:186), which is the plan's "decided in-method → leave it" posture. The initial draft had mislabelled it "caller-gated"; the in-method decision is the correct posture.
- **Noticed (guard-clause trap, avoided):** the `DeleteAsync` marker was first drafted with `ArgumentNullException.ThrowIfNull(request)` (the `UpdateAsync` shape) instead of the correct `ArgumentNullException.ThrowIfNull(actorRoles)` (the `DeleteAsync` shape). The `diff --stat` check (0 deletions required) would not have caught it (the old/new strings were the same length), but the build would have failed (no `request` in scope). The marker was corrected before the build; the final diff is 20 insertions, 0 deletions.
- **Next unit (IS13):** triage batch 3 (Pages / Targets, 16 methods) — the same `// SEC-READ:` marker or a pause-on-leak; the 52 → 0 gate is still IS15's. `PageService.cs` and `ContentTargetResolver.cs` are the deliverable files; seeders / `LocalizationService.SeedBaselineAsync` / `SeededContentCatalog` are **excluded by path** in IS15's gate and not marked.

## IS13 — Triage batch 3 (Pages / Targets, SEC-READ markers)

- **Deliverables (2 marker files + this append):** `src/Kumunita.Core/Pages/PageService.cs`, `src/Kumunita.Core/Targets/ContentTargetResolver.cs`. Comment-only edits (no method body, signature, `using`, or test changed). `dotnet build` 0 errors; `git diff --stat` on the two marker files is **22 insertions, 0 deletions** (the IS11 guard-clause trap was avoided). The affected Core classes are all green in-process (xunit.v3 `-class` form): `PageServiceTests` + `PageStandingPolicyTests` + `ContentTargetKindsTests` = **131 pass / 0 fail**.
- **Count of markers added: 22** (21 PageService, 1 ContentTargetResolver). Every marker line ends in a reason (IS15's `SecReadMarker_NamesAReason` enforces). The plan said "the ~16 such reads"; the batch-3 file set carried 22 decision-free reads + 6 forwarder methods — the count was a floor, not a pin (the gate IS15 retires the count to 0, it does not pin a number).
- **`method → verdict → evidence line` table** (one row per marked method, in file order):

  | Method (file) | Taxonomy line used | Evidence (file:line, makes it safe) |
  |---|---|---|
  | `PageService.GetByPathAsync` (PageService.cs:141) | `caller-gated by PageController.Show (CanAsync Read) / AdminController (GlobalAdmin) / StaticPagesController (public) / KioskItemCatalog (CanAsync Read)` | PageController.cs:234 → :259: `authz.CanAsync(actorId, AccessAction.Read, new PageToAuditableResource(page))` runs before the Web layer reads the page's content; the file header (PageService.cs:130-137) pins "the per-page authorization decision (the frozen CanAsync(Read) / CanSeeAsync(Read)) is made by the Web layer through the PageToAuditableResource adapter (ADR 0039 §3.4)" |
  | `PageService.GetBySlugUnderParentAsync` (PageService.cs:165) | `caller-gated by PageController.Translations (PageToAuditableResource, the per-translation read seam)` | PageController.Translations.cs:83/175/255: the three `authz.CanAsync(actorId, Authorization.AccessAction.Read, new PageToAuditableResource(page))` calls run before the read |
  | `PageService.GetTreeAsync` (PageService.cs:176) | `caller-gated by PageController.Index (CanSeeAsync C6) / HomeController (CanSeeAsync C6) / BlogController (title+href only) / SearchController (title+href only)` | PageController.cs:111-116: `authz.CanSeeAsync(actorId, Authorization.AccessAction.Read, candidate.Select(p => new PageToAuditableResource(p)))` runs over the whole tree; HomeController.cs:567: the same C6 aggregate; BlogController.cs:104-118: the feed is the resident's own (the `userId` scoping + the draft gate are in the caller); SearchController.cs:269: title+href only |
  | `PageService.GetTranslationsAsync` (PageService.cs:188) | `caller-gated by PageController.Show / PageController.Translations / HomeController (after the CanSeeAsync C6 already ran, C-M3·1)` | PageController.cs:264: `var translations = await pages.GetTranslationsAsync(page.Id)` runs *after* the `CanAsync(Read)` at :259; PageController.Translations.cs:83/175/255: the per-translation read seam runs after the `CanAsync(Read)` |
  | `PageService.ResolvePageAsync` (PageService.cs:206) | `caller-gated by StaticPagesController (the page's Read decision, the ADR 0039 §3.4 shape)` | StaticPagesController.cs:139/145: the `ResolvePageAsync` call is the single read seam; the page's Read decision is the Web route's (the ADR 0039 §3.4 adapter shape) |
  | `PageService.GetByMountPointAsync` (PageService.cs:246) | `caller-gated by PageMountResolver (the Web route's CanSeeAsync)` | PageMountResolver.cs:46: the mount-point read is followed by the route's decision; the mount point is a UI slot, not a content read |
  | `PageService.GetDepthAsync` (PageService.cs:286) | `no disclosure: returns an int (depth count); no audience-bearing field is exposed` | PageService.cs:286-303: the only return value is `int depth` — a derived count, not a content field |
  | `PageService.GetPathAsync` (PageService.cs:318) | `no disclosure: returns a slug-path string; no audience-bearing field (the ADR 0039 §3.3 derived path)` | PageService.cs:318-345: the only return value is `string?` (a slug chain) — a derived path, not a content field |
  | `PageService.EnsureNoCycleAsync` (PageService.cs:354) | `no disclosure: a hierarchy guard (cycle check); no content field is read or exposed` | PageService.cs:354-380: the method walks the `ParentId` chain and returns `void` (throws on a cycle) — no audience-bearing field is exposed |
  | `PageService.EnsureDepthWithinLimitAsync` (PageService.cs:382) | `no disclosure: a hierarchy guard (depth cap); no content field is read or exposed` | PageService.cs:382-393: the method calls `GetDepthAsync` and returns `void` (throws on a depth violation) — no audience-bearing field is exposed |
  | `PageService.GetBlogRootAsync` (PageService.cs:395) | `owner-only — scoped to the actor's own blog root (AuthorId == actorId, PageKind.User)` | PageService.cs:395-410: the query is scoped to `p.AuthorId == actorId && p.Kind == PageKind.User` — the actor's own rows only |
  | `PageService.GetBlogPagesAsync` (PageService.cs:411) | `owner-only — scoped to the author's own blog pages (AuthorId == authorId, PageKind.User)` | PageService.cs:411-424: the query is scoped to `p.AuthorId == authorId && p.Kind == PageKind.User` — the author's own rows only |
  | `PageService.IsUnderBlogAsync` (PageService.cs:426) | `no disclosure: returns a bool (is-under-blog probe); no content field is exposed` | PageService.cs:426-445: the only return value is `bool` — a probe, not a content field |
  | `PageService.CreateAsync` (PageService.cs:483) | `owner-only — standing is in-method (PageStandingPolicy.CheckCreateStanding, ADR 0040)` | PageService.cs:507: `PageStandingPolicy.CheckCreateStanding(actorId, actorRoles, page)` runs before the write — the standing decision is in-method |
  | `PageService.UpdateAsync` (PageService.cs:711) | `owner-only — standing is in-method (PageStandingPolicy.CheckEditStanding, ADR 0040)` | PageService.cs:730: `PageStandingPolicy.CheckEditStanding(actorId, actorRoles, existing)` runs after the load, before the write |
  | `PageService.PublishAsync` (PageService.cs:812) | `owner-only — author-only publish lane (ADR 0037, AuthorId == actorId)` | PageService.cs:834-836: `!string.Equals(page.AuthorId, actorId, StringComparison.Ordinal)` → `UnauthorizedAccessException` (the ADR 0037 pin) |
  | `PageService.MoveAsync` (PageService.cs:865) | `owner-only — standing is in-method (ResolveMoveDeleteStanding, ADR 0040)` | PageService.cs:887-891: `ResolveMoveDeleteStanding(page.Kind, page.AuthorId, page.ComponentId, actorId, actorRoles)` runs after the load, before the write |
  | `PageService.DeleteAsync` (PageService.cs:951) | `owner-only — standing is in-method (ResolveMoveDeleteStanding, ADR 0040)` | PageService.cs:973-977: `ResolveMoveDeleteStanding(page.Kind, page.AuthorId, page.ComponentId, actorId, actorRoles)` runs after the load, before the write |
  | `PageService.ResetToSeededAsync` (4-arg, PageService.cs:1092) | `owner-only — standing is in-method (PageStandingPolicy.CheckEditStanding, ADR 0058 D3)` | PageService.cs:1116: `PageStandingPolicy.CheckEditStanding(actorId, actorRoles, page)` runs after the load, before the write |
  | `PageService.GetSeededPageStatusAsync` (PageService.cs:1150) | `caller-gated by AdminHelpController (GlobalAdmin, [Authorize])` | AdminHelpController.cs:51: the `GetSeededPageStatusAsync` call is in a GlobalAdmin-only action (the `[Authorize]` is the convenience pre-gate; the status is a display probe — no content field is exposed) |
  | `PageService.ResetAllSeededPagesAsync` (3-arg, PageService.cs:1274) | `owner-only — standing is in-method (GlobalAdmin check, ADR 0058 D3)` | PageService.cs:1291-1292: `!actorRoles.Contains(Roles.GlobalAdmin)` → `UnauthorizedAccessException` (the GlobalAdmin gate runs before the write) |
  | `ContentTargetResolver.ResolveForListAsync` (ContentTargetResolver.cs:141) | `caller-gated by BookmarkService / PersonalNoteService (owner-scoped bookmark/note lanes; the list lane is a title+link projection, ADR 0118 D5)` | BookmarkService.cs:125: the `ResolveForListAsync` call is in the owner-scoped bookmark lane (the `ownerId` scoping is the caller's); PersonalNoteService.cs:199: the same owner-scoped pattern; the list lane returns only `Title` + `Link` (no audience-bearing field — the ADR 0118 D5 "zero-audit personal read" shape) |

- **Methods left as *already decided* or *forwarders* (no marker):**
  - `PageService.AddTranslationAsync` (PageService.cs:1006) — a thin forwarder to `_translationLane.AddAsync` (one-line delegation; the standing decision is in the delegate's method body — the IS11 `_groupPosts` forwarder pattern).
  - `PageService.UpdateTranslationAsync` (PageService.cs:1032) — a thin forwarder to `_translationLane.UpdateAsync` (same forwarder pattern).
  - `PageService.RemoveTranslationAsync` (PageService.cs:1049) — a thin forwarder to `_translationLane.RemoveAsync` (same forwarder pattern).
  - `PageService.ResetToSeededAsync` (3-arg, PageService.cs:1337) — a thin forwarder to the 4-arg overload (opens a lightweight session and delegates; the standing decision is in the 4-arg body).
  - `PageService.ResetAllSeededPagesAsync` (2-arg, PageService.cs:1345) — a thin forwarder to the 3-arg overload (same forwarder pattern).
  - `ContentTargetResolver.ResolveAsync` (ContentTargetResolver.cs:90) — the write-lane dispatcher (D3 create-gate check): each private `Resolve*TargetAsync` method runs `CanAsync(Read)` in-method (e.g. `ResolvePostTargetAsync` at ContentTargetResolver.cs:199-200: `authz.CanAsync(actorId, AccessAction.Read, new PostToAuditableResource(post))`). **Not marked** — the decision is in-method over the private methods it dispatches to.
  - `ContentTargetResolver.ResolvePostTargetAsync` / `ResolveGrouppostTargetAsync` / `ResolveEventTargetAsync` / `ResolveTodoTargetAsync` / `ResolveProjectTargetAsync` / `ResolveBoardTargetAsync` / `ResolveAnnouncementTargetAsync` / `ResolvePageTargetAsync` / `ResolveInventoryTargetAsync` / `ResolveDirectoryTargetAsync` / `ResolveDocumentTargetAsync` / `ResolvePageGroupTargetAsync` / `ResolvePageProjectTargetAsync` (all private, ContentTargetResolver.cs:185-362) — each runs `CanAsync(Read)` or the owner-only ordinal in-method (the D3 write-lane shape). **Not marked** — they are private and the decision is in-method.
  - `ContentTargetResolver.ResolvePostListAsync` / `ResolveGrouppostListAsync` / `ResolveEventListAsync` / `ResolveTodoListAsync` / `ResolveProjectListAsync` / `ResolveBoardListAsync` / `ResolveAnnouncementListAsync` / `ResolvePageListAsync` / `ResolveInventoryListAsync` / `ResolveDirectoryListAsync` / `ResolveDocumentListAsync` / `ResolvePageGroupListAsync` / `ResolvePageProjectListAsync` (all private, ContentTargetResolver.cs:364-504) — each returns only `Title` + `Link` (no audience-bearing field; the ADR 0118 D5 "zero-audit personal read" shape). **Not marked** — they are private and the caller (`ResolveForListAsync`) is the seam the marker names.
  - `ContentTargetResolver.BuildPagePathAsync` (ContentTargetResolver.cs:506) — a pure slug-path builder (the plan's special note): it receives an already-resolved page id and assembles a string. **Not marked** — it is private and returns only a `string?` (the ADR 0039 §3.3 derived path, the same "no disclosure" posture as `PageService.GetPathAsync`).
- **No leaks found; no pause.** Every method either self-decides in-method (`CanAsync` / `CanSeeAsync` / `CheckCreateStanding` / `CheckEditStanding` / `ResolveMoveDeleteStanding` / the author-only ordinal), is owner-scoped (`AuthorId == actorId` / `AuthorId == authorId` on the candidate set), returns no audience-bearing field (int depth, string path, bool probe, title+link projection), or is a read seam whose caller already ran the decision (`PageController.Show`'s `CanAsync(Read)`, `PageController.Index`'s `CanSeeAsync` C6, `HomeController`'s `CanSeeAsync` C6, `BlogController`'s `userId` scoping + draft gate, `SearchController`'s title+href, `StaticPagesController`'s Read decision, `PageMountResolver`'s route decision, `AdminHelpController`'s GlobalAdmin `[Authorize]`, `BookmarkService` / `PersonalNoteService`'s owner-scoped lanes).
- **Noticed:** the `PageService.cs` file header (lines 130-137) is the authoritative statement of the read-lane posture: "The read lanes load and filter documents only. The per-page authorization decision (the frozen CanAsync(Read) / CanSeeAsync(Read)) is made by the Web layer through the PageToAuditableResource adapter (ADR 0039 §3.4) — PG adds an adapter, not a branch (ADR 0006 §A)." This is the same posture as the IS11/IS12 "caller-gated by …" markers; the markers record the specific Web-layer seam for each method.
- **Noticed:** `ContentTargetResolver.ResolveAsync` (the write-lane dispatcher) is the D3 create-gate check shape — each private `Resolve*TargetAsync` method runs `CanAsync(Read)` in-method (the frozen read decision over the target's own audience). This is the plan's "decided in-method → leave it" posture; **not marked**.
- **Noticed (forwarder pattern, avoided):** `PageService.AddTranslationAsync` / `UpdateTranslationAsync` / `RemoveTranslationAsync` are thin forwarders to `_translationLane.{Add,Update,Remove}Async` (one-line `return await _translationLane.…` delegation). The standing decision is in the delegate's method body (the `TranslationLane`'s `CheckTranslateStanding` seam — the IS11 `_groupPosts` forwarder pattern). **Not marked** — per the plan's "forwarder / seam" posture, the decision is in the delegate's method body, not this seam's.
- **Noticed (guard-clause trap, avoided):** all 22 marker insertions were anchored on the method signature + `{` + first body line (not spanning guard clauses). The `diff --stat` check (0 deletions required) confirms the final diff is 22 insertions, 0 deletions.
- **Next unit (IS14):** triage batch 4 (Web controllers, 6 methods) + the component-list bypass — the same `// SEC-READ:` marker or a pause-on-leak; the 52 → 0 gate is still IS15's. `PostsController.cs` (the two `Edit` actions), `GroupsController.cs` (`EditGroupPost`), `PageController.cs` (the two `Edit` actions) are the deliverable files; the `PostsController` ≈ :743 "GlobalAdmin bypasses the membership check" is verified to list component *names* only (never content) — if it lists content, pause.

## IS14 — Triage batch 4 (Web controllers + component-list bypass, SEC-READ markers)

- **Deliverables (0 marker files + this append):** this batch is *marker-free* — every one of the 6 Web controller Edit actions runs its standing gate **in-method over the row it reads**, which is the plan's "decided in-method → leave it" posture (the same as IS12's `EventService.UpdateAsync` / `DeleteAsync` and IS13's `PageService.UpdateAsync`). No `// SEC-READ:` marker was added; the verdict table below records *why* each action is already gated (IS15's `EveryAudienceBearingRead_IsDecidedOrMarked` gate is satisfied by the in-method decision, not by a marker — the gate's "decided in-method" branch covers these). Comment-only (no edit at all — the three deliverable files are **unchanged**); `dotnet build` 0 errors; `Kumunita.Web.Tests.PageControllerTests` 39 pass / 0 fail (identical before and after, as it must be — no code changed).
- **Count of markers added: 0** (6 actions, all *already decided in-method*). The 64 prior markers (22 batch-1 + 20 batch-2 + 22 batch-3) are unchanged — verified by `grep -rn "SEC-READ" src/ --include=*.cs | grep -v obj | wc -l` → **64** both before and after this unit.
- **`method → verdict → evidence line` table** (one row per action — all six *left unmarked* because the decision is in-method):

  | Method (file:line) | Verdict | Evidence (the in-method gate over the row it reads) |
  |---|---|---|
  | `PostsController.Edit` GET (PostsController.cs:1025) | **already decided** — author-only ordinal in-method | PostsController.cs:1032: `var post = await session.LoadAsync<Post>(id)` (the read); :1039: `if (post.AuthorId != actor) return Forbid();` (the gate over the read row, before any content is rendered); the doc (PostsController.cs:1011-1024) pins "a non-author never reaches the form" and names the POST's `UpdatePostAsync` re-pin (PostService.cs:716) as defense-in-depth |
  | `PostsController.Edit` POST (PostsController.cs:1116) | **already decided** — author-only ordinal in-method + the service re-pins | PostsController.cs:1136: `var post = await probe.LoadAsync<Post>(id)` (the read); :1137: `if (post is null \|\| post.AuthorId != actor) return Forbid();` (the gate over the read row, before `model.Body` / `model.Audience` are bound to the post); the service re-pins at PostService.cs:711-713 (`post.AuthorId == actorId` else `UnauthorizedAccessException` — the IS11-marked `UpdatePostAsync`); the `catch (UnauthorizedAccessException) → Forbid()` (PostsController.cs:1196-1201) is the 403 shape, not the decision |
  | `GroupsController.EditGroupPost` GET (GroupsController.cs:1803) | **already decided** — author-only ordinal in-method (the group-lane 404 shape) | GroupsController.cs:1820: `var post = await read.LoadAsync<Post>(postId)` (the read); :1824: `if (post.AuthorId != actor) return NotFound();` (the gate over the read row, before `Title` / `Body` are rendered into the form); the doc (GroupsController.cs:1771-1782) pins "a non-author … all return a 404" (the G·3/G·4 non-leaky shape, deliberately not a 403) |
  | `GroupsController.EditGroupPost` POST (GroupsController.cs:1857) | **already decided** — forwarder to `GroupPostService.UpdateGroupPostAsync`, whose body runs the author-only ordinal | GroupsController.cs:1894-1902: the action's only audience-bearing read is `groupPosts.UpdateGroupPostAsync(postId, actor, …)` (the write); the read is inside the delegate — GroupPostService.cs:476-477: `if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal)) throw new UnauthorizedAccessException("Only the author of a post may edit it.")` (the in-method gate in the delegate's body, the IS11 `_groupPosts` forwarder pattern); the controller's `catch (UnauthorizedAccessException) → NotFound()` (GroupsController.cs:1908-1913) is the 404 shape, not the decision |
  | `PageController.Edit` GET (PageController.cs:641) | **already decided** — `HasEditStanding` in-method (ADR 0040, kind-aware) | PageController.cs:647: `page = (await session.LoadAsync<Page>(id))!` (the read); :661: `if (!HasEditStanding(page, actorId, KumunitaPrincipal.RoleSet(User))) return new ForbidResult();` (the gate over the read row, before `Title` / `Body` / `Audience` are rendered); the service re-pins at PageService.cs:740 (`PageStandingPolicy.CheckEditStanding` — the IS13-marked `UpdateAsync`); the `[Authorize]` (PageController.cs:640) is the convenience pre-gate, not the decision |
  | `PageController.Edit` POST (PageController.cs:710) | **already decided** — `HasEditStanding` in-method (ADR 0040) | PageController.cs:730: `existing = (await session.LoadAsync<Page>(id))!` (the read); :740: `if (!HasEditStanding(existing, actorId, KumunitaPrincipal.RoleSet(User))) return new ForbidResult();` (the gate over the read row, before `model.Title` / `model.Body` are written); the service re-pins at PageService.cs:740 (the IS13-marked `UpdateAsync`'s `CheckEditStanding`); the `catch (UnauthorizedAccessException) → Forbid()` (PageController.cs:779-781) is the 403 shape, not the decision |

- **Methods left as *already decided* (no marker — all 6 actions, the plan's "decided in-method → leave it" posture):**
  - `PostsController.Edit` GET / POST — the author-only ordinal (`post.AuthorId != actor` / `post.AuthorId == actorId`) runs in-method over the `Post` row it reads, before any content field is rendered or bound; the service's `UpdatePostAsync` (PostService.cs:711-713, IS11-marked) re-pins the same gate at write time (defense-in-depth — the "form is a shape, service is the gate" split the `AnnouncementController` edit lane uses).
  - `GroupsController.EditGroupPost` GET — the author-only ordinal runs in-method (the G·3/G·4 404 shape); the POST is the IS11 `_groupPosts` forwarder pattern (the decision is in `GroupPostService.UpdateGroupPostAsync`'s body at GroupPostService.cs:476-477, not this seam's).
  - `PageController.Edit` GET / POST — `HasEditStanding` (ADR 0040, kind-aware: a system page is GlobalAdmin-only, a blog page is author ∪ GlobalAdmin) runs in-method over the `Page` row it reads; the service's `UpdateAsync` (PageService.cs:740, IS13-marked) re-pins `PageStandingPolicy.CheckEditStanding` at write time.
- **Component-list bypass verdict (the plan's explicit pause point): VERIFIED — names only, never content.** `PostsController.AccessibleComponentsAsync` (PostsController.cs:738-767) returns `IReadOnlyList<Component>` — the `Component` aggregate (src/Kumunita.Core/UserInfo/Component.cs:15-44) carries `Id`, `Name`, `Description`, `Icon`, `SortOrder`, `Enabled`, `ModeratorAccess`, `Mandatory`. **No `Post`, `PostReply`, `Audience`, `Body`, `Title`, or any post-content field.** The GlobalAdmin branch (PostsController.cs:744: `return all; // GlobalAdmin bypasses the membership check`) returns the full enabled-component list — a *picker candidate set* (which communities are postable), not a content read. The non-GlobalAdmin branch (PostsController.cs:746-766) filters by the actor's effective community set + moderator-scope claims — the same rule the POST gate in `PostService.CreatePostAsync` applies (the doc at PostsController.cs:721-736 pins "the picker and the POST gate on one rule"). **No pause; no `IS10x` needed.** This is the plan's "verification + record" step, not a `// SEC-READ:` marker.
- **Seeders / `LocalizationService.SeedBaselineAsync` / `SeededContentCatalog` were not marked** (excluded by path in IS15's gate, per the plan). The `PageController.Edit` GET's `SeededContentCatalog.HasSeededText(page.Slug)` call (PageController.cs:675) is a display probe (a `bool` — whether the slug has a seeded baseline), not an audience-bearing read; it was not marked and is not in scope.
- **No leaks found; no pause.** All 6 actions either run the author-only ordinal / `HasEditStanding` / the IS11 `_groupPosts` forwarder (the decision in the delegate's body) in-method over the row they read, before any content field is rendered or written. The plan's "decided in-method → leave it" posture applies to all of them; the marker is the *alternative* for caller-gated / owner-scoped / non-disclosing seams that do **not** run the decision in-method (the IS11-13 pattern).
- **Noticed:** this is the first IS11–14 batch with **0 markers** — the other three batches (IS11: 22, IS12: 20, IS13: 22) all marked service-layer methods that are *seams* (the decision is in the caller or the delegate, not in the seam's own body). The Web controller Edit actions are the *opposite* shape: the decision is in the action's own body (the Web layer is the gate, not the service). This is the plan's "form is a shape, service is the gate" split *reversed* for the read: the action is the gate, the service is the re-pin.
- **Noticed:** `GroupsController.EditGroupPost` POST is the IS11 `_groupPosts` forwarder pattern (one-line `await groupPosts.UpdateGroupPostAsync(…)` delegation; the decision is in `GroupPostService.UpdateGroupPostAsync`'s body at GroupPostService.cs:476-477). **Not marked** — per the plan's "forwarder / seam" posture, the decision is in the delegate's method body, not this seam's. The GET is the opposite shape (the decision is in the action's own body — the author-only ordinal at GroupsController.cs:1824).
- **Noticed (guard-clause trap, N/A):** no marker insertions were made (0 markers), so the IS11/IS12/IS13 guard-clause trap did not apply. The `git diff --stat` check (0 deletions required) is trivially satisfied (the three deliverable files are **unchanged** — `git diff --stat` on them is 0 insertions, 0 deletions).
- **Noticed (the 52 → 0 gate is still IS15's):** the 6 actions in this batch are *already satisfied* by the in-method decision (the gate's "decided in-method" branch covers them — no marker needed). The 52 → 0 count is the gate's *baseline*; IS15's `EveryAudienceBearingRead_IsDecidedOrMarked` will verify that each of the 52 is either (a) decided in-method (the IS14 pattern) or (b) marked with a `// SEC-READ:` line (the IS11-13 pattern). The IS14 actions are (a).
- **Next unit (IS15):** the decision-free-read gate — `tests/Kumunita.Web.Tests/SecGate/DecisionFreeReadGateTests.cs` (a C# port of M7, per the plan). The gate's audience-bearing type list is read from IS03 §2.5 class (b) types; path excludes cover seeders, portability, the eraser, purge jobs and migrations. Tests: `EveryAudienceBearingRead_IsDecidedOrMarked` (52 → **0** unmarked), `SecReadMarker_NamesAReason`.

## IS15 — The decision-free-read gate (S4/E4, "52 → 0")

- **Deliverables (one test file, no src edit, no behaviour change):** refined the pre-written scaffold `tests/Kumunita.Web.Tests/SecGate/DecisionFreeReadGateTests.cs` until both tests pass. This is a **gate-only unit** — the gate is a *source scan* (it reads `src/**/*.cs` as text and applies regex, it does not compile or execute the target), so it needs no live server and no DB. The two tests are `EveryAudienceBearingRead_IsDecidedOrMarked` (per-method scan; retires the 52-candidate baseline to **0** unmarked) and `SecReadMarker_NamesAReason` (every `// SEC-READ:` marker opens with a taxonomy word *and* ends in a reason beyond it). `dotnet build` 0 errors; `dotnet exec …Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.SecGate.DecisionFreeReadGateTests"` → **Total: 2, Failed: 0**. `git diff --stat -- src/` is **empty** (no src file changed); the only tracked change is the one test file.
- **The gate's in-scope rule (what it flags):** a method is *in scope* only when it is **public** AND makes a **genuine audience-bearing read** (a Marten read idiom `LoadAsync<T>` / `Load` / `Query…` / `Fetch…` / `QuerySingle` naming one of the **41 class-(b)** type names from design §2.5 — the set is pinned verbatim in `AudienceBearingTypes`, not reconstructed from memory) AND carries an **actor** (an actor-like parameter: `actorId` / `actor` / `roles` / `principal` / `residentId` / `selfId` / `ownerId` / `userId` / `addedBy`, *or* a request-principal reference: `SubjectId(` / `RoleSet(` / `KumunitaPrincipal` / `User`). An in-scope method must then either **decide in-method** or carry a **`// SEC-READ:`** marker. A public method with no actor param and no request-principal reference is *structurally* caller-gated / background (the caller is the gate, or it is a seed / export / purge) and is **out of scope** — this is what keeps the 13 caller-gated fetch seams and the seeder surfaces from being false positives.
- **The two "satisfied" branches:** (a) **decided in-method** — `IsDecided(body)` is true when the body contains one of the **named-guard substrings** (`Standing` (a *suffix* — `CheckTodoStanding` / `HasEditStanding` / `ResolveMoveDeleteStanding` — matched as a substring, not a word boundary), `AccessVia`, `EnsureEditPermission`, `EnsurePermission`, `EnsureEnabledAsync`, `IsMessagingAllowed`, `GuardianVeto`, `Participants.Contains`, `IsParticipant`, `IsMember`, `Members.Contains`, `Contains(actor`, `ResolveAsync`, `ResolveForListAsync`, `ResolveReadVisibility`, `LoadActorReadableContent`, `.IsPublic`, `.IsPrivate`) **or** one of the **`DecisionPatterns` regex families**; (b) **marked** — the body contains `// SEC-READ:`.
- **The `DecisionPatterns` regex families (generalized, so a new seam cannot drift past a flat name list):** `Can*` standing family (`CanSeeAsync` / `CanSeeGroupAsync` / `CanSeeGroupFeedAsync` / `CanPost` / `CanRead` / …), the `Gate*` decision helpers (`GateAsync`), the `Decide*` family, and the **owner/author/self ordinal** — a principal field (`AuthorId|OwnerId|UserId|CreatedBy|RecipientId|SubjectId|SenderId|InvitedId`) compared to an actor token (`actor|owner|author|principal|updatedBy|resident|self|userId|ownerId|actorId`) in *both* comparison orders (`x.OwnerId == actor` and `actor == x.OwnerId`), the `string.Equals(x.OwnerId, actor)` form, and the bare `.Field ==` self-scoped-query shape. The 6 **IS14 in-method Edit actions** (the author-only ordinal in the Web controller bodies) are resolved by this ordinal branch — they are **not** flagged, and **not** marked (per IS14's "decided in-method → leave it" posture).
- **Calibration decisions (the triage that drove 151 raw candidates → 0):** (1) the scaffold's `MethodDecl` regex over-matched constructors / field initializers / private helpers / lambdas — narrowed to the per-request (public) surface; (2) `DecisionIdioms` was missing real decisions the repo actually uses (`HasEditStanding` / `CheckTodoStanding` via the `Standing` suffix, `CanSeeGroupFeedAsync`, `EnsureEditPermission`, `LoadActorReadableContent`, `GateAsync`) — added; (3) the 3 `string.Equals(…PrincipalField, actor)` owner-ordinal methods (EventService.Group.cs, GroupPostService.cs, PersonalNoteService.cs) are **legitimate owner-only gates**, captured by the ordinal patterns, not leaks; (4) deliberately **dropped the bare English words** `guardian` / `veto` — a *comment* mentioning them is not a decision; only the specific named guard `GuardianVeto` counts. After the drop, the Python calibration model still shows **0 violations**, confirming no real decision was relying on the bare words.
- **Path-exclude list (a file whose path contains any token is skipped wholesale):** `Seeder`, `Portability`, `Eraser`, `Purge`, `Migration`, `DocTypes`, `SeededContentCatalog` — the seed / export / erasure / purge / schema surfaces, per the plan.
- **Non-vacuity proven (the gate is not a vacuous all-green):** the gate is a source scan, so the proof needs no rebuild. Dropped a synthetic `src/Kumunita.Core/Posts/__Is15LeakProbe.cs` — a **public** method with an **actor** param that does `LoadAsync<Post>(…)` (Post is class-(b)) with **no decision and no marker** — re-ran the gate: `EveryAudienceBearingRead_IsDecidedOrMarked` went **RED**, flagging the exact line (`reads <Post> — no decision in-method, no SEC-READ marker`), while `SecReadMarker_NamesAReason` stayed green on the 64 real markers. Deleted the probe → both tests **green** again. So the gate (i) retires the real 52-candidate baseline to 0, (ii) catches a genuine leak, and (iii) does not flag the 6 IS14 in-method actions.
- **No leaks found; no pause; no `IS10x` needed.** All 151 raw candidates resolved via (a) an in-method decision (named guard / `Can*` / `Gate*` / the owner-ordinal / self-scoped query), (b) a `// SEC-READ:` marker, (c) the path-exclude, or (d) out-of-scope (public, no actor, no request-principal reference → structurally caller-gated / background). The 129 in-scope audience-bearing reads are **all decided**. The 64 markers (22 IS11 + 20 IS12 + 22 IS13 + 0 IS14) all open with a taxonomy word and end in a reason.
- **Next unit:** none named in the lane — IS15 was the S4/E4 close. If a future lane re-opens a class-(b) read surface, the gate is the thing that will catch an undecided / unmarked one on the per-request surface (the "52 → 0" baseline is now *re-pinned* by `EveryAudienceBearingRead_IsDecidedOrMarked` and cannot silently regress).

## IS16 — ADR 0298 + the `[DataClass]` attribute + the catalog reader (Phase C foundation)

- **Deliverables (4, all new except the ADR index row):** `docs/adr/0298-data-classes-in-code.md` (77 lines, ≤80); the index row `| 0298 | … | Accepted |` appended to `docs/adr/README.md` (matched the 0292–0297 house style); `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (new file, new `Privacy` module) carrying the three enums, the attribute, and the `DataClassCatalog`; `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs` (new). `dotnet build` (solution) → **0 errors**; `dotnet exec …Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.DataClassCatalogTests"` → **Total: 3, Failed: 0**.
- **The enums (verbatim from D2 — no synonyms invented):** `DataClassKind { Pii, PrivateContent, AuditModeration, Secret, Media, Operational, Platform }`; `Erasure { Delete, Pseudonymize, Keep }`; `ExportScope { Resident, InstanceOnly, Never }`.
- **The attribute:** `DataClassAttribute(kind, erasure, export, reason, retentionKey? = null, principalField? = null)` — `Kind` / `Erasure` / `Export` / `Reason` (required, non-null) / `RetentionKey` / `PrincipalField`. The two invariants live in `Validate()`: **`Keep` → non-empty `Reason`**, **`Pseudonymize` → non-empty `PrincipalField`**; each throws `ArgumentException` naming the violated rule. `DataClassCatalog.For(Type)` returns the row or a `null` "none" sentinel (validated before returning, wrapped with the type name); `DataClassCatalog.All(Assembly)` returns every attributed concrete type, sorted by `FullName`, each validated, as `DataClassEntry(Type, Policy)` (a `readonly record struct`).
- **The three tests (the IS16 pins):** `Catalog_ReadsAttribute` (two throwaway fixture types — a `SessionDoc` `Delete` row + a `Report` `Pseudonymize` row — assert kind/erasure/export/retention/reason/principal read back, and that `All(Assembly)` finds both; un-attributed types return the `null` sentinel); `Keep_RequiresReason` (a `Keep` row with a blank reason throws `ArgumentException`; the good `Keep` passes); `Pseudonymize_RequiresPrincipalField` (a `Pseudonymize` row with no `PrincipalField` throws; the good row with `ActorId` passes).
- **Scope held:** no existing doc type is annotated (that is IS17–IS20); no eraser / export reads the attribute yet (IS21–IS24). The attribute is **inert** — no Marten, no DI, no behaviour. `grep -rn "\[DataClass(" src --include="*.cs"` → **only** the new `Privacy` file (the two fixture usages are in the test assembly, not `src/`).
- **Noticed (for IS17–IS20):** the §2.5 "Reason" column values are the exact `reason:` strings to copy per type; the §2.5 "Class" column's letters a–e / operational / platform map to the `DataClassKind` members as `a→Pii, b→PrivateContent, c→AuditModeration, d→Secret, e→Media, operational→Operational, platform→Platform`. The 9 `Pseudonymize` rows in §2.5 already name their principal field (the `Pseudonymize_RequiresPrincipalField` rule is satisfied by the table); IS17–IS20 must carry that field into `principalField:`.
- **Next unit:** IS17 (annotate the Identity/UserInfo/Authorization/Localization batch, ~20 types, one line each, copied verbatim from §2.5).

## IS17a — Identity batch (4 types: `OutboxEmail`, `EmailDeadLetter`, `IdentityToken`, `GuestAccess`)

- **Files (3):** `Identity/EmailMessages.cs` (both), `Identity/IdentityToken.cs`, `Identity/GuestAccess.cs`. Each gains one `[DataClass]` + `using Kumunita.Core.Privacy;`; no new method, no DI, no behaviour.
- **Values (verbatim §2.5):** `OutboxEmail` `Pii/Delete/Never` reason "F1 §2C … D6: body nulled 7 days … row deleted 90 days" `retentionKey: OutboxBodyDays` (the §2.5 two-key row; `OutboxRowDays` is the row-delete key, an IS34a concern); `EmailDeadLetter` `Operational/Delete/Never` reason "D6: 30 days … (IS34a)" `retentionKey: DeadLetterDays`; `IdentityToken` `Secret/Delete/Never` reason "F1 §2C … (IS22); D6: 7 days …" `retentionKey: TokenDays`; `GuestAccess` `AuditModeration/Delete/Never` reason "F1 §2C … (IS22)".
- **Tests:** none (IS17–IS19 are "no test" per design §2.3; the 81/81 gate is IS20's). `grep -c "\[DataClass("` over the 3 files → **4**. `dotnet build` (solution) → **0 errors**.

## IS17b — UserInfo batch 1 (5 types: `Group`, `GroupMembership`, `DelegationGrant`, `GuardianLink`, `GuardianTimeLimitSchedule`)

- **Files (3):** `UserInfo/Group.cs` (first three), `UserInfo/GuardianLink.cs`, `UserInfo/GuardianTimeLimitSchedule.cs`.
- **Values (verbatim §2.5):** `Group` `PrivateContent/Keep/InstanceOnly` reason "IS03 §2C decision … ADR 0142" (a `Keep` row — reason present, satisfies IS16's `Keep_RequiresReason`); `GroupMembership` `PrivateContent/Delete/InstanceOnly` reason "F1 §2B … (step 6)"; `DelegationGrant` `AuditModeration/Delete/Never` reason "F1 §2C … (IS22)"; `GuardianLink` `AuditModeration/Delete/Never` reason "F1 §2B … (step 8b, dissolved)"; `GuardianTimeLimitSchedule` `AuditModeration/Delete/Never` reason "IS23: the child's schedule is the child's data".
- **Tests:** none. `grep -c` over the 3 files → **5**. `dotnet build` → **0 errors**.

## IS17c — UserInfo batch 2 (3 types annotated; `Profile` **PAUSED**)

- **Files (1):** `UserInfo/Component.cs` — `Component` `Platform/Keep/InstanceOnly` reason "a community component … F1 §2C excluded"; `ModeratorAssignment` + `ComponentMembership` both `AuditModeration/Delete/InstanceOnly` reason "F1 §2B … (step 7)".
- **PAUSE — `Profile` (not annotated, recorded per rule 1–9):** the §2.5 row is `a | Pseudonymize | Resident | — | F1 §2B: already handled by DeleteCoreAsync (step 8)…` — a **Pseudonymize** row that names **no principal field** (unlike `AccessAudit`, whose §2.5 names `ActorId`). IS16's `Pseudonymize_RequiresPrincipalField` invariant would throw at read time, and the IS17 prompt states this case explicitly: *"a Pseudonymize type with no field named in §2.5 is a pause, not a guess."* Left unannotated; needs an owner/IS21 decision on which field is pseudonymized (the §2.6 Rule 3 blanked PII fields `Email`/`Phone`/`Address` are candidates, but §2.5 does not name one).
- **Tests:** none. `grep -c` over `Component.cs` → **3**. `dotnet build` → **0 errors**.

## IS17d — Authorization batch (2 types annotated; `AdminOverride` **out of scope**)

- **Files (1):** `Authorization/AccessAudit.cs` — `AccessAudit` `AuditModeration/Pseudonymize/Never` reason "F1 §2B … data-classes.md §3.1 …" `principalField: ActorId` (the §2.5 row names `ActorId` / `EffectivePrincipalId`; `ActorId` is the acting account, the primary principal — matches the prompt's `AccessAudit → ActorId` example); `AuditPurgeSummary` `AuditModeration/Keep/Never` reason "an operational record of the purge itself; no resident principal".
- **Out of scope — `AdminOverride` (not annotated):** listed in the IS17 file map but **not a registered `Schema.For<T>` doc type** — `CoreDocTypes.cs:138`: *"AdminOverride is hand-rolled, see AuthorizationFeature"*; there is no `Schema.For<AdminOverride>` anywhere in `src/`. It has **no row in the 81-row §2.5 table** (grep confirms it appears only at the BreakGlassProbe SQL note and the IS28 gate note, never as a `| \`AdminOverride\` |` row). Annotating it would add a non-registered type to `DataClassCatalog.All` and break IS20's 81/81 coverage gate. File-map correction recorded; the successor may fold it into a "registered vs. hand-rolled" note in IS41.
- **Tests:** none. `grep -c` over `AccessAudit.cs` → **2**. `dotnet build` → **0 errors**.

## IS17e — Localization + Notifications batch (4 types: `LanguageCatalog`, `LocaleSettings`, `TranslationResource`, `SuppressedAddress`)

- **Files (3):** `Localization/LanguageCatalog.cs` (both), `Localization/TranslationResource.cs`, `Notifications/SuppressedAddress.cs`.
- **Values (verbatim §2.5):** `LanguageCatalog` `Platform/Keep/InstanceOnly` reason "the language list; no resident principal"; `LocaleSettings` `Platform/Keep/InstanceOnly` reason "the community's locale defaults; no resident principal"; `TranslationResource` `Platform/Keep/InstanceOnly` reason "the UI translation strings; no resident principal"; `SuppressedAddress` `Operational/Delete/Never` reason "the resident opted out of notifications; the row is theirs; D2: declared on the type".
- **Tests:** none. `grep -c` over the 3 files → **4**. `dotnet build` → **0 errors**.

## IS17 — batch summary (18 of the 20 listed types annotated)

- **Total:** `grep -rc "\[DataClass(" src --include="*.cs"` → **18** (the 11 files above; `Profile.cs` and `AdminOverride.cs` correctly at **0**). Every annotated file has exactly one `using Kumunita.Core.Privacy;`. No test added, no eraser/export touched, no IS18–IS20 type annotated. `dotnet build` (solution) → **0 errors**.
- **Two pauses / corrections (not annotated):** `Profile` (Pseudonymize row names no principal field — the prompt's explicit "pause, not a guess" case) and `AdminOverride` (not a registered doc type — hand-rolled, no §2.5 row). Both are recorded above for the successor and the IS21/IS24 consumers.
- **retentionKey convention:** the bare `RetentionOptions` field name (`OutboxBodyDays`, `DeadLetterDays`, `TokenDays`) — matching IS16's `DataClassAttribute` doc examples (`SessionDays`, `TokenDays`) — not the full `Retention__*` config key.
- **Next unit:** IS18 (Posts / Announcements / Events / Pages / Tags / Moderation batch, copied verbatim from §2.5).

## IS18a — Posts batch (4 types: `Post`, `PostReply`, `PostTranslation`, `ReplyTranslation`)

- **Files (4):** `Posts/Post.cs`, `Posts/PostReply.cs`, `Posts/PostTranslation.cs`, `Posts/ReplyTranslation.cs`. Each gains one `[DataClass]` + `using Kumunita.Core.Privacy;`; no new method, no DI, no behaviour.
- **Values (verbatim §2.5):** all four `PrivateContent/Delete/Resident`, no `retentionKey` (every §2.5 Retention-key cell is `—`), no `principalField`. Reasons: `Post` "F1 §2A: resident-authored content"; `PostReply` "F1 §2A"; `PostTranslation` "F1 §2A; ADR 0022 user-added translations"; `ReplyTranslation` "F1 §2A; ADR 0022".
- **Tests:** none (IS17–IS19 are "no test" per design §2.3). `grep -c "\[DataClass("` over the 4 files → **4**. `dotnet build` (solution) → **0 errors**.

## IS18b — Announcements batch (3 types: `Announcement`, `AnnouncementTranslation`, `AnnouncementComment`)

- **Files (3):** `Announcements/Announcement.cs`, `Announcements/AnnouncementTranslation.cs`, `Announcements/AnnouncementComment.cs`.
- **Values (verbatim §2.5):** all three `PrivateContent/Delete/Resident`. Reasons: `Announcement` "F1 §2A" with the §2.5 condition carried in the reason text ("condition: `AuthorId == resident`; platform `AuthorId == ""` are kept"); `AnnouncementTranslation` "F1 §2A"; `AnnouncementComment` "F1 §2A: the resident's comments".
- **Tests:** none. `grep -c` over the 3 files → **3**. `dotnet build` → **0 errors**.

## IS18c — Events batch (4 types: `Event`, `EventTranslation`, `EventRsvp`, `GuardianEventRequest`)

- **Files (4):** `Events/Event.cs`, `Events/EventTranslation.cs`, `Events/EventRsvp.cs`, `Events/GuardianEventRequest.cs`.
- **Values (verbatim §2.5):** `Event`/`EventTranslation`/`EventRsvp` `PrivateContent/Delete/Resident` — reasons "F1 §2A", "F1 §2A; ADR 0059", "F1 §2A: the resident's own RSVPs (not the event's authorship)"; `GuardianEventRequest` `AuditModeration/Delete/Never` reason "IS23: a child's event request is the child's data".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS18d — Pages + Tags batch (4 types: `Page`, `PageTranslation`, `Tag`, `TagTranslation`)

- **Files (4):** `Pages/Page.cs`, `Pages/PageTranslation.cs`, `Tags/Tag.cs`, `Tags/TagTranslation.cs`.
- **Values (verbatim §2.5):** all four `PrivateContent/Delete/Resident`. Reasons: `Page` "F1 §2A: the resident's blog pages"; `PageTranslation` "F1 §2A; the E6 gap this lane closes (IS31)"; `Tag` "F1 §2A: the tags the resident created"; `TagTranslation` "F1 §2A: user-added tag translations".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS18e — Moderation (1 type: `Report`)

- **Files (1):** `Posts/Report.cs` (a Moderation-type row that lives under `Posts/`, per the plan's file map). No hand-rolled data-class comment in the file was found (superseded-comment check: none).
- **Values (verbatim §2.5):** `AuditModeration/Pseudonymize/Never`, `principalField: "ReporterId"` (the field exists on the class, verified). Reason: "F1 §2C decision (now decided): moderation state, not authored content (IS23); M27 C-M27·3 excludes it from the delete set". Satisfies IS16's `Pseudonymize_RequiresPrincipalField` invariant.
- **Tests:** none. `grep -c` over `Report.cs` → **1**. `dotnet build` → **0 errors**.

## IS18 — batch summary (16 of the 16 listed types annotated)

- **Total:** `grep -rc "\[DataClass(" src --include="*.cs"` → **34** (18 after IS17 + 16 here). Every annotated file has exactly one `using Kumunita.Core.Privacy;`. 15 `Delete` rows + 1 `Pseudonymize` row (`Report`); no `Keep` rows, no `retentionKey` in the batch (every §2.5 cell `—`), `principalField` only on `Report`.
- **No pauses, no corrections:** every §2.5 row was present and unambiguous; no hand-rolled data-class comment superseded. No test added, no eraser/export touched, no type outside the batch annotated. `dotnet build` (solution) → **0 errors** (160 warnings, all pre-existing route/nullable warnings in `Kumunita.Web` — unchanged by this lane).
- **Next unit:** IS19 (Projects / Documents / Bookmarks / Inventory / PersonalNotes / Media / Kiosk batch, copied verbatim from §2.5).

## IS19a — Projects 1 batch (4 types: `Project`, `ProjectGoal`, `ProjectTranslation`, `TodoItem`)

- **Files (4):** `Projects/Project.cs`, `Projects/ProjectGoal.cs`, `Projects/ProjectTranslation.cs`, `Projects/TodoItem.cs`.
- **Values (verbatim §2.5):** all four `PrivateContent/Delete/Resident`. Reasons: `Project` "F1 §2A"; `ProjectGoal` "F1 §2A"; `ProjectTranslation` "F1 §2A; ADR 0088"; `TodoItem` "F1 §2A" (the §2.5 condition "where `AuthorId == resident`; **not** `AssigneeId`" is the eraser's query, not part of the attribute — `principalField` is Pseudonymize-only, so it is not carried).
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS19b — Projects 2 batch (4 types: `TodoComment`, `TodoTranslation`, `KanbanBoard`, `KanbanLane`)

- **Files (4):** `Projects/TodoComment.cs`, `Projects/TodoTranslation.cs`, `Projects/KanbanBoard.cs`, `Projects/KanbanLane.cs`.
- **Values (verbatim §2.5):** `TodoComment` `PrivateContent/Delete/Resident` "F1 §2A"; `TodoTranslation` `PrivateContent/Delete/Resident` "F1 §2A; ADR 0088"; `KanbanBoard` `PrivateContent/Delete/Resident` "F1 §2A: the board the resident owns"; `KanbanLane` `PrivateContent/Keep/InstanceOnly` reason "F1 §2A sub-entities: travels with `KanbanBoard`; the board row's Delete is what removes it, not a principal query" (a cascade row — `Keep_RequiresReason` satisfied, no `principalField`).
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS19c — Projects 3 batch (2 types: `BoardItemPlacement`, `BoardTranslation`)

- **Files (2):** `Projects/BoardItemPlacement.cs`, `Projects/BoardTranslation.cs`.
- **Values (verbatim §2.5):** `BoardItemPlacement` `PrivateContent/Keep/InstanceOnly` reason "F1 §2A sub-entities: under `KanbanBoard`" (cascade row, as `KanbanLane`); `BoardTranslation` `PrivateContent/Delete/Resident` "F1 §2A; ADR 0088".
- **Tests:** none. `grep -c` over the 2 files → **2**. `dotnet build` → **0 errors**.

## IS19d — Documents + Bookmarks batch (4 types: `Document`, `DocumentFolder`, `Bookmark`, `BookmarkFolder`)

- **Files (4):** `Documents/Document.cs`, `Documents/DocumentFolder.cs`, `Bookmarks/Bookmark.cs`, `Bookmarks/BookmarkFolder.cs`.
- **Values (verbatim §2.5):** all four `PrivateContent/Delete/Resident`. Reasons: `Document` "F1 §2A: the resident's uploads"; `DocumentFolder` "F1 §2A"; `Bookmark` "F1 §2A"; `BookmarkFolder` "F1 §2A; the E6 gap this lane closes (IS31)".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS19e — Inventory + PersonalNotes batch (3 types annotated: `InventoryItem`, `PersonalNote`, `NoteFolder`; 1 paused: `InventoryCheckout`)

- **Files (3):** `Inventory/InventoryItem.cs`, `PersonalNotes/PersonalNote.cs`, `PersonalNotes/NoteFolder.cs`.
- **Values (verbatim §2.5):** all three `PrivateContent/Delete/Resident`. Reasons: `InventoryItem` "F1 §2A"; `PersonalNote` "F1 §2A; M37 / ADR 0167; the E6 gap this lane closes (IS31)"; `NoteFolder` "F1 §2A; the E6 gap this lane closes (IS31)".
- **⛔ PAUSE — `Inventory/InventoryCheckout.cs` NOT annotated.** The §2.5 row is `b | Pseudonymize (the resident's identity is erased, the checkout state is kept for stock accuracy) | Never` but **names no principal field** — the exact IS17 `Profile` pause (`/memories/repo/dataclass-annotation-convention.md`). IS16's `Pseudonymize_RequiresPrincipalField` invariant would throw if annotated without one, and guessing is not allowed (rules 1–9). `BorrowerId` (`InventoryCheckout.cs:30`) is the candidate principal field for the owner / IS21 to confirm. Recorded here per the plan; the type stays unannotated, so IS20's 81/81 coverage gate will surface it until it is resolved.
- **Tests:** none. `grep -c` over the 3 annotated files → **3**. `dotnet build` → **0 errors**.

## IS19f — Media + Kiosk batch (2 types: `MediaObject`, `KioskPlaylist`)

- **Files (2):** `Media/MediaObject.cs`, `Kiosk/KioskPlaylist.cs`.
- **Values (verbatim §2.5):** `MediaObject` `Media/Delete/Never` reason "F1 §2A: the catalog rows + their bytes (U4's set); the bytes live on a dedicated volume, not in the export"; `KioskPlaylist` `Operational/Delete/Never` reason "a per-resident display preference (Id = the resident's subject id, ADR 0164/0285); the resident's own playlist config; IS22's `ResidentAccountRowsEraser` adds the delete".
- **Tests:** none. `grep -c` over the 2 files → **2**. `dotnet build` → **0 errors**.

## IS19 — batch summary (19 of the 20 listed types annotated, 1 paused)

- **Total:** `grep -rc "\[DataClass(" src --include="*.cs"` → **53** (34 after IS18 + 19 here). Every annotated file has exactly one `using Kumunita.Core.Privacy;` (verified: none of the 19 files imported it before). 17 `Delete` rows + 2 `Keep` cascade rows (`KanbanLane`, `BoardItemPlacement`, both with non-empty `reason`); no `Pseudonymize` annotated (the one such row is the paused `InventoryCheckout`).
- **Conventions held (per `/memories/repo/dataclass-annotation-convention.md`):** no `principalField` anywhere in the batch (Pseudonymize-only), no `retentionKey` anywhere (every §2.5 "Retention key" cell in the batch is `—`), `reason:` verbatim from §2.5.
- **Pause:** `InventoryCheckout` left unannotated (no principal field named in §2.5 — IS17 `Profile` precedent; `BorrowerId` is the candidate for the owner / IS21). No hand-rolled data-class comment was superseded in any file (none carried one, verified per the plan).
- **No test added, no eraser/export touched, no type outside the batch annotated.** `dotnet build` (solution) → **0 errors** (160 warnings, all pre-existing — unchanged by this lane).
- **Next unit:** IS20 (Messaging / GroupMessaging / Notifications / Sessions / Usage / ErrorReports / SiteContent / SurfaceLabels / Escalation / Onboarding / Community* batch — **plus** the `EveryRegisteredDocType_HasADataClass` 81/81 coverage gate; note `InventoryCheckout` and IS17's `Profile` will both need resolution before that gate can go green).

## IS20a — Messaging + GroupMessaging threads (4 types: `Conversation`, `Message`, `GroupConversation`, `GroupMessage`)

- **Files (4):** `Messaging/Conversation.cs`, `Messaging/Message.cs`, `GroupMessaging/GroupConversation.cs`, `GroupMessaging/GroupMessage.cs`.
- **Values (verbatim §2.5):** `Conversation` `PrivateContent/Keep/Resident` reason "D3: the other participant's thread must stay coherent; IS21's `ResidentThreadPseudonymizer` does the tombstone"; `Message` `PrivateContent/Pseudonymize/Resident` reason "D3: 1:1 direct messages survive under the tombstone, not deleted (IS21)" `principalField: "SenderId"`; `GroupConversation` `PrivateContent/Keep/Resident` reason "D3: group threads stay for the other participants (IS21)"; `GroupMessage` `PrivateContent/Pseudonymize/Resident` reason "D3 (IS21); shipped after F1 — the E2 gap this lane closes" `principalField: "SenderId"`.
- **Tests:** none (pure annotation). `grep -c "[DataClass("` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS20b — Invitation + 3 Notifications (4 types: `GroupMessagingInvitation`, `Notification`, `NotificationPreference`, `NotificationSubscription`)

- **Files (4):** `GroupMessaging/GroupMessagingInvitation.cs`, `Notifications/Notification.cs`, `Notifications/NotificationPreference.cs`, `Notifications/NotificationSubscription.cs`.
- **Values (verbatim §2.5):** `GroupMessagingInvitation` `PrivateContent/Delete/Never` reason "an invitation to a thread; deleting the account drops the pending invite (IS21)"; `Notification` `Operational/Delete/Never` reason "F1 §2C decision (now decided): received per-resident state, not authored (IS22); D6: read 90 days, unread 365 days" `retentionKey: "NotificationUnreadDays"` (the **unread** key — the longer-living, per the prompt's convention: the attribute takes a single key, so the read key 90 stays in the reason text verbatim); `NotificationPreference` `Operational/Delete/Never` reason "F1 §2C decision (now decided): the resident's own preference (IS22)"; `NotificationSubscription` `Operational/Delete/Never` reason "F1 §2C decision (now decided): the resident's own subscription (IS22)".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS20c — QuietSchedule + SessionDoc + Usage (4 types: `NotificationQuietSchedule`, `SessionDoc`, `UsageEvent`, `CommunityStorageSettings`)

- **Files (4):** `Notifications/NotificationQuietSchedule.cs`, `Sessions/SessionDoc.cs`, `Usage/UsageEvent.cs`, `Usage/CommunityStorageSettings.cs`.
- **Values (verbatim §2.5):** `NotificationQuietSchedule` `Operational/Delete/Never` reason "F1 §2C decision (now decided): the resident's own schedule (IS22)"; `SessionDoc` `Pii/Delete/Never` reason "F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry" `retentionKey: "SessionDays"`; `UsageEvent` `Operational/Pseudonymize/Never` reason "F1 §2C excluded (per-user storage row / usage history): derived metrics, not content; the resident's identity is erased, the metric stays (IS23)" `principalField: "ActorId"`; `CommunityStorageSettings` `Operational/Keep/Never` reason "the community's storage cap; no resident principal" (per §2.5, not the prompt-table's "a sample of platform storage" — §2.5 is the source of truth).
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.
- **Note:** the `CommunityStorageSettings` reason fix was landed and committed inside IS20f's commit (the `git commit --amend --no-edit` amended HEAD, which at that point was IS20f — the file content is correct either way; this is a commit-attribution detail, not a code defect).

## IS20d — StorageSample + ErrorReports (4 types: `StorageMetricsSample`, `ErrorReport`, `EscalationToken`, `EscalationOutboundConfig`)

- **Files (4):** `Usage/StorageMetricsSample.cs`, `ErrorReports/ErrorReport.cs`, `ErrorReports/EscalationToken.cs`, `ErrorReports/EscalationOutboundConfig.cs`.
- **Values (verbatim §2.5):** `StorageMetricsSample` `Operational/Keep/Never` reason "a sample of platform storage; no resident principal"; `ErrorReport` `AuditModeration/Pseudonymize/Never` reason "F1 §2C decision (now decided): an operator-facing report the resident filed (IS23); D6: 180 days after resolution" `retentionKey: "ErrorReportDays"` `principalField: "SubjectId"`; `EscalationToken` `Secret/Pseudonymize/Never` reason "F1 §2C decision (now decided): an operator credential, but the `CreatedBy` is the resident's (IS23); D6: 7 days after revocation or expiry" `retentionKey: "TokenDays"` `principalField: "CreatedBy"`; `EscalationOutboundConfig` `Secret/Keep/Never` reason "the outbound webhook config; the `Token` is an operator secret, not a resident PII field".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS20e — SiteContent + SurfaceLabels (4 types: `SiteContent`, `SiteContentTranslation`, `SurfaceLabels`, `SurfaceLabelTranslation`)

- **Files (4):** `SiteContent/SiteContent.cs`, `SiteContent/SiteContentTranslation.cs`, `SurfaceLabels/SurfaceLabels.cs`, `SurfaceLabels/SurfaceLabelTranslation.cs`.
- **Values (verbatim §2.5):** all four `Platform/Keep/InstanceOnly`. Reasons: `SiteContent` "F1 §2C excluded: platform singletons, not resident content"; `SiteContentTranslation` "F1 §2C excluded: the site's hero text per language"; `SurfaceLabels` "F1 §2C excluded: the admin's surface labels"; `SurfaceLabelTranslation` "F1 §2C excluded: the labels per language".
- **Tests:** none. `grep -c` over the 4 files → **4**. `dotnet build` → **0 errors**.

## IS20f — Community / UserInfo edges (4 types: `CommunityMembershipRequest`, `GroupInvitation`, `GroupJoinRequest`, `GroupTranslation` + `CommunityTranslation`)

- **Files (4, the last holds 2 types):** `UserInfo/CommunityMembershipRequest.cs`, `UserInfo/GroupInvitation.cs`, `UserInfo/GroupJoinRequest.cs`, `UserInfo/Translations.cs`.
- **Values (verbatim §2.5):** `CommunityMembershipRequest` `AuditModeration/Delete/Never` reason "IS23: the request is the resident's; deleting the account drops it"; `GroupInvitation` `AuditModeration/Delete/Never` reason "F1 §2C decision (now decided): an identity-graph edge received; the row is stale once the account is gone"; `GroupJoinRequest` `AuditModeration/Delete/Never` reason "F1 §2C decision (now decided): an identity-graph edge submitted; the row is stale once the account is gone"; `GroupTranslation` `PrivateContent/Keep/InstanceOnly` reason "F1 §2A sub-entities: under `Group`; carries only `GroupId` + `LanguageCode`, no principal field (the `Group` row's `Keep(reason)` is what governs it)"; `CommunityTranslation` `PrivateContent/Keep/InstanceOnly` reason "F1 §2A sub-entities: under `Group`/`Component`; carries only `GroupId`/`ComponentId` + `LanguageCode`, no principal field".
- **Tests:** none. `grep -c` over the 4 files → **5** (Translations.cs counts 2). `dotnet build` → **0 errors**.

## IS20g — the platform onboarding singleton (1 type: `AdminOnboarding`)

- **Files (1):** `AdminOnboarding/AdminOnboarding.cs`.
- **Values (verbatim §2.5):** `AdminOnboarding` `Platform/Keep/Never` reason "the platform's onboarding state; the `CompletedAt` is an operator timestamp".
- **Tests:** none. `grep -c` over the file → **1**. `dotnet build` → **0 errors**.

## IS20h — the P-1 coverage gate (`EveryRegisteredDocType_HasADataClass`)

- **File (1 new):** `tests/Kumunita.Core.Tests/DataClassCoverageTests.cs`.
- **Location decision:** the plan's §2.3 names the gate in `tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs`, but IS16 landed its sibling catalog tests in `tests/Kumunita.Core.Tests/DataClassCatalogTests.cs`. **Chose the Core.Tests location for consistency** (the gate exercises `DataClassCatalog.For`/`All`, which live in Core; both test projects reference `Kumunita.Core`, so either compiles). D7's "gates live in SecGate" refers to the *other* gates (M1/M4/M7/inline-handler); this is the one P-1 coverage gate that belongs with the catalog tests it exercises. Recorded here per the plan.
- **Form (a) — red gate, chosen.** The assertion is strict (all 81 covered) and the test currently **fails**, naming the two unresolved pauses. It does **not** hard-code 79. D7: "start from the measured baseline and retire it to zero; never freeze it."
- **How it enumerates the 81:** builds one `StoreOptions`, calls the `Configure(StoreOptions)` method on each of the 24 `*DocTypes` classes via reflection, then reads `IReadOnlyStoreOptions.AllKnownDocumentTypes().Select(dt => dt.DocumentType)` (Marten 9.50.2; `PortabilityDocTypes` is excluded — it has no `Configure`, its rows are an inventory map, not Marten registrations). **Registered set = exactly 81** (verified: `grep -rhoE 'Schema.For<[^>]+>' src/Kumunita.Core --include=*.cs | sort -u` = 82, minus 1 false positive `Schema.For</c>` from a doc comment = 81).
- **Result: RED, 79/81, naming exactly the two pauses.** Failure message: `Uncovered: Kumunita.Core.Inventory.InventoryCheckout, Kumunita.Core.UserInfo.Profile`. This is the *correct, expected* state — IS17 paused `Profile` and IS19 paused `InventoryCheckout`, both `Pseudonymize` rows naming no principal field, so annotating them would throw on IS16's `Pseudonymize_RequiresPrincipalField`. **IS21 must resolve both** (name the principal field: `BorrowerId` for `InventoryCheckout` is the candidate; `Profile` candidates per IS17 — §2.6 Rule 3 blanked PII `Email`/`Phone`/`Address`), at which point the gate moves 79 → 81.
- **Tests:** `DataClassCatalogTests` (IS16, 3 tests) → **green**. `DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` → **red** (by design). `dotnet build` (solution) → **0 errors**.

## IS20 — batch summary (26 of the 26 listed types annotated, gate landed red at 79/81)

- **Total:** `grep -rc "\[DataClass(" src --include="*.cs"` → **79** (53 after IS19 + 26 here). Every annotated file has exactly one `using Kumunita.Core.Privacy;` (verified: none of the 25 files imported it before). `Translations.cs` carries **2** attributes + 1 `using`.
- **Batch shape (26 rows):** 9 `Delete` + 12 `Keep` (all with non-empty `reason`) + 5 `Pseudonymize`. **Exactly 5 `principalField`** rows in the batch: `Message`→`SenderId`, `GroupMessage`→`SenderId`, `UsageEvent`→`ActorId`, `ErrorReport`→`SubjectId`, `EscalationToken`→`CreatedBy` — **each verified to name a field that exists on its POCO** (checked against the class body before writing). **Exactly 4 `retentionKey`** rows in the batch: `Notification`→`NotificationUnreadDays` (the unread key; read key 90 kept in the reason), `SessionDoc`→`SessionDays`, `ErrorReport`→`ErrorReportDays`, `EscalationToken`→`TokenDays` — all bare `RetentionOptions` field names (design §2.2), not `Retention__*` config keys. No `Delete`/`Keep` row carries a `principalField`.
- **Conventions held (per `/memories/repo/dataclass-annotation-convention.md`):** `reason:` verbatim from §2.5; `retentionKey` = bare `RetentionOptions` field name; `principalField` Pseudonymize-only and field-verified. One §2.5-vs-prompt-table drift found and resolved in favor of §2.5: `CommunityStorageSettings` reason is "the community's storage cap; no resident principal" (not the prompt's "a sample of platform storage").
- **Paused (not annotated, per the plan — do not resolve here):** `Profile` (IS17) and `InventoryCheckout` (IS19) both stay unannotated; the gate names them. No hand-rolled data-class comment was superseded in any of the 25 files (verified: none carried one before).
- **No eraser/export touched, no paused type annotated, no type outside the 26 annotated, no new behaviour.** `dotnet build` (solution) → **0 errors** (160 warnings, all pre-existing — unchanged by this lane).
- **Splits landed as one commit each:** IS20a (4 files) → IS20b (4) → IS20c (4) → IS20d (4) → IS20e (4) → IS20f (4) → IS20g (1) → IS20h (1 gate file). All within D8's ≤ 4 files.
- **Next unit:** IS21 (Erasure: pseudonymize 1:1 and group threads, D3) — the unit that must also **resolve the two pauses** (`Profile`, `InventoryCheckout`) by naming their principal fields, so IS20h's gate can move 79 → 81.
