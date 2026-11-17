---
id: dx21-prompt
title: "DX21 prompt — the D10 translation-key surface rule: the recipe line already exists (DX18); DX21 adds the code-map 'Key surfaces' column that flags the inv/inventory exception, then regenerates the map"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX21** — **phase D, unit 7**
> (DX20 is committed). DX20 fixed the ratchet-base phrase in `in-code.md`. DX21
> is the **D10 unit** (plan `// TODO ❓`): settle the translation-key surface
> rule where the controller and the key disagree (e.g. `inv.*` vs `inventory.*`).
> **The one thing to get right — the drift this brief exists to prevent:**
> the plan says "DX21 gains the surface rule **and** a code-map 'key surfaces'
> column." But the **rule already exists** — DX18 wrote it into
> `docs/dev/recipes.md` (the "Add a UI string" recipe, lines 34–37: *surface =
> lower-case controller name*, *exceptions live in the code map (e.g. `inv.*`
> on the Inventory surface)*, *rename nothing*). So the rule text is **not** a
> DX21 deliverable; what is missing is the **column the rule points at**. DX21's
> real work is **one file + one regen**: add a **"Key surfaces"** column to
> `tools/devx/code-map.ps1`, regenerate `docs/dev/code-map.md`, and **land no new
> devx rule** (the existing `code-map` rule — rule **5 of 9** — already fails a
> stale map, so the new column is gated for free). **Who is it for:** the
> clean-slate agent starting DX21. **When to read the rest:** before touching
> `tools/devx/code-map.ps1`.

# DX21 — Translation-key surface rule (D10)

## What you are doing

You are carrying **DX21** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D10** decision row
(*"write the rule (surface = lower-case controller name), list today's
exceptions in the code map, rename nothing (a rename orphans admin-edited
translation rows in the DB)"*) and the **DX21** unit section first; they are
your spec. Today the "Add a UI string" recipe already states the rule and points
at the code map for the exceptions — but the map has **no** "Key surfaces"
column, so the exceptions (notably `inv` vs `inventory`) are not listed in one
hop. DX21 adds that column to the generator and regenerates the map. It touches
**one script + one regenerated doc**; **no** product code, **no** key-file
rename, **no** new devx rule, **no** gate.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gate is pre-existing (recorded in the DX20
handoff) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `28032aa5`, post-DX20)

1. **No new devx rule — `devx-check` stays at 9 rules.** The **existing**
   `code-map` rule (the 5th of the 9; `devx-check.ps1` lines ~168–183) runs
   `code-map.ps1 -Check` and **fails the moment the map is stale**. So once you
   regenerate and commit `docs/dev/code-map.md`, the new column is already gated
   — **do not add a 10th rule** to `devx-check.ps1`. `devx-check` must still
   read **PASS, 9 rules**, exit 0 after your change.
2. **The surface rule ALREADY EXISTS — do not re-add or reword it.**
   `docs/dev/recipes.md`, "## Add a UI string", lines 34–37 (written at DX18)
   already say: *surface = **lower-case controller name***; *Known exceptions
   live in the code map (e.g. `inv.*` on the Inventory surface)*; ***rename
   nothing*** (a rename orphans admin-edited translation rows). So
   `docs/dev/recipes.md` is **not** a DX21 deliverable. The one thing missing is
   the code-map column that line points at — that is your work.
3. **The exception is real and verified:** under
   `src/Kumunita.Core/Localization/Keys/en/` both `inv.json` **and**
   `inventory.json` exist. The Inventory surface's clean name is `inventory`
   (lower-case of `InventoryController.cs`); `inv.*` is the **exception**.
   Note `inv` is a **leading 3-char substring** of `inventory` — your matching
   must not collapse them into one. There is also a cluster of non-controller
   surfaces (`bm`, `pl`, `pn`, `rc`, `nav_variant`, `myportability`,
   `group_message`, `message`, `messaging`, `notification`, `notifications`,
   `tag`, `tags`, `community`, `communities`); flag any surface whose name is
   not a clean lower-case controller base, with `inv` as the named Inventory
   exception.
4. **`code-map.ps1` anchors (196 lines):** `param([switch]$Check)` at **line
   18**; the table **header** row at **line 175**
   (`| Context | *DocTypes.cs | I*Service | Controllers | Views | Tests | Top ADRs |`);
   the **separator** at **line 176**; the **per-row** build at **line 143**; the
   front-matter `updated: 2026-11-15` constant at **line 157**. `-Check`
   regenerates in memory and diffs (exit 0 = fresh, 1 = stale). Today the map is
   **fresh** (`code-map.ps1 -Check` → exit 0, 30 contexts).
5. **Appending the column as the LAST column is safe.** Nothing else parses the
   map's columns — `devx-check.ps1` only invokes `code-map.ps1 -Check` (it never
   reads the column count), and no other `tools/` script consumes the table. So
   add "Key surfaces" after "Top ADRs"; keep the existing seven columns
   **byte-identical**.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor is the regex `\[..\](..)` with a `.md` target, resolved relative to
   the doc's directory. **Backtick paths are NOT extracted.** The link-free
   briefs (`dx17`/`dx18`/`dx19`) are **not** in the broken list; the linked ones
   (`dx03`/`dx04`/`dx15`/`dx16`/`dx20`) each carry their own broken self-link.
   So reference every `.md` file in this brief **in backticks only** (e.g.
   `docs/dev/code-map.md`), **never** as `[label](path)`. That way this brief
   adds **+1 orphan** (expected, archived at DX37) and **zero** broken links.
   (The DX20 handoff hit exactly this when it first drafted a `[]()` link into a
   tracked note — avoid it here.)
7. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**, ADR
   >250 **1**, broken links **20**, orphans **18**, archive **50**, plus
   `improve-check` gate `b` (`0305`/`0306`). After you create **this brief**
   (a `tier: living` doc under `docs/plans-milestones/in-progress/`, unreachable
   from a `docs/start/` entry page), orphans read **19**, not 18 — that is the
   expected +1, archived at DX37. `docs/dev/code-map.md` is `tier: reference`,
   so it is **not** an orphan and not in the gate-4 list; regenerating it must
   add **no new** broken link.
8. **You are not renaming anything and not touching other columns.** **Rename
   nothing** (D10 — a rename orphans admin-edited translation rows in the DB).
   The existing seven map columns, the recipes.md rule, and `code-check` are
   all **correct and unchanged**.

## DX21 deliverables

- **`tools/devx/code-map.ps1`** — add a **"Key surfaces"** column as the
  **last** column. For each context row, derive the context's translation-key
  surface(s) from `src/Kumunita.Core/Localization/Keys/en/*.json` by matching
  the lower-case controller base (and the context stem). Mark any surface whose
  name is **not** a clean lower-case controller base with an exception marker —
  so the **Inventory** row shows `inventory` and flags `inv` (the D10
  exception). A context with no clear key surface may read `—`. Keep the column
  **generated and deterministic** (no hand-editing of `code-map.md`); a short
  comment in the script stating the matching rule is welcome. Add the header
  cell (line 175), the separator cell (line 176), and the row cell (line 143).
  The other seven columns stay **byte-identical**.
- **`docs/dev/code-map.md`** — **regenerated** by `pwsh tools/devx/code-map.ps1`
  (generated text only, no hand edits). The `updated:` front-matter constant
  (line 157) is a literal, not `Get-Date`, so `-Check` stays byte-stable whether
  you keep `2026-11-15` or bump it to the run date.
- **The handoff note** — a `## DX21 — translation-key surface rule (D10)`
  section (below).

## DX21 exit (the named checks)

- **`pwsh tools/devx/code-map.ps1 -Check`** → **exit 0** (fresh, 30 contexts).
  This is the primary check: the new column is present and the committed map
  matches a fresh regen.
- **No regression:** `devx-check` still **PASS, 9 rules**, exit 0 (the
  `code-map` rule — rule 5 — passes because the map is fresh; **no 10th rule**
  added); `-Doctor` still **all 5 checks OK**, exit 0.
- **`-Only docs` reds are pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **20**, orphans **19**, archive **50**;
  improve-check gate `b` `0305`/`0306`): the **+1** orphan (18→19) is **this
  brief** (link-free by fact 6, so it adds **no** broken link; archived at DX37);
  `docs/dev/code-map.md` is `tier: reference` and is **not** in the gate-4 or
  gate-5 lists. Verify by grepping the gate output for `code-map.md` — it must
  **not** appear as a broken link.
- `git status --short` shows the modified `tools/devx/code-map.ps1`, the
  regenerated `docs/dev/code-map.md`, the handoff note, plus the untracked
  `dx*-prompt.md` briefs (DX37 archives them).

## Handoff

Append a `## DX21 — translation-key surface rule (D10)` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15/DX16/DX17/DX18/DX19/DX20 sections). It must
state: **the surface rule already existed** (DX18 wrote it into `recipes.md`
"Add a UI string", lines 34–37 — *surface = lower-case controller name*,
*exceptions in the code map*, *rename nothing*), so DX21 added the **column it
points at**, not the rule; the **`Key surfaces` column** was added as the last
column to `code-map.ps1` (header/separator/row) and `docs/dev/code-map.md` was
**regenerated** (fresh, 30 contexts; the other seven columns byte-identical);
**`inv`** is flagged as the Inventory exception (both `inv.json` and
`inventory.json` exist; `inv` is a leading substring of `inventory`); **no new
devx rule** (the existing `code-map` rule — rule 5 of 9 — gates the fresh map;
`devx-check` still **9**); **nothing renamed** (D10). The `-Only docs` reds are
**pre-existing and unchanged** (the **19** orphans include this brief, one more
than DX20's 18, archived at DX37; `code-map.md` is `tier: reference`, so it
adds no gate-4/gate-5 entry). Cite **ADR 0307** (D10, the "surface names
disagree" finding); the plan body's "0300" is a stale number (consistent with
the DX04–DX20 handoffs).

## Commit

One commit. Stage explicitly (`git add tools/devx/code-map.ps1
docs/dev/code-map.md
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx21-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `docs(devx): DX21 — code-map 'Key surfaces' column flags the inv/inventory exception (D10; the recipe rule already existed) (IMPROVE-DEVX DX21)`

## Definition of Done

1. **`docs/dev/code-map.md`** has a new **"Key surfaces"** column (last
   position) that lists each context's translation-key surface(s) and flags
   surfaces not equal to a lower-case controller base — with **`inv`** shown as
   the Inventory exception. The other seven columns are **byte-identical**; the
   file is **generated**, not hand-edited.
2. **`tools/devx/code-map.ps1`** emits that column (header, separator, row);
   `pwsh tools/devx/code-map.ps1 -Check` → **exit 0** (fresh).
3. **No new devx rule** (`devx-check` still **PASS, 9 rules**); `-Doctor` still
   all 5 OK.
4. **`-Only docs` reds are pre-existing** (17 / 1 / 20 / **19** orphans incl.
   this brief / 50; improve-check `b` `0305`/`0306`); `code-map.md` adds no
   gate-4 broken link and no gate-5 orphan; **no key file or surface renamed**.
5. `git status --short` shows the two modified files + the handoff note (plus
   the untracked prompt briefs); one commit on `development`.
