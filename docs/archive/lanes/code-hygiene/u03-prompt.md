---
id: ch03-prompt
title: "CH03 prompt — Test mechanical warnings"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH03 — Test mechanical warnings (CS4014/CS0105/CS0219/CS0108)

## What you are doing

You are the **third working agent** on the **CODE-HYGIENE** lane.
CH01–CH02 are committed.

This unit fixes the **8 mechanical warnings** in 4 test files. All are
mechanical: add `await`, remove a duplicate `using`, remove a dead variable,
add `new` keyword.

## Entry reads (only these)

- `sed -n '65,125p' tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs`
  (the 4 CS4014 unawaited calls at lines 70, 89, 104, 119).
- `head -10 tests/Kumunita.Web.Tests/HealthControllerTests.cs`
  (the CS0105 duplicate using at line 2).
- `sed -n '105,115p' tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`
  (the CS0219 dead variable at line 110).
- `sed -n '45,65p' tests/Kumunita.Web.Tests/ProjectsBaseControllerTests.cs`
  (the 2 CS0108 hiding warnings at lines 49, 61).

## Deliverables (4 files)

1. `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` — add
   `await` to the 4 unawaited calls. If the method is not `async`, make
   it `async Task` and add `await`.
2. `tests/Kumunita.Web.Tests/HealthControllerTests.cs` — remove the
   duplicate `using System.Security.Claims;` (line 2).
3. `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` —
   remove the unused `baseLine` variable (line 110) or prefix with `_`.
4. `tests/Kumunita.Web.Tests/ProjectsBaseControllerTests.cs` — add `new`
   keyword to `ProjectRow` (line 49) and `HasSortParam` (line 61).

## Exit

`dotnet build` green. The 8 warnings are gone:
`dotnet build --no-incremental 2>&1 | grep -E "CS4014|CS0105|CS0219|CS0108"`
returns nothing.
Handoff note: 3 lines starting `## U3 — test mechanical`.
