# ADR 0037 — Draft mode: saved-but-not-published, author-only (community posts, group posts, announcements)

Status: Accepted
Date: 2026-09-18
Amends: none (additive). Builds on the frozen base of **0004 §B.1**
(additive-field schema evolution — a new `bool` column is detected and
added, no schema-file change), **0006** (the module boundary contracts and
the frozen `CanAsync` / `CanSeeAsync` signatures the gate composes with),
**0013** (the group-post membership lane), **0014 / 0016 / 0024** (the
author-only lane precedent — a pure `AuthorId == actorId` ordinal gate, no
role branch, and the no-audit-row convention for author's own content),
and **0036** (the community-visible audience decision a published post is
evaluated against).

## Context

A resident composes a post, group post, or announcement, and it is often
**not finished** when they click save — a first draft of a longer thought,
a title with a body they'll write tonight, an announcement with a
placeholder date. Before this lane the write path was binary: create meant
**public under the author's chosen audience**, full stop. To withhold work
in progress, a resident had to (a) hold it in a local editor and lose it on
a refresh, (b) target the audience to just themselves and remember to
re-target it later — which fights **ADR 0036** (the community-visible
default is the *right* answer for the common case, and a self-only target
is the *wrong* lever for "not ready yet"), or (c) save, realize it's
half-finished, and delete it.

None of those is the right shape. "Saved but not made public yet" is a
distinct state from "published to a tiny audience": it carries no audience
semantics at all (the author has not yet decided who sees it), it must be
**visible to the author and no one else**, and it must be the thing the
author can come back to, finish, and flip to public. The platform already
has the two halves it needs — the author-only gate (`ADR 0014/0016/0024`)
and the additive field convention (`ADR 0004 §B.1`). What it lacked was a
first-class **`IsDraft`** state on the three published-content documents and
the read-lane plumbing to keep it out of everyone's feed but the author's.

The standing question is how strong the visibility gate must be. The
natural first instinct is "a draft is only as private as a self-only
audience." That is the **wrong model**: a self-only audience still runs
through `CanAsync`, still produces an `AccessAudit` row, and — critically —
is *permeable to a `GlobalAdmin`* under any admin-visibility branch the
audience lane may grow. The requirement for a draft is stronger and
simpler: **the author, and no one else — not a moderator, not even a
`GlobalAdmin` who is not the author.** A draft is the author's private
scratchpad; it has not entered the public record at all, so none of the
public-record rules (audience evaluation, audit, admin override) apply to
it. This is exactly the **author-only, no-role-branch, no-audit-row** shape
of ADR 0014, extended from "edit" to "read a not-yet-published item."

## Decision

- **A draft is a first-class `IsDraft` state, additive, on all three
  published-content documents.** `Post` and `Announcement` each gain a
  `public bool IsDraft { get; set; }` (default `false`); the create-lane
  records `PostDraft` and `GroupPostDraft` each gain a trailing nullable
  `bool? IsDraft = null` so a caller that does not supply it produces a
  live item exactly as before (the `?? false` wiring at the create seam is
  the only place the null becomes a concrete value). Per ADR 0004 §B.1 this
  is a new nullable-by-default column the versioned schema detects and
  adds; no schema-file edit, no backfill (existing rows read `false`).

- **The three read surfaces keep drafts out of every feed.** Each list
  query adds a `!p.IsDraft` / `a.IsDraft == false` term alongside the
  existing `DeletedAt == null` gate: the community feed, the
  multi-component feed, the group feed (`PostService`), and both the
  visible-announcement list and the pinned-announcement list
  (`AnnouncementService`). A draft is, for feed purposes, as absent as a
  soft-deleted row — it never competes for an ordering slot and never
  surfaces under a pin.

- **The detail surface is author-only, and the gate is *before* the
  public-record decision.** `PostService.GetPostAsync`,
  `PostService.GetGroupPostAsync`, and `AnnouncementService.GetAsync` each
  load the document and, if `IsDraft` is set, branch on the **pure**
  `AuthorId == actorId` ordinal check (the ADR 0014 shape) *before* calling
  `CanAsync` / `CanSeeGroupAsync`. The author gets the item and its
  replies; everyone else — a member, a component moderator, and a
  `GlobalAdmin` alike — gets the not-found result (`Post: null` / `null`)
  with **no `AccessAudit` row**, because a private scratchpad is not a
  restricted public record to be audited against. A `null` actor (a
  signed-out visitor) is denied on the same branch. The gate is composed
  with, never inside, the frozen `AuthorizationService` seam (ADR 0006):
  the draft branch short-circuits before the authorization query runs.

- **Publishing is an author-only, idempotent state flip.** The new seams
  `PostService.PublishPostAsync(postId, actorId, session) → Task<Post>`
  and `AnnouncementService.PublishAsync(id, actorId, session) →
  Task<Announcement>` are the single publish lane. Their one hard gate is
  `AuthorId == actorId`; anything else throws
  `UnauthorizedAccessException` (a `GlobalAdmin` who is not the author is
  **denied** — publishing a draft is an authorship act, not an admin act).
  An empty id throws `ArgumentException`, a missing id throws
  `KeyNotFoundException`. The flip is **idempotent**: when the item is
  already live, the method is a no-op (it does not re-stamp `Modified` and
  does not write an audit row). Publishing an announcement deliberately
  does **not** touch `Pinned` — publishing is about *who may see it*, not
  *where it ranks*.

- **Editing a draft never publishes it.** The existing `Update*Async`
  seams are left untouched with respect to `IsDraft`: an author can revise
  a draft's title/body/audience/translations without it becoming visible,
  and only an explicit publish clears the flag. The save path and the
  publish path are two separate author acts.

- **A draft is discoverable to its author in one place.** The new
  `PostService.ListMyDraftsAsync(actorId)` and
  `AnnouncementService.ListMyDraftsAsync(actorId)` each return the actor's
  own live drafts (`IsDraft && AuthorId == actorId && DeletedAt == null`,
  newest first) and nothing else — not another person's draft, not a
  deleted draft. The Web surface `GET /my/drafts`
  (`MyDraftsController`) renders both lists on one page, resolving each
  post's lane label (its group's name in the group lane, its component's
  name in the community lane) and each row's return link to the matching
  detail route, so the author can jump back into whatever they left open.

- **The Web lane shapes match each surface's existing not-found posture.**
  Community post: `POST /posts/{id}/publish` → `ForbidResult` for a
  non-author (a 403, since the community lane already distinguishes
  "exists but not for you" from "doesn't exist"). Group post:
  `POST /groups/{id}/posts/{postId}/publish` → `NotFoundResult` (the group
  lane is fail-closed 404 for anything it can't confirm, the ADR 0013
  shape). Announcement: `POST /announcements/{id}/publish` →
  `ForbidResult` (non-author) / `NotFoundResult` (missing). All three map
  `UnauthorizedAccessException` and `KeyNotFoundException` from the core
  seam to the same results. Each composer (`Posts/New`, `Groups/New`,
  `Announcement/New`) gains a **Save as draft** checkbox that sets
  `IsDraft` on the create seam; each detail view (`Posts/Detail`,
  `Groups/PostDetail`, `Announcement/Detail`) shows a draft badge and the
  **Publish** action *inside the author's `IsAuthor` block* only, so a
  non-author sees neither.

- **The C# `&&`/`||` precedence is pinned in the announcement list.**
  `ListVisibleAsync` and `PinnedAsync` read
  `a.IsDraft == false && ((a.Scope == Public || a.Scope == Community) ||
  <targeted-branch>)`. The draft term is **left-most and `&&`-bound**, so
  a draft targeted at a community a `GlobalAdmin` belongs to is still
  excluded — the precedence is not an accident but the reason a
  community-targeted draft does not leak to an admin through the
  targeted branch. A regression to `a.IsDraft == false && a.Scope ==
  Public || ...` (dropping the inner parens) would let the `||` split the
  draft gate away from the scope clause.

## Consequences

- **A resident can stop and come back.** Work-in-progress on any of the
  three surfaces is durable and private: the author sees it (feed detail
  page, and the `/my/drafts` index), finishes it, and publishes. This
  replaces the three lossy workarounds (local editor, self-only target,
  delete-and-re-post) with one state.

- **Drafts are stronger-private than a self-only audience.** They are
  visible to **exactly one** person (the author) regardless of admin
  standing, produce **no audit row**, and are **not** subject to audience
  evaluation. A `GlobalAdmin` who is not the author cannot see a draft,
  cannot publish one, and cannot tell it exists — the same exclusivity
  ADR 0014/0016/0024 give the author over editing. This is *by design*
  stronger than the public-record rules; it is the correct trade for a
  "not made public yet" state.

- **The gate is composed with, not inside, the frozen authorization
  seam.** The draft branch is a plain ordinal check that runs before
  `CanAsync` / `CanSeeGroupAsync`, so it does not widen the ADR 0006
  boundary contract and cannot be reached through an admin-visibility
  branch. A future change to audience or admin rules cannot accidentally
  surface a draft.

- **Idempotent, no-op publishing** means the Web layer can safely redirect
  after publish without distinguishing "was a draft" from "already live";
  the core seam is the single source of the state flip.

- **Schema is additive and zero-backfill.** New `IsDraft` columns default
  `false`, so every existing post/announcement is a live item the moment
  the migration applies; no data move, no re-target.

- **Follow-on lane (own design doc + ADR, not this one):** a **delete
  draft** seam on the author (today a draft is removed via the existing
  author soft-delete lane, which already respects `IsDraft`). Not part of
  this lane's scope; kept out so publishing and de-publishing stay the
  two explicit author acts.

## Tests

Behavior is pinned at the **core** seam (the sealed, un-substitutable
`PostService` / `AnnouncementService` run against a real `postgres:18`
via Testcontainers), in `tests/Kumunita.Core.Tests/PostDraftModeTests.cs`:

- Feed exclusion for everyone — community feed, group feed, announcement
  list, and pinned list all omit a draft from the author, a member, and a
  `GlobalAdmin` alike, while a live item still surfaces to its audience.
- **`Draft_TargetedCommunity_NotLeakedToGlobalAdmin`** — the precedence
  pin: a community-targeted draft is absent even for the admin, asserting
  the `&&`/`||` shape of `ListVisibleAsync`.
- Detail author-only — `GetPostAsync`, `GetGroupPostAsync`, and
  `AnnouncementService.GetAsync` return the draft to the author and
  `null` to a non-author (incl. a `null` actor), with **no `AccessAudit`
  row** written (the ADR 0014/0016/0024 no-audit-row convention).
- Publish author-only / idempotent / throws — `PublishPostAsync` (both
  community and group lanes) and `AnnouncementService.PublishAsync` flip
  the flag for the author, deny a `GlobalAdmin` non-author
  (`UnauthorizedAccessException`), throw on a missing id
  (`KeyNotFoundException`), are idempotent on a live item (no re-stamp of
  `Modified`), and preserve `Pinned` on the announcement.
- `ListMyDraftsAsync` (both surfaces) returns **only** the actor's live
  drafts — not another actor's, not a deleted draft.

The Web thin-layer mapping is pinned in
`tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the
announcement lane is the interface-backed surface, substitutable via
`IAnnouncementService`): `Publish` maps draft+author → publish + redirect
to `Detail`, a non-author (even `GlobalAdmin`) → `ForbidResult` with no
write, `UnauthorizedAccessException` → `ForbidResult`,
`KeyNotFoundException` → `NotFoundResult`, anonymous → `UnauthorizedResult`;
`Detail` flags `IsAuthor && IsDraft` for the author and `NotFound` for a
non-author; `New` (POST) wires the `SaveAsDraft` checkbox onto the
`Announcement.IsDraft` create field (checked → draft, unchecked → live).

**Why there are no route-level Web tests for the post / group-post /
My-Drafts lanes:** `PostService` is a `public sealed class` that returns
`Marten.Linq.IMartenQueryable<T>` from `Query<T>()` — it cannot be faked
under NSubstitute, so the announcement lane (interface-backed) is the
substitutable Web surface. The post and group-post *behavior* (the exact
draft gates, publish seam, and list filtering this ADR pins) is exhaustively
covered at the core lane above; the Web post/group routes are thin
`Result`-mapping wrappers over those already-pinned seams, and the
`/my/drafts` index is a read-only projection of `ListMyDraftsAsync`. Pinning
the core lane is therefore the behavior pin; the thin Web mapping is
asserted on the interface-backed surface as the representative case.
