# ADR 0072 — Kanban board: the placeholder attributes joined the localized surface

Status: Accepted
Date: 2026-09-24
Amends: 0015 (the `kw-l` key registry — the HTML-attribute exclusion gains a
documented exception), 0067 (the M5 board surface this localizes)

## Context

ADR 0067 shipped the M5 Kanban board: `Views/Projects/BoardDetail.cshtml` (the
board with its lanes, cards, the add-to-do foot, and the add-lane placeholder
lane) and `Views/Projects/BoardNew.cshtml` (the create form with its initial
lane). Every visible label, button, hint, and empty state on that surface is
wrapped in the `<kw-l>` TagHelper and resolved through the `ITranslationProvider`
(ADR 0015). One class of string was left out:

- **The `placeholder` attributes on the board's input fields.** The add-to-do
  foot's `Title` input read `placeholder="@("To-do title")"`, the add-lane
  placeholder lane's `Title` input read `placeholder="@("New lane title")"`,
  and the New-board form's initial-lane `Title` input read
  `placeholder="@("Planned")"`. A resident with a non-`en` preference saw these
  three inputs in English while the rest of the same page was localized.

This is the same class of exclusion ADR 0015 recorded — "HTML attributes
(`placeholder`, `aria-label`, `title`) … are out of the TagHelper's reach by
design and are left hardcoded." The reason holds for the *TagHelper* specifically
(`<kw-l>` emits element **content**; it cannot wrap an attribute), but it is not
a reason the strings have to stay English: the board already resolves
**another** placeholder the same way — the optional `Status` field uses
`@optionalPlaceholder`, resolved in the view's `@{ … }` block via
`Translation.GetAsync("common.optional", _kwL)` and registered in
`KnownTranslationKeys` in all four languages. So the sanctioned mechanism for
"an attribute that needs to be localized" already existed in this very file; the
three board placeholders simply predated using it.

Two constraints bound the change:

- **The registry parity pins** (`tests/Kumunita.Core.Tests/
  KnownTranslationKeys_ParityTests.cs`) require every registered key to appear
  key-for-key in **all four** language dictionaries (`En` / `De` / `Fr` / `Da`)
  with non-empty values. A new key added to `EnValues` alone would fail the
  `DeValues_Keys_Match_AllKeys_Exactly_NoEmptyValues` (and `Fr` / `Da`) tests.
- **The drift guard.** The reason ADR 0015 excluded attributes is not
  "attributes" per se — it is that an attribute whose rendered value can **diverge
  from the registered key** (e.g. one that interpolates an inlined data value, or
  an `aria-label` built by concatenation like `"Actions for lane " + lane.Title`)
  would make the registry a lie. Simple, value-free placeholder copy has no such
  drift: the registered English *is* the rendered English, and the provider floor
  (ADR 0015 D1) resolves the key to that exact English on every instance.

## Decision

- **The three board input placeholders are registered and resolved through the
  provider, not left hardcoded.** Three keys are added to `KnownTranslationKeys`
  (in all four language dictionaries, preserving the parity pins):
  - `projects.board.lane.add_todo_placeholder` — "To-do title" (the board
    detail add-to-do foot input).
  - `projects.board.lane.add_lane_placeholder` — "New lane title" (the board
    detail add-lane placeholder-lane input).
  - `projects.board.lane.first_title_placeholder` — "Planned" (the New-board
    form's initial-lane suggested-title input).

  Both views resolve them in their `@{ … }` block exactly as `optionalPlaceholder`
  already is:
  `await Translation.GetAsync("<key>", _kwL)` where `_kwL` is
  `EffectiveLanguageCode.ResolveAsync(…)`, and use the result in the input's
  `placeholder` attribute (and, for the board detail inputs, the matching
  `aria-label`, which is the same fixed copy). A non-`en` resident now sees their
  language; an unregistered key falls back to the provider floor (`en` registry)
  then to the raw key (ADR 0015) — identical safety to every other board string.

- **This is a *documented exception* to ADR 0015's HTML-attribute exclusion, not
  a reversal of it.** The exclusion stands for the cases that motivated it:
  value-interpolating attributes, `confirm()` dialogs, and C#-built markup are
  still out of scope (the TagHelper cannot wrap them, and registering a key whose
  rendered value could drift would be a lie). The exception is narrow and named:
  **simple, value-free `placeholder` attributes that are fixed UI copy.** The
  `common.optional` key (already in the registry) and the three new
  `projects.board.lane.*_placeholder` keys are that set. The
  `KnownTranslationKeys` class doc-comment is amended to record the exception
  explicitly so the registry and the ADR agree.

- **No new controller, service, or JS seam.** This is a view + registry change
  only: two Razor views and the one static registry. `client/lib/projects-board.ts`
  is untouched (it never reads the placeholder text), the `IProjectService`
  surface is untouched, and there is no schema or seeding change beyond the
  first-boot seeder's `en` upsert now picking up the three new keys (the
  existing, already-mechanized path — ADR 0005 B).

## Consequences

- **The Kanban board is now fully localized on its resident-facing surface.**
  The add-to-do and add-lane inputs (and the New-board initial-lane input)
  render in the resident's effective language, consistent with every other label
  on the page. The `en` source text is unchanged, so a fresh `en` instance
  renders identically (the M·9 / M·12 floor is preserved).
- **The registry gains three keys.** The parity tests pass because all four
  dictionaries are updated together. The admin translation editor (ADR 0015 U6)
  lists them under the closed `AllKeys` set with their English reference text, so
  a resident-facing translation can be added later through the existing editor
  with no further code change.
- **The placeholder-attribute exclusion is now precise rather than blanket.**
  Future work localizing another simple placeholder follows the same pattern
  (register in all four dictionaries + resolve via `Translation.GetAsync` in the
  view block), and the doc-comment tells them so. The drift guard for
  value-interpolating attributes is unchanged and still binding.
- **Precedent, not special-case.** `common.optional` already proved the pattern;
  this ADR generalizes it deliberately and records where it stops, so "translate
  this placeholder" and "do not translate this interpolated label" are both
  settled in one place.
