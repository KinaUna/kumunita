---
id: c05-prompt
title: "C05 — One content-target resolver"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C05 — One content-target resolver

## What you are doing

You are the **sixth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C05**, the sixth of 13. It is a **seam-linking** unit (E7, *Silent coupling*): two services each carry a **private, session-touching near-clone** that maps `(TargetKind, targetId)` to a display row (title / link / visibility) by switching over the closed `TargetKind` set — `BookmarkService.ResolveTargetAsync` (+ its list-shape sibling `ResolveForListAsync`), and `PersonalNoteService.ResolveTargetAsync` (+ `ResolveForListAsync`). That switch is duplicated twice (≈450 clone lines) and the two controllers carry copies too. You unify them into **one `ContentTargetResolver`** that both services and both controllers use, split into a **pure kind table** (unit-testable) + the **loader calls** (integration-tested at the seam).

**How this differs from C04:** C04 moved *pure static* decisions into `*Policy` statics. C05 unifies *private async* methods that **open a `QuerySession`** and re-run the target's own `CanAsync(Read)`. The resolver is therefore **not** a `*Policy` static — it is a new type whose **cross-module** use must stay D6-legal (see Practical notes). Behaviour stays byte-identical: same kinds, same `CanAsync(Read)` decisions, same `Title`/`Link`/`Visible` shape, same audit rows.

**You are NOT** doing C00–C04 (all shipped: gate+baseline, ADRs 0286–0289, the data move, `INotificationEmitter`, the `*Policy` parts) or C06–C13. No new feature, no route or rendered-HTML change, no document-schema change, **no new ADR** (C05 builds against the plan's C05 section + ADR 0167 / 0118 + the D6 rule the gate already enforces). You do NOT touch the `IUserInfoService` / `IProjectService` interface splits (C07/C08).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `2ad85cb2` (C04 + its spec just landed).
- Commit style: `feat(improve-code): C05 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- Baseline entries that should **retire** once both `ResolveTargetAsync` are gone: `BookmarkService.ResolveTargetAsync(string,string,string)` (max 142, cc 20) and `PersonalNoteService.ResolveTargetAsync(string,string,string)` (max 215, cc 31) in `tools/code/code-baseline.json`. **These are the authoritative current numbers — trust the baseline over the plan's estimates** (the plan says 208/CC36 and 287/CC61).

## Entry reads

- The plan's **C05** section, **E7**, **D6**, and the part-vs-whole rule (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- ADR **0167** (PersonalNotes) + ADR **0118** (Bookmarks) — the closed `TargetKind` set, the "the service owns the decision, not the Web" shape, and the ADR 0118 amendment that unified the Bookmarks / PersonalNotes sets.
- The four methods: `src/Kumunita.Core/Bookmarks/BookmarkService.cs` (`ResolveTargetAsync` ~L724, `ResolveForListAsync` ~L612, `ClosedTargetKinds` ~L61) and `src/Kumunita.Core/PersonalNotes/PersonalNoteService.cs` (`ResolveTargetAsync` ~L919, `ResolveForListAsync` ~L729). Note the return shapes differ slightly: the target resolvers return `(bool Visible, string? Title, string? Link)`; `ResolveForListAsync` returns `(string? Title, string? Link)` (the list's zero-audit shape).
- The two controllers with the cloned blocks (E7): `Controllers/BookmarksController.cs` + `Controllers/NotesController.cs`.
- `tools/code/code-baseline.json` (the two method entries above + any boundary edges your resolver introduces).

## Do

- Create one **`ContentTargetResolver`** (in `Core/Query/` or a new `Core/Targets/` — pick the folder in this unit) that maps `(TargetKind, targetId, actorId)` to a display row **through the owning surfaces' interfaces**, never through their documents, re-running each target's own frozen `CanAsync(Read)` exactly as today.
- Split it into a **pure kind table** (kind → route + loader) that is **unit-testable with no Postgres** — every kind in the closed set resolves to a route + a loader — **plus** the session-touching loader calls (integration-tested at the seam, one test per kind family).
- Have **both services and both controllers** use it; delete the four private clones (both `ResolveTargetAsync` and both `ResolveForListAsync`).
- A **unit test file** naming the resolver (non-Testcontainers) covering the kind table; an **integration test** per kind family (resolve → title / link / visibility).
- Apply **D4** to the code you move (what / when / throws + the ADR number).
- **Retire the two baseline method entries** in the same commit. If the resolver's new methods land under the ceiling they need no entry; if any land over it they become a *new* entry at the current value (shrink-only, never a raise).

## Do NOT do

- Do NOT move the **E11 claim-to-do** logic (`ProjectService.ClaimStandingAsync`, the `GetGroupIdsAsync` / `GetCommunityIdsAsync` path) — it belongs on `IAuthorizationService` and is the IMPROVE-SEC lane's call. C05's resolver already calls `CanAsync(Read)`; keep that path and do not re-derive access.
- Do NOT do **C06** (the `TranslationLane`) even though some targets are translatable — C05 resolves *which target it is and whether it's visible*, not its translations.
- Do NOT split the `IUserInfoService` / `IProjectService` interfaces (C07/C08) or re-architect the two services (C10).
- Do NOT change behaviour: the closed `TargetKind` set, the `CanAsync(Read)` decisions, the `Title`/`Link`/`Visible` shape, and the audit rows all stay byte-identical.
- Do NOT raise any baseline `max` / `count` — the ratchet is shrink-only.
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you commit)

1. Both `BookmarkService.ResolveTargetAsync` and `PersonalNoteService.ResolveTargetAsync` are **gone** from the baseline (retired, not lowered) and the four private clones are deleted.
2. The two services + the two controllers all resolve through the one `ContentTargetResolver`; no remaining per-surface copy of the kind switch.
3. The kind table has a **pure unit test** (no Postgres); each kind family has an **integration test** at the seam.
4. `BookmarkServiceTests` / `PersonalNoteServiceTests` (and the two controllers' tests) pass **unmodified**.
5. `pwsh tools/code/code-check.ps1` → exit 0 (stale-method rule satisfied; any new resolver methods under the ceiling or a new entry under the max; boundary edges legal).
6. `pwsh tools/docs/improve-check.ps1` → exit 0.
7. `dotnet build` clean; the Bookmark/PersonalNote resolver region is gone from the clone report.

## Practical notes

- **D6 trap (the key gotcha):** a concrete `ContentTargetResolver` referenced from the `Bookmarks` and `PersonalNotes` modules is a **boundary violation** (D6 allows only interface / record / enum / `*Policy`/`*Keys`/`*Kinds`). Expose it behind an **`IContentTargetResolver`** (a legal target) and register that; the pure kind table is the unit-testable part. The resolver's *outbound* load calls (Posts / Events / Projects / … interfaces) are legal; only a **concrete-class inbound edge** is not.
- **Trust the code, not the plan's counts.** The plan says "21 kinds"; the actual set is `BookmarkService.ClosedTargetKinds` (10 kinds: post, event, todo, announcement, page, project, board, inventory, directory). Re-derive the table from the real set and confirm PersonalNotes' set matches (ADR 0118 unified them).
- These methods are **private + async + session-touching** (unlike C04's pure statics) — a **C05-shaped move, not a C04 `*Policy`**; the resolver legitimately opens a `QuerySession`.
- Edit `code-baseline.json` **textually** (the file was generated with `\u003C`/`\u003E` escapes) — a `json.load`/`dump` round-trip churns every escape site and muddies the diff.
- Commit as one coherent unit (the resolver + the interface + DI + the service/controller re-points + the tests + the baseline retirements). Commit with: `feat(improve-code): C05 — one content-target resolver (Bookmark + PersonalNote)`.
