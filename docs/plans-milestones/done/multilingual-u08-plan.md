# U8 — Multilingual: run + record the acceptance gate

**Milestone:** multilingual (`ML`) · **Register:** `docs/plans-milestones/done/plan-multilingual.md` · **Read first:** the design doc `## Acceptance gate` + `§Pinned seam tests` + the U7 handoff section.

## Goal

Run the **multilingual acceptance gate** through the reliable in-process runner (per `AGENTS.md`) and **record** the result into `docs/design/multilingual-design.md` by filling the existing `## Multilingual — Gate (recorded by U8)` placeholder — the gate shape frozen in the design doc `## Acceptance gate`: **closed loop** (GlobalAdmin saves a `TranslationResource` → the next `pl`-preference request resolves it; the `translation.save` audit row exists), **handoff** (GlobalAdmin sets the default to `pl` → a no-preference resident sees `pl` on the next request; the `LocaleSettings` change is picked up live), **part-vs-whole** (the 19 seam tests pass together with the inherited `Kumunita.Core.Tests` anchors).

**This unit changes no production code and adds no tests.** If a gate case fails, the failure is a U1–U7 production/test bug — record the exact failing name + suspected file in the handoff note and **stop** (do not fix code here).

## Entry reads

1. `docs/design/multilingual-design.md` — `## Acceptance gate` (the three-test shape), `§Pinned seam tests` (the 19 names to confirm all 19 ran), and the `## Multilingual — Gate (recorded by U8)` placeholder (the fill-in target).
2. `docs/plans-milestones/done/multilingual-handoff-notes.md` — **only the U7 section** (per the protocol: the prior unit's handoff entry + your own entry-reads).
3. `AGENTS.md` — the "Running the tests (test-runner quirk)" section (the exact build + `dotnet exec` commands).
4. `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` — confirm all **19** `[Fact]` names are present verbatim (the part-vs-whole evidence).

## Deliverables (1 file)

- `docs/design/multilingual-design.md` — **fill** the `## Multilingual — Gate (recorded by U8)` placeholder (replace its *Placeholder* italic paragraph, keep the heading) with: the date, the machine/runner line, the pass counts for **both** assemblies (`Kumunita.Web.Tests` + `Kumunita.Core.Tests`), and a `| # | Test | Evidence |` table for the three gates (closed loop = M9 + the `translation.save` audit row; handoff = M10 + the `language.set-default` audit row; part-vs-whole = the 19/19 line inside the same `Kumunita.Core.Tests` run alongside the inherited anchors). **Append/replace the designated placeholder only** — do not rewrite any other section (unit-series rule 2; the placeholder is U8's own designated slot).

## Exit

All three gates PASS and recorded. **The command sequence (per AGENTS.md — the runner quirk makes `dotnet test` / VS Test Explorer unreliable on this machine):**

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Handoff note (append to `docs/plans-milestones/done/multilingual-handoff-notes.md`): `## U8 — gate run + record` — the date + machine, the runner line, the two pass counts, the part-vs-whole evidence (19/19 present character-for-character in `LocalizationServiceTests.cs` and inside the same `Kumunita.Core.Tests` run), the note that **no production code and no tests** changed, and the hand-off to U9 (the close: `Milestones.cs` `ML` → done / `M4` → next + README / `ARCHITECTURE.md` §8/§9 sync + folder moves). Then move this unit plan file `in-progress/multilingual-u08-plan.md` → `done/`.

**Nothing is staged or committed — the user reviews first.**
