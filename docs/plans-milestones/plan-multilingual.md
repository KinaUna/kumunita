# Multilingual (UI & platform texts) — sealed unit register (ML, in progress)

> **In progress.** This is the **plan** for the multilingual lane (`ML`, ADR 0005),
> split into **sealed units** sized for a ~64K-context fresh agent one at a time,
> exactly like `plan-group-posts.md` and `done/plan-media-file-storage.md`. The
> **primary** reference tier — the exact C# seams every unit codes against — is the
> design doc `docs/design/multilingual-design.md` (authored in full up front,
> including the §Pinned contract and the 19 pinned seam-test names). The
> **secondary** tier is **this file** (unit registry + deliverables + exit
> criteria). The **scratch** tier is
> `docs/plans-milestones/in-progress/multilingual-handoff-notes.md` (one appended
> section per unit, never rewritten).
>
> **The M1 seed is already shipped** — `LanguageCatalog` / `LocaleSettings` are in
> `src/Kumunita.Core/Localization/`, registered on `M1DocTypes`, and materialized
> by the first-run seeder (`FirstBootSeeder` step 4: the `en` row + the `en`
> default). This lane builds **on top of that**: the two content documents
> (`TranslationResource` / `LocalizedPage`), the per-request read path
> (`ITranslationProvider`), the user-preference cookie (`LocaleCookie`), the
> admin-management seam (`ILocalizationService`), and the `/admin/languages`
> surface.

## Understanding

M1 shipped the **data seed** of ADR 0005 — the language catalog + the instance
default, in `mt`, managed as data (ADR 0005 B: "Languages and translations are
data, not config"). But the platform is still **English-only by construction**:
the UI strings are hardcoded in the views, and the static pages (terms, about,
help) are static files. A non-English-speaking neighborhood cannot run the
platform in its own language without a code change and a deploy.

This lane closes that gap. It moves the platform from *"English, by
construction"* to **"a non-English-speaking neighborhood can run its entire
platform in its own language without a code change or a deploy"** (ADR 0005,
positive consequences). The value chain moves one arrow: from *"the admin can
declare the languages an instance supports"* (M1) to **"*the admin can
translate the platform's UI and static pages, per language, and residents can
pick their language — and it takes effect on the next request."***

The two content documents the ADR promises — `TranslationResource` (UI strings)
and `LocalizedPage` (static pages) — are the **module surface** this lane ships.
The read path is a single `ITranslationProvider` (HTTP-free, M·8) that resolves
per request: **user preference → instance default → `en`** (M·1), with
**per-string / per-page fallback** (M·2) so a resident never sees a blank label.
The admin surface is a single `ILocalizationService` (M·6 — every mutation
audited, `Via = Admin`) over a thin GlobalAdmin-gated `LanguagesController`.
The user preference is a **cookie**, not a claim (thin-token rule, M·5).

**Naming.** Multilingual is a **named lane** (`ML`), not a milestone letter —
the **media** (`M4-adjacent`, ADR 0011) and **group posts** (`GP`, ADR 0013)
lanes are the precedent. The roadmap letters **M4/M5/M6 stay
Events / Projects / Portability**; this lane consumes no letter. The roadmap is
**already bumped** (`ML` = *next* in `Milestones.cs` / `MilestonesTests.cs` /
README) — U9 (the close) moves `ML` → *done* and `M4` → *next*.

**The one thing every unit must respect:** the provider resolves **platform
text only** (M·3) — it never reads a `Post` / `PostReply` / `Group` body. UGC is
authored and rendered as written; machine translation is a **deferred trust
boundary** (ADR 0005 C), not a feature this lane ships.

## Assumptions (pinned; the design doc makes them exact)

- **Scope = the design doc, verbatim, no more.** In-scope: the two content
  documents + the `ITranslationProvider` read path + the `LocaleCookie`
  preference + the `ILocalizationService` admin seam + the `/admin/languages`
  surface + the settings/static-page Web routes. Out: **machine translation of
  UGC** (ADR 0005 C, *Deferred*), **federation** (ADR 0001-B), **any renumbering
  of M4/M5/M6** (design doc `## Scope`).
- **Data is two additive documents (M·4).** `TranslationResource` (`key`,
  `languageCode`, `text`) and `LocalizedPage` (`slug`, `languageCode`, `title`,
  `body` Markdown, `updated`) are Marten docs in `mt` — the pair-idiom business
  key (surrogate `Id` + `UniqueIndex` on the pair), exactly `GroupMembership` /
  `ComponentMembership`. Registered on `M1DocTypes` (the existing surface —
  ADR 0004 §B.1 additive: Marten's delta picks up the new docs, **no re-seed**).
  No new `DocTypes` surface.
- **The read path is one HTTP-free provider (M·1–M·3, M·8, M·9).**
  `ITranslationProvider` resolves the effective language (preference → default →
  `en`), one UI string (per-string fallback → the key itself as the last-resort),
  a batch of UI strings (one query), and one static page (per-page fallback →
  `null`). It reads **only** the two content documents + the M1 seed
  (`LanguageCatalog` / `LocaleSettings`) — never UGC (M·3), never the
  authorization service.
- **The admin surface is one HTTP-free seam (M·4, M·6, M·7).**
  `ILocalizationService` manages the catalog (add / enable / disable / reorder /
  remove / set-default), the UI strings (upsert), the pages (upsert), and the
  completeness view. Every mutation commits exactly **one** `AccessAudit` row
  (`Via = Admin`, the `UserInfoService` admin-action idiom). Removing the current
  default **throws** (fail-closed, no row); `LocalizedPage` rows for a removed
  language are **retained** (M·7).
- **The preference is a cookie, not a claim (M·5, M·8).** The Web layer reads /
  writes / clears the `kumunita.locale` cookie (`LocaleCookie`) and passes the
  code to the provider as a plain BCP-47 string — the cookie is **never** an
  identity claim, **never** part of the authorization decision. This is the
  **only** HTTP seam in the lane (M·8 — Core stays HTTP-free).
- **The Web surface is thin (M·6, M·7).** `LanguagesController` (GlobalAdmin-
  gated, the `AdminController` precedent) delegates each action to **one**
  `ILocalizationService` method and maps the M·7 blocked-removal to **409**
  (the optimistic-concurrency story). The settings page writes the cookie (M5/M7
  FACES); the static-page routes (`/terms`, `/about`, `/help`) render
  `ITranslationProvider.GetPageAsync` (per-page fallback → 404 when truly absent).
- **Out of scope (carried forward, named):** machine translation of UGC (ADR
  0005 C — a *deferred trust boundary*, per-item opt-in with a third-party-
  boundary review), federation (ADR 0001-B — the platform catalog may move with
  the IdP, per-instance languages stay local), any renumbering of M4/M5/M6
  (multilingual is `ML`, a named lane).
- **Test / acceptance model (unchanged).** `xunit.v3`: on this machine the
  discovery path (VS Test Explorer / `dotnet test`) is a known-broken bug — the
  reliable runner is `dotnet build` + `dotnet exec <test-assembly>.dll`
  (AGENTS.md). Three-test acceptance gate (closed loop / handoff / part-vs-
  whole) + the **19** invariant-anchored seam tests (13 FACES + 6 audit-row-shape,
  pinned in the design doc).

## Approach

Three tracks, sequenced. **Track A (Core, U1–U3):** the two content documents +
`M1DocTypes` indexes (U1), the `ITranslationProvider` read seam (U2), the
`ILocalizationService` admin seam + `LanguageCompleteness` (U3). **Track B
(Web, U4–U6):** the `LocaleCookie` + DI (U4), the `LanguagesController`
admin surface (U5), the settings page + static-page routes (U6). **Track C
(governance, U7–U9):** the 19 pinned seam tests (U7), run + record the
acceptance gate (U8), docs close + folder moves + the `Milestones.cs` bump
`ML` → done / `M4` → next (U9).

Each **code** unit ends **build green**. Each **test** unit verifies with the
`dotnet exec` assemblies runner (not `dotnet test`). Doc units (U8's close
record, U9's close) never build.

---

## Workflow — handoff protocol for fresh-context agents

**Per-unit template** (each unit plan file follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal file
list, 3–5 files <~300 lines each — no full-repo scan; the design-doc section
cited is named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files
/ ~500 LOC, no misc cleanups); **Exit** (`run_build` green for the touched
projects; the handoff-note entry appended *before* the folder move; the unit plan
file moved to `done/`).

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/multilingual-design.md`, authored
  in full up front) — the exact C# of every seam U1–U6 must match, the 19 pinned
  seam-test names, the gate shape, the drift-guard.
- **Secondary — this file** (`docs/plans-milestones/plan-multilingual.md`) — the
  unit registry with each unit's deliverables and exit criteria (and pointers to
  the per-unit plan files).
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/multilingual-handoff-notes.md`, **created
  by U0** — this session's kickoff). One section per unit, appended (never
  rewritten); each unit writes exactly one short section before it exits; the
  next unit reads only that section + its own entry-read list.

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the design doc's
§Drift guard; (3) never introduces a test whose exact name is not in the design
doc §Pinned seam tests list (a rename only with a note — no silent drift); (4)
never opens a new seam beyond the design doc's pin (the two content docs, the two
interfaces + `LanguageCompleteness`, `LocaleCookie`, the `M1DocTypes` indexes)
— **any other Core/Web ADD is a `## U<m> — Drift pause`**; (5) never re-shapes
`TranslationResource` / `LocalizedPage` / `ITranslationProvider` /
`ILocalizationService` outside the design doc's §Pinned contract; (6) if entry
reads reveal the design doc is out of date, the unit **pauses** and records
`## U<m> — Drift pause` in the handoff note.

---

## Pinned contract (directional — the design doc §Pinned contract is authoritative)

```
// The two content documents (the design doc §Pinned contract §1):
TranslationResource  { Id, Key, LanguageCode, Text }           // pair idiom; M1DocTypes unique index (Key, LanguageCode)
LocalizedPage        { Id, Slug, LanguageCode, Title, Body, Updated }  // pair idiom; M1DocTypes unique index (Slug, LanguageCode)

// The read seam (HTTP-free, M·8) — the design doc §Pinned contract §2:
Task<string>                       ResolveEffectiveLanguageAsync(string? preferredLanguageCode);
Task<string>                       GetAsync(string key, string? preferredLanguageCode);
Task<IReadOnlyDictionary<string, string>> GetManyAsync(IReadOnlyCollection<string> keys, string? preferredLanguageCode);
Task<LocalizedPage?>               GetPageAsync(string slug, string? preferredLanguageCode);

// The admin seam (HTTP-free, M·6/M·7) — the design doc §Pinned contract §3:
ListLanguagesAsync · AddLanguageAsync · SetLanguageEnabledAsync · ReorderLanguagesAsync
RemoveLanguageAsync (throws if default — M·7) · SetDefaultLanguageAsync
GetTranslationAsync · UpsertTranslationAsync
GetPageAsync(slug, code) · UpsertPageAsync
GetCompletenessAsync → LanguageCompleteness(LanguageCode, PresentKeys, MissingKeys, PresentPageSlugs, MissingPageSlugs)
// Every mutation: exactly one AccessAudit row, Via = Admin, Outcome = Allow (M·6).

// The one Web HTTP seam (M·5/M·8) — the design doc §Pinned contract §4:
LocaleCookie.Name = "kumunita.locale" · Read(HttpRequest) · Write(HttpResponse, code) · Clear(HttpResponse)
```

## Invariants (pinned for the multilingual lane — the design doc is authoritative)

- **M·1** — **Resolution order is total and deterministic:** user preference (cookie) if enabled → instance default (`LocaleSettings`) if enabled → `en` (always seeded); a resident **never sees a blank label** — the last-resort floor is the string's **key itself**.
- **M·2** — **Fallback is per-string / per-page:** a partially translated language degrades gracefully per key (UI strings) and per slug (static pages) — never as a whole view flipping to `en`.
- **M·3** — **UGC is never translated:** the provider resolves **platform** text only (UI keys + `LocalizedPage` slugs); it never touches a `Post` / `PostReply` / `Group` body (ADR 0005 C — machine translation is deferred).
- **M·4** — **Languages are data, not config** (ADR 0005 B): all four documents are Marten docs in `mt`; an admin edit takes effect on the **next request** (no env var, no rebuild, no satellite assemblies).
- **M·5** — **The preference is a cookie, not a claim** (thin-token rule, ADR 0001-B): the per-request preferred language comes from the `kumunita.locale` cookie (Web layer) and is passed to the provider as a plain BCP-47 string — **never** an identity claim, **never** part of the authorization decision.
- **M·6** — **The admin surface is GlobalAdmin-gated and audited:** every `/admin/languages` mutation appends **exactly one** `AccessAudit` row (`Via = Admin`, `Outcome = Allow`) — the `UserInfoService` admin-action idiom (ARCHITECTURE.md §5).
- **M·7** — **Removing the default is blocked; removed-language rows are retained:** `RemoveLanguageAsync` on the current default **throws** (fail-closed — no audit row); a preference pointing at a removed language **falls back to the default** (M·1); `LocalizedPage` rows for a removed language are **retained** so re-adding restores them.
- **M·8** — **Core stays HTTP-free** (ADR 0006-D): `ITranslationProvider` / `ILocalizationService` reference no ASP.NET/HTTP types; the cookie is read/written only in the Web layer and passed down as a BCP-47 string.
- **M·9** — **The source language `en` is the guaranteed floor:** its catalog row and every UI string are materialized by the first-run seeder (M1, already shipped); `en` is **always resolvable** and is the last fallback in M·1.

## FACES (pinned for the multilingual lane — the design doc is authoritative)

| F | Statement | Invariants |
|---|---|---|
| M1 | a resident with a `pl` preference cookie sees a UI string in Polish | M·1, M·2 |
| M2 | a string missing in the preferred language falls back **per string** — that one label degrades, the rest of the view stays in the preferred language | M·2 |
| M3 | a resident with **no** preference cookie sees the platform in the instance default language | M·1 |
| M4 | a fresh instance (default `en`, no cookie) renders entirely in English — the `en` floor is always present | M·1, M·9 |
| M5 | a static page renders in the preferred language per page; if the preferred version is absent, the `en` page renders (per-page fallback) | M·2, M·7 |
| M6 | a `pl`-preferring resident reads an `en` **post** → the body is the `en` text as authored — **never** translated | M·3 |
| M7 | a resident switches their preference `en → pl` on the settings page (the cookie is written) → the **next** request renders in Polish | M·1, M·5 |
| M8 | a resident whose preference points at a **removed** language silently falls back to the instance default — no error, no blank label | M·1, M·7 |
| M9 | the GlobalAdmin saves a UI string in `/admin/languages` → it is visible on the **next** request **and** one `AccessAudit` row (`Via = Admin`) is written | M·4, M·6 |
| M10 | the GlobalAdmin sets the default language to `pl` → every resident without a preference now sees `pl` on the next request **and** one audit row is written | M·1, M·6 |
| M11 | the GlobalAdmin attempts to **remove the current default** language → the action is **blocked** (the service throws; the catalog is unchanged) | M·7, M·6 |
| M12 | the GlobalAdmin opens the **per-language completeness view** for `pl` → it lists the UI keys and pages present vs. missing | M·2, M·6 |
| M13 | the GlobalAdmin **removes** a language, re-adds it, and opens its page editor → the previously-saved `LocalizedPage` rows are **restored** (retained, not deleted) | M·7 |

## Pinned seam tests (19 — the design doc §Pinned seam tests is authoritative; U7 implements)

`tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` — the 13 FACES tests
(one per M1–M13) + the 6 audit-row-shape tests:

1. `M1_PreferenceCookie_ResolvesPolishUIString`
2. `M2_MissingStringInPreferredFallsBackPerString`
3. `M3_NoPreference_UsesInstanceDefault`
4. `M4_FreshInstance_DefaultIsEnglish`
5. `M5_StaticPageLocalizesPerPage`
6. `M6_UgcRendersAsAuthored_NotTranslated`
7. `M7_PreferenceChange_TakesEffectNextRequest`
8. `M8_PreferenceAtRemovedLanguage_FallsBackToDefault`
9. `M9_AdminSavesTranslation_VisibleNextRequest`
10. `M10_AdminSetsDefault_ResidentSeesNewDefault`
11. `M11_RemoveDefaultLanguage_Blocked`
12. `M12_CompletenessView_ShowsMissingKeys`
13. `M13_RemovedLanguagePageRows_Retained`
14. `Admin_AddLanguage_AuditRowShape_ViaAdmin`
15. `Admin_RemoveLanguage_AuditRowShape_ViaAdmin`
16. `Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin`
17. `Admin_SaveTranslation_AuditRowShape_ViaAdmin`
18. `Admin_SavePage_AuditRowShape_ViaAdmin`
19. `Admin_RemoveDefaultLanguage_Blocked_NoAuditRow`

Acceptance gate (U8 records, the group-posts §2.4 shape): **closed loop** (the
GlobalAdmin saves a `TranslationResource` → the next request with that preference
resolves it; the `translation.save` audit row exists) · **handoff** (the
GlobalAdmin sets the default to `pl` → a resident with **no** preference sees
`pl` on the next request — the `LocaleSettings` change is picked up live) ·
**part-vs-whole** (the 19 seam tests pass together with the inherited
`Kumunita.Core.Tests` anchors).

## Units (9 total — one plan file each, created by each unit as it starts)

| U | Goal (one line) | Plan file (in-progress → done on exit) |
|---|---|---|
| U1 | The two content documents (`TranslationResource` / `LocalizedPage`) + the `M1DocTypes` unique indexes | `multilingual-u01-plan.md` |
| U2 | The `ITranslationProvider` read seam + `TranslationProvider` impl | `multilingual-u02-plan.md` |
| U3 | The `ILocalizationService` admin seam + `LocalizationService` impl + `LanguageCompleteness` | `multilingual-u03-plan.md` |
| U4 | The `LocaleCookie` helper + the Core DI registrations | `multilingual-u04-plan.md` |
| U5 | The `LanguagesController` `/admin/languages` surface (GlobalAdmin-gated, thin over `ILocalizationService`) | `multilingual-u05-plan.md` |
| U6 | The user settings page (cookie write) + the static-page routes (`/terms`, `/about`, `/help`) | `multilingual-u06-plan.md` |
| U7 | The 19 pinned seam tests — `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` | `multilingual-u07-plan.md` |
| U8 | Run + record the multilingual acceptance gate (the design doc §Acceptance gate) | `multilingual-u08-plan.md` |
| U9 | Close — `Milestones.cs` `ML` → done / `M4` → next + README Roadmap + `ARCHITECTURE.md` §8/§9 sync + folder moves | `multilingual-u09-plan.md` |

**Execution order is strict** U1 → U9; a unit may run against whatever the
previous state is (every exit is self-verified: doc units exit by section
presence; code units by a green build; test units by the recorded run).
