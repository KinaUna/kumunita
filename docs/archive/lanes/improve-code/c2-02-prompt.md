---
id: c2-02-prompt
title: "C2-02 — Translation lane: the rest of the surfaces (DoD #5 to ≤40)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C2-02 — Translation lane: the rest of the surfaces (DoD #5 to ≤40)

## What you are doing

You are the **third** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-2.md`). Your unit is **C2-02**.
C2-00 landed the honest ratchet gate (`adr: 0290` named exceptions); C2-01
(`5bf44a68`) collapsed the **Posts** surface onto the `TranslationLane` — its six
write forwarders retired, the `PostService` class baseline entry gone, the
`*Translation*` count down by 6. Your unit is the **DoD #5** line, the
**remaining surfaces**: drive the `*Translation*` method total to **≤40** and
keep **clone regions ≤5 000**.

The seam already exists and is proven (C06 + C2-01): one generic
`TranslationLane<TParent, TVar>` (`src/Kumunita.Core/Translations/TranslationLane.cs`)
parameterized by a per-surface `TranslationDescriptor` (ADR 0288). Several
surfaces already **construct** it; the collapse never finished because their
per-surface `*Translation*` methods are still there. The plan's D3 is one
sentence: *"One surface per commit, the method retires as it moves, the count
goes down per surface."* Your unit is that sentence, for **every remaining
surface**.

**You are NOT** doing C2-03…C2-08 (module surface #6, Marten allowlist #6,
still-large files #3/#4, the two untested services #7, the SEC pointer #8), and
NOT the 4-shrink god-types (the `EventService` class / `IEventService`
interface / the `cc_max`-22 method — ADR 0290's named seams). You are NOT
re-opening the lane itself (it works — you are *migrating onto* it), NOT
re-opening the C05/C07/C08 part-vs-whole splits or the C00 doctrine rules, and
NOT touching routes, rendered HTML, document schemas, or adding a feature.

**Do not re-fix the gate or the lane.** If a surface has a genuine quirk the
descriptor can't express, **stop and name it** (a descriptor option or an
`adr:`) — don't silently special-case it, don't rewrite the lane to fit one
surface.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `5bf44a68` (C2-01). Working tree clean except an untracked
  `docs/plans-milestones/c2-01-prompt.md` (leave it alone).
- Commit style: `feat(improve-code-2): C2-02 — <short description>` (one surface
  per commit if you split; the gate green after each).
- Read `AGENTS.md` + `.github/copilot-instructions.md` (the doctrine), the plan's
  **D3** + **C2-02** rows (your spec), **D2** (part-vs-whole), and
  `docs/philosophy/in-code.md` § "The ratchet, the split, the comment".
- Read the seam: `src/Kumunita.Core/Translations/TranslationLane.cs` + ADR
  `docs/adr/0288-one-translation-variant-lane.md`, and **C2-01's handoff entry**
  (`docs/archive/lanes/improve-code/improve-code-2-handoff-notes.md`) — it records the
  M3/M4 session-ownership split and the true count basis (60 → 54), which is
  the exact context you need.
- **Re-derive the count per surface from the code before touching anything** —
  the doctrine is "trust the code, not the plan's counts."

### The concrete starting numbers (measured at `5bf44a68`)

`*Translation*` lane methods (public `Get/Add/Update/Remove*Translation*Async`
on the Core services — the C06/ADR 0288 basis; the `CanAddTranslation` display
probes and `*TranslationPolicy` files are **not** counted):

- **Total: 54** (after C2-01). Target: **≤40** → retire **≥14** methods.
- **Wired — the lane is constructed, the forwarders just need to retire**
  (the per-surface method is a thin `=> _lane.XAsync(…)`): `EventService` 4 ·
  `AnnouncementService` 4 · `PageService` 4 · `ProjectService` 12 ·
  `BoardService` 4 · `TodoService` 4 · `GroupService` 4 ·
  `CommunityMembershipService` 4 = **40 methods**.
- **Never wired — the lane is absent; its `*Translation*` methods are
  self-contained clones** (wire a descriptor, then collapse): `SurfaceLabelsService`
  4 · `SiteContentService` 4 · `LocalizationService` 3 · `TagService` 1 =
  **12 methods**.
- The 2 `PostService` reads (`Get*TranslationsAsync`) stay — C2-01 retired the
  6 write forwarders; the reads are surface-specific shape the lane does not own.
- Baseline entries to **retire** as each method moves (shrink-only; a new
  over-ceiling lane method becomes a *new* entry at its current value).

## The decision to make first (name it in the handoff note)

C2-01 left this explicitly: the **wired** surfaces split two ways, and that
split decides *how* each one collapses.

- **M3 shape (controller owns the session, passes it in)** — the forwarder
  takes an `IDocumentSession` param. Collapsing it is a **pure move** (the
  session is already at the call site), exactly what C2-01 did for Posts.
  Re-point the controller at the lane, delete the forwarder, retire the entry.
- **M4 shape (service opens its own session; the controller has no store)** —
  collapsing the forwarder means **moving the session ownership to the
  controller** (give it the `IDocumentStore`, open the session there). That is a
  real change, not a move. It is also **net good** for the lane: it shrinks the
  controller-Marten surface (which feeds C2-04's allowlist). Do it deliberately,
  per surface, keeping the C3 same-transaction invariant (one
  `SaveChangesAsync`) and the standing re-check byte-identical.

For the **never-wired** surfaces, the first step is *adding* a descriptor (a few
lines, per ADR 0288: "a new translatable surface is a descriptor, not a copied
day of work"), unit-testing it (pure, no Postgres), then collapsing. That is a
larger unit than a wired collapse — order the never-wired surfaces last.

## Collapse the remaining surfaces (one per commit)

For each surface, in an order you choose (suggested: the wired M3/M4 surfaces
first — the lane is already there — then the never-wired ones):

- **Retire the per-surface `*Translation*` methods** (delete, not wrap); the
  `I*Service`/service + the controller code against the `TranslationLane` + its
  descriptor. The standing matrix, the audit row, and the C3
  same-transaction invariant move with the descriptor — byte-identical per
  surface.
- **Route the read lane(s)** through the lane's read surface if the surface
  still hand-rolls the query (one list path per surface).
- **Update** the controller call sites, the `*Translation*` doc `cref`s (they
  break on delete), and any test that named the old method. **Add** a
  descriptor unit test for any never-wired surface you wire.
- **Retire** the baseline entry as each method moves (shrink-only). **Re-run**
  the count after each surface and record before → after in the handoff note.
  Do not add a new `*Translation*` method to a service to compensate.

## Definition of done (C2-02)

- [ ] **Every remaining surface** is collapsed onto the `TranslationLane` +
      descriptor; the per-surface `*Translation*` write methods **retired**
      (deleted, not wrapped). The never-wired surfaces get a descriptor + a
      pure unit test first.
- [ ] **The count is ≤40** (from 54; record before → after on the same basis —
      public 4-verb lane methods on the Core services). No new `*Translation*`
      method added to a service to compensate.
- [ ] **Clone regions ≤5 000** (the gate measures this; re-run and record).
- [ ] **Behaviour unchanged:** standing decisions, audit rows, stored variant
      documents, and the allowed-language set are byte-identical per surface. No
      route or rendered-HTML change. Any M4→M3 session-ownership move keeps the
      C3 same-transaction invariant (one `SaveChangesAsync`).
- [ ] **Part-vs-whole passes:** the seam is the shared lane + per-surface
      descriptor — not a ceremony type. (The C13 cold read would verify each.)
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. Every migrated surface
      has a passing seam test (add → variant resolves → remove); **run** the
      affected suites (in-process runner — see `docs/dev/testing-and-ci.md` for
      the xunit.v3 quirk), don't just build.
- [ ] `tools/code/code-baseline.json` — retired entries removed (shrink-only;
      no entry raised). **Edit it textually** — a `json.load`/`dump` round-trip
      churns the `\u003C` escapes (C2-01 hit this; C06's practical note says so).
- [ ] `tools/code/code-check.ps1` — green.
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note gets a **C2-02** entry (continue the running log): which
      surfaces, the session-ownership decision you made per surface, the
      before → after count, the entries you retired, and the exact remaining
      list (so C2-03 / the reviewer can see DoD #5 is fully closed — **check
      the ≤40 box only if the measured count actually holds**; if it does not,
      say which line fails and why, per the doctrine).
