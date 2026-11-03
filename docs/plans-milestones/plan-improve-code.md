---
id: plan-improve-code
title: "Plan: IMPROVE-CODE — differentiate the code along real seams"
tier: living
kind: plan
status: proposed
audiences: [developer, agent]
related:
  - docs/archive/lanes/improve/plan-improve.md
  - docs/archive/lanes/improve/handoff-notes.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/philosophy/in-code.md
  - docs/philosophy/anti-patterns.md
lane: improve-code
updated: 2026-11-03
---

> **Abstract:** The code half of the second IMPROVE round. It measures where
> Kumunita's code is under-differentiated (god types, long branchy methods,
> data kept as C# literals) or under-linked (copied patterns, concrete
> cross-module calls, controllers on Marten, parts with no unit tests). It
> then plans splits along seams that already exist in the code.
> It replaces the grandfathering size gates (IMPROVE a/e/f) with a Roslyn
> **ratchet gate on code lines, method size and complexity, type surface,
> and module boundaries**.
> **Who is it for:** whoever picks up a `C`-unit, and reviewers of the splits.
> **When to read the rest:** before starting any `C`-unit, or before raising
> any number in `tools/code/code-baseline.json`.

# Plan: IMPROVE-CODE — differentiate the code along real seams

Measured on `main` @ `88384ff` (2026-11-03). The measurement tool is
[`tools/code/CodeMetrics/`](../../../tools/code/CodeMetrics/Program.cs)
(Roslyn syntax pass, plus a semantic pass over first-party types) and
[`tools/code/ts-metrics.js`](../../../tools/code/ts-metrics.js). The same
tool becomes the gate in `C00`. Everything below can be reproduced with:

```
dotnet run --project tools/code/CodeMetrics -- . .tmp/metrics
NODE_PATH=<typescript@5 node_modules> node tools/code/ts-metrics.js src/Kumunita.Web/client > .tmp/metrics/ts.csv
```

## Why now

The first IMPROVE lane (Oct 2026, U00–U09) set ceilings and
*grandfathered* the files already over them. In the 26 days since, gate (a)
was **re-baselined about 30 times** (its own comment block records each
one). Every lane close that grew a god-file raised that file's baseline,
because the alternative was a red gate. That means the gate allowed the
growth it was meant to stop:

| File | U00 baseline (Oct 8) | Now | Δ |
|---|---:|---:|---:|
| `Localization/KnownTranslationKeys.cs` | 8 948 | 15 157 | +69 % |
| `Bootstrap/FirstBootSeeder.cs` | 4 471 | 6 880 | +54 % |
| `Controllers/ProjectsController.cs` | 4 634 | 5 374 | +16 % |
| `Projects/ProjectService.cs` | 4 301 | 5 016 | +17 % |
| `UserInfo/UserInfoService.cs` | 3 693 | 4 287 | +16 % |
| `Views/Projects/TodoDetail.cshtml` | 890 | 1 154 | +30 % |
| `Views/Projects/BoardDetail.cshtml` | 1 431 → 838 (U06) | 1 090 | regrew; its baseline still says 1 431 |

That is the anti-pattern **Integration decays** (principle 6), made worse
by a gate that ratchets *up*. This lane makes the ratchet go one way only.

## What this lane is (and is not)

- **Is:** splitting code that already does several jobs into the parts
  those jobs imply, linking the duplicate copies through one named seam,
  moving data out of code, and putting tests where parts meet.
- **Is not:** a re-architecture (the modular monolith and the three access
  modules of ADR 0006 stay), a feature, a route or rendered-HTML change, a
  document-schema change, or a comment sweep (see D4).
- **Rule for every split (the part-vs-whole test):** a new type must
  satisfy at least one of: (a) it has two or more callers, (b) it is a
  decision that gets its own unit test, (c) it owns a route family or an
  aggregate. A type that meets none of these is ceremony and is folded back
  (the U04 per-surface views are the example: 12 properties, no consumers).

## Decisions

| # | Decision | Chosen | Alternatives rejected |
|---|---|---|---|
| D1 | What the gate measures | **Code lines** (lines holding a non-trivia token) per file, **code lines + cyclomatic complexity per method**, **public methods per type / interface**, **cross-module edges**. Ratchet: baseline = today, **shrink-only**, an entry **retires itself** once under the ceiling, and raising a number fails CI unless the entry carries an `adr:` reason. | Physical lines with no raising (still counts comments); hard ceilings with no baselines |
| D2 | Ceiling levels ("moderate", near p95) | C# file **600** code lines · method **80** code lines / **CC 15** · class **25** public methods · interface **20** methods · Razor view **600** lines · TS file **600** code lines, named function **80** lines / CC 15 | Strict (400/60/12/20/15); loose (800/100/20/30/25) |
| D3 | God interfaces | **Split, with an ADR amending 0006.** `IUserInfoService` goes back to the access core; sibling interfaces in the UserInfo module take the later clusters. The same treatment applies to `IProjectService`. | Composed facade (keeps an 86-method injectable surface); implementation-only split |
| D4 | Narration in comments | **Contract + link rule, applied on touch.** A doc-comment states what, when and throws, plus the ADR number. History stays in the ADR or handoff note. Every `C`-unit applies it to the code it moves. The comment ratio is *reported*, not gated. | A thinning sweep (huge diffs, link loss); leaving comments alone |
| D5 | Data kept as C# literals | **Embedded resources.** Translation keys go to per-language, per-surface JSON; guide pages to embedded Markdown; the sample corpus to JSON. A loader keeps the `EnValues` / `AllKeys` API, and a parity test pins it. The U04 views are deleted. | C# partials per surface; a data-file exemption |
| D6 | Module-boundary enforcement | **An analyzer rule in the gate.** A module reaches another only through an interface, record, enum, or a named `*Policy` / `*Keys` static. No concrete `*Service` crosses a module. Controllers stay off `IDocumentStore` / `IDocumentSession` / `IQuerySession`, except a named allowlist. Ratcheted like D1. No project split. | `.Contracts` projects per module; pilot both |

## Evidence

Severity follows the first IMPROVE lane: **S** = trust at risk, **M** =
maintainability at risk, **L** = discoverability at risk.

| # | What | Where / numbers | Principle · test · anti-pattern | Sev | Unit |
|---|---|---|---|---|---|
| E1 | The size gates allow growth | gate (a) re-baselined ≈30× in 26 days (table above); gate (f) `BoardDetail` baseline 1 431 vs actual 1 090 (stale, allows +341) | P6 · Stable consumed · *Integration decays* | M | C00 |
| E2 | Physical-line ceilings count narration | src: **72 550 code / 59 675 comment / 10 769 blank** lines; 368 / 516 files have more comment than code lines; `IUserInfoService` 87 % comment, `Program.cs` 61 % | P2 · part-vs-whole | M | C00, D4 |
| E3 | Files over 600 code lines | **22** files, led by `KnownTranslationKeys` 12 963, `FirstBootSeeder` 5 688, `ProjectsController` 3 185, `UserInfoService` 2 702, `ProjectService` 2 493, `GroupsController` 1 647, `SampleDataSeeder` 1 632 | P2 · *The god part* | M | C02, C07–C10 |
| E4 | God interfaces / classes | `IUserInfoService` **86 methods** (ADR 0006 froze 8), which cluster as Groups 25 / Community+Components 20 / Guardian+Delegation 16 / Profile+prefs 14 · `UserInfoService` 86 public methods · `IProjectService` 58 (boards 18 / todos 13 / translations 9 / goals 5) · `ProjectsController` 98 methods in route families boards 24 / todos 22 / projects 9 / goals 6 · 9 classes over 25 public methods, 6 interfaces over 20 | P2 · handoff · *The god part* | M | C01, C07, C08 |
| E5 | Long or branchy methods | **99** methods over 80 code lines or CC 15 (56 by length, 83 by CC). Worst: `SampleDataSeeder.BackfillSampleCorpusAsync` 893 lines / CC 138 · `DependencyInjection.AddKumunitaCore` 641 · `UserPortabilityService.ResolveAsync` 423 / CC 65 · `ProjectsController.BoardDetail` 313 / 34 · `EventController.Calendar` 303 / 32 · `IdentityService.DeleteCoreAsync` 288 / 22 · `PersonalNoteService.ResolveTargetAsync` 287 / 61 · `PortabilityValidate.Run` 227 / 53 | P3 · handoff | M | C05, C08–C10 |
| E6 | The translation lane is copied per surface | **133 `*Translation*` methods, 5 290 lines, 25 files** (`ProjectsController` 12, `ProjectService` 18, `GroupsController` 9, `PostService` 10, `UserInfoService` 10, …) | P1 · *Silent coupling* / *Distributed fragmentation* | M | C06 |
| E7 | Exact-token clones | **10 291 lines in 337 regions of ≥10 lines** (identifiers kept, literals normalized, 100-token windows). Real seams among them: Bookmark ↔ PersonalNote target resolvers (2 × the 21-kind switch, 495 lines) and their controllers · upload handling in `ContentImage` / `Attachment` / `Document` controllers · `IcsWriter` ↔ `TodoIcsWriter` · `ProjectBoardViewModels` ↔ `ProjectTodoViewModels` · the translation actions across Event / Announcement / Posts / Page controllers | P1 · *Silent coupling* | M | C05, C06, C11 |
| E8 | U04 and U05 split without linking | U04's 12 per-surface key views: **0 consumers outside tests**. U05 left 9 two-line forwarding stubs in 4 controllers; `ProfileController` and `GroupsController` still hold private copies of the picker trio | P2 · part-vs-whole · *Part sprawl* | L | C02, C11 |
| E9 | Concrete cross-module calls | Core = one assembly, 30 feature folders, **205 folder→folder edges**, no `.Contracts` project. **`NotificationService` has no interface; 9 modules inject the concrete class** (Posts 26 refs, Announcements 26, Moderation 14, UserInfo 13, GroupMessaging 12, …). Static policies on concrete services (`EventService.CheckEditStanding`, `PostService.CanAddTranslation`) are called from Web | P1 · handoff · *Accidental integration* | M | C03, C04 |
| E10 | Controllers doing domain work | **21 / 67** controllers query or write Marten directly: `ModerationController` 22 calls, `NotesController` 12 × `SaveChangesAsync`, `ProjectsController` 6, `Bookmarks` 6, `Page` / `Document` / `EscalationConfig` 5 each, … | P3 · ADR 0006 §D · *Distributed fragmentation* | M | C09 |
| E11 | **Access decisions outside `IAuthorizationService`** | `ModerationController.IsInActorScopeAsync` reads `ModeratorAssignment` itself; `ProjectService` claim-to-do computes group / community standing via `GetGroupIdsAsync` / `GetCommunityIdsAsync` | ADR 0006 §D "the authorization path is unique" | **S** | **→ SEC handoff** (this lane only moves the code, behaviour unchanged) |
| E12 | Parts have no unit tests | 2 504 tests; **200 / 329 test files are Testcontainers**. Of 43 `*Service` classes, **0 have a unit test**, 16 are integration-only, 3 have none (`MailAlarms`, `Suppression`, `UsagePurge`). 9 controllers have no xunit test naming them, including `PostsController` (2 030) and `ModerationController` (642). No test names `PortabilityApplyDocuments` (678), `PortabilityValidate` (301), `SmtpProbe` (295) or `SchemaBootstrap` (191) | P3 · closed-loop · *Parts work, seams don't* (inverted: the parts are untestable alone) | M | C04, C05, C12 |
| E13 | Razor / TS | 9 views over 600 lines (`TodoDetail` 1 154, `BoardDetail` 1 090, `_Layout` 830, `Groups/Detail` 813, `Groups/PostDetail` 764, `Guardian/Detail` 747, `Event/Detail` 724, `Posts/Detail` 662, `_BoardCard` 634) · `rich-editor.ts` 1 634 lines, `bindRichEditor` an **828-line closure** · 9 named TS functions over 80 lines | P2 | L | C11 |

**The pattern behind E4, E6 and E12.** Decisions (edit standing,
translation rules, target-kind resolution, picker contents) live *inside*
Marten-bound services. That is why the services are big, why the same
decision is copied per surface, and why every test needs Postgres. Pulling
the decisions out as pure parts is the seam that differentiates the code
and makes it unit-testable in the same move.

## Prior work, compared

| Unit | What it did | What this lane does with it |
|---|---|---|
| Gate (a) `.cs` > 2 000 | grandfathered 10 files, re-baselined ≈30× | **Retired** (C00), replaced by the D1/D2 ratchet on code lines |
| Gate (e) `.ts` > 800 | grandfathered `rich-editor.ts` 1 634 (unchanged since) | Retired → TS gate (600 / function 80); `rich-editor.ts` split in C11 |
| Gate (f) `.cshtml` > 800 | grandfathered 2, then 5 views; stale `BoardDetail` entry | Retired → view gate 600, shrink-only |
| U04 key groups | 12 derived views, no consumers | Views deleted; data moves to resources (C02) |
| U05 composer trio | one helper, 9 forwarding stubs remain | Stubs removed, Profile/Groups copies migrated (C11) |
| U06 partials | `_BoardCard`, `_PostReply`; criterion "> 150 lines and repeated or independently touched" | Criterion kept as the view rule; `BoardDetail` / `TodoDetail` re-examined (C11) |

## The gate (what C00 builds)

- **Tool:** `tools/code/CodeMetrics` (in this patch, as measurement).
  `C00` adds `--check <baseline.json>` and `--tighten` modes, plus
  `tools/code/code-check.ps1` / `code-report.ps1` wrappers.
- **Baseline file:** `tools/code/code-baseline.json`, keyed by stable ids:
  `file`, `file::Type`, `file::Type.Member/arity`, and `from→to:symbol` for
  boundary edges. Each entry holds `max` and an optional `adr`.
- **Check fails when:** (1) a new item is over its ceiling; (2) a
  baselined item exceeds its `max`; (3) **a stale entry exists**, meaning
  the item is gone or under the ceiling (so retirement cannot be
  forgotten; `--tighten` removes it); (4) any `max` is higher than on
  `origin/main` without an `adr:` field (CI diffs the JSON).
- **A moved method gets a new id.** Its old entry goes stale and must be
  removed, and the new id is a new item that must land *under* the
  ceiling. Splits therefore cannot carry their old exemption along.
- **Boundary rule (D6):** a semantic edge from module A to a type defined
  in module B (B ≠ A, both under `src/Kumunita.Core/*`, or Web → Core) is
  allowed when the target is an interface, record, enum, or a static whose
  name ends `Policy`, `Keys` or `Kinds`. Anything else is a violation
  (ratcheted). Controllers referencing `IDocumentStore` /
  `IDocumentSession` / `IQuerySession` are violations unless listed in
  `controllers-marten-allowlist` with a one-line reason.
- **Seam-test rule (report in C00, gated in C12):** every public `*Policy`,
  `*Resolver`, `*Evaluator` or `*Builder` type is named by at least one
  unit (non-Testcontainers) test file. Every public `I*Service` is named
  by at least one integration test file.
- **Report-only:** comment ratio per file (D4), clone lines, Marten-touching
  controllers count, services with zero unit-tested parts.

## Units

Each unit is sized for one agent session and ends green (build, unit,
integration, `code-check`). It retires the baseline entries it fixes in
the same commit. Every unit applies **D4** to the code it touches and the
**part-vs-whole rule** to every type it creates. Routes, rendered HTML and
document shapes do not change unless a unit says so. **No unit re-opens an
ADR except through a new amending ADR.**

---

### `C00` — Ratchet gate + baseline (retire IMPROVE a/e/f)

**Entry reads:** `tools/docs/improve-check.ps1` · `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs` · `.github/workflows/ci.yml` · this plan's *The gate* section.

**Do:**
- Switch `tools/code/CodeMetrics` to the `Microsoft.CodeAnalysis.CSharp`
  package (already the default when `RoslynDir` is unset). Add the
  `--check` / `--tighten` modes, the boundary rule and the
  controller-Marten rule.
- Add `tools/code/code-check.ps1` (exit code) and `code-report.ps1`
  (top-N red/amber/green, comment ratio, clones, seam-test counts).
- Generate `tools/code/code-baseline.json` from `main`. The expected
  starting list: 22 files · 99 methods · 9 classes · 6 interfaces · 9
  views · 1 TS file + 9 TS functions · boundary edges · ~21 Marten
  controllers.
- Remove gates (a), (e) and (f) from `improve-check.ps1`. Keep (b), (c),
  (d) and (g), which belong with the docs gates. Point
  `ImproveHarnessTests` at `code-check.ps1` as well.
- CI: add a `code` job (`dotnet run` the tool, `pwsh code-check.ps1`). It
  is **blocking from day one**: the baseline equals today, so nothing
  fails until something grows.
- `AGENTS.md` + `docs/philosophy/in-code.md`: add the **ratchet rule**
  ("a baseline number only goes down; raising it needs an `adr:` reason"),
  the **part-vs-whole rule** for new types, and the **D4 comment rule**,
  each stated once in one place and linked from the other.

**Exit:** gate green on `main`; a deliberate +1 line on a baselined method
fails it; `--tighten` after a deliberate −1 rewrites the entry.

**FACES:** strengthens *Stable* (the ceiling can no longer drift up) and
*Adaptive* (the gate now sees methods, not just files); consumes
*Flexible* (one more tool and job to keep running).

---

### `C01` — ADRs first: the contracts the splits will need

**Entry reads:** ADR 0006 · the M5 projects ADRs cited in
`IProjectService.cs` · ADR 0015 / 0018 (localization, translations) ·
`docs/adr/TEMPLATE.md`.

**Do:** write the decisions before the code, each ADR ≤ 80 lines:
- **ADR A (amends 0006):** `IUserInfoService` returns to the access core
  (profile read, `GetGroupIdsAsync`, `GetActiveGrantAsync`, membership
  writes, community ids). New sibling interfaces in the UserInfo module:
  `IGroupService`, `ICommunityMembershipService`, `IGuardianService`,
  `IProfileSettingsService`. The ADR names the method → interface map and
  the migration story for callers (audit rows unchanged, since `Via`
  values do not move).
- **ADR B:** the Projects module splits into `IProjectService` (projects
  + goals), `IBoardService` (boards, lanes, placements) and
  `ITodoService` (todos, comments, claim/assign). Web gets
  `BoardsController`, `TodosController` and `ProjectsController`, with
  routes unchanged. The ADR names which ADR-cited "frozen seams" move
  where, so the kiosk and other citations keep resolving.
- **ADR C:** one shared **translation-variant lane** (the Core contract
  plus the Web action helper) that every translatable surface uses.
- **ADR D:** translation keys, guide pages and the sample corpus are
  **embedded resources**, not C#.
- `// TODO ❓` ADR numbers: take the next free ones at write time
  (0286–0289 at `88384ff`).

**Exit:** four ADRs accepted; the ADR index gate (b) green.

**FACES:** strengthens *Coherent* (each split has a named contract before
a line moves); consumes *Flexible* (four more permanent records).

---

### `C02` — Data out of code (D5)

**Entry reads:** `KnownTranslationKeys.cs` (structure only: the four
`*Values` literals and the 12 U04 views) · `LocalizationService.cs` ·
`FirstBootSeeder.cs` (the `*GuidePages` members) · `SampleDataSeeder.cs`
· ADR D from C01.

**Do:**
- Generate `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/<surface>.json`
  from the current dictionaries with a one-off script (kept under
  `tools/code/`). Surfaces are the key prefixes. Mark them as embedded
  resources.
- `KnownTranslationKeys` becomes a loader of ~60 lines. `EnValues`,
  `DeValues`, `FrValues`, `DaValues` and `AllKeys` keep their exact
  surface. **Delete the 12 U04 views** and their test.
- Parity tests (unit): every `en` key exists in de/fr/da, or is listed as
  intentionally missing; no duplicate keys across surface files;
  byte-identical values against a snapshot taken before the move (one
  commit, then the snapshot is deleted).
- Guide pages: `Bootstrap/GuidePages/{lang}/<slug>.md` embedded, read by
  slug. `FirstBootSeeder` keeps the seeding logic only.
- Sample corpus: `Bootstrap/SampleCorpus/*.json` plus a table-driven
  applier. `BackfillSampleCorpusAsync` (893 / CC 138) and `SeedAsync`
  (732 / CC 97) become loops over records.

**Exit:** `KnownTranslationKeys.cs`, `FirstBootSeeder.cs` and
`SampleDataSeeder.cs` are off the file list (−≈20 000 code lines); the
seeded DB is identical (compare a fresh seed's document export, before vs
after); all localization tests pass unmodified apart from the deleted
U04 test.

**FACES:** strengthens *Adaptive* (translators edit JSON/Markdown, not C#)
and *Coherent*; consumes *Stable* (resources now fail at run time rather
than at compile time, which the parity tests compensate for).

---

### `C03` — A contract for Notifications

**Entry reads:** `Notifications/NotificationService.cs` (the public
surface) · the 9 caller modules' constructor and DI lines ·
`DependencyInjection.cs` (the notification registrations).

**Do:** extract `INotificationEmitter` (`EmitAsync` and whatever else
callers actually use; find them with the xmodule report). Move the static
`DeferredKey` into `NotificationKeys`. Switch all 9 modules and the Web
side-effects to the interface. The concrete class becomes
module-internal in intent (`internal` if DI allows).

**Exit:** zero boundary edges to `NotificationService`; the
notification integration tests pass unmodified; a unit test with a fake
emitter proves one caller (posts) emits the expected kind.

**FACES:** strengthens *Flexible* (callers depend on one contract) and
*Stable*; consumes *Coherent* slightly (one more interface to find).

---

### `C04` — Decisions out of services: `*Policy` parts

**Entry reads:** the boundary report's "static on concrete service"
edges · `EventService` (`CheckCreateStanding`, `CheckEditStanding`,
`CanAddTranslation`) · `PostService.CanAddTranslation` · the `ProjectService`
edit-standing helpers it cites as "the EventService shape".

**Do:** move each pure decision into a static `*Policy` class next to its
service (`EventEditPolicy`, `PostTranslationPolicy`, `TodoEditPolicy`, …).
Each takes plain inputs and returns a decision, with no session. Callers
in Core and Web call the policy. **Unit tests per policy**, written as a
truth table from the existing integration tests' cases. **Do not move the
claim-to-do standing logic** (E11): leave a `// TODO ❓ SEC` pointer to the
SEC lane's decision on routing it through `IAuthorizationService`.

**Exit:** zero Web → concrete-service static calls; ≥ 6 policies, each
with a unit test file; integration tests unmodified.

**FACES:** strengthens *Adaptive* (a rule change is one unit-tested
function) and *Coherent*; consumes *Flexible* (more small files).

---

### `C05` — One content-target resolver

**Entry reads:** `BookmarkService.ResolveTargetAsync` (208 / CC 36) ·
`PersonalNoteService.ResolveTargetAsync` (287 / CC 61) ·
`BookmarksController` and `NotesController` (the cloned blocks in E7) ·
ADR 0167, 0118.

**Do:** one `ContentTargetResolver` (in `Core/Query/` or a new
`Core/Targets/`; `// TODO ❓` pick the folder at the unit). It maps
`(TargetKind, id)` to a display row through the owning surfaces'
**interfaces**, never through their documents. It is split into a pure
kind table (unit-tested: every one of the 21 kinds has a route and a
loader) plus the loader calls (integration-tested at the seam, one test
per kind family). Both services and both controllers use it.

**Exit:** both `ResolveTargetAsync` methods are gone from the method list;
−≈450 clone lines; bookmark and note tests pass unmodified.

**FACES:** strengthens *Coherent* (a new target kind lands in one table)
and *Stable*; consumes *Flexible* (two surfaces now share a seam they
must both satisfy).

---

### `C06` — One translation-variant lane

**Entry reads:** ADR C from C01 · `PostService` (its 10 translation
methods) and `PostsController` (its 7) as the reference surface · the
`*Translation` document types · the clone report rows for translation
actions.

**Do:**
- Core: a generic `TranslationLane<TDoc, TVariant>` (add / update /
  remove / list, the `CanAddTranslation` policy from C04, the audit row,
  the language validation), parameterized by a small per-surface
  descriptor.
- Web: a `TranslationActions` helper (or a base for the action bodies) and
  one shared modal partial, if the views prove to be clones too.
- Migrate **one surface per commit**: posts → events → announcements →
  pages → projects / boards / todos → groups → surface labels → the rest.
- Unit tests for the descriptor mapping; one integration test per
  surface at the seam (add → render variant → remove).

**Exit:** `*Translation*` methods fall from 133 / 5 290 lines to
≤ 40 / ≤ 1 500 (the lane plus per-surface descriptors); translation tests
pass unmodified.

**FACES:** strengthens *Coherent* and *Energizing* (a new translatable
surface becomes a descriptor, not a copied day of work); consumes
*Flexible* (eight surfaces share one lane, and a surface-specific quirk
must become an explicit descriptor option).

---

### `C07` — Split the UserInfo module (ADR A)

**Entry reads:** ADR A · `IUserInfoService.cs` (signatures only, not the
comments) · `UserInfoService.cs` (one cluster at a time) · the DI
registration.

**Do:** create `GroupService`, `CommunityMembershipService`,
`GuardianService` and `ProfileSettingsService`, one interface each, with
the implementation moved verbatim cluster by cluster (one commit per
cluster). Any shared private helpers go to an internal `UserInfoStore`.
Callers inject only what they use. `IUserInfoService` keeps the access
core. Existing integration tests move with their cluster. Each new
interface gets a contract test file naming it (the seam-test rule).

**Exit:** `IUserInfoService` ≤ 20 methods; no UserInfo type over the
class / interface ceilings; `UserInfoService.cs` off the file list.

**FACES:** strengthens *Coherent* (the guardian lane has its own door)
and *Stable* (the access core is small again); consumes *Flexible* (four
more registrations).

---

### `C08` — Split the Projects module (ADR B)

**Entry reads:** ADR B · `IProjectService.cs` signatures ·
`ProjectsController` route attributes (the family map: boards 24, todos
22, projects 9, goals 6) · `ProjectService` one aggregate at a time.

**Do:**
- Core: `BoardService`, `TodoService` and `ProjectService` (projects +
  goals), each behind its interface. Shared standing checks go through the
  C04 policies.
- Web: `BoardsController`, `TodosController` and `ProjectsController`.
  **Route attributes move verbatim**; prove it by diffing the route table
  (a test enumerating `EndpointDataSource`) before and after.
- Extract **view-model builders** for the two long actions:
  `BoardDetailViewModelBuilder` (`BoardDetail` 313 / 34) and
  `TodoDetailViewModelBuilder` (`TodoDetail` 272 / 27). The builders are
  integration-tested; the actions shrink to under 40 lines.
- The C11 picker-stub cleanup for these controllers happens here.

**Exit:** none of the five Projects files is over the file ceiling; the
route-table diff is empty; served HTML for board and to-do detail is
identical (the U06 Razor doctrine).

**FACES:** strengthens *Coherent* (one controller per route family) and
*Adaptive*; consumes *Flexible* (three DI registrations, three test
classes).

---

### `C09` — Controllers off Marten

**Entry reads:** the controller-Marten report · `ModerationController`
· `NotesController` · the owning services' interfaces.

**Do:** move each controller's queries and writes behind the owning
module's interface (`IModerationService` read seams; `IPersonalNoteService`
write lanes take over the 12 `SaveChangesAsync` calls; bookmarks; pages;
documents; escalation config; …). Extract `EventCalendarViewModelBuilder`
for `EventController.Calendar` (303 / 32). For
`ModerationController.IsInActorScopeAsync`, **move it verbatim into
`IModerationService`** behind a `// TODO ❓ SEC` (E11). The decision on
routing it through `IAuthorizationService` belongs to the SEC lane.
Whatever stays on Marten goes to the allowlist with a reason (target
≤ 5, e.g. pure diagnostics).

**Exit:** Marten-touching controllers from 21 to ≤ 5 allowlisted;
`ModerationController` and `NotesController` each get their first xunit
test.

**FACES:** strengthens *Stable* (one write path per aggregate) and the
access seam's *Coherent*; consumes *Flexible* (more service methods,
added under the D2 interface ceiling).

---

### `C10` — Long Core methods along real seams

**Entry reads:** the method list (Core rows) · `DependencyInjection.cs`
· `IdentityService.DeleteCoreAsync` · `UserPortabilityService` ·
`PortabilityValidate` · `NotificationService.EmitAsync`.

**Do:**
- `AddKumunitaCore` (641 lines) → one `Add<Module>()` extension **in each
  module folder** (module self-registration: the module owns its wiring,
  and `AddKumunitaCore` composes them).
- `DeleteCoreAsync` (288) → an `IUserDataEraser` per module, run in a
  fixed order. Each module owns how its data is erased. An integration
  test proves full erasure across modules.
- Portability `ResolveAsync` / `ClassifyAsync` / `ExportAsync` /
  `Validate.Run` → a per-entity-kind strategy table. Unit tests per
  strategy; the first tests ever for `PortabilityApplyDocuments` and
  `PortabilityValidate`.
- `EmitAsync`, `PageService.CreateAsync`, `ProjectService.UpdateTodoAsync`
  (after C08: `TodoService`): extract the named steps they already
  comment as phases.

**Exit:** no Core method over 80 code lines or CC 15 except baselined
entries that carry a reason; the GDPR erasure test passes.

**FACES:** strengthens *Adaptive* (a new module brings its own wiring
and erasure) and *Stable*; consumes *Coherent* (wiring is no longer
readable in one file; the composition method lists the modules instead).

---

### `C11` — Web: views, TypeScript and the small duplicates

**Entry reads:** the view and TS lists · `rich-editor.ts` (structure) ·
`ComposerSeedOptions.cs` · the upload clones · `IcsWriter` /
`TodoIcsWriter`.

**Do:**
- Views: apply the U06 criterion (> 150 lines **and** repeated or
  independently touched) to `TodoDetail`, `BoardDetail`, `_Layout`,
  `Groups/Detail`, `Groups/PostDetail` and `Guardian/Detail`. Record the
  blocks that were *not* extracted, and why. Byte-identical served HTML.
- `rich-editor.ts`: split `bindRichEditor` into ES modules (toolbar,
  commands, paste / markdown bridge, selection state). Plain `tsc`, no
  bundler (ADR 0031).
- Composer: delete the 9 forwarding stubs and call `ComposerSeedOptions`
  directly; migrate the Profile / Groups copies.
- One `UploadHandler` for the ContentImage / Attachment / Document
  controllers. One calendar serializer shared by `IcsWriter` and
  `TodoIcsWriter`.

**Exit:** view and TS lists at ≤ 3 entries each; the picker trio is
defined once with no stubs; −≈500 clone lines.

**FACES:** strengthens *Coherent*; consumes *Flexible* (more partials and
modules to keep in step with their parents).

---

### `C12` — Tests at the seams (turn the seam-test rule on)

**Entry reads:** the seam-test report · the controllers and services with
no test.

**Do:** integration tests at each untested seam: `PostsController`
(composer and feed), `ReportTargetsController`, `DocumentFolderController`,
`MyDraftsController`, `AdminSetupController`, `MailAlarmsService`,
`SuppressionService`, `UsagePurgeService`, `SchemaBootstrap`, `SmtpProbe`
(with a fake server). Unit tests for any `*Policy` / `*Resolver` /
`*Builder` still missing one. Then switch the seam-test rule in
`code-check` from report to **gate**.

**Exit:** the seam-test rule is gated and green; the "services with a
unit-tested part" count goes from 0 to ≥ 10.

**FACES:** strengthens *Stable*, and the closed loop for maintainers
(a failing seam names itself); consumes test run time (the integration
suite grows, partly offset by the new unit tests).

---

### `C13` — Close + independent verification

**Do:**
- Run `code-report.ps1` and record before → after in a `## Close` section
  of the handoff note.
- **Cold-read verification:** an agent that has not seen this lane gets
  the plan's Definition of Done and the repo. It re-runs the tool, checks
  every DoD line, and picks three new types at random to judge against the
  part-vs-whole rule (callers? own test? own route family or aggregate?).
  Types that fail are folded back before the close.
- Close flip per `AGENTS.md` (the `WhatsNew` entry: "a measured reduction,
  no new feature").
- Move the lane to `docs/archive/lanes/improve-code/`.

## Definition of Done

1. `code-check` is blocking in CI, and **no baseline `max` went up during
   the lane** (`git log -p tools/code/code-baseline.json` shows only
   decreases and removals).
2. Gates (a), (e) and (f) are gone from `improve-check.ps1`; its
   re-baseline comment block is deleted with them.
3. Baseline list sizes fall to at most: files **8** (from 22) · methods
   **45** (from 99) · classes **3** (from 9) · interfaces **2** (from 6) ·
   views **3** (from 9) · TS **2** (from 10).
4. `KnownTranslationKeys`, `FirstBootSeeder`, `SampleDataSeeder`,
   `ProjectsController`, `ProjectService`, `UserInfoService` and
   `IUserInfoService` are **off** the lists entirely.
5. Exact-token clones ≥ 10 lines fall from 10 291 to **≤ 5 000**;
   `*Translation*` methods from 133 to **≤ 40**.
6. Concrete-service boundary edges: **0**. Marten-touching controllers:
   **≤ 5**, each allowlisted with a reason.
7. The seam-test rule is gated; services with a unit-tested part **≥ 10**;
   every controller is named by at least one xunit test.
8. The E11 items are handed to IMPROVE-SEC with file / line references,
   unchanged in behaviour.
9. Ratchet, part-vs-whole and comment rules live in `AGENTS.md` /
   `in-code.md`, one place each.
10. Cold-read verification (C13) passes.

## Handoffs to other lanes

- **IMPROVE-SEC:** E11 (`ModerationController.IsInActorScopeAsync`;
  `ProjectService` claim-to-do standing). `ModerationController` has no
  xunit test until C09.
- **IMPROVE-DOCS / DEVX:** D4 explains the comment ratio, but the
  narration itself (≈ 60k lines) is a docs-integration question.
  `docs/ARCHITECTURE.md` holds single lines of several thousand
  characters. The project instructions still mention `.Contracts`
  projects that don't exist (D6 decided against them).