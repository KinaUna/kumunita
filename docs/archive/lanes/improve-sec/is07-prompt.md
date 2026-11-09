---
id: is07-prompt
title: "IS07 prompt — redact the identity + SMTP log templates (the consumer side of P-3)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS07 — Redact the identity + SMTP log templates

## What you are doing

You are the **seventh working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS06 are committed (plan,
design Part 1/2a/2b, the gate harness, the Sentry breadcrumb floor, and the
`LogRedact` helper). Your unit is **IS07** — the *consumer* of that helper.
Four `IdentityService`/`SmtpSender` log templates still carry a raw email
(`{Email}` / `{Recipient}`); you replace the address with **`LogRedact`'s
domain or 8-hex hash** so the file-log sink never sees it. This is a
**template-only** change: **zero behaviour change**. The helper (IS06) and the
gate (IS08) already exist as a seam; you are filling the four source sites it
was built for.

**Deliverables (2 files + the handoff-notes append):**

1. `src/Kumunita.Core/Identity/IdentityService.cs` — the 3 sites below.
2. `src/Kumunita.Core/Identity/SmtpSender.cs` — the 1 site below.

Each `{Email}` / `{Recipient}` placeholder becomes a **redacted value** computed
by the IS06 helper — `LogRedact.EmailDomain(addr)` (the domain) or
`LogRedact.Hash8(addr)` (the stable 8-hex) — passed as the log argument. Keep
the rest of the template and every other argument identical. No signature
change, no new field, no new method; a `using Kumunita.Core.Logging;` if the
file does not already have it.

**The 4 sites (verify each with the read, then edit the template line only):**

| File:line | Current template | Redaction |
|---|---|---|
| `IdentityService.cs:159` | `…disposable-email domain {Domain} (email {Email}).` | drop the raw address: `LogRedact.EmailDomain(email)` (the `{Domain}` already carries the domain) |
| `IdentityService.cs:218` | `Registered unverified resident {UserId} (email {Email}).` | `LogRedact.EmailDomain(email)` |
| `IdentityService.cs:271` | `Re-issued verification (attempt {Attempt}) for resident {UserId} (email {Email}).` | `LogRedact.EmailDomain(email)` |
| `SmtpSender.cs:295` | `Delivered email {Idp} to {Recipient} (attempt sent).` | `LogRedact.Hash8(email.Recipient)` |

Where the raw address is available, pass **the redacted value, not the address**,
as the argument — i.e. change `(email {Email})` → `(email {EmailDomain})` and
`{Email}` arg `email` → `LogRedact.EmailDomain(email)`. (The `{…Id}` /
`{UserId}` placeholders are ids, not PII — leave them.)

**This unit adds NO test.** §2.3 records IS07 as *"no new test — the change is
four log templates with zero behavior change, and P-3 is pinned by IS05 (sink)
and IS08 (gate)."* Your regression gate is that
`tests/Kumunita.Core.Tests/IdentityServiceSignupPolicyTests.cs` **stays green**
(it asserts behaviour, not the template text — no test pins the strings today,
so the template change cannot break it).

**You are NOT** doing IS08 (the seeder redaction + the log-PII gate), IS09
(the docs), or any other unit. You touch **only** the two `.cs` files above and
the handoff notes. **Do not** edit the seeders (`FirstBootSeeder.cs`,
`SampleDataSeeder.cs`), `Program.cs`, any controller, `docs/adr/`, or add a test
file. IS07 is pure Core — the Web project is untouched, so no Web test run is
required.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS06 is the current HEAD** (`test(improve-sec): IS06 — LogRedact helper
  (EmailDomain, Hash8)`). The helper already exists and compiles:
  `src/Kumunita.Core/Logging/LogRedact.cs` —
  `public static string EmailDomain(string?)` (lower-cased domain, or
  `"(none)"`) and `public static string Hash8(string?)` (8 lowercase-hex, or
  `"(none)"`).
- The `LogRedact` namespace is `Kumunita.Core.Logging` (next to `LogLine`);
  `IdentityService.cs` / `SmtpSender.cs` are in `Kumunita.Core.Identity` — add
  the `using` if missing.
- `dotnet build` is available; the regression test is in
  `tests/Kumunita.Core.Tests` (the Core suite, ~20 s).
- Commit style: `test(improve-sec): IS07 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS07** unit spec under "Phase A — S3" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract.
3. `docs/design/improve-sec-design.md` **§2.2** (the `LogRedact` shape),
   **§2.3** (IS07 = "no new test"), and the **IS07** row of **§2.4** (the
   anchor: `grep -rn "{Email}" src/Kumunita.Core/Identity/` → **0 hits**
   afterwards).
4. `src/Kumunita.Core/Logging/LogRedact.cs` — the helper you are consuming
   (read the two doc-comments; `EmailDomain` returns a domain, `Hash8` a hash).
5. The 4 sites, **range reads only** (do not read these whole files):
   `sed -n '150,165p;210,222p;262,275p' src/Kumunita.Core/Identity/IdentityService.cs`
   and `sed -n '285,300p' src/Kumunita.Core/Identity/SmtpSender.cs`.
6. `tests/Kumunita.Core.Tests/IdentityServiceSignupPolicyTests.cs` — the
   regression class you must keep green (read the `[Fact]`s, not the whole
   file if it is long).
7. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS05` and `## IS06` sections (IS06 notes the in-process runner form for
   this machine).

## The work

For **each of the 4 sites**: change **only** the template string and the
argument(s) that carried the raw address. The address is never logged — it is
redacted *before* it is passed to the logger. Concretely:

- **`{Email}` sites (the 3 in `IdentityService.cs`):** keep the surrounding
  template, but the placeholder now shows the redacted value. Either (a) keep
  the `{Email}` label and pass `LogRedact.EmailDomain(email)` as the argument,
  or (b) rename the label to `{EmailDomain}` for clarity — your call, but the
  **argument must be the domain, not the address**. For `:159` the template
  already carries `{Domain}`, so prefer dropping the `(email {Email})` tail or
  replacing its argument with the domain — do not let the full address remain.
- **`{Recipient}` site (`SmtpSender.cs:295`):** pass
  `LogRedact.Hash8(email.Recipient)` as the argument (keep or rename the label
  to `{RecipientHash}`).

Add `using Kumunita.Core.Logging;` to any file that does not already import it.
Do **not** change the log *level*, the other arguments (`{Domain}`, `{UserId}`,
`{Attempt}`, `{Idp}`), the control flow, or the method signatures.

## Verification (all green before you finish)

- [ ] `dotnet build` — **0 errors**.
- [ ] **No `{Email}` remains** in the identity module:
      `grep -rn "{Email}" src/Kumunita.Core/Identity/` → **0 hits** (§2.4
      anchor). (Note: `{Recipient}` at `SmtpSender.cs:295` is covered by the
      §2.4 row's intent; verify the raw `{Recipient}` placeholder is gone too.)
- [ ] **The redaction is real, not relabelled:**
      `grep -rn "LogRedact.EmailDomain\|LogRedact.Hash8" src/Kumunita.Core/Identity/`
      → hits at the sites you edited (≥ 4).
- [ ] `IdentityServiceSignupPolicyTests` **still green**: build, then
      `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
      -class Kumunita.Core.Tests.IdentityServiceSignupPolicyTests`.
      (On this machine `dotnet test -- -class` reports "Zero tests ran / exit
      5" — a known runner quirk, not a real failure. The Core suite takes
      ~20 s, not <1 s like the Web suite.)
- [ ] `git status` shows **only** `IdentityService.cs`, `SmtpSender.cs`, and the
      handoff-notes append.

## Definition of done (IS07)

- [ ] The 4 templates carry a redacted value (`LogRedact.EmailDomain` /
      `LogRedact.Hash8`), never the raw address; **no behaviour change**.
- [ ] `grep -rn "{Email}" src/Kumunita.Core/Identity/` → 0 hits; the
      `LogRedact.*` calls are present at the edited sites.
- [ ] `dotnet build` 0 errors; `IdentityServiceSignupPolicyTests` still green.
- [ ] Handoff note has a `## IS07 — <title>` section (3–8 lines: the 4 sites +
      which redaction each used, the regression class + pass count, what IS08
      consumes next).
- [ ] One commit: `test(improve-sec): IS07 — redact identity + SMTP log templates`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS08's work (the seeders + the log-PII gate) to make
it pass.** A failed check is a recoverable step, not a stopping point: read the
error, adjust within the unit's scope, continue.
