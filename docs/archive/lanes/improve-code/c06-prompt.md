---
id: c06-prompt
title: "C06 — One translation-variant lane"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C06 — One translation-variant lane

## What you are doing

You are the **seventh** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C06**, the seventh of 13. It is a **seam-linking** unit (E7, *Silent coupling*): the translation actions (add / update / remove / list variants) are re-implemented per surface — ~**133 `*Translation*` methods over ~5 290 lines** across 8+ surfaces (posts, events, announcements, pages, projects/boards/todos, groups, surface labels). The reference surface is `PostService` (its 10 translation methods) + `PostsController` (its 7). You unify them into **one `TranslationLane<TDoc, TVariant>`** parameterized by a small per-surface descriptor (add / update / remove / list, the `CanAddTranslation` policy from C04, the audit row, the language validation) — a new translatable surface then becomes a descriptor, not a copied day of work.

**How this differs from C05:** C05 unified *resolution* (which target it is, is it visible). C06 is *translation variants* — the read/write shape of the `<Doc>Translation` documents around a target. C05's resolver already resolves the target; C06 does **not** change resolution, access decisions, or the closed `TargetKind` sets.

**You are NOT** doing C00–C05 (all shipped: gate+baseline, ADRs, the data move, `INotificationEmitter`, the `*Policy` parts, `ContentTargetResolver` at `f0bab4fb`) or C07–C13. No new feature, no route or rendered-HTML change, no document-schema change, **no new ADR** (C06 builds against the plan's C06 section + the C01 ADR). You do NOT touch the `IUserInfoService` / `IProjectService` interface splits (C07/C08).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `f0bab4fb` (C05 just landed).
- Commit style: `feat(improve-code): C06 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- Baseline: check `tools/code/code-baseline.json` for the `*Translation*` method/file entries you will shrink or retire. **Trust the baseline + the code over the plan's estimates** — re-derive the method set per surface from the code before touching anything.

## Entry reads

- The plan's **C06** section, **E7**, **D6**, and the part-vs-whole rule (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- The **C01 ADR** (ADR C) — the translation-lane decision record C06 implements.
- The reference surface: `PostService`'s 10 translation methods + `PostsController`'s 7 translation actions — read one surface fully, then diff the other surfaces against it to find the *divergences* (a surface-specific quirk must become an explicit descriptor option, not a silent special case).
- The `*Translation` document types (one per surface).
- The clone report rows for translation actions (E7) — the authoritative list of surfaces carrying copies.
- `tools/code/code-baseline.json` (the entries your lane retires or shrinks + any new boundary edges it introduces).

## Do

- **Core:** one generic `TranslationLane<TDoc, TVariant>` — add / update / remove / list, the C04 `CanAddTranslation` policy, the audit row, the language validation — parameterized by a small **per-surface descriptor** (doc type, variant type, allowed-language set, the surface-specific quirks you found in the diff).
- **Web:** a `TranslationActions` helper (or a base for the action bodies). Only if the view *modals* also prove to be clones: one shared modal partial.
- **Migrate one surface per commit**, in this order: posts → events → announcements → pages → projects / boards / todos → groups → surface labels → the rest. Each commit is a full unit: lane wiring + service/controller re-points + deleted clones + tests + baseline updates.
- **Unit tests** for the descriptor mapping (pure, no Postgres); **one integration test per surface** at the seam (add → render variant → remove).
- Apply **D4** to the code you move (what / when / throws + the ADR number).
- **Baseline:** retire or shrink the per-surface `*Translation*` method entries as each surface migrates (shrink-only, never a raise; new lane methods over the ceiling become *new* entries at their current value).

## Do NOT do

- Do NOT change translation **behaviour**: allowed languages, the `CanAddTranslation` decisions, the audit rows, the stored variant documents all stay byte-identical per surface.
- Do NOT touch resolution, access decisions, or the closed `TargetKind` sets (C05's territory).
- Do NOT split the `IUserInfoService` / `IProjectService` interfaces (C07/C08) or re-architect the services (C10).
- Do NOT raise any baseline `max` / `count` — the ratchet is shrink-only.
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you commit)

1. `*Translation*` methods fall from **133 / ~5 290 lines to ≤ 40 / ≤ 1 500** (the lane plus per-surface descriptors).
2. All 8+ surfaces add / update / remove / list through the one `TranslationLane`; no remaining per-surface copy of the action bodies.
3. The descriptor mapping has a **pure unit test** (no Postgres); each surface has an **integration test** at the seam.
4. Existing translation tests pass **unmodified**.
5. `pwsh tools/code/code-check.ps1` → exit 0 (stale-method rule satisfied; boundary edges legal; no max raised).
6. `pwsh tools/docs/improve-check.ps1` → exit 0.
7. `dotnet build` clean; the translation-action regions are gone from the clone report.

## Practical notes

- **D6 trap (same shape as C05):** a concrete `TranslationLane<T>` referenced across modules is a boundary violation if a surface module holds a *concrete* edge. Keep cross-module inbound references to **interfaces** and the **descriptor types** (records are legal D6 targets); the generic lane's outbound document work is fine.
- **Trust the code, not the plan's counts.** Re-derive the per-surface method set and the divergence list from the code; a divergence you did not see is a descriptor option you forgot.
- **One surface per commit** keeps each diff reviewable and lets the baseline shrink step by step — do not batch all 8 surfaces into one commit.
- Edit `code-baseline.json` **textually** (the file uses `\u003C`/`\u003E` escapes) — a naive `json.load`/`dump` round-trip churns every escape site and muddies the diff.
- The known xunit.v3 discovery quirk (`dotnet test` → "Zero tests ran / Exit code 5") is documented in `docs/dev/testing-and-ci.md`; use the in-process runner (`dotnet exec tests/.../*.dll -class "..."`) to run test classes.
