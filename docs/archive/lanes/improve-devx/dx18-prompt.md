---
id: dx18-prompt
title: "DX18 prompt — write docs/dev/recipes.md, the six how-to recipes, with a path-exists truth gate"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX18** — **phase D, unit 4**
> (DX17 is committed). DX17 rewrote `testing-and-ci.md` to the live gates and
> landed the 8th devx rule. DX18 writes the doc the cold-start agent *asked
> for* — **`docs/dev/recipes.md`**, six ≤ 25-line how-tos (add a UI string; add
> a field to a doc type; add a new doc type; add a controller + view; add a
> test and where it goes; read a red CI run) — and it *pins the truth* by
> landing the **9th devx rule**: every repo path the recipes quote must
> actually exist (so a stale path can't survive in the doc). **The one thing to
> get right:** each recipe's files/tests/gates/command must be **true today** —
> verify every path against the live tree and quote the *real* gate names and
> *real* runner form (the `dotnet exec <dll>` loop, not `dotnet test`) before
> writing it, because the whole point is that *a cold agent can copy a recipe
> and have it work the first time*. **And docs-check is already red** for
> pre-existing reasons, so "docs-check is green" means your unit adds **no
> new** failure. **Who is it for:** the clean-slate agent starting DX18.
> **When to read the rest:** before touching `docs/dev/recipes.md`, the new
> rule in `devx-check.ps1`, or the two link lines in `AGENTS.md` /
> `docs/dev/README.md`.

# DX18 — Recipes

## What you are doing

You are carrying **DX18** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **E8** evidence row
(*"No recipes; implicit facts"*) and the **DX18** unit section first; they are
your spec). Plan E8: the cold agent that did three tasks used **32 calls and 7
wrong turns**, and its "wished existed" list started with **recipes** (a
one-hop "add a UI string", "add a field", "add a test and where it goes").
DX18 writes that one doc so the next cold agent stops rediscovering it, and it
*proves the paths are real* with a rule so a recipe can't drift to pointing at
a file that no longer exists.

This unit creates **one doc** (`docs/dev/recipes.md`), **lands one devx-check
rule** (rule 9), and adds **two one-line links** (the `AGENTS.md` `## Read when`
row and the `docs/dev/README.md` table row). It does **not** touch the gates,
the context code, or the product — it *describes* the loop and *links* into it.
The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables (plus the handoff note); run only the named checks;
**never edit around a failing gate.** The red docs/code gates are pre-existing
(recorded in the DX17 handoff) and are **not** yours to fix — you only prove
your unit adds **none**.

## Drift facts you must know (verified 2026-11-16, `development` @ `58a12838`, post-DX17)

1. **HEAD is `58a12838` (DX17).** `devx-check.ps1` has **8 rules** (DX04
   `ci-triggers`, DX08 `iclassfixture`, DX09 `iclassfixture-web`, DX12
   `vscode-task-dotnet-test`, DX15 `code-map`, DX16 `agents-md-tokens` +
   `read-before-tokens`, DX17 `testing-and-ci-messages`). **DX18 lands the 9th**;
   the first eight rule blocks stay **byte-untouched** (`$ruleCount` goes **8 →
   9**). `docs/dev/recipes.md` **does not exist yet** — you create it.
2. **The runner to quote is `check.ps1` / `dotnet exec <dll>`** (the DX17
   rewrite is the source of truth). In `testing-and-ci.md` today: the
   one-command loop is `pwsh tools/check.ps1`; the targeted in-process form is
   `dotnet exec tests/Kumunita.<X>.Tests/bin/Debug/net10.0/Kumunita.<X>.Tests.dll`;
   `dotnet test` / VS Test Explorer is the **broken** discovery path (xunit.v3).
   Every recipe that says "run it" quotes **this** form — never `dotnet test`.
3. **Quote the *live* gates, verbatim** (the same truth DX17 pinned): `docs-check`
   (6 gates), `improve-check` (gates **b/c/d/g**, *not* the retired `a/e/f`),
   `code-check` (the Roslyn ratchet), `devx-check` (9 rules once yours lands),
   the `SecGate` tests (`tests/Kumunita.Web.Tests/SecGate/`, 6 files, 2 known-reds
   in `DataClassCoverageTests`), and the `@media` pin (`PwaManifestTests`
   `M10 U06: site.css media-block boundary pinned`, **12** blocks). If a recipe
   names a gate, it must be one of these — a retired name (gate `f`,
   `KnownTranslationKeys` pin, `ImproveHarnessTests`) fails the cold-read the
   DX17 brief was fixing.
4. **The six recipes are the deliverable; each ≤ 25 lines.** Plan DX18 pins the
   six: **add a UI string** · **add a field to a doc type** · **add a new doc
   type** · **add a controller + view** · **add a test (and where it goes)** ·
   **read a red CI run**. Each lists, for that change: the **files** touched
   (real paths), the **tests** that must stay green, the **gate(s)** that will
   fire if you drift, and the **one command** to run. The "add a UI string"
   recipe must state the surface-naming rule **today** — **surface = lower-case
   controller name**, with the known exceptions (e.g. `inv.*`) — see the D10 row
   and the code-map's context list; do **not** rename anything (D10 default).
5. **The new rule (9) is a path-exists gate — it must be reproducible.** Pin the
   definition in the rule's comment: it reads `docs/dev/recipes.md`, extracts
   every **backtick-quoted repo path**, and requires each to resolve to an
   existing file or directory. A **reproducible** extractor (so the definition
   can't drift): a quoted token counts as a repo path iff it contains a `/` and
   starts with one of the top-level roots `src/`, `tools/`, `tests/`, `docs/`,
   or `e2e/` — this deliberately excludes prose like `keys/en`, `wwwroot/js`,
   or `dotnet exec …` commands. A missing/empty doc (zero path tokens) **fails
   closed** (the recipes are the point; an empty doc can't silently pass). Bump
   `$ruleCount` **8 → 9**; the first eight rule blocks stay **byte-untouched**.
   Follow the existing rule-block shape exactly (comment header naming the unit
   + intent, then the measurement, then `Check`, then the bump).
6. **`docs-check` is RED for pre-existing reasons — byte-identity is your exit.**
   Live today (verify with the gate, don't assume): **front-matter 17, ADR >250
   1, broken links 19, orphans 16, archive 50** + `improve-check` gate `b`
   (`0305`/`0306`). That set is **not** yours to fix — note **orphans is 16**,
   one more than the **15** the DX17 handoff recorded: this brief
   (`dx18-prompt.md`, `tier: living` under `docs/plans-milestones/in-progress/`,
   unreachable from a `docs/start/` entry page) is the +1, and DX37 archives it.
   Your two new risks are: Your two new risks are:
   (a) a **broken relative link** from `recipes.md` (keep every `[…](…)` a valid
   path relative to `docs/dev/`), and (b) a **gate-5 orphan** — `recipes.md` is
   `tier: living`, so it must be **reachable from a `docs/start/` entry page**;
   the `docs/dev/README.md` table row (a relative link, and `README.md` is
   already reachable) is what makes it so. Confirm explicitly you added **no
   new** gate-4 broken link and **no new** gate-5 orphan.
7. **Two token caps you must not break** (the DX16 rules, still green at DX17):
   `AGENTS.md` ≤ **2 000** tokens and the `## Start here` read-before chain ≤
   **8 000** tokens. Add the `recipes.md` link to `AGENTS.md`'s **`## Read
   when`** section (one line) — **not** `## Start here`, because only `## Start
   here` counts toward the 8 000 chain. One line keeps `AGENTS.md` under 2 000.
   Re-run `-Only devx` and both token rules must still read PASS.
8. **The code map is the context source of truth** (DX15): the "add a field to
   a doc type" / "add a new doc type" / "add a controller + view" recipes should
   point at `docs/dev/code-map.md` for *which* context folder a concern lives in
   (one link, don't restate the 30 contexts). The "add a UI string" recipe points
   at the `Localization/Keys/<lang>/` surface. Keep the recipes as *pointers to
   the right place + the one command*, not a copy of the map or the gates.

## DX18 deliverables

- **`docs/dev/recipes.md`** (new, each recipe ≤ 25 lines; the whole doc
  front-mattered `tier: living` with the Abstract, who/when trailer, and the
  six recipe sections above). Every repo path is backtick-quoted so the rule
  (9) checks it; every gate name is a **live** gate; every "run it" command is
  the `check.ps1` / `dotnet exec <dll>` form.
- **`tools/devx/devx-check.ps1`** — **rule 9** (`recipes-paths-exist`), appended
  after the `testing-and-ci-messages` block, per drift fact 5. Measure, `Check`,
  bump `$ruleCount` to **9**. The first eight rule blocks stay **byte-untouched**.
- **`AGENTS.md`** — one line in **`## Read when`**: when you're adding a UI
  string / field / doc type / controller / test, read `docs/dev/recipes.md`.
- **`docs/dev/README.md`** — one table row for `recipes.md` (the "what it
  covers" cell), so the doc is reachable (gate-5) and discoverable (gate-4).
- **The handoff note** — a `## DX18 — recipes` section (below).

## DX18 exit (the named checks)

- **The new rule lands and passes:** `pwsh tools/devx/devx-check.ps1` → **PASS,
  9 rules**, exit 0. Every repo path in `recipes.md` resolves. Proven the other
  way: temporarily put a fake path (e.g. `` `tools/devx/no-such.ps1` ``) in a
  recipe → devx-check **FAILs** (rule 9); restore → PASS.
- **`-Only devx` → PASS, exit 0, 9 rules** (the eight prior rules unregressed —
  especially the DX16 `agents-md-tokens` / `read-before-tokens` still green, and
  the DX17 `testing-and-ci-messages` still green).
- **`recipes.md` is new and conformant:** six recipes, each ≤ 25 lines; live
  gates only; the `dotnet exec <dll>` runner form (never `dotnet test`); the
  "add a UI string" recipe states **surface = lower-case controller name** (+
  the exceptions) and says **rename nothing**; linked from both `AGENTS.md`
  (`## Read when`) and `docs/dev/README.md`.
- **`-Only docs` → FAIL for the pre-existing set, byte-identical** to pre-DX18
  (front-matter **17**, ADR >250 **1**, broken links **19**, orphans **16**,
  archive **50**; improve-check gate `b` `0305`/`0306`). **Record, don't fix.**
  Confirm explicitly that `recipes.md` added **no new** gate-4 broken link and
  **no new** gate-5 orphan (the `docs/dev/README.md` row keeps it reachable);
  the baseline to be byte-identical to is the **16** you measured at session
  start (the DX17 handoff's **15** drifted by this brief, archived at DX37).
- **`-Doctor` → all 5 checks OK, exit 0** (DX11 unregressed).

## Handoff

Append a `## DX18 — recipes` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15/DX16/DX17 sections). It must state:
**`docs/dev/recipes.md` is new** (six recipes, each ≤ 25 lines: add a UI string;
add a field to a doc type; add a new doc type; add a controller + view; add a
test and where it goes; read a red CI run) — each lists files/tests/gates +
the one command, every runner quote is the **`check.ps1` / `dotnet exec <dll>`**
form (not `dotnet test`), and the "add a UI string" recipe states **surface =
lower-case controller name** (with the `inv.*` exception) and says **rename
nothing** (the D10 default); the recipes are **pointers** to `docs/dev/code-map.md`
and the live gates, not a restatement; **`AGENTS.md`** gained one line in
**`## Read when`** (not `## Start here`, to keep the 8 000 read-before chain and
the 2 000 `AGENTS.md` cap green) and **`docs/dev/README.md`** gained one table
row (what keeps `recipes.md` reachable for gate-5); and that **rule 9 landed**
(`recipes-paths-exist`: every backtick-quoted repo path in `recipes.md` must
resolve; the definition is pinned in the rule's comment; a missing/empty doc
fails closed; the first eight rule blocks byte-untouched, `$ruleCount` **8 → 9**)
— proven by the fake-path FAIL probe. Record the **-Only devx → PASS, 9 rules,
exit 0**, **-Doctor → all 5 OK**, and **-Only docs** reds are **pre-existing and
unchanged** (front-matter 17 / ADR>250 1 / broken 19 / orphans 16 / archive 50;
improve-check `b` `0305`/`0306` — the **16** orphans include this brief, one more
than DX17's 15, archived at DX37); `recipes.md` adds **no new** gate-4 broken link
and **no new** gate-5 orphan. Cite **ADR 0307** (D8, the unit shape; E8, the
"no recipes" finding); the plan body's "0300" is a stale number (consistent with
the DX04–DX17 handoffs).

## Commit

One commit, conventional-commits + lane tag (DX18 is a doc + a rule landing, so
`feat(devx):` is the honest type). Stage explicitly
(`git add docs/dev/recipes.md tools/devx/devx-check.ps1 AGENTS.md
docs/dev/README.md docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`),
commit with `git commit -F <file>` (write the message to
`.tmp/dx18-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `feat(devx): DX18 — docs/dev/recipes.md (six how-to recipes) + AGENTS/README links + recipes-paths-exist rule (IMPROVE-DEVX DX18)`

## Definition of Done

1. `docs/dev/recipes.md` exists (front-mattered, `tier: living`, Abstract +
   who/when), **six recipes, each ≤ 25 lines**: add a UI string; add a field to
   a doc type; add a new doc type; add a controller + view; add a test (and where
   it goes); read a red CI run. Each lists the **files** (real paths), the
   **tests**, the **live gate(s)**, and the **one command** (the `check.ps1` /
   `dotnet exec <dll>` form, never `dotnet test`). "Add a UI string" states
   **surface = lower-case controller name** (+ exceptions) and says **rename
   nothing**.
2. **The new rule landed:** `devx-check.ps1` rule 9 (`recipes-paths-exist`:
   every backtick-quoted repo path in `recipes.md` resolves; definition pinned
   in a comment; missing/empty doc fails closed); `$ruleCount` is **9**; the
   first eight rule blocks are **byte-untouched**.
3. **`-Only devx` → PASS, exit 0, 9 rules** (a planted fake path makes rule 9
   **FAIL**, restore → PASS); **`-Doctor` → all 5 OK, exit 0**; the DX16 token
   rules and the DX17 `testing-and-ci-messages` rule are still green.
4. **`AGENTS.md`** (+ one line in `## Read when`) and **`docs/dev/README.md`**
   (+ one table row) both link `recipes.md`; the 2 000 / 8 000 token caps stay
   PASS.
5. **`-Only docs` reds are pre-existing and unchanged** (front-matter 17 /
   ADR>250 1 / broken links 19 / orphans 16 / archive 50; improve-check `b`
   `0305`/`0306` — the **16** includes this brief, archived at DX37);
   `recipes.md` adds **no new** gate-4 broken link and **no new** gate-5 orphan.
6. `git status --short` shows the new doc, `devx-check.ps1`, the two link edits,
   and the handoff note (plus the untracked prompt briefs); one commit on
   `development`.
