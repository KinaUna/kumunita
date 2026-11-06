---
id: c2-06-prompt
title: "C2-06 — the two untested services get a unit-tested decision part"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C2-06 — the two untested services get a unit-tested decision part

## What you are doing

You are the **seventh** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-2.md`). Your unit is **C2-06, DoD #7 —
"the two untested services covered."** The lane before you: C2-00 landed the
honest ratchet gate; C2-01 + C2-02 finished the translation-lane collapse (the
`≤ 40` line met); C2-03 + C2-04 closed DoD #6 (the concrete-`*Service`
crossings 21 → 9, the Marten allowlist 21 → 19); **C2-05 just finished DoD
#3/#4's clean seams** — `FirstBootSeeder` split into the `SeededContentCatalog`
(991→567/432), the `GoalsController` route family split off
`ProjectsController` (797→577/253), `SampleDataSeeder` (1633) and the 7 god-
services recorded as honest keeps (RED 25→23), `d768ace2`.

**D6** says it in one sentence: *"The two untested services (`MailAlarms` and
`UsagePurge`) each get a **unit-tested decision part** — a `*Policy` or pure
helper with a test, per the C04/C12 pattern. This is the part-vs-whole rule
applied to the *test* requirement, not a new type for ceremony."* The
anti-pattern D6 names is a test that **just calls the service** (no decision
extracted) — that leaves the seam-test rule *unmet* for them, so it does not
count. Your unit is exactly that: for **each** of the two services, pull the
**pure decision** (the thing that decides *whether* to act and *what the
outcome's shape is*) out of the I/O method into a **named, pure,
unit-testable** surface, re-point the service to use it, and write a **unit
test** for that decision surface.

The codebase is a modular monolith with explicit module boundaries (ADR 0006).
A **service whose decision logic is tangled into its I/O method** is the smell
here: the "should this fire / which rows qualify" decision can only be
exercised by spinning up a Postgres `IDocumentStore` (a Testcontainers test),
so there is no **unit** that names the decision — the seam-test gate's
*"services-with-unit-tested part"* line can never count it. The C04 reference
shape is the opposite: the decision is a **static `*Policy` / pure helper**
(the `EventStandingPolicy` / `GoalStandingPolicy` / `*TranslationPolicy`
precedents — pure methods over plain values, no `IDocumentStore`, no
`IDocumentSession`), and the service's I/O method reads/commits and **delegates
the decision to the policy**. The policy is then covered by a **plain unit
test** (no Postgres) — the seam-test line is met.

**You are NOT** doing C2-07 (the SEC pointer on `ClaimTodoAsync` — the `// TODO
❓ SEC` marker), C2-08 (the close + the six-member flip), or re-opening C2-05
(the still-large files — its honest keeps stay; do not re-split
`SampleDataSeeder` or the 7 god-services here). You are **not** touching routes,
rendered HTML, or document schemas, **not** adding a feature, **not** mooting or
lowering the seam-test floor (it is **already met** — 71 services have a
unit-tested part vs. the floor of 10), and **not** minting a new `I*Service`
interface to make the test line pass (the seam-test rule wants a **unit-tested
decision**, not a new interface with any test).

**Do not re-fix the gate.** The ratchet is honest (C2-00). The two hard gate
checks that shape your work (understand these before editing):

1. **A new `*Policy` / `*Resolver` / `*Evaluator` / `*Builder` class MUST be
   named by a unit (non-`IClassFixture<PostgresFixture>`) test** (the seam-test
   Rule 1). That is the whole point of the extraction — the decision surface
   you pull out *is* the thing the unit test names. If you add a `*Policy` and
   its only test is a Testcontainers test, the gate fails.
2. **A new `I*Service` interface needs a test that names it** (the seam-test
   Rule 2) — but you do **not** want to mint one here; the D6 shape is a
   `*Policy` / pure helper, not a new service.

There is **no hard count gate** forcing "these two specifically" — that is the
**plan's DoD #7 named line** (MailAlarms + UsagePurge) you pursue by extracting
their decision parts, not a number the gate enforces. A new type you add is a
**real seam** (the part-vs-whole rule a C13 cold read would pass) or nothing at
all — no ceremony type.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `d768ace2` (C2-05 close).
- Commit style: `feat(improve-code-2): C2-06 — <short description>` (conventional
  commits, em dash, no ADR in the subject). **One service (its decision surface +
  its unit test) per commit** — the gate green after each, the decision surface
  added + the service re-pointed + the test present in the same commit.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/archive/lanes/improve-code/plan-improve-code-2.md` — the **D6**
  decision and the **C2-06** row are your spec, and **DoD #7** ("the two
  untested services covered").
- Read the code-shape doctrine: `docs/philosophy/in-code.md` — the **part-vs-
  whole test** (the extracted decision must be a real seam a C13 cold read would
  pass, not a ceremony type), the **ratchet** (baseline only goes down), and the
  **comment rule** (what/when/throws + ADR number).
- Read the **C04 `*Policy` precedent** (the reference shape for a decision
  surface): `src/Kumunita.Core/Events/EventStandingPolicy.cs`,
  `src/Kumunita.Core/Projects/GoalStandingPolicy.cs`, and any `*TranslationPolicy.cs`
  — a **static class of pure methods over plain values** (no `IDocumentStore`,
  no `IDocumentSession`, no async I/O), with a plain unit test in
  `tests/Kumunita.Core.Tests/` (e.g. `GoalStandingPolicyTests.cs`).
- Read the **C2-05 handoff note**
  (`docs/archive/lanes/improve-code/improve-code-2-handoff-notes.md`) — the last entry
  names **your** unit as the successor, and is where your entry continues the
  running log in the same shape.

### The concrete starting numbers (measured at `d768ace2` — trust these + the
### code, re-derive before touching anything)

- **The two services** (the plan's DoD #7 named targets — both currently have
  **no unit (non-Testcontainers) test naming a decision of theirs**):
  - `src/Kumunita.Core/Notifications/MailAlarmsService.cs` — **146** lines. One
    I/O method: `EvaluateAsync(IDocumentStore store, IAlertSink? alertSink,
    DateTimeOffset now, ct)` (static). It reads (in ONE read-only `QuerySession`)
    the window's `SuppressedAddress` `bounce` / `complaint` counts + the
    `EmailDeadLetter` count, **decides** the T4 threshold breach
    (`bounces + complaints >= AlertThreshold`, `AlertThreshold = 5`,
    `WindowHours = 24`), **builds** the `AlertEvent` payload (the pin §3 mapping —
    counts + reason breakdown, no PII), then **fires** it through
    `IAlertSink.ReportAsync` (best-effort, `try/catch` swallow; a `null` sink is a
    no-op). The **pure decision part** = the threshold check + the `AlertEvent`
    construction (given the three counts + the window/threshold constants → a
    breach boolean + the `AlertEvent`). The I/O part = the `QuerySession` reads
    + the `ReportAsync` fire.
  - `src/Kumunita.Core/Usage/UsagePurgeService.cs` — **61** lines. One I/O method:
    `PurgeAsync(IDocumentStore store, DateTimeOffset now, ct)` (static). It
    computes the retention cutoff (`now - RetentionDays`, `RetentionDays = 365`)
    and **decides** which `UsageEvent` rows qualify (`e.At < cutoff`), then
    deletes them in one `OpenSession` batch (id-collection + delete + a single
    `SaveChangesAsync` — the `AuditPurgeService` house shape) and returns the
    count. The **pure decision part** = the cutoff computation + the row-
    qualifies predicate (given `now` + a row's `At` → keep/purge). The I/O part
    = the `Query`/`Delete`/`SaveChangesAsync`.
- **The seam-test gate** (the line this unit feeds): Rule 1 — every `*Policy` /
  `*Resolver` / `*Evaluator` / `*Builder` must be named by a **unit** (non-
  `IClassFixture<PostgresFixture>`) test. Rule 3 — services-with-unit-tested-
  part **≥ floor 10**, currently **71** (already met). So DoD #7 is a **named
  coverage line** (these two specifically get a unit-tested decision part), not
  a floor-count line — the floor is already green and your job is the *named*
  two, not to raise the floor.
- **Already covered (do not re-work):** `SuppressionService` (the plan's DoD #7
  "Suppression already covered" note) and the C04 `*Policy` set (`EventStandingPolicy`,
  `GoalStandingPolicy`, `*TranslationPolicy`) — the reference shapes, each with a
  plain unit test.

## Give each service a unit-tested decision part (one per commit)

- **Extract the pure decision** out of each I/O method into a **static
  `*Policy`** (or a pure `*Evaluator` / named helper — a **real seam**, the
  part-vs-whole rule; a C13 cold read of the extracted surface passes on its own).
  The policy is **pure**: plain in/out values, no `IDocumentStore`, no
  `IDocumentSession`, no async I/O — exactly the `EventStandingPolicy` /
  `GoalStandingPolicy` shape. Keep the I/O method's **behaviour identical**:
  the same reads, the same threshold/cutoff, the same `AlertEvent` / deleted-row
  set, the same best-effort `ReportAsync` / single-`SaveChangesAsync` commit —
  only the *decision* moves to the policy, which the I/O method now calls.
- **Write the unit test** for the decision surface (in `tests/Kumunita.Core.Tests/`,
  **no** `IClassFixture<PostgresFixture>` — a plain xunit.v3 `[Fact]`): pin the
  boundary (a count exactly at the threshold, one below / one above; a row
  exactly at the cutoff, one older / one newer), the no-PII payload shape
  (MailAlarms), and the keep/purge edge (UsagePurge). This is the test that the
  seam-test Rule 1 needs — it *names* the decision surface.
- **Re-point the service** to delegate its decision to the policy (the C04
  `EventService.CheckCreate/CheckEdit/CanAddTranslation` → `EventStandingPolicy`
  forwarder shape — single-source, so the display and the gate can never drift).
- **Do not mint a new `I*Service`** to make the line pass — the seam-test rule
  wants a **unit-tested decision**, not a new interface. Do not touch the
  seam-test floor (already met).
- **Do not raise the `cs_file` ceiling** or add a ceremony type. The two
  services are small (146 / 61 lines) — the extraction is the *test-coverage*
  seam, not a size seam (neither is over the 600 file ceiling; there is no
  `files` entry to retire here).

## Definition of done (C2-06)

- [ ] **Both services have a unit-tested decision part:** `MailAlarmsService` and
      `UsagePurge` each delegate their decision to a named, **pure** `*Policy` /
      helper, and that surface has a **plain unit test** (no
      `IClassFixture<PostgresFixture>`) in `tests/Kumunita.Core.Tests/`.
- [ ] **No ceremony type:** the extracted surface is the *true* decision seam
      (the part-vs-whole rule a C13 cold read would pass); a test that merely
      calls the service (no decision extracted) does **not** count.
- [ ] **Behaviour unchanged:** the same reads / threshold / cutoff / `AlertEvent`
      / deleted-row set / best-effort fire / single `SaveChangesAsync` — only the
      decision's *location* moves. `dotnet build Kumunita.slnx` — 0 errors.
- [ ] **The gate is green:** `tools/code/code-check.ps1` — OK (0 violations)
      (the new `*Policy` is named by a unit test — seam-test Rule 1 satisfied;
      no new `I*Service`; the floor is unchanged and still met).
- [ ] **The existing service behaviour is still pinned:** any
      Testcontainers-level test of `EvaluateAsync` / `PurgeAsync` (if one exists)
      still passes against the re-pointed service.
- [ ] **Record before → after** in the handoff note (the two services, the
      decision surface each got, the test each got) — and **the docs gate is
      green** (`tools/docs/improve-check.ps1` — ALL GATES PASS).

**If any DoD line fails, stop and say which one and why — do not fix it
silently.** The honest limit of this unit is small (two small services, a
decision extraction each) — if a service's decision is genuinely inseparable
from its I/O (no pure surface to name), record that with a code-verified reason
rather than forcing a ceremony type. The DoD's *"say which line fails and why"*
is honored by the handoff entry, exactly as C2-05's was.
