# M19 — Guest accounts (design doc)

> **Milestone M19 — the *last* milestone in `Milestones.All`.** **Guest
> accounts** (the README M19 row, verbatim: "limited-privilege accounts for
> consultants, coaches, teachers, speakers, entertainers, and similar
> community-adjacent roles who need a place in the platform without full
> resident standing; **admins set the limits on what a guest may access and
> for how long**"). A guest is an *outside* person whom a resident invites
> into a **limited, time-bounded** slice of the platform. The "what" is an
> admin-settled, closed surface set (D4); the "when" is a bounded window on
> the allowance (D3) — both **data**, not config, no job.
>
> **A milestone over the frozen M1 identity surface.** Zero new bounded
> context, zero new `*DocTypes` surface, zero new `IAuthorizationService`
> surface, zero new `AccessAction`, zero new `AccessVia`, zero new adapter,
> zero new `Decide()` branch (C-M19·6, D5). The **only** schema changes are
> **one additive `bool` on the existing `Profile` POCO** (`IsGuest`, D1 —
> the ADR 0004 §B.1 `Blocked`/`Verified` additive-field precedent) **+ one
> new document** (`GuestAccess`, D2 — registered on `M1DocTypes`) **+ one
> closed enum** (`GuestSurface`, D4) **+ one new role-string constant**
> (`Roles.Guest`, D5 — a new claim *value* on the existing `Kumunita.Role`
> claim *type*; the `ClaimTypes.All` set is **untouched**). The closed
> `kw-l` key set (10 keys × en/de/fr/da, §8) + the four GATE acceptance
> tests (GATE-1…GATE-4, §10) are locked in **ADR 0120 (Accepted,
> 2026-09-30)**.
>
> **The frozen authorization seams, the audit log, and the resident standing
> are byte-identical** (C-M19·1/2/6). The guest rides the same thin-token /
> fat-authorization split as every other standing (ADR 0001-B): a real
> `User`, a real `Profile`, a real `SubjectId` — the frozen
> `IAuthorizationService` and the `AccessAudit` lane treat the guest exactly
> like any other actor; the guest simply carries a *different* standing (no
> `Member`, the `Guest` claim + the allowance set) into those decisions.
>
> **Status.** **LOCKED.** The decisions D1–D7, the invariants C-M19·1…C-M19·6,
> the FACES F1–F5 + the named trade, the §8 `kw-l` key list, and the §10 gate
> test names are locked in **ADR 0120 (Accepted, 2026-09-30)**. The
> `[PROPOSED]` set in the register
> `docs/plans-milestones/in-progress/plan-m19-guest-accounts.md` is the locked
> set this doc restates **verbatim** (the U00 handoff entry records the lock).
> **One annotation amendment** was needed — the D2 `AllowedSurfaces` field
> shape (array → `[Flags]` scalar) is recorded in §1.a; the D4 *decision*
> (a closed, enumerated surface set with an empty floor) is unchanged.
>
> **The one thing every unit must respect:** M19 **composes only frozen
> seams** — the frozen `IIdentityService` principal reads (U01 **adds** one
> read + one audited write seam **alongside** the existing
> `IsSignupOpenAsync`/`SetSignupOpenAsync` pair, the ADR 0050 shape; the
> signatures of every existing member are **untouched**), the frozen
> `IAuthorizationService` + the frozen `Decide()` (D5 — the guest's "what may
> I see" is answered by the same `CanAsync`/`CanSeeAsync` calls a resident
> uses; the guest carries a different standing, not a new branch), and the
> frozen `AccessAudit` lane (C-M19·5 — one row per guest-standing write,
> `Via = Admin`). It does **not** add a branch to `Decide()`, a new
> `IAuthorizationService` signature, a new `AccessAction`, a new `AccessVia`,
> a new adapter, a new bounded context, a relational standing table, a
> background expiry job, or a guest-as-`Member` (C-M19·6, D1/D3/D5, the
> §drift-guard below). Every register unit (U01's Core seam, U02's claim
> mint, U03's `kw-l` keys, U04's controller) enforces this at a different
> seam; the §11 frozen list below is the **exact** set that is untouched.

## 0 — Scope & non-scope

**What M19 does (README verbatim):** M19 is "**Guest accounts**:
limited-privilege accounts for consultants, coaches, teachers, speakers,
entertainers, and similar community-adjacent roles who need a place in the
platform without full resident standing; **admins set the limits on what a
guest may access and for how long**." Three things follow from that
sentence (the register's "Understanding"):

1. **A guest is an account, not a role granted to a resident.** Guests are
   created by an admin (the "admins set" half), they are distinct from the
   resident population, and they do **not** hold the verified-resident
   standing (`Roles.Member`). They are the *outside* of the resident circle.
2. **"What a guest may access"** is an admin-settled, *limited* allowance — a
   narrow set, **not** the full resident standing. The floor is the
   *least*-privileged default; an admin opts a guest *in* to specific
   surfaces, never to full membership.
3. **"And when"** = **bounded in time.** A guest standing carries an expiry;
   the standing is live only while `ValidFrom ≤ now < ValidUntil`. The "when"
   is a first-class field, not a note in the ADR.

**What M19 is NOT (the register's "What M19 is NOT", verbatim):**

- **Not self-service.** A guest does not create their own account and pick
  their own access. An admin creates the guest and settles what the guest may
  do. (The inverse of ADR 0050's open sign-up lane — guests enter through the
  *admin* lane, not the `/account/signup` lane.)
- **Not a full membership.** A guest is never a `Member`. The guest does not
  acquire the verified-resident standing, does not join the community, and
  does not inherit any audience a resident holds. The guest's access is the
  *allowance document's* set — nothing more.
- **Not a new authorization surface.** M19 rides the frozen
  `IAuthorizationService` seams (ADR 0006). It does **not** add a branch to
  `Decide()`, does not touch `CanAsync`/`CanSeeAsync`'s frozen signatures,
  and does not re-derive "may this actor see that resource." The guest's
  standing is settled by an *admin write lane* (who the guest is) and read
  back per request (what the guest may see) — the same thin-token /
  fat-authorization split as every other standing in the repo (ADR 0001-B).

**The architectural decision (locked by U00):** M19 expresses the guest as an
**additive standing on the existing account** (a `Profile.IsGuest` flag + a
small admin-settled `GuestAccess` allowance document + a closed `GuestSurface`
set + one `Guest` role-string), and mints that standing **at the
Identity↔cookie seam** (the `KumunitaClaimsPrincipalFactory`, where
`Verified`/`Blocked` are already consulted). This is D1 + D2 + D5 together —
it is the whole point of the milestone, and it is what lets every other
surface (the frozen authorization, the audit log, the resident standing) keep
working **unchanged**: they already read the same thin principal and run the
same `CanAsync`/`CanSeeAsync` decisions.

## 1 — Decisions (D1–D7)

**D1 — The guest is a standing on the account, not a new `Profile`
subclass.** A guest is expressed as an additive flag on the existing
`Profile` (`bool IsGuest`, additive, ADR 0004 §B.1 — the `Blocked`/`Verified`
field precedent) **plus** a dedicated admin-settled **allowance document**
(D2). The guest is still a real Identity account (real `User`, real
`Profile`, real `SubjectId`) so the frozen authorization seams and the audit
log treat the guest exactly like any other actor.

*Forbids:* a separate `GuestProfile` document type, a parallel account store,
or any new bounded context for guests — the guest is a standing, not a new
entity.

**D2 — The allowance is an admin-settled, time-bounded document, one audited
write lane.** A new document `Kumunita.Core.Identity.GuestAccess` (id = the
guest's `SubjectId`) carries the *limited* standing: the bounded window
(D3), the closed surface set (D4), and the settling admin. One **single
audited write lane** on `IIdentityService`
(`SetGuestAccessAsync(GuestAccess, string adminSubjectId)`) — the exact
shape of ADR 0050's `SetSignupOpenAsync` (load-or-create, store, one
`AccessAudit` row `Via = Admin` in the same session) — plus a plain read
(`GetGuestAccessAsync`) the claim mint (U02) and the admin surface (U04)
ride. The Web boundary owns the GlobalAdmin standing check; the Core seam
does not re-check `User`.

*Forbids:* a free-form string list for `AllowedSurfaces` (it must be a closed
enum so a typo cannot silently grant a surface), a second write lane, or an
unaudited direct `IDocumentSession.Store` from a controller.

**D3 — "When" is a bounded window on the allowance, evaluated per request.**
The standing is live only while `ValidFrom ≤ now < ValidUntil`. A guest
outside that window has **no standing** (the factory mints no `Guest` role —
see D5), exactly as an unverified account is denied `Member`. No expiry job,
no projection: the window is read at the Identity↔cookie seam (the
`KumunitaClaimsPrincipalFactory`), the same place `Verified`/`Blocked` are
consulted. This is the "decision-needed" half the roadmap names — "and
when" — and it is settled by *data*, not config, and not a background job
(the ADR 0050 "data, not config" posture).

*Forbids:* a background "expire the guest" job, a projection that lags the
window, or an unbounded guest standing (no `ValidUntil`).

**D4 — The allowed surface set is a closed enum (the floor is empty).** A
guest may be allowed onto a **closed, enumerated** set of read surfaces; each
surface is a distinct value, an admin composes a set, and a *typo or a wrong
value grants nothing*. The floor (empty set) is the least-privileged default
— a guest account with no surfaces is a signed-in shell, not an error.

```csharp
public enum GuestSurface
{
    None = 0,
    Announcements = 1,   // see the community's public announcement feed (read)
    Events = 2,          // see public events (the M4 events read surface, read)
    Directory = 4,       // see public directory basic info (name + verified badge, read)
}
```

*Forbids:* free-form surface strings, a boolean-per-surface field on `Profile`
(scattered, hard to audit as one settled set), or granting `Member` to make a
surface "just work."

**D5 — The guest standing is a claim minted at the Identity↔cookie seam,
riding the frozen authorization.** `Roles.Guest = "Guest"` is added to the
`Roles` static class (the ADR 0030 composable-role / `AccessVia` append
precedent — a new claim string, the existing claim set untouched in shape;
the `ClaimTypes.All` set is **untouched** — a guest's `Guest` role rides the
existing `Kumunita.Role` claim *type*, a value not a new type).
`KumunitaClaimsPrincipalFactory` mints `Guest` **iff** the account is a
guest (`Profile.IsGuest`), not blocked, and the allowance window is live
(D3). A guest **does not** get `Member` (D4 floor) and **does not** get any
community standing. The frozen `IAuthorizationService` seams are *untouched*:
the guest's "what may I see" is answered per request by the same
`CanAsync`/`CanSeeAsync` calls a resident uses — the guest simply carries a
different standing (no `Member`, the `Guest` claim + the allowance set) into
those decisions.

*Forbids:* a new branch in `Decide()` for guests, an extended `CanAsync`
signature, or encoding guest standing in a relational table — the claim rides
the existing thin-token / fat-authorization seam exactly as `Verified`/`Blocked`
do today.

**D6 — The admin surface is `/admin/guests`, GlobalAdmin-gated, the ADR 0050
precedent.** A dedicated `AdminGuestsController` — **not** a new action on
`AdminController` — mirroring `AdminSignupController` (ADR 0050):
`[Route("admin/guests")]`, `[Authorize(Roles = Roles.GlobalAdmin)]`, a thin
list / create / set-window / set-surfaces surface over
`IIdentityService.SetGuestAccessAsync`. The gate is checked **first** on both
GET and POST (the ADR 0050 "gate is authoritative on both the read and write
surface" rule). Affordances on `/admin` index are **hidden-not-disabled** for
non-GlobalAdmin actors.

*Forbids:* a new action on the fat `AdminController`, a self-service guest
creation path, or an admin surface that writes the allowance without a
GlobalAdmin standing check (the Web boundary owns that check).

**D7 — Deferred lanes (each a future ADR, not part of M19).**

1. **Per-guest content authors** (a guest posting into a group / an event
   note) — M19 is *read-only standing*; a guest authoring UGC is a later
   design (it touches the author-only edit lanes ADR 0014/0016/0017 and the
   translation lanes, and needs its own access model).
2. **Guest invitations** (a resident invites a guest, the guest self-serves a
   password from an admin-sent link) — the admin-creates path (D6) is M19; the
   resident-issues-a-token path is the ADR 0050 invitation-mechanism follow-on
   applied to guests.
3. **Per-surface fine-grained control** (a guest sees *some* announcements
   but not *some* events) — M19's `AllowedSurfaces` is a coarse, closed set
   (D4); per-resource grants ride the existing audience machinery and are a
   later design.
4. **Guest notification / messaging** — a guest does not enter the M6
   notifications or M9 messaging surface in M19.
5. **Guest analytics / audit depth** — the guest's actions append the same
   `AccessAudit` rows as any actor (C-M19·5), but a dedicated guest-usage
   report is a later lane (the M13 analytics surface is resident-shaped).

*Forbids:* a unit implementing any of these inside an M19 unit.

### 1.a — Amendments

- **2026-09-30 — D2 `AllowedSurfaces` shape annotation: array → `[Flags]`
  scalar.** The register's D2 prose block showed `GuestSurface[] AllowedSurfaces`
  for readability. The `GuestSurface` enum is declared `[Flags]` (a closed
  bit-flag union — D4's "closed, enumerated set… a typo or a wrong value
  grants nothing" is the canonical C# idiom for a composed set), so the
  idiomatic shape is a **single scalar** `GuestSurface AllowedSurfaces`
  (defaulting to `GuestSurface.None`, the empty floor), not an array. U00
  pins the **scalar** form in §4 and §9 (the authoritative C#); U01/U02/U04
  all implement the scalar form. **The D4 *decision* is unchanged** — the
  set is still closed, enumerated, with an empty floor, and a new surface is
  still a new enum value (an additive append, never a free-form string). This
  is a representation annotation only; a later unit may not re-open it.

## 2 — Invariants (C-M19·1 … C-M19·6)

**C-M19·1 (the guest is a standing, not an entity).** A guest is a real
Identity account + `Profile.IsGuest` + a `GuestAccess` document (D1/D2).
There is no `GuestProfile`, no parallel store, no new bounded context. A test
asserts the guest resolves through the same `IIdentityService` /
`IAuthorizationService` seams a resident does.

**C-M19·2 (the guest never holds `Member`).** A guest's role set contains
`Guest` and (if elevated, which M19 does not grant) nothing else. A test
asserts a live guest's minted claims contain `Guest` and **not** `Member`,
and that the standing is *limited* to the `AllowedSurfaces` set (D4 floor).

**C-M19·3 ("when" is a live window, not a job).** The standing is live iff
`ValidFrom ≤ now < ValidUntil` (D3). A test asserts a guest outside the
window mints no `Guest` claim (no standing), and there is **no** background
expiry job.

**C-M19·4 (closed surface set, empty floor).** `AllowedSurfaces` is a closed
`GuestSurface` set (D4); an unknown/typo value grants nothing; the empty set
is a valid, signed-in shell. A test asserts an empty-set guest sees no gated
content.

**C-M19·5 (every guest action is audited, same as any actor).** The
allowance write lane appends exactly one `AccessAudit` row (`Via = Admin`,
D2); the guest's content reads append the same `AccessAudit` rows a resident's
reads do (the frozen seams, untouched). No unaudited access (ADR 0006-E).

**C-M19·6 (zero new authorization surface).** M19 adds no branch to
`Decide()`, no new `IAuthorizationService` signature, and no relational
standing table. The guest standing rides the existing thin-token /
fat-authorization seam (D5, ADR 0001-B / ADR 0006). A claim-shape pin test
asserts the only new claim string is `Kumunita.Role` = `"Guest"` (the
`ClaimTypes.All` set is unchanged).

## 3 — FACES (F1–F5) + the named trade

**F1 — The admin creates a guest and settles the standing** (D2, D6): pick a
surface set (D4), set the window (D3), one audited write.

**F2 — The guest signs in to a limited shell** (D5): a live guest sees only
the allowed surfaces; an expired guest sees nothing (no standing).

**F3 — The standing is time-bounded and data-settled** (D3): "and when" is a
window on the allowance, changed by an admin, live on the next request — no
restart, no job.

**F4 — The guest is auditable like any actor** (C-M19·5): the allowance write
and the guest's reads both append `AccessAudit` rows.

**F5 — A guest is outside the resident circle** (C-M19·2): no `Member`, no
community standing, no UGC authoring in M19 (D7 lane 1).

**The named trade (U00 records this in the design doc).** M19 buys a
*limited, time-bounded, admin-settled* guest standing with **zero new
authorization surface** and **one additive `Profile` flag + one small
document + one enum + one claim**, in exchange for a **coarse, closed surface
set** (D4) — a guest gets "the announcements surface" or "the events
surface," not "announcement #42 but not #47." The per-resource fine-grain
(D7 lane 3) and the guest-as-author (D7 lane 1) are deliberately *not* in
M19: they are larger designs that need their own ADRs, and M19's value (a
consultant/coach/teacher who can *see* the feed they're engaged with, for a
bounded window, with nothing to lose if they're not) is delivered by the
closed set.

## 4 — The `GuestSurface` enum + the `GuestAccess` document (exact C#)

The exact C# for U01 to implement (the §9 block is the authoritative pin).
Namespace `Kumunita.Core.Identity`.

```csharp
namespace Kumunita.Core.Identity;

/// <summary>
/// M19 (ADR 0120, D4) — the closed, enumerated set of surfaces a guest may
/// be allowed onto. The floor is the empty set (a guest with no surfaces
/// signs in to a shell with no content). Additive: a new surface is a new
/// enum value (the ADR 0030 composable-role / the AccessVia append
/// precedent), never a free-form string. A typo or a wrong value grants
/// nothing — the set is a closed union.
/// </summary>
[Flags]
public enum GuestSurface
{
    /// <summary>No surfaces (the floor; a guest with this set signs in to a shell).</summary>
    None = 0,
    /// <summary>See the community's public announcement feed (read).</summary>
    Announcements = 1,
    /// <summary>See public events (the M4 events read surface, read).</summary>
    Events = 2,
    /// <summary>See public directory basic info (name + verified badge, read).</summary>
    Directory = 4,
}

/// <summary>
/// M19 (ADR 0120, D2) — the admin-settled, time-bounded guest standing.
/// One document per guest (id = the guest's <see cref="SubjectId"/>). The
/// standing is live only while <c>ValidFrom ≤ now &lt; ValidUntil</c> (D3);
/// a guest outside the window has no standing (no <c>Guest</c> claim —
/// U02's mint, C-M19·3). <see cref="AllowedSurfaces"/> is the closed,
/// enumerated surface set (D4) — the floor is <see cref="GuestSurface.None"/>
/// (an empty set is a valid, least-privileged state, not an error —
/// C-M19·4).
/// </summary>
public sealed class GuestAccess
{
    /// <summary>Document identity — the guest's subject id (one allowance per
    /// guest account; the same id as <see cref="Profile.SubjectId"/>).</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// The bounded window start (D3, "when"). The standing is live from this
    /// instant (inclusive). Set by an admin (D6), not by the guest.
    /// </summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>
    /// The bounded window end (D3, "when"). The standing is live **until**
    /// this instant (exclusive: <c>now &lt; ValidUntil</c>). A guest at or
    /// past this instant has no standing. Never unbounded (no <c>null</c> —
    /// C-M19·3 forbids an unbounded guest standing).
    /// </summary>
    public DateTimeOffset ValidUntil { get; set; }

    /// <summary>
    /// The closed, enumerated surface set the guest may access (D4). The
    /// floor is <see cref="GuestSurface.None"/> (an empty set). A typo or an
    /// unknown value grants nothing. Additive: a new surface is a new
    /// <see cref="GuestSurface"/> value, never a string (the ADR 0030
    /// append precedent).
    /// </summary>
    public GuestSurface AllowedSurfaces { get; set; } = GuestSurface.None;

    /// <summary>
    /// The admin subject id that settled this standing (recorded for audit;
    /// the <see cref="AccessAudit"/> row's <c>ActorId</c> is the same value
    /// at write time).
    /// </summary>
    public string SetByAdmin { get; set; } = string.Empty;
}
```

**The `M1DocTypes` registration** (U01 — the `GuestAccess` document is
Marten-native with a conventional `SubjectId` identity, like the other M1
POCOs; the additive-surface delta is applied idempotently at boot — ADR
0004 §B.1, zero migration):

```csharp
// In M1DocTypes.Configure(StoreOptions opts), alongside the other Identity
// POCOs — a conventional identity (Marten's default) — so one line:
opts.Schema.For<Identity.GuestAccess>().Identity(g => g.SubjectId);
```

## 5 — The `Profile.IsGuest` additive flag + the `IIdentityService` seam (exact C#)

The additive `Profile` field (D1 — the `Blocked`/`Verified` additive-field
precedent, ADR 0004 §B.1). U01 places it near `Blocked`/`Verified`:

```csharp
// In Kumunita.Core/Identity/ (UserInfo) Profile.cs, near Blocked/Verified:

/// <summary>
/// M19 (ADR 0120, D1) — the guest standing flag. <c>true</c> means this
/// account is a guest (a limited-privilege, outside-the-resident-circle
/// account — a consultant/coach/teacher/speaker/entertainer). A guest is
/// still a real Identity account (real <c>User</c>, real <c>Profile</c>,
/// real <c>SubjectId</c>) so the frozen authorization seams and the audit
/// log treat it exactly like any other actor (C-M19·1). A guest **never**
/// holds <c>Member</c> (C-M19·2) and its access is limited to the
/// <c>GuestAccess.AllowedSurfaces</c> set within the bounded window
/// (D3/D4). The flag is additive (ADR 0004 §B.1) — a <c>false</c> default
/// means every existing account is non-guest, no migration.
/// </summary>
public bool IsGuest { get; set; }
```

The exact `IIdentityService` ADD (D2 — the single audited write lane + the
plain read), in the **`IsSignupOpenAsync` / `SetSignupOpenAsync`** shape
(ADR 0050). U01 adds **after** the `SetNotifyAdminsOnSignupAsync` member,
under a new section header:

```csharp
// In IIdentityService.cs, after SetNotifyAdminsOnSignupAsync:

// ── M19 guest standing (ADR 0120, D2 — the single audited write lane) ────

/// <summary>
/// The guest standing for a subject (ADR 0120, D2): the bounded window
/// (D3) + the closed surface set (D4). A **read** (no audit row — the
/// <c>IsSignupOpenAsync</c> plain-read shape). Returns <c>null</c> when the
/// account has no settled standing yet — a guest with no allowance is a
/// shell (C-M19·4, the empty floor). U02's claim mint and U04's admin
/// surface both ride this read.
/// </summary>
Task<GuestAccess?> GetGuestAccessAsync(string subjectId);

/// <summary>
/// Set the guest standing for a subject (ADR 0120, D2): a GlobalAdmin
/// settles the bounded window (D3) + the closed surface set (D4) + marks
/// the account <see cref="Kumunita.Core.UserInfo.Profile.IsGuest"/>. Writes
/// the <see cref="GuestAccess"/> document and appends exactly one
/// <c>AccessAudit</c> row (<c>via: Admin</c>, action
/// <c>"guest.set-standing"</c>, <c>TargetKind</c> "guest",
/// <c>TargetId</c> "guest:{subjectId}") in the same session (C-M19·5 — no
/// silent, unaudited access). This is the **single** write lane: the Web's
/// <c>AdminGuestsController</c> (U04) enforces the GlobalAdmin standing and
/// calls only this — a controller may not <c>IDocumentSession.Store</c> the
/// document directly (C-M19·5, D2's *Forbids*).
/// </summary>
Task SetGuestAccessAsync(GuestAccess access, string adminSubjectId);
```

**The `IdentityService` implementation shape** (U01 — the exact
`SetSignupOpenAsync` body pattern: one write session, load-or-create, store
the document + **exactly one** `AccessAudit` row, commit). The
`AccessAudit` row uses the file's existing shape:

```csharp
public async Task SetGuestAccessAsync(GuestAccess access, string adminSubjectId)
{
    await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
    var ct = System.Threading.CancellationToken.None;

    // Load-or-create the allowance (the SetSignupOpenAsync load-or-create
    // shape) and settle the window + surface set + the settling admin.
    access.SubjectId = access.SubjectId;            // id = the guest's SubjectId
    access.SetByAdmin = adminSubjectId;
    session.Store(access);

    // Mark the account a guest (the D1 flag, C-M19·1) in the same session.
    var profile = await session.LoadAsync<Kumunita.Core.UserInfo.Profile>(
        access.SubjectId, ct).ConfigureAwait(false);
    if (profile is not null)
    {
        profile.IsGuest = true;
        session.Store(profile);
    }

    // Exactly one audit row (C-M19·5, D2) — the singleton-toggle shape
    // (the signup.set-open precedent).
    session.Store(new Authorization.AccessAudit
    {
        Id = Guid.NewGuid().ToString("N"),
        At = DateTimeOffset.UtcNow,
        ActorId = adminSubjectId,
        EffectivePrincipalId = adminSubjectId,
        Action = "guest.set-standing",
        TargetKind = "guest",
        TargetId = $"guest:{access.SubjectId}",
        Via = Authorization.AccessVia.Admin,
        Outcome = Authorization.AccessOutcome.Allow
    });

    await session.SaveChangesAsync(ct).ConfigureAwait(false);
}

public async Task<GuestAccess?> GetGuestAccessAsync(string subjectId)
{
    using var session = documentStore.QuerySession();
    // The C-M19·4 empty floor: null is a valid state (a guest with no
    // settled standing), not an error.
    return await session.LoadAsync<GuestAccess>(subjectId, CancellationToken.None);
}
```

**The two pinned test names from GATE-3** (U01's deliverable; the §10 block
is the authoritative pin):

1. `SetGuestAccess_Stores_One_Document_And_One_Audit_Row` (GATE-3) — one
   `SetGuestAccessAsync` call stores the `GuestAccess` doc, sets
   `Profile.IsGuest = true`, and appends **exactly one** `AccessAudit` row
   with `Via = Admin` + `Action = "guest.set-standing"`.
2. `GetGuestAccess_With_No_Allowance_Returns_Null` (C-M19·4 / GATE-2 floor) —
   the read seam returns `null` for a subject with no settled standing (the
   empty floor is a valid state, not an error).

## 6 — The `Roles.Guest` claim mint at the seam (exact C# diff)

The exact `Roles` addition (D5 — the ADR 0030 composable-role append
precedent: a new claim string, the `ClaimTypes.All` set **untouched**). U02
adds it in `ThinPrincipal.cs`, after `Translator`:

```csharp
// In Kumunita.Core/Identity/ThinPrincipal.cs, the Roles static class:

/// <summary>
/// M19 (ADR 0120, D5) — the guest standing: a limited-privilege,
/// time-bounded account **outside** the resident circle (a
/// consultant/coach/teacher/speaker/entertainer). A guest **never** holds
/// <see cref="Member"/> (C-M19·2); its access is limited to the
/// <see cref="GuestAccess.AllowedSurfaces"/> set within the bounded window
/// (D3/D4). The claim rides the frozen authorization seams (C-M19·6) — the
/// only new claim string is this one; the <see cref="ClaimTypes.All"/> set
/// is unchanged. Minted at the Identity↔cookie seam
/// (<see cref="KumunitaClaimsPrincipalFactory"/>) iff the account is a
/// guest, not blocked, and the allowance window is live (C-M19·3).
/// </summary>
public const string Guest = "Guest";
```

> **The `ClaimTypes.All` set is NOT modified.** A guest's `Guest` role is
> carried in the existing `Kumunita.Role` claim type (a role string value),
> exactly as `Member` / `GlobalAdmin` are. C-M19·6's claim-shape pin asserts
> `ClaimTypes.All` is unchanged — do not add a new claim *type*.

The exact `KumunitaClaimsPrincipalFactory` branch (D5 / C-M19·3 — mint `Guest`
iff `IsGuest` && not blocked && window live). U02 adds the `IIdentityService`
dependency to the factory constructor and the branch to
`BuildRoleListAsync` (the exact `Verified → Member` precedent — the guest
branch is **independent** of the `verified` branch):

```csharp
// KumunitaClaimsPrincipalFactory — constructor gains IIdentityService:
public sealed class KumunitaClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IUserInfoService userInfo,
    Kumunita.Core.Identity.IIdentityService identity,   // NEW (M19, D5)
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, IdentityRole>(userManager, roleManager, options)
{
    // …CreateAsync unchanged except BuildRoleListAsync below gains the guest
    //    branch, and it is called with the profile (so the guest branch can
    //    short-circuit on Profile.IsGuest without an extra mt read for
    //    non-guests — C-M19·6's "zero new reads on the non-guest path").
}

// BuildRoleListAsync — the guest branch (D5 / C-M19·3):
private async Task<IReadOnlyList<string>> BuildRoleListAsync(
    User user, bool verified, Profile? profile)
{
    var userRoleNames = (await UserManager.GetRolesAsync(user)).ToList();

    var roles = new List<string>();
    if (verified)
        roles.Add(Roles.Member);          // Member = verified-resident standing

    // M19 (ADR 0120, D5/C-M19·3) — the guest standing. Minted iff the account
    // is a guest (Profile.IsGuest), not blocked, and the allowance window is
    // live (ValidFrom ≤ now < ValidUntil). A guest OUTSIDE the window mints
    // NO Guest claim (no standing — C-M19·3). A guest NEVER gets Member
    // (C-M19·2) — the guest is outside the resident circle. The window read
    // rides U01's GetGuestAccessAsync seam (one extra mt read, **only** when
    // the account is a guest — non-guests short-circuit before the read).
    if (profile?.IsGuest == true)
    {
        var guestAccess = await identity.GetGuestAccessAsync(user.Id ?? string.Empty);
        var now = DateTimeOffset.UtcNow;
        if (guestAccess is not null
            && now >= guestAccess.ValidFrom
            && now < guestAccess.ValidUntil)
        {
            roles.Add(Roles.Guest);
        }
    }

    roles.AddRange(userRoleNames);        // explicit Identity roles (GlobalAdmin, Moderator, …)

    if (userRoleNames.Contains(Roles.Moderator))
    {
        var assignments = await userInfo.GetAssignmentsAsync(user.Id ?? string.Empty);
        foreach (var a in assignments)
            roles.Add(Roles.ModeratorComponent(a.ComponentId));
    }

    return roles;
}
```

> **Placement + short-circuit:** the `Guest` mint is **independent** of the
> `verified` branch (a guest does not need `Member`; a verified resident does
> not need `Guest`). The guest branch reads the window **only** when
> `Profile.IsGuest == true` (non-guests short-circuit — no extra mt read on
> the resident path, C-M19·6). The blocked account already mints **no**
> roles (the existing `blocked → Array.Empty<string>()` branch in
> `CreateAsync` runs first — the guest branch is unreachable for a blocked
> account, so C-M19·3's "not blocked" holds). The `now` is
> `DateTimeOffset.UtcNow` — the window is evaluated **per request** at the
> seam (D3, no job, no projection).

**The two pinned test names from GATE-1 + GATE-4** (U02's deliverable; the
§10 block is the authoritative pin):

1. `Guest_Within_Window_Mints_Guest_Not_Member` (GATE-1, C-M19·2/·3) — a
   live guest (window live, `IsGuest = true`, not blocked) mints `Guest` and
   **not** `Member`.
2. `Guest_Outside_Window_Mints_No_Guest` (GATE-1, C-M19·3) — an expired guest
   (window lapsed) mints **no** `Guest` claim (no standing).
3. `Claim_Shape_Pin_Only_New_Claim_String_Is_Guest` (GATE-4, C-M19·6) — assert
   `ClaimTypes.All` is unchanged (the four frozen claim types), and the only
   new role string minted for a guest is `"Guest"` (a role *value*, not a new
   claim *type*).

## 7 — The `/admin/guests` admin surface (F1, D6)

The exact controller actions (U04), mirroring `AdminSignupController` (ADR
0050) — the **exact template**. A dedicated controller: the `AdminController`
constructor is pinned by two Web-layer test harnesses
(`AdminControllerBlockTests` / `AdminControllerMandatoryTests`), so a new
dependency there would break them (the ADR 0050 dedicated-controller
rationale).

```csharp
using Kumunita.Core.Identity;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/guests</c> surface (ADR 0120, D6) — the GlobalAdmin's
/// control plane over the guest standing: settle the bounded window (D3) +
/// the closed surface set (D4) for a guest account. Mirrors
/// <see cref="AdminSignupController"/> (ADR 0050) exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated, a thin wrapper over the <b>one</b>
/// matching <see cref="IIdentityService"/> seam (the
/// <see cref="IIdentityService.SetGuestAccessAsync"/> /
/// <see cref="IIdentityService.GetGuestAccessAsync"/> pair), and the audit
/// row is the **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>,
/// action <c>guest.set-standing</c> — the C-M19·5 single + audited write
/// lane). A dedicated controller: the <c>AdminController</c> constructor is
/// pinned by two Web-layer test harnesses, so a new dependency there would
/// break them (the ADR 0050 dedicated-controller rationale).
/// </summary>
[Route("admin/guests")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminGuestsController(IIdentityService identity) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/guests</c> — the guest standing control plane. Seeds the
    /// form with the current standing (<see cref="IIdentityService.GetGuestAccessAsync"/>
    /// — the C-M19·4 empty floor: a guest with no settled standing renders the
    /// empty set, not an error).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? subjectId)
    {
        // The form model (window + surface set) per §9.7 — a guest with no
        // settled standing renders the empty set (C-M19·4, the empty floor).
        var model = new GuestAdminViewModel { SubjectId = subjectId ?? string.Empty };
        if (subjectId is not null)
            model.Seed(await identity.GetGuestAccessAsync(subjectId));
        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/guests</c> — settles the standing. Delegates to
    /// <see cref="IIdentityService.SetGuestAccessAsync"/> (the single audited
    /// write lane — exactly one <c>AccessAudit</c> row, <c>Via = Admin</c>,
    /// C-M19·5). Success → a surfaced <c>TempData["info"]</c>
    /// (<c>admin.guests_saved</c>, the U03 key) + redirect (the change is
    /// live on the very next <c>GetGuestAccessAsync</c> / render — data, not
    /// config, D3).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string subjectId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        bool announcements,
        bool events,
        bool directory)
    {
        // Validate the form (window non-empty, ValidUntil > ValidFrom) with
        // the existing composer's validation shape.
        if (validUntil <= validFrom)
        {
            // … the existing validation-error shape (the ADR 0050 Save shape) …
            return BadRequest();
        }

        // The closed surface set (D4) — compose the [Flags] value from the
        // checkboxes; unchecked = absent (the empty floor is the default).
        var surfaces = GuestSurface.None;
        if (announcements) surfaces |= GuestSurface.Announcements;
        if (events)        surfaces |= GuestSurface.Events;
        if (directory)     surfaces |= GuestSurface.Directory;

        // Delegate to the SINGLE audited write lane (C-M19·5) — the
        // controller does NOT IDocumentSession the document itself.
        var actor = ActorId(User) ?? string.Empty;
        await identity.SetGuestAccessAsync(
            new GuestAccess
            {
                SubjectId = subjectId,
                ValidFrom = validFrom,
                ValidUntil = validUntil,
                AllowedSurfaces = surfaces,
            },
            actor);

        TempData["info"] = "Guest standing saved.";   // the U03 admin.guests_saved key
        return RedirectToAction(nameof(Index), new { subjectId });
    }
}
```

> **The gate (D6, C-M19·5):** the controller does **not** `IDocumentSession`
> — it delegates the write to `SetGuestAccessAsync` (U01's single audited
> lane). The GlobalAdmin standing is enforced by
> `[Authorize(Roles = Roles.GlobalAdmin)]` at the boundary (the Web owns the
> standing check; the Core seam does not re-check `User` — the ADR 0050
> split). The `/admin` index affordance is **hidden-not-disabled**: the
> `admin.guests_title` link renders **only** for GlobalAdmin actors (a
> non-GlobalAdmin actor's `/admin` index renders no guest link — the ADR 0050
> "affordances hidden, not just gated" rule).
>
> **The `kw-l` keys the surface consumes (U03's — §8 is the authoritative
> list):** `admin.guests_title` (the `/admin` index nav item),
> `admin.guests_empty` (the empty list state), `admin.guests_create` (the
> create button), `admin.guests_window_label` (the "Access window" label,
> D3), `admin.guests_surfaces_label` (the "Allowed surfaces" label, D4),
> `admin.guests_surface_announcements` / `_events` / `_directory` (the three
> surface checkbox labels, D4), `admin.guests_saved` (the `TempData["info"]`
> after a save, F1). **The guest shell** (F2/F5) consumes
> `account.guest_welcome` (the signed-in guest's greeting / shell notice) —
> a signed-in guest with a live window renders that notice; an expired guest
> (no `Guest` claim) sees nothing (no standing — C-M19·3).

## 8 — `kw-l` key list (the closed 10-key set × en/de/fr/da)

The exact 10 keys (D6's admin surface + F2/F5's guest-shell notice). Added to
**all four** of the closed dictionaries in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `EnValues` (the
`en` floor, the canonical source text), `DeValues`, `FrValues`, `DaValues`
(the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder materializes each
key's `en` row on first boot; the non-`en` rows are the localized text).

| # | Key | Surface | One-line description |
|---|---|---|---|
| 1 | `admin.guests_title` | `/admin` index | The admin index nav item ("Guest accounts"). |
| 2 | `admin.guests_empty` | `/admin/guests` | The empty list state ("No guest accounts yet."). |
| 3 | `admin.guests_create` | `/admin/guests` | The create-guest button label. |
| 4 | `admin.guests_window_label` | `/admin/guests` | The "Access window" field label (D3). |
| 5 | `admin.guests_surfaces_label` | `/admin/guests` | The "Allowed surfaces" field label (D4). |
| 6 | `admin.guests_surface_announcements` | `/admin/guests` | The `Announcements` surface checkbox label (D4). |
| 7 | `admin.guests_surface_events` | `/admin/guests` | The `Events` surface checkbox label (D4). |
| 8 | `admin.guests_surface_directory` | `/admin/guests` | The `Directory` surface checkbox label (D4). |
| 9 | `admin.guests_saved` | `/admin/guests` | The `TempData["info"]` after a save (F1). |
| 10 | `account.guest_welcome` | guest shell | The signed-in guest's greeting / shell notice (F2/F5). |

**The closed set is 10 keys × 4 languages = 40 strings.** The
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` enforce the
4-language set + non-empty values; the ADR 0015 / 0052 warm-boot backfill
seeds them idempotently. U03 authors them; U04 **consumes** them (U04 does
not add a key).

## 9 — `Seams & contracts (Part 2)` (exact C#)

The exact C# for the `GuestAccess` document + the `Profile.IsGuest` flag +
the `IIdentityService` ADD (U01), the `Roles.Guest` constant + the factory
branch (U02), and the controller actions (U04). **This is the section a later
unit reads to implement; it is the authoritative C#** (the prose sections
above are the explanation).

### 9.1 The `GuestSurface` enum + `GuestAccess` document (U01)

See §4 (the exact C# is verbatim above — the `[Flags]` `GuestSurface` enum
(`None = 0`, `Announcements = 1`, `Events = 2`, `Directory = 4`), the
`GuestAccess` document (`SubjectId`, `ValidFrom`, `ValidUntil`,
`AllowedSurfaces` (the `[Flags]` **scalar** — §1.a amendment), `SetByAdmin`),
and the `M1DocTypes` `Identity.GuestAccess` registration).

### 9.2 The `Profile.IsGuest` additive flag (U01)

See §5 (the exact `Profile` field is verbatim above — the additive `bool
IsGuest`, the `Blocked`/`Verified` precedent, ADR 0004 §B.1).

### 9.3 The `IIdentityService` ADD (U01)

See §5 (the exact C# for `GetGuestAccessAsync` + `SetGuestAccessAsync` is
verbatim above — the `IsSignupOpenAsync`/`SetSignupOpenAsync` shape, the
`AccessAudit` row shape, the `IdentityService` body, the two pinned GATE-3 /
C-M19·4 test names).

### 9.4 The `Roles.Guest` constant (U02)

See §6 (the exact `Roles` addition is verbatim above — the `Guest = "Guest"`
constant, the `ClaimTypes.All` set **untouched**).

### 9.5 The `KumunitaClaimsPrincipalFactory` branch (U02)

See §6 (the exact factory diff is verbatim above — the new
`IIdentityService` constructor dependency, the `BuildRoleListAsync(User, bool
verified, Profile? profile)` signature change, the guest branch that mints
`Guest` iff `IsGuest` && not blocked && window live, the non-guest
short-circuit, the three pinned GATE-1 / GATE-4 test names).

### 9.6 The 10 `kw-l` keys (U03)

See §8 (the exact 10-key table is verbatim above — U03 adds them to all four
of `EnValues` / `DeValues` / `FrValues` / `DaValues`; the
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pins close
the set).

### 9.7 The `AdminGuestsController` actions (U04)

See §7 (the exact controller C# is verbatim above — the
`[Route("admin/guests")]` + `[Authorize(Roles = Roles.GlobalAdmin)]` surface,
the thin `Index`/`Save` pair over `SetGuestAccessAsync`, the
hidden-not-disabled `/admin` index affordance, the two pinned GATE-3 / D6
Web-layer test names).

## 10 — §gate (the four acceptance tests)

M19 is **done** when, and only when, all four of these are true (each is a
pinned test in the named unit's deliverables). These four gates are the
milestone's acceptance criterion; a unit that does not land its pinned test
is not done, and the milestone is not done until all four are green.

**GATE-1 (the standing is limited + time-bounded — U02).** A live guest
(window live, `AllowedSurfaces = [Announcements]`) mints `Guest`, **not**
`Member`, and may see the announcements read surface but **not** a
`Member`-gated surface. An expired guest (window lapsed) mints no `Guest`
claim. *(C-M19·2 / ·3.)* Pinned test names:
`Guest_Within_Window_Mints_Guest_Not_Member` +
`Guest_Outside_Window_Mints_No_Guest` (U02).

**GATE-2 (the closed surface set floors to nothing — U01).** A guest with
`AllowedSurfaces = []` signs in to a shell and sees no gated content (the
empty set is a valid, least-privileged state, not an error). *(C-M19·4.)*
Pinned test name: `GetGuestAccess_With_No_Allowance_Returns_Null` (U01).

**GATE-3 (the allowance write lane is single + audited — U01 Core + U04
Web).** One call to `SetGuestAccessAsync` stores the document and appends
exactly one `AccessAudit` row (`Via = Admin`, `Action` naming the
guest-standing write); a hand-crafted direct store from a controller is
impossible (the Web boundary owns the GlobalAdmin check; the seam is the only
write). *(C-M19·5, D2.)* Pinned test names:
`SetGuestAccess_Stores_One_Document_And_One_Audit_Row` (U01) +
`Save_Delegates_To_Single_Audited_Lane` (U04).

**GATE-4 (zero new authorization surface — U02).** The claim-shape pin test
passes: the only new claim string is `Kumunita.Role` = `"Guest"`; the
`ClaimTypes.All` set is unchanged; `Decide()` has no guest branch.
*(C-M19·6, D5.)* Pinned test name:
`Claim_Shape_Pin_Only_New_Claim_String_Is_Guest` (U02).

## 11 — §drift-guard

A **drift event** is any of: a unit changing a D# / invariant / FACES /
key list in the design doc or ADR outside U00; a unit adding a file outside
its own Deliverables; a unit extending a frozen seam (`IAuthorizationService`
/ `Decide()` / `AccessAction` / `AccessVia` / the frozen `IIdentityService`
principal reads); a unit adding a `GuestProfile` doc, a parallel account
store, a new bounded context for guests, a relational standing table, a
background expiry job, a free-form surface string, a boolean-per-surface
`Profile` field, granting `Member` to a guest, a self-service guest
creation path, or a new `kw-l` namespace. On a drift event the unit **stops**,
appends a `## U# — DRIFT` section to the handoff notes describing the
conflict, and **does not proceed** until the design doc is amended (by U00 or
a designated design-owner) and the ADR is re-accepted. The ADR is the
arbiter; the design doc is the lock; the unit plan is the assignment.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not
re-derive what an earlier unit already settled (a D# amendment, a key set, a
seam shape).

**The register is the map, not the code.** If a unit is tempted to "just add
a field to `Decide()`" or "make the guest a `Member`," that is the
§drift-guard firing — M19's value is precisely that it does *not*.

## 12 — §deferred (the D7 lanes)

The five deferred lanes verbatim (the register's D7), each with a one-line
"why it is deferred":

1. **Per-guest content authors** (a guest posting into a group / an event
   note) — deferred because M19 is *read-only standing*; a guest authoring UGC
   touches the author-only edit lanes (ADR 0014/0016/0017) + the translation
   lanes and needs its own access model (a **new** `AccessAction` / a
   `Decide()` branch — the §drift-guard's "granting `Member` / a new
   `AccessAction`" pin). A follow-up lane with its own ADR.
2. **Guest invitations** (a resident invites a guest, the guest self-serves a
   password from an admin-sent link) — deferred because the admin-creates
   path (D6) is M19; the resident-issues-a-token path is the ADR 0050
   invitation-mechanism follow-on (token lifecycle, expiry, admin UX) applied
   to guests (a **new** token surface + a self-served write lane — the
   §drift-guard's "self-service guest creation path" pin). A follow-up lane
   with its own ADR.
3. **Per-surface fine-grained control** (a guest sees *some* announcements but
   not *some* events) — deferred because M19's `AllowedSurfaces` is a coarse,
   closed set (D4); per-resource grants ride the **existing** audience
   machinery (the `Post`/`Event` `Audience` + `CanAsync` seams, unchanged)
   rather than a new guest-grant surface (the §drift-guard's "per-resource
   grant" pin). A follow-up lane with its own ADR.
4. **Guest notification / messaging** (a guest entering the M6 notifications
   or M9 messaging surface) — deferred because M19 is read-only standing;
   entering the notification/messaging surface would require a **new**
   emission branch + a **new** participant surface (the §drift-guard's
   "guest-as-participant" pin). A follow-up lane with its own ADR.
5. **Guest analytics / audit depth** (a dedicated guest-usage report) —
   deferred because the guest's actions already append the same `AccessAudit`
   rows as any actor (C-M19·5); a dedicated guest-usage report is a later
   lane on the M13 analytics surface (which is resident-shaped — a **new**
   aggregation branch + a **new** admin surface). A follow-up lane with its
   own ADR.
