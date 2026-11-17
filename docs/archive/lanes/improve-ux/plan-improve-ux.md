---
id: plan-improve-ux
title: "Plan: IMPROVE-UX — close the loops, per audience"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/how-it-works.md
  - docs/archive/lanes/improve/plan-improve.md
  - docs/archive/lanes/improve/improve-audit.md
  - docs/adr/0142-delete-account.md
  - docs/ops/privacy-operations.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** the UX lane of the IMPROVE round. It asks whether each
> audience — a resident (incl. guests, guardians, kiosk viewers), a community
> admin/moderator, and the platform operator — can take a need in and get an
> outcome out **without leaving the platform** (closed-loop test) and without
> copy-paste, re-keying or "ask the admin" (handoff test). IMPROVE `U09`
> traced five resident journeys on paper and found them closed; reading the
> code finds open loops it could not see: reports that never come back, a
> moderator who cannot act on reported content, an invitation-only sign-up
> with no way to invite, blank 404s, a kiosk that needs a resident's session
> and an operator who learns about trouble from logs. The visual layer
> (palettes, contrast, phone widths, hard-coded English) is the UI-refresh
> lane's and is not repeated. Units are deliberately small: one seam, ≤ ~5
> files, readable by an agent with a 32–64K context window.
> **Who is it for:** whoever picks up an `X`/`R`/`A`/`O` unit, and the cold
> reader at the close. **When to read the rest:** before starting a unit —
> read the Decisions table, then only your unit's card.

# Plan: IMPROVE-UX — close the loops, per audience

Measured from `main` `de76490` (2026-11-06). Work lands on `development`;
check `docs/adr` there for the next free ADR number before writing one
(the UI-refresh lane has been consuming numbers in parallel).

## What this lane is (and is not)

**Is:** journeys, loops and seams — feedback that comes back, actions that
complete, errors you can recover from, the operator's view of their own
instance, plus the structural accessibility (labels, alt text), empty states
and resident-facing wording the UI-refresh lane did not cover.

**Is not:** visual design, contrast, phone layout, hard-coded-English sweeps
(UI-refresh lane, `claude/ui-refresh-plan.md`), native review of de/fr/da
machine translations (parked there), provisioning automation
(`claude/deployment-manager-plan.md`), or new security policy (each unit that
touches access carries a SEC-lane review step instead).

## Decisions

| # | Decision | Chosen | Alternatives rejected |
|---|---|---|---|
| D1 | What a reporter gets back (content reports **and** `/issues/new`) | **A "My reports" page** listing the resident's reports and issues with status and a short moderator-picked outcome + optional note; the resolve notification links to it | Notification only (no history); record-and-accept as U09 did |
| D2 | Error recovery for ~550 bare `NotFound()`/`Forbid()` returns | **One friendly status page site-wide**: `UseStatusCodePagesWithReExecute("/status/{code}")`, localized, keeps the nav, offers Back/Home/Search. **HTML page requests only** — fetch/JSON, `/health`, `.ics`, media and kiosk slides keep their bare responses. Controllers unchanged | Per-action views (hundreds of edits); a "deleted on {date}" variant (later, if wanted) |
| D3 | Leaving the platform (ADR 0142 D5 lets only a GlobalAdmin self-delete) | **Full self-service with a grace period**: the resident schedules deletion; it executes after N days unless they cancel by signing in. Child accounts under a guardian cannot self-delete. Amends ADR 0142 D5. **SEC-lane review step required** | Request + admin confirmation; keep 0142 and only reword |
| D4 | Kiosk viewers (`/kiosk` runs under a resident's session) | **Two modes.** *Personal kiosk* stays: signed in, the owner's full visibility, existing per-entry options. *Shared kiosk link* is new: per-playlist, revocable, long random token, read-only, reaches only `/kiosk` + its slides, shows **community-visible items only** (others get the existing denied slide), last-used shown, every read audited under the owner as via-kiosk-link. **SEC-lane review step required** | Longer resident session + friendlier sign-out screen; docs-only "use a dedicated account" |
| D5 | Delivery | **Audit all three audiences, then one plan** (this file) | One plan revised per audience; three plans |
| D6 | Moderation (`HidePostAsync`/`RemovePostAsync` have no web caller; reports reach no moderator) | **Act from the report and route reports.** The D1 outcome picker performs the action: Hide (reversible, with Unhide), Remove, No action — posts first. New reports notify the component's standing moderators + GlobalAdmins with *no content*; nav badge with the open count. The Unlock privacy gate and its audit are unchanged | Actions only; routing only |
| D7 | Getting people in when sign-up is invitation-only (no invite exists) | **Email invitations from admins, plus an admin setting that lets residents invite neighbours** (optionally with admin approval). Single-use, expiring links that work while sign-up is closed | Admin creates the account directly; admin invitations only |
| D8 | The operator's view of instance health | **`/admin/status`** (version, uptime, the `/health` checks, dead letters + mail alarms, storage, config warnings such as `SeedAdmin__*` still set), a one-line dashboard notice when anything is red, and mail alarms also delivered to GlobalAdmins' inbox. Read-only, no secrets | Config warnings only; leave it to the deployment manager |
| D9 | Smaller items (stated, not objected to) | R7 notification links on `TodoAssign`/`GroupAdded`/report kinds · guest notice shows the window end + how to reach admins, friendly expired page · label/alt fixes with a falling ceiling · 16 jargon strings rewritten · the 48 plain empty states moved to `.kmb-empty` with one next action · `how-it-works.md` brought up to date · A5 account picker for guests · A6 "request more space" + per-resident quota override · O3 runbook update · O4 break-glass stays SQL by design (runbook gets a checked block) · O5 escalation outcome **parked** (cross-instance protocol change) | — |

## Evidence (measured 2026-11-09, `main` `de76490`, `python3 tools/ux/ux-measure.py`)

Heuristic counts are leads, checked by hand where the table says so.
Severity: **S** = an audience cannot complete a core journey; **M** = a
journey completes only via a seam or leaks; **L** = friction.

| # | Audience | Finding | Where | Principle / test / anti-pattern | Sev |
|---|---|---|---|---|---|
| E1 | Admin/mod | Moderator cannot act on reported content: `HidePostAsync`/`RemovePostAsync` are tested but have **no web caller**; Resolve page offers Assign/Unlock/Resolve only; post delete is author-only. `how-it-works.md` promises "remove it" | `PostService.cs:1221,1258`; `Views/Moderation/Resolve.cshtml`; `PostsController.cs:1410` | P1 linkage; closed-loop ✗; *part-without-whole* | **S** |
| E2 | Admin/mod | `PostStatus.Hidden/Removed` is written but **no read path filters on it** — wiring E1 alone would change nothing visible | `rg "PostStatus\."` → only `Post.cs:47`, `PostService.cs:1239,1274` | P3 bugs at seams | **S** |
| E3 | Admin/mod | New reports reach no moderator: `ReportFiled` goes to the *reported author*; moderators hear only after a GlobalAdmin unlocks + assigns; no queue badge | `ModerationService.ReportIntake.cs:121-147,538-546`; `_AccountNav.cshtml:215` | test 3 "depends on one person"; *single point of failure* | M |
| E4 | Resident | Report resolution comes back empty: the notification repeats the reporter's own reason, no outcome, no link; `Report` has no resolution field; no "my reports" | `ReportIntake.cs:703-711`; `Report.cs` | P4 feedback loops; closed-loop ✗ | M |
| E5 | Resident | `/issues/new` never comes back: `ResolutionNote` stored, never shown or sent; contact email "never mailed" | `ErrorReport.cs:73`; `ErrorReportDraft.cs:16` | P4; closed-loop ✗ | M |
| E6 | Resident | Blank 404s: ~550 bare status returns in resident controllers, no `UseStatusCodePages` | `Program.cs:1031`; script `errors` | P3; *dead end* | M |
| E7 | Resident | Residents cannot delete their own account ("contact an administrator") | `AccountController.cs:193-300`; ADR 0142 D5; `account.delete_refused` | handoff ✗ | M |
| E8 | Resident (kiosk) | `/kiosk` requires a resident session on the wall screen; session expiry lands on login | `KioskController.cs:42,84` | handoff ✗; Stable | M |
| E9 | Admin | Invitation-only sign-up is a dead end: the closed page promises "an administrator will add your account", no create/invite action exists (accounts come only from self sign-up or a guardian adding a child) | `account.signup_closed_body`; `IdentityService.cs:176` | closed-loop ✗; *promise without seam* | M |
| E10 | Operator | No in-app instance status; alarms only by email via `SupportEmailAlertSink` (SMTP may be what failed) | `Program.cs:397`; `MailAlarmsService` | P4; Energizing | M |
| E11 | Operator | First boot: set `SeedAdmin__*`, read logs, remember to remove env; nothing warns if left set | `docs/ops/first-boot.md` steps 1–5 | handoff ✗ | M |
| E12 | Operator | Runbook stale: says export is "deferred" and deletion is operator SQL; both are in-app | `docs/ops/privacy-operations.md:36-45` | P6 decay; *doc drift* | M |
| E13 | Resident | `how-it-works.md` (the resident entry point) says events/projects are "not built yet" and routes feedback to GitHub issues | `docs/philosophy/how-it-works.md` | P6 decay | M |
| E14 | Resident | 7 of 32 notification emits carry no link back (`TodoAssign`, `GroupAdded`, all report kinds) | script `notify_links` | closed-loop partial | M |
| E15 | Resident | 83 inputs without a programmatic label, 18 `<img>` without `alt` (101 leads in 48 views; spot-checked: translation-modal labels lack `for`) — WCAG 2.1 1.3.1 / 1.1.1 / 4.1.2 | script `a11y` | Coherent | M |
| E16 | Resident | Empty states: 4 friendly, 48 plain, 9 none in 61 list views | script `empty` | Energizing | L |
| E17 | Resident | 16 resident strings use developer vocabulary (GlobalAdmin, standing, lane, intake, slug) | script `jargon` | Coherent | L |
| E18 | Resident (guest) | Guest notice gives no end date or next step | `account.guest_welcome` | handoff | L |
| E19 | Admin | Guest form asks for a free-text "Guest subject id" | `Views/AdminGuests/Index.cshtml:26` | handoff (re-key) | L |
| E20 | Resident/admin | "Ask an administrator to raise the quota" — no request path, quota only global | `account.storage_remaining_note`; `AdminStorageController` | handoff | L |
| E21 | Operator | Break-glass = hand-written `INSERT` (by design: hostile-admin case) | `privacy-operations.md:49-58` | accepted | L |
| E22 | Operator | Escalation to the maintainer is one-way | `ErrorReportService.AcceptInboundAsync` | P4 | L (parked) |
| — | All | Localization: 2 635 keys × en/de/fr/da, 0 missing | script `l10n` | — | ✓ |
| — | Resident | Closed loops that hold: post → reply → notification; event → RSVP → reminder → iCal; messaging; group invite accept/decline in the inbox; guardian approvals | — | — | ✓ |

## The units

Rules for every unit: **one commit, one seam, ≤ ~5 files** (plus
translation JSON). Each card names the files to read first so an agent with
a small context does not have to explore. Intermediate commits may compile
without running. New strings go to en/de/fr/da (de/fr/da machine-written,
flagged for the parked native review). A unit that makes a decision writes
or amends an ADR in the same commit. `ux-measure.py --check` (after `X01`)
must not regress. Open questions sit as `// TODO ❓` at the unit that needs
them; ask Per when you reach one.

Dependencies are listed per card. Suggested order: `X01`, then `R01–R02`
(cheap, broad win), then `A01–A06` with `R03–R08` (the moderation loop is
one story), then the rest in any order.

### Lane

| # | Unit | Depends | Done when |
|---|---|---|---|
| X00 | This plan + `tools/ux/ux-measure.py` | — | plan merged |
| X01 | **Gate**: `tools/ux/ux-baseline.json` + `--check` (fails if a tracked count rises: bare-status pages without status page, a11y leads, plain/none empty states, jargon, notify-without-link); report-only CI step | X00 | CI prints the table; a raised count fails locally |
| X02 | **Close + cold read**: a fresh agent that has not seen this lane runs the gate, walks every journey row below on a running instance (closed-loop + handoff tests), checks each DoD line, writes the handoff note | all | handoff note with before → after, or an honest held close |

### Resident

| # | Unit | Read first | Depends | Done when |
|---|---|---|---|---|
| R01 | Status page: `StatusController` (`GET /status/{code}`) + `Views/Status/Index.cshtml` (404/403/400/413/415/429/500 texts, Back/Home/Search) + `status.*` keys | `Program.cs` ~1020-1040, `Views/Shared/Error.cshtml` | — | page renders with nav for each code |
| R02 | Wire `UseStatusCodePagesWithReExecute` **for HTML GETs only** (skip when `Accept` lacks `text/html`, `X-Requested-With`, `/health`, `*.ics`, `/media/`, `/kiosk/slide`, `/settings/kiosk/items`, `/api/`); integration tests for both sides | `Program.cs`, one existing integration test as template | R01 | tests: HTML 404 has nav; fetch 404 body unchanged; status codes unchanged |
| R03 | `Report` resolution fields: `Outcome` (enum `NoAction/Hidden/Removed/Other`), `ResolutionNote` (≤500), `ResolvedAt/By`; `ResolveReportAsync` gains outcome + note; unit tests | `Report.cs`, `ReportIntake.cs:648-720`, `IModerationService.cs:160-175` | — | tests pin the stored outcome + audit row unchanged |
| R04 | Resolve form: outcome radio + note textarea (labels associated), controller passes them; keys | `Views/Moderation/Resolve.cshtml`, `ModerationController.cs:477+`, `ModerationResolveViewModel.cs` | R03 | resolving stores outcome; no action executed yet (A02 does) |
| R05 | Core read: `IModerationService.ListMyReportsAsync(reporterId)` (status, outcome, note, target title — target title only if the reporter can still read it) + `IErrorReportService.ListMineAsync(subjectId)` (status, resolution note); tests | `ModerationReadModels.cs`, `ErrorReportService.cs` | R03 | tests incl. "target no longer visible → no title" |
| R06 | `GET /reports/mine` page (two sections: content reports, issues), link in the account menu, friendly empty state; keys | `_AccountNav.cshtml`, a simple list view e.g. `Bookmarks/Index.cshtml` | R05 | page lists both kinds |
| R07 | `ReportResolved` body = localized outcome (+ note), `linkPath` `/reports/mine#r-{id}`; tests | `ReportIntake.cs:703-711`, `NotificationKeys.cs` | R03, R06 | notification links to the row |
| R08 | New kind `issue.resolved` (+ `issue.triaged` optional ❓) emitted from `MarkResolvedAsync` to the filer when `SubjectId` is set, link `/reports/mine#i-{id}`; add to `NotificationKinds.Known`; tests | `ErrorReportService.cs:117-145`, `NotificationKinds.cs` | R06 | signed-in filer gets an inbox row. `// TODO ❓` anonymous filers: mail the contact email? (SEC: currently "never mailed") |
| R09 | `linkPath` on `TodoAssign` (`/projects/todos/{id}`) and `GroupAdded` (`/groups/{id}`); tests | `TodoService.Lifecycle.cs:95-107`, `UserInfoService.cs:240-250` | — | script `notify_links` shrinks by 2 |
| R10 | **ADR** amending 0142 D5: self-service deletion with grace period; child rule; what "cancel" means; `// TODO ❓` grace length (proposal 14 days) | ADR 0142, `ops/privacy-operations.md` | — | ADR merged |
| R11 | Core: `DeletionRequest` doc + `ScheduleDeletionAsync` / `CancelDeletionAsync` (child under guardian refused; last-GlobalAdmin guard reused); tests | `IdentityService.AccountOps.cs` (DeleteAccountAsync), `GuardianService` child check | R10 | tests for schedule/cancel/child refusal |
| R12 | Wolverine scheduled job runs due requests through the existing `DeleteAccountAsync` lane (D2 pseudonymisation unchanged); tests | an existing scheduled job (e.g. `MailAlarmsTick.cs`) | R11 | due request executes once; cancelled one doesn't |
| R13 | Web: `Account/Delete` for every resident — schedule, show date, cancel; signing in during the grace period shows a notice with Cancel; export link offered first; keys (replace `account.delete_refused`) | `AccountController.cs:167-300`, `Views/Account/Delete.cshtml` | R11 | flow works signed in; refused text gone |
| R14 | **SEC review** of R10–R13 (independent agent or the SEC lane): audit rows, re-auth before scheduling, child rule, guard ordering | R10–R13 diffs | R13 | review note in the handoff; findings fixed or filed |
| R15 | **ADR** + Core `KioskLink` doc (owner, playlist, token *hash*, created, lastUsed, revoked) + create/revoke/validate service; tests | `Kumunita.Core/Kiosk/*`, ADR 0285 | — | tests: revoked/unknown token refused, hash only at rest |
| R16 | Kiosk-link authentication: a handler/middleware that accepts the token **only** on `/kiosk` and `/kiosk/slide`, builds a restricted principal (owner + `kiosk-link` claim), refuses everything else; rate-limited; tests | `KioskController.cs:42-150`, `Program.cs` auth setup | R15 | tests: token on `/posts` → refused; on `/kiosk` → playlist |
| R17 | Community-visible filter in kiosk-link mode: the catalog's `ResolveAsync` returns the denied slide for anything not visible to the whole community; audit `Via` = kiosk link; tests | `KioskItemCatalog` (Web/Services), `AccessVia` | R16 | tests: group-private item → denied slide |
| R18 | `/settings/kiosk`: create link (copy once), list with last used, revoke; personal mode untouched; keys | `Views/Kiosk/Settings.cshtml`, `client/lib/kiosk-settings.ts` | R15 | create/revoke works |
| R19 | **SEC review** of R15–R18 | diffs | R18 | review note; findings fixed or filed |
| R20 | Guest notice shows access end (resident's zone) + link to message the admins; friendly page when the window has passed; keys | `GuestAccess.cs`, notice strip partial (ADR 0174) | — | guest sees end date |
| R21 | Plain words for the 16 jargon strings (en/de/fr/da) — list from `ux-measure.py` `jargon` | the 4 `Localization/Keys/*` files involved | — | `jargon` count 0 (or each survivor justified in baseline) |
| R22 | `how-it-works.md`: events/projects shipped, feedback → `/issues/new` + "My reports", reports reach moderators (after A05), deletion is self-service (after R13) — docs only; doc-check passes | `docs/philosophy/how-it-works.md` | A05, R13 (or mark pending) | no "not built yet"; no GitHub-only feedback |
| R23 | a11y heuristic refinement in the script (ignore `@* *@` comments, nested `<label>` wraps) + re-baseline | `tools/ux/ux-measure.py` | X01 | false positives removed, count recorded |
| R24 | a11y batch 1: `Posts/Detail`, `Posts/Index`, `Shared/_PostReply`, `Groups/PostDetail`, `Groups/_GroupPostReplies`, `Groups/_GroupTranslationModals` | those views | R23 | their leads → 0 |
| R25 | a11y batch 2: `Event/Detail`, `Event/Create`, `Event/Edit`, `Announcement/Detail`, `Announcement/Index` | those views | R23 | leads → 0 |
| R26 | a11y batch 3: `Projects/ProjectDetail`, `ProjectEdit`, `ProjectNew`, `Boards/BoardNew`, `Todos/TodoDetail`, `Todos/TodosIndex`, `Todos/_TodoStatusAssignEvent`, `Shared/_BoardCard` | those views | R23 | leads → 0 |
| R27 | a11y batch 4: `Document/Edit`, `Document/New`, `Document/Index`, `Directory/Index`, `Profile/Edit`, `Profile/Preview`, `Guardian/Detail`, `Guardian/Index` | those views | R23 | leads → 0 |
| R28 | a11y batch 5 (remaining resident): `Community/Manage`, `Community/Translations`, `Locale/DateFormat`, `Notes/NoteRemoveTag`, `SecuritySettings/Mfa`, `Shared/_AccountNav`, `Shared/_GrantPickers`, `Shared/_NoteButton`, `Account/Portability/*`, `Languages/Translations` | those views | R23 | resident leads → 0 |
| R29 | Empty states batch 1 (feeds): `Posts/Index`, `Blog/Feed`, `Home/Index`, `Search/Index`, `Tag/ByTag`, `Tag/Index`, `Notifications/Index`, `Notifications/Subscriptions` — `.kmb-empty` + one next action | `Groups/Index.cshtml:144` (reference) | — | 8 views friendly |
| R30 | Empty states batch 2 (people/messages): `Directory/Index`, `Directory/Detail`, `FindPeople/Bio`, `FindPeople/Tag`, `Messages/Index`, `Messages/Thread`, `GroupMessages/Index`, `GroupMessages/Thread` | same | — | 8 views friendly |
| R31 | Empty states batch 3 (projects): `Boards/BoardIndex`, `Boards/_BoardLanes`, `Boards/_BoardList`, `Projects/ProjectsIndex`, `Projects/ProjectDetail`, `Todos/TodosIndex`, `Todos/_TodoComments`, `Todos/_TodoDisplay`, `Shared/_BoardCard` | same | — | friendly |
| R32 | Empty states batch 4 (content): `Document/Index`, `Document/Deleted`, `Page/Index`, `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`, `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index` | same | — | friendly |
| R33 | Empty states batch 5 (details/groups/guardian): `Announcement/Detail`, `Event/Detail`, `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`, `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`, `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`, `Community/Manage`, `Community/Translations` | same | — | `empty` plain count ≤ 1 (Moderation/Index is A06's) |

### Admin / moderator

| # | Unit | Read first | Depends | Done when |
|---|---|---|---|---|
| A01 | **Reads honour `PostStatus`**: feeds, group feeds, search, kiosk catalog and `GetPostAsync` exclude Hidden/Removed for everyone except the author (sees "hidden by a moderator") and moderators via the report lane; tests | `PostService.cs` list/get methods, `Post.cs:10-47` | — | tests per read path |
| A02 | Outcome executes the action: `Hidden` → `HidePostAsync`, `Removed` → `RemovePostAsync`, in the same session as resolve (one transaction, audit rows as today); post targets only; tests. `// TODO ❓` a **reply** report: hide the parent post (the existing comment's lever) or add a reply-level hide? | `ReportIntake.cs:648-720`, `PostService.cs:1209-1290` | R03, R04, A01 | resolving with Hidden hides the post |
| A03 | `UnhidePostAsync` (Moderate-gated, audited) + "Unhide" on the resolved report page; tests | `PostService.cs:1209-1250` | A02 | hidden → active round trip |
| A04 | Hide for the next target kind — `// TODO ❓` which first (group posts are the closest copy) | `ReportTargetsController.cs`, `ReportIntake.cs` `LoadReportTargetResourceAsync` | A02 | one more kind hideable; the rest listed as follow-ups |
| A05 | Routing: new kind `report.queued` to the component's standing moderators + GlobalAdmins, **no content in body**, link `/moderation/{id}` (scoped read still applies); `ReportFiled` to the author unchanged; tests | `ReportIntake.cs:121-147,265-275,410-420`, `ModeratorAssignment` | — | moderators get an inbox row on intake |
| A06 | Nav badge: open-report count the actor may see (same scoped query as the queue, cached ≤60 s) on the Moderation menu item; `Moderation/Index` friendly empty state | `_AccountNav.cshtml:210-230`, `ModerationService.GetQueueAsync` | A05 | badge shows; 0 hides |
| A07 | **ADR** + Core `Invitation` doc (email, inviter, token hash, expiry, status) + create/revoke/accept service; tests | `IdentityService.cs:140-200` (`RegisterAsync`), `IdentityPlatformGateService.cs` | — | tests: expired/revoked refused, single use |
| A08 | Sign-up accepts `?invite=` while the gate is closed; email prefilled + must match; marks accepted; tests | `AccountController.cs` Signup (≈380-420), `SignupClosed` view | A07 | invited person can sign up with gate closed |
| A09 | `/admin/invitations`: invite one or more emails, list sent/accepted/expired, resend, revoke; invitation email template; keys | an admin list view e.g. `Views/AdminGuests/Index.cshtml`, `EmailMessages.cs` | A07 | admin invites end to end |
| A10 | Acceptance notifies the inviter (+ admins per the sign-up notify setting); rewrite `account.signup_closed_body` to match reality | `IdentityService.cs:213` (AccountSignup emit) | A08 | text no longer promises a missing action |
| A11 | Resident invitations: admin setting (off / on / on-with-approval) + Core rule. `// TODO ❓` who may invite and how many per period | `AdminSignupController.cs`, A07 service | A07 | setting stored; rule tested |
| A12 | Resident "Invite a neighbour" (profile/account menu) + admin approval queue on `/admin/invitations` when approval is on; keys | A09 view | A11 | resident invite works in both modes |
| A13 | Guest form: account picker instead of free-text subject id | `Views/AdminGuests/Index.cshtml`, `Views/Admin/Accounts.cshtml:41` (picker) | — | no free-text id |
| A14 | "Request more space" on My storage → notification to GlobalAdmins with link to the account; rewrite `account.storage_remaining_note` | `Views/Account/Storage.cshtml`, `NotificationKinds.cs` | — | request reaches admins |
| A15 | Per-resident quota override on the admin account page; quota check uses override ?? global; tests | `AdminStorageController.cs`, quota check in Media | A14 | override honoured |

### Operator

| # | Unit | Read first | Depends | Done when |
|---|---|---|---|---|
| O01 | Core `IInstanceStatusService`: reuse the `/health` checks, dead-letter count, mail alarms, storage metrics, version/uptime, and a pure `ConfigWarningsPolicy` (`SeedAdmin__*` set after admin exists, `SampleData__Enabled` in Production, `Health__Token` unset, Data Protection keys not persisted); policy unit tests | `HealthController.cs`, `MailAlarmsService.cs`, `docs/ops/configuration-reference.md` | — | policy tests green; no secret values returned |
| O02 | `/admin/status` page (GlobalAdmin) + admin hub card; keys | `Views/Admin/Index.cshtml`, `AdminStorageMetricsController.cs` | O01 | page shows all rows |
| O03 | Dashboard notice (notice strip, admins only) when any status row is red, linking to `/admin/status` | notice strip partial (ADR 0174), O01 | O02 | red row → notice |
| O04 | Mail alarms also emit an inbox notification (`ops.alarm`) to GlobalAdmins, link `/admin/status`; tests | `MailAlarmsService.cs`, `SideEffects/SupportEmailAlertSink.cs` | O01 | alarm → inbox row |
| O05 | Runbooks: `privacy-operations.md` (export + deletion in-app, D3 grace period), `first-boot.md` (check `/admin/status` instead of logs for SeedAdmin), break-glass SQL as one checked block; docs-check passes | those docs | R13, O02 | no "deferred"/operator-SQL deletion text |
| O06 | Record E22 (escalation outcome) as parked and link `deployment-manager-plan.md` for provisioning — docs only | `docs/ops/monitoring.md` | — | parked item visible in the runbook |

## Journeys for the cold read (`X02`)

Each row is walked on a running instance; a row passes when the outcome
arrives back in-app with no copy-paste, re-key or "ask the admin".

| Audience | Journey | Passes when |
|---|---|---|
| Resident | Report a post → see the outcome | inbox row links to "My reports" with outcome; post hidden if so decided |
| Resident | Report an issue → hear back | issue shows its resolution note on "My reports" |
| Resident | Follow a notification to deleted content | friendly status page with nav |
| Resident | Get assigned a to-do | inbox row opens the to-do |
| Resident | Leave the platform | deletion scheduled, cancellable, executes after the grace period |
| Kiosk viewer | Wall screen in a common room | runs on a kiosk link; private items never shown; owner revokes it |
| Guest | Arrive, learn the window, reach an admin | end date visible; message link works |
| Moderator | Report filed in my community → act | inbox row + badge → hide/remove from the report → reporter told |
| Admin | Invite a neighbour while sign-up is closed | invitation accepted, admin notified |
| Admin | Resident needs more space | request arrives; override set in-app |
| Operator | First boot to healthy | `/admin/status` green, no SeedAdmin warning, no log reading |
| Operator | SMTP breaks | inbox alarm + red status row, not only an email |

## Definition of Done

1. Every journey row above passes on a running instance (X02).
2. `ux-measure.py --check` is green and every tracked count is at or below
   its `X00` value; these reach **0**: emits without a link back (E14),
   resident jargon strings (E17), resident a11y leads after R23's
   refinement (E15); plain/none empty states ≤ 1 (E16).
3. HTML page requests never render a blank error body (R02 tests).
4. The two SEC review notes (R14, R19) exist and every finding is fixed or
   filed with the SEC lane.
5. `how-it-works.md` and the two runbooks match the shipped behaviour
   (R22, O05); docs-check passes.
6. ADRs written for D2 (if non-trivial), D3 (amends 0142), D4, D6, D7, D8.
7. `dotnet build`, unit and integration tests green; translation parity test
   green.
8. Handoff note records before → after numbers and anything held, honestly
   (the IMPROVE-CODE close precedent).

## Not in this lane (recorded so nobody re-finds it)

- Visual polish, contrast, phone widths, hard-coded English → UI-refresh lane.
- Native review of de/fr/da → parked in the UI-refresh plan.
- Provisioning, multi-instance monitoring, backups-as-a-service → deployment manager.
- Cross-instance escalation outcome (E22) → parked (O06).
- "Deleted on {date}" variant of the status page (D2 alternative 3) → later, if wanted.