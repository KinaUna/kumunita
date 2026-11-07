---
id: is09-prompt
title: "IS09 prompt — the trust-boundary + log-hygiene docs (Sentry B7, the B4 class-(b) note, split the L8c log-hygiene item)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS09 — Trust-boundary + log-hygiene docs

## What you are doing

You are the **ninth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS08 are committed. The
**code** half of S3/E3 is done: IS05 set the Sentry breadcrumb floor, IS06
built `LogRedact`, and IS07/IS08 redacted all seven PII templates and added the
`LogTemplatePiiGateTests` gate. Your unit is **IS09** — the **documentation**
half: make the security *records* match the code. It adds the missing
**Sentry (B7)** trust boundary, records that **class (b) crosses B4** (the
≤ 200-char UGC/DM snippets in notification emails), and makes the **log-hygiene
open item honest** — its PII half is now closed by IS08, its log-forging half
(L8c) is still open. **No code, no build, no test** — this is the "the control
lives in a doc" fix, and it is the last IS-unit that closes an E3 finding
(E11's doc-truth is IS41's, not yours).

**Deliverables (2 docs + the handoff-notes append):**

1. `docs/security/trust-boundaries.md` — the B7 row, the B4 note, and the
   abstract's boundary count (6 → 7).
2. `docs/security/decisions-open-items.md` — split the log-hygiene row into
   its PII half (closed) and its log-forging half (still open).

Each doc's front-matter `updated:` date bumps to **2026-11-07**. You touch
**only** these two docs and the handoff notes.

**The 3 edits (verify each anchor with the read, then edit):**

| Doc:line | Current | Change |
|---|---|---|
| `trust-boundaries.md:15` (abstract) | "the six boundaries (B1–B6)" | "the seven boundaries (B1–B7)" and add "…and a third-party error tracker" to the boundary list |
| `trust-boundaries.md:32` (B4 row) | "…provider can read email bodies — see [class (a)](data-classes.md)" | keep it; **add** that notification/DM emails also carry ≤ 200-char UGC/DM snippets, so **class (b)** crosses B4 too |
| `trust-boundaries.md:34` (after B6) | (end of table) | **add a B7 row** (Sentry) in the same 4-column shape |
| `decisions-open-items.md:175` (L8c row) | "Log hygiene: … — **still open (SEC L8c …)**" | split it: the **PII half is decided and closed by IS08**; the **log-forging half (L8c) stays open and says so** |

**The B7 row (Sentry).** Match the table's 4 columns (`# | Boundary | Who
controls it | Protection / notes`), third-party bold like B5. The facts are all
true at HEAD — use them, do not invent:

- **Boundary:** App ↔ Sentry (third-party error tracker).
- **Who controls it:** Sentry SaaS (third party) — bold, like B5.
- **Protection / notes:** **DSN-gated** — `SENTRY_DSN` unset means the
  `UseSentry` call is not executed and an instance is byte-identical to before
  the seam (`Program.cs` `SENTRY_DSN` guard, ≈ :449). When enabled:
  `MinimumBreadcrumbLevel = LogLevel.Warning` (the IS05 floor — nothing below
  Warning reaches the third party, so no PII templates do), `SendDefaultPii =
  false`, the request scrubber redacts headers/cookies/query, and log templates
  carry no PII (the IS08 gate).

**The B4 note.** The row already says class (a) (email bodies) crosses B4.
Append that **class (b)** also crosses it: notification and DM/mention emails
carry a ≤ 200-character UGC/DM snippet (the `UgcSnippets.Truncate` default cap,
`src/Kumunita.Core/Notifications/UgcSnippets.cs:23`). Keep the existing
`[class (a)](data-classes.md)` link and add the class (b) fact beside it.

**The L8c split.** One current row is doing two jobs. Make it honest:
- **PII half** — "no PII in log templates / nothing below Warning reaches a
  third party" — is **decided and closed by IS08** (the
  `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` gate + the
  IS05 breadcrumb floor). Mark it closed and link the gate.
- **Log-forging half** — "sanitize user-supplied newlines/CR before logging so
  logs can't be forged" (SEC L8c, parked 2026-10-31) — **stays open** and says
  so (no owner decision).

You may reword the single row into two, or annotate the one row — either way a
reader must see *one half closed (IS08), one half still open (L8c)*. Do not
close L8c; do not open a new finding.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS08 is the current HEAD**
  (`test(improve-sec): IS08 — redact the seeders + the log-PII gate`). The code
  it documents already exists: `grep -rn "{Email}" src/Kumunita.Core/` →
  **0 hits** (IS07+IS08 anchors) and the gate
  `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs` passes 1/1.
- `trust-boundaries.md` is 39 lines; the B1–B6 table is at `## 2. Trust
  boundaries` (≈ :22–:34). **B7 does not exist yet** (`grep -n "^| B7"
  docs/security/trust-boundaries.md` → 0 hits) — adding it is your job.
- The Sentry facts are in `src/Kumunita.Web/Program.cs` ≈ :449–:470 (the
  `UseSentry` block) and `:1371` (`SentryPiiScrubber`).
- **No build, no test.** This is a docs-only unit (§2.3 records IS09 as "no
  test — docs only; the B7/B4 rows are doc-truth pinned by IS41").
- Commit style (docs prefix): `docs(improve-sec): IS09 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS09** unit spec under "Phase A — S3" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract (2 docs, no build).
3. `docs/design/improve-sec-design.md` **§2.3** (IS09 = "no test — docs only")
   and the **IS09** row of **§2.4** (anchors: `grep -n "^| B7"
   docs/security/trust-boundaries.md`; the B4 row carries "class (b)").
4. `docs/security/trust-boundaries.md` (whole — 39 lines) and
   `docs/security/decisions-open-items.md` **lines 160–190** (the open-items
   table; the L8c row at ≈ :175).
5. The Sentry block: `sed -n '449,470p' src/Kumunita.Web/Program.cs` (to copy
   the B7 facts accurately) and `grep -n "SentryPiiScrubber" -A3 src/Kumunita.Web/Program.cs`.
6. The snippet cap: `sed -n '1,25p' src/Kumunita.Core/Notifications/UgcSnippets.cs`
   (the `Truncate(string?, int max = 200)` default) for the B4 note.
7. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS05` (Sentry floor) and `## IS08` (the gate) sections — what the docs
   must now reference.

## The work

1. **B7 row** — append it after B6 in the same 4-column shape; update the
   abstract's "six (B1–B6)" to "seven (B1–B7)". Bold the third-party "Who
   controls it" cell, exactly as B5 does.
2. **B4 note** — add the class (b) UGC/DM snippet fact to the existing B4 row,
   keeping its class (a) link.
3. **L8c split** — make the log-hygiene item show the PII half closed (IS08)
   and the log-forging half (L8c) still open.
4. **Dates** — bump both docs' `updated:` to `2026-11-07`.

**You are NOT** doing IS41 (the `data-classes.md` / `WhatsNew` token-at-rest /
AGENTS.md-rules doc-truth pass), IS39 (the control-map rewrite — the B7 control-map
row lands **there**, not in this unit), or any `src/**` code. You touch **only**
`trust-boundaries.md`, `decisions-open-items.md`, and the handoff notes.

## Verification (all green before you finish)

- [ ] `grep -n "^| B7" docs/security/trust-boundaries.md` → **1 hit** (the new
      row), and it names DSN-gated, the Warning floor, and no PII templates.
- [ ] The B4 row now carries **class (b)** (the snippet note) *and* keeps the
      class (a) link.
- [ ] `grep -n "six boundaries\|seven boundaries"
      docs/security/trust-boundaries.md` → **seven** (B1–B7).
- [ ] The log-hygiene item in `decisions-open-items.md` reads as **two halves**:
      PII closed by IS08 (linking the gate), log-forging (L8c) **still open**.
- [ ] Both front-matter `updated:` dates are `2026-11-07`.
- [ ] No `src/**` file changed, no code, no test. `git status` shows **only**
      the 2 docs and the handoff-notes append.

## Definition of done (IS09)

- [ ] A reader of `trust-boundaries.md` sees **B7 (Sentry)** as a real boundary —
      DSN-gated, Warning floor, scrubber, no PII templates — and the **B4** row
      notes that **class (b)** UGC/DM snippets cross it.
- [ ] The log-hygiene open item is honest: **PII half closed (IS08), log-forging
      half (L8c) open** — not a single ambiguous "still open".
- [ ] `git status` shows only the 2 docs + the handoff append; no code, no
      control-map edit (that is IS39's).
- [ ] Handoff note has a `## IS09 — <title>` section (3–8 lines: the B7 row, the
      B4 class-(b) note, the L8c split, that the control-map row is IS39's, and
      what IS41/IS42 consume next).
- [ ] One commit: `docs(improve-sec): IS09 — trust-boundary + log-hygiene docs`.

**If a DoD line fails, stop and say which one and why — do not fix it silently,
and do not start IS41's work to make it pass.** A failed check is a recoverable
step, not a stopping point: read it, adjust within the unit's scope, continue.
If the *same* check failed twice, change approach — don't retry the identical
edit a third time.
