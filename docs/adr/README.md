---
id: adr-index
title: "Architecture Decision Records (index)"
tier: reference
kind: guide
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/TEMPLATE.md
  - docs/DOCS-CONVENTIONS.md
updated: 2026-11-02
---

> **Abstract:** the index of the 289 architecture decision records (ADRs) —
> one row per ADR (number, title, status), generated from the ADRs'
> front-matter. **Who is it for:** anyone finding or adding an ADR.
> **When to read the rest:** before writing a new ADR (start from
> [`TEMPLATE.md`](TEMPLATE.md)), or to locate the ADR that decided a specific
> surface.
>

# Architecture Decision Records

Lightweight records of significant technical decisions. Format: Status / Context /
Decision / Consequences. New decisions take the next number.

> **Index maintenance:** every ADR file under this folder must have a row in the
> index; the `improve-check.ps1` gate fails the close if the two drift (IMPROVE
> lane U01).

| ID   | Title                                        | Status   |
|------|----------------------------------------------|----------|
| [0001](0001-core-stack-and-identity-model.md) | Core stack & identity model | Accepted |
| [0002](0002-deployment-topology.md) | Deployment topology: one instance per neighborhood | Accepted |
| [0003](0003-roles-and-moderator-scoping.md) | Roles & moderator scoping | Accepted |
| [0004](0004-data-persistence-and-schema-evolution.md) | Data persistence & schema evolution | Accepted |
| [0005](0005-multilingual-support.md) | Multilingual support (UI & static pages) | Accepted |
| [0006](0006-module-boundary-contracts.md) | Module boundary contracts (Identity, UserInfo, Authorization) | Accepted |
| [0007](0007-group-management-lane-owner-or-globaladmin.md) | Group management lane: owner ∪ GlobalAdmin for add and remove | Accepted |
| [0008](0008-member-self-leave-lane.md) | Member self-leave lane (owner excepted) | Accepted |
| [0009](0009-group-description-display-and-edit-lane.md) | Group description: resident-facing display + owner ∪ GlobalAdmin edit | Accepted |
| [0010](0010-private-groups.md) | Private groups: a membership/organizing unit, hidden from the audience pickers | Accepted |
| [0011](0011-media-and-file-storage.md) | Media & file storage: content-addressed local-volume bytes + a `mt` catalog | Accepted |
| [0012](0012-community-membership-mandatory-and-moderator.md) | Community membership: mandatory communities + moderator-managed optional membership | Accepted |
| [0013](0013-group-posts-membership-lane.md) | Group posts: a membership-scoped group channel | Accepted |
| [0014](0014-post-edit-lane-author-only.md) | Post edit lane: author-only | Accepted |
| [0015](0015-ui-view-localization-mechanics.md) | UI view-localization mechanics | Accepted |
| [0016](0016-group-post-and-reply-edit-lane-author-only.md) | Group-post + reply edit lane: author-only | Accepted |
| [0017](0017-announcement-edit-lane-author-or-globaladmin.md) | Announcement edit lane: author-of-record ∪ GlobalAdmin (flat lane) | Accepted |
| [0018](0018-ugc-authored-in-language-tag.md) | UGC authored-in language tag (posts, replies, announcements) | Accepted |
| [0019](0019-timezone-platform-default-and-resident-override.md) | Timezone: platform default (admin) + per-resident override | Accepted |
| [0020](0020-date-format-platform-default-and-resident-override.md) | Date & time format: platform default (admin) + per-resident override + custom | Accepted |
| [0021](0021-translator-role-delegated-translation-editing.md) | Translator role: delegate translation editing to non-admin residents | Accepted |
| [0022](0022-user-added-post-reply-translations.md) | User-added post & reply translations (the lane ADR 0018 deferred) | Accepted |
| [0023](0023-reply-report-target-lane.md) | Reply as a report target (extending the M3b report lane) | Accepted |
| [0024](0024-author-soft-delete-lane.md) | Author soft-delete lane (posts + replies, community + group) | Accepted |
| [0025](0025-rich-content-markdown-and-content-images.md) | Rich content: Markdown bodies + in-content images | Accepted |
| [0026](0026-group-community-name-description-translations.md) | Group & community name/description translations | Accepted |
| [0027](0027-post-reply-translation-display-and-swap.md) | Post & reply translation display and swap (authored-in as first-class variant) | Accepted |
| [0028](0028-guardian-controls-account-scope-supervision.md) | Guardian controls: account-scope supervision of a child's account | Accepted |
| [0029](0029-announcement-translations.md) | 0029 — Announcement user-added translations (the ADR 0022 lane carried to announcements) | Accepted |
| [0030](0030-role-independence-composable-elevated-roles.md) | Role independence: elevated roles are composable, not mutually exclusive | Accepted |
| [0031](0031-wysiwyg-editor-and-toolbar.md) | WYSIWYG editor + toolbar over the RC Markdown lane | Accepted |
| [0032](0032-inline-editor-rendered-default-view.md) | Inline editor: the rendered pane is the default view | Accepted |
| [0033](0033-wysiwyg-inline-editing.md) | WYSIWYG inline editing: the rendered pane is the editable surface | Accepted |
| [0034](0034-file-attachments-posts-replies-announcements.md) | File attachments lane: downloads on post / reply / announcement | Accepted |
| [0035](0035-group-post-media-membership-lane.md) | Group-post media: the membership lane is the serve decision | Accepted |
| [0036](0036-community-visible-audience-default.md) | Community-visible audience: the default for new community posts | Accepted |
| [0037](0037-draft-mode-author-only.md) | Draft mode: saved-but-not-published, author-only (community posts, group posts, announcements) | Accepted |
| [0038](0038-guardian-assignment.md) | Guardian assignment: an existing guardian assigns a second guardian to a child's account | Accepted |
| [0039](0039-pages-hierarchy-audience-translations.md) | Pages: a hierarchical, audience-restricted, translatable knowledge tree (absorbs the static-page lane) | Accepted |
| [0040](0040-pages-system-and-user-kinds-and-blog-feed.md) | Pages: system vs user (blog) kinds, and the per-resident blog feed | Accepted |
| [0041](0041-page-audience-scope-dropdown.md) | Page audience scope dropdown: \"All residents\" as a first-class flag | Accepted |
| [0042](0042-bundled-initial-language-pack.md) | Bundled initial language pack (de / fr ship on first boot) | Accepted |
| [0043](0043-system-pages-shipped-and-discoverable.md) | System pages shipped + discoverable (SP) | Accepted |
| [0044](0044-tags.md) | Tags: free author-set subject labels for posts and blog pages (TG) | Accepted |
| [0045](0045-danish-preseeded-disabled.md) | Danish (da) pre-seeded, disabled by default | Accepted |
| [0046](0046-default-language-browser-accept-language-match.md) | Default-language fallback: browser `Accept-Language` match | Accepted |
| [0047](0047-static-page-localization-and-backfill.md) | Static-page localization: the hard-coded routes render in the | Accepted |
| [0048](0048-translation-edit-and-remove-lanes.md) | Edit + remove lanes for user-added translations | Accepted |
| [0049](0049-default-language-variant-selection.md) | Default-visible variant: the viewer's current language (when a translation exists) | Accepted |
| [0050](0050-admin-managed-signup-gate-open-or-invitation-only.md) | Admin-managed sign-up gate: open vs invitation-only | Accepted |
| [0051](0051-list-surface-variant-default.md) | List surfaces: the default-visible variant is the viewer's current language | Accepted |
| [0052](0052-warm-boot-ui-string-baseline-backfill.md) | Warm-boot backfill of the UI-string `de` / `fr` / `da` baselines | Accepted |
| [0053](0053-community-translation-surface-own-page.md) | Community translation surface on its own page | Accepted |
| [0054](0054-events-rsvp-reminders.md) | M4: Events, RSVPs & reminders (a new `Kumunita.Core.Events` context reusing the frozen authorization / audience / renderer / editor / email / §6.4-job seams) | Accepted |
| [0055](0055-development-sample-data-lane.md) | Development sample data: a first-boot, Development-only mock neighborhood | Accepted |
| [0056](0056-sample-data-opt-in-and-deploy-posture.md) | Sample-data opt-in: a flag-gated seeder with a deploy posture | Accepted |
| [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) | User guides: resident-facing guides live in the `help/` subtree of the existing page tree; `en` floor + curated `de`/`fr`/`da` baselines (community-owned); a three-layer consistency loop keeps them honest as code changes (the `UG` named lane) | Accepted |
| [0058](0058-page-reset-to-seeded-text-lane.md) | Page \"Reset to seeded text\" lane | Accepted |
| [0059](0059-event-translations.md) | Event user-added translations (the ADR 0022/0029 lane carried to the M4 surface) | Accepted |
| [0060](0060-event-translation-sample-backfill.md) | Warm-boot backfill of the sample events' `de` / `fr` / `da` translations | Accepted |
| [0061](0061-email-and-notification-language.md) | Email & notification language: per-resident outbound-channel language | Accepted |
| [0062](0062-admin-surface-split.md) | The /admin surface split: overview dashboard + linkable section pages | Accepted |
| [0063](0063-events-calendar-view.md) | The events calendar (`EV-CAL`): a month-anchored, display-only overview of visible events | Accepted |
| [0064](0064-events-calendar-dwm-views.md) | The events calendar Day/Week/Month views (`EV-DWM`): the three named views over the same authorized events | Accepted |
| [0065](0065-events-feed-your-upcoming-events.md) | The `/events` feed's \"Your upcoming events\" section (`EV-MINE`): the signed-in viewer's own events above the feed | Accepted |
| [0066](0066-event-calendar-color-lane.md) | Event calendar color: an author-picked chip/block background | Accepted |
| [0067](0067-m5-projects-todos-and-kanban.md) | M5: Projects (to-dos + Kanban boards) — a new `Kumunita.Core.Projects` context reusing the frozen authorization / audience / renderer / editor / standing seams | Accepted |
| [0068](0068-lane-action-menu-and-add-to-do-lane.md) | Lane action menu: rename / limit / status / move + add-to-do foot | Accepted |
| [0069](0069-kanban-drag-dnd-fixed-statuses-and-add-lane.md) | Kanban board: drag-and-drop moves, fixed statuses, add-lane | Accepted |
| [0070](0070-board-edit-lane-and-board-polish.md) | Board edit lane (title + description) + board polish | Accepted |
| [0071](0071-lane-edit-arrow-autosubmit-and-menu-dividers.md) | Kanban lane / card editing: modals, autosubmit status, menu dividers | Accepted |
| [0072](0072-kanban-placeholder-attributes-localized.md) | Kanban board: the placeholder attributes joined the localized surface | Accepted |
| [0073](0073-todo-group-community-claim-lane.md) | To-do group / community assignment and claim lane (self-claim of unassigned work) | Accepted |
| [0074](0074-kanban-card-assign-to-person-group-community.md) | Kanban card \"Assign to…\": person, group, or community | Accepted |
| [0075](0075-client-side-image-editing-crop-and-resize.md) | Client-side image editing (crop + resize) for avatars and inline images | Accepted |
| [0076](0076-notifications-inbox-and-recipient-email.md) | M6: Notifications (inbox + recipient email) — a new `Kumunita.Core.Notifications` context reusing the frozen durable-email / translation / personal-read seams | Accepted |
| [0077](0077-admin-account-signup-verify-notification.md) | Admin account notifications: notify every GlobalAdmin when a resident signs up or verifies (a new notify gate + two admin-lane kinds on the frozen M6 emitter) | Accepted |
| [0078](0078-sample-account-notification-suppression-in-production.md) | Sample-account notification suppression in production | Accepted |
| [0079](0079-todo-optional-start-and-due-dates.md) | To-do optional start and due dates | Accepted |
| [0080](0080-settings-surface-split.md) | The /settings surface split: linkable section pages | Accepted |
| [0081](0081-events-calendar-week-default-and-quick-create.md) | Events calendar: week as the default view + quick-create from empty space + month single-line grid | Accepted |
| [0082](0082-content-actions-dropdown.md) | Content actions dropdown (announcements, posts, pages, replies) | Accepted |
| [0083](0083-group-add-and-invite-notifications.md) | Group membership notifications: notify the resident when they are added to a group or invited to join one (kinds `group.added` / `group.invite`), and the tabbed group detail page | Accepted |
| [0084](0084-per-target-notification-subscriptions.md) | Per-target notification subscriptions: opt-in/opt-out per (recipient, kind, target) lane on the frozen M6 emitter, a settings-page list, and the per-page subscribe toggle | Accepted |
| [0085](0085-notification-item-link-and-reply-highlight.md) | Notification item link + reply highlight: a same-origin `LinkPath` on the `Notification` doc (inbox renders it clickable; the email carries it **absolute**, the `VerificationOptions.BaseUrl` + relative-path precedent), and a `#reply-{id}` anchor + scroll-and-highlight on the post detail | Accepted |
| [0086](0086-goals-projects-lane.md) | Goals & Projects lane (`PL`): the higher-level goals + projects page on top of M5 | Accepted |
| [0087](0087-todo-dependency-blocked-by-lane.md) | Todo dependency lane (`TBD`): one `BlockedByTodoId?` on `TodoItem` — the \"waiting on\" pointer (a display chip + a feed filter + one write lane; **never a gate**, never a graph) | Accepted |
| [0088](0088-projects-todos-boards-translations.md) | Projects / to-dos / Kanban-board user-added translations (the ADR 0022/0029/0059 lane carried to the M5/PL surface) | Accepted |
| [0089](0089-group-events-membership-lane.md) | Group events: a membership-scoped group events lane | Accepted |
| [0090](0090-pagination-and-filtering.md) | Pagination & filtering: the one canonical paging contract (M7) | Accepted |
| [0091](0091-search.md) | M8 — Search: one `/search` surface over the frozen seams | Accepted |
| [0092](0092-borderless-kebab-dropdown-triggers.md) | Borderless ⋮ dropdown triggers (all views) | Accepted |
| [0093](0093-group-delete-lane.md) | Group delete lane (owner ∪ GlobalAdmin) | Accepted |
| [0094](0094-group-join-request-lane.md) | Resident join-request lane for public groups (self-initiated, the reverse of the m2b invitation lane) | Accepted |
| [0095](0095-group-invite-accept-decline-links.md) | Group-invite accept / decline links (the group-invite notification is now actionable) | Accepted |
| [0096](0096-notification-read-unread-per-row-toggle.md) | Per-row read/unread toggle on the Notifications inbox | Accepted |
| [0097](0097-board-lane-delete-lane.md) | Board lane-delete lane (creator ∪ GlobalAdmin) | Accepted |
| [0098](0098-board-audience-edit-lane.md) | Board audience-edit lane (creator ∪ GlobalAdmin) | Accepted |
| [0099](0099-todo-inherits-board-audience.md) | A to-do created on the board inherits the board's audience | Accepted |
| [0100](0100-todo-comments-lane.md) | Comments + replies on a to-do | Accepted |
| [0101](0101-announcement-comments-lane.md) | Signed-in resident comments on announcements | Accepted |
| [0102](0102-community-visible-exclusivity-and-all-communities.md) | \"Everyone in this community\" is exclusive + the \"all communities\" scope ⇒ all residents | Accepted |
| [0103](0103-navbar-notification-open-lane.md) | Navbar notification open lane: dropdown deep-link → auto mark-read + highlight | Accepted |
| [0104](0104-full-page-and-calendar-fullscreen-modes.md) | Full-page expand mode (board + calendar) and calendar full-screen | Accepted |
| [0105](0105-messaging.md) | M9: 1:1 resident messaging (a new `Kumunita.Core.Messaging` context — participant-by-id access, plain-text capped immutable messages, the ADR 0101 admin-toggle shape with the floor inverted to OFF, the M6 `message.new` nudge, zero new authorization surface) | Accepted |
| [0106](0106-todo-self-assign-lane-and-board-default-audience.md) | To-do self-assign lane (\"Assign to me\") and new-board \"Everyone\" default | Accepted |
| [0107](0107-pwa-and-responsive-design.md) | M10: PWA and responsive design (substrate-only PWA — an honest static manifest + the two new icon PNGs + one quiet localized install affordance; a GET-only closed-allowlist service worker that never caches signed-in content; one pinned breakpoint rule + a closed, split phone-width surface pass + two pinned a11y floors; zero Core change, zero new authorization surface, zero new audit verb, no `LocaleSettings` toggle) | Accepted |
| [0108](0108-portability-import-export.md) | M11: Portability (import/export) — whole-instance export + a fail-closed restore of a versioned `*.kumunita` archive (a new `Kumunita.Core/Portability/` operator context — the closed content inventory read from the `*DocTypes` surfaces, the no-secret identity graph (never the credential material), the content-addressed media, the instance identity + localizations via `config.json`; validate-then-apply fail-closed atomic import; the GlobalAdmin admin plane with one `AccessAudit` row per action; zero new authorization surface) | Accepted |
| [0109](0109-events-past-lane.md) | Events \"Past\" lane (the /events past-events option) | Accepted |
| [0110](0110-platform-versioning-and-whats-new.md) | Platform versioning + the \"What's new\" lane (the `VN` lane) | Accepted |
| [0111](0111-nav-layout-variants-b-and-c.md) | Navigation layout variants (the `kumunita.nav` preference, B default / C opt-in) | Accepted |
| [0112](0112-ical-calendar-export.md) | iCal (calendar export of visible events) | Accepted |
| [0113](0113-nav-row-overflow-fold.md) | the variant-B nav-row overflow fold (Community + Groups into the More menu) | Accepted |
| [0114](0114-logging-and-usage-analytics.md) | M13: Logging and usage analytics | Accepted |
| [0115](0115-integration-of-events-and-projects.md) | Integration of Events and Projects (M14) | Accepted |
| [0116](0116-translation-bulk.md) | M15: Translation bulk (import/export, review & extend the platform's translations as a batch) | Accepted |
| [0117](0117-inventory.md) | M16: Inventory (check-out / check-in + usage history) | Accepted |
| [0118](0118-bookmarks.md) | M17: Bookmarks (save posts, events, todos, etc. for quick | Accepted |
| [0119](0119-recurring-events.md) | M18: Recurring events (repeating events over the M4 events surface) | Accepted |
| [0120](0120-guest-accounts.md) | Guest accounts (limited-privilege, time-bounded accounts on the M1 identity surface) | Accepted |
| [0121](0121-notification-quiet-times.md) | Notification quiet times (per-resident quiet schedules on the M6 notification lane) | Accepted |
| [0122](0122-document-management.md) | Document management (a shared, per-access-controlled repository of official documents, served as downloads) | Accepted |
| [0123](0123-extended-profiles.md) | Extended user profiles (a resident biography + free author-set tags + a find-people read) | Accepted |
| [0124](0124-search-expansion.md) | M8 expansion — Search: six extended surfaces + tag-name match | Accepted |
| [0125](0125-document-edit-lane-owner-only.md) | Document edit lane: owner-only (change who can access it, or replace the file) | Accepted |
| [0126](0126-attachment-inline-preview.md) | Attachment inline preview: the safe-renderable subset renders in the browser | Accepted |
| [0127](0127-guide-set-covers-every-navbar-surface.md) | Every navbar surface has a resident guide: the guide set is expanded to cover announcements, directory, people, inventory, bookmarks, documents, pages, and tags (one guide per remaining navbar item); the `help` page links all of them (the `UG` lane continues) | Accepted |
| [0128](0128-admin-help-bulk-reset-and-update-surfacing.md) | Admin bulk help-page reset and \"newer shipped text\" surfacing | Accepted |
| [0129](0129-sample-data-embedded-json-document.md) | Development sample data moved to an embedded JSON document | Accepted |
| [0130](0130-sample-corpus-warm-boot-backfill.md) | Sample corpus: whole-corpus create-if-missing backfill on warm boot | Accepted |
| [0131](0131-smtp-transport-mailkit-swap.md) | SMTP transport: MailKit in place of the BCL SmtpClient | Accepted |
| [0132](0132-onboarding.md) | Onboarding (a guided, non-blocking walk-through of a new resident's account setup, with a completion record + a dismissible banner) | Accepted |
| [0133](0133-dark-theme-forest.md) | Dark theme (\"Forest\") — an OS-following default with an explicit per-resident override, resolved entirely in CSS (no JS, no first-paint flash, CSP-safe) | Accepted |
| [0134](0134-storage-metrics.md) | Storage metrics (M24) — a read-only, `GlobalAdmin`-gated admin view of the media byte store: total used space, available space, user-content used space, and space used per user | Accepted |
| [0135](0135-upload-limits.md) | Upload limits (M25): admin-set per-file limit + per-user content quota over the content-addressed store; `Media__MaxBytes` demoted to the fallback; a pure-Core decision + a single Web gate adopted by every upload lane; a resident self-usage view | Accepted |
| [0136](0136-platform-storage-limit.md) | Platform storage limit: an operator env knob (`Media__MaxPlatformBytes`) capping the total resident content the platform may hold; `/admin/storage`'s \"available\" capped to the remaining budget; all new uploads blocked once the budget is spent or the volume's free space drops below a fixed 100 MiB floor | Accepted |
| [0137](0137-documents-organization.md) | Documents organization: `TagIds` + `FolderId` on `Document` (tags reuse the TG tag vocabulary, folders a Page-style `DocumentFolder` ParentId forest — a display/organizational facet, never a gate) | Accepted |
| [0138](0138-sample-data-change-password-lock.md) | Sample-data change-password lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of changing their own password — except the sample `GlobalAdmin` — plus the resident self-serve change-password surface it guards | Accepted |
| [0139](0139-per-resident-messaging-controls.md) | Per-resident messaging controls (opt-in + guardian's ceiling on the M9 lane) | Accepted |
| [0140](0140-guardian-community-block-and-hide.md) | Guardian community block-and-hide (the per-child ceiling on the ADR 0028 surface) | Accepted |
| [0141](0141-guardian-community-approval-lane.md) | GU community-approval lane: guardian approves / rejects a supervised child's community membership + group invitation | Accepted |
| [0142](0142-delete-account.md) | Delete account: a resident-leave / admin-removal lane that pseudonymizes the audit trail, removes memberships and the profile, and deletes the Identity account — with the last-GlobalAdmin lockout pin and the self-serve GlobalAdmin gate | Accepted |
| [0143](0143-guardian-delete-child.md) | Guardian deletes a child account: the 6th GU supervisory action that reuses the ADR 0142 deletion core, gated by an active `GuardianLink` and audited `Via: Guardian` | Accepted |
| [0144](0144-guardian-event-rsvp-gate.md) | Guardian's event-attendance gate: the consent lane's missing promise made functional | Accepted |
| [0145](0145-sorting.md) | Sorting: the one canonical sort contract (M26) | Accepted |
| [0146](0146-child-account-child-owns-password.md) | Child accounts: the child owns the password (the guardian never sets it; the child sets it at the confirmation link) | Accepted |
| [0147](0147-todos-and-boards-tags.md) | Tags on to-dos and Kanban boards | Accepted |
| [0148](0148-user-scoped-portability.md) | User-scoped portability (resident self-export + per-entity conflict-resolution import) | Accepted |
| [0149](0149-home-intro-hide-preference.md) | Home intro sections: a per-resident \"hide and show me the feed\" preference | Accepted |
| [0150](0150-site-content-customization.md) | Site content customization (the two landing surfaces' hero text is admin-editable + the sections are show/hide) | Accepted |
| [0151](0151-guardian-time-limits.md) | Guardian time limits (a per-child allow/block schedule that gates the child's platform access) | Accepted |
| [0152](0152-admin-surface-labels.md) | Admin surface labels (a GlobalAdmin edits the display name of each top-navigation item and the related page header follows the choice) | Accepted |
| [0153](0153-admin-onboarding.md) | Admin onboarding (a guided walk-through for a new GlobalAdmin through the seven most important initial settings) | Accepted |
| [0154](0154-production-error-handling.md) | Production error handling (a report-an-issue affordance on the 500 error page + a GlobalAdmin triage surface) | Accepted |
| [0155](0155-issue-submission-escalation.md) | Issue submission & escalation (a general issue-submission lane + a GlobalAdmin resolution + escalation surface on the M31 ErrorReports surface) | Accepted |
| [0156](0156-storage-metrics-history.md) | Storage metrics history (M33) — the M24 `/admin/storage` admin surface gains a trend view over time so an operator can estimate whether the instance's capacity is sufficient in the future | Accepted |
| [0157](0157-site-content-translation.md) | Site content hero-translation (the SITE-2 translation lane ADR 0150 §D5 named) | Accepted |
| [0158](0158-surface-label-translations.md) | Surface label translations (the LBL-2 translation lane ADR 0152 §D8 named) | Accepted |
| [0159](0159-escalation-authorization.md) | Escalation authorization + receiving inbox (a token-gated acceptance lane + the receiving inbox + the outbound config page on the M32 ErrorReports surface) | Accepted |
| [0160](0160-nested-help-guides-tabbed-surface-subpages.md) | Nested help guides: each tabbed surface's sub-pages become child guides under their top-level guide (the UG guide tree gains a second level) | Accepted |
| [0161](0161-page-translation-standing-author-branch.md) | Page-translation standing: the editor of a page can also translate it | Accepted |
| [0162](0162-analytics-history.md) | Analytics history (M34) — the M13 `/admin/analytics` admin surface gains a trend view over time so an operator can see usage patterns evolve | Accepted |
| [0163](0163-home-upcoming-events-and-todos.md) | Home upcoming events & to-dos — a resident can opt in to seeing the next events + to-dos on the home page, above the \"What's new\" feed | Accepted |
| [0164](0164-kiosk-mode.md) | Kiosk mode (M35) — a full-page / full-screen display that automatically cycles a GlobalAdmin-set playlist of the community's own surfaces (boards, calendar, to-dos), changing view on a set interval and refreshing each view's content on a separate, faster interval | Accepted |
| [0165](0165-mentions.md) | M36: @mentions — a cross-surface `@<resident>` reference token: type a resident's directory display name after an `@`, the platform resolves it against the frozen M2 directory and (a) notifies the mentioned resident on that surface (a new mention kind per surface, the ADR 0076 D2 \"follow-on lane\") and (b) renders the name as a link to the resident's directory page; additive on the frozen surfaces, no new bounded context | Accepted |
| [0166](0166-onboarding-walk-through-modal.md) | Onboarding walk-through: each onboarding step opens its setting in a modal (a same-origin iframe of the surface's own route) with Previous / Next navigation + a \"Step X of Y\" counter, so the resident / admin onboarding pages are a clean linear process | Accepted |
| [0167](0167-personal-notes.md) | M37: Personal notes (a private note a resident attaches to | Accepted |
| [0168](0168-bookmark-folders.md) | Bookmarks: a personal folder tree (the M17 §deferred \"bookmark | Accepted |
| [0169](0169-first-day-of-week.md) | First day of week: a per-resident display preference | Accepted |
| [0170](0170-site-content-save-upsert-on-missing.md) | Site content singleton save: upsert-on-missing | Accepted |
| [0171](0171-least-privilege-db-role.md) | Least-privilege DB role: the two-role split (migrations under a separate owner role; app role demoted to DML) | Accepted |
| [0172](0172-password-hash-posture-pbkdf2-identity-default.md) | Password-hash posture: PBKDF2 (Identity default), no custom scheme | Accepted |
| [0173](0173-report-targets-lane.md) | Report targets: the \"Report this…\" lane for events, projects, | Accepted |
| [0174](0174-mfa-qr-code-and-security-tab.md) | MFA enrollment: QR code pairing + first-class Security tab | Accepted |
| [0175](0175-palettes-and-feed-card.md) | Palettes (Forest / Ember) and the refreshed feed card | Proposed |
| [0176](0176-notice-strip.md) | One notice strip instead of stacked banners | Proposed |
| [0177](0177-groups-tile-and-empty-state.md) | Groups tile and friendly empty state | Proposed |
| [0178](0178-home-restyle.md) | Home restyle (no structural change) | Proposed |
| [0179](0179-event-card.md) | Event card with date tile | Proposed |
| [0180](0180-event-detail.md) | Event detail: rich header | Proposed |
| [0181](0181-calendar.md) | Calendar tab restyle | Proposed |
| [0182](0182-form-look.md) | One calm look for card-per-field-group forms | Proposed |
| [0183](0183-announcements.md) | Announcements as cards | Proposed |
| [0184](0184-announcement-detail.md) | Announcement detail | Proposed |
| [0185](0185-post-detail.md) | Community post detail | Proposed |
| [0186](0186-group-pages.md) | Group detail and group posts | Proposed |
| [0187](0187-group-events.md) | Group event detail and RSVP badges | Proposed |
| [0188](0188-projects-lists.md) | Projects landing, goal / project detail, shared list rows | Proposed |
| [0189](0189-todos.md) | To-dos: status chip, assignee, filter bar | Proposed |
| [0190](0190-kanban-boards.md) | Kanban boards | Proposed |
| [0191](0191-inventory.md) | Inventory | Proposed |
| [0192](0192-documents.md) | Documents | Proposed |
| [0193](0193-portability-import-modes.md) | Portability import modes: a \"keep existing\" choice | Accepted |
| [0194](0194-sample-data-mfa-lock.md) | Sample-data MFA (two-factor) lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of the MFA settings surface — except the sample `GlobalAdmin` — plus the login leg 1.5b forced-enrollment exemption that keeps the sample admin sign-in-able | Accepted |
| [0195](0195-kanban-card-edit-modal.md) | Kanban board card edit modal: clicking a card (or choosing **Edit** in its ⋮ menu) opens a modal that edits the to-do in place instead of navigating to the detail page | Accepted |
| [0196](0196-pages.md) | Pages | Proposed |
| [0197](0197-profile-directory.md) | Profile and Directory | Proposed |
| [0198](0198-account-pages.md) | Account pages | Proposed |
| [0199](0199-admin-shared-look.md) | Admin: shared look | Proposed |
| [0200](0200-projects-forms-board-header.md) | Projects forms and board header | Proposed |
| [0201](0201-verification-round-3.md) | Verification round 3 fixes | Proposed |
| [0202](0202-documents-verification.md) | Documents verification and detail fixes | Accepted |
| [0203](0203-signed-out-auth-verification.md) | Signed-out auth verification | Accepted |
| [0204](0204-profile-preview-avatar.md) | Profile preview avatar monogram | Accepted |
| [0205](0205-admin-table-scroll-shadows.md) | Admin table scroll shadows | Accepted |
| [0206](0206-board-header-chip-wrap.md) | Board header chips wrap | Accepted |
| [0207](0207-detail-page-h1-titles.md) | Detail page titles are `<h1>` | Accepted |
| [0208](0208-contrast-pass.md) | Contrast pass (reduced-brightness check) | Accepted |
| [0209](0209-focus-rings.md) | Focus rings: the two `outline: 0/none` rules | Accepted |
| [0210](0210-hover-and-active-states.md) | Hover and active states pass | Accepted |
| [0211](0211-contrast-pass-remaining-pages.md) | Contrast pass, remaining pages | Accepted |
| [0212](0212-quick-fixes.md) | Quick fixes: multi-day event time, member count, inventory date, community hint | Accepted |
| [0213](0213-event-form-sections.md) | Event form in headed sections | Accepted |
| [0214](0214-login-mfa-fixes.md) | Login and MFA challenge fixes | Accepted |
| [0215](0215-mfa-challenge-translations.md) | MFA challenge page translations | Accepted |
| [0216](0216-pages-tree-phone.md) | Pages tree and page detail at phone width | Accepted |
| [0217](0217-admin-appearance-check.md) | Admin Appearance form: end-to-end check and duplicate flash | Accepted |
| [0218](0218-hover-states.md) | Hover states: icon rail tooltips, folded nav, pagination | Accepted |
| [0219](0219-numbered-pages-narrow-topbar.md) | Numbered pager pages; the variant-B top row at 576–767px | Accepted |
| [0220](0220-nav-icon-tooltips.md) | Tooltips for the icon-only top-row links | Accepted |
| [0221](0221-non-text-contrast.md) | Non-text contrast pass (WCAG 1.4.11) | Accepted |
| [0222](0222-accounts-table-phones.md) | Admin Accounts table on phones | Accepted |
| [0223](0223-error-flash-persists.md) | Error flashes stay until dismissed | Accepted |
| [0224](0224-headed-form-sections.md) | Headed section cards on the other forms | Accepted |
| [0225](0225-palette-choice-cards.md) | Palette choice cards in the admin Appearance form | Accepted |
| [0226](0226-feed-reply-count.md) | Feed card footer with a reply count | Accepted |
| [0227](0227-feed-card-bookmark.md) | Bookmark toggle in the feed card footer | Accepted |
| [0228](0228-housekeeping-light-dark-detail-rename-doc-types.md) | Housekeeping: light-dark() palette tokens, `kmb-detail-*` rename, notice-strip anchoring, Documents allow-list | Accepted |
| [0229](0229-headed-form-sections-2.md) | Headed form sections, second batch | Accepted |
| [0230](0230-headed-form-sections-3.md) | Headed form sections, third batch (Document, Goal, Board, Profile) | Accepted |
| [0231](0231-phone-audit-admin-account.md) | Phone audit of the admin and account pages (tab strips, Analytics overflow, Platform table, headings) | Accepted |
| [0232](0232-verification-gaps-faq-mfa-upload.md) | Verification gaps: FAQ accordion in dark mode, MFA challenge, recovery code, lockout, PPTX/image upload | Accepted |
| [0233](0233-upload-guard-reports-in-form.md) | A rejected document upload re-renders the form with an error | Accepted |
| [0234](0234-sign-in-landing-page.md) | Where a sign-in lands: Home, and MFA settings after a recovery code | Accepted |
| [0235](0235-site-css-light-dark-tokens.md) | site.css colour tokens collapse into light-dark() (second half of the light-dark cleanup) | Accepted |
| [0236](0236-site-css-component-patches-light-dark.md) | site.css component dark patches collapse into light-dark() (step 2 of the light-dark cleanup) | Accepted |
| [0237](0237-avatar-upload-guard-reports-in-form.md) | A rejected avatar upload re-renders Profile/Edit with a localized error | Accepted |
| [0238](0238-signed-in-signup-verify-bounce-home.md) | The already-signed-in bounce on sign-up and verify goes Home | Accepted |
| [0239](0239-document-uploader-attribution-and-delete-lane.md) | Document uploader attribution + the owner-only delete lane | Accepted |
| [0240](0240-document-delete-confirm-and-modified-by-row.md) | Document delete asks for confirmation; \"Last modified by\" shown only when it adds a name | Accepted |
| [0241](0241-document-restore-lane.md) | Documents can be restored: Undo in the delete toast and a Deleted documents list | Accepted |
| [0242](0242-document-delete-confirm-text-mentions-restore.md) | The document delete confirmation points to Deleted documents | Accepted |
| [0243](0243-audit-table-one-line-cells-on-small-screens.md) | The admin Audit table keeps cells on one line on small screens | Accepted |
| [0244](0244-section-cards-todo-page-kiosk-forms.md) | Section cards on the last flat forms: To-do New/Edit, Page New/Edit, Kiosk settings | Accepted |
| [0245](0245-document-list-meta-wrap-on-phones.md) | The document list meta row keeps each item whole on phones | Accepted |
| [0246](0246-events-week-grid-scrolls-inside-its-box.md) | The events week grid scrolls inside its own box on narrow screens | Accepted |
| [0247](0247-kanban-board-contains-its-hidden-labels.md) | The Kanban board contains its screen-reader labels, so the page stops scrolling sideways | Accepted |
| [0248](0248-announcement-card-comment-count.md) | Announcement cards show a comment count | Accepted |
| [0249](0249-languages-and-ui-strings-tables-fit-narrow-screens.md) | The Languages and UI-strings tables fit narrow screens | Accepted |
| [0250](0250-group-form-back-links.md) | Group forms use the same back link as Post edit | Accepted |
| [0251](0251-post-edit-new-localized-strings.md) | Post edit and Post new: hard-coded English moves to translation keys | Accepted |
| [0252](0252-guardian-admin-back-links.md) | Guardian and admin pages use the accent back link | Accepted |
| [0253](0253-new-todo-goal-project-everyone-default.md) | New to-do, goal and project forms open with \"Everyone\" on | Accepted |
| [0254](0254-page-form-localized.md) | The Page form is localized | Accepted |
| [0255](0255-tag-hint-noun-free.md) | The tag input hint no longer names the item | Accepted |
| [0256](0256-comment-avatars-initials.md) | Comment and reply avatars show the resident's initial | Accepted |
| [0257](0257-groups-pages-localized.md) | The Groups pages are localized | Accepted |
| [0258](0258-shared-close-and-tooltips.md) | Alert/modal Close labels and the \"only you can see this\" tooltips are localized | Accepted |
| [0259](0259-kiosk-settings-signup-localized.md) | Kiosk Settings and Account/Signup are localized | Accepted |
| [0260](0260-guardian-rsvp-rows.md) | Guardian attendance rows show the real RSVP status, localized | Accepted |
| [0261](0261-shared-flash-extension.md) | A shared FlashAsync extension; Guardian, Announcement and Event flashes localized | Accepted |
| [0262](0262-posts-flashes-localized.md) | Posts controller flashes are localized | Accepted |
| [0263](0263-page-flashes-localized.md) | Page controller flashes are localized | Accepted |
| [0264](0264-community-flashes-localized.md) | Community controller flashes are localized | Accepted |
| [0265](0265-projects-flashes-localized.md) | Projects controller flashes are localized | Accepted |
| [0266](0266-groups-flashes-localized.md) | Groups controller flashes are localized | Accepted |
| [0267](0267-admin-flashes-localized.md) | Admin controller flashes are localized | Accepted |
| [0268](0268-remaining-controller-flashes.md) | Remaining resident-facing controller flashes are localized | Accepted |
| [0269](0269-modelstate-messages-localized.md) | Controller ModelState validation messages are localized | Accepted |
| [0270](0270-badrequest-bodies-localized.md) | BadRequest bodies are localized; Core exception text stays English | Accepted |
| [0271](0271-admin-hub-views-localized.md) | Admin hub views are localized (Admin/*) | Accepted |
| [0272](0272-admin-site-and-settings-views-localized.md) | AdminSite and the other admin settings views are localized | Accepted |
| [0273](0273-security-settings-leftovers-and-page-titles-localized.md) | Security settings views, the last leftovers and page titles are localized | Accepted |
| [0274](0274-portability-import-tag-merge.md) | Portability import (KeepExisting): merge tags by slug + default-language name | Accepted |
| [0275](0275-portability-import-principal-merge.md) | Portability import (KeepExisting): merge principals by email, keep the existing profile | Accepted |
| [0276](0276-portability-import-locale-restore-and-escalation-config.md) | Portability import: restore the archive's locale settings, and carry the escalation outbound config (endpoint + enabled only) | Accepted |
| [0277](0277-directory-sort-and-filter.md) | Directory: name search, verified-only filter, and name sort | Accepted |
| [0278](0278-group-messaging-n1-lane.md) | Group messaging: multi-participant chat threads (the N:1 generalization of M9) | Accepted |
| [0279](0279-hand-drawn-icon-sprite.md) | Hand-drawn nav and account-menu icons from one sprite | Accepted |
| [0280](0280-change-password-into-security-tab.md) | The resident's self-serve change-password surface moves off its own settings tab onto the Security tab (co-located with MFA — one tab holds both credential-security options) | Accepted |
| [0281](0281-session-doc-and-per-session-revocation.md) | Per-session model + per-session revocation (the `sessions` named lane): a `SessionDoc` in a new `Kumunita.Core.Sessions` bounded context, a `SessionService` 5-method surface, the `kumunita.sid` cookie item, the `SessionRevocationMiddleware`, and the `SecuritySettingsController` extension — on top of F5's whole-account stamp rotation (the two mechanisms coexist and are independent) | Accepted |
| [0282](0282-community-feed-wider.md) | Community feed: widen the feed column, keep the communities rail on the left | Accepted |
| [0283](0283-tab-and-switch-icons.md) | Icons on tab strips and button-group switches | Accepted |
| [0284](0284-home-whats-new-feed-for-anonymous.md) | Home \"What's new\" feed for anonymous visitors | Accepted |
| [0285](0285-kiosk-playlist-any-item.md) | Kiosk playlist: any item, per-entry duration, scroll and comments | Accepted |
| [0286](0286-iuserservice-access-core-split.md) | IUserInfoService back to the access core: four sibling interfaces for groups, community, guardian, and profile settings | Accepted |
| [0287](0287-projects-module-split.md) | Projects module split: IProjectService (projects + goals), IBoardService (boards, lanes, placements), ITodoService (todos, comments, claim/assign) | Accepted |
| [0288](0288-one-translation-variant-lane.md) | One shared translation-variant lane: the Core contract plus the Web action helper that every translatable surface uses | Accepted |
| [0289](0289-data-as-embedded-resources.md) | Data as embedded resources: translation keys, guide pages, and the sample corpus are embedded resources, not C# literals | Accepted |
| [0290](0290-improve-code-2-baseline-raises-and-seams.md) | IMPROVE-CODE-2 C2-00: the 30 baseline raises — 4 named seams (pending split) + 26 intended-coupling edges (C2-03 to extract where accidental) | Proposed |
| [0291](0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md) | IMPROVE-CODE-2 C2-03: the 9 remaining Web→concrete-*Service crossings — 7 static-utility services (C2-06) + NotificationService (Wolverine binding) + PostService (C2-05 god-surface) | Proposed |
| [0292](0292-improve-code-3-c3-02-sampledataseeder-decomposition-raise.md) | IMPROVE-CODE-3 C3-02: the SampleDataSeeder per-entity decomposition — the 2 god-method retires + the 1 files-entry honest raise (1633 → 2060) | Proposed |
| [0293](0293-improve-code-3-c3-03-god-service-seam-boundary-edge-raises.md) | IMPROVE-CODE-3 C3-03: the god-service seam extractions — the honest boundary-edge count raises (the new named-seam file lands on an already-existing module edge) + the `GroupsController` seam-injection collateral | Proposed |
| [0294](0294-improve-code-3-c3-04-partial-class-splits-boundary-raises.md) | IMPROVE-CODE-3 C3-04: the still-large-file tail — 15 partial-class splits (files 23→8) + 31 boundary-edge raises (partial files land on already-existing module edges) | Proposed |
| [0295](0295-improve-code-3-c3-05-marten-allowlist-clean-retirements.md) | IMPROVE-CODE-3 C3-05: the two clean Marten-allowlist retirements (Goals, Account) + the `IIdentityService` interface raise (33→34, the `LoadVerifyTokenAsync` seam) + 5 honest keeps (Announcement, Document, Tag, Posts, Guardian) | Proposed |
| [0296](0296-improve-code-3-c3-06-marten-allowlist-remainder-honest-finding.md) | IMPROVE-CODE-3 C3-06: the Marten-allowlist remainder — the 1 clean retirement (ReportTargets, the `FileReportTargetAsync` self-session seam) + the honest-17 floor (the DoD #6 ≤5 line not met; the remainder is the no-service / edge-minting / diagnostics / C08-frozen class) | Proposed |
| [0297](0297-improve-code-3-c3-07-flagged-accidental-boundary-edges.md) | IMPROVE-CODE-3 C3-07: the 3 flagged-accidental boundary edges — 2 contract extractions (`IInstanceBaseUrlReader`, `IIdentityPrincipalStore`) + the 4 name-collision retires (the Web `TagSlugs`→`TagSlugParser` rename) → boundary_violations 465→459 | Proposed |
| [0298](0298-data-classes-in-code.md) | Data classes in code — the `[DataClass]` attribute + `DataClassCatalog` (one policy row per registered doc type; `Keep` → reason, `Pseudonymize` → principal field) | Accepted |
| [0299](0299-instance-export-operator-gated.md) | The instance export is operator-gated — content-free by default; full only under an active AdminOverride, audited Via: BreakGlass, with resident transparency (D1/D10) | Accepted |
| [0300](0300-issue-resolved-notification.md) | The resolved platform-issue loop closes — the `issue.resolved` kind notifies the signed-in filer; the anonymous-filer mailing path is a held SEC decision | Accepted |
| [0301](0301-self-service-deletion-grace-period.md) | Self-service account deletion for every resident — the ADR 0142 D5 GlobalAdmin gate is removed; the self-serve branch schedules with a grace period (proposed 14 days, `// TODO ❓`) cancelled by signing in; a child under an active `GuardianLink` cannot self-delete (admin path included, `// TODO ❓`); the last-GlobalAdmin pin and every other 0142 invariant stand | Accepted |
| [0302](0302-shared-kiosk-link.md) | The shared kiosk link — a per-playlist, revocable, long-random-token read-only credential (`kskl_` + 256 bits, shown once, stored only as its SHA-256 digest) that authenticates only on the kiosk surface, shows community-visible items only, is audited under the owner, and is revoked per link (token length / expiry / max-links-per-playlist held as `// TODO ❓` for the R19 SEC review); the personal kiosk (0164/0285) stands unchanged | Accepted |
| [0303](0303-report-queued-routing.md) | A filed report reaches the moderators — the `report.queued` routing kind + the GlobalAdmin enumeration seam | Accepted |
| [0304](0304-email-invitations.md) | Email invitations: a single-use, expiring, revocable credential that opens sign-up while the gate is closed — the invitation lane's Core seam (A07), with the A11 3-state resident-invite mode + the A12 `PendingApproval` row state and admin-approve lane (D6 c settled; the D6 b rate ceiling held) | Accepted |
| [0307](0307-developer-feedback-loop.md) | The developer feedback loop — the branch flow (`development` → `release` → `main`, CI on `release`), one Postgres container per test assembly, `tools/check.ps1` as the one entry point, the generated code map, mirrored test trees, the lane-code ratchet, and `devx-check` (D1–D7) | Accepted |
