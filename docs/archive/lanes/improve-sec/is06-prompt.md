---
id: is06-prompt
title: "IS06 prompt — the LogRedact helper (the source-side half of P-3)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS06 — `LogRedact` helper

## What you are doing

You are the **sixth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS05 are committed (plan,
design Part 1/2a/2b, the gate harness, and the Sentry breadcrumb floor). Your
unit is **IS06** — the `LogRedact` helper, the **source-side** half of P-3.
IS05 capped the *sink* (Sentry stops taking Information logs as breadcrumbs);
IS06 fixes the *source* so that even the file-log sink never carries a raw
email address — it carries the domain (or a stable 8-hex hash) instead. This is
a **pure helper**: no state, no DI, no PII retention, BCL-only.

**Deliverables (2 new files + the handoff-notes append):**

1. `src/Kumunita.Core/Logging/LogRedact.cs` — `public static class LogRedact`
   in the `Logging` module, next to `LogLine`. Two members, exact signatures
   (design §2.4 anchor):
   - `public static string EmailDomain(string? value)` — returns the
     **lower-cased domain** of a syntactically valid email
     (`"a@example.test"` → `"example.test"`), or `"(none)"` for null/empty/
     malformed input. The local part is **never** returned.
   - `public static string Hash8(string? value)` — the **first 8 lowercase-hex
     characters of the SHA-256 digest** over the input, for correlating log
     lines without carrying the address.
2. `tests/Kumunita.Core.Tests/LogRedactTests.cs` — a **pure** unit (no
   `PostgresFixture`, no Marten): `namespace Kumunita.Core.Tests;`,
   `public class LogRedactTests`, `[Fact]` from `Xunit`.

**Tests (exact names, `§2.3`):**
- `EmailDomain_ReturnsDomainOnly`
- `EmailDomain_NullOrMalformed_IsNone`
- `Hash8_IsStableAndShort`

**You are NOT** doing IS07+ (redacting the actual `IdentityService`/`SmtpSender`
templates — that's the *consumer* of this helper), the seeder redaction, or the
log-PII gate (IS08). You touch **only** the two new files above and the handoff
notes. **Do not** edit `Program.cs`, any controller, `IdentityService*.cs`,
`SmtpSender.cs`, the seeders, or `docs/adr/`. IS06 is pure Core — the Web
project is untouched, so no Web test run is required.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- IS05 is the current HEAD (`test(improve-sec): IS05 — Sentry breadcrumb floor`).
- `src/Kumunita.Core/Logging/LogLine.cs` is the style reference — same
  namespace (`Kumunita.Core.Logging`), `public static class`, **BCL-only**
  (the module deliberately avoids `System.Text.Json`; SHA-256 is
  `System.Security.Cryptography.SHA256`, which is BCL and fine).
- `dotnet build` is available; the test project for this unit is
  `tests/Kumunita.Core.Tests`.
- Commit style: `test(improve-sec): IS06 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS06** unit spec under "Phase A — S3" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract.
3. `docs/design/improve-sec-design.md` **§2.2** (the `LogRedact.EmailDomain` /
   `Hash8` shape — the exact return contract), **§2.3** (the three test names),
   and **§2.4** (the IS06 anchor: `public static string EmailDomain(` and
   `public static string Hash8(` in `src/Kumunita.Core/Logging/LogRedact.cs`).
4. `src/Kumunita.Core/Logging/LogLine.cs` — the module's style (namespace,
   `public static class`, XML doc-comment convention, BCL-only).
5. One pure Core test for style — e.g. `tests/Kumunita.Core.Tests/BoardStandingPolicyTests.cs`
   or `GoalStandingPolicyTests.cs` (fixture-free, `[Fact]`, `Xunit`).
6. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS01` through `## IS05` sections (IS05 notes the in-process runner form
   for this machine; IS04 has the `SecGate` pattern you are **not** reusing here).

## The work

### 1. `LogRedact.cs`

- `public static class LogRedact` in `namespace Kumunita.Core.Logging;`, with
  an XML doc-comment that states the P-3 contract: *the log never carries the
  local part of an address; it carries the domain (or a hash) instead, and
  nothing is retained*.
- `EmailDomain(string?)`:
  - null / empty / no `@` / empty local part / empty domain → `"(none)"`.
  - a valid `local@domain` → the **lower-cased** domain (the text after the
    `@`). Do **not** return the local part.
  - Keep it simple and defensive; no regex required (a `LastIndexOf('@')`
    check is sufficient for the "domain after the `@`" rule). The local part is
    discarded — you never need to parse it beyond confirming it is non-empty.
  - `using` for `System.Security.Cryptography` only if you use `SHA256` here;
    the email parse itself is pure BCL.
- `Hash8(string?)`:
  - `SHA256.HashData(...)` (or the equivalent) over the UTF-8 bytes of the
    input; return the **first 8 lowercase hex characters** of the digest.
  - `"(none)"` (or an equivalent sentinel — pick one and pin it in the test)
    for null/empty, so the helper is total and the caller never sees a
    `null`. State the choice in the doc-comment.
  - **No PII retention** — the hash is computed and returned; nothing is stored.

### 2. `LogRedactTests.cs`

- `namespace Kumunita.Core.Tests;`, `public class LogRedactTests`, `[Fact]`.
- `EmailDomain_ReturnsDomainOnly` —
  `EmailDomain("a@example.test")` == `"example.test"`;
  `EmailDomain("A@Example.TEST")` == `"example.test"` (lower-cased);
  and the local part is **not** in the result for a known address.
- `EmailDomain_NullOrMalformed_IsNone` — `EmailDomain(null)`, `EmailDomain("")`,
  `EmailDomain("no-at-sign")`, `EmailDomain("@example.test")` (empty local
  part), `EmailDomain("a@")` (empty domain) all return `"(none)"`.
- `Hash8_IsStableAndShort` — `Hash8("a@example.test")` is **exactly 8**
  lowercase-hex characters, and **stable** (calling it twice on the same input
  yields the same value); a different input yields a different value.

## Verification (all green before you finish)

- [ ] `dotnet build` — **0 errors**.
- [ ] `LogRedactTests` — **3 passed, 0 failed**. Run via the in-process runner
      the repo documents (`docs/dev/testing-and-ci.md`): build, then
      `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
      -class Kumunita.Core.Tests.LogRedactTests`.
      (On this machine `dotnet test -- -class` reports "Zero tests ran / exit
      5" — a known runner quirk, not a real failure. `Kumunita.Core.Tests`
      takes ~20 s, not <1 s like the Web suite.)
- [ ] `grep -n "public static string EmailDomain(\|public static string Hash8("
      src/Kumunita.Core/Logging/LogRedact.cs` → **both anchors present**.
- [ ] `grep -rn "a@example.test\|System.Text.Json" src/Kumunita.Core/Logging/LogRedact.cs`
      → the helper itself carries **no** PII literal and no `System.Text.Json`.
- [ ] `git status` shows **only** `LogRedact.cs`, `LogRedactTests.cs`, and the
      handoff-notes append.

## Definition of done (IS06)

- [ ] `LogRedact.cs` carries `public static string EmailDomain(string?)` and
      `public static string Hash8(string?)` (the §2.4 anchor), BCL-only,
      next to `LogLine` in the `Logging` module.
- [ ] The three `LogRedactTests` pass; `dotnet build` 0 errors.
- [ ] Handoff note has a `## IS06 — LogRedact helper` section (3–8 lines: the
      two members + the sentinel choice for `Hash8` on null/empty, the 3 tests
      + pass count, what IS07 consumes next).
- [ ] One commit: `test(improve-sec): IS06 — LogRedact helper (EmailDomain, Hash8)`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS07's work (redacting the templates) to make it
pass.** A failed check is a recoverable step, not a stopping point: read the
error, adjust within the unit's scope, continue.
