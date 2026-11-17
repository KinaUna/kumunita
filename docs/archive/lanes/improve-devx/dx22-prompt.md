---
id: dx22-prompt
title: "DX22 prompt — ADR index links each ADR (there is NO generator — the plan assumed one) + the 0117↔0191 inventory-ADR relation: linkify the hand-maintained index rows, update gate `b`'s row regex + the reachability metric so the index counts as a reach, cross-link the two inventory ADRs"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX22** — **phase D, unit 8**
> (DX21 is committed, `development` @ `5df9ca6e`). DX21 added the code-map
> `Key surfaces` column (D10). DX22 is the **ADR-index-links + inventory-ADR-
> relation** unit. **The one thing to get right — the drift this brief exists
> to prevent:** the plan's DX22 says *"the generator emits each ID as a link to
> its ADR file; regenerate the index."* **There is no generator.**
> `docs/adr/README.md` is **hand-maintained** (rows added by commit — e.g.
> `7a682151 fix(improve): add missing ADR 0303/0304 index rows`;
> `improve-check` gate `b` pins row count vs file count). So "regenerate the
> index" = a **mechanical linkify** of each data row's `| 0NNN |` ID cell into a
> link to the ADR file (text = the number, href = the ADR file name in the same
> folder). Two couplings follow: linkifying **breaks gate
> `b`** unless its row regex is updated, and the plan's exit (**"0
> unreachable"**) only holds if the reachability metric **counts the index as a
> reach** (it currently *excludes* it). Plus the small part: cross-link
> **0117 ↔ 0191** (both already reached) + one status line in 0191. **No new
> devx rule** (devx-check stays at **9**; the "unreachable" number is a
> *report* line, not a check rule). **Who is it for:** the clean-slate agent
> starting DX22. **When to read the rest:** before touching
> `docs/adr/README.md` or either metric/check script.

# DX22 — ADR index links + inventory ADR relation

## What you are doing

You are carrying **DX22** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX22** unit section
and **DoD line 9** *"the leftovers are gone; ADRs unreachable by link = 0"*
first; they are your spec). The plan's intent, in one sentence: **make the ADR
index the thing that reaches every ADR, so `devx-report` shows 0 unreachable,
and cross-link the two inventory ADRs (0117 the accepted M16 feature; 0191 the
proposed UI scaffolding).** Today the index rows are plain numbers (not links),
the reachability metric deliberately *ignores* the index, and 0117/0191 are not
cross-referenced. DX22 flips all three. It touches **the index (mechanical
linkify) + two scripts + two ADRs**; **no** new devx rule, **no** product code,
**no** ADR number change, **no** `devx-baseline.json` edit.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the DX21
handoff) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `5df9ca6e`, post-DX21)

1. **There is NO ADR-index generator — do not look for one.**
   `docs/adr/README.md` is a hand-maintained table
   (`| 0001 | Core stack & identity model | Accepted |`, one row per ADR,
   ~307 rows). `improve-check` gate `b` is the *only* automation over it: it
   counts rows (`(?m)^\|\s*(0\d{3})\b`) and compares to the `docs/adr/0*.md`
   file set (files=307, index rows=305, missing=`0305`,`0306`). So the
   "generator emits links / regenerate" deliverable = **a mechanical linkify of
   the ID cell**, not a regen. (grep `tools/` for the generator: you will find
   only `improve-check` (gate b), `linkify.ps1` (the *prose* ADR-linking pass,
   which **excludes** `docs/adr/README.md` by rule), `improve-report`, and
   `devx-report` — none of them *write* the index rows.)
2. **Linkifying the rows breaks gate `b` unless its regex is updated.**
   Gate `b`'s row pattern is `(?m)^\|\s*(0\d{3})\b`. A linkified row — the ID
   cell becomes a link, so a `[` sits between the pipe and the number — would
   not match that pattern, so the *current* regex would match **0** rows → gate
   `b` reports all 307 "missing from index" → **FAIL**. The fix is to make the
   `[` optional: `(?m)^\|\s*\[?(0\d{3})\b`. After that, linkified rows still
   count as index rows (row count stays 305, missing stays `0305`,`0306`,
   gate `b` stays red *for its pre-existing reason only*).
3. **The "0 unreachable" exit needs the metric to count the index as a reach.**
   `devx-report.ps1` (the "E12 — ADRs unreachable by link" block) currently
   *excludes* the index from the reach scan: the line
   `if ($d.Name -eq 'README.md' -and $d.Directory.Name -eq 'adr') { continue }`.
   Its own comment says *"the index row in docs/adr/README.md does not count as
   a reach — DX22's gate is that the index itself emits links."* So the
   two changes are **coupled**: linkify the index **and** remove that
   exclusion (so the linkified rows *do* count as a reach). Together they make
   every ADR reached → **0 unreachable** (live today: **53 of 307**). Do only
   one and the number does not move (linkify-without-metric-change → still 53;
   metric-change-without-linkify → the index has no links to count → still 53).
4. **0117 and 0191 are already cross-reached; this is a `related:` link, not a
   supersede.** Verified: both `0117-inventory.md` and `0191-inventory.md` are
   linked from some other doc (neither is in the 53-unreachable set). Their
   front-matter: **0117** `status: accepted` (the M16 Inventory *feature*:
   check-out/check-in + usage history), **0191** `status: proposed` (the
   Inventory *UI scaffolding*; `related:` 0182/0188/0189). Both have
   `supersedes: []` and `superseded_by: []` — so 0191 does **not** supersede
   0117. The plan's "proposed vs superseded" phrasing is the **one `// TODO ❓`**:
   the default is a **`related:` cross-link in both** + a status line in 0191
   stating *"Proposed; related to (not superseding) ADR 0117."* If the owner
   wants a different framing (a re-scope, a supersede), ask — **don't guess**.
5. **No new devx rule — `devx-check` stays at 9.** The
   `adrs_unreachable_by_link` entry in `devx-baseline.json` (value **53**,
   `shrink_only: true`) is a **report line**, not a check rule (it has no
   `devx-check` rule reading it, exactly like `ci_seconds` which has `gate:
   none`). So DX22 **does not touch `devx-baseline.json`**; DX35's before→after
   table reads **53 → 0** (baseline = the before, the report = the live after).
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target, resolved relative
   to the doc's directory. **Backtick paths are NOT extracted.** The DX21
   brief is link-free and added **zero** broken links. So reference every
   `.md` file in this brief **in backticks only** (e.g. `docs/adr/README.md`),
   **never** as `[label](path)`. That way this brief adds **+1 orphan**
   (expected, archived at DX37) and **zero** broken links.
7. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**, ADR
   >250 **1**, broken links **20**, orphans **19**, archive **50**, plus
   `improve-check` gate `b` (`0305`/`0306`). After **this brief** exists,
   orphans read **20**, not 19 — that is the expected +1 (this brief,
   `dx22-prompt.md`), archived at DX37. Your linkified index rows and the two
   ADR edits must add **no new** broken link (the index links are
   same-directory relative links to files that gate `b` already confirms
   exist; the two ADRs already resolve their `related:` paths).
8. **You are not changing the index's *content* (titles, statuses, row order,
   or which rows exist).** Only the **ID cell** of each data row becomes a
   link; the Title and Status cells stay **byte-identical**, and you add
   **no** rows (the two missing rows `0305`/`0306` are gate-`b`'s pre-existing
   red, **not** yours — do not add them).

## DX22 deliverables

- **`docs/adr/README.md`** — linkify each **data row's ID cell**: `| 0NNN |`
  → the ID cell becomes a link to that ADR file (text = the number, href = the
  ADR file name in the same folder), resolving `0NNN` to its real
  `docs/adr/0NNN-*.md` filename (gate `b` guarantees every row's number has a
  file; the href is the actual file name, not the shortened Title cell).
  Title + Status cells **byte-identical**; header row
  (`| ID | Title | Status |`) and separator row untouched; **no rows added or
  removed**. A small one-off (pwsh or python) over the ~307 data rows is the
  mechanical batch; a hand `sed` per row is not.
- **`tools/docs/improve-check.ps1`** — gate `b` row regex only: change
  `(?m)^\|\s*(0\d{3})\b` to `(?m)^\|\s*\[?(0\d{3})\b` (make the `[` optional)
  so linkified rows still count as index rows. Nothing else in gate `b`
  changes; the `0305`/`0306` missing-set and the baseline stay as-is.
- **`tools/devx/devx-report.ps1`** — the "E12 — ADRs unreachable by link"
  block: **stop excluding the ADR index** from the reach scan — remove the
  `if ($d.Name -eq 'README.md' -and $d.Directory.Name -eq 'adr') { continue }`
  line and fix the adjacent comment (the index *is* now a reach, per the
  comment's own "DX22" note). No other change to the metric.
- **`docs/adr/0117-inventory.md`** — add `docs/adr/0191-inventory.md` to
  `related:`.
- **`docs/adr/0191-inventory.md`** — add `docs/adr/0117-inventory.md` to
  `related:` **and** one status line: *"Proposed; related to (not superseding)
  ADR 0117 (the accepted M16 Inventory feature)."* (`// TODO ❓` the exact
  framing per fact 4 — ask, don't guess.)
- **The handoff note** — a `## DX22 — ADR index links + inventory ADR relation`
  section (below).

## DX22 exit (the named checks)

- **`pwsh tools/devx/devx-report.ps1`** → the "unreachable" line reads
  **`unreachable : 0`** (was **53**). This is the primary check (the plan's
  DoD line 9).
- **`improve-check` gate `b` unchanged in verdict:** still
  `missing=(0305,0306), extra=()` (row count preserved — the linkified rows
  still match the updated regex; gate `b` stays red for its **pre-existing**
  reason only, not a new failure).
- **`docs-check` green for the touched files:** the linkified index links
  resolve (same-directory, files that gate `b` confirms exist); the two ADRs'
  `related:` paths resolve; **no new** gate-4 broken link, **no new** gate-5
  orphan beyond this brief.
- **No regression:** `devx-check` still **PASS, 9 rules**, exit 0 (**no 10th
  rule** added); `-Doctor` still **all 5 checks OK**, exit 0.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **20**, orphans **20**, archive **50**;
  improve-check gate `b` `0305`/`0306`): the **+1** orphan (19→20) is **this
  brief** (link-free by fact 6, so it adds **no** broken link; archived at
  DX37). `docs/adr/README.md` is `tier: reference` — it is **not** in the
  gate-4 or gate-5 lists.
- `git status --short` shows the five modified files, plus the untracked
  `dx*-prompt.md` briefs (DX37 archives them).

## Handoff

Append a `## DX22 — ADR index links + inventory ADR relation` section (3–8
lines) to `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in
the established voice (match the DX15/DX16/DX17/DX18/DX19/DX20/DX21 sections).
It must state: **there is no ADR-index generator** (the plan assumed one —
`docs/adr/README.md` is hand-maintained, `improve-check` gate `b` is the only
automation over it), so the deliverable was a **mechanical linkify** of the ID
cells, not a regen; the **two coupled** changes — gate `b`'s row regex
(`\[?` made optional) so linkified rows still count, and the `devx-report`
reachability metric now **counts the index as a reach** (the `README.md …
continue` exclusion removed) — together flipping
**unreachable 53 → 0** (linkify alone or metric alone leaves it at 53);
**0117 ↔ 0191** cross-`related:` (0117 = accepted M16 feature, 0191 = proposed
UI scaffolding; **not** a supersede — both `supersedes: []`), with the exact
framing the one `// TODO ❓`; **no new devx rule** (the "unreachable" number
is a *report* line, `devx-check` still **9**; `devx-baseline.json` **untouched**
— DX35 reads 53 → 0); **no index rows added/removed** (the `0305`/`0306`
missing-set is gate-`b`'s pre-existing red, not DX22's). The `-Only docs` reds
are **pre-existing and unchanged** (the **20** orphans include this brief, one
more than DX21's 19, archived at DX37; `docs/adr/README.md` is `tier:
reference`, so it adds no gate-4/gate-5 entry). Cite **ADR 0307** (D4/E12, the
"ADRs unreachable by link" finding); the plan body's "0300" is a stale number
(consistent with the DX04–DX21 handoffs).

## Commit

One commit. Stage explicitly (`git add docs/adr/README.md
tools/docs/improve-check.ps1 tools/devx/devx-report.ps1
docs/adr/0117-inventory.md docs/adr/0191-inventory.md
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx22-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `docs(devx): DX22 — ADR index links each ADR + 0117↔0191 relation (no generator exists; linkify + gate-b regex + metric) (IMPROVE-DEVX DX22)`

## Definition of Done

1. **`docs/adr/README.md`** data-row ID cells are links (ID cell = a link whose
   text is the number and href is the ADR file name) pointing at the real ADR
   file; Title/Status cells and row order **byte-identical**; **no rows added
   or removed**.
2. **`improve-check` gate `b`** row regex tolerates the link (`\[?`); the
   missing-set stays `0305`,`0306` (pre-existing red, not a new failure).
3. **`devx-report`** "unreachable" reads **0** (was **53**) — the index now
   counts as a reach and every ADR is reached through it.
4. **0117 ↔ 0191** cross-`related:` + one status line in 0191 (Proposed;
   related-to-not-superseding 0117); **no** supersede invented, **no** ADR
   number changed.
5. **No new devx rule** (`devx-check` still **PASS, 9 rules**); `devx-baseline.json`
   **untouched**; `-Doctor` still all 5 OK.
6. **`-Only docs` reds are pre-existing** (17 / 1 / 20 / **20** orphans incl.
   this brief / 50; improve-check `b` `0305`/`0306`); the index + the two ADRs
   add **no** gate-4 broken link and **no** gate-5 orphan.
7. `git status --short` shows the five modified files + the handoff note (plus
   the untracked prompt briefs); one commit on `development`.
