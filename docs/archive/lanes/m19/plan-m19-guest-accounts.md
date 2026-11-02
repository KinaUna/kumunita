# M19 — Guest accounts (the register)

> **This file is the register** — the milestone map, the locked [PROPOSED]
> decision set, the invariants, the FACES, the unit map, the workflow, the
> atomicity contract, and the §drift-guard. It is read first by **every** unit
> agent (U00–U05) before anything else. It is **not** a per-unit plan: each unit
> has its own self-contained file (`m19-uNN.md`) that a ~32K-context agent can
> execute without re-deriving this file.
>
> **M19 is the *last* milestone in `Milestones.All`.** There is no M20, and
> after U05's close flip no milestone will be `StatusNext` anymore. U05 handles
> that end-of-roadmap re-pin — do not "promote" a nonexistent M20.

## Tiering (three documents, like M18)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `in-progress/plan-m19-guest-accounts.md` | U00 (author) + all units (read) | moves to `done/` at U05 |
| 2 — Per-unit plans | `in-progress/m19-uNN.md` | the `U##` agent | moves to `done/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/m19-guest-accounts-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/` at U05 |

The handoff-notes file is created by **U00** (at runtime, not by this authoring
pass). Every unit appends a short `## U##` section before it moves its own plan
to `done/`.

## Atomicity contract (sized for ~32K context)

Each unit is one self-contained step a fresh agent can complete and hand off:

- **Entry reads:** 4–8 files, named in the unit plan (with a one-line "why"
  each).
- **Deliverables:** ≤ 7 small files, named exactly (paths +, where locked, the
  exact C# / keys the design doc pins).
- **Exit gate:** **one** `dotnet build Kumunita.slnx -c Debug` **plus one**
  `dotnet exec` test assembly — either
  `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **or**
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`. Both
  must be green.
- **A unit that needs *both* test assemblies is too big — split it.**
  (Core units exit on Core.Tests; Web units exit on Web.Tests. The close unit
  exits on Web.Tests — it flips `Milestones.cs`, which is pinned by
  `MilestonesTests` in `Kumunita.Web.Tests`.)
- **No build/test exit for U00** — it is docs-only (design doc + ADR + index
  row). Its exit is "the files exist and the [PROPOSED] set is locked or
  amended in-unit."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Unit map

| Unit | Title | Track | Exit test assembly |
| --- | --- | --- | --- |
| U00 | Design doc + ADR 0120 (the sign-off gate) | docs-only | none (no build/test) |
| U01 | `Profile.Guest` + `GuestAccess` doc + `IIdentityService` seam + pinned tests | Core | `Kumunita.Core.Tests` |
| U02 | `Roles.Guest` claim mint at the Identity↔cookie seam + claim-shape pin | Web | `Kumunita.Web.Tests` |
| U03 | `kw-l` key set × en/de/fr/da + parity pin | Core | `Kumunita.Core.Tests` |
| U04 | `/admin/guests` GlobalAdmin controller + views + nav | Web | `Kumunita.Web.Tests` |
| U05 | Close unit — milestone flip, `MilestonesTests` re-pin, docs parity, `done/` move | Web + docs | `Kumunita.Web.Tests` |

## Understanding (what M19 is)

**M19 = guest accounts.** A guest is an *outside* person — a consultant, coach,
teacher, speaker, entertainer — whom a resident invites into a **limited,
time-bounded** slice of the platform. The roadmap line is the scope:
> "limited-privilege accounts for consultants, coaches, teachers, speakers,
> entertainers, etc.; **admins set what a guest may access and when**."

Three things follow from that sentence:

1. **A guest is an account, not a role granted to a resident.** Guests are
   created by an admin (the "admins set" half), they are distinct from the
   resident population, and they do **not** hold the verified-resident standing
   (`Roles.Member`). They are the *outside* of the resident circle.
2. **"What a guest may access"** is an admin-settled, *limited* allowance —
   a narrow set, **not** the full resident standing. The floor is the
   *least*-privileged default; an admin opts a guest *in* to specific
   surfaces, never to full membership.
3. **"And when"** = **bounded in time.** A guest standing carries an expiry;
   the standing is live only while `ValidFrom ≤ now < ValidUntil`. The "when"
   is a first-class field, not a note in the ADR.

Everything else about M19 is *what it is not* — see "What M19 is NOT" below and
the §deferred lanes (D7).

## What M19 is NOT

- **Not self-service.** A guest does not create their own account and pick
  their own access. An admin creates the guest and settles what the guest may
  do. (This is the inverse of ADR 0050's open sign-up lane — guests enter
  through the *admin* lane, not the `/account/signup` lane.)
- **Not a full membership.** A guest is never a `Member`. The guest does not
  acquire the verified-resident standing, does not join the community, and does
  not inherit any audience a resident holds. The guest's access is the *allowance
  document's* set — nothing more.
- **Not a new authorization surface.** M19 rides the frozen `IAuthorizationService`
  seams (ADR 0006). It does **not** add a branch to `Decide()`, does not touch
  `CanAsync`/`CanSeeAsync`'s frozen signatures, and does not re-derive
  "may this actor see that resource." The guest's standing is settled by an
  *admin write lane* (who the guest is) and read back per request (what the
  guest may see) — the same thin-token / fat-authorization split as every other
  standing in the repo (ADR 0001-B).

## Decisions (D1–D7) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the design doc, or amends them **in U00** (recorded
> in the design doc's §1.a + the handoff notes). A later unit may not amend a
> D#; if a D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — The guest is a **standing on the account**, not a new `Profile` subclass

A guest is expressed as an additive flag on the existing `Profile`
(`bool IsGuest`, additive, ADR 0004 §B.1 — the `Blocked`/`Verified` field
precedent) **plus** a dedicated admin-settled **allowance document** (D2). The
guest is still a real Identity account (real `User`, real `Profile`, real
`SubjectId`) so the frozen authorization seams and the audit log treat the guest
exactly like any other actor.

*Forbids:* a separate `GuestProfile` document type, a parallel account store, or
any new bounded context for guests — the guest is a standing, not a new entity.

### D2 — The allowance is an **admin-settled, time-bounded document**, one audited write lane

A new document `Kumunita.Core.Identity.GuestAccess` (id = the guest's
`SubjectId`) carries the *limited* standing:

```csharp
public sealed class GuestAccess
{
    public string SubjectId { get; set; } = string.Empty; // document identity
    /// <summary>The bounded window (D3). The standing is live only while
    /// now >= ValidFrom && now < ValidUntil.</summary>
    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    /// <summary>
    /// The closed, enumerated set of surfaces the guest may access (D4) — a
    /// small fixed union, not free-form strings. The floor is the empty set.
    /// </summary>
    public GuestSurface[] AllowedSurfaces { get; set; } = [];
    /// <summary>Who settled this standing (admin SubjectId); recorded for audit.</summary>
    public string SetByAdmin { get; set; } = string.Empty;
}
```

One **single audited write lane** on `IIdentityService`
(`SetGuestAccessAsync(GuestAccess, string adminSubjectId)`) — the exact shape
of ADR 0050's `SetSignupOpenAsync` (load-or-create, store, one `AccessAudit`
row `Via = Admin` in the same session). The Web boundary owns the GlobalAdmin
standing check; the Core seam does not re-check `User`.

*Forbids:* a free-form string list for `AllowedSurfaces` (it must be a closed
enum so a typo cannot silently grant a surface), a second write lane, or an
unaudited direct `IDocumentSession.Store` from a controller.

### D3 — "When" is a **bounded window on the allowance**, evaluated per request

The standing is live only while `ValidFrom ≤ now < ValidUntil`. A guest outside
that window has **no standing** (the factory mints no `Guest` role — see D5),
exactly as an unverified account is denied `Member`. No expiry job, no
projection: the window is read at the Identity↔cookie seam (the
`KumunitaClaimsPrincipalFactory`), the same place `Verified`/`Blocked` are
consulted. This is the "decision-needed" half the roadmap names — "and when" —
and it is settled by *data*, not config, and not a background job (the ADR 0050
"data, not config" posture).

*Forbids:* a background "expire the guest" job, a projection that lags the
window, or an unbounded guest standing (no `ValidUntil`).

### D4 — The allowed surface set is a **closed enum** (the floor is empty)

```csharp
/// <summary>
/// M19 (ADR 0120, D4) — the closed, enumerated set of surfaces a guest may
/// be allowed onto. The floor is the empty set (a guest with no surfaces
/// signs in to a shell with no content). Additive: a new surface is a new
/// enum value (the ADR 0030 composable-role / the AccessVia append precedent),
/// never a free-form string.
/// </summary>
public enum GuestSurface
{
    None = 0,
    /// <summary>See the community's public announcement feed (read).</summary>
    Announcements = 1,
    /// <summary>See public events (the M4 events read surface, read).</summary>
    Events = 2,
    /// <summary>See public directory basic info (name + verified badge, read).</summary>
    Directory = 4,
}
```

A closed bit-flag enum: each surface is a distinct value, an admin composes a
set, and a *typo or a wrong value grants nothing*. The floor (empty set) is the
least-privileged default — a guest account with no surfaces is a signed-in
shell, not an error.

*Forbids:* free-form surface strings, a boolean-per-surface field on `Profile`
(scattered, hard to audit as one settled set), or granting `Member` to make a
surface "just work."

### D5 — The guest standing is a **claim minted at the Identity↔cookie seam**, riding the frozen authorization

`Roles.Guest = "Guest"` is added to the `Roles` static class (the ADR 0030
composable-role / `AccessVia` append precedent — a new claim string, the
existing claim set untouched in shape). `KumunitaClaimsPrincipalFactory` mints
`Guest` **iff** the account is a guest (`Profile.IsGuest`), not blocked, and the
allowance window is live (D3). A guest **does not** get `Member` (D4 floor) and
**does not** get any community standing. The frozen `IAuthorizationService`
seams are *untouched*: the guest's "what may I see" is answered per request by
the same `CanAsync`/`CanSeeAsync` calls a resident uses — the guest simply
carries a different standing (no `Member`, the `Guest` claim + the allowance
set) into those decisions.

*Forbids:* a new branch in `Decide()` for guests, an extended `CanAsync`
signature, or encoding guest standing in a relational table — the claim rides
the existing thin-token / fat-authorization seam exactly as `Verified`/`Blocked`
do today.

### D6 — The admin surface is **`/admin/guests`, GlobalAdmin-gated**, the ADR 0050 precedent

A dedicated `AdminGuestsController` — **not** a new action on `AdminController`
— mirroring `AdminSignupController` (ADR 0050): `[Route("admin/guests")]`,
`[Authorize(Roles = Roles.GlobalAdmin)]`, a thin list / create / set-window /
set-surfaces surface over `IIdentityService.SetGuestAccessAsync`. The gate is
checked **first** on both GET and POST (the ADR 0050 "gate is authoritative on
both the read and write surface" rule). Affordances on `/admin` index are
hidden-not-disabled for non-GlobalAdmin actors.

*Forbids:* a new action on the fat `AdminController`, a self-service guest
creation path, or an admin surface that writes the allowance without a GlobalAdmin
standing check (the Web boundary owns that check).

### D7 — **Deferred lanes** (each a future ADR, not part of M19)

1. **Per-guest content *authors*** (a guest posting into a group / an event
   note) — M19 is *read-only standing*; a guest authoring UGC is a later design
   (it touches the author-only edit lanes ADR 0014/0016/0017 and the
   translation lanes, and needs its own access model).
2. **Guest invitations** (a resident invites a guest, the guest self-serves a
   password from an admin-sent link) — the admin-creates path (D6) is M19; the
   resident-issues-a-token path is the ADR 0050 invitation-mechanism follow-on
   applied to guests.
3. **Per-surface fine-grained control** (a guest sees *some* announcements but
   not *some* events) — M19's `AllowedSurfaces` is a coarse, closed set
   (D4); per-resource grants ride the existing audience machinery and are a
   later design.
4. **Guest notification / messaging** — a guest does not enter the M6
   notifications or M9 messaging surface in M19.
5. **Guest analytics / audit depth** — the guest's actions append the same
   `AccessAudit` rows as any actor (C-M19·5), but a dedicated guest-usage
   report is a later lane (the M13 analytics surface is resident-shaped).

## Invariants (C-M19·1 … C-M19·6)

- **C-M19·1 — The guest is a standing, not an entity.** A guest is a real
  Identity account + `Profile.IsGuest` + a `GuestAccess` document (D1/D2). There
  is no `GuestProfile`, no parallel store, no new bounded context. A test
  asserts the guest resolves through the same `IIdentityService` /
  `IAuthorizationService` seams a resident does.
- **C-M19·2 — The guest never holds `Member`.** A guest's role set contains
  `Guest` and (if elevated, which M19 does not grant) nothing else. A test
  asserts a live guest's minted claims contain `Guest` and **not** `Member`,
  and that the standing is *limited* to the `AllowedSurfaces` set (D4 floor).
- **C-M19·3 — "When" is a live window, not a job.** The standing is live iff
  `ValidFrom ≤ now < ValidUntil` (D3). A test asserts a guest outside the
  window mints no `Guest` claim (no standing), and there is **no** background
  expiry job.
- **C-M19·4 — Closed surface set, empty floor.** `AllowedSurfaces` is a closed
  `GuestSurface[]` (D4); an unknown/typo value grants nothing; the empty set is
  a valid, signed-in shell. A test asserts an empty-set guest sees no gated
  content.
- **C-M19·5 — Every guest action is audited, same as any actor.** The
  allowance write lane appends exactly one `AccessAudit` row (`Via = Admin`,
  D2); the guest's content reads append the same `AccessAudit` rows a resident's
  reads do (the frozen seams, untouched). No unaudited access (ADR 0006-E).
- **C-M19·6 — Zero new authorization surface.** M19 adds no branch to
  `Decide()`, no new `IAuthorizationService` signature, and no relational
  standing table. The guest standing rides the existing thin-token /
  fat-authorization seam (D5, ADR 0001-B / ADR 0006). A claim-shape pin test
  asserts the only new claim string is `Kumunita.Role` = `"Guest"` (the
  `ClaimTypes.All` set is unchanged).

## FACES (F1–F5) + the named trade

- **F1 — The admin creates a guest and settles the standing** (D2, D6): pick a
  surface set (D4), set the window (D3), one audited write.
- **F2 — The guest signs in to a limited shell** (D5): a live guest sees only
  the allowed surfaces; an expired guest sees nothing (no standing).
- **F3 — The standing is time-bounded and data-settled** (D3): "and when" is a
  window on the allowance, changed by an admin, live on the next request — no
  restart, no job.
- **F4 — The guest is auditable like any actor** (C-M19·5): the allowance write
  and the guest's reads both append `AccessAudit` rows.
- **F5 — A guest is outside the resident circle** (C-M19·2): no `Member`, no
  community standing, no UGC authoring in M19 (D7 lane 1).

**The named trade.** M19 buys a *limited, time-bounded, admin-settled* guest
standing with **zero new authorization surface** and **one additive `Profile`
flag + one small document + one enum + one claim**, in exchange for a **coarse,
closed surface set** (D4) — a guest gets "the announcements surface" or "the
events surface," not "announcement #42 but not #47." The per-resource
fine-grain (D7 lane 3) and the guest-as-author (D7 lane 1) are deliberately
*not* in M19: they are larger designs that need their own ADRs, and M19's
value (a consultant/coach/teacher who can *see* the feed they're engaged with,
for a bounded window, with nothing to lose if they're not) is delivered by the
closed set.

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — The standing is limited + time-bounded.** A live guest (window
  live, `AllowedSurfaces = [Announcements]`) mints `Guest`, not `Member`, and
  may see the announcements read surface but **not** a `Member`-gated surface.
  An expired guest (window lapsed) mints no `Guest` claim. *(C-M19·2 / ·3.)*
- **GATE-2 — The closed surface set floors to nothing.** A guest with
  `AllowedSurfaces = []` signs in to a shell and sees no gated content (the
  empty set is a valid, least-privileged state, not an error). *(C-M19·4.)*
- **GATE-3 — The allowance write lane is single + audited.** One call to
  `SetGuestAccessAsync` stores the document and appends exactly one `AccessAudit`
  row (`Via = Admin`, `Action` naming the guest-standing write); a hand-crafted
  direct store from a controller is impossible (the Web boundary owns the
  GlobalAdmin check; the seam is the only write). *(C-M19·5, D2.)*
- **GATE-4 — Zero new authorization surface.** The claim-shape pin test
  passes: the only new claim string is `Kumunita.Role` = `"Guest"`; the
  `ClaimTypes.All` set is unchanged; `Decide()` has no guest branch. *(C-M19·6,
  D5.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard).
2. **Read the unit plan** (`m19-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/m19-uNN.md` → `done/m19-uNN.md` (flat,
   directly under `done/` — the M13–M18 convention, **not** a `done/m19/`
   subfolder).

## Unit-series rules

- **Order is U00 → U05.** U00 (docs + ADR) must land before any code unit,
  because the design doc's "Seams & contracts (Part 2)" section pins the exact
  C# U01–U04 implement. A later unit may read an earlier unit's code, but never
  re-derive a D#.
- **One write lane per unit.** U01 owns `SetGuestAccessAsync` + the `GuestAccess`
  doc + the `Profile.IsGuest` flag. U02 owns the claim mint. U04 owns the
  controller. No unit both *reads* and *writes* a seam another unit owns.
- **One test-assembly exit per unit.** A Core unit exits on Core.Tests; a Web
  unit exits on Web.Tests. If a unit needs both, it is too big — split it (see
  the Atomicity contract).
- **The close unit is last and owns the milestone flip.** U05 flips
  `Milestones.cs` (M19 → `StatusDone`), re-pins `MilestonesTests` for the
  **end-of-roadmap** case (no `StatusNext` remains — see U05), flips the README
  Roadmap + `docs/STATUS.md` + `docs/ARCHITECTURE.md`, and moves all M19
  artifacts flat to `done/`.
- **`kw-l` parity is closed in U03.** Every new user-visible string M19
  introduces is a `KnownTranslationKeys` entry present, non-empty, in **all
  four** languages (en/de/fr/da); the `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` pin the closure. U04 *consumes* the keys;
  it does not add them.

## §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, or a relational standing table** — that violates C-M19·6 /
  D5; stop and report (the whole point of M19 is zero new authorization
  surface).
- A user-visible string that is **not** already in `KnownTranslationKeys` for all
  four languages is about to be rendered — add it in U03's closed set first; do
  not inline a string (the parity pin will fail, and that is the point).
- The close unit (U05) finds a milestone after M19, or a `StatusNext` that is
  not M19 — the roadmap assumption is wrong; stop and confirm the milestone
  order before flipping.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not re-derive
what an earlier unit already settled (a D# amendment, a key set, a seam shape).

**The register is the map, not the code.** If a unit is tempted to "just add a
field to `Decide()`" or "make the guest a `Member`," that is the §drift-guard
firing — M19's value is precisely that it does *not*.
