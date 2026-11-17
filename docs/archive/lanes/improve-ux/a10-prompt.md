---
id: a10-prompt
title: "Prompt: IMPROVE-UX A10 — acceptance notifies the inviter + the closed sign-up text matches reality"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0304-email-invitations.md
  - docs/adr/0076-notifications.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A10** — the **acceptance** moment of the invitation lane.
> A07 shipped the `Invitation` doc + `IInvitationService`, A08 the
> `?invite=` sign-up gate, A09 the `/admin/invitations` surface + the
> invitation email. A10 closes the loop on the *sending* side: when an
> invited person accepts, **the inviter gets notified**, and the closed
> sign-up page (`account.signup_closed_body`) **stops promising an action
> that does not exist yet** (resident self-invite — A11/A12). **Who is it
> for:** the next coding agent (fresh context). **When to read the rest:**
> when starting A10 (A07/A08/A09 are shipped; the acceptance path exists,
> it just never tells the inviter).

# Fresh-context prompt: IMPROVE-UX A10

You are continuing the Kumunita repository on `development` (HEAD
`efd82deb`, **A09 just landed** — `/admin/invitations` + `InviteAsync` +
`ListAsync` + the invitation email; **A08 before it** — the `?invite=`
sign-up gate + `ValidatePresentedAsync`/`AcceptPresentedAsync`; **A07
before that** — ADR 0304 + the `Invitation` doc + `IInvitationService`).
A10 is the **fourth unit** of the invitation lane (A07–A12). The plan card
(in `plan-improve-ux.md`, the A10 row):

> A10 — Acceptance notifies the inviter (+ admins per the sign-up notify
> setting); rewrite `account.signup_closed_body` to match reality.
> Read first: `IdentityService.cs:213` (the AccountSignup emit).
> **Done when: text no longer promises a missing action.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`src/Kumunita.Core/Identity/InvitationService.cs`** →
  **`AcceptAsync`** (≈line 168–225) and **`AcceptPresentedAsync`** (which
  just hashes the token and delegates to `AcceptAsync`) — the **one place
  acceptance happens**. Step 4 already writes the `invitation.accept`
  **audit** row using `invitation.InviterSubjectId` (≈line 220). That is
  where the A10 inviter notification belongs, **alongside the existing
  audit row**, in the same committed session. Note `InvitationService`'s
  ctor already takes four **optional** A09 params (`mailer`,
  `baseUrlReader`, `translationProvider`, `communityOptions`, all default
  `null` so the A07/A08 harness `new InvitationService(store, identity)`
  keeps compiling) — add the emitter the **same way** (an optional
  `INotificationEmitter?` defaulting to `null`; a `null` emitter skips the
  notify lane, so the existing harness stays green).
- **`src/Kumunita.Core/Identity/IdentityService.AccountOps.cs`** →
  **`EmitAccountNotificationAsync`** (≈line 579–636) — the **admin-lane
  emitter precedent**: best-effort (a throw is swallowed + logged — the
  account op must succeed even if the notify lane is down), gated by the
  instance `NotifyAdminsOnSignup` flag, fan-out to `GlobalAdmin`, the
  stable content-derived idempotency key
  `notification:{kind}:{accountId}:{adminId}`. **Copy this posture** for
  the inviter notify (best-effort, one key per recipient) — but the
  recipient is the **inviter** (`invitation.InviterSubjectId`), not the
  GlobalAdmin set.
- **`src/Kumunita.Core/Notifications/INotificationEmitter.cs`** → the
  **full** `EmitAsync(session, recipientId, kind, idempotencyKey, body,
  targetId?, linkPath?, acceptPath?, declinePath?, ct)` — the contract you
  call. `linkPath` is a same-origin relative path (the inbox renders it as
  a link; the email carries it BaseUrl-prefixed).
- **`src/Kumunita.Core/Notifications/NotificationKinds.cs`** — the **closed
  `Known` set** (ADR 0076 D2). You **add** a new kind constant
  (`InvitationAccepted`, e.g. `"invitation.accepted"`) and append it to the
  `Known` list (the resident-facing kinds cluster — the inviter is a
  resident, so it is **not** the admin-lane tail like `account.signup`).
  Do **not** reorder the existing constants; append at the resident-facing
  block.
- **`src/Kumunita.Core/Notifications/NotificationService.cs`** → the two
  `EmitAsync` overloads + how `linkPath`/body become the stored `Notification`
  (the dedup-by-key, the quiet-hours / suppression gates you inherit for
  free) + **`src/Kumunita.Core/Identity/IdentityService.cs`** ≈line 232 (the
  `EmitAccountNotificationAsync(..., NotificationKinds.AccountSignup, ...)`
  call on `RegisterAsync`) — the **admin notify that already fires on every
  registration, including an invitation-accept** (`AcceptAsync` calls
  `RegisterAsync` in step 3). This is the **"+ admins per the sign-up notify
  setting"** half of A10's card — it is **already wired**; A10 does **not**
  re-emit it (do not double-notify admins).
- **`src/Kumunita.Core/Localization/Keys/en/notifications.json`** — the
  `notifications.kind.*` inbox labels (e.g. `notifications.kind.account
  .signup`). A new kind needs a **label key in all four languages** (see
  the keys step below).
- **`src/Kumunita.Web/Views/Account/SignupClosed.cshtml`** (≈line 18) +
  **`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/account.json`** —
  the **`account.signup_closed_body`** text A10 rewrites. The current en
  floor promises *"…ask an administrator — **or a neighbour who can
  invite** — to send you an invitation link…"* — the *"or a neighbour who
  can invite"* clause is the **missing action** (resident self-invite is
  A11/A12, not built). The rewrite drops that clause so the page only
  promises what a09 actually ships (an admin-invited link).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (A09's commit `efd82deb` must be intact; you
  are on `development`).

**The one critical seam detail (get this right first).** Acceptance happens
in **`InvitationService.AcceptAsync` step 4** (the `AcceptPresentedAsync`
path funnels into it, so **one emission point covers both** the A07/A08
hash-based seam and the A08 presented-token seam). A10 adds **one
best-effort notification to the inviter** there — recipient
`invitation.InviterSubjectId`, a **new** `NotificationKinds.InvitationAccepted`
kind, a stable idempotency key
(`notification:invitation.accepted:{invitationId}` — the
`EmitAccountNotificationAsync` key shape), body = the accepted person's
display name (the `{displayName}` snippet the `AccountSignup` emitter uses),
and a `linkPath` to the inviter's list of invitations (A09's
`/admin/invitations` — **`// TODO ❓`** the correct link once A11/A12 give
residents their own invite surface; for A10 the admin list is the only one
that exists). Keep it **best-effort**: a missing emitter / an emit error is
swallowed + logged (the account op + the `invitation.accept` audit row are
the durable record; the inbox row is the nudge — the
`EmitAccountNotificationAsync` D5 posture). Do **not** change the
`AcceptAsync`/`AcceptPresentedAsync` **signatures** or the existing
`invitation.accept` audit row — you **add** the notification beside them.
Do **not** emit to the GlobalAdmin set here (the `account.signup` admin
notify already fires on `RegisterAsync` — re-emitting it would double-notify
admins on the invitation path).

The **work**:

1. **New notification kind (Core).** `NotificationKinds.InvitationAccepted`
   (e.g. `"invitation.accepted"`) + append to the `Known` list at the
   resident-facing block (with a `<summary>` naming the emitter +
   recipient + idempotency key + `LinkPath`, the A05 `report.queued`
   admin-lane summary as the closest template). It is a **resident-facing**
   kind (the inviter is a resident), so it is **not** gated by
   `NotifyAdminsOnSignup`.
2. **The inviter notification (Core).** `InvitationService.AcceptAsync`
   step 4: after the `invitation.accept` audit row, emit the
   `InvitationAccepted` notify to `invitation.InviterSubjectId` via an
   optional `INotificationEmitter?` ctor param (default `null`; a `null`
   emitter or emit failure is swallowed + logged — best-effort, the
   `EmitAccountNotificationAsync` posture). Same committed session. **`//
   TODO ❓`** the `linkPath` (A09's `/admin/invitations` now; the resident
   invite surface when A11/A12 land) and whether an inviter who is also a
   `GlobalAdmin` (A09's admin-invite case) getting **two** rows
   (`invitation.accepted` + the existing `account.signup`) is acceptable —
   I believe it is (two distinct kinds, two distinct meanings); flag it for
   the SEC review either way.
3. **The closed sign-up text (Web + keys).** Rewrite
   **`account.signup_closed_body`** (the `kw-l` floor in
   `Views/Account/SignupClosed.cshtml` + the `en` value in
   `Localization/Keys/en/account.json`, and re-translate **de/fr/da**
   machine-written, flagged for the parked native review) so it **only
   promises what exists**: the sign-up is invitation-only, and the way in
   is an **administrator's** invitation link (drop the *"or a neighbour who
   can invite"* clause — A11/A12). Keep `account.signup_closed_title` as-is
   unless the body change makes it read oddly.

**Keys (4 languages):**
- **`notifications.json`** (en + de/fr/da machine, flagged for the parked
  native review): `notifications.kind.invitation.accepted` (the inbox
  label). This is a **`kw-l` key rendered in a Razor view** (the inbox
  renders `notifications.kind.@n.Kind`), so it must land in
  `KnownTranslationKeys.EnValues` (fed by the embedded `en`
  `notifications.json`) or a resident sees the raw key — the
  `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` pin
  is the guard, but that pin **skips `@`-dynamic keys**, so the
  `C02_DataAsEmbeddedResourcesTests` **four-language parity** test is the
  real gate here: every new `en` key present in de/fr/da.
- **`account.json`** (en + de/fr/da): the rewritten
  `account.signup_closed_body`.
- ⚠️ **Do NOT use `inv.*`** for any of these — that is the **Inventory**
  namespace (`inv.detail.*` = "Inventory"); it is unrelated and
  closed-set-adjacent. `account.*`, `admin.*`, `email.*`,
  `notifications.*` are **not** closed-set pinned (only `about.*`, `bm.*`,
  `pn.*`, `sort.*` are — the `KnownTranslationKeys_ParityTests` /
  `BookmarksControllerTests` closed-set pins), so adding there needs no pin
  re-baseline — but **C02 four-language parity** must stay green.

**Tests:**
- **Core** (`InvitationServiceTests`, the A07/A08/A09 Testcontainers
  harness): (a) **acceptance emits one `invitation.accepted` notify to the
  inviter** (assert the stored `Notification`: `Kind =
  NotificationKinds.InvitationAccepted`, `RecipientId =` the inviter's
  subject id, body carries the accepted person's display name); (b) the
  notify is **best-effort** — a failing/unwired emitter does **not** throw
  and the `invitation.accept` audit row + the `Accepted` mark still commit
  (the D5 posture pin); (c) **idempotency** — a duplicate emission of the
  same `(invitation, inviter)` is a no-op (the stable-key pin, the
  `EmitAccountNotificationAsync` F10 shape); (d) the **A08 presented-token
  path** (`AcceptPresentedAsync`) emits the **same** single notify (the two
  paths must not diverge — the one-fact invariant). The A07/A08/A09 tests
  (hash + presented seam, the `ListAsync`/`InviteAsync` seams, the admin
  notify on `RegisterAsync`) stay green.
- **Web** (the direct-construction harness as the template): (e) the
  **closed sign-up text** no longer contains the *"or a neighbour who can
  invite"* clause (a view-source / key-floor pin on the
  `account.signup_closed_body` value, the `KwLRegistryConsistencyTests`
  view-text shape); (f) if you add a view, it renders the new
  `notifications.kind.invitation.accepted` label (not the raw key).

**Scope rules:**

- **Do not touch** the `Invitation` doc, `CoreDocTypes`, the eraser, or the
  A07/A08/A09 `CreateAsync`/`RevokeAsync`/`ValidateAsync`/`AcceptAsync`/
  `ValidatePresentedAsync`/`AcceptPresentedAsync`/`ListAsync`/`InviteAsync`
  **signatures or existing audit rows** — you **add** one best-effort
  inviter notify in `AcceptAsync` step 4 + one new `NotificationKinds`
  constant + a text rewrite. The A09 `/admin/invitations` surface is
  **already built** — A10 only *notifies* and *rewords*; do not re-wire it.
- **Do not start** A11 (the resident-invite admin setting + Core rule),
  A12 (the resident "Invite a neighbour" + the approval queue), A13, A14,
  or A15 — A10 is the **inviter acceptance notify + the closed-text
  rewrite** only. The `Done when` bar ("text no longer promises a missing
  action") is the only acceptance gate.
- **One commit per change** (the house rule): A10 is one `feat(ux):` commit
  (`feat(ux): notify the inviter on acceptance + the closed sign-up text
  matches reality (IMPROVE-UX A10)`). No new ADR is required — ADR 0304
  already covers the invitation lane and the accept path; A10 wires an
  existing notify pattern (ADR 0076 D2's kind vocabulary) + rewords a
  string. If you settle the `linkPath` / the two-rows-when-inviter-is-admin
  question as a decision, split a `docs(ux):` ADR-amendment commit first
  (the A07 precedent).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review). The **C02 four-language parity** test
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress — A10
  removes a *jargon/over-promise* string and adds a notification kind (not a
  tracked anti-pattern); confirm it before committing.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new A10 Core tests +
`InvitationServiceTests` (the A07/A08/A09 seams still intact) + the
`NotificationServiceTests` / a `NotificationKinds` closed-set pin (if one
exists) + the `C02_DataAsEmbeddedResourcesTests` four-language parity + the
`KwLRegistryConsistencyTests`. If a test fails, read the error, adjust,
continue — a failed test is a recoverable step, not a stopping point.

Then report: where you emitted the inviter notify (the `AcceptAsync` step,
the best-effort posture, the idempotency key, the `linkPath` you chose + the
`// TODO ❓` you flagged), the exact rewritten `account.signup_closed_body`
(en floor), the new `NotificationKinds` constant + its position in `Known`,
the new test class(es) + test count, the commit hash(es), and the A07/A08/A09
seams you confirmed unchanged (all `IInvitationService` contracts + the
existing `account.signup` admin notify). Do not commit unrelated work.
