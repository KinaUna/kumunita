# C2-01 — Translation lane: collapse the first surface onto the `TranslationLane`

## What you are doing

You are the **second** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/plans-milestones/plan-improve-code-2.md`). Your unit is **C2-01**, the
second of 9. C2-00 just landed (`8218f3fc`): the ratchet gate is honest and the
30 predecessor raises are no longer silent (they carry `adr: 0290`). Your unit
is the **DoD #5** line, first surface only — the **translation-lane collapse**.

The predecessor (`IMPROVE-CODE`) built the seam in C06: one generic
`TranslationLane<TDoc, TVariant>` (`src/Kumunita.Core/Translations/TranslationLane.cs`)
parameterized by a small per-surface descriptor (ADR 0288). Seven services
already construct it. But the collapse never finished — **each of those surfaces
still keeps its own `*Translation*` methods.** The write lanes are *thin
forwarders* into the lane (e.g. `EventService.AddEventTranslationAsync` opens a
session and calls `_translationLane.AddAsync(...)`); the read lanes often don't
use the lane at all (`GetEventTranslationsAsync` still hand-writes the
`session.Query<EventTranslation>()` query). The plan's D3 is one sentence:
*"The `*Translation*` methods were not collapsed onto them. One surface per
commit, the method retires as it moves, the count goes down per surface."*

**Your unit is exactly that one sentence, for the first surface.** Pick **one**
surface, move its `*Translation*` methods onto the `TranslationLane` + its
descriptor so the per-surface methods **retire** (delete them; the lane's public
surface is the seam the controller codes against), and **verify the count goes
down.** That is the unit. Everything else is C2-02.

**You are NOT** doing C2-02…C2-08. You are NOT the *rest* of the surfaces
(that is C2-02 — do not pre-migrate more than the one surface this unit owns,
even if it's easy). You are NOT the module-surface reduction (#6), the
Marten allowlist (#6), the still-large files (#3/#4), the two untested
services (#7), or the SEC pointer (#8). You are NOT the 4-shrink god-types
(A0290's named seams — C2-05 / the successor's explicit scope). You are NOT
re-opening the C06 lane itself (the `TranslationLane` + descriptor already
exist and work — you are *migrating onto* it, not re-architecting it), and NOT
re-opening the part-vs-whole splits (C05/C07/C08) or the doctrine rules (C00).
You are NOT touching routes, rendered HTML, document schemas, or adding a
feature.

**Do not re-fix the gate or the lane.** The ratchet (`tools/code/CodeMetrics`,
`code-check.ps1`) is honest (C2-00). The `TranslationLane` is the seam — build
on it. If a surface has a genuine quirk the descriptor can't express, **stop
and say which and why** — name it (a descriptor option or an `adr:`), don't
silently special-case it, and don't rewrite the lane to fit one surface.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `8218f3fc` (C2-00).
- Commit style: `feat(improve-code-2): C2-01 — <short description>` (conventional
  commits, em dash, no ADR in the subject). **One surface per commit** — if you
  do this in more than one commit, each commit is one surface and the gate is
  green after each.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/plans-milestones/plan-improve-code-2.md` — the sections
  **D3** and **C2-01** are your spec. Read **D2** (part-vs-whole) and
  `docs/philosophy/in-code.md` § "The ratchet, the split, the comment" — the
  method you delete must be a *move onto the seam*, not a ceremony extraction.
- Read the seam: `src/Kumunita.Core/Translations/TranslationLane.cs` + ADR
  `docs/adr/0288-one-translation-variant-lane.md` (the descriptor shape), and
  the predecessor's C06 record
  (`docs/archive/lanes/improve-code/c06-prompt.md` — the reference-surface
  method, the descriptor options, and the divergence-diff discipline).
- Read the C2-00 handoff note
  (`docs/plans-milestones/improve-code-2-handoff-notes.md`) so your entry
  continues the running log in the same shape.

### The concrete starting numbers (measured at `8218f3fc` — trust these + the
### code, re-derive per surface before touching anything)

- **46** public `*Translation*` methods across Core services (the DoD #5 count
  to drive **down**).
- **Already wired to the `TranslationLane`** (construct it): `PostService`,
  `EventService`, `AnnouncementService`, `PageService`, `ProjectService`,
  `BoardService`, `TodoService`. Per-surface method counts (public, in the
  service): `ProjectService` 12 · `PostService` 8 · `PageService` 5 ·
  `EventService` 4 · `AnnouncementService` 4 · `BoardService` 4 · `TodoService` 4.
  **These are your candidates** — the lane is already there; the methods are
  thin forwarders (writes) or hand-rolled reads.
- **Never wired to the lane** (their `*Translation*` methods are self-contained
  clones, not forwarders): `GroupService` 4 · `CommunityMembershipService` 4 ·
  `SurfaceLabelsService` 4 · `SiteContentService` 4 · `LocalizationService` 7 ·
  `TagService` 1. **These are harder** (the lane isn't there yet) — prefer an
  already-wired surface for this unit; a never-wired surface is a *larger* unit
  and belongs to C2-02 unless you can name why it's the right first one.
- The baseline (`tools/code/code-baseline.json`) carries the per-surface
  `*Translation*` method entries you will **retire** as each method moves.

## Pick the first surface, then collapse it

- **Pick one surface.** Default to an **already-wired** surface (the lane is
  present; you're collapsing forwarders + routing the read through the lane).
  `PostService` is the reference surface C06 diffed the others against — a good
  first pick — or `EventService` (4 methods, the cleanest forwarder set) if you
  read it and it's the tightest. Say which one and why in the handoff note.
- **Move it onto the seam.** For each `*Translation*` method on that surface:
  - **write lanes** (add/update/remove) — retire the per-surface method; the
    controller (and the `I*Service` interface) points at the `TranslationLane`
    + descriptor directly. The standing check, the audit row, and the
    same-transaction invariant (invariant C3) move with the descriptor — they
    must stay byte-identical per surface.
  - **read lane(s)** (list/get variants) — if the surface still hand-rolls the
    query, route it through the lane's read surface so there is one list path.
  - **Delete** the retired per-surface methods and retire their `I*Service`
    declarations. Update the controller call sites. Update any test that named
    the old method.
- **One surface per commit; the method retires as it moves.** After the surface
  is done, the count of `*Translation*` methods must be **lower** than 46 (re-run
  the count; record before → after in the handoff note). Do not add a new method
  to compensate — a new lane method over the ceiling becomes a *new* baseline
  entry at its current value, never a raise.

## Definition of done (C2-01)

- [ ] **One surface** chosen (named in the handoff note) and its `*Translation*`
      methods collapsed onto the `TranslationLane` + descriptor; the per-surface
      methods **retired** (deleted, not wrapped).
- [ ] **The count is down:** the `*Translation*` method total is **< 46**
      (record before → after). No new `*Translation*` method added to a service
      to compensate.
- [ ] **Behaviour unchanged:** standing decisions, audit rows, stored variant
      documents, and the allowed-language set for the migrated surface are
      byte-identical (the migration is a move, not a change). No route or
      rendered-HTML change.
- [ ] **Part-vs-whole passes:** the seam you moved onto is a real seam the
      C13 cold read would verify (the lane is shared; the descriptor is the
      per-surface difference) — not a ceremony type to hit a number.
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. The migrated surface has
      a passing test at the seam (add → variant resolves → remove); **run** the
      affected tests, don't just build.
- [ ] `tools/code/code-baseline.json` — the retired method entries removed
      (shrink-only; a new over-ceiling lane method becomes a *new* entry at its
      current value). No entry raised.
- [ ] `tools/code/code-check.ps1` — green on the current tree.
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note gets a **C2-01** entry (continue the running log): which
      surface, the before → after method count, the seams you retired, and the
      remaining surfaces (so C2-02 has the exact list).
