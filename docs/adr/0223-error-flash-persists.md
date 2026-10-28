# ADR 0223 — Error flashes stay until dismissed

Status: Accepted

## Context
ADR 0217 removed five admin views' duplicate inline alerts and kept the layout toast as the one flash surface. That
left an error flash (for example an invalid endpoint on `/admin/escalation`) vanishing after 5 s, before it could be
read or acted on. About 218 controller call sites write `TempData["error"]`.

## Decision
`client/lib/flash-toast.ts`: a toast with `role="alert"` (the error flavour) is created with `autohide: false`; the
`role="status"` info toast keeps the 5 s auto-hide. The close button (`data-bs-dismiss`) already existed and has a
translated accessible name. No markup, CSS or controller change; it applies to every page at once.

## Verified (live, compiled JS run against injected toasts)
- After 7.5 s the info toast was gone and the error toast was still shown; clicking its close button hid it.
- `tsc --strict` compiles the module clean.

## Not changed / open
- Several errors in one render stack in the fixed top-right container until each is closed.
- The error toast persists on the page it landed on; it is gone after the next navigation (TempData is one-shot).
