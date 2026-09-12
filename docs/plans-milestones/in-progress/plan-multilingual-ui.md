# Multilingual — UI wiring (`ML-UI`, plan)

> **Status.** This is the **plan** for a follow-on lane that completes what the
> `ML` lane (ADR 0005, `plan-multilingual.md`) **promised but never wired**: the
> main platform UI is still **English-only by construction** even though the
> read path, the admin seams, the settings page, and the static-page engine all
> exist. This lane ships the **integration** — the views actually resolving
> through `ITranslationProvider`, a canonical `en` string registry that the
> seeder materializes (so M·12's completeness view and M·9's `en` floor become
> real), a **key-managed** admin translation editor, and a language picker that
> works for signed-out residents.
>
> **Why a separate lane and not "ML was never done"?** The `ML` lane closed
> green (370/370, all 9 units in `done/`) because its acceptance gate tested the
> **seam in isolation** — save a `TranslationResource` → the provider resolves
> it. That is true and still true. But **nothing in the shipped UI ever calls
> that seam**: no view references `ITranslationProvider`, no `TagHelper`, no
> `HtmlHelper` extension, no seeded `en` string rows, and the admin editor asks
> an admin to **hand-type** a key that no view defines. The gate passed the
> *part*; the *whole* (a resident actually seeing another language) was never
> closed. This lane closes it. It is named `ML-UI` (the UI-wiring completion of
> the ML promise) rather than reopening ML, following the **named-lane with a
> short ID** convention (`ML`, `GP`, media `M4-adjacent`).

## The four gaps this lane closes (verified against the code, 2026-09-12)

These are the facts the plan codes against — each was read directly, not
assumed.

1. **No view resolves through the provider.** A search of every
   `src/Kumunita.Web/Views/**/*.cshtml` finds **no** call to
   `ITranslationProvider` and no custom `TagHelper` (only the stock
   `Microsoft.AspNetCore.Mvc.TagHelpers`). The only `@Html.*` usage anywhere is
   `@Html.AntiForgeryToken()`. Home, Posts, Groups, Directory, Profile, Account,
   Admin, `_Layout`, `_AccountNav`, and the footer all carry **hardcoded
   English**. A fully-populated catalog changes nothing a resident sees.
2. **The `en` floor is not seeded.** `FirstBootSeeder.SeedLanguageCatalogAsync`
   stores only the `LanguageCatalog` `en` row + the `LocaleSettings` singleton.
   It stores **zero** `TranslationResource` rows and **zero** `LocalizedPage`
   rows. `GetCompletenessAsync` (the M·12 view) computes "missing" as
   *`en`-present-but-`code`-absent* — so with no `en` rows it reports the `en`
   universe as **empty** for every language. The design doc's M·9 claim ("its
   catalog row **and UI strings** … materialized by the seeder") is only half
   true: the catalog row is; the UI strings are not.
3. **The admin editor is a free-form form.** `Views/Languages/PreviewPage.cshtml`
   exposes `SaveTranslation(code, key, text)` with the **key hand-typed**. There
   is no list of the platform's actual strings — because no view defines any.
   `Views/Languages/Index.cshtml` links each language row to
   `…/pages/terms` only; there is no per-key editor.
4. **Discoverability.** `/admin/languages` has **no nav link anywhere** (the
   "Admin" item in `_AccountNav.cshtml` routes to `Admin/Index`, the accounts
   page). The picker (`/settings/language`, `LocaleController`) is
   `[Authorize]`d and labeled generically "Settings" — a **signed-out** visitor
   has no way to choose a language.

## Understanding

The value the `ML` lane set out to deliver — *"a non-English-speaking
neighborhood can run its entire platform in its own language without a code
change or a deploy"* (ADR 0005, positive consequences) — is still blocked at
the **last mile**: the data model, the resolution algorithm, the audit trail,
and the two isolated admin/settings surfaces exist, but the **primary surface**
(the pages residents and admins actually read and write) is not wired to them,
and the thing that makes the M·12 completeness view and the admin editor
meaningful (a seeded, canonical `en` string set) was never created.

This lane moves the platform from *"the provider works if you call it"* to
**"a resident sees the platform in their language, an admin edits a
well-defined set of strings per language, and anyone — signed in or out — can
pick a language."** The value chain moves one arrow: from *the seam resolves a
string* to *the view path resolves a string, for a bounded, admin-editable,
`en`-floored set of keys*.

**Two design decisions (settled, 2026-09-12 — D1 and D2 are this lane's ADR
payload):**

- **D1 — How a Razor view resolves a string.** A **`TagHelper`**
  (`<kw-l key="nav.home">`), resolved per request against
  `ITranslationProvider`, with a `LocaleCookie.Read` of the request. Chosen over
  an `HtmlHelper` extension because it is the idiomatic Razor fit, reads the
  cookie from the `HttpContext` cleanly, and falls back to **the key itself**
  (M·1) if the provider is ever absent. The provider's `GetAsync` (single) and
  `GetManyAsync` (batch, M·2 "no N round-trips") are both available; the TagHelper
  uses `GetAsync` per element (the view path is small and bounded).
- **D2 — How the canonical key set is defined and seeded.** A **curated
  bounded registry** in Core (`KnownTranslationKeys` — keys **and** their `en`
  source text), materialized as `en` `TranslationResource` rows by the
  first-run seeder (a new step). The single source of truth is read by the
  seeder (U1), the TagHelper's completeness, and the admin editor (U6). This
  makes M·9's `en` floor and M·12's completeness view real the moment a new
  instance boots, and gives the admin a closed list to edit per language instead
  of a hand-typed key.

**The one thing every unit must respect:** the provider still resolves
**platform text only** (M·3) — UGC (`Post` / `PostReply` / `Group` bodies) is
authored and rendered as written; machine translation remains a **deferred
trust boundary** (ADR 0005 C). Nothing in this lane touches UGC bodies.

## Assumptions (pinned)

- **The existing seams are frozen and correct.** `TranslationResource` /
  `LocalizedPage`, `ITranslationProvider` + `TranslationProvider`,
  `ILocalizationService` + `LocalizationService` + `LanguageCompleteness`,
  `LocaleCookie`, the `M1DocTypes` unique indexes, and the two `AddTransient`
  DI registrations all **stay as they are**. This lane adds **no** re-shape of
  any of them (the `ML` lane's drift rule holds). The **only** allowed Core
  additions are the new `KnownTranslationKeys` registry and one optional batch
  read on `ILocalizationService` (U6) — both additive, both in `Localization/`.
- **The key set is bounded and curated.** The registry (U1) is a **closed** list
  covering the shared layout + the main resident/admin pages named in the
  Assumptions scope below. Adding a key is part of a unit's deliverables
  (drift-pause check if it is *outside* the named surface). The registry is
  **not** "every string in every view" — it is the curated platform surface.
- **`en` is the only language seeded.** The seeder (U1) materializes `en` string
  rows + the `en` **terms/help** page rows for the registry. **Pinned U1
  decisions:** (a) the seeder **upserts with code-wins for `en`** — a registry
  change flows through on the next start and new keys appear on upgrade
  (ADR 0005: the source language's UI strings are *embedded and materialized*
  by the seeder); the seeder **never** touches non-`en` rows. (b) **`about` is
  NOT seeded** — a fresh instance's `/about` keeps the product-story view
  (U7's fallback); an admin *can* create an `about` page via the editor
  (M·4 — data, not config), and then it renders. Other languages are **created
  and translated by the admin** at runtime. Tests create `pl` rows themselves;
  the image ships no sample non-`en` translation.
- **The picker is a harmless cookie write.** Writing `kumunita.locale` is **not**
  an authorization decision (M·5 — the cookie is never a claim, never part of
  the authz decision). So a **public** picker (signed-out residents can pick a
  language) is defensible and is shipped (U7). The existing `[Authorize]`d
  settings page remains as the full settings experience.
- **`/about` is wired in this lane** (the `ML` close record explicitly
  *recorded, not shipped* the exact change: add `"about"` to
  `StaticPagesController.Slugs` or point `HomeController.About` at
  `GetPageAsync("about")`, reusing `Views/StaticPages/Page.cshtml` +
  `MarkdownRenderer`). U7 takes it.
- **Out of scope (carried forward, named):** machine translation of UGC (ADR
  0005 C), federation (ADR 0001-B), any renumbering of M4/M5/M6.
- **Test / acceptance model (unchanged).** `xunit.v3`; on this machine the
  discovery path (VS Test Explorer / `dotnet test`) is the known-broken bug —
  the reliable runner is `dotnet build` + `dotnet exec <assembly>.dll`
  (AGENTS.md). The acceptance gate is the same three-test shape (closed loop /
  handoff / part-vs-whole) + new invariant-anchored FACES tests.

## Invariants (this lane)

The `ML` lane's **M·1–M·9** all still bind and are **not re-stated**. Two new
invariants are added for the surface this lane ships:

- **M·10** — **The view path resolves through the provider:** every in-scope
  UI string is emitted via the `<kw-l>` TagHelper against
  `ITranslationProvider`; there is **no** hardcoded English in the in-scope
  views. A resident with a `pl` preference (with `pl` rows present) sees `pl`;
  without `pl` rows the per-string fallback (M·2) degrades that one label, and
  the last-resort floor is the **key itself** (M·1) — never a blank.
- **M·11** — **The language choice is available to every resident, signed in or
  out:** the picker writes `kumunita.locale` (M·5) and is reachable from the
  layout for **unauthenticated** visitors; the change takes effect on the
  **next request** (M·4 — no rebuild).

## FACES (this lane)

| F | Statement | Invariants |
|---|---|---|
| L1 | a signed-in resident with a `pl` preference cookie sees the **nav bar** in Polish (was: hardcoded English) | M·1, M·10 |
| L2 | a `pl`-preferring resident sees a label that has **no** `pl` row fall back **per string** to `en`, while the rest of the page stays `pl` | M·2, M·10 |
| L3 | a **signed-out** visitor opens the picker, chooses `pl`, and the **next** request renders the nav in `pl` (no sign-in required) | M·1, M·11 |
| L4 | a fresh instance (default `en`, no cookie) renders the nav, footer, and every in-scope page entirely in English — the `en` floor is seeded and always present | M·1, M·9 |
| L5 | the GlobalAdmin opens `/admin/languages/pl` and sees the **closed list** of canonical keys, each with its `en` reference text and the current `pl` value, and edits **per key** — no hand-typed key | M·4, M·6 |
| L6 | the GlobalAdmin saves a `pl` value for a key → it is visible on the **next** request **and** one `AccessAudit` row (`Via = Admin`) is written | M·4, M·6 |
| L7 | the `pl` completeness view (M·12) now lists **real** missing/present keys (because `en` rows are seeded) and is **100% present** for `en` itself | M·2, M·9 |
| L8 | a resident whose preference is `pl` reads an `en` **post** → the body is the `en` text as authored — **never** translated (the new TagHelper path does not touch UGC) | M·3 |
| L9 | `/about` renders a `LocalizedPage` body in the preferred language (per-page fallback → the product-story view when truly absent) | M·2, M·7 |

## Pinned contract (the **new** C# this lane adds — additive only)

```csharp
// ── U1: the canonical registry (NEW — Kumunita.Core/Localization) ──────────
// Single source of truth. Read by the seeder (en rows), the admin editor (the
// closed key list + en reference), and completeness (the "known" universe).
namespace Kumunita.Core.Localization;
public static class KnownTranslationKeys
{
    /// <summary>The closed, curated key → `en` source-text set (M·9 floor).</summary>
    public static IReadOnlyDictionary<string, string> EnValues { get; } = new Dictionary<string, string>
    {
        ["nav.home"]        = "Home",
        ["nav.announcements"]= "Announcements",
        ["nav.community"]   = "Community",
        ["nav.groups"]      = "Groups",
        ["nav.directory"]   = "Directory",
        ["nav.sign_in"]     = "Sign in",
        ["nav.sign_up"]     = "Sign up",
        ["nav.sign_out"]    = "Sign out",
        ["nav.profile"]     = "Profile",
        ["nav.admin"]       = "Admin",
        ["footer.tagline"]  = "A private home for one neighborhood — …",
        // … the full closed set (U1 defines it; grouped by nav / layout / posts /
        //    groups / directory / profile / account / admin / settings) — see the
        //    unit's deliverable list for the exact keys.
    };
    public static IReadOnlyCollection<string> AllKeys => EnValues.Keys;
}
// The seeder (FirstBootSeeder, NEW step) upserts (code-wins for `en`) an `en`
// TranslationResource row per key and an `en` LocalizedPage row per slug
// (terms + help only — `about` is admin-created, not seeded; the seeder never
// touches non-`en` rows).

// ── U2: the TagHelper (NEW — Kumunita.Web/TagHelpers/LocalizeTagHelper.cs) ──
[HtmlTargetElement("kw-l", Attributes = "key")]
public sealed class LocalizeTagHelper : TagHelper
{
    private readonly ITranslationProvider _provider;   // ctor-injected (DI)
    [HtmlAttributeName] public string Key { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext c, TagHelperOutput o)
    {
        var req  = c.ViewContext.HttpContext.Request;
        var pref = Kumunita.Web.Security.LocaleCookie.Read(req);      // M·5, M·8
        var text = await _provider.GetAsync(Key, pref);               // M·1/M·2 floor
        o.TagMode = TagMode.StartTagAndEndTag;                          // or SelfClosing for inline
        o.Content.SetHtmlContent(text);                                  // text is platform copy, not UGC
    }
}

// ── U6 (optional): one additive batch read on ILocalizationService ─────────
// (used twice in the editor — for "en" reference and for the target language;
//  avoids N×2 single reads). Additive, in Localization/; no re-shape.
Task<IReadOnlyDictionary<string, string>> GetTranslationsForAsync(string languageCode);
```

Everything else — the two content docs, `ITranslationProvider`, the existing
`ILocalizationService` catalog/page/upsert methods, `LocaleCookie`, the
`M1DocTypes` indexes, the two `AddTransient` registrations — is **frozen** (the
`ML` lane's pin). The admin `SaveTranslation` / `SavePage` / catalog-mutation
controller actions already exist and are reused, not rewritten.

## Approach

Two tracks, sequenced. **Track A (the seam + surface, U1–U2):** the canonical
`en` registry + seeder (U1), the `<kw-l>` TagHelper + the layout (U2). **Track
B (wire the rest, U3–U7):** translate the main views (U3–U5), the key-managed
admin editor (U6), the public picker + `/about` (U7). **Track C (governance,
U8–U9):** the FACES tests + acceptance gate (U8), the close — ADR 0015 (settles
D1/D2), `Milestones.cs`/README/`MilestonesTests.cs` sync, folder moves (U9).

Each **code** unit ends **build green** (`dotnet build Kumunita.slnx -c Debug`).
The **test** unit (U8) verifies with the `dotnet exec` assemblies runner (not
`dotnet test`). Doc units (U9) never build beyond the re-verify.

---

## Workflow — handoff protocol for fresh-context agents

**Per-unit template** (each unit plan file follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal file
list, 3–5 files <~300 lines each — the design/seam it cites is named);
**Deliverables** (a closed set of new/modified files, ≤ ~4 files / ~500 LOC, no
misc cleanups); **Exit** (build green for the touched projects; the handoff-note
entry appended *before* the folder move; the unit plan file moved to `done/`).

**Shared state (three-tier contract):**
- **Primary — the pinned seams** already in code (`ITranslationProvider`,
  `ILocalizationService`, `LocaleCookie`, `FirstBootSeeder`) + the **Pinned
  contract** above (the new `KnownTranslationKeys` / TagHelper / optional batch
  read). The exact C# each unit codes against is named per unit.
- **Secondary — this file** (`docs/plans-milestones/in-progress/plan-multilingual-ui.md`)
  — the unit registry with each unit's deliverables and exit criteria (and
  pointers to the per-unit plan files).
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md`,
  **created by U0** — this session's kickoff). One section per unit, appended
  (never rewritten); each unit writes exactly one short section before it
  exits; the next unit reads only that section + its own entry-read list.

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never re-shapes a frozen `ML` seam (`ITranslationProvider` /
`ILocalizationService` / the two content docs / `LocaleCookie` / the indexes)
— **any re-shape is a `## U<m> — Drift pause`**; (3) never adds a key to the
registry outside the named in-scope surface (a new key is a unit deliverable,
but a key for an out-of-scope view is a drift pause); (4) never touches a UGC
body (M·3); (5) if entry reads reveal a frozen seam missing or different than
the `ML` close record claims, the unit **pauses** and records `## U<m> — Drift
pause` in the handoff note.

---

## Units (9 total — one plan file each, created by each unit as it starts)

| U | Goal (one line) | Plan file (in-progress → done on exit) |
|---|---|---|
| U0 | Kickoff — create the scratch handoff note, verify the four gaps still hold, author this file's per-unit entry-read lists | `multilingual-ui-u00-plan.md` |
| U1 | The canonical `en` registry (`KnownTranslationKeys`) + the seeder step (the `en` string rows + `en` terms/help page rows — `about` is admin-created, not seeded) | `multilingual-ui-u01-plan.md` |
| U2 | The `<kw-l>` TagHelper + wiring the shared layout (`_Layout` nav + footer, `_AccountNav`) | `multilingual-ui-u02-plan.md` |
| U3 | Translate the **Posts** views (Index / Detail / New / Edit) to `<kw-l>` | `multilingual-ui-u03-plan.md` |
| U4 | Translate the **Groups** views (Index / Detail / New / PostDetail) to `<kw-l>` | `multilingual-ui-u04-plan.md` |
| U5 | Translate the **remaining in-scope** views (Directory/Index, Profile/Edit, Admin/Index, Account/Login, Account/Signup) to `<kw-l>` | `multilingual-ui-u05-plan.md` |
| U6 | The **key-managed** admin editor (per-key list with `en` reference + current value, no hand-typed keys) | `multilingual-ui-u06-plan.md` |
| U7 | The **public** language picker (signed-out) + wire `/about` | `multilingual-ui-u07-plan.md` |
| U8 | The FACES tests (L1–L9) + run + record the acceptance gate | `multilingual-ui-u08-plan.md` |
| U9 | Close — **ADR 0015** (settles D1/D2) + `Milestones.cs` `ML-UI` roadmap + README + `MilestonesTests.cs` + folder moves | `multilingual-ui-u09-plan.md` |

**Execution order is strict** U1 → U9 (U0 is the kickoff). A unit may run
against whatever the previous state is (every exit is self-verified: code units
by a green build; the test unit by the recorded `dotnet exec` run; doc units by
section presence).

### Per-unit entry-read list (the minimal files, each unit's "start here")

- **U1** — `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the `SeedLanguageCatalogAsync` step, ~lines 225–260, + the `SeedAsync` call site), `src/Kumunita.Core/Localization/TranslationResource.cs`, `LocalizedPage.cs`, `src/Kumunita.Core/M1DocTypes.cs` (the two schema pins). *No* other Core file. **Plus (read-only, for string extraction):** the in-scope views — `Views/Shared/{_Layout,_AccountNav}.cshtml`, `Views/Home/Index.cshtml`, `Views/Account/{Login,Signup}.cshtml`, `Views/Posts/{Index,Detail,New,Edit}.cshtml`, `Views/Groups/{Index,Detail,New,PostDetail}.cshtml`, `Views/Directory/Index.cshtml`, `Views/Profile/Edit.cshtml`, `Views/Admin/Index.cshtml` — to define the registry's `en` values as the **exact current** strings (U2–U5 do the view edits, not U1).
- **U2** — `src/Kumunita.Web/Security/LocaleCookie.cs`, `src/Kumunita.Core/Localization/ITranslationProvider.cs`, `src/Kumunita.Web/Views/Shared/_Layout.cshtml`, `_AccountNav.cshtml`, `src/Kumunita.Web/Views/_ViewImports.cshtml`.
- **U3** — `src/Kumunita.Web/Views/Posts/{Index,Detail,New,Edit}.cshtml` + U1's `KnownTranslationKeys`.
- **U4** — `src/Kumunita.Web/Views/Groups/{Index,Detail,New,PostDetail}.cshtml` + U1's registry.
- **U5** — `src/Kumunita.Web/Views/{Directory/Index,Profile/Edit,Admin/Index,Account/Login,Account/Signup}.cshtml` + U1's registry.
- **U6** — `src/Kumunita.Web/Controllers/LanguagesController.cs`, `src/Kumunita.Core/Localization/ILocalizationService.cs`, `src/Kumunita.Web/Views/Languages/{Index,PreviewPage}.cshtml`, U1's registry.
- **U7** — `src/Kumunita.Web/Controllers/LocaleController.cs`, `StaticPagesController.cs`, `src/Kumunita.Web/Views/Shared/_Layout.cshtml`, `src/Kumunita.Web/Views/StaticPages/Page.cshtml`.
- **U8** — `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` (the `ML` seam tests, the anchor), `src/Kumunita.Core/Localization/*` (the frozen seams), the FACES table above.
- **U9** — `docs/adr/0014-post-edit-lane-author-only.md` (the ADR shape/numbering), `src/Kumunita.Web/Milestones.cs`, `tests/Kumunita.Web.Tests/MilestonesTests.cs`, `README.md`, `docs/ARCHITECTURE.md` §8/§9, `docs/plans-milestones/` (the folder moves).

## In-scope surface (the bounded key set — D2)

The registry (U1) covers, and **only** these, the in-scope views:

- **Shared layout:** the nav (Home / Announcements / Community / Groups /
  Directory / Sign in / Sign up / Profile / Admin / Sign out), the footer
  (tagline + "Good to know" copy), the language-picker labels.
- **Account:** Login (title, submit), Signup (title, submit).
- **Home:** the hero + the section headings.
- **Posts:** list / detail / new / edit — headings, button labels, empty-states.
- **Groups:** list / detail / new / post-detail — headings, button labels,
  empty-states.
- **Directory / Profile / Admin:** the page headings + the primary action labels.

Keys use the dotted convention (`nav.home`, `posts.new_title`,
`groups.empty`, …) from the `ML` design doc's example. A view string that is
**UGC** (a post body, a reply, a group description, an announcement body) is
**never** a key (M·3). A view string outside the in-scope surface (e.g. a
moderation-only control, a setup-flow label) is **not** keyed in this lane
(drift-pause if a unit tries).

## Acceptance gate (U8 records — the `ML` §2.4 three-test shape, re-anchored)

- **Closed loop** (the admin edits a real, canonical key → a resident with a
  matching preference sees the new text on the **next** request, and the
  `translation.save` `AccessAudit` row exists).
- **Handoff** (the GlobalAdmin sets the default to `pl` → a resident with
  **no** preference and **no** `pl` row for some key sees that one label fall
  back to `en` while the rest resolves — the `LocaleSettings` change is picked
  up live, per-string, M·2).
- **Part-vs-whole** (the 9 FACES tests L1–L9 pass **together** with the
  inherited `ML` `LocalizationServiceTests` anchors — the 19 `ML` seam tests —
  in the **same** `Kumunita.Core.Tests` run as the M1/M2/M3/M3b/GP suites).

**Runner (re-verified per AGENTS.md):** `dotnet build Kumunita.slnx -c Debug`
green, then `dotnet exec` on each test assembly (**not** `dotnet test` / VS
Test Explorer — the xunit.v3 discovery quirk on this machine).

## Close (U9) — the doc/roadmap consistency set

- **ADR 0015** — "UI view-localization mechanics" (settles **D1** = TagHelper
  and **D2** = curated registry + `en` seed). Numbered after the current
  highest (0014). This is the ADR payload for the two settled design
  decisions, per the repo convention *"a new capability that settles a design
  question gets an ADR."*
- **`Milestones.cs`** — add the `ML-UI` lane to the roadmap (right after `ML`;
  `M4`/`M5`/`M6` untouched). **`MilestonesTests.cs`** — the single-in-progress
  milestone moves from `M4` to `ML-UI` (the order + single-in-progress pins
  update together, per the `ML` close record).
- **README Roadmap + status** — the Multilingual feature restated to include the
  live UI (not just the seams); `ML-UI` as the lane.
- **`docs/ARCHITECTURE.md` §8/§9** — the `Localization/` tree note gains
  `KnownTranslationKeys`; the value-chain table names the UI-wiring lane.
- **Folder moves** — `multilingual-ui-uNN-plan.md` → `done/` per unit; the
  plan file + handoff note move to `done/` at close; the `ML` close record's
  "recorded, not shipped" `/about` note is marked **shipped (ML-UI U7)**.

## Why the `ML` lane's tests didn't catch this (and what U8 does differently)

The `ML` gate's **closed loop** was *"save a `TranslationResource` → the
provider resolves it"* — a **part** that exercises the seam. It never had a
test asserting that a **view** emits a string through the provider, or that a
fresh instance has **any** `en` rows to be "complete" against. This lane's gate
adds exactly those: L1 (nav renders in the preferred language **through the
view path**), L4 (the `en` floor is **seeded**, not assumed), L7 (completeness
is **100% for `en`** because the registry is seeded), and L8 (the new view path
still never touches UGC). That is the difference between *the seam works* and
*the platform is actually multilingual*.
