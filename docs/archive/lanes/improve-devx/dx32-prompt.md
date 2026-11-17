---
id: dx32-prompt
title: "DX32 prompt — lane-code ratchet (pin the canonical regex, re-baseline, rule 13) + the comment rule"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX32** —
> **phase F, unit 2** (DX31 is committed, `development` @ `7d282ee9`;
> `devx-check` is at **12 rules**, the `lane-codes` glossary rule is green).
> DX32 lands **D6** (the lane-code ratchet) *part 2*: pin the **canonical
> lane-code regex**, **re-baseline** `lane_code_hits` in
> `devx-baseline.json` to the live count, land **rule 13**
> (`lane-codes-ratchet`) that keeps it shrink-only, and write the **comment
> rule** into `in-code.md`. **The one drift this brief exists to prevent:**
> the plan's E11 number is **3 391**, but the candidate regex's live count is
> **~16 500** — a ~5× jump that is **not** tree growth. It is the candidate
> regex over-counting `D<n>`, which in `src`/`tests` comments is almost always
> an **ADR-decision reference** (`ADR 0307, D1`), not an improve-docs lane
> code. DX32's job is to *pin the canonical regex* (a real lane code vs a
> decision ref) and baseline the ratchet to **that** number — **not** to
> rubber-stamp 16 500, and **not** to carry the stale 3 391 forward.
> **Who is it for:** the clean-slate agent starting DX32. **When to read the
> rest:** before touching `devx-baseline.json`, `devx-check.ps1`,
> `devx-report.ps1`, or `in-code.md`.

# DX32 — Lane-code ratchet (D6, part 2)

## What you are doing

You are carrying **DX32** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX32** unit section
and **D6** *"lane codes as identifiers"* first; they are your spec). DX31
landed the *glossary*; DX32 lands the *ratchet* (the DoD line: *"the
lane-code count is ≤ baseline (gate)"*). **Four deliverables:**

1. **The canonical regex, pinned in one place.** The single source of truth is
   `devx-report.ps1`'s E11 line. Today it is the *candidate*:
   `'\b(?:F|U|C|D|IS|LS|R|A|O|GP|UPL)[0-9]{1,2}[Uu]?[0-9]*(?:-[0-9]+)?\b'`.
   The canonical form **excludes `D`** — in `src`/`tests` a `D<n>` is an
   ADR-*decision* reference (`ADR 0307, D1`), which is the **desired** form
   (cite ADR numbers), not the anti-pattern the ratchet tracks. So the
   canonical set is `F|U|C|IS|LS|R|A|O|GP|UPL`. Update the E11 line to it and
   fix its "candidate, not canonical" comment to say it **is** canonical now.
   (If you find a real reason `D` or another letter must stay in or leave,
   say so in the handoff with the count for both — do not silently pick.)
2. **`devx-baseline.json`** — set `lane_code_hits.value` to the **live count
   under the canonical regex** (re-measure; do **not** reuse 3 391). Keep
   `shrink_only: true`, keep `plan: 3391`, and update the `source` note to
   name the canonical regex and today's date. The `generated_from` / `notes`
   block already explains the shape — extend, don't rewrite.
3. **`devx-check.ps1` rule 13** (`lane-codes-ratchet`): count the live lane-code
   hits (same canonical regex as `devx-report.ps1`; the house style is to
   duplicate the regex with a `# source: devx-report.ps1 E11, keep in sync`
   comment — do **not** shell out). Fail when the live count exceeds the
   baseline's `lane_code_hits` value. Rule count **12 → 13**.
4. **`in-code.md`** — extend the existing **comment rule** bullet (line ~116,
   `## The ratchet, the split, the comment`) with the lane-code half: a code
   comment cites the **ADR number** (or the ADR's decision, e.g. `D1`); a raw
   **unit code** (`F14.U2`, `C3-06`, `IS21`) belongs only in the **plan** or
   the **handoff** doc, not in a code comment. One or two sentences; link
   `docs/dev/lane-codes.md` (the DX31 glossary) for the prefix → lane map.

**No** ADR. **No** code changes (the ratchet is a count gate, not a scrub —
the ~12 200 existing hits are the *floor*, exactly like DX30's 142 test
files; DX32 records them, it does not strip them). **No** change to
`lane-codes.ps1` (DX31's glossary is already the prefix → lane map).

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gates are pre-existing (recorded in the
DX23–DX31 handoffs) and are **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `7d282ee9`, post-DX31)

1. **The plan's 3 391 is stale — it was measured with an unpinned regex.**
   `devx-baseline.json` still carries `lane_code_hits.value = 3391` (the plan's
   2026-11-09 number) and its `source` note already says *"DX32 pins the
   canonical one and re-baselines when it lands the ratchet."* DX32 **replaces**
   that value with today's live measure under the canonical regex. **Do not
   treat 3 391 as a target and do not carry it forward.**
2. **The live count is ~16 500 under the *candidate* regex — and that is the
   trap.** `pwsh tools/devx/devx-report.ps1` prints E11 `lines: 16554`.
   The jump from 3 391 is **not** tree growth; it is the candidate matching
   `D<n>`. In `src`/`tests`, `D1`…`D11` are ADR-**decision** references (e.g.
   `// M20 (ADR 0121, D1)`), and there are **≈ 4 300** of them (16 554 −
   12 231). Excluding `D` gives **≈ 12 200** — the real lane/unit-code count
   the ratchet should track.
   **This is expected, not a bug:** the whole point of DX32 is to pin the
   canonical regex (D-excluded) so the ratchet measures lane codes, not
   decision refs. Measure both numbers and record them in the handoff.
3. **`devx-check` currently has 12 rules** (DX31 landed rule 12
   `lane-codes` — the glossary freshness gate). Rule 13 is the new
   `lane-codes-ratchet`. It reads `lane_code_hits.value` from
   `devx-baseline.json` (the same pattern as rule 11 `test-files-at-root`,
   which reads `test_files_at_project_root`), and counts `src`/`tests` `*.cs`
   with the canonical regex (obj/ and bin/ excluded).
4. **The ratchet is a floor, like DX30's 142.** After DX32 the baseline is
   ≈ 12 200 and the gate fails only on a **new** unit code added to a comment
   (a count *above* the floor), not on the existing ≈ 12 200. That is the
   *shrink-only* meaning: the number may only go down; raising needs an
   `adr:` reason.
5. **`in-code.md` already has the comment-rule bullet** — do **not** add a
   second one. The bullet is at line ~116 under `## The ratchet, the split,
   the comment`; it currently reads "…plus the ADR number. History stays in
   the ADR or the handoff note." DX32 **extends that one bullet** with the
   unit-code half (cite ADR numbers / decision refs; unit codes only in the
   plan or handoff). `in-code.md` is **not** in the `AGENTS.md` token cap, so
   length is not a constraint here.
6. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths are
   NOT extracted.** Reference every `.md` file in this brief **in backticks
   only**, never as `[label](path)`. That way this brief adds **+1 orphan**
   (expected) and **zero** broken links.
7. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **29** — incl. the
   DX31 brief; archive **50**; `improve-check` gate `b` `0305`/`0306`). After
   this brief lands, orphans go **29 → 30** (this brief adds +1; backtick-only,
   zero broken links). `in-code.md` and `devx-baseline.json` are already
   tracked and linked, so they add **no** new orphan.

## DX32 deliverables

- **`tools/devx/devx-report.ps1`** — the E11 line becomes the **canonical**
  regex (D-excluded); its "candidate, not canonical" comment becomes
  "canonical — pinned at DX32." The E11 section then reports the real count.
- **`tools/devx/devx-baseline.json`** — `lane_code_hits.value` = the live
  canonical count (≈ 12 200; record the exact number), `shrink_only: true`,
  `plan: 3391` kept, `source` note updated (canonical regex + date + reason).
- **`tools/devx/devx-check.ps1`** — **rule 13** (`lane-codes-ratchet`): live
  count ≤ `lane_code_hits` baseline. Rule count **12 → 13**.
- **`docs/philosophy/in-code.md`** — the comment-rule bullet extended with the
  lane-code half (cite ADR numbers / decision refs; unit codes only in the
  plan or handoff); link `docs/dev/lane-codes.md`.

## DX32 exit (the named checks)

- **`devx-check` → PASS, 13 rules, exit 0** (rule 13 `lane-codes-ratchet`
  green: live ≤ baseline). All 12 prior rules still green (re-verified).
- **`devx-report`** prints the E11 canonical count (≈ 12 200), no longer
  "~16 500 candidate."
- **`in-code.md`** carries the extended comment rule; docs-check gate 2
  (abstract) and gate 3 (ADR length) stay as they were (unchanged).
- **`-Only docs` reds pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **30** — incl. this brief, one
  more than DX31's **29**; archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **No ADR, no code changes, no `lane-codes.ps1` edit.** `git status` shows
  changes to `devx-report.ps1`, `devx-baseline.json`, `devx-check.ps1`,
  `in-code.md`, and the handoff note only.

## Handoff

Append a `## DX32 — lane-code ratchet (D6, part 2)` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX25–DX31 sections). It must state: **rule 13**
(`lane-codes-ratchet`) landed — live ≤ baseline, shrink-only; the canonical
regex is **D-excluded** and pinned in `devx-report.ps1`; `lane_code_hits`
re-baselined **3 391 → the exact live count** (candidate ~16 500 explained as
the D-decision-ref over-count; the real lane-code count is ≈ 12 200);
`in-code.md` comment rule extended (unit codes → plan/handoff only);
`devx-check` **PASS, 13 rules**, exit 0; `-Only docs` reds pre-existing
(orphans **30** incl. this brief). Cite **ADR 0307** (D6); the plan body's
"0300" is a stale number (consistent with the DX04–DX31 handoffs).

## Commit

One commit. Stage the 4 changed files + the handoff note:

```
git add tools/devx/devx-report.ps1 tools/devx/devx-baseline.json \
      tools/devx/devx-check.ps1 docs/philosophy/in-code.md \
      docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx32-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `test(devx): DX32 — lane-code ratchet (canonical regex, rule 13, re-baselined) + comment rule (IMPROVE-DEVX DX32)`

## Definition of Done

1. **`devx-check` → PASS, 13 rules, exit 0** (rule 13 `lane-codes-ratchet`
   green: live ≤ baseline). All 12 prior rules re-verified green.
2. **The canonical regex is pinned** in `devx-report.ps1` (D-excluded) and is
   the same regex rule 13 uses; `devx-report`'s E11 prints the real count
   (≈ 12 200), not the ~16 500 candidate.
3. **`devx-baseline.json`** `lane_code_hits` re-baselined to the live
   canonical count (not the stale 3 391), `shrink_only: true`, `plan: 3391`
   kept for the DX35 before→after table.
4. **`in-code.md`** comment rule extended (cite ADR numbers / decision refs;
   unit codes only in the plan or handoff; links the DX31 glossary).
5. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **30** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`).
6. **No ADR, no code changes, no `lane-codes.ps1` edit.** `git status` shows
   only the 4 deliverable files + the handoff note.
7. One commit on `development`; the handoff section states every number above
   and the candidate-vs-canonical reasoning (3 391 → ≈ 12 200, not 16 554).
