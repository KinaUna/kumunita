---
id: dx16-prompt
title: "DX16 prompt — slim AGENTS.md onto the code map, with two token-cap gates"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX16** — **phase D, unit 2**
> (DX15 is committed; DX16 is the one that *links the map in* and then proves
> the entry is cheap). DX10–DX15 are done: `tools/check.ps1` is the one-command
> loop, `code-map.ps1` writes the 30-row `docs/dev/code-map.md`, and the `devx`
> gate already lands **5 rules**. DX16 turns `AGENTS.md`'s hand-listed 7-of-30
> context list into a link to the map, adds a three-line `## Commands` block,
> rewords "Start here" into *read-when* triggers, and **lands the 6th and 7th
> devx rules**: a cap on `AGENTS.md` itself (≤ 2 000 tokens, today ≈ 2 096) and
> a cap on the "read before your first change" chain (≤ 8 000 tokens, today
> ≈ 29 000). **The one thing to get right:** the two new rules *are* the
> deliverable that makes the slimming stick — they must be **reproducible**
> (same estimator, same chain definition, every run), or the gate passes on a
> whim and silently re-bloats. And **docs-check is already red** for
> pre-existing reasons, so "docs-check is green" means *your* unit adds **no
> new** failure. **Who is it for:** the clean-slate agent starting DX16.
> **When to read the rest:** before touching `AGENTS.md`, `docs/start/agent.md`,
> or the two new rules in `devx-check.ps1`.

# DX16 — slim AGENTS.md onto the code map

## What you are doing

You are carrying **DX16** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D4** decision row
(code map) and the **DX16** unit section first; they are your spec). `AGENTS.md`
is loaded into **every** agent session, so its length is a per-session cost the
lane keeps measuring (plan E7: a cold agent burned **123 K tokens** partly
because the entry forced a long mandatory read chain). DX15 built the map;
**DX16 is the unit that links it in** and makes the entry cheaper, and it
*proves* the cheapness with two shrink-only token rules so it can't rot.

This unit edits **two docs** (`AGENTS.md`, `docs/start/agent.md`) and **lands
two devx-check rules**. It does **not** touch the map (DX15's), the context
code, or the product. The per-unit contract (plan D8) binds you: read only the
named entry reads; edit only the deliverables (plus the handoff note); run only
the named checks; **never edit around a failing gate.** The red docs/code gates
are pre-existing (the DX15 handoff records them) and are **not** yours to fix —
you only need to prove your unit adds **none**.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX15)

1. **HEAD is `bf02cd6a` (DX15).** DX15 landed `tools/devx/code-map.ps1` (196
   lines), the generated `docs/dev/code-map.md` (**30 rows**, `tier: reference`),
   and the **fifth** devx rule (`code-map`, `$ruleCount` 4 → 5). The five rules
   are: `ci-triggers` (DX04), `iclassfixture` (DX08), `iclassfixture-web` (DX09),
   `vscode-task-dotnet-test` (DX12), `code-map` (DX15). **DX16 appends rules
   6 and 7**; the first five rule blocks stay **byte-untouched**
   (`$ruleCount` goes **5 → 7**).
2. **`AGENTS.md` today ≈ 2 096 tokens** (8 383 chars ÷ 4, the estimator you will
   pin in the rule) — it **already exceeds** the 2 000 cap the plan pins. So rule
   6 is *not* a "hold the line" gate; it forces a small trim. The plan's "2 052
   today" is the 2026-11-09 figure; trust the live number.
3. **The "read-before" chain today ≈ 29 000 tokens** — the sum of `AGENTS.md`'s
   own `## Start here` links (the docs an agent reads up front): `README.md`
   (≈1 440), `docs/ARCHITECTURE.md` (**≈11 307** — the heavy one), ADR 0004
   (≈2 344), ADR 0006 (≈2 954), `docs/SECURITY.md` (≈1 105),
   `docs/DOCS-CONVENTIONS.md` (≈1 393), `docs/dev/branch-flow.md` (≈552),
   `docs/dev/razor-verification.md` (≈1 550), `docs/dev/testing-and-ci.md`
   (≈1 384), `docs/dev/live-server-debugging.md` (≈687),
   `docs/dev/agent-environment-quirks.md` (≈2 228). The 8 000 cap forces the
   mandatory up-front chain to shrink to a few *core* reads; the rest become
   *read-when* pointers (still linked, but no longer "read all of these first").
4. **`Bootstrap/` is in AGENTS.md's context list but is NOT a map row.** It has
   **no `DependencyInjection.cs`** (it is the one-shot seeder /
   `SchemaBootstrap` concern), so it is not one of the 30 self-registered
   contexts. Replacing the hand list with the map link **naturally drops it** —
   that is correct, not a loss. Do not try to keep it or to make the map claim
   it.
5. **`docs/dev/code-map.md` exists and is fresh** (`code-map.ps1 -Check` → exit 0,
   30 rows). **This unit links it** — that is the whole reason DX15 gave it
   `tier: reference` instead of `tier: living` (a `living` doc is a gate-5
   orphan until linked). Once `AGENTS.md` links it, it becomes reachable and is
   no longer an orphan candidate. Linking it does **not** change the orphan
   count (it was already exempt as `reference`), but it is the *intended* link.
6. **The `devx` gate has 5 rules; DX16 lands rules 6 and 7.** Each is a block
   that calls `Check` and then bumps `$ruleCount` (currently the last bump is
   `$ruleCount = 5` after `code-map`). **Append two blocks** after the `code-map`
   block: a token cap on `AGENTS.md`, and a token cap on the read-before chain.
   Follow the existing rule-block shape exactly (comment header naming the unit
   and intent, then the measurement, then `Check`, then the `$ruleCount` bump).
   The first five rule blocks stay **byte-untouched**.
7. **The token estimator must be pinned, not improvised.** Use **one** definition
   and use it in both rules and the report: `tokens ≈ <chars> / 4` (integer
   division), where `<chars>` is the character count of the file (or the sum
   for a chain). This is a *proxy* gate — it tracks growth, not an exact
   tokenizer — and the comment must say so. Do **not** reach for a real
   tokenizer (it would make the rule non-reproducible across machines).
8. **The read-before chain must have ONE reproducible definition** in the rule,
   because "read before your first change" is otherwise fuzzy. Pin it: the
   chain is `AGENTS.md` plus **every relative `.md` file it links from its
   `## Start here` section** (parse that one section, resolve each `[…](…)` to a
   file, sum `chars/4`). Do NOT transclude recursively (that would pull in the
   entire `docs/dev/` tree and make the 8 000 cap unreachable). Record the
   definition in the rule's comment so DX17/DX18 (which read `AGENTS.md`) see
   the same number.
9. **`docs-check` is RED for pre-existing reasons.** Live today (verify with the
   gate, don't assume): `docs-check: 5 GATE(S) FAILED` (front-matter **17**,
   ADR >250 **1**, broken links **18**, orphans **13**, archive **50**) plus
   `improve-check` gate `b` (ADR-index drift, `0305`/`0306`). That set is **not**
   yours to fix. Your realistic exit: the two token rules land and pass, and
   docs-check's failure set is **byte-identical** to pre-DX16 — in particular
   `AGENTS.md` and `docs/start/agent.md` must add **no new** gate-4 broken link
   (keep the map link a valid relative path) and **no new** gate-5 orphan.
10. **`-Doctor` is green** (all 5 checks OK, exit 0) and `tools/check.ps1`'s
    steps are untouched by this unit (DX16 edits `AGENTS.md`,
    `docs/start/agent.md`, and `devx-check.ps1` only). Keep `-Doctor` and the
    DX15 `code-map` rule unregressed.

## DX16 deliverables

- **`AGENTS.md`** — slimmed, in the plan's order:
  - Replace the **7-context list** in "The shape of the code" with **two lines**
    and a link to [`docs/dev/code-map.md`](docs/dev/code-map.md): "one row per
    bounded context (folder, DocTypes, service, controllers, views, tests,
    top ADRs)" — the map is the source of truth for *which* contexts exist.
    Keep the `Kumunita.Web`, persistence, and `Announcements`-is-a-feature-module
    sentences (they are not in the map and are still the must-knows). Drop
    `Bootstrap/` (it is not a context; drift fact 4).
  - Add a **`## Commands`** block of **three lines**: `pwsh tools/check.ps1`
    (the full loop), `pwsh tools/check.ps1 -Fast` (skip the slow `core` step),
    `pwsh tools/check.ps1 -Doctor` (environment self-check). One line each.
  - Turn **"Start here"** into **"read when …"** triggers: keep the links, but
    word each as a *condition* ("read when you touch persistence / when a doc is
    red / before a cross-context call …"), so nothing beyond `AGENTS.md` itself
    is mandatory up front. This is what gets the read-before chain under 8 000
    tokens: the *mandatory* set shrinks to the few that every change needs, the
    rest are conditional.
  - Replace the **~20 s / ~1 min** timings (in "Don't pause mid-task") with
    "see `check.ps1` output" (the plan's E3: the numbers are stale).
  - Keep the front-matter, the Abstract, the "one thing to internalize"
    (Marten/Wolverine version trap), the conventions, and the doc-sync section —
    those are the durable must-knows. The goal is *fewer mandatory up-front
    reads*, not *deleting the doctrine* (it still lives in `docs/dev/`, linked).
- **`docs/start/agent.md`** — the same "read when …" treatment (it is the
  per-audience entry page that points at `AGENTS.md`). It stays **links-only**;
  add the `code-map.md` row (it is the natural home for the map link alongside
  the doctrine links). No new facts.
- **`tools/devx/devx-check.ps1`** — **rules 6 and 7**, appended after the
  `code-map` block:
  - **Rule 6 (`agents-md-tokens`):** `AGENTS.md` ≤ **2 000** `chars/4` tokens.
    Measure, `Check`, bump `$ruleCount` to **6**.
  - **Rule 7 (`read-before-tokens`):** the read-before chain (drift fact 8
    definition) ≤ **8 000** `chars/4` tokens. Measure, `Check`, bump
    `$ruleCount` to **7**.
  - Both use the same pinned estimator (drift fact 7). The first five rule
    blocks stay byte-untouched.
- **The handoff note** — a `## DX16 — slim AGENTS.md` section (see below).

## DX16 exit (the named checks)

- **The two rules land and pass:** `pwsh tools/devx/devx-check.ps1` → **PASS,
  7 rules**, exit 0. `AGENTS.md` ≤ 2 000 tokens; the read-before chain ≤ 8 000
  tokens (both measured by the *rule's own* estimator, not by hand).
- **`-Only devx` → PASS, exit 0, 7 rules** (rules 6+7 land; the first five
  unregressed — especially the DX15 `code-map` rule still green).
- **`-Doctor` → all 5 checks OK, exit 0** (DX11 unregressed).
- **`AGENTS.md` links the map** (`docs/dev/code-map.md`), and that link **resolves**
  (it is a valid relative path — no new gate-4 broken link). The 7-context list
  and the stale timings are **gone**; `## Commands` (three lines) is **present**;
  "Start here" reads as *read-when* triggers.
- **`-Only docs` → FAIL for the pre-existing set, byte-identical** to pre-DX16
  (front-matter **17**, ADR >250 **1**, broken links **18**, orphans **13**,
  archive **50**; improve-check gate `b` `0305`/`0306`). **Record, don't fix.**
  Confirm explicitly that `AGENTS.md` + `docs/start/agent.md` added **no new**
  gate-4 broken link and **no new** gate-5 orphan.

## Handoff

Append a `## DX16 — slim AGENTS.md` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX12/DX13/DX14/DX15 sections). It must state: the
**7-context list is gone** (replaced by a link to `code-map.md` — which
*dropped `Bootstrap/`, correctly, since it is not a DI context); a **three-line
`## Commands` block** added; **"Start here" → "read when …"** triggers; the
**stale ~20 s / ~1 min** timings replaced with "see `check.ps1`"; that the **two
token rules landed** (rule 6 `AGENTS.md` ≤ 2 000 tokens, rule 7 read-before
chain ≤ 8 000 tokens, both using the pinned `chars/4` estimator, `$ruleCount`
**5 → 7**, the first five rule blocks byte-untouched); the **before → after
token numbers** (AGENTS.md ≈ 2 096 → ≤ 2 000; chain ≈ 29 000 → ≤ 8 000); that
**-Only devx → PASS, 7 rules, exit 0**, **-Doctor → all 5 OK**, and
**-Only docs** reds are **pre-existing and unchanged** (front-matter 17 /
ADR>250 1 / broken 18 / orphans 13 / archive 50; improve-check `b` `0305`/`0306`);
and that **DX17 rewrites `testing-and-ci.md`** (the message → script table) —
this unit only *links* the doctrine, it does not rewrite it. Cite **ADR 0307**
(D4, the code map + entry cost); the plan body's "0300" is a stale number
(consistent with the DX04–DX15 handoffs).

## Commit

One commit, conventional-commits + lane tag (the lane uses `feat(devx):` for
new capabilities — DX10/DX11/DX12/DX15):

- `feat(devx): DX16 — slim AGENTS.md onto code-map.md + Commands block + read-when triggers + two token-cap devx rules (IMPROVE-DEVX DX16)`

Stage explicitly (`git add AGENTS.md docs/start/agent.md tools/devx/devx-check.ps1 docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with `git commit -F <file>` (write the message to `.tmp/dx16-commit-msg.txt` first, then `rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `AGENTS.md` is slimmed: the 7-context list (incl. `Bootstrap/`) is gone and
   replaced by a link to `docs/dev/code-map.md`; a **three-line `## Commands`**
   block is present; "Start here" reads as *read-when* triggers; the stale
   `~20 s / ~1 min` timings are replaced with "see `check.ps1`". `docs/start/agent.md`
   carries the same read-when treatment + the map row.
2. **The two devx rules landed:** `devx-check.ps1` rule 6 (`AGENTS.md` ≤ 2 000
   `chars/4` tokens) and rule 7 (read-before chain ≤ 8 000 tokens), both using
   the **pinned** estimator, both with a reproducible chain definition in a
   comment; `$ruleCount` is **7**; the first five rule blocks are
   byte-untouched.
3. **`-Only devx` → PASS, exit 0, 7 rules**; **`-Doctor` → all 5 OK, exit 0**
   (DX11 unregressed); the DX15 `code-map` rule is still green.
4. **`-Only docs` reds are pre-existing and unchanged** (front-matter 17 /
   ADR>250 1 / broken links 18 / orphans 13 / archive 50; improve-check gate `b`
   `0305`/`0306`); `AGENTS.md` + `docs/start/agent.md` add **no new** gate-4
   broken link and **no new** gate-5 orphan.
5. `git status --short` shows the two docs, `devx-check.ps1`, and the handoff
   note (plus the untracked prompt briefs); one commit on `development`.
