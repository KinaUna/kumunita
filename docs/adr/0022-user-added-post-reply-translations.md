# ADR 0022 — User-added post & reply translations (the lane ADR 0018 deferred)

Status: Accepted
Date: 2026-09-17

## Context

ADR 0018 (UGC authored-in language tag) deliberately scoped out a **separate
feature**: a human authoring a *translation* of an existing post or reply into
another supported language. ADR 0005 §C likewise keeps out of scope "translating
user-generated content (posts, replies, announcements) — a separate, later
feature." ADR 0021 added a `Translator` role, but that role reaches the *platform
surface* only (the UI strings and static pages) — it does not touch UGC, and
it should not: a neighbor translating a friend's post into Polish needs none of
the control-plane standing.

For a small neighborhood the practical shape is the opposite of a control-plane
role. The people best placed to write a post in a second language are often the
ones most affected by it — the author themselves (who knows their own meaning),
or, for a community post, a moderator who can carry a neighbor's intent into the
community's other spoken language. This is add-only, per-post, and cheap to
audit, and it fits the single-neighborhood trust model the rest of the repo is
built on.

## Decision

- **Two new documents, one per parent kind**, in `Kumunita.Core.Posts`:
  - `PostTranslation` — `PostId`, `LanguageCode`, optional `Title?`, `Body`,
    `AuthorId`, `Created`.
  - `ReplyTranslation` — `ReplyId`, `LanguageCode`, `Body`, `AuthorId`,
    `Created` (replies have no title).

  Both are registered in `M3DocTypes` with a `UniqueIndex` on
  `(parent id, LanguageCode)`, so a post/reply carries **at most one
  translation per language** — adding a translation for a language that
  already has one overwrites that row (same standing, same actor). No own
  `Audience`, no own moderation lane: the translation row **inherits the
  parent's single access decision** (C-M3·1), exactly as `PostReply` does.

- **Add-only lane.** There is no edit or remove of a translation row — the
  supported surface is "add a translation in a language that has none yet."
  (The unique index permits re-saving a language, but no separate "edit" or
  "delete" endpoint or service seam is offered; this matches the add-only
  shape of the feature as requested.)

- **Standing to add** is owned in Core (`PostService.CanAddTranslation` — a
  public display probe the Web uses to decide whether to show the form, and
  `ResolveTranslationStanding`, the private decision), per ADR 0006-D
  ("the service owns the decision, not the Web"):
  - **Community post / reply** — the **author** (of the post, or of the reply
    for reply translations), **∪ that community's Moderator** (a
    `moderator:<componentId>` claim), **∪ GlobalAdmin**.
  - **Group post / reply** — the **author** **∪ GlobalAdmin only**. A
    component-moderator claim does *not* qualify on a group lane — ADR 0007
    established that groups have no component-moderator scope, so a Moderator
    of the post's community cannot act on a group's post.
  - Standing is resolved from the row's own facts (lane, component, row
    author), never from a UI affordance — the form's visibility is a
    consequence of the same `CanAddTranslation` call, not an independent gate.

- **Reads inherit, writes audit.**
  - **Reads** (`GetPostTranslationsAsync`, `GetReplyTranslationsAsync`) are
    plain document reads with **no authorization gate and no audit row** — the
    translation row is only ever rendered on a page that already required the
    parent's `Read`, so a dedicated `Can`/audit would be a redundant second
    decision (C-M3·1, the `PostReply` precedent).
  - **Writes** (`AddPostTranslationAsync`, `AddReplyTranslationAsync`) each
    resolve standing (→ `UnauthorizedAccessException` when denied) and, on
    success, write **one hand-written `AccessAudit` row** in the same session,
    single `SaveChangesAsync` (C3):
    - `posttranslation.add`, `TargetKind = "post"`, `TargetId = <postId>`
    - `replytranslation.add`, `TargetKind = "reply"`, `TargetId = <replyId>`
    - `Via` = `Owner` (author), `Admin` (GlobalAdmin), or `Moderator`
      (community moderator); `ActorId` / `EffectivePrincipalId` = the actor.

- **Web surface — the read side shows what's there; the write side offers
  what's missing.** The post and reply detail views (community **and** group
  lanes) render:
  - the **available translations** as a chip per language, each expandable to
    its (optional) title + body — the "show which translations are available"
    half of the request;
  - for each **supported language lacking a translation row**, an "Add a
    `<language>` translation" form (optional title for posts, body for posts
    and replies) — shown **only when the actor has standing** for that
    lane (author / community-moderator / GlobalAdmin), i.e. the "an option to
    add a translation should be shown too" half, scoped exactly to the
    standing matrix above.
  - **Supported languages** are the *enabled* `LanguageCatalog` rows ordered
    by `SortOrder` (the same source `kumunita.enabled_languages` reads); a
    disabled or non-seeded language never appears as an option.

- **Announcements are explicitly out of scope** (as the request states, "each
  post and each reply"). The announcement edit lane (ADR 0017) is untouched.

## Consequences

Positive
- A post or reply can now carry a human-authored translation into any enabled
  language, visible to everyone who can already read the parent — no new
  audience, no new moderation lane, no machine-translation (ADR 0005 §C stays
  intact: this is *user-authored*, not *machine* translation).
- Standing is decided in Core and reused for both the decision and the UI
  affordance, so the "who can add" rule cannot drift between the form's
  visibility and the server's enforcement.
- Audit-by-default is preserved for writes (each add is a logged, attributed,
  standing-tagged row), while reads stay as cheap as the `PostReply` read they
  mirror.

Negative / accepted risks
- **Add-only** means there is no way to *correct* a bad translation after the
  fact other than overwriting the same language — and no way to remove one
  entirely. Accepted for the single-neighborhood trust model; a remove/correct
  lane is the natural next step if it's ever needed (see "Revisit when").
- A community **Moderator** can add a translation to *any* post/reply in their
  community, not just ones they would moderate. That is broader than the
  post-edit lane (author-only, ADR 0014/0016) — accepted, because a
  translation is a *new* artifact in a language the moderator speaks, not an
  edit of the author's text, and the row is attributed to the moderator's
  account on the audit line.
- The two new documents are a real schema delta. It is applied idempotently at
  boot by Marten (`ApplyAllConfiguredChanges`), the seeder is unaffected (no
  seed rows), and the new UI keys are added to the **closed**
  `KnownTranslationKeys` registry (the seeder materializes their `en` rows and
  the provider's floor covers `en`), so no new provider seam is needed.

## Revisit when

- A **remove** or **correct** lane is wanted for a bad translation — that is
  a new seam (an `UpdatePostTranslationAsync` / `DeletePostTranslationAsync`
  pair), not a change to this add-only shape.
- Translation of a post/reply is needed **beyond** the parent's audience
  (e.g. a translation is visible even where the original is not) — then the
  "inherits the parent's Read" invariant breaks and the row needs its own
  audience + moderation, which is a much larger change.
- **Announcement** translations are requested — that is the ADR 0017 lane
  extended, a separate decision.
