# ADR 0018 — UGC authored-in language tag (posts, replies, announcements)

Status: Accepted
Date: 2026-09-14

## Context

Announcements and community/group posts are user-generated content that is,
per ADR 0005 §C, **always rendered as authored — never translated**. Machine
translation of UGC is a *deferred* feature (a separate trust boundary, off by
default). But residents in a multilingual neighborhood write in more than one
language, and the platform is about to gain **search** (deferred, README →
Deferred). For either of those to work well, each piece of UGC should carry a
**concrete BCP-47 code identifying the language it was authored in** — so a
reader can tell which language a row is in, so a reader who wants a version in
their own language can *add* one (a separate, later feature), and so a future
search can scope to a language.

ADR 0005 is about **platform** text (UI strings, static pages) and is explicit
that it does *not* touch UGC. This ADR adds a small **metadata tag** to the
three UGC document shapes; it does **not** add any translation, display, or
machine-translation behavior. ADR 0005 §C is therefore **unchanged** — the tag
is data, not a translation step.

This is the natural follow-on to ADR 0013 (group posts), which already carried
a `GroupId` on the same `Post` document: a new, optional, additive field on an
existing document. It follows the **ADR 0004 §B.1 additive** pattern — a new
POCO field is delta-detected and idempotent, and no re-seed is required.

## Decision

- **Add a `LanguageCode` (BCP-47, e.g. `en`, `pl`) field to three
  documents:**
  - `Kumunita.Core.Posts.Post` (community posts *and* group posts — both are
    the same `Post` document, distinguished by `GroupId`)
  - `Kumunita.Core.Posts.PostReply` (a reply carries **its own** tag,
    independent of the parent post's — a reply may be in a different language
    than the thread it is in)
  - `Kumunita.Core.Announcements.Announcement`
- **Resolution at write time (never an empty row):** the author's chosen code
  → the instance default (`LocaleSettings.DefaultLanguageCode`) → the `en`
  floor. The choice is **materialized to a concrete BCP-47 code** by the
  service at create time (`PostService.ResolveLanguageCodeAsync`,
  `AnnouncementService.ResolveLanguageCodeAsync`), so no stored row is ever
  blank — this is what makes the field safe to use for search and for a
  future "add a translation" lane.
- **The author's choice is captured in the drafts** — `PostDraft.LanguageCode`
  and `GroupPostDraft.LanguageCode` (both `string?`, so the existing
  positional-construction test sites keep compiling). A null/empty draft value
  means "use the instance default."
- **Set at authoring time. Immutability splits by lane:**
  - **Posts: create-time tag, corrected on the author-only edit lanes
    (amended 2026-09-13, per owner request).** The ADR 0014 (community post
    edit) and ADR 0016 (group-post edit) seams now accept a
    `languageCode` argument and re-resolve it through the same
    `PostService.ResolveLanguageCodeAsync` helper — a non-empty submission is
    written verbatim (the author can correct the language the post was
    written in), and a blank submission re-materializes the instance default
    (never blanking a stored tag). The Web edit lanes
    (`PostsController.Edit`, `GroupsController.EditGroupPost`) seed the
    picker from the enabled catalog pre-selected to the post's stored tag,
    exactly like the announcement edit lane (ADR 0017).
  - **Replies: create-time only.** The ADR 0016 reply-edit seam
    (`UpdateReplyAsync`) stays body-only — the reply's tag is **not**
    editable there, consistent with the lane's existing "body only" rule
    (like the reply's `PostId` / `AuthorId`).
  - **Announcements: editable on the edit lane.** The announcement edit
    surface (ADR 0017) is *not* ADR-frozen to a narrower field set, so the tag
    is editable there. `AnnouncementService.UpdateAsync` re-resolves the value
    through the same default-resolution helper (so a no-op re-save that leaves
    the picker at the instance default does not blank a previously-stored
    concrete code) and includes it in the changed-detection.
- **Display / picker (Web layer):** the three compose surfaces
  (`Posts/New`, `Groups/New`, `Announcement/New` + `Announcement/Edit`), the
  two post edit surfaces (`Posts/Edit`, `Groups/Edit` — amended 2026-09-13),
  and the two reply forms (`Posts/Detail`, `Groups/PostDetail`) each get a
  `Language` `<select>` posting the author's choice. The options are the
  instance's **enabled** `LanguageCatalog` (ADR 0005 B), ordered by
  `SortOrder`. The picker is pre-selected to the stored row's tag on the
  edit lanes; on the create lanes and the reply/comment forms the
  pre-selection is the actor's **current effective language** (ADR 0049 —
  the `kumunita.locale` cookie → first enabled `Accept-Language` match →
  instance default → `en` floor, the same chain the `<kw-l>` TagHelper
  resolves UI strings through) — "write in the language you're reading in"
  — amended 2026-09-26, generalizing the earlier instance-default
  pre-selection. The highlighted option is the one that will be submitted.
  This is a **tag picker, not a translation** — the form help text says so
  explicitly.
- **New read seam:** `ILocalizationService.GetDefaultLanguageCodeAsync()`
  (a read, no audit row — same shape as `ListLanguagesAsync`) returns the
  instance default with the `en` floor, so the Web compose handlers can
  pre-select the picker without each re-implementing the singleton read.

## Consequences

Positive
- Every piece of UGC now carries a **concrete, non-null BCP-47 code** at rest,
  ready for (a) a future "add your own language version of this" feature and
  (b) a future **language-scoped search** — neither of which is in scope here,
  but neither requires a migration when it lands (the field is already there
  and already populated).
- Follows the established ADR 0004 §B.1 additive pattern: no re-seed, no
  breaking change; existing rows backfill to the instance default on their
  next write and read as the instance default until then.
- Keeps ADR 0005 §C's trust posture intact: **nothing is translated, nothing
  is displayed differently** — this is purely a metadata tag. The platform's
  "UGC is never machine-translated" invariant (the README → Deferred trust
  boundary) is untouched.

Negative / accepted risks
- A blank value on a **pre-ADR-0018 row** reads as "no tag" until that row is
  next written. This is acceptable: the field is additive and the resolution
  helper's `en` floor means any *read* that needs a concrete code can fall
  back to the instance default; there is no read path that today depends on
  `LanguageCode` being non-null (search / translations are deferred).
- Two different "default" code paths exist: the Core services resolve the
  default at **write** time (from `LocaleSettings`), and the Web layer
  pre-selects the picker from the same `LocaleSettings` at **read** time. They
  read the same row, so they agree; but they are two call sites that must keep
  the same `en` floor. (The `GetDefaultLanguageCodeAsync` seam is the single
  read path the Web uses; the Core services read `LocaleSettings` directly
  because they run inside a write session and already hold one.)

## Related

- **ADR 0005** — multilingual support (platform text; §C UGC "never
  translated" is *unchanged* by this ADR).
- **ADR 0004 §B.1** — additive schema-evolution pattern this field follows.
- **ADR 0013** — group posts (the `Post` document this tag rides on).
- **ADR 0014 / 0016** — post edit lanes this tag is **editable** on
  (amended 2026-09-13) and the reply edit lane it stays **frozen** on
  (body-only, create-time tag).
- **ADR 0017** — announcement edit lane this tag is **editable** on.
