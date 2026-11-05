# C2-04 — Module surface: the Marten allowlist down to ≤ 5

## What you are doing

You are the **fifth** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/plans-milestones/plan-improve-code-2.md`). Your unit is **C2-04**, the
second of the two **DoD #6** units (the module-surface lines). C2-00 landed the
honest ratchet gate; C2-01 + C2-02 finished the translation-lane collapse
(**54 → 39**, the `≤ 40` line met); **C2-03 just closed DoD #6's first half** —
the concrete-`*Service` crossings are down from **21 → 9** (12 retired, 9
carried with `adr: 0291`, `4f59f033`). Your unit is **DoD #6, half two: the
`controllers_marten_allowlist`.**

**D4** says it in one sentence: *"A controller on Marten moves its document
access to the service it owns; the allowlist keeps only the ones whose reason is
*not* 'C09 survivor.'"* Your unit is exactly that: take the **21**
`controllers_marten_allowlist` entries and reduce them to **≤ 5**, by moving
each moveable controller's direct Marten document access to the **service that
owns it** (the ADR 0006 "the service owns the write, the controller is a thin
handler" shape) — and **keep** only the ones whose reason is genuinely
*not* "a C09 survivor we can still move," each with an honest reason.

The codebase is a modular monolith with explicit module boundaries (ADR 0006,
`docs/adr/0006-module-boundary-contracts.md`). A **controller that opens its own
Marten session** to read or write domain documents is a smell the lane exists to
remove: it means the write/aggregate logic that *belongs to a bounded context's
service* is living in the Web layer, so the controller is both the transport
*and* the write-surface. The C03/C09 reference shape is the opposite: the
controller resolves the actor and calls the **owning service**'s lane, which
opens the session and commits (the `TranslationLane` the C2-01/C2-02 units just
collapsed the write forwarders onto is the model). **D4's target is the count:
21 → ≤ 5.**

**Your unit is exactly that.** For each of the 21 allowlist entries, decide
against the code: can the controller's document access move to the service it
owns (re-point the controller, let the service open the session), or is the
remaining Marten touch **genuinely warranted** (pure diagnostics, a
cross-aggregate write that has no owning service, a C08 frozen surface) and
**kept with an honest reason**? The behaviour is unchanged — the same documents
are read/written, the same audit rows are recorded — only **which layer owns the
session** moves.

**You are NOT** doing C2-03 (concrete-`*Service` crossings — done, `4f59f033`),
C2-05 (the still-large files: `FirstBootSeeder` 991 / `SampleDataSeeder` 1633 /
`ProjectsController` 802), C2-06 (the two untested services `MailAlarms` +
`UsagePurge`), C2-07 (the SEC pointer), or C2-08 (the close). You are **not**
re-opening C2-01/C2-02 (the translation lane is done — don't touch
`*Translation*` forwarders) and **not** re-opening the 9 carried concrete
crossings (those are C2-05/C2-06's scope, `adr: 0291`). You are **not** touching
routes, rendered HTML, or document schemas, **not** adding a feature, and **not**
doing a project split (the predecessor explicitly rejected one).

**Do not re-fix the gate.** The ratchet is honest (C2-00). The allowlist is a
**shrink-only** list: you *retire* an entry as its controller stops touching
Marten, and you never add a *new* entry to compensate a move you didn't make.
The gate has **two hard checks** (understand these before editing):

1. **Stale entry fails** — an allowlist entry whose file no longer touches
   Marten is a failure (`stale controllers-marten-allowlist entry`). So when a
   controller stops opening a session, **retire its entry in the same commit**.
2. **Missing entry fails** — a controller that touches Marten but has no
   allowlist entry is a failure (`controller on Marten without an allowlist
   entry`). So you never *remove* an entry from a controller that still opens a
   session.

There is **no hard `≤ 5` gate check** — that is the **plan's DoD target** you
pursue by moving code, not a number the gate enforces. A *new* edge you must
keep, or any baseline `count` you must raise, still needs a **named `adr:`** in
the same commit (the ratchet rule).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `4f59f033` (C2-03 close).
- Commit style: `feat(improve-code-2): C2-04 — <short description>` (conventional
  commits, em dash, no ADR in the subject). **One controller (or one coherent
  family of controllers, e.g. the 4 C08 Projects-cluster controllers together)
  per commit** — the gate green after each, the entry retires as the controller
  moves off Marten.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/plans-milestones/plan-improve-code-2.md` — the **D4**
  decision and the **C2-04** row are your spec, and **DoD #6** (the target:
  "Marten allowlist ≤ 5").
- Read the contract: ADR `docs/adr/0006-module-boundary-contracts.md` — the
  "the service owns the decision/write, the Web is the transport" shape. The
  C2-01/C2-02 `TranslationLane` migration (the controller passes a session to the
  lane, or the service opens its own — `BoardService` / `TodoService` /
  `ProjectService` post-C2-02) is the **reference move** for a controller
  handing its document access to the owning service.
- Read the **C2-03 handoff note**
  (`docs/plans-milestones/improve-code-2-handoff-notes.md`) — it names **your**
  unit as the successor, notes that **`DocumentController` +
  `DocumentFolderController`'s reason text is now stale** (C2-03 minted
  `IDocumentService` / `IDocumentFolderService`), and is where your entry
  continues the running log in the same shape.

### The concrete starting numbers (measured at `4f59f033` — trust these + the
### code, re-derive from `tools/code/code-baseline.json` before touching anything)

- **21** `controllers_marten_allowlist` entries. **Three groups by their
  `reason` field** (re-derive each against the code — the reason is the *claimed*
  basis, not proof):

  - **C09 survivor — moveable to its owning service (the bulk of the 21).**
    These are the ones D4 says to move: the controller opens a session to write
    an aggregate the **owning bounded context's service** should own. E.g.
    `AnnouncementController` (write `Post` + `Translation` + moderation rows —
    the `AnnouncementService` owns it), `PageController` (page + block +
    translation + moderation — `PageService`), `PostsController` (post/reply
    aggregate — `PostService`), `CommunityController` (community + membership),
    `GroupsController` (group + membership), `GuardianController` (consent +
    side-row), `TagController` (tag + translation + side-row), `AttachmentController`
    / `ContentImageController` (parent `Post` read + `Media` quarantine — the
    `Media` service owns the quarantine), `DirectoryController` (per-member
    `Tag`/`TagTranslation` read — the `DirectoryService`/`IDirectoryService`
    C2-03 just minted owns the read), `ReportTargetsController` (read `Post`/
    `Reply` + translations across Posts + Moderation — `IModerationService`),
    `AdminController` (first-run setup + seed-user creation),
    `AccountController` (2FA setup reads an `IdentityToken`),
    `AdminAnalyticsController` (audit-log export writes an `AccessAudit` row).
  - **C08 frozen surface (the Projects cluster — 4 entries).**
    `ProjectsController` / `BoardsController` / `ProjectsBaseController` /
    `TodosController` — their read builders + shared-base reads were C08's
    frozen surface; C09 "does not reshape it." **Verify each against the code:**
    if the owning service (`ProjectService` / `BoardService` / `TodoService`,
    all with committed surfaces post-C2-02) can now own the read, move it and
    retire the entry; if it is genuinely still a cross-aggregate read the service
    does not own, keep it with that honest reason (not "C09 survivor").
  - **Pure diagnostics (1 entry).** `HealthController` — the only store op is the
    readiness connectivity probe (`Storage.Database.AssertConnectivityAsync`),
    which is the plan's named diagnostics example; there is **no owning service**
    to route it through. **Keep** this one (its reason is legitimately *not*
    "C09 survivor").

**The 21 → ≤ 5 gap is ~16 entries to move.** Not every one is clean — that is
the unit's work: move the ones whose owning service exists or is a *narrow*
mint (the C2-03 `I*Service` pattern), and **keep with an honest reason** the ones
that are genuinely warranted (the diagnostics probe, a cross-aggregate write with
no owning service, a C08 surface the service still does not own). A controller
you **cannot** move because minting the owning service would be a ceremony type
(or would pre-empt C2-05's god-surface shrink of `PostService`) is **named**, not
forced — record the reason, do not raise the ratchet to hide it.

### Two facts from C2-03 that change the starting state (verify, then act)

- **`DocumentController` + `DocumentFolderController` (entries 9 + 10)** now
  have committed interfaces (`IDocumentService` / `IDocumentFolderService`,
  minted in C2-03 `e6f5cd1b`) — but they **still open their own Marten session**
  for the cross-context `Tag`/`DocumentFolder` reads + the `Media` quarantine
  write. Their `reason` text ("uses the concrete `DocumentService` (no
  interface)" / "needs an `IDocumentFolderService` committed surface") is now
  **stale** — the interfaces exist. Either (a) move the remaining cross-context
  reads into the owning services and retire the entries, or (b) keep them with a
  **corrected** reason (the cross-context span, not a missing interface).
- **The 9 carried concrete crossings (`adr: 0291`)** include `PostService` (8
  files) and `NotificationService` (4 files) — **do not** try to resolve those
  here; they are C2-05/C2-06's scope. If a controller you are moving also crosses
  a carried concrete service, leave that crossing alone and note it.

## Reduce the allowlist to ≤ 5 (one controller / family per commit)

- **Re-derive the 21** from `tools/code/code-baseline.json`
  (`controllers_marten_allowlist` → `file` + `reason`). For each, find the
  controller's actual Marten use (`IDocumentSession`, `store.
  LightweightSession()`, `Query<...>()`, `InsertAsync`/`SaveChangesAsync`) — the
  *code* is the truth, the reason is the claim.
- **Moveable → re-point to the owning service.** Where the owning service exists
  (an `I*Service` from C2-02/C2-03, or the concrete with a committed surface),
  move the controller's document read/write into the service's lane (the
  C2-02 `TranslationLane` shape, or a `*Async` method that opens its own session
  via the service's `_store`), re-point the controller to the service method,
  and **retire the allowlist entry** (the controller no longer touches Marten).
  If the owning service does not expose the method, **add it to the service**
  (the C2-03 "add the missing methods to the interface" pattern) — not a new
  ceremony type. Where a narrow `I*Service` must be minted for the move, follow
  the C2-03 pattern (narrow surface, the part-vs-whole rule, a seam-test naming
  it — the seam-test Rule 2: an `I*Service` must be named by a test).
- **Genuinely warranted → keep with an honest reason.** The diagnostics probe
  (`HealthController`), a cross-aggregate write with **no** owning service, or a
  C08 surface the service still does not own — keep the entry and make the
  `reason` a **code-verified** statement of *why it cannot move* (name the
  cross-aggregate span or the missing service), **not** "C09 survivor." These
  are the ≤ 5 that survive.
- **Update** the service + its `I*Service`, the DI registration, the controller,
  any test that pins the controller's Marten behaviour (re-point it to the
  service's seam), and the `controllers_marten_allowlist` entry (retire it if the
  controller is off Marten; correct the `reason` if it stays). **Edit the
  baseline textually** — a `json.load`/`dump` round-trip churns the `\u003C`
  escapes (C2-01/C2-02/C2-03 all hit this). A new boundary edge or a `count`
  raise you must keep gets a **named `adr:`** in the same commit (the ratchet
  rule); if the whole move is a genuine cross-cutting decision, name the ADR.
- **One controller / family per commit; the entry retires as it moves.** After
  each, re-run the allowlist count — it must be **lower** than 21, trending to
  **≤ 5**. Do not add a new entry to compensate a move you didn't make. Record
  before → after in the handoff note.

## Definition of done (C2-04)

- [ ] **The allowlist reduced to ≤ 5:** the 21 `controllers_marten_allowlist`
      entries are either **retired** (the controller now moves its document
      access to the owning service) or **kept with an honest, code-verified
      reason** (diagnostics / no owning service / a C08 surface the service does
      not own) — **not** "C09 survivor." The count is **≤ 5**; record before →
      after (21 → N) in the handoff note.
- [ ] **No ceremony types:** every service method added and every `I*Service`
      minted is the *true* cross-layer surface (the part-vs-whole rule a C13 cold
      read would pass); anything that would be a split-for-a-number is **named**
      (an honest `reason` / an `adr:`) instead.
- [ ] **Behaviour unchanged:** each move is an ownership change (the service now
      opens the session), not a logic change — the same documents are read/written,
      the same audit rows are recorded, the same C3 single-`SaveChangesAsync`.
      No route, rendered-HTML, document-schema, or feature change.
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. The touched seams have
      passing tests (**run** the affected suites — the re-pointed controllers'
      Web tests + the owning services' Core tests — don't just build). Note the
      xunit.v3 quirk: `--filter-class` needs the **fully-qualified** class name
      (a bare name returns `total: 0 / exit 8`), and one assembly with 0 matching
      tests is the documented "Zero tests ran / Exit code 8" quirk, not a failure.
- [ ] `tools/code/code-baseline.json` — the retired / reason-corrected
      `controllers_marten_allowlist` entries updated (shrink-only; **no stale
      entry** — the gate fails one; **no new entry** to compensate; any new edge
      or `count` raise you must keep carries a **named `adr:`** in the same
      commit). **Edit it textually.**
- [ ] `tools/code/code-check.ps1` — green on the current tree (it is the
      authoritative check — a `--report` list truncates to the top 10 per
      category, so **verify every entry decision against `code-check.ps1`**).
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note gets a **C2-04** entry (continue the running log): which
      controllers moved to which owning service, which were kept and *why*
      (the honest reasons), the before → after allowlist count (21 → N ≤ 5), the
      baseline entries retired / reason-corrected, and the exact remaining ≤ 5
      (so C2-05 — the still-large files — and the reviewer see DoD #6's second
      half is closed).
