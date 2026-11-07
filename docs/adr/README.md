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
| 0001 | Core stack & identity model | Accepted |
| 0002 | Deployment topology: one instance per neighborhood | Accepted |
| 0003 | Roles & moderator scoping | Accepted |
| 0004 | Data persistence & schema evolution | Accepted |
| 0005 | Multilingual support (UI & static pages) | Accepted |
| 0006 | Module boundary contracts (Identity, UserInfo, Authorization) | Accepted |
| 0007 | Group management lane: owner ∪ GlobalAdmin for add and remove | Accepted |
| 0008 | Member self-leave lane (owner excepted) | Accepted |
| 0009 | Group description: resident-facing display + owner ∪ GlobalAdmin edit | Accepted |
| 0010 | Private groups: a membership/organizing unit, hidden from the audience pickers | Accepted |
| 0011 | Media & file storage: content-addressed local-volume bytes + a `mt` catalog | Accepted |
| 0012 | Community membership: mandatory communities + moderator-managed optional membership | Accepted |
| 0013 | Group posts: a membership-scoped group channel | Accepted |
| 0014 | Post edit lane: author-only | Accepted |
| 0015 | UI view-localization mechanics | Accepted |
| 0016 | Group-post + reply edit lane: author-only | Accepted |
| 0017 | Announcement edit lane: author-of-record ∪ GlobalAdmin (flat lane) | Accepted |
| 0018 | UGC authored-in language tag (posts, replies, announcements) | Accepted |
| 0019 | Timezone: platform default (admin) + per-resident override | Accepted |
| 0020 | Date & time format: platform default (admin) + per-resident override + custom | Accepted |
| 0021 | Translator role: delegate translation editing to non-admin residents | Accepted |
| 0022 | User-added post & reply translations (the lane ADR 0018 deferred) | Accepted |
| 0023 | Reply as a report target (extending the M3b report lane) | Accepted |
| 0024 | Author soft-delete lane (posts + replies, community + group) | Accepted |
| 0025 | Rich content: Markdown bodies + in-content images | Accepted |
| 0026 | Group & community name/description translations | Accepted |
| 0027 | Post & reply translation display and swap (authored-in as first-class variant) | Accepted |
| 0028 | Guardian controls: account-scope supervision of a child's account | Accepted |
| 0029 | 0029 — Announcement user-added translations (the ADR 0022 lane carried to announcements) | Accepted |
| 0030 | Role independence: elevated roles are composable, not mutually exclusive | Accepted |
| 0031 | WYSIWYG editor + toolbar over the RC Markdown lane | Accepted |
| 0032 | Inline editor: the rendered pane is the default view | Accepted |
| 0033 | WYSIWYG inline editing: the rendered pane is the editable surface | Accepted |
| 0034 | File attachments lane: downloads on post / reply / announcement | Accepted |
| 0035 | Group-post media: the membership lane is the serve decision | Accepted |
| 0036 | Community-visible audience: the default for new community posts | Accepted |
| 0037 | Draft mode: saved-but-not-published, author-only (community posts, group posts, announcements) | Accepted |
| 0038 | Guardian assignment: an existing guardian assigns a second guardian to a child's account | Accepted |
| 0039 | Pages: a hierarchical, audience-restricted, translatable knowledge tree (absorbs the static-page lane) | Accepted |
| 0040 | Pages: system vs user (blog) kinds, and the per-resident blog feed | Accepted |
| 0041 | Page audience scope dropdown: \"All residents\" as a first-class flag | Accepted |
| 0042 | Bundled initial language pack (de / fr ship on first boot) | Accepted |
| 0043 | System pages shipped + discoverable (SP) | Accepted |
| 0044 | Tags: free author-set subject labels for posts and blog pages (TG) | Accepted |
| 0045 | Danish (da) pre-seeded, disabled by default | Accepted |
| 0046 | Default-language fallback: browser `Accept-Language` match | Accepted |
| 0047 | Static-page localization: the hard-coded routes render in the | Accepted |
| 0048 | Edit + remove lanes for user-added translations | Accepted |
| 0049 | Default-visible variant: the viewer's current language (when a translation exists) | Accepted |
| 0050 | Admin-managed sign-up gate: open vs invitation-only | Accepted |
| 0051 | List surfaces: the default-visible variant is the viewer's current language | Accepted |
| 0052 | Warm-boot backfill of the UI-string `de` / `fr` / `da` baselines | Accepted |
| 0053 | Community translation surface on its own page | Accepted |
| 0054 | M4: Events, RSVPs & reminders (a new `Kumunita.Core.Events` context reusing the frozen authorization / audience / renderer / editor / email / §6.4-job seams) | Accepted |
| 0055 | Development sample data: a first-boot, Development-only mock neighborhood | Accepted |
| 0056 | Sample-data opt-in: a flag-gated seeder with a deploy posture | Accepted |
| 0057 | User guides: resident-facing guides live in the `help/` subtree of the existing page tree; `en` floor + curated `de`/`fr`/`da` baselines (community-owned); a three-layer consistency loop keeps them honest as code changes (the `UG` named lane) | Accepted |
| 0058 | Page \"Reset to seeded text\" lane | Accepted |
| 0059 | Event user-added translations (the ADR 0022/0029 lane carried to the M4 surface) | Accepted |
| 0060 | Warm-boot backfill of the sample events' `de` / `fr` / `da` translations | Accepted |
| 0061 | Email & notification language: per-resident outbound-channel language | Accepted |
| 0062 | The /admin surface split: overview dashboard + linkable section pages | Accepted |
| 0063 | The events calendar (`EV-CAL`): a month-anchored, display-only overview of visible events | Accepted |
| 0064 | The events calendar Day/Week/Month views (`EV-DWM`): the three named views over the same authorized events | Accepted |
| 0065 | The `/events` feed's \"Your upcoming events\" section (`EV-MINE`): the signed-in viewer's own events above the feed | Accepted |
| 0066 | Event calendar color: an author-picked chip/block background | Accepted |
| 0067 | M5: Projects (to-dos + Kanban boards) — a new `Kumunita.Core.Projects` context reusing the frozen authorization / audience / renderer / editor / standing seams | Accepted |
| 0068 | Lane action menu: rename / limit / status / move + add-to-do foot | Accepted |
| 0069 | Kanban board: drag-and-drop moves, fixed statuses, add-lane | Accepted |
| 0070 | Board edit lane (title + description) + board polish | Accepted |
| 0071 | Kanban lane / card editing: modals, autosubmit status, menu dividers | Accepted |
| 0072 | Kanban board: the placeholder attributes joined the localized surface | Accepted |
| 0073 | To-do group / community assignment and claim lane (self-claim of unassigned work) | Accepted |
| 0074 | Kanban card \"Assign to…\": person, group, or community | Accepted |
| 0075 | Client-side image editing (crop + resize) for avatars and inline images | Accepted |
| 0076 | M6: Notifications (inbox + recipient email) — a new `Kumunita.Core.Notifications` context reusing the frozen durable-email / translation / personal-read seams | Accepted |
| 0077 | Admin account notifications: notify every GlobalAdmin when a resident signs up or verifies (a new notify gate + two admin-lane kinds on the frozen M6 emitter) | Accepted |
| 0078 | Sample-account notification suppression in production | Accepted |
| 0079 | To-do optional start and due dates | Accepted |
| 0080 | The /settings surface split: linkable section pages | Accepted |
| 0081 | Events calendar: week as the default view + quick-create from empty space + month single-line grid | Accepted |
| 0082 | Content actions dropdown (announcements, posts, pages, replies) | Accepted |
| 0083 | Group membership notifications: notify the resident when they are added to a group or invited to join one (kinds `group.added` / `group.invite`), and the tabbed group detail page | Accepted |
| 0084 | Per-target notification subscriptions: opt-in/opt-out per (recipient, kind, target) lane on the frozen M6 emitter, a settings-page list, and the per-page subscribe toggle | Accepted |
| 0085 | Notification item link + reply highlight: a same-origin `LinkPath` on the `Notification` doc (inbox renders it clickable; the email carries it **absolute**, the `VerificationOptions.BaseUrl` + relative-path precedent), and a `#reply-{id}` anchor + scroll-and-highlight on the post detail | Accepted |
| 0086 | Goals & Projects lane (`PL`): the higher-level goals + projects page on top of M5 | Accepted |
| 0087 | Todo dependency lane (`TBD`): one `BlockedByTodoId?` on `TodoItem` — the \"waiting on\" pointer (a display chip + a feed filter + one write lane; **never a gate**, never a graph) | Accepted |
| 0088 | Projects / to-dos / Kanban-board user-added translations (the ADR 0022/0029/0059 lane carried to the M5/PL surface) | Accepted |
| 0089 | Group events: a membership-scoped group events lane | Accepted |
| 0090 | Pagination & filtering: the one canonical paging contract (M7) | Accepted |
| 0091 | M8 — Search: one `/search` surface over the frozen seams | Accepted |
| 0092 | Borderless ⋮ dropdown triggers (all views) | Accepted |
| 0093 | Group delete lane (owner ∪ GlobalAdmin) | Accepted |
| 0094 | Resident join-request lane for public groups (self-initiated, the reverse of the m2b invitation lane) | Accepted |
| 0095 | Group-invite accept / decline links (the group-invite notification is now actionable) | Accepted |
| 0096 | Per-row read/unread toggle on the Notifications inbox | Accepted |
| 0097 | Board lane-delete lane (creator ∪ GlobalAdmin) | Accepted |
| 0098 | Board audience-edit lane (creator ∪ GlobalAdmin) | Accepted |
| 0099 | A to-do created on the board inherits the board's audience | Accepted |
| 0100 | Comments + replies on a to-do | Accepted |
| 0101 | Signed-in resident comments on announcements | Accepted |
| 0102 | \"Everyone in this community\" is exclusive + the \"all communities\" scope ⇒ all residents | Accepted |
| 0103 | Navbar notification open lane: dropdown deep-link → auto mark-read + highlight | Accepted |
| 0104 | Full-page expand mode (board + calendar) and calendar full-screen | Accepted |
| 0105 | M9: 1:1 resident messaging (a new `Kumunita.Core.Messaging` context — participant-by-id access, plain-text capped immutable messages, the ADR 0101 admin-toggle shape with the floor inverted to OFF, the M6 `message.new` nudge, zero new authorization surface) | Accepted |
| 0106 | To-do self-assign lane (\"Assign to me\") and new-board \"Everyone\" default | Accepted |
| 0107 | M10: PWA and responsive design (substrate-only PWA — an honest static manifest + the two new icon PNGs + one quiet localized install affordance; a GET-only closed-allowlist service worker that never caches signed-in content; one pinned breakpoint rule + a closed, split phone-width surface pass + two pinned a11y floors; zero Core change, zero new authorization surface, zero new audit verb, no `LocaleSettings` toggle) | Accepted |
| 0108 | M11: Portability (import/export) — whole-instance export + a fail-closed restore of a versioned `*.kumunita` archive (a new `Kumunita.Core/Portability/` operator context — the closed content inventory read from the `*DocTypes` surfaces, the no-secret identity graph (never the credential material), the content-addressed media, the instance identity + localizations via `config.json`; validate-then-apply fail-closed atomic import; the GlobalAdmin admin plane with one `AccessAudit` row per action; zero new authorization surface) | Accepted |
| 0109 | Events \"Past\" lane (the /events past-events option) | Accepted |
| 0110 | Platform versioning + the \"What's new\" lane (the `VN` lane) | Accepted |
| 0111 | Navigation layout variants (the `kumunita.nav` preference, B default / C opt-in) | Accepted |
| 0112 | iCal (calendar export of visible events) | Accepted |
| 0113 | the variant-B nav-row overflow fold (Community + Groups into the More menu) | Accepted |
| 0114 | M13: Logging and usage analytics | Accepted |
| 0115 | Integration of Events and Projects (M14) | Accepted |
| 0116 | M15: Translation bulk (import/export, review & extend the platform's translations as a batch) | Accepted |
| 0117 | M16: Inventory (check-out / check-in + usage history) | Accepted |
| 0118 | M17: Bookmarks (save posts, events, todos, etc. for quick | Accepted |
| 0119 | M18: Recurring events (repeating events over the M4 events surface) | Accepted |
| 0120 | Guest accounts (limited-privilege, time-bounded accounts on the M1 identity surface) | Accepted |
| 0121 | Notification quiet times (per-resident quiet schedules on the M6 notification lane) | Accepted |
| 0122 | Document management (a shared, per-access-controlled repository of official documents, served as downloads) | Accepted |
| 0123 | Extended user profiles (a resident biography + free author-set tags + a find-people read) | Accepted |
| 0124 | M8 expansion — Search: six extended surfaces + tag-name match | Accepted |
| 0125 | Document edit lane: owner-only (change who can access it, or replace the file) | Accepted |
| 0126 | Attachment inline preview: the safe-renderable subset renders in the browser | Accepted |
| 0127 | Every navbar surface has a resident guide: the guide set is expanded to cover announcements, directory, people, inventory, bookmarks, documents, pages, and tags (one guide per remaining navbar item); the `help` page links all of them (the `UG` lane continues) | Accepted |
| 0128 | Admin bulk help-page reset and \"newer shipped text\" surfacing | Accepted |
| 0129 | Development sample data moved to an embedded JSON document | Accepted |
| 0130 | Sample corpus: whole-corpus create-if-missing backfill on warm boot | Accepted |
| 0131 | SMTP transport: MailKit in place of the BCL SmtpClient | Accepted |
| 0132 | Onboarding (a guided, non-blocking walk-through of a new resident's account setup, with a completion record + a dismissible banner) | Accepted |
| 0133 | Dark theme (\"Forest\") — an OS-following default with an explicit per-resident override, resolved entirely in CSS (no JS, no first-paint flash, CSP-safe) | Accepted |
| 0134 | Storage metrics (M24) — a read-only, `GlobalAdmin`-gated admin view of the media byte store: total used space, available space, user-content used space, and space used per user | Accepted |
| 0135 | Upload limits (M25): admin-set per-file limit + per-user content quota over the content-addressed store; `Media__MaxBytes` demoted to the fallback; a pure-Core decision + a single Web gate adopted by every upload lane; a resident self-usage view | Accepted |
| 0136 | Platform storage limit: an operator env knob (`Media__MaxPlatformBytes`) capping the total resident content the platform may hold; `/admin/storage`'s \"available\" capped to the remaining budget; all new uploads blocked once the budget is spent or the volume's free space drops below a fixed 100 MiB floor | Accepted |
| 0137 | Documents organization: `TagIds` + `FolderId` on `Document` (tags reuse the TG tag vocabulary, folders a Page-style `DocumentFolder` ParentId forest — a display/organizational facet, never a gate) | Accepted |
| 0138 | Sample-data change-password lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of changing their own password — except the sample `GlobalAdmin` — plus the resident self-serve change-password surface it guards | Accepted |
| 0139 | Per-resident messaging controls (opt-in + guardian's ceiling on the M9 lane) | Accepted |
| 0140 | Guardian community block-and-hide (the per-child ceiling on the ADR 0028 surface) | Accepted |
| 0141 | GU community-approval lane: guardian approves / rejects a supervised child's community membership + group invitation | Accepted |
| 0142 | Delete account: a resident-leave / admin-removal lane that pseudonymizes the audit trail, removes memberships and the profile, and deletes the Identity account — with the last-GlobalAdmin lockout pin and the self-serve GlobalAdmin gate | Accepted |
| 0143 | Guardian deletes a child account: the 6th GU supervisory action that reuses the ADR 0142 deletion core, gated by an active `GuardianLink` and audited `Via: Guardian` | Accepted |
| 0144 | Guardian's event-attendance gate: the consent lane's missing promise made functional | Accepted |
| 0145 | Sorting: the one canonical sort contract (M26) | Accepted |
| 0146 | Child accounts: the child owns the password (the guardian never sets it; the child sets it at the confirmation link) | Accepted |
| 0147 | Tags on to-dos and Kanban boards | Accepted |
| 0148 | User-scoped portability (resident self-export + per-entity conflict-resolution import) | Accepted |
| 0149 | Home intro sections: a per-resident \"hide and show me the feed\" preference | Accepted |
| 0150 | Site content customization (the two landing surfaces' hero text is admin-editable + the sections are show/hide) | Accepted |
| 0151 | Guardian time limits (a per-child allow/block schedule that gates the child's platform access) | Accepted |
| 0152 | Admin surface labels (a GlobalAdmin edits the display name of each top-navigation item and the related page header follows the choice) | Accepted |
| 0153 | Admin onboarding (a guided walk-through for a new GlobalAdmin through the seven most important initial settings) | Accepted |
| 0154 | Production error handling (a report-an-issue affordance on the 500 error page + a GlobalAdmin triage surface) | Accepted |
| 0155 | Issue submission & escalation (a general issue-submission lane + a GlobalAdmin resolution + escalation surface on the M31 ErrorReports surface) | Accepted |
| 0156 | Storage metrics history (M33) — the M24 `/admin/storage` admin surface gains a trend view over time so an operator can estimate whether the instance's capacity is sufficient in the future | Accepted |
| 0157 | Site content hero-translation (the SITE-2 translation lane ADR 0150 §D5 named) | Accepted |
| 0158 | Surface label translations (the LBL-2 translation lane ADR 0152 §D8 named) | Accepted |
| 0159 | Escalation authorization + receiving inbox (a token-gated acceptance lane + the receiving inbox + the outbound config page on the M32 ErrorReports surface) | Accepted |
| 0160 | Nested help guides: each tabbed surface's sub-pages become child guides under their top-level guide (the UG guide tree gains a second level) | Accepted |
| 0161 | Page-translation standing: the editor of a page can also translate it | Accepted |
| 0162 | Analytics history (M34) — the M13 `/admin/analytics` admin surface gains a trend view over time so an operator can see usage patterns evolve | Accepted |
| 0163 | Home upcoming events & to-dos — a resident can opt in to seeing the next events + to-dos on the home page, above the \"What's new\" feed | Accepted |
| 0164 | Kiosk mode (M35) — a full-page / full-screen display that automatically cycles a GlobalAdmin-set playlist of the community's own surfaces (boards, calendar, to-dos), changing view on a set interval and refreshing each view's content on a separate, faster interval | Accepted |
| 0165 | M36: @mentions — a cross-surface `@<resident>` reference token: type a resident's directory display name after an `@`, the platform resolves it against the frozen M2 directory and (a) notifies the mentioned resident on that surface (a new mention kind per surface, the ADR 0076 D2 \"follow-on lane\") and (b) renders the name as a link to the resident's directory page; additive on the frozen surfaces, no new bounded context | Accepted |
| 0166 | Onboarding walk-through: each onboarding step opens its setting in a modal (a same-origin iframe of the surface's own route) with Previous / Next navigation + a \"Step X of Y\" counter, so the resident / admin onboarding pages are a clean linear process | Accepted |
| 0167 | M37: Personal notes (a private note a resident attaches to | Accepted |
| 0168 | Bookmarks: a personal folder tree (the M17 §deferred \"bookmark | Accepted |
| 0169 | First day of week: a per-resident display preference | Accepted |
| 0170 | Site content singleton save: upsert-on-missing | Accepted |
| 0171 | Least-privilege DB role: the two-role split (migrations under a separate owner role; app role demoted to DML) | Accepted |
| 0172 | Password-hash posture: PBKDF2 (Identity default), no custom scheme | Accepted |
| 0173 | Report targets: the \"Report this…\" lane for events, projects, | Accepted |
| 0174 | MFA enrollment: QR code pairing + first-class Security tab | Accepted |
| 0175 | Palettes (Forest / Ember) and the refreshed feed card | Proposed |
| 0176 | One notice strip instead of stacked banners | Proposed |
| 0177 | Groups tile and friendly empty state | Proposed |
| 0178 | Home restyle (no structural change) | Proposed |
| 0179 | Event card with date tile | Proposed |
| 0180 | Event detail: rich header | Proposed |
| 0181 | Calendar tab restyle | Proposed |
| 0182 | One calm look for card-per-field-group forms | Proposed |
| 0183 | Announcements as cards | Proposed |
| 0184 | Announcement detail | Proposed |
| 0185 | Community post detail | Proposed |
| 0186 | Group detail and group posts | Proposed |
| 0187 | Group event detail and RSVP badges | Proposed |
| 0188 | Projects landing, goal / project detail, shared list rows | Proposed |
| 0189 | To-dos: status chip, assignee, filter bar | Proposed |
| 0190 | Kanban boards | Proposed |
| 0191 | Inventory | Proposed |
| 0192 | Documents | Proposed |
| 0193 | Portability import modes: a \"keep existing\" choice | Accepted |
| 0194 | Sample-data MFA (two-factor) lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of the MFA settings surface — except the sample `GlobalAdmin` — plus the login leg 1.5b forced-enrollment exemption that keeps the sample admin sign-in-able | Accepted |
| 0195 | Kanban board card edit modal: clicking a card (or choosing **Edit** in its ⋮ menu) opens a modal that edits the to-do in place instead of navigating to the detail page | Accepted |
| 0196 | Pages | Proposed |
| 0197 | Profile and Directory | Proposed |
| 0198 | Account pages | Proposed |
| 0199 | Admin: shared look | Proposed |
| 0200 | Projects forms and board header | Proposed |
| 0201 | Verification round 3 fixes | Proposed |
| 0202 | Documents verification and detail fixes | Accepted |
| 0203 | Signed-out auth verification | Accepted |
| 0204 | Profile preview avatar monogram | Accepted |
| 0205 | Admin table scroll shadows | Accepted |
| 0206 | Board header chips wrap | Accepted |
| 0207 | Detail page titles are `<h1>` | Accepted |
| 0208 | Contrast pass (reduced-brightness check) | Accepted |
| 0209 | Focus rings: the two `outline: 0/none` rules | Accepted |
| 0210 | Hover and active states pass | Accepted |
| 0211 | Contrast pass, remaining pages | Accepted |
| 0212 | Quick fixes: multi-day event time, member count, inventory date, community hint | Accepted |
| 0213 | Event form in headed sections | Accepted |
| 0214 | Login and MFA challenge fixes | Accepted |
| 0215 | MFA challenge page translations | Accepted |
| 0216 | Pages tree and page detail at phone width | Accepted |
| 0217 | Admin Appearance form: end-to-end check and duplicate flash | Accepted |
| 0218 | Hover states: icon rail tooltips, folded nav, pagination | Accepted |
| 0219 | Numbered pager pages; the variant-B top row at 576–767px | Accepted |
| 0220 | Tooltips for the icon-only top-row links | Accepted |
| 0221 | Non-text contrast pass (WCAG 1.4.11) | Accepted |
| 0222 | Admin Accounts table on phones | Accepted |
| 0223 | Error flashes stay until dismissed | Accepted |
| 0224 | Headed section cards on the other forms | Accepted |
| 0225 | Palette choice cards in the admin Appearance form | Accepted |
| 0226 | Feed card footer with a reply count | Accepted |
| 0227 | Bookmark toggle in the feed card footer | Accepted |
| 0228 | Housekeeping: light-dark() palette tokens, `kmb-detail-*` rename, notice-strip anchoring, Documents allow-list | Accepted |
| 0229 | Headed form sections, second batch | Accepted |
| 0230 | Headed form sections, third batch (Document, Goal, Board, Profile) | Accepted |
| 0231 | Phone audit of the admin and account pages (tab strips, Analytics overflow, Platform table, headings) | Accepted |
| 0232 | Verification gaps: FAQ accordion in dark mode, MFA challenge, recovery code, lockout, PPTX/image upload | Accepted |
| 0233 | A rejected document upload re-renders the form with an error | Accepted |
| 0234 | Where a sign-in lands: Home, and MFA settings after a recovery code | Accepted |
| 0235 | site.css colour tokens collapse into light-dark() (second half of the light-dark cleanup) | Accepted |
| 0236 | site.css component dark patches collapse into light-dark() (step 2 of the light-dark cleanup) | Accepted |
| 0237 | A rejected avatar upload re-renders Profile/Edit with a localized error | Accepted |
| 0238 | The already-signed-in bounce on sign-up and verify goes Home | Accepted |
| 0239 | Document uploader attribution + the owner-only delete lane | Accepted |
| 0240 | Document delete asks for confirmation; \"Last modified by\" shown only when it adds a name | Accepted |
| 0241 | Documents can be restored: Undo in the delete toast and a Deleted documents list | Accepted |
| 0242 | The document delete confirmation points to Deleted documents | Accepted |
| 0243 | The admin Audit table keeps cells on one line on small screens | Accepted |
| 0244 | Section cards on the last flat forms: To-do New/Edit, Page New/Edit, Kiosk settings | Accepted |
| 0245 | The document list meta row keeps each item whole on phones | Accepted |
| 0246 | The events week grid scrolls inside its own box on narrow screens | Accepted |
| 0247 | The Kanban board contains its screen-reader labels, so the page stops scrolling sideways | Accepted |
| 0248 | Announcement cards show a comment count | Accepted |
| 0249 | The Languages and UI-strings tables fit narrow screens | Accepted |
| 0250 | Group forms use the same back link as Post edit | Accepted |
| 0251 | Post edit and Post new: hard-coded English moves to translation keys | Accepted |
| 0252 | Guardian and admin pages use the accent back link | Accepted |
| 0253 | New to-do, goal and project forms open with \"Everyone\" on | Accepted |
| 0254 | The Page form is localized | Accepted |
| 0255 | The tag input hint no longer names the item | Accepted |
| 0256 | Comment and reply avatars show the resident's initial | Accepted |
| 0257 | The Groups pages are localized | Accepted |
| 0258 | Alert/modal Close labels and the \"only you can see this\" tooltips are localized | Accepted |
| 0259 | Kiosk Settings and Account/Signup are localized | Accepted |
| 0260 | Guardian attendance rows show the real RSVP status, localized | Accepted |
| 0261 | A shared FlashAsync extension; Guardian, Announcement and Event flashes localized | Accepted |
| 0262 | Posts controller flashes are localized | Accepted |
| 0263 | Page controller flashes are localized | Accepted |
| 0264 | Community controller flashes are localized | Accepted |
| 0265 | Projects controller flashes are localized | Accepted |
| 0266 | Groups controller flashes are localized | Accepted |
| 0267 | Admin controller flashes are localized | Accepted |
| 0268 | Remaining resident-facing controller flashes are localized | Accepted |
| 0269 | Controller ModelState validation messages are localized | Accepted |
| 0270 | BadRequest bodies are localized; Core exception text stays English | Accepted |
| 0271 | Admin hub views are localized (Admin/*) | Accepted |
| 0272 | AdminSite and the other admin settings views are localized | Accepted |
| 0273 | Security settings views, the last leftovers and page titles are localized | Accepted |
| 0274 | Portability import (KeepExisting): merge tags by slug + default-language name | Accepted |
| 0275 | Portability import (KeepExisting): merge principals by email, keep the existing profile | Accepted |
| 0276 | Portability import: restore the archive's locale settings, and carry the escalation outbound config (endpoint + enabled only) | Accepted |
| 0277 | Directory: name search, verified-only filter, and name sort | Accepted |
| 0278 | Group messaging: multi-participant chat threads (the N:1 generalization of M9) | Accepted |
| 0279 | Hand-drawn nav and account-menu icons from one sprite | Accepted |
| 0280 | The resident's self-serve change-password surface moves off its own settings tab onto the Security tab (co-located with MFA — one tab holds both credential-security options) | Accepted |
| 0281 | Per-session model + per-session revocation (the `sessions` named lane): a `SessionDoc` in a new `Kumunita.Core.Sessions` bounded context, a `SessionService` 5-method surface, the `kumunita.sid` cookie item, the `SessionRevocationMiddleware`, and the `SecuritySettingsController` extension — on top of F5's whole-account stamp rotation (the two mechanisms coexist and are independent) | Accepted |
| 0282 | Community feed: widen the feed column, keep the communities rail on the left | Accepted |
| 0283 | Icons on tab strips and button-group switches | Accepted |
| 0284 | Home \"What's new\" feed for anonymous visitors | Accepted |
| 0285 | Kiosk playlist: any item, per-entry duration, scroll and comments | Accepted |
| 0286 | IUserInfoService back to the access core: four sibling interfaces for groups, community, guardian, and profile settings | Accepted |
| 0287 | Projects module split: IProjectService (projects + goals), IBoardService (boards, lanes, placements), ITodoService (todos, comments, claim/assign) | Accepted |
| 0288 | One shared translation-variant lane: the Core contract plus the Web action helper that every translatable surface uses | Accepted |
| 0289 | Data as embedded resources: translation keys, guide pages, and the sample corpus are embedded resources, not C# literals | Accepted |
| 0290 | IMPROVE-CODE-2 C2-00: the 30 baseline raises — 4 named seams (pending split) + 26 intended-coupling edges (C2-03 to extract where accidental) | Proposed |
| 0291 | IMPROVE-CODE-2 C2-03: the 9 remaining Web→concrete-*Service crossings — 7 static-utility services (C2-06) + NotificationService (Wolverine binding) + PostService (C2-05 god-surface) | Proposed |
| 0292 | IMPROVE-CODE-3 C3-02: the SampleDataSeeder per-entity decomposition — the 2 god-method retires + the 1 files-entry honest raise (1633 → 2060) | Proposed |
| 0293 | IMPROVE-CODE-3 C3-03: the god-service seam extractions — the honest boundary-edge count raises (the new named-seam file lands on an already-existing module edge) + the `GroupsController` seam-injection collateral | Proposed |
| 0294 | IMPROVE-CODE-3 C3-04: the still-large-file tail — 15 partial-class splits (files 23→8) + 31 boundary-edge raises (partial files land on already-existing module edges) | Proposed |
| 0295 | IMPROVE-CODE-3 C3-05: the two clean Marten-allowlist retirements (Goals, Account) + the `IIdentityService` interface raise (33→34, the `LoadVerifyTokenAsync` seam) + 5 honest keeps (Announcement, Document, Tag, Posts, Guardian) | Proposed |
| 0296 | IMPROVE-CODE-3 C3-06: the Marten-allowlist remainder — the 1 clean retirement (ReportTargets, the `FileReportTargetAsync` self-session seam) + the honest-17 floor (the DoD #6 ≤5 line not met; the remainder is the no-service / edge-minting / diagnostics / C08-frozen class) | Proposed |
| 0297 | IMPROVE-CODE-3 C3-07: the 3 flagged-accidental boundary edges — 2 contract extractions (`IInstanceBaseUrlReader`, `IIdentityPrincipalStore`) + the 4 name-collision retires (the Web `TagSlugs`→`TagSlugParser` rename) → boundary_violations 465→459 | Proposed |
| 0298 | Data classes in code — the `[DataClass]` attribute + `DataClassCatalog` (one policy row per registered doc type; `Keep` → reason, `Pseudonymize` → principal field) | Accepted |
