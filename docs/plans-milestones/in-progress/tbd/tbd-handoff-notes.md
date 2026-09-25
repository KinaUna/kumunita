# `TBD` — Todo dependency ("waiting on") — handoff notes (scratch tier)

> **Append-only scratch log.** One `## U#` section per unit, appended (never
> rewritten). The **primary reference** is
> `docs/design/tbd-todo-dependency-design.md`; the **secondary register** is
> `plan-tbd-todo-dependency.md`.
>
> **Convention:** each `## U#` section records (1) what landed, (2) what was
> deferred / deferred-to, (3) the next unit's entry reads (a pointer to the
> plan's `Entry reads` list is enough — don't duplicate it).

## U00 — Design doc + ADR 0087 (sign-off)

- `docs/design/tbd-todo-dependency-design.md` **authored** (Part 1: What
  this lane is; the 10 verified existing surfaces; the D1–D9 decisions;
  the C-TBD·1…6 invariants; the F1–F8 FACES; the 8-part parts-affected
  list; the 3-test gate; the drift-guard; the 5 risks. Part 2: the exact
  C# shapes for the `TodoItem` field, the `M5DocTypes` index, the
  `ListTodosAsync` / `GetTodoAsync` / `CreateTodoAsync` /
  `UpdateTodoAsync` / `ListPickerTodosAsync` seams, the `BlockerChip`
  record, the Web controller + view shapes, the 11 Core pins + 6 Web pins,
  the three-test gate, the drift-guard).
- `docs/adr/0087-todo-dependency-blocked-by-lane.md` **authored** (the
  D1–D9 decisions; **Status: Proposed (draft for evaluation)** — flips to
  **Accepted** at sign-off; the "Out of scope" list; the
  "Consequences" section).
- `docs/adr/README.md` — the ADR 0087 index row **added** (the `Proposed`
  status until sign-off).
- **Deferred:** the `TBD` lane's *implementation* (U01–U04) — the ADR is
  **Proposed**, not yet **Accepted**; the user is evaluating the concrete
  artifact.
- **Next unit's entry reads:** the plan's `Entry reads` list (the 10
  files above) + the design doc's §7 (Part 2) for the exact C# shapes.
