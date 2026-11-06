# C3-05 — IMPROVE-CODE-3 unit prompt (the Marten-allowlist clean retirements)

You are the **sixth agent** on the **IMPROVE-CODE-3** lane in the **Kumunita** repo
(`/home/agent/projects/kumunita`, a self-hosted single-community platform on .NET 10,
Marten + Wolverine, Razor Pages/MVC, Postgres). C3-00…C3-04 are done. You run the
**C3-05** unit — the **`controllers_marten_allowlist` clean retirements**.

## Why you're here

C3-03 retired 3 of the 23 allowlist entries; C3-04 dropped the remaining god-files
into partial siblings and — as a side effect — **the owning services behind the
group-(a) allowlist keeps dropped back under 600**, which is exactly the condition
their "honest keep" was waiting for. C3-05 retires those now-unblocked entries plus
`GoalsController`.

## What this unit must achieve (the unit DoD)

**Retire the allowlist entries whose owning service is now ≤ 600** (the god-file
"keep" is no longer true), and verify each against the ratchet gate. Concretely,
work through the 20 `controllers_marten_allowlist` entries in
`tools/code/code-baseline.json` and, for each one, decide:

1. **`GoalsController`** (reason: "C08 Projects cluster: the goal composer +
   delete are the goal lanes; ProjectService is the store seam") — the clean
   retirement. Move the goal store ops to `ProjectService` through the DI seam and
   retire the entry.
2. **The group-(a) keeps** — the entries whose keep rationale was "the owning
   service is a god-file at the cs_file ceiling." C3-04's partial splits made
   several of them **≤ 600 now**. The candidates (verify each — do NOT assume):
   `AccountController` → `IdentityService` (now 338), `AnnouncementController` →
   `AnnouncementService` (now 518), `DocumentController` → `DocumentService`
   (now 192), `TagController` → `TagService` (now 528), `PostsController` →
   `PostService` (now 582), `GuardianController` → `GuardianService` (now 554).
   **Retire each one only where the service has headroom** — moving the store op
   in must NOT push that file over 600. If a service is at 582 (PostService) and
   the op is large, that's an **honest finding**: record it and keep the entry
   (it becomes a C3-06 item), do NOT force it.
3. **Leave for C3-06** (do NOT touch this unit): `PageController` (its owner
   `PageService` is still 852 — a god-file, so the keep still stands), and the
   cross-module / C08-cluster / diagnostics / no-service entries (AdminController,
   AdminAnalyticsController, AttachmentController, BoardsController,
   ContentImageController, DirectoryController, GroupsController,
   HealthController, ProjectsController, ProjectsBaseController,
   ReportTargetsController, TodosController).

The ratchet target the plan tracks for this lane is **allowlist 20 → ≤ 5 by C3-06**;
C3-05 lands the clean subset, C3-06 the rest. **You are judged on retiring every
genuinely-retire-able entry and honestly recording the ones you can't** — not on
hitting a specific number.

## Starting state (verified at C3-04 close)

`CodeMetrics`: **files 8, classes 3, interfaces 8, methods 89,
boundary_violations 465, allowlist 20**. Branch `development`, HEAD
`1e76c9f4` (C3-04). The 8 files over 600 are `SampleDataSeeder` (ADR 0292 keep),
`PageService` (852), `GroupsController` (1641), `EventController` (1249),
`TodosController` (1096), `PostsController` (1004), `UserPortabilityService` (715),
`LocalizationService` (603).

## How to do it

- For each retire: add the store op (the `IDocumentStore` call the controller makes
  today) to the owning service as a new method (or fold it into an existing one),
  change the controller to take the service via the DI seam and call it, and
  **remove the `IDocumentStore` field/param from the controller** where the retires
  allow it. Verify with `CodeMetrics` that the service file is still ≤ 600.
- **Retiring** = remove the entry from `controllers_marten_allowlist` in
  `tools/code/code-baseline.json` AND remove it from `CodeMetrics`'s `--exclude`
  list (the same two places, per the C3-03 / ADR 0293 pattern). A controller that
  loses its last store op no longer needs to be excluded at all.
- **Edit `tools/code/code-baseline.json` by text/regex only** — it carries
  `\u003C` escapes in the `methods` ids; `json.load`/`dump` churns them to literal
  `<` and breaks the gate. Validate with `python3 -c "import json; json.load(open(...))"`
  before writing.
- The `boundary_violations` baseline **may change** when a controller stops
  touching module documents directly (moving the op to the service can *lower*
  some edge counts). **Do NOT hand-edit `boundary_violations`** — run
  `CodeMetrics` after the code change and let the measured value be the new
  baseline (it may go down — that's fine, the ratchet only forbids *raises* you
  didn't ADR). If a count *rises*, that's an honest finding to ADR.

## Rules of the road

- **Ratchet + gate are the arbiter.** Retire only what `CodeMetrics` confirms;
  don't guess which entries are clean.
- **The honest-finding doctrine.** If a "clean" retirement turns out to push the
  owning service over 600, or to mint a boundary edge, **record it and keep the
  entry** (note it for C3-06) rather than forcing it. The plan explicitly
  anticipated this for the god-file keeps.
- **Commit style** (see `docs/dev/testing-and-ci.md` + the C3-04 commit):
  `feat(improve-code-3): C3-05 — <short desc>`. No ADR number in the **subject**
  (subject = the lane ID + scope only; the ADR number goes in the **body** when you
  add one).
- **Keep the docs in sync** (`AGENTS.md`): the ADR (the allowlist retirements, the
  entries retired vs kept + why), the ADR-index README row (the ADR-index drift
  gate (b) requires `docs/adr/0*.md` file count == README row count), the
  `WhatsNew.cs` row if a lane ships (this is a code-lane unit — extend the C3
  line), and the handoff notes.
- **Don't pause mid-task** (AGENTS.md): a failed tool call is a recoverable step —
  read the error, adjust, continue.

## Definition of done — verify in this order before you call it done

1. `dotnet build` → 0 errors.
2. The affected tests (the Web tests for each retired controller; the Core tests
   for each service that gained an op) pass.
3. `pwsh tools/code/code-check.ps1` → **0 violations** (the allowlist entries you
   retired are gone; the ones you kept are still there with a reason).
4. `pwsh tools/docs/improve-check.ps1` → **ALL GATES PASS** (esp. the ADR-index
   drift gate (b) if you added an ADR).
5. `tools/code/code-baseline.json` still has its 36 `\u003C` escapes; `files`,
   `classes`, `interfaces`, `methods` unchanged (you're not touching those this
   unit — only `allowlist` and possibly `boundary_violations`).
6. The handoff notes have a C3-05 entry (what you retired, what you kept + the
   honest reasons, the allowlist before → after).
7. `git status` clean and committed.
