---
id: ch05-prompt
title: "CH05 prompt — IAuthenticationService stub nullability"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH05 — IAuthenticationService stub nullability (CS8767/CS8622)

## What you are doing

You are the **fifth working agent** on the **CODE-HYGIENE** lane.
CH01–CH04 are committed.

This unit fixes the **52 nullability warnings** in the 2 test stub files.
The `StubAuthService` classes implement `IAuthenticationService` but
declare `scheme` and `properties` as non-nullable where the interface
allows null. Add `?` to match.

## The one thing to get right

The interface `IAuthenticationService` declares `string? scheme` and
`AuthenticationProperties? properties`. The stubs declare them non-nullable.
Add `?` to the stubs to match the interface. Do NOT change the interface.

## Entry reads (only these)

- `grep -rn "interface IAuthenticationService" src/` (the interface
  definition — confirm `scheme` is `string?` and `properties` is
  `AuthenticationProperties?`).
- `sed -n '225,260p' tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs`
  (the `StubAuthService` class with the CS8767 warnings).
- `sed -n '255,285p' tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs`
  (the `StubAuthService` class).

## Deliverables (2 files)

1. `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` — in
   `StubAuthService`, change `string scheme` → `string? scheme` and
   `AuthenticationProperties properties` → `AuthenticationProperties?
   properties` on all 6 methods (Authenticate, Challenge, Forbid, SignIn,
   SignOut, and any others).
2. `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` — same
   fix in its `StubAuthService`.

## Exit

`dotnet build` green. The 52 warnings are gone:
`dotnet build --no-incremental 2>&1 | grep -E "CS8767|CS8622" | wc -l`
returns a count ≤ 4 (the non-stub ones, if any).
Handoff note: 3 lines starting `## U5 — stub nullability`.
