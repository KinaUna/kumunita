namespace Kumunita.Web;

/// <summary>
/// The platform's version history, as shown in the "What's new" section of the
/// About page and announced once per version by the dismissible toast (the
/// <c>VN</c> lane, ADR 0110). This is source-controlled static data — not
/// configuration — because versions are decided in the repo, not per-deployment,
/// following the <see cref="Milestones"/> precedent.
///
/// Keep this in sync with the shipped surface: when a named lane or milestone
/// ships, add its version (or extend the newest one) here. The <see cref="All"/>
/// list is ordered newest-first; <see cref="Latest"/> is its head. Version
/// dates are fixed, timezone-independent ISO dates (they are a fact about when
/// the release shipped, not a user-visible timestamp — so no <c>kw-dt</c>).
/// </summary>
public static class WhatsNew
{
    public sealed record Release(string Version, string Date, IReadOnlyList<string> Changes);

    public static IReadOnlyList<Release> All { get; } = new List<Release>
    {
        new("0.77.0", "2026-10-30", new List<string>
        {
            "The platform speaks your language — the last of the hard-coded English is gone. Over the last two days the whole remaining surface moved onto the translation catalog (the kw-l registry, en / de / fr / da): the **admin hub** (the /admin index, platform, communities, accounts, manage, security, audit, break-glass, error reports and escalation tokens pages), the **admin settings pages** (site appearance, storage, surface labels, help, guest accounts, storage metrics, setup, quiet hours, portability, and the MFA + security-sessions settings views), **every sign-in / validation / flash message** a resident can be shown (the account, announcement, document, event, group, guardian, inventory, page, post, profile and project write lanes — the guard text you see when a save is refused now resolves in your language), the **guardian event-attendance statuses** (a pending request reads \"Wants to attend: {choice}\" and the current-attendance rows show the real RSVP — Going / Maybe / No — instead of every row reading \"Wants to attend\"), the **Kiosk settings** and **sign-up** pages, the **page form**, the **tag input hint**, the **Groups pages**, and **all page titles** (the browser-tab / heading titles are now resolved centrally in the layout through a title-key map, so a page titled in your language shows a matching title). The English text is kept as the floor everywhere, so a missing translation still shows the English rather than a blank — the de / fr / da strings are machine-written and parked for a native review. Roughly 900 new translation keys across en / de / fr / da (the KnownTranslationKeys parity pin re-based through 14 687), the ControllerFlashExtensions.FlashAsync seam (ADR 0261) as the one resolve-with-floor path, and zero new bounded context / doc / authorization surface (ADRs 0253–0273).",
        }),
        new("0.76.0", "2026-10-29", new List<string>
        {
            "Documents — who uploaded, who changed, and the delete lane — the /documents feed rows now show **who uploaded** each document (the uploader's name, resolved to a display name and falling back to the raw id when the profile is missing) alongside the size + date, and the detail page gains a **\"Uploaded by\"** meta row (the uploader's display name, the same IUserInfoService.GetProfileAsync read lane the post feed uses — a *display* lookup, never an AccessAudit subject, the ADR 0026-C boundary). A new Document.ModifiedById field (ADR 0004 §B.1 additive, zero migration) is stamped by the edit lane (ADR 0125 UpdateAsync) and the re-file lane (DocumentFolderService.MoveDocumentAsync) — when the last modifier is **distinct** from the uploader, the detail page also shows a **\"Last modified by\"** row (when the modifier is the uploader the row is hidden as redundant with the \"Uploaded by\" row; the date is still shown by the \"Last updated\" row). A new **owner-only soft-delete** lane (the ADR 0024 Post.DeletedAt shape carried to documents): the uploader (Document.OwnerId) may remove a document — a non-owner GlobalAdmin is refused (the ADR 0125 D1 owner-reading, the ADR 0122 D7 posture: 404 not 403) — and every read surface (feed, detail, download, search, notes) hides the soft-deleted document (Document.DeletedAt + Document.DeletedById are stamped, the record is kept, never hard-deleted). A new **Delete** button on the detail page (owner-only, the ADR 0125 CanEdit shape — a non-owner never sees the affordance) posts to POST /documents/{id}/delete; on success the controller redirects to the feed (the document is gone from the detail page by construction). Four new translation keys (en / de / fr / da — the KnownTranslationKeys parity pin): documents.detail.uploaded_by, documents.detail.last_modified_by, documents.delete, documents.flash_deleted. Zero new bounded context / doc / authorization surface, zero new AccessAction, zero new Decide() branch — the delete lane is the ADR 0122 §1.a A2 write-lane shape (writes are authenticated, not audience-restricted reads), and the attribution is the same display-read seam the post feed already uses (ADR 0239).",
        }),
        new("0.75.0", "2026-10-27", new List<string>
        {
            "Kanban board card edit modal — on a Kanban board (the M5 Projects `/projects/boards/{slug}` details page, ADR 0190) **clicking a card — or choosing Edit in its ⋮ menu — now opens a modal that edits the to-do in place instead of navigating to the detail page**: the same core fields the dedicated Edit page carries (title, status, assignee, parent, blocked-by, start/due, tags, body, component, language), **prefilled from the card's current values** — so you no longer have to leave the board to *reach* the edit form (the whole point: the form now opens on the board). Saving posts the update (`/projects/todos/{id}`, the same route the Edit page uses) and lands you on the to-do detail page (ADR 0189) — the same destination the dedicated Edit page already uses, so the save behavior is unchanged, only the starting point moved. The card's ⋮ menu keeps a **View details** link for the full detail page, and the dedicated Edit / detail pages are unchanged and still the deep link. The modal reuses the existing `UpdateTodoAsync` update lane — zero new bounded context / doc / authorization surface, zero migrations (the update lane never touches audience, so the modal omits the audience editor entirely and the to-do keeps its current audience on save), one new client module (`board-edit-modal.ts`) that re-homes the modal to `<body>` alongside the existing card modal (the Bootstrap backdrop / transformed-ancestor trap, ADR 0190), and zero new `AccessAction` / `Decide()` branch (the edit is the same `Projects:Edit` grant the Edit page already requires — the modal posts to the same route and inherits the same authorization). `Milestones.cs` / README Roadmap / `MilestonesTests.cs` are byte-identical (a named lane on the shipped **M5 / Projects** Kanban-boards surface; the M-letter roadmap is untouched — ADR 0195).",
        }),
        new("0.74.0", "2026-10-27", new List<string>
        {
            "Sample-data MFA (two-factor) lock — on a demo site (a `SampleData__Enabled` instance) a **GlobalAdmin** can now also lock the **shared demo accounts** out of the two-factor settings surface (the **MFA** toggle on `/admin/sample`, a sibling of the existing change-password lock — ADR 0138). When the MFA lock is on, a demo (sample) account sees a short **\"MFA is locked on this demo\"** notice at `/settings/security/mfa` instead of the enroll / recovery / disable controls (the same notice shape the change-password lock already uses), and any direct enroll / recovery-rotation / disable attempt is refused before it touches the account — so a visitor testing the demo can no longer mint a TOTP secret + recovery codes on a shared account and break the credential for everyone else. The **sample GlobalAdmin is exempt** (keeps its own MFA lane, exactly as it keeps its own password lane), and it is **exempt from the forced-MFA-at-login redirect** so the demo admin can still sign in with just the shared password (a real, non-sample GlobalAdmin is still forced to enroll — that enforcement is unchanged). The two toggles (change-password + MFA) are independent (each its own form on `/admin/sample`), both default **off** (a fresh or real instance never locks anything), both audit their set/unset (the `sample.set-mfa-lock` / `sample.set-password-lock` actions, AccessVia.Admin), and both read the *same* closed `SampleAccountEmails` set (one source of truth, so the password lock, the MFA lock, and the notification-suppression gate cannot drift). Zero new bounded context / doc / authorization surface (the lock is a pure read decision + a single write guard on the MFA surface, the ADR 0138 gate posture re-applied), zero migrations (the `LocaleSettings.SampleMfaLocked` field is delta-detected — ADR 0004 §B.1, the ADR 0138 `SamplePasswordChangeLocked` precedent), and the `Milestones.cs` / README Roadmap / `MilestonesTests.cs` are byte-identical (a named lane on the shipped sample-data surface — the ADR 0138 precedent). (ADR 0194.)",
        }),
        new("0.73.0", "2026-10-27", new List<string>
        {
            "Portability import modes — the /admin/portability import now offers a **\"keep existing\"** choice. **Unticked (the default)** is a clean-slate restore: the instance's accounts + content are wiped, then the archive is applied — now succeeding where a restore onto an instance that already held the archive's accounts used to fail with a 500 (\"username is already taken\"). **Ticked** is a merge: the instance's existing accounts + content are kept intact and only new, non-existent accounts + content are added (existing principals are never changed, no password reset). Zero new bounded context / doc / authorization surface, zero migrations (the two new \"keep existing\" labels are rows in the translation catalog), and the fail-closed validate-before-write discipline holds in both modes (ADR 0193).",
        }),
        new("0.72.0", "2026-10-26", new List<string>
        {
            "Security settings tab + MFA pairing QR code — two-factor (MFA) setup, which until now was only reachable by typing the /settings/security/mfa URL directly, now lives under a **Security** tab on the settings strip (the same tab shape as Language / Time zone / Quiet hours / Messaging / Children / Change password / Kiosk — ADR 0080), and the enroll screen shows a **QR code** to pair your authenticator app. Scan the QR with Google Authenticator, 1Password, Authy, or any TOTP app and it pairs in one step; the setup key and otpauth URI remain below as the manual-entry fallback (copy the key, or let the app import the URI). The QR is rendered **server-side** (the QRCoder encoder — no new client JS, no new static asset, and the page still works with scripts disabled), embedded as a self-contained data: URI so it carries no cache/identity metadata and needs no extra request; it appears only on the enroll/first-setup leg (a steady \"MFA is on\" page and a recovery-code rotation show no QR, because neither has a fresh pairing key to encode). Zero new bounded context, doc, or authorization surface (the QR encodes the same otpauth:// URI the resident already had to copy — pure presentation, ADR 0174); zero migrations; zero new AccessAction / AccessVia / Decide() branch / adapter (the C-M conventions hold). Two post-ship fixes to the pairing surface, both same-day: the QR is now generated at its natural **QR version ~10** (75×75 modules) instead of being pinned to version 40 (177×177 — far too dense for a phone camera to resolve at the view's 220px render, which is why Google / Microsoft Authenticator initially could not read it — ADR 0174 D1 errata), and the issuer shown in your authenticator app is now **\"Kumunita - {your community's host}\"** (e.g. \"Kumunita - example.kumunita.com\") instead of the ASP.NET Identity default \"Microsoft.AspNetCore.Identity.UI\", so you can tell which Kumunita deployment an entry belongs to and the label never leaks a framework name (ADR 0174 D1 errata 2).",
        }),
        new("0.71.0", "2026-10-27", new List<string>
        {
            "Report events, projects, boards, to-dos, and group posts — the \"Report this …\" lane that already existed for community posts and replies (M3b, ADR 0003 / ADR 0023) now reaches the five other content surfaces you can see: an **event**, a **project**, a **Kanban board**, a **to-do**, and a **group post**. On each of those pages there is now a **Report this {event|project|board|to-do|post}** item in the ⋮ action menu — it opens a small dialog asking for an optional reason and files the report the same way a post report does. You can file one if you can see the content (the page's own visibility check is the precondition — the same standing the post/report lanes have, C-M3b·1), it's rate-limited to 10 per hour per IP (the existing \"report\" policy, re-used), and the author is notified without your reason ever being shown to them (the 2026-09-25 no-reason-disclosure fix, re-applied). The moderation queue and the report detail page now show these new targets alongside posts and replies — a kind badge (event / project / board / to-do / group post) + the target's title + its author + a body preview, and the assign / unlock / resolve workflow is unchanged (the same Status transitions, the same audit trail — AccessVia.Admin, the two-negatives pin). Zero new AccessAction / AccessVia / Decide() branch / adapter (the compose-not-extend convention, C-M3b·1, re-applied); zero migrations (the two additive TargetKind / TargetId fields on the existing Report document are delta-detected — ADR 0004 §B.1). (ADR 0173.)",
        }),
        new("0.70.0", "2026-10-25", new List<string>
        {
            "Kiosk auto-refresh now skips unchanged slides — the /kiosk display was re-navigating each slide's iframe on **every** refresh tick (every `data-refresh-interval` s), which made the wall display flicker and re-load even when the underlying board / calendar / to-do had not changed. Now each slide carries a small **content fingerprint** (a stable FNV-1a 32 digest of its display content — the board's lanes + cards, the to-do rows' id / title / status / modified stamp, or the in-range events — computed server-side by the existing frozen read seams, so no new seam, no new audit row, and no new authorization branch): on each refresh tick the client checks the slide's fingerprint against the last rendered one and **only reloads the iframe when it actually changed** — the /kiosk/slide endpoint (the CSRF-free GET the M35 design reserved for this purpose) now returns the fingerprint alongside the classification, and the `kiosk.ts` state machine compares and skips the re-render when they match (a visible↔denied flip still triggers a reload, since the fingerprint's kind prefix keeps those distinct). The slide-advance cycle, the chrome-hidden stage, the per-resident playlist, and the one `kiosk.save` audit row are all unchanged (the ADR 0164 amendment — the additive delta to the M35 kiosk state machine, display-only and never an access input, M35·4).",
        }),
        new("0.69.0", "2026-10-25", new List<string>
        {
            "Page-level notes — the Personal Notes surface (M37, ADR 0167) now carries a note for the *page itself*, not just for the items on it: on the announcements, community, groups (list + each group's page), events, projects (list + each project's page), to-dos, boards, inventory, and documents pages there is now an **Add note** button in the page header — click it to attach a private note about that page (e.g. \"the announcements page needs a search box\"), the same author-only, organizable (folder + tag) note as your item-level notes. The note is yours alone — invisible to every other resident and to any GlobalAdmin who is not you, your own /notes list commits zero AccessAudit rows (the ADR 0105 \"operator has no read standing\" precedent, re-applied); a page note whose target you can no longer see (a group or project you left or lost access to) degrades to the generic \"no longer available\" label with your own body still visible (the D7 pin, re-applied). The closed TargetKind set grows from eleven to **twenty-one** kinds (the ADR 0167 amendment — ten new `page-*` kinds join the M37 closed set; the D3 write-lane check re-runs the owning surface's own frozen read for the group / project page kinds — the same calls the detail pages use — and is a closed-set + fixed-title + fixed-link check for the eight static page kinds, since a page itself has no item-level visibility to re-check); zero new bounded context, doc, or authorization surface (C-M37·1 holds — a page note is a PersonalNote row, not a new record); zero migrations (the ten new kinds are rows in the existing PersonalNote table — the (OwnerId, TargetKind, TargetId) unique index already covers them). (ADR 0167 amendment.)",
        }),
        new("0.68.0", "2026-10-25", new List<string>
        {
            "Prod-hardening pass — the platform's own 2026-10-22 production-readiness audit FAIL rows are closed (F1–F22, a *named lane*, not a milestone — the M0–M37 + named-lane roadmap is unchanged): **complete GDPR deletion** (your account deletion now removes the content docs you authored — posts, replies, pages, events, to-dos, boards, documents, bookmarks, notes, tags, conversations, messages — plus your media bytes on the volume and the `MediaObject` catalog rows, in the same session as the Identity delete — `DeleteAccount_RemovesResidentAuthoredDocs` / `DeleteAccount_RemovesResidentMediaBytes` / `DeleteAccount_RemovesMediaObjectCatalogRows` / `DeleteAccount_KeepsOthersContentIntact`); **tokens hashed at rest** (verification / setup / MFA tokens are stored as SHA-256 hex, not cleartext — `VerifyToken_StoredHashedNotCleartext` / `VerifyToken_ConsumedOrExpiredStillRejected` / `VerifyToken_ExistingCleartextRowHandled`); **MFA for `GlobalAdmin`** (a TOTP authenticator app + recovery codes, enforced on the `GlobalAdmin` role at login — `GlobalAdminLogin_RequiresMfaChallenge` / `GlobalAdminRecoveryCode_WorksOnce` / `NonAdmin_MfaOptional`); **session kill-all + invalidation** (a `/settings/security/sessions` list + a \"Sign out everywhere\" action, and a password change invalidates your other active sessions — `PasswordChange_InvalidatesOtherSessions` / `KillAllSessions_SignsOutAll`); **signup hardening** (a disposable-email blocklist on the sign-up write lane + a Turnstile bot-challenge seam, OFF by default until your instance's site key is set — `Signup_DisposableEmail_Rejected` / `Signup_BotChallengeEnforced`); **error tracking** (a DSN-gated `Sentry.AspNetCore` registration + a PII-scrubbing filter — `Error_TrackerRegisteredWhenDsnSet`); **dead-letter + alert routing** (an `IAlertSink` seam + the `NoopAlertSink` default + a `SupportEmail`-routed channel for a real human — `DeadLetter_FiresAlert` / `Alert_RoutedToConfiguredChannel`); **`security.txt` + VDP** (a `GET /.well-known/security.txt` route with the RFC 9116 fields + `docs/SECURITY-VDP.md` — `SecurityTxt_ReturnsValidFields`); **CI test gate on `release`** (`.github/workflows/ci.yml` runs the two test-dll jobs, no `dotnet test`); **byte-store lifecycle/retention** (a `MediaLifecycleTick` Wolverine recurring job deletes bytes past the retention rule, idempotent + reusing the GDPR byte-delete seam — `Lifecycle_DeletesExpiredBytes`); **upload scan seam** (an `IVirusScanner` + a quarantine flag on the `MediaObject` doc + a serve-lane gate, fail-open by default — `InfectedUpload_NotServed`); **bounce/complaint suppression** (a `SuppressedAddress` doc + the `ISuppressionService` gate + a `POST /api/mail/suppress` ingest route — `SuppressedAddress_NotMailed` / `SuppressedAddress_NotPresent_EmailStillSent`); **mail alarms** (the `MailAlarmsTick` code leg, the T4 volume alarm); **E2E suite** (a standalone `e2e/` Playwright project + `signup_verify_login.e2e.ts` / `post_write.e2e.ts` driving the compose app, wired into the CI as a third job); **one-step rollback runbook** (the `docs/OPS.md` §5 runbook + the \"last drilled\" line — the drill itself is operator-executed); **app↔DB TLS** (the `DbTls__SslMode` + `DbTls__CaPath` env-knob pair in `Program.cs`, byte-identical to the baseline when unset); and **least-privilege DB role** (the two-role split — `kumunita_migrate` owns the database + the `mt` / `identity` schemas and carries the DDL-creation capability; the runtime `kumunita` app role is demoted to DML + `mt` schema-level `CREATE`, refused `CREATE SCHEMA` / `DROP SCHEMA` / `DROP DATABASE` — `AppRole_CannotDropSchema` — **ADR 0171 Accepted**, amending ADR 0004 §B's boot-ownership invariant; the `ConnectionStrings:KumunitaMigrate` env key is the real key, not the pin's `DbMigrate` placeholder). The lane's owner-ops open items (the ops half of each `code+ops` fix — the prod Postgres two-role provisioning + `ConnectionStrings:KumunitaMigrate`; the `SENTRY_DSN`; the Turnstile site key; the ClamAV daemon; the provider bounce/complaint webhook + alarms; the PITR / WAL archiving; the rollback drill; the prod Postgres TLS + CA bundle; the Coolify CI-gate wiring; the live E2E + F5 sign-out verifications) are **recorded, not assumed done** — see the `## Summary` table in `docs/plans-milestones/done/prod-hardening/hardening-handoff-notes.md` (a named lane, not a milestone — the M0–M37 + named-lane roadmap is byte-identical).",
        }),
        new("0.67.0", "2026-10-16", new List<string>
        {
            "Bookmark and note inventory + directory — the bookmark affordance now appears on the **inventory item** detail page and the **directory** (resident) detail page, and the note affordance now appears on the inventory item detail page (the _BookmarkButton + _NoteButton partials, the same shape already on posts, events, to-dos, projects, boards, pages, announcements, and group posts): click **Bookmark** to save the item or resident to your personal list, or click **Add note** to attach a private note. An inventory item or resident that you can no longer see (the item was deleted, or the resident is blocked) degrades to the generic \"no longer available\" label (the M17 D5 degrade, exactly as on the other kinds); the bookmark is never lost, never leaks anything about the gone target, and you can still unbookmark it from your list (the D5 / F4 pin, re-applied). The D3 write-lane check re-runs the owning surface's own frozen read (IInventoryService.GetItemAsync for inventory — the same call the /inventory/{id} detail page uses; IUserInfoService.GetProfileAsync + IAuthorizationService.CanAsync(Read) for directory — the M37 PersonalNotes \"directory\" D3 shape re-applied), so a target you cannot see cannot be bookmarked or noted (the M16 create-gate posture). The closed TargetKind set grows from seven to **nine** kinds on the M17 surface (the ADR 0118 amendment — the M37 PersonalNotes closed-set vocabulary re-applied; the M16 inventory + M2 directory kinds join the M17 surface) and from ten to **eleven** on the M37 surface (the ADR 0167 extension — the M16 inventory kind joins the M37 closed set, so the inventory detail page's note affordance is also now live); the /bookmarks and /notes list pages render the two new groups when you have pinned or noted them (the bm.list.kind.inventory / bm.list.kind.directory / pn.list.kind.inventory labels, the same terminology your other lists already used); zero new bounded context, doc, or authorization surface (C-M17·1 / C-M37·1 hold); zero migrations (the two new kinds are rows in the existing Bookmark / PersonalNote tables — the (OwnerId, TargetKind, TargetId) unique index already covers them).",
        }),
        new("0.66.0", "2026-10-16", new List<string>
        {
            "Bookmark projects and boards — the bookmark affordance now appears on the project detail page and the Kanban board detail page (just like it already did on posts, events, to-dos, announcements, and pages): click **Bookmark** to save the project or board to your personal list, and it shows up under a new **Projects** group or a new **Boards** group on the /bookmarks page (the bm.list.kind.project / bm.list.kind.board labels, the same terminology your notes already used). A bookmark on a project or board that you can no longer see (the author removed it, or it was soft-deleted) degrades to the generic \"no longer available\" label (the M17 D5 degrade, exactly as on the other five kinds); the bookmark is never lost, never leaks anything about the gone target, and you can still unbookmark it from your list (the D5 / F4 pin, re-applied). A bookmark is a personal pin — no one else can see it, a GlobalAdmin who is not you gets a non-leaky 404, and your own list read commits zero AccessAudit rows (the ADR 0105 \"operator has no read standing\" precedent, re-applied). The D3 write-lane check re-runs the owning surface's own frozen read (IProjectService.GetProjectAsync / GetBoardAsync — the same call the detail page uses), so a project or board you cannot see cannot be bookmarked (the M16 create-gate posture). The closed TargetKind set grows from five to **seven** kinds (the ADR 0118 amendment — the M37 PersonalNotes closed-set vocabulary already carried them; the M17 surface now matches); zero new bounded context, doc, or authorization surface (C-M17·1 holds); zero migrations (the two new kinds are rows in the existing Bookmark table — the (OwnerId, TargetKind, TargetId) unique index already covers them).",
        }),
        new("0.65.0", "2026-10-15", new List<string>
        {
            "First day of week — a per-resident display preference for how the week is ordered. Weeks default to Monday-start (the calendar's existing shape), but you can now switch your first day to Sunday: a new Profile.FirstDayOfWeek preference (the additive ADR 0004 §B.1 field, the PageSize / TimeZone single-write-lane shape — no migration) written by the owner-scope SetProfileFirstDayOfWeekAsync lane and resolved on the read path by the new WeekAnchor helper (a null / out-of-range value resolves to the Monday default, so the default is byte-identical to before). The preference shows up on the Date & time format settings page as a \"First day of week\" picker (the settings.dateformat_firstday.* keys × en/de/fr/da), reorders the days-of-week selectors on the Quiet-hours and Guardian time-limit surfaces (now with named day labels, not numerals, from the resident's week's start), and re-anchors the events calendar's week + month grids to your first day (the Monday default stays exactly as it was). A pure display preference — it never touches authorization or data (ADR 0169).",
        }),
        new("0.64.0", "2026-10-15", new List<string>
        {
            "Kiosk display mode (full page vs. full screen) — the /kiosk display is now true full-page (all page chrome — the top nav, the icon-rail + topbar (variant C), the onboarding banner, the what's-new toast, the footer — is hidden and the stage fills the browser viewport edge-to-edge, the ADR 0104 chrome-hide idiom extended to every chrome element the layout renders) and a new DisplayMode field on the KioskPlaylist (the ADR 0164 amendment, a per-resident display-only preference, never an access input, M35·4) lets the resident pick either Full page (fill the browser window, the default) or Full screen (the browser's Fullscreen API — the stage owns the whole monitor, the M35 \"shared physical display\" question for a wall-display TV): the KioskDisplayMode enum (FullPage | FullScreen) + the KioskPlaylist.DisplayMode field (default FullPage) + the IKioskService.SavePlaylistAsync displayMode parameter + the KioskController parse/persist/read-through (a blank / invalid value falls back to FullPage, a display preference is not a validation gate) + the /settings/kiosk display-mode picker (the kiosk.settings.display_mode.* kw-l key set × en/de/fr/da) + the /kiosk stage's data-display-mode attribute + the FullScreen-mode enter/exit-fullscreen buttons (the board's .kanban-fullscreen-toggle / calendar requestFullscreen precedent — a user gesture is required to enter, so the display degrades to FullPage where the gesture is unavailable) + the :fullscreen stage rules in the view's inline CSS; the KioskView / KioskViewKind / the two interval members / the 2-method seam shape / the one kiosk.save audit row are unchanged (the amendment's additive delta, the per-resident shape preserved).",
        }),
        new("0.63.0", "2026-10-15", new List<string>
        {
            "Kiosk display is now per-resident — the kiosk playlist editor has moved out of the admin area (was /admin/kiosk, GlobalAdmin-only) into Settings → Kiosk (/settings/kiosk), where any signed-in user configures their OWN /kiosk display: the KioskPlaylist is now one row per resident (keyed on the user's subject id, the LocaleSettings per-user shape, ADR 0004 §B.1) instead of a single shared GlobalAdmin singleton, so /kiosk shows the viewer's own playlist and a resident who has not set one sees the empty state; the [Authorize(Roles = GlobalAdmin)] gate is dropped in favour of the /settings/* any-signed-in-user convention, the kiosk.save audit row is now Via = Owner (the acting resident writes their own playlist, the timezone.set-default per-resident standing) and keyed on the resident, a Kiosk tab joins the /settings strip, and a Kiosk help page (nested under the Settings guide) documents the surface (the ADR 0164 amendment — the bounded context, doc shapes, 2-method seam, /kiosk display, kiosk.ts state machine, and closed kiosk.* key set are unchanged).",
        }),
        new("0.62.0", "2026-10-14", new List<string>
        {
            "Bookmark folders — your bookmarks are now organizable, the same way your notes already were: create a personal folder tree (root → child, bounded in depth), move a bookmark into a folder, and your /bookmarks list nests under the tree (an unfiled bucket for anything you haven't filed). A folder's deletion moves its bookmarks back to the top — a bookmark is never lost. A bookmark can only live in a folder you created (a bookmark is a personal pin, never an audience decision — a GlobalAdmin who is not you gets a non-leaky 404, and your own list read commits zero AccessAudit rows and runs zero CanSeeAsync passes — the ADR 0105 \"operator has no read standing\" precedent); zero new AccessAction / AccessVia / Decide() branch / adapter (C-M17·6); zero migrations for existing bookmarks (a new BookmarkFolder doc + a FolderId field on the existing BookmarkDocTypes surface, delta-detected, idempotent). A named lane settling the M17 \"bookmark folders\" §deferred question — not a milestone (ADR 0168).",
        }),
        new("0.61.0", "2026-10-14", new List<string>
        {
            "Personal notes — a private note you attach to any content entity (a post, group post, event, page, project, to-do, board, document, or person): the note is a body of your own writing (not a pointer — like M17 bookmarks but the notes are organizable), and you organize them the way you want: nest them into a personal folder tree (categories, bounded in depth) and label them with free author-set tags. Author-only — invisible to every other resident and to any GlobalAdmin who is not the author; a note whose target is gone degrades to a generic label with your own body still visible (the body is content you wrote, not a pointer to something else). The D3 write-lane check is the target's own frozen Read decision (one decision, the target's own row — never a note row); the D2 personal read of your own note list commits zero AccessAudit rows and runs zero CanSeeAsync passes over the note rows (the ADR 0105 \"operator has no read standing\" precedent); zero new AccessAction / AccessVia / Decide() branch / adapter (C-M37·1 — a note is a personal-by-id record, never an audience decision); M37 is the last milestone on the roadmap — there is no M38 to promote (the M36·10 / M35·11 \"last milestone, no successor\" framing, re-applied) (ADR 0167).",
        }),
        new("0.60.0", "2026-10-13", new List<string>
        {
            "Events ↔ boards — a Kanban board can now be linked to an event (the same association a to-do already had), and the event page shows those linked boards right beside the linked to-dos, so you can gather the checklists and plan boards that make an event happen in one place: a new additive EventId field + index on the board (a feed filter / association, never a gate — the M14 interlock's ADR 0115 discipline carried to the board surface) + two new IProjectService seams (the ListBoardsForEventAsync reverse read + the SetBoardEventAsync write, creator ∪ GlobalAdmin standing + the \"you can only link to an event you can see\" guard, both reusing the frozen read/authorization surface — zero new AccessAction / AccessVia / Decide() branch / adapter) + the board detail's \"Linked event\" chip + the ⋮ menu's \"Link to event…\" picker (a standalone set-event lane, the to-do's set-event shape verbatim) + the event detail's \"Linked boards\" section (the LinkedTodoRow sibling) + the closed board.event_link / board.link_to_event / board.event_hint / events.linked_boards kw-l key set × en/de/fr/da; additive-only, delta-detected, zero migration for existing boards.",
            "Add a to-do to this event — the event detail page now has a one-field inline composer right beside the linked boards: type a title and hit \"Add to-do\" and the platform creates the to-do and links it to the event in a single POST (a POST /events/{id}/todos lane that composes the frozen CreateTodoAsync + SetTodoEventAsync seams — the to-do inherits the event's audience-default + instance-default language, no new service surface, zero new AccessAction / Decide() branch) so a checklist item or prep task that belongs to an event can be captured without leaving the event page; the closed events.add_todo.title / hint / submit kw-l key set × en/de/fr/da.",
        }),
        new("0.59.0", "2026-10-13", new List<string>
        {
            "Onboarding walk-through — the resident (/onboarding) and admin (/admin/onboarding) onboarding pages are now a clean linear process: each step's \"go to this setting\" button opens the setting's page inside a modal (a same-origin iframe of the surface's own route, so the page's form, save lane, and anti-forgery token all work — the modal never re-implements a write lane, ADR 0132 D3 / ADR 0153·7 \"rides frozen lanes\"), with Previous / Next buttons to walk the steps in order and a \"Step X of Y\" counter so it's always clear where you are and how to proceed; the framed page drops its page chrome while iframed (a body class driven by the Sec-Fetch-Dest: iframe signal, the ADR 0104 / M35 chrome-hide idiom) and a fresh resident or admin who never touches the surface sees exactly the onboarding they see today (the ONB lane).",
        }),
        new("0.58.0", "2026-10-13", new List<string>
        {
            "@mentions — type a resident's directory display name after an @ in a post, reply, page, event, to-do comment, group post, announcement, or message and the platform resolves it against the resident directory, tells the mentioned resident on that surface (a mention notification kind per surface — the ADR 0076 D2 \"follow-on lane\" now wired: the reserved post.mention goes live on posts + replies, and six sibling kinds join the closed vocabulary for group posts, pages, events, to-do comments, announcements, and messages), and renders the name as a link to their directory page (a post-render pass over the frozen MarkdownRenderer — the one-renderer pin preserved): one new shared Core module Kumunita.Core.Mentions (the pure MentionTokens parser + the IMentionService read seam over the frozen IDirectoryService — the M34 \"derived, not captured\" shape) + an additive mention-emitter step in each surface's frozen write lane (signatures + audit rows untouched) + the MentionRenderer render pass + the closed mention kw-l key set (7 kinds × 4 keys) × en/de/fr/da; zero new bounded context / doc / EF migration (a mention is derived, not captured); zero new AccessAction / AccessVia / Decide() branch / adapter (a mention is a personal read); the author is never notified of their own mention and an ambiguous or blocked name is inert; detection runs on the publish lane, not draft-save; M36 is the last milestone on the roadmap (ADR 0165).",
        }),
        new("0.57.0", "2026-10-12", new List<string>
        {
            "Kiosk mode — a full-page / full-screen display at /kiosk that automatically cycles a GlobalAdmin-set preset list of views (boards, calendar, todos) on a set interval and refreshes each view's content on a separate, faster interval: a new bounded context Kumunita.Core.Kiosk (the KioskPlaylist singleton doc + the KioskDocTypes surface + the IKioskService seam + the KioskService impl — one kiosk.save audit row per GlobalAdmin write) + the /kiosk signed-in display (the chrome-hidden layout + the first slide) + the GET /kiosk/slide refresh fetch + the /admin/kiosk settings surface (the playlist editor + the interval pickers) + the client/lib/kiosk.ts auto-cycle/auto-refresh state machine (tsc-only) + the closed kiosk.* kw-l key set × en/de/fr/da; the frozen board / calendar / todos read seams + render partials are reused, not re-shape (ADR 0164).",
        }),
        new("0.56.0", "2026-10-12", new List<string>
        {
            "Home upcoming events & to-dos — a resident can now opt in (Settings → Home page) to seeing the next upcoming events and to-dos on the home page, just above the \"What's new\" feed: a new per-resident display preference (Profile.HomeShowUpcoming, ADR 0004 §B.1 additive — the HideHomeIntro / PageSize single-write-lane shape, off by default so a fresh account is unchanged) + a /home \"Upcoming\" section that reuses the existing /events + /projects/todos read seams (so a row only shows when the viewer could already see it there) + an optional \"only mine\" scope (Profile.HomeUpcomingMineOnly, off by default) that lists the resident's own events (RSVP'd or authored, EV-MINE) and to-dos (assigned to them) instead of the community-wide lists + a per-column \"show more\" that reveals rows beyond the first five (the mine-more / roadmap-more disclosure idiom) + the closed settings.home.* + home.upcoming.* kw-l key set × en/de/fr/da (ADR 0163).",
        }),
        new("0.55.0", "2026-10-11", new List<string>
        {
            "Analytics history — the M13 /admin/analytics surface gains a trend view over time (a derived read over the existing UsageEvent rows — one UsageDailyTotal per UTC day over a pinned window — + a per-day table + an inline sparkline over a pinned 7/30/90/180/365-day window) so an operator can see usage patterns evolve: the M13 Usage surface extended additively (the UsageDailyTotal + UsageHistoryResult DTOs + the GetHistoryAsync read seam + the closed analytics.trend.* kw-l key set × en/de/fr/da) (ADR 0162).",
        }),
        new("0.54.0", "2026-10-11", new List<string>
        {
            "Page translations for editors + platform-pages translation surface — the editor of a page can now add / edit / remove translations of their own page (the ADR 0040 standing matrix gains the author branch on blog pages, mirroring the edit standing — a system page's author is still an audit identity, not a standing branch, and a community Moderator still has no translation standing on either kind), and the /admin/platform platform pages table gains a per-page Translations ⋮ dropdown (the posts/announcements pattern) for the four Page-backed surfaces (about stays preview-only, ADR 0043 D1) — the shared _PageTranslationModals partial extracted from the page detail surface so both render the same add / edit / remove modals without duplication (the PageTranslationModalsModel view-model + the AdminPlatformViewModel.PlatformPage row + the BuildPlatformPageTranslationsAsync composition seam + the ILocalizationService optional ctor field on AdminController; the PageController.Show ⋮ dropdown is unchanged — same items, same pg id-prefix — only the standing now lets a blog author see them) (ADR 0161).",
        }),
        new("0.53.0", "2026-10-10", new List<string>
        {
            "Nested help guides — each tabbed surface's sub-pages now have a dedicated help page under the surface guide (Projects → To-dos and Boards; Events → Calendar; Settings → Time zone, Date format, Items per page, Home page, Quiet hours, Messaging, Change password; Group → Members and Settings; Admin → Accounts, Communities, Platform, Security, Analytics), so the help tree mirrors the app's own tab structure: the UG guide tree gains a second level (a closed GuideParentMap registry + 18 new nested guide bodies in the en floor + the root help page's guides list extended × en/de/fr/da); the existing 23 top-level guides are untouched (same slugs, same parent, same body), and their de/fr/da baselines are unchanged — the new guides ship the en floor; their localized versions are a Translator lane (ADR 0160).",
        }),
        new("0.52.0", "2026-10-10", new List<string>
        {
            "Escalation authorization + receiving inbox — a receiving GlobalAdmin generates labeled, individually-revocable tokens (shown once, hashed at rest) and hands them to origin platforms; an origin GlobalAdmin sets their endpoint + token at /admin/escalation; when they escalate, the receiving platform validates the token (SHA-256 compare) and only then accepts the issue as an 'escalated' row in the same /admin/error-reports list (a new 'escalated' chip + a local/received filter + the FromInstance on the detail view): the M32 ErrorReports surface extended additively (the EscalationToken + EscalationOutboundConfig docs + the IEscalationTokenService seam + the AcceptInboundAsync seam + the AccessVia.Escalation value + the POST /escalations/inbound public token-gated endpoint + the EscalationForwarder Bearer-token change + the closed escalation.* kw-l key set × en/de/fr/da) (ADR 0159).",
        }),
        new("0.51.0", "2026-10-10", new List<string>
        {
            "Surface label translations — a GlobalAdmin can now add / edit / remove translations of the 13 top-navigation surface labels from /admin/labels, and a resident reading in that language sees the translated labels in the nav and in each surface's page header: the LBL lane's ADR 0152 §D8 \"future LBL-2 translation lane\" (the SurfaceLabelTranslation row shape, one row per language on the SurfaceLabelsDocTypes surface) implemented on the ADR 0157 SITE-2 shape (the GetTranslationsAsync read seam + the Add / Update / RemoveTranslationAsync write lanes, GlobalAdmin-only standing, one surface_labels_translation.* AccessAudit row per write), with the per-language overlay on the existing GetLabelAsync resolver (translation → singleton → kw-l floor — the 14 nav + header views are untouched, so a rename stays consistent across the surface) — a fresh instance that never touches the surface looks exactly the same as it does today (ADR 0158).",
        }),
        new("0.50.0", "2026-10-10", new List<string>
        {
            "Site content hero translations — a GlobalAdmin can now add / edit / remove translations of the two landing surfaces' hero eyebrow + lead and the home hero's two supporting lines (home + about) from /admin/site — a language-chip preview that switches between the English default and each translation plus a ⋮ add / edit / remove menu (the posts/announcements pattern) — and a resident reading in that language sees the translated hero text: the SITE lane's ADR 0150 §D5 \"future SITE-2 translation lane\" (the SiteContentTranslation row shape, one row per language on the SiteContentDocTypes surface) implemented on the ADR 0022 / ADR 0048 post-translation shape (the GetTranslationsAsync read seam + the Add / Update / RemoveTranslationAsync write lanes, GlobalAdmin-only standing, one site.save-style AccessAudit row per write), with the hero render overlay on / and /about (a blank hero field falls back to the singleton's value, then to the shipped kw-l floor); the home hero's scope + FIG lines are also admin-editable + translatable (defaults byte-identical to the shipped kw-l text), and the /admin/site section toggles now save and re-display correctly — a fresh instance that never touches the surface looks exactly the same as it does today (ADR 0157).",
        }),
        new("0.49.0", "2026-10-10", new List<string>
        {
            "Storage metrics history — the M24 /admin/storage surface gains a trend view over time (a daily capture tick stores one StorageMetricsSample, a 365-day retention prunes it, and a per-day table + inline sparkline render a pinned 30/90/180/365-day window) so an operator can estimate whether the instance's capacity is sufficient in the future: the M24 Usage surface extended additively (the StorageMetricsSample doc + the StorageHistoryDocTypes surface + the GetHistoryAsync read seam + the StorageMetricsCaptureService tick + the closed storage.trend.* kw-l key set × en/de/fr/da) (ADR 0156).",
        }),
        new("0.48.0", "2026-10-09", new List<string>
        {
            "Issue submission & escalation — a resident submits an issue at /issues/new (anonymous-safe, not tied to an error page); a GlobalAdmin resolves it locally with a note or forwards it to a configurable escalation endpoint (the KUMUNITA_ESCALATION_ENDPOINT env var — a fork or multi-instance operator redirects where escalations land without a code change): the M31 ErrorReports surface extended additively (the resolved TriageStatus value + the Origin/ResolvedAt/ResolvedBy/ResolutionNote fields + the MarkResolvedAsync seam + the IEscalationForwarder Web-layer HTTP service + the closed issue.* / errorreport.resolve.* / errorreport.escalate.* kw-l key set × en/de/fr/da + the M30 onboarding step-7 route re-point) (ADR 0155).",
        }),
        new("0.47.0", "2026-10-09", new List<string>
        {
            "Production error handling — a report-an-issue form on the 500 error page (a resident says what went wrong; the admin triages it at /admin/error-reports): one new bounded context (ErrorReports), one new doc (ErrorReport), one new service (IErrorReportService), the closed errorreport.* kw-l key set × en/de/fr/da (ADR 0154).",
        }),
        new("0.46.0", "2026-10-08", new List<string>
        {
            "Admin onboarding — a guided walk-through for a new GlobalAdmin through the seven most important initial settings (community name, languages, moderation, notifications, storage limits, site content, issue escalation): a links-only walk-through on the M22 / SITE lane's shape (a singleton completion flag, not a new write path), and a fresh GlobalAdmin who never touches the surface sees the admin banner (the affordance, not a wall) (ADR 0153).",
        }),
        new("0.45.0", "2026-10-08", new List<string>
        {
            "Admin surface labels — a GlobalAdmin edits the display name of each top-navigation item and the related page header follows the choice, so a rename stays consistent across the surface: a label store on the SITE lane's shape (a per-item label, not a re-route), and a fresh instance that never touches the surface looks exactly the same as it does today (ADR 0152).",
        }),
        new("0.44.0", "2026-10-08", new List<string>
        {
            "A quiet pass over what we already ship — no new feature. We applied the platform's own integration principles to the code and docs: the four largest files are smaller, the ADR index no longer has missing rows, the two agent-instruction files no longer disagree, the long handoff notes and design docs now open with a 10-second summary, and every access decision is audited with who and why. The codebase is measurably smaller, the docs measurably more navigable, the seams measurably more tested — and nothing you already use has changed (the IMPROVE lane).",
        }),
        new("0.43.0", "2026-10-07", new List<string>
        {
            "Guardian time limits — a parent/guardian sets when the child may use the platform: a per-child allow/block schedule (hours × days of week, in the child's own time zone) that gates the child's whole-platform access — a sign-out, exactly like a suspension but windowed. The guardian sets it on the child's manage page, a GlobalAdmin can clear it, and the child cannot set or clear their own (ADR 0151).",
        }),
        new("0.42.0", "2026-10-07", new List<string>
        {
            "Site content customization — the landing surfaces' hero text is admin-editable + the sections are show/hide: a GlobalAdmin can now change what the home and about pages say (the hero eyebrow + lead) and choose which sections appear at all; the defaults are byte-identical to the shipped text, so a fresh instance that never touches the surface looks exactly the same as it does today (ADR 0150).",
        }),
        new("0.41.0", "2026-10-07", new List<string>
        {
            "Home, your way — if you've seen the intro enough, hide it: a new Home page setting turns off the two " +
                "intro sections (what Kumunita is and what it does) so the home page opens straight on what's new; " +
                "switch it off any time to see them again, and it's saved on your account only (ADR 0149).",
        }),
        new("0.40.0", "2026-10-07", new List<string>
        {
            "My data — you can now move your own content out of the platform and back in again: export the posts, replies, events, projects, documents, pages, messages and images you created into one portable archive (your account stays yours — no passwords or tokens ever travel with it), and when you bring that archive into a community where some of its references don't exist, you choose for each one whether to add it somewhere you belong or to discard it — nothing is merged on your behalf (ADR 0148).",
        }),
        new("0.39.0", "2026-10-06", new List<string>
        {
            "Sorting — feeds, lists, and search results now offer a sort control (posts, events, projects, announcements, documents, inventory, tags, people find, and search): choose what to sort by and the direction, carried through the pager links like M7's filters; an unchosen sort behaves exactly as before, and sorting never changes what you can see (ADR 0145).",
            "Child accounts are yours, not your parent's — when a guardian adds a child account they no longer set the child's password; the child sets their own when they open the confirmation link, and the guardian keeps their usual controls (suspend, memberships, invitations) without ever holding the credential (ADR 0146).",
        }),
        new("0.38.0", "2026-10-05", new List<string>
        {
            "Guardian's event gate — a guardian now decides a supervised child's event attendance: an event the child wants to attend reaches the guardian, who approves it, denies it, or sets it to auto-allow (three postures, the strictest the default); the child's own everyday RSVP is untouched (ADR 0144).",
        }),
        new("0.37.0", "2026-10-05", new List<string>
        {
            "Guardian deletes a child account — a guardian can now delete a supervised child's account (the sixth guardian supervisory action). It reuses the same deletion core as the self-serve / admin lane — pseudonymized audit, memberships and profile removed — and is audited under the guardian's standing (ADR 0143).",
        }),
        new("0.36.0", "2026-10-04", new List<string>
        {
            "Delete account — you can leave the platform (or a GlobalAdmin can remove a resident): the Identity account is deleted, the memberships and profile go with it, and the audit trail is pseudonymized so it stays an accountability record; the last GlobalAdmin can never be locked out, and a GlobalAdmin deleting themselves is gated (ADR 0142).",
        }),
        new("0.35.0", "2026-10-04", new List<string>
        {
            "Guardian approvals — a guardian now settles a supervised child's community membership (approve or decline, so an admin's add no longer lands unreviewed) and can decline a group invitation sent to the child; the invite notifications deep-link to the manage-child page where the buttons live (ADR 0141).",
        }),
        new("0.34.0", "2026-10-04", new List<string>
        {
            "Guardian community control — where \"removing a child from a community\" used to be a silent no-op for a mandatory community, a guardian can now block a supervised child's access to a community and hide it from them (a per-community restriction, not a membership change); the confusing add/remove-community forms on the manage-child view are gone (ADR 0140).",
            "Guardian consent, made real — adding a child to a guardian-managed account now requires explicit consent, and a newly assigned guardian must accept (with consent) or decline before the assignment takes effect (ADR 0038 §F).",
        }),
        new("0.33.0", "2026-10-04", new List<string>
        {
            "Messaging controls — you now decide for yourself whether you take 1:1 messages (an opt-in, under the instance's master toggle), and a guardian can place a ceiling on a supervised child's messaging; the ceiling always wins over the child's own opt-in (ADR 0139).",
        }),
        new("0.32.0", "2026-10-05", new List<string>
        {
            "Documents, organized — shared documents can now carry tags (the same tag vocabulary as posts, pages, events, and to-dos) and live in folders (a Page-style folder tree), so a community can group contracts, minutes, and notices without any new authorization model (ADR 0137).",
        }),
        new("0.31.0", "2026-10-05", new List<string>
        {
            "Per-community administration — a GlobalAdmin can now add and remove members and assign moderators within a single community from the /admin surface, reusing the existing membership and moderator lanes (ADR 0062).",
        }),
        new("0.30.0", "2026-10-04", new List<string>
        {
            "Change password — you can now change your own account password (current → new → confirm) from your account menu. After saving you're signed out and asked to sign in again with the new password (ADR 0138).",
            "Sample-data demo — on a demo instance (sample data enabled), the admin can lock the demo accounts out of changing their own password, so visitors can test features without breaking the shared credentials — the demo admin keeps its own password lane (ADR 0138).",
        }),
        new("0.29.0", "2026-10-04", new List<string>
        {
            "Platform storage limit — an operator can cap how much resident content the platform may hold (Media__MaxPlatformBytes, 0 = unlimited). When set, the GlobalAdmin's /admin/storage \"available\" figure is capped to the remaining budget, and all new uploads are blocked once that budget is spent or the volume's free space drops below 100 MiB (ADR 0136).",
            "Storage settings — the per-file size limit and per-user content quota on /admin/storage/settings are now entered in MiB instead of raw bytes (blank still means the platform default / unlimited).",
        }),
        new("0.28.0", "2026-10-03", new List<string>
        {
            "Upload limits — admin-set per-file size limit and per-user total content quota; residents see how much space they are using and how much of their quota remains (ADR 0135).",
        }),
        new("0.27.0", "2026-10-03", new List<string>
        {
            "Storage metrics — a GlobalAdmin view of the byte store at `/admin/storage`: how full the volume is, how much is left, how much is resident content, and who is storing the most (per user). It is a read-only snapshot — it writes nothing and audits nothing — and the per-user figures are the seam the upload-limits lane builds on (ADR 0134).",
        }),
        new("0.26.0", "2026-10-03", new List<string>
        {
            "Appearance — read the platform in a dark room: a dark theme (\"Forest\") now follows your device's appearance by default, and you can pin light or dark instead. The choice lives in the account menu (Appearance) and sticks per browser — a preference, not an account claim; pure CSS, so no first-paint flash and nothing the site's content-security policy would forbid (ADR 0133).",
        }),
        new("0.25.0", "2026-10-02", new List<string>
        {
            "Onboarding — a guided walk-through for your first sign-in: one page walks you through display name, avatar, interface language, time zone, date & time format, email & notification language, and contact details, each step linking into the setting that already owns it, and a dismissible reminder banner nudges you until you finish or skip (ADR 0132).",
        }),
        new("0.24.0", "2026-10-03", new List<string>
        {
            "Messaging, as a conversation — a 1:1 thread now reads as a chat: messages run oldest-to-newest, your own on the right and the other person's on the left, each with the sender's avatar; a \"Load earlier\" button pulls in older messages; and a message you send confirms with the other person's name (\"Sent to …\").",
        }),
        new("0.23.0", "2026-10-03", new List<string>
        {
            "Guides for every surface — every resident surface in the top navigation now has a how-to guide (announcements, directory, finding people, inventory, bookmarks, documents, pages, and tags, alongside the earlier guides), and the Help page now links all of them so it is the one place to start (ADR 0127).",
        }),
        new("0.22.0", "2026-10-03", new List<string>
        {
            "Attachment preview — the browser-displayable attachments (images, PDFs, plain text, and CSV) now open in a new tab and preview in your browser instead of forcing a download; Office files and zip archives still download. The preview is a separate tab — never embedded in the page — and every serve keeps the same stored type, the same nosniff lock, and the same one-row audit trail as a download (ADR 0126).",
        }),
        new("0.21.0", "2026-10-03", new List<string>
        {
            "Document editing — the owner of a document (its uploader) can update it: re-choose who may access it, and replace the file (the title and summary are editable too). Editing is owner-only — the uploader alone — and a document's identity and creation date are unchanged (ADR 0125).",
        }),
        new("0.20.0", "2026-10-02", new List<string>
        {
            "Search, expanded — the nav search box now finds across all ten resident content surfaces: the original four (posts, events, pages, announcements) plus projects, boards, to-dos, inventory items, documents, and people. A search now also matches the names of a post's, event's, page's, or to-do's own tags — a tag is a label, never a gate, so a tag-name match never widens what you can see, only what it finds. The people surface folds the directory's “who is here” into the same box (a search for a neighbour by name or bio word, signed-in only). Zero schema change (ADR 0124).",
        }),
        new("0.19.0", "2026-10-01", new List<string>
        {
            "Extended profiles — add a biography and your own tags (skills, interests, knowledge, expertise) to your profile, visible to the people your profile's visibility allows; find neighbours with something in common by tag or by a word in their bio — a match only surfaces people you can already see, and every such read is audited (ADR 0123).",
        }),
        new("0.18.0", "2026-10-01", new List<string>
        {
            "Documents — a shared repository for the community's official documents (contracts, minutes, notices, bylaws): each document is readable only by the people its uploader chose (an empty audience is a public denial), is served as a download (never rendered in the browser), and is audited every time it is opened; a GlobalAdmin or Moderator uploads a document and chooses who can see it (ADR 0122).",
        }),
        new("0.17.0", "2026-09-30", new List<string>
        {
            "Notification quiet hours — choose when your notification emails are held, in your own time zone: a per-resident schedule of quiet hours of day and days of week. Your emails are held (never lost) and arrive once the quiet window lifts, while your inbox always keeps the full record. An admin sets how often held notifications are re-checked (ADR 0121).",
        }),
        new("0.16.0", "2026-09-30", new List<string>
        {
            "Guest accounts — a GlobalAdmin settles a limited, time-bounded standing for a guest (a consultant, coach, teacher, speaker, or similar outside-the-resident-circle account): the bounded window the standing is live, and a closed set of read surfaces the guest may see (announcements, events, directory). A guest is never a full member — the standing is live only inside the window, settled by data, and read per request; one audited write lane, zero new authorization surface (ADR 0120).",
        }),
        new("0.15.0", "2026-09-30", new List<string>
        {
            "Recurring events — set up an event that repeats daily, weekly, monthly, or yearly (with an interval, and either a fixed number of occurrences or a last date). Each occurrence is its own concrete event, so RSVPs, reminders, the calendar, and the iCal feed all keep working per occurrence; the author can skip or restore a single occurrence without touching the rest of the series.",
        }),
        new("0.14.0", "2026-09-30", new List<string>
        {
            "Bookmarks — save the posts, events, to-dos, announcements, and pages you care about into one private list you can come back to: a one-click pin on each of those pages, your personal list grouped by type, and a pin that quietly greys out (never breaks, never leaks) when its target is removed.",
        }),
        new("0.13.0", "2026-09-29", new List<string>
        {
            "Inventory — check out and check in shared, community-owned, or private items (equipment, sports-team clothes, books, …): the item's page shows who has it now, and its usage history keeps a running record of every check-out — how much it's used and by whom.",
        }),
        new("0.12.0", "2026-09-29", new List<string>
        {
            "Translation bulk — check, update, and extend the platform's translations as a batch rather than one at a time: download the whole closed set as one CSV bundle, upload the edited bundle back (blank cells are skipped — they never erase a translation; a file with an unknown key or language is refused unchanged), and review or extend a new language with one save in the batch editor.",
        }),
        new("0.11.0", "2026-09-29", new List<string>
        {
            "Events & projects, linked — a to-do can now be for one event: the event page shows its linked to-dos, and each to-do shows its linked event with a picker to set it.",
            "To-dos on your calendar — dated to-dos can be added to a calendar app: an \"Add to calendar\" link on each to-do, plus a subscribable to-do calendar feed your calendar can keep up to date.",
        }),
        new("0.10.0", "2026-09-28", new List<string>
        {
            "Search — one /search surface (nav search box, anonymous + signed-in) over the four resident content surfaces: community + group posts, community + group events, pages, announcements.",
            "Messaging — 1:1 resident messaging: a signed-in resident opens a conversation with another resident, exchanges messages, and sees read state; an admin can enable or disable the feature instance-wide.",
            "PWA and responsive design — the site installs and behaves as an app on the device.",
            "Portability — import and export of the community's data for a self-hosted move.",
            "iCal — export your visible events to a calendar app: an \"Add to calendar\" link on each event, plus a subscribable \"Calendar feed (iCal)\" your calendar can keep up to date.",
            "Navigation layout — choose how the site's top navigation looks: a compact top row with a \"More\" menu (the default), or an icon rail beside a slim top bar; picked in the account menu, per browser.",
            "Navigation layout (top row) — in a narrow window the Community and Groups links now tuck into the \"More\" menu so the top row never needs a horizontal scrollbar; they pop back out when there is room.",
            "Logging and analytics — the operator's feedback loop: a dated file log sink (next to `docker logs`) and a GlobalAdmin-gated `/admin/analytics` surface with the surface-rank table, the distinct-account count over the last 7/30/90 days, and a CSV export.",
            "The \"What's new\" section on the About page and the version toast — you are reading this.",
        }),
        new("0.9.0", "2026-09-18", new List<string>
        {
            "Notifications — residents get notified about the things they opted into.",
            "Pagination and filtering across the longer lists.",
        }),
        new("0.8.0", "2026-09-14", new List<string>
        {
            "Projects — goals, tasks, and contributors the whole community can see and join.",
        }),
        new("0.7.0", "2026-09-12", new List<string>
        {
            "Events, RSVPs & reminders — post an event, RSVP to it, and get a day-before reminder email.",
            "Events calendar — a month-anchored overview of your visible events with prev/next/today navigation.",
            "Calendar day/week/month views — Day, Week (Monday-start, time-ruler), and Month views over the same authorized events.",
            "Calendar quick-create — the calendar defaults to the week view; a \"New event\" button and clicking an empty slot opens a quick-create modal.",
        }),
        new("0.6.0", "2026-09-09", new List<string>
        {
            "Pages — a hierarchical, audience-restricted, translatable knowledge tree for the community's durable content.",
            "User guides — resident-facing how-to guides in the help subtree of the page tree.",
            "Tags — free author-set subject labels on posts and pages; browse by tag, autocomplete as you type.",
        }),
        new("0.5.0", "2026-09-06", new List<string>
        {
            "Rich editor — a WYSIWYG split-view + Markdown-splice toolbar over the Markdown lane.",
            "Rich content — Markdown bodies + in-content images on posts, replies, announcements & static pages.",
            "Guardian controls — a parent adds a child's account and supervises it at the account level (suspend, communities/groups, invitation approval); no standing to read the child's private content.",
            "Guardian assignment — an existing guardian assigns a second guardian to a child's account.",
        }),
        new("0.4.0", "2026-09-03", new List<string>
        {
            "Multilingual, live UI — every in-scope view resolves per request; seeded en floor; key-managed admin editor; public language picker.",
            "Languages seeded — German & French ship enabled on first boot with complete UI baselines.",
            "System pages shipped — About, Terms, Help, Privacy, and Code of conduct ship complete in en/de/fr.",
            "Timezone — platform default + per-resident override; timestamps rendered in the effective zone.",
            "Date & time format — platform default + per-resident override + custom format.",
            "Translator role — delegate translation editing to non-admin residents.",
        }),
        new("0.3.0", "2026-08-30", new List<string>
        {
            "Posts & announcements — a community feed with group-scoped posts and a pinned-announcement lane.",
            "Moderation + reports — residents report content; moderators act within their scope.",
            "Group posts — the membership-scoped post channel inside a group.",
        }),
        new("0.2.0", "2026-08-28", new List<string>
        {
            "Directory of residents, profile visibility & group management — the first view of your neighbours and the groups they form.",
        }),
        new("0.1.0", "2026-08-25", new List<string>
        {
            "Identity, groups, delegation & the authorization model — accounts, sign-in, and the thin-token / fat-authorization core the platform is built on.",
        }),
    };

    public static Release Latest => All[0];
}
