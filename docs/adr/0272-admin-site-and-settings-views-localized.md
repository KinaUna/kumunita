# ADR 0272 — AdminSite and the other admin settings views are localized

Status: Accepted
Date: 2026-10-29

## Context
After the Admin hub (ADR 0271) the remaining admin-area English was concentrated in AdminSite (77 strings: hero/section toggles, palette, hero translations) and the
smaller settings views AdminStorage, AdminSurfaceLabels, AdminHelp, AdminGuests, AdminStorageMetrics, AdminSetup, AdminQuiet and AdminPortability.

## Decision
- Same mechanism as ADR 0271: `kw-l` for text, server-side variables (`_actionsLabel`, `_removeConfirm`) for the two AdminSite attributes. Repeated labels (the six hero
  field labels, "Save translation", "Add translation", the ⋮ menu verbs) share one key per text across the add and edit forms.
- 100 new keys in en/de/fr/da: `admin.site.*`, `admin.storage.*`, `admin.labels.*`, `admin.help.*`, `admin.guests.*`, `admin.storagemetrics.*`, `admin.setup.*`, `admin.quiet.lede/range_hint`,
  `admin.portability.*`, plus the leftover paragraphs on Admin/BreakGlass and Admin/Security from ADR 0271 (`admin.breakglass.explainer`, `admin.security.audit_desc`).
  Reused: `admin.hub.storage_title`, `admin.hub.storage_settings_title`, `admin.platform.site_title`, `admin.platform.help_title`, `common.*`, `account.login_submit`.
- Paragraphs with inline markup (`<code>`, `<c>`, `<b>`, `<em>`, `<strong>`) become plain text, per ADR 0259/0271. Where the markup carried a `<` (`now < Until` on AdminGuests),
  the sentence was reworded ("live from the From time up to, but not including, the Until time") because `kw-l` content cannot hold a raw `<` and escaping it would double-encode.
- The help-page "Reset all" button uses a new `admin.help.reset_all_button`; `admin.help.reset_all` already exists as the confirm-dialog text.
- The AdminSite "Add/Edit/Remove — <language>" menu items localize the verb and keep the language name as-is.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Admin `ViewData["Title"]`s (e.g. "Admin — Site content") remain English.
Remaining: Mfa and Sessions settings views, a handful of leftovers (Portability pages, MyDrafts, Moderation/Resolve, StaticPages/About, Layout), and the page titles.
