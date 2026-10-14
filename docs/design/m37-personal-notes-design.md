# M37 — Personal notes (a private note a resident attaches to any content
entity; organizable: a personal folder tree + free author-set tags;
author-only) — design

> **Milestone M37.** The resident's own **organized notes**: a private,
> personal *body of writing* the resident attaches to any of the resident
> content surfaces (post / group post / event / page / project / to-do /
> board / document / directory), that the resident then **organizes** into
> a **personal folder tree** (bounded in depth) and labels with **free
> author-set tags** — "a private note a resident attaches to any content
> entity," the README roadmap row names verbatim.
>
> **A standalone core surface** (a roadmap letter, not a named lane): one
> **new bounded context** `Kumunita.Core.PersonalNotes` with **two
> documents** (`PersonalNote` + `NoteFolder`) on a **new
> `PersonalNotesDocTypes`** surface (the M17 `BookmarkDocTypes` / M9
> `MessagingDocTypes` precedent — additive, idempotent, **zero migrations**
> for existing surfaces), **no adapter at all** (a personal note is a
> *personal-by-id* record, the M17 `Bookmark` / M9 ADR 0105 shape — D2), and
> the **`pn.*`** `kw-l` registry block (the §kw-l below).
> **Zero new authorization surface** (C-M37·1): no new `AccessAction`, no
> new `AccessVia`, no branch in `Decide()`, no method on
> `IAuthorizationService` (D2). The **only** authorization call in the
> entire milestone is the D3 write-lane visibility check, which is the
> **target's own** frozen `CanAsync(Read)` — M37 adds no decision of its own.
>
> **Status.** **LOCKED.** The decisions D1–D9, the invariants
> C-M37·1…8, the FACES F1–F5 + the named trade, the §kw-l key list, the §gate
> test names, and the §drift-guard are locked in **ADR 0167 (Accepted,
> 2026-10-14)**. The `[PROPOSED]` set in the register
> `docs/plans-milestones/plan-m37-personal-notes.md` is the locked set this
> doc restates **verbatim** (the U00 handoff entry records the lock; **no
> veto** was recorded before the lock).
>
> **The one thing every unit must respect:** M37 **composes only frozen
> seams** — the `IAuthorizationService` (the D3 write-lane check only) + the
> owning surfaces' existing read seams (the D7 degraded resolution) + the
> **frozen** `MarkdownRenderer` (the D4 body render) over the
> host-registered Marten `IDocumentStore`. It does **not** add a new
> `AccessAction`, a new `AccessVia`, a new `Decide()` branch, a new adapter,
> or a new `IAuthorizationService` method (C-M37·1, D2). Every register unit
> (U01's context + surface, U02's note-CRUD service seam, U03's folder-tree
> seam, U04's note button, U05's list surface) enforces this at a different
> seam; the §2.7 drift-guard frozen list below is the **exact** set that is
> untouched.

## Context

M17 (Bookmarks) shipped the **personal-pin** world: one row per (resident,
target), one list, one toggle — a *pointer*, not content. M36 (mentions)
shipped the **cross-surface reference** world: a `@<resident>` token that
notifies + links. M37 ships the **personal-note** world: a **body of the
resident's own writing** attached to the target (a *note*, not a pointer),
and the notes are **organizable** — the resident **nests them into a
personal folder tree** (categories, bounded in depth) and adds **free
author-set tags**. A note is **author-only** by construction
(privacy-first; ADR 0001-B) — a `GlobalAdmin` who is not the author gets a
non-leaky **404** and **no audit row** (the M17 / ADR 0105 shape).

The precedents M37 composes:

1. **The frozen `IAuthorizationService`** (ADR 0006 §A) — `CanAsync(Read)`
   (detail) over an `IAuditableResource`, the `IDocumentSession`
   overloads for C3 same-transaction audits. The frozen surface is
   4-method + the group lane; the frozen `AccessAction` set is
   `Read` / `Moderate`; the frozen `AccessVia` set is Owner / Admin (+
   the existing values). **M37 adds none of these — it adds no adapter
   at all** (C-M37·1 / D2).
2. **The ADR 0105 (M9 messaging) participant-by-id shape** — the
   *primary* precedent (D2 / C-M37·1 / C-M37·2): a **personal-by-id
   record** whose access story is **identity comparison in the service**,
   never an audience decision. A non-owner — an operator included — gets a
   **non-leaky 404**, and the owner's own read is a **personal read**
   that **never audits** (M9: "reads never audit"; the M6 inbox shape).
   The owner's own note list is exactly this: `Where(n => n.OwnerId ==
   ownerId)`, **zero** `AccessAudit` rows, **zero** `CanSeeAsync` passes
   over the note rows (C-M37·2).
3. **The M17 `BookmarkDocTypes` unique-index-as-witness idiom** — the
   **unique** index on `(OwnerId, TargetKind, TargetId)` is the F1
   idempotency witness (at-most-one-note-per-owner-target); the M9
   `convo_uidx_pair` shape, re-applied.
4. **The `*DocTypes` parallel-surface shape** (`BookmarkDocTypes` /
   `MessagingDocTypes`) — `opts.Schema.For<T>()` + named indexes;
   `ApplyAllConfiguredChangesToDatabaseAsync()` delta-detects and
   applies the new tables **idempotently** at boot (ADR 0004 §B.1) —
   **zero migrations for existing surfaces**.
5. **The closed-string-set idiom** (the M5 `KanbanStatuses` shape) —
   `TargetKind` is a **string** restricted to the closed set
   {`post`, `grouppost`, `event`, `page`, `project`, `todo`, `board`,
   `document`, `directory`}, the **exact vocabulary the existing
   `*ToAuditableResource` adapters and the `AccessAudit` rows already
   emit** — D1.
6. **The `pn.*` `kw-l` block in `KnownTranslationKeys`** (the
   closed-key registry, ADR 0015) — the `pn.*` block it joins (the
   `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
   pins extend automatically — §kw-l).
7. **The M31 / ADR 0031 frozen `MarkdownRenderer`** (the one-renderer pin)
   — the D4 body render path: the body is stored as raw text and
   Markdown-rendered on read via the **frozen** `MarkdownRenderer`.
8. **The M26 `Tag` doc's `Name` normalization shape** (ADR 0044) — the
   D5 tag normalization (lowercase, trimmed, deduped, sorted) is the
   M26 `Name` idempotency-witness idiom, re-applied to *personal*
   labels (a different shape than the M26 *shared* vocabulary — D5).
9. **The M14 / M16 author-only edit-lane shape** (ADR 0014 / ADR 0016) —
   the D4 body-revision shape: the body is **never** mutated in place;
   edit is a **new revision** and the `Modified` timestamp is the witness.
10. **The M5 `projects.*` core-surface precedent** (D8) — M37 is a
    **milestone**, not a **lane**: always available, **no**
    off-by-default admin toggle (the inverse of the ADR 0101/0105
    `LocaleSettings` toggle shape — C-M37·7).

The gap M37 closes is the README row itself: the neighborhood today has a
*pointer* (the M17 bookmark — "come back to this") but nowhere to say
*here is what I think about this, and these thoughts go together*. M37
adds that surface: two documents, one new surface, one service seam, a
note button on the nine target detail surfaces, a `/notes` list view +
nav entry, and a closed `pn.*` `kw-l` block (the §kw-l list) — and **no**
new authorization surface (C-M37·1 / D2).

M37 builds entirely on **frozen, verified seams** (the list above). This
is a **milestone** (a roadmap letter, not a named lane) and the **last**
milestone on the roadmap (no `M38` to promote — D9): the close unit (U06)
flips `Milestones.cs` / the README Roadmap / `docs/STATUS.md` /
`docs/ARCHITECTURE.md` + `MilestonesTests.cs` **+** `src/Kumunita.Web/WhatsNew.cs`
(the **six-member** close flip — the AGENTS.md doc↔code parity contract —
D9). This design doc (authored U00, **LOCKED**) is the **primary tier**;
the register `docs/plans-milestones/plan-m37-personal-notes.md` is the
secondary tier; the scratch handoff note is
`docs/plans-milestones/in-progress/m37-personal-notes-handoff-notes.md`.

## Scope

**In (this milestone):**

- The `PersonalNote` + `NoteFolder` documents (ns
  `Kumunita.Core.PersonalNotes`; §2.1) and the **new
  `PersonalNotesDocTypes`** surface (the **unique** index on `(OwnerId,
  TargetKind, TargetId)` — the D3/F1 witness; the non-unique `(OwnerId,
  Created)` list-ordering index; the non-unique `(OwnerId, FolderId)`
  folder-tree index; `NoteFolder`'s **unique** `(OwnerId, ParentId, Name)`
  index — the D6 witness; ADR 0004 §B.1 additive, **zero migrations** for
  existing surfaces).
- `IPersonalNoteService` + `PersonalNoteService` — the note-CRUD seams
  (`CreateNoteAsync` / `GetNoteAsync` / `ListNotesAsync` /
  `UpdateNoteBodyAsync` / `DeleteNoteAsync` / `AddTagAsync` /
  `RemoveTagAsync` / `HasNoteAsync`), the D5 tag-normalization seam, the
  D3 write-lane visibility check, the D7 degraded resolution, the
  C-M37·2 zero-audit personal read + the folder-tree seams
  (`CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync`
  (moves notes to the root) / `MoveNoteToFolderAsync`) + the DI
  registration.
- The `NotesController` + the `GET /notes` list view (grouped by
  `TargetKind` **and** nested under the owner's folder tree, the
  `pn.list.degraded` label on dangling rows, the per-row edit / move /
  tag / delete controls, the tag chips) + the nav entry.
- The `_NoteButton` partial + the `POST /notes/attach` action, wired into
  the **nine** target detail surfaces (post, group post, event, page,
  project, to-do, board, document, directory — the M37 `TargetKind`
  closed set).
- The `pn.*` `kw-l` keys × en/de/fr/da (the §kw-l list).
- The 18 pinned seam tests (§2.5) + the three acceptance gate tests
  (§2.6).

**Out (deferred lanes, each its own ADR — §deferred):**

- The "nudge the author on note" notification lane.
- Note **sharing** / per-surface "top noted" stats.
- Note **search**.
- The **platform-wide tag vocabulary** (the M26 `Tag` doc lane — M37's
  tags are *personal* labels, not a shared vocabulary).
- **Folder drag-and-drop reordering**.
- **Note export**.

## Human cost

Gives the resident's time **back**: the note captures the resident's own
thinking about a piece of content in one attach, and the folder tree +
tags keep that thinking *organized* — the resident's notes are not a
second feed to scroll, they are the resident's own index with the
resident's own categories. The list degrades **quietly** — a dead note
renders as one static label with the resident's own body still visible
(the named trade), never a dead-end 404 and never a leak. It optimizes
the whole (a resident's own memory), not a part: there is no engagement
metric, no per-surface "top noted" count, no nudge (C-M37·7 / D8 /
§deferred). The team's boundary is respected by construction: the
milestone is two documents, one surface, one service seam, one list — a
pace that can hold in the atomic-unit register.

## Parts affected

- **`Kumunita.Core`** — a **new bounded context**
  `Kumunita.Core/PersonalNotes/` (`PersonalNote`, `NoteFolder`,
  `IPersonalNoteService`, `PersonalNoteService`, the result records,
  `PersonalNotesDocTypes`) + its DI registration in
  `DependencyInjection.cs`.
- **`Kumunita.Web`** — `NotesController` + the `Views/Notes/` list view +
  the `_NoteButton` partial + the nine target detail views' button wiring
  + the nav entry + the `pn.*` `kw-l` rows (×4).
- **`tests/Kumunita.Core.Tests`** — `PersonalNoteServiceTests.cs` (the 18
  pinned seams, §2.5). **`tests/Kumunita.Web.Tests`** — the list /
  button / folder-tree pins + the three acceptance gate tests (U06).
- **Unchanged (the frozen set):** `IAuthorizationService`, `AccessAction`,
  `AccessVia`, `Decide()`, every existing `*DocTypes` surface,
  `LocaleSettings`, the `MarkdownRenderer` (read-only), the notification
  machinery, `Milestones.cs` until U06 (D9).

## Invariants (pinned for M37)

Verbatim from the register.

- **C-M37·1 — The personal note is a personal-by-id record; the surface
  adds zero authorization.** `PersonalNote` carries `OwnerId` +
  `TargetKind` + `TargetId` + `Body` + `FolderId?` + `Tags` and nothing
  that an access decision reads; there is **no** `PersonalNote` adapter,
  **no** new `AccessAction`, **no** new `AccessVia`, **no** `Decide()`
  branch, **no** new `IAuthorizationService` method (D2). The **only**
  authorization call in the entire milestone is the D3 write-lane
  visibility check, which is the *target's own* frozen
  `CanAsync(Read)` — M37 adds no decision of its own.
- **C-M37·2 — The owner's note list is a personal read, audited zero.**
  Loading one's **own** note list is the M6-inbox / M17-bookmark
  personal-read shape: **no** `CanSeeAsync` pass over the note rows,
  **no** `AccessAudit` row for the owner's own list, **no** owner-check
  bypass. A **non-owner** (a `GlobalAdmin` included) gets a non-leaky
  **404** — and that 404 commits **no** `AccessAudit` row either (the
  ADR 0105 "operator has no read standing" precedent).
- **C-M37·3 — Note-attaching requires visibility; note-attaching grants
  nothing.** The write lane re-runs the target's frozen `Read` decision
  **before** creating the note row; Deny ⇒ **404** (not 403), and the note
  row never appears in, widens, or is consulted by any `CanSeeAsync` pass
  anywhere (D3).
- **C-M37·4 — A note is *content*, not a pointer: it carries a body, and
  the body is owned by the author.** `PersonalNote` carries `Body`
  (the resident's own writing) + `Modified` (the body's revision
  witness). The body is **never** mutated in place (edit = a new revision,
  the M14 / M16 author-only lane); the body is **visible to the author
  always** — even when the target is gone (D4 / D7). *This is the named
  trade with M17:* M17's `Bookmark` has no body; M37's `PersonalNote`
  does.
- **C-M37·5 — Tags are author-set, free-form, per-note; the tag set is
  normalized on write.** A note's `Tags` is an `IReadOnlyList<string>`
  (or a JSON column — U01's call); on write, each tag is **lowercase,
  trimmed, deduped, sorted** (the M26 `Tag` doc's `Name` normalization
  shape, the idempotency witness). A tag is **not** a platform-wide `Tag`
  doc (the M26 tag lane is a *shared* vocabulary; M37's tags are *personal*
  labels — different shapes, different surfaces, different invariants).
- **C-M37·6 — The folder tree is a personal tree, bounded in depth,
  unique in name.** Each resident has their own tree of `NoteFolder`
  rows, keyed on `OwnerId` + a bounded `ParentId` (depth ≤ 2: root → child
  → grandchild). A folder's `Name` is unique within the owner's tree (the
  `(OwnerId, ParentId, Name)` unique index). A note's `FolderId` is the
  **owner's** folder id (a note cannot live in another resident's folder).
  A folder's deletion **moves** its notes to the root (`FolderId = null`)
  — the note body is never lost.
- **C-M37·7 — M37 is a standing core surface (no toggle).** M37 has **no**
  off-by-default admin toggle and **no** `LocaleSettings`-style admin
  switch (D8); it is always available to every resident.
- **C-M37·8 — The close flip is atomic (the six-member close flip).**
  The M37 close (U06) flips the six surfaces — `Milestones.cs` (M37 →
  done, **no** M38), `MilestonesTests.cs` (re-pinned to the "last
  milestone, no successor" pin), the README Roadmap/Status,
  `docs/STATUS.md`, `docs/ARCHITECTURE.md` value-chain M37 row, **and**
  `src/Kumunita.Web/WhatsNew.cs` (the required sixth member — ADR 0110)
  — **in the same unit** (D9).

## Decisions (D1–D9, locked — ADR 0167)

Verbatim from the register, each with its `*Forbids:*` tail.

- **D1 — One new bounded context `Kumunita.Core.PersonalNotes`, two
  documents, one new surface.** M37 adds a new bounded context
  `Kumunita.Core.PersonalNotes` with **two documents**:
  `PersonalNote` (`OwnerId`, `TargetKind`, `TargetId`, `Body`,
  `FolderId?`, `Tags` (a closed list of author-set strings), `Created`,
  `Modified?`) and `NoteFolder` (`OwnerId`, `ParentId?`, `Name`),
  registered on a **new `PersonalNotesDocTypes`** surface (the M17
  `BookmarkDocTypes` / M9 `MessagingDocTypes` shape — a parallel additive
  surface, delta-detected and applied idempotently at boot, **zero
  migrations** for existing surfaces). `TargetKind` is a **string**
  restricted to the closed set
  {`post`, `grouppost`, `event`, `page`, `project`, `todo`, `board`,
  `document`, `directory`} — the exact vocabulary the existing
  `*ToAuditableResource` adapters already emit (the M17 `Bookmark`
  `TargetKind` shape, extended with the M37 surfaces the README names).
  *Forbids:* a separate document per surface; a `PersonalNote`-per-kind
  set of tables; an enum type for `TargetKind`; a migration of an existing
  surface; reusing the M17 `Bookmark` doc (M37 is a *new* context — the
  M17 surface is untouched).
- **D2 — A personal note is a personal-by-id record, never an audience
  decision.** M37 adds **no adapter**, **no** new `AccessAction`, **no**
  new `AccessVia`, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` (the M17 `Bookmark` / M9 ADR 0105 shape). The
  **owner's** own note list is a personal read (like the M6 inbox / M17
  bookmark list): **no** `CanSeeAsync` pass over the note rows, **no**
  `AccessAudit` row for the owner's own list. A **non-owner** (a
  `GlobalAdmin` included) gets a non-leaky **404** — and that 404 commits
  **no** `AccessAudit` row either (the ADR 0105 "operator has no read
  standing" precedent). *Forbids:* a `PersonalNoteToAuditableResource`;
  running the owner's own note list through `IAuthorizationService`; a new
  `AccessAction`/`AccessVia`.
- **D3 — Note-attaching requires visibility; note-attaching grants
  nothing.** The write lane (attach a note to a target) **re-runs the
  target's frozen `Read` decision** (`CanAsync(Read)` over the target's
  existing adapter — the *same call the detail page uses*) and **refuses
  404** (not 403 — the Deny row **does not survive**, the M16 create-gate
  posture) when the target is not visible. The note row itself **never**
  appears in any `CanSeeAsync` pass, never widens an audience, and never
  confers standing. *Forbids:* a note on an invisible target; a note
  consulted by any read decision anywhere; a `GlobalAdmin` peek branch
  into the target's visibility on the write lane (the detail page's own
  decision is the truth).
- **D4 — A note is *content*, not a pointer: it carries a body, and the
  body is owned by the author.** `PersonalNote` carries `Body` (the
  resident's own writing, stored as raw text, Markdown-rendered on read
  via the **frozen** `MarkdownRenderer` — the M31 / ADR 0031 shape) +
  `Modified` (the body's revision witness). M17's `Bookmark` has **no**
  body; M37's `PersonalNote` **does**. The body is **never** mutated in
  place — edit is a **new revision** (the M14 post-edit / M16 group-post-
  edit author-only lane, the `Modified` timestamp is the witness). The
  body is **visible to the author always** — even when the target is gone
  (the D7 degraded shape; the named trade). *Forbids:* a `Bookmark`-shaped
  `PersonalNote` (no body); a body that mutates in place (no edit = new
  revision); a body that is not visible to the author when the target is
  gone (the D7 pin).
- **D5 — Tags are author-set, free-form, per-note; the tag set is
  normalized on write.** A note's `Tags` is an `IReadOnlyList<string>`
  (or a JSON column — U01's call, mirroring the codebase's existing
  list-of-strings shape). On write, each tag is **lowercase, trimmed,
  deduped, sorted** (the M26 `Tag` doc's `Name` normalization shape, the
  idempotency witness). A tag is **not** a platform-wide `Tag` doc (the
  M26 tag lane is a *shared* vocabulary; M37's tags are *personal* labels
  — the two are different shapes, different surfaces, different
  invariants). *Forbids:* a `PersonalNote` that references the M26 `Tag`
  doc's `TagId`; a tag set that is not normalized (no idempotency
  witness); a tag that is platform-visible (M37's tags are *personal*
  labels, not a shared vocabulary).
- **D6 — The folder tree is a personal tree, bounded in depth, unique in
  name.** Each resident has their own tree of `NoteFolder` rows, keyed on
  `OwnerId` + a bounded `ParentId` (depth ≤ 2: root → child → grandchild —
  the "nested" shape, not an arbitrary tree). A folder's `Name` is unique
  within the owner's tree (the `(OwnerId, ParentId, Name)` unique index is
  the witness). A note's `FolderId` is the **owner's** folder id (the note
  and the folder are in the same owner's tree — a note cannot live in
  another resident's folder). A folder's deletion **moves** its notes to
  the root (`FolderId = null`) — the note body is never lost. *Forbids:*
  an arbitrary-depth folder tree (depth ≤ 2); a folder `Name` that is not
  unique within the owner's tree; a note that lives in another resident's
  folder; a folder deletion that removes note bodies.
- **D7 — The list degrades, never leaks; the note body survives the
  target's death.** A note whose target is absent, soft-deleted, or no
  longer visible renders the **generic** `pn.list.degraded` label (a
  closed `kw-l` key) — **no title, no link, no id** — and the note's
  **body remains visible** to the owner (the D4 "content, not pointer"
  pin). The note controls (edit / move / tag / delete) still work on the
  degraded row — none of them require the target's own read seam (they are
  keyed on the note's own id + the owner's own tree). *Forbids:* a 404/403
  on a dangling note row; leaking a removed target's title; a note body
  that is not visible to the owner when the target is gone; a per-row
  `CanSeeAsync` branch in the list beyond the owning surface's existing
  seam.
- **D8 — M37 is a standing core surface, not a lane: no off-by-default
  toggle.** M37 is a standing core surface (like M5 Projects, M17
  Bookmarks): **no** off-by-default admin toggle, **no** `LocaleSettings`-
  style admin switch, **no** `KumunitaFeature` admin flag. It is always
  available to the community. (M9's off-by-default toggle was a *lane*;
  M37 is a *milestone*.) *Forbids:* an off-by-default admin toggle for
  M37; a `LocaleSettings`-style switch.
- **D9 — The close flip is one atomic unit (U06) — the six-member close
  flip.** M37 is the **last** milestone on the roadmap (no `M38` to
  promote — the M36·10 / M35·11 framing, re-applied). The M37 close (U06)
  flips the roadmap parity **together**: `Milestones.cs` M37
  `StatusNext` → `StatusDone` (and **no** `M38` row); `MilestonesTests.cs`
  re-pinned to the **"last milestone, no successor"** pin (M37 → done,
  **no** successor — the M36 close's pin shape); the README **Roadmap**
  (M37 → done) + the M37 **Status** line; `docs/STATUS.md`;
  `docs/ARCHITECTURE.md`'s value-chain table gains the **M37 row**; **and**
  `src/Kumunita.Web/WhatsNew.cs` gains the **M37 entry** (the
  **required sixth member** — ADR 0110; the M36 close's "six-member
  close flip" shape). All six ship **in the same unit** (the M36
  close-flip precedent). *Forbids:* reordering M37; adding an `M38` row;
  editing a shipped milestone's text; breaking the `MilestonesTests`
  pins; flipping one surface and not the others; **omitting the
  `WhatsNew.cs` entry** (the M27-shipped-without-entry drift, caught in
  review — the ADR 0110 "required sixth member" pin).

## FACES (F1–F5) + the named trade

- **F1 — The note-attach is atomic and idempotent.** Attaching a note to
  the same target twice commits **one** note (the owner's own note, keyed
  on `(OwnerId, TargetKind, TargetId)` — the M17 `Bookmark` unique-index
  shape, re-used); a second attach is a **no-op** (the button reflects
  "noted"); re-attach after removal is a **fresh** `Created` timestamp (the
  F1 / C-M37·4 witness).
- **F2 — The write lane is the target's own frozen decision.** Attaching a
  note to a post you cannot see ⇒ **404**, no note row, no Deny row
  surviving (the M16 create-gate posture); the *same* `CanAsync(Read)`
  call the detail page used is the truth — M37 adds no branch (C-M37·3).
- **F3 — The list is the owner's personal read (zero audit).** The
  owner's own `/notes` loads their notes with **zero** `AccessAudit` rows
  and zero `CanSeeAsync` passes over the note rows (C-M37·2); a
  `GlobalAdmin` who is not the owner gets a 404 and no row.
- **F4 — A dangling note degrades, never leaks; the body survives.** The
  target is soft-deleted / hidden / removed ⇒ the row shows the generic
  `pn.list.degraded` label, no title, no link, no id — **and the body
  remains visible to the owner** (C-M37·4 / C-M37·5, D4 / D7); the note
  controls (edit / move / tag / delete) still work on it.
- **F5 — M37 is a standalone core surface** (C-M37·7 / D8): it does not
  depend on a lane being on; the only external composition is the frozen
  `IAuthorizationService` (D3 write lane) + the owning surfaces' existing
  read seams (D7 list resolution).

**The named trade — the note body's permanence vs. the dangling-target
leak surface.** M37 keeps the note row (and its **body**) when its target
dies (soft-deleted post, removed event, hidden to-do, gone page) instead
of silently removing it, because *silently-removed notes are worse than a
greyed-out one* — the resident's list would shrink under them with no
explanation, and the body is **content the resident wrote** (not a
pointer to something else). The trade: the row then has to render
something for a target it can no longer resolve, and the invariant that
keeps that safe is **C-M37·5** — the degraded label is a **closed `kw-l`
key** (`pn.list.degraded`) with **no title, no link, no id**, so the
leak surface of a dangling row is exactly **one static string** + the
author's own body (which is visible to the author always by D4). The note
controls (edit / move / tag / delete) stay — removing a dangling note
needs no target read at all (it is keyed on the note's own id).

## Seams & contracts (mandatory)

M37 touches **no existing seam**. It creates one context, two documents,
one document surface, one service seam, one Web controller + partial —
and it depends on **exactly** the frozen set named in §2.1–§2.7. It does
**not** touch the **access model** (audiences, groups, delegation,
moderator scope): a personal note is **not** an audience, an `AccessVia`,
or a standing — it is *content the author owns* (C-M37·1 / D2). There is
**no migration path** to plan: the `PersonalNotesDocTypes` surface is
delta-detected and applied idempotently at boot (ADR 0004 §B.1), and the
**only** audited call in the milestone is the D3 write-lane check, which
is the **target's own** frozen `CanAsync(Read)` (the target's own audit
row, in the target's own transaction — M37 commits none of its own,
C-M37·2 / C-M37·3).

### 2.1 new M37-owned Core types (exact C#)

```csharp
namespace Kumunita.Core.PersonalNotes;

/// One personal note: a *body of the resident's own writing* attached to
/// a target (content, not a pointer — D4). No Audience, no IsDeleted
/// (the body survives the target's death — D7), no ComponentId, no
/// LanguageCode (D1/D2/D4).
public sealed class PersonalNote
{
    public string Id { get; set; } = "";                       // string surrogate
    public string OwnerId { get; set; } = "";                  // the authoring resident
    public string TargetKind { get; set; } = "";               // closed set:
                                                               //   post | grouppost | event
                                                               //   | page | project | todo
                                                               //   | board | document | directory
    public string TargetId { get; set; } = "";                 // the target's doc id
    public string Body { get; set; } = "";                     // the resident's own writing,
                                                               // raw text; Markdown-rendered on
                                                               // read via the frozen MarkdownRenderer
    public string? FolderId { get; set; }                      // the owner's folder id (D6)
    public IReadOnlyList<string> Tags { get; set; } = [];      // author-set, normalized on write (D5)
    public DateTimeOffset Created { get; set; }                // never mutated (F1)
    public DateTimeOffset? Modified { get; set; }              // the body's revision witness — null until first edit (D4)
}

/// One personal folder node. A personal tree, bounded in depth (≤ 2),
/// unique in name within the owner's tree (D6).
public sealed class NoteFolder
{
    public string Id { get; set; } = "";                       // string surrogate
    public string OwnerId { get; set; } = "";                  // the owning resident
    public string? ParentId { get; set; }                      // the owner's parent folder id — depth ≤ 2
    public string Name { get; set; } = "";                     // unique within the owner's tree (D6)
    public DateTimeOffset Created { get; set; }
}

/// The new parallel surface (ADR 0004 §B.1 — additive, idempotent, zero
/// migrations for existing surfaces).
public static class PersonalNotesDocTypes
{
    public static void Configure(Marten.Schema.StoreOptions opts)
    {
        opts.Schema.For<PersonalNote>()
            .UniqueIndex("pn_uidx_owner_target", n => n.OwnerId, n => n.TargetKind, n => n.TargetId)
            .Index("pn_ix_owner_created", n => n.OwnerId, n => n.Created)
            .Index("pn_ix_owner_folder", n => n.OwnerId, n => n.FolderId);

        opts.Schema.For<NoteFolder>()
            .UniqueIndex("nf_uidx_owner_parent_name", f => f.OwnerId, f => f.ParentId, f => f.Name)
            .Index("nf_ix_owner_parent", f => f.OwnerId, f => f.ParentId);
    }
}
```

**No** `Audience`, **no** `IsDeleted` (a note is *content*, not a
pointer — D4; the body survives the target's death), **no** `ComponentId`,
**no** `LanguageCode` (D1/D2/D4) — the exact field set is a frozen pin
(§2.7). `TargetKind` is a **string** (the M5 `KanbanStatuses`
string-not-enum shape) restricted to the closed set
{`post`, `grouppost`, `event`, `page`, `project`, `todo`, `board`,
`document`, `directory`} — the exact vocabulary the existing
`*ToAuditableResource` adapters already emit. The **unique** index on
`(OwnerId, TargetKind, TargetId)` is the D3/F1 idempotency witness (the
M17 `Bookmark` shape); the non-unique `(OwnerId, Created)` index is the
list-ordering index; the non-unique `(OwnerId, FolderId)` index is the
folder-tree list index. `NoteFolder`'s **unique** index on `(OwnerId,
ParentId, Name)` is the D6 witness; its non-unique `(OwnerId, ParentId)`
index is the folder-tree list index.

### 2.2 the service seam (exact C#)

```csharp
namespace Kumunita.Core.PersonalNotes;

public sealed record NoteRow(
    string Id,
    string TargetKind,
    string TargetId,
    string Body,
    string? FolderId,
    IReadOnlyList<string> Tags,
    DateTimeOffset Created,
    DateTimeOffset? Modified,
    bool Degraded,            // true when the target is absent/soft-deleted/no longer visible (D7)
    string? Title,           // null when Degraded — the pn.list.degraded closed key (D7)
    string? Link);           // null when Degraded (D7)

public sealed record FolderRow(string Id, string Name, string? ParentId, int NoteCount);
public sealed record NoteGroup(string Kind, string? FolderId, IReadOnlyList<NoteRow> Items);
public sealed record NoteListResult(IReadOnlyList<NoteGroup> Groups, IReadOnlyList<FolderRow> Folders);

public enum NoteCreateStatus { Created, AlreadyNoted, Refused }
public enum NoteDeleteStatus { Deleted, NotNoted, Refused }
public enum FolderDeleteResult { NotesMoved, FoldersDeleted }

public interface IPersonalNoteService
{
    /// Note attach — the D3 write-lane visibility check (the target's own
    /// frozen CanAsync(Read)) re-run BEFORE the row is created.
    ///   visible + dup  => AlreadyNoted   (no-op; one row, one Created — F1)
    ///   visible        => Created        (the row is created)
    ///   Deny / absent  => Refused        (the Web layer maps to 404, not 403; no row; no surviving Deny row)
    Task<NoteRow> CreateNoteAsync(string ownerId, string targetKind, string targetId, string body, IDocumentSession session);

    /// The owner's own note by id — zero audit, C-M37·2; a non-owner gets a
    /// non-leaky 404 (the Web layer's [Authorize] + ownerId == User id check).
    Task<NoteRow> GetNoteAsync(string ownerId, string noteId);

    /// The owner's own notes, grouped by TargetKind + nested under the
    /// owner's folder tree, the D7 degraded rows, zero audit, C-M37·2.
    /// NO AccessAudit row, NO CanSeeAsync pass over the note rows (C-M37·2).
    Task<NoteListResult> ListNotesAsync(string ownerId);

    /// The body's new revision — the Modified timestamp is the witness (D4).
    /// The body is never mutated in place; edit is a new revision.
    Task<NoteRow> UpdateNoteBodyAsync(string ownerId, string noteId, string body, IDocumentSession session);

    /// Physical removal — the body is gone, the note row is gone (the M17
    /// Bookmark shape). A dangling row is removable (D7 / F4 — no target read).
    Task<NoteDeleteStatus> DeleteNoteAsync(string ownerId, string noteId, IDocumentSession session);

    /// The tag is normalized — lowercase, trimmed, deduped, sorted (D5).
    Task<NoteRow> AddTagAsync(string ownerId, string noteId, string tag, IDocumentSession session);

    /// The tag is normalized — lowercase, trimmed, deduped, sorted (D5).
    Task<NoteRow> RemoveTagAsync(string ownerId, string noteId, string tag, IDocumentSession session);

    /// The "noted" state on reload — the F1 witness, zero audit, C-M37·2.
    Task<bool> HasNoteAsync(string ownerId, string targetKind, string targetId);

    // Folder-tree seams (D6)

    /// The depth ≤ 2 pin (D6); the (OwnerId, ParentId, Name) unique index is the witness.
    Task<FolderRow> CreateFolderAsync(string ownerId, string name, string? parentId, IDocumentSession session);

    /// The D6 witness — rename within the owner's tree.
    Task<FolderRow> RenameFolderAsync(string ownerId, string folderId, string newName, IDocumentSession session);

    /// Moves the folder's notes to the root (FolderId = null) — D6; the note bodies are never lost.
    Task<FolderDeleteResult> DeleteFolderAsync(string ownerId, string folderId, IDocumentSession session);

    /// The D6 pin — the note and the folder are in the same owner's tree.
    Task<NoteRow> MoveNoteToFolderAsync(string ownerId, string noteId, string? folderId, IDocumentSession session);
}
```

The composition root (U02) wires `PersonalNoteService` over the
host-registered Marten `IDocumentStore` + the owning surfaces' read seams
(the list resolution uses) + the frozen `IAuthorizationService` (the D3
check only) + the **frozen** `MarkdownRenderer` (the D4 body render — read-
only, never mutated). **No** `INotificationService` (§deferred). The
non-owner path is the Web layer's: `[Authorize]` + a controller
`ownerId == User id` check, **404** on mismatch, **no** `AccessAudit` row
(§2.4).

### 2.3 the D3 write-lane visibility check

| Target state at attach time | Result | Row | Audit |
|---|---|---|---|
| visible to the actor (the target's own frozen `CanAsync(Read)` ⇒ Allow) | `Created` (fresh) / `AlreadyNoted` (dup — no-op) | one row (unique index is the witness — F1) | the **target's own** frozen audit row (M37 adds none) |
| invisible to the actor / absent (Deny, soft-deleted, gone) | `Refused` → **404** (never 403) | **no** row | **no** M37 Deny row survives (the M16 create-gate posture) |

The *same* `CanAsync(Read)` call the detail page used is the truth — M37
adds no branch (C-M37·3).

### 2.4 the D7 degraded-resolution mechanism

| Row's target state at list time | Row rendering | Leak surface |
|---|---|---|
| resolves to a doc visible to the owner | `Title` + `Link` set, `Degraded = false`, `Body` rendered | the target's own visible fields |
| absent / soft-deleted / no longer visible to the owner | `Degraded = true`, `Title` / `Link` = **null** → the closed `pn.list.degraded` label; **`Body` still rendered** (the D4 "content, not pointer" pin) | **exactly one static string** + the author's own body (the named trade) |

One list load, **zero** new authorization branches (C-M37·2).

### 2.5 pinned seam tests (exact names)

File `tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`:

1. `F1_AttachIdempotentOneRowOneCreated`
2. `F1_ReAttachAfterDelete_FreshCreated`
3. `F2_AttachInvisibleTarget_Refused_NoRow_NoDenyRow`
4. `F2_AttachAbsentTarget_Refused_NoRow`
5. `F3_OwnerList_ZeroAuditRows_ZeroCanSeeAsyncPasses`
6. `F3_NonOwnerList_404_NoAuditRow`
7. `F3_NonOwnerGetNote_404_NoAuditRow`
8. `F4_DegradedNote_BodyStillVisible_NoTitle_NoLink_NoId`
9. `F4_DegradedNote_ControlsStillWork`
10. `C_M37_4_BodyIsContent_NotPointer`
11. `C_M37_5_TagNormalization_Lowercase_Trimmed_Deduped_Sorted`
12. `C_M37_5_TagIsNotATagDoc`
13. `C_M37_6_FolderTree_DepthBounded_UniqueName`
14. `C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive`
15. `C_M37_6_NoteCannotLiveInAnotherResidentFolder`
16. `PersonalNoteService_MakesNoModerateCall`
17. `PersonalNoteService_MakesNoCanSeeAsyncCallOnList`
18. `PersonalNoteService_MakesNoAccessAuditRowOnOwnerList`

### 2.6 acceptance gate (U06 records)

The three acceptance tests, verbatim from the register's §gate:

- **(a) closed-loop** — a resident attaches a note to a post they can see
  → it appears in their `/notes` list under the `post` group with the
  body rendered → they edit the body (a new `Modified` timestamp) → they
  add a tag (the tag set is normalized — lowercase, trimmed, deduped,
  sorted) → they move the note into a folder they created (the
  `(OwnerId, ParentId, Name)` unique index is the witness) → they delete
  the note → it is gone; a second attach of the same target is a **no-op**
  — one row, one `Created`.
- **(b) handoff-authorization-boundary** — attaching a note to a post the
  resident *cannot* see ⇒ 404, no note row, no Deny row surviving; a
  `GlobalAdmin` who is not the owner of a note list gets a 404 with
  **no** `AccessAudit` row — the C-M37·2 zero-audit personal read.
- **(c) part-vs-whole-audit-completeness** — the owner's own list load
  commits **zero** `AccessAudit` rows — C-M37·2 — while the *write*
  lane's single `CanAsync(Read)` check is the target's own frozen
  decision (C-M37·3): the part (one attach) and the whole (the list)
  audit differently by design, and both pins hold together.

### 2.7 drift-guard (frozen once written)

The **frozen pins**: the frozen seams (`IAuthorizationService`,
`AccessAction`, `AccessVia`, `Decide()`, every existing `*DocTypes`
surface, the `MarkdownRenderer`); the D1–D9 decisions; the C-M37·1…8
invariants; the F1–F5 FACES + the named trade; the `PersonalNote` /
`NoteFolder` doc shapes (the exact field set, §2.1); the
`PersonalNotesDocTypes` surface (+ the unique indexes); the
`IPersonalNoteService` ctor + the 11 public methods + the
`NoteCreateStatus` / `NoteDeleteStatus` / `FolderDeleteResult` enums
(the ctor **composes** the frozen `IAuthorizationService` + the owning
surfaces' read seams + the frozen `MarkdownRenderer` — the added
dependencies are frozen seams, so they are composition detail, not a new
surface); the §2.3 / §2.4 tables; the **18** test names (§2.5); the
closed `TargetKind` set; the §kw-l key list. A change to any of these
after U00 is a **drift event** (`## U<m> — Drift pause` per the
unit-series rules; recorded in the §drift-guard drift log below).

## §kw-l key list (locked — U05's source of truth)

The `pn.*` keys × en/de/fr/da (24 keys total). Adjust names to the views
as U05 authors them; the **list** is what U05 registers — the
count/names are locked here so U05 and the parity tests agree.

1. `pn.nav`
2. `pn.list.title`
3. `pn.list.empty`
4. `pn.list.degraded`
5. `pn.list.kind.post`
6. `pn.list.kind.grouppost`
7. `pn.list.kind.event`
8. `pn.list.kind.page`
9. `pn.list.kind.project`
10. `pn.list.kind.todo`
11. `pn.list.kind.board`
12. `pn.list.kind.document`
13. `pn.list.kind.directory`
14. `pn.note.edit`
15. `pn.note.delete`
16. `pn.note.move`
17. `pn.note.tag.add`
18. `pn.note.tag.remove`
19. `pn.note.attach`
20. `pn.note.attached`
21. `pn.folder.new`
22. `pn.folder.rename`
23. `pn.folder.delete`
24. `pn.folder.empty`

## §deferred (each its own ADR)

- The "nudge the author on note" notification lane (a follow-on lane, own
  ADR).
- Note **sharing** / per-surface "top noted" stats.
- Note **search** (a follow-on lane, own ADR).
- The **platform-wide tag vocabulary** (the M26 `Tag` doc lane — M37's
  tags are *personal* labels, not a shared vocabulary).
- **Folder drag-and-drop reordering** (a follow-on lane, own ADR).
- **Note export** (a follow-on lane, own ADR).

## Feedback loops

The 18 pinned seams (§2.5) cover the service boundary (attach
idempotency, the 404 split, the zero-audit read, the degraded row with
body survival, the body-is-content pin, the tag normalization, the
folder-tree depth/unique-name/other-resident pins, and the three
"makes no call" pins); the Web pins cover the list rendering, the button
state, the folder-tree rendering, the tag chips, and the non-owner 404;
the three acceptance gates (§2.6) cover the closed loop, the
authorization boundary, and the part-vs-whole audit split. Watch: a
`pn.list.degraded` row that *resolves to a title* (a leak), a degraded
row whose *body is hidden* (a D4 violation), a list load that *commits an
`AccessAudit` row* (an audit leak), or a tag set that is *not
normalized* (an idempotency leak) — all pinned red.

## Emergent impact

Privacy **improves** by construction: the surface's only leak surface on a
dangling row is one static string + the author's own body (the named
trade), the non-owner 404 leaks nothing (C-M37·2), and attaching a note to
a target *cannot* exist for a target the actor cannot see (C-M37·3) — so
the list is a **subset** of the resident's already-visible world, never a
window into it. Trust: the list is the resident's own; nobody else — a
`GlobalAdmin` included — can read it, which is stronger than the content
surfaces themselves (an operator can *see* a public post, but not *that*
a resident wrote a note about it). Reliability: one row per (owner,
target), one unique index as the idempotency witness; the folder tree is
bounded in depth so there is no unbounded-recursion surface. Cost: two new
tables + four indexes; the list resolution is one extra read per row,
bounded by the resident's own note count.

## Local-optimization check

The part being optimized: **the resident's own memory** — one attach to
capture the thought, one folder to organize it, one tag to find it. The
whole pays: two small personal stores, one list query, one button on nine
detail views. It does **not** optimize a metric: there is no "top noted"
ranking, no per-surface count, no nudge (C-M37·7 / D8 / §deferred) — the
note is private by construction, so it cannot become an engagement lever.

## FACES check

**f**lexible — the closed `TargetKind` set means a new content surface
joins the note world with one vocabulary word + one list-resolution
branch, no schema change. **a**daptive — a dead note degrades to a label
with the body still visible instead of breaking the list or leaking
(F4). **c**oherent — the one rule (D3) means the note can never outlive
the visibility that justified it at creation, and the list never leaks
what the detail page doesn't; the body is *content* the author owns, not
a pointer that can dangle invisibly. **e**nergizing — one attach to
capture the thought, the folder tree to organize it. **s**table — two
documents, four indexes, one seam, no state machine, no toggle to drift;
the folder tree is bounded in depth (≤ 2) so there is no unbounded tree to
break. Consumed: the **stable** face pays for **flexible** — the closed
set is deliberately nine-wide, not open (adding a tenth kind is a drift
event). The named trade (body permanence vs. dangling-target leak surface)
is the priced cost.

## Rollout & rollback

Deploy: the `PersonalNotesDocTypes` surface delta-detects and applies at
boot (ADR 0004 §B.1 — idempotent, **zero migrations** for existing
surfaces); no seeder step, no data migration, no feature flag (D8 — there
is **nothing to roll back**: removing the `PersonalNote` + `NoteFolder`
tables + the `PersonalNotesDocTypes` registration restores the pre-M37
state; no row is referenced by anything). See `docs/OPS.md`.

## Risks

- **The list-resolution seam drifts from the owning surface's read
  seam** (a row that should degrade resolves, or vice versa) — pinned by
  F4 tests 8–9 + gate (c); the resolution must use the owning surface's
  *existing* seam, never a new one (unit-series rule 4).
- **A leak on a dangling row** (title/id/audit) — pinned by C-M37·5 +
  the named trade: the closed `pn.list.degraded` key is the **only**
  output for a dead target.
- **A degraded row whose body is hidden** (a D4 violation — the body is
  *content* the author owns, visible always) — pinned by F4 test 8 + gate
  (a).
- **An accidental `AccessAudit` row** on the owner's list or the
  non-owner 404 — pinned by F3 tests 5–7 + gate (b)/(c).
- **A tag set that is not normalized** (no idempotency witness) — pinned
  by C-M37·5 test 11 + gate (a).
- **A folder tree that grows past depth ≤ 2** or a folder `Name` that is
  not unique within the owner's tree — pinned by C-M37·6 tests 13–15.
- **The attach button wired into a surface whose detail decision differs
  from the D3 check** — the same `CanAsync(Read)` call is the single
  truth (F2); a divergent check is a drift event (§2.7).

## Integration step served

The value-chain arrow this moves: **understanding → decision** — the
resident's own thinking about a piece of content (what they want to
remember, what it means, how it connects) becomes a first-class,
organizable surface, so the next action (returning to it, organizing it,
acting on it) is frictionless. It is not a new signal and not a new
coordination surface; it is the resident's own memory over the surfaces
that already exist.

## World seams

One inbound world seam: the resident's existing sign-in (the list is
`[Authorize]`; the attach posts through the standard CSRF shape). Zero
outbound seams — the list links **back into** the platform (each row's
`Link` is a same-origin detail route), never out. No tool handoff is
created or depended on; the note is a pointer + a body into the surfaces
the resident already crosses.
