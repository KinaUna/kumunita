# ML-UI · U4 — wire the Groups views to `<kw-l>`

> Part of the `ML-UI` lane (`docs/plans-milestones/in-progress/plan-multilingual-ui.md`,
> **M·10**). Per-unit template (Goal / Entry reads / Deliverables / Exit). U4 builds on
> U1 (`KnownTranslationKeys`, 52 keys) + U2 (`<kw-l>` TagHelper) + U3 (Posts views).

## Goal

Wire the **Groups** views (`Index` / `Detail` / `New` / `PostDetail`) so their
in-scope hardcoded English resolves through the `<kw-l>` TagHelper — the 12
`groups.*` keys (13 placements, `groups.new_back` used twice). A fresh `en`
instance renders identically to today (the inner `en` text is the M·1 floor);
a `pl`-preferring resident with `pl` rows sees those strings in Polish.

## Entry reads (in order)

1. `docs/plans-milestones/in-progress/plan-multilingual-ui.md` — M·10, the
   in-scope surface, the U4 row + entry-read list, unit-series rules (3 & 4).
2. `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` — the
   **U3** section (the `ViewData["Title"]` tab-title limitation, the
   leave-out-of-scope-as-is pattern, exact-match principle).
3. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the 12
   `groups.*` keys + exact `en` values (ground truth for replacements).
4. `src/Kumunita.Web/Views/Groups/{Index,Detail,New,PostDetail}.cshtml` — read
   in full (the U4 edit surface; **verify** each placement against the current
   text, don't assume).
5. `src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs` — usage confirmation
   only (the `<kw-l key="…">en</kw-l>` syntax); **not edited**.

## Deliverables (closed set — 4 views modified, 0 new)

- `Views/Groups/Index.cshtml` — 4 keys: `groups.title`, `groups.lead`,
  `groups.empty` (first sentence of the compound empty-state only — Note 1),
  `groups.create` (the bottom button, NOT the empty-state's "Create one").
- `Views/Groups/Detail.cshtml` — 5 keys: `groups.back_all` (entire link text
  incl. the arrow — Note 2), `groups.posts_heading`, `groups.new_post`,
  `groups.posts_empty_can`, `groups.posts_empty`.
- `Views/Groups/New.cshtml` — 3 keys: `groups.new_back` (text after the arrow
  only — Note 3), `groups.new_title`, `groups.new_submit`.
- `Views/Groups/PostDetail.cshtml` — 1 key: `groups.new_back` (text after the
  arrow only — Note 3).

Text-only replacements: no `href`/`action`/`method`/`name`/`id`/
`@Html.AntiForgeryToken()`/`@if`/`@foreach`/`@Url.Action`/`Model.*`/`TempData.*`
changes. No `src/Kumunita.Core/**` (registry is frozen — adding a key is a
drift pause), no TagHelper/`_ViewImports`/`_Layout`/non-Groups views, no
controllers/models/tests.

## Exit

- **Build green:** `dotnet build Kumunita.slnx -c Debug` (all 4 projects).
  **Not** `dotnet test` / VS Test Explorer (the known xunit.v3 discovery quirk,
  AGENTS.md).
- **Spot-check (quick, non-test):** all 13 `<kw-l>` placements present (4 in
  `Index`, 5 in `Detail`, 3 in `New`, 1 in `PostDetail`); no other view
  changed. (Full behavioral proof — a `pl` preference renders the Groups pages
  in Polish — is U8's job, not U4's.)
- **Handoff note:** append a short `## U4` section to
  `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` (the 13
  placements, the three notes + resolutions, the `ViewData["Title"]`
  left-as-is decision, out-of-scope strings confirmed and left untouched, any
  deviations) **before** the folder move.
- **Folder move:** this file → `docs/plans-milestones/done/`.
- **Clean git status** — only the four Groups views + the plan/handoff docs
  changed.

## Drift-pause condition

A registry key whose `en` value does not exactly match the current view text,
a missing file, or a frozen seam contradicting the `ML` close record → stop,
record `## U4 — Drift pause` in the handoff note with exact evidence, do
nothing destructive, do **not** edit the registry.
