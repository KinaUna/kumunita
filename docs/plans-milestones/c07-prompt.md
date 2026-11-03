# C07 — Split the UserInfo module (ADR A)

## What you are doing

You are the **eighth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C07**, the eighth of 14. It is a **seam-splitting** unit (E4, *The god part*): `IUserInfoService` is **86 methods** and `UserInfoService.cs` is ~**2 700 code lines** — the largest god interface in the repo. The **C01 ADR** (ADR A, `docs/adr/0286-iuserservice-access-core-split.md`, accepted) already decided *what* to do. You implement it.

The split, per ADR 0286: **`IUserInfoService` returns to the access core (15 methods)** and the rest moves to **four sibling interfaces + implementation classes**, one cluster each:

| Sibling interface | Owns | Named callers |
|---|---|---|
| `IGroupService` | group CRUD, descriptions, privacy, invitations, join requests, group/community translations | `GroupsController`, `NotificationsController`, `ModerationController` |
| `ICommunityMembershipService` | community CRUD, member lanes, community translations, membership-request resolve | `CommunityController`, `AdminController`, `GuardianController` |
| `IGuardianService` | guardian link lifecycle, suspend/unsuspend, messaging/RSVP/community-block restrictions, time limits | `GuardianController`, `TimeLimitMiddleware`, `BlockedAccountMiddleware` |
| `IProfileSettingsService` | timezone, page size, first-day-of-week, home prefs, date format, email language, onboarding, messaging opt-in, directory/component reads | `ProfileController`, `AccountController`, `DirectoryController`, `OnboardingController`, `AdminController` |

The **access core** stays on `IUserInfoService`: the 8 methods ADR 0006 froze (profile read, group membership, delegation grant) **plus** the community-id read/write lanes feature modules use for access decisions (`GetCommunityIdsAsync`, `GetEffectiveCommunityIdsAsync`, the community-membership/mandatory lanes).

**How this differs from C06:** C06 unified the *translation lane* (a shared write path). C07 is *differentiation* — splitting one god interface into the parts its callers already imply. C06 is done and **in-flight**: the group/community *translation* methods in `UserInfoService` are now thin forwarders into the shared `TranslationLane`. **You move those forwarders verbatim with their cluster — do not re-implement or re-target them.**

**You are NOT** doing C00–C06 (all shipped) or C08–C13. No new feature, **no route or rendered-HTML change**, no document-schema change, **no new ADR** (C07 builds against ADR 0286). You do NOT touch the `IProjectService` split (C08) or re-architect the module (C10).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `04d92c53` (C06 just landed).
- `IUserInfoService.cs` ≈ **86 methods** · `UserInfoService.cs` ≈ **2 700 code lines** (off the 600-line file list is the goal, so it currently sits over it).
- The four siblings **do not exist yet** (`IGroupService` / `ICommunityMembershipService` / `IGuardianService` / `IProfileSettingsService` — confirm none exist before creating).
- Commit style: `feat(improve-code): C07 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- Baseline: `tools/code/code-baseline.json` has the `UserInfoService` + `IUserInfoService` entries you will **retire or shrink**. **Trust the baseline + the code over the ADR's exact numbers** — re-derive the method → interface map from `IUserInfoService.cs` before touching anything (ADR 0286's "15 access-core" is the target, but the exact per-method assignment must be verified against the code + the ADR's cluster descriptions).

## Entry reads

- **ADR A** (`docs/adr/0286-iuserservice-access-core-split.md`) — the method → interface map, the caller sets, and the migration story (move, not rename).
- The plan's **C07** section, **D3** (god interfaces), **D6** (module boundaries), and the **part-vs-whole rule** (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- `IUserInfoService.cs` — **signatures only, not the comments** (E2: it is 87 % comment).
- `UserInfoService.cs` — **one cluster at a time**, in the commit order below.
- The UserInfo DI registration (`src/Kumunita.Core/UserInfo/DependencyInjection.cs`) — where the siblings get registered.
- The existing UserInfo test files under `tests/Kumunita.Core.Tests/UserInfo*` and `.../UserInfo/` — these **move with their cluster**.
- `tools/code/code-baseline.json` (the `UserInfoService` / `IUserInfoService` / per-method entries you retire).

## Do

**One commit per cluster**, in this order (each commit is a full unit: interface + moved implementation + caller DI re-point + moved tests + baseline updates, and it is green):

1. **Groups** → `IGroupService` + `GroupService`.
2. **Community membership** → `ICommunityMembershipService` + `CommunityMembershipService`.
3. **Guardian** → `IGuardianService` + `GuardianService`.
4. **Profile settings** → `IProfileSettingsService` + `ProfileSettingsService`.

Each commit:
- **Create the sibling interface + implementation.** Move the cluster's methods **verbatim** (move, not rename — no signature or body change). Keep the C06 translation forwarders intact inside their cluster.
- **Shared private helpers** that two or more of the implementations now need → an internal `UserInfoStore` (a part-vs-whole-legal shared seam, ≥ 2 callers).
- **Callers inject only what they use:** switch each named caller's DI from `IUserInfoService` to the specific sibling. Do not leave a caller on the god interface for a method that now lives elsewhere.
- **Move the integration tests** for that cluster to a file naming the new interface (e.g. `UserInfoServiceGroupPrivacyTests` → `GroupService...Tests`) — or keep the file but point it at the sibling. The **seam-test rule** requires each public `I*Service` to be named by at least one integration test file.
- **Baseline:** retire the moved methods' old entries (a moved method gets a new id; the old entry goes stale and must be removed) and the `UserInfoService`/`IUserInfoService` file/interface entries as they fall under the ceiling. Shrink-only — a sibling method or file over a ceiling lands as a *new* entry at its current value.

## Do NOT do

- Do NOT **rename or re-sign** any method — the split is a move. Audit rows are unchanged because the `Via` values (`Owner`, `Admin`, `Guardian`, `Delegation`, `BreakGlass`, `Moderator`) are recorded by `AuthorizationService`, not by the service being split — do not touch them.
- Do NOT change the **access core's** behaviour or the 8 ADR-0006-frozen methods.
- Do NOT touch the C06 **`TranslationLane`** or re-implement the group/community translation forwarders (C06's territory).
- Do NOT split `IProjectService` (C08) or re-architect the module (C10).
- Do NOT raise any baseline `max` / `count` — the ratchet is shrink-only (a raise without an `adr:` reason fails CI).
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you finish)

1. **`IUserInfoService` ≤ 20 methods** (ADR 0286 target: 15 — the access core only).
2. **No UserInfo type over the class (25 public methods) / interface (20) ceilings** — except a sibling that lands over, which must carry a fresh baseline entry at its current value (e.g. ADR 0286 notes `IGroupService` ~26 exceeds the ceiling; baseline it, do not exceed the ceiling without an `adr:` reason).
3. **`UserInfoService.cs` off the 600-line file list** (the file list in the code report no longer flags it).
4. Each of the four siblings has an **integration test file naming it** (seam-test rule).
5. Existing UserInfo tests pass **unmodified in behaviour** (moved, not rewritten).
6. `pwsh tools/code/code-check.ps1` → exit 0 (stale-method rule satisfied; boundary edges legal; no max raised).
7. `pwsh tools/docs/improve-check.ps1` → exit 0.
8. `dotnet build` clean; routes and rendered HTML unchanged; `IGroupService` / `ICommunityMembershipService` / `IGuardianService` / `IProfileSettingsService` all resolve at runtime.

## Practical notes

- **C06 is in-flight inside UserInfo.** The group/community *translation* methods are now thin forwarders into the shared `TranslationLane` (see `improve-code` C06 memory). Move them **verbatim** with their cluster — do not "clean them up" or re-point them; that would change C06's already-shipped seam.
- **The ratchet's moved-method rule** (the gate section): a moved method gets a **new id**. Its old entry goes stale and must be *removed*, and the new id must land **under** the ceiling — a split cannot carry its old exemption along. Budget baseline edits into every cluster commit.
- **Edit `code-baseline.json` via `--tighten`** (`dotnet run --project tools/code/CodeMetrics -- --tighten tools/code/code-baseline.json .`), which removes stale entries and preserves `adr:` fields. The file uses `\u003C`/`\u003E` escapes — a naive `json.load`/`dump` round-trip churns every escape site and muddies the diff; prefer `--tighten` over a textual hand-edit here.
- **Trust the code, not the ADR's counts.** ADR 0286 gives the cluster shape and the 15-access-core target; re-derive the exact per-method assignment from `IUserInfoService.cs` + the ADR's cluster descriptions. A method you can't place in a cluster belongs in the access core (profile read / membership / community-id lanes) — when unsure, keep it there.
- **One cluster per commit** keeps each diff reviewable and lets the baseline retire step by step — do not batch all four clusters into one commit.
- The **xunit.v3 discovery quirk** (`dotnet test` → "Zero tests ran / Exit code 5") is in `docs/dev/testing-and-ci.md`; run a class with the in-process runner: `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.<Class>"`.
- **Terminal output can be swallowed** in this sandbox — redirect to `.tmp/` and read the file, and verify file existence with `read_file` rather than trusting an empty terminal.
