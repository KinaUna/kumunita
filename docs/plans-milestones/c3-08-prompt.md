# C3-08 — the close: verify every DoD line, then decide the six-member flip

You are the **ninth and final** agent on the **IMPROVE-CODE-3** lane in the
**Kumunita** repo (`/home/agent/projects/kumunita`, a self-hosted single-community
platform on .NET 10, Marten + Wolverine, Razor Pages/MVC, Postgres). C3-00…C3-07
are done and green. You run **C3-08, the close + independent verification** — the
successor to every unit before you. There is no unit after you: you either land
the six-member "shipped" flip the two predecessor lanes withheld, or you close
**held** and hand the remainder to a successor.

## Why this close is different from C2-08

C2-08 (the predecessor's close) re-verified every DoD line from a clean state and
**closed held** — two lines did not hold: `files` **23** (> 8) and the Marten
allowlist **20** (> 5). The C3 lane finished the first: **`files` is now 8** (C3-00…
C3-04, ADR 0292/0294) — DoD #3/#4 is **met**. The second is the crux of this close:
C3-05 + C3-06 retired the clean allowlist entries, but **`allowlist` is 17** — the
honest floor (ADR 0296), because the 17 keeps are the no-service / edge-minting /
diagnostics / C08-frozen class the plan expected to be hard. **DoD #6's `≤ 5` line is
therefore NOT met (17 > 5).** C3-07 retired the 3 flagged-accidental boundary edges
(2 contract extractions + the `TagSlugs`→`TagSlugParser` name-collision rename),
moving `boundary_violations` **465 → 459** (ADR 0297).

So the close's honest question is: **does "every carried-in DoD line holds" include
DoD #6's `≤ 5`, or does the plan's own honest-finding clause ("if the honest outcome
is 8 or 9 (not 5) … that is a correct honest-finding, not a failure" — quoted in ADR
0296) license closing shipped on a measured 17?** That is your decision, made with
the full picture below. You do **not** get to quietly raise the allowlist or paper
over it — the ratchet is the arbiter.

## What you are doing

Your job is **not** to do new reduction work. It is to **re-verify every carried-in
DoD line against the tree from a clean state** (the plan's instruction, carried
forward from both predecessors: *"If any DoD line fails, stop and say which one and
why — do not fix it silently."*), and then — **and only if every line holds** —
**land the six-member "shipped" flip** the two predecessors deliberately withheld.
That flip is the `AGENTS.md` contract: `Milestones.cs` / `WhatsNew.cs` / `README.md`
/ `STATUS.md` / `ARCHITECTURE.md` / the two pin tests (`WhatsNewTests` +
`MilestonesTests`). A "shipped reduction" records a reduction that **happened**;
the honest hold (C2-08) preserved the flip because the reduction had not.

**You are NOT** re-opening any prior unit, adding a feature, touching routes /
rendered HTML / document schemas, adding or removing any type / method / test that
the close itself does not require, or re-fixing the gate (the ratchet is honest
since C2-00). You are **verifying**, then — conditionally — **recording the ship**
(or the honest hold).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `3e871134` (C3-07 close + handoff). **Every C3 unit C3-00…C3-07 is merged.**
- The two predecessor closes both closed **held**: C2-08 at `ede7e883` (the C3-lane
  start; `files` 23 · `methods` 91 · `classes` 6 · `interfaces` 8 · `views` 3 ·
  `boundary_violations` 465 · `allowlist` 20). **The true "before" for the flip is
  that C2-08 close** (`ede7e883`), measured — not either predecessor's intent.
- The current "after" (C3-07 close, `3e871134`), measured: `files` **8** ·
  `methods` **89** · `classes` **3** · `interfaces` 8 · `views` 3 ·
  `boundary_violations` **459** · `allowlist` **17**. (The C3-lane delta: `files`
  23 → 8, `methods` 91 → 89, `classes` 6 → 3, `boundary` 465 → 459, `allowlist`
  20 → 17, `interfaces` / `views` unchanged.) **Re-measure all of these yourself
  from a clean state in step 1 — do not trust these numbers.**
- **The 5 DoD lines that hold (re-verify each):** #1 ratchet honest (the fixed
  gate vs the C2-00 "before" `311d0a4b` = 0 violations; all raises carry an `adr:`
  reason) · #5 translation `≤ 40` (measured **39**) + clone regions in ceiling ·
  #6 **concrete-edges ≤ 9** (9 named, `adr: 0291`) · #7 the two untested services
  (`MailAlarmThresholdPolicy` + `UsagePurgePolicy`) each have a plain unit test ·
  #8 both E11 SEC pointers present (`IModerationService.cs:98` + `TodoService.cs:1033`).
- **The 2 lines under dispute (re-verify each):** #3/#4 `files ≤ 8` (measured **8**
  — **MET**) · #6 `allowlist ≤ 5` (measured **17** — **NOT MET**, the honest floor,
  ADR 0296).
- Commit style (the C3-06 / C3-07 precedent): `feat(improve-code-3): C3-08 — <short
  description>` for the flip (conventional commits, em dash, **no ADR number in the
  subject** — this is a ship, not a new capability), then a `docs(improve-code-3):
  C3-08 — handoff entry …` commit for the note. If you close **held**, the commits
  are docs-only.
- Read first: `AGENTS.md` + `.github/copilot-instructions.md` (the doctrine — the
  six-member contract is spelled out in `AGENTS.md` § "Keeping the docs in sync
  with the code"); the plan `docs/plans-milestones/plan-improve-code-3.md` — the
  **C3-08** row, the **"The close (the six-member flip…)"** section, and the
  **carried-in DoD table** are your spec; the running record
  `docs/plans-milestones/improve-code-3-handoff-notes.md` — the per-unit before →
  after for C3-00…C3-07 (the C3-06 honest-finding on the allowlist is the one to
  read closely); the predecessor's close (the model for the honest flip / honest
  hold): `docs/plans-milestones/improve-code-2-handoff-notes.md` — the C2-08
  section, and `c2-08-prompt.md`.

### The six-member contract (what "land the flip" concretely means)

- **`src/Kumunita.Web/WhatsNew.cs`** — add a newest-first version entry for the
  lane's shipped reduction. The current newest is **`0.85.0`** (`2026-11-01`);
  your entry is a newer `0.86.x`. The **before → after must be the true numbers** —
  the *before* being the C2-08 close (`ede7e883`: files 23 · methods 91 · classes 6
  · interfaces 8 · boundary 465 · allowlist 20), the *after* being your step-1
  clean-state measurement.
- **`src/Kumunita.Web/Milestones.cs`** — add the shipped-lane entry (the "named
  lane, not a renumber" precedent — `Milestones.cs` is the canonical list the home
  page renders; the last entry is **`M37`** `StatusDone`). Note: `Milestones.cs` is
  the **user-facing roadmap** (M1–M37 are all resident-facing features); a
  code-quality lane's entry here must read as a shipped platform change, not as a
  resident feature — read `MilestonesTests.cs` to see what the pin expects (exact
  order + status).
- **`README.md` + `docs/STATUS.md`** — point at the shipped state (they point at
  `Milestones.cs`, the source of truth — update the status, do **not** restate the
  roadmap list).
- **`docs/ARCHITECTURE.md`** — reflect the settled module-boundary surface (the
  two new contracts `IInstanceBaseUrlReader` / `IIdentityPrincipalStore`, ADR 0297;
  the boundary line 465 → 459).
- **The two pin tests** — `WhatsNewTests` + `MilestonesTests` stay **green** (they
  pin the version order and the milestone order/statuses; they are *verified*, not
  edited, unless a new row changes what they pin — in which case the edit is to the
  data, not the pin).

## The work (verify, then decide)

1. **Re-run the tooling from a clean state** and capture the numbers (git state
   clean, no uncommitted work):
   - `pwsh tools/code/code-check.ps1` — must be **OK (0 violations)**.
   - `pwsh tools/docs/improve-check.ps1` — must be **ALL GATES PASS** (esp. the
     ADR-index drift gate (b)).
   - `dotnet build Kumunita.slnx` — **0 errors**.
   - The pin tests + a smoke of the affected suites (e.g.
     `NotificationServiceTests` / `PortabilityRoundTripTests` / `WhatsNewTests` /
     `MilestonesTests`) — green. (xunit.v3 in-process runner:
     `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
     -class "NS.Class"`, single dash; the seam tests use Testcontainers/Docker,
     ~15–35 s startup.)
   - Capture the full baseline (`files` / `methods` / `classes` / `interfaces` /
     `views` / `boundary_violations` / `controllers_marten_allowlist`) as the
     measured "after", and read the C2-08-close (`ede7e883`) baseline as the "before".
2. **Check every carried-in DoD line against the tree** (the plan's table),
   recording the **measured** value of each, not the intended one. In particular,
   re-verify the two under-dispute lines: `files ≤ 8` and `allowlist ≤ 5`.
3. **Branch on the result — exactly one of two, honestly recorded in the handoff:**
   - **Every line holds (or the allowlist is the measured honest floor the plan's
     clause licenses) →** land the six-member flip (above), with the true
     before → after, and a C3-08 handoff entry recording the verification + the
     flip + why the allowlist's 17 is honest (ADR 0296) rather than a failure.
   - **A DoD line fails and is NOT the honest-floor case →** do **not** land the
     flip. Record the failing lines + measured values in a C3-08 handoff entry, and
     hand the remainder to `IMPROVE-CODE-4` — exactly as C2-08 did. The most likely
     successor scope is the 17 `controllers_marten_allowlist` keeps (the
     no-service / edge-minting / diagnostics / C08-frozen class) — name them.

## Definition of done (C3-08)

- [ ] **Every carried-in DoD line re-verified against the tree** — measured values
      recorded (not intent); the clean-state gate runs captured (`code-check.ps1`
      OK, `improve-check.ps1` ALL GATES PASS, build 0 errors, pin tests green).
- [ ] **The outcome is one of exactly two, honestly recorded in the handoff:**
      - **Shipped:** every DoD line holds → the six members flipped (`WhatsNew.cs`
        0.86.x newest-first + true before → after, `Milestones.cs` entry,
        `README.md` / `STATUS.md` status, `ARCHITECTURE.md` surface, pin tests
        green); **or**
      - **Held:** a DoD line fails and is not the honest-floor case → the flip is
        **not** landed, the failing lines + measured values are recorded, and the
        remainder (most likely the 17 allowlist keeps) is handed to
        `IMPROVE-CODE-4`.
- [ ] **The C3-08 handoff entry** is written in
      `docs/plans-milestones/improve-code-3-handoff-notes.md` in the shape of the
      C3-07 entry (the scope, the clean-state verification, the before → after, and
      why the lane is closed shipped or closed with unmet DoD), and the file's
      front-matter "Current state" is updated to C3-08's outcome.
- [ ] **No `code-baseline.json` entry is raised** by the close (a ship records
      reality; if the tree moved, the gate fails — fix the tree, not the baseline).
