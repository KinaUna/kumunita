# U3 — The two `<kw-l>` keys in `KnownTranslationKeys.cs`

- **Lane:** Inline editor (`IE`)
- **Unit:** U3 (of U0–U5)
- **Kind:** code (C# — one registration block, additive)

## Goal

Register the two new keys (`rc.editor.source`,
`rc.editor.showPreview`) in the **closed** `rc.editor.*` set in
`KnownTranslationKeys.cs`, and update the closed-set comment at the
registration site to name them — making the design doc's key pin and
the code agree.

## Entry reads (≤ 3 files)

1. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
   `rc.editor.*` registration block (around line 630 — the `//
   registers the closed rc.editor.* set` comment + the 11 existing
   keys `rc.editor.bold` … `rc.editor.preview`) — the **exact shape U3
   extends**. Read the surrounding block (± 40 lines) to see the
   comment style + the key-value layout.
2. `docs/design/inline-editor-design.md` — the **two key names + their
   English values** (the pin: `rc.editor.source` = `</>`,
   `rc.editor.showPreview` = `Preview`).
3. `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
   the **`## U1 — Module toggle`** section (the binder references these
   exact two key names — confirm the names match) and the **`## U2 —
   Toggle buttons`** section (the button markup references
   `rc.editor.source` — confirm the name matches).

## Deliverables (closed set — 2 files; the 2nd is conditional)

- **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** —
  append the two keys to the existing `rc.editor.*` block (do
  **not** re-order or re-style the existing 11 keys):

  ```csharp
  ["rc.editor.source"]      = "</>",   // the toggle button's label while the source is hidden
  ["rc.editor.showPreview"] = "Preview", // the toggle button's label while the source is visible
  ```

  + update the closed-set comment above the block to name the two new
  keys (append `+ rc.editor.source + rc.editor.showPreview` — the
  comment currently names the RE set; U3 appends the two IE keys, in
  the comment's existing style).

- **`docs/design/inline-editor-design.md`** (conditional — only if it
  already exists from U0 and its key names/values **differ** from the
  register's pin) — update the doc's key names/values to match the
  **registered** values. The doc is the primary tier; the code is the
  artifact; they must agree — U3 is the unit that makes them agree, and
  the handoff note records the final values. If U0's design doc already
  pins exactly `rc.editor.source` = `</>` / `rc.editor.showPreview` =
  `Preview` (the register's assumption), this second deliverable is a
  no-op and the deliverable set collapses to 1 file (record that in the
  handoff note).

**Constraints (the load-bearing pins):**
- The existing 11 `rc.editor.*` keys are **byte-identical** in the
  diff (values unchanged; no re-ordering).
- The key **names** match what U1's binder + U2's markup reference —
  if the design doc, the binder, and the markup disagree on a name,
  U3 **pauses** and records `## U3 — Drift pause` naming the three
  sources + the disagreement (do **not** silently pick one).
- **No** other entry in the file is touched (the file is a large
  closed key registry; the diff is the 2 new lines + the comment edit
  and **nothing else**).
- If `KnownTranslationKeys.cs` has a **completeness test** (a test
  asserting the key set matches the views' `<kw-l>` usages, or similar
  — the RE U08 drift-pause precedent suggests one exists or the
  registration is manually closed), U3 notes it in the handoff note;
  U4's build/test run will surface any failure.

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** (the `Kumunita.Core`
  compile).
- The two keys are present in `KnownTranslationKeys.cs` with the exact
  values the design doc pins; the closed-set comment names them.
- The existing `rc.editor.*` keys are **byte-identical** in the diff.
- Handoff note: 3–4 lines starting `## U3 — kw-l keys` — (a) the two
  key names + their exact registered values, (b) the comment update
  (file + line), (c) confirmation the existing 11 keys are
  byte-identical in the diff, (d) whether a key-registry completeness
  test exists + whether it passed (if U3 could check it) — otherwise
  "U4's run is the check."
- Move this plan file `in-progress/` → `done/` (**last**).

## Notes / deviations

- **Value choice is a pin, not a preference.** If a fresh localization
  pass later wants different English values (or per-language values),
  that is a **localization task**, not an IE-lane re-open — the
  registered values here are the fallback the `<kw-l>` renders until a
  translation row exists.
- The `</>` value contains `<` and `>` — inside a C# string literal
  that is inert (no escaping needed); in the view's `<kw-l>` fallback
  text it is already HTML-escaped as `&lt;/&gt;` (U2's markup). Both
  forms render as the two-character label `</>`. Do **not** "fix" one
  to look like the other — they are in different worlds (C# string vs.
  Razor/HTML).
