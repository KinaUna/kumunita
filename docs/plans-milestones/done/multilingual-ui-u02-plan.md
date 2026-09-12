# ML-UI U2 — the `<kw-l>` TagHelper + the shared layout wiring

> **Unit 2 of the `ML-UI` lane** (`plan-multilingual-ui.md`). A **code** unit —
> the exit gate is a green build. U1 (U0/U1 in the handoff notes) shipped the
> canonical `en` registry (`KnownTranslationKeys`, 52 keys) and the seeder step
> that materializes the `en` floor. U2 turns the shared layout from hardcoded
> English into strings resolved through `ITranslationProvider`.

## Goal

Create the `<kw-l>` TagHelper (`LocalizeTagHelper`) and wire the **shared
layout** — the `_Layout.cshtml` nav + footer and the `_AccountNav.cshtml`
signed-in/out items — to resolve their hardcoded English through
`ITranslationProvider`. After U2, a fresh `en` instance renders the nav and
footer **identically to today** (the inner `en` text is the M·1 floor), while a
`pl`-preferring resident (with `pl` rows present) sees the nav/footer in Polish.
**U2 touches only the shared layout — not the individual page views (U3–U5).**

## Entry reads (the minimal files)

- `plan-multilingual-ui.md` — D1 (TagHelper), the Pinned contract §U2 (exact
  TagHelper shape), M·10 (view-path invariant), U2's entry-read list, the
  in-scope surface (U2 = nav + footer + settings-picker labels only).
- `multilingual-ui-handoff-notes.md` — the **U1** section (the 52 keys, the
  registry shape, the seeder step, the `.ToList()` build-fix) and the **U0**
  section (the frozen-seam list, D1/D2).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — read in full (52
  keys); the exact nav / footer / settings key names + their `en` values.
- `src/Kumunita.Core/Localization/ITranslationProvider.cs` — the
  `GetAsync(string key, string? preferredLanguageCode)` signature (the one the
  TagHelper calls).
- `src/Kumunita.Web/Security/LocaleCookie.cs` — the
  `LocaleCookie.Read(HttpRequest)` signature (the preference read).
- `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the nav + footer to edit.
  Nav links use `asp-controller`/`asp-action` (MVC TagHelpers); `<kw-l>` goes
  **inside** each `<a>`.
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the signed-in/out items.
- `src/Kumunita.Web/Views/_ViewImports.cshtml` — add the TagHelper namespace.
- `src/Kumunita.Core/DependencyInjection.cs` (~lines 138–139) — confirm
  `AddTransient<ITranslationProvider, TranslationProvider>` (it is — U0/U1).

## Deliverables (4 files — closed set)

1. **New** `src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs`
   - `namespace Kumunita.Web.TagHelpers;`
   - `public sealed class LocalizeTagHelper : TagHelper`, ctor-injected
     `ITranslationProvider`.
   - `[HtmlTargetElement("kw-l", Attributes = "key")]`
   - `[HtmlAttributeName] public string Key { get; set; } = "";`
   - `ProcessAsync`: `c.ViewContext.HttpContext.Request` →
     `LocaleCookie.Read(request)` → `await _provider.GetAsync(Key, pref)` → emit
     the translated text. `o.TagMode = TagMode.StartTagAndEndTag;` +
     `o.Content.SetHtmlContent(text);` (the plan's pinned contract). Empty/blank
     `Key` emits the key itself (the M·1 floor) rather than throwing.
2. **Modified** `_ViewImports.cshtml` — add one line
   `@addTagHelper *, Kumunita.Web.TagHelpers` after the existing line (do not
   remove/modify it).
3. **Modified** `_Layout.cshtml` — replace the five nav labels (Home /
   Announcements / Community / Groups / Directory) and the five footer strings
   (tagline `<p>`, copyright suffix, "Good to know" `<h3>`, privacy `<p>`,
   OSS `<p>`) with `<kw-l key="…">en</kw-l>`. Do **not** touch the `<title>`,
   `@ViewData["Title"]`, logo, RepositoryInfo links, `<script>` tags, the
   `fullBleed` logic, `@RenderBody()`/`@RenderSectionAsync`, or the footer's
   "Community" / "The project" column headings (not in the registry).
4. **Modified** `_AccountNav.cshtml` — replace the six labels (Profile,
   Settings, Admin, Sign out, Sign in, Sign up) with `<kw-l key="…">en</kw-l>`.
   Do **not** touch `@Html.AntiForgeryToken()`, the `asp-*`/`method` attributes,
   the `KumunitaPrincipal.IsGlobalAdmin` check, or the form structure.

## Out of this unit (unit-series rule 1)

No `Kumunita.Core/**` changes. No view edits outside `_Layout.cshtml` /
`_AccountNav.cshtml` (Posts / Groups / Directory / Profile / Account / Admin page
views are U3–U5). No controller changes. No new DI registration. No tests (U8).
No README / ADR / `Milestones.cs` edits (U9). **Any other Core/Web ADD is a drift
pause.**

## Exit

- **Build green:** `dotnet build Kumunita.slnx -c Debug` (all 4 projects).
- **Spot-check (non-test):** TagHelper compiles (build is the proof);
  `_ViewImports.cshtml` has the new `@addTagHelper` line; `<kw-l>` appears in
  `_Layout.cshtml` and `_AccountNav.cshtml`.
- **Handoff note:** append a short `## U2` section to
  `multilingual-ui-handoff-notes.md` (exact `ProcessAsync` logic, `SetContent`
  vs `SetHtmlContent` choice + why, the 16 `<kw-l>` elements placed, deviations)
  — before the folder move.
- **Folder move:** this plan file → `docs/plans-milestones/done/`.
- **Clean git status** — only the four deliverable files + plan/handoff docs.

## Status

COMPLETED — build green (all 4 projects); 16 `<kw-l>` elements placed (5 nav +
5 footer + 6 account-nav); one recorded build-fix (the plan's pinned-contract
snippet used `TagHelperContext.ViewContext`, which does not exist — replaced
with ctor-injected `IHttpContextAccessor`, the repo's established seam, no new
DI registration). See the U2 section of the handoff notes.
