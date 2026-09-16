# GA U02 — `IIdentityService.FindSubjectByEmailAsync` + the impl

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Add the **one** `IIdentityService` ADD (the pinned contract's exact seam
`FindSubjectByEmailAsync(string email)`) + its implementation (the
`userManager.FindByEmailAsync` precedent — a read, no audit row, null on
unknown email). **No Web, no tests** (U03 pins the seam tests; U06 pins the
Web tests).

## Context you need (read these first, in this order)

1. `docs/design/guardian-assignment-design.md` § **`## Pinned contract`
   → `### IIdentityService.FindSubjectByEmailAsync (exact C#)`** (U01
   pinned it) — the *primary* source for this unit. Match it verbatim.
2. `src/Kumunita.Core/Identity/IIdentityService.cs` — the **frozen surface**
   + the M1 lifecycle ADDs. Note the `// ── M1 lifecycle (ADR 0006-E
   compatible lane) ──` section header; the new seam goes **after**
   `ResendVerificationEmailAsync` (the shape precedent) and **before** the
   break-glass lane.
3. `src/Kumunita.Core/Identity/IdentityService.cs` — the
   `userManager.FindByEmailAsync` usage (the `ResendVerificationEmailAsync`
   precedent at ~line 137: `var user = await
   userManager.FindByEmailAsync(email);`). The new seam wraps this one
   call.
4. `docs/adr/0038-guardian-assignment.md` §Decision (B) (the ADR authority
   for the seam — the "one `IIdentityService` ADD" line).
5. `docs/design/guardian-assignment-design.md` § Invariants G-A·2 (the
   no-leak shape the doc-comment anchors).

## Deliverables (2 files, modify)

### 1. `src/Kumunita.Core/Identity/IIdentityService.cs` (modify)

Append the **exact** `FindSubjectByEmailAsync(string email)` seam from the
pinned contract, with its doc-comment (the ADR 0006-E lane; the G-A·2
no-leak shape). Place it in the M1 lifecycle block (the
`// ── M1 lifecycle (ADR 0006-E compatible lane) ──` section), **after**
the `ResendVerificationEmailAsync` method and **before** the break-glass
lane (`ConsumeBreakGlassAsync`). The exact seam:

```csharp
/// <summary>
/// GA (ADR 0038): resolve an email to a subject id (the assign form's one
/// external identifier). A read — no audit row, no mutation. Returns the
/// subject id, or null if the email has no account (the Web's user-
/// presentable error surface — the ADR 0008 "a non-guardian learns nothing"
/// shape: a null return, not an exception that names the email). ADR 0006-E
/// compatible ADD — the M1 lifecycle ADD precedent (the
/// <c>ResendVerificationEmailAsync</c> shape, a read over
/// <c>userManager.FindByEmailAsync</c>).
/// </summary>
Task<string?> FindSubjectByEmailAsync(string email);
```

### 2. `src/Kumunita.Core/Identity/IdentityService.cs` (modify)

Implement `FindSubjectByEmailAsync`:

```csharp
public async Task<string?> FindSubjectByEmailAsync(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        return null;
    var user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
    return user?.Id;
}
```

The `ResendVerificationEmailAsync` precedent: a read, no audit row, null on
unknown email. **No** new `using` needed (the `userManager` field is already
in the class). **No** `ArgumentException` on null/whitespace email (the
`ResendVerificationEmailAsync` precedent — the Web's form validation is the
gate; the seam is a read; returning null on whitespace is the no-leak
shape).

## Exit

`dotnet build` green. The seam is on the interface + the impl. **No new
test** (U03 pins). Handoff note: 4–5 lines starting `## U02 —
FindSubjectByEmailAsync` — (a) the seam's exact signature (verbatim), (b)
the impl's two-liner (verbatim), (c) the placement in the interface (the
M1 lifecycle block, after `ResendVerificationEmailAsync`), (d) a
confirmation no other `IIdentityService` member changed, (e) any compile
warnings.
