# ADR 0085 — Notification item link + reply highlight: a same-origin `LinkPath` on the `Notification` doc (inbox renders it clickable; the email carries it **absolute**, the `VerificationOptions.BaseUrl` + relative-path precedent), and a `#reply-{id}` anchor + scroll-and-highlight on the post detail

Status: Accepted
Date: 2026-09-26
Amends: **0076** (the M6 notifications context — this ADR **adds one optional
field** to the `Notification` doc (`LinkPath`, additive on the `M6DocTypes`
surface, the ADR 0084 `NotificationSubscription`-doc precedent) and **adds one
optional parameter** to the frozen `EmitAsync` writer (`linkPath`, default
`null`) — the same frozen-surface-amendment precedent ADR 0084 set for its
7-arg overload, and the ADR 0078 additive-gate precedent: the existing
signature's *required* params and return shape are unchanged, the new param is
additive and null-defaulted so every existing emitter and the existing test
call sites keep compiling and behaving), and **0084** (the per-target
subscriptions lane — the three `announcement` / `community.post` / `page.child`
emitters it wired inherit `LinkPath` for free in this ADR, as do the three
kinds ADR 0076 already wired (`post.reply` / `group.post` / `event.rsvp`));
**0015** (the closed `KnownTranslationKeys` registry — the one new UI key
`notifications.view` is added in all four languages, en floor "View" / de
"Ansehen" / fr "Voir" / da "Se"; the registry parity test enforces the delta),
and **0061** (the email-and-notification-language / outbound-channel ADR — the
absolute-link precedent this ADR reuses: the verification email's one-time
link is `BaseUrl + relative path` exactly as the notification email's view
link now is).
Additive on **0005** (the per-recipient language resolution — the new
`notifications.view` label resolves in the recipient's `Profile.EmailLanguage`
exactly as the other notification copy does) and **0039** (the page lane —
the `page.child` emitter's link is the page's `/pages/{a/b/c}` ancestor-slug
chain, the `GET /pages/{**path}` show route's address, derived by a new
`PageService.GetPathAsync` read seam that walks the `ParentId` chain).
`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` **untouched** (a
named lane within the M6 notifications surface, not a milestone — the ADR
0083 / 0084 named-lane precedent).

## Context

The M6 notifications surface (ADR 0076) shipped the **shared awareness** arrow
— an inbox row + a best-effort email nudge for the content/reply events a
resident is involved in (`post.reply`, `group.post`, `community.post`,
`announcement`, `event.rsvp`, `page.child`). ADR 0083 added the
group-membership kinds and ADR 0084 the per-target subscriptions. But two
ergonomics gaps remained on exactly the "something happened to *my* content"
surface, and both are the difference between "a nudge" and "a nudge I can act
on without first opening the platform to find where to look":

1. **No link to the item.** When a resident gets a notification that "X
   replied to your post" / "a new post landed in your community" / "someone
   RSVP'd to your event," the inbox row and the email body say *what* happened
   (the kind badge + the localized subject/body + the UGC snippet) but not
   *where*. To act, the resident has to re-derive the item's address by hand —
   open the post list, find the post, scroll to the reply. The reply-content
   part of that gap was already closed by ADR 0076 (the UGC snippet — the
   reply's / post's body — is in the email, so the resident can judge
   "do I need to look?" without a round trip). What is *not* in the inbox or
   the email is the **address** of the thing they should go to.

2. **No reply highlight.** Even if the resident knows the post, clicking into
   it lands them at the top — the reply they were notified about is down in
   the replies list, and on a long post they have to scan to find which reply
   was new. There is no anchor on a reply, and nothing that would scroll to
   and mark the one the notification pointed at.

Both are **owned by the content lane that fires the event** (the
`PostService` / `AnnouncementService` / `EventService` / `PageService` write
seams that already call `EmitAsync`) and the M6 read/display surface (the inbox
view + the email stage + the post-detail view) — not by a new bounded context.
The natural fix is:

- the emitter knows the item's **same-origin relative path** (it has the
  post/reply/announcement/event/page id in scope), so it passes that to
  `EmitAsync` as a `linkPath`;
- the `Notification` doc stores it in a new optional `LinkPath` field (the
  inbox renders it as a clickable "View" link — a *relative* link, since the
  inbox is same-origin);
- the email stage appends the **absolute** form (the instance `BaseUrl` + the
  relative path — the `VerificationOptions.BaseUrl` + `/account/verify`
  precedent the M1 verification email uses) to the email body, so the email —
  which is not same-origin and cannot resolve a relative path — still works;
- the post-detail view gains an `id="reply-{id}"` anchor on each reply, and an
  inline script that, when the URL carries a `#reply-{id}` hash (the fragment
  a reply-notification link sets), scrolls that reply into view and flashes a
  highlight.

This ADR executes that: the additive `LinkPath` field + the additive
`linkPath` param on `EmitAsync`, the six content/reply emitters wiring their
item's relative path, the inbox "View" link, the email absolute-link
append, the post-detail anchor + scroll/highlight, and the one new `kw-l` key.

## Decision

**D1 — One additive field on the `Notification` doc: `LinkPath` (a
same-origin relative path, or `null`).** A new `public string? LinkPath { get;
set; }` on the `Notification` doc (the `M6DocTypes` surface, additive on the
ADR 0084 doc). It holds the **same-origin relative** path of the item the
notification is about — e.g. `/posts/{postId}#reply-{replyId}` for a reply,
`/posts/{postId}` for a community post, `/groups/{groupId}/posts/{postId}`
for a group post, `/announcements/{id}`, `/events/{id}`, `/pages/{a/b/c}` for
a page. `null` for a kind with no item to point at (the non-content lanes —
reports, to-dos, group add/invite, the account lanes — and any emitter that
predates the field). *Forbids:* an absolute URL in the stored field (the
absolute form is derived at email-stage time from the instance `BaseUrl`, so a
redeploy with a new base URL does not need a data migration), a `LinkPath` on
a kind whose recipient cannot actually read the item (the recipient universe
is already the content lane's, but a link is never a substitute for the
content lane's own audience decision — the resident clicking the link still
goes through the normal Read gate), and any change to the field that the
M6/Martens schema-evolution contract (ADR 0004 §B) does not allow.

**D2 — One additive optional parameter on the frozen `EmitAsync`: `linkPath`
(default `null`), and the email carries it absolute.** `EmitAsync` gains a
`string? linkPath = null` parameter, placed before `ct`. The main overload's
required params (`session`, `recipientId`, `kind`, `idempotencyKey`, `body`)
and return shape (`Task<Notification?>`) are **unchanged**; `targetId` and
`linkPath` both default to `null`, so the existing 5-arg short overload and
every existing emitter / test call site keep compiling and behaving
identically (an emitter that passes no `linkPath` produces a `null`
`LinkPath` on the row and no link append on the email — the pre-0085
behavior). When a non-empty `linkPath` is supplied: (a) the stored
`Notification.LinkPath` is set to it verbatim (the relative form, D1); (b)
the email body is appended with a localized label + the **absolute** link —
`BaseUrl.TrimEnd('/') + linkPath` — exactly the `IdentityService.VerificationLink`
shape (`BaseUrl` + `/account/verify?id=…`), resolved via the
`notifications.view` key in the recipient's `Profile.EmailLanguage` (ADR
0005 / 0061). The link is appended **only to the email** — the stored
`Notification.Body` is left as the inbox's localized subject/body + UGC snippet
(the inbox renders `LinkPath` as its own clickable link, D3). A `null` /
whitespace `linkPath` appends nothing (the non-content lanes). *Forbids:*
storing an absolute URL in the doc (D1), appending the link to the stored
`Body` (it would leak into the inbox, where the relative `LinkPath` link is
rendered), or a `linkPath` that is not a same-origin relative path (the
absolute form is `BaseUrl + linkPath`; a `linkPath` that is itself absolute
would double the host).

**D3 — The inbox renders `LinkPath` as a clickable "View" link.** In
`Views/Notifications/Index.cshtml`, the plain-row branch (the one that renders
`n.Subject` + `n.Body`, as distinct from the `todo.assign` structured card
which already links to its to-do) gains a "View" link gated on
`!string.IsNullOrWhiteSpace(n.LinkPath)`, rendered as
`<a href="@n.LinkPath">` + a `kw-l key="notifications.view">View</kw-l>`
label. A row whose kind has no `LinkPath` (reports, to-dos, group add/invite,
account lanes) renders no link — the row shape is otherwise unchanged. *Forbids:*
rendering a link for a `null` `LinkPath`, and a re-wrapping of the link in
another `kw-l` (the link is a presentation value, not platform copy — the
`kw-l` TagHelper is platform-copy-only, the M·3 pin).

**D4 — Six content/reply emitters wire their item's relative path.** The six
emitters that already call `EmitAsync` for the content/reply surface pass the
item's same-origin relative path as `linkPath`:

| kind           | emitter                                  | `linkPath`                          |
|----------------|------------------------------------------|-------------------------------------|
| `post.reply`   | `PostService.CreateReplyAsync`           | `/posts/{postId}#reply-{replyId}`   |
| `community.post` | `PostService.CreatePostAsync`          | `/posts/{postId}`                   |
| `group.post`   | `PostService` (the group-post lane)      | `/groups/{groupId}/posts/{postId}`  |
| `announcement` | `AnnouncementService` (community + flat, create + edit) | `/announcements/{id}` |
| `event.rsvp`   | `EventService.RsvpAsync`                 | `/events/{eventId}`                 |
| `page.child`   | `PageService.CreateAsync`                | `/pages/{a/b/c}` (D5)              |

Each uses the id already in scope at the call site (`postId` / `reply.Id` /
`draft.GroupId` / `post.Id` / `announcement.Id` / `@event.Id` / `page.Id`). The
`post.reply` link carries the `#reply-{id}` fragment (D5); the others link to
the item itself. *Forbids:* an emitter passing an absolute URL (D2), a
`linkPath` for a kind the resident cannot read (the audience gate still
governs the click-through, D1), and an emitter that omits `linkPath` on a
content/reply kind it otherwise has the id for (the gap this ADR closes is
exactly "the id is in scope but the resident is not handed the address").

**D5 — The post-detail reply gets an `id="reply-{id}"` anchor + a
hash-match scroll-and-highlight.** `Views/Posts/Detail.cshtml` sets
`id="reply-@r.Id"` on each reply `<li>` (the id that the `post.reply` link's
`#reply-{replyId}` fragment points at). A new `@section Scripts` inline
module reads `location.hash`; when it is a `#reply-{id}` anchor, it finds the
matching element, `scrollIntoView({ behavior: "smooth", block: "center" })`
it, and adds a `.reply-highlight` class (removed after 4 s so a re-click can
re-trigger). The `.reply-highlight` CSS (in `wwwroot/css/site.css`) is a
tinted background + a soft primary outline that fades over ~1.5 s via a
`@keyframes`, with a `prefers-reduced-motion: reduce` fallback to a static
tint (the fade is gated; the color still lands). *Forbids:* a server-rendered
highlight that hardcodes a specific reply (the highlight is driven by the URL
fragment the resident arrived with — the same mechanism as the inbox link, so
a "view" link to any reply works without a dedicated route), a highlight that
persists (the 4 s removal + the fade keep it a *flash*, not a sticky state),
and a highlight for a reply that does not exist (the script no-ops when
`getElementById` returns null — the inbox never 404s on an old notification,
and the detail page never throws on a stale hash).

**D6 — One new `kw-l` key, `notifications.view`, in all four languages.** The
closed `KnownTranslationKeys` registry gains `["notifications.view"]` in the
`en` / `de` / `fr` / `da` blocks (en "View", de "Ansehen", fr "Voir", da "Se"),
adjacent to the other `notifications.*` keys. The inbox "View" link (D3) and
the email's link prefix (D2) both resolve it through the recipient's
`Profile.EmailLanguage` (ADR 0005 / 0061), `en` as the floor. The registry
parity test (enforced by `KwLRegistryConsistencyTests`) now requires the new
key in all four languages. *Forbids:* a language missing the key (the parity
test fails), and a re-use of an existing key with different meaning.

**D7 — A new `PageService.GetPathAsync(pageId)` read seam (the page link
path).** The `page.child` emitter's `linkPath` is the page's
`/pages/{a/b/c}` ancestor-slug chain (the `GET /pages/{**path}` show route's
address, ADR 0039 §3.3). `PageService` gains `GetPathAsync(pageId)` — walks the
`ParentId` chain up to the root collecting the slugs (the same `MaxDepth`-
derived cycle guard as `GetDepthAsync` / `EnsureNoCycleAsync`), reverses to
root-to-leaf order, and returns `/pages/` + the joined slugs (or `null` when
the chain cannot be resolved). The emitter calls it **once** before the
recipient loop (a single chain walk, not per recipient) and passes the result
as `linkPath`. *Forbids:* a page link that is not the full derived path (a
single-segment `/pages/{slug}` would 404 on any non-root page), a per-recipient
chain walk (the walk is a read, not a decision — compute it once), and a
`GetPathAsync` that changes the page lane's audience decision (it is a pure
read over the `ParentId` chain; the Read decision is the Web layer's, ADR
0006-D).

## Consequences

**Positive**

- The content/reply notifications now say *where*, not just *what*. A resident
  who gets "a reply was added to your post" sees a "View" link in the inbox
  (click → the post, the reply scrolled into view and flashed) and an absolute
  link in the email (click → the same). The UGC snippet (ADR 0076) still lets
  them judge "do I need to look?" before clicking — the link is the
  action, not the decision.
- The reply highlight closes the last mile: the resident lands on the reply
  they were told about, not the top of a long post. It is fragment-driven
  (D5), so the *same* mechanism works for any reply link and adds no new
  route.
- The stored `LinkPath` is relative and the absolute form is derived from the
  instance `BaseUrl` at email-stage time, so a redeploy that changes the base
  URL requires no data migration — the inbox link is same-origin (no base
  needed) and the email link is re-derived from the current `BaseUrl` on
  every emission.
- The frozen-surface amendment is the ADR 0078 / 0084 additive precedent: one
  optional field + one optional param, both null-defaulted; every existing
  emitter, the short `EmitAsync` overload, and the existing test call sites
  keep compiling and behaving. The non-content lanes (reports, to-dos, group
  add/invite, account) are untouched — they emit no `linkPath` and so store no
  `LinkPath` and append no link.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are untouched —
  this is a named lane within the M6 notifications surface (the ADR 0083 /
  0084 precedent), not a milestone.

**Negative / cost**

- One more field on the `Notification` doc and one more optional param on
  `EmitAsync` — a small surface growth, but the additive shape is the repo's
  established way to amend the frozen M6 writer.
- The post-detail page now carries an inline script + a CSS animation. The
  script is inert unless the URL carries a `#reply-{id}` hash (every other
  detail-page load is unchanged), and the CSS animation is gated on
  `prefers-reduced-motion`.
- The page link path requires a `ParentId` chain walk per `page.child`
  emission (D7) — a single read, bounded by `MaxDepth * 2`, on the write path
  of the `page.child` kind only (not on the more common post/reply kinds).

**Out of scope / follow-ons**

- Linking the non-content kinds (`report.*`, `todo.assign`, `group.added`,
  `group.invite`, `account.*`) to their items. The `todo.assign` inbox card
  already links to its to-do (ADR 0076's structured card); the other
  non-content kinds either have no single item to point at (the account
  lanes) or their "item" is the recipient's own settings/group (not the
  content the resident is acting on). If a follow-on wants a "view" link on
  any of these, it is a one-line `linkPath` on the existing emitter — this ADR
  deliberately does not reach for them. **Note:** ADR 0095 amends this
  clause for the `group.invite` kind — it makes the group-invite
  notification *actionable* by adding two **separate** action-path fields
  (`AcceptPath` / `DeclinePath`, the accept/decline link targets), which are
  **not** a `LinkPath` "View" link and do not change this ADR's single-`LinkPath`
  item-link contract. The other non-content kinds (including `group.added`)
  remain no-link under this clause.
- A "view" deep-link into the inbox itself (a `/notifications?open={id}`
  affordance that pre-opens a specific row). The inbox is a flat list; the
  "View" link on each row already carries the recipient to the item, which is
  the actual goal. A follow-on could add it, but it is not required for the
  stated gap.
- Translating the UGC snippet into the recipient's language. The snippet is
  the sender's authored-in content (ADR 0018) and is deliberately shown as-is
  (the resident sees what was said, in the language it was said in); this ADR
  does not change that.

**Tests**

- `NotificationServiceTests` gains two pinned tests: `Emit_WithLinkPath_…`
  (the stored row carries the relative `LinkPath` verbatim, and the staged
  email body contains the `BaseUrl` + relative-path absolute link, prefixed by
  the `notifications.view` label) and `Emit_WithoutLinkPath_…` (no `LinkPath`
  on the row, no link appended to the email). The existing 12 FACES are
  unchanged (an emitter that passes no `linkPath` is the pre-0085 behavior).
- The `KnownTranslationKeys` parity test now requires `notifications.view` in
  all four languages.
- The Web suite is unchanged on this surface (the inbox view + the post-detail
  anchor + the highlight are presentation, exercised by the existing view
  tests and by the manual `#reply-{id}` affordance); the full Web + Core
  suites are green.
