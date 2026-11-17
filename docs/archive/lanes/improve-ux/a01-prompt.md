---
id: a01-prompt
title: "Prompt: IMPROVE-UX A01 — the reads honour PostStatus"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0024-author-soft-delete-lane.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A01** — the first unit of the **admin/moderator** block and the
> foundation of the moderation loop (A01–A06, the plan's "one story"). The
> finding is **E1/E2**: `HidePostAsync`/`RemovePostAsync` write
> `PostStatus.Hidden`/`.Removed`, but **no read path filters on it** — so
> wiring the moderator action alone would change nothing visible. A01 makes
> the reads honour the status: feeds and search exclude Hidden/Removed for
> everyone, and the detail lane is Status-gated so the **author** sees a
> "hidden by a moderator" placeholder, a **moderator** (via the report lane)
> still sees it, and any other resident gets a clean 404.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting A01 (the moderation loop's opening unit — no
> dependencies, Core-only reads + tests).

# Fresh-context prompt: IMPROVE-UX A01

You are continuing the Kumunita repository on `development` (HEAD
`08d7749e`, R21 landed — the resident-facing jargon strings are now plain
words). The **entire resident block (R01–R21) is shipped**; the next work is
the **admin/moderator** block, and the first unit is **A01**: the reads honour
`PostStatus`. The plan card (in `plan-improve-ux.md`, the A01 row):

> A01 — **Reads honour `PostStatus`**: feeds, group feeds, search, kiosk
> catalog and `GetPostAsync` exclude Hidden/Removed for everyone except the
> author (sees "hidden by a moderator") and moderators via the report lane;
> tests.
> Read first: `PostService.cs` list/get methods, `Post.cs:10-47`.
> **Done when: tests per read path.**

This is the **S**-severity E1/E2 pair — the loop that never closed. It is a
**Core-only** seam (the read predicates) + tests, **≤ ~5 files** (plus one
localized placeholder string in `en`/`de`/`fr`/`da`). **No ADR is required for
A01 itself** — the plan's DoD #6 reserves one for the moderation loop (D6); if
you consider this the loop's opening unit and decide it warrants one, the next
free number is **0303** (the highest today is 0302) and it records the
visibility semantics below. A01 does **not** add a web action, a moderator UI,
or the outcome-execution (that is A02); it only makes the existing
write-through visible.

## The state of the code (measured 2026-11-10, `development`)

- **`PostStatus`** (`src/Kumunita.Core/Posts/Post.cs:12`) — `Active` (default),
  `Hidden`, `Removed`. `Post.Status` (`Post.cs:51`) defaults to `Active`.
  Written **only** by `PostService.HidePostAsync` (`PostService.cs:1229`, sets
  `.Hidden`) and `RemovePostAsync` (`PostService.cs:1266`, sets `.Removed`) —
  both `Moderate`-gated and already unit-tested (`PostServiceTests.cs:863` /
  `:951`). So the write is real and correct; **the read is what is missing**.
- **The feeds** filter today on `p.DeletedAt == null && !p.IsDraft` only:
  - `ListFeedAsync` — `PostService.cs:217` (`Query<Post>().Where(p => p.ComponentId == componentId && p.DeletedAt == null && !p.IsDraft)`)
  - `ListAllFeedAsync` — `PostService.cs:297` (same predicate, multi-component)
  - `ListGroupFeedAsync` — `PostService.cs:1315` (delegates to `GroupPostService.ListGroupFeedAsync`; the group-lane post query lives there)
  - A01 adds `&& p.Status == PostStatus.Active` to each — **mirror the existing `DeletedAt == null` clause exactly** (same predicate shape, same place).
- **Search** — `src/Kumunita.Core/Search/SearchService.cs:308-313`: the post
  candidate query is `.Where(p => p.GroupId == string.Empty && p.DeletedAt == null && !p.IsDraft)`. A01 adds `&& p.Status == PostStatus.Active`. Do the same for the **group-scope** post search candidate query (the `SearchScope.Groups` path) if it has its own `Post` candidate `Where`.
- **The detail lanes** — `GetPostAsync` (`PostService.cs:362`) and
  `GetGroupPostAsync` (`PostService.cs:1323`) **return the post today
  regardless of `Status`** (so a hidden post is still fetchable by a direct
  `/posts/{id}` URL — a leak). A01 makes these **Status-aware**:
  - **Author** (`actorId == Post.AuthorId`) → still return the post; the Web
    view renders the **"hidden by a moderator"** placeholder (see *The work*
    for how to express that without a new Core shape).
  - **Moderator** (a `Moderate`-holder, reached from the report lane) → still
    return the post (their lever is hide/remove/unhide, and the report/queue
    read must keep resolving the target).
  - **Any other resident** → treat as not found (`KeyNotFoundException` → the
    Web 404 → now the friendly R01/R02 status page). **This is the leak fix.**
- **Kiosk catalog** — `src/Kumunita.Web/Services/KioskItemCatalog.cs:480`
  (`GetPostAsync`) and `:503` (`GetGroupPostAsync`). The kiosk's
  community-visible mode already renders the **denied slide** when a detail
  read is refused — so once the detail lanes 404 a non-author/non-moderator
  read of a hidden post, the kiosk is covered **for free** (hidden → 404 →
  denied slide). Verify this in a test rather than re-implementing a filter.

The model to copy is **ADR 0024's `DeletedAt`** handling, documented right in
the code (`PostService.cs:1034`): *"Read lanes hide it — the feeds … filter on
`DeletedAt is null` — but the detail lanes (`GetPostAsync`,
`GetGroupPostAsync`) still return the post so the author can see a
placeholder."* A01 is that same shape applied to `PostStatus`, with one extra
branch (the moderator is also allowed to see a hidden post, unlike an author's
own soft-delete).

## The trap that will bite you — the "filter the feed, forget the URL" leak

The obvious fix is to add `Status == Active` to the three feed queries and
call it done. That leaves **`/posts/{id}` still serving a hidden post to any
signed-in resident** — the feed is clean, the direct link is a leak. And the
symmetric over-fix is to 404 the hidden post for *everyone*, which breaks
**two** things: the author loses sight of their own post (they need the
"hidden by a moderator" placeholder to understand what happened), and the
**report lane** can no longer resolve the target (the moderator's
hide/remove/unhide read would 404). So the detail lane needs the **three-way
branch** (author → placeholder, moderate → see it, other → 404), not a blanket
exclusion. A test for each of the three branches is the DoD.

Also: **do not touch `PostStatus`'s write lanes or the enum** — A01 is reads
only. `HidePostAsync`/`RemovePostAsync` stay exactly as they are. And **do
not build A02 here** — the outcome picker calling `HidePostAsync` from the
Resolve page is a separate unit (A02 depends on this one).

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (the comment rule: what/when/throws + ADR number; the ratchet; the
  don't-pause-mid-task rule).
- **`plan-improve-ux.md`** — the Decisions table (D6 moderation), the A01 row,
  and the E1/E2 evidence rows (this unit closes both).
- **`src/Kumunita.Core/Posts/Post.cs:1-60`** — the `PostStatus` enum + the
  `Status` field (the single M3b ADD on the POCO) + the `DeletedAt` field.
- **`src/Kumunita.Core/Posts/PostService.cs`** — `ListFeedAsync` (`:217`),
  `ListAllFeedAsync` (`:297`), `GetPostAsync` (`:362`), `ListGroupFeedAsync`
  (`:1315`), `GetGroupPostAsync` (`:1323`), and the ADR 0024 comment at
  `:1030-1045` (the `DeletedAt` precedent you are mirroring). Also
  `HidePostAsync`/`RemovePostAsync` (`:1229`/`:1266`) — read-only, to confirm
  the actor + audit shape.
- **`src/Kumunita.Core/Search/SearchService.cs:300-420`** — the post + group
  search candidate queries (the `Where(...DeletedAt == null && !p.IsDraft)`
  clauses you extend).
- **`src/Kumunita.Web/Services/KioskItemCatalog.cs:470-510`** — the kiosk
  detail reads (to confirm the "404 → denied slide" path, not to change it).
- **`tests/Kumunita.Core.Tests/PostServiceTests.cs:845-1020`** — the existing
  M3b `HidePostAsync`/`RemovePostAsync` tests (the fixture, the
  `PostgresFixture` shape, the actor setup) — your new tests go here.
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 in-process `dotnet exec`
  invocation (the reliable runner).
- **`git status`** (R21's commit must be intact; you are on `development`).

## The work

1. **Feeds exclude non-Active.** Add `&& p.Status == PostStatus.Active` to the
   candidate `Where` in `ListFeedAsync`, `ListAllFeedAsync`, and the
   group-lane feed query behind `ListGroupFeedAsync`. Keep the existing
   `DeletedAt == null && !p.IsDraft` clauses untouched; A01 adds one more
   conjunct.
2. **Search excludes non-Active.** Add the same predicate to the post and
   group-post candidate queries in `SearchService.cs`.
3. **Detail lanes are Status-gated (the leak fix + the author/moderator
   branch).** In `GetPostAsync` (and `GetGroupPostAsync` for the group-lane
   post), after the post is loaded: if `Post.Status == PostStatus.Active`,
   return as today. If `Hidden`/`Removed`: **author** → return the post (the
   Web view renders the placeholder); **moderator** (a `Moderate`-holder, as
   the report lane resolves the target) → return the post; **otherwise** →
   `throw new KeyNotFoundException(...)` (→ 404). Reuse the existing
   authorization check the method already runs — A01 adds a `Status` branch on
   top, it does not change the audience decision.
4. **"Hidden by a moderator" placeholder for the author.** Do **not** add a new
   Core record — `PostDetailResult` already carries `Post? Post`; the Web view
   for the post detail can render the placeholder when `post.Status != Active`
   and the actor is the author. If a localized string is needed, add the key in
   **all four** languages (`en` + `de`/`fr`/`da`, the `kw-l` floor; de/fr/da
   machine-written, flagged for the parked native review). If the view already
   handles a "not visible" case, reuse it.
5. **Tests (one per read path — the DoD).** In `PostServiceTests.cs` (and the
   `SearchService` test class for the search path):
   - a hidden post is **absent from `ListFeedAsync`** (for the author **and**
     for another resident);
   - a hidden post is **absent from search**;
   - `GetPostAsync` on a hidden post: **author** → returns the post (placeholder
     case); **another resident** → `KeyNotFoundException` (404); **moderator**
     → returns the post;
   - a **removed** post behaves the same as hidden (`.Removed` is a hard lane
     — no unhide — but the read branches are identical);
   - the kiosk detail read of a hidden post → the refused/404 path (so the
     denied slide renders) — a Core-level test on `GetPostAsync` for a
     non-author/non-moderator principal is sufficient; the Web "denied slide"
     rendering is already pinned by the R17 kiosk tests.
6. **Verify the gates.** `python3 tools/ux/ux-measure.py` (report-only) and
   `--check` must stay green — A01 is Core C# and does not touch the tracked UX
   counts; if your new `KeyNotFoundException` branches add any bare
   `return NotFound()`-style lines in a **Web controller**, that would raise
   `bare_status` — keep the 404 in **Core** (`KeyNotFoundException`, mapped by
   the existing Web layer) so the count is untouched.

**`// TODO ❓` (ask Per if you reach it):** the plan card says "for everyone
except the author (sees 'hidden by a moderator') and moderators via the report
lane." I have read that as **feeds + search exclude non-Active for everyone
(including the author — the author sees it in the detail placeholder, not in
the feed)**, mirroring the ADR 0024 `DeletedAt` precedent. If Per intends the
author to still see their own hidden post **in the feed** (with a placeholder
row), that is a different shape — flag it before writing, don't guess.

## Scope rules

- **Core read predicates + the author/moderator/other branch + tests.** Do not
  touch the write lanes (`HidePostAsync`/`RemovePostAsync`), the enum, the
  report Resolve page, or the outcome-execution (A02). Do not rename keys,
  restructure the bounded context, or touch `admin.json`. The only files that
  should change: `PostService.cs`, `GroupPostService.cs` (the group feed
  query), `SearchService.cs`, the post-detail **view** (placeholder only, if a
  string is needed), the four `Keys/{lang}/posts.json` (one new placeholder key
  each, only if needed), and the test files.
- **One commit** (the house rule): the read filters + the detail branch + the
  tests land in **one** `feat(ux):` commit
  (`feat(ux): the feeds, search, and detail reads honour PostStatus (IMPROVE-UX A01)`).
- **The `ux-measure.py --check` gate + the translation-parity family** stay
  green (A01 lowers nothing tracked; if you added a localized key the parity
  tests must see it in all four languages).

**Before committing** (see `testing-and-ci.md` for the in-process runner — the
reliable path; `dotnet test` discovery is broken here):

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check     # must stay green
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.PostServiceTests"
# + the SearchService test class (confirm its name in tests/Kumunita.Core.Tests/Search/)
# + the KioskItemCatalog / Kiosk web tests that pin the denied slide (R17),
#   e.g. Kumunita.Web.Tests.KioskLink* — confirm the hidden-post → denied path still holds
```

If a test fails, read the error, adjust, continue. Note: `Kumunita.Web.Tests`
is expected to fail **3** known-reds (the two `SecGate.DataClassCoverageTests`
M2 gates + `ImproveHarnessTests.CodeCheck_Gate_Passes`) — recorded drift (the
SEC-lane M2 + the ratchet), **not** regressions; don't "fix" them (a different
unit's scope).

Then report: the read paths you changed (feeds / search / detail, with file:line),
how you expressed the author placeholder (and whether you needed a new localized
key — name it, all four languages), the **three-way detail-branch** outcome
(author / moderator / other), the test class names + pass counts, the
`ux-measure.py --check` line, the `// TODO ❓` (did you resolve it, and with
whom), and the commit hash. Do not commit unrelated work.
