# ADR 0014 — Post edit lane: author-only

Status: Accepted
Date: 2026-09-12

## Context

M3 shipped the post **create** lane (`PostService.CreatePostAsync`): a resident
composes a title, body, and an audience, and the `Component` feed organizer is
fixed at creation time. But a post was **write-once** — a resident who mistyped
their body, or who later wanted to tighten the audience from "everyone" to a
single group, had no way to correct it. The only lever a non-author had over a
post was M3b's `HidePostAsync` / `RemovePostAsync` (a **Moderate**-gated
moderation action), and the only lever an author had after create was to
delete and re-post (orphaning any one-level replies and losing the original
`Created` timestamp and any access-audit history).

Two already-accepted decisions constrain what the edit lane may be:

- **ADR 0001-B** — the audience is the **author's choice, written verbatim**.
  There is no auto-augmentation and no platform-owned default; the author is
  the sole authority over *who* a post reaches.
- **The moderator's role over a post is moderation, not authorship.** ADR 0003
  and the M3b lane make a component moderator's lever `Hide` / `Remove` — a
  `Moderate` action audited against a **Moderate** decision — never
  *rewriting the author's words*.

The parallel decision in the group lane, **ADR 0009** (group description:
owner ∪ GlobalAdmin edit), is the closest analog — but it is the **wrong
precedent to copy here**. A group description is an **organizational** field
the *owner* curates on behalf of the group, so owner ∪ GlobalAdmin is the
right standing. A post is the **author's personal voice**; the standing that
may edit it should be the author, **full stop** — not the author's owner, not
a component moderator, not even a `GlobalAdmin` who is not the author.

## Decision

- **Post editing is author-only, with no moderator or admin branch.** The new
  seam `PostService.UpdatePostAsync(postId, actorId, title, body, audience,
  session)` is the **single** write lane. Its one hard gate is
  `post.AuthorId == actorId` (ordinal); anything else throws
  `UnauthorizedAccessException`. There is deliberately **no** role check on
  this lane: a component moderator, a `Moderator`, and a `GlobalAdmin` who is
  not the post's author are all denied. The author's lever over their own
  words is exclusive; the platform's lever over a post is moderation
  (Hide/Remove), not rewriting.

- **The editable surface is `Title`, `Body`, and `Audience` — and only those.**
  `ComponentId` is **immutable on the edit lane**: the feed organizer
  (Safety / Maintenance / Social / …) is a **creation-time** choice, and a
  re-targeting edit (moving a post between components) is out of scope for an
  author-edit. `AuthorId`, `Created`, `Status`, and `GroupId` are likewise
  never touched by this lane. A `GroupId`-non-empty post is a **group-lane**
  post (ADR 0013), whose create lane is the only authoring path and whose
  access unit is membership — it has no component feed organizer to edit.

- **The audience is written verbatim (ADR 0001-B).** The author re-chooses
  the audience on edit exactly as on create; the seam stores the `Audience`
  value passed in, byte-for-byte, with no auto-augmentation and no platform
  default. The Web layer's single deserialization site is unchanged —
  `AudienceEditorModel.BuildAudience` is the **one** audience deserializer
  (the M2 single-source pin, carried to the edit lane), and the new
  `AudienceEditorModel.FromAudience` is its exact inverse, seeding the "Who
  to grant to" picker with the post's current grants pre-checked.

- **`Modified` is stamped; `Created` is not.** On a successful edit the seam
  sets `post.Modified = DateTimeOffset.UtcNow` and leaves `Created` at the
  original publish time. A post that has never been edited has `Modified ==
  null`; after an edit it carries the last-edit time. Strong consistency
  (invariant C4): the new values are live on the **very next** `GetPostAsync`
  / feed render.

- **The failure shape is 403, not 404 — for both a missing post and a
  non-author.** A non-author (or a post id that does not exist) is a
  `Forbid()` **403** on both the `GET` and `POST` edit routes. A **404**
  would leak which post ids are real; a re-render of the form would leak the
  post's content to a non-author. A 403 tells the viewer nothing about either.
  This is the M3 U7 "403 on denied, not a blank page" pin, applied to a
  *write* lane. (The service still throws `KeyNotFoundException` for a truly
  missing id, and the controller retains a `NotFound()` catch for it, but the
  Web layer's up-front `post is null` check routes the reachable case to the
  403.)

- **Defense-in-depth: the Web layer is a shape gate, the service is the
  decision.** The `PostsController.Edit` actions re-check
  `post.AuthorId == actor` before rendering the form (so a non-author never
  sees the seeded Title/Body/Audience) **and** the service re-pins the same
  author-only gate at write time. This is the same "form is a shape, service
  is the gate" split the `AnnouncementController` edit lane uses — the route
  enforces nothing that the service does not also enforce.

## Consequences

- A post is finally a **live, correctable** artifact: a resident can fix a
  typo, refine the body, or tighten/widen the audience after publishing,
  without deleting (and orphaning replies to) and re-posting.
- `Post.Modified` becomes a live field (it was `null`-only before this lane) —
  the first surface that reads it is the edit round-trip; any future
  "edited" badge or audit trail has a field to read.
- The author-only standing is **deliberately narrower** than ADR 0009's
  owner ∪ GlobalAdmin: post text is the author's voice, so the author — and
  only the author — may edit it. This is the one place the platform's
  "GlobalAdmin can manage anything" story is **not** extended to a content
  field, because a GlobalAdmin is not the author.
- The **moderator's lever over a post is unchanged**: it is still
  Hide/Remove (a `Moderate` action, M3b). Editing a post's text is *not* a
  moderation action and gains no moderator branch here.
- A new Core seam `PostService.UpdatePostAsync` and seven `PostServiceTests`
  (author-allows + `Modified`-stamped, audience-verbatim round-trip,
  immutable-fields-untouched, non-author Member / GlobalAdmin /
  component-moderator all denied, missing id) pin the lane. No new verb
  enters the audit vocabulary: an author-edit is the author's own content
  change, not an access decision, so it writes **no** `AccessAudit` row (the
  seam touches only the `Post` document, unlike ADR 0009's group lane which
  appends a `group.update` row).
- Component re-targeting (moving a post between feed buckets) and group-post
  editing remain **out of scope** — each is a separate lane + decision if
  ever wanted.
