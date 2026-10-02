# ADR 0124 — M8 expansion — Search: six extended surfaces + tag-name match

Status: Accepted
Date: 2026-09-26

## Context

ADR 0091 (M8) shipped a single `/search` surface over the **four** resident
content surfaces (posts, events, pages, announcements). Its D2 explicitly
scoped the surface to those four and named the rest "out: projects, todo
items, boards, goals, the directory, tags, groups as entities" — with the
note that "adding it later is a one-line predicate, not a redesign."

M5 (projects/boards/todos), M16 (inventory), M21 (documents), and M23
(extended profiles with bio + tags) have since shipped, making the search
surface an incomplete surface of the platform: a resident can search for a
post but not a project, a todo, or a document. The platform now has **ten**
resident content surfaces that a search box should cover.

Three design forks were resolved by the platform owner:

1. **Bookmarks** (ADR 0118) are **excluded**: a bookmark carries no title
   or metadata of its own (it references a post/event/page) — searching
   "bookmarks" would be searching the referenced document, which the
   original four surfaces already cover.
2. **Tag-name match**: the query matches "content that carries a tag"
   (a post/todo/profile whose `TagIds` include a `Tag` whose `Name`
   matches), not "tags as searchable entities." A tag is a label, never a
   gate (C-TG·1) — matching on a tag name is a feed organizer, never an
   access decision.
3. **Profile find merged into search**: the M23 "find people" surface
   (a standalone `/people?name=` box) is folded into the nav search box
   as a **"people"** surface. The hit's id is the `Profile.SubjectId`
   (the directory route's parameter); the detail link goes to
   `/directory/{subjectId}`.

## Decision

- **Six new surfaces (E1).** In addition to the original four (ADR 0091
  D2), `surface=all` and the paged single-surface view now cover:
  `projects`, `boards`, `todos`, `inventory`, `documents`, `people`.
  Each surface's candidate predicate is the **same canonical read
  predicate** the surface's own feed uses (C-M8·2 extended):
  <list type="table">
    <listitem>
      <b>Surface</b> | <b>Canonical predicate</b> | <b>Match fields</b> | <b>Tag-aware</b>
    </listitem>
    <listitem>
      projects | `!IsDeleted` | Title, Description | no
    </listitem>
    <listitem>
      boards | `!IsDeleted` | Title, Description | no
    </listitem>
    <listitem>
      todos | `!IsDeleted` | Title, Body | yes
    </listitem>
    <listitem>
      inventory | `!IsDeleted` | Name, Description | no
    </listitem>
    <listitem>
      documents | (no IsDeleted field) | Title, Summary | no
    </listitem>
    <listitem>
      people | `!Blocked` (directory predicate, M2) | DisplayName, Bio | yes
    </listitem>
  </list>

- **The original four tag-aware surfaces gain tag-name match (E2).**
  Posts, events, pages, and todos (todos were tag-aware from the start;
  posts/events/pages now join) match not only `Title` + `Body` but also
  the **names** of the document's own tags (the `Tag.Id → Tag.Name` map,
  loaded once per search call via `LoadTagNamesAsync`). A query matching
  a tag name on a document the actor can see is a hit, even if the
  Title/Body do not match. The tag is a label, never a gate (C-TG·1):
  the tag-name match changes **which** documents match, never **whether**
  a document is visible — visibility is still decided by the same frozen
  `CanSeeAsync` pass (C-M8·1/2).

- **People surface: pure catalog read, no authorization pass (E3).**
  The directory (ADR 0003, M2) is a pure catalog read: "every
  non-blocked resident, for any signed-in viewer" — the
  `DirectoryService` docs are explicit that `CanSeeAsync` is **not** run
  on the list (there is no hidden-count to count). The `Profile.
  Visibility` audience is reserved for a *future* detailed-profile
  surface. The people search surface inherits this: the canonical
  predicate is `!Blocked`, there is no hidden set, and the audit row's
  `HiddenCount` is always 0. An audit row **is** still emitted (C-M8·3
  always-on discipline: a search visit is recorded; the directory's own
  catalog read is not a "decision" and so emits no row).

- **Anonymous: zero hits on all six new surfaces (E4).** The six new
  surfaces' canonical feeds are all `[Authorize]`-gated (projects,
  boards, todos, inventory, documents require sign-in; the directory
  lists residents to signed-in viewers only). For an anonymous actor,
  `PeopleAsync` returns `[]` (the `actorId` guard at the top), and the
  `signedIn` gate in `SearchAsync` skips all six. This is the C1-style
  degrade: no 403, no refusal surface — the surfaces simply have no
  candidates for an anonymous caller (C-M8·2: the hit set is a subset of
  the canonical feed, and the canonical feed is empty for anonymous).

- **`Profile.Created` sentinel (E5).** The `Profile` document has no
  `Created` field (M1 identity doc — the profile is created at
  registration and its timestamp is not stored on the document). The
  people hits' `Created` value is `DateTimeOffset.MinValue` — a
  display-only ordering key (C-M8·5), never an access input. It sorts
  before all real timestamps, which is the correct semantic: people do
  not have a meaningful "creation date" in the search ordering.

- **Zero schema change (E6).** No new document, no new column, no
  `tsvector`, no migration. The `Tag` document, `Profile`, `Project`,
  `Board`, `TodoItem`, `InventoryItem`, and `Document` documents already
  exist with the fields this ADR reads. The `TagIds` field exists on
  `Post`, `Event`, `Page`, `TodoItem`, and `Profile` (the five
  tag-aware surfaces). ADR 0004 §B is untouched.

- **Zero new authorization surface (E7).** The six new surfaces compose
  the **same three frozen seams** as ADR 0091 D8: `IDocumentStore`,
  `IAuthorizationService`, `IUserInfoService`. No new `AccessAction`, no
  new `AccessVia`, no new adapter, no new `Decide()` branch (C-M8·1).
  The people surface does **not** run `CanSeeAsync` (E3) — it is a
  catalog read, not an authorization decision.

- **Detail routes (E8).** Each new surface maps to its canonical detail
  route (the `HrefFor` projection in `SearchIndexViewModel`):
  <list type="bullet">
    <item>projects → `/projects/projects/{id}`</item>
    <item>boards → `/projects/boards/{id}`</item>
    <item>todos → `/projects/todos/{id}`</item>
    <item>inventory → `/inventory/{id}`</item>
    <item>documents → `/documents/{id}`</item>
    <item>people → `/directory/{id}` (the id is the SubjectId)</item>
  </list>

## Consequences

- **A resident can find content** across all ten surfaces — the first
  time — with the search box in the nav. The search box is now a
  platform-wide find, not a partial one.
- **Tag-name match widens the candidate set** without widening the
  visibility set: a document that was hidden (audience-gated) is still
  hidden, even if its tag name matches. The tag is a label, never a
  gate (C-TG·1).
- **The people surface replaces the standalone "find people" box** with
  the nav search box (M23's surface is now a `surface=people` section of
  the same `/search` page). The directory route (`/directory/{id}`) is
  the detail link.
- **Anonymous still searches** the original four surfaces (posts,
  events, pages, announcements) — unchanged from ADR 0091. The six new
  surfaces are signed-in-only (their canonical feeds are gated).
- **The audit log** now records `search:projects`, `search:boards`,
  `search:todos`, `search:inventory`, `search:documents`, and
  `search:people` aggregate rows — the same `TargetKind = "search:" +
  surface` shape, `TargetId = null` (C-M8·3 extended).
- **Bookmarks are excluded** by design (no searchable content of their
  own) — a future ADR can add them if the bookmark document grows a
  title/metadata field.
- **The deferred lanes from ADR 0091 are unchanged:** language-scoped
  search (ADR 0018) and full-text search (a future ADR with the
  `tsvector` migration) both land without re-designing this one,
  because the seam is still one interface (D8 extended) and the engine
  is still one predicate per surface (D6 extended).
