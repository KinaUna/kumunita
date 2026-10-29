# ADR 0237 — A rejected avatar upload re-renders Profile/Edit with a localized error

Status: Accepted

## Context
ADR 0233 fixed the blank page after a rejected document upload. The same audit of the other upload lanes found one more plain
form post: `POST /profile/avatar` from Profile/Edit. Its guards (empty file, over the size or quota limit, wrong type) returned a
bare `BadRequest("Choose an image.")` or a `StatusCodeResult` (413 / 415), which a browser shows as a blank page. The attachment
(`POST /attachment`) and content-image (`POST /content-image`) lanes are only called with `fetch` from the rich editor, which reads the
status code, so they are unchanged.

## Decision
- A rejected avatar upload re-renders `Profile/Edit` with the **same status code** (400, 413, 415) and a localized message next to the
  avatar field, passed as `ViewData["AvatarError"]` (the main form's validation summary does not repeat it).
- `Edit()` now builds its model in a private `BuildEditModelAsync(subject)`, which the reject path reuses, so the page is identical
  to a normal load. `ProfileEditViewModel` is untouched (its "exactly seven form fields" pin holds).
- New keys `profile.avatar_err_choose`, `profile.avatar_err_too_large`, `profile.avatar_err_type` in en, de, fr and da (the three
  non-English strings are machine translations and need native review, like those in ADR 0233).
- The guard order is unchanged and still runs before `PutAsync`, so a reject writes nothing. `UploadGate` is unchanged and keeps
  returning `StatusCodeResult`; the controller turns it into the view, as in `DocumentController`.
- Tests: the four reject cases in `ProfileAvatarUploadTests` (empty 400, oversize 413, over-quota 413, wrong type 415) now assert a
  `ViewResult` for "Edit" with the status code, a `ProfileEditViewModel` and the message; the no-write assertions are kept.

## Not covered
Razor and C# were not compiled or run in the authoring environment. The view was not looked at in a browser.

## Follow-ups
- Native review of the de, fr and da strings.
