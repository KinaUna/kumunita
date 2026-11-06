# C3-06 — IMPROVE-CODE-3 unit prompt (the Marten-allowlist remainder: the no-service + edge-minting keeps)

You are the **seventh agent** on the **IMPROVE-CODE-3** lane in the **Kumunita** repo
(`/home/agent/projects/kumunita`, a self-hosted single-community platform on .NET 10,
Marten + Wolverine, Razor Pages/MVC, Postgres). C3-00…C3-05 are done. You run the
**C3-06** unit — the **`controllers_marten_allowlist` remainder**: the 18 entries
C3-05 left, driven down to the lane's **`≤ 5`** DoD target (or honestly recorded
as non-met, see "How to judge this unit").

## Why you're here

C3-05 (ADR 0295) retired the two **clean** entries (`GoalsController` — 0 ops of
its own, all via `IProjectService`; `AccountController` — its one verify-token
read moved to the new `IIdentityService.LoadVerifyTokenAsync` seam), taking the
allowlist **20 → 18**. It deliberately left the 18 hard ones — the ones that are
**not** a clean "move the op to an under-600 service" move. C3-05's handoff hands
them to you. C3-06 is the unit that lands the lane's **`allowlist ≤ 5`** DoD line,
and if it does (and every other carried-in DoD line holds), it unblocks the
six-member "shipped" flip that C3-08 the close lands.

## What this unit must achieve (the unit DoD)

**Drive `controllers_marten_allowlist` from 18 down to ≤ 5** (DoD #6), retiring
every genuinely-retire-able entry and **honestly recording** the ones that can't
retire without forcing a ceremony split, minting a boundary edge, or reshaping a
frozen seam. Work through all 18 in `tools/code/code-baseline.json`. They fall
into resolution classes — pick the right one per entry, don't force one:

1. **No-service keeps (mint a *real* owning service, or record the aggregate
   decision):** `AdminController`, `GroupsController`, `AttachmentController`,
   `ContentImageController`, `AdminAnalyticsController`. These write a
   multi-document / cross-module aggregate that no single service owns. **Minting
   a ceremony `I*Service` to hide a concrete dependency is the anti-pattern** —
   only mint a real part-vs-whole service where one genuinely owns the surface
   (e.g. a real `IAdminService` for the admin-setup write cluster). Where the
   writes legitimately span modules no service owns, **record the aggregate
   decision** (keep the entry, name the reason) rather than forcing a type.
2. **Edge-minting / cross-module read keeps (the edge decision):** `DocumentController`
   (its tag read + Media quarantine write would MINT NEW `Documents→Tags:Tag` +
   `Documents→Media:MediaObject` edges), `DirectoryController` (needs
   `HttpContext.Request`'s effective language), `ProjectsController` +
   `ProjectsBaseController` (C08 builder reads of a concrete cross-module type).
   The decision: keep the read on the store **and** name the minted edge (ADR
   it), or extract the contract (an interface / `*Keys`) so routing it through a
   service is not a new edge.
3. **The C08 frozen-surface reads:** `BoardsController`, `TodosController` (the
   board/todo lane reads `Board`/`BoardItemPlacement`/`Todo` + membership). The
   C08 builder extraction was deferred as C08's exit — C3-06 is the unit that
   either does that extraction (a real seam) or records it as a C08-surface keep.
4. **Diagnostics:** `HealthController` (the readiness connectivity probe — a
   pure diagnostic, no owning service). This is the plan's named diagnostics
   keep; the honest resolution is to **record it** (keep the entry, name it),
   not to mint a `IHealthService` ceremony type.
5. **The 5 honest-keeps C3-05 recorded (ADR 0295)** + the 2 still-god-file
   keeps: `AnnouncementController` (caller-owns-transaction, `AnnouncementService`
   519 — needs the seam reshaped to open its own sessions), `TagController`
   (transactional write + the register-pinned "no new seam" read), `PostsController`
   (`PostService` 583, no headroom), `GuardianController` (`GuardianService` 555,
   no headroom), and `PageController` (`PageService` 852 — **still** a god-file,
   the keep still stands) + `ReportTargetsController` (`ModerationService` god-file).
   For each: the honest resolution is the **seam-reshape** (a real
   part-vs-whole service that opens its own session, so the controller's
   caller-owns-transaction shape disappears) — **or** a documented no-headroom /
   pinned keep, if the reshape would itself breach the ceiling or fight a frozen
   seam.

**You are judged on retiring every genuinely-retire-able entry and honestly
recording the ones you can't** — not on hitting `5` at any cost. If the honest
outcome is 8 or 9 (not 5) because the remainder is genuinely the
no-service / edge-minting / diagnostics / C08-frozen class, **that is a correct
honest-finding, not a failure** — record it in the handoff + ADR, and C3-08 the
close decides the flip.

## Starting state (verified at C3-05 close)

`CodeMetrics`: **files 8, classes 3, interfaces 8, methods 89,
boundary_violations 465, allowlist 18**. Branch `development`, HEAD `1f028426`
(C3-05). The 8 files over 600 are `SampleDataSeeder` (ADR 0292 keep),
`PageService` (852), `GroupsController` (1641), `EventController` (1249),
`TodosController` (1096), `PostsController` (1004), `UserPortabilityService` (715),
`LocalizationService` (603). The 18 allowlist entries are the 5 classes above;
their current `reason` text (C3-05 wrote the 5 honest-keep ones to the measured
rationale) is in `tools/code/code-baseline.json`.

## How to do it

- **Retiring** = remove the entry from `controllers_marten_allowlist` in
  `tools/code/code-baseline.json` (the gate's pure-text scan for
  `IDocumentStore`/`IDocumentSession`/`IQuerySession` must find **none** in the
  file — remove the ctor param, the `using Marten;`, and any doc-comment `cref`
  that names a store type). The C3-05 `GoalsController` detach (base `store` →
  `IDocumentStore?` + `SeedExistingTagSlugsAsync` null-guard) is the reusable
  pattern for the base-class children (`Boards`/`Todos`/`Projects`/`Goals`).
- **The service seam** (where minting a real service is the honest move): add the
  op to the owning service (or a new real part-vs-whole service), change the
  controller to call it via DI, and **remove the `IDocumentStore` field/param**.
  Verify with `CodeMetrics` the service is still ≤ 600 **before** you retire the
  entry (C3-05's 5 honest-keeps are exactly the ones where the service has **no
  headroom** — that's the finding, not a bug).
- **Minting a boundary edge** is allowed *only* with an `adr:` — a new
  `Documents→Tags:Tag` or `Web→*` edge is an honest raise to ADR in the **same
  commit** (the gate fails otherwise). Prefer the contract extraction
  (interface / `*Keys` / `*Policy`) where it avoids the edge entirely.
- **Edit `tools/code/code-baseline.json` by text/regex only** — it carries 36
  `\u003C` escapes in the `methods` ids; `json.load`/`dump` churns them to
  literal `<` and breaks the gate. Validate with
  `python3 -c "import json; json.load(open(...))"` before writing.
- The `boundary_violations` baseline **may change** (a minted edge rises, an
  extracted contract lowers an edge). **Do NOT hand-edit counts** — run
  `CodeMetrics` and let the measured value be the baseline; a *rise* is an honest
  finding to ADR, a *drop* is fine (the ratchet only forbids un-ADR'd raises).

## Rules of the road

- **Ratchet + gate are the arbiter.** Retire only what `CodeMetrics` confirms;
  don't guess which entries are clean.
- **The honest-finding doctrine (central here).** This remainder is the
  no-service / edge-minting / diagnostics / C08-frozen class — the ones the plan
  expected to be hard. **Record a keep with its real reason** (minting a ceremony
  type, minting an edge, or a no-headroom seam is all "forcing it"); a documented
  keep is the correct outcome, not a cop-out. The plan: *"If any DoD line fails,
  stop and say which one and why — do not fix it silently."*
- **The `≤ 5` target is a DoD line, not a command.** If the honest floor is 8,
  land 8 with the reasons named, and let C3-08 decide the flip on the true number.
- **Commit style** (see `docs/dev/testing-and-ci.md` + the C3-05 commit):
  `feat(improve-code-3): C3-06 — <short desc>`. No ADR number in the **subject**
  (subject = the lane ID + scope only; the ADR number goes in the **body** when
  you add one).
- **Keep the docs in sync** (`AGENTS.md`): the ADR (the entries retired vs kept +
  why, any minted edge + any new service), the ADR-index README row (gate (b)
  requires `docs/adr/0*.md` file count == README row count), the `WhatsNew.cs`
  row **only if C3-08 the close lands the flip** (C3-06 is a unit, not the close —
  do not add a version row), and the handoff notes.
- **Don't pause mid-task** (AGENTS.md): a failed tool call is a recoverable step —
  read the error, adjust, continue.

## Definition of done — verify in this order before you call it done

1. `dotnet build` → 0 errors.
2. The affected tests (the Web tests for each retired controller; the Core tests
   for each service that gained an op or was newly minted) pass.
3. `pwsh tools/code/code-check.ps1` → **0 violations** (the allowlist entries you
   retired are gone; the ones you kept are still there with a reason; any minted
   edge or new service's over-ceiling surface carries its `adr:`).
4. `pwsh tools/docs/improve-check.ps1` → **ALL GATES PASS** (esp. the ADR-index
   drift gate (b) if you added an ADR).
5. `tools/code/code-baseline.json` still has its 36 `\u003C` escapes; `files` 8,
   `classes` 3, `methods` 89 unchanged unless a service split legitimately moves
   them (if so, the move is measured, not hand-typed); `allowlist` ≤ 5 **or the
   honest floor with every remaining entry's reason named**; `boundary_violations`
   measured (any rise ADR'd).
6. The handoff notes have a C3-06 entry (what you retired, what you kept + the
   honest reasons, the allowlist before → after, and — if the floor is above 5 —
   the explicit "DoD #6 `≤ 5` not met, floor is N" finding for C3-08).
7. `git status` clean and committed.
