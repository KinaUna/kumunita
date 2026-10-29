# ADR 0240 — Document delete asks for confirmation; "Last modified by" shown only when it adds a name

Status: Accepted
Date: 2026-10-29
Amends: ADR 0239 (D1 wording, D6)

## Context
A live pass on the running instance after ADR 0239 found two problems on the document detail page:
1. **"Last modified by" never appears.** The controller leaves `ModifiedByName` null when the last modifier is the uploader, and
   edit and delete are owner-only (ADR 0125, ADR 0239 D3), so the modifier is always the uploader. The ADR 0239 D1 text and the
   view comment said the row is rendered, the controller comment said it is hidden.
2. **Delete is one click.** Download, Edit and Delete sit side by side, there is no restore lane (ADR 0239 D4), and a stray tap on a
   phone removes the document from every read surface.

## Decision
- **Modifier row:** keep the controller rule. The row is shown only when the last modifier is someone other than the uploader
  (so it appears once another lane, such as a future admin edit, can change a document). The view comment, the
  `DocumentDetailViewModel.ModifiedByName` comment and this ADR now say so; ADR 0239 D1 is amended accordingly. The uploader's own
  edits are shown by "Last updated" alone.
- **Delete confirmation (amends D6):** the Delete form carries `data-confirm`, the house confirm-before-submit
  (`client/lib/confirm.ts`, a native `window.confirm`, used by 69 forms). The text is the new closed key `documents.delete_confirm`
  (en, de, fr, da; `{0}` is the document title), resolved in the viewer's language in the view. No new JS, no new route, and the
  soft delete and the 404 for non-owners are unchanged. A restore lane stays out of scope.
- Tests: `Detail_ModifiedByAnotherResident_ShowsModifierName`, and `Detail_ModifiedByUploaderOrNobody_HidesModifierName`
  (edited by the uploader, never edited), which also pin `OwnerName`.

## Not covered
Razor and C# were not compiled or run in the authoring environment. The confirm dialog was not exercised in a browser; the
de, fr and da strings are machine translations and need native review.

## Follow-ups
- A Bootstrap modal could replace the native dialog on all 69 forms at once if a styled confirm is wanted; not done here.
- An admin restore lane (ADR 0239 D4) would make Delete reversible.
