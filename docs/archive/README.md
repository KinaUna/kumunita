---
id: archive-readme
title: "Archive — shipped lane history"
tier: archive
kind: entry
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/plans-milestones/README.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the home of every *shipped* milestone and named lane — the
> register, the unit plans, and the handoff note for each, frozen at the lane's
> close. Read this to find where a past lane's record now lives, the
> conventions that applied to it, and the redirect map for old paths.
> **Who is it for:** anyone who hits an old `plans-milestones/done/…` path or
> needs to see how a shipped feature was decided and closed.
> **When to read the rest:** when you need the *history* of a lane, not its
> *current* behavior (for current behavior, follow the design doc or ADR the
> lane links to).

# `docs/archive/` — shipped lane history

This folder holds the **lane history** the `IMPROVE-DOCS` lane moved out of
`docs/plans-milestones/done/` by `D02` (2026-11-02). Each lane — an M-milestone
(`mNN`) or a named lane (`<lane-slug>`) — keeps the three-tier record under
`lanes/<lane>/`:

- the **register** (`plan-*.md`) — what the lane set out to do and why;
- the **unit plans** (`*-uNN*.md` / `*-uNN-plan.md`) — the step-by-step work;
- the **handoff note** (`*-handoff-notes.md`) — the running log, frozen at close.

The design doc for the lane's subsystem lives in `docs/design/` (it stays
*living* and is the current truth); the ADR(s) the lane decided on live in
`docs/adr/`. The archive is the *decision history*, not the *current* contract.

## Status of this tier

`tier: archive` — frozen at the lane's close. Editing an archived file is a
gate violation (the `D14` close gate: nothing new under `docs/archive/` except
at a lane close). The `IMPROVE` gate (d) still scans this folder: any
`*-handoff-notes.md` over 600 lines without a TL;DR/Abstract in its first 20
lines fails the close. See the note under [Gate conventions](#gate-conventions).

## Old-path redirects

Any doc, comment, code reference, or bookmark still pointing at
`docs/plans-milestones/done/<lane>/…` is carried to its new home by
[REDIRECTS.md](REDIRECTS.md). The move was a `git mv`, so `git log --follow`
still shows the original history for every file.

## Conventions that moved here

These applied to the shipped lanes and are preserved here (they used to live in
`docs/plans-milestones/README.md`):

### The three-tier contract (per lane)

1. **primary** — the design doc under `docs/design/` (current truth, living);
2. **secondary** — the register (`plan-*.md`) in the lane folder (the
   what/why/how-the-lane-ships record);
3. **scratch** — the handoff note (`*-handoff-notes.md`) (the running log,
   frozen at close).

### The TL;DR / Abstract convention

**Every `*-handoff-notes.md` over 600 lines carries a `> **TL;DR:**` (or
`> **Abstract:**`) blockquote in its first 20 lines.** The block answers three
questions in the note's own vocabulary — *what did this lane ship (capability,
ADR, green-gate count)?* · *what is the one seam it created or changed?* ·
*what is the one thing a future reader must not re-litigate?* The
`improve-check.ps1` gate (d) enforces this. The `Abstract` name wins for new
docs; `TL;DR` is an accepted alias (see
[DOCS-CONVENTIONS.md](../DOCS-CONVENTIONS.md)).

### Gate conventions

The `improve-check.ps1` gate scans `docs/archive/lanes/` for the handoff-note
TL;DR rule (gate d). The `IMPROVE` lane's own register, audit ledger and
handoff note are in `lanes/improve/`; its audit harness (`improve-check.ps1`,
`improve-report.ps1`) lives at `tools/docs/` (moved out of the lane folder by
`D00` so it would not be archived).

## Lane index

One row per lane — register and handoff note as real links. The lane's design
doc (`docs/design/`) and ADRs are named in the register's own `related:` /
body; the [redirect map](REDIRECTS.md) is the authoritative path index.

| Lane | Register | Handoff note |
|---|---|---|
| [`esc`](lanes/esc/) | [plan-esc-escalation-authorization.md](lanes/esc/plan-esc-escalation-authorization.md) | [esc-handoff-notes.md](lanes/esc/esc-handoff-notes.md) |
| [`ev-dwm`](lanes/ev-dwm/) | [plan-ev-dwm.md](lanes/ev-dwm/plan-ev-dwm.md) | [handoff-notes.md](lanes/ev-dwm/handoff-notes.md) |
| [`events-calendar`](lanes/events-calendar/) | [plan-events-calendar.md](lanes/events-calendar/plan-events-calendar.md) | [events-calendar-handoff-notes.md](lanes/events-calendar/events-calendar-handoff-notes.md) |
| [`file-attachments`](lanes/file-attachments/) | [plan-file-attachments.md](lanes/file-attachments/plan-file-attachments.md) | [file-attachments-handoff-notes.md](lanes/file-attachments/file-attachments-handoff-notes.md) |
| [`group-events`](lanes/group-events/) | [plan-group-events.md](lanes/group-events/plan-group-events.md) | — |
| [`group-posts`](lanes/group-posts/) | [plan-group-posts.md](lanes/group-posts/plan-group-posts.md) | [group-posts-handoff-notes.md](lanes/group-posts/group-posts-handoff-notes.md) |
| [`guardian-assignment`](lanes/guardian-assignment/) | [plan-guardian-assignment.md](lanes/guardian-assignment/plan-guardian-assignment.md) | [guardian-assignment-handoff-notes.md](lanes/guardian-assignment/guardian-assignment-handoff-notes.md) |
| [`guardian-assignment-second-audit-row`](lanes/guardian-assignment-second-audit-row/) | [plan-…-second-audit-row.md](lanes/guardian-assignment-second-audit-row/plan-guardian-assignment-second-audit-row.md) | […-handoff-notes.md](lanes/guardian-assignment-second-audit-row/guardian-assignment-second-audit-row-handoff-notes.md) |
| [`guardian-controls`](lanes/guardian-controls/) | [plan-guardian-controls.md](lanes/guardian-controls/plan-guardian-controls.md) | [guardian-controls-handoff-notes.md](lanes/guardian-controls/guardian-controls-handoff-notes.md) |
| [`improve`](lanes/improve/) | [plan-improve.md](lanes/improve/plan-improve.md) | [handoff-notes.md](lanes/improve/handoff-notes.md) · [improve-audit.md](lanes/improve/improve-audit.md) |
| [`inline-editor`](lanes/inline-editor/) | [plan-inline-editor.md](lanes/inline-editor/plan-inline-editor.md) | [inline-editor-handoff-notes.md](lanes/inline-editor/inline-editor-handoff-notes.md) |
| [`ip`](lanes/ip/) | [plan-ip-integration-polish.md](lanes/ip/plan-ip-integration-polish.md) | [ip-integration-polish-handoff-notes.md](lanes/ip/ip-integration-polish-handoff-notes.md) |
| [`languages-seeded`](lanes/languages-seeded/) | [plan-languages-seeded.md](lanes/languages-seeded/plan-languages-seeded.md) | — |
| [`m1`](lanes/m1/) | [plan-m1-identity-groups-delegation-authorization.md](lanes/m1/plan-m1-identity-groups-delegation-authorization.md) | [m1-step-7-handoff-notes.md](lanes/m1/m1-step-7-handoff-notes.md) |
| [`m2`](lanes/m2/) | [plan-m2-directory-profiles-groups.md](lanes/m2/plan-m2-directory-profiles-groups.md) | [m2-handoff-notes.md](lanes/m2/m2-handoff-notes.md) |
| [`m3`](lanes/m3/) | [plan-m3-posts-components.md](lanes/m3/plan-m3-posts-components.md) | [m3-handoff-notes.md](lanes/m3/m3-handoff-notes.md) |
| [`m3b`](lanes/m3b/) | [plan-m3b-moderation.md](lanes/m3b/plan-m3b-moderation.md) | [m3b-handoff-notes.md](lanes/m3b/m3b-handoff-notes.md) |
| [`m4`](lanes/m4/) | [plan-m4.md](lanes/m4/plan-m4.md) | [m4-handoff-notes.md](lanes/m4/m4-handoff-notes.md) |
| [`m7`](lanes/m7/) | [plan-m7-pagination-filtering.md](lanes/m7/plan-m7-pagination-filtering.md) | [m7-handoff-notes.md](lanes/m7/m7-handoff-notes.md) |
| [`m8`](lanes/m8/) | [plan-m8-search.md](lanes/m8/plan-m8-search.md) | [m8-handoff-notes.md](lanes/m8/m8-handoff-notes.md) |
| [`m9`](lanes/m9/) | [plan-m9-messaging.md](lanes/m9/plan-m9-messaging.md) | [m9-messaging-handoff-notes.md](lanes/m9/m9-messaging-handoff-notes.md) |
| [`m10`](lanes/m10/) | [plan-m10-pwa-responsive.md](lanes/m10/plan-m10-pwa-responsive.md) | [m10-pwa-handoff-notes.md](lanes/m10/m10-pwa-handoff-notes.md) |
| [`m11`](lanes/m11/) | [plan-m11-portability.md](lanes/m11/plan-m11-portability.md) | [m11-portability-handoff-notes.md](lanes/m11/m11-portability-handoff-notes.md) |
| [`m12`](lanes/m12/) | [plan-m12-ical.md](lanes/m12/plan-m12-ical.md) | [m12-ical-handoff-notes.md](lanes/m12/m12-ical-handoff-notes.md) |
| [`m13`](lanes/m13/) | [plan-m13-logging-analytics.md](lanes/m13/plan-m13-logging-analytics.md) | [m13-logging-analytics-handoff-notes.md](lanes/m13/m13-logging-analytics-handoff-notes.md) |
| [`m14`](lanes/m14/) | [plan-m14-events-projects.md](lanes/m14/plan-m14-events-projects.md) | [m14-events-projects-handoff-notes.md](lanes/m14/m14-events-projects-handoff-notes.md) |
| [`m15`](lanes/m15/) | [plan-m15-translation-bulk.md](lanes/m15/plan-m15-translation-bulk.md) | [m15-translation-bulk-handoff-notes.md](lanes/m15/m15-translation-bulk-handoff-notes.md) |
| [`m16`](lanes/m16/) | [plan-m16-inventory.md](lanes/m16/plan-m16-inventory.md) | [m16-inventory-handoff-notes.md](lanes/m16/m16-inventory-handoff-notes.md) |
| [`m17`](lanes/m17/) | [plan-m17-bookmarks.md](lanes/m17/plan-m17-bookmarks.md) | [m17-bookmarks-handoff-notes.md](lanes/m17/m17-bookmarks-handoff-notes.md) |
| [`m18`](lanes/m18/) | [plan-m18-recurring-events.md](lanes/m18/plan-m18-recurring-events.md) | [m18-recurring-events-handoff-notes.md](lanes/m18/m18-recurring-events-handoff-notes.md) |
| [`m19`](lanes/m19/) | [plan-m19-guest-accounts.md](lanes/m19/plan-m19-guest-accounts.md) | [m19-guest-accounts-handoff-notes.md](lanes/m19/m19-guest-accounts-handoff-notes.md) |
| [`m20`](lanes/m20/) | [plan-m20-notification-quiet-times.md](lanes/m20/plan-m20-notification-quiet-times.md) | [m20-notification-quiet-times-handoff-notes.md](lanes/m20/m20-notification-quiet-times-handoff-notes.md) |
| [`m21`](lanes/m21/) | [plan-m21-document-management.md](lanes/m21/plan-m21-document-management.md) | [m21-document-management-handoff-notes.md](lanes/m21/m21-document-management-handoff-notes.md) |
| [`m22`](lanes/m22/) | [plan-m22-onboarding.md](lanes/m22/plan-m22-onboarding.md) | [m22-onboarding-handoff-notes.md](lanes/m22/m22-onboarding-handoff-notes.md) |
| [`m23`](lanes/m23/) | [plan-m23-extended-profiles.md](lanes/m23/plan-m23-extended-profiles.md) | [m23-extended-profiles-handoff-notes.md](lanes/m23/m23-extended-profiles-handoff-notes.md) |
| [`m24`](lanes/m24/) | [plan-m24-storage-metrics.md](lanes/m24/plan-m24-storage-metrics.md) | [m24-handoff-notes.md](lanes/m24/m24-handoff-notes.md) |
| [`m25`](lanes/m25/) | [plan-m25-upload-limits.md](lanes/m25/plan-m25-upload-limits.md) | [m25-handoff-notes.md](lanes/m25/m25-handoff-notes.md) |
| [`m26`](lanes/m26/) | [plan-m26-sorting.md](lanes/m26/plan-m26-sorting.md) | [m26-handoff-notes.md](lanes/m26/m26-handoff-notes.md) |
| [`m27`](lanes/m27/) | [plan-m27-user-scoped-portability.md](lanes/m27/plan-m27-user-scoped-portability.md) | [m27-handoff-notes.md](lanes/m27/m27-handoff-notes.md) |
| [`m28`](lanes/m28/) | [plan-m28-guardian-time-limits.md](lanes/m28/plan-m28-guardian-time-limits.md) | [m28-handoff-notes.md](lanes/m28/m28-handoff-notes.md) |
| [`m29`](lanes/m29/) | [plan-m29-admin-surface-labels.md](lanes/m29/plan-m29-admin-surface-labels.md) | [m29-handoff-notes.md](lanes/m29/m29-handoff-notes.md) |
| [`m30`](lanes/m30/) | [plan-m30-admin-onboarding.md](lanes/m30/plan-m30-admin-onboarding.md) | [m30-handoff-notes.md](lanes/m30/m30-handoff-notes.md) |
| [`m31`](lanes/m31/) | [plan-m31-production-error-handling.md](lanes/m31/plan-m31-production-error-handling.md) | [m31-handoff-notes.md](lanes/m31/m31-handoff-notes.md) |
| [`m32`](lanes/m32/) | [plan-m32-issue-submission-escalation.md](lanes/m32/plan-m32-issue-submission-escalation.md) | [m32-handoff-notes.md](lanes/m32/m32-handoff-notes.md) |
| [`m33`](lanes/m33/) | [plan-m33-storage-metrics-history.md](lanes/m33/plan-m33-storage-metrics-history.md) | [m33-handoff-notes.md](lanes/m33/m33-handoff-notes.md) |
| [`m34`](lanes/m34/) | [plan-m34-analytics-history.md](lanes/m34/plan-m34-analytics-history.md) | [m34-handoff-notes.md](lanes/m34/m34-handoff-notes.md) |
| [`m35`](lanes/m35/) | [plan-m35-kiosk-mode.md](lanes/m35/plan-m35-kiosk-mode.md) | [m35-handoff-notes.md](lanes/m35/m35-handoff-notes.md) |
| [`m36`](lanes/m36/) | [plan-m36-mentions.md](lanes/m36/plan-m36-mentions.md) | [m36-handoff-notes.md](lanes/m36/m36-handoff-notes.md) |
| [`m37`](lanes/m37/) | [plan-m37-personal-notes.md](lanes/m37/plan-m37-personal-notes.md) | [m37-personal-notes-handoff-notes.md](lanes/m37/m37-personal-notes-handoff-notes.md) |
| [`media`](lanes/media/) | [plan-media-file-storage.md](lanes/media/plan-media-file-storage.md) | [media-file-storage-handoff-notes.md](lanes/media/media-file-storage-handoff-notes.md) |
| [`multilingual`](lanes/multilingual/) | [plan-multilingual.md](lanes/multilingual/plan-multilingual.md) | [multilingual-handoff-notes.md](lanes/multilingual/multilingual-handoff-notes.md) |
| [`multilingual-ui`](lanes/multilingual-ui/) | [plan-multilingual-ui.md](lanes/multilingual-ui/plan-multilingual-ui.md) | [multilingual-ui-handoff-notes.md](lanes/multilingual-ui/multilingual-ui-handoff-notes.md) |
| [`notifications`](lanes/notifications/) | [plan-m6-notifications.md](lanes/notifications/plan-m6-notifications.md) | [notifications-handoff-notes.md](lanes/notifications/notifications-handoff-notes.md) |
| [`pages`](lanes/pages/) | [plan-pages.md](lanes/pages/plan-pages.md) | [pages-handoff-notes.md](lanes/pages/pages-handoff-notes.md) |
| [`pl`](lanes/pl/) | [plan-pl-goals-projects.md](lanes/pl/plan-pl-goals-projects.md) | [pl-handoff-notes.md](lanes/pl/pl-handoff-notes.md) |
| [`prod-hardening`](lanes/prod-hardening/) | [plan-prod-hardening.md](lanes/prod-hardening/plan-prod-hardening.md) | [hardening-handoff-notes.md](lanes/prod-hardening/hardening-handoff-notes.md) |
| [`projects`](lanes/projects/) | [plan-m5-projects.md](lanes/projects/plan-m5-projects.md) | [projects-handoff-notes.md](lanes/projects/projects-handoff-notes.md) |
| [`report-targets`](lanes/report-targets/) | — | [handoff-notes.md](lanes/report-targets/handoff-notes.md) |
| [`rich-content`](lanes/rich-content/) | [plan-rich-content.md](lanes/rich-content/plan-rich-content.md) | [rich-content-handoff-notes.md](lanes/rich-content/rich-content-handoff-notes.md) |
| [`rich-editor`](lanes/rich-editor/) | [plan-rich-editor.md](lanes/rich-editor/plan-rich-editor.md) | [rich-editor-handoff-notes.md](lanes/rich-editor/rich-editor-handoff-notes.md) |
| [`sec`](lanes/sec/) | [plan-sec-remediation.md](lanes/sec/plan-sec-remediation.md) | [sec-handoff-notes.md](lanes/sec/sec-handoff-notes.md) |
| [`sessions`](lanes/sessions/) | [plan-sessions.md](lanes/sessions/plan-sessions.md) | [sessions-handoff-notes.md](lanes/sessions/sessions-handoff-notes.md) |
| [`site`](lanes/site/) | [plan-site.md](lanes/site/plan-site.md) | [site-handoff-notes.md](lanes/site/site-handoff-notes.md) |
| [`system-pages`](lanes/system-pages/) | [plan-system-pages.md](lanes/system-pages/plan-system-pages.md) | — |
| [`tags`](lanes/tags/) | [plan-tags.md](lanes/tags/plan-tags.md) | [tags-handoff-notes.md](lanes/tags/tags-handoff-notes.md) |
| [`tbd`](lanes/tbd/) | [plan-tbd-todo-dependency.md](lanes/tbd/plan-tbd-todo-dependency.md) | [tbd-handoff-notes.md](lanes/tbd/tbd-handoff-notes.md) |
| [`translation-display`](lanes/translation-display/) | [plan-translation-display.md](lanes/translation-display/plan-translation-display.md) | [translation-display-handoff-notes.md](lanes/translation-display/translation-display-handoff-notes.md) |
| [`user-guides`](lanes/user-guides/) | [plan-user-guides.md](lanes/user-guides/plan-user-guides.md) | — |
| [`wysiwyg`](lanes/wysiwyg/) | [plan-wysiwyg.md](lanes/wysiwyg/plan-wysiwyg.md) | [wysiwyg-handoff-notes.md](lanes/wysiwyg/wysiwyg-handoff-notes.md) |
