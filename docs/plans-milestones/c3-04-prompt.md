# C3-04 — the still-large-file tail: drive `files` from 23 to ≤ 8

## What you are doing

You are the **fifth** agent on the **IMPROVE-CODE-3** lane (plan:
`docs/plans-milestones/plan-improve-code-3.md`). Your unit is **C3-04**, the
fifth of 9. C3-00 (the `EventService` shrink), C3-01 (the `PageService` shrink),
C3-02 (the `SampleDataSeeder` decomposition), and C3-03 (the 5 god-service
seams) are all done and green — read their handoff entries before you start
(below). C3-04 is the unit the plan names as the **`files` 23 → ≤ 8** tail
(DoD #3/#4, target D4): every one of the **23 files still over the 600
`cs_file` ceiling** that C3-00…C3-03 did **not** retire gets a real
part-vs-whole split (or a recorded honest-keep) so the count reaches **≤ 8**.

**This is the biggest, highest-risk unit in the lane — and it carries the same
honest finding that C3-00…C3-03 all proved:** a part-vs-whole extraction
shrinks a file but **does not reliably get it under the 600 ceiling**. Every
one of the previous four units found its `files` entry *tightening or
growing*, not retiring (C3-00 `EventService` 997 → 709; C3-01 `PageService`
917 → 852; C3-02 `SampleDataSeeder` **grew** 1633 → 2060; C3-03 all 5
god-services tightened to 631–817, none retired). **Do not assume a split
retires a `files` entry. The plan's *"the `files` entries retire"* line is a
claim to verify against the gate, not a fact to assume.** Your honest target
is *"as many of the 23 as the gate confirms dropped under 600, retired; the
rest tightened (or, if they grow under the extraction, raised with an ADR)"* —
and the DoD's **≤ 8** is what you must actually hit if it is reachable, else
record exactly which files still stand over and why.

**You are NOT** doing C3-05 / C3-06 (the Marten-allowlist retirements — the
group-(b) no-service, group-(c) edge-minting, group-(d) diagnostics keeps),
C3-07 (the 3 flagged-accidental boundary edges), or C3-08 (the close + the
six-member flip). You are NOT re-opening the met lines (#1 ratchet, #5
translation, #6 concrete-edges, #7 untested services, #8 SEC pointers) — those
hold and stay. You are NOT touching routes, rendered HTML, document schemas, or
adding a feature. You are NOT re-doing the 5 god-service seams C3-03 landed
(their forwarders are the shape that shipped — build *on* them, don't revert
them).

**The ratchet is honest — do not re-fix the gate.** A unit that *shrinks* never
raises; a unit that must *raise* names the ADR it amends or creates in the same
commit. **This unit may need to raise** — if a file *grows* under its extraction
(the C3-02 `SampleDataSeeder` 1633 → 2060 pattern: an extracted helper's
signatures are net-positive on code lines), the `files` entry is **raised with
an ADR**, not papered over with a ceremony split. The DoD's *"total over-ceiling
surface strictly smaller, 0 silent raises"* still holds — a raise is allowed,
but it must be named.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `3bae0b6d` (the C3-03 close).
- Commit style: `feat(improve-code-3): C3-04 — <short description>` (conventional
  commits, em dash, **no ADR in the subject** — if a raise is needed, the ADR
  goes in the body, as C3-02 did with ADR 0292 and C3-03 with ADR 0293).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first (the doctrine).
- Read the plan: `docs/plans-milestones/plan-improve-code-3.md` — **Why now**,
  **D4** (the still-large-file tail), the **C3-04** row, and **the close** are
  your spec.
- Read the handoff: `docs/plans-milestones/improve-code-3-handoff-notes.md` —
  the **C3-00**, **C3-01**, **C3-02**, and **C3-03** entries (all four honest
  findings: `files` tightened or raised, *never* retired; C3-02 records the
  *grew* pattern; C3-03 records that the `methods`/`classes`/`interfaces`
  lines all held steady while only the `files` excess moved down).
- Read ADR 0290 (the named seams), ADR 0292 (the C3-02 files-entry honest-keep
  raise — the precedent for the *raise* path), and ADR 0293 (the C3-03
  boundary-edge raises — the precedent for a seam that lands on an
  already-existing module edge).

### The 23 over-600 files (current, at HEAD `3bae0b6d`; the gate is the oracle)

> **Probe the true measured count before you trust a number** — set a `files`
> entry's `max` to `1`, run `pwsh tools/code/code-check.ps1`, read the
> *"now N, max 1"* line, then restore. (`--report` shows only the top-10 per
> section; `.tmp/metrics/*.csv` is stale — `--check` is authoritative.)

| # | File | `files` max | module | note |
|---:|---|---:|---|---|
| 1 | `src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs` | **2060** | Bootstrap | the C3-02 *grew* honest-keep (two god-methods, each over 600) |
| 2 | `src/Kumunita.Web/Controllers/GroupsController.cs` | **1641** | Web | already had +2 (seam collateral) in C3-03 |
| 3 | `src/Kumunita.Web/Controllers/EventController.cs` | **1249** | Web | already had +5 (seam collateral) in C3-00 |
| 4 | `src/Kumunita.Web/Controllers/TodosController.cs` | **1096** | Web | group-(d) C08 allowlist keep |
| 5 | `src/Kumunita.Web/Controllers/PostsController.cs` | **1010** | Web | group-(a) allowlist keep (owns `PostService`, now 631) |
| 6 | `src/Kumunita.Web/Controllers/BoardsController.cs` | **857** | Web | group-(d) C08 allowlist keep |
| 7 | `src/Kumunita.Core/Pages/PageService.cs` | **852** | Pages | C3-01 tightened (was 901 → 852) |
| 8 | `src/Kumunita.Web/Controllers/PageController.cs` | **833** | Web | group-(a) allowlist keep |
| 9 | `src/Kumunita.Core/Identity/IdentityService.cs` | **817** | Identity | C3-03 tightened (was 989) |
| 10 | `src/Kumunita.Core/Portability/UserPortabilityService.cs` | **806** | Portability | C3-03 tightened (was 922) |
| 11 | `src/Kumunita.Web/Controllers/AdminController.cs` | **777** | Web | group-(b) no-service keep |
| 12 | `src/Kumunita.Core/UserInfo/GuardianService.cs` | **771** | UserInfo | group-(a) allowlist keep |
| 13 | `src/Kumunita.Core/Moderation/ModerationService.cs` | **753** | Moderation | group-(a) allowlist keep |
| 14 | `src/Kumunita.Web/Controllers/GuardianController.cs` | **747** | Web | group-(a) allowlist keep |
| 15 | `src/Kumunita.Core/Projects/TodoService.cs` | **744** | Projects | C3-03 tightened (was 839) |
| 16 | `src/Kumunita.Core/Tags/TagService.cs` | **736** | Tags | group-(a) allowlist keep |
| 17 | `src/Kumunita.Core/Events/EventService.cs` | **709** | Events | C3-00 tightened (was 997) |
| 18 | `src/Kumunita.Core/Projects/BoardService.cs` | **706** | Projects | C3-03 tightened (was 1025) |
| 19 | `src/Kumunita.Core/UserInfo/GroupService.cs` | **705** | UserInfo | group-(a) allowlist keep |
| 20 | `src/Kumunita.Core/Announcements/AnnouncementService.cs` | **704** | Announcements | group-(a) allowlist keep |
| 21 | `src/Kumunita.Core/Localization/LocalizationService.cs` | **684** | Localization | group-(b)? (verify) |
| 22 | `src/Kumunita.Core/Posts/PostService.cs` | **631** | Posts | C3-03 tightened (was 821) |
| 23 | `src/Kumunita.Web/Controllers/AnnouncementController.cs` | **611** | Web | group-(a) allowlist keep |

**The DoD totals at the start of C3-04 (measured at HEAD `3bae0b6d`):**
`files` **23** · `methods` **89** · `classes` **5** · `interfaces` **8** ·
`controllers_marten_allowlist` **20** · `boundary_violations` **465** (the 12
C3-03 raises are in).

**The DoD target (D4):** `files` **23 → ≤ 8** — i.e. at least **15** of the 23
must drop under 600 (retire) or the lane's DoD #3/#4 line does not hold at the
C3-08 close. Every other line (`methods` 89, `classes` 5, `interfaces` 8,
allowlist 20) is another unit's target (C3-05/C3-06 for the allowlist; the
method/class/interface lines are the *tighten-not-retire* floor C3-03 set).

## The work

### Part 1 — pick the seams in a real order (not alphabetical)

Split the **biggest, clearest-cut** files first while your context is fresh,
and the **hardest** (the ones that may not drop under 600 — a controller with
no owning service, or a god-service whose forwarder surface can't shrink) last.
Suggested order:

1. **The 5 Web controllers that are the largest** (`GroupsController` 1641,
   `EventController` 1249, `TodosController` 1096, `PostsController` 1010,
   `BoardsController` 857, `PageController` 833, `AdminController` 777,
   `GuardianController` 747, `AnnouncementController` 611) — a controller's
   action-group (e.g. the CRUD lane, the admin lane, the board-move lane) is
   a natural part-vs-whole seam (a `*Controller` partial + a named
   `*Handler` / `*Service` that owns the action group).
2. **The 5 Core services C3-03 tightened but did not retire**
   (`PageService` 852, `IdentityService` 817, `UserPortabilityService` 806,
   `GuardianService` 771, `ModerationService` 753, `TagService` 736,
   `EventService` 709, `BoardService` 706, `GroupService` 705,
   `AnnouncementService` 704, `LocalizationService` 684, `PostService` 631) —
   each already has a seam from a prior unit; a **second** seam (a named
   domain concept the C13 cold read would pass) is the natural next split.
3. **`SampleDataSeeder`** (2060) last — it *grew* under C3-02, so the
   honest-keep path (an ADR) is more likely than a retirement. Do not
   "ceremony split" a file that cannot drop under 600.

**The shape of each split:** a **real part-vs-whole seam** (a C13 cold read
would pass on the extracted type on its own — the concern extracted is a named
domain concept with its own standing / policy / guard / handler, not a
mechanical "extract N methods" ceremony). The extracted type is a **new, named
type** (a `*Policy` / `*Resolver` / `*Evaluator` / `*Builder` static class, or
an `I*Service` + `*Service` + DI registration, or a `*Controller` partial + a
named `*Handler`) with **its own unit test** (the part-vs-whole rule requires
it). The extracted type must be **under every ceiling** (the file < 600, the
class < 25 public methods, the interface < 20 methods, the methods < 80 lines
/ 15 cc) — otherwise it inherits the god-file's over-ceiling status and the
split is a ceremony, not a reduction. **The C3-03 forwarder pattern is the
proven shape** — keep the extracted methods on the god-file's public surface
as thin forwarders (no `async` keyword — that's a CS4016 error) so the
existing tests + consumers keep compiling unchanged.

**The file that remains** (after the extraction) must drop **under the
600 `cs_file` ceiling** for its `files` entry to **retire**. If it does not,
the `files` entry is **tightened** (max → measured). **If it grew under the
extraction** (the C3-02 pattern), the `files` entry is **raised with a new
ADR** (the C3-02 ADR 0292 precedent). **Say which for each file you touch.**

**No new boundary edge.** The extracted seam lives in the same module as the
file it comes out of. Verify against the gate — the C3-01/C3-03 boundary-grow
trap (a seam file landing on an already-existing edge) applies here. If a
seam lands on an already-existing edge and the count rises, name it with the
new ADR (the C3-03 ADR 0293 precedent: the *set* of edges is unchanged, the
count is a file-count reflection of a real new file on a sanctioned edge).

**Comment rule** (what/when/throws + the ADR that names the seam) on each
extracted type.

### Part 2 — retire/tighten/raise the `files` entries the splits free

After each split, run the authoritative gate (`pwsh tools/code/code-check.ps1`)
and **measure** (do not assume the plan's "retires" line):

- **The `files` entry for each file you touched** — under 600 → **retires**
  (the DoD #3/#4 "files" line drops 23 → 22 → 21 … per retirement). Still
  over 600 → **tightened** (max → measured). Grew under the extraction →
  **raised with a new ADR** (the C3-02 ADR 0292 precedent). **Say which for
  each file.**
- **The DoD #6 allowlist line (20 → ≤ 5)** — C3-04 does **not** retire
  allowlist entries directly (that is C3-05/C3-06's job). C3-04 may **unblock**
  group-(a) keeps by dropping their owning service under 600 (e.g.
  `PostsController` unblocks if `PostService` drops under 600 — it is 631
  now, so C3-04's split of `PostService` is the move). If it does, **note it**
  in the handoff (the retirement itself is C3-05).
- **The `methods` / `classes` / `interfaces` lines** — C3-03 set the honest
  floor: the forwarder pattern keeps method counts on the god-class/interface,
  and the extracted methods keep their over-cc entries (re-pointed, not
  retired). C3-04 may **retire some `methods` entries** if it further
  decomposes an over-cc seam method under the ceiling (e.g. the cc 22
  `MoveTodoToLanePositionAsync` on `BoardLaneService`) — the gate is the
  oracle. **Do not assume** a split retires a `methods`/`classes`/`interfaces`
  entry; measure.

**Edit the baseline TEXTUALLY** — a `json.load`/`dump` round-trip churns the
`\u003C` escapes (C2-01…C3-03 all hit this). **Take a CLEAN backup before each
probe-restore cycle** so the restore does not undo your own tighten.

## Verification (all green before the next unit)

- [ ] **`dotnet build Kumunita.slnx -c Debug` — 0 errors, 0 warnings.**
- [ ] **The affected test suites pass** (the existing unit tests for each file
      you split, + the new seam's own unit test).
- [ ] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** (The gate is
      the oracle — verify every retire/tighten/raise decision against it.)
- [ ] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.** (If you raised
      a baseline entry, the ADR-index row is present — gate (b).)
- [ ] **The `\u003C` escapes in the baseline survived** (the baseline was edited
      textually, never `json.load`/`dump`).
- [ ] **The C3-04 handoff entry is written** in
      `docs/plans-milestones/improve-code-3-handoff-notes.md` with the honest
      before → after per file (retired / tightened / raised), the DoD line
      movements (files 23 → N; allowlist unblocked but not retired; methods /
      classes / interfaces as measured), and the ADR(s) named for any raise.
- [ ] **`git status` is clean** and the commit is in (the C3-04 commit).
