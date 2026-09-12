# ML-UI · U5 — wire the remaining in-scope views to `<kw-l>`

> Part of the `ML-UI` lane (`docs/plans-milestones/in-progress/plan-multilingual-ui.md`,
> **M·10**). Per-unit template (Goal / Entry reads / Deliverables / Exit). U5 builds on
> U1 (`KnownTranslationKeys`, 52 keys) + U2 (`<kw-l>` TagHelper) + U3 (Posts views) +
> U4 (Groups views). U5 completes the **view-wiring track** (U2–U5).

## Goal

Wire the **6 remaining in-scope views** (`Home/Index`, `Account/Login`,
`Account/Signup`, `Directory/Index`, `Profile/Edit`, `Admin/Index`) so their
in-scope hardcoded English resolves through the `<kw-l>` TagHelper — the 16
keys (`home.*` 3, `account.*` 6, `directory.*` 3, `profile.*` 2, `admin.*` 2).
A fresh `en` instance renders identically to today (the inner `en` text is the
M·1 floor); a `pl`-preferring resident with `pl` rows sees those 16 strings in
Polish. After U5, every in-scope string in the registry is wired to a `<kw-l>`
element.

## Entry reads (in order)

1. `docs/plans-milestones/in-progress/plan-multilingual-ui.md` — M·10, the
   in-scope surface, the U5 row + entry-read list, unit-series rules (3 & 4).
2. `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` — the
   **U3** + **U4** sections (the `ViewData["Title"]` tab-title left-as-is
   convention, the out-of-scope leave-untouched pattern, exact-match
   principle, arrow-entity handling).
3. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the `home` (3),
   `account` (6), `directory` (3), `profile` (2), `admin` (2) groups — U5's
   closed set (16 keys).
4. `src/Kumunita.Web/Views/{Home/Index,Account/Login,Account/Signup,Directory/Index,Profile/Edit,Admin/Index}.cshtml`
   — read in full (the U5 edit surface; **verify** each placement against the
   current text, don't assume).
5. `src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs` — usage confirmation
   only (the `<kw-l key="…">en</kw-l>` syntax); **not edited**.

## Deliverables (closed set — 6 views modified, 0 new)

- `Views/Home/Index.cshtml` — 3 keys: `home.eyebrow`, `home.lead` (the entire
  multi-line paragraph wrapped as a single value — single-line registry text as
  inner text), `home.support` (**first sentence only** — before the mailto
  `<a>`; the `<a>` + trailing `.` left as-is).
- `Views/Account/Login.cshtml` — 3 keys: `account.login_title`,
  `account.login_submit`, `account.login_no_account` (**question text only** —
  the cross-link `<a>` left as-is).
- `Views/Account/Signup.cshtml` — 3 keys: `account.signup_title`,
  `account.signup_submit`, `account.signup_has_account` (**question text only**
  — the cross-link `<a>` left as-is).
- `Views/Directory/Index.cshtml` — 3 keys: `directory.title`, `directory.lead`,
  `directory.empty`.
- `Views/Profile/Edit.cshtml` — 2 keys: `profile.title`, `profile.save_avatar`.
- `Views/Admin/Index.cshtml` — 2 keys: `admin.title`, `admin.verify`.

Text-only replacements: no `href`/`action`/`method`/`name`/`id`/`asp-*`/
`@Html.AntiForgeryToken()`/`@if`/`@foreach`/`@Url.Action`/`Model.*`/`TempData.*`/
`@Model.CommunityName`/`Milestones.All`/`RepositoryInfo.Links` changes. No
`src/Kumunita.Core/**` (registry is frozen — adding a key is a drift pause), no
TagHelper/`_ViewImports`/`_Layout`/`_AccountNav`/Posts/Groups views, no
controllers/models/tests/docs.

## Exit

- **Build green:** `dotnet build Kumunita.slnx -c Debug` (all 4 projects).
  **Not** `dotnet test` / VS Test Explorer (the known xunit.v3 discovery quirk,
  AGENTS.md).
- **Spot-check (quick, non-test):** all 16 `<kw-l>` placements present (3 in
  `Home/Index`, 3 in `Account/Login`, 3 in `Account/Signup`, 3 in
  `Directory/Index`, 2 in `Profile/Edit`, 2 in `Admin/Index`); no other view
  changed. (Full behavioral proof — a `pl` preference renders all in-scope
  pages in Polish — is U8's job, not U5's.)
- **Handoff note:** append a short `## U5` section to
  `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` (the 16
  placements, the two notes + resolutions, the `ViewData["Title"]` left-as-is
  decision, out-of-scope strings confirmed and left untouched — especially the
  Login setup-token section, the `home.lead` multi-line handling, any
  deviations) **before** the folder move.
- **Folder move:** this file → `docs/plans-milestones/done/`.
- **Clean git status** — only the six U5 views + the plan/handoff docs changed.

## Drift-pause condition

A registry key whose `en` value does not exactly match the current view text,
a missing file, or a frozen seam contradicting the `ML` close record → stop,
record `## U5 — Drift pause` in the handoff note with exact evidence, do
nothing destructive, do **not** edit the registry.
