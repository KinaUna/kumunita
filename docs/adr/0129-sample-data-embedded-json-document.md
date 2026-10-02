# ADR 0129 — Development sample data moved to an embedded JSON document

**Status:** Accepted
**Date:** 2026-09-12
**Amends:** **0060** D1 (the de / fr / da event-translation baselines are now
sourced from the embedded JSON document rather than a C# literal, preserving the
"one registry, two lanes" shape), **0078** (the closed sample-account e-mail set is
now sourced from the same embedded document, so the set and the seeded corpus cannot
drift apart), and **0056** (the deploy-posture sample-data lane — the account and
credential shape is unchanged; only where the neighborhood data *lives* moves).
**Supersedes** none; the seeder's public API is preserved exactly.

## Context

The development sample data — a mock neighborhood of accounts, groups,
announcements, community + group posts with replies, published events with RSVPs,
tags, a resident blog, and the de / fr / da translations that make the locale
switcher exercisable — was **hard-coded** in `SampleDataSeeder.cs` as one method of
interleaved `new … { … }` document constructions. As the platform's surface grew
(M19–M23, the ADR 0123 extended profiles, the ADR 0059/0060 event translations),
that method grew into a several-hundred-line block of C# object literals. Two
consequences:

1. **Growing the test corpus cost a C# change every time.** The whole point of the
   sample data is to exercise features — most recently the **search** expansion
   (ADR 0091 / ADR 0124) — with a large, varied corpus. But every additional post,
   reply, event, translation, or tag had to be authored *in C#*, recompiled, and
   re-reasoned about. A developer who wants "just more searchable content" should
   not have to touch `SampleDataSeeder.cs` at all.
2. **The data and its invariants were entangled.** The two ADR-pinned,
   code-owned constants — `SampleAccountEmails` (ADR 0078, the closed set the
   production notification-suppression gate compares against) and
   `EventTranslationBaselines` (ADR 0060 D1, the de / fr / da rows the warm-boot
   backfill re-applies) — were derived *by hand* from the same literals `SeedAsync`
   used. They could only agree if a developer kept the three places in step.

## Decision

### D0 — The corpus is an additive, growing neighborhood

The ADR 0129 lane exists so the developer can keep *adding* content without a C#
change per item. That intent now covers the **whole** mock neighborhood, and the
corpus is grown in **additive** passes — each new lane's surface (more accounts,
posts, events, pages, and — the surface added in this revision — the `PL`
goals/projects/to-dos) is *appended* to the document, never a rewrite of what is
already there. The generator / edit is **additive by construction**: a new batch
of announcements, posts, events, pages, goals, projects, and to-dos lands on top
of the hero content (the "Community Cleanup Day" event with its de / fr / da
translations, the seven demo accounts) which stays byte-for-byte. All appended
bulk content is **English-only** (`translations: []`); the non-English surface
is carried by the hand-authored hero rows, so the demo never carries
English-into-foreign placeholder text. This revision appends: **25**
announcements, **50** community posts, **25** group posts (across the new groups),
**20** events, **5** goals, **10** projects (each with **10** to-dos), and the
sub-pages that bring the resident blog to its current depth.

### D1 — The sample neighborhood is one embedded JSON document

The mock neighborhood is described by a single `SampleDataDocument` POCO tree
(`Kumunita.Core/Bootstrap/SampleDataDocument.cs`) serialized to
`Kumunita.Core/Data/sample-data.json` and **embedded in the `Kumunita.Core`
assembly** (an `<EmbeddedResource>` item in `Kumunita.Core.csproj`, logical name
`Kumunita.Core.sample-data.json`). A developer grows the corpus — more accounts,
posts, replies, events, translations, groups, tags, blog pages — by editing that
**one file**, with **no C# change required**. That is the point of the lane: a large,
easily-extended corpus to exercise search (ADR 0091 / ADR 0124), translation
display, feeds, RSVPs, and the directory, without the data bloating the seeder's
source.

`SampleDataSeeder` is thereby demoted from "the data" to a **generic materializer**:
it loads the document (once, cached), resolves the file's human-readable
cross-references (accounts by e-mail, tags / groups by slug, blog pages by parent
slug) to Marten document ids, and stores the documents. The materialization order
and the ADR 0056 / 0078 / 0060 behavior are unchanged.

### D2 — The two ADR-pinned constants are derived from the same document

Both code-owned constants read the **same** `SampleDataDocument` instance
(`LoadDocument()`, lazily cached):

- **`SampleAccountEmails` (ADR 0078)** is now the `accounts` list's e-mails
  (case-insensitive). Because the seeder materializes *from that same list*, the
  closed set and the seeded corpus **cannot drift apart** — the drift that hand-
  derived constants invite is eliminated by construction.
- **`EventTranslationBaselines` (ADR 0060 D1)** is now each event's
  `translations` (keyed by the event's English title), so `SeedAsync` and
  `BackfillEventTranslationsAsync` read the **same** de / fr / da set — the
  ADR 0060 D1 "one registry, two lanes" shape, now *sourced from the JSON* rather
  than a C# literal. A fresh and a backfilled instance still agree.

### D3 — Fail-fast, not partial seed

A missing or undecodable embedded resource is a **hard, loud error**
(`InvalidOperationException`), not a silent partial neighborhood. The seeder is a
Development-only surface (gated on `SampleData__Enabled` **and** the pristine-DB
check, ADR 0056), so a broken file must surface immediately rather than seed an
empty mock neighborhood that looks "fine." A JSON account with an unknown elevated
role (a typo) is likewise a loud error, not a silently-created bogus EF role.

### D4 — Cross-references by stable, human-readable key (not id)

The document references entities by their **stable, human-readable keys**, never
by generated ids: accounts by **e-mail**, tags / groups by **slug**, blog pages by
**parent slug**. This is what makes the file editable without understanding the
Marten id scheme: a developer names an account `anna@examplium.com`, a tag
`baking`, a group `street-green`, and the materializer resolves them to the
correct `Tag.Id` / `Group.Id` / `Profile.SubjectId` / `Post.GroupId` it mints.

### D5 — The `PL` goals / projects / to-dos surface (ADR 0086 / ADR 0067)

The `PL` "goals & projects" lane (ADR 0086) and the M5 to-dos it hangs on
(ADR 0067) are seeded from the same embedded document, through two new
top-level keys — `goals` and `projects` — and two new POCO types
(`SampleGoal` / `SampleProject` + the nested `SampleTodo`):

- **Cross-references stay human-readable (the D4 shape).** A goal is keyed by
  its **title** (the "events keyed by English title" precedent, not a generated
  id); a project names its goal by **`goalTitle`** (null = a standalone
  project); and to-dos are **nested under their project**, so the only
  cross-document resolution the materializer does in this lane is
  `goalTitle → ProjectGoal.Id`. The seeder resolves `authorEmail` /
  `assigneeEmail` to user ids via the same `usersByEmail` table it already
  maintains — a typo is a loud `KeyNotFoundException`, never an orphaned row
  (the D3 fail-fast shape).
- **Materialized in the single `mt` session (invariant C3).** Goals are stored
  first (populating a `goalsByTitle` map), then projects (resolving their
  `GoalId` from that map), then each project's nested to-dos (keyed by the
  project id just minted). All of this is stored inside the *same*
  `IDocumentSession` as the rest of the corpus and committed **once** at the
  existing `SaveChangesAsync` — no new session, no new commit point.
- **Entity mapping.** `SampleGoal → ProjectGoal` (en-only; there is no
  `GoalTranslation` type — goals are authored in English), `SampleProject →
  Project` (a generic `ProjectTranslation` loop runs over its `translations`
  for the ADR 0088 lane — a no-op while the corpus is English-only, declared
  for generality mirroring the post / announcement / event / page translation
  lanes), and `SampleTodo → TodoItem` (the to-do's `ComponentId` is inherited
  from its project; `StartAt` is left null — a sample to-do has no start date).
  `LanguageCode = "en"`, `IsDeleted = false`, and the demo's `Audience = null`
  (public) shape are applied throughout, matching how the rest of the corpus is
  seeded.
- **Status vocabulary.** `SampleProject.Status` and `SampleTodo.Status` are
  plain state-label strings in the `KanbanStatuses` vocabulary
  (`not-started` / `in-progress` / `done` / `cancelled`), never an enum — the
  exact ADR 0086 / ADR 0067 pin (`Status?` string, `null` = none).

## Consequences

- **Positive** — the test corpus is now data, not code: growing it for search
  (ADR 0091 / ADR 0124), translation, feeds, or the directory is a one-file edit,
  no recompile of the seeder's logic.
- **Positive** — `SampleAccountEmails` and `EventTranslationBaselines` are derived
  from the *same* source as the seeded corpus, so the ADR 0078 and ADR 0060 D1
  invariants hold by construction rather than by a developer keeping three literals
  in step.
- **Neutral** — the seeder's **public API is preserved exactly**: the credential
  constants, `SampleAccountEmails`, `EventTranslationBaseline` /
  `EventTranslationBaselines`, `SeedAsync`, and `BackfillEventTranslationsAsync`
  keep their signatures; `Program.cs` and every caller are untouched.
- **Positive** — the `PL` goals / projects / to-dos surface (D5) is now part of
  the same one-file corpus: the goals page, the project feed, and the M5 to-do
  feed are all exercisable off a fresh seed, with the goal→project→to-do
  association intact (the ADR 0086 `Project.GoalId` / ADR 0067
  `TodoItem.ProjectId` shape). No new DocTypes surface is needed — the
  `ProjectGoal` / `Project` / `TodoItem` / `ProjectTranslation` documents are
  already registered by `M5DocTypes.Configure` (ADR 0086 / ADR 0088) and the
  tables already exist.
- **Constraint (D3)** — the embedded resource and a valid `accounts[].role` are
  load preconditions; a broken file fails the (Development-only) seed loudly rather
  than seeding partially.
- **Test coverage** — `SampleDataEventTranslationBackfillTests` (the ADR 0060
  warm-boot lane: create-if-missing / never-clobbers / idempotent) and
  `NotificationServiceTests` (the ADR 0078 suppression gate) both read the constants
  the seeder now derives from the JSON; both pass unchanged, confirming the
  derived constants still agree with the seeded corpus.

## Status note

`Milestones.cs`, the README Roadmap, and `MilestonesTests.cs` are **untouched** —
this is an internal data-representation change to the already-shipped development
sample surface (the ADR 0034 / 0035 named-lane precedent), not a roadmap milestone.
