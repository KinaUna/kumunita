---
id: dx19-prompt
title: "DX19 prompt — declare the UI languages once (KnownTranslationKeys.Languages) + the folder-equality parity test"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX19** — **phase D, unit 5**
> (DX18 is committed). DX18 wrote `docs/dev/recipes.md` and landed the 9th
> devx rule. DX19 is the next plan unit and a **product-code** unit, not a
> doc/rule one: it declares the supported UI languages **once** — a new
> `KnownTranslationKeys.Languages` (`["en","de","fr","da"]`) — and adds the
> **one parity test** the plan pins: the set of `Localization/Keys/*` folders
> equals `Languages`. That makes "what languages does this platform support"
> a single source of truth instead of four hard-coded `Load("…")` literals +
> an implied folder set (plan E8: the supported UI languages were "only
> implied by `Keys/{en,de,fr,da}/`"). **The one thing to get right:** DX19
> lands **no new devx rule and no new gate** — it is a small, self-contained
> Core change with a parity test; don't drift into inventing a gate (the
> `devx-check` rule count stays **9**). **And the docs gate is already red**
> for pre-existing reasons — "docs-check is green" is not the exit; **the
> named Core test class passes** is. **Who is it for:** the clean-slate agent
> starting DX19. **When to read the rest:** before touching
> `KnownTranslationKeys.cs` or the one parity test.

# DX19 — Declare the UI languages once

## What you are doing

You are carrying **DX19** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **E8** evidence row
(*"Supported UI languages only implied by `Keys/{en,de,fr,da}/`"*) and the
**DX19** unit section first; they are your spec). Today the set of supported
UI languages is **declared nowhere** — it is implied by the four
`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/` folders and by four
hard-coded `Load("en") / Load("de") / Load("fr") / Load("da")` literals in
`KnownTranslationKeys`. DX19 gives that set **one name**
(`KnownTranslationKeys.Languages`) and a **test** that the declared set and
the folder set agree, so the two can't drift apart. It does **not** touch the
web app, the gates, the map, or any other context.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables (plus the handoff note); run only the named
checks; **never edit around a failing test.** The red docs gate is pre-existing
(recorded in the DX18 handoff) and is **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `d748f1bd`, post-DX18)

1. **DX19 lands no devx rule.** `devx-check.ps1` has **9 rules** (DX04
   `ci-triggers`, DX08/09 `iclassfixture[-web]`, DX12 `vscode-task-dotnet-test`,
   DX15 `code-map`, DX16 `agents-md-tokens` + `read-before-tokens`, DX17
   `testing-and-ci-messages`, DX18 `recipes-paths-exist`). **Do not add a
   10th** — DX19 is a product-code unit. `devx-check` must still read
   **PASS, 9 rules** after your change.
2. **The current declaration is four literals + an implied folder set.** In
   `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`:
   `EnValues = Load("en")`, `DeValues = Load("de")`, `FrValues = Load("fr")`,
   `DaValues = Load("da")`. There is **no** `Languages` member yet (verified).
   The folders are `en de fr da` (verified); the csproj embeds
   `Localization/Keys/**\/*.json` (78 surface files per language). The
   supported set is `["en","de","fr","da"]` — that is the only fact you may
   assert.
3. **There is no existing `Languages` array to duplicate.** `LanguageCatalog.cs`
   is a *per-instance admin-editable* doc type (different concern — the
   instance's supported-language rows), **not** the platform's built-in set.
   Do not reference or merge with it. `KnownTranslationKeys.Languages` is the
   new **platform** constant.
4. **Extend an existing test file; do not create a new one at the project
   root.** The parity family already lives in
   `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (a pure
   registry-shape test, no database, no Testcontainers — the right home). Add
   the one new `[Fact]` there. (DX30 later lands the root-level-test-file
   gate; stay inside the existing class either way.)
5. **The docs gate is RED for pre-existing reasons — it is not your exit.**
   Live today (verify with the gate, don't assume): front-matter **17**,
   ADR >250 **1**, broken links **19**, orphans **16**, archive **50** +
   `improve-check` gate `b` (`0305`/`0306`). **Your new risk:** this brief
   (`dx19-prompt.md`, `tier: living` under `docs/plans-milestones/in-progress/`,
   unreachable from a `docs/start/` entry page) is the **17th** orphan — so
   after you create it, `-Only docs` orphans read **17**, not 16. That is
   expected and archived at DX37 (same kind of drift the DX03–DX18 handoffs
   record). DX19 is a code unit, so it should add **no new** gate-4 broken
   link and **no new** doc.
6. **You are not changing behavior, only declaring it.** `Load("en")` and the
   four `*Values` dictionaries are the *data* (the loader is thin, ADR 0289).
   The new `Languages` member is the **closed set**; the parity test is the
   **gate on drift**. Replacing the `Load("…")` literals to iterate over
   `Languages` is *optional* ("where trivial" in the plan) and **not**
   required for the exit — the exit is the member + the parity test. Do not
   refactor the loader.

## DX19 deliverables

- **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** — add one
  member, near the other static properties:
  `public static IReadOnlyList<string> Languages { get; } = new[] { "en", "de", "fr", "da" };`
  with a `<summary>` doc comment: *the closed set of platform UI languages
  (ADR 0005); the `Keys/<lang>/` folders are defined by this set — the parity
  test pins folder-set == `Languages`.* Do **not** change the four `*Values`
  properties or the `Load`/`SurfacesFor` loader.
- **`tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs`** — add the
  one `[Fact]`: the set of sub-folder names of
  `src/Kumunita.Core/Localization/Keys/` (i.e. the distinct `<lang>` tokens)
  equals `KnownTranslationKeys.Languages` as a **set** (order-independent).
  Resolve the folder path relative to the running test assembly's project
  (`Path.GetFullPath` up to `src/`, or the same convention the existing
  file already uses to reach `Localization/Keys/`); if it cannot resolve,
  `Assert.Fail` with a clear message (fail closed, do not silently pass).
- **The handoff note** — a `## DX19 — declare the UI languages once` section (below).

## DX19 exit (the named checks)

- **The new test passes (in-process runner form, not `dotnet test`):**
  ```bash
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class Kumunita.Core.Tests.KnownTranslationKeys_ParityTests
  ```
  → **PASS** (all existing parity facts + the new folder-equality fact).
- **No regression:** `devx-check` still **PASS, 9 rules**, exit 0; `-Doctor`
  still **all 5 checks OK**, exit 0; `code-check` (the Roslyn ratchet) still
  green for `Kumunita.Core` (the added member is tiny — if the ratchet flags
  it, that's a real signal: record it, don't loosen the ceiling).
- **`-Only docs` reds are pre-existing** (front-matter **17**, ADR >250 **1**,
  broken links **19**, orphans **16→17** — the **+1** is this brief, archived
  at DX37; archive **50**; improve-check `b` `0305`/`0306`). DX19 adds **no
  new** gate-4 broken link and **no new** doc. **Record, don't fix.**
- `git status --short` shows the two source edits and the handoff note (plus
  the untracked `dx*-prompt.md` briefs — DX37 archives them).

## Handoff

Append a `## DX19 — declare the UI languages once` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX15/DX16/DX17/DX18 sections). It must state:
**`KnownTranslationKeys.Languages` is new** (the closed UI-language set
`["en","de","fr","da"]`, ADR 0005; the loader and the four `*Values` are
untouched — the data is ADR 0289, this is only the *set*); **one parity test**
added to `KnownTranslationKeys_ParityTests` (folder-set == `Languages`,
fail-closed if the path can't resolve); **no new devx rule** (`devx-check`
still 9) and **no gate loosened**; the exit is the named test class passing in
the `dotnet exec <dll> -class` form (not `dotnet test`); the `-Only docs`
reds are **pre-existing and unchanged** (the **17** orphans include this brief,
one more than DX18's 16, archived at DX37). Cite **ADR 0307** (D8, the unit
shape; E8, the "languages implied by the folders" finding); the plan body's
"0300" is a stale number (consistent with the DX04–DX18 handoffs).

## Commit

One commit. Stage explicitly
(`git add src/Kumunita.Core/Localization/KnownTranslationKeys.cs
tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (write the message to `.tmp/dx19-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

Suggested subject:

- `feat(localization): DX19 — KnownTranslationKeys.Languages (closed UI-language set) + folder-equality parity test (IMPROVE-DEVX DX19)`

## Definition of Done

1. `KnownTranslationKeys.Languages` exists: `IReadOnlyList<string>`
   `["en","de","fr","da"]`, with a doc comment citing ADR 0005; the four
   `*Values` and the `Load`/`SurfacesFor` loader are **unchanged**.
2. **One parity test** in `KnownTranslationKeys_ParityTests` asserts
   folder-set == `Languages` (set equality), failing closed if the
   `Localization/Keys/` path cannot be resolved; it passes in the
   `dotnet exec <dll> -class` form.
3. **No new devx rule** (`devx-check` still **PASS, 9 rules**); `-Doctor`
   still all 5 OK; the `code-check` ratchet is not loosened.
4. **`-Only docs` reds are pre-existing** (17 / 1 / 19 / **17** orphans incl.
   this brief / 50; improve-check `b` `0305`/`0306`); no new gate-4 broken
   link, no new doc.
5. `git status --short` shows the two source edits + the handoff note (plus
   the untracked prompt briefs); one commit on `development`.
