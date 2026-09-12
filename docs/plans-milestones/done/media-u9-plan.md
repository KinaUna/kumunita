# U9 — Web seam tests: the upload guard (U7a/b/c) + the FACES M1–M6 serving gate

> Self-contained: this file + the **entry reads** below is the whole context.
> The **exact** test names are `docs/design/media-file-storage-design.md` §2.5;
> the **gate shape** is §2.6. A prior handoff section (if any) is **U8**.

## Understanding

Lock the **Web-side** behavior the design doc §2.5 pins: the **upload guard**
(self-only / size / type — U7a/b/c) + the **serving gate** (FACES M1–M6, owner
branch + audit + blocked + unknown + the `nosniff` header). This is the
"the gate is the product" unit for the Web surface — it proves C-MED·1/2/5/8
hold **before** the feature is closed (U10).

## Assumptions

- The **exact** test names are **fixed** by the design doc §2.5 (the
  `AvatarUpload_*` U7a/b/c + the `Serving_M1..M6` FACES rows). A unit
  renaming a test name drifts the seam contract (C-MED·1/5/8).
- The Web controller tests mirror the **existing** `Kumunita.Web.Tests`
  service/controller harness (the `DirectoryService` test shape — the
  audited-serve seam the `Serving_M*` rows model).

## Approach

Write the upload-guard tests (self-only / oversize / wrong-type — the
§2.5 U7a/b/c names) + the serving-gate tests (FACES M1–M6, owner branch +
audit + blocked + unknown + the `nosniff` header). Use the existing
`Kumunita.Web.Tests` harness; the `Serving_M*` rows model the
`CanAsync` FACES decision (the same shape `DirectoryService.PreviewAsAsync`
models).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.5 (the **exact** Web test
  names: `AvatarUpload_Owner_Roundtrip`, `AvatarUpload_NonOwner_Rejected`,
  `AvatarUpload_Oversize_Rejected`, `AvatarUpload_WrongType_Rejected` +
  `Serving_M1..M6`) + §2.6 (the gate shape).
- `docs/plans-milestones/done/media-u6-plan.md` + `media-u7-plan.md`
  + `media-u8-plan.md` + the **U6** + **U7** + **U8** handoff (the actions +
  views + form to exercise).
- the existing `tests/Kumunita.Web.Tests/` controller-test harness
  (the `DirectoryService` test shape to mirror for the audited-serve seam).

## Deliverables (1 test file, new or append)

- `tests/Kumunita.Web.Tests/ProfileAvatarTests.cs` (or fold into an existing
  `Kumunita.Web.Tests` profile/controller test file — the unit chooses,
  notes it in the handoff).

## Risks & open questions

- **The §2.5 Web test names** (fixed by the design doc — match verbatim):
  `AvatarUpload_Owner_Roundtrip` (U7a), `AvatarUpload_NonOwner_Rejected`
  (U7b), `AvatarUpload_Oversize_Rejected` (U7c + the size guard),
  `AvatarUpload_WrongType_Rejected` (the C-MED·5 type guard), and the
  `Serving_M1..M6` FACES rows (the C-MED·1/2/5 gate + the nosniff header).
- **The `Serving_M*` rows:** the **owner branch** (`Serving_M1`) is the
  `subject` == the current user (the `Read` `AccessVia`) — the action gates
  **self-only** (C-MED·8, the **serve** side of the C-MED·8 lane). A
  `subject`-param drift is a **fail-closed** design-doc violation.
- **The `nosniff` header** (`Serving_M*` rows assert `X-Content-Type-Options:
  nosniff`) is C-MED·5, §2.7 rule 5 — a **fail-closed** design-doc violation
  if missing.
- **The FACES rows** (M1–M6) model the `CanAsync` decision
  (`Allowed` → stream; `Denied` → 404) — the **exact** FACES row mapping is
  in the design doc §2.5 (the U7 handoff pins it).

## Steps

1. Write the upload-guard tests (self-only / oversize / wrong-type — the
   §2.5 U7a/b/c names).
2. Write the serving-gate tests (FACES M1–M6, the C-MED·1/2/5 gate + the
   `nosniff` header).
3. `run_build` → green; the §2.5 Web test names discover + pass via the
   §2.6 `dotnet exec Kumunita.Web.Tests.dll` path (AGENTS.md — **not**
   `dotnet test`).
4. Append **`## U9`** to the handoff notes: the test file path, the test
   names (verbatim), the pass count (verified via `dotnet exec`), and the
   FACES M1–M6 row mapping (which row = which `Decision` branch — the
   contract every follow-on lane copies).
