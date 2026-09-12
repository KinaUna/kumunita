# U4 execution plan (working) — group posts: `Post.GroupId` additive field

> My (unit U4's) working plan for this pass. The **authoritative spec** is
> `group-posts-u04-plan.md` (Goal / Entry reads / Deliverables / Exit) + the
> **master register** `plan-group-posts.md` (invariant G·2/G·8 pins) + the
> **design doc** `docs/design/group-posts-design.md` Part 2 **§2.2** (the
> **frozen** `Post.GroupId` line + doc-comment shape — U2 froze it; U5–U9
> implement against it). Prior section read: **U3** (in
> `group-posts-handoff-notes.md`) — the field is written **only** by
> `CreateGroupPostAsync` (U6) with `ComponentId = string.Empty` +
> `Audience = new Audience()` (G·2/G·8); **no filter** is added to the M3
> feeds (G12/G13 hold structurally — design doc §2.3(a), U2's note).

## Scope (in / out)
- **In (mine):** 1 file — **modify**
  `src/Kumunita.Core/Posts/Post.cs`: add exactly one property,
  `public string GroupId { get; set; } = string.Empty;`, with the pinned
  doc-comment (G·2 lane exclusivity + G·8 empty audience + ADR 0013 +
  ADR 0004 §B.1 additive precedent, the M3b `Status` ADD comment style).
  **One field, nothing else in this file** (the entry plan's Deliverables).
- **Out:** everything else — no `M3DocTypes.cs` change (verified read-only:
  `opts.Schema.For<Post>()` is registered there, line 26 — Marten's delta
  picks the new property up: **no re-seed, no new doc surface**, exactly
  M3b's `PostStatus` ADD lane), no `PostReply` change, no `PostService`
  change, no authorization lane (U5), no tests (U9), no build of the Web
  project as a deliverable (a solution build *runs*; the touched project is
  `Kumunita.Core`). **No** doc files touched, no ADR edit.

## Constraints I keep pinned while I write
- **The exact pin** (design doc §2.2 — frozen; any mismatch is a drift pause):
  `public string GroupId { get; set; } = string.Empty;` — `string` (not
  `string?`), default `string.Empty`, member **name** `GroupId`,
  **appended after** the M3b `Status` member (the "POCO members unchanged …
  single additive, appended" shape of §2.2).
- **Doc-comment content** (pinned): ADR 0013; **non-empty ⇒ group-lane post**
  (G·2); membership is the **sole** access decision with the audience lane
  **never** evaluated and the audience written non-null **empty** (G·1/G·8);
  `ComponentId` empty ⇒ structurally absent from `ListFeedAsync` /
  `ListAllFeedAsync` (§2.3(a)); only members may create or see it (G·3/G·4);
  empty ⇒ component post (M3/M3b) unchanged; written **only** via
  `PostService.CreateGroupPostAsync` (U6).
- **cref adaptation (build-green requirement):
  does not exist yet (U6's ADD — rule 4 forbids me creating it). The pinned
  comment names it; I write that one reference as a `<c>` literal (not a
  `<see cref>`) so the build stays green *now* and U6 can upgrade it to a
  cref when the member lands. All other crefs
  (`PostService.ListFeedAsync` / `PostService.ListAllFeedAsync`,
  `ComponentId`) resolve today.
- **M3b comment-style mirror:** `Status`'s member carries a `// M3b ADD
  (…)` marker line above its `<summary>` — I mirror it as a `// group posts
  ADD (…)` marker so the file's "one ADD per milestone" convention stays
  visible in-source.
- **No drift:** if the file state contradicts the §2.2 pin on any point not
  in my closed set above, I stop and record `## U4 — Drift pause` in the
  handoff note instead of improvising.

## Entry reads (done)
| Read | Why |
|------|-----|
| `docs/plans-milestones/in-progress/group-posts-u04-plan.md` | my sealed spec (Goal / Entry reads / Deliverables / Exit) |
| `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` (U3 section, prior) | U3's hand-off: field written only by U6's `CreateGroupPostAsync`; no filter on M3 feeds |
| `docs/design/group-posts-design.md` §2.2 (the `Post` block) | the frozen `GroupId` line + the pinned doc-comment wording |
| `src/Kumunita.Core/Posts/Post.cs` | the POCO + the M3b `Status` ADD comment style to mirror |
| `docs/adr/0004-data-persistence-and-schema-evolution.md` §B.1 (§B / §B.1) | the Marten-native additive rule: delta-detected, idempotent, no re-seed |
`opts.Schema.For<Post>()` already registered ⇒ **no change needed** here (cited in the handoff note, not modified) |
| `src/Kumunita.Core/Posts/PostService.cs` (member list only) | confirm `ListFeedAsync`/`ListAllFeedAsync` exist for the crefs; `CreateGroupPostAsync` correctly **absent** (U6) |

## Steps
1. Write this exec plan file (this step).
2. **Modify** `src/Kumunita.Core/Posts/Post.cs` — append after `Status`
   (inside the class, before the closing `}`), mirroring the `Status`
   ADD-block shape:
   - a `// group posts ADD (ADR 0013, ADR 0004 §B.1 additive — delta-
     detected, idempotent, no re-seed; the single new Post field after
     M3b's Status)` marker line;
   - the `<summary>` carrying the pinned comment (G·1/G·2/G·3/G·4/G·8 +
     §2.3(a) + "written only via `<c>PostService.CreateGroupPostAsync</c>`"
     (U6) + "empty ⇒ component post (M3/M3b), unchanged");
   - `public string GroupId { get; set; } = string.Empty;` — **exactly**
     the §2.2 pin.
3. **Build** — `dotnet build Kumunita.slnx -c Debug` (the runner path
   AGENTS.md pins); the touched project is `Kumunita.Core`.

## Exit (per the unit plan's "Exit" + the master workflow template)
- `Kumunita.Core` builds **green**; `Post` gained exactly one member
  (`GroupId`), `PostStatus` / `PostReply` / all other members byte-
  untouched.
- Handoff section (heading `## U4 — Post.GroupId additive`) appended to
  `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` **before**
  the plan file move.
- `group-posts-u04-plan.md` moved to `docs/plans-milestones/done/` (a plain
  file move — **not** staged, **not** committed; the user reviews
  everything first).

## Verification
- `Post.cs` diff: one `GroupId` property + one marker line + one summary;
  nothing else changed; default `string.Empty`; property placed after
  `Status`.
- Ref-check: `M3DocTypes.cs` untouched (its `Schema.For<Post>()` line 26 is
  the registration the delta applies against — cited, not modified); no
  `.cs` file modified except `Post.cs`.
- Build output: solution builds green (zero errors; no new warnings from
  the new doc-comment).
- Handoff file gained exactly one `## U4` section; U1/U2/U3 sections
  byte-untouched.
- `git status --short` (no `git add`): `Post.cs` (modified), exec plan
  (new, untracked), u04 plan file (moved to `done/`) — **nothing** staged,
  **no** commit.
