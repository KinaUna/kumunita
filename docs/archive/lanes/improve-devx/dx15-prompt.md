---
id: dx15-prompt
title: "DX15 prompt — code-map generator: the one map from a concern to its folder, with a freshness gate"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX15** — **phase D, the
> first unit** (branch C is done; DX15 is the start of the sequential phase
> `DX15 → DX16 → … → DX22`). DX10–DX14 are committed: `tools/check.ps1` is the
> proven one-command loop, `-Doctor` self-checks the environment, and the
> `devx` gate already lands **4 rules**. DX15 adds the **code map** (D4): a
> generator `tools/devx/code-map.ps1` that writes a *generated*
> `docs/dev/code-map.md` with **one row per context** (the Core folder, its
> `*DocTypes.cs`, its `I*Service`, the Web controllers that reference that
> service, the view folders, the test files that reference that service, and
> the top 3 ADRs cited in that folder's comments), plus the **fifth** devx-check
> rule: the map is **fresh** (`code-map.ps1 -Check` passes iff regenerating
> changes nothing). **The one thing to get right:** there are **32**
> `DependencyInjection.cs` files but the map wants exactly **30 rows** — exclude
> the composition-root aggregator and the `Announcements` feature module, and
> the map stays 30. And **docs-check is already red** for pre-existing reasons,
> so "docs-check is green" means *your* unit adds **no new** failure (in
> particular, `code-map.md` must be neither a new gate-5 orphan nor a new
> gate-4 broken link). **Who is it for:** the clean-slate agent starting DX15.
> **When to read the rest:** before writing `code-map.ps1` or the `code-map`
> rule in `devx-check.ps1`.

# DX15 — code-map generator

## What you are doing

You are carrying **DX15** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D4** decision row
(code map) and the **DX15** unit section first; they are your spec). Today, to
go from a *concern* (e.g. "where does inventory data live, which controller
serves it, which test covers it") to the *folders and files*, a maintainer or
agent has to grep by hand. `AGENTS.md` lists only **7 of ~30** contexts, and
its length is a per-session cost (DX16 will slim it once the map exists).
**DX15 builds that map and makes it trustworthy:** a `pwsh` generator walks the
code (contexts, doc types, services, controllers, views, tests, ADR citations)
and writes `docs/dev/code-map.md` — **generated text only, no hand edits** —
and the devx gate fails the moment the map goes stale, so the map and the code
cannot drift apart. This is the "signals without loops" fix for discoverability:
the map is now a *living artifact with a loop*, not a hand-maintained list that
rottes.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables (plus the handoff note); run only the named
checks; **never edit around a failing gate.** The red docs/code gates are
pre-existing (the DX10–DX14 handoffs record them) and are **not** yours to fix —
you only need to prove your unit adds **none**.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX14)

1. **HEAD is `6dfc1a3f` (DX14); branch C is complete.** DX15 is **phase D's
   first** unit and the first unit that builds a *generated* doc. You are
   leaving the parallel-branch phase — DX15 has no in-branch dependencies to
   coordinate with (DX16 is the *next* unit and is the one that links the map
   into `AGENTS.md` / `docs/start/agent.md`; **do not** do DX16's work now).
2. **The 32 → 30 trap (the single most important fact).**
   `find src -name DependencyInjection.cs` returns **32** files, but the map
   must have exactly **30 rows** ("one row per context folder that has a
   `DependencyInjection.cs`"). **Exclude exactly two, by rule, not by luck:**
   - `src/Kumunita.Core/DependencyInjection.cs` — the **composition-root
     aggregator** (`AddKumunitaCore`); it *calls* each context's `Add*`, it is
     **not** a context. Do not give it a row.
   - `src/Kumunita.Core/Announcements/DependencyInjection.cs` — a **feature
     module** on `PostDocTypes`, not a top-level bounded context (AGENTS.md says
     so explicitly). Do not give it a row.
   That leaves **30**. Encode the exclusion as a rule in the generator (e.g.
   "skip the `Kumunita.Core` root folder and the `Announcements` folder"), not
   by hand-picking the file list — otherwise the gate will pass on 30 today and
   silently break when a context is added. **If you find a third file that is
   clearly neither a context nor one of these two, the 30-row exit is the source
   of truth**: adjust the exclusion rule, and record the reasoning in the
   handoff.
3. **`docs/dev/code-map.md` does not exist yet.** `docs/dev/` today holds
   `README.md`, `agent-environment-quirks.md`, `branch-flow.md`,
   `live-server-debugging.md`, `razor-verification.md`, `running-locally.md`,
   `testing-and-ci.md`. `tools/devx/` holds `devx-baseline.json`,
   `devx-check.ps1`, `devx-report.ps1`. DX15 adds **two** files: the generator
   (`tools/devx/code-map.ps1`) and the generated artifact (`docs/dev/code-map.md`).
4. **The devx gate has 4 rules; DX15 lands the 5th.** `devx-check.ps1`
   currently lands: `ci-triggers` (DX04), `iclassfixture` (DX08),
   `iclassfixture-web` (DX09), `vscode-task-dotnet-test` (DX12). Each is a block
   that calls `Check` and then bumps `$ruleCount` (currently `1 → 2 → 4`).
   **DX15 appends a `code-map` rule** that runs `code-map.ps1 -Check` and passes
   iff it exits 0 (regeneration produces no diff); `$ruleCount` goes **4 → 5**.
   Follow the existing rule-block shape exactly (comment header naming the unit
   and the intent, then the check, then the `$ruleCount` bump). The first four
   rule blocks stay **byte-untouched**.
5. **`-Check` semantics (the rule's whole meaning):** `code-map.ps1 -Check`
   regenerates the map into a **scratch location** and compares it to the
   committed `docs/dev/code-map.md`; it exits **0** if identical and **1** (with
   a one-line diff hint) if they differ. The *non-flag* run writes the file. So
   the "generated text only, no hand edits" invariant is enforced by the gate:
   hand-editing the map → `-Check` fails → devx fails.
6. **docs-check is RED for pre-existing reasons — the plan's "docs-check is
   green" exit predates these reds.** Live today: `docs-check: 5 GATE(S) FAILED`
   (front-matter **17**, ADR >250 lines **1**, broken links **17**, living
   orphans **12**, archive-not-frozen **50**) plus `improve-check` gate `b`
   (ADR-index drift, `0305`/`0306`). That set is **not** yours to fix. Your
   realistic exit is: the map has **30 rows** and docs-check's failure set is
   **byte-identical** to pre-DX15 — in particular `code-map.md` must add **no
   new gate-5 orphan** and **no new gate-4 broken link**.
7. **How to keep `code-map.md` from tripping the docs gates (the subtlety).**
   Gate 4 flags only *relative `.md` markdown links* to files that don't exist;
   gate 5 flags only **`tier: living`** docs unreachable from a `docs/start/`
   entry page. Two safe ways to avoid a new orphan — pick **one** and record it:
   - **(a) preferred:** give `code-map.md` a **`tier: reference`** front-matter
     (the precedent is the *generated* ADR index `docs/adr/README.md`, which is
     `tier: reference`). A `tier: reference` doc is **exempt** from the living
     orphan gate, and it still passes gate 1 (front-matter present). This is the
     right fit because DX15 does **not** link the map anywhere — **DX16 is the
     unit that links it** into `AGENTS.md` / `docs/start/agent.md`.
   - **(b) alternative:** keep it `tier: living` and add a row/link to
     `docs/dev/README.md` (which is reachable from `docs/start/`). Only choose
     this if the owner wants DX15 to also do the link (the plan assigns the link
     to DX16, so default to (a)).
   Either way: reference source paths inside the map as **plain text / inline
   code, not markdown links** (a `[…](src/…)` or `[…](../adr/0004.md)` link
   would be a new gate-4 candidate — the ADR "links" column is a **list of ADR
   numbers**, not hyperlinks).
8. **The `-Doctor` bar (DX11) is green** — all 5 checks OK, exit 0. Keep it
   unregressed (DX15 touches neither `check.ps1`'s `-Doctor` nor its steps).
9. **No `devx-baseline.json` value is required for the `code-map` rule** — it
   is a regenerate-and-diff freshness check, not a shrink-only number (contrast
   DX08/DX12, which added baseline values). Leave `devx-baseline.json`
   untouched unless you add an *optional* traceability entry; the rule does not
   read one.

## DX15 deliverables

- **`tools/devx/code-map.ps1`** (≤ 200 lines). It:
  - Walks `src/Kumunita.Core/`, emitting **one row per context folder that has
    its own `DependencyInjection.cs`**, **excluding** the `Kumunita.Core` root
    (the aggregator) and `Announcements` (the feature module) — the 30 rows.
  - For each row, the columns (the plan's list, in order):
    1. the **Core folder** (e.g. `Inventory`);
    2. the **`*DocTypes.cs`** file or files (if any — some contexts have none;
       leave the cell empty, not a placeholder);
    3. the **`I*Service`** (the public service interface the context exposes);
    4. the **Web controllers** in `src/Kumunita.Web/Controllers/` that reference
       that service;
    5. the **view folders** in `src/Kumunita.Web/Views/` that correspond;
    6. the **test files** that reference that service (across
       `tests/Kumunita.Core.Tests/` and `tests/Kumunita.Web.Tests/`) — these are
       flat **files** for now; Phase E (DX25–DX30) turns them into folders, and
       DX30 is the unit that points this column at the folders. Name the files.
    7. the **top 3 ADR numbers** cited in that folder's comments (scan the
       folder's `.cs` for ADR citations — `ADR 0xxx` / `adr/0xxx` / a bare
       `0xxx` adjacent to "ADR" — and list the three most-cited; empty if none).
  - Writes `docs/dev/code-map.md` with **front-matter** (`id: dev-code-map`,
    `title`, `tier: reference` per drift fact 7, `kind`, `status`, `audiences`,
    `lane: improve-devx`, `updated:`) **plus an Abstract** (gate 1 requires
    front-matter on every doc; gate 2 requires an Abstract on non-ADR docs over
    60 lines — the map will be ~one row per line of 30 + header, so keep the
    Abstract or stay under the 60-line threshold; include one to be safe), and a
    **`> generated by `tools/devx/code-map.ps1` — do not hand-edit` marker line**
    so a reader and the reviewer both see it is generated.
  - **`-Check` mode:** regenerate to a temp file, diff against the committed
    map, exit **0** if identical / **1** otherwise (with a one-line "run
    `code-map.ps1` to regenerate" hint). Default (no flag) writes the file.
  - **Generated text only:** every cell is derived from the code; there is no
    hand-written prose in the rows.
- **`docs/dev/code-map.md`** — the artifact the generator writes (the first run
  produces the 30-row map). It is **generated**, not authored: the proof is that
  `code-map.ps1 -Check` passes against it.
- **`tools/devx/devx-check.ps1`** — the **fifth** rule, `code-map`, appended
  after the `vscode-task-dotnet-test` block: run `code-map.ps1 -Check`, pass iff
  exit 0; bump `$ruleCount` **4 → 5**. The first four rule blocks stay
  byte-untouched.
- **The handoff note** — a `## DX15 — code-map generator` section (see below).

## DX15 exit (the named checks)

- **The map has 30 rows.** `code-map.ps1` (default run) writes
  `docs/dev/code-map.md`; count the context rows → **30** (not 32 — the
  aggregator and `Announcements` are excluded). Every one of the 30 context
  folders is present; none is missing.
- **`-Check` is fresh:** `pwsh tools/devx/code-map.ps1 -Check` → **exit 0**
  (regeneration produces no diff against the committed map).
- **`-Only devx` → PASS, exit 0, 5 rules** (the new `code-map` rule lands; the
  first four unregressed).
- **`-Doctor` → all 5 checks OK, exit 0** (DX11 unregressed).
- **`-Only docs` → FAIL for the pre-existing set, byte-identical** to pre-DX15
  (front-matter **17**, ADR >250 **1**, broken links **17**, orphans **12**,
  archive **50**; improve-check gate `b` `0305`/`0306`). **Record, don't fix.**
  Confirm explicitly that `code-map.md` added **no new** gate-4 broken link and
  **no new** gate-5 orphan (i.e. both counts are unchanged from pre-DX15).

## Handoff

Append a `## DX15 — code-map generator` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX12/DX13/DX14 sections). It must state: the
**generator** (`tools/devx/code-map.ps1`, ≤ 200 lines) writes the
**generated** `docs/dev/code-map.md` with **30 rows** — and *why 30 and not 32*
(the `Kumunita.Core` composition-root aggregator and the `Announcements`
feature module are excluded by rule); that the **`code-map` devx rule landed**
(`-Check` freshness; `$ruleCount` **4 → 5**, the first four rule blocks
byte-untouched); that the map's front-matter is **`tier: reference`** (precedent:
the generated ADR index) so it adds **no new gate-5 orphan**, and its ADR
"links" column is a **list of numbers, not markdown links** (so **no new
gate-4 broken link**); that **`-Only devx` → PASS, 5 rules, exit 0**, **`-Doctor`
→ all 5 OK**, and **`-Only docs`** reds are **pre-existing and unchanged**
(front-matter 17 / ADR>250 1 / broken 17 / orphans 12 / archive 50; improve-check
`b` `0305`/`0306`); and that **DX16 is the unit that links the map** into
`AGENTS.md` / `docs/start/agent.md` (this unit did **not** do that). Cite **ADR
0307** (D4, the code map); the plan body's "0300" is a stale number (consistent
with the DX04–DX14 handoffs).

## Commit

One commit, conventional-commits + lane tag (the lane uses `feat(devx):` for
new capabilities — DX10/DX11/DX12):

- `feat(devx): DX15 — code-map generator + docs/dev/code-map.md (30 contexts) + the code-map freshness rule (IMPROVE-DEVX DX15)`

Stage explicitly (`git add tools/devx/code-map.ps1 docs/dev/code-map.md tools/devx/devx-check.ps1 docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with `git commit -F <file>` (write the message to `.tmp/dx15-commit-msg.txt` first, then `rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `tools/devx/code-map.ps1` (≤ 200 lines) generates `docs/dev/code-map.md` —
   **30 rows**, one per context folder that has a `DependencyInjection.cs`,
   **excluding** the `Kumunita.Core` aggregator and the `Announcements` feature
   module. Each row carries the 7 plan columns (Core folder; `*DocTypes.cs`;
   `I*Service`; Web controllers; view folders; test files; top 3 ADRs).
2. **Generated, not hand-written:** the ADR column is a list of numbers (not
   markdown links); every cell is derived from the code; a `> generated by`
   marker is present. `code-map.ps1 -Check` → **exit 0**.
3. **The `code-map` devx rule landed:** `devx-check.ps1` runs `code-map.ps1
   -Check`; `$ruleCount` is **5**; the first four rule blocks are byte-untouched.
4. **`-Only devx` → PASS, exit 0, 5 rules**; **`-Doctor` → all 5 OK, exit 0**
   (DX11 unregressed).
5. **`-Only docs` reds are pre-existing and unchanged** (front-matter 17 /
   ADR>250 1 / broken links 17 / orphans 12 / archive 50; improve-check gate `b`
   `0305`/`0306`); the new `code-map.md` adds **no new** gate-4 broken link and
   **no new** gate-5 orphan (front-matter is `tier: reference`).
6. `git status --short` shows the new generator, the generated map,
   `devx-check.ps1`, and the handoff note (plus the untracked prompt briefs);
   one commit on `development`.
