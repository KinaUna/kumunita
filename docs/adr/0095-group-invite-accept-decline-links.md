# ADR 0095 — Group-invite accept / decline links (the group-invite notification is now actionable)

Status: Accepted
Date: 2026-10-06
Amends **ADR 0085** (the `LinkPath` item-link lane, whose D-lane list named "group add/invite" a **no-link** lane) and **ADR 0083** (the group-membership emitters, whose `group.invite` emission carried no link). This ADR turns the group-invite notification into an **actionable** one: the invitee can accept or decline directly from the **email** and the **inbox**, without first navigating to the group.

## Context

The ADR 0083 group-membership notification lane emits a `group.invite` row (the
inbox) + a best-effort email when the owner invites a resident to a group. The
ADR 0085 D-lane that listed the link-eligible kinds named "group add/invite" a
**no-link** lane — the notification body said only "You've been invited to
join the group {group name}", with no `LinkPath`. The invitee's path to acting
was: open the inbox (or the email), read the group name, then navigate to the
group and find the invitations card.

That is one extra hop that the product now wants removed. **The
group-invitation email and the inbox notification should each carry an
*Accept* link and a *Decline* link** the invitee can click straight through.
The accept/decline **resolve lane already exists** (the m2b self-lane, ADR
0007 / ADR 0008: `AcceptGroupInvitationAsync` / `DeclineGroupInvitationAsync`,
with the "only the actor, only a Pending row" Core wall + the C-M2b·3
`InvalidOperationException` invalid-transition shape, and the Web's POST
`AcceptInvitation` / `DeclineInvitation` self-lane gate). What's missing is the
**link-clickable surface**: an email link is a GET (it cannot POST with an
anti-forgery token, and the invitee is often not yet signed in when they click
it), and the inbox row has no accept/decline affordance.

The standing for the new surface is already settled by three precedents:

- **A GET one-time-link action** — the M1 `/account/verify?id=…` lane
  (`AccountController.Verify`): an `[AllowAnonymous]` GET that loads a row by
  its stable id, performs the action, and signs in. The invitee's
  accept/decline actions are the same shape, but `[Authorize]` (the
  self-lane gate *is* the authorization story — "the row must be in MY pending
  list", C-M2b·2 — never a link-token), so an unsigned-in invitee is bounced
  to sign-in by the controller's `[Authorize]` (the cookie
  `LoginPath`/`ReturnUrl` round-trip) and lands back signed in.
- **A stored relative path + an absolute email link** — ADR 0085's `LinkPath`
  + `VerificationOptions.BaseUrl` precedent: the inbox stores the same-origin
  relative path and renders it as a clickable link; the email appends the
  instance `BaseUrl` to make it absolute.
- **An additive frozen-surface field / param** — ADR 0084 / ADR 0085's
  null-defaulted doc field + `EmitAsync` param precedent: the frozen
  `EmitAsync` surface gains two optional params (default `null`) so every
  existing emitter and the existing 5-arg / 7-arg overloads keep compiling
  unchanged.

## Decision

The group-invite notification now carries **two action paths** — an accept and
a decline — stored **relative** on the `Notification` doc, appended to the
**email** as **absolute** links with localized labels, and rendered as
clickable buttons in the **inbox** and the `/groups` invitations card. The two
are **independent** (either may be absent); only the `group.invite` lane
supplies both.

**Core — the `Notification` doc (`M6DocTypes` surface):** two additive
nullable fields, `AcceptPath?` + `DeclinePath?` (same-origin relative paths,
the ADR 0085 `LinkPath` shape). Marten delta-detects them; **no explicit
index** (display/debug only, never a query gate — the `IdempotencyKey` index
is the only one this surface has). `null` = the kind isn't actionable (every
kind but `group.invite`, and every emitter that predates the fields).

**Core — `NotificationService.EmitAsync` (additive frozen-surface):** the
main 7-arg overload gains two trailing optional params, `acceptPath = null` +
`declinePath = null` (the CS1736 trailing-param idiom, ADR 0084/0085
precedent), placed **after** `linkPath` and **before** `ct`. The 5-arg and the
existing 7-arg call sites keep compiling. When supplied:

- the stored row keeps them **relative** (the inbox renders them as its own
  clickable buttons);
- the **email** appends each as an **absolute** link (instance `BaseUrl` +
  the relative path, the `VerificationOptions.BaseUrl` precedent) with its own
  localized label — `notifications.accept` / `notifications.decline`
  (`kw-l` keys, the `notifications.view` sibling pattern), each appended
  **only to the email** (the stored `Body` stays the inbox's localized text).

**Core — the emitter (`UserInfoService.InviteGroupMemberAsync`):** the
`group.invite` emission now passes
`acceptPath: /groups/{groupId}/invitations/accept` +
`declinePath: /groups/{groupId}/invitations/decline`. The idempotency key
(`notification:group.invite:{groupId}:{userId}`), the kind, the body (the
group name), and the dedup / re-invite behavior are **unchanged** (ADR 0083's
emission shape — this is an additive ADR 0095 lane, not a re-emission; a
re-invite still dedups on the same key, the ADR 0083 F10 pin).

**Web — `GroupsController` (two new GET actions):** the link-clickable forms
of the existing POST self-lane, `AcceptInvitationLink` +
`DeclineInvitationLink`:

- `GET /groups/{id}/invitations/accept` → the identical self-lane gate (the
  row must be in MY `GetPendingInvitationsForUserAsync` list, C-M2b·2) + the
  identical seam (`AcceptGroupInvitationAsync`) + the identical C-M2b·3 wall
  (`InvalidOperationException` → `TempData["error"]` + redirect `Index`); the
  happy path redirects into `Detail` (the invitee is now a member).
- `GET /groups/{id}/invitations/decline` → identical gate + seam + wall; the
  happy path redirects to `Index` (the invitee never becomes a member).
- **No** anti-forgery token (a GET must be link-clickable; the M1
  `Verify` GET precedent). **No** route collision with the POST self-lane
  (different verbs, same template — the POST forms remain a valid path too).
- An unauthenticated invitee (a fresh cookie after clicking the email link)
  is bounced to sign-in by the controller's `[Authorize]` and lands back here
  signed in.

**Web — the two views:**

- `Views/Notifications/Index.cshtml` — the inbox's plain-row branch renders
  `n.AcceptPath` / `n.DeclinePath` (each gated non-empty) as clickable
  `btn-sm` buttons (the `btn-primary` / `btn-outline-secondary` shape the
  `/groups` invitations card uses).
- `Views/Groups/Index.cshtml` — the "Your invitations" card's accept / decline
  buttons are converted from anti-forgery POST forms to **GET links** on the
  two new actions (the card now matches the email + inbox — one self-lane
  surface, all reachable by link-click). The `InvitationViewModel` 3-tuple
  (test-pinned) is **untouched**.

**Localization (ADR 0015 registry, parity preserved):** two new `kw-l` keys
× 4 languages — `notifications.accept` (en "Accept" / de "Annehmen" /
fr "Accepter" / da "Acceptér") + `notifications.decline` (en "Decline" /
de "Ablehnen" / fr "Refuser" / da "Afvis") — the `notifications.view` sibling
pattern, added to the closed `KnownTranslationKeys` set (the
`KnownTranslationKeys_ParityTests` AllKeys == EnValues.Keys + De/Fr
bidirectional-exact pins stay green).

**Tests:** the ADR 0083 `group.invite` pins now assert the stored row carries
both relative action paths; a BaseUrl-bound variant asserts the email carries
both as absolute links with the two localized labels; `NotificationServiceTests`
pins the new-overload behavior (both / one / neither); a new
`GroupsControllerInvitationLinkTests` pins the two GET actions' happy path,
the not-in-my-pending-list 404 (no seam call), the C-M2b·3 error mapping, and
the unauthenticated 401.

## Consequences

- **C1 — the group-invite email is actionable:** the invitee accepts or
  declines by clicking a link, the way the M1 verification email's
  one-time link already is — no extra "find the group" hop.
- **C2 — the inbox + the `/groups` card are actionable:** the same two
  self-lane GET actions back all three surfaces (email, inbox, card), so the
  invitee's decision lands the same way no matter where they start.
- **C3 — zero new authorization surface:** the self-lane gate (the row must be
  in MY pending list) + the Core's C-M2b·3 invalid-transition wall are
  unchanged; ADR 0007 / ADR 0008's "only the actor, only a Pending row"
  standing is the whole access story. No new `AccessAction` / `AccessVia` /
  adapter / `Decide()` branch.
- **C4 — the GU supervised-child wall (ADR 0028 §C) still applies:** a
  supervised child with an active `GuardianLink` is refused on the accept lane
  by `AcceptGroupInvitationAsync` → the C-M2b·3 `InvalidOperationException` →
  the Web's "That invitation is no longer pending." error message (the lane's
  existing refusal shape, never a 500).
- **C5 — additive, frozen-surface:** the 5-arg / 7-arg `EmitAsync` overloads
  and every existing emitter (the `group.added` lane, the content/reply
  lanes, the announcement / page / event lanes) keep compiling and behaving
  unchanged; only the `group.invite` lane now supplies the two new params.
- **C6 — ADR 0085's "group add/invite = no-link" clause is amended:** the
  `group.invite` lane now carries **action** links (accept / decline), which
  are distinct from ADR 0085's single `LinkPath` "View" link — the two are
  independent fields, and the `group.added` lane (a content notification, not
  an actionable one) stays a no-action lane.

## Follow-on

- The `group.added` lane (the owner adds a resident directly) is **not**
  made actionable in this ADR — there is no pending row for the added
  resident to resolve (the membership is already live), so there is nothing
  to accept / decline. It stays ADR 0083's content-only shape.
- A "you have N pending invitations" inbox badge (the ADR 0094 no-notification
  follow-on) is a separate lane; this ADR only makes the *existing*
  group-invite notification actionable.
- The two new GET actions are not idempotent-token-carrying (a click is a
  GET, not a CSRF surface); the self-lane gate + the C-M2b·3 wall are the
  integrity story (a stale click on a resolved row is a 404 or an error
  message, never a wrong state).
