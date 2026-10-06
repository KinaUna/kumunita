# ADR 0147 — Tags on to-dos and Kanban boards

Status: Accepted
Date: 2026-09-24

Extends **0044** (Tags: free author-set subject labels for posts and blog
pages — the `Tag` / `TagTranslation` doc model, the `Slug` business key, the
attach/translate standing split, and the one read seam) to two more
`Projects`-context surfaces: **`TodoItem`** (which already carried a
`TagIds` field from the tag-lane rollout but had **no write path or
detail/edit surface** until now) and **`KanbanBoard`** (which had **no
`TagIds` at all**). It also closes the one search-surface gap — the
`boards` search surface matched `Title` + `Description` but not tag names,
while `todos` already matched tags.

Builds on the frozen base of **0044** (the tag doc + attach-lane +
read-seam shape — this ADR adds **no** new tag documents and **no** new
read lanes; it reuses the existing `TagService` attach lanes for two more
target kinds), **0004** (Marten doc ownership + additive schema-evolution,
§B.1 — `KanbanBoard.TagIds` is an additive doc field exactly as
`TodoItem.TagIds` and `Post.ImageIds` are), **0067** (the `Projects` bounded
context — the `TodoItem` + `KanbanBoard` / `KanbanLane` /
`BoardItemPlacement` documents and the `IProjectService` seam), **0013**
(the group-post lane — a tag rides the shared doc, so one `TagIds` field
covers the surface), and **0098** (the board edit lane — a tag is a further
editable field on that lane, not a re-targeting of the board's standing /
component / language). No amendment to 0044, 0004, 0067, 0013, or 0098 —
this ADR is additive on their decisions exactly as written.

## Context

ADR 0044 landed tags as a *shared organizational primitive*: a small set of
free subject labels an author attaches to content they may already edit,
browsable and searchable *by* label without any of them becoming an access
boundary. That primitive landed on **posts and blog pages**. It was
*partially* present on **to-dos** — the `TodoItem.TagIds` field exists and
the `todos` search surface matches against tag names — but the field was
**dead**: no write lane resolved slugs → ids onto it, no detail page showed
chips, and no edit form seeded them. And it was **absent** on **boards**:
a Kanban board is the *top-level* organizer of a neighborhood's shared work
(exactly the kind of "what are we all doing" surface that benefits from a
`sanitation` or `budget` label), yet it had no tag surface at all.

The gap is the same one 0044 named, two surfaces over: a resident who wants
"all the to-dos *and* boards we're tracking under `sanitation`" has a tag
label but only a post/page can carry it. And the search asymmetry was a
latent bug — a query that finds a todo by tag but not an equivalent board is
a silent, surprising miss.

The fix is small and entirely additive: extend the *existing* tag model
(two more `TagIds` fields on docs that already exist, one more search
surface made tag-aware) rather than inventing a new one. The tag *document*
and the *attach/translate/read* mechanics are 0044's and are reused verbatim.

## Decision

### D1 — `KanbanBoard.TagIds` is an additive field, mirroring `TodoItem.TagIds`

`KanbanBoard` gains **`TagIds`** — `IReadOnlyList<string>` (default
empty), the resolved `Tag` doc ids, the exact `TodoItem.TagIds` /
`Post.TagIds` shape. It is an **ADR 0004 §B.1 additive field**:
delta-detected by `ApplyAllConfiguredChangesToDatabaseAsync`, idempotent,
no re-seed, no EF migration. It registers in `PortabilityDocTypes` as
`A("TagIds", "Tag")` (an array-valued tag reference, the `TodoItem`
`TagIds` entry's shape) so a board export carries its tags. **No new
document** is added to the `M5DocTypes` / `PortabilityDocTypes` surface —
only a field on an existing doc type.

### D2 — The write path reuses the `TagService` attach lanes; standing is the board's / to-do's own edit lane

The attach lanes are **0044's pattern** (resolve each slug → create-or-reuse
a `Tag` → store the resolved ids on the target doc → store a `tag.attach`
audit row) extended to two more **target kinds**:

- **`ITagService.AttachToTodoAsync(todoId, slugs, actorId, roles, session)`**
  — target kind `todo`. Standing (re-checked inside the lane, self-contained):
  **creator ∪ assignee ∪ GlobalAdmin** — the *same* standing
  `ProjectService.CheckTodoStanding` already enforces for a to-do edit, so
  "attaching a tag is part of editing that to-do" (0044 D4, carried over).
- **`ITagService.AttachToBoardAsync(boardId, slugs, actorId, roles, session)`**
  — target kind `board`. Standing: **creator ∪ GlobalAdmin** — the *same*
  standing `ProjectService.CheckBoardStanding` / `UpdateBoardAsync` enforces
  (0098), so a board's tags edit exactly as its title/description/audience
  already do.

Two public display probes mirror `CanAttachToPost`: `CanAttachToTodo(todo,
actorId, roles)` and `CanAttachToBoard(board, actorId, roles)`. The
**`ProjectService`** write lanes call the attach lane (the `PostService._tags`
re-store + save shape — store the doc first, then attach, then re-store the
resolved ids):

- `CreateTodoAsync` + `AddSubtaskAsync` — if `request.TagSlugs` is
  `{ Count: > 0 }`, attach (a to-do with no tags is the default-empty
  shape; the subtask lane reuses the to-do create request, so it gets tags
  for free — the actor is the subtask's creator, so standing passes).
- `UpdateTodoAsync` — if `request.TagSlugs` is `not null`, attach: an
  **empty** list detaches all (the 0044 empty-set-detach shape); a
  non-empty set replaces; `null` leaves the stored tags untouched (the
  partial-update pin).
- `CreateBoardAsync` / `UpdateBoardAsync` — the same two shapes on the
  board.

The request DTOs carry the author's **typed labels under `TagSlugs`**
(`IReadOnlyList<string>?`, the `PostDraft.TagSlugs` naming — the request
carries *labels*, the doc stores *ids*); the stored `TagIds` fields carry
the resolved ids. A bad slug is an `ArgumentException` (0044 C-TG·4) — the
Web maps it to a form error.

### D3 — The read surface reuses `ITagService.ListForActorAsync`; the detail pages render chips

The todo + board **detail** actions resolve the doc's `TagIds` to display
names in the viewer's language via the *existing*
`ITagService.ListForActorAsync` (which already does the `TagTranslation`
lookup) — the `PostsController` detail idiom. A "read, not a decision"
surface: the doc's single `Read` decision already ran; a dangling `TagId`
(a removed `Tag` row) simply drops (renders as nothing, not a 404). The
edit **GET**s pre-seed the tag-suggest input's chips from the stored
tags' **slugs** (a new `SeedExistingTagSlugsAsync` helper — resolve
`TagIds` → `Tag.Slug`), the `PostsController`
`SeedExistingTagSlugsAsync` idiom. The six views carry the tag input
(todo create/edit, board create/edit — the `data-tag-suggest` self-wired
input, `tag-suggest.js`) and the detail chips (todo + board detail — the
`/tags/{slug}` badge, the `Posts/Detail.cshtml` chip idiom). No new `kw-l`
keys are introduced: the existing `nav.tags` + `tag.input.hint` keys and the
`tags.events_example` placeholder are reused (the `kw-l` registry is
untouched, so `KwLRegistryConsistencyTests` is unaffected).

### D4 — The `boards` search surface becomes tag-aware; `todos` is unchanged

`SearchService.BoardsAsync` now takes the `tagNames` map and matches
`Title` + `Description` **+ tag names** via the existing
`MatchsWithTags` helper (the `TodosAsync` shape, which was already
tag-aware). This closes the search asymmetry: a query that finds a todo by
tag now finds an equivalent board. `TodosAsync` is already tag-aware and is
untouched.

## Consequences

- **Two more doc fields, no new docs.** The cost is one additive field on
  `KanbanBoard`, a `TagSlugs` field on the two board request DTOs, two
  attach-lane target kinds, and one search-surface signature change. There
  is **no** new bounded context, **no** new `DocTypes` registration, and
  **no** EF migration (0004 §B.1).
- **Standing is unambiguous.** A to-do's tags edit under the to-do's own
  edit standing (creator ∪ assignee ∪ GlobalAdmin); a board's tags edit
  under the board's edit standing (creator ∪ GlobalAdmin). There is no new
  "who may tag" rule — it is the object's existing edit rule, exactly as
  0044 D4 pinned it for posts/pages.
- **Search parity across the project surfaces.** After this ADR the
  `todos`, `boards` (now), and the post/page surfaces all match tag names
  in search — the one remaining asymmetry is gone.
- **Not-in-scope (locked):** `TagIds` is **not** added to `KanbanLane` or
  `BoardItemPlacement` (a lane and a card are not tag-labelable surfaces —
  the board is), and the **subtask** lane reuses the parent's to-do create
  request but the *subtask form itself* posts no tags (the simplified inline
  "add subtask" shape keeps its `TagSlugs = null`). A tag remains a
  label, never a gate (0044 C-TG·1).
