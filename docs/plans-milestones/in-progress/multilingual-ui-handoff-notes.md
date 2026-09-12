# Multilingual — UI wiring (`ML-UI`) — handoff notes

> **Scratch tier.** One section per unit, **appended** (never rewritten). Each
> unit writes exactly one short `## U#` section before it exits; the next unit
> reads only that section + its own entry-read list (the three-tier contract,
> `docs/plans-milestones/in-progress/plan-multilingual-ui.md`). **U0** is this
> session's kickoff — the verification unit that confirms the four gaps still
> hold before any code unit runs.

## U0 — Kickoff (this session)

**Date:** 2026-09-12 · **Machine:** Windows (PowerShell terminal) · **Role:**
verification + plan finalization only — **no code** in this session (U1 onward
create their own `multilingual-ui-uNN-plan.md` as they start).

**Why this lane exists (the `ML` gap, re-verified 2026-09-12):** the `ML`
lane (ADR 0005) shipped the **seams** and two isolated admin/settings surfaces
and closed green (370/370), but the **main platform UI is still
English-only by construction**. Its acceptance gate tested the *part* (save a
`TranslationResource` → the provider resolves it), never the *whole* (a view
emitting a string through the provider, or a fresh instance having any `en`
rows to be complete against). This lane closes that.

**Four gaps — re-verified this session (the facts every unit codes against):**
- **Gap 1 (no view resolves through the provider):** a search of every
  `src/Kumunita.Web/Views/**/*.cshtml` finds **zero** references to
  `ITranslationProvider` and **zero** custom `TagHelper`s (only the stock
  `Microsoft.AspNetCore.Mvc.TagHelpers` via `_ViewImports.cshtml`
  `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`). The only `@Html.*`
  usage in any view is `@Html.AntiForgeryToken()`. Nav, footer, Home, Posts,
  Groups, Directory, Profile, Account, Admin are all **hardcoded English**.
- **Gap 2 (the `en` floor is not seeded):**
  `FirstBootSeeder.SeedLanguageCatalogAsync` (lines ~236–251) stores only the
  `LanguageCatalog` `en` row + the `LocaleSettings` singleton. It stores
  **zero** `TranslationResource` rows and **zero** `LocalizedPage` rows. So
  `LocalizationService.GetCompletenessAsync` (the M·12 view) computes the `en`
  universe as **empty** for every language — M·12 and M·9's "en floor" are
  both vacuous until U1 seeds the registry.
- **Gap 3 (the admin editor is a free-form form):**
  `Views/Languages/PreviewPage.cshtml`'s `SaveTranslation(code, key, text)`
  asks the admin to **hand-type the key**; `Views/Languages/Index.cshtml` links
  each row to `…/pages/terms` only — there is no per-key editor and no list of
  the platform's actual strings.
- **Gap 4 (discoverability):** `/admin/languages` has **no nav link anywhere**
  (the "Admin" item in `_AccountNav.cshtml` routes to `Admin/Index`, the
  accounts page). The picker (`/settings/language`, `LocaleController`) is
  `[Authorize]`d and labeled "Settings" — a **signed-out** visitor cannot
  choose a language.

**Seams confirmed FROZEN (do not re-shape — the `ML` pin holds):**
- `ITranslationProvider` + `TranslationProvider` — `GetAsync` / `GetManyAsync` /
  `GetPageAsync` / `ResolveEffectiveLanguageAsync`; `ResolveChainAsync` builds
  the effective → default → `en` chain (M·1/M·2), `GetAsync`'s floor is **the
  key itself**, `GetPageAsync`'s floor is **null** (→ 404). All present,
  working, HTTP-free (M·8).
- `ILocalizationService` + `LocalizationService` — catalog mutations
  (add/enable/disable/reorder/remove/set-default), `UpsertTranslationAsync`
  (pair idiom, one `translation.save` audit row `Via=Admin`),
  `UpsertPageAsync`, `GetCompletenessAsync` (computes missing = `en`-present
  minus `code`-present), `GetTranslationAsync`, `GetPageAsync`. All present.
- `LocaleCookie` (Web, `Security/`) — `Name = "kumunita.locale"`, `Read` /
  `Write` (365-day, `HttpOnly`, `SameSite=Lax`) / `Clear`. The **only** Web
  HTTP seam (M·5/M·8).
- `M1DocTypes.cs` — `opts.Schema.For<TranslationResource>().UniqueIndex(Key,
  LanguageCode)` + `opts.Schema.For<LocalizedPage>().UniqueIndex(Slug,
  LanguageCode)` (lines ~93–95). Present.
- DI — `DependencyInjection.cs` lines 138–139:
  `AddTransient<ITranslationProvider, TranslationProvider>` +
  `AddTransient<ILocalizationService, LocalizationService>`. Present.
- The two content docs (`TranslationResource` / `LocalizedPage`) + `LanguageCatalog`
  / `LocaleSettings` — present in `src/Kumunita.Core/Localization/`.

**Only allowed ADDS for this lane (the `ML` drift rule holds):**
- `KnownTranslationKeys` (new, `Kumunita.Core/Localization`) — U1.
- One seeder step (new, `FirstBootSeeder`) — U1.
- `LocalizeTagHelper` (new, `Kumunita.Web/TagHelpers`) — U2.
- One optional additive batch read `GetTranslationsForAsync` on
  `ILocalizationService` (new, `Localization/`) — U6, **only if** U6 needs it.
- **Any other Core/Web ADD is a `## U<m> — Drift pause`.**

**Roadmap state (re-verified):** `ML` is currently **Done** and `M4` is the
single **Next** (the `ML` close, U9, moved them). This lane (`ML-UI`) lands
**between `ML` and `M4`** — a named lane with a short ID (the `ML` / `GP` /
media precedent). U9 (the close) adds `ML-UI` to the roadmap and moves the
single-in-progress milestone from `M4` to `ML-UI`; `MilestonesTests.cs` updates
with it. **M4/M5/M6 stay Events / Projects / Portability** — untouched.

**Design decisions settled (D1/D2 — the ADR 0015 payload, U9):**
- **D1 = TagHelper** (`<kw-l key="…">`), chosen over an `HtmlHelper`
  extension. Reads `LocaleCookie.Read(HttpContext.Request)` and calls
  `ITranslationProvider.GetAsync` (single, M·1/M·2 floor).
- **D2 = curated bounded registry** (`KnownTranslationKeys` — key → `en`
  source text) seeded as `en` `TranslationResource` rows by the seeder. Single
  source of truth read by the seeder (U1), the admin editor (U6), and
  completeness (U1's registry is the "known" universe).

**Unit count: 9** — U1 (registry + seeder) · U2 (TagHelper + layout) · U3
(Posts views) · U4 (Groups views) · U5 (remaining in-scope views) · U6
(key-managed admin editor) · U7 (public picker + `/about`) · U8 (FACES tests
L1–L9 + acceptance gate) · U9 (close: ADR 0015 + roadmap + folder moves).

**Drift-pause count: 0** (this session is U0 only — verification, no code).

## U1 — the canonical `en` registry + the seeder step

**Date:** 2026-09-12 · **Kind:** code unit · **Exit:** build green
(`dotnet build Kumunita.slnx -c Debug` — all 4 projects succeeded).

**What was added (the two deliverables):**
- **New** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
  closed, curated `en` registry (D2). `public static class KnownTranslationKeys`
  with `public static IReadOnlyDictionary<string, string> EnValues { get; }`
  (key → `en` source text) and `public static IReadOnlyCollection<string>
  AllKeys => EnValues.Keys.ToList();` (`.Keys` is `KeyCollection<T>`, which
  doesn't cast to `IReadOnlyCollection<T>` — a LINQ projection to `List<T>`
  does; noted as the one build-fix in the session).
- **Modified** `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — a new step
  `SeedTranslationResourcesAsync` called from `SeedAsync` **after**
  `SeedLanguageCatalogAsync` (the step-5 slot; the first-boot email stays
  step 6). The class doc's "Five steps" → "Six steps" + a new `<li>` for the
  step, to keep the doc in sync.

**Key count: 52** (the closed in-scope set, grouped + commented by area):
- nav 10 · footer 5 · settings 2 · home 3 · account 6 · posts 7 · groups 12 ·
  directory 3 · profile 2 · admin 2.
- **No UGC keys** (M·3 — post/reply/group/announcement bodies, group
  descriptions, author display names are not keyed). **No moderation-only /
  setup-flow / out-of-scope keys** — the registry is deliberately the curated
  platform surface (nav, footer, language-picker labels, page headings, primary
  action labels, empty-states), not "every string in every view".

**Upsert semantics implemented (code-wins, `en`-only):**
- **UI strings:** per key in `KnownTranslationKeys.EnValues`, query
  `(Key, "en")` → `Store` a fresh row (`Id = Guid "N"`) if absent, or overwrite
  `Text` in place if present. The query is scoped to `LanguageCode == "en"` —
  **never reads or writes a non-`en` row** (admin translations for other
  languages are untouched).
- **Pages:** upserts the `en` `terms` + `help` `LocalizedPage` rows by
  `(Slug, "en")` (title + Markdown body). **`about` is NOT seeded** — a fresh
  instance's `/about` keeps its product-story view; an admin can create an
  `about` page at runtime (the pinned U1 decision (b)).
- **One `SaveChangesAsync`** at the end of the step (atomic), **no
  `AccessAudit` row** (the seeder is not an actor — deliberately different from
  the admin `LocalizationService.Upsert*` path, which writes an audit row), a
  `LogInformation` on success.
- **Upgrade-safe:** re-running adds any new registry keys and refreshes `en`
  text to the code's current values; leaves non-`en` rows and admin-created
  `about` untouched. Mirrors the `LocalizationService.Upsert*` pair idiom
  (query-then-`Store`, surrogate `Id` "N" convention) — no re-shape of a frozen
  seam, no new index (the `M1DocTypes` pins were confirmed present and left
  alone).

**Deviations:** none against the plan. The only build fix was the
`AllKeys` `.Keys` → `.Keys.ToList()` projection (a C#-collection-typing detail,
not a design change).

**Spot-check (quick, non-test):** `KnownTranslationKeys.AllKeys` non-empty
(44); `SeedTranslationResourcesAsync` wired into `SeedAsync`. (Full behavioral
proof — `en` completeness 100%, fresh-instance nav in `en` — is U8's job, not
U1's.)

**Drift-pause count: 0.**

## U2 — the `<kw-l>` TagHelper + the shared layout wiring

**Date:** 2026-09-12 · **Kind:** code unit · **Exit:** build green
(`dotnet build Kumunita.slnx -c Debug` — all 4 projects succeeded).

**What was added (the four deliverables):**
- **New** `src/Kumunita.Web/TagHelpers/LocalizeTagHelper.cs` — the
  `<kw-l>` TagHelper (D1). `[HtmlTargetElement("kw-l", Attributes = "key")]`,
  `public sealed class LocalizeTagHelper : TagHelper`, ctor-injected
  `ITranslationProvider` + `IHttpContextAccessor`. `[HtmlAttributeName]
  public string Key { get; set; } = "";`
- **Modified** `Views/_ViewImports.cshtml` — added one line after the existing
  `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`:
  `@addTagHelper *, Kumunita.Web.TagHelpers` (existing line untouched).
- **Modified** `Views/Shared/_Layout.cshtml` — **5 nav** labels
  (`nav.home`, `nav.announcements`, `nav.community`, `nav.groups`,
  `nav.directory`) + **5 footer** strings (`footer.tagline`,
  `footer.copyright`, `footer.gtk_heading`, `footer.gtk_privacy`,
  `footer.gtk_oss`) wrapped in `<kw-l key="…">en</kw-l>`. Left untouched: the
  `<title>`/`@ViewData["Title"]`, the logo, the RepositoryInfo links, the
  `<script>` tags, the `fullBleed` logic, `@RenderBody()`/`@RenderSectionAsync`,
  and the footer's "Community" / "The project" column headings + their "Home" /
  "Announcements" / "The feed" links (not in the registry — out-of-scope).
- **Modified** `Views/Shared/_AccountNav.cshtml` — **6 labels**
  (`nav.profile`, `settings.settings`, `nav.admin`, `nav.sign_out`,
  `nav.sign_in`, `nav.sign_up`) wrapped in `<kw-l key="…">en</kw-l>`. Left
  untouched: `@Html.AntiForgeryToken()`, the `asp-*`/`method` attributes, the
  `KumunitaPrincipal.IsGlobalAdmin` check, the form structure.

**16 `<kw-l>` elements placed** (5 nav + 5 footer + 6 account-nav). Each
element's inner text is the exact `en` string from U1's registry (the M·1
source floor, and what a fresh `en` instance renders identically).

**`ProcessAsync` logic (as shipped):** read
`_httpContextAccessor.HttpContext?.Request` → `LocaleCookie.Read(request)`
(null-safe — a missing context degrades to "no preference") →
`await _provider.GetAsync(Key, pref)` → `output.TagMode =
TagMode.StartTagAndEndTag; output.Content.SetContent(text);`.

**`SetContent` vs `SetHtmlContent` — chose `SetContent`.** The plan allows
either. I chose `SetContent` (auto-escaping) because the resolved value is
platform copy and the safer path against any injection from an admin-entered
translation string; the element's inner `en` reference is still the source M·1
floor. (The provider already resolves the value — escaping it again is
harmless and conservative.)

**One build-fix (recorded, per the unit-series "deviation" duty):** the plan's
pinned-contract snippet used `context.ViewContext.HttpContext.Request`, but
`TagHelperContext` does **not** expose `ViewContext` (first build failed:
`error CS1061: 'TagHelperContext' does not contain a definition for
'ViewContext'`). The idiomatic, repo-consistent fix is to ctor-inject
`IHttpContextAccessor` (registered via `AddHttpContextAccessor()` at
`Program.cs:209`; the same seam `ClaimsSource` ctor-injects and
`Views/Directory/Detail.cshtml` `@inject`s). This is **not** a re-shape of a
frozen seam and **not** a new DI registration — it uses the existing
`IHttpContextAccessor` registration. The provider call is unchanged.

**Deviations vs the plan:** only the `IHttpContextAccessor` substitution above
(a C#-API correction to the pinned-contract *snippet's* intent, not a design
change). Everything else — the TagHelper shape, the key set, the 16 placements,
the frozen-seam non-touch — is exactly as planned.

**Drift-pause count: 0.**

## U3 — the Posts views wired to `<kw-l>`

**Date:** 2026-09-12 · **Kind:** code unit (view edits only) · **Exit:** build
green (`dotnet build Kumunita.slnx -c Debug` — all 4 projects succeeded in 3.5s)
+ the 7 `posts.*` keys present as `<kw-l>` elements in the three touched views.

**What was changed (3 files modified, 0 new — the closed set):**
- **`Views/Posts/Index.cshtml` — 3 keys:** `posts.write` (the `CanPost`-branch
  "Write a post" primary button), `posts.empty_can_post` (the `CanPost`-branch
  empty-feed `<text>`), `posts.empty` (the else-branch empty-feed `<text>`).
- **`Views/Posts/New.cshtml` — 2 keys:** `posts.new_title` (the
  `<h1>Write a post</h1>`), `posts.new_submit` (the form's submit button).
- **`Views/Posts/Edit.cshtml` — 2 keys:** `posts.edit_title` (the
  `<h1 class="mt-2">Edit post</h1>`), `posts.edit_save` (the form's submit
  button).

**7 `<kw-l>` elements placed** (3 + 2 + 2). Each wraps the **exact current
English** as inner text (the M·1 source floor — a fresh `en` instance renders
identically). The multi-line `posts.empty_can_post` value stays as a single
wrapped line inside the existing `<text>` wrapper (the `@if`/`@else` structure
untouched — text-only replacement). No `href`/`action`/`method`/`name`/`id`/
`@Html.AntiForgeryToken()`/`@if`/`@foreach`/`@Url.Action`/`Model.*`/`TempData.*`
changed — only the visible English text that matched a `posts.*` key.

**Left as-is (deliberate, recorded per the unit's instructions):**
- **`ViewData["Title"] = "Write a post"`** (`New.cshtml`) and
  **`= "Edit post"`** (`Edit.cshtml`) — localizing a browser-tab title requires
  a C# `@{}` code line calling the provider (not a `<kw-l>` element), which is
  outside U3's closed view-text scope. **Deliberate U3 limitation:** the tab
  title stays `en`; the visible `<h1>` is what gets translated. If the tab title
  should also be localized, that is a **U9 follow-on proposal** (not implemented
  here). `Index.cshtml`'s `ViewData["Title"] = $"{Model.ComponentName}"` and
  `Detail.cshtml`'s `= Model.Post.Title ?? "Post"` are dynamic (UGC-adjacent)
  and likewise out of scope.
- **`Detail.cshtml` left untouched** — scanned in full; **none** of the 7
  `posts.*` exact strings appear. The page is the UGC-reading surface (post
  body, replies, author name — M·3 / unit-series rule 4) plus out-of-scope
  strings ("Edit" button, "Report this post", "Replies", "Reply", "back to the
  post", "No replies yet"). Not a registry key, so not keyed.
- **All out-of-scope strings in the touched views** (per the lane plan's
  "explicitly out of scope" list): "Manage members", "Leave", "Communities" /
  "Browse communities" / "All", the "Everyone" ADR 0012 badge, the
  `@Model.Total post@…` hidden-count hint, the "community" helper paragraphs,
  the "Title" label, the "optional" placeholder, the "No community feed…" /
  "This post's community feed…" warnings, the audience-picker copy, the "Cancel"
  buttons — none in the `posts.*` registry, so not keyed (rule 3).

**Deviations vs the plan:** none. The 7 placements, the `ViewData["Title"]`
left-as-is decision, and the `Detail.cshtml` non-touch are all exactly as the
unit instructions specified.

**Drift-pause count: 0** (no registry/value mismatch, no frozen-seam
contradiction, no out-of-scope string attempted).
