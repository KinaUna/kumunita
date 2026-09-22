# U6 — the resident-facing Web surface (locale settings + static-page routes) + the admin Razor views

> **ML lane · U6 (of 9).** Sealed unit — sized for a ~64K-context fresh agent,
> one at a time. The **primary** reference is the design doc
> `docs/design/multilingual-design.md` §4 (`LocaleCookie`) + §5 tail (the
> settings page + the `/terms` `/about` `/help` routes) + §Pinned seam tests
> M7/M8/M5 anchors. **Secondary** is the unit register
> `docs/plans-milestones/done/plan-multilingual.md` (this unit's row). **Scratch** is
> `docs/plans-milestones/done/multilingual-handoff-notes.md` (read the
> **U5** section — it confirms the `LanguagesController` + its two public
> nested view models are the stable HTTP surface U6 builds against).

## Goal

Ship U6's closed set of Web deliverables — the **resident locale-settings page**
(the `LocaleCookie` write, M5/M7 FACES), the **static-page routes**
(`/terms` `/help` over `ITranslationProvider.GetPageAsync`, M5 FACES), the
**Markdown body renderer** those pages use, and the **`Views/Admin/Languages`
Razor surface** that binds to U5's `LanguagesController`. The lane's resident
and admin UI is now present and compiles green.

## Entry reads (minimal)

1. `docs/design/multilingual-design.md` §4 + §5 (tail) + the M5/M7/M8 FACES
   rows. (Authoritative.)
2. `src/Kumunita.Web/Security/LocaleCookie.cs` — U4's read/write/clear trio
   (already shipped; U6 is its **only** consumer in this lane).
3. `src/Kumunita.Web/Controllers/LanguagesController.cs` — U5's 10-action
   surface + the two **public** nested view models (`LanguageRowViewModel`,
   `PageEditorViewModel`) the admin views bind to.
4. `src/Kumunita.Core/Localization/ILocalizationService.cs` — the catalog /
   translation / page methods the admin views' forms POST to (already shipped
   by U3; do not re-shape).
5. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the nav-link
   convention U6's new "Settings" link extends (a small edit, inside this
   unit's deliverables).

## Deliverables (closed set — 8 files)

| # | File | What ships |
|---|------|-----------|
| 1 | **New** `src/Kumunita.Web/Controllers/LocaleController.cs` | `GET /settings/language` (the signed-in resident's locale preference page; the `LocaleCookie.Read` echo) + `POST /settings/language` (the `LocaleCookie.Write`/`Clear` save — M7 FACES). One `[Authorize]`-gated action pair, `ILocalizationService` in the ctor (the **only** Core seam consumed; the catalog read drives the picker). A nested **public** view model (`LocaleSettingsViewModel`) so the view binds cleanly. |
| 2 | **New** `src/Kumunita.Web/Controllers/StaticPagesController.cs` | `GET /terms` + `GET /help` over `ITranslationProvider.GetPageAsync(slug, LocaleCookie.Read(Request))` (the M·2 per-page fallback lives in U2's provider — this controller is a thin route). `null` → 404 (M·5's "truly absent"). `ITranslationProvider` in the ctor (U2's seam, already registered by U4). |
| 3 | **New** `src/Kumunita.Web/Security/MarkdownRenderer.cs` | A **minimal, escape-first** Markdown→HTML renderer (the "single page engine" the ADR 0005 A row promises for `LocalizedPage.Body`). Supports the common subset (headings 1–6, paragraphs, ordered/unordered lists, `**bold**`, `*italic*`, `` `code` ``, `[label](url)`, fenced code blocks). **Escapes every input character** before applying inline rules (XSS-safe by construction), **whitelists** `http:` / `https:` / `mailto:` / relative URLs (no `javascript:`), and exposes `RenderHtml(string)` as a static entry point for the views. |
| 4 | **Modified** `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` | Adds the **Settings** nav link (signed-in only, next to Profile) — the discovery point for the locale page. Two lines inside the existing `isAuthenticated` block. |
| 5 | **New** `src/Kumunita.Web/Views/Locale/LocaleSettings.cshtml` | The settings-page body: the enabled-language `<select>`, the current-preference echo (`LocaleCookie.Read`), a "Save" button (POST), and a "Reset to default" button (POST with a `clear=1` marker). TempData flash rendering (the `Admin/Index.cshtml` idiom). |
| 6 | **New** `src/Kumunita.Web/Views/StaticPages/Page.cshtml` | The shared static-page view (binds to `LocalizedPage`): renders `Title` as an `<h1>` and `Body` via `MarkdownRenderer.RenderHtml` (the single page engine). Used by both `/terms` and `/help`. |
| 7 | **New** `src/Kumunita.Web/Views/Admin/Languages/Index.cshtml` | The GlobalAdmin catalog shell (binds to `IReadOnlyList<LanguagesController.LanguageRowViewModel>`): the add-language form, per-language enable/disable/set-default/remove buttons, a reorder form, and the M·12 completeness summary (present / missing keys + page slugs, the U5-pinned shape). TempData flash, `@Html.AntiForgeryToken()` on every form. |
| 8 | **New** `src/Kumunita.Web/Views/Admin/Languages/PreviewPage.cshtml` | The editor/preview pane (binds to `LanguagesController.PageEditorViewModel`): the title + body form (`SavePage`), and a translation-key editor (key + text, `SaveTranslation`) — both the `ILocalizationService` methods U5 already exposes. The "no saved translation yet" state (`HasSaved == false`) renders an empty form. |

**Not in U6's deliverables (out of scope):** no new Core seam, no new
`DocTypes` surface, no new package, no re-shape of U5's controller or U3's
service, no new test names (U7 owns the 19 pinned seam tests), no `Milestones.cs`
bump (U9 owns the close), no renumbering of M4/M5/M6.

**About-page note (M5 FACES):** the existing `/about` route is owned by
`HomeController.About` (the product-story landing page). U6's deliverables do
**not** touch `HomeController.cs` (unit rule 1 — a unit never modifies a file
not in its own `Deliverables`). The about-page localization is therefore
reachable through `ITranslationProvider.GetPageAsync("about", …)` from any
view that wants it (U7's seam tests exercise that path directly). If U9 (the
close) wants `/about` to render the `LocalizedPage` body when one exists, that
is a **U9** follow-on (a one-line change to `HomeController.About` + a small
localized view), not a U6 concern.

## Exit

1. `dotnet build Kumunita.slnx -c Debug` is **green** (touched projects:
   `Kumunita.Web` — the two controllers, the Markdown renderer, the four views,
   and the two one-line nav/layout edits all compile; Razor compiles the
   `@model` bindings against the U5-pinned view-model shapes).
2. The **U6** section is appended to
   `docs/plans-milestones/done/multilingual-handoff-notes.md` **before**
   the folder move.
3. This plan file is moved `in-progress/` → `done/`.
