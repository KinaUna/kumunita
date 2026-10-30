# ADR 0271 — Admin hub views are localized (Admin/*)

Status: Accepted
Date: 2026-10-29

## Context
The admin views were the largest remaining block of hard-coded English (~270 strings by a text-node scan). The Admin/* hub pages (Index, Platform, Communities,
Accounts, Manage, Security, Audit, BreakGlass, ErrorReports/Detail and Index, Escalation/Tokens) held about 110 of them: card titles and descriptions, table
headers, buttons, empty states and confirm/tooltip attributes.

## Decision
- Text content uses `<kw-l key="…">English floor</kw-l>`; attributes (`title`, `data-confirm`) use server-side variables with an English floor, declared in a small
  `@{ … }` block after the view's directives (injects added where the view had none).
- 84 new keys in en/de/fr/da: `admin.hub.*` (cards), `admin.platform.*`, `admin.communities.*`, `admin.accounts.*`, `admin.manage.*`, `admin.security.*`,
  `admin.audit.*`, `admin.errorreport.*`, `admin.breakglass.*`, `admin.escalation.active`, and four shared `common.edit/preview/manage` (+ the existing `common.delete`).
  Existing keys reused where the text and context matched (`settings.timezone_title`, `settings.dateformat_title`, `admin.signup_title`, `labels.title`,
  `admin.quiet.title`, `admin.anncomments_title`, `admin.messaging_title`, `nav.account`, `nav.admin`, `admin.roles_heading`, `common.add/remove/name/save/email`).
- Paragraphs with inline markup (the break-glass "once activated" note: `<em>` and a link to the audit log) become plain text, as for the Kiosk intro (ADR 0259):
  `kw-l` escapes its content and cannot carry markup.
- Page titles set in `ViewData["Title"]` (e.g. "Admin — Communities") are not part of this batch.
- The matcher skips comments, scripts and text already inside `kw-l`, and asserts every listed string was found.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Remaining admin-area English: AdminSite, AdminStorage, AdminSurfaceLabels, AdminHelp,
AdminGuests, AdminStorageMetrics, AdminSetup, AdminQuiet, AdminPortability (next patch); Mfa and Sessions; admin page titles.

The improve-check.ps1 `KnownTranslationKeys.cs` pin was re-based 13243 -> 13575 on 2026-10-29 for the 84 new keys x en/de/fr/da of this lane (the ADR 0265/0266 shipped-additions mechanism).
