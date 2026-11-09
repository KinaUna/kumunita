---
id: is27-prompt
title: "IS27 prompt — PortabilityExportScope + the content-free filter"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS27 — `PortabilityExportScope` + the content-free filter

## What you are doing

You are the **twenty-seventh working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS26 are committed.

This unit is the **S1 fix + its scope seam** (E1, plan row IS27). ADR 0299
(IS25) is the spec and `BreakGlassProbe` (IS26) is the gate the controller
will call (IS28) — this unit only decides **what a given scope exports**.
Concretely: a new `PortabilityExportScope` enum, a content-free filter in the
export loop driven by the `[DataClass]` catalog (ADR 0298, IS16–IS24), and an
`IPortabilityService` overload where the **old** `ExportAsync(actor, ct)`
delegates to `ContentFree`.

**Scope, not gate:** IS27 changes *which rows/fields leave the instance*. It
does **not** wire the operator-override 403 (that's IS28) or the resident
transparency notification (IS29). It does **not** touch `Program.cs`.

## The one thing to get right

**The old `ExportAsync(actor, ct)` callers must keep compiling and keep
meaning `ContentFree`.** The new overload is `ExportAsync(string actor,
PortabilityExportScope scope, CancellationToken ct = default)`; the old
two-arg form delegates to it with `ContentFree`. If an existing round-trip
test now expects private rows, it must call the `Full` overload — that edit is
**IS27b** (tests only), a separate commit, not a fifth file here.

## Blocked (decide first, then pause if unresolved)

IS24's handoff left **successor decisions open** that §2.6 depends on — the
`Profile.ContactEmail`/`Bio` `Keep`/`Pseudonymize` rows and the three
`UsageEvent` `Keep` rows. **Re-read** `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
(the IS24 section) and design §2.6 before annotating which `Profile` PII fields
`ContentFree` blanks. If a field's class/erasure is still an open `// TODO ❓`,
**pause** and record it — do not invent a policy.

## Deliverables (≤ 4 files, this commit)

1. **`src/Kumunita.Core/Portability/PortabilityExportScope.cs`** (new) —
   `public enum PortabilityExportScope { ContentFree, Full }`.
2. **`src/Kumunita.Core/Portability/PortabilityExportDocuments.cs`** — in the
   export loop, `ContentFree` **skips** the §2.6-marked types (driven by
   `DataClassCatalog`: `PrivateContent` kinds and rows with a non-null
   restrictive `Audience`) and **blanks** the §2.6 PII fields on `Profile`.
   `Full` behaves exactly as it does today (no behaviour change for it).
3. **`IPortabilityService` / `PortabilityService`** — add the `scope`
   overload; the old `ExportAsync(actor, ct)` delegates to `ContentFree`.
   Keep the `CancellationToken` parameter.
4. **`tests/Kumunita.Core.Tests/PortabilityExportScopeTests.cs`** (new) — the
   four tests below.

## Entry reads (only these — no whole-file reads)

- `sed -n '130,175p' src/Kumunita.Core/Portability/PortabilityExportDocuments.cs`
  (the export loop).
- `grep -rn "interface IPortabilityService" -A20 src/` (the service seam).
- `docs/design/improve-sec-design.md` §2.6 (the export-scope table: which M11
  types and fields `ContentFree` strips).
- `docs/adr/0299-instance-export-operator-gated.md` (the Constraints section
  names this scope; the gate IS28 will call `BreakGlassProbe`).
- `src/Kumunita.Core/Privacy/DataClassAttribute.cs` (the catalog + enums
  `DataClassKind` / `ExportScope` from IS16 — drive the filter from it).
- The existing callers that keep compiling:
  `grep -n "ExportAsync(" tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs`
  and `tests/Kumunita.Web.Tests/AdminPortabilityControllerTests.cs`.
- The SEC protocol: `docs/plans-milestones/plan-improve-sec.md` per-unit
  contract (D8) — read only the entry reads, edit only the deliverables, no
  PII, pause on drift, never edit around a failing test.
- Test-runner form (xunit.v3 in-process `-class`, **not**
  `dotnet test --filter`): `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3).

## Tests (4 new, `PortabilityExportScopeTests`)

- `ContentFree_HasNoConversationsOrMessages` — a `ContentFree` archive has no
  `Conversation`/`Message` (and no `GroupConversation`/`GroupMessage`) rows.
- `ContentFree_HasNoRestrictedPosts_HasPublicOnes` — audience-restricted
  posts are absent; public posts are present.
- `ContentFree_BlanksProfileContactFields` — the §2.6 PII fields on `Profile`
  (per the resolved decision) are blank; non-PII profile fields are intact.
- `Full_RoundTripsAsBefore` — `Full` is byte-identical to the pre-change
  export (private rows included), proving the old path is unchanged.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `PortabilityExportScopeTests` green (4/4) via the xunit.v3 runner form.
- [ ] `PortabilityRoundTripTests` still green (round-trip uses `Full`; if it
      now needs `Full` explicitly that's IS27b, a separate commit).
- [ ] Old `ExportAsync(actor, ct)` callers still compile (the overload stays).
- [ ] No PII in code, tests or notes (`user-a`, `a@example.test` style only).
- [ ] One `## IS27` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the public names, tests + pass count, next-unit notes, anything
      noticed — 3–8 lines per the per-unit contract).
- [ ] One commit (IS27b, if needed, is a second commit).

## Definition of done (IS27)

- [ ] `PortabilityExportScope { ContentFree, Full }` exists; the export loop
      applies the content-free filter driven by `DataClassCatalog`.
- [ ] Old `ExportAsync(actor, ct)` delegates to `ContentFree`; `Full` is
      unchanged; all old callers compile.
- [ ] 4 new tests green + `PortabilityRoundTripTests` green.
- [ ] One commit, one handoff section, no PII; §2.6 decisions either resolved
      or paused-and-recorded.
- [ ] **Next unit: IS28** — Controller: scope, override gate, audit row
      (`GET export?scope=full` returns `Forbid` unless
      `BreakGlassProbe.HasActiveAsync`; the audit row records
      `Action = portability.export.{content-free|full}` + `Via = Admin|BreakGlass`;
      two view buttons; tests `Export_Default_IsContentFree`,
      `FullExport_WithoutOverride_Forbidden`,
      `FullExport_WithOverride_AuditedBreakGlass`).
