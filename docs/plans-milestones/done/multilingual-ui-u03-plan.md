# ML-UI · U3 — the Posts views wired to `<kw-l>`

**Date:** 2026-09-12 · **Kind:** code unit (view edits only) · **Exit:** build green
(`dotnet build Kumunita.slnx -c Debug` — all 4 projects) + the 7 `posts.*` keys
present as `<kw-l>` elements in the three touched views + handoff `## U3` section
+ clean git status.

## Goal

Wire the **Posts** views (`Index` / `New` / `Edit`) so the 7 `posts.*` registry
keys' hardcoded English resolves through the `<kw-l>` TagHelper (U2). `Detail.cshtml`
is **left untouched** — it contains none of the 7 exact registry strings (it is the
UGC-reading page: post body + replies, never keyed — M·3 / unit-series rule 4).
After U3, a fresh `en` instance renders the Posts pages identically to today (the
inner `en` text is the M·1 floor), and a `pl`-preferring resident with `pl` rows
sees those 7 strings in Polish.

## Entry reads (in this order)

1. `docs/plans-milestones/in-progress/plan-multilingual-ui.md` — **M·10**,
   **in-scope surface** (the Posts row), the **U3 row**, U3's entry-read list,
   unit-series rules (rule 3 — never key an out-of-scope string; rule 4 — never
   wrap UGC).
2. `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` — the
   **U2** section (the TagHelper's real shape: `IHttpContextAccessor` +
   `SetContent`, not `SetHtmlContent`; the `ViewContext` gotcha — U3 does not hit
   it since it is view-only).
3. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the **7
   `posts.*`** keys + their exact `en` values (ground truth for the replacements).
4. `src/Kumunita.Web/Views/Posts/Index.cshtml` — read in full.
5. `src/Kumunita.Web/Views/Posts/New.cshtml` — read in full.
6. `src/Kumunita.Web/Views/Posts/Edit.cshtml` — read in full.
7. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — read to confirm none of the
   7 exact `posts.*` strings appear (verified — none do; the "Edit" button,
   "Report", "Replies", "Reply", "back to the post", "No replies yet" are
   all out-of-scope / UGC-adjacent). Left untouched.
8. `src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs` — read only to confirm the
   `<kw-l key="…">en</kw-l>` usage idiom (U3 does not edit it).

## Deliverables (the closed set — 3 files modified, 0 new)

**Modified** `src/Kumunita.Web/Views/Posts/Index.cshtml` — **3** keys wrapped:
- `posts.write` — the `CanPost` branch "Write a post" primary button.
- `posts.empty_can_post` — the `CanPost` branch empty-feed `<text>`.
- `posts.empty` — the else branch empty-feed `<text>`.

**Modified** `src/Kumunita.Web/Views/Posts/New.cshtml` — **2** keys wrapped:
- `posts.new_title` — the `<h1>Write a post</h1>`.
- `posts.new_submit` — the form's submit button.

**Modified** `src/Kumunita.Web/Views/Posts/Edit.cshtml` — **2** keys wrapped:
- `posts.edit_title` — the `<h1 class="mt-2">Edit post</h1>`.
- `posts.edit_save` — the form's submit button.

**Touched (plan + handoff):** `multilingual-ui-u03-plan.md` (this file),
`multilingual-ui-handoff-notes.md` (the `## U3` section).

**Deliberately NOT touched (per the instructions + the unit-series rules):**
- **`ViewData["Title"] = "Write a post"`** (`New.cshtml`) and
  **`= "Edit post"`** (`Edit.cshtml`) — localizing a tab title requires a C#
  `@{}` code line calling the provider, which is out of U3's closed scope.
  Left as-is; recorded in the handoff note as a U9 follow-on proposal.
- **`Detail.cshtml`** — none of the 7 registry strings appear; the page is the
  UGC-reading surface (post body + replies, M·3 / rule 4).
- All out-of-scope strings in the three touched views: "Manage members",
  "Leave", "Communities" / "Browse communities" / "All", the "Everyone" ADR 0012
  badge, the hidden-count hint (`@Model.Total post@…`), the "community" helper
  paragraphs, the "Title" label, the "optional" placeholder, the "No community
  feed…" / "This post's community feed…" warnings, the audience-picker copy, the
  "Cancel" buttons — none in the `posts.*` registry.

## Exit

- **Build green:** `dotnet build Kumunita.slnx -c Debug` (all 4 projects).
- **Spot-check:** each of the 7 `posts.*` keys appears as a `<kw-l key="posts.…">`
  element in `Index.cshtml` / `New.cshtml` / `Edit.cshtml`; no other view changed.
- **Handoff note:** `## U3` section appended to
  `multilingual-ui-handoff-notes.md` **before** the folder move — the 7
  placements (file + which string), the `ViewData["Title"]` limitation
  (left as-is, recorded as a U9 follow-on proposal), `Detail.cshtml` confirmed
  untouched (and why), any deviations.
- **Folder move:** this plan file →
  `docs/plans-milestones/done/multilingual-ui-u03-plan.md`.
- **Clean git status:** only the three Posts views + the plan/handoff docs
  changed; no stray edits.

**Drift-pause count: 0** (no mismatches, no frozen-seam contradictions, no
out-of-scope strings attempted).
