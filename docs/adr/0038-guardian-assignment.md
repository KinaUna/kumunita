# ADR 0038 — Guardian assignment: an existing guardian assigns a second guardian to a child's account

Status: Accepted
Date: 2026-09-16
Amends: 0028 (the G·4 "no self-serve claim" non-decision is superseded for
the *existing active guardian* case — the assign lane is the deliberate
exception), 0006 (a new `IIdentityService` ADD — a compatible ADD on the
owning module's public surface, named here), 0012/0013 (the standing-gate +
the "a non-guardian learns nothing" 404 shape inherited)

## Context

The GU lane (ADR 0028) shipped the account-level supervision surface for a
child's account, and ADR 0028 §B already records that **"a child may have
one or two guardians (each an active row)"** — the `GuardianLink` document
and the `CreateGuardianLinkAsync` seam both already support multiple active
rows for the same `ChildId`. The gap: today the **only** path to
`CreateGuardianLinkAsync` is the GU formation lane
(`GuardianController.AddChild`, paired with `IIdentityService.
RegisterAsync`), where the child's account is **created** in the same
commit. A guardian who created a child's account cannot later add a second
guardian (a co-parent, a grandparent) to that **existing** account — ADR
0028 §D G·4 explicitly records the non-decision "no self-serve 'claim
guardianship over an existing account' lane."

This ADR is the **deliberate supersession** of that non-decision for the
*existing active guardian* case: the standing basis for the second
guardian is not "created" but **"assigned-by-an-existing-guardian."** The
arrow moved from supervision to co-supervision; the standing conferred is
the same standing. `docs/design/guardian-assignment-design.md` freezes the
invariants **G-A·1–G-A·6**, the FACES, the pinned contract, and the seam
tests this ADR points at.

## Decision

### A. The standing

The **assigning** guardian's active `GuardianLink` over the child is the
standing basis (G-A·1 — the GU lane's G·2 "standing is from the active
link, checked live" precedent, inherited). The `ActiveLinkAsync` helper the
`Dissolve` route already uses gates the assign POST; a non-guardian → 404
(the ADR 0012/0013 "a non-guardian learns nothing" shape). No new
`AccessVia` value; the `AccessVia.Guardian` value (the GU lane's 9th value)
is **reused** as the audit `Via` for the `guardian.create` row the assign
lane emits.

### B. The seams

The **one** `IIdentityService` ADD (`FindSubjectByEmailAsync` — the email →
subject id read, null on unknown email, no audit row, no mutation) + the
**one** `GuardianController` action (`Assign`, `POST
me/children/{childId}/assign`) + the **one** `AssignGuardianForm` view
model + the **one** `GuardianItem` record + the **one**
`MembershipEditorModel.GuardianItems` field + the **two** `Detail.cshtml`
appends (the "other guardians" list + the assign form) + the
`guardian.assign.*` / `guardian.otherGuardians.*` localization keys. The
rest is the GU lane's existing surface (byte-identical): the
`CreateGuardianLinkAsync` seam, the `GuardianLink` POCO, the `M1DocTypes`
registration, the five supervisory actions, the GU enforcement path — none
of it moves.

### C. The invariants

G-A·1–G-A·6 (the design doc's §Invariants, verbatim): standing from the
assigning guardian's active link, checked live (G-A·1); the assigned
guardian must have an account — no auto-create (G-A·2); the assigned
guardian's standing is identical in kind to the creator's — no content
read, no "assigned" tier, `GuardianLink` byte-identical (G-A·3, inherits
G·1); idempotency — a duplicate pair is a no-op (G-A·4, inherited);
self-assignment is refused (G-A·5); no remove path (G-A·6, a named
deferral).

### D. The audit

The **existing** `guardian.create` verb (no new verb). The `ActorId` and
`EffectivePrincipalId` are both the **assigned** guardian — the
standing-holder. The lane reuses the GU byte-identical seam
`CreateGuardianLinkAsync(childId, guardianId)`, which sets
`ActorId = guardianId`; the `Assign` action passes the **assigned**
guardian's id as that argument. The `TargetId` is the new `GuardianLink`
row's id; the `TargetKind` is `"guardian-link"`; the `Via` is
`AccessVia.Guardian`. The row answers **"who now holds standing over this
child"** (the assigned guardian) and targets **what** (the link row over
the child); the **assigning** guardian's identity is **not persisted** on
the row — a legibility limitation accepted because G-A·3 forbids a new
seam (a seam that threaded the conferrer in would be a GU-surface change).
This is the GU lane's existing audit shape (ADR 0028 §E), unchanged.

### E. The non-decisions (the "for now")

- **No remove path** — a co-guardian dissolving *another* co-guardian's
  link, or the child dissolving a co-guardian's link. The ADR 0028 G·5
  safety valve (a GlobalAdmin dissolving any active link, audited `Via:
  Admin`) remains the only "dissolve any active link" path.
- **No acceptance/consent step** on the assigned guardian — the standing
  is from the active link, not from an acceptance.
- **No bulk assign** — one email per form.
- **No self-assignment** — refused (G-A·5), not a lane.
- **No second audit verb** — `guardian.create` is the one verb (its
  `ActorId` records the **assigned** guardian as standing-holder, not the
  assigning guardian — the GU seam's shape, §D).
- **No email notification** to the assigned guardian — the durable outbox
  / M1's verification lane is unchanged; the assigned guardian simply has
  standing on their next read.

Each is *deferred*, not *denied* — each re-litigates as an ADR 0038
amendment when it earns its keep.

## Consequences

- **The family case gains a *co-supervision* lane, not just a private-group
  one** — the ADR 0028 "the family case gains a supervision lane" precedent,
  extended: a second adult can be brought into standing over an existing
  child's account without a GlobalAdmin in the loop and without creating a
  new account.

- **The cardinal privacy rule is held and *proven* held.** G-A·3 is a pin,
  not a promise: the assigned guardian's standing is identical in kind to
  the creator's (the five GU actions, no content read); the `GuardianLink`
  POCO, the GU seams, and the GU enforcement path are all byte-identical. A
  family that later wants a *remove* lane or an *acceptance* step is told
  that is an ADR 0038 amendment, not a toggle.

- **The `Authority` chain gains a co-supervision standing row:** the
  `guardian.create` audit row records the **assigned** guardian as the
  standing-holder over the child's account (the GU seam's shape, §D), so
  "who now holds standing over this child" is legible and traceable (the
  ADR 0028 family legibility row, extended one step). **Named
  limitation:** the row does **not** persist the *assigning* guardian —
  "who conferred the standing" is not on the row (the assigning
  guardian's id is the actor of the web `POST`, recoverable only from the
  web layer's own request log, if any). If a future family wants the
  conferring guardian on the row, that is an ADR 0038 amendment (a
  deliberate GU-surface change), not a toggle.

- **The roadmap trio moves together** (AGENTS.md contract): `README.md`
  Features + Roadmap and `Milestones.cs` gain the `GA` named lane (pulled
  forward ahead of **M4**, the `GU` precedent); `MilestonesTests.cs`
  updates in the same commit. M4/M5/M6 stay Events / Projects /
  Portability.

- **Backward-compat:** fully additive. The `GuardianLink` POCO is
  byte-identical; the GU seams are byte-identical; the new
  `IIdentityService` member is an ADD (ADR 0006-E compatible); the new
  `GuardianController` action is an ADD; the new view model and record are
  new; the `Detail.cshtml` appends are additive; no stored row's meaning
  changes. Re-deploying an older image over a forward-migrated database is
  safe.

## Amendment (2026-09-17)

Two corrections, made by reconciling this ADR to the shipped code (which is
authoritative per G-A·3) and by promoting one acceptance-gate leg from
inference to a test. Both are additive/rectifying — no `GuardianLink`
POCO, GU seam, or enforcement-path change (G-A·3 held); the §E
non-decisions are unchanged.

1. **§D corrected** — the `guardian.create` row's `ActorId` /
   `EffectivePrincipalId` record the **assigned** guardian (the
   standing-holder), not the assigning guardian; the assigning
   guardian's identity is not persisted (legibility limitation named in
   §D). The pre-amendment prose ("both the assigning guardian") did not
   match the GU byte-identical seam and is superseded.
2. **Acceptance gate: the handoff leg is now a test, not an inference.**
   The design doc pins a 9th seam test,
   `Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild`
   (`tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs`): after the GA
   lane confers standing, the assigned guardian drives
   `SuspendChildAsync` / `UnsuspendChildAsync` over the child (G-A·3,
   identical in kind, no content read). The gate is now 4 Core + 5 Web =
   **9 pinned seam tests**, and the handoff leg is proven, not inferred.
