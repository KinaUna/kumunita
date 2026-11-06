# C3-01 — the PageService shrink: adopt the ADR 0290 named seam + unblock the PageController allowlist keep

## What you are doing

You are the **second** agent on the **IMPROVE-CODE-3** lane (plan:
`docs/plans-milestones/plan-improve-code-3.md`). Your unit is **C3-01**, the
second of 9. C3-00 (the `EventService` shrink) is done — read its handoff +
honest finding before you start (below). C3-01 is the **second god-service
shrink**: it adopts the second ADR 0290 named seam — `PageTranslationStandingService`
(the 4-method translation-standing cluster) — out of `PageService`, and, if
the shrink is big enough, **unblocks the `PageController` allowlist keep**
(the group-(a) god-file-at-ceiling entry that has been waiting on
`PageService` to drop under 600). It has up to three parts:

1. **Shrink `PageService` (baseline file max 917)** by extracting the
   **named seam** in ADR 0290 — `PageTranslationStandingService` (the
   4-method translation-standing cluster: `CheckCreateStanding` /
   `CheckEditStanding` / `CheckTranslateStanding` / `CanTranslatePage` — the
   C04 `*Policy` pattern, mirroring `EventStandingPolicy`). The extraction is
   a *real* part-vs-whole split (a C13 cold read would pass on its own — the
   standing-matrix gate concern, ADR 0015 / the standing-matrix doctrine),
   not a ceremony type.
2. **Retire the baseline entries the shrink frees** (shrink-only; never
   raise): the `PageService` **class** entry (30 methods), the `IPageService`
   **interface** entry (22 methods), the `PageService` **file** entry (917).
   The extracted `PageTranslationStandingService` is a new, *named* type with
   its own unit test (the part-vs-whole rule requires it — ≥2 callers, or its
   own unit test, or an owned route family / aggregate).
3. **Retire the `PageController` allowlist entry** (DoD #6 group-(a)
   god-file-at-ceiling) — **only if** the shrink gets `PageService` under the
   600 `cs_file` ceiling (the allowlist reason names exactly this: *"the
   owning PageService is a god-file at its ratchet ceiling — moving the lanes
   in would raise the ratchet, which is C2-05's scope"* — C3-01 **is** that
   scope). If `PageService` is still over 600 after the extraction, the
   allowlist entry **cannot** retire yet and this becomes a C3-05 / C3-06
   matter — see the honest caveat.

**You are NOT** doing C3-02…C3-08. You are NOT the `SampleDataSeeder`
decomposition (C3-02), the other god-services (C3-03), the still-large-file
tail (C3-04), the other allowlist retirements (C3-05/C3-06), the 3 boundary
edges (C3-07), or the close (C3-08). You are NOT re-opening the met lines
(#1 ratchet, #5 translation, #6 concrete-edges, #7 untested services, #8 SEC
pointers) — those hold and stay. You are NOT touching routes, rendered HTML,
document schemas, or adding a feature.

**The ratchet is honest — do not re-fix the gate.** The `CODE_BASELINE_BASE`
check (fixed in `ebf5b3b5`) runs against the PR base and fails a `max` raised
without an `adr:`. A unit that *shrinks* never raises; a unit that must
*raise* names the ADR it amends or creates in the same commit. This unit
shrinks — it does not raise anything.

**Carried from C3-00 (read this before Part 2):** C3-00's honest finding is
that extracting the ADR 0290 seam from `EventService` (997 → 709) did **not**
get the file under the 600 ceiling, so the `files` entry was **tightened, not
retired**, and C3-00's headline "files 23 → 22" did not hold. **C3-01 is the
same class of risk** (see below) — the plan's claim that the `PageService`
*class* entry retires is **not guaranteed by the math** (30 − 4 = 26, still
over the 25 ceiling). Measure against the gate; do not assume the plan's
"retires" line holds.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `ca93fdb5` (the C3-00 close, on top of the C2-07/C2-08 prompt commits
  `c0203cf2` / `18fd22bf`).
- Commit style: `feat(improve-code-3): C3-01 — <short description>`
  (conventional commits, em dash, **no ADR in the subject** — this unit
  shrinks, so it is `feat`).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — they are the
  doctrine you are enforcing (the part-vs-whole rule, the ratchet, the comment
  rule). Read the plan: `docs/plans-milestones/plan-improve-code-3.md` — the
  **Why now**, **D1**, and the **C3-01** row are your spec.
- Read the predecessor's record:
  `docs/plans-milestones/improve-code-3-handoff-notes.md` — the **C3-00**
  entry, especially its **honest finding** (the `EventService` file tightened
  to 709, not retired; the class retired; the interface tightened) and its
  "what C3-00 did NOT do" list.
- Read the ADR: `docs/adr/0290-improve-code-2-baseline-raises-and-seams.md` —
  the `PageTranslationStandingService` seam (the 4-method translation-standing
  cluster, the C04 `*Policy` pattern mirroring `EventStandingPolicy`).
- The gate: `tools/code/CodeMetrics/Program.cs` + `tools/code/code-check.ps1`
  + the `code` job in `.github/workflows/ci.yml`.
- The baseline: `tools/code/code-baseline.json`. The entries this unit touches:
  - `files` / `src/Kumunita.Core/Pages/PageService.cs` — **max 917**
  - `classes` / `PageService` — **max 30**, **`adr: 0290`** (the seam to adopt)
  - `interfaces` / `IPageService` — **max 22** (over the 20 ceiling)
  - `controllers_marten_allowlist` / `src/Kumunita.Web/Controllers/PageController.cs`
    — the group-(a) god-file-at-ceiling keep (reason names `PageService` at
    its ceiling).
- The in-repo `*Policy` precedent: `src/Kumunita.Core/Pages/PageTranslationPolicy.cs`
  (the `CanTranslatePage` core rule) + `EventStandingPolicy` (the ADR 0290
  named precedent for the standing-cluster shape). The 4 standing methods are
  `static` on `PageService` today — moving them to the new seam is the seam.

## The work

### Part 1 — extract `PageTranslationStandingService` (the named seam)

ADR 0290 names the seam: the 4-method translation-standing cluster
(`CheckCreateStanding` / `CheckEditStanding` / `CheckTranslateStanding` /
`CanTranslatePage`). Extract it as a new, *named* type that owns that cluster
— a real part-vs-whole surface (the standing-matrix gate concern), not a
mechanical ceremony. Follow the C04 `*Policy` pattern (the ADR 0290 named
precedent: `EventStandingPolicy`; the in-repo `PageTranslationPolicy` is the
`CanTranslatePage` core rule). Each extracted part must pass the part-vs-whole
rule: ≥2 callers, or its own unit test, or an owned route family / aggregate.

- **The extracted part is unit-tested** — a new seam test (the C2-01
  `PostTranslationTests` / C2-02 `ProjectsTranslationLaneTests` shape) that
  exercises the standing cluster's decisions (the create / edit / translate
  standing, the `CanTranslatePage` display probe) against a `PostgresFixture`,
  the same harness the predecessors used.
- **`PageService` delegates** to the new service for the 4 standing methods
  (the same shape as C3-00's `EventService` → `EventRsvpGuardianService` move).
  The service keeps the methods it owns; the seam owns the cluster.
- **The `IPageService` surface narrows** — the 4 standing methods move to the
  new seam's interface. The Web consumer (the `PageController`) codes against
  the interface, not the concrete — the C03 `INotificationEmitter` pattern.
- **No new boundary edge** (the seam is in the `Pages` module; the controller
  already references `Pages` types — verify against the gate).
- **Comment rule** (what/when/throws + the standing-matrix / ADR 0015 refs)
  on the new type + the delegation.

### Part 2 — retire/tighten the baseline entries the shrink frees

After the extraction, run the authoritative gate
(`pwsh tools/code/code-check.ps1`) and **measure** (do not assume the plan's
"retires" line — C3-00 proved the plan's file claim can be wrong):

- **`PageService` file** (917) — if the extraction drops it **under** the 600
  `cs_file` ceiling → the `files` entry **retires** (the DoD #3/#4 "files"
  line moves down). **If it is still over 600** (the C3-00 pattern: 997 → 709
  did not retire) → the `files` entry is **tightened** (max 917 → measured),
  **not retired**, and the "files" line does not move. **Say which.**
- **`PageService` class** (30, `adr: 0290`) — **the math is the trap here**:
  the seam takes **4** methods, so the class goes **30 → 26**, still **over
  the 25 ceiling** → the `classes` entry **tightens** (max 30 → 26), it does
  **not** retire — *unless* the seam takes more than 4 (e.g. the private
  `CanTranslatePageCore` helper + any other standing-only helpers). If the
  class lands **at/under 25** → it **retires** (the `adr: 0290` field drops
  with it — the clean resolution ADR 0290 named). **Measure; do not assume the
  plan's "class entry retires" line holds.**
- **`IPageService` interface** (22) — the 4 standing methods move off it, so
  it goes **22 → 18**, **under the 20 ceiling** → the `interfaces` entry
  **retires** (the `adr:` drops with it, if present). **This one the math
  supports** — but confirm with the gate.
- **The extracted `PageTranslationStandingService`** — if it is itself over a
  ceiling (unlikely for a 4-method surface), it gets a *baseline entry* (a
  new, honest entry) or stays under the ceiling (no entry). The ratchet only
  moves down — the *total* over-ceiling surface goes down.

**The `--report` top-10 is a preview; `--check` is authoritative** (the C2-01
/C2-02 trap). Verify every retire/tighten decision against
`pwsh tools/code/code-check.ps1` before touching
`tools/code/code-baseline.json`.

**Edit the baseline TEXTUALLY** — a `json.load`/`dump` round-trip churns the
`\u003C` escapes (C2-01…C3-00 all hit this). Replace the entry object (the
`"id"` + `"max"` + `"adr"` lines) with nothing (retire) or with a tightened
value (shrink-only).

### Part 3 — retire the `PageController` allowlist entry (only if Part 2 retires the file)

The `PageController` allowlist reason is the group-(a) god-file-at-ceiling
keep: the page create/edit lanes are multi-session aggregate writes and the
owning `PageService` was a god-file at its ceiling. **That block retires the
moment `PageService` is under 600.**

- **If Part 2 retires the `PageService` file entry** (the file is now under
  600) → the `PageController` allowlist entry's reason is obsolete: move the
  page create/edit document access into the now-`PageService` (the owning
  service is no longer at the ceiling, so moving the lanes in does **not**
  raise the ratchet), and **retire the `PageController` allowlist entry**.
  **The DoD #6 allowlist count moves 20 → 19.**
- **If Part 2 only tightens the `PageService` file entry** (still over 600 —
  the C3-00 pattern) → the allowlist entry's reason **still holds** (the
  service is still at the ceiling), so the entry **stays** (its reason text
  may be updated to name C3-01's measurement, not a forced retirement). **The
  DoD #6 allowlist line does not move** in this unit. **Say which.**

## Verification (all green before the next unit)

- [ ] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [ ] **The new seam test runs and passes** (the standing cluster's decision
  extracted + unit-tested; the same harness the predecessors used).
- [ ] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** Every
  retire/tighten decision matches the measured value (no stale baseline, no
  silent raise). **State the before → after for each of the 3 entries**
  (file / class / interface) — retired or tightened, and why.
- [ ] **The `PageController` allowlist line is resolved honestly** — retired
  (20 → 19) *if* the `PageService` file entry retired, or kept with an updated
  reason *if* it tightened. **State which, and why.**
- [ ] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [ ] **The affected test suites pass** (the `PageService` tests + the
  `PageController` tests — the re-pointed seam + any allowlist-retired lane).
- [ ] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).
- [ ] **The handoff note gets a C3-01 entry** (the scope, the before → after
  for the 3 entries + the allowlist line, the seam's shape, the test, and the
  gate runs).

## Definition of done (C3-01)

- [ ] **The `PageService` shrink is a real part-vs-whole seam** (a C13 cold
  read of `PageTranslationStandingService` would pass on its own; ≥2 callers
  or its own unit test; no ceremony).
- [ ] **The extracted `PageTranslationStandingService` is unit-tested** (the
  standing cluster's decision extracted + tested; the same harness the
  predecessors used).
- [ ] **The 3 baseline entries are resolved against the measurement** (the
  file / class / interface entries each retired *or* tightened to the true
  measured value — the ratchet only moved down, total over-ceiling surface
  strictly smaller, 0 raises).
- [ ] **The `PageController` allowlist entry is resolved honestly** — retired
  (the service is now under 600) *or* kept with an updated reason (the service
  is still over 600); not a silent keep, not a forced retirement.
- [ ] **No new boundary edge** (the gate's count does not rise).
- [ ] **No route, rendered-HTML, document-schema, or feature change.**
- [ ] **All verification gates green** (build 0 errors, code-check OK,
  improve-check ALL GATES PASS, the new seam test + the affected suites pass).
- [ ] **The handoff note gets a C3-01 entry** (the shape above, with the
  honest before → after).

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** That is the discipline of the lane: the gate (honest since C2-00)
plus a clean-state re-measurement is what makes the reduction honest by
construction rather than by self-report. C3-00 proved the plan's headline
claim can be wrong — C3-01's "class entry retires" and "files line moves"
claims are **claims to verify, not facts to assume.**
