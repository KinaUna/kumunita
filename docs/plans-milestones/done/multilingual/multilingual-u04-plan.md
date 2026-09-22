# U4 — Multilingual: the `LocaleCookie` Web seam + the Core DI registrations

**Milestone:** multilingual (`ML`, ADR 0005) · **Read first (5 min):**
`docs/design/multilingual-design.md` §Pinned contract §4 + the §5 registration
paragraph (the **exact** C# of `LocaleCookie` and the two `AddTransient`
registrations — this unit matches them verbatim) +
`docs/plans-milestones/done/plan-multilingual.md` (master register — invariants
M·1–M·9, FACES M1–M13, unit-series rules) + the **U3** section of
`docs/plans-milestones/done/multilingual-handoff-notes.md` (U3 shipped
`ILocalizationService` + `LocalizationService` — the provider and the service
are both implemented now and both need their registrations). **No repo-wide
scan.**

## Goal
Ship the **one Web HTTP seam** of the lane — `LocaleCookie` in
`src/Kumunita.Web/Security/` (the `kumunita.locale` preference cookie, the
read/write/clear trio, M·5/M·8) — and register the two U2/U3 seams in
`src/Kumunita.Core/DependencyInjection.cs`:
`AddTransient<ITranslationProvider, TranslationProvider>()` +
`AddTransient<ILocalizationService, LocalizationService>()`, both receiving the
host-registered `Marten.IDocumentStore`. **Code unit — build must be green.**

## Entry reads (the minimal set — read in this order)
1. `docs/design/multilingual-design.md` §Pinned contract §4 + §5 only — the
   exact `LocaleCookie` class (member names, `Name = "kumunita.locale"`,
   `MaxAgeDays = 365`, HttpOnly / `SameSite=Lax`) and the registration paragraph
   (verbatim contract).
2. `docs/plans-milestones/done/multilingual-handoff-notes.md` — the U3
   section (U2 + U3 shipped the two seams; **U4's registration is what they are
   waiting on** — `ITranslationProvider` → `TranslationProvider`,
   `ILocalizationService` → `LocalizationService`).
3. `src/Kumunita.Core/Localization/TranslationProvider.cs` (confirm the
   constructor shape — `TranslationProvider(IDocumentStore)` — so the
   `AddTransient` registration resolves cleanly).
4. `src/Kumunita.Core/Localization/LocalizationService.cs` (confirm the
   constructor shape — `LocalizationService(IDocumentStore)`).
5. `src/Kumunita.Core/DependencyInjection.cs` (the composition-root surface —
   append the two registrations in the lane-comment style; the media lane's
   `IMediaStore` block is the exact shape to mirror).
6. `src/Kumunita.Web/Security/KumunitaPrincipal.cs` (the Web `Security/` folder
   + the static-class / doc-comment style to match — the thin-token rule's
   existing home).

## Deliverables (2 files)
1. **New:** `src/Kumunita.Web/Security/LocaleCookie.cs` — **verbatim** from the
   design doc §Pinned contract §4:
   - `public const string Name = "kumunita.locale"`;
   - `public const int MaxAgeDays = 365`;
   - `Read(HttpRequest)` → the preferred BCP-47 code, or `null` (no
     preference — the provider resolves the instance default, M·1);
   - `Write(HttpResponse, string languageCode)` → appends the cookie
     `HttpOnly`, `SameSite=Lax`, `MaxAge = TimeSpan.FromDays(MaxAgeDays)` (the
     settings-page save, M7 FACES);
   - `Clear(HttpResponse)` → deletes the cookie with the same attributes
     (the settings page's "reset to default").
   - **The only place the cookie is read or written** (M·8 — Core stays
     HTTP-free; M·5 — never a claim, never part of the authorization decision).
2. **Modified:** `src/Kumunita.Core/DependencyInjection.cs` — inside
   `AddKumunitaCore`, append (after the media lane's `IMediaStore` block):
   - `AddTransient<Localization.ITranslationProvider,
     Localization.TranslationProvider>()` (U2's seam; both resolve the
     host's `IDocumentStore`);
   - `AddTransient<Localization.ILocalizationService,
     Localization.LocalizationService>()` (U3's seam).
   - A lane comment naming ML/U4 and the M·4/M·8 anchors (matching the
     file's per-lane comment style). No other line in this file is touched.

**Out of this unit (unit-series rule 1):** no `LanguagesController` (U5), no
settings page / static-page routes (U6), no tests (U7), no seeder edit (the M1
`en` row is already shipped), no README / ADR / `Milestones.cs` edits (U9).

## Exit
- `dotnet build Kumunita.slnx -c Debug` **green**.
- The U4 handoff-note section appended to
  `docs/plans-milestones/done/multilingual-handoff-notes.md` **before**
  the folder move.
- This unit plan file moved to `docs/plans-milestones/done/multilingual-u04-plan.md`.
