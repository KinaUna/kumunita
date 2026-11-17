---
id: dx17-prompt
title: "DX17 prompt — rewrite testing-and-ci.md to the live gates, with a message-prefix truth gate"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX17** — **phase D, unit 3**
> (DX16 is committed). DX16 slimmed `AGENTS.md` and landed the 6th/7th devx
> rules; DX17 turns `docs/dev/testing-and-ci.md` from a doc that describes a
> **retired** gate into one that is *true* about the gates that exist today,
> and it *pins the truth* by landing the **8th devx rule**: every quoted
> failure-message prefix in the doc's "message → script → fix" table must
> actually occur in `tools/` or `tests/` (so a retired message can't survive
> in the doc). **The one thing to get right:** the table must quote the
> **live** prefixes *verbatim* — verify each one against the real gate output
> before writing it, because a stale or invented prefix fails the new rule, and
> the whole point is that *the doc can't lie about the gates*. **And docs-check
> is already red** for pre-existing reasons, so "docs-check is green" means your
> unit adds **no new** failure. **Who is it for:** the clean-slate agent starting
> DX17. **When to read the rest:** before touching `testing-and-ci.md` or the
> new rule in `devx-check.ps1`.

# DX17 — rewrite `testing-and-ci.md` to the live gates

## What you are doing

You are carrying **DX17** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **E4** evidence row
(*"Testing doc describes a retired gate"*) and the **DX17** unit section first;
they are your spec). `docs/dev/testing-and-ci.md` is the doc a maintainer reads
when a gate is red or a suite misbehaves — so when it lies, a cold agent burns
calls chasing a gate that no longer exists (plan E4: the cold agent found the
quoted message "no longer exists anywhere"). DX17 rewrites that one doc to the
**live** gates and adds a **truth gate** so the doc can't drift back to lying.

This unit edits **one doc** (`docs/dev/testing-and-ci.md`) and **lands one
devx-check rule** (rule 8). It does **not** touch the gates themselves, the
context code, or the product — it *describes* them and *links* to them. The
per-unit contract (plan D8) binds you: read only the named entry reads; edit
only the deliverables (plus the handoff note); run only the named checks;
**never edit around a failing gate.** The red docs/code gates are pre-existing
(recorded in the DX15/DX16 handoffs) and are **not** yours to fix — you only
prove your unit adds **none**.

## Drift facts you must know (verified 2026-11-15, `development` @ `1e13ee88`, post-DX16)

1. **HEAD is `1e13ee88` (DX16).** `testing-and-ci.md` is **115 lines** today —
   the plan's "≤ 120 lines" ceiling is a **tight** margin; the rewrite must stay
   within it. `devx-check.ps1` has **7 rules** (DX04 `ci-triggers`, DX08
   `iclassfixture`, DX09 `iclassfixture-web`, DX12 `vscode-task-dotnet-test`,
   DX15 `code-map`, DX16 `agents-md-tokens` + `read-before-tokens`). **DX17 lands
   the 8th**; the first seven rule blocks stay **byte-untouched** (`$ruleCount`
   goes **7 → 8**).
2. **The doc's `improve-check` description is FALSE — this is the fix.** The
   live `tools/docs/improve-check.ps1` runs gates **b, c, d, g** (gates **a, e,
   f were RETIRED** by IMPROVE-CODE C00 — `a`/`f` (view/`.cs` > 800 lines) and
   `e` (`.ts` > 800 lines) now live in `tools/code/code-check.ps1`). The doc
   *currently* claims `improve-check` "pins `KnownTranslationKeys.cs` to a
   line count … fails the run if the file has grown past the baseline." **That
   gate no longer exists.** The real live `improve-check` gate that fires is
   **gate `b` — ADR index drift** (`docs/adr/README.md` index rows !=
   `docs/adr/0*.md` file count; live: `files=307, index rows=305, missing=(0305,0306)`).
   Replace the `KnownTranslationKeys` pin story with the true gate set.
3. **`improve-check` is ALIVE, not deleted.** DX13 (D9) *moved* `improve-check`
   into the `docs` CI job + `check.ps1`'s `docs` step and **deleted**
   `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs` (the in-test pwsh shell-out).
   So `improve-check.ps1` is a **live** gate to document; `ImproveHarnessTests`
   is **gone** (any doc reference to it is stale — delete it).
4. **The `@media` pin is real, but the doc's number is stale.** The live test is
   `tests/Kumunita.Web.Tests/PwaManifestTests.cs` →
   `M10 U06: site.css media-block boundary pinned (D6)`. It counts
   `@media (max-width: 767.98px)` + `@media (max-width: 575.98px)` +
   `@media (prefers-reduced-motion: reduce)` + `@media (pointer: coarse)` blocks
   (4 + 4 + 1 + 1 = **10** as verified). The doc says "historically six, now
   twelve as of the M10 drift-guard" — **twelve is wrong**; read the test's
   exact expected count and quote it, don't carry the stale "twelve".
5. **`SecGate` is the privacy-gate surface (document it, don't restate it).**
   `tests/Kumunita.Web.Tests/SecGate/` has **6 test files**:
   `AccessViaDenyMatrixTests`, `ControlMapTraceabilityTests`,
   `CspInlineDisciplineGateTests`, `DataClassCoverageTests`,
   `DecisionFreeReadGateTests`, `LogTemplatePiiGateTests`. **2 are known-reds**
   (both in `DataClassCoverageTests` — the A10 canonical known-reds). The doc
   should point here, not restate what each asserts.
6. **The runner changed meaning.** `check.ps1` (DX10) is the one-command loop and
   runs both suites via the **full in-process** form —
   `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
   and the `Kumunita.Core.Tests.dll` equivalent (not `-class`). `dotnet test` /
   VS Test Explorer is the *broken* discovery path (the DX12 devx rule even bans
   a VS Code task running `dotnet test`). Reframe the doc around **`check.ps1`**
   as the runner, with the `dotnet exec <dll>` form as the targeted alternative.
7. **The timings are stale (E3) — replace with measured numbers.** The doc says
   Web "well under a second" and Core "~20 s". **Measure them live** on this
   machine (run the two `dotnet exec` commands, or read the `check.ps1` timing
   table from a `-Only web,core` run) and record the real seconds — do **not**
   carry the old numbers and do **not** guess. The handoff notes carry prior
   measurements (Web ~22-24 s, Core ~175-178 s) as a sanity bound, but your
   number is the one you measured.
8. **The new rule (8) gates the message table — it must be reproducible.** Pin the
   definition in the rule's comment: it reads `docs/dev/testing-and-ci.md`, finds
   the **"message → script → fix"** table, extracts the **backtick-quoted**
   failure-prefix in each row's message column, and requires each to be a
   **literal substring** of the combined text of `tools/**/*.ps1` + `tests/**/*.cs`
   (exclude `obj/`/`bin/`). A **retired** message (the old `KnownTranslationKeys`
   line-count pin, `gate f` "view over 800", `ImproveHarnessTests`) **cannot
   survive** — it fails because it occurs nowhere. Every **live** message you
   quote (the `docs-check`/`improve-check`/`code-check`/`devx-check` prefixes +
   the `@media` pin test name) **must** resolve. Bump `$ruleCount` **7 → 8**; the
   first seven rule blocks stay **byte-untouched**. Follow the existing rule-block
   shape exactly (comment header naming the unit + intent, then the measurement,
   then `Check`, then the bump).
9. **`docs-check` is RED for pre-existing reasons — byte-identity is your exit.**
   Live today (verify with the gate, don't assume): **front-matter 17, ADR >250
   1, broken links 19, orphans 14, archive 50** + `improve-check` gate `b`
   (`0305`/`0306`). That set is **not** yours to fix. Rewriting `testing-and-ci.md`
   is a *living, reachable, already-front-mattered* doc, so the counts should be
   **byte-identical** — your one real risk is introducing a **broken relative
   link** (keep every `[…](…)` a valid path). Confirm explicitly you added **no
   new** gate-4 broken link and **no new** gate-5 orphan.
10. **The live gate message prefixes (verified 2026-11-15)** — these are the exact
    strings to quote in the table (each *is* the gate's own message): `docs-check`
    → `front-matter on every doc`, `Abstract on every non-ADR doc >60 lines`,
    `ADR ≤250 lines`, `zero broken relative .md links`, `every living doc
    reachable from an entry page`, `archive frozen — every archive doc
    tier:archive`; `improve-check` → `ADR index drift — new beyond U01 baseline`;
    `code-check` → (read `tools/code/code-check.ps1` for its own prefix);
    `devx-check` → one line per rule name (e.g. `ci-triggers`, `code-map`,
    `agents-md-tokens`, `read-before-tokens`); the `@media` pin → the
    `PwaManifestTests` test `DisplayName` (`site.css media-block boundary
    pinned`). **Verify each against the live gate output before writing it** — the
    rule (8) will check them.

## DX17 deliverables

- **`docs/dev/testing-and-ci.md`** (rewrite, ≤ 120 lines):
  - **The runner:** link `tools/check.ps1` as the one-command loop; give the
    targeted in-process form `dotnet exec tests/…/Kumunita.<X>.Tests.dll`; name
    `dotnet test` / VS Test Explorer as the **broken** discovery path (why: the
    xunit.v3 discovery bug the doc already documents).
  - **Measured timings:** the Web + Core seconds you measured live (drift fact 7).
  - **The live gates:** `docs-check` (6 gates), `improve-check` (gates **b, c, d,
    g** — *not* the retired `a/e/f`), `code-check` (the Roslyn ratchet),
    `devx-check` (8 rules), the `SecGate` tests (6 files, point don't restate),
    and the `@media` pin test — each with a one-line "what it guards" + the link.
  - **A message → script → fix table:** one row per live gate failure a maintainer
    will meet. Columns: the **exact failure prefix** (backtick-quoted, verbatim
    from the gate) | the **script/test** that emits it | the **sanctioned fix**
    (the "re-baseline + annotate the ADR" rule for the pins; the install/owner-ops
    line for the environmental ones). This is what the new rule (8) gates.
  - **The CI known-failure cheatsheet, trimmed to the current items:** the
    environmental CI ones that are still true (verify each — `dotnet/setup-dotnet`
    org, the e2e `chromium` step, the local `libnss3/libnspr4/libasound2t64`
    line, the `Node 20 is deprecated` pin). Drop anything no longer true.
  - **Delete** the retired text: the `KnownTranslationKeys.cs` line-count pin
    story, the `gate f` / "view over 800" reference, and any `ImproveHarnessTests`
    mention (drift facts 2, 3).
  - Keep the front-matter, the Abstract, the "who is it for / when to read"
    trailer, and the "by design — re-baseline + annotate, never loosen the gate"
    doctrine (that part is still true and is the point).
- **`tools/devx/devx-check.ps1`** — **rule 8** (`testing-and-ci-messages`),
  appended after the `read-before-tokens` block, per drift fact 8. Measure,
  `Check`, bump `$ruleCount` to **8**. The first seven rule blocks stay
  byte-untouched.
- **The handoff note** — a `## DX17 — rewrite testing-and-ci.md` section (below).

## DX17 exit (the named checks)

- **The new rule lands and passes:** `pwsh tools/devx/devx-check.ps1` → **PASS,
  8 rules**, exit 0. Every quoted message prefix in the table resolves to
  `tools/` or `tests/`. Proven the other way: temporarily put a retired message
  (e.g. the old `KnownTranslationKeys` pin text) into the table → devx-check
  **FAILs** (rule 8); restore → PASS.
- **`-Only devx` → PASS, exit 0, 8 rules** (the seven prior rules unregressed —
  especially the DX16 `agents-md-tokens` / `read-before-tokens` still green).
- **`testing-and-ci.md` ≤ 120 lines**; the `KnownTranslationKeys` pin text, the
  `gate f` "view over 800" text, and any `ImproveHarnessTests` reference are
  **gone**; the live gates, the measured timings, the `@media` pin, the `SecGate`
  surface, and the message → script → fix table are **present**.
- **`-Only docs` → FAIL for the pre-existing set, byte-identical** to pre-DX17
  (front-matter **17**, ADR >250 **1**, broken links **19**, orphans **14**,
  archive **50**; improve-check gate `b` `0305`/`0306`). **Record, don't fix.**
  Confirm explicitly that `testing-and-ci.md` added **no new** gate-4 broken
  link and **no new** gate-5 orphan.
- **`-Doctor` → all 5 checks OK, exit 0** (DX11 unregressed).

## Handoff

Append a `## DX17 — rewrite testing-and-ci.md` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15/DX16 sections). It must state: the
**`KnownTranslationKeys` line-count pin story is gone** (that gate does not
exist — `improve-check` runs **b/c/d/g**, and the live firing gate is **`b` ADR
index drift** `0305`/`0306`); the **`gate f` "view over 800"** and any
**`ImproveHarnessTests`** references are deleted (gate `f` retired at C00 →
`code-check`; `ImproveHarnessTests` deleted at DX13); the **runner** is now
`check.ps1` (+ the `dotnet exec <dll>` form, `dotnet test` named as the broken
path); the **stale timings** ("well under a second" / "~20 s") replaced with the
**measured** Web/Core seconds; the **`@media` pin** re-pointed to
`PwaManifestTests` `M10 U06 … media-block boundary pinned` with the **correct**
block count (not the stale "twelve"); the **`SecGate`** surface documented as a
pointer (6 files, 2 known-reds); and that **rule 8 landed**
(`testing-and-ci-messages`: every quoted message prefix in the table must occur
in `tools/` or `tests/`; the first seven rule blocks byte-untouched,
`$ruleCount` **7 → 8**) — proven by the retired-message FAIL probe. Record the
**-Only devx → PASS, 8 rules, exit 0**, **-Doctor → all 5 OK**, and **-Only docs**
reds are **pre-existing and unchanged** (front-matter 17 / ADR>250 1 / broken 19
/ orphans 14 / archive 50; improve-check `b` `0305`/`0306`). Cite **ADR 0307**
(D7, the gate form; E4, the retired-gate finding); the plan body's "0300" is a
stale number (consistent with the DX04–DX16 handoffs).

## Commit

One commit, conventional-commits + lane tag (the lane uses `docs(devx):` for doc
rewrites and `feat(devx):` when a rule lands — DX17 lands **both** a doc rewrite
and rule 8, so `feat(devx):` is the honest type). Stage explicitly
(`git add docs/dev/testing-and-ci.md tools/devx/devx-check.ps1
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx17-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `feat(devx): DX17 — rewrite testing-and-ci.md to the live gates + message→script→fix table + testing-and-ci-messages rule (IMPROVE-DEVX DX17)`

## Definition of Done

1. `testing-and-ci.md` (≤ 120 lines) describes the **live** gates: the `check.ps1`
   runner + the `dotnet exec <dll>` form, the measured timings, `improve-check`
   (b/c/d/g — **not** the retired a/e/f), `code-check`, `devx-check` (8 rules),
   the `SecGate` surface, and the `@media` pin (correct count). The
   `KnownTranslationKeys` pin story, the `gate f` "view over 800" text, and any
   `ImproveHarnessTests` reference are **gone**.
2. **The new rule landed:** `devx-check.ps1` rule 8
   (`testing-and-ci-messages`: every backtick-quoted failure-prefix in the
   message → script → fix table must be a literal substring of `tools/**/*.ps1`
   + `tests/**/*.cs`; the definition pinned in a comment); `$ruleCount` is **8**;
   the first seven rule blocks are **byte-untouched**.
3. **`-Only devx` → PASS, exit 0, 8 rules** (a planted retired message makes
   rule 8 **FAIL**, restore → PASS); **`-Doctor` → all 5 OK, exit 0**; the DX16
   token rules are still green.
4. **`-Only docs` reds are pre-existing and unchanged** (front-matter 17 /
   ADR>250 1 / broken links 19 / orphans 14 / archive 50; improve-check gate `b`
   `0305`/`0306`); `testing-and-ci.md` adds **no new** gate-4 broken link and
   **no new** gate-5 orphan.
5. `git status --short` shows the one doc, `devx-check.ps1`, and the handoff note
   (plus the untracked prompt briefs); one commit on `development`.
