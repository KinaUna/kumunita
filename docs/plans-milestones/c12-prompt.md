# C12 — Tests at the seams (turn the seam-test rule on)

## What you are doing

You are the **fourteenth** agent on the **IMPROVE-CODE** lane (plan:
`docs/plans-milestones/plan-improve-code.md`). C00–C11 are all shipped
(C11 just landed — the six view splits, the `rich-editor` module split, the
composer-picker consolidation, the shared `UploadGateHandler`, the one
`IcsFormat` serializer). **Your unit is C12: make the seam-test rule a gate.**

The seam-test rule (plan §The gate, D6; DoD #7) is currently **report-only**:
`tools/code/code-baseline.json → seam_test.mode` is `"report"`,
`CodeMetrics` only *prints* the coverage numbers (in `--report`), and
`--check` (the CI gate) does **not** enforce it. C12 does three things:
(1) add the integration/unit tests the named seams are missing,
(2) add the **"services with a unit-tested part"** metric (not in the tooling
yet — you build it), and (3) flip the seam-test rule from report to **gate**
in `code-check` so a future untested seam fails the close by name.

**The goal in one sentence:** every public `*Policy` / `*Resolver` /
`*Evaluator` / `*Builder` is named by a unit test, every named controller and
service seam has a test, the "services with a unit-tested part" count is ≥ 10,
and `--check` fails (not just reports) when any of those is missing.

**You are NOT** doing C00–C11 (shipped) or C13 (lane close + cold-read
verification). No new feature, no route or served-HTML change, no
document-schema change, **no new ADR**. You write **tests and one tooling
metric** — you do not refactor the production code under test (add tests *to*
existing seams; if a seam can't be tested as-is, stop and say why, don't
reshape it).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at
  `3f37222b` (C11 just landed).
- The seam-test rule is **report-only**. `seam_test = { "mode": "report" }`
  in `tools/code/code-baseline.json`; `CodeMetrics/Program.cs` `PrintSeamTest`
  prints the counts and `CheckAgainstBaseline` (`--check`) ignores them.
  `tools/code/code-check.ps1` delegates to `--check`;
  `tools/code/code-report.ps1` to `--report` (informational, always exit 0).
- **Verified before-state** (re-run `pwsh tools/code/code-report.ps1` to
  confirm):
  - `*Policy / *Resolver / *Evaluator / *Builder` named by a test: **14 / 14**
    (already fully covered — the gate should hold these, not grow them).
  - `I*Service` named by a test: reported as **2 / 2** — **this is a
    bug in the heuristic** (`PrintSeamTest` filters `Kind == "class"` then
    `name.StartsWith("I")`, but interfaces have `Kind == "interface"`; the
    real `I*Service` surface is ~37). **Fix the heuristic before promoting it
    to a gate**, or you will gate on a number that is wrong.
  - **Controllers NOT named by any `.cs` test file (8):** `AdminAnnouncementCommentsController`,
    `AdminSetupController`, `DocumentFolderController`, `MyDraftsController`,
    `NavLayoutController`, `ProjectsBaseController`, `ReportTargetsController`,
    `ThemeController`. (Note: `*ControllerTests.cs`-style files and
    `coretest.out`/`webtest.out` runner dumps do NOT count — the corpus is the
    `.cs` test sources under `tests/`.)
  - **The plan's named service seams with no test:** `MailAlarmsService`,
    `SuppressionService`, `UsagePurgeService`, `SmtpProbe` (needs a fake SMTP
    server). `PostsController` (composer + feed) and `SchemaBootstrap` already
    have several test files — confirm they cover the *named seam*, add if not.
  - **"services with a unit-tested part":** metric does **not exist** in
    `CodeMetrics` yet. DoD #7 wants it **≥ 10** (from 0). You implement it.
- Test layout: `tests/Kumunita.Core.Tests` (services, mostly Testcontainers
  for the Postgres ones) and `tests/Kumunita.Web.Tests` (controllers; a
  `PostgresFixture.cs` Testcontainers helper exists). Use the existing
  fixtures — do not invent a new DB harness.

## Entry reads

- The plan's **`C12`** section (Do/Exit above) + **DoD #7** + **§The gate**
  (D6, the seam-test rule) in `docs/plans-milestones/plan-improve-code.md`.
- `tools/code/CodeMetrics/Program.cs` → `PrintSeamTest` (the rule, the buggy
  `I*Service` heuristic) and `CheckAgainstBaseline` (where the gate is
  enforced — this is where you add the seam-test failures when
  `seam_test.mode == "gate"`).
- `tools/code/code-check.ps1` (the CI gate) and `code-report.ps1`.
- The untested seams' source: `MailAlarmsService`, `SuppressionService`,
  `UsagePurgeService`, `SmtpProbe`, the 8 un-named controllers,
  `PostsController` composer + feed actions.
- An existing integration test to copy the harness from (e.g.
  `tests/Kumunita.Web.Tests/*ControllerTests.cs` + `PostgresFixture.cs`).
- `docs/philosophy/in-code.md` — the part-vs-whole + comment rules still apply
  to any test-helper type you add.

## Do

**One or two commits**, each green (build + affected tests + both gates):

1. **Add the missing seam tests** (integration where a Postgres/Marten seam is
   exercised, unit where it is pure logic): the 8 un-named controllers above,
   `MailAlarmsService`, `SuppressionService`, `UsagePurgeService`, `SmtpProbe`
   (fake SMTP server — the plan calls this out), and the `PostsController`
   composer + feed seams. Name the type under test in the test file (that is
   what the rule counts).
2. **Unit tests** for any `*Policy` / `*Resolver` / `*Evaluator` / `*Builder`
   still missing one (the report says 14/14 today — keep it 14/14; add any that
   appear).
3. **Implement the "services with a unit-tested part" metric** in
   `CodeMetrics` (report + check): a `*Service` counts when some type it
   exposes or a seam it owns is named by a non-Testcontainers test. Print it in
   `--report`, enforce `>= 10` in `--check`.
4. **Fix the `I*Service` heuristic** (`Kind == "interface"`, not `"class"`) so
   the gate counts the real surface.
5. **Flip the gate:** set `seam_test.mode` to `"gate"` in
   `tools/code/code-baseline.json` and make `CheckAgainstBaseline` fail when an
   untested seam is present (so `code-check.ps1` goes non-zero).

Each commit:
- **Add tests; do not refactor the production seam** to make a test pass.
- **Every new test-helper type passes part-vs-whole** (≥ 1 caller, its own
  test, or owns a seam) and follows the comment rule.
- **Gates stay green:** `code-check.ps1` → `OK (0 violations)` (now *including*
  the seam-test gate), `improve-check.ps1` → `ALL GATES PASS`.

## Do NOT

- Do NOT change **routes, served HTML, document schemas, or production
  behaviour** to satisfy a test — the tests exercise the existing seam.
- Do NOT **rename or re-sign** any C08–C11 committed public surface.
- Do NOT **weaken the seam-test rule** (loosen the `I*Service` filter, exempt
  seams, or drop the "unit-tested part" floor) to make the gate green — a red
  seam names itself; fix the test, not the gate.
- Do NOT raise any baseline `max` / `cc_max` / boundary edge without an `adr:`
  reason (you should not need to).
- Do NOT touch C13 (the lane close, `WhatsNew` flip, archive move) — that is the
  next agent.
- Do NOT add a **new ADR** or **new module**.

## Exit criteria (all must pass before you commit)

1. **Seam-test rule is gated and green:** `seam_test.mode == "gate"` in the
   baseline, `CheckAgainstBaseline` enforces it, and `code-check.ps1` fails if
   you remove any of the new tests (proof it is a real gate).
2. **Every `*Policy` / `*Resolver` / `*Evaluator` / `*Builder`** is named by a
   unit test; the `I*Service` count uses the fixed (interface) heuristic.
3. **The 8 un-named controllers + the 4 named service seams** (`MailAlarms`,
   `Suppression`, `UsagePurge`, `SmtpProbe`) each have a test that names them.
4. **"Services with a unit-tested part" is measured and ≥ 10** (was 0/absent).
5. `dotnet build` → 0 errors; the new test classes run green (xunit.v3
   in-process runner — rebuild the test project first).
6. `pwsh tools/code/code-check.ps1` → `OK (0 violations)` **with the seam-test
   gate on**; `pwsh tools/docs/improve-check.ps1` → `ALL GATES PASS`.

## Mechanics

- **xunit.v3 in-process runner:** `dotnet exec tests/<Proj>/bin/Debug/net10.0/
  <Proj>.dll -class "<Ns>.<Class>"` — **rebuild the test project first**.
- **Testcontainers** for Postgres seams (a `PostgresFixture.cs` exists in
  `Kumunita.Web.Tests`; the Core side has its own) — do not add a second DB
  harness.
- **The gate is `code-check.ps1` (exit 0/1); `code-report.ps1` is informational
  (always 0).** Flip the rule in `--check`, keep `--report` printing it.
- **`multi_replace_string_in_file` fails on repeated identical strings** — use
  `sed -i` for in-file repeats.
- **Boundary edges are a hard ratchet** — check
  `code-baseline.json → boundary_violations` before adding any cross-module
  reference in a test (a test that reaches across a module can add a boundary
  file; prefer testing through the existing seam).
- **The `I*Service` heuristic is currently wrong** (see Starting state) — fix
  it before you trust any number it prints.
- **Commit style:** `test(improve-code): C12 — <short description>` (em dash,
  no ADR in subject; use `test:` since the unit adds tests + a metric).
