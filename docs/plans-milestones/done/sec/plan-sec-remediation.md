# SEC — Security-audit remediation — sealed unit register

## Understanding
A static security review of `main` @ `bdcaff7` (2026-10-30; the findings table is reproduced in U1, nothing was executed) found **3 High, 5 Medium and 8 Low** issues. The authorization core, CSP/XSS posture, SQL parameterization, file-path handling and MFA primitives held up; the gaps are places where code drifted from `docs/SECURITY.md`. This lane closes every finding with **one regression test per finding**, in units small enough for a fresh agent with a **32K–64K token window**.

The three Highs are:
- **H1** — the resident-data import (`UserPortabilityService`) `session.Store`s archive rows with no ownership or `Id`-collision check.
- **H2** — GlobalAdmin 2FA is a redirect, not a gate (`AccountController.Login`).
- **H3** — `app.UseForwardedHeaders()` has no options, so it does nothing: one shared rate-limit bucket, no HSTS, cookie not `Secure`.

## Assumptions
- **Scope:** all of H1–H3, M1–M5, L1–L8, except where a unit says *parked*. **Parked → owner decision:** L1 (uniform signup/login responses; a privacy-vs-UX call) and the open-ended log-forging item in L8. Both stay as written units but are not on the critical path.
- **Fixes are minimal and local.** No new module, no new document type, no migration. New code is small static helpers, middleware or options classes, each with a pure unit test where possible.
- **Frozen seams stay frozen** (ADR 0006): no change to `IAuthorizationService`, `IUserInfoService` or `IIdentityService` signatures. L3 touches `IdentityService` internals only.
- **Open vetoes** (defaults are pinned in U2; the owner may flip any before the unit that uses it starts):
  1. H1 — `GroupMembership`, `GroupInvitation`, `GroupJoinRequest` rows are **not importable** (they grant access); `Group` is importable only when `OwnerId == actor` and the `Id` is unused; `Profile` only when `SubjectId == actor` (overwrites the actor's own profile).
  2. H2 — enforce with a **server-side gate** (middleware) that allows only the enrolment/logout/static routes to an un-enrolled GlobalAdmin, rather than a pending cookie scheme. Sample-data accounts keep their existing exemption.
  3. H3 — trusted proxies come from config (`ReverseProxy:KnownNetworks`, default loopback + RFC1918 ranges, i.e. the Docker network); unset/empty never means "trust everyone".
  4. M4 — `/health` returns a minimal payload to anonymous callers when `Health__Token` is unset; the full payload needs the token or GlobalAdmin.
  5. M5 — a cookie with no stamp is rejected once `Security:RevocationStamp:RequiredAfter` (UTC date config) has passed; default = deploy date + 14 days, so existing sessions are not logged out at once.
- **Environment limits:** a unit that needs the network (resolving image digests / action SHAs, advisory scans) records "blocked" in its handoff note rather than guessing values.
- **Test model:** every finding gets named regression tests pinned in the design doc Part 2; a three-test acceptance gate (hostile-import / admin-without-2FA / part-vs-whole) is recorded in U29.

## Approach
Six phases, sequenced so that each fix lands before anything that depends on it:
**0 Foundations** (design doc, baseline) → **A H1** → **B H2** → **C H3** → **D Mediums** → **E Lows** → **F Close**. Within a phase, units are strictly ordered; across phases only the dependencies listed on the unit matter (marked **Needs**).

Every unit ends with **build green** and a handoff note. Units that touch behaviour end with their **own pinned tests green**; the **full suites run only in U3 and U29**.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U1–U31 below), one unit per fresh agent with a **32K–64K context window**. An agent starts with nothing but this file, the design doc and the handoff note.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/sec-remediation-design.md`): Part 1 = context, scope, invariants, findings register; Part 2 = exact fix shapes, signatures, config keys, test names, acceptance gate, drift-guard. U1/U2 write it; U3–U31 match it.
- **Secondary — this file** (`docs/plans-milestones/in-progress/sec/plan-sec-remediation.md`): the unit registry.
- **Scratch — the rolling handoff note** (`docs/plans-milestones/in-progress/sec/sec-handoff-notes.md`): one section per unit, appended and never rewritten. A unit reads only **its own entry list plus the last 1–2 sections it names as `Needs`**.

**Token budget per unit (≈ 45K target):** entry reads ≤ ~20K · edits and thinking ≤ ~20K · handoff note ≤ ~1K. To stay inside it:
- **Never read these whole:** `Program.cs` (1,336 lines), `AccountController.cs` (1,200), `IdentityService.cs` (1,825), `UserPortabilityService.cs` (1,329), `PostsController.cs` (2,106). Each unit names a `sed -n 'A,Bp'` range; read only that range. Use `grep -n` to re-find anchors, since line numbers drift after edits.
- Run only the unit's own tests: `dotnet test <project> --no-build --filter "FullyQualifiedName~<Class>"` (U3 confirms the exact working command and records it in the handoff note; later units use that one). Build the touched project only: `dotnet build src/<Project> -c Debug`.
- If an entry read is longer than expected, read less (grep, then a range). A unit never "just reads the whole file to be safe".

**Per-unit template:** **Goal** (one sentence) · **Needs** (earlier handoff sections) · **Entry reads** (≤ 5 items, ranges for large files) · **Deliverables** (a closed file set, ≤ ~4 files / ~600 LOC) · **Exit** (build green, own tests green, handoff note appended *before* finishing).

**Handoff note shape** (3–8 lines, `## U<n> — <title>`): (a) files changed, (b) public names/config keys added, exact, (c) test names run + pass count, (d) anything the next unit must know, (e) warnings or skipped items.

**Unit-series rules:**
1. A unit never edits a file that is not in its own `Deliverables`.
2. Never rewrites the design doc outside the drift-guard (U2's section).
3. Never adds a test whose exact name is not in the Part 2 list.
4. Never changes a frozen interface signature (ADR 0006).
5. Never "fixes" an unrelated finding it notices; it records it under `## Noticed` in its handoff note.
6. If entry reads contradict the design doc (a line moved, a method renamed, a finding already fixed), the unit pauses and writes `## U<n> — Drift pause`; it does not improvise.
7. **No PII and no secrets** in code comments, tests, notes or audit rows; use synthetic ids and emails (`user-a`, `a@example.test`).
8. Test fixtures build hostile inputs in code (in-memory zips, fake principals); no binary fixtures are checked in.
9. A fix that makes an existing test fail is **not** edited around: the unit records the test name and why, and pauses. Old tests that assert the insecure behaviour are updated only when the Part 2 list names them.

---

## Units (31 total)

### Phase 0 — Foundations

#### U1 — Design doc Part 1
- **Goal:** write `docs/design/sec-remediation-design.md` Part 1 — Context, Scope, Invariants, Findings register. **No code, no build.**
- **Entry reads:** this file (Understanding/Assumptions), `docs/SECURITY.md` §5–§6 (control map + open items), `docs/SECURITY-AUDIT.md` §3 Checklist C + §8 (the audit-log row format), `docs/philosophy/templates/design-doc.md`, and the **Appendix A — Findings register** at the end of this file (the source for the register table; line numbers there are from `bdcaff7` and are re-verified in U2).
- **Deliverables (1 new file, ~180 lines):**
  - `## Context`: what the audit was and was not (static, `bdcaff7`, nothing run).
  - `## Scope`: in = H1–H3, M1–M5, L1–L8; parked = L1, log-forging (L8c); out = new features, dependency upgrades, anything not in the register.
  - `## Invariants`: **S-1** an import never creates or changes a row the actor does not own; **S-2** a GlobalAdmin session cannot reach any route but enrolment/logout without 2FA; **S-3** client IP and scheme come only from a configured trusted proxy; **S-4** every state-changing request validates an anti-forgery token unless explicitly token-authenticated; **S-5** every anonymous write has a rate limit and a size cap; **S-6** a session whose stamp is missing or stale is rejected after the cut-off; **S-7** user-controlled content never produces an off-site link or an unvalidated file type; **S-8** every fix has a named regression test.
  - `## Findings register`: one row per finding: id, severity, file:line from the audit (marked *re-verify in U2*), invariant, owning unit.
- **Exit:** file exists, tables render, no build. Handoff note: 3–4 lines starting `## U1 — design Part 1`: invariant ids, register row count (must be 16 plus the two parked sub-items).

#### U2 — Design doc Part 2 (pins)
- **Goal:** pin every fix shape, config key, signature and test name, and re-verify every file:line in the register against the current tree. **No product code.**
- **Needs:** U1.
- **Entry reads:** `docs/design/sec-remediation-design.md` (Part 1), and these **greps** (run them, do not read whole files): `grep -rn "SignInAsync(" src --include=*.cs`, `grep -rn "EnableRateLimiting" src/Kumunita.Web/Controllers/IssueController.cs src/Kumunita.Web/Controllers/HomeController.cs`, `grep -rn "escalations/inbound\|api/mail/suppress" src --include=*Controller.cs`, `grep -n "FixedTimeEquals\|breakGlass\|BreakGlass" src/Kumunita.Core/Identity/IdentityService.cs`, `sed -n '1,60p' src/Kumunita.Web/Security/UploadGate.cs`.
- **Deliverables (1 file, modify):** the design doc, append **Part 2**:
  - **2.1 Re-verified register**: corrected file:line for every finding; pins the exact paths for the escalation-inbound and mail-suppress controllers, the break-glass compare site and the five upload sites.
  - **2.2 Fix shapes** (one short paragraph per unit U4–U28; the `Open vetoes` defaults restated as decisions): class and method names, config keys, and the exact signature of every new public member. Planned names (U2 may refine): `ImportOwnershipGuard.Check(string type, object row, string actorSubjectId, Func<string,string?> existingOwnerOf)` → `ImportRowVerdict`; `AdminTwoFactorMiddleware`; `ForwardedHeadersSetup.AddKumunitaForwardedHeaders(IServiceCollection, IConfiguration)`; `ArchiveLimits` (max entries, max entry bytes, max total bytes); `UploadSignatureSniffer.Matches(string declaredContentType, ReadOnlySpan<byte> head)`.
  - **2.3 Test names** (exact, one list per unit — see "Pinned test names" at the end of this file for the seed list; U2 completes it and fixes class names/projects).
  - **2.4 Acceptance gate** (three tests; U29).
  - **2.5 Drift-guard**: the rule that a unit pauses on contradiction, and the list of anchors (method names) each unit greps for.
- **Exit:** Part 2 complete; every unit has a test-name list or an explicit "no new test, because …". Handoff note starting `## U2 — design Part 2`: list of corrected line refs and any finding that was **already fixed or not reproducible** (those units become no-ops that record "n/a" and exit).

#### U3 — Baseline: build, test, dependency scan
- **Goal:** record a trustworthy baseline so later units can tell "I broke it" from "it was already red", and run the NuGet vulnerability scan the audit could not.
- **Needs:** U2.
- **Entry reads:** `docs/design/sec-remediation-design.md` Part 2 §2.4 only; `ls tests`; the repo `README` build section (≤ 60 lines).
- **Deliverables (no code):** the handoff note section only. Commands: `dotnet build Kumunita.slnx -c Debug`; both test projects (Core tests need Docker for Testcontainers; if Docker is absent, record "Core.Tests: not runnable here" and which test classes are Postgres-backed); `dotnet list package --vulnerable --include-transitive`; `npm audit` in the web client folder.
- **Exit:** handoff note `## U3 — baseline` with: exact working commands (build, per-class test filter for each test project), total/passed/failed per project, names of pre-existing failures, the vulnerability scan output summarized (package, version, severity, advisory id) or "blocked: <reason>". **If any pre-existing failure exists, later units treat it as the baseline, never as their regression.**

### Phase A — H1: resident import trusts the archive

#### U4 — `ImportOwnershipGuard` (pure) + tests
- **Goal:** one pure, reflection-free-at-call-site guard that decides whether an archive row may be imported for an actor.
- **Needs:** U2.
- **Entry reads:** design Part 2 §2.2 (H1 paragraph), `src/Kumunita.Core/Portability/UserScopeInventory.cs` lines 60–120 (the `Entries` table: ownership fields) and 130–159 (`IsInScope`), one existing small test file in `tests/Kumunita.Core.Tests/` for the xunit style (e.g. `UserPortabilityResolveModelTests.cs`, first 60 lines).
- **Deliverables (≤ 2 files, new):**
  - `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`: `ImportRowVerdict` (`Allowed` / `Rejected(reason)`), and `Check(...)`. Rules: type must be in `UserScopeInventory.Entries`; every `OwnershipFields` value must equal `actorSubjectId` (for `Conversation`: actor must be `ParticipantA` or `ParticipantB`); `Message` has no direct field, so it is allowed only when its `ConversationId` is a conversation the actor is in (the caller supplies `IReadOnlySet<string> actorConversationIds`); type policy from the veto: `GroupMembership`/`GroupInvitation`/`GroupJoinRequest` → always `Rejected("access-granting row")`; `Group` → `OwnerId == actor`; if `existingOwnerOf(row.Id)` returns an owner different from the actor → `Rejected("id collision")`.
  - `tests/Kumunita.Core.Tests/ImportOwnershipGuardTests.cs`: the pinned names for U4 (see end of file).
- **Exit:** `dotnet build src/Kumunita.Core` green; the new test class green. Handoff note `## U4 — guard`: the public signature verbatim (U5/U6 call it), the reason strings (they become user-visible codes, so list them), pass count.

#### U5 — Wire the guard into `ClassifyAsync`
- **Goal:** classification marks hostile rows as rejected, so they never appear as importable.
- **Needs:** U4 (signature + reason strings).
- **Entry reads:** `UserPortabilityService.cs` `grep -n "ClassifyAsync"` then `sed -n '705,860p'` (the classify loop); `src/Kumunita.Core/Portability/UserPortabilityImportPlan.cs` (or wherever `UserPortabilityEntityStatus` is declared: `grep -rn "enum UserPortabilityEntityStatus" src`); `ImportOwnershipGuard.cs`; `tests/Kumunita.Core.Tests/UserPortabilityClassifyTests.cs` lines 40–140 (the Postgres fixture shape).
- **Deliverables (≤ 3 files):** `UserPortabilityService.cs` (classify only); the status enum file **only if** a `Rejected` status does not already exist (add the member; do not renumber); `tests/Kumunita.Core.Tests/UserPortabilityHostileClassifyTests.cs` (new, U5's pinned names).
- **Exit:** build green; new tests + `UserPortabilityClassifyTests` + `UserPortabilityAcceptanceTests` green (the existing classify tests must still pass unchanged). Handoff note `## U5 — classify guard`: the status/reason shape exposed in `UserPortabilityImportPlan`, and whether the Web resolve view needs to render it (U6 decides).

#### U6 — Enforce at write time in `ResolveAsync`
- **Goal:** defense in depth — `ResolveAsync` re-runs the guard on every row immediately before `session.Store`, so a plan tampered with between classify and apply (or a forged resolve form) cannot write a foreign row.
- **Needs:** U4, U5.
- **Entry reads:** `UserPortabilityService.cs`: `grep -n "session.Store(row)"` then `sed -n '1060,1150p'`; `ImportOwnershipGuard.cs`; the U5 handoff section; `tests/Kumunita.Core.Tests/UserPortabilityResolveTests.cs` lines 1–80.
- **Deliverables (≤ 3 files):** `UserPortabilityService.cs` (resolve only: guard before both `session.Store(row)` sites; on `Rejected`, skip the row and add `import.rejected:<reason>` to the result's message list, and count it); `tests/Kumunita.Core.Tests/UserPortabilityHostileResolveTests.cs` (new); optionally `Kumunita.Web/Views/UserPortability/*.cshtml` **only if** U5's note says the review page must show rejected rows (then ≤ 1 view file).
- **Exit:** build green; hostile-resolve tests green, plus `UserPortabilityResolveTests`, `PortabilityRoundTripTests` (a genuine round-trip of the actor's own data still imports). Handoff note `## U6 — resolve guard`: the three attack scenarios now covered (forged author, `Id` collision, foreign membership) and the round-trip result.

### Phase B — H2: GlobalAdmin 2FA

#### U7 — `AdminTwoFactorMiddleware` + tests
- **Goal:** a middleware that, for an authenticated GlobalAdmin without enrolled 2FA (and not a sample-data account), allows only the enrolment, logout and static routes and redirects everything else to enrolment.
- **Needs:** U2.
- **Entry reads:** `src/Kumunita.Web/Security/RevocationStampMiddleware.cs` lines 1–110 (the house middleware shape and how it reads the principal); `SecuritySettingsController.cs`: `grep -n "HttpGet\|HttpPost\|Route(" ` (the enrolment route paths) and `sed -n '255,300p'` (`Mfa()`); `AccountController.cs` `sed -n '893,925p'` (what the login currently does for this case); `grep -n "IsSampleDataAccountAsync" src/Kumunita.Core/Identity/IIdentityService.cs`.
- **Deliverables (≤ 2 files, new):** `src/Kumunita.Web/Security/AdminTwoFactorMiddleware.cs` (reads the role from the `Kumunita.Role` claim, `TwoFactorEnabled` through `UserManager`/`IIdentityService`; a small allow-list of path prefixes taken from the pinned enrolment routes); `tests/Kumunita.Web.Tests/AdminTwoFactorMiddlewareTests.cs`.
- **Exit:** `dotnet build src/Kumunita.Web` green; new tests green. Handoff note `## U7 — admin 2FA gate`: the class name, constructor dependencies (U8 registers it), the allow-listed routes, the sample-account exemption mechanism.

#### U8 — Register the gate and reconcile the login flow
- **Goal:** put the middleware in the pipeline after authentication and before authorization, and make `Login` and docs agree with it.
- **Needs:** U7.
- **Entry reads:** `Program.cs`: `grep -n "UseAuthentication\|UseAuthorization\|RevocationStampMiddleware"` then a ±15-line range around the hits; `AccountController.cs` `sed -n '880,925p'`; `docs/SECURITY.md`: `grep -n "2FA\|two-factor" docs/SECURITY.md` (then only the hit lines ±5); the U7 handoff section.
- **Deliverables (≤ 3 files):** `Program.cs` (one `app.UseMiddleware<AdminTwoFactorMiddleware>()` line + a comment); `AccountController.cs` (only the comment/behaviour at the redirect so it states that the redirect is now a convenience, not the control); `docs/SECURITY.md` (the 2FA line: names the middleware as the control).
- **Exit:** build green; `AdminTwoFactorMiddlewareTests` plus `AccountControllerSignupGateTests` and `AdminControllerSetRoleTests` green (nothing else regresses). Handoff note `## U8 — admin 2FA wired`: pipeline position, and the route list an un-enrolled admin can still reach.

### Phase C — H3: forwarded headers

#### U9 — `ForwardedHeadersSetup` + tests
- **Goal:** explicit `ForwardedHeadersOptions` (`XForwardedFor | XForwardedProto`, trusted networks from config, never "trust all").
- **Needs:** U2.
- **Entry reads:** `Program.cs` `sed -n '936,956p'` and `sed -n '36,48p'`; `src/Kumunita.Web/appsettings.json` (whole, ~20 lines); `docs/COOLIFY.md`: `grep -n "proxy\|Proxy\|Forwarded\|Caddy\|Traefik" docs/COOLIFY.md | head -20`.
- **Deliverables (≤ 2 files, new):** `src/Kumunita.Web/Security/ForwardedHeadersSetup.cs` (extension `AddKumunitaForwardedHeaders`; parses `ReverseProxy:KnownNetworks` CIDR strings and `ReverseProxy:KnownProxies` IPs; defaults per the veto; sets `ForwardLimit = 1`; throws a clear `InvalidOperationException` on an unparsable CIDR so a typo fails the boot, not open); `tests/Kumunita.Web.Tests/ForwardedHeadersSetupTests.cs` (builds the options in isolation and drives a `DefaultHttpContext` through `ForwardedHeadersMiddleware`: trusted peer is honored; untrusted peer's `X-Forwarded-For` is ignored).
- **Exit:** build green; new tests green. Handoff note `## U9 — forwarded headers setup`: config keys and defaults (U10 documents them), method name.

#### U10 — Wire it, make the cookie `Secure`, document it
- **Goal:** call the setup from `Program.cs`, mark the auth and antiforgery cookies `Secure` in production, and write the operator-facing verification steps.
- **Needs:** U9.
- **Entry reads:** `Program.cs`: `sed -n '36,48p'`, `sed -n '640,680p'` (cookie/Identity options, `SecurityStampValidatorOptions`), `sed -n '936,956p'`; `grep -n "Cookie\." src/Kumunita.Web/Program.cs`; `docs/COOLIFY.md` around the U9-grepped proxy lines; `docs/SECURITY.md` §6 lines 160–175.
- **Deliverables (≤ 4 files):** `Program.cs` (register + `Cookie.SecurePolicy = CookieSecurePolicy.Always` outside Development, same for the antiforgery cookie); `docs/COOLIFY.md` (a short "Verify forwarded headers" checklist: expected response headers `Strict-Transport-Security`, `Set-Cookie ...; secure`, and a login-lockout test from two IPs); `docs/SECURITY.md` §6 (proxy trust decision); `tests/Kumunita.Web.Tests/CookieSecurePolicyTests.cs` only if the options can be resolved from a built `ServiceProvider` without booting the app (else record "no test, because the host cannot be built in unit scope" and rely on U9's tests).
- **Exit:** build green; U9 tests + the new test green. Handoff note `## U10 — H3 wired`: **the live-check steps an operator must run after deploy** (this finding depends on production config, which no test proves).

### Phase D — Medium findings

#### U11 — Anti-forgery: make the seven forms and fetches send a token
- **Goal:** before enforcing globally, make sure every client of the seven un-validated actions already sends a token.
- **Needs:** U2.
- **Entry reads:** the seven actions (`CommunityController` `SetMandatory`/`AddMember`/`RemoveMember`/`Leave`, `MessagesController` `Open`/`Send`, `PostsController` `Replies`): use `grep -n` on each route attribute; `grep -rn "community/manage\|/leave\|messages/open\|/replies" src/Kumunita.Web/Views src/Kumunita.Web/client --include=*.cshtml --include=*.ts` to find their forms and fetches; `src/Kumunita.Web/client/lib/api.ts` lines 1–60.
- **Deliverables (≤ 4 files; U2 pins the exact view/ts list):** the Razor views and/or TypeScript modules for those forms. A `<form method="post" action="…">` with a raw `action` does **not** get an auto token; use `asp-controller`/`asp-action` or add `@Html.AntiForgeryToken()`; fetches use `api.ts`. The actions themselves are **not** edited here.
- **Exit:** `dotnet build src/Kumunita.Web` + `tsc` green (use the build script in `package.json`). Handoff note `## U11 — clients send tokens`: a table `form → file → fix`, and any client the agent could not find (U12 must not enforce until that is resolved).

#### U12 — Global `AutoValidateAntiforgeryToken` + coverage test
- **Goal:** one global filter, with `[IgnoreAntiforgeryToken]` only on the two token-authenticated endpoints.
- **Needs:** U11.
- **Entry reads:** `Program.cs` `sed -n '36,48p'`; the escalation-inbound and mail-suppress controllers (paths pinned in U2; read only the action signature ±10 lines); the U11 handoff table.
- **Deliverables (≤ 4 files):** `Program.cs` (`AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))`); the two token-authenticated controllers (add `[IgnoreAntiforgeryToken]` to the action, with a one-line reason comment); `tests/Kumunita.Web.Tests/AntiforgeryCoverageTests.cs` (reflection over every controller action with `HttpPost/Put/Delete/Patch`: it must have `[ValidateAntiForgeryToken]`, or be covered by the global filter and not carry `[IgnoreAntiforgeryToken]` unless it is in a closed allow-list of exactly two).
- **Exit:** build green; coverage test green; **the existing controller test classes that touch the seven actions still pass**. Handoff note `## U12 — antiforgery enforced`.

#### U13 — Archive read limits (zip bomb)
- **Goal:** `KumunitaArchive.ReadAsync` enforces entry-count, per-entry and total decompressed size limits while streaming, and rejects duplicate entry names.
- **Needs:** U2.
- **Entry reads:** `src/Kumunita.Core/Portability/KumunitaArchive.cs` lines 255–304 (`ReadAsync`/`ReadEntryBytesAsync`); `grep -rn "KumunitaArchive.ReadAsync" src` (all callers, to see the catch sites); `tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs` lines 1–60 (how archives are built in tests).
- **Deliverables (≤ 3 files):** `KumunitaArchive.cs` (an optional `ArchiveLimits` parameter with safe defaults, e.g. 10,000 entries, 100 MB per entry, 500 MB total — U2 pins the numbers; the copy loop counts real bytes read rather than trusting `entry.Length`; throws `InvalidDataException`); `src/Kumunita.Core/Portability/ArchiveLimits.cs` (new record); `tests/Kumunita.Core.Tests/KumunitaArchiveLimitsTests.cs` (builds an in-memory zip of repeated bytes that expands past the limit; entry-count; duplicate name; a normal archive still reads).
- **Exit:** build green; new tests + `PortabilityRoundTripTests` green. Handoff note `## U13 — archive limits`: default values, and the callers' behaviour on `InvalidDataException` (U14 depends on it surfacing as a user error, not a 500).

#### U14 — Imported media gate + rate limit on the import actions
- **Goal:** imported media goes through the same size/quota/type checks as an upload, and the two import actions get the `upload` rate-limit policy.
- **Needs:** U13.
- **Entry reads:** `src/Kumunita.Web/Security/UploadGate.cs` (whole file; pinned size in U2, expected < 150 lines); `UserPortabilityService.cs` `grep -n "mediaStore.PutAsync"` then `sed -n '1160,1185p'`; `UserPortabilityController.cs` lines 120–135 and 248–260 (the action attributes); the U13 handoff section.
- **Deliverables (≤ 4 files):** `UserPortabilityService.cs` (media block only; if the gate lives in the Web project and Core cannot reference it, U2 pins the seam: add a small Core interface `IImportMediaPolicy` with one method and implement it in Web; this is the **only** new interface in the lane); `UserPortabilityController.cs` (`[EnableRateLimiting("upload")]` and `[RequestSizeLimit]` on `import` and `import/resolve`); the Web implementation file if needed; `tests/Kumunita.Core.Tests/UserPortabilityMediaGateTests.cs` (oversized and disallowed-type media are skipped with a reason; quota respected).
- **Exit:** build green; new tests + `UserPortabilityControllerTests` green. Handoff note `## U14 — import media gate`.

#### U15 — Anonymous writes: rate limits and size caps
- **Goal:** every anonymous state-storing POST has a rate-limit policy, a bounded text field and a request-size cap.
- **Needs:** U2.
- **Entry reads:** `Program.cs` `sed -n '610,640p'` (the policy list: `report`, `message`, `write`, `upload`…); `HomeController.cs` `sed -n '255,300p'` (`ErrorReport`); `IssueController.cs` lines 40–110; `Models/ErrorReportFormModel.cs`; the pinned escalation-inbound/mail-suppress action signatures (U2).
- **Deliverables (≤ 4 files):** the actions' attributes (`[EnableRateLimiting(...)]`, `[RequestSizeLimit(...)]`) on `/Home/Error/Report`, `/issues/new`, `/escalations/inbound`, `/api/mail/suppress` — **U2 first checks which already have a limiter and skips those**; `[MaxLength(4000)]` on `ErrorReportFormModel.Description` and on the issue form's free-text fields; if a new policy name is needed it goes in `Program.cs` as one `AddWindow` line; `tests/Kumunita.Web.Tests/AnonymousWriteLimitsTests.cs` (reflection: each listed action carries the attribute; model validation rejects 4,001 characters and accepts 4,000).
- **Exit:** build green; new tests + `ErrorReportPageTests` + `IssuePageTests` green. Handoff note `## U15 — anonymous writes`: the final policy per route.

#### U16 — `/health`: minimal by default, cached probe, constant-time compare
- **Goal:** anonymous callers get a minimal payload unless a token is configured and presented; the SMTP probe result is cached; the token compare is constant-time.
- **Needs:** U2.
- **Entry reads:** `HealthController.cs` (whole, 128 lines); `tests/Kumunita.Web.Tests/` `grep -ln "HealthController" tests/Kumunita.Web.Tests/*.cs` (read only the first hit's setup, ≤ 80 lines); `docs/COOLIFY.md` `grep -n "Health__Token\|/health"`.
- **Deliverables (≤ 3 files):** `HealthController.cs` (anonymous + token-unset → minimal; `CryptographicOperations.FixedTimeEquals` on UTF-8 bytes with length handled; SMTP probe cached ~60 s in a small static/`IMemoryCache`); the existing health test file (update **only** the tests named in Part 2 as asserting the old default); `docs/COOLIFY.md` (the setting and the new default).
- **Exit:** build green; health tests green. Handoff note `## U16 — health`: the new default and the cache key/TTL.

#### U17 — Stamp every sign-in path
- **Goal:** the email-verification sign-in (`ConfirmAndSignInAsync`) and the one in `SecuritySettingsController` attach the `kumunita.stamp` the revocation middleware expects.
- **Needs:** U2.
- **Entry reads:** `AccountController.cs` `sed -n '685,745p'` (`ConfirmAndSignInAsync`); `SecuritySettingsController.cs` `sed -n '225,262p'`; `RevocationStampMiddleware.cs` `grep -n "kumunita.stamp"` and lines 100–168; how `SignInManager` paths set it: `grep -rn "kumunita.stamp" src --include=*.cs`.
- **Deliverables (≤ 3 files):** `AccountController.cs` (that method only), `SecuritySettingsController.cs` (that sign-in only), and a test `tests/Kumunita.Web.Tests/VerifySignInStampTests.cs` (the principal/properties passed to `SignInAsync` contain a non-empty stamp that matches the user's current stamp).
- **Exit:** build green; new test + existing revocation tests (`grep -ln "RevocationStamp" tests/Kumunita.Web.Tests/*.cs`) green. Handoff note `## U17 — stamped sign-ins`: the list of every sign-in site and whether it now stamps (U18 relies on "all do").

#### U18 — Fail closed on a missing stamp
- **Goal:** after the configured cut-off date, a cookie with no stamp is rejected; before it, behaviour is unchanged.
- **Needs:** U17.
- **Entry reads:** `RevocationStampMiddleware.cs` lines 100–168; `Program.cs` `grep -n "RevocationStamp"` (options/config registration); the existing revocation middleware tests (first 80 lines).
- **Deliverables (≤ 3 files):** `RevocationStampMiddleware.cs` (read `Security:RevocationStamp:RequiredAfter`; if set and `UtcNow` is past it, an empty stamp is treated as revoked, same response as a stale stamp); `appsettings.json` (the key with the pinned default; **no real production date inside the repo if the owner prefers**, then leave it unset and document in COOLIFY.md in U30); `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` (before the date: pass; after: reject; stamped and fresh: pass; verify → change password → old cookie rejected).
- **Exit:** build green; new + existing revocation tests green. Handoff note `## U18 — stamp fail-closed`: the config key, default, and the rollout note for the operator.

### Phase E — Low findings

#### U19 — Markdown link filter (L4)
- **Goal:** `IsSafeUrl` rejects backslashes and control characters, allows `mailto:` explicitly, still rejects other schemes.
- **Needs:** U2.
- **Entry reads:** `MarkdownRenderer.cs` lines 380–420; its existing tests (`grep -ln "MarkdownRenderer" tests/Kumunita.Web.Tests/*.cs`, read the first hit's link-related tests only).
- **Deliverables (≤ 2 files):** `MarkdownRenderer.cs`; the markdown test file (new tests with U19's pinned names: `/\evil.example`, `\\evil`, `/%5Cevil` unchanged semantics, `mailto:a@example.test` rendered, `javascript:` and `data:` still plain text).
- **Exit:** build green; markdown tests green. Handoff note `## U19 — link filter`.

#### U20 — `UploadSignatureSniffer` (pure) + tests (L2)
- **Goal:** a pure magic-byte check for the allowed upload types.
- **Needs:** U2 (allowed type list pinned from `UploadGate.cs`).
- **Entry reads:** `UploadGate.cs` (whole); the U2 allowed-type table; one existing pure Web test for style.
- **Deliverables (≤ 2 files, new):** `src/Kumunita.Web/Security/UploadSignatureSniffer.cs` (PNG, JPEG, GIF, WebP, PDF, plain text/markdown by UTF-8 validity, zip-based office types by `PK\x03\x04`; unknown declared type → `false`); `tests/Kumunita.Web.Tests/UploadSignatureSnifferTests.cs`.
- **Exit:** build green; tests green. Handoff note `## U20 — sniffer`: the exact signature and the types it covers.

#### U21 — Wire the sniffer: attachments and content images
- **Needs:** U20.
- **Entry reads:** `AttachmentController.cs` `grep -n "ContentType"` then ±20 lines at each hit; `ContentImageController.cs` the same; the U20 handoff; their existing upload tests' setup (first 60 lines each).
- **Deliverables (≤ 4 files):** the two controllers (reject with the controller's existing 400/validation response when the head bytes do not match the declared type; read only the first 16 bytes via a rewindable stream or the buffered file); one new test class `tests/Kumunita.Web.Tests/UploadSniffAttachmentImageTests.cs`.
- **Exit:** build green; new + `AttachmentUploadTests` green. Handoff note `## U21 — sniffer wired (1/2)`.

#### U22 — Wire the sniffer: avatars and documents
- **Needs:** U20, U21 (copy its pattern).
- **Entry reads:** `ProfileController.cs` `grep -n "ContentType"` then ±20 lines (~line 463); `DocumentController.cs` the same (~lines 413, 646); U21 handoff.
- **Deliverables (≤ 3 files):** the two controllers; `tests/Kumunita.Web.Tests/UploadSniffAvatarDocumentTests.cs`.
- **Exit:** build green; new + `ProfileAvatarUploadTests` green. Handoff note `## U22 — sniffer wired (2/2)`: includes the note that dedup-by-hash keeps the first uploader's declared type; U30 records whether a follow-up is needed.

#### U23 — Verification tokens: store only a hash (L3, part 1)
- **Goal:** the stored value is a hash of a secret that is not in the database; the link carries `Id` + secret; the validate path no longer accepts the stored value as a credential.
- **Needs:** U2.
- **Entry reads:** `IdentityService.cs`: `grep -n "Sha256Hex\|ConsumedAt\|Kind ==" ` then ranges `sed -n '1670,1725p'` (validate) and the mint site (find with `grep -n "new IdentityToken"`, ±25 lines); `IdentityToken` document (`grep -rn "class IdentityToken" src`); `tests/Kumunita.Core.Tests/IdentityServiceTokenHashTests.cs` (whole; the tests that currently pin the dual-accept behaviour).
- **Deliverables (≤ 3 files):** `IdentityService.cs` (mint stores `Sha256Hex(secret)`; validate matches only the hash of the presented secret); the token test file (update only the tests Part 2 names as pinning the old dual-accept behaviour, and add the pinned new ones); `src/Kumunita.Core/Identity/IdentityToken.cs` **only** if a field comment needs updating. **Compatibility:** outstanding tokens are short-lived; U2 pins the TTL and whether a one-release grace is needed (default: none, document that pending links must be re-requested).
- **Exit:** build green; token tests + `IdentityServiceSignupPolicyTests` green. Handoff note `## U23 — hash-only tokens`: the new link shape (U24 needs it).

#### U24 — Verification link: GET shows a confirm page, POST signs in (L3, part 2)
- **Needs:** U23.
- **Entry reads:** `AccountController.cs` `sed -n '560,690p'` (the `Verify` action(s)) ; the existing verify view (`grep -rln "Verify" src/Kumunita.Web/Views/Account`); `tests/Kumunita.Web.Tests/AccountControllerSignupGateTests.cs` lines 1–60.
- **Deliverables (≤ 4 files):** `AccountController.cs` (GET renders a page with a form; POST validates the token, signs in via `ConfirmAndSignInAsync`, and is `[ValidateAntiForgeryToken]`); one view (the confirm form); the localized strings file only if the repo requires keys for new text (`KnownTranslationKeys.cs`: add keys by appending, never reformatting); `tests/Kumunita.Web.Tests/VerifyConfirmStepTests.cs` (GET never signs in; POST does; a prefetching GET leaves the token unconsumed).
- **Exit:** build green; new + existing account tests green. Handoff note `## U24 — verify confirm step`.

#### U25 — Container and CI hardening (L5)
- **Needs:** U2.
- **Entry reads:** `Dockerfile` (55 lines); `.github/workflows/ci.yml` (178 lines); `docs/SECURITY.md` `grep -n "pinned\|image" docs/SECURITY.md`.
- **Deliverables (≤ 3 files):** `Dockerfile` (non-root `USER`, writable paths `chown`ed, drop curl/wget if the `HEALTHCHECK` can use a built-in alternative, otherwise keep and record why); `ci.yml` (`permissions: contents: read` at the top, minimal per-job overrides); `docs/SECURITY.md` (state what is pinned). **Digest and SHA pinning:** resolve the digests / commit SHAs only if the network allows; otherwise leave the tags and list "to pin" items in the handoff note, never invented values.
- **Exit:** the workflow YAML parses (`python -c "import yaml…"`), `docker build` if Docker is available, else "not built here". Handoff note `## U25 — container/CI`: what is pinned and what is not.

#### U26 — Compose, hosts, and headers (L6, L7)
- **Needs:** U10 (HSTS lives next to the forwarded-header wiring).
- **Entry reads:** `docker-compose.yml` (113 lines); `appsettings.json`; `WellKnownController.cs` (`grep -n "Request.Host"` ±15); `Program.cs` `sed -n '936,990p'` (HSTS + security-header block).
- **Deliverables (≤ 4 files):** `docker-compose.yml` (bind published ports to `127.0.0.1`; comment "dev only — not a deploy template"); `appsettings.json` (`AllowedHosts` stays `*` in the committed file, with the production value documented in COOLIFY.md; do not break local dev); `WellKnownController.cs` (build the URL from a configured public base URL when set, else `Request.Host`); `Program.cs` (HSTS `MaxAge` 1 year, `includeSubDomains` **off** unless U2 records an owner decision, `Permissions-Policy` header with a restrictive default).
- **Exit:** build green; `WellKnown` tests (if any: `grep -ln WellKnown tests/Kumunita.Web.Tests/*.cs`) green. Handoff note `## U26 — compose/hosts/headers`: the config keys the operator should set.

#### U27 — Smaller items (L8, without log-forging)
- **Needs:** U10 (the sample-account exemption is removed only after proxy IPs are correct).
- **Entry reads:** `Program.cs` `sed -n '575,610p'` (the `login` policy); the break-glass token compare found by U2 (`IdentityService.cs`, ±15 lines); `tests/Kumunita.Web.Tests/` login rate-limit tests (`grep -ln "RateLimit" tests/Kumunita.Web.Tests/*.cs`).
- **Deliverables (≤ 3 files):** `Program.cs` (remove the sample-account exemption in the `login` policy; partition on IP only, and do not read the form body inside the limiter); `IdentityService.cs` (break-glass comparison through `CryptographicOperations.FixedTimeEquals`); one test class `tests/Kumunita.Web.Tests/LoginLimiterPartitionTests.cs` (and, in Core.Tests, a constant-time-compare test **only if** the compare is behind an accessible helper).
- **Exit:** build green; new + existing sample-account tests (`IdentityServiceSamplePasswordLockTests`, `IdentityServiceSampleMfaLockTests`) green. Handoff note `## U27 — L8 smalls`: if removing the exemption makes a sample-account test fail, record it and **do not** edit around it (rule 9).

#### U28 — Account enumeration (L1) — *parked: needs an owner decision*
- **Goal:** uniform responses: signup always answers "check your email", the login lockout message is generic, and login does equal work for unknown emails.
- **Needs:** U2; owner confirmation recorded in the handoff note (`## Decision — L1`). Without that line the unit exits as "parked".
- **Entry reads:** `AccountController.cs` `sed -n '500,540p'` (signup), `sed -n '775,800p'` and `sed -n '845,870p'` (login timing, lockout text); localized-string keys for those messages (`grep -n "signup.*exists\|locked" src/Kumunita.Core/Localization/KnownTranslationKeys.cs | head`).
- **Deliverables (≤ 4 files):** `AccountController.cs`; the string keys if wording changes; tests with the pinned names.
- **Exit:** build green; account tests green. Handoff note `## U28 — enumeration` or `## U28 — parked (no decision)`.

### Phase F — Close

#### U29 — Run and record the acceptance gate
- **Goal:** execute the three gate tests and the **full** suites once, and record the result in the design doc.
- **Needs:** all of U4–U27 done (U28 optional).
- **Entry reads:** design doc Part 2 §2.4; the handoff note headers only (`grep -n "^## " docs/plans-milestones/in-progress/sec/sec-handoff-notes.md`); the U3 baseline section.
- **Gate (§2.4):** (a) **hostile import** — a hand-built archive with a forged author, a colliding `Id`, and a foreign `GroupMembership` imports zero of those rows while the actor's genuine rows still import; (b) **admin without 2FA** — an authenticated GlobalAdmin with no enrolled 2FA is redirected from `/admin` and every non-allow-listed route; (c) **part-vs-whole** — every pinned test from the Part 2 list passes together, and the full suites show no failure that is not in the U3 baseline.
- **Deliverables (1 file, modify):** the design doc, append `### Run result (SEC acceptance gate — <date>)`: the three results, per-project totals compared with U3, and one line per unresolved `Drift pause` or `Noticed` item. **No code.**
- **Exit:** handoff note `## U29 — gate recorded`. **If any gate test is red, this unit stops here and lists it; it does not fix.**

#### U30 — Close: audit log, SECURITY.md, operator checklist
- **Goal:** record the remediation in the repo's own audit program and hand the operator the live checks no test can do.
- **Needs:** U29.
- **Entry reads:** `docs/SECURITY-AUDIT.md` lines 215–227 (§8) and §4 Checklist D (lines 96–129) headings only; `docs/SECURITY.md` §6 (lines 153–end); the `## U10`, `## U16`, `## U18`, `## U25`, `## U26` handoff sections.
- **Deliverables (≤ 3 files, modify):** `docs/SECURITY-AUDIT.md` §8 (one row per finding: id, date, "fixed in <commit/PR>", test class; **no PII**) plus a short Checklist D addition for the post-deploy checks (forwarded headers, `Secure` cookie, HSTS, `Health__Token`, `AllowedHosts`, ClamAV `Scan` setting, revocation cut-off date); `docs/SECURITY.md` (close or update §6 open items: forwarded headers, 2FA gate, log-forging stays open and says so); `docs/plans-milestones/in-progress/sec/sec-handoff-notes.md` (append `## Summary`: table of U1–U31, one line each, test counts, the **deferred list** — L1 if parked, log-forging, any "to pin" digests/SHAs, any live-config check the operator still owes).
- **Exit:** no build. Handoff note: the `## Summary` section is present.

#### U31 — Final: confirm the three-tier contract is intact
- **Goal:** one last consistency check; close the lane.
- **Needs:** U30.
- **Entry reads:** design doc (all of it is ~400 lines; read in two ranges), this file's unit headings (`grep -n "^#### U"`), the handoff note's `## Summary` and `grep -n "Drift pause"`.
- **Deliverables (≤ 2 files, modify):** the design doc, append `## SEC — Closed (recorded)` (gate result, deferred list from the Summary, one line per open veto and its final decision); optionally `docs/plans-milestones/README.md` (one line adding the `SEC` lane to the list, if the file has a lane list).
- **Exit:** the design doc ends with `## SEC — Closed (recorded)`. No orphan references, no unresolved drift pause. The `## Summary` in the handoff note is the sole handoff artifact. After this, a human moves the lane folder from `in-progress/sec/` to `done/sec/`.

---

## Pinned test names (seed list — U2 completes, fixes class names and projects)

| Unit | Class | Names |
|---|---|---|
| U4 | `ImportOwnershipGuardTests` | `Allows_RowOwnedByActor`, `Rejects_ForgedAuthorId`, `Rejects_ForgedOwnerId`, `Rejects_ForgedUserId`, `Rejects_IdCollisionWithOtherOwner`, `Rejects_AccessGrantingTypes_Membership_Invitation_JoinRequest`, `Group_RequiresActorAsOwner`, `Message_RequiresActorConversation`, `Conversation_ActorMustBeParticipant`, `Rejects_TypeOutsideInventory` |
| U5 | `UserPortabilityHostileClassifyTests` | `Classify_ForgedAuthor_IsRejected`, `Classify_IdCollision_IsRejected`, `Classify_ForeignMembership_IsRejected`, `Classify_OwnRows_StillClean` |
| U6 | `UserPortabilityHostileResolveTests` | `Resolve_NeverStoresForgedAuthorRow`, `Resolve_NeverOverwritesForeignDocument`, `Resolve_NeverAddsForeignGroupMembership`, `Resolve_TamperedPlan_StillGuarded`, `Resolve_GenuineRoundTrip_StillImports` |
| U7 | `AdminTwoFactorMiddlewareTests` | `UnenrolledAdmin_Admin_RedirectsToEnrolment`, `UnenrolledAdmin_AnyOtherRoute_Redirects`, `UnenrolledAdmin_EnrolmentAndLogout_Allowed`, `EnrolledAdmin_PassesThrough`, `NonAdmin_PassesThrough`, `SampleDataAdmin_Exempt`, `AdminWhoDisables2fa_IsGatedOnNextRequest` |
| U9 | `ForwardedHeadersSetupTests` | `TrustedProxy_ClientIpAndSchemeHonored`, `UntrustedPeer_HeadersIgnored`, `EmptyConfig_DefaultsToPrivateRangesNotTrustAll`, `InvalidCidr_FailsBoot` |
| U12 | `AntiforgeryCoverageTests` | `EveryUnsafeAction_IsCoveredByAntiforgery`, `OnlyTwoActionsIgnoreAntiforgery` |
| U13 | `KumunitaArchiveLimitsTests` | `Rejects_TotalDecompressedOverLimit`, `Rejects_EntryCountOverLimit`, `Rejects_DuplicateEntryNames`, `NormalArchive_StillReads` |
| U14 | `UserPortabilityMediaGateTests` | `ImportedMedia_OverSize_IsSkipped`, `ImportedMedia_DisallowedType_IsSkipped`, `ImportedMedia_RespectsQuota` |
| U15 | `AnonymousWriteLimitsTests` | `AnonymousWrites_HaveRateLimitPolicy`, `ErrorReportDescription_Over4000_IsInvalid`, `ErrorReportDescription_At4000_IsValid` |
| U16 | existing health test class + new | `Health_Anonymous_TokenUnset_ReturnsMinimalPayload`, `Health_ValidToken_ReturnsFullPayload`, `Health_SmtpProbe_IsCached`, `Health_TokenCompare_IsConstantTime` |
| U17 | `VerifySignInStampTests` | `VerifySignIn_CarriesCurrentStamp` |
| U18 | `RevocationStampFailClosedTests` | `NoStamp_BeforeCutoff_PassesThrough`, `NoStamp_AfterCutoff_IsRejected`, `FreshStamp_AfterCutoff_Passes`, `Verify_ThenChangePassword_OldSessionRejected` |
| U19 | markdown tests | `Link_BackslashPrefixed_RendersAsText`, `Link_Mailto_IsAllowed`, `Link_JavascriptAndData_StillRejected` |
| U20 | `UploadSignatureSnifferTests` | `Png_Jpeg_Gif_WebP_Pdf_Accepted`, `DeclaredPng_ButHtmlBytes_Rejected`, `UnknownDeclaredType_Rejected` |
| U21 | `UploadSniffAttachmentImageTests` | `Attachment_MismatchedBytes_Rejected`, `ContentImage_MismatchedBytes_Rejected` |
| U22 | `UploadSniffAvatarDocumentTests` | `Avatar_MismatchedBytes_Rejected`, `Document_MismatchedBytes_Rejected` |
| U23 | token tests | `Token_StoredValue_IsNotAcceptedAsCredential`, `Token_RawSecret_Validates`, `Token_LinkCarriesIdAndSecret` |
| U24 | `VerifyConfirmStepTests` | `VerifyGet_DoesNotSignIn`, `VerifyPost_SignsIn`, `VerifyGet_LeavesTokenUnconsumed` |
| U27 | `LoginLimiterPartitionTests` | `LoginLimiter_PartitionsOnIpOnly`, `LoginLimiter_NoSampleAccountExemption` |
| U28 | (parked) | `Signup_ExistingEmail_SameResponse`, `Login_UnknownEmail_DoesEqualWork`, `Lockout_Message_IsGeneric` |

U1–U3, U8, U10 (conditionally), U25, U26, U29–U31 add no named tests; each says why in its handoff note.

## Run order and dependencies at a glance
`U1 → U2 → U3` · H1 `U4 → U5 → U6` · H2 `U7 → U8` · H3 `U9 → U10` · `U11 → U12` · `U13 → U14` · `U15`, `U16`, `U17 → U18`, `U19`, `U20 → U21 → U22`, `U23 → U24`, `U25` independent after U2 · `U26`, `U27` after U10 · `U28` parked · `U29 → U30 → U31`.
Phases A, B and C are independent of each other, so several agents can work them in parallel on separate branches, provided only one agent edits `Program.cs` at a time (U8, U10, U12, U15, U18, U26, U27 all touch it with small, anchored edits; **merge them one by one and re-run the build between**).

## Appendix A — Findings register (source for U1; from the static audit of `main` @ `bdcaff7`, 2026-10-30)

| ID | Sev | Finding | Where (audit-time) | Unit(s) |
|---|---|---|---|---|
| H1 | High | Resident import stores archive rows with no ownership or `Id`-collision check (forge authorship, overwrite documents, join private groups); tests only cover export | `UserPortabilityService.cs:714` (classify), `:1100`, `:1141` (`session.Store(row)`); `UserScopeInventory.cs` | U4–U6 |
| H2 | High | GlobalAdmin 2FA is a redirect, not a gate; the admin cookie already exists; nothing checks `TwoFactorEnabled` | `AccountController.cs:802`, `:893-907` | U7–U8 |
| H3 | High | `UseForwardedHeaders()` has no options (no-op): one shared rate-limit bucket, HSTS not emitted, cookie not `Secure` (depends on live proxy config) | `Program.cs:948`, `:951`, `:565`, `:599`, `:586` | U9–U10 |
| M1 | Med | Seven state-changing POSTs lack anti-forgery validation; no global filter | `CommunityController` `SetMandatory` (114), `AddMember` (136), `RemoveMember` (158), `Leave` (198); `MessagesController` `Open` (162), `Send` (333); `PostsController` `Replies` (1280) | U11–U12 |
| M2 | Med | Zip bomb (no entry-count / decompressed-size cap) and imported media bypasses upload gate and quota; import actions unthrottled | `KumunitaArchive.cs:273-300`; `UserPortabilityService.cs:1169` | U13–U14 |
| M3 | Med | Anonymous writes without rate limit; error-report text has no length cap | `HomeController.cs` (`/Home/Error/Report`), `IssueController.cs` (`/issues/new`), `/escalations/inbound`, `/api/mail/suppress`, `ErrorReportFormModel.cs:29` | U15 |
| M4 | Med | Anonymous `/health` does a DB query + SMTP handshake per request; full detail public unless `Health__Token` set; non-constant-time compare | `HealthController.cs:67-109` | U16 |
| M5 | Med | Session revocation fails open when a cookie has no stamp; verify-link sign-in is unstamped; built-in stamp validator interval is 14 days | `RevocationStampMiddleware.cs:130`; `AccountController.cs:719`; `Program.cs:663` | U17–U18 |
| L1 | Low | Account enumeration: signup message, lockout message, login timing (**parked**) | `AccountController.cs:522`, `:783`, `:855` | U28 |
| L2 | Low | Upload type trusted from client; dedup keeps first uploader's declared type; ClamAV off by default | `AttachmentController.cs:100`, `ContentImageController.cs:190`, `ProfileController.cs:463`, `DocumentController.cs:413,646` | U20–U22 |
| L3 | Low | Verification token "hash at rest" is accepted as a credential; GET link signs the browser in | `IdentityService.cs:1698`; `AccountController.cs` Verify | U23–U24 |
| L4 | Low | Markdown link filter lets `/\host` through; `mailto:` never accepted | `MarkdownRenderer.cs:397` | U19 |
| L5 | Low | Container runs as root; floating image tags; CI actions pinned by tag, no `permissions:` | `Dockerfile`, `.github/workflows/ci.yml` | U25 |
| L6 | Low | Dev compose publishes Postgres with default credentials, Development mode | `docker-compose.yml` | U26 |
| L7 | Low | HSTS scope, `AllowedHosts: *`, `security.txt` URL from `Request.Host`, no `Permissions-Policy` | `Program.cs:951`, `appsettings.json:12`, `WellKnownController.cs` | U26 |
| L8 | Low | Sample-account limiter exemption and form-body read inside limiter; non-constant-time break-glass compare; log-forging open item (**log-forging parked**) | `Program.cs:586-595`, `IdentityService.cs`, `SECURITY.md` | U27 |

**What held up (do not "fix"):** parameterized SQL at the three raw sites; strict CSP and Markdown escaping; service-level authorization on Announcement actions; hex-only media paths and safe download headers; lockout, TOTP code checks, rate-limited second leg; anti-forgery on all other state-changing POSTs.