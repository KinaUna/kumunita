# ADR 0233 — A rejected document upload re-renders the form with an error

Status: Accepted

## Context
ADR 0232 found that a browser form post which fails an upload guard returned a bare status (`StatusCodeResult` for 413/415,
`BadRequestObjectResult` for 400) with an empty body, so the person saw a blank white page and lost what they had typed.
While checking the form live, a second bug showed up: a plain `GET /documents/new` rendered "The Mode field is required."
on a fresh form.

## Decision
- `DocumentController.Upload` and `Edit` return the form view (`New` / `Edit`) for a failed guard — blank title, no or empty
  file, over size or over quota (the shared `UploadGate` still decides; its `StatusCodeResult` status is carried over), and a
  disallowed type — with the same status code (400 / 413 / 415), the bound model (title, summary and audience are kept),
  and a localized `ModelState` error on `Title` or `File`. The views already render `ModelState` errors in the danger alert.
  Every guard still runs before any byte is written.
- `UploadGate` itself is unchanged: it stays the single producer of the 413 decision. The Attachment, ContentImage and
  Profile lanes are not touched (see follow-ups).
- New keys `documents.err_title_required`, `documents.err_file_required`, `documents.err_too_large`,
  `documents.err_type_not_allowed` (en/de/fr/da). The file-required text tells the person to pick the file again, because
  browsers never refill a file input.
- `GET /documents/new` no longer takes a bound `DocumentUploadViewModel?` parameter. The model was validated on a plain GET,
  and the non-nullable `Audience.Mode` raised the false "Mode field is required" alert. The view already renders blank
  without a model.

## Tests
`DocumentControllerTests`: the eight pinned asserts (`StatusCodeResult` 413/415, `BadRequestObjectResult` 400, upload and edit)
now go through `AssertFormRejected` (a `ViewResult` with the view name, the same status code and a `ModelState` error on the
field); new `Upload_BlankTitle_400_ReRendersForm_NoPut`.

## Verified
Live, in the browser pane: the unfixed GET shows the false Mode alert (server-rendered), and a bare 415 returns an empty body.
The danger alert that will now carry the guard message was checked with the real text in Ember dark (7.6:1). The controller
and test changes are not compiled or run in this sandbox (no dotnet).

## Follow-ups
- Re-run the document tests and try a real wrong-type upload once the new build is running.
- The other upload lanes (post attachments, content images, avatar) still answer a failed guard with a bare status. Not
  checked here whether each is a plain form post or an async upload; if any is a plain form post it has the same blank page.
