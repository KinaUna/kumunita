# GU U04 — Core: formation + suspension + independence seams

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Implement the three **account-level** seam groups on `IUserInfoService` +
`UserInfoService`: **formation** (`CreateGuardianLinkAsync`), **suspend /
unsuspend** (`SuspendChildAsync` / `UnsuspendChildAsync`), and **independence**
(`DissolveGuardianLinkAsync`, incl. the GlobalAdmin safety valve). These are
the lane's additive seams (ADR 0013 "one additive lane" discipline) — they sit
**beside** the membership lanes, never inside a content lane. **No Web surface,
no tests** (U09 pins the tests; U07+ wires the Web).

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract` →
   `### IUserInfoService guardian seams (exact C#)`** (U01 pinned it) — the
   *primary* source. Match the **signatures** verbatim (parameter names matter
   — the audit rows reference them).
2. `docs/adr/0028-...md` §C (the five actions' audit verbs + their `Via`) + §D
   (G·1–G·5: suspend is live standing off `Profile.Blocked`; dissolve is the
   one-way close with a GlobalAdmin-only safety valve) + §E (the audit-verb
   list: `guardian.create` / `guardian.suspend` / `guardian.unsuspend` /
   `guardian.dissolve`).
3. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — the interface: where to
   **append** the new `#region` (near the end, after the membership lanes), and
   the `/// <inheritdoc />` + XML-doc-comment voice to mirror.
4. `src/Kumunita.Core/UserInfo/UserInfoService.cs` § `AcceptGroupInvitationAsync`
   (~line 643) — the **write-path precedent**: load the target doc, verify
   preconditions (throw `InvalidOperationException` on a bad state), mutate in a
   single `Session` + `await session.SaveChangesAsync()` (invariant C3), then
   `session.Store(new Authorization.AccessAudit { … Via: …, Outcome: Allow })`
   in the **same session**. U04's seams follow this exact shape.
5. `src/Kumunita.Core/Identity/Profile.cs` § `Blocked` (~line 55) — the field
   U04's suspend/unsuspend set (suspend = `Blocked = true`; unsuspend =
   `Blocked = false`). Confirm the `Profile` `SubjectId` field name (line 34)
   is what U04's `childId` maps to.

## Deliverables (≤3 files, modify)

### 1. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (modify)

Append a new `#region` (title it `// GU guardian lanes (ADR 0028) — additive, beside the membership lanes`) at the **end** of the interface body (before the closing `}`), containing the four seam declarations **exactly as pinned**:

- `Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);`
- `Task SuspendChildAsync(string childId, string guardianId);`
- `Task UnsuspendChildAsync(string childId, string guardianId);`
- `Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);`

Each with an XML doc-comment (mirror the interface voice) that names the
guardian standing (G·1: **not** a content read; G·2: suspend is live standing;
G·5: the `viaAdmin` safety valve on dissolve).

### 2. `src/Kumunita.Core/UserInfo/UserInfoService.cs` (modify)

Implement the four seams as `/// <inheritdoc />` methods, each a **single
session** (C3). The exception vocabulary (the U01 frozen pin — the design doc's
"(else `UnauthorizedAccessException` → the Web's 404)" convention) is **two
kinds**: `UnauthorizedAccessException` when the actor has **no standing** (no
active link, or not the `GuardianId`) — the Web surfaces this as a 404; and
`InvalidOperationException` when a **row is missing** or in a bad state
(a missing/dissolved `GuardianLink`, a missing `Profile`). Each seam then
mutates the `GuardianLink` / `Profile.Blocked` and `session.Store(new
Authorization.AccessAudit { … })` **in the same session** with the pinned
audit verb + `Via`:

- **`CreateGuardianLinkAsync`** — **upserts** the `(GuardianId, ChildId)`
  `Active` row in the same session: if an `Active` row already exists for this
  pair, it is an **idempotent no-op** (refresh `CreatedAt` is *not* done; the
  row is left as-is and the method returns it — G·4, the U01 pin's "idempotent
  no-op on re-attach"), otherwise it inserts a fresh `Active` row. Audit verb
  `guardian.create`, `Via: Guardian`, `ActorId = guardianId`,
  `EffectivePrincipalId = guardianId`, `TargetKind = "guardian-link"`,
  `TargetId = linkId`. **No throw** on the duplicate case (the no-op is the
  contract, not an error).
- **`SuspendChildAsync`** — **standing gate first**: an **active**
  `GuardianLink` must exist for (guardian, child), with `GuardianId ==
  guardianId` — else throw **`UnauthorizedAccessException`** (the Web's 404,
  G·2/G·3). Then loads the `Profile` by `SubjectId = childId` (missing →
  `InvalidOperationException`), sets `Profile.Blocked = true` (**the same flag**
  `BlockedAccountMiddleware` + the directory already read — enforcement
  parity, the U01 pin). Audit verb `guardian.suspend`, `Via: Guardian`,
  `ActorId = guardianId`, `EffectivePrincipalId = guardianId`, `TargetKind =
  "profile"`, `TargetId = childId`.
- **`UnsuspendChildAsync`** — same standing gate (`UnauthorizedAccessException`
  on no active link); loads the `Profile`, sets `Profile.Blocked = false`; audit
  verb `guardian.unsuspend`, `Via: Guardian`.
- **`DissolveGuardianLinkAsync`** — loads the `GuardianLink` by `linkId` (missing
  → `InvalidOperationException`); if `!viaAdmin`, the actor **must be** the
  `GuardianId` (else **`UnauthorizedAccessException`** — G·4); if `viaAdmin`, no
  actor-identity check (the GlobalAdmin safety valve, G·5). Sets `Status =
  Dissolved`, `DissolvedAt = now`, `DissolvedBy = actorId`. **The child's
  memberships are preserved — a dissolve writes nothing to membership, and it
  does NOT set `Profile.Blocked`** (the design doc §D: "dissolving the link
  writes nothing to membership; the child's self-lanes restore on the very next
  read (C4)"). Un-suspend is a **separate** act — the GlobalAdmin `UnblockAsync`
  (the G·5 valve) or the guardian's `UnsuspendChildAsync` — not a side-effect of
  dissolve. Audit verb `guardian.dissolve`, `Via: (viaAdmin ? Admin :
  Guardian)`, `ActorId = actorId`, `EffectivePrincipalId = actorId`,
  `TargetKind = "guardian-link"`, `TargetId = linkId`.

### 3. `src/Kumunita.Core/DependencyInjection.cs` (modify **only if** the `UserInfoService` constructor signature changed)

If U04's impls require a **new** constructor parameter (they should not — U04
only touches the `store`-owned `UserInfoService(IDocumentStore store)` ctor),
add the registration line. **If no ctor change, do NOT touch this file.**

## Exit

`dotnet build Kumunita.slnx -c Debug` green. The four seams are declared on
`IUserInfoService` and implemented on `UserInfoService`. **No new test** (U09
pins the 11 GU tests — U04's throw conditions are the *preconditions* U09's
tests assert). Handoff note (append): 6–8 lines starting `## U04 — formation
+ suspension + independence seams` — (a) the four seam signatures (verbatim),
(b) the audit verb + `Via` for each, (c) the **exact** throw precondition for
each (one line each — `UnauthorizedAccessException` for the no-link gate,
`InvalidOperationException` for a missing row), (d) a confirmation dissolve
**does NOT** set `Profile.Blocked` (the design doc §D "writes nothing to
membership"; un-suspend is the separate `UnblockAsync`/`UnsuspendChildAsync`
act), (e) a confirmation duplicate formation is an **idempotent no-op** (not a
throw), (f) whether `DependencyInjection.cs` was touched (and why), (g) any
compile warnings.
