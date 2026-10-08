# Plan: IMPROVE — a fractal integration audit of the shipped surface

> **Planned.** This is the **lane register** (secondary tier of the three-tier
> contract) for the **`IMPROVE`** named lane — a *new* lane, numbered
> **after** the M28 close, so **no milestone letter moves** (precedent:
> `GP`, `ML`, `LS`, `SP`, `TZ`, `DF`, `TR`, `RC`, `GU`, `GA`, `RE`, `TG`,
> `PG`, …). The lane's purpose is not a new capability — it is the
> *intentional* application of the philosophy in
> [`../philosophy/README.md`](../philosophy/README.md) to the code and docs
> we already have. Principle 6 says: **"Integration decays. ADRs, review,
> audit, and retrospectives are how we close that decay."** This lane is
> that closing pass, made visible and testable.
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria (≤ ~4 files / ~400 LOC, 3–6
> entry reads, one build + test run). **Zero new `AccessAction`, zero new
> `AccessVia`, zero new `Decide()` branch, zero new `IAuthorizationService`
> surface** — this is a *reading* + *refactoring* pass, not a feature. Any
> unit that discovers a *new* capability requirement **stops, records the
> requirement in the handoff note, and does not build it** (a new
> requirement becomes a *new* named lane, per the convention).
>
> **The one thing that makes this lane different from a lane like M28:**
> M28's units each ship *a new capability*. IMPROVE's units ship *a
> reduction* — fewer lines, fewer duplicates, fewer untested seams, fewer
> places where two docs disagree. The Definition of Done for every unit is
> the *inverse* of M28's: **the codebase is measurably smaller, or
> measurably more integrated, or measurably more discoverable, and the
> existing tests still pass.**

## Why now (the drift we are closing)

The platform has shipped M1–M28 plus ~15 named lanes. The seams the
philosophy names as load-bearing are now *many* — and the seams we haven't
integrated are visible in concrete numbers, collected 2026-10-08 (see
§Evidence):

- **Four files dominate the C# surface** (total: ~22K of 187K lines):
  `KnownTranslationKeys.cs` 8 948, `ProjectsController.cs` 4 634,
  `FirstBootSeeder.cs` 4 471, `ProjectService.cs` 4 301. They each carry a
  *single, coherent* job, but the *internal* differentiation — the small
  seams *inside* them — has not been made. This is "the god part"
  (`anti-patterns.md`, local symptom: "one script that does all of
  moderation").
- **ADR-index drift:** `docs/adr/README.md` has 143 rows; 151 ADR files on
  disk; **0096, 0137–0143** are missing from the index. "Accidental
  integration" in the doc layer: the index is the seam every reader uses to
  find an ADR, and it is stale by 8 rows.
- **Doc duplication:** `AGENTS.md` (257 lines) and
  `.github/copilot-instructions.md` (163 lines) share verbatim sections on
  PowerShell here-strings, `$variables`, Razor verification, git-state,
  browser trusted-folder, and "don't pause mid-task". The repo instructions
  are the *entry seam* for every AI agent; maintaining them in two places
  is "silent coupling" in the agent-integration layer.
- **Handoff notes without an abstract:** the largest
  (`m3b-handoff-notes.md`) is 1 860 lines, 20 more are 800+ lines, 100+ are
  400+ lines. A new agent's first read is *the entire file*; there is no
  "read the abstract, decide if this is the right lane, then read the rest"
  path. "Part sprawl" in the doc layer.
- **Test files mirror service-file size:** `ProjectServiceTests.cs` 4 105,
  `EventServiceTests.cs` 2 292. The philosophy says "we test the seams
  specifically: empty-audience-denies under `All`, delegation scoped
  narrower than the grant, moderator access off-by-default" — the current
  layout makes it *harder* to find those tests than the happy-path ones.

The *pattern* is the one the philosophy names: **the parts got bigger; the
integration between the parts did not keep up.**

## What this lane is (and is not)

**Is:**

1. A *read* pass over the shipped surface, scored against the six
   principles + the three tests + FACES, with findings written to a single
   **audit ledger** (`docs/plans-milestones/done/improve/improve-audit.md`). The ledger
   is the *deliverable the community reads*.
2. A *do* pass — units `U01..U09` — that close the highest-value findings,
   each with an exit criterion that *measures* the reduction.
3. A *harness* — repeatable checks (PowerShell scripts + one CI-able test)
   that keep the improvements honest: a new god-file, a new ADR-index drift,
   a new doc duplication, a new handoff note over 600 lines without a TL;DR
   — each *fails the harness* and therefore *fails the close*.

**Is not:**

- **A re-architecture.** No new bounded context, no re-splitting of the
  three contexts. The "modular monolith" shape (ADR 0001, ADR 0006) stays.
- **A feature.** No new `kw-*` tag, no new surface, no new ADR *adding*
  capability.
- **A rewrite.** No re-host, no re-stack, no re-language.

## Evidence (numbers the plan is built on)

Collected 2026-10-08 with `pwsh` scripts under `.tmp/profile-*.ps1`.

| Scope | Files | Lines |
|---|---:|---:|
| All C# (src + tests) | 632 | 187 604 |
| `src/Kumunita.Core` | 243 | 61 855 |
| `src/Kumunita.Web` | 146 | 38 718 |
| `tests/Kumunita.Core.Tests` | 120 | 50 446 |
| `tests/Kumunita.Web.Tests` | 123 | 36 585 |
| Markdown (docs/) | 744 | 167 831 |
| ADRs | 151 + index | — |
| Design docs | 53 | — |
| First-party TS (client/) | 89 | max 1 544 |
| Razor views | 150 | max 1 411 |

**Top 12 C# files:** 8 948 `KnownTranslationKeys.cs` · 4 634
`ProjectsController.cs` · 4 471 `FirstBootSeeder.cs` · 4 301
`ProjectService.cs` · 3 693 `UserInfoService.cs` · 2 736
`GroupsController.cs` · 2 029 `SampleDataSeeder.cs` · 1 993
`PostsController.cs` · 1 986 `EventService.cs` · 1 957 `PostService.cs` ·
1 802 `EventController.cs` · 1 540 `PageService.cs`.

**Top 8 docs:** 1 860 `m3b-handoff-notes.md` · 1 693
`m13-logging-analytics-design.md` · 1 413 `pages-handoff-notes.md` ·
1 319 `m10-pwa-handoff-notes.md` · 1 287 `m11-portability-handoff-notes.md`
· 1 220 `plan-m13-logging-analytics.md` · 1 162
`file-attachments-handoff-notes.md` · 1 130 `m3b-moderation.md`.

**ADR index drift:** 8 ADRs on disk missing from the index (0096, 0137–0143).

**Doc duplication:** `AGENTS.md` ↔ `.github/copilot-instructions.md` share
6 verbatim sections; the two are not in sync (the instructions file has a
slightly different "Getting a live server" section and a slightly different
Razor-trap list).

## The audit framework (what every unit scores)

Every unit applies the *same* three questions, in the philosophy's own
vocabulary:

1. **Closed-loop?** — can a resident take a problem in and get an outcome
   out, without leaving the platform? *(Code: does a test exercise the
   exit? Docs: does the reader land on the right page without a search
   engine?)*
2. **Handoff?** — is the transfer between parts explicit and named, or
   implicit and tribal? *(Code: named interface vs. `dynamic` call. Docs:
   "design doc → ADR → plan → handoff note" explicit vs. reader must read
   all four.)*
3. **Part-vs-whole?** — does this optimize a part at the cost of the
   whole? *(Code: does shrinking this file make the seam between it and
   its neighbors clearer, or just smaller? Docs: does splitting make the
   relationship between docs clearer, or just each one shorter?)*

Plus the **FACES** check: name at least one face the unit *strengthens*
and one it *consumes*.

## Units

Each unit is sized for a single ~32K-context agent, has its own exit
criteria, and is ordered so each unit's output is a *stable input* to the
next. **No unit re-opens an ADR.**

---

### `U00` — Baseline: audit ledger + improvement harness

**Entry reads (3):** `docs/philosophy/README.md` · `docs/philosophy/in-code.md` ·
`docs/plans-milestones/done/m28/plan-m28-guardian-time-limits.md` (lane register
style, unit sizing, exit-criteria shape).

**Do:**

1. Author **`docs/plans-milestones/done/improve/improve-audit.md`** — the **audit
   ledger**. One `##` section per principle (1–6), one `###` per three-test
   (closed-loop / handoff / part-vs-whole), one `###` per FACES face.
   Under each: a bullet list of findings, each with **where** (file + line
   or path), **what** (one sentence), **which principle/test/face it
   violates**, **severity** (S / M / L — S = trust at risk, M =
   maintainability at risk, L = discoverability at risk), and **a proposed
   unit** (one of `U01..U09`, or "record and accept"). This is the
   *reading* pass; it is the only unit that produces a doc rather than a
   code change.

2. Author **`docs/plans-milestones/done/improve/improve-check.ps1`** — the
   CI-able gate. Exits non-zero if *any* of:
   - (a) a `.cs` file in `src/` exceeds **2 000 lines** (current top-12 are
     the *baseline*; the gate is the *ceiling* — a new file crossing the
     line fails the close);
   - (b) `docs/adr/README.md` index rows ≠ `docs/adr/0*.md` file count;
   - (c) `AGENTS.md` and `.github/copilot-instructions.md` share a `## `
     heading with different content (simple heading-level diff);
   - (d) any `*-handoff-notes.md` under `done/` exceeds **600 lines**
     without a `> **TL;DR:**` block in its first 20 lines;
   - (e) any first-party `.ts` file under `client/` exceeds **800 lines**
     (current max 1 544 — the gate is the ceiling);
   - (f) any `.cshtml` view exceeds **800 lines** (current max 1 411 —
     the gate is the ceiling).

   The gate *allows* the current baseline (it does not force an immediate
   fix); it *prevents* the baseline from growing.

3. Author **`docs/plans-milestones/done/improve/improve-report.ps1`** — the
   human-facing summary: top 10 files by lines (red/amber/green against
   the ceiling), ADR drift count, doc-duplication count,
   handoff-without-TL;DR count, TS-over-800 count, view-over-800 count.

4. Author **`tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`** — a single
   xunit.v3 test that shells out to `improve-check.ps1` and asserts exit
   code 0. Fast (shell-out, not Testcontainers), deterministic (reads the
   repo, not the network).

**Exit:** the audit ledger exists; the harness scripts exist; the test
exists and passes; `dotnet build` + test run succeed.

**FACES:** strengthens *C*oherent (a new resident can narrate what this
lane is in one breath) and *A*daptive (the harness responds to a new
god-file, ADR drift, or doc duplication); consumes *F*lexible (one more
script and one more test to keep in sync — a named, permanent integration
cost).

---

### `U01` — Close the ADR-index drift

**Entry reads (2):** `docs/adr/README.md` · `improve-report.ps1` output.

**Do:**
- Add the 8 missing rows (0096, 0137–0143) to the index, in numeric
  order, with Title / Status pulled from each ADR's H1.
- Add a one-line "index-maintenance" note to the README's top: "Every ADR
  file under this folder must have a row in the index; the
  `improve-check.ps1` gate fails the close if the two drift."
- Run `improve-report.ps1`; confirm drift count = 0.

**Exit:** 8 rows present; gate passes. No other file changes.

**FACES:** strengthens *C*oherent (the index is now the reliable seam);
consumes nothing (pure addition).

---

### `U02` — TL;DR convention for handoff notes

**Entry reads (3):** the 8 largest handoff notes (from Evidence) ·
`docs/philosophy/in-code.md` ("public surface is permanent integration
cost") · `improve-check.ps1` (the gate).

**Do:**
- For each of the 8 largest handoff notes, add a **`> **TL;DR:**`** block
  in the first 20 lines (3–7 lines, blockquote). Answers: *what did this
  lane ship, in one sentence? what is the one seam it created or changed?
  what is the one thing a future reader must not re-litigate?* The rest of
  the file is *untouched*.
- Add a `## TL;DR convention` section (one paragraph) to
  `docs/plans-milestones/README.md` (create if missing).
- Run `improve-report.ps1`; confirm the over-600-without-TL;DR count for
  the 8 largest is 0.

**Exit:** 8 TL;DR blocks added; convention section added; gate passes.

**FACES:** strengthens *C*oherent (a reader decides in 10 seconds whether
a lane's history is the right read); consumes nothing (pure addition to
existing files).

---

### `U03` — De-duplicate `AGENTS.md` ↔ `.github/copilot-instructions.md`

**Entry reads (3):** `AGENTS.md` (full) · `.github/copilot-instructions.md`
(full) · `docs/philosophy/in-code.md` ("seam chosen silently will be
re-litigated by whoever hits it next").

**Do:**
- Identify the 6 shared sections (PowerShell here-string, `$variables`,
  Razor verification, git-state, browser trusted-folder, don't-pause).
- **Choose one canonical location** for each shared section. The
  recommended shape: `AGENTS.md` is the *source of truth* (it is read by
  every agent framework — Copilot, Claude, Cursor, etc.);
  `.github/copilot-instructions.md` becomes a *thin pointer*: it keeps its
  own unique content (the VS Code-specific "Getting a live server"
  section, the `.tmp/` harness rules) and *links* to the shared sections
  in `AGENTS.md` rather than duplicating them. The link is a relative
  Markdown link, not a copy.
- Update the `improve-check.ps1` gate (c) to check that the two files do
  *not* share a `## ` heading with different content (the gate already
  does this; confirm it now passes).

**Exit:** the two files no longer duplicate; the gate passes; both files
are still self-sufficient (the pointer file links to the source).

**FACES:** strengthens the "S" face of FACES (Stability - the trust the agent-integration layer stands on holds under a future handoff) and *A*daptive (a change to the doctrine now lands in one place and both agents see it); consumes *F*lexible (the pointer file now has a dependency on `AGENTS.md` - if it is renamed, the pointer breaks; the gate (c) fails if the link target is missing).

---

### `U04` - Differentiate `KnownTranslationKeys.cs` into per-surface key groups

**Entry reads (4):** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the full file - the one file a unit is *allowed* to read in its entirety, because it is the registry itself) / `src/Kumunita.Core/Localization/LocalizationService.cs` (the reader) / `src/Kumunita.Web/TagHelpers/` (the `kw-l` consumer) / `docs/adr/0015-ui-view-localization-mechanics.md` (the contract this file serves).

**Do:**
- The file is a *flat dictionary* of ~4 500 key-to-value pairs. It is not a god-function; it is a *god-data-file*. The fix is not to split the logic (there is none) but to *differentiate the data* so a reader can find the keys for *their* surface without scrolling 8 948 lines.
- Split the single `EnValues` dictionary into **per-surface static properties** (e.g. `Admin.Guests`, `Settings.Quiet`, `Guardian.TimeLimit`, `Projects.Board`, `Posts.Detail`, `Announcements`, `Pages`, `Tags`, `Events`, `Groups`, `Account`, `Common`) - each an `IReadOnlyDictionary<string, string>`. The top-level `EnValues` remains as a *composition* of the per-surface dictionaries (a `Concat` of `.ToDictionary` calls), so the *public surface* (`KnownTranslationKeys.EnValues`, `KnownTranslationKeys.AllKeys`) is **unchanged** - no reader breaks, no seeder breaks, no TagHelper breaks.
- Each per-surface property gets a one-line doc-comment naming the ADR(s) that introduced its keys (the file already carries per-key ADR comments; we are promoting them to per-group comments).
- The file stays in the same namespace, same file name, same public surface. The *internal* differentiation is the only change.

**Exit:** the public surface (`EnValues`, `AllKeys`) is unchanged; the file is split into per-surface properties; `dotnet build` + test run succeed; the file's line count is *lower* (the per-surface grouping removes the need for a single 4 500-line dictionary literal).

**FACES:** strengthens *C*oherent (a reader can find the `guardian.timelimit.*` keys without scrolling past `admin.guests.*` and `settings.quiet.*`) and the "S" face (the registry holds under a future lane that adds 200 new keys - they land in a new per-surface property, not a 9 000-line monolith); consumes *F*lexible (the composition layer is a new permanent seam - if a per-surface property is added, the `EnValues` composition must include it; the `AllKeys` test pins this).

---

### `U05` - Consolidate the shared controller composer/picker pattern

**Entry reads (4):** `src/Kumunita.Web/Controllers/ProjectsController.cs` (the `SeedGrantPickerOptionsAsync` / `SeedComponentPickerAsync` / `SeedLanguagePickerAsync` composer trio, per its own doc-comment "the M2/M3/M4 shared pattern, reused not reinvented") / `src/Kumunita.Web/Controllers/PostsController.cs` / `src/Kumunita.Web/Controllers/EventController.cs` / `src/Kumunita.Web/Controllers/AnnouncementController.cs`.

**Do:**
- **First: verify the claim.** The `ProjectsController` doc-comment says the composer trio is "the M2/M3/M4 shared pattern, reused not reinvented." Grep all four controllers for the three seed-method names. If the methods are *each defined per controller* (copy-paste, the "silent coupling" anti-pattern in the code layer), that is a finding to record in the ledger with severity M.
- **If duplicated:** extract the trio into one shared helper (a `static` class or a `ControllerBase`-injected helper in `src/Kumunita.Web/Models/` or a new `src/Kumunita.Web/Controllers/Shared/` file) that takes the minimum dependencies it needs (`ILocalizationService`, `ITagService`?, the picker option model). Replace the per-controller copies with calls to the shared helper. The *public surface* (routes, view-models, rendered HTML) is **unchanged** - this is a pure refactor, verified by the existing controller tests (`PostsControllerTests`, `EventControllerTests`, `ProjectsControllerTests`, `AnnouncementControllerTests`) passing *unmodified*.
- **If already shared:** record in the ledger as "verified, no action" with the file/line of the shared helper, and *that is the unit's deliverable* - a verified negative is a legitimate seam-audit outcome.
- In either case, add a one-line note to the shared helper's doc-comment: "the composer trio shared by the post/announcement/event/project/group composer GETs (U05, IMPROVE lane) - new composer GETs call this, do not copy it."

**Exit:** the composer trio is defined exactly once (or the verification is recorded); `dotnet build` + the four affected controller test classes pass *unmodified*; the total line count of the four controllers is *lower* (or the ledger records the verified negative).

**FACES:** strengthens *C*oherent (a new composer GET has one place to copy from, named) and *A*daptive (a picker change now lands in one file); consumes *F*lexible (the shared helper is a new permanent integration cost - it now must satisfy four controllers' needs, and a fifth lane's composer must fit it or extend it deliberately).

---

### `U06` - Extract the two longest views into partials

**Entry reads (3):** `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` (1 411) / `src/Kumunita.Web/Views/Posts/Detail.cshtml` (983) / `AGENTS.md` (the Razor verification doctrine - the 4 known Razor traps; the extracted partials must be re-verified against the *served* HTML, not the source).

**Do:**
- `BoardDetail.cshtml`: identify the self-contained sub-surfaces (the board's lane/column list, the to-do card, the board settings panel - read the view and name what is actually there). Extract each into `Views/Shared/` partials (e.g. `_BoardColumn.cshtml`, `_TodoCard.cshtml`). The parent view's *rendered output must be byte-identical* - verified per the Razor doctrine: rebuild, sign in as the sample GlobalAdmin, navigate to the board, and diff the served HTML against the pre-extraction capture.
- `Posts/Detail.cshtml`: same discipline for its largest self-contained sub-surface (the post body + reply thread, or the translation-swap block - name whichever is the largest contiguous block).
- **Do not extract for the sake of extracting** (part-vs-whole): a partial that is rendered in exactly one place and shares no structure with anything else is *over-differentiation* (`anti-patterns.md`) - the "distributed fragmentation" smell in the view layer. Extract only the block that is (a) > 150 lines, *and* (b) either repeated across two+ views or a distinct concern a future lane will touch independently. Record in the ledger the blocks examined and the reason each was or was not extracted.

**Exit:** the parent views are shorter; the rendered HTML is byte-identical (served-page evidence in the unit's handoff section); the existing view tests / controller tests pass; the ledger records the extraction rationale.

**FACES:** strengthens *C*oherent (a future lane touching the to-do card finds it in one named partial) and the "S" face (the view holds under a future board-settings lane without a 1 400-line edit); consumes *F*lexible (each new partial is a new file a rename or model change must keep in sync with its parent's view-model).

---

### `U07` - Abstracts for the largest design docs

**Entry reads (3):** the 5 largest design docs from the Evidence table (`m13-logging-analytics-design.md` 1 693, `m3b-moderation.md` 1 130, `m18-recurring-events-design.md` 1 100, `m20-notification-quiet-times-design.md` 1 094, `m5-projects-design.md` 1 048) / `docs/plans-milestones/done/improve/improve-check.ps1` (extend the gate) / `docs/plans-milestones/README.md` (the TL;DR convention from U02 - extend it to design docs).

**Do:**
- For each of the 5, add a **`> **Abstract:**`** block in the first 15 lines (5-10 lines, blockquote), answering: *what question does this design settle? what is the one contract it creates (the seam name)? what is explicitly out of scope?* This is the user's exact ask - "a short abstract with key points at the start, so you don't have to read the whole thing to decide if it contains what you need for your current task."
- Extend the `improve-check.ps1` gate: any `docs/design/*.md` over 400 lines without an Abstract block in its first 15 lines fails the close. (The 5 above are the baseline exceptions *after* this unit; new design docs over 400 lines need the abstract from day one.)
- Extend the `docs/plans-milestones/README.md` convention section (U02) to name the design-doc abstract as the same convention at the design tier: "TL;DR for handoff notes, Abstract for design docs - both are the 10-second 'is this the right read' gate."

**Exit:** 5 abstracts added; the gate extended and passing; the convention section extended. No design doc's body is modified (pure addition, like U02).

**FACES:** strengthens *C*oherent (a maintainer decides in 10 seconds whether `m18-recurring-events-design.md` is the right read before investing 1 100 lines of attention) and *E*nergizing (it gives the reader's time back - the philosophy's "the platform gives the residents' time and attention back, not take them," applied to the team); consumes nothing (pure addition).

---

### `U08` - The security & privacy seam audit (the user's explicit question)

**Entry reads (4):** `docs/SECURITY.md` (the privacy model, threat model, access-control rules) / `docs/SECURITY-AUDIT.md` / `src/Kumunita.Core/Authorization/AuthorizationService.cs` (the single decision path) / `src/Kumunita.Web/Middleware/` (the enforcement seams - `BlockedAccountMiddleware`, `TimeLimitMiddleware`).

**Do:**
- **Closed-loop audit of the access model.** For each `AccessAction` and each `AccessVia` in the codebase, verify: (a) there is a test that exercises the *denied* path, not just the granted path; (b) there is an audit row written on the granted path *and* on the denied path where the denial is a real security decision (vs. a 404-vs-403 split that is a UX decision); (c) the audit row carries *who* (the actor's standing) and *why* (the seam that resolved), per `in-code.md`'s "Observable: we can tell it worked, and the audit trail shows *who* and *why*." Record each gap in the ledger with severity S (a privacy leak or a non-auditable access decision is always S, per `docs/SECURITY.md`).
- **The guardian boundary.** ADR 0028 says the guardian has "no standing to read the child's private content." Grep every `GuardianController` action and every `IUserInfoService` guardian seam for a read of child content (post body, message body, document). Any hit is an S finding. (This is the "delegation scoped narrower than the grant" pin from `in-code.md`, applied to the GU surface.)
- **The translation-swap surface.** ADR 0027 / 0049 / 0051 changed what a *viewer* sees first. Verify the swap never *reveals* a translation the viewer may not read (the variant is a display preference over rows the viewer may already read, by the tag/audience seams - confirm a test pins this for posts, group posts, announcements, and pages).
- **The portability lane.** ADR 0148: "no passwords or tokens ever travel with it." Grep `UserPortabilityService` for any identity-credential field in the export shape; confirm the export's round-trip test (`PortabilityRoundTripTests`) covers the *import into a community where references don't exist* path (the ADR's own acceptance case).
- **No code changes in this unit unless an S finding is found.** An S finding is *recorded in the ledger with the exact file/line and a proposed fix*, and the lane **stops and surfaces it to a human** - it is a security decision, not a refactor, and the philosophy says "the most load-bearing contract in the codebase" is changed only through an ADR. An S finding that needs a code change becomes a *new* named lane (e.g. `SECAUDIT-1`), per this lane's own rule.

**Exit:** the ledger has a complete `## Security & privacy` section (one row per `AccessAction` x `AccessVia`, with test/audit/standing evidence or a finding); the guardian-boundary grep result is recorded; the translation-swap and portability pins are recorded; any S finding is surfaced, not fixed in-lane.

**FACES:** strengthens *A*daptive (a report that a private detail leaked has an audit row to trace, per the philosophy's "the audit log, read, produces a response") and *C*oherent (a moderator can trace any access decision backward to a grant); consumes *E*nergizing (this is the most expensive unit in the lane - it reads the whole authorization surface, not a slice of it).

---

### `U09` - The three-audience UX audit (resident / admin / maintainer) + close

**Entry reads (4):** `docs/philosophy/how-it-works.md` (the resident's entry point - "No technical background needed; the entry point for anyone in the neighborhood who isn't a contributor") / `docs/philosophy/in-product.md` (how we run the platform & community) / `docs/OPS.md` (the maintainer's book) / `src/Kumunita.Web/Views/Home/Index.cshtml` (the first screen a resident sees).

**Do:**
- **Resident (the closed-loop test, `in-code.md`):** for each named resident journey in `how-it-works.md` (post to an audience; report a post; join/leave a group; read a page; translate), trace the *exit* - the moment the resident must leave the platform to complete the task (a "copy into a chat app," a "re-key into a spreadsheet" - the handoff test). Record each exit in the ledger with severity L (a handoff is a value leak, not a security risk). **No fix in-lane** - each exit is a *candidate* for a future named lane, and the philosophy says "every new part states which seam it integrates."
- **Admin (the standing test):** for each admin surface (`/admin/*`), verify the admin can complete the task *within* the surface (no "open the DB, run a query" step, which is a handoff + an accidental-integration risk - the "one admin who knows everything" god-part smell). Record each handoff. Verify the admin's actions are *audited under their standing* (the U08 pin, from the admin side).
- **Maintainer (the decay test):** verify the maintainer's "one page" exists: `README.md` (what + status + running) / `AGENTS.md` (how to work here) / `docs/ARCHITECTURE.md` (the map) / `docs/OPS.md` (the ops) / `docs/SECURITY.md` (the model). This is the *entry-seam* audit - the philosophy's "a new resident can narrate what this place is for in one breath" applied to the team. Record any doc that is the *only* place a fact lives (a single-point-of-failure in the doc layer - the "tribal knowledge" anti-pattern), and any fact that lives in *three+* places (the silent-coupling anti-pattern, like the U03 `AGENTS.md`/`copilot-instructions.md` drift but in the wider doc set).
- **The integrative question (the user's last ask - "for communities, for societies at large"):** this is not a code audit, it is a *part-vs-whole* reading. Record in the ledger's final section: which shipped surfaces create *linkage* (a group post, a shared project, a report that closes a loop) and which create *exit* (a feature that sends the resident to another tool). This is the philosophy's "quality is the quality of the linkage" applied to the shipped surface, and it is the section a *non-technical* community member reads first in the audit ledger.
- **The close flip (the last act of the lane):** run `improve-check.ps1` (the gate, all six checks) and `improve-report.ps1` (the summary). Record the before/after numbers (the four god-files' line counts, the ADR drift, the doc-duplication, the handoff/abstract coverage) in a `## Close` section of the handoff note. Add the `IMPROVE` lane to `WhatsNew.cs` (the sixth member of the close flip, per `AGENTS.md`: "When a milestone or a named lane ships, add its version here - this is a required sixth member of the close flip"). The version is **not** a new capability - the `WhatsNew` entry says "A pass of the platform's own integration audit - the codebase is measurably smaller, the docs measurably more navigable, the seams measurably more tested; no new feature" (the honest statement of a reduction, per this lane's Definition of Done). Flip the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` set together (the close flip contract, `AGENTS.md`).

**Exit:** the ledger's `## Security & privacy`, `## The three audiences`, and `## The integrative question` sections are complete; the gate passes; the close flip is landed (all six members in step, per `AGENTS.md`); `dotnet build` + full test run pass; `MilestonesTests` and `WhatsNewTests` pass with the new entry.

**FACES:** strengthens *C*oherent (the audit ledger is the one document that answers "is this platform well-integrated?" in the philosophy's own vocabulary - the "a moderator can trace any access decision backward to a grant" and "a new resident can narrate what this place is for in one breath" tests, both met) and *E*nergizing (the close flip names what this lane *consumed* - its own attention - and what it *strengthened*, so the next lane knows the baseline); consumes the lane's entire budget (this is the most expensive unit after U08 - it reads the whole product, not a slice).

## The close (what "done" means for this lane)

The lane is **done** when:

1. `improve-audit.md` (the ledger) exists, and a *non-technical* reader can read its `## The integrative question` section and narrate back, in their own words, what the platform does well and where it leaks (the "judge the whole" clause of `in-code.md`).
2. `improve-check.ps1` passes (all six gates green), and the `ImproveHarnessTests.cs` test that runs it is green in the test run.
3. The four god-files are *smaller* than the Evidence baseline (or the ledger records why a file was not split and the gate's ceiling is the *only* thing standing between it and growth).
4. The ADR index, the `AGENTS.md`/`copilot-instructions.md` pair, the handoff TL;DRs, and the design-doc abstracts all meet their gates (U01, U03, U02, U07).
5. The close flip is landed - `Milestones.cs`, `README.md`, `STATUS.md`, `ARCHITECTURE.md`, `MilestonesTests.cs`, and `WhatsNew.cs` are all in step (the sixth-member contract, `AGENTS.md`), and the full test run is green.
6. The handoff note's `## Close` section records the before/after numbers (the reduction, measured - the lane's Definition of Done).

This is the inverse of M28's Definition of Done. M28 ships *a new capability* and proves it works. IMPROVE ships *a reduction* and proves the platform still works - the tests are the proof.