# C2-05 — Module surface: the still-large files under the ceiling

## What you are doing

You are the **sixth** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/plans-milestones/plan-improve-code-2.md`). Your unit is **C2-05**,
**DoD #3/#4 — "the still-large files off the list."** C2-00 landed the honest
ratchet gate; C2-01 + C2-02 finished the translation-lane collapse (the `≤ 40`
line met); **C2-03 closed DoD #6's first half** (the concrete-`*Service`
crossings, 21 → 9, 9 carried `adr: 0291`); **C2-04 just closed DoD #6's second
half** — the `controllers_marten_allowlist` is down from **21 → 19** (2 clean
moves retired, 19 kept with code-verified reasons, `10c8029e`). Your unit is the
**size** line: **the over-600 files off the baseline `files` list.**

**D5** says it in one sentence: *"Shrink the three over-600 files
(`FirstBootSeeder` 991, `SampleDataSeeder` 1633, `ProjectsController` 802) by
the same data-to-resource and service-extraction the predecessor applied to
their siblings."* The broader DoD #3/#4 target is **files ≤ 8** (from 25) and
**the 7 named god-files gone** — so the 3 named files are the D5 seams, and the
other over-600 files (the god-service files) are the same class of work you
continue. Each shrink is a **real split** (the part-vs-whole rule) that
**retires the baseline entry** — the file drops under the 600 `cs_file` ceiling,
the entry is gone, the ratchet only moves down.

The codebase is a modular monolith (ADR 0006). A **file over the 600-line
`cs_file` ceiling** is a smell the lane exists to remove: it means one type has
accumulated too many concerns (a seeder that owns both the seed *data* and the
seed *logic*, a service that owns both the write lanes and the read projections,
a controller that owns both the transport and the builders). The C02/C13
reference shape is the opposite: the seed data lives in a resource/fixture, the
write lanes live in a *named* sub-service, the read builders live in a *named*
projector — each with its own test. **D5's target is the count: 25 → ≤ 8 files
over the ceiling, the 7 named god-files gone.**

**Your unit is exactly that.** For each over-600 file on the baseline `files`
list, decide against the code: can a concern split out to a *named* sub-surface
(a fixture, a sub-service, a projector) — and is that split a **real seam**
(the part-vs-whole rule a C13 cold read would pass) or a **ceremony split**
(split-for-a-number)? Split the real ones (the file drops under 600, the entry
retires); **name** the ones that are genuinely warranted (a single inseparable
concern) with an honest reason — do not raise the ceiling to hide them.

**You are NOT** doing C2-06 (the two untested services `MailAlarms` +
`UsagePurge`), C2-07 (the SEC pointer on `ClaimTodoAsync`), or C2-08 (the
close). You are **not** re-opening C2-01/C2-02 (the translation lane is done —
don't touch `*Translation*` forwarders), C2-03 (the concrete crossings — the 9
carried are `adr: 0291`; shrinking a god-service here *unblocks* them, but you
do **not** re-point those crossings — that is the C2-05 → C2-08 handoff), or
C2-04 (the Marten allowlist is done — `10c8029e`; the group-(a) keeps unblock as
you shrink the god-services, but you do **not** re-move controllers). You are
**not** touching routes, rendered HTML, document schemas, or features, and
**not** doing a project split (the predecessor explicitly rejected one).

**Do not re-fix the gate.** The ratchet is honest (C2-00). The `files` baseline
is a **shrink-only** list: you *retire* an entry as its file drops under 600,
and you **never raise the `cs_file` ceiling** (the anti-pattern the whole round
exists to stop). The gate has the same hard checks as every ratchet line:

1. **Stale entry fails** — a `files` entry whose file is now under 600 is a
   failure. When a split drops a file under the ceiling, **retire its entry in
   the same commit**.
2. **Missing entry fails** — a file over 600 with no `files` entry is a
   failure. You never *remove* an entry from a file that is still over the
   ceiling.
3. A split that **adds** a new type over the ceiling (a bad ceremony split) is a
   failure — the part-vs-whole rule forbids it.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD `10c8029e`
  (C2-04 close).
- Commit style: `feat(improve-code-2): C2-05 — <short description>`
  (conventional commits, em dash, no ADR in the subject). **One file (or one
  coherent family, e.g. a seeder + its fixture together) per commit** — the gate
  green after each, the entry retires as the file drops under the ceiling.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/plans-milestones/plan-improve-code-2.md` — the **D5**
  decision and the **C2-05** row are your spec, and **DoD #3/#4** (the target:
  "files ≤ 8 · the 7 named god-files gone").
- Read the code-shape doctrine: `docs/philosophy/in-code.md` — the **ratchet**
  (the baseline only goes down), the **part-vs-whole test** for new types (the
  split must be a real seam a C13 cold read would pass, not a ceremony split),
  and the **comment rule** (what/when/throws + ADR number).
- Read the **C2-04 handoff note**
  (`docs/plans-milestones/improve-code-2-handoff-notes.md`) — it names **your**
  unit as the successor, notes that the **group-(a) Marten keeps** (Posts /
  Announcement / Page / Guardian / ReportTargets / Tag) resolve once you shrink
  their god-service files (`PostService` 821, `PageService` 917, `GuardianService`
  771, `ModerationService` 753, `TagService` 736), and is where your entry
  continues the running log in the same shape.
- Read the predecessor's own splits for the reference shapes: the **C02
  data-move** (seed data out of the service into a resource/fixture) and the
  **C13 service-extraction** (a god-service split into *named* sub-services,
  each with its own test). The seeders follow C02; the god-services follow C13.

### The concrete starting numbers (measured at `10c8029e` — trust these + the
### code, re-derive from `tools/code/code-baseline.json` before touching anything)

- **25** files over the 600 `cs_file` ceiling (the baseline `files` list).
  **Two groups:**
  - **The 3 named D5 seams** (the plan's explicit C2-05 scope):
    - `Bootstrap/FirstBootSeeder.cs` — **991** (4 over-ceiling methods:
      `SeedDefaultPagesAsync` 89, `BackfillGuideTranslationsAsync` 85,
      `BackfillUserGuidesAsync` 75, `BackfillPageTranslationsAsync` 66 — the seed
      **data** + the seed **logic** in one file; the C02 data-move splits them).
    - `Bootstrap/SampleDataSeeder.cs` — **1633** (the largest file in the repo;
      `BackfillSampleCorpusAsync` 782, `SeedAsync` 615, `EnsureUserAsync` 69 —
      the same C02 data-move shape).
    - `Controllers/ProjectsController.cs` — **797** (the C08 Projects cluster;
      `ProjectDetail` 124, `ProjectsIndex` 103, `ProjectCreate` 54 — **loses the
      route families its C08 twins — `BoardsController` / `TodosController` —
      already took**; the builder extraction is the C08 exit D5 references).
  - **The god-service files (the "7 named god-files" + the rest — the same class
    of work):** Core — `BoardService` 1025, `EventService` 997, `IdentityService`
    989, `UserPortabilityService` 922, `PageService` 917, `TodoService` 839,
    `PostService` 821, `GuardianService` 771, `ModerationService` 753, `TagService`
    736, `GroupService` 705, `AnnouncementService` 704, `LocalizationService` 684;
    Web — `EventController` 1244, `GroupsController` 1639, `TodosController`
    1096, `PostsController` 1010, `BoardsController` 857, `PageController` 833,
    `AdminController` 777, `GuardianController` 746, `AnnouncementController` 611.
- **6** classes over the `class_public_methods` ceiling (25) — DoD #3/#4 target
  **≤ 3** (C2-00 shrank one from 7 → 6; finish it here).
- **8** interfaces over the `interface_methods` ceiling (20) — DoD #3/#4 target
  **≤ 2** (C2-03 carried some; finish the size ones here).
- The `views` (3) and `ts` (0) lines are **already met** (≤ 3 / ≤ 2) — do not
  touch them. `ts_functions` (13) is the TS-function line, already at its target.

**The 25 → ≤ 8 gap is ~17 files to shrink.** Not every one is a clean split —
that is the unit's work: split the ones with a real seam (the C02 data-move for
the seeders, the C13 service-extraction for the god-services, the C08
builder-extraction for `ProjectsController`), and **name** the ones that are
genuinely a single inseparable concern (an honest reason / an `adr:`) — do not
raise the ceiling or force a ceremony split.

## Shrink the files under the ceiling (one file / family per commit)

- **Re-derive the 25** from `tools/code/code-baseline.json`
  (`files` → `id` + `max`). For each, find the file's actual concerns (the
  over-ceiling methods on the `methods` list, the public surface on the
  `classes` list) — the *code* is the truth, the baseline is the claim.
- **Real seam → split, retire the entry.** Where a concern is a *named*
  sub-surface, extract it:
  - **The seeders (C02 data-move):** move the seed **data** (the literal
    page / user / guide / corpus bodies) out of the service into a
    resource/fixture (a `*SeedData.cs` or a JSON/resource), leaving the service
    with only the seed **logic** (the `Ensure*Async` / `Seed*Async`
    orchestration). The data file is a fixture, not a ceremony type — it has no
    behaviour.
  - **The god-services (C13 service-extraction):** split the god-service into
    *named* sub-services (a write-lane sub-service + a read-projector sub-service,
    or a `*Policy` per the C04 pattern) — **only** where the split is a real
    seam (the part-vs-whole rule a C13 cold read would pass; the C03
    narrow-`I*Service` / C06 `INotificationEmitter` precedent). Each new
    sub-service gets its own unit test (the seam-test rule).
  - **`ProjectsController` (the C08 builder-extraction):** move the
    `ProjectDetail` / `ProjectsIndex` / `ProjectCreate` builders + the route
    families its C08 twins already took into a *named* projector / the twin,
    leaving the controller as the thin transport.
  - After the split, the file is **under 600** — **retire its `files` entry**
    (and any `methods` / `classes` / `interfaces` entry the split resolved)
    **in the same commit**.
- **Genuinely warranted → name it, do not force it.** A file that is a single
  inseparable concern (a migration `BuildModel` / `Up`, a `MarkdownRenderer`, a
  `SmtpProbe`) — **keep** it and give the `files` entry an honest `reason` (or
  an `adr:` if it is a deliberate keep), **not** a ceremony split. The gate then
  names the reason. **Do not raise the ceiling.**
- **Update** the split type(s) + their `I*Service` (if the split mints one — the
  C2-03 narrow-surface pattern), the DI registration, the tests (each new
  sub-service gets its own test), and the baseline (`files` retire; `methods` /
  `classes` / `interfaces` retire or correct). **Edit the baseline textually** —
  a `json.load`/`dump` round-trip churns the `\u003C` escapes (C2-01 / C2-02 /
  C2-03 / C2-04 all hit this). A new type you must keep over the ceiling, or any
  `count` raise, gets a **named `adr:`** in the same commit (the ratchet rule).
- **One file / family per commit; the entry retires as the file shrinks.** After
  each, re-run the `files` count — it must be **lower** than 25, trending to
  **≤ 8**. Do not add a new entry to compensate a split you didn't make. Record
  before → after in the handoff note.

## Definition of done (C2-05)

- [ ] **The files line reduced toward ≤ 8:** the 3 named D5 files
      (`FirstBootSeeder` / `SampleDataSeeder` / `ProjectsController`) are **off
      the `files` list** (each under 600, each entry retired), and the
      god-service files are split where a real seam exists (the C13
      part-vs-whole rule) — the **7 named god-files gone** (each under the
      ceiling or carrying an honest `adr:`). Record before → after (25 → N) in
      the handoff note.
- [ ] **No ceremony types:** every new type (fixture / sub-service / projector)
      is the *true* seam (the part-vs-whole rule a C13 cold read would pass);
      anything that would be a split-for-a-number is **named** (an honest
      `reason` / an `adr:`) instead, and the file **stays** (the ceiling is not
      raised).
- [ ] **Behaviour unchanged:** each split is an ownership change (the data moves
      to a fixture, the lanes move to a sub-service, the builders move to a
      projector), not a logic change — the same documents are seeded/read/written,
      the same routes render the same HTML, the same audit rows are recorded. No
      route, rendered-HTML, document-schema, or feature change.
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. The touched seams have
      passing tests (**run** the affected suites — the split sub-services' Core
      tests + the controllers' Web tests — don't just build). Note the xunit.v3
      quirk: `--filter-class` needs the **fully-qualified** class name (a bare
      name returns `total: 0 / exit 8`), and one assembly with 0 matching tests
      is the documented "Zero tests ran / Exit code 8" quirk, not a failure.
- [ ] `tools/code/code-baseline.json` — the retired `files` / `methods` /
      `classes` / `interfaces` entries updated (shrink-only; **no stale entry**
      — the gate fails one; **no new entry** to compensate; **no ceiling raise**;
      any new type over the ceiling or `count` raise you must keep carries a
      **named `adr:`** in the same commit). **Edit it textually.**
- [ ] `tools/code/code-check.ps1` — green on the current tree (it is the
      authoritative check — a `--report` list truncates to the top 10 per
      category, so **verify every entry decision against `code-check.ps1`**).
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note gets a **C2-05** entry (continue the running log): which
      files split into which named seams (fixture / sub-service / projector),
      which stayed and *why* (the honest reasons), the before → after files
      count (25 → N), the baseline entries retired (`files` / `methods` /
      `classes` / `interfaces`), and the exact remaining over-ceiling files (so
      C2-06 — the two untested services — and the reviewer see DoD #3/#4's
      status, and the C2-03 carried crossings / C2-04 group-(a) keeps that now
      unblock).
