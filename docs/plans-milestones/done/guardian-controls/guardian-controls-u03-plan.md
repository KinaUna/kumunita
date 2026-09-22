# GU U03 — `AccessVia.Guardian` (the 9th value)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Append `Guardian` as the **9th** value on `AccessVia` (after `Group`) — the
M1 `Admin` / ADR 0013 `Group` append precedent (an additive value, never a
renumbering; no stored audit row's meaning changes). **One file, no renumber,
no other surface touched.**

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract` →
   `### AccessVia.Guardian (exact C#)`** (U01 pinned it) — the *primary*
   source. Match the value name + the doc-comment's intent.
2. `docs/adr/0028-guardian-controls-account-scope-supervision.md` §A (the
   standing: the 9th additive value, action-scoped, **never** a content read) +
   §C (the "no new authorization lane on the frozen `IAuthorizationService`"
   discipline) — the ADR authority.
3. `src/Kumunita.Core/Authorization/Decision.cs` — the `AccessVia` enum. The
   current tail is exactly: `… Admin,` then `Group` (no trailing comma) then
   `}`. Read the **`Admin`** and **`Group`** doc-comments (the "least-
   distortion slot is a … value named for it" voice) — U03's `Guardian`
   doc-comment mirrors that voice.

## Deliverables (1 file, modify)

### `src/Kumunita.Core/Authorization/Decision.cs`

- Add a trailing comma to `Group` (→ `Group,`).
- Add the new value immediately after, with its doc-comment (mirror the
  `Admin`/`Group` voice, and pin G·1 / G·3):

```
    /// <summary>
    /// The GU standing (guardian controls, ADR 0028): action-scoped to the
    /// five supervisory actions (suspend, community/group membership curation,
    /// invitation approval — G·3), exercised only on the IUserInfoService
    /// management lanes — **never** on a CanAsync / CanSeeAsync content
    /// decision (G·1). The M1 <see cref="Admin"/> / ADR 0013 <see cref="Group"/>
    /// append precedent: an additive enum value, the eight frozen values
    /// untouched.
    /// </summary>
    Guardian
```

- **Do not** renumber, rename, or reorder any existing value. **Do not** touch
  `AccessOutcome`, `Decision`, or `VisibleSet`.

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `AccessVia` has exactly **9**
values, `Guardian` last (index 8). **No new test** (U09's tests exercise it
indirectly via the audit rows). Handoff note (append): 3–4 lines starting
`## U03 — AccessVia.Guardian` — (a) the enum now has 9 values, (b) `Guardian`
is the 9th / last, (c) the G·1 doc-comment line (verbatim), (d) a confirmation
no existing value's position or name changed, (e) any compile warnings.
