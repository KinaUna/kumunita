# ADR 0167 — M37: Personal notes (a private note a resident attaches to
any content entity; organizable: a personal folder tree + free author-set
tags; author-only)

Status: Accepted
Date: 2026-10-14
Extends the **M17 `Bookmark` personal-by-id precedent** (`Kumunita.Core.Bookmarks`
— ADR 0118: the owner's own note list is a **personal read** with **zero
audit surface**, the non-owner 404 commits **no row**, the **unique-index-
as-witness** idiom on `(OwnerId, TargetKind, TargetId)`), the **ADR 0105
(M9 messaging) participant-by-id shape** (the *primary* precedent — a
**personal-by-id record** whose access story is **identity comparison**,
never an audience decision; the non-owner — an operator included — gets a
**non-leaky 404** that commits **no** `AccessAudit` row), the **ADR 0004
§B.1 delta-detected, idempotent, zero-migration document-surface shape**
(the new `PersonalNotesDocTypes` surface), the **frozen
`IAuthorizationService` seam** (the 4-method surface + the
`IDocumentSession` overloads, C3 — ADR 0006 §A), the **frozen
`AccessAction` / `AccessVia` sets + the frozen `Decide()`** (ADR 0006 —
"M37 adds **no adapter at all**" pin), the **M16 create-gate posture**
(Deny ⇒ 404, the Deny row does not survive), the **M5 `KanbanStatuses`
string-not-enum shape** (the closed `TargetKind` set), the **M31 / ADR
0031 frozen `MarkdownRenderer`** (the D4 body render path — the one-
renderer pin), the **M26 `Tag` doc's `Name` normalization shape** (ADR
0044 — the D5 tag normalization, re-applied to *personal* labels), the
**M14 / M16 author-only edit-lane shape** (ADR 0014 / ADR 0016 — the D4
body-revision shape: the body is **never** mutated in place; edit is a
new revision, the `Modified` timestamp is the witness), and the **closed
`KnownTranslationKeys` registry + the en/de/fr/da parity pins** (ADR 0015
/ ADR 0052). This ADR makes **a resident's organized notes real** (the
ARCHITECTURE.md M37 value-chain row, verbatim): **a private note a
resident attaches to any content entity** — a standalone core surface over
**two documents, one new surface, one service seam, and zero new
authorization surface** (C-M37·1), with **zero migrations for existing
surfaces** (D1).

## Context

M17 (Bookmarks) shipped the **personal-pin** world: one row per
(resident, target), one list, one toggle — a *pointer*, not content. The
neighborhood today has a place to say *these things are mine to come back
to*, but nowhere to say *here is what I think about this, and these
thoughts go together*. M37 ships the **personal-note** world: a **body of
the resident's own writing** attached to the target (a *note*, not a
pointer), and the notes are **organizable** — the resident **nests them
into a personal folder tree** (categories, bounded in depth) and adds
**free author-set tags**.

M5 (Projects) already proved the core-surface shape M37 copies: a
**standalone core surface** on its own bounded context, its own
`*DocTypes` Marten surface (the ADR 0004 §B.1 parallel-surface shape —
delta-detected, applied idempotently at boot, **zero migrations for
existing surfaces**), always available with **no** off-by-default toggle.
M17 (Bookmarks, ADR 0118) adds the **primary** precedent: a
**personal-by-id record** whose access story is **identity comparison**,
never an audience decision — the owner's own read is a **personal read**
that **never audits** ("reads never audit"), and a non-owner — an
operator included — gets a **non-leaky 404** that commits **no**
`AccessAudit` row. The M17 `BookmarkDocTypes` unique-index precedent adds
the **unique-index-as-witness** idiom the `PersonalNotesDocTypes` unique
index on `(OwnerId, TargetKind, TargetId)` copies (F1 — a second attach of
the same target is a no-op **at the DB layer**).

The constraint that shapes the decision is the same one that shaped M5 /
M9 / M17, and sharper: **compose the frozen seams, never extend them —
and M37 extends them by adding nothing** (C-M37·1) — no new `AccessAction`,
**no** new `AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService`, and **no adapter at all** (D2 — a personal note
is a *personal-by-id* record, the ADR 0105 shape, not an audience
decision). The **one rule that keeps M37 safe** (D3): you may only attach
a note to **what you can see** — the write lane re-runs the target's
**own** frozen `Read` decision (the *same* `CanAsync(Read)` the detail
page uses) and refuses **404** when the target is not visible; a note
**grants nothing**. The surface stays **core** (D8) — M37 is a
**milestone**, not a **lane** (the ADR 0101/0105 `LocaleSettings` toggle
shape **inverted**: there is **no toggle at all**).

M37 is the **last** milestone on the roadmap — the close flips M37 →
done and promotes **no** successor (M37·10, the M36·10 / M35·11 framing,
re-applied).

## Decision

**D1 — One new bounded context `Kumunita.Core.PersonalNotes`, two
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
**No** `Audience`, **no** `IsDeleted`, **no** `ComponentId`, **no**
`LanguageCode`. *Forbids:* a separate document per surface; a
`PersonalNote`-per-kind set of tables; an enum type for `TargetKind`; a
migration of an existing surface; reusing the M17 `Bookmark` doc (M37 is
a *new* context — the M17 surface is untouched).

**D2 — A personal note is a personal-by-id record, never an audience
decision.** M37 adds **no adapter** (there is no
`PersonalNoteToAuditableResource`), **no** new `AccessAction`, **no** new
`AccessVia`, **no** branch in `Decide()`, **no** method on
`IAuthorizationService` (the M17 `Bookmark` / M9 ADR 0105 shape — a
*participant-by-id* / *owner-by-id* record, not an audience decision).
The **owner's** own note list is a personal read (like the M6 inbox / M17
bookmark list): `Where(n => n.OwnerId == ownerId)`, **no** `CanSeeAsync`
pass over the note rows, **no** `AccessAudit` row (C-M37·2). A **non-
owner** (a `GlobalAdmin` included) gets a non-leaky **404** — and that
404 commits **no** `AccessAudit` row either (the ADR 0105 "operator has
no read standing" precedent). *Forbids:* a
`PersonalNoteToAuditableResource`; running the owner's own note list
through `IAuthorizationService`; a new `AccessAction`/`AccessVia`.

**D3 — Note-attaching requires visibility; note-attaching grants
nothing.** The write lane (attach a note to a target) **re-runs the
target's frozen `Read` decision** (`CanAsync(Read)` over the target's
existing adapter — the *same call the detail page uses*) and **refuses
404** (not 403 — the Deny row **does not survive**, the M16 create-gate
posture) when the target is not visible. The note row itself **never**
appears in any `CanSeeAsync` pass, never widens an audience, and never
confers standing. *Forbids:* a note on an invisible target; a note
consulted by any read decision anywhere; a `GlobalAdmin` peek branch into
the target's visibility on the write lane (the detail page's own decision
is the truth).

**D4 — A note is *content*, not a pointer: it carries a body, and the
body is owned by the author.** `PersonalNote` carries `Body` (the
resident's own writing, stored as raw text, Markdown-rendered on read via
the **frozen** `MarkdownRenderer` — the M31 / ADR 0031 shape) + `Modified`
(the body's revision witness). M17's `Bookmark` has **no** body; M37's
`PersonalNote` **does**. The body is **never** mutated in place — edit is
a **new revision** (the M14 post-edit / M16 group-post-edit author-only
lane, the `Modified` timestamp is the witness). The body is **visible to
the author always** — even when the target is gone (the D7 degraded shape;
the named trade). *Forbids:* a `Bookmark`-shaped `PersonalNote` (no body);
a body that mutates in place (no edit = new revision); a body that is not
visible to the author when the target is gone (the D7 pin).

**D5 — Tags are author-set, free-form, per-note; the tag set is
normalized on write.** A note's `Tags` is an `IReadOnlyList<string>` (or
a JSON column — U01's call, mirroring the codebase's existing list-of-
strings shape). On write, each tag is **lowercase, trimmed, deduped,
sorted** (the M26 `Tag` doc's `Name` normalization shape, the idempotency
witness). A tag is **not** a platform-wide `Tag` doc (the M26 tag lane is
a *shared* vocabulary; M37's tags are *personal* labels — different
shapes, different surfaces, different invariants). *Forbids:* a
`PersonalNote` that references the M26 `Tag` doc's `TagId`; a tag set that
is not normalized (no idempotency witness); a tag that is platform-visible
(M37's tags are *personal* labels, not a shared vocabulary).

**D6 — The folder tree is a personal tree, bounded in depth, unique in
name.** Each resident has their own tree of `NoteFolder` rows, keyed on
`OwnerId` + a bounded `ParentId` (depth ≤ 2: root → child → grandchild —
the "nested" shape, not an arbitrary tree). A folder's `Name` is unique
within the owner's tree (the `(OwnerId, ParentId, Name)` unique index is
the witness). A note's `FolderId` is the **owner's** folder id (the note
and the folder are in the same owner's tree — a note cannot live in
another resident's folder). A folder's deletion **moves** its notes to the
root (`FolderId = null`) — the note body is never lost. *Forbids:* an
arbitrary-depth folder tree (depth ≤ 2); a folder `Name` that is not
unique within the owner's tree; a note that lives in another resident's
folder; a folder deletion that removes note bodies.

**D7 — The list degrades, never leaks; the note body survives the
target's death.** A note whose target is absent, soft-deleted, or no
longer visible renders the **generic** `pn.list.degraded` label (a closed
`kw-l` key) — **no title, no link, no id** — and the note's **body
remains visible** to the owner (the D4 "content, not pointer" pin). The
note controls (edit / move / tag / delete) still work on the degraded row
— none of them require the target's own read seam (they are keyed on the
note's own id + the owner's own tree). One list load, **zero** new
authorization branches (C-M37·2). *Forbids:* a 404/403 on a dangling note
row; leaking a removed target's title; a note body that is not visible to
the owner when the target is gone; a per-row `CanSeeAsync` branch in the
list beyond the owning surface's existing seam.

**D8 — M37 is a standing core surface, not a lane: no off-by-default
toggle.** M37 is a standing core surface (like M5 Projects, M17
Bookmarks): **no** off-by-default admin toggle, **no** `LocaleSettings`-
style admin switch, **no** `KumunitaFeature` admin flag. It is always
available to the community. (M9's off-by-default toggle was a *lane*; M37
is a *milestone*.) *Forbids:* an off-by-default admin toggle for M37; a
`LocaleSettings`-style switch.

**D9 — The close flip is one atomic unit (U06) — the six-member close
flip.** M37 is the **last** milestone on the roadmap (no `M38` to promote
— the M36·10 / M35·11 framing, re-applied). The M37 close (U06) flips the
roadmap parity **together**: `Milestones.cs` M37 `StatusNext` →
`StatusDone` (and **no** `M38` row); `MilestonesTests.cs` re-pinned to
the **"last milestone, no successor"** pin (M37 → done, **no** successor —
the M36 close's pin shape); the README **Roadmap** (M37 → done) + the M37
**Status** line; `docs/STATUS.md`; `docs/ARCHITECTURE.md`'s value-chain
table gains the **M37 row**; **and** `src/Kumunita.Web/WhatsNew.cs` gains
the **M37 entry** (the **required sixth member** — ADR 0110; the M36
close's "six-member close flip" shape). All six ship **in the same unit**
(the M36 close-flip precedent). *Forbids:* reordering M37; adding an `M38`
row; editing a shipped milestone's text; breaking the `MilestonesTests`
pins; flipping one surface and not the others; **omitting the
`WhatsNew.cs` entry** (the M27-shipped-without-entry drift, caught in
review — the ADR 0110 "required sixth member" pin).

## Consequences

**Positive:**

- **A resident's organized notes are real** (the README M37 row,
  end-to-end): a resident attaches a note to a post, group post, event,
  page, project, to-do, board, document, or directory from the detail
  surface's note button; the note appears in `/notes` under its kind group
  (and nested under the owner's folder tree); the body renders via the
  frozen `MarkdownRenderer`; a second attach of the same target is a
  **no-op** (one row, one `Created` — the unique index is the F1
  witness); editing the body is a new revision (the `Modified` timestamp
  is the D4 witness); adding a tag normalizes the set (lowercase,
  trimmed, deduped, sorted — the D5 witness); moving the note into a
  folder the resident created rides the `(OwnerId, ParentId, Name)` unique
  index (the D6 witness); deleting the note removes the row (the M17
  `Bookmark` physical-removal shape). A dead note degrades to the closed
  `pn.list.degraded` label and the **body stays visible** (D7 / F4).
- **Zero new authorization surface — the strongest form** (C-M37·1):
  M37 adds **no adapter at all** (D2), **no** new `AccessAction`, **no**
  new `AccessVia`, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` — the **only** authorization call in the
  entire milestone is the D3 write-lane check, which is the **target's
  own** frozen `CanAsync(Read)` (M37 adds no decision of its own). The
  frozen seams are **byte-identical** after M37 (§drift-guard).
- **The note is a subset of the visible world, never a window into it**
  (C-M37·3): a note can only exist for a target the actor could see at
  attach time, and the note never appears in, widens, or is consulted by
  any `CanSeeAsync` pass anywhere — a `GlobalAdmin` who is not the owner
  cannot read a resident's note list (a non-leaky 404, no `AccessAudit`
  row — the ADR 0105 "operator has no read standing" precedent, C-M37·2).
- **Zero migrations for existing surfaces** (D1): the
  `PersonalNotesDocTypes` surface is **parallel** (the ADR 0004 §B.1 shape
  — delta-detected and applied idempotently at boot) — the two new
  tables + the four indexes are additive; the `CoreDocTypes` …
  `BookmarkDocTypes` surfaces are **byte-identical** after M37
  (§drift-guard).
- **The body is content, not a pointer** (C-M37·4 / D4): the body
  survives the target's death — the resident's list does not shrink under
  them with no explanation; the body is visible to the author always, and
  the leak surface of a dangling row is exactly **one static string**
  (`pn.list.degraded`) + the author's own body (the named trade).
- **A milestone, not a lane** (D8 / C-M37·7): M37 is **always**
  available to the community (the M5 Projects / M17 Bookmarks shape) —
  **no** off-by-default admin toggle, **no** `LocaleSettings`-style
  switch — the surface does not depend on an admin's opt-in (C-M37·7).
- **The owner's own note list is a personal read, audited zero**
  (C-M37·2 / F3): loading one's own notes commits **no** `AccessAudit`
  row and runs **no** `CanSeeAsync` pass over the note rows — the
  strongest form of the M6-inbox / M17-bookmark personal-read shape.

**Neutral / cost:**

- **The named trade — the note body's permanence vs. the dangling-target
  leak surface**: the row (and its body) survives its target's death
  (soft-deleted post, removed event, hidden to-do, gone page) instead of
  being silently removed, and the invariant that keeps that safe is
  **C-M37·5** — the closed `pn.list.degraded` label with no title, link,
  or id, + the author's own body (which is visible to the author always
  by D4).
- **The list resolution is one extra read per row**, bounded by the
  resident's own note count; each row resolves through the owning
  surface's **existing** read seam (D7 — no new branch).
- **The closed `TargetKind` set is deliberately nine-wide, not open**
  (D1): a new content surface joins the note world with one vocabulary
  word + one list-resolution branch; adding a tenth kind is a **drift
  event** (the design doc's §drift-guard).
- **The folder tree is bounded in depth (≤ 2)** (D6): the "nested" shape,
  not an arbitrary tree — depth ≤ 2 means root → child → grandchild, so
  there is no unbounded-recursion surface; a folder's deletion moves its
  notes to the root (the body is never lost).

**Follow-on lanes (each its own ADR — the design doc's §deferred):** the
"nudge the author on note" notification lane; note **sharing** / per-
surface "top noted" stats; note **search**; the **platform-wide tag
vocabulary** (the M26 `Tag` doc lane — M37's tags are *personal* labels,
not a shared vocabulary); **folder drag-and-drop reordering**; **note
export**.

Additive on 0118 [the M17 `Bookmark` personal-by-id precedent this ADR
generalizes from a pointer to a body + a folder tree + personal tags] +
0105 [the ADR 0105 participant-by-id shape — the *primary* precedent for
the zero-audit personal read + the non-owner non-leaky 404] + 0004 §B.1
[the `*DocTypes` parallel-surface shape — the new
`PersonalNotesDocTypes` surface, delta-detected, idempotent, zero
migrations for existing surfaces] + 0006-C3 [the audit-always-on pin —
the **only** audited call in M37 is the D3 write-lane check, which is the
target's own frozen `CanAsync(Read)` in the target's own transaction] +
0001-B [the author's choice is absolute — a note is *author-only* by
construction; a non-owner gets a non-leaky 404 and no audit row] + 0015
[the `kw-l` registry — the closed `pn.*` key block × en/de/fr/da joins the
`KnownTranslationKeys` registry].
