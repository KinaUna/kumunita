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
