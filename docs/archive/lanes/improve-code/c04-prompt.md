---
id: c04-prompt
title: "C04 — Decisions out of services: `*Policy` parts"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C04 — Decisions out of services: `*Policy` parts

## What you are doing

You are the **fifth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C04**, the fifth of 13. It is a **decision-extraction** unit: pure "may this actor do that?" rules (edit standing, whether a translation may be added) currently live **inside** Marten-bound services as `public static` methods, and Web controllers reach across into the concrete service to call them (`EventService.CheckEditStanding`, `PostService.CanAddTranslation`, `ProjectService.CheckTodoStanding`, …). That is the E9/E12 anti-pattern — *decisions inside services* — which is why the services are big, the same rule is copied per surface, and every test needs Postgres.

You move each **pure decision** out into a named `*Policy` static class (the D6 allowlist explicitly permits `*Policy` statics as cross-module targets), so Core and Web both call the policy, and each policy gets a **unit test** (a truth table lifted from the existing integration tests). Behaviour is unchanged — the same rules, the same `AccessVia`/`AccessAudit` rows, just relocated so the decision is testable alone.

**You are NOT** doing C00 (shipped — the gate + baseline), C01 (shipped — ADRs 0286–0289), C02 (shipped — the data move), or C03 (shipped — `INotificationEmitter`). You are **NOT** doing C05–C13. You write **no new feature, no route or rendered-HTML change, no document-schema change, no new ADR** (C04 builds against the plan's C04 section + the D6 rule the gate already enforces). Do **not** touch the `IUserInfoService` / `IProjectService` interface splits (C07/C08) — you are moving *static decision methods*, not the interfaces.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `44bef9d0` (C03 just landed).
- Commit style: `feat(improve-code): C04 — <short description>` (conventional commits, em dash).
- Gates you must keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- **Precedent to mirror:** `src/Kumunita.Core/Usage/UsageCapturePolicy.cs` — the one existing `*Policy` static in this codebase. Match its shape (a `public static class X…Policy` of pure, session-free static methods with a `<summary>` doc-comment stating what / when / throws + the ADR number).
- The D6 allowlist (plan §The gate, and `tools/code/CodeMetrics/Program.cs` `IsAllowedBoundaryTarget`) permits a cross-module target when it is an interface, record, enum, or a **static class whose name ends `Policy` / `Keys` / `Kinds`**. A `*Policy` static you introduce is therefore a *legal* boundary target — that is the whole point.

## Entry reads

- The plan's **C04** section, **D6**, and the **part-vs-whole rule** (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- ADR **0006** §D ("the authorization path is unique") — the *reason* the standing rules are extracted as pure parts; and the E11 handoff (the plan's `Evidence` E11 row + `Handoffs to other lanes` → IMPROVE-SEC) — the one decision you must **not** move.
- The pure decisions to extract (all `public static`, all session-free — take plain inputs, return a decision, touch no `IDocumentSession`):
  - `src/Kumunita.Core/Events/EventService.cs`: `CheckCreateStanding` (~L496), `CheckEditStanding` (~L518), `CanAddTranslation` (~L547) **+ its private helper** `ResolveTranslationStanding` (~L564, moves with it).
  - `src/Kumunita.Core/Posts/PostService.cs`: `CanAddTranslation` (~L2190).
  - `src/Kumunita.Core/Projects/ProjectService.cs`: the standing matrix `CheckTodoStanding` (~L1044), `CheckBoardStanding` (~L1074), `CheckGoalStanding` (~L1103), `CheckProjectStanding` (~L1133).
- The **Web call sites** you must re-point at the policies (grep confirmed): `Controllers/EventController.cs` (`CheckEditStanding` ×4, `CanAddTranslation` ×1), `Controllers/PostsController.cs` (`PostService.CanAddTranslation` ×2), `Controllers/GroupsController.cs` (`PostService.CanAddTranslation` ×2), and any `ProjectService.Check*Standing` call in `Controllers/ProjectsController.cs`.
- The **truth-table sources** for the new unit tests (existing integration cases to lift): `tests/Kumunita.Core.Tests/EventServiceTests.cs`, `PostTranslationTests.cs`, `ProjectServiceTests.cs`.
- `tools/code/code-baseline.json` (the `Web → EventService` / `Web → PostService` / `Web → ProjectService` boundary entries you will **shrink**, and any baselined method you move).

## Do

- Create a `*Policy` static class **next to its service**, one per decision family (≥ 6 to satisfy the plan):
  - `Events/EventStandingPolicy` (or `EventEditPolicy`) — `CheckCreate`, `CheckEdit`, `CanAddTranslation` (+ `ResolveTranslationStanding` as its private helper).
  - `Posts/PostTranslationPolicy` — `CanAddTranslation`.
  - `Projects/…Policy` — the four standing checks (`Todo` / `Board` / `Goal` / `Project`). Split them into `*StandingPolicy` types that each own their aggregate's rule; the part-vs-whole test justifies the split (each owns a route family / aggregate).
- Each policy method keeps the **exact same body and signature** (same inputs, same throws, same `AccessVia`), so the decision is byte-identical. Move them verbatim; do **not** rewrite the rule while relocating.
- Have the service's old method become a **thin `=> Policy.…` forwarder** (or delete it and re-point every caller, Core and Web, to the policy — pick one and be consistent; the forwarder is the lower-risk choice if a caller is in a spot you can't cleanly re-point). Either way, **no Web file may call the concrete service for a decision anymore**.
- Re-point every Web call site in the Entry-reads list to the new policy.
- **A unit test file per policy** (non-Testcontainers): a truth table lifted from the integration tests' cases — every input combination that yields allow / deny / throw is asserted against the policy directly. This is the first pure unit test for these rules (they were previously only reachable through Postgres).
- Apply **D4** to the code you move (each method's doc-comment states what / when / throws + the ADR number; history links stay in the ADR).
- **Retire the baseline edges you clear.** Moving a Web call from `PostService.CanAddTranslation` to `PostTranslationPolicy.CanAddTranslation` removes a `Web → PostService` reference — if a `boundary_violations` entry's `count` drops, lower it (shrink-only), and if a baselined *method* is moved to a new type, its old entry goes stale and the new one must land **under** the ceiling.

## Do NOT do

- **Do NOT move the claim-to-do standing logic (E11).** That is the **private, session-touching** `ProjectService.ClaimStandingAsync` (~L1706) — it calls `_userInfo.GetGroupIdsAsync` / `GetCommunityIdsAsync` (~L1718–1725). It is an **access decision that belongs on `IAuthorizationService`** — leave it exactly where it is and add a `// TODO ❓ SEC — routes through IAuthorizationService per the IMPROVE-SEC lane (E11)` pointer. The decision to move it is the SEC lane's, not C04's. ⚠️ **Do not confuse it with the pure `CheckTodoStanding`** (public static, ~L1044) — that one *is* in C04's scope; only `ClaimStandingAsync` is out.
- Do NOT move any method that touches a `IDocumentSession` / `IQuerySession` — only *pure* (session-free) decisions qualify. If a "decision" you find reads a session, leave it (that is a C09/C10-shaped move).
- Do NOT change behaviour: the standing rules, the thrown exceptions, the `AccessVia` / `AccessAudit` rows, and the audit story all stay byte-identical.
- Do NOT rename public members the owner surface or other modules rely on beyond the forwarder/policy move, and do NOT re-architect the services (that is C07/C08/C10).
- Do NOT raise any baseline `max` / `count` — the ratchet is shrink-only; a moved method's new id must land *under* the ceiling.
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates — this is a code unit.

## Exit criteria (all must pass before you commit)

1. **Zero Web → concrete-service static decision calls.** `grep -rn "EventService.CheckEditStanding\|EventService.CheckCreateStanding\|EventService.CanAddTranslation\|PostService.CanAddTranslation\|ProjectService.CheckTodoStanding\|ProjectService.CheckBoardStanding\|ProjectService.CheckGoalStanding\|ProjectService.CheckProjectStanding" src/Kumunita.Web/` returns **no call sites** (comment `<see cref>` references that point at the *new policy* are fine; a live `.Check*Standing(` / `.CanAddTranslation(` call is not).
2. **≥ 6 `*Policy` static types** exist, each next to its service, each with a **unit test file** naming it (non-Testcontainers — no `PostgresFixture`, no `IClassFixture` Postgres).
3. The truth-table unit tests pass, and the existing integration tests (`EventServiceTests`, `PostTranslationTests`, `ProjectServiceTests`, `EventControllerTests`, …) pass **unmodified**.
4. The E11 claim-to-do logic — the private `ProjectService.ClaimStandingAsync` (the `GetGroupIdsAsync`/`GetCommunityIdsAsync` path) — is **unchanged** in place with a `// TODO ❓ SEC` pointer (do not relocate it). (The pure `CheckTodoStanding` *is* moved to a policy — that is in scope.)
5. `pwsh tools/code/code-check.ps1` → exit 0 (shrunken / retired boundary entries; no stale-entry failure; no baselined `max` raised).
6. `pwsh tools/docs/improve-check.ps1` → exit 0.
7. `dotnet build` clean.

## Practical notes

- **A moved method gets a new id.** The gate keys a method by `file::Type.Member/arity`. When `EventService.CheckEditStanding` becomes `EventStandingPolicy.CheckEdit`, the old `EventService.CheckEditStanding` baseline entry (if present) goes **stale** and must be removed in the same commit, and the new `EventStandingPolicy.CheckEdit` must land **under** the ceiling (80 code lines / CC 15) — if it is over the ceiling it is a *new* violation, so keep the policy method small (it should already be, since these are pure decisions).
- **The boundary entries *shrink*, they usually do not fully retire.** The Web controllers still inject and call the *instance* services (`EventService`, `PostService`, `ProjectService`) for their write lanes — so `Web → EventService` / `Web → PostService` / `Web → ProjectService` entries will **drop in `count`** (the decision call sites leave) but likely stay. Lower them to the new count (shrink-only); deleting them when the edge still exists fails the ratchet's new-violation rule. Edit `code-baseline.json` **textually** — a `json.load`/`dump` round-trip churns the `\u003C`/`\u003E` escapes and muddies the diff.
- **Comments do not create boundary edges** — the gate's token collector uses `DescendantTokens(descendIntoTrivia: false)`, so a `/// <see cref="…"/>` or `//` mention of the concrete service does *not* count. Only a live code identifier does. So the `<see cref>` references in `Models/EventEditorModel.cs` are harmless; the `.Check*Standing(` calls in the controllers are the real edges.
- **Forwarder vs. delete:** prefer a one-line forwarder on the service (`public static void CheckEditStanding(…) => EventStandingPolicy.CheckEdit(…);`) *only if* a caller you can't cleanly re-point depends on it; otherwise delete and re-point. Do not leave a dead copy of the logic in two places — that is the *Silent coupling* anti-pattern C04 exists to kill.
- The part-vs-whole rule is your justification for the split (each policy owns a route family / aggregate and gets its own unit test). Keep each `*Policy` focused; if a "policy" starts owning several unrelated rules, that is ceremony — fold it back.
- Commit as one coherent unit (the policies + the service forwarders/deletions + the Web re-points + the unit tests + the baseline edits share the same gate). Commit with: `feat(improve-code): C04 — decisions out of services: *Policy parts (standing + translation rules)`.
