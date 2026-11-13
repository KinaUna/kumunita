---
id: a14-prompt
title: "Prompt: IMPROVE-UX A14 — the resident's 'request more space' (a closed loop to the admin)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A14** — the resident's "request more space" loop. Today the
> "My storage" page tells a resident *"Ask an administrator to raise the
> quota if you need more"* and then offers **no path to ask** (E20 — a
> handoff, not a closed loop). A14 adds a "Request more space" button on
> `/account/storage` that notifies the **GlobalAdmins** with a link back to
> the resident's account (so they can act — A15, the per-resident quota
> override, is a separate, later unit). A14 is the **next unit in the
> Admin/moderator sequence** — A13 (the guest form's account picker) just
> closed and A14 has **no dependencies**. **Who is it for:** the next
> coding agent (fresh context). **When to read the rest:** when starting
> A14 (A13 has shipped; nothing about storage has changed yet).

# Fresh-context prompt: IMPROVE-UX A14

You are continuing the Kumunita repository on `development` (HEAD
`db0b5e57`, **A13 just closed** — the guest-standing form now uses an
account `<select>` instead of a free-text subject id; before it A12
shipped the resident "Invite a neighbour" surface + admin approval queue,
A11 the resident-invite setting, A10 the acceptance notify, A09 the
`/admin/invitations` surface, A08 the `?invite=` gate, A07 the `Invitation`
doc + `IInvitationService`). A14 is a **small, self-contained loop** with
**no dependencies** — it touches the "My storage" surface and the
notification seam, and it does **not** implement the quota override
(A15, a separate unit that depends on A14). The plan card (in
`plan-improve-ux.md`, the A14 row):

> A14 — "Request more space" on My storage → notification to GlobalAdmins
> with link to the account; rewrite `account.storage_remaining_note`.
> Read first: `Views/Account/Storage.cshtml`, `NotificationKinds.cs`.
> **Done when: request reaches admins.**

The problem it fixes: `Views/Account/Storage.cshtml` renders the
`account.storage_remaining_note` text — *"Your remaining is what's left of
your per-user quota ({0}). Ask an administrator to raise the quota if you
need more."* — but there is **no action** for the resident to actually ask.
The resident is told to do something the platform gives no seam for (E20,
the "promise without seam" anti-pattern). A14 adds the action: a "Request
more space" button that, on submit, **notifies every GlobalAdmin** with a
link to the resident's account detail (`/admin/accounts/{subjectId}`, the
surface A15 will add the override to), so the resident's request reaches
an admin *in-app* — no copy-paste, no "ask the admin" handoff. The
**quota override** (the admin actually raising it) is A15's; A14 only
sends the nudge.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`plan-improve-ux.md`** (this lane) — the **A14 card** + the
  "What this lane is" + DoD + the admin journeys (A14 is the
  "Admin — Resident needs more space" journey's resident half; A15 is the
  admin half that acts on it).
- **`src/Kumunita.Web/Views/Account/Storage.cshtml`** — the page to add
  the button to. The resident-facing "My storage" surface: the three
  stat cards (used / quota / remaining), the `@if (Model.QuotaUnlimited)`
  branch (the C-UP·5 sentinel: quota 0 ⇒ unlimited), and the
  `remainingNote` (`account.storage_remaining_note` — the text A14
  rewrites). A14 adds a **"Request more space" button** (a `POST` form
  with `@Html.AntiForgeryToken()`) in the **limited** branch (not in the
  unlimited branch — there is no cap to ask about) + the rewritten
  `remainingNote` + a "your request was sent" confirmation (the
  `TempData["info"]` / `FlashAsync` shape the other admin/resident forms
  use — see `AdminGuestsController.Save`'s `TempData["info"] =
  await this.FlashAsync(...)` idiom).
- **`src/Kumunita.Web/Controllers/AccountController.cs`** (the `Storage`
  action, ≈line 160–195) — the read that seeds the page (quota + usage via
  `IStorageSettingsService` from the request scope — the Verify idiom that
  keeps the controller constructor untouched for the harnesses). A14
  adds a **`POST /account/storage/request`** action (or a small
  `RequestMoreSpace` action) that calls the new Core notify lane (below)
  and redirects back with the `TempData["info"]` confirmation. The
  subject is `SubjectId(User)` (self-only — never a path param).
- **`src/Kumunita.Core/Notifications/INotificationEmitter.cs`** — the
  emission contract (the `EmitAsync` overloads). The full-shape overload
  (with `linkPath`) is what A14 uses — one row per GlobalAdmin,
  `linkPath = /admin/accounts/{subjectId}`.
- **`src/Kumunita.Core/Identity/IdentityService.AccountOps.cs`**
  (`EmitAccountNotificationAsync`, ≈line 596) — the **canonical
  "notify the GlobalAdmins" precedent** (ADR 0077): it reads the
  GlobalAdmins via `userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)`,
  emits one inbox row per admin (best-effort, the D5 posture — a throw is
  swallowed + logged, the domain write must not fail because the notify
  lane is unavailable), and skips when no GlobalAdmin exists (a fresh
  instance). A14's notify lane mirrors this shape — the per-admin
  idempotency-key `notification:{kind}:{subjectId}:{adminId}` (the F10
  shape), the best-effort catch, the no-admins no-op.
- **`src/Kumunita.Core/Identity/InvitationService.cs`** (`ApproveAsync`,
  ≈line 180–210) — the **most recent precedent** (A12, ADR 0304 D6 c) for
  a Core lane that emits a notification with a `linkPath` in the same
  committed session (the `EmitAsync` with `linkPath: "/account/invite"`,
  best-effort, the `notifications is not null` guard). A14's lane is the
  same shape, fanned out to the GlobalAdmins instead of one inviter.
- **`src/Kumunita.Core/Notifications/NotificationKinds.cs`** — the closed
  kind set. A14 adds **one new const** (`public const string
  StorageRequest = "storage.request";` — the admin-lane kind, the recipient
  is a GlobalAdmin, mirroring `AccountSignup` / `ReportQueued`) **and
  appends it to the `Known` list** (the admin-lane kinds trail — see the
  `Known` block's comment about `AccountSignup` / `AccountVerified`).
- **`tests/Kumunita.Web.Tests/NotificationsControllerTests.cs`** (≈line
  280–287) — the **closed-set pin**: `Assert.Equal(35, vm.AllKinds.Count)`.
  Adding a kind to `Known` makes this **36** — update the pin + the
  re-baseline-and-annotate comment (the same doctrine as the improve-check
  gate / the site.css media-block pin — the count rises, annotate the
  reason + the ADR/unit).
- **`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/notifications.json`**
  — the `notifications.kind.*` label keys (e.g. `notifications.kind.account
  .signup` ≈line 54). A14 adds **`notifications.kind.storage.request`** in
  all four languages (the settings-toggle label the closed set requires;
  en first, de/fr/da machine-written + flagged for the parked native
  review).
- **`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/account.json`** —
  the `account.storage_remaining_note` key (≈line 89 in en — the text A14
  rewrites to match the new button; *"…Ask an administrator to raise the
  quota if you need more"* → the button *is* that ask) + A14's new keys
  (`account.storage_request`, the button label; `account.storage_request_sent`,
  the confirmation) in all four languages.
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (A13's `db0b5e57` must be intact; you are
  on `development`).

**The one thing to get right.** A14 is a **new closed loop, not a new
capability** — it adds a *request* the resident can actually send (a
button + a Core notify lane that fans out to the GlobalAdmins with a link
back to the account), and it rewrites the "ask an administrator" text so
the promise and the seam match. The **quota override** (the admin raising
the limit, the per-resident `override ?? global` read) is **A15's** — A14
does not touch `IStorageSettingsService`, the quota check in Media, or
`AdminStorageController`. The notify is **best-effort** (the request
button still "succeeds" + flashes the confirmation even if the notify
lane throws — the ADR 0077 / A12 D5 posture; the durable record is the
request was made, the inbox row is the nudge). The emit is **fanned out to
every GlobalAdmin** (the `EmitAccountNotificationAsync` shape — one row
per admin, the per-admin idempotency key), with a `linkPath` that lands on
the admin's account-detail page (`/admin/accounts/{subjectId}` — the
surface A15 will extend with the override control).

The **work**:

1. **The Core notify lane (the seam).** A new method on
   **`IIdentityService`** (or a sibling seam — your call, but it must be
   NSubstitutable and best-effort):
   `Task RequestStorageQuotaIncreaseAsync(string subjectId,
   CancellationToken ct = default)`.
   - Reads the GlobalAdmins (the
     `EmitAccountNotificationAsync` `userManager
     .GetUsersInRoleAsync(Roles.GlobalAdmin)` shape); **no GlobalAdmin →
     no-op** (a fresh instance has nothing to notify).
   - For each GlobalAdmin, emits **one** inbox row via
     `INotificationEmitter.EmitAsync(session, recipientId: adminId, kind:
     NotificationKinds.StorageRequest, idempotencyKey:
     $"notification:storage.request:{subjectId}:{adminId}", body:
     <the resident's display name / email — a short, non-PII snippet>,
     linkPath: $"/admin/accounts/{subjectId}", ct)`. The `body` carries
     the requesting resident (so the admin knows *who* is asking) —
     resolve it via `IUserInfoService.GetProfileAsync(subjectId)` (the
     `DisplayName` / `Email`), not a raw id.
   - **Best-effort**: the whole emit is wrapped in a `try { ... } catch
     { logger.LogWarning(...); }` (the ADR 0077 D5 posture — the request
     "succeeded" is the durable fact; a notify-lane failure must not
     surface as an error to the resident).
   - **No audit row** (this is a notify, not an access decision — the
     A12 `ApproveAsync` notify is likewise not an audit row; the durable
     record is the request, not the notify).
2. **The Web action + view.** A new **`POST /account/storage/request`** on
   **`AccountController`** (self-only — `SubjectId(User)`),
   `[ValidateAntiForgeryToken]`, that calls the new Core lane and redirects
   back to `Storage` with `TempData["info"] = await this.FlashAsync(
   "account.storage_request_sent", "Your request was sent to the admins.")`.
   In `Views/Account/Storage.cshtml`, add the button **only in the
   limited branch** (not when `Model.QuotaUnlimited` — there is no cap to
   ask about): a small `<form method="post" action="/account/storage/request">`
   with `@Html.AntiForgeryToken()` + a `btn btn-outline-secondary` "Request
   more space" button. Render the `TempData["info"]` confirmation (the
   notice-strip / `@ViewData` / `TempData` idiom the other surfaces use).
   Rewrite the `remainingNote` text (the key below) so it no longer
   promises a missing action — the button *is* the ask.
3. **The new kind + closed set.** In
   **`NotificationKinds.cs`**: add `public const string
   StorageRequest = "storage.request";` (with a `/// <summary>` naming
   IMPROVE-UX A14 + the admin-lane recipient, mirroring the
   `AccountSignup` / `ReportQueued` comment style) **and** append it to
   the `Known` list (the admin-lane kinds trail — the `Known` block's
   existing comment explains this). **Update the closed-set pin** in
   **`NotificationsControllerTests`** (`35` → `36` + the
   re-baseline-and-annotate comment, the `improve-check` gate doctrine).
4. **Keys (4 languages).** Add **`notifications.kind.storage.request`** to
   `notifications.json` (en first, then de/fr/da machine-written + flagged
   for the parked native review) — the closed set requires a label for
   every kind in `Known`. Add **`account.storage_request`** (the button
   label) + **`account.storage_request_sent`** (the confirmation) to
   `account.json`, and **rewrite `account.storage_remaining_note`** to
   match the new button (the "ask an administrator" promise is now the
   button, not a dead-end). All four languages (en / de / fr / da) — the
   `C02_DataAsEmbeddedResourcesTests` four-language parity gate must stay
   green.

**Tests:**
- **Web** (a view-pin + a controller-call pin, the house "string pin, no
  TestServer" idiom of `BookmarksControllerTests` /
  `InventoryControllerTests`):
  (a) the "My storage" page **renders the "Request more space" button**
  (a `<form method="post" action="/account/storage/request">` + a button)
  **only in the limited branch** — read the `.cshtml` and assert the form
  is present, and that the unlimited branch (the
  `account.storage_unlimited_note` path) does **not** render the request
  form (a quota-0 / unlimited resident has no cap to ask about);
  (b) the `POST /account/storage/request` action **calls the new Core
  lane** (the seam is consulted with the resident's `subjectId`) and
  **redirects back to `Storage`** with the `TempData["info"]` confirmation
  (the `RedirectToActionResult` + `TempData` idiom — the
  `AdminGuestsController.Save` shape);
  (c) `account.storage_remaining_note` (en) **no longer promises a missing
  action** — the text now matches the button (a small string pin, the
  house convention).
- **Core** (if you put the lane on `IIdentityService`, a unit test): the
  notify lane **fans out one inbox row per GlobalAdmin** (the
  `EmitAccountNotificationAsync` F10 shape — the per-admin idempotency
  key, the `linkPath = /admin/accounts/{subjectId}`), **no-ops when there
  is no GlobalAdmin** (a fresh instance), and **swallows a notify-lane
  throw** (best-effort, the ADR 0077 D5 posture — the request still
  "succeeds").
- If you change the `Known` set, keep the
  `NotificationsControllerTests` closed-set pin (updated to 36) + the
  `C02_DataAsEmbeddedResourcesTests` four-language parity (the new
  `notifications.kind.storage.request` + `account.*` keys in all four
  languages) green.

**Scope rules:**

- **Do not implement A15** (the per-resident quota override on the admin
  account page, the `override ?? global` read in the quota check) — that
  is a **separate unit** that *depends on* A14. A14 only sends the
  request; A15 acts on it.
- **Do not change** `IStorageSettingsService` (the quota doc / the
  per-user usage read), the quota check in Media, or
  `AdminStorageController` — A14 is a *request loop*, not a *quota
  change*.
- **Do not add a new account-listing / admin-read seam** — the notify lane
  reuses the `EmitAccountNotificationAsync` GlobalAdmin-read shape
  (`userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)`) + the
  `IUserInfoService.GetProfileAsync` resident-resolve (the ADR 0077 /
  A12 precedent); no parallel read.
- **No ADR.** A14 makes no new decision (it adds a closed loop the plan
  card already decided on — D9's "request more space" item; the
  storage / quota model is unchanged), so it writes no ADR and amends
  none. Do not fabricate one.
- **One commit** (the house rule): `feat(ux): my storage — resident "request
  more space" notifies the GlobalAdmins (IMPROVE-UX A14)`.
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged
  for the parked native review). **C02 four-language parity**
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A14 closes a handoff (not a tracked anti-pattern); confirm it before
  committing.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a
  small set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.
  CodeCheck_Gate_Passes` — the IMPROVE-CODE Roslyn ratchet). Do **not**
  attribute these to A14 or try to fix them here; your A14 suite must be
  green.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new A14 storage
tests + the `NotificationsControllerTests` (the closed-set pin, now 36) +
`C02_DataAsEmbeddedResourcesTests` (four-language parity). If a test
fails, read the error, adjust, continue — a failed test is a recoverable
step, not a stopping point.

Then report: where the request button lives (the
`Views/Account/Storage.cshtml` form + the `AccountController` action) +
the Core notify lane you added (the seam + the GlobalAdmin fan-out you
reused — the `EmitAccountNotificationAsync` shape, not a new read), the
new kind (`storage.request`) + the closed-set pin you bumped (35 → 36),
the keys you updated/added (en/de/fr/da) and that C02 parity stayed
green, the new test(s) + count, the commit hash, and the quota-override
surface (A15) you confirmed you did **not** touch. Do not commit
unrelated work.
