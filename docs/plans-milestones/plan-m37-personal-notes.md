# Plan: M37 — Personal notes (a private note a resident attaches to any content entity; organizable: a personal folder tree + free author-set tags; author-only)

> **M37 is the current in-progress milestone** (`Milestones.cs`: `M37` =
> `StatusNext`, and the **new last** milestone on the roadmap — there is
> **no** `M38` to promote; `MilestonesTests.cs` is already re-pinned to
> `M37_Is_The_Single_InProgress_Milestone_And_The_Last`). This register is
> the **milestone-level** plan (the secondary tier of the three-tier
> contract); each unit is a separate **self-contained** file under
> `docs/plans-milestones/in-progress/` (`m37-u00.md` … `m37-u06.md`) that a
> fresh agent can execute reading **only its own unit plan + its entry
> reads**. When a unit is done, its plan file is moved to
> `docs/plans-milestones/done/m37/` (the `done/` folder is created at the
> **close** (U06); U00–U05 move into it as they complete — the
> `in-progress/` → `done/` move is the M17 / M36 precedent).
>
> **Handoff notes (scratch tier):**
> `docs/plans-milestones/in-progress/m37-personal-notes-handoff-notes.md`
> — `## U#` sections appended (never rewritten) as each unit completes; a
> new agent reads the relevant `## U#` section *after* its own unit plan
> for context. The skeleton is authored by **U00**.
>
> **Design doc (primary tier):**
> `docs/design/m37-personal-notes-design.md` — authored by **U00** (this
> register's D1–D9, C-M37·*, F1–F5 + the named trade, the §kw-l key list,
> and the §gate names are [PROPOSED] here and **locked verbatim** into the
> design doc by U00). Every later unit reads the relevant design-doc
> section **verbatim**, not this register.
>
> **ADR:** `docs/adr/0167-personal-notes.md` (confirm **0167** is free
> against `docs/adr/README.md` — the index currently ends at **0166**) —
> authored by **U00**, `Status: Accepted`, with its row appended to the
> `docs/adr/README.md` index after 0166.
>
> **U00 is the sign-off gate.** No code unit (U01+) may start until U00 has
> locked the decisions into `docs/design/m37-personal-notes-design.md` and
> `docs/adr/0167-personal-notes.md`.
>
> **[PROPOSED] veto window.** The D1–D9 below are [PROPOSED] and
> **lockable** (U00 promotes them to Accepted in ADR 0167 + the design
> doc). A veto window applies: if a decision is vetoed before U00 locks
> it, the register + the affected unit plans are amended and the veto is
> recorded in the handoff notes. After U00 locks, a change is a **drift
> event** (design doc §drift-guard).
>
> **Atomicity contract (this register is sized for ~32K-context agents).**
> Every unit is small enough that an agent with a ~32K token context window
> can read **only its own unit plan + its entry reads**, execute, and hand
> off. Concretely, per unit:
>
> - **Entry reads: 4–8 files** (named, with a one-line why each). No "read
>   the whole project."
> - **Deliverables: ≤ 7 small files** (each named, with the exact required
>   content or the pinned test names it must contain).
> - **Exit: one `dotnet build Kumunita.slnx -c Debug`** + **one**
>   `dotnet exec` test-assembly run (the relevant test project), both
>   green. A unit that needs a second test assembly to prove its exit is
>   **too big** — split it. (U00 is docs-only: no build gate, no test gate.)
>
> **Unit-series rules (apply to every unit):**
>
> 1. **Never touch files outside your own Deliverables list.** If a change
>    is needed in another unit's files, note it in the handoff notes under
>    *your* `## U#` section and stop — do not edit it.
> 2. **Never rewrite the design doc outside its §drift-guard.** U00 authored
>    it; U01+ read it verbatim. A change is a drift event.
> 3. **No tests beyond the pinned list.** A unit adds **only** the pinned
>    tests named in its Deliverables; it does not re-run or re-name tests
>    from earlier units.
> 4. **The frozen seams are never extended.** `IAuthorizationService`
>    (4-method surface + the group lane), `AccessAction`
>    (`Read`/`Moderate` only), `AccessVia` (Owner/Admin + the existing
>    values), `Decide()`, and every existing `*DocTypes` surface are
>    frozen. M37 adds **no** adapter at all — a personal note is a
>    **personal-by-id** record (the M17 `Bookmark` / M9 ADR 0105 shape),
>    never an audience decision (D1).
> 5. **M37 is a core milestone surface, not a lane.** It has **no**
>    off-by-default admin toggle and **no** `LocaleSettings`-style admin
>    switch (D8 / C-M37·7) — it is always available to every resident,
>    like M5 Projects and M17 Bookmarks.
> 6. **M37 is the **last** milestone on the roadmap** — the close flip (U06)
>    flips M37 `StatusNext` → `StatusDone` and promotes **no** successor
>    (the M36·10 / M35·11 "last milestone, no successor" framing, re-
>    applied). **No** `M38` row is added.

---

## Understanding (one paragraph)

M17 shipped the **personal-pin** world: one row per (resident, target),
one list, one toggle — a *pointer*, not content. M37 ships the **personal-
note** world: a **body of the resident's own writing** attached to the
target (a *note*, not a pointer), and the notes are **organizable** — the
resident **nests them into a personal folder tree** (categories) and adds
**free author-set tags**. A note is **author-only** by construction
(privacy-first; ADR 0001-B) — a `GlobalAdmin` who is not the author gets a
non-leaky **404** and **no audit row** (the M17 / ADR 0105 shape). A note
whose target dies (soft-deleted post, removed event, hidden to-do, gone
page) **degrades** — it renders the closed `pn.list.degraded` label (no
title, no link, no id) and the note's **body remains visible to the owner**
(the note is *content*, not a pointer — the named trade). M37 builds
entirely on **frozen, verified seams** (the closed `TargetKind` set + the
owning surfaces' frozen read seams + the M17 `Bookmark` precedent + the
ADR 0004 §B.1 `*DocTypes` shape). It is a **milestone** (a roadmap letter,
not a named lane): the close unit (U06) flips `Milestones.cs` / the README
Roadmap / `MilestonesTests.cs` / `docs/STATUS.md` / `docs/ARCHITECTURE.md`
**+** `src/Kumunita.Web/WhatsNew.cs` (the six-member close flip —
`WhatsNew.cs` is the **required sixth member**, the AGENTS.md doc↔code
parity contract — D9). This design doc (authored U00, **LOCKED**) is the
**primary tier**; the register
`docs/plans-milestones/plan-m37-personal-notes.md` is the secondary tier;
the scratch handoff note is
`docs/plans-milestones/in-progress/m37-personal-notes-handoff-notes.md`.

## The one thing every unit must respect

**Personal-note semantics (locked in ADR 0167, U00):**

- **A note is a personal-by-id record, never an audience decision
  (D1/C-M37·1).** M37 adds **no adapter**, **no** new `AccessAction`,
  **no** new `AccessVia`, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` (the M17 `Bookmark` / M9 ADR 0105 shape). The
  **owner's** own note list is a personal read (like the M6 inbox / M17
  bookmark list): **no** `CanSeeAsync` pass over the note rows themselves,
  **no** `AccessAudit` row for the owner's own list. A **non-owner** (a
  `GlobalAdmin` included) gets a non-leaky **404** — and that 404 commits
  **no** `AccessAudit` row either.
- **Note-attaching requires target visibility; note-attaching grants
  nothing (D3/C-M37·3).** The write lane (attach a note to a target)
  **re-runs the target's frozen `Read` decision** (the same `CanAsync(Read)`
  the detail page uses) *before* the note row is created; Deny ⇒ **404**
  (not 403 — the Deny row **does not survive**, the M16 create-gate
  posture). The note row itself **never** appears in any `CanSeeAsync`
  pass, never widens an audience, and never confers standing.
- **A note is *content*, not a pointer (D4/C-M37·4 — the named trade with
  M17).** `PersonalNote` carries a **body** (the resident's own writing,
  Markdown-rendered on read, stored as raw text) — M17's `Bookmark` has no
  body. The body is **owned by the author** and is **visible to the
  author always** — even when the target is gone (the D7 degraded shape).
  The body is **never** mutated in place (edit = a new revision, the
  M14 post-edit / M16 group-post-edit author-only lane — the body's
  `Modified` timestamp is the witness).
- **The folder tree is a **personal** tree (D6/C-M37·6).** Each resident
  has their own tree of `NoteFolder` rows, keyed on `OwnerId` + a bounded
  `ParentId` (depth ≤ 2: root → child → grandchild — the "nested" shape,
  not an arbitrary tree). A folder's `Name` is unique within the owner's
  tree (the `(OwnerId, ParentId, Name)` unique index). A note's
  `FolderId` is the **owner's** folder id (the note and the folder are in
  the same owner's tree — a note cannot live in another resident's
  folder). A folder's deletion **moves** its notes to the root
  (`FolderId = null`) — the note body is never lost.
- **Tags are author-set, free-form, per-note (D5/C-M37·5).** A note's
  `Tags` is an `IReadOnlyList<string>` (or a JSON column — U01's call,
  mirroring the codebase's existing list-of-strings shape; the M26
  `Tag` doc's `Name` normalization is the precedent — **lowercase,
  trimmed, deduped, sorted** on write, so the tag set is idempotent). A
  tag is **not** a platform-wide `Tag` doc (the M26 tag lane is a
  *shared* vocabulary; M37's tags are *personal* labels — the two are
  different shapes, different surfaces, different invariants — U00 records
  the distinction in the design doc §2.1 doc-comment).
- **The list degrades, never leaks (D7/C-M37·5).** A note whose target is
  absent, soft-deleted, or no longer visible renders the **generic**
  `pn.list.degraded` label (a closed `kw-l` key) — **no title, no link, no
  id** — and the note's **body remains visible** to the owner (the D4
  "content, not pointer" pin). The note controls (edit / move / tag /
  delete) still work on the degraded row — none of them require the
  target's own read seam (they are keyed on the note's own id + the
  owner's own tree).
- **M37 is a standing core surface (no toggle) (D8/C-M37·7).** No off-by-
  default admin toggle, no `LocaleSettings`-style switch. Always available
  to every resident.
- **The close flip is atomic (D9/C-M37·8 — the six-member close flip).**
  U06 flips the roadmap parity **together**: `Milestones.cs` M37
  `StatusNext` → `StatusDone` (and **no** `M38` row — M37 is the **last**
  milestone); `MilestonesTests.cs` re-pinned (M37 → done, **no**
  successor, the "last milestone" pin); the README **Roadmap** (M37 →
  done) + the M37 **Status** line; `docs/STATUS.md`;
  `docs/ARCHITECTURE.md`'s value-chain table gains the **M37 row**;
  **and** `src/Kumunita.Web/WhatsNew.cs` gains the **M37 entry** (the
  **required sixth member** — ADR 0110). All six ship **in the same
  unit** (the M36 close-flip precedent — the M36 handoff note's
  "six-member close flip" shape).

---

## Assumptions / decisions — [PROPOSED, lockable by ADR 0167 in U00]

> U00 locks these into `docs/design/m37-personal-notes-design.md` +
> `docs/adr/0167-personal-notes.md` (verbatim) and records any veto. A
> `*Forbids:*` tail names the drift the decision guards against.

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
  **{`post`, `grouppost`, `event`, `page`, `project`, `todo`, `board`,
  `document`, `directory`}` — the exact vocabulary the existing
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
  invariants — U00 records the distinction in the design doc §2.1
  doc-comment). *Forbids:* a `PersonalNote` that references the M26
  `Tag` doc's `TagId`; a tag set that is not normalized (no idempotency
  witness); a tag that is platform-visible (M37's tags are *personal*
  labels, not a shared vocabulary).
- **D6 — The folder tree is a personal tree, bounded in depth, unique in
  name.** Each resident has their own tree of `NoteFolder` rows, keyed on
  `OwnerId` + a bounded `ParentId` (depth ≤ 2: root → child → grandchild —
  the "nested" shape, not an arbitrary tree). A folder's `Name` is unique
  within the owner's tree (the `(OwnerId, ParentId, Name)` unique index
  is the witness). A note's `FolderId` is the **owner's** folder id (the
  note and the folder are in the same owner's tree — a note cannot live
  in another resident's folder). A folder's deletion **moves** its notes
  to the root (`FolderId = null`) — the note body is never lost. *Forbids:*
  an arbitrary-depth folder tree (depth ≤ 2); a folder `Name` that is not
  unique within the owner's tree; a note that lives in another resident's
  folder; a folder deletion that removes note bodies.
- **D7 — The list degrades, never leaks; the note body survives the
  target's death.** A note whose target is absent, soft-deleted, or no
  longer visible renders the **generic** `pn.list.degraded` label (a
  closed `kw-l` key) — **no title, no link, no id** — and the note's
  **body remains visible** to the owner (the D4 "content, not pointer"
  pin). The note controls (edit / move / tag / delete) still work on the
  degraded row — none of them require the target's own read seam (they
  are keyed on the note's own id + the owner's own tree). *Forbids:* a
  404/403 on a dangling note row; leaking a removed target's title; a note
  body that is not visible to the owner when the target is gone; a per-
  row `CanSeeAsync` branch in the list beyond the owning surface's
  existing seam.
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
  close flip" shape). All six ship **in the same unit** (the M36 close-
  flip precedent). *Forbids:* reordering M37; adding an `M38` row;
  editing a shipped milestone's text; breaking the `MilestonesTests` pins;
  flipping one surface and not the others; **omitting the `WhatsNew.cs`
  entry** (the M27-shipped-without-entry drift, caught in review — the
  ADR 0110 "required sixth member" pin).

---

## Invariants (C-M37·1 … C-M37·8) — U00 locks these verbatim

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
  Loading one's **own** note list is the M6-inbox / M17-bookmark personal-
  read shape: **no** `CanSeeAsync` pass over the note rows, **no**
  `AccessAudit` row for the owner's own list, **no** owner-check bypass.
  A **non-owner** (a `GlobalAdmin` included) gets a non-leaky **404** —
  and that 404 commits **no** `AccessAudit` row either (the ADR 0105
  "operator has no read standing" precedent).
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

---

## FACES (F1–F5) + the named trade — U00 refines

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

---

## Approach

Three tracks + a close, each unit atomic and self-contained. **U00 is the
sign-off gate (no code unit may start before it).**

**Track A — Docs (U00).** The design doc (decisions/invariants/FACES +
trade/§kw-l key list/gate tests/§drift-guard) + ADR 0167 + the ADR index
row.

**Track B — Core (U01, U02, U03).** U01 the context docs (`PersonalNote` +
`NoteFolder`) + `PersonalNotesDocTypes` (+ the unique indexes). U02 the
note CRUD + the tag-normalization seam (`IPersonalNoteService` /
`PersonalNoteService`: `CreateNoteAsync` / `GetNoteAsync` / `ListNotesAsync`
/ `UpdateNoteAsync` / `DeleteNoteAsync` / `AddTagAsync` / `RemoveTagAsync`)
+ the D3 write-lane visibility check + the D7 degraded resolution + the DI
registration + pinned Core tests. U03 the **folder tree** seam
(`CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync` (moves
notes to root) / `MoveNoteToFolderAsync`) + pinned Core tests.

**Track C — Web (U04, U05).** U04 the **note button** — a shared
`_NoteButton` partial + the `POST /notes/attach` action (the D3 write-lane
visibility check at the Web layer) + wiring into the **nine** target detail
surfaces (post, group post, event, page, project, todo, board, document,
directory — the M37 `TargetKind` closed set). U05 the `/notes` list view
(the owner's notes, grouped by `TargetKind` and **nested under the owner's
folder tree**, the `pn.list.degraded` label on dangling rows, the per-row
edit / move / tag / delete controls, the tag chips) + the `pn.*` `kw-l`
keys (× en/de/fr/da) + the nav entry.

**Close (U06).** The three acceptance gate tests + the **six-member close
flip** (D9 — `Milestones.cs` + `MilestonesTests.cs` + README Roadmap/
Status + `docs/STATUS.md` + `docs/ARCHITECTURE.md` +
`src/Kumunita.Web/WhatsNew.cs`, in the **same unit**; M37 is the **last**
milestone — no successor to promote).

**Unit map** (sized for ~32K):

| Unit | Track | One-line goal |
|---|---|---|
| **U00** | A | Design doc + ADR 0167 (lock D1–D9, C-M37·*, F1–F5 + trade, §kw-l, gate tests, §drift-guard) |
| **U01** | B | `Kumunita.Core.PersonalNotes` context: `PersonalNote` + `NoteFolder` + `PersonalNotesDocTypes` (the unique indexes) + boot wiring |
| **U02** | B | `IPersonalNoteService` note CRUD + the tag-normalization seam + the D3 write-lane visibility check + the D7 degraded resolution + the DI registration + pinned Core tests |
| **U03** | B | the **folder tree** seam (`CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync` (moves notes to root) / `MoveNoteToFolderAsync`) + pinned Core tests |
| **U04** | C | the `_NoteButton` partial + the `POST /notes/attach` action (the D3 write-lane visibility check at the Web layer) + wiring into the nine target detail views + pinned Web tests |
| **U05** | C | the `/notes` list view (grouped by `TargetKind` + nested under the owner's folder tree, degraded rows, per-row edit / move / tag / delete, tag chips) + the `pn.*` `kw-l` keys (×4) + the nav entry + pinned Web tests |
| **U06** | Close | the three acceptance gate tests + the **six-member close flip** (D9, M37 is the **last** milestone — no successor) |

---

## Workflow (per unit, sized for ~32K)

Each unit agent:

1. Reads **its own unit plan** (`docs/plans-milestones/in-progress/m37-uNN.md`)
   in full.
2. Reads the **entry reads** listed in the unit plan (4–8 files, named, with
   a one-line why each).
3. If a **handoff note** exists for an earlier unit the current one depends
   on, reads that `## U#` section of
   `docs/plans-milestones/in-progress/m37-personal-notes-handoff-notes.md`.
4. Executes the **deliverables** (≤ 7 small files) named in the unit plan.
5. Runs the **exit gate**: `dotnet build Kumunita.slnx -c Debug` **+** the
   relevant `dotnet exec` test assembly, both green (U00 is docs-only:
   no build gate, no test gate).
6. **Appends** a `## U# — <one-line summary>` section to the handoff notes
   (the state it left the codebase in, any drift observed, any follow-on
   noted) — **never** rewrites an earlier `## U#` section.
7. Moves its own unit plan from `docs/plans-milestones/in-progress/` to
   `docs/plans-milestones/done/m37/`.

The next unit starts fresh, reading only its own plan + its entry reads +
the relevant `## U#` handoff sections.
