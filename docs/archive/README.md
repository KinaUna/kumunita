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

One row per lane — the register, the handoff note, the lane's design doc
(`docs/design/`), and its governing ADR, all as real links. The one-line
outcome each lane shipped lives in its handoff note's Abstract (the
*what did this lane ship* · *the one seam it created* · *the one thing a
future reader must not re-litigate* block) and the register Abstract; this
index is the pointer table, not a restatement. Lanes whose decision rests on
a foundational ADR (rather than a lane-named one) point at the
[ADR index](../adr/README.md). The [redirect map](REDIRECTS.md) is the
authoritative old→new path index.

| Lane | Register | Handoff note | Design doc | ADR |
|---|---|---|---|---|
| `esc` | [plan-esc-escalation-authorization](lanes/esc/plan-esc-escalation-authorization.md) | [esc-handoff-notes](lanes/esc/esc-handoff-notes.md) | [esc-escalation-authorization-design.md](../design/esc-escalation-authorization-design.md) | [0159-escalation-authorization](../adr/0159-escalation-authorization.md) |
| `ev-dwm` | [plan-ev-dwm](lanes/ev-dwm/plan-ev-dwm.md) | [handoff-notes](lanes/ev-dwm/handoff-notes.md) | [events-calendar-dwm-design.md](../design/events-calendar-dwm-design.md) | [0064-events-calendar-dwm-views](../adr/0064-events-calendar-dwm-views.md) |
| `events-calendar` | [plan-events-calendar](lanes/events-calendar/plan-events-calendar.md) | [events-calendar-handoff-notes](lanes/events-calendar/events-calendar-handoff-notes.md) | [events-calendar-design.md](../design/events-calendar-design.md) | [0063-events-calendar-view](../adr/0063-events-calendar-view.md) |
| `file-attachments` | [plan-file-attachments](lanes/file-attachments/plan-file-attachments.md) | [file-attachments-handoff-notes](lanes/file-attachments/file-attachments-handoff-notes.md) | [file-attachments-design.md](../design/file-attachments-design.md) | [0034-file-attachments-posts-replies-announcements](../adr/0034-file-attachments-posts-replies-announcements.md) |
| `group-events` | [plan-group-events](lanes/group-events/plan-group-events.md) | — | — | [0089-group-events-membership-lane](../adr/0089-group-events-membership-lane.md) |
| `group-posts` | [plan-group-posts](lanes/group-posts/plan-group-posts.md) | [group-posts-handoff-notes](lanes/group-posts/group-posts-handoff-notes.md) | [group-posts-design.md](../design/group-posts-design.md) | [0013-group-posts-membership-lane](../adr/0013-group-posts-membership-lane.md) |
| `guardian-assignment` | [plan-guardian-assignment](lanes/guardian-assignment/plan-guardian-assignment.md) | [guardian-assignment-handoff-notes](lanes/guardian-assignment/guardian-assignment-handoff-notes.md) | [guardian-assignment-design.md](../design/guardian-assignment-design.md) | [0038-guardian-assignment](../adr/0038-guardian-assignment.md) |
| `guardian-assignment-second-audit-row` | [plan-guardian-assignment-second-audit-row](lanes/guardian-assignment-second-audit-row/plan-guardian-assignment-second-audit-row.md) | [guardian-assignment-second-audit-row-handoff-notes](lanes/guardian-assignment-second-audit-row/guardian-assignment-second-audit-row-handoff-notes.md) | [guardian-assignment-design.md](../design/guardian-assignment-design.md) | [0038-guardian-assignment](../adr/0038-guardian-assignment.md) |
| `guardian-controls` | [plan-guardian-controls](lanes/guardian-controls/plan-guardian-controls.md) | [guardian-controls-handoff-notes](lanes/guardian-controls/guardian-controls-handoff-notes.md) | [guardian-controls-design.md](../design/guardian-controls-design.md) | [0028-guardian-controls-account-scope-supervision](../adr/0028-guardian-controls-account-scope-supervision.md) |
| `improve` | [plan-improve](lanes/improve/plan-improve.md) | [handoff-notes](lanes/improve/handoff-notes.md) | — | [the ADR index](../adr/README.md) |
| `improve-code` | [plan-improve-code](lanes/improve-code/plan-improve-code.md) (round 1) · [plan-improve-code-2](lanes/improve-code/plan-improve-code-2.md) (round 2) · [plan-improve-code-3](lanes/improve-code/plan-improve-code-3.md) (round 3) | [handoff-notes](lanes/improve-code/handoff-notes.md) (round 1) · [improve-code-2-handoff-notes](lanes/improve-code/improve-code-2-handoff-notes.md) (round 2) · [improve-code-3-handoff-notes](lanes/improve-code/improve-code-3-handoff-notes.md) (round 3, **shipped 2026-11-06** — the six-member flip landed; the 17 Marten-allowlist keeps are the honest floor, ADR 0296) | — (the lane's *output* is the code split + the ratchet gate at `tools/code/`; the C00–C13 / C2-00–C2-08 / C3-00–C3-08 units all live in `lanes/improve-code/`) | — (the decisions are the amending ADRs [0286](../adr/0286-iuserservice-access-core-split.md) / [0287](../adr/0287-projects-module-split.md) / [0288](../adr/0288-one-translation-variant-lane.md) / [0289](../adr/0289-data-as-embedded-resources.md) / [0290](../adr/0290-improve-code-2-baseline-raises-and-seams.md) / [0291](../adr/0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md) / [0292](../adr/0292-improve-code-3-c3-02-sampledataseeder-decomposition-raise.md)–[0297](../adr/0297-improve-code-3-c3-07-flagged-accidental-boundary-edges.md)) |
| `improve-docs` | [plan-improve-docs](lanes/improve-docs/plan-improve-docs.md) | — (the D14 close report is in the plan's `D14` Shipped note; the harness is the `tools/docs/` close gate, wired into CI) | — (the lane's *output* is the doc layer itself; the design docs it restructured stay living in `docs/design/`) | — (the lane's decision is the doc conventions in [`../DOCS-CONVENTIONS.md`](../DOCS-CONVENTIONS.md); the code seams it gates are the `docs` CI job + `ImproveCheck_Gate_Passes`) |
| `improve-sec` | [plan-improve-sec](lanes/improve-sec/plan-improve-sec.md) | [improve-sec-handoff-notes](lanes/improve-sec/improve-sec-handoff-notes.md) (IS45 close, **shipped 2026-11-09** — the six-member flip landed; the two M2-agreement residuals + `CodeCheck_Gate_Passes` are the recorded successor-scope known-reds) | [improve-sec-design.md](../design/improve-sec-design.md) | [0298-data-classes-in-code](../adr/0298-data-classes-in-code.md) · [0299-instance-export-operator-gated](../adr/0299-instance-export-operator-gated.md) |
| `inline-editor` | [plan-inline-editor](lanes/inline-editor/plan-inline-editor.md) | [inline-editor-handoff-notes](lanes/inline-editor/inline-editor-handoff-notes.md) | [inline-editor-design.md](../design/inline-editor-design.md) | [0032-inline-editor-rendered-default-view](../adr/0032-inline-editor-rendered-default-view.md) |
| `ip` | [plan-ip-integration-polish](lanes/ip/plan-ip-integration-polish.md) | [ip-integration-polish-handoff-notes](lanes/ip/ip-integration-polish-handoff-notes.md) | — | [the ADR index](../adr/README.md) |
| `languages-seeded` | [plan-languages-seeded](lanes/languages-seeded/plan-languages-seeded.md) | — | — | [0042-bundled-initial-language-pack](../adr/0042-bundled-initial-language-pack.md) |
| `m1` | [plan-m1-identity-groups-delegation-authorization](lanes/m1/plan-m1-identity-groups-delegation-authorization.md) | [m1-step-7-handoff-notes](lanes/m1/m1-step-7-handoff-notes.md) | [m1-identity-access.md](../design/m1-identity-access.md) | [the ADR index](../adr/README.md) |
| `m10` | [plan-m10-pwa-responsive](lanes/m10/plan-m10-pwa-responsive.md) | [m10-pwa-handoff-notes](lanes/m10/m10-pwa-handoff-notes.md) | [m10-pwa-responsive-design.md](../design/m10-pwa-responsive-design.md) | [0107-pwa-and-responsive-design](../adr/0107-pwa-and-responsive-design.md) |
| `m11` | [plan-m11-portability](lanes/m11/plan-m11-portability.md) | [m11-portability-handoff-notes](lanes/m11/m11-portability-handoff-notes.md) | [m11-portability-design.md](../design/m11-portability-design.md) | [0108-portability-import-export](../adr/0108-portability-import-export.md) |
| `m12` | [plan-m12-ical](lanes/m12/plan-m12-ical.md) | [m12-ical-handoff-notes](lanes/m12/m12-ical-handoff-notes.md) | [m12-ical-design.md](../design/m12-ical-design.md) | [0112-ical-calendar-export](../adr/0112-ical-calendar-export.md) |
| `m13` | [plan-m13-logging-analytics](lanes/m13/plan-m13-logging-analytics.md) | [m13-logging-analytics-handoff-notes](lanes/m13/m13-logging-analytics-handoff-notes.md) | [m13-logging-analytics-design.md](../design/m13-logging-analytics-design.md) | [0114-logging-and-usage-analytics](../adr/0114-logging-and-usage-analytics.md) |
| `m14` | [plan-m14-events-projects](lanes/m14/plan-m14-events-projects.md) | [m14-events-projects-handoff-notes](lanes/m14/m14-events-projects-handoff-notes.md) | [m14-events-projects-design.md](../design/m14-events-projects-design.md) | [0115-integration-of-events-and-projects](../adr/0115-integration-of-events-and-projects.md) |
| `m15` | [plan-m15-translation-bulk](lanes/m15/plan-m15-translation-bulk.md) | [m15-translation-bulk-handoff-notes](lanes/m15/m15-translation-bulk-handoff-notes.md) | [m15-translation-bulk-design.md](../design/m15-translation-bulk-design.md) | [0116-translation-bulk](../adr/0116-translation-bulk.md) |
| `m16` | [plan-m16-inventory](lanes/m16/plan-m16-inventory.md) | [m16-inventory-handoff-notes](lanes/m16/m16-inventory-handoff-notes.md) | [m16-inventory-design.md](../design/m16-inventory-design.md) | [0117-inventory](../adr/0117-inventory.md) |
| `m17` | [plan-m17-bookmarks](lanes/m17/plan-m17-bookmarks.md) | [m17-bookmarks-handoff-notes](lanes/m17/m17-bookmarks-handoff-notes.md) | [m17-bookmarks-design.md](../design/m17-bookmarks-design.md) | [0118-bookmarks](../adr/0118-bookmarks.md) |
| `m18` | [plan-m18-recurring-events](lanes/m18/plan-m18-recurring-events.md) | [m18-recurring-events-handoff-notes](lanes/m18/m18-recurring-events-handoff-notes.md) | [m18-recurring-events-design.md](../design/m18-recurring-events-design.md) | [0119-recurring-events](../adr/0119-recurring-events.md) |
| `m19` | [plan-m19-guest-accounts](lanes/m19/plan-m19-guest-accounts.md) | [m19-guest-accounts-handoff-notes](lanes/m19/m19-guest-accounts-handoff-notes.md) | [m19-guest-accounts-design.md](../design/m19-guest-accounts-design.md) | [0120-guest-accounts](../adr/0120-guest-accounts.md) |
| `m2` | [plan-m2-directory-profiles-groups](lanes/m2/plan-m2-directory-profiles-groups.md) | [m2-handoff-notes](lanes/m2/m2-handoff-notes.md) | [m2-directory-profiles-groups.md](../design/m2-directory-profiles-groups.md) | [the ADR index](../adr/README.md) |
| `m20` | [plan-m20-notification-quiet-times](lanes/m20/plan-m20-notification-quiet-times.md) | [m20-notification-quiet-times-handoff-notes](lanes/m20/m20-notification-quiet-times-handoff-notes.md) | [m20-notification-quiet-times-design.md](../design/m20-notification-quiet-times-design.md) | [0121-notification-quiet-times](../adr/0121-notification-quiet-times.md) |
| `m21` | [plan-m21-document-management](lanes/m21/plan-m21-document-management.md) | [m21-document-management-handoff-notes](lanes/m21/m21-document-management-handoff-notes.md) | [m21-document-management-design.md](../design/m21-document-management-design.md) | [0122-document-management](../adr/0122-document-management.md) |
| `m22` | [plan-m22-onboarding](lanes/m22/plan-m22-onboarding.md) | [m22-onboarding-handoff-notes](lanes/m22/m22-onboarding-handoff-notes.md) | [m22-onboarding-design.md](../design/m22-onboarding-design.md) | [0132-onboarding](../adr/0132-onboarding.md) |
| `m23` | [plan-m23-extended-profiles](lanes/m23/plan-m23-extended-profiles.md) | [m23-extended-profiles-handoff-notes](lanes/m23/m23-extended-profiles-handoff-notes.md) | [m23-extended-profiles-design.md](../design/m23-extended-profiles-design.md) | [0123-extended-profiles](../adr/0123-extended-profiles.md) |
| `m24` | [plan-m24-storage-metrics](lanes/m24/plan-m24-storage-metrics.md) | [m24-handoff-notes](lanes/m24/m24-handoff-notes.md) | [m24-storage-metrics-design.md](../design/m24-storage-metrics-design.md) | [0134-storage-metrics](../adr/0134-storage-metrics.md) |
| `m25` | [plan-m25-upload-limits](lanes/m25/plan-m25-upload-limits.md) | [m25-handoff-notes](lanes/m25/m25-handoff-notes.md) | [m25-upload-limits-design.md](../design/m25-upload-limits-design.md) | [0135-upload-limits](../adr/0135-upload-limits.md) |
| `m26` | [plan-m26-sorting](lanes/m26/plan-m26-sorting.md) | [m26-handoff-notes](lanes/m26/m26-handoff-notes.md) | [m26-sorting-design.md](../design/m26-sorting-design.md) | [0145-sorting](../adr/0145-sorting.md) |
| `m27` | [plan-m27-user-scoped-portability](lanes/m27/plan-m27-user-scoped-portability.md) | [m27-handoff-notes](lanes/m27/m27-handoff-notes.md) | [m27-user-scoped-portability-design.md](../design/m27-user-scoped-portability-design.md) | [0148-user-scoped-portability](../adr/0148-user-scoped-portability.md) |
| `m28` | [plan-m28-guardian-time-limits](lanes/m28/plan-m28-guardian-time-limits.md) | [m28-handoff-notes](lanes/m28/m28-handoff-notes.md) | [m28-guardian-time-limits-design.md](../design/m28-guardian-time-limits-design.md) | [0151-guardian-time-limits](../adr/0151-guardian-time-limits.md) |
| `m29` | [plan-m29-admin-surface-labels](lanes/m29/plan-m29-admin-surface-labels.md) | [m29-handoff-notes](lanes/m29/m29-handoff-notes.md) | [m29-admin-surface-labels-design.md](../design/m29-admin-surface-labels-design.md) | [0152-admin-surface-labels](../adr/0152-admin-surface-labels.md) |
| `m3` | [plan-m3-posts-components](lanes/m3/plan-m3-posts-components.md) | [m3-handoff-notes](lanes/m3/m3-handoff-notes.md) | [m3-posts-design.md](../design/m3-posts-design.md) | [the ADR index](../adr/README.md) |
| `m30` | [plan-m30-admin-onboarding](lanes/m30/plan-m30-admin-onboarding.md) | [m30-handoff-notes](lanes/m30/m30-handoff-notes.md) | [m30-admin-onboarding-design.md](../design/m30-admin-onboarding-design.md) | [0153-admin-onboarding](../adr/0153-admin-onboarding.md) |
| `m31` | [plan-m31-production-error-handling](lanes/m31/plan-m31-production-error-handling.md) | [m31-handoff-notes](lanes/m31/m31-handoff-notes.md) | [m31-production-error-handling-design.md](../design/m31-production-error-handling-design.md) | [0154-production-error-handling](../adr/0154-production-error-handling.md) |
| `m32` | [plan-m32-issue-submission-escalation](lanes/m32/plan-m32-issue-submission-escalation.md) | [m32-handoff-notes](lanes/m32/m32-handoff-notes.md) | [m32-issue-submission-escalation-design.md](../design/m32-issue-submission-escalation-design.md) | [0155-issue-submission-escalation](../adr/0155-issue-submission-escalation.md) |
| `m33` | [plan-m33-storage-metrics-history](lanes/m33/plan-m33-storage-metrics-history.md) | [m33-handoff-notes](lanes/m33/m33-handoff-notes.md) | [m33-storage-metrics-history-design.md](../design/m33-storage-metrics-history-design.md) | [0156-storage-metrics-history](../adr/0156-storage-metrics-history.md) |
| `m34` | [plan-m34-analytics-history](lanes/m34/plan-m34-analytics-history.md) | [m34-handoff-notes](lanes/m34/m34-handoff-notes.md) | [m34-analytics-history-design.md](../design/m34-analytics-history-design.md) | [0162-analytics-history](../adr/0162-analytics-history.md) |
| `m35` | [plan-m35-kiosk-mode](lanes/m35/plan-m35-kiosk-mode.md) | [m35-handoff-notes](lanes/m35/m35-handoff-notes.md) | [m35-kiosk-mode-design.md](../design/m35-kiosk-mode-design.md) | [0164-kiosk-mode](../adr/0164-kiosk-mode.md) |
| `m36` | [plan-m36-mentions](lanes/m36/plan-m36-mentions.md) | [m36-handoff-notes](lanes/m36/m36-handoff-notes.md) | [m36-mentions-design.md](../design/m36-mentions-design.md) | [0165-mentions](../adr/0165-mentions.md) |
| `m37` | [plan-m37-personal-notes](lanes/m37/plan-m37-personal-notes.md) | [m37-personal-notes-handoff-notes](lanes/m37/m37-personal-notes-handoff-notes.md) | [m37-personal-notes-design.md](../design/m37-personal-notes-design.md) | [0167-personal-notes](../adr/0167-personal-notes.md) |
| `m3b` | [plan-m3b-moderation](lanes/m3b/plan-m3b-moderation.md) | [m3b-handoff-notes](lanes/m3b/m3b-handoff-notes.md) | [m3b-moderation.md](../design/m3b-moderation.md) | [the ADR index](../adr/README.md) |
| `m4` | [plan-m4](lanes/m4/plan-m4.md) | [m4-handoff-notes](lanes/m4/m4-handoff-notes.md) | [m4-events-design.md](../design/m4-events-design.md) | [0054-events-rsvp-reminders](../adr/0054-events-rsvp-reminders.md) |
| `m7` | [plan-m7-pagination-filtering](lanes/m7/plan-m7-pagination-filtering.md) | [m7-handoff-notes](lanes/m7/m7-handoff-notes.md) | [m7-pagination-filtering-design.md](../design/m7-pagination-filtering-design.md) | [0090-pagination-and-filtering](../adr/0090-pagination-and-filtering.md) |
| `m8` | [plan-m8-search](lanes/m8/plan-m8-search.md) | [m8-handoff-notes](lanes/m8/m8-handoff-notes.md) | [m8-search-design.md](../design/m8-search-design.md) | [0091-search](../adr/0091-search.md) |
| `m9` | [plan-m9-messaging](lanes/m9/plan-m9-messaging.md) | [m9-messaging-handoff-notes](lanes/m9/m9-messaging-handoff-notes.md) | [m9-messaging-design.md](../design/m9-messaging-design.md) | [0105-messaging](../adr/0105-messaging.md) |
| `media` | [plan-media-file-storage](lanes/media/plan-media-file-storage.md) | [media-file-storage-handoff-notes](lanes/media/media-file-storage-handoff-notes.md) | [media-file-storage-design.md](../design/media-file-storage-design.md) | [0011-media-and-file-storage](../adr/0011-media-and-file-storage.md) |
| `multilingual` | [plan-multilingual](lanes/multilingual/plan-multilingual.md) | [multilingual-handoff-notes](lanes/multilingual/multilingual-handoff-notes.md) | [multilingual-design.md](../design/multilingual-design.md) | [0005-multilingual-support](../adr/0005-multilingual-support.md) |
| `multilingual-ui` | [plan-multilingual-ui](lanes/multilingual-ui/plan-multilingual-ui.md) | [multilingual-ui-handoff-notes](lanes/multilingual-ui/multilingual-ui-handoff-notes.md) | [multilingual-design.md](../design/multilingual-design.md) | [0015-ui-view-localization-mechanics](../adr/0015-ui-view-localization-mechanics.md) |
| `notifications` | [plan-m6-notifications](lanes/notifications/plan-m6-notifications.md) | [notifications-handoff-notes](lanes/notifications/notifications-handoff-notes.md) | [m6-notifications-design.md](../design/m6-notifications-design.md) | [0076-notifications-inbox-and-recipient-email](../adr/0076-notifications-inbox-and-recipient-email.md) |
| `pages` | [plan-pages](lanes/pages/plan-pages.md) | [pages-handoff-notes](lanes/pages/pages-handoff-notes.md) | [pages-design.md](../design/pages-design.md) | [0039-pages-hierarchy-audience-translations](../adr/0039-pages-hierarchy-audience-translations.md) |
| `pl` | [plan-pl-goals-projects](lanes/pl/plan-pl-goals-projects.md) | [pl-handoff-notes](lanes/pl/pl-handoff-notes.md) | [pl-goals-projects-design.md](../design/pl-goals-projects-design.md) | [0086-goals-projects-lane](../adr/0086-goals-projects-lane.md) |
| `prod-hardening` | [plan-prod-hardening](lanes/prod-hardening/plan-prod-hardening.md) | [hardening-handoff-notes](lanes/prod-hardening/hardening-handoff-notes.md) | — | [the ADR index](../adr/README.md) |
| `projects` | [plan-m5-projects](lanes/projects/plan-m5-projects.md) | [projects-handoff-notes](lanes/projects/projects-handoff-notes.md) | [m5-projects-design.md](../design/m5-projects-design.md) | [0067-m5-projects-todos-and-kanban](../adr/0067-m5-projects-todos-and-kanban.md) |
| `report-targets` | — | [handoff-notes](lanes/report-targets/handoff-notes.md) | — | [0173-report-targets-lane](../adr/0173-report-targets-lane.md) |
| `rich-content` | [plan-rich-content](lanes/rich-content/plan-rich-content.md) | [rich-content-handoff-notes](lanes/rich-content/rich-content-handoff-notes.md) | [rich-content-design.md](../design/rich-content-design.md) | [0025-rich-content-markdown-and-content-images](../adr/0025-rich-content-markdown-and-content-images.md) |
| `rich-editor` | [plan-rich-editor](lanes/rich-editor/plan-rich-editor.md) | [rich-editor-handoff-notes](lanes/rich-editor/rich-editor-handoff-notes.md) | [rich-editor-design.md](../design/rich-editor-design.md) | [0031-wysiwyg-editor-and-toolbar](../adr/0031-wysiwyg-editor-and-toolbar.md) |
| `sec` | [plan-sec-remediation](lanes/sec/plan-sec-remediation.md) | [sec-handoff-notes](lanes/sec/sec-handoff-notes.md) | [sec-remediation-design.md](../design/sec-remediation-design.md) | [the ADR index](../adr/README.md) |
| `sessions` | [plan-sessions](lanes/sessions/plan-sessions.md) | [sessions-handoff-notes](lanes/sessions/sessions-handoff-notes.md) | [sessions-per-session-revocation.md](../design/sessions-per-session-revocation.md) | [0281-session-doc-and-per-session-revocation](../adr/0281-session-doc-and-per-session-revocation.md) |
| `site` | [plan-site](lanes/site/plan-site.md) | [site-handoff-notes](lanes/site/site-handoff-notes.md) | [site-content-design.md](../design/site-content-design.md) | [0150-site-content-customization](../adr/0150-site-content-customization.md) |
| `system-pages` | [plan-system-pages](lanes/system-pages/plan-system-pages.md) | — | — | [0043-system-pages-shipped-and-discoverable](../adr/0043-system-pages-shipped-and-discoverable.md) |
| `tags` | [plan-tags](lanes/tags/plan-tags.md) | [tags-handoff-notes](lanes/tags/tags-handoff-notes.md) | [tags-design.md](../design/tags-design.md) | [0044-tags](../adr/0044-tags.md) |
| `tbd` | [plan-tbd-todo-dependency](lanes/tbd/plan-tbd-todo-dependency.md) | [tbd-handoff-notes](lanes/tbd/tbd-handoff-notes.md) | [tbd-todo-dependency-design.md](../design/tbd-todo-dependency-design.md) | [0087-todo-dependency-blocked-by-lane](../adr/0087-todo-dependency-blocked-by-lane.md) |
| `translation-display` | [plan-translation-display](lanes/translation-display/plan-translation-display.md) | [translation-display-handoff-notes](lanes/translation-display/translation-display-handoff-notes.md) | [translation-display-design.md](../design/translation-display-design.md) | [0027-post-reply-translation-display-and-swap](../adr/0027-post-reply-translation-display-and-swap.md) |
| `user-guides` | [plan-user-guides](lanes/user-guides/plan-user-guides.md) | — | — | [0057-user-guides-in-help-subtree-and-consistency-loop](../adr/0057-user-guides-in-help-subtree-and-consistency-loop.md) |
| `wysiwyg` | [plan-wysiwyg](lanes/wysiwyg/plan-wysiwyg.md) | [wysiwyg-handoff-notes](lanes/wysiwyg/wysiwyg-handoff-notes.md) | [wysiwyg-editor-design.md](../design/wysiwyg-editor-design.md) | [0033-wysiwyg-inline-editing](../adr/0033-wysiwyg-inline-editing.md) |
