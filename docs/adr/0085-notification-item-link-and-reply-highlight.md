---
id: adr-0085
title: "Notification item link + reply highlight: a same-origin `LinkPath` on the `Notification` doc (inbox renders it clickable; the email carries it **absolute**, the `VerificationOptions.BaseUrl` + relative-path precedent), and a `#reply-{id}` anchor + scroll-and-highlight on the post detail"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m12-ical-design.md, docs/design/adr-handoffs.md]
supersedes: [adr-0076]
superseded_by: []
updated: 2026-11-02
---

# ADR [0085](0085-notification-item-link-and-reply-highlight.md) — Notification item link + reply highlight: a same-origin `LinkPath` on the `Notification` doc (inbox renders it clickable; the email carries it **absolute**), and a `#reply-{id}` anchor + scroll-and-highlight on the post detail

Status: Accepted | Date: 2026-09-26

> **Abstract:** closes the two "nudge I can't act on" ergonomics gaps on the
> M6 notifications surface — (1) a stored **same-origin relative** `LinkPath`
> on the `Notification` doc (the inbox renders it a clickable "View" link; the
> email appends the **absolute** form, the `VerificationOptions.BaseUrl`
> precedent) and (2) a `#reply-{id}` anchor on the post detail + a
> scroll-and-highlight. Additive: one optional `Notification` field + one
> null-defaulted `EmitAsync` param; the six content/reply emitters wire their
> item's path; one new `kw-l` key. **For** a developer touching
> `EmitAsync`, the inbox / email render, or the post-detail reply anchor.
> **Read the rest** before changing the link storage (relative vs absolute),
> the six emitters, or the highlight mechanics. Amends + emitter table +
> highlight code in the [handoff
> note](../design/adr-handoffs.md#adr-0085--notification-item-link--reply-highlight-moved-detail).

## Context

The M6 notifications surface (ADR [0076](0076-notifications-inbox-and-recipient-email.md)) shipped the **shared awareness** arrow
— an inbox row + a best-effort email nudge for the content/reply events a
resident is involved in (`post.reply`, `group.post`, `community.post`,
`announcement`, `event.rsvp`, `page.child`). ADR [0083](0083-group-add-and-invite-notifications.md) added the
group-membership kinds and ADR [0084](0084-per-target-notification-subscriptions.md) the per-target subscriptions. But two
ergonomics gaps remained on exactly the "something happened to *my* content"
surface, and both are the difference between "a nudge" and "a nudge I can act
on without first opening the platform to find where to look":

1. **No link to the item.** When a resident gets a notification that "X
   replied to your post" / "a new post landed in your community" / "someone
   RSVP'd to your event," the inbox row and the email body say *what* happened
   (the kind badge + the localized subject/body + the UGC snippet) but not
   *where*. To act, the resident has to re-derive the item's address by hand.
   The reply-content part of that gap was already closed by ADR [0076](0076-notifications-inbox-and-recipient-email.md) (the UGC
   snippet is in the email). What is *not* in the inbox or the email is the
   **address** of the thing they should go to.
2. **No reply highlight.** Even if the resident knows the post, clicking into
   it lands them at the top — the reply they were notified about is down in
   the replies list, and on a long post they have to scan to find which reply
   was new. There is no anchor on a reply, and nothing that would scroll to
   and mark the one the notification pointed at.

Both are **owned by the content lane that fires the event** (the
`PostService` / `AnnouncementService` / `EventService` / `PageService` write
seams that already call `EmitAsync`) and the M6 read/display surface (the
inbox view + the email stage + the post-detail view) — not by a new bounded
context. The natural fix is: the emitter passes the item's **same-origin
relative path** to `EmitAsync` as a `linkPath`; the `Notification` doc stores
it in a new optional `LinkPath` field (the inbox renders it a clickable
relative "View" link); the email stage appends the **absolute** form (the
instance `BaseUrl` + the relative path — the `VerificationOptions.BaseUrl`
precedent) so the email — which is not same-origin — still works; and the
post-detail view gains an `id="reply-{id}"` anchor + an inline script that,
when the URL carries a `#reply-{id}` hash, scrolls that reply into view and
flashes a highlight.

This ADR executes that: the additive `LinkPath` field + the additive
`linkPath` param on `EmitAsync`, the six content/reply emitters wiring their
item's relative path, the inbox "View" link, the email absolute-link append,
the post-detail anchor + scroll/highlight, and the one new `kw-l` key.

## Decision

- **D1 — One additive field on the `Notification` doc: `LinkPath` (a
  same-origin relative path, or `null`).** A new `public string? LinkPath`
  (the `NotificationDocTypes` surface, additive on the ADR [0084](0084-per-target-notification-subscriptions.md) doc) holds the
  **same-origin relative** path of the item the notification is about — e.g.
  `/posts/{postId}#reply-{replyId}` for a reply, `/posts/{postId}` for a
  community post, `/groups/{groupId}/posts/{postId}` for a group post,
  `/announcements/{id}`, `/events/{id}`, `/pages/{a/b/c}` for a page. `null`
  for a kind with no item to point at (the non-content lanes). *Forbids:* an
  absolute URL in the stored field (the absolute form is derived at
  email-stage time from the instance `BaseUrl`, so a redeploy with a new base
  URL does not need a data migration), a `LinkPath` on a kind whose recipient
  cannot read the item (a link is never a substitute for the content lane's
  own audience decision — the resident clicking the link still goes through
  the normal Read gate), and any change the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B contract does not
  allow.
- **D2 — One additive optional parameter on the frozen `EmitAsync`:
  `linkPath` (default `null`), and the email carries it absolute.**
  `EmitAsync` gains a `string? linkPath = null` parameter, placed before
  `ct`. The main overload's required params (`session`, `recipientId`, `kind`,
  `idempotencyKey`, `body`) and return shape (`Task<Notification?>`) are
  **unchanged**; `targetId` and `linkPath` both default to `null`, so the
  existing 5-arg short overload and every existing emitter / test call site
  keep compiling and behaving identically. When a non-empty `linkPath` is
  supplied: (a) the stored `Notification.LinkPath` is set to it verbatim (the
  relative form, D1); (b) the email body is appended with a localized label +
  the **absolute** link — `BaseUrl.TrimEnd('/') + linkPath` — exactly the
  `IdentityService.VerificationLink` shape, resolved via the
  `notifications.view` key in the recipient's `Profile.EmailLanguage` (ADR
  0005 / 0061). The link is appended **only to the email** — the stored
  `Notification.Body` is left as the inbox's copy. A `null` / whitespace
  `linkPath` appends nothing. *Forbids:* storing an absolute URL in the doc
  (D1), appending the link to the stored `Body` (it would leak into the
  inbox), or a `linkPath` that is itself absolute (it would double the host).
- **D3 — The inbox renders `LinkPath` as a clickable "View" link.** In
  `Views/Notifications/Index.cshtml`, the plain-row branch gains a "View"
  link gated on `!string.IsNullOrWhiteSpace(n.LinkPath)`, rendered as
  `<a href="@n.LinkPath">` + a `kw-l key="notifications.view">View</kw-l>`
  label. A row whose kind has no `LinkPath` renders no link — the row shape is
  otherwise unchanged. *Forbids:* rendering a link for a `null` `LinkPath`,
  and a re-wrapping of the link in another `kw-l` (the link is a presentation
  value, not platform copy — the M·3 pin).
- **D4 — Six content/reply emitters wire their item's relative path.** The
  six emitters that already call `EmitAsync` for the content/reply surface
  pass the item's same-origin relative path as `linkPath` — `post.reply` →
  `/posts/{postId}#reply-{replyId}`, `community.post` → `/posts/{postId}`,
  `group.post` → `/groups/{groupId}/posts/{postId}`, `announcement` →
  `/announcements/{id}`, `event.rsvp` → `/events/{eventId}`, `page.child` →
  `/pages/{a/b/c}` (the full emitter / `linkPath` table is in the [handoff
  note](../design/adr-handoffs.md#d4--the-six-contentreply-emitters)). Each
  uses the id already in scope at the call site; the `post.reply` link carries
  the `#reply-{id}` fragment (D5). *Forbids:* an emitter passing an absolute
  URL (D2), a `linkPath` for a kind the resident cannot read (the audience
  gate still governs the click-through, D1), and an emitter that omits
  `linkPath` on a content/reply kind it otherwise has the id for (the gap this
  ADR closes is exactly "the id is in scope but the resident is not handed the
  address").
- **D5 — The post-detail reply gets an `id="reply-{id}"` anchor + a
  hash-match scroll-and-highlight.** `Views/Posts/Detail.cshtml` sets
  `id="reply-@r.Id"` on each reply `<li>` (the id the `post.reply` link's
  `#reply-{replyId}` fragment points at). A new self-hosted
  `@section Scripts` module (`client/lib/reply-highlight.ts`, tsc-built — the
  app's CSP is `script-src 'self'` with no 'unsafe-inline', OPS §10) reads
  `location.hash`; when it is a `#reply-{id}` anchor it finds the matching
  element, `scrollIntoView` it, and adds a `.reply-highlight` class (removed
  after 4 s so a re-click can re-trigger) with a `prefers-reduced-motion:
  reduce` fallback. *Forbids:* a server-rendered highlight that hardcodes a
  specific reply (the highlight is driven by the URL fragment the resident
  arrived with — so a "view" link to any reply works without a dedicated
  route), a highlight that persists (the 4 s removal + the fade keep it a
  *flash*, not a sticky state), and a highlight for a reply that does not
  exist (the script no-ops when `getElementById` returns null — the inbox
  never 404s on an old notification, and the detail page never throws on a
  stale hash).

## Consequences

- **A "nudge" becomes a "nudge I can act on."** The inbox row now carries a
  clickable "View" link to the item, and the email carries the same link in
  absolute form — the resident no longer has to re-derive the address by
  hand. The `post.reply` case lands *at the reply* (the anchor +
  scroll/highlight), not at the top of the post.
- **Frozen-surface, additive.** The `Notification` doc gains one optional
  field and `EmitAsync` one null-defaulted param — the existing 5-arg short
  overload and every existing emitter / test call site keep compiling and
  behaving identically (the ADR [0084](0084-per-target-notification-subscriptions.md) frozen-surface-amendment precedent). The
  three ADR [0084](0084-per-target-notification-subscriptions.md) emitters (`announcement` / `community.post` / `page.child`)
  inherit `LinkPath` for free, as do the three ADR [0076](0076-notifications-inbox-and-recipient-email.md) kinds
  (`post.reply` / `group.post` / `event.rsvp`).
- **Relative stored, absolute derived.** The stored `LinkPath` is
  same-origin relative (D1); the email appends the **absolute** form
  (`BaseUrl + linkPath`, D2) — the `VerificationOptions.BaseUrl` + relative-
  path precedent (ADR [0061](0061-email-and-notification-language.md)) reused, so a redeploy with a new base URL needs no
  data migration.
- **No new bounded context, no new `AccessAction`, no new `AccessVia`.** The
  six emitters are the existing content lanes' write seams; the inbox / email
  render are the existing M6 display surface; the post-detail anchor is the
  existing `PostService` read surface. A link is **never a substitute** for
  the content lane's own audience decision — the resident clicking the link
  still goes through the normal Read gate (the D1 pin).
- **One new `kw-l` key, all four languages.** `notifications.view` (en
  "View" / de "Ansehen" / fr "Voir" / da "Se") joins the closed
  `KnownTranslationKeys` registry; the `KnownTranslationKeys_ParityTests`
  invariant enforces the delta.
