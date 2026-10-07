# M28 — Guardian time limits (design doc)

> **Milestone M28 — Guardian time limits.** The README M28 line, verbatim:
> "**Guardian time limits — for a child's account, a parent/guardian sets
> when the child may use the platform: allow or block certain hours of each
> day and days of the week.**" M28 is a **new lane on the GU guardian surface**
> (ADR 0028 / ADR 0038) — the **inverse of the M20 notification-quiet lane**
> (ADR 0121): M20 lets a *resident* hold their own *notification emails* during
> a window they choose (a best-effort email deferral); M28 lets a *guardian*
> gate a *child's* *whole-platform access* during a window the guardian chooses
> (a hard sign-out). It reuses the M20 **pure-evaluator shape** (the
> `QuietScheduleEvaluator.IsQuietNow(schedule, now, zone)` discipline,
> wall-clock-first per ADR 0019) but flips the **owner** (guardian, not
> resident), the **polarity** (allowed/restricted, not quiet/held), and the
> **effect** (a sign-out access gate, not an email deferral).
>
> **A lane on the frozen GU guardian surface + the frozen ADR 0019 zone
> resolution + the frozen M20 evaluator discipline.** Zero new bounded
> context, zero new `IAuthorizationService` surface, zero new `AccessAction`,
> zero new `AccessVia`, zero new adapter, zero new `Decide()` branch
> (C-M28·5, D5). The **only** schema change is **one new document**
> (`GuardianTimeLimitSchedule`, D2 — registered on the **existing `M1DocTypes`**
> GU surface, next to `GuardianLink`) **+ one new enum** (`TimeLimitMode`, D2)
> **+ three additive seams on `IUserInfoService`** (D4, ADR 0006-E compatible
> ADDs) **+ one new middleware** (`TimeLimitMiddleware`, D5 — the
> `BlockedAccountMiddleware` shape). The closed `kw-l` key set (14 keys ×
> en/de/fr/da, §2.7) + the three acceptance-gate tests over the 20-test seam list
> (§2.8) are locked in **ADR 0151 (Accepted, 2026-10-07)**.
>
> **The frozen GU standing (the `GuardianLink` active-link read), the frozen
> `BlockedAccountMiddleware` sign-out shape, and the frozen ADR 0019 zone
> resolution are byte-identical** (C-M28·5/6). The time-limit verdict is a
> **pure function** of (schedule, instant, effective zone) — not an
> authorization decision (D3). The M20 `NotificationQuietSchedule` doc, the
> `NotificationService` quiet seams, the `LocaleController` `/settings/quiet`
> surface, the `QuietScheduleEvaluator`, and the `settings.quiet.*` /
> `admin.quiet.*` kw-l keys are **all unchanged** (D9).
>
> **Status.** **LOCKED.** The decisions D1–D9, the invariants C-M28·1…C-M28·7,
> the FACES F1–F6, the §2.1 three-seam list, the §2.2–§2.5 doc + enum + evaluator +
> middleware + surface pins, the §2.6 20-test seam list, the §2.7 `kw-l` key list,
> the §2.8 gate, and the §2.9 drift-guard are locked in **ADR 0151 (Accepted,
> 2026-10-07)**. The `[PROPOSED]` set in the register
> `docs/plans-milestones/plan-m28-guardian-time-limits.md` is the locked set
> this doc restates **verbatim** (the U00 handoff entry records the lock + the
> one refinement in §1.a).
>
> **The one thing every unit must respect:** M28 **gates the whole platform as
> a sign-out, not a 403** (D5, C-M28·1) and **adds zero new authorization
> surface** (D3, C-M28·5). The time-limit verdict is a pure function (D3) —
> the frozen `IUserInfoService` GU standing read, the frozen
> `BlockedAccountMiddleware` sign-out shape, and the frozen ADR 0019 zone
> resolution are the **only** seams touched (three *additive* seams on
> `IUserInfoService` + one *additive* doc on `M1DocTypes` + one *additive*
> middleware + one `?error=time-limit` login landing). It does **not** add a
> branch to `Decide()`, a new `IAuthorizationService` signature, a new
> `AccessAction`, a new `AccessVia`, a new adapter, or a relational time-limit
> table (C-M28·5, D1–D7, the §2.9 drift-guard below). The M20 lane is **untouched**
> (D9).

## Context

The GU guardian lane (ADR 0028) shipped **account-level** supervision of a
child's account — suspend/lock (`Profile.Blocked`, the `BlockedAccountMiddleware`),
curate community/group membership, approve a group invitation, dissolve the link,
and (ADR 0038/0144) gate event RSVPs — but that supervision is **binary**: a
child is either fully blocked or fully open. There is no *time* dimension. A
parent who wants "no platform from 22:00 to 07:00" has no path short of
suspending the account — the wrong tool (a total block, not a windowed one, and
it reads as a punishment in the audit log).

M28 adds the **time dimension at the guardian layer**, as the **inverse of the
M20 notification-quiet lane** (ADR 0121): M20 lets a *resident* hold their own
*notification emails* during a window (a best-effort email deferral); M28 lets a
*guardian* gate a *child's* *whole-platform access* during a window (a hard
sign-out). It reuses the M20 **pure-evaluator shape** (the
`QuietScheduleEvaluator.IsQuietNow(schedule, now, zone)` discipline,
wall-clock-first per ADR 0019) but flips the **owner** (guardian, not resident),
the **polarity** (allowed/restricted, not quiet/held), and the **effect** (a
sign-out access gate, not an email deferral). The scope is the **whole platform,
not a feature** (D1).

## 0 — Scope & non-scope

**What M28 does (register's "Understanding", verbatim):** M28 = **guardian
time limits** — for a child's account, a parent/guardian sets when the child
may use the platform: allow or block certain hours of each day and days of the
week. It is the **time dimension at the guardian layer** of the GU lane: today
a GU supervisor can suspend a child's account (the `Profile.Blocked` flag, the
`BlockedAccountMiddleware`), curate memberships, approve a group invitation,
and dissolve the link — but that supervision is **binary** (fully blocked or
fully open). M28 adds a **windowed** layer: a per-child schedule (a `Blocked`
window or an `Allowed` window over hours-of-day × days-of-week, evaluated in
the **child's** effective zone, ADR 0019) that, when the child is **outside the
allowed window**, **signs the child out** and lands them on a distinct
"you're outside your allowed hours" login message (the `BlockedAccountMiddleware`
shape, the `?error=blocked` landing pattern, but a distinct
`?error=time-limit` code + message).

Three things follow:

1. **The time limit is *per-child*, set by the *guardian*.** One row per child
   (id = `ChildId`, the M20 `NotificationQuietSchedule` "one row per subject"
   shape, ADR 0121 D2, with `ChildId` in place of `RecipientId`). A child with
   two guardians shares **one** schedule (the `GuardianLink` multi-guardian
   shape — the second guardian sets/clears the **same** row via the same
   guardian-gated seam; there is no per-guardian schedule). The **child cannot
   set or clear their own** — that is the whole point of the control (C-M28·7).
2. **"Allow or block certain hours of each day and days of the week"** = a
   **schedule** with a *mode* (block-list vs allow-list) over two independent
   axes (a set of hours of day ∪ a set of days of week), the exact
   `TimeLimitMode`/`Hours`/`DaysOfWeek` shape of the M20 doc (D2, D3). The
   floor is **no schedule / a disabled schedule = never restricted** (C-M28·3).
3. **The effect is a sign-out gate, not a 403 and not a 404** (C-M28·1): the
   `TimeLimitMiddleware` (the `BlockedAccountMiddleware` analog) runs on every
   authenticated request, **after** `BlockedAccountMiddleware` (a
   fully-blocked account hits the `blocked` landing first); when the child is
   restricted it **signs out** (clears both cookies via
   `SignInManager.SignOutAsync`, exactly as `BlockedAccountMiddleware`) and
   lands the child on the login page with `?error=time-limit`.

**What M28 is NOT (register's D1 out-of-scope set + D9):**

- **Not a *resident* self-service time limit.** A non-guardian version (a
  resident gating their own access) is the **inverse** of this lane — a new,
  ADR-gated lane (D9·1), not a toggle.
- **Not per-feature / per-content.** This is a **whole-platform** gate
  (browse, post, message, RSVP — all of it), not "allowed to browse but not
  message." Per-feature / per-content time limits are a new, ADR-gated lane
  (D9·2/D9·5).
- **Not an awareness / reminder lane.** M28 is **enforcement** (a sign-out),
  not a "your parent has enabled time limits" notification. An awareness/reminder
  lane is new, ADR-gated (D9·3).
- **Not enforcement-history auditing.** The middleware is silent (no per-request
  deny log). Emitting a `UsageEvent` for denied access is a future lane (D9·4,
  the M13 ADR 0114 capture shape).
- **Not a new authorization surface.** M28 rides the frozen GU standing read +
  the frozen `BlockedAccountMiddleware` shape + the frozen ADR 0019 zone
  resolution. It adds **no** `AccessAction` / `AccessVia` / `Decide()` branch /
  adapter / `IAuthorizationService` method (C-M28·5). The verdict is a pure
  function of (schedule, instant, effective zone) — not an authorization
  decision.
- **Not a total block.** A `Blocked` (fully-suspended) account is handled by
  M1 (`Profile.Blocked` + `BlockedAccountMiddleware`), which runs **first** in
  `Program.cs`; a fully-blocked child never reaches the time-limit check. M28 is
  the **time-window** layer, not the total-block layer.
- **Not the M20 quiet lane.** The M20 `NotificationQuietSchedule` doc, the
  `NotificationService.GetQuietScheduleAsync` / `SetQuietScheduleAsync` owner-
  scope seams, the `LocaleController` `/settings/quiet` surface, the
  `QuietScheduleEvaluator`, and the `settings.quiet.*` / `admin.quiet.*` kw-l
  keys are **all unchanged**. M28 adds **separate** seams on `IUserInfoService`
  (the GU surface), a **separate** doc, a **separate** middleware, and a
  **distinct** kw-l namespace (`guardian.timelimit.*` + `account.time_limit.*`).
  A resident's M20 quiet hours and a child's M28 time limits are **independent**
  (a child can be out-of-hours AND their notifications can be held — the two
  gates don't interact).

**The architectural decision (locked by U00):** M28 expresses the time limit as
a **per-child, guardian-set, timezone-aware access schedule** (a new
`GuardianTimeLimitSchedule` document + a pure `GuardianTimeLimitEvaluator` +
three additive `IUserInfoService` seams + one additive middleware), and applies
the verdict in a **`TimeLimitMiddleware`** (the `BlockedAccountMiddleware`
shape — sign-out + `?error=time-limit` landing) — so the M20 quiet lane, the
frozen GU standing read, and the frozen `IAuthorizationService` surface keep
working **unchanged**. This is D1 + D2 + D3 + D4 + D5 together — it is the whole
point of the milestone, and it is what lets every other surface (the ADR 0028
GU invariants G·1–G·5, the frozen `BlockedAccountMiddleware`, the ADR 0019 zone
chain) keep working **unchanged**.

## Invariants (pinned, C-M28·1 … C-M28·7)

- **C-M28·1 — The time limit gates the whole platform, not a subset.** A
  restricted child is **signed out** (all routes), exactly like a blocked
  account (`BlockedAccountMiddleware`) — a sign-out, not a route-by-route
  403/404. (D5.)
- **C-M28·2 — The verdict is a pure function, not an authorization decision.**
  No `CanAsync`/`CanSeeAsync` call, no `Decide()` branch, no `AccessAudit` row
  on the enforcement read. (D3.)
- **C-M28·3 — The floor: a missing or disabled schedule is never restricted.**
  `null` schedule or `Enabled == false` → allowed, always. (D3.)
- **C-M28·4 — Guardian standing is from the active link, live; GlobalAdmin is
  the safety valve; strong consistency.** The write gate = active
  `GuardianLink` ∪ GlobalAdmin; a save is live on the next read. (D4.)
- **C-M28·5 — Zero new authorization surface.** No `AccessAction`, no
  `AccessVia`, no `Decide()` branch, no `IAuthorizationService` signature, no
  adapter. The frozen `IAuthorizationService` surface stays byte-identical.
  (D5/D7.)
- **C-M28·6 — The zone is the child's effective zone (ADR 0019),
  wall-clock-first.** (D3.)
- **C-M28·7 — The child cannot set/clear their own time limit (no self-lane).**
  The only write is the guardian-gated (∪ GlobalAdmin) seam. (D6.)

## FACES (pinned, 6)

- **F1 — Blocked mode, in-window → restricted; out-of-window → allowed.** (The
  "no platform at night" reading.) Binds C-M28·3 (the floor) + C-M28·6 (the
  zone).
- **F2 — Allowed mode, in-window → allowed; out-of-window → restricted.** (The
  "only allowed during these hours" reading.) Binds C-M28·3 + C-M28·6.
- **F3 — The floor: no schedule / disabled → always allowed.** Binds C-M28·3.
- **F4 — Zone-dependent verdict.** (The same wall-clock schedule is a different
  verdict in the child's zone vs UTC.) Binds C-M28·6.
- **F5 — Guardian write requires an active link (∪ GlobalAdmin); a non-guardian
  is denied (404).** Binds C-M28·4 + C-M28·7.
- **F6 — The enforcement signs out (not a 403) and lands on a distinct
  message.** Binds C-M28·1 + C-M28·5.

## 2.1 — The frozen seam list (three `IUserInfoService` ADDs, exact C#)

Three **additive** seams on `IUserInfoService` (ADR 0006-E compatible ADDs — the
M27 / M3 / GU ADD precedent). The ADR 0006-E "named here (…)" list gains these
three methods. **No** `IAuthorizationService` method, **no** new `AccessAction` /
`AccessVia`, **no** `Decide()` branch (C-M28·5). Namespace
`Kumunita.Core.UserInfo`.

```csharp
// In Kumunita.Core/UserInfo/IUserInfoService.cs (U03) — three additive ADDs (D4):

/// <summary>
/// M28 (ADR 0151, D4) — the guardian's EDIT-PAGE READ. Gate = an active
/// <c>GuardianLink</c> for (guardianId, childId) ∪ GlobalAdmin (G·2 live, G·5
/// safety valve); a non-guardian, non-admin → <c>UnauthorizedAccessException</c>
/// (Web 404, the GU standing-lane shape, the <c>SuspendChildAsync</c>
/// precedent). A <c>LoadAsync</c> read; returns the row or <c>null</c> =
/// "never restricted" (the floor, C-M28·3). **No** <c>AccessAudit</c> row
/// (a read).
/// </summary>
Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(string guardianId, string childId);

/// <summary>
/// M28 (ADR 0151, D4) — the guardian's WRITE. Same gate as the read. A
/// <c>null</c> <paramref name="schedule"/> **deletes** the row (the M20
/// <c>SetQuietScheduleAsync(recipientId, null)</c> "clear" idiom — the
/// <c>session.Delete</c> shape); otherwise an upsert of the singleton-per-
/// child row (<c>session.Store</c>). Appends **exactly one**
/// <c>AccessAudit</c> row (<c>Via: Guardian</c> for a guardian write,
/// <c>Via: Admin</c> for a GlobalAdmin write — the G·5 safety valve;
/// <c>TargetKind</c> the child account; verb <c>guardian.time-limit.set</c> —
/// the <c>guardian.suspend</c> / <c>guardian.unsuspend</c> shape, ADR 0028 §E).
/// Strong consistency (C-M28·4): the write is in the caller's session — a
/// save is live on the very next read (no projection, no cache — the ADR 0050
/// <c>IsSignupOpen</c> shape).
/// </summary>
Task SetChildTimeLimitAsync(string guardianId, string childId, GuardianTimeLimitSchedule? schedule);

/// <summary>
/// M28 (ADR 0151, D4) — the **ENFORCEMENT** READ. Child-keyed, **no**
/// guardian gate (the <c>BlockedAccountMiddleware</c> <c>GetProfileAsync</c>
/// shape), **no** <c>AccessAudit</c> row (a read, C-M28·2). The
/// <c>TimeLimitMiddleware</c>'s single-row load. Returns the row or
/// <c>null</c> = "never restricted" (the floor, C-M28·3).
/// </summary>
Task<GuardianTimeLimitSchedule?> GetActiveTimeLimitAsync(string childId);
```

**Forbids:** a new method on `IAuthorizationService`, a new `AccessAction` /
`AccessVia` / `Decide()` branch / adapter (C-M28·5), an *owner*-scope seam
(a child writing their own schedule — C-M28·7), an unaudited guardian write
(the write is **one** audited row, ADR 0028 §E), an audited *read* (the two
reads emit no row), or a direct `IDocumentSession.Store` from a controller
(the three service seams are the only writes).

## 2.2 — New M28-owned Core types (doc + enum, exact C#)

Namespace `Kumunita.Core.UserInfo`. The **mirror** of the M20
`NotificationQuietSchedule` + `QuietScheduleMode` (ADR 0121 D2), per-child
(id = `ChildId`) rather than per-recipient.

```csharp
namespace Kumunita.Core.UserInfo;

/// <summary>
/// M28 (ADR 0151, D2) — the time-limit mode: the *polarity* of the schedule.
/// <see cref="Blocked"/> (the lean default) = "restricted DURING the listed
/// hours/days" (a block-list — the intuitive "no platform at night" reading).
/// <see cref="Allowed"/> = "restricted EXCEPT the listed hours/days" (an
/// allow-list — the child may use the platform only during the listed windows).
/// A closed two-value enum: a typo or a wrong value is impossible (the ADR
/// 0120 D4 "closed, enumerated set" idiom), and a new mode is a new enum value
/// (an additive append, never a free-form string).
/// </summary>
public enum TimeLimitMode
{
    /// <summary>Restricted DURING the listed hours/days (the lean default).</summary>
    Blocked = 0,
    /// <summary>Restricted EXCEPT the listed hours/days (the allow-list reading).</summary>
    Allowed = 1,
}

/// <summary>
/// M28 (ADR 0151, D2) — the per-child guardian time-limit schedule. One
/// document per child (id = the child's <see cref="ChildId"/> — the
/// <see cref="NotificationQuietSchedule"/> "one row per subject" shape, ADR
/// 0121 D2, with <c>ChildId</c> in place of <c>RecipientId</c>; identity
/// pinned to <c>ChildId</c> in <c>M1DocTypes</c>, next to
/// <see cref="GuardianLink"/>). A child with two guardians shares **one**
/// schedule (the <see cref="GuardianLink"/> multi-guardian shape).
/// <see cref="Enabled"/> is the master on/off: <c>false</c> = "never
/// restricted" (the floor — the child is always allowed, exactly as pre-M28,
/// C-M28·3). When <see cref="Enabled"/> is <c>true</c>, the allow/restrict
/// verdict is the pure <see cref="GuardianTimeLimitEvaluator.IsAllowedNow"/>
/// over <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/> in
/// the **child's** effective zone (D3, C-M28·6). <see cref="Hours"/> (0–23)
/// and <see cref="DaysOfWeek"/> (0=Sunday…6=Saturday) are independent axes; an
/// empty array means "all" (a schedule with only hours set is restricted on
/// every day during those hours, in <see cref="Blocked"/> mode).
/// </summary>
public sealed class GuardianTimeLimitSchedule
{
    /// <summary>Document identity — the child's subject id (one schedule per
    /// child; the same id as the child row's <see cref="GuardianLink.ChildId"/>).</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>
    /// The master on/off (D2, C-M28·3). <c>false</c> = "never restricted" —
    /// the floor: the child is always allowed, exactly as pre-M28, regardless
    /// of <see cref="Mode"/> / <see cref="Hours"/> / <see cref="DaysOfWeek"/>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The schedule's mode (D2): <see cref="TimeLimitMode.Blocked"/>
    /// (restricted DURING the listed hours/days, the lean default) vs
    /// <see cref="TimeLimitMode.Allowed"/> (restricted EXCEPT the listed
    /// hours/days, the allow-list).
    /// </summary>
    public TimeLimitMode Mode { get; set; } = TimeLimitMode.Blocked;

    /// <summary>
    /// The hours of day (0–23) the mode applies to. **Empty = the mode applies
    /// to ALL hours** (the "all hours" reading — a <see cref="TimeLimitMode.Blocked"/>
    /// schedule with only days-of-week set is restricted on those days all day).
    /// </summary>
    public int[] Hours { get; set; } = [];

    /// <summary>
    /// The days of week (0=Sunday…6=Saturday) the mode applies to. **Empty =
    /// the mode applies to ALL days** (a schedule with only hours set is
    /// restricted on every day during those hours).
    /// </summary>
    public int[] DaysOfWeek { get; set; } = [];

    /// <summary>The instant the guardian last saved/cleared this schedule (for
    /// display; not a gate).</summary>
    public DateTimeOffset? Updated { get; set; }
}
```

**The `M1DocTypes` registration** (U01 — the `GuardianTimeLimitSchedule`
document is Marten-native with a conventional `ChildId` identity; the additive-
surface delta is applied idempotently at boot — ADR 0004 §B.1, zero migration,
**no** new `*DocTypes` surface, **no** EF):

```csharp
// In M1DocTypes.Configure(StoreOptions opts), alongside Schema.For<GuardianLink>():
opts.Schema.For<UserInfo.GuardianTimeLimitSchedule>().Identity(s => s.ChildId);
```

**Why `M1DocTypes` and not a new `M28DocTypes` (D2):** it is **part of the GU
guardian-lane surface** (the "lane on the M1 surface, not a new bounded
context" shape, the M27 D7 / SITE "one new doc type" precedent) — the cheapest
+ most consistent choice. The seeder does **not** seed a schedule for any
child (a fresh child account has no schedule — the C-M28·3 floor).

## 2.3 — The pure `GuardianTimeLimitEvaluator` (exact C#, the mirror of M20)

Namespace `Kumunita.Core.UserInfo`. The **exact mirror** of
`QuietScheduleEvaluator.IsQuietNow` (D3, C-M28·2/C-M28·6), with the **polarity
inverted** because here **allowed** is the permission, not the restriction.

```csharp
namespace Kumunita.Core.UserInfo;

/// <summary>
/// M28 (ADR 0151, D3) — the pure "is the child allowed now?" evaluator. A
/// **pure function** (no session, no HTTP, no IO — the <c>IcsWriter</c> /
/// <c>UsageCapturePolicy</c> "pure, closed, testable in isolation" discipline):
/// it takes the resolved (schedule, instant, effective zone) and returns a
/// <c>bool</c> — <c>true</c> = the child MAY use the platform now. It converts
/// <paramref name="now"/> to <paramref name="effectiveZone"/>'s **wall clock**
/// (ADR 0019 — the instant is converted to the zone's wall clock *first*, then
/// the hour/day are read, exactly the <c>kw-dt</c> TagHelper +
/// <c>EventReminderService</c> resolution order), then evaluates the
/// schedule's <c>Mode</c>/<c>Hours</c>/<c>DaysOfWeek</c> against that wall
/// clock. <c>null</c> / <c>Enabled == false</c> → <c>true</c> (allowed, the
/// floor, C-M28·3). An empty <c>Hours</c> means "all hours"; an empty
/// <c>DaysOfWeek</c> means "all days". A **separate** pure function from M20's
/// <c>QuietScheduleEvaluator</c> (so the <c>UserInfo</c> context does not
/// depend on <c>Notifications</c>), sharing the exact evaluation discipline.
/// </summary>
public static class GuardianTimeLimitEvaluator
{
    /// <summary>
    /// D3 — pure, timezone-aware "is the child allowed now?" A <c>null</c>
    /// schedule is always allowed (the floor, C-M28·3); a disabled schedule is
    /// always allowed (the D2 master on/off). Otherwise the instant is
    /// converted to <paramref name="effectiveZone"/>'s wall clock FIRST (ADR
    /// 0019), then: <see cref="TimeLimitMode.Blocked"/> — restricted DURING the
    /// window, so allowed iff the wall clock is OUTSIDE the window
    /// (<c>!windowMatches</c>). <see cref="TimeLimitMode.Allowed"/> — restricted
    /// EXCEPT the window, so allowed iff the wall clock is INSIDE the window
    /// (<c>windowMatches</c>). (A <c>Blocked</c> schedule with both axes empty
    /// is the "all hours × all days" window → always restricted, the "total
    /// block" reading; a <c>Allowed</c> schedule with both axes empty is the
    /// "all hours × all days" window → always allowed, the floor.)
    /// </summary>
    public static bool IsAllowedNow(
        GuardianTimeLimitSchedule? schedule,
        DateTimeOffset now,
        TimeZoneInfo effectiveZone)
    {
        ArgumentNullException.ThrowIfNull(effectiveZone);

        // The floor: no schedule, or the master off → always allowed (C-M28·3).
        if (schedule is null || !schedule.Enabled)
            return true;

        // ADR 0019 — wall-clock-first: convert the instant to the zone's local
        // time, THEN read the hour + day (the kw-dt TagHelper shape).
        var wall = TimeZoneInfo.ConvertTime(now, effectiveZone);

        // An empty axis = "all" (the D2 "empty = all hours / all days" reading).
        var hourMatches   = schedule.Hours.Length     == 0 || schedule.Hours.Contains(wall.Hour);
        var dayMatches    = schedule.DaysOfWeek.Length == 0 || schedule.DaysOfWeek.Contains((int)wall.DayOfWeek);
        var windowMatches = hourMatches && dayMatches;   // the schedule's window contains the instant

        // Blocked = restricted DURING the window (the lean default) → allowed iff
        // OUTSIDE. Allowed = restricted EXCEPT the window (the allow-list) →
        // allowed iff INSIDE. (The mirror of M20's IsQuietNow — the polarity is
        // inverted because here *allowed* is the permission, not the restriction.)
        return schedule.Mode == TimeLimitMode.Blocked
            ? !windowMatches
            : windowMatches;
    }
}
```

**Forbids:** evaluating in `UTC` (the same wall-clock schedule is a different
verdict in the child's UTC+2 zone vs UTC — the *point*, C-M28·6), taking a
client-supplied wall-clock string, an impure evaluator that reads a session, a
`CanAsync` / `CanSeeAsync` call, a `Decide()` branch, or an `AccessAudit` row
(C-M28·2), or a dependency from `UserInfo` on `Notifications` (a separate pure
function, D3).

## 2.4 — The `TimeLimitMiddleware` (exact shape, the `BlockedAccountMiddleware` analog)

`Kumunita.Web.Security` (D5, C-M28·1/C-M28·5). The **exact**
`BlockedAccountMiddleware` shape — scoped-resolution from `RequestServices`,
the single-row load, the effective-zone resolution via the ADR 0019 chain, the
`IsAllowedNow` call, the `SignInManager.SignOutAsync` + the `?error=time-limit`
redirect. **Zero** new authorization surface: the middleware does **not** call
`IAuthorizationService`.

The exact pipeline (U04):

1. **Anonymous → pass.** (A signed-out child is already out.)
2. **Resolve the subject** (the signed-in principal).
3. **`IUserInfoService.GetActiveTimeLimitAsync(subject)`** (the enforcement
   read, §2.1) → `null` / `Enabled == false` → **pass** (the floor, C-M28·3).
4. **Resolve the subject's effective zone** (the ADR 0019 chain via
   `EffectiveTimezoneResolver` / the `Profile.TimeZone` override → platform
   default → `UTC` floor — the `EventReminderService` `TryResolveZone` shape).
5. **`GuardianTimeLimitEvaluator.IsAllowedNow(schedule, now, zone)`** (D3) →
   `true` → **pass**; `false` → **`SignInManager.SignOutAsync()`** (clear both
   cookies, exactly as `BlockedAccountMiddleware`) **and redirect
   `/Account/Login?error=time-limit`** (the **distinct** code — not `blocked`).

**The `Program.cs` registration** (U04) — `app.UseMiddleware<TimeLimitMiddleware>();`
**after** the `app.UseMiddleware<BlockedAccountMiddleware>();` line (a
fully-blocked account hits the `blocked` landing first; M28 is the time-window
layer, not the total-block layer).

**The `?error=time-limit` login landing** (U04) — `AccountController.Login` GET
gains the distinct error code, mapped to the new `account.time_limit.login_message`
kw-l key (the `?error=blocked` precedent in the same switch; the key itself is
authored in U05, §2.7).

**Forbids:** a route-by-route `403` / `404` (C-M28·1 — it is a **sign-out**,
like `BlockedAccountMiddleware`), a `Decide()` branch / `AccessAction` /
`AccessVia` / `IAuthorizationService` call (C-M28·5), a `?error=blocked` reuse
(a **distinct** code + message), or an enforcement path that reads the child's
content (G·1-adjacent — the middleware reads only the schedule row).

## 2.5 — The surface (the guardian's Detail "Time limits" section, no self-lane)

`GuardianController` + `GuardianViewModels` + `Views/Guardian/Detail.cshtml`
(D6, C-M28·4/7). The GU Detail page's existing suspend / membership / invitation
sections as the shape. **No** `/settings/time-limit` (the child cannot set/clear
their own — C-M28·7); the Detail section is the **single** surface (a
GlobalAdmin reaches the same write seam — the G·5 safety valve — the standing
is resolved by the seam, not a separate admin page).

- **`ChildAccountItem` (GU Index card) — 3-field → 4-field re-pin (U06).**
  Add `bool HasTimeLimits` (the `Blocked` flag precedent) — the GU Index view's
  "time-limits set?" badge. U06 re-pins the GU Index test that asserts the
  3-field shape to the 4-field shape.
- **`Detail` GET** — seed the "Time limits" section with
  `GetChildTimeLimitAsync(subject, childId)` (the guardian-gated read, §2.1); a
  `TimeLimits` section view-model (the `LocaleSettingsViewModel.Quiet` shape —
  the `Enabled` / `Mode` / `Hours` / `DaysOfWeek` + the `HasTimeLimits` badge).
- **`SaveTimeLimits` POST** — the `LocaleController.SaveQuiet` shape —
  `SetChildTimeLimitAsync(subject, childId, schedule)` (a `null` body clears =
  never restricted, C-M28·3) + the `guardian.timelimit.flash_saved` /
  `_cleared` flash.
- **The view** — the M20 `Views/Locale/Quiet.cshtml` shape: the `Enabled`
  checkbox, the `Mode` radio (Blocked/Allowed), the `Hours` + `DaysOfWeek`
  multi-select, the Save / Clear buttons, bound to the `guardian.timelimit.*`
  keys (§2.7). The section is **guarded** to the child's Detail page (not a new
  route — D6).

## 2.6 — The pinned seam tests (exact names, 20) + the three acceptance-gate tests

The **exact** names the units must match (U02's 7 pure, U03's 7 standing/audit,
U04's 3 middleware, U06's 3 surface = **20**). The three acceptance-gate tests
(U07 authors, U08 records) are defined over this 20-test seam list in §2.8.

**U02 — the 7 pure tests** (`tests/Kumunita.Core.Tests/GuardianTimeLimitEvaluatorTests.cs`):

1. `F3_Missing_Schedule_Is_Always_Allowed` (C-M28·3)
2. `F3_Disabled_Schedule_Is_Always_Allowed` (C-M28·3)
3. `F1_Blocked_InWindow_Is_Restricted` (F1)
4. `F1_Blocked_OutOfWindow_Is_Allowed` (F1)
5. `F2_Allowed_InWindow_Is_Allowed` (F2)
6. `F2_Allowed_OutOfWindow_Is_Restricted` (F2)
7. `F4_Verdict_Differs_Between_Zones` (C-M28·6 — the M20 `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` analog)

**U03 — the 7 standing/audit tests** (`tests/Kumunita.Core.Tests/GuardianTimeLimitStandingTests.cs`):

8. `F5_NonGuardian_Set_Is_Denied` (C-M28·4/C-M28·7)
9. `F5_Guardian_Set_EmitsOneAuditRow_ViaGuardian` (C-M28·4)
10. `F5_GlobalAdmin_Set_EmitsOneAuditRow_ViaAdmin` (C-M28·4, G·5)
11. `F5_Set_Is_StronglyConsistent_NextReadSeesNewValue` (C-M28·4)
12. `C_M28_7_Child_Cannot_Set_Own_TimeLimit` (C-M28·7)
13. `F5_Clear_SetsNull_DeletesRow` (C-M28·3/D4)
14. `C_M28_5_IAuthorizationService_Surface_Count_Unchanged` (C-M28·5 — the structural pin)

**U04 — the 3 middleware tests** (`tests/Kumunita.Web.Tests/TimeLimitMiddlewareTests.cs`):

15. `F6_Restricted_SignsOutAndRedirects_TimeLimit` (C-M28·1/C-M28·5)
16. `F6_Allowed_PassesThrough` (C-M28·3)
17. `F6_NoSchedule_PassesThrough` (C-M28·3)

**U06 — the 3 surface tests** (`tests/Kumunita.Web.Tests/GuardianTimeLimitSurfaceTests.cs`):

18. `F5_Detail_Get_SeedsWithCurrentSchedule` (D6)
19. `F5_Detail_Post_SavesAndFlashes` (D6)
20. `F5_Detail_Post_Clear_SetsNull` (D6, C-M28·3)

## 2.7 — `kw-l` key list (the closed key set × en/de/fr/da)

The exact closed set from the register's D7. Added to **all four** of the closed
dictionaries in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` —
`EnValues` (the `en` floor, the canonical source text), `DeValues`, `FrValues`,
`DaValues` (the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder
materializes each key's `en` row on first boot; the non-`en` rows are the
localized text). **A distinct namespace** — **not** the M20 `settings.quiet.*` /
`admin.quiet.*` keys (those are the notification lane's, unchanged — D9). **U05
authors the COMPLETE set; U06 consumes** (U06 does not add a key).

| # | Key | Surface | One-line description |
|---|---|---|---|
| 1 | `guardian.timelimit.title` | GU Detail "Time limits" | Section title (guardian). |
| 2 | `guardian.timelimit.description` | GU Detail "Time limits" | Helper text under the title (guardian). |
| 3 | `guardian.timelimit.enabled` | GU Detail "Time limits" | The master on/off label (guardian). |
| 4 | `guardian.timelimit.mode_label` | GU Detail "Time limits" | "When may the child use the platform?" (guardian). |
| 5 | `guardian.timelimit.mode_blocked` | GU Detail "Time limits" | "Blocked during the selected hours & days" (guardian). |
| 6 | `guardian.timelimit.mode_allowed` | GU Detail "Time limits" | "Allowed only during the selected hours & days" (guardian). |
| 7 | `guardian.timelimit.hours_label` | GU Detail "Time limits" | "Hours of day" picker label (guardian). |
| 8 | `guardian.timelimit.days_label` | GU Detail "Time limits" | "Days of week" picker label (guardian). |
| 9 | `guardian.timelimit.save` | GU Detail "Time limits" | The save button (guardian). |
| 10 | `guardian.timelimit.clear` | GU Detail "Time limits" | The clear button (guardian). |
| 11 | `guardian.timelimit.flash_saved` | GU Detail "Time limits" | "Time limits saved" flash (guardian). |
| 12 | `guardian.timelimit.flash_cleared` | GU Detail "Time limits" | "Time limits cleared" flash (guardian). |
| 13 | `guardian.timelimit.badge_set` | GU Index card | The "time-limits set?" badge (`HasTimeLimits`, U06 re-pin). |
| 14 | `account.time_limit.login_message` | `/Account/Login?error=time-limit` | The child's "you're outside your allowed hours" landing (the generic `?error=blocked`-shaped message). |

**The closed set is 14 keys × 4 languages = 56 strings.** The
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` + the new
`GuardianTimeLimitKwLParityTests` (U05) enforce the 4-language set + non-empty
values; the ADR 0015 / 0052 warm-boot backfill seeds them idempotently.

## 2.8 — §gate (the three acceptance tests, U07 runs, U08 records)

M28 is **done** when, and only when, all three of these are true (each is a
pinned test in U07's `tests/Kumunita.Core.Tests/M28AcceptanceGateTests.cs`),
**using** the 20-test seam list (§2.6) as the part-vs-whole evidence:

- **GATE-1 — Closed-loop: a guardian sets a `Blocked`-mode window covering
  "now" for their child → the child is restricted.** A guardian sets a
  `Blocked`-mode schedule whose window contains the current instant (in the
  child's zone) → `GuardianTimeLimitEvaluator.IsAllowedNow` returns `false` →
  the `GetActiveTimeLimitAsync` read sees the schedule → the middleware would
  sign out. *(F1 + C-M28·4.)* Pinned test name:
  `Gate1_ClosedLoop_BlockedWindow_ContainingNow_RestrictsChild` (U07).
- **GATE-2 — Handoff: an `Allowed`-mode window permitting "now" → allowed; then
  cleared → always allowed.** A guardian sets an `Allowed`-mode schedule whose
  window contains the current instant → the child is allowed; then clears it
  (`null`) → the child is again always allowed (the floor). *(F2-in-window +
  F3 floor + C-M28·4 strong consistency.)* Pinned test name:
  `Gate2_Handoff_AllowedWindow_Then_Clear_Is_AlwaysAllowed` (U07).
- **GATE-3 — Part-vs-whole: the full 20-test seam list passes together.** The
  U02 7 pure + U03 7 standing + U04 3 middleware + U06 3 surface, all green in
  the same run. *(C-M28·5 — the `IAuthorizationService` surface count is
  unchanged.)* Pinned test name:
  `Gate3_PartVsWhole_AllTwentySeamTestsPassTogether` (U07).

## 2.9 — §drift-guard (frozen once written)

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the locked set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a relational standing
  table** — that violates C-M28·5; stop and report (the whole point of M28 is
  zero new authorization surface — the verdict is a pure function, not an
  authorization decision).
- The enforcement is about to be a **route-by-route `403`/`404`** (not a
  sign-out) — that violates C-M28·1 / D5; stop and report (M28 is a
  **sign-out** gate, the `BlockedAccountMiddleware` shape).
- The evaluator is about to be **impure** (read a session, call
  `CanAsync`/`CanSeeAsync`, or emit an `AccessAudit` row) or to **depend on
  `Notifications`** — that violates C-M28·2/C-M28·5 / D3; stop and report (it is
  a separate pure function, the `UserInfo` context does not depend on
  `Notifications`).
- The write seam is about to be **owner-scoped** (a child writing their own
  schedule) or **unaudited** — that violates C-M28·7 / D4; stop and report.
- A unit is about to **touch the M20 lane** (the `NotificationQuietSchedule`
  doc, the `NotificationService` quiet seams, the `/settings/quiet` surface,
  the `QuietScheduleEvaluator`, or the `settings.quiet.*` / `admin.quiet.*`
  keys) — that violates D9 (M20 is untouched); stop and report.
- A user-visible string that is **not** already in `KnownTranslationKeys` for
  all four languages is about to be rendered — add it to U05's closed set first;
  do not inline a string (the parity pin will fail, and that is the point).
- A unit other than **U09** is about to touch `Milestones.cs` /
  `MilestonesTests.cs` / `WhatsNew.cs` / README §Roadmap / `STATUS.md` /
  `ARCHITECTURE.md` — stop; only the close unit owns the roadmap + the close
  flip (M28 is the **LAST** milestone — U09's reframe).
- A unit needs *both* test assemblies green to exit **and** it is not U07/U08 —
  it is too big; split it per the Atomicity contract rather than running both.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not
re-derive what an earlier unit already settled (a D# amendment, the key set, a
seam shape, the polarity, the zone-resolution chain).

**The register is the map, not the code.** If a unit is tempted to "add a
`Decide()` branch for the time limit" or "return a 403 on the restricted page,"
that is the §drift-guard firing — M28's value is precisely that it does *not*:
it gates the whole platform as a **sign-out**, keeps the verdict a **pure
function**, adds **zero** authorization surface, respects the **child's own**
time zone, and leaves the M20 lane **untouched**.

## 2.10 — §deferred (the D9 lanes)

The deferred lanes verbatim (register D1 out-of-scope set + D9), each with a
one-line "why it is deferred":

1. **A *resident* self-service time limit** (the inverse — a non-guardian
   version, a resident gating their own access) — deferred because M28 is
   **guardian-set, per-child** (D1/D6, C-M28·7); a self-service version is a
   **new, ADR-gated** lane (it would be the M20 owner-scope shape re-pointed at
   access rather than email — a different policy surface). A follow-up lane with
   its own ADR.
2. **Per-feature / per-content restrictions** (browse-yes / message-no, or
   time-limit a specific content kind) — deferred because M28 is a
   **whole-platform** gate (D1, C-M28·1 — "allowed to browse but not message" is
   a different policy); per-feature time limits need their own design + ADR.
3. **An awareness / reminder lane** ("your parent has enabled time limits") —
   deferred because M28 is **enforcement** (a sign-out), not notification (the
   landing message is generic, the `?error=blocked` precedent); a proactive
   awareness surface is a new ADR.
4. **Enforcement-history auditing** (which requests were denied) — deferred
   because the middleware is silent (D5, C-M28·2 — no per-request deny log); a
   `UsageEvent` for denied access is the M13 ADR 0114 capture shape, a new ADR.
5. **Per-content time limits** (a future refinement of D9·2) — deferred because
   M28's scope is the whole platform (D1); per-content is a larger design. A
   follow-up lane with its own ADR.

## 1.a — Amendments

- **2026-10-07 — One refinement, recorded.** The register's [PROPOSED] set
  (D1–D9) locked as-is. The one refinement against the source: the register's
  D7 lists the closed kw-l set as "the 14 keys" but the `guardian.timelimit.*`
  family is **13** keys + the single `account.time_limit.login_message` key =
  **14 total** (verified against the M20 `settings.quiet.*` 15-key closed-set
  shape in §10 of `m20-notification-quiet-times-design.md`). No D# required a
  representation amendment (unlike M19's D2 array→`[Flags]`). The D2
  `GuardianTimeLimitSchedule` document + `TimeLimitMode` enum are authored
  verbatim as the register's prose block (a `bool` master + a two-value enum +
  two `int[]` axes), mirroring ADR 0121 D2's `NotificationQuietSchedule`.
  **ADR 0151 verified free** against `docs/adr/README.md` (index runs to 0150)
  and on disk (no `0151-*.md`). Status remains **Accepted**.
