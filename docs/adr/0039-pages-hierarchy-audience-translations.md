# ADR 0039 — Pages: a hierarchical, audience-restricted, translatable knowledge tree (absorbs the static-page lane)

Status: Accepted
Date: 2026-09-17
Amends: **0005** (the static-page lane — `LocalizedPage`, `GetPageAsync`,
`UpsertPageAsync`, `/terms`/`/help`/`/about` — is **absorbed** into the new
`Pages` context and retired in U07), **0018** (the authored-in language tag
carried onto `Page.LanguageCode`), **0022 / 0026 / 0029** (the user-added
translation row shape carried onto `PageTranslation`), **0027** (the
chip-swap display reused on the page post view), and **0037** (the draft
idiom carried onto `Page.IsDraft`). **Additive** on **0001-B** (the `Audience`
doc — *reused*, not extended), **0006** (the frozen `CanAsync` /
`CanSeeAsync` signatures — the new `PageToAuditableResource` adapter
plugs into them, no signature change), **0036** (the `Audience.Community`
branch — reused verbatim for community-scoped pages), **0025** (the
`MarkdownRenderer` + content-image idiom — the page body is rendered by the
*one* renderer and edited by the *one* `bindRichEditor`), and **0034** (the
attachment idiom — `Page.AttachmentIds`).

## Context

The platform ships **one** resident-facing surface for long-form, admin-
authored prose: the **static-page** lane (ADR 0005 A) — `LocalizedPage`
(flat slugs: `terms` / `help` / `about`), served by `StaticPagesController`
over `ITranslationProvider.GetPageAsync`, written by the admin/translator-
only `UpsertPageAsync` seam, **public by construction** (no audience, no
authorization, no `AccessAudit`), and editable in the WYSIWYG editor (ADR
0031 / 0033, the `bindRichEditor` surface — the static-page image lane is
complete, per `Views/Languages/PreviewPage.cshtml`). It is **flat** (no
hierarchy), **admin-only** (a community Moderator has no standing to add a
"how the platform works" page), and **not audience-restricted** (a page
cannot be kept private to a subset of residents).

The requirement is a **knowledge tree**:

1. **Seeded default pages** (home, about, terms & conditions) that ship with
   translations and are editable in the WYSIWYG editor — the existing lane
   already does the edit + the render; what it lacks is *translation rows*
   (it has a single `en`-only body) and *who can edit* (admin-only).
2. **Admin- and community-Moderator-authored pages** — a blogging surface for
   "how the platform works", "what's coming", longer resident prose. The
   existing lane's standing matrix (admin/translator only) cannot express a
   *community-Moderator* authoring lane.
3. **Audiences like community posts** — a page is **private** (a subset of
   users/groups), **community-visible** (all members of a component), or
   **public** (everyone). The existing lane has no audience at all — it is
   public by construction.
4. **A hierarchy** — pages nest like folders/files (`parent_id`), listed as
   a tree and rendered like blog posts. **Special root nodes** name *where a
   page appears in the UI* (`footer/community` for the about page,
   `help/account` for a change-password help page). The existing lane is
   flat — three slugs, no parent, no mount-point concept.
5. **Referenced from UGC** — a page's URL is a Markdown link
   `[label](/pages/{path})` in a post/reply/announcement body. This is
   **free**: `MarkdownRenderer` already renders `[label](url)` links and
   `IsSafeUrl` already accepts relative paths — **zero read-path change**
   (the RC / ATT idiom).

**The fork that determined this ADR:** absorb the existing lane into one
unified tree, or run a *parallel* `Page` context alongside `LocalizedPage`.
**Absorb was chosen** (user sign-off 2026-09-17): the requirement is *one*
tree ("about is in `footer/community`"), and a parallel lane would give two
editors, two renderers, two translation lanes, and a `/about` that is
special-cased forever. Absorbing makes `/about` an *ordinary page* with a
mount point. The absorb is **additive, not destructive** — the seeded pages
carry the *same* `en` floor a fresh instance has today, and the three
hard-coded routes keep resolving (now from the tree). The old `LocalizedPage`
surface is **retired last** (U07), after the new surface is proven — the
repo's "no destructive step until green" discipline.

## Decision

- **A new bounded context `Kumunita.Core.Pages`** — `Page` (the body/
  structure of a page) + `PageTranslation` (a user-added translation row,
  the ADR 0022/0026/0029 shape). The `Page` doc carries: `Id`, `ParentId?`
  (hierarchy — `null` = a root), `Slug` (unique per parent), `Title`,
  `Body` (Markdown, authored-in language), **`Audience?`** (the **existing**
  `Kumunita.Core.Authorization.Audience` — *not* a new scope; `null` =
  public, the frozen `Decide()` branch 5), `AuthorId`, `ComponentId?`
  (moderator scoping), `LanguageCode` (ADR 0018 authored-in tag),
  `MountPoint?` (a nullable string tag — where a page appears in the UI),
  `Created` / `Modified?`, `IsDraft` (ADR 0037 idiom), `ImageIds` (RC
  ADR 0025 idiom), `AttachmentIds` (ATT ADR 0034 idiom).
  `PageTranslation`: `Id`, `PageId`, `LanguageCode`, `Title?`, `Body`,
  `AuthorId`, `Created` — `(PageId, LanguageCode)` unique index (the
  `PostTranslation` business-key convention; an explicit short index name if
  the auto-derived name exceeds Postgres's 64-char `NAMEDATALEN` limit, the
  `ann_tr_uidx_ann_lang` precedent).

- **The audience is the post `Audience`, reused verbatim.** A page's
  `Audience` is **exactly** a post's `Audience` (ADR 0001-B / 0036):
  `null` = public (everyone, including unauthenticated — `Decide()` branch
  5, *unchanged*); non-null = `Mode` + `Grants` (users/groups) + the
  `Community` flag (ADR 0036). **No new `Audience` field**, **no new
  `AudienceMode`**, **no new `AccessVia` value** — the frozen
  `Decide()` algorithm already covers every standing a page needs
  (`Owner` / `Moderator` / `Community` / `Audience` / `Delegation` /
  `BreakGlass` / `Admin`). The **default** for a new page is **non-public,
  community-visible** (`Audience.Community = true` + a `ComponentId`, empty
  grants) — **consistent with posts** (ADR 0036 seeds posts community-visible
  by default). ~~Originally "pages default public (`Audience = null`) — the
  one place pages deliberately differ from posts"~~ — **reversed 2026-09-17**
  so pages match posts (a page is still meant to be *read*, and the author can
  opt a page into the public capability when they want it world-readable). This
  is a *per-surface composer choice*, not a change to the `Audience` doc.

- **The decision path is the frozen `IAuthorizationService`.** A new
  **`PageToAuditableResource`** adapter (mirrors
  `PostToAuditableResource` verbatim: `Id` / `Name` / `OwnerId = AuthorId`
  / `Audience` (**null allowed**) / `ComponentId` / `TargetKind = "page"`)
  presents a `Page` to the **frozen** `CanAsync(…Read…)` /
  `CanSeeAsync(…Read…)` seams. **No new `AccessAction`** (the existing
  `Read` / `Moderate` are enough), **no new authorization branch** (the
  adapter plugs into the *existing* `Decide()` algorithm — `PG` adds an
  *adapter*, not a *branch*), **no new `AccessVia`** (the audience /
  community / moderator / owner branches already cover the standing). The
  adapter is the **only** new authorization surface in this lane.

- **The hierarchy is a forest of `Page` docs, nested by `ParentId`, with a
  derived (not stored) path.** A page's canonical path is the chain of
  ancestor `Slug`s: `Page("/a/b/c")` ⇒ `a` (root) → `b` → `c`. The tree is a
  **forest** (any number of roots — `about`, `help`, `blog`,
  `footer/community`, …). There is no single mandatory root. A page is
  either a *folder* (has children, `Body` may be empty) or a *leaf* (has a
  `Body`), or both ("folder-with-index"). "Folder" is not a type, it is
  "has ≥1 child". The `(ParentId, Slug)` unique index enforces one page per
  slug per parent (the `GroupMembership` business-key convention). A
  **cycle-guard + depth-cap** (e.g. 8) is validated in the write lane
  (the `PageService`), not a schema constraint (Marten has no FK).
  **Renaming a page** rewrites the *derived* paths of its subtree (paths
  are not stored, so a `Slug` change is a single-column write). **References
  from posts break by path, not by id** — the same property the RC
  content-image lane has; acceptable for a knowledge tree.

- **Mount points are pages, not a second mechanism.** `footer/community` and
  `help/account` are ordinary pages in the tree **whose `MountPoint` field
  is set**. The UI slot (the `_Layout.cshtml` footer, the account-help
  panel) resolves "the page whose `MountPoint == 'footer/community'`" and
  renders / `<a href>`s it. `MountPoint` is a **display** concern (where to
  surface a link), **not** an access boundary — access is always the
  `Audience` + `CanAsync(Read)`. The `about` slot is `footer/community`; the
  account-change-password slot is `help/account`. This is what makes "the
  about page is in `footer/community`" expressible without a special case.

- **Translatable UGC row shape, reused verbatim.** A `PageTranslation` row
  (the ADR 0022/0026/0029 shape) carries a user-added translation of a page.
  The **authored-in** language is `Page.LanguageCode` (ADR 0018); the base
  body is authored in one language; the `PageTranslation` rows are
  **user-added** (never machine-translated — ADR 0005 C stands). **Standing**
  = GlobalAdmin ∪ Translator (ADR 0021/0022) **plus the community-Moderator
  lane for a community-scoped page** (`ComponentId` set and equal to a
  community the actor moderates — the ADR 0029 announcement lane, carried
  over). **Add-only** (ADR 0022); a wrong translation is corrected by the
  same standing adding the right one, the unique index preventing a
  duplicate. **Display** = the ADR 0027 chip-swap, reused verbatim: the
  authored-in variant is the default visible one, a chip per
  `PageTranslation`, click to swap. The page post view renders exactly like
  the post/announcement/reply detail translation surface. **No new display
  rule.**

- **The standing matrix** (enforced server-side in the `PageService`, the
  `AnnouncementService.CreateAsync` C3 pattern — the Web `[Authorize(Roles=…)]`
  is a convenience pre-gate, not the source of truth):

  | Action | Standing | `AccessVia` |
  |---|---|---|
  | **Create** a page | GlobalAdmin ∪ (community Moderator, `ComponentId` = a community they moderate) | `Admin` / `Moderator` |
  | **Edit** a page's body / audience / hierarchy | its `AuthorId` ∪ GlobalAdmin ∪ (a Moderator of its `ComponentId`) | `Owner` / `Admin` / `Moderator` |
  | **Add a translation** | GlobalAdmin ∪ Translator ∪ (community Moderator, community-scoped) | `Admin` / `Moderator` |
  | **Publish** a draft | its `AuthorId` only (ADR 0037 pin) | `Owner` |
  | **Move / rename / delete** | GlobalAdmin ∪ (a Moderator of its `ComponentId`) — *not* a plain author (a page is platform content, not a personal note) | `Admin` / `Moderator` |

  **Every write lane** stores its `AccessAudit` row in the caller's session
  (invariant C3, ADR 0006), `TargetKind = "page"`, `Action`
  `page.create` / `page.update` / `page.translation.add` / `page.publish` /
  `page.delete` / `page.move`.

- **Delete is a soft-delete** (`IsDeleted = true` flag, the ADR 0024
  author-lane shape), filtered in the read lanes (`CanSeeAsync` excludes
  `IsDeleted` pages) — so an accidental delete of a seeded page is
  recoverable and a page's children are not orphaned mid-tree. A hard
  delete (like announcements, ADR 0017) is a future lane if needed.

- **Reference-from-UGC is free.** A page is referenced from a
  post/reply/announcement body as a plain Markdown link
  `[About](/pages/about)`. `MarkdownRenderer` already renders
  `[label](url)` links and `IsSafeUrl` already accepts relative paths, so
  **the read path needs zero changes**. The lane *adds* the serving route
  `GET /pages/{path}` (the post view), which is itself audience-gated by
  `CanAsync(Read)` — so a link to a private page renders as a link in the
  (already-authorized) post, and *opening* it is a separate `Read` decision
  that can 403/404. No `ImageIds`-style population is needed (a page
  reference is a URL, not an in-body media id).

- **The Web surface** — `PageController` (`Kumunita.Web.Controllers`):
  `GET /pages` (the tree browse, `CanSeeAsync(Read)`-filtered),
  `GET /pages/{**path**}` (the post view — title + rendered body via the
  *one* `MarkdownRenderer` + the ADR 0027 chip-swap, `CanAsync(Read)`-gated,
  404 on absent, 403 on denied), `GET /pages/new` + `POST /pages/new`
  (compose — title, body, parent picker, the **`AudienceEditorModel`**
  verbatim, the ADR 0018 language picker — WYSIWYG via `bindRichEditor`),
  `GET /pages/{id}/edit` + `POST /pages/{id}/edit` (the edit lane),
  `POST /pages/{id}/publish` / `delete` / `move` (the standing-gated
  actions). **`StaticPagesController` becomes a thin reader over the
  tree** — `/terms`, `/help`, `/about` resolve to the seeded page at that
  path (or the existing `/about` product-story fallback when truly absent).
  The three hard-coded routes are **kept** (backward-compatible) but **read
  from the tree**, so there is one store, not two. **Mount points** — the
  layout partials resolve "the page mounted at `X`" and render / `<a href>`
  it. **The editor** reuses `Views/Languages/PreviewPage.cshtml`'s existing
  `bindRichEditor` + RC content-image + ATT attachment wiring — the composer
  is the *same* editor posts/announcements use, pointed at a `Page` body.

- **The data migration (absorb)** — (1) create the `Page` +
  `PageTranslation` tables (new doc types, ADR 0004 §B.1); (2) **seed the
  three default pages** (`about`, `terms`, `help`) as `Page` docs carrying
  the *current* `LocalizedPage` `en` bodies/titles, `Audience = null`
  (public), `MountPoint` set (`about` → `footer/community`),
  `LanguageCode = "en"` — **idempotent** (the seeder's existing first-boot
  idiom); (3) **keep `LocalizedPage` read-only + non-destructive for one
  release** (the old `GetPageAsync` / `UpsertPageAsync` seam still works
  against it, so the old `StaticPagesController` fallback is *available* if
  the new route misbehaves); then (4) **delete the `LocalizedPage` surface**
  in U07, once the new route is green. **No seed reset, no schema-file
  change** for the new *fields* — they are ADR 0004 §B.1 additive on the
  *new* `Page` doc (a clean create), and the migration above is the only
  data move.

- **The lane is `PG`** (a named lane, the `ML` / `GP` / `RC` / `RE`
  convention — a short ID, *not* a renumber). **M4 / M5 / M6 stay
  Events / Projects / Portability.** No roadmap letter moves. The `PG`
  entry is added to `Milestones.All` **after `RE`** (a `StatusPlanned`
  entry — the "single in-progress milestone" pin (M4) is unchanged; the
  test's `Ids` ordered list gains `"PG"` in the same commit). The README
  Roadmap gains the `PG` row in the same commit.

## Consequences

- **One page engine, one editor, one translation lane, one decision path** —
  the lane *unifies* instead of *duplicating*, which is the whole point of
  absorbing `LocalizedPage`. The "single page engine" (ADR 0005 A) now
  covers a tree, not three slugs.
- **The audience machinery is reused end-to-end**, so a page gets
  private/community/public visibility, the correct `AccessVia` audit tag,
  and delegation for free (the `Decide()` algorithm is unchanged; only the
  `PageToAuditableResource` adapter is new).
- **`/about`, `/terms`, `/help` keep working** (now backed by the tree) and
  become *editable, translatable, hierarchy-aware, audience-capable* — a
  strict superset of today.
- **The "mount point" concept is new and small** (one nullable string column
  + one resolver), and it is the mechanism that makes the
  "about is in `footer/community`" requirement expressible without a special
  case.
- **The only irreversible step is the absorb migration**, and it is sequenced
  *last* (U07), after the new surface is proven, with the old surface kept
  readable until then — the lowest-risk ordering the repo's
  "no destructive step until green" convention allows.
- **The default-audience now matches posts (amended 2026-09-17).** Posts
  default community-visible (ADR 0036); **pages now do too** (composer seeds
  `IsPublic = false`, `Audience.Community = true`, the first reachable
  `ComponentId`). ~~Previously "pages default public (`Audience = null`)"~~ —
  reversed to keep pages consistent with posts. The `Audience` doc is still
  *identical* on both surfaces; a page author who wants a **public** page sets
  `IsPublic = true` (audience `null`), and one who wants a specific grant set
  picks that community / users / groups.
- **The standing matrix is the existing role matrix re-checked server-side.**
  No new role, no new `AccessVia`, no new `AccessAction`. A community
  Moderator's standing on a page is the *same* `moderator:{CommunityId}`
  claim the announcement translation lane (ADR 0029) already uses — the
  new dimension is *which page*, not a new authorization kind.
- **Reference-from-UGC is free** — the RC / ATT idiom already renders
  `[label](/pages/{path})` links; the lane only adds the audience-gated
  serving route. A post body can link a page today, with no renderer change.
- **The `LocalizedPage` retirement is the last unit and the only destructive
  step.** It runs only after U01–U06 are green, and the old surface is kept
  readable until then. A fresh instance's `/about` / `/terms` / `/help`
  are byte-identical to today (the seeded pages carry the same `en`
  bodies), so nothing a resident can see is lost.

## Tests

- **Core (`Kumunita.Core.Tests`, DB-backed)** — `PageServiceTests`: the
  standing matrix (create/edit/translate standing — GlobalAdmin /
  community-Moderator / author / plain member), the `null`-audience-public
  branch, the `Community` branch (the `A0036_*` family shape, a `PG_*`
  family), the grants branch, the hierarchy cycle-guard + depth-cap, the
  `GetByPath` resolution, the translation standing (the ADR 0029 matrix),
  the soft-delete filter (a deleted page is absent from
  `GetTreeAsync` / `GetByPathAsync`), the publish author-only pin.
- **Web (`Kumunita.Web.Tests`)** — `PageControllerTests` (NSubstitute
  `IPageService`, no live Postgres — the `AnnouncementControllerTests`
  shape): the route map (`/pages`, `/pages/{path}`, `/about` → tree), the
  404-vs-403 split, the tree filter, the mount-point resolution, the
  composer/edit audience round-trip through the `AudienceEditorModel`.
- **Renderer/editor parity** — a page body with the same Markdown markers a
  post body uses renders identically (it does, by construction — one
  `MarkdownRenderer`) and the `bindRichEditor` binding is byte-identical to
  the post/announcement composer (it is — same
  `textarea[data-rich-editor]`).
- **`MilestonesTests.cs`** — the `Ids` ordered list gains `"PG"` (after
  `"RE"`, before `"M4"`); the single-in-progress pin (M4) is unchanged; the
  order + no-blank-title pins hold. The `Shipped_Milestones_Are_Marked_Done`
  test is *not* affected (PG is `StatusPlanned`, not `StatusDone`).
