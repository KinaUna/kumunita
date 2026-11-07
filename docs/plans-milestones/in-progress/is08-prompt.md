---
id: is08-prompt
title: "IS08 prompt — redact the seeder log templates + the log-PII gate (retire P-3's last source)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS08 — Redact the seeders, and the log-PII gate

## What you are doing

You are the **eighth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS07 are committed (plan,
design, the gate harness, the Sentry breadcrumb floor, the `LogRedact` helper,
and the 4 identity/SMTP templates). Your unit is **IS08** — the **last source
redaction**: three seeder log templates still carry a raw address
(`{Email}`), and then you build the **gate** that makes the whole class of
defect unrepeatable. A one-line PII gate test scans every log call in
`src/**/*.cs`, finds `{…Email…|…Ip…|…Phone…|…Address…|…Recipient…|…Body…}`
placeholders, and fails unless they are redacted. After you, the M4 baseline
retires: **7 PII log placeholders → 0**, and the next developer who writes
`logger.LogInformation("{Email}", e)` gets a **red gate**, not a review comment.
This is the lane's doctrine in one sentence: *the parts are cheap, the linkage
is rare* — you are adding the linkage.

**Deliverables (3 files + the handoff-notes append):**

1. `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — the 2 sites below.
2. `src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs` — the 1 site below.
3. `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs` — **new**,
   the gate (namespace `Kumunita.Web.Tests.SecGate`, next to
   `AccessViaDenyMatrixTests`).

Each `{Email}` placeholder becomes a **redacted value** computed by the IS06
helper — `LogRedact.EmailDomain(email)` (the domain) — passed as the log
argument. **Template-only change for the two seeders: zero behaviour change.**
The new test file is the gate itself. Add `using Kumunita.Core.Logging;` to
either seeder that does not already import it (neither does, as of HEAD —
verify, don't assume).

**The 3 sites (verify each with the read, then edit the template line + its
address argument only):**

| File:line | Current template | Redaction |
|---|---|---|
| `FirstBootSeeder.cs:230` | `First boot: seed admin '{Email}' already exists; skipping the seed lane (idempotent).` | `LogRedact.EmailDomain(email)` (label → `{EmailDomain}`) |
| `FirstBootSeeder.cs:295` | `First boot: seeded GlobalAdmin '{Email}' (userId {UserId}); setup token staged to outbox.` | `LogRedact.EmailDomain(email)` (label → `{EmailDomain}`; `{UserId}` is an id — leave it) |
| `SampleDataSeeder.cs:2579` | `Sample data: ensured account {Email} ({Name}).` | `LogRedact.EmailDomain(email)` (label → `{EmailDomain}`; `{Name}` is a display name, leave it) |

**The gate test (one test, `NoLogTemplate_CarriesAPiiPlaceholder`):**

- Scan `src/**/*.cs` via the existing harness
  (`tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` — `RepoRoot`, `ReadSrc`,
  `AllSrcFiles`; read it first, 59 lines).
- **Only log calls count** — a `logger.Log{Information|Warning|Error|Debug|
  Critical|Trace}(` template (also `Log(logLevel, "…")` if you find one).
  **Not** exception messages, `throw new …("…")`, ordinary string literals.
  Verified clean at HEAD: the only other `{…Body…}`-style hits in `src/` are
  two exception messages (`GroupMessagingService.cs:487`,
  `MessagingService.cs:332`) — they must NOT trip the gate.
- **Handle multi-line calls**: a template and its arguments can span 2–3
  lines (both FirstBoot sites do). A naive single-line regex misses them —
  either join the call's text first or scan the *concatenated* log-call body.
  The test you write must itself be immune to line-wrapping.
- **The PII word set** (case-insensitive, placeholder = `{` + word + `}`):
  `Email`, `Ip` (but NOT `Id`, `Index`, `Idp`, `IdempotencyKey` — word-boundary
  rule: the placeholder must be exactly the word, or the word + `Id`/`Key`
  excluded by the **id rule** below), `Phone`, `Address`, `Recipient`, `Body`.
- **The id rule (from the plan):** placeholders ending in `Id` —
  `{RecipientId}`, `{UserId}`, `{Idp}` — are **ids, not PII**; exclude them.
  `…IdempotencyKey` likewise (an id-shaped key).
- **The allowlist is empty.** If a site genuinely cannot be redacted, you do
  not add it to an allowlist silently — pause and record the question in the
  handoff. As of HEAD the allowlist stays `Array.Empty<…>()` and the gate
  passes with the 3 seeder fixes in.
- The test name is pinned by §2.3; §2.4's anchor for the gate is this test
  existing and passing — the test name is the drift guard.

**This unit adds exactly ONE test** — the gate itself. §2.3 records IS08 as
`NoLogTemplate_CarriesAPiiPlaceholder` (7 → 0). Do not add more tests; the
gate is the unit.

**You are NOT** doing IS09 (the trust-boundary + log-hygiene docs), IS10 or
later, or any other unit. You touch **only** the two seeder `.cs` files, the
one new test file, and the handoff notes. **Do not** edit
`IdentityService.cs`, `SmtpSender.cs` (IS07's, already committed),
`Program.cs`, any controller, `docs/security/`, or add other tests.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS07 is the current HEAD** (`test(improve-sec): IS07 — redact identity +
  SMTP log templates`). The helper already exists and compiles:
  `src/Kumunita.Core/Logging/LogRedact.cs` —
  `public static string EmailDomain(string?)` (lower-cased domain, or
  `"(none)"`) and `public static string Hash8(string?)` (8 lowercase-hex).
- `src/Kumunita.Core/Identity/` is already clean:
  `grep -rn "{Email}" src/Kumunita.Core/Identity/` → **0 hits** (IS07's
  anchor — if it is not 0, stop, something upstream drifted).
- The SecGate harness exists: `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs`
  (`RepoRoot`, `ReadSrc(relativePath)`, `AllSrcFiles(pattern)`,
  `TestMethodExists(name)`), namespace `Kumunita.Web.Tests.SecGate`.
- `dotnet build` is available; the new test lives in the **Web** test project
  (where the SecGate harness lives), even though the redacted sources are Core.
- Commit style: `test(improve-sec): IS08 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS08** unit spec under "Phase A — S3" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract.
3. `docs/design/improve-sec-design.md` **§2.2** (the `LogRedact` shape),
   **§2.3** (IS08 = `NoLogTemplate_CarriesAPiiPlaceholder`, 7 → 0), and the
   **IS08** row of **§2.4** (anchor: `grep -rn "{Email}"
   src/Kumunita.Core/Bootstrap/` → **0 hits** afterwards).
4. `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (whole, 59 lines) and
   `AccessViaDenyMatrixTests.cs` lines 1–40 (the SecGate test style + the
   repo-root pattern).
5. The 3 sites, **range reads only** (do not read these whole files):
   `sed -n '225,235p;290,300p' src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`
   and `sed -n '2574,2583p' src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs`.
6. `src/Kumunita.Core/Logging/LogRedact.cs` — the helper you are consuming
   (IS07 already consumed it in `IdentityService.cs`; look at one of those
   sites for the shape: `sed -n '158,163p' src/Kumunita.Core/Identity/IdentityService.cs`).
7. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS06` and `## IS07` sections (IS07 records the in-process runner form
   that works on this machine).

## The work

1. **Redact the 3 sites.** In each: change the template label `{Email}` →
   `{EmailDomain}` and pass `LogRedact.EmailDomain(email)` instead of the raw
   `email`. Nothing else moves — no log level, no other argument, no control
   flow, no method signature. Add `using Kumunita.Core.Logging;` to each seeder
   file that lacks it (neither does at HEAD).
2. **Write the gate.** `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder`:
   collect every `src/**/*.cs` file via `SecGateSource.AllSrcFiles`, find the
   log-call templates (multi-line-safe), and assert none carries a PII-word
   placeholder that is not id-excluded. The allowlist is **empty** — the test
   should pass as-is once the seeders are redacted. Fail with a useful message
   (file:line + the offending placeholder) so the next drift is a 30-second
   diagnosis.
3. **Sanity-check the gate actually bites.** Before you finish, temporarily
   verify the test would fail on an unredacted site (e.g. run it once with one
   seeder edit stashed, or reason through it), then restore. A gate that cannot
   fail is decoration — the IS44 cold read (`logger.LogInformation("{Email}", e)`
   must make it fail) depends on this.

## Verification (all green before you finish)

- [ ] `dotnet build` — **0 errors**.
- [ ] **No `{Email}` remains in the seeders:**
      `grep -rn "{Email}" src/Kumunita.Core/Bootstrap/` → **0 hits** (§2.4
      anchor).
- [ ] **No raw PII placeholder remains anywhere in `src/` log calls:**
      `grep -rnE '\{[A-Za-z]*(Email|Ip|Phone|Address|Recipient|Body)[A-Za-z]*\}'
      src/ --include='*.cs'` → hits only in non-log code (the two exception
      messages, `MessagesController`'s `{RecipientId}`, the `Program.cs`
      comment) — **zero in log templates**.
- [ ] **The redaction is real, not relabelled:**
      `grep -rn "LogRedact.EmailDomain" src/Kumunita.Core/Bootstrap/` → **3 hits**.
- [ ] **The gate test exists and passes:**
      `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
      -class "Kumunita.Web.Tests.SecGate.LogTemplatePiiGateTests"` → 1/1 pass.
      (On this machine `dotnet test -- -class` reports "Zero tests ran / exit
      5" — a known xunit.v3 runner quirk, not a real failure; use the
      in-process `dotnet exec … -class` form.)
- [ ] **The gate still bites on its neighbours:**
      `dotnet exec … -class "Kumunita.Web.Tests.SecGate.AccessViaDenyMatrixTests"`
      → still green (3/3) — you did not break the harness.
- [ ] `git status` shows **only** the 2 seeders, the 1 new test file, and the
      handoff-notes append.

## Definition of done (IS08)

- [ ] The 3 seeder templates carry `LogRedact.EmailDomain(email)` — never the
      raw address; **no behaviour change** (templates only).
- [ ] `grep -rn "{Email}" src/Kumunita.Core/Bootstrap/` → 0 hits; the
      `LogRedact.EmailDomain` calls are present at all 3 sites.
- [ ] `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` exists,
      scans `src/**/*.cs` log calls multi-line-safely, excludes id-shaped
      placeholders, has an **empty** allowlist, and passes. You verified it
      would fail on an unredacted site.
- [ ] `dotnet build` 0 errors; the new gate class green; the IS04 matrix class
      still green.
- [ ] Handoff note has a `## IS08 — <title>` section (3–8 lines: the 3 sites,
      the gate's scan shape + id rule, the gate-pass evidence, the bite-check,
      what IS09 consumes next).
- [ ] One commit: `test(improve-sec): IS08 — redact the seeders + the log-PII gate`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS09's work (the trust-boundary + log-hygiene docs)
to make it pass.** A failed check is a recoverable step, not a stopping point:
read the error, adjust within the unit's scope, continue. If the *same* check
failed twice, change approach (e.g. the gate's log-call detection) — don't
retry the identical edit a third time.
