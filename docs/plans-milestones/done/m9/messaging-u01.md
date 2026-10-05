# M9 Messaging — U01 · Documents: `Conversation` + `Message` + `M9DocTypes` + boot wiring

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build. **Unit-series rule:**
> never touch files outside your own Deliverables; no tests beyond the pinned
> list (none in this unit); no new seams on frozen interfaces.
>
> **This unit is docs-adjacent to code:** it ships the two domain POCOs +
> the Marten registration surface + the two boot-path wirings. **No test
> here** — U03 owns the first M9 test.

## Goal

Create the two POCOs (namespace `Kumunita.Core.Messaging`), the
`M9DocTypes.Configure(StoreOptions)` document surface (with a
`(ConversationId, Created)` index on `Message` for thread reads), and wire
it into **both** boot paths (the dev loop in `Kumunita.Web/Program.cs` +
`Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — the M3 U3 precedent).

## Context (the locked shapes to copy verbatim)

- **`Conversation`** (ns `Kumunita.Core.Messaging`): `Id` (Marten
  default surrogate string), `ParticipantA` + `ParticipantB` (the two
  resident `SubjectId`s — **store them normalized so `ParticipantA <
  ParticipantB` lexicographically**, which makes "open the same pair in
  either order returns the same conversation" trivially correct + lets
  `M9DocTypes` put a **unique index on `(ParticipantA, ParticipantB)`** for
  the idempotent-open invariant). `Created`, `LastMessageAt` (nullable,
  updated on each send — the `ListConversationsAsync` newest-activity-first
  sort key).
- **`Message`**: `Id` (surrogate string), `ConversationId` (references the
  conversation), `SenderId` (the resident `SubjectId` who sent it), `Body`
  (plain text, the 2000-char cap enforced in the service **not** here — this
  is the doc, the rule is U03's), `ReadBy` (nullable string — the recipient
  `SubjectId` who has read it, `null` = unread; the D8 personal-read marker),
  `Created`.
- **`M9DocTypes`** (ns `Kumunita.Core`, mirroring `M3DocTypes` /
  `M1DocTypes`): `Configure(StoreOptions opts)` →
  `opts.Schema.For<Conversation>()` with the unique index on
  `(ParticipantA, ParticipantB)`; `opts.Schema.For<Message>()` with an index
  on `(ConversationId, Created)`. Marten-native, **no** hand-rolled
  `FeatureSchemaBase` (ADR 0004 §B.1).
- **Boot wiring (the M3 U3 precedent):** add `M9DocTypes.Configure(opts);`
  next to the existing `M3DocTypes.Configure(...)` (or `M1DocTypes`) call in
  **both** `Kumunita.Web/Program.cs` (dev loop) and
  `Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (all-env). One line each.

## Entry reads (5)

1. `docs/design/m9-messaging-design.md` §documents (the locked shapes).
2. `src/Kumunita.Core/M3DocTypes.cs` (the registration shape to mirror —
   **read the actual current file**, not this plan's paraphrase).
3. `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the boot wiring point —
   find the existing `…DocTypes.Configure` call to place `M9DocTypes` next
   to).
4. `src/Kumunita.Web/Program.cs` (the dev-loop boot path — same).
5. `docs/adr/0004-data-persistence-and-schema-evolution.md` (§B.1 only).

## Deliverables (5)

- `src/Kumunita.Core/Messaging/Conversation.cs` — the POCO above.
- `src/Kumunita.Core/Messaging/Message.cs` — the POCO above.
- `src/Kumunita.Core/M9DocTypes.cs` — the registration surface + the two
  indexes.
- `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — one line added.
- `src/Kumunita.Web/Program.cs` — one line added.

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `M9DocTypes` compiles; both
boot paths reference it. **No test in this unit.**

Append a `## U01 — documents + M9DocTypes + boot` section to the handoff
note: (a) the `M9DocTypes` `.Schema.For` lines + the two index definitions
exactly as written, (b) the two boot-path insertion points (file + the
neighbor line it sat next to), (c) the `ParticipantA < ParticipantB`
normalization rule as written (U03 depends on it), (d) any schema shape that
drifted from the design doc (record it, don't silently fix it).

**Last action:** once the Exit above is satisfied and the `## U01` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u01.md` to
`docs/plans-milestones/done/m9/messaging-u01.md`. Each unit moves only its own
file as it completes — U02's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
