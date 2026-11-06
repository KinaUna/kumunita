---
id: c3-00-prompt
title: "C3-00 — the EventService shrink: retire the ADR 0290 named seam"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C3-00 — the EventService shrink: retire the ADR 0290 named seam

## What you are doing

You are the **first** agent on the **IMPROVE-CODE-3** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-3.md`). Your unit is **C3-00**, the
first of 9. It is the **first god-service shrink** — the unit that turns
`EventService` from a god-file into a part-vs-whole pair, and retires the
baseline entries that shrink frees. It has exactly two parts:

1. **Shrink `EventService` (997 lines)** by extracting the **named seam** in
   ADR 0290 — `EventRsvpGuardianService` (the 6-method RSVP cluster:
   `RsvpAsync` / `ApproveEventRsvpAsync` / `DenyEventRsvpAsync` /
   `VetoEventRsvpAsync` / `GetPendingEventRsvpRequestsAsync` /
   `GetChildRsvpsAsync`). The extraction is a *real* part-vs-whole split (a
   C13 cold read would pass on its own), not a ceremony type.
2. **Retire the baseline entries the shrink frees** (shrink-only; never
   raise): the `EventService` **file** entry (997 lines → under the 600
   `cs_file` ceiling), the `EventService` **class** entry (public methods 31
   → ≤ 25), and the `IEventService` **interface** entry (28 → ≤ 20). The
   extracted `EventRsvpGuardianService` is a new, *named* type with its own
   unit test (the part-vs-whole rule requires it — ≥2 callers, or its own
   unit test, or an owned route family / aggregate).

After this unit, `EventService` is off the "files over 600" list and its class
+ interface entries are retired — a clean, honest reduction the gate verifies.
The DoD #3/#4 "files" line moves **23 → 22**, and the "classes" and
"interfaces" lines each move down by one.

**You are NOT** doing C3-01…C3-08. You are NOT the `PageService` shrink
(C3-01), the `SampleDataSeeder` decomposition (C3-02), the other god-services
(C3-03), the still-large-file tail (C3-04), the Marten allowlist (C3-05/C3-06),
the 3 boundary edges (C3-07), or the close (C3-08). You are NOT re-opening the
met lines (#1 ratchet, #5 translation, #6 concrete-edges, #7 untested services,
#8 SEC pointers) — those hold and stay. You are NOT touching routes, rendered
HTML, document schemas, or adding a feature.

**The ratchet is honest — do not re-fix the gate.** The `CODE_BASELINE_BASE`
check (fixed in `ebf5b3b5`) runs against the PR base and fails a `max` raised
without an `adr:`. A unit that *shrinks* never raises; a unit that must
*raise* names the ADR it amends or creates in the same commit. This unit
shrinks — it does not raise anything.

**A note on the Marten allowlist (so the scope is honest):** `EventService`
owns **no** allowlist keep. `EventController` is **not** on the
`controllers_marten_allowlist` (it has zero `IDocumentStore` /
`IDocumentSession` / `IQuerySession` references — C2-02 verified this), so
shrinking `EventService` retires **no** allowlist entry. The group-(a)
allowlist keeps unblock in the units whose service *does* own them: `PageService`
→ `PageController` (C3-01), and `PostService` / `GuardianService` /
`ModerationService` / `TagService` → their controllers (C3-03 / C3-04). Do not
expect an allowlist retirement from this unit.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `549d7e80` (the IMPROVE-CODE-3 plan commit, on top of the C2-08 close
  `ede7e883` and C2-07 `f4487e19`).
- Commit style: `feat(improve-code-3): C3-00 — <short description>`
  (conventional commits, em dash, **no ADR in the subject** unless the commit
  *is* the ADR the `adr:` field points at — this unit shrinks, so it is
  `feat`).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — they are the
  doctrine you are enforcing (the part-vs-whole rule, the ratchet, the
  comment rule). Read the plan:
  `docs/archive/lanes/improve-code/plan-improve-code-3.md` — the sections **Why now**,
  **D1**, and the **C3-00** row are your spec. Everything else is context for
  later units.
- Read the predecessor's record:
  `docs/archive/lanes/improve-code/improve-code-2-handoff-notes.md` — the C2-05 entry
  (the god-service named-keeps + the ADR 0290 de-scope note) and the C2-04
  entry (the allowlist group-(a) keeps + the move pattern).
- Read the ADR: `docs/adr/0290-improve-code-2-baseline-raises-and-seams.md` —
  the 4 named seams (this unit adopts the `EventRsvpGuardianService` one) and
  the scope note that says the successor must adopt the 4-shrink scope.
- The gate: `tools/code/CodeMetrics/Program.cs` + `tools/code/code-check.ps1`
  + the `code` job in `.github/workflows/ci.yml`.
- The baseline: `tools/code/code-baseline.json` (23 files · 91 methods · 6
  classes · 8 interfaces · 465 boundary · 20 allowlist). The entries this
  unit touches: `Core/Events/EventService.cs` (file, max 997) + **class**
  (public methods, max 31, `adr: 0290`) + `Core/Events/IEventService.cs`
  **interface** (methods, max 28, `adr: 0290`).

## The work

### Part 1 — extract `EventRsvpGuardianService` (the named seam)

ADR 0290 names the seam: the 6-method RSVP cluster. Extract it as a new,
*named* type (`EventRsvpGuardianService`) that owns that cluster — a real
part-vs-whole surface (the guardian-RSVP concern: ADR 0028 account-scope
supervision, ADR 0090 pagination, ADR 0115 guardian-event-link), not a
mechanical ceremony. Each extracted part must pass the part-vs-whole rule:
≥2 callers, or its own unit test, or an owned route family / aggregate.

- **The extracted part is unit-tested** — a new seam test (the C2-01
  `PostTranslationTests` / C2-02 `ProjectsTranslationLaneTests` shape) that
  exercises the RSVP cluster's decision (the standing check, the
  approve/deny/veto paths, the pending-requests query) against a
  `PostgresFixture`, the same harness the predecessor used.
- **`EventService` delegates** to the new service for the 6 RSVP methods (the
  same shape as C2-03's service-delegates-to-seam moves). The service keeps
  the methods it owns; the seam owns the cluster.
- **The `IEventService` surface narrows** — the 6 RSVP methods move to the
  new seam's interface (`IEventRsvpGuardian` per ADR 0290). The Web consumer
  (the controller) codes against the interface, not the concrete — the C03
  `INotificationEmitter` pattern.
- **No new boundary edge** (the seam is in the `Events` module; the controller
  already references `Events` types — verify against the gate).
- **Comment rule** (what/when/throws + ADR 0028/0090/0115 refs) on the new
  type + the delegation.

### Part 2 — retire the baseline entries the shrink frees

After the extraction, run the authoritative gate
(`pwsh tools/code/code-check.ps1`) and measure:

- **`EventService` file** — the 997 lines drop under the 600 `cs_file`
  ceiling → the `files` entry **retires** (the ratchet only moves down; the
  entry is gone, not just shrunk). **The DoD #3/#4 "files" line moves 23 →
  22.**
- **`EventService` class** — the public-method count drops to/under the 25
  ceiling → the `classes` entry **retires** (the `adr: 0290` field is dropped
  with it — the clean resolution ADR 0290 named).
- **`IEventService` interface** — the method count drops to/under the 20
  ceiling → the `interfaces` entry **retires** (same).
- **The extracted `EventRsvpGuardianService`** — if it is itself over a
  ceiling (unlikely for a 6-method surface), it gets a *baseline entry* (a
  new, honest entry) or stays under the ceiling (no entry). The ratchet only
  moves down — the *total* over-ceiling surface goes down.

**The `--report` top-10 is a preview; `--check` is authoritative** (the C2-01
/C2-02 trap). Verify every retire/tighten decision against
`pwsh tools/code/code-check.ps1` before touching
`tools/code/code-baseline.json`.

**Edit the baseline TEXTUALLY** — a `json.load`/`dump` round-trip churns the
`\u003C` escapes (C2-01/C2-02/C2-03/C2-04 all hit this). Replace the entry
object (the `"id"` + `"max"` + `"adr"` lines) with nothing (retire) or with a
tightened value (shrink-only).

## Verification (all green before the next unit)

- [ ] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [ ] **The new seam test runs and passes** (the RSVP cluster's decision
  extracted + unit-tested; the same harness the predecessor used).
- [ ] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** The
  `EventService` file + class + `IEventService` interface entries retired;
  no new over-ceiling surface (the extracted seam is under the ceilings or
  has an honest baseline entry). **The "files" count moved 23 → 22** (the
  "classes" + "interfaces" counts each moved down by one).
- [ ] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [ ] **The affected test suites pass** (the `EventService` tests + the
  `EventController` tests — the re-pointed seam).
- [ ] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).
- [ ] **The handoff note gets a C3-00 entry** (the scope, the before → after
  for the 3 retired entries, the seam's shape, the test, and the gate runs).

## Definition of done (C3-00)

- [ ] **`EventService` is off the "files over 600" list** — the `files`
  entry retired (the 997 lines drop under 600). **The DoD #3/#4 "files" line
  moved 23 → 22.**
- [ ] **The `EventService` class + `IEventService` interface entries are
  retired** — the method-count shrinks (the ADR 0290 named-seam clean
  resolution) drop them to/under their ceilings.
- [ ] **The extracted `EventRsvpGuardianService` is a real part-vs-whole
  seam** (a C13 cold read would pass on its own; ≥2 callers or its own unit
  test; no ceremony).
- [ ] **The new seam is unit-tested** (the RSVP cluster's decision extracted
  + tested; the same harness the predecessor used).
- [ ] **No new boundary edge** (the gate's count does not rise).
- [ ] **No route, rendered-HTML, document-schema, or feature change.**
- [ ] **The ratchet only moved down** (3 entries retired, 0 raised; the total
  over-ceiling surface is strictly smaller).
- [ ] **All verification gates green** (build 0 errors, code-check OK,
  improve-check ALL GATES PASS, the new seam test + the affected suites pass).
- [ ] **The handoff note gets a C3-00 entry** (the shape above).

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** That is the discipline of the lane: the gate (honest since C2-00)
plus a clean-state re-measurement is what makes the reduction honest by
construction rather than by self-report.
