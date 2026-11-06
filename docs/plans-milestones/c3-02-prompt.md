# C3-02 — the SampleDataSeeder decomposition: split the two god-methods into per-entity builders

## What you are doing

You are the **third** agent on the **IMPROVE-CODE-3** lane (plan:
`docs/plans-milestones/plan-improve-code-3.md`). Your unit is **C3-02**, the
third of 9. C3-00 (the `EventService` shrink) and C3-01 (the `PageService`
shrink) are both done — read their handoff entries before you start (below).
Both had the same honest finding: the god-service extraction shrank the file
but did **not** get it under the 600 `cs_file` ceiling, so the `files` entry
tightened, not retired. **C3-02 is a different class of work**: it is the
decomposition of the two largest methods in the repo — `SeedAsync` (615 / cc 83)
and `BackfillSampleCorpusAsync` (782 / cc 128) — into named per-entity builders.
A class-level split cannot get this file under 600 (a single 782-line method
forces any file over the ceiling); only decomposing the methods themselves can.

**You are NOT** doing C3-03…C3-08. You are NOT the other god-services (C3-03),
the still-large-file tail (C3-04), the allowlist retirements (C3-05/C3-06), the
3 boundary edges (C3-07), or the close (C3-08). You are NOT re-opening the met
lines (#1 ratchet, #5 translation, #6 concrete-edges, #7 untested services,
#8 SEC pointers). You are NOT touching routes, rendered HTML, document schemas,
or adding a feature.

**The ratchet is honest — do not re-fix the gate.** A unit that *shrinks* never
raises; a unit that must *raise* names the ADR in the same commit. This unit
shrinks — it does not raise anything.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `f076a0c6` (C3-01 close).
- Commit style: `feat(improve-code-3): C3-02 — <short description>`
- Read `AGENTS.md` + `.github/copilot-instructions.md` first (the doctrine).
- Read the plan: `docs/plans-milestones/plan-improve-code-3.md` — **Why now**,
  **D2**, and the **C3-02** row are your spec.
- Read the handoff: `docs/plans-milestones/improve-code-3-handoff-notes.md` —
  the **C3-00** and **C3-01** entries (both honest findings: `files` tightened,
  not retired — the C3-01 entry also records the `PageService` interface
  staying at 22 because the 4 methods were `public static`, never on the
  interface). **Read the C3-01 "honest finding" before Part 2** — it is the
  same class of risk: the plan's "files 23 → 22" claim is a claim to verify,
  not a fact to assume.

### The file and its baseline entries

- **`src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs`** — **1633** code lines
  (unchanged through C3-00 and C3-01; the two god-methods are the bottleneck).
- **`files` / `SampleDataSeeder.cs`** — **max 1633**
- **`methods` / `SeedAsync`** — **max 615, cc_max 83**
- **`methods` / `BackfillSampleCorpusAsync`** — **max 782, cc_max 128**
- **`methods` / `EnsureUserAsync`** — **max 69, cc_max 23** (over the cc 15
  ceiling; may or may not need to stay — measure)

### The two god-methods (measured at C2-08 close, unchanged)

| Method | Code lines | cc | What it does |
|---|---:|---:|---|
| `SeedAsync` | **615** | **83** | Seeds the first-boot community: accounts, groups, posts, events, tags, pages — the entire sample corpus in one method |
| `BackfillSampleCorpusAsync` | **782** | **128** | Re-seeds (backfills) an existing deployment with the sample corpus: the same per-entity blocks as `SeedAsync`, but as updates rather than creates |

Both are `public static async Task` methods that receive `AppDbContext`,
`IDocumentStore`, `UserManager<User>`, `RoleManager<IdentityRole>`,
`IUserInfoService`, `IMailerStage?`, `string?`, `ILogger`, `CancellationToken`.
They share the same dependency shape; `BackfillSampleCorpusAsync` is
`SeedAsync`'s update-version.

### The entities the methods seed

Both methods seed the same set of entities in the same order (the per-entity
blocks are parallel in structure): **accounts** (user/role creation via
`EnsureUserAsync`), **groups** (community + sub-groups), **posts**, **events**,
**tags**, **pages**. Each block is a self-contained unit: it creates/updates
its entity type, handles standing/roles, and writes its documents. The blocks
are the natural per-entity seam.

## The work

### Part 1 — decompose the two god-methods into per-entity builders

D2: *"Per-entity builders. Decompose `SeedAsync` and `BackfillSampleCorpusAsync`
into named per-entity builders (accounts / groups / posts / events / tags /
pages), each a separate, unit-testable surface."*

Each per-entity builder is a **real part-vs-whole surface** (a C13 cold read
would pass on its own — the entity being seeded is a named domain concept with
its own standing / role / document shape). The helpers are in the
`Bootstrap` module (the seeder's module); they do not cross a new boundary.

**The shape to follow:** for each entity (accounts, groups, posts, events,
tags, pages), extract a **named private static method** on
`SampleDataSeeder` (the most natural shape — the seeder IS the orchestrator,
and the per-entity blocks are its internal helpers; this also avoids Gate
Rule 1, which applies only to *class* types). The parent method
(`SeedAsync` / `BackfillSampleCorpusAsync`) becomes a thin orchestrator that
calls the per-entity helpers in order. Each helper must be **under the
80-line / 15-cc ceiling** (or carry its own baseline entry if it is
genuinely over — but the goal is to get each under).

**Naming constraint (Gate Rule 1 — read this before choosing a type shape):**
if you extract the per-entity logic into **separate classes**, the class name
suffix matters. The gate's seam-test Rule 1 requires every class ending in
`Policy` / `Resolver` / `Evaluator` / `Builder` to be named by a **plain**
(non-`PostgresFixture`) unit test. But the seeder's per-entity logic needs a
DB (`AppDbContext` / `IDocumentStore`), so its test uses `PostgresFixture` →
a `*Builder` class name would **fail** the gate. **Use private static methods
on `SampleDataSeeder`** (not separate `*Builder` classes) to avoid this
trap. If you choose separate classes, name them with a suffix that is NOT in
the Rule 1 list (e.g. `*Seeder` — which is not `Policy`/`Resolver`/
`Evaluator`/`Builder`), and they will not trigger Rule 1.

**The two parent methods must both drop under 80 code lines / 15 cc** after
the extraction — they become orchestrators that call the per-entity builders.
If a parent method is still over the ceiling after the extraction, the
`methods` entry is tightened, not retired — say which.

**The extracted helpers must be tested** — the existing `SampleDataSeeder`
tests (which use `PostgresFixture` because the seeder needs a DB) exercise the
per-entity helpers through the parent orchestrators' calls. If a helper's
logic is pure enough to test in isolation (no DB), a new plain unit test is a
bonus; if it needs a DB, the existing harness is the same harness the
predecessors used. The gate's seam-test Rule 1 (which requires `*Policy` /
`*Resolver` / `*Evaluator` / `*Builder` **classes** to be named by a plain
unit test) does NOT apply to private static methods — only to class types.
If you choose separate classes, name them with a suffix NOT in the Rule 1
list (see the naming constraint above).

**No new boundary edge.** The helpers are in the `Bootstrap` module; they
reference the same types the god-methods already reference (`AppDbContext`,
`IDocumentStore`, `UserManager`, etc.). Verify against the gate.

**Comment rule** (what/when/throws + the first-boot seeding doctrine refs) on
each new helper.

### Part 2 — retire/tighten the baseline entries the shrink frees

After the decomposition, run the authoritative gate
(`pwsh tools/code/code-check.ps1`) and **measure** (do not assume the plan's
"retires" line — C3-00 and C3-01 both proved the plan's file claim can be
wrong):

- **`SampleDataSeeder.cs` file** (1633) — if the decomposition drops it
  **under** the 600 `cs_file` ceiling → the `files` entry **retires** (the
  DoD #3/#4 "files" line moves 23 → 22). **If it is still over 600** → the
  `files` entry is **tightened** (max 1633 → measured), **not retired**, and
  the "files" line does not move. **Say which.**
- **`SeedAsync` method** (615/cc 83) — if the decomposition drops it
  **under** the 80-line / 15-cc ceiling → the `methods` entry **retires**.
  If still over → **tightened**. **Say which.**
- **`BackfillSampleCorpusAsync` method** (782/cc 128) — same. **Say which.**
- **`EnsureUserAsync` method** (69/cc 23) — if it is still over the cc 15
  ceiling → the entry **stays** (tightened if the cc changes). If the
  decomposition gets it under 15 cc → the entry **retires**. **Say which.**
- **The extracted per-entity builders** — if any is over a ceiling (unlikely
  for a focused per-entity builder), it gets a *baseline entry* (a new,
  honest entry) or stays under the ceiling (no entry). The ratchet only moves
  down.

**The `--report` top-10 is a preview; `--check` is authoritative** (the
C2-01/C2-02 trap). Verify every retire/tighten decision against
`pwsh tools/code/code-check.ps1` before touching `tools/code/code-baseline.json`.

**Edit the baseline TEXTUALLY** — a `json.load`/`dump` round-trip churns the
`\u003C` escapes (C2-01…C3-01 all hit this).

### Part 3 — (no Part 3 for this unit)

C3-02 does not unblock any allowlist entry (the `SampleDataSeeder` is not a
service; no controller is on the allowlist waiting on it). The DoD #6
allowlist line does not move in this unit.

## Verification (all green before the next unit)

- [ ] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [ ] **The affected test suites pass** (the existing `SampleDataSeeder` tests
  + any first-boot seeding tests — the per-entity helpers are tested through
  the parent orchestrators' existing harness, which is `PostgresFixture`
  because the seeder needs a DB; no new test file is required unless a
  helper's logic is pure enough to test in isolation).
- [ ] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** Every
  retire/tighten decision matches the measured value. **State the before →
  after for each of the 4 entries** (file + 3 methods) — retired or
  tightened, and why.
- [ ] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [ ] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).
- [ ] **The handoff note gets a C3-02 entry** (the scope, the before → after
  for the 4 entries, the helper shape, and the gate runs).

## Definition of done (C3-02)

- [ ] **The two god-methods are decomposed into named per-entity helpers**
  (a C13 cold read of each helper passes on its own — the entity being
  seeded is a named domain concept with its own standing / role / document
  shape; the parent methods are thin orchestrators under 80 lines / 15 cc).
- [ ] **The decomposition is behaviour-preserving** — the existing
  `SampleDataSeeder` tests (and any first-boot seeding tests) still pass
  against the decomposed orchestrators. If a helper's logic is pure enough
  to test in isolation (no DB), a new plain unit test is a bonus; if it
  needs a DB, the existing `PostgresFixture` harness is the same harness
  the predecessors used.
- [ ] **The 4 baseline entries are resolved against the measurement** (the
  file + 3 method entries each retired *or* tightened to the true measured
  value — the ratchet only moved down, total over-ceiling surface strictly
  smaller, 0 raises).
- [ ] **No new boundary edge** (the gate's count does not rise).
- [ ] **No route, rendered-HTML, document-schema, or feature change.**
- [ ] **All verification gates green** (build 0 errors, code-check OK,
  improve-check ALL GATES PASS, the new seam test + the affected suites pass).
- [ ] **The handoff note gets a C3-02 entry** (the shape above, with the
  honest before → after).

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** C3-00 and C3-01 both proved the plan's headline claim can be
wrong — C3-02's "files 23 → 22" claim is a claim to verify, not a fact to
assume.
