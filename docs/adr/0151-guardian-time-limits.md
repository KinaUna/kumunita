# ADR 0151 — Guardian time limits (a per-child allow/block schedule that gates the child's platform access)

Status: Accepted
Date: 2026-10-07
Amends: 0028 (a new GU seam + a new GU doc + one GU audit verb — all compatible ADDs; the GU standing / G·1–G·5 invariants are unchanged), 0006-E (three additive `IUserInfoService` seams, named here)
References: 0121 (the `QuietScheduleEvaluator` / `NotificationQuietSchedule` pure-evaluator + doc shape this ADR mirrors, polarity-inverted), 0019 (the wall-clock-first effective-zone resolution the evaluator + middleware use), 0004 §B.1 (the additive doc type on the existing `M1DocTypes` surface)

## Context

The GU guardian lane (ADR 0028) shipped **account-level** supervision of a
child's account: suspend/lock (`Profile.Blocked`, the
`BlockedAccountMiddleware`), curate community/group membership, approve a group
invitation, dissolve the link, and (ADR 0038/0144) gate event RSVPs. But that
supervision is **binary** — a child is either fully blocked or fully open. There
is no *time* dimension. A parent who wants "no platform from 22:00 to 07:00"
today has no path short of suspending the account — the wrong tool (a total
block, not a windowed one, and it reads as a punishment in the audit log).

M28 adds the **time dimension, at the guardian layer**. The README M28 line is
the scope, verbatim: "**Guardian time limits — for a child's account, a
parent/guardian sets when the child may use the platform: allow or block
certain hours of each day and days of the week.**" This is a **new lane on the
GU surface** (ADR 0028/0038), the **inverse of the M20 notification-quiet lane**
(ADR 0121): M20 lets a *resident* hold their own *notification emails* during a
window (a best-effort email deferral, per-resident, self-set); M28 lets a
*guardian* gate a *child's* *whole-platform access* during a window (a hard
sign-out, per-child, guardian-set).

The constraint that shapes the decision is the same one that shaped ADR 0028 and
ADR 0121: **compose the frozen seams, never extend them** — and specifically,
**add zero new authorization surface** (the ADR 0006 frozen-surface discipline).
The time-limit verdict is a *pure function* of (schedule, instant, effective
zone) — **not** an authorization decision; the enforcement is a *middleware* (the
`BlockedAccountMiddleware` shape), not a `Decide()` branch; the standing is the
*existing* GU `GuardianLink` active-link read (G·2), and the GlobalAdmin clear
is the *existing* G·5 safety valve. `docs/design/m28-guardian-time-limits-design.md`
freezes the invariants **C-M28·1–C-M28·7**, the FACES **F1–F6**, and the seam
tests this ADR points at.

## Decision

**D1 — The scope is a per-child schedule that gates the child's whole-platform
access (not a feature, not the M20 lane).** A guardian sets a per-child
schedule (a `Blocked` window "no platform DURING these hours/days" or an
`Allowed` window "platform EXCEPT these hours/days") over hours-of-day ×
days-of-week, evaluated in **the child's** effective zone (ADR 0019). When the
child is **outside the allowed window**, the child is **signed out** and lands
on a distinct `?error=time-limit` login message. **Out of scope (deferred, §10
of the design doc / ADR 0151 D9):** a resident self-service time limit
(inverse), per-feature / per-content restrictions, an awareness/reminder lane,
enforcement-history auditing. *Forbids:* a `Profile` field for the schedule
(the structured mode+hours+days value belongs in its own doc — the
`NotificationQuietSchedule` precedent), or a per-feature gate.

**D2 — The doc is per-child (id = `ChildId`), registered on the existing
`M1DocTypes` GU surface (the `GuardianLink` neighbor).** One row per child — the
exact `NotificationQuietSchedule` "one row per subject" shape (ADR 0121 D2),
with `ChildId` in place of `RecipientId`. A child with **two** guardians shares
**one** schedule (the `GuardianLink` multi-guardian shape; the second guardian
sets/clears the same row via the same guardian-gated seam). Registered **next
to `GuardianLink`** on `M1DocTypes` (the GU lane's surface, ADR 0028 §B; the ADR
0004 §B.1 additive-doc-type precedent — delta-detected, idempotent, **no** new
`*DocTypes`, **no** EF migration, **no** DDL). The seeder does **not** seed a
schedule for any child (a fresh child has no schedule — the C-M28·3 floor).
*Forbids:* a new `M28DocTypes` surface, an EF table, or a per-(guardian, child)
row.

**D3 — The verdict is a pure function (mirrors ADR 0121 D3, polarity-inverted).**
`GuardianTimeLimitEvaluator.IsAllowedNow(schedule, now, effectiveZone)` → `bool`
(`true` = the child MAY use the platform now), in `Kumunita.Core.UserInfo`. A
**pure function** (no session, no HTTP, no IO — the `IcsWriter` /
`UsageCapturePolicy` "pure, closed, testable in isolation" discipline). Converts
`now` to `effectiveZone`'s **wall clock** *first* (ADR 0019), then evaluates
`Mode`/`Hours`/`DaysOfWeek`: `Blocked` → allowed iff the wall clock is
**outside** the listed window; `Allowed` → allowed iff **inside** it; an empty
axis = "all". `null` / `Enabled == false` → **allowed** (the floor, C-M28·3). A
**separate** pure function from M20's `QuietScheduleEvaluator` (so `UserInfo`
does not depend on `Notifications`), sharing the exact evaluation discipline.
*Forbids:* evaluating in `UTC` (the same wall-clock schedule is a different
verdict in the child's UTC+2 zone vs UTC — the *point*), a `CanAsync` /
`CanSeeAsync` call, a `Decide()` branch, an `AccessAudit` row (C-M28·2/C-M28·5),
or an impure evaluator that reads a session.

**D4 — The write lane is guardian-scoped (∪ GlobalAdmin), and IS audited.**
Three **additive** seams on `IUserInfoService` (ADR 0006-E compatible ADDs,
named here):

- `GetChildTimeLimitAsync(guardianId, childId)` — the guardian's edit-page read;
  gate = active `GuardianLink` for (guardianId, childId) ∪ GlobalAdmin (G·2
  live, G·5 safety valve; deny → `UnauthorizedAccessException`, Web 404, the
  `SuspendChildAsync` shape); **no** audit row (a read).
- `SetChildTimeLimitAsync(guardianId, childId, schedule?)` — the guardian's
  write; the same gate; a `null` schedule **deletes** the row (the M20
  `SetQuietScheduleAsync(recipientId, null)` "clear" idiom); otherwise an upsert;
  **exactly one** `AccessAudit` row (`Via: Guardian`, or `Via: Admin` on the
  GlobalAdmin clear — the G·5 safety valve; `TargetKind` the child account; verb
  `guardian.time-limit.set` — the ADR 0028 §E `guardian.suspend` shape). Strong
  consistency (C-M28·4): a save is live on the very next read (the ADR 0050
  `IsSignupOpen` shape).
- `GetActiveTimeLimitAsync(childId)` — the **enforcement** read (child-keyed,
  **no** guardian gate, **no** audit row) — the middleware's single-row load (the
  `BlockedAccountMiddleware` `GetProfileAsync` shape).

**The child cannot write their own schedule** (a child subject used as
`guardianId` on their own `childId` is denied — the G·3 pin, the "a child cannot
approve their own invitation" analog, C-M28·7). *Forbids:* an owner-scoped seam,
an unaudited guardian write, an audited read, or a direct `IDocumentSession.Store`
from a controller.

**D5 — Enforcement is a new middleware, NOT a `Decide()` branch.**
`TimeLimitMiddleware` in `Kumunita.Web.Security`, the **exact**
`BlockedAccountMiddleware` shape (scoped-resolution from `RequestServices`, the
single-row `GetActiveTimeLimitAsync` load, the effective-zone resolution via the
ADR 0019 chain, the `IsAllowedNow` call, the `SignInManager.SignOutAsync` + the
`?error=time-limit` redirect). Registered in `Program.cs` **after**
`BlockedAccountMiddleware` (a fully-blocked account hits the `blocked` landing
first). **Zero** new authorization surface (C-M28·5): no `AccessAction`, no
`AccessVia`, no `Decide()` branch, no adapter, no `IAuthorizationService`
signature. The `?error=time-limit` code maps to a new `kw-l` key on
`AccountController.Login` GET (the `?error=blocked` precedent). *Forbids:* a
route-by-route `403`/`404` (C-M28·1 — it is a **sign-out**), a `?error=blocked`
reuse, or an enforcement path that reads the child's content (G·1-adjacent).

**D6 — The surface is the guardian's Detail section — no self-lane.** A "Time
limits" section on the child's `/me/children/{childId}` Detail page (the GU
Detail page's existing suspend / membership / invitation sections as the shape,
the M20 `Views/Locale/Quiet.cshtml` mode/hours/days form shape): `GET` seeds the
form with the current schedule (`GetChildTimeLimitAsync`), `POST` saves/clears
(`SetChildTimeLimitAsync`, a `null` body clears = never restricted). The
`ChildAccountItem` GU Index card gains a `HasTimeLimits` badge (the `Blocked`
flag precedent — the 3-field → 4-field re-pin). A GlobalAdmin reaches the same
write seam (the G·5 safety valve) — the Detail page is the single surface.
**No** `/settings/time-limit` (the child cannot set/clear their own — C-M28·7).

**D7 — kw-l keys (a distinct namespace, × 4).** The closed set: the guardian
Detail section (`guardian.timelimit.title` / `.description` / `.enabled` /
`.mode_label` / `.mode_blocked` / `.mode_allowed` / `.hours_label` /
`.days_label` / `.save` / `.clear` / `.flash_saved` / `.flash_cleared` /
`.badge_set`) + the login landing (`account.time_limit.login_message`) = **14
keys × en/de/fr/da = 56 strings**, landed in **all four**
`KnownTranslationKeys` dicts (the `en`/`de`/`fr`/`da` parity pin). **Not** the
M20 `settings.quiet.*` / `admin.quiet.*` keys (those are the notification
lane's, unchanged — D9).

**D8 — Tests: a pure-evaluator seam set + a standing/audit seam set + a
middleware seam set + a surface seam set, and the three-test acceptance gate.**
The 20 seam tests (U02's 7 pure + U03's 7 standing/audit + U04's 3 middleware +
U06's 3 surface) + the three acceptance-gate tests (closed-loop / handoff /
part-vs-whole, U07 authors + runs, U08 records) over the 20-test seam list. The
exact names are pinned in the design doc §6.

**D9 — Deferred lanes (each a future ADR, listed so a unit does not reach for
them).** (1) A *resident* self-service time limit (the inverse). (2) Per-feature
/ per-content restrictions (this is a whole-platform gate). (3) An awareness /
reminder lane ("your parent has enabled time limits"). (4) Enforcement-history
auditing (the M13 ADR 0114 `UsageEvent` shape). (5) Per-content time limits.
*Forbids:* a unit implementing any of these inside an M28 unit.

### The invariants (owned by the design doc, pinned by seam tests)

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
  adapter. The frozen 4-method surface stays byte-identical. (D5/D7.)
- **C-M28·6 — The zone is the child's effective zone (ADR 0019),
  wall-clock-first.** (D3.)
- **C-M28·7 — The child cannot set/clear their own time limit (no self-lane).**
  The only write is the guardian-gated (∪ GlobalAdmin) seam. (D6.)

## Consequences

**Positive:**

- **A guardian gains a *time* dimension over a child's account, not just a
  total block** (the README M28 row, end-to-end): a guardian sets a per-child
  `Blocked`/`Allowed` window on the child's Detail page (D2, D4, D6), the child
  is signed out and lands on a distinct `?error=time-limit` message when
  outside the allowed window (D5 — GATE-1's
  `Gate1_ClosedLoop_BlockedWindow_ContainingNow_RestrictsChild` pin), and a
  GlobalAdmin clears the schedule as the safety valve (D4 — GATE-2's
  `Gate2_Handoff_AllowedWindow_Then_Clear_Is_AlwaysAllowed` pin). No new
  authorization surface (C-M28·5).
- **Zero new authorization surface — the strongest form** (C-M28·5): M28 adds
  **no** new `AccessAction`, **no** new `AccessVia`, **no** new adapter, **no**
  branch in `Decide()`, **no** new `IAuthorizationService` method, **no** new
  claim *type*. The verdict is a **pure function** (D3) — the frozen
  `IUserInfoService` GU standing read + the frozen `BlockedAccountMiddleware`
  sign-out shape + the frozen ADR 0019 zone resolution are the **only**
  touchpoints. The GU standing (G·1–G·5) and the frozen `IAuthorizationService`
  surface are **byte-identical** after M28 (GATE-3's
  `C_M28_5_IAuthorizationService_Surface_Count_Unchanged` pin).
- **Floor = never restricted, byte-identical to pre-M28** (C-M28·3): a child
  with no `GuardianTimeLimitSchedule` row (or a disabled schedule) is always
  allowed — the GU lane's "we decided not to restrict" default is preserved.
- **Zero migrations** (D2): the one new `GuardianTimeLimitSchedule` document is
  delta-detected and applied idempotently at boot on the existing `M1DocTypes`
  surface (the ADR 0004 §B.1 additive-surface shape) — every existing row reads
  its pre-M28 state, unchanged.
- **The cardinal privacy rule is held** (G·1-adjacent): the enforcement read is
  the schedule row + the active `GuardianLink`, **never** the child's
  posts/profile; the landing message is a generic "outside your allowed hours"
  (the `?error=blocked` generic-message precedent), not a privileged reveal of
  which hours.

**Neutral / cost:**

- **The named trade — a *coarse, per-child, whole-platform, guardian-set*
  schedule for zero new authorization surface:** M28 buys *a time window that
  respects the child's own zone and enforces as a sign-out* with **zero new
  authorization surface + one new doc (`GuardianTimeLimitSchedule`) + one new
  enum (`TimeLimitMode`) + three additive `IUserInfoService` seams + one new
  middleware + one `?error=time-limit` landing**, in exchange for a **coarse,
  per-child, whole-platform** schedule (D9): a guardian gets "no platform at
  night" (or "allowed only these hours these days"), **not** "allowed to browse
  but not message," **not** "hold the notification emails too," and **not** "the
  child sets their own." The per-feature (D9·2), awareness (D9·3), and
  enforcement-history (D9·4) lanes are deliberately *not* in M28: they are
  larger policy designs that need their own ADRs.
- **The two gates are independent** (D1/D9): a resident's M20 quiet hours and a
  child's M28 time limits do not interact — a child can be out-of-hours AND
  their notifications held (the M20 `NotificationQuietSchedule` lane is
  untouched).
- **The enforcement is synchronous per request** (D5): the middleware loads the
  schedule on every authenticated request (one `mt` round-trip — the
  `BlockedAccountMiddleware` cost profile). A schedule save is live on the next
  request (strong consistency, C-M28·4).

**Follow-on lanes (each its own ADR — the design doc's §10 / ADR 0151 D9):** a
resident self-service time limit (D9·1); per-feature / per-content restrictions
(D9·2/D9·5); an awareness / reminder lane (D9·3); enforcement-history auditing
(D9·4).

## Amendments

- **2026-10-07 — None.** The register's [PROPOSED] set (D1–D9) locked as-is.
  No D# required a representation amendment (unlike M19's D2 array→`[Flags]`).
  The D2 `GuardianTimeLimitSchedule` document + `TimeLimitMode` enum are authored
  verbatim as the register's prose blocks (a `bool` master + a two-value enum +
  two `int[]` axes), mirroring ADR 0121 D2's `NotificationQuietSchedule`. The
  D7 closed kw-l set is 14 keys (13 `guardian.timelimit.*` + the single
  `account.time_limit.login_message`) × 4 languages. Status remains **Accepted**.

## Supersedes

- **None.** M28 **adds a time dimension to the GU guardian surface**; it does
  not supersede an earlier ADR. It *rides* ADR 0028 (the GU standing + G·1–G·5
  invariants + the §E audit-verb shape), ADR 0121 (the evaluator + doc shape it
  mirrors, polarity-inverted), ADR 0019 (the wall-clock-first effective-zone
  chain), ADR 0004 §B.1 (the additive doc type on the M1 surface), and ADR 0006-E
  (the additive-seam discipline). The GU invariants G·1–G·5, the frozen
  `GuardianLink` standing read, the `BlockedAccountMiddleware` sign-out shape,
  the frozen `IAuthorizationService` surface, and the entire M20 quiet lane are
  **unchanged** (C-M28·5). This is stated explicitly so a later reader knows M28
  is an **additive guardian time-limit surface, not a correction** — no earlier
  ADR's decisions are revised or re-scoped by this one.

## Affected files

- `src/Kumunita.Core/UserInfo/GuardianTimeLimitSchedule.cs` — new (the
  `TimeLimitMode` enum + the `GuardianTimeLimitSchedule` document) (D2).
- `src/Kumunita.Core/UserInfo/GuardianTimeLimitEvaluator.cs` — new (the pure
  `IsAllowedNow` evaluator) (D3).
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — the three additive seams
  (D4).
- `src/Kumunita.Core/UserInfo/UserInfoService.cs` — the three-seam
  implementation (the active-link + GlobalAdmin gate, the one audit row, the
  strong-consistency read) (D4).
- `src/Kumunita.Core/M1DocTypes.cs` — the `GuardianTimeLimitSchedule`
  registration (next to `GuardianLink`) (D2).
- `src/Kumunita.Web/Security/TimeLimitMiddleware.cs` — new (the enforcement
  gate) (D5).
- `src/Kumunita.Web/Program.cs` — the `UseMiddleware<TimeLimitMiddleware>()`
  registration (after `BlockedAccountMiddleware`) (D5).
- `src/Kumunita.Web/Controllers/AccountController.cs` — the
  `?error=time-limit` login landing (D5).
- `src/Kumunita.Web/Controllers/GuardianController.cs` +
  `src/Kumunita.Web/Models/GuardianViewModels.cs` — the Detail "Time limits"
  section + the `ChildAccountItem.HasTimeLimits` badge (D6).
- `src/Kumunita.Web/Views/Guardian/Detail.cshtml` — the "Time limits" section
  (D6).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the 14-key × 4
  closed kw-l set (D7).
