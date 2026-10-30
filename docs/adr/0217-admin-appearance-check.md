# ADR 0217 — Admin Appearance form: end-to-end check and duplicate flash

Status: Accepted

## Context
The admin Appearance form (`/admin/site`, ADR 0175) had not been exercised end to end. Checked live as the
sample admin: change the default palette, save, read what a cookie-free visitor and a member see, restore.

## Verified
- Save changes the platform default at once: a cookie-free request to `/` went from Forest to Ember (the
  provider cache is invalidated), and back.
- A member's own palette pick keeps winning over the platform default; the account menu's "Platform default"
  clears the override and follows the new default (checkmark moves correctly).
- A POST without the anti-forgery token is 400; an anonymous POST is redirected.
- Dark mode: select, label, heading and the saved-message box read correctly.
- A tampered value (`DefaultPalette=bogus`) stores Forest. This is deliberate (ADR 0175 D3, pinned by
  `SavePalette_UnknownValue_StoresForest`), so it is left as is.

## Finding and fix
After a save the confirmation showed twice: the layout's flash toast (`_FlashToast`, the "one, uniform
flash-message surface", ADR comment in `_Layout`) and an inline `.alert` that five admin views still render
from the same `TempData["info"]` / `["error"]` (a read does not consume the value within a request). The
inline copies are removed from `AdminSite/Index`, `AdminSurfaceLabels/Index`, `Admin/ErrorReports/Index`,
`Admin/ErrorReports/Detail` and `Admin/Escalation/Index`; the toast (role status / alert, 5 s) remains.

## Not changed / open
- The toast hides after 5 s. An error flash on `Admin/Escalation` (an invalid endpoint) is therefore no
  longer persistent on the page; the form still shows its own validation. Say if errors should stay inline.
- The form shows no preview of the two palettes (a name and a dash-description only).
- Some view header comments (ErrorReports) still describe the removed inline alert.
- Not verified: keyboard focus ring on the select, phone width, the other four views' flash after an action.
- Nothing was compiled.
