# ADR 0099 — A to-do created on the board inherits the board's audience

Status: Accepted
Date: 2026-09-26
Extends the **M5 board-lane surface** (ADR 0067 the standing matrix + the frozen
`IProjectService` convention; ADR 0068 the add-to-lane lane + the `board.add_todo`
audit row; ADR 0070 the board-update seam) and the **ADR 0001-B** copy-verbatim
audience convention (an `Audience` is written/copied verbatim — a `null` audience
is public, a non-null one is carried as-is — the `CopyTodoToBoardAsync` /
`UpdateBoardAsync` precedent) and the **ADR 0098** board audience-edit lane (the
board's `Audience` is now a stored, editable value rather than a frozen
creation-time choice). This ADR amends the one audience decision ADR 0068 had
pinned: the card created by the add-to-lane lane now carries the board's
audience instead of being forced public.

## Context

ADR 0068 pinned the add-to-lane lane (`AddTodoToLaneAsync`) to a public card —
the card's initializer carried `Audience = null` on the rationale "the board's
`Audience` is the gate (C-M5·3)". That rationale is only half right, and the
other half was a leak:

- **On the board, the card is correctly gated.** The board detail page runs a
  two-level decision (C-M5·3): first the board's own `Audience` (a viewer who
  cannot see the board cannot see any card on it), then, for each card the
  viewer *can* reach, the card's own `Audience`. When the card is `null`
  (public), the second level is a no-op and the board's gate alone decides. For
  a viewer of the board, the two-level decision already reduces to "the board's
  audience is the gate" — so a public card looks correct *on the board*.
- **In the standalone to-do feed, the card leaked.** The feed
  (`ListTodosAsync`) gates on the **to-do's own `Audience` only** — it has no
  notion of which board a card was added from (a to-do keeps its standalone form
  + any placements on other boards, C-M5·2; the board is a container, not the
  card's visibility). A card added to a **restricted** board therefore carried a
  `null` (public) audience and appeared in the feed for **everyone** — including
  residents the board's audience deliberately excludes. The "board is the gate"
  rationale simply does not hold off the board.

ADR 0098 made the board's `Audience` first-class (stored, editable, written
verbatim), so the board already *has* the audience the card should take. The
missing step was to hand it to the card at creation. The house convention for
doing so — copy the `Audience` verbatim, never re-derive it (ADR 0001-B) — is
already the shape of `CopyTodoToBoardAsync` and `UpdateBoardAsync`, so this
change is a one-line correction, not a new mechanism.

## Decision

`AddTodoToLaneAsync` (the card it mints) now sets

```csharp
Audience = board.Audience,   // a `null` board audience stays `null` / public;
                             // a non-null one is carried verbatim (ADR 0001-B).
```

in place of the pinned `Audience = null`. The board is already loaded and
null/`IsDeleted`-checked earlier in the lane, so `board.Audience` is in hand.

- **Verbatim, by reference — the ADR 0001-B shape.** The card's `Audience` is the
  board's `Audience` object, not a deep copy. This is exactly `CopyTodoToBoardAsync`'s
  precedent (`Audience = original.Audience`). It is safe because every write
  *replaces* the `Audience` reference on its own document (Marten serializes each
  document independently and the write lanes never mutate a shared `Audience`'s
  `Grants` in place), so a later audience-edit to the board or to the card cannot
  bleed across to the other.
- **`null` stays `null` (public).** A board whose audience is `null` still yields
  a public card — the pre-ADR 0068 behavior for the common case, unchanged.
- **Snapshot at creation, not a live link.** The card is its own `TodoItem`
  row with its own `Audience`. A later board audience-edit (ADR 0098) does **not**
  retro-change cards already added — the card keeps the audience it was created
  with. (A "cards follow the board" rule is a deliberate non-goal; it would
  require either mutating the card on every board edit or re-deriving the card's
  audience, both of which break the verbatim / never-re-derive convention.)
- **Nothing else changes.** No new seam, no signature change, no new index, no
  new `AccessAction` / `AccessVia` / adapter, no schema change, and no new audit
  verb — the existing `board.add_todo` `AccessAudit` row (the C-M5·6 standing
  matrix: creator ∪ GlobalAdmin over the board) is unchanged. The `Milestones.cs`
  / README Roadmap / `MilestonesTests.cs` triple is **untouched** (a named lane on
  the already-shipped M5 surface, not a milestone — the ADR 0086/0088/0089/0093
  precedent).
- **Web layer unchanged.** `AddTodoToLanePost` still posts only a `Title`; the
  audience now flows in from the board the card is added to, not from the form.
  The `IProjectService.AddTodoToLaneAsync` doc comment gains the inheritance pin
  (the "the audience inheritance (C-M5·3)" paragraph) so the contract is visible
  at the frozen seam.
- **Tests.** Two new `ProjectServiceTests` F12 pins:
  - `F12_AddTodoToLane_InheritsBoardAudience` — a board with a
    user-grant audience yields a card carrying that exact grant
    (`Mode = Any`, single `User` grant), and behaviorally the card is visible
    to the grantee in the feed and **invisible to a stranger** in the feed.
  - `F12_AddTodoToLane_PublicBoardYieldsPublicCard` — a `null`-audience board
    yields a card with `Audience = null` (public), visible to a stranger.

## Consequences

- The feed no-leak invariant (ADR 0012) is restored for board-added cards: a
  card's visibility is now the board's whether read **on the board** (the
  two-level decision, where card-audience == board-audience at creation makes
  the board gate alone sufficient) or **in the standalone feed** (where the
  card's own audience is the gate — now non-`null` for a restricted board).
- Public boards are unchanged in behavior: `null` → `null`, so a board with no
  audience restriction still yields public cards, exactly as before.
- A creator can now add a card to a board and be confident it does not surface
  in the public feed when the board is restricted — the card "inherits the
  audience of the board", as a resident would expect.
- Because the inheritance is a creation-time snapshot (verbatim, ADR 0001-B), a
  card added to a board **before** the board was restricted keeps its audience
  for as long as it exists; a board's later widening / narrowing (ADR 0098) does
  not reach back. This is the same "a card is its own document" wall as C-M5·2.
