# U6 — the key-managed admin translation editor (`ML-UI`)

**Lane:** `ML-UI` (multilingual UI wiring) · **Unit:** U6 of 9 (U0–U5 done)
**Date:** 2026-09-12

## Goal

Replace the hand-typed-key translation form with a **per-key list editor**:
a new GET action + view that shows every canonical key with its `en`
reference and the current value in the target language, each row editable and
saved through the **existing** `SaveTranslation` action — no new save path, no
hand-typed key, no re-shape of any frozen seam.

## Deliverables (closed set — 5 files touched, 1 new)

1. **`ILocalizationService.cs`** — add the D6-1 batch read:
   `Task<IReadOnlyDictionary<string, string>> GetTranslationsForAsync(string languageCode);`
2. **`LocalizationService.cs`** — implement it (one `QuerySession`, one query,
   no fallback, empty-not-null).
3. **`LanguagesController.cs`** — the new `Translations(string code)` GET +
   the nested public `TranslationEditorViewModel` / `TranslationRow` (D6-5).
4. **`Views/Languages/Translations.cshtml`** (NEW) — the per-key list editor.
5. **`Views/Languages/Index.cshtml`** — a "UI strings" button per language row.
6. **`Views/Languages/PreviewPage.cshtml`** — delete the hand-typed-key form;
   add a "UI strings" link.
7. **`LocalizationServiceTests.cs`** — the batch-read test (raw rows, no
   fallback, empty-not-null).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green (all 4 projects).
- `SaveTranslation` present only in `LanguagesController.cs` + the new
  `Translations.cshtml`; no **typed** `name="key"` input anywhere in `Views/`.
- `Translations.cshtml` renders 52 rows from the model (no hardcoded key
  list); `en` reference from `KnownTranslationKeys.EnValues`.
- The batch-read test passes (`dotnet exec` in-process runner).
- `## U6 — …` appended to the handoff note; this file moved to `done/`.
- `git status` — only the files above + the note + this plan-file move.
