# Plan: M28 — Guardian time limits (a per-child allow/block schedule that gates the child's access to the platform)

> **Planned.** This is the **lane register** (secondary tier of the lane's
> three-tier contract) for **M28** — the README / `Milestones.cs` line,
> verbatim: "**Guardian time limits — for a child's account, a
> parent/guardian sets when the child may use the platform: allow or block
> certain hours of each day and days of the week.**" This is a **new lane on
> the GU guardian surface** (ADR 0028 / ADR 0038), the **inverse of the M20
> notification-quiet lane** (ADR 0121): M20 lets a *resident* hold their own
> *notification emails* during a window they choose; M28 lets a *guardian*
> gate a *child's* *whole-platform access* during a window the guardian
> chooses. It reuses the M20 **pure-evaluator shape** (the
> `QuietScheduleEvaluator.IsQuietNow(schedule, now, zone)` discipline,
> wall-clock-first per ADR 0019) but flips the **owner** (guardian, not
> resident), the **polarity** (allowed/restricted, not quiet/held), and the
> **effect** (a sign-out access gate, not an email deferral). **U00**
> authors the primary-tier design doc + **ADR 0151** (the next free number —
> the index runs 0001–0150; U00 verifies 0151 is free against
> `docs/adr/README.md` and on disk). **U01** adds the
> `GuardianTimeLimitSchedule` doc + `TimeLimitMode` enum on the existing
> `M1DocTypes` GU surface. **U02** authors the pure
> `GuardianTimeLimitEvaluator.IsAllowedNow`. **U03** adds the three
> `IUserInfoService` seams (guardian read / guardian write / enforcement
> read) + their implementation + the standing/audit pins. **U04** ships the
> `TimeLimitMiddleware` (the `BlockedAccountMiddleware` analog) + the
> `?error=time-limit` login landing. **U05** lands the closed `kw-l` key set
> × 4. **U06** ships the guardian's Detail-section surface. **U07/U08** run
> + record the acceptance gate. **U09** flips the close — **M28 is the LAST
> milestone on the roadmap**, so the close reframes `MilestonesTests` from
> "M28 is the single in-progress" to "the roadmap is fully shipped."
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M27 / M20 / M3 style
> (≤ ~4 files / ~400 LOC, 3–6 entry reads, one build + test run).
> **Zero new `AccessAction`**, **zero new `AccessVia`**, **zero new
> `Decide()` branch**, **zero new `IAuthorizationService` surface** (the
> verdict is a pure function; the enforcement is middleware; the standing is
> the existing GU `GuardianLink`). **One new document**
> (`GuardianTimeLimitSchedule`, **per-child**, id = `ChildId` — the exact
> `NotificationQuietSchedule` "one row per subject" shape, ADR 0121 D2).
> **One new enum** (`TimeLimitMode { Blocked, Allowed }` — the
> `QuietScheduleMode` shape). **Three additive seams on
> `IUserInfoService`** (ADR 0006-E compatible ADDs — the M27 / M3 / GU
> precedent). **One new middleware** (`TimeLimitMiddleware`, the
> `BlockedAccountMiddleware` shape). **No EF migration** (a new Marten doc
> type is additive per ADR 0004 §B.1; the delta is applied idempotently at
> boot on the existing `M1DocTypes` surface — the `GuardianLink` neighbor).
> **One new route** for the write (a section on the existing
> `/me/children/{childId}` Detail surface — no new controller, no new route
> for the read; the enforcement is a middleware, not a route). **No roadmap
> letter moves** (M28 stays the last M-milestone; the roadmap is complete
> after the close).

## Understanding (one paragraph)

Today a GU (ADR 0028) supervisor can suspend a child's account (the
`Profile.Blocked` flag, enforced by the `BlockedAccountMiddleware`), curate
their community/group memberships, approve a group invitation, and dissolve
the link (independence) — but the supervision is **binary**: a child is
either fully blocked or fully open. There is no *time* dimension. A parent
who wants "no platform from 22:00 to 07:00" today has no path short of
suspending the account (which is the wrong tool — it's a total block, not a
windowed one, and it reads as a punishment in the audit log). **M28 adds the
time dimension, at the guardian layer.** A guardian sets a per-child
schedule — a **Blocked** window ("no platform DURING these hours/days") or
an **Allowed** window ("platform EXCEPT these hours/days") over hours-of-day
× days-of-week, evaluated in **the child's effective time zone** (ADR 0019).
When the child's account is **outside the allowed window**, the child is
**signed out** and lands on a distinct "you're outside your allowed hours"
message (the `BlockedAccountMiddleware` shape, the `?error=blocked` landing
pattern, but a distinct `?error=time-limit` code + message). This is the
**exact inverse of M20's quiet lane**: M20's `NotificationQuietSchedule` is
per-*resident*, *self*-set, and *defers the email* (a best-effort side
effect); M28's `GuardianTimeLimitSchedule` is per-*child*, *guardian*-set,
and **gates access** (a hard sign-out). The two are deliberately **separate**
documents on **separate** surfaces (the M20 `NotificationService` owner-scope
seams are untouched; M28 adds its own seams on `IUserInfoService`, the GU
surface) — but they **share the evaluator discipline** (a pure function of
(schedule, instant, effective zone), wall-clock-first, the M20 C-M20·3 "the
floor: a missing/disabled schedule is never restricted" pin). The
**`ChildAccountItem`** GU Index card gains a "time-limits set?" badge (the
`Blocked` flag precedent); the **Detail** page gains a "Time limits" section
(the GU Detail page's existing suspend / membership / invitation sections as
the shape to mirror). **No child self-lane** (the child cannot set or clear
their own time limits — that is the whole point of the control); a
**GlobalAdmin** can clear them (the G·5 safety valve, the
`DissolveGuardianLinkAsync(viaAdmin: true)` audit shape).

## The one thing every unit must respect

**Guardian-time-limit semantics (locked in ADR 0151, U00):**

- **The verdict is a pure function — not an authorization decision.**
  `GuardianTimeLimitEvaluator.IsAllowedNow(schedule, now, effectiveZone)`
  is a `static` pure function (the exact `QuietScheduleEvaluator.IsQuietNow`
  shape — no session, no HTTP, no IO). It converts `now` to the child's
  effective zone's **wall clock FIRST** (ADR 0019, the `kw-dt` TagHelper +
  `EventReminderService` discipline), then evaluates `Mode` / `Hours` /
  `DaysOfWeek`. **`null` schedule or `Enabled == false` → allowed, always**
  (the floor, C-M28·3). **No** `CanAsync` / `CanSeeAsync` call, **no**
  `Decide()` branch, **no** `AccessAudit` row on the **enforcement** read
  (C-M28·2). The `IAuthorizationService` surface stays byte-identical
  (C-M28·5).
- **The polarity is permission, not restriction.** `IsAllowedNow` returns
  **true = the child MAY use the platform now**. `TimeLimitMode.Blocked`
  (the lean default) → the child is **restricted DURING** the listed
  window; `TimeLimitMode.Allowed` → the child is **restricted EXCEPT** the
  listed window (an allow-list — the child may use the platform only during
  the listed window). This is the **mirror** of M20's `IsQuietNow`
  (M20: `Blocked` → quiet during the window; M28: `Blocked` → restricted
  during the window). A schedule with both `Hours` and `DaysOfWeek` empty
  is "all hours × all days" — `Blocked` → always restricted (the
  "total block" reading), `Allowed` → always allowed (the floor).
- **The effect is a sign-out gate, not a 403 and not a 404.** The
  `TimeLimitMiddleware` (the `BlockedAccountMiddleware` analog) runs on
  **every** authenticated request, **after** `BlockedAccountMiddleware`
  (a fully-blocked account hits the `blocked` landing first). If the child
  is restricted, the middleware **signs out** (clears both cookies via
  `SignInManager.SignOutAsync`, exactly as `BlockedAccountMiddleware`) and
  lands the child on the login page with `?error=time-limit` (a **new**
  distinct code — the `?error=blocked` precedent on
  `AccountController.Login` GET — mapped to a `kw-l` key). A restricted
  child cannot *see* a restricted page — they are out. (C-M28·1.)
- **The write is guardian-scoped (∪ GlobalAdmin), NOT owner-scoped.**
  Three additive seams on `IUserInfoService` (ADR 0006-E compatible ADDs):
  `GetChildTimeLimitAsync(guardianId, childId)` (the guardian's edit-page
  read — no audit), `SetChildTimeLimitAsync(guardianId, childId, schedule?)`
  (the guardian's write — **one** `AccessAudit` row, `Via: Guardian` or
  `Via: Admin`, `TargetKind` the child account, verb
  `guardian.time-limit.set` — the `guardian.suspend` /
  `guardian.unsuspend` verb shape), and `GetActiveTimeLimitAsync(childId)`
  (the **enforcement** read, child-keyed, no guardian, no audit — the
  middleware's single-row load). The write gate is an **active
  `GuardianLink`** for (guardianId, childId) **∪ GlobalAdmin** (G·2
  deny-by-default, G·5 safety valve); a non-guardian, non-admin gets
  `UnauthorizedAccessException` (Web 404 — the GU standing-lane shape,
  `SuspendChildAsync` precedent). **The child cannot write their own
  schedule** (a child subject used as `guardianId` on their own `childId`
  is denied — the G·3 pin, the "a child cannot approve their own invitation"
  analog, C-M28·7).
- **The M20 lane is untouched.** The M20 `NotificationQuietSchedule` doc,
  the `NotificationService.GetQuietScheduleAsync` /
  `SetQuietScheduleAsync` owner-scope seams, the `LocaleController`
  `/settings/quiet` surface, the `QuietScheduleEvaluator`, and the
  `admin.quiet.*` / `settings.quiet.*` kw-l keys are **all unchanged**.
  M28 adds **separate** seams on `IUserInfoService` (the GU surface), a
  **separate** doc, a **separate** middleware, and a **distinct** kw-l
  namespace (`guardian.timelimit.*` + `account.time_limit.*` — **not**
  `settings.quiet.*`). A resident's M20 quiet hours and a child's M28 time
  limits are **independent** (a child can be out-of-hours AND their
  notifications can be held — the two gates don't interact).
- **The GU invariants are preserved.** G·1 (the guardian standing never
  reads the child's content) is untouched — M28's read is the schedule row
  + the child's active `GuardianLink`, never the child's posts/profile.
  G·2 (standing from the active link, live) is the write gate. G·5 (the
  safety valve) is the GlobalAdmin clear. G·4 (formation is
  creation-based) is unchanged (M28 does not create or dissolve links).
  The `ChildAccountItem` 3-field pin gains a **4th field** (`HasTimeLimits`)
  — a U06-re-pinned record, the `Blocked` flag precedent (U00 records the
  re-pin in the design doc; U06 applies it + re-pins the GU Index test).
- **`Milestones.cs` / README / `MilestonesTests` are untouched until the
  lane *ships* (U09 owns the close flip).** **M28 is the LAST milestone on
  the roadmap** — after the close there is **no** `StatusNext` milestone.
  The `MilestonesTests` pin
  `M28_Is_The_Single_InProgress_Milestone` must be **reframed** to
  "the roadmap is fully shipped" (assert **zero** `StatusNext` + **all**
  done) — a semantic reframe, not a rename. The `WhatsNew.cs` registry
  gains one entry (newest-first, the `0.43.0` row) naming the lane + ADR
  0151 — the M27 "shipped with no entry until caught in review" lesson
  (AGENTS.md) is held (the required **sixth** member of the close flip).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0151 in U00]

> **Open veto.** These are the decisions the user can still change cheaply —
> **before U00 runs**. After U00 they are locked by
> `docs/design/m28-guardian-time-limits-design.md` + ADR 0151 and changeable
> only via the drift guard.

- **D1 · Scope = a per-child schedule that gates the child's whole-platform
  access.** In: the `GuardianTimeLimitSchedule` doc (per-child, id =
  `ChildId`), the `TimeLimitMode` enum, the pure
  `GuardianTimeLimitEvaluator`, the three `IUserInfoService` seams +
  implementation, the `TimeLimitMiddleware` + its `Program.cs` registration,
  the `?error=time-limit` login landing, the guardian's Detail "Time limits"
  section (+ the `ChildAccountItem` `HasTimeLimits` badge), the closed kw-l
  key set × 4, and the seam tests + the recorded acceptance gate. **Out of
  scope:** a *resident* self-service time limit (a non-guardian version — a
  **new** lane, the inverse of this one), **per-feature** restrictions
  ("allowed to browse but not message" — this is a **whole-platform** gate,
  not feature-scoped), an **awareness / reminder** lane ("your parent has
  enabled time limits" — M28 is enforcement, not notification), and
  **enforcement-history** auditing (which requests were denied — the
  middleware is silent; a future lane could emit a `UsageEvent`, the
  M13 ADR 0114 capture shape). **Alternative considered and rejected:**
  making the time limit a `Profile` field (the `Profile.Blocked` /
  `Profile.MessagingOptIn` shape) — rejected because the schedule is a
  structured (mode + hours + days) value that belongs in its own doc (the
  M20 `NotificationQuietSchedule` precedent) and because a `Profile` field
  would be read by the `KumunitaClaimsPrincipalFactory` at mint time (too
  early + too wide) whereas a dedicated read seam is read fresh per request
  (the ADR 0050 `IsSignupOpen` strong-consistency shape, C-M28·4).
- **D2 · The doc is per-child (id = `ChildId`), registered on the existing
  `M1DocTypes` GU surface (the `GuardianLink` neighbor).** One row per child
  — the exact `NotificationQuietSchedule` "one row per subject" shape (ADR
  0121 D2), with `ChildId` in place of `RecipientId`. Registered **next to
  `GuardianLink`** on `M1DocTypes` (the GU lane's surface, ADR 0028 §B; the
  ADR 0004 §B.1 additive-doc-type precedent — delta-detected, idempotent, no
  new `*DocTypes` surface, no new boot-path wiring, no EF migration).
  **Why `M1DocTypes` and not a new `M28DocTypes`:** it is **part of the GU
  guardian-lane surface** (the "lane on the M1 surface, not a new bounded
  context" shape, the M27 D7 / SITE "one new doc type" precedent); the
  cheapest + most consistent choice. The seeder does **not** seed a
  schedule for any child (a fresh child account has no schedule — the
  C-M28·3 floor).
- **D3 · The verdict is a pure function (mirrors M20 C-M20·5).**
  `GuardianTimeLimitEvaluator.IsAllowedNow(schedule, now, effectiveZone)`
  → `bool` in `Kumunita.Core.UserInfo`. `null` / `Enabled == false` →
  **allowed** (the floor, C-M28·3). Wall-clock-first (ADR 0019). The
  evaluator does **not** call `IAuthorizationService`, **does not** open a
  session, and **does not** emit an audit row (C-M28·2). It is a **separate**
  pure function from M20's `QuietScheduleEvaluator` (so the `UserInfo`
  context does not depend on `Notifications`), but **shares the exact
  evaluation discipline** (the `Hours`/`DaysOfWeek` window test + the
  `Mode` polarity), so a reader can see the two side by side.
- **D4 · The write lane is guardian-scoped (∪ GlobalAdmin), NOT owner-scoped,
  and IS audited.** Three additive seams on `IUserInfoService` (ADR 0006-E):
  - `Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(string guardianId, string childId)` —
    the guardian's edit-page read; gate = active `GuardianLink` ∪ GlobalAdmin;
    **no** audit row (a read).
  - `Task SetChildTimeLimitAsync(string guardianId, string childId, GuardianTimeLimitSchedule? schedule, CancellationToken ct = default)` —
    the guardian's write; gate = active `GuardianLink` ∪ GlobalAdmin
    (deny → `UnauthorizedAccessException`, Web 404); **one** `AccessAudit`
    row (`Via: Guardian` for a guardian write, `Via: Admin` for a GlobalAdmin
    write — the G·5 safety valve; `TargetKind` the child account; verb
    `guardian.time-limit.set`); a `null` schedule **deletes** the row (the
    M20 `SetQuietScheduleAsync(recipientId, null)` "clear" shape, the
    `session.Delete` idiom).
  - `Task<GuardianTimeLimitSchedule?> GetActiveTimeLimitAsync(string childId)` —
    the **enforcement** read (child-keyed, no guardian, **no** audit row) —
    the middleware's single-row load (the `BlockedAccountMiddleware`
    `GetProfileAsync` shape).
  **Strong consistency** (C-M28·4): a save is live on the very next
  `GetActiveTimeLimitAsync` / middleware read (no projection, no cache —
  the ADR 0050 `IsSignupOpen` shape).
- **D5 · Enforcement is a new middleware, NOT a `Decide()` branch.**
  `TimeLimitMiddleware` in `Kumunita.Web.Security`, the exact
  `BlockedAccountMiddleware` shape (scoped-resolution from
  `RequestServices`, the single-row `GetActiveTimeLimitAsync` load, the
  `SignInManager.SignOutAsync` + the `?error=time-limit` redirect).
  Registered in `Program.cs` **after** `BlockedAccountMiddleware` (a
  blocked account hits the `blocked` landing first). **Zero** new
  authorization surface (C-M28·5): no `AccessAction`, no `AccessVia`, no
  `Decide()` branch, no adapter, no `IAuthorizationService` signature.
  The `?error=time-limit` code maps to a new `kw-l` key on
  `AccountController.Login` GET (the `?error=blocked` precedent).
- **D6 · The surface is the guardian's Detail section — no self-lane.**
  A "Time limits" section on the child's `/me/children/{childId}` Detail
  page (the GU Detail page's existing suspend / membership / invitation
  sections as the shape): GET seeds the form with the current schedule
  (`GetChildTimeLimitAsync`), POST saves / clears (`SetChildTimeLimitAsync`),
  bound to the `guardian.timelimit.*` kw-l keys. The `ChildAccountItem` GU
  Index card gains a `HasTimeLimits` badge (the `Blocked` flag precedent —
  U06 re-pins the 3-field → 4-field record + the GU Index test). **No
  `/settings/time-limit`** (the child cannot set/clear their own — C-M28·7).
  A GlobalAdmin reaches the same write seam (the G·5 safety valve) — the
  Detail page is the single surface; the GlobalAdmin standing is resolved by
  the seam, not a separate admin page.
- **D7 · kw-l keys (a distinct namespace, × 4).** The closed set: the
  guardian Detail section (`guardian.timelimit.title`,
  `guardian.timelimit.description`, `guardian.timelimit.enabled`,
  `guardian.timelimit.mode_label`, `guardian.timelimit.mode_blocked`,
  `guardian.timelimit.mode_allowed`, `guardian.timelimit.hours_label`,
  `guardian.timelimit.days_label`, `guardian.timelimit.save`,
  `guardian.timelimit.clear`, `guardian.timelimit.flash_saved`,
  `guardian.timelimit.flash_cleared`, `guardian.timelimit.badge_set`) + the
  login landing (`account.time_limit.login_message`). **Must land in all
  four** `KnownTranslationKeys` dicts (the `en`/`de`/`fr`/`da` parity
  pin — the M27 "all four dicts" lesson). **Not** the M20 `settings.quiet.*`
  / `admin.quiet.*` keys (those are the notification lane's, unchanged).
  (U05 lands these; U00 pins the exact closed set in the design doc.)
- **D8 · Tests: a pure-evaluator seam set + a standing/audit seam set + a
  middleware seam set + a surface seam set, and the three-test acceptance
  gate.** The three acceptance tests (closed-loop / handoff / part-vs-whole)
  + the Web pins. (U00 pins the exact names; U07 authors; U08 records.)
- **D9 · Deferred lanes (named).** (a) A *resident* self-service time limit
  (the inverse of this lane — a non-guardian version); (b) **per-feature**
  restrictions (browse-yes / message-no — a **whole-platform** gate is M28's
  scope); (c) an **awareness / reminder** lane ("your parent has enabled
  time limits"); (d) **enforcement-history** auditing (which requests were
  denied — the M13 ADR 0114 `UsageEvent` capture shape, deferred); (e) a
  **per-feature** or **per-content** time limit (a future lane). Each is a
  **new, ADR-gated** decision, not a toggle (the ADR 0028 G·1 "we decided
  not to" vs "it cannot happen" discipline).

## Invariants (pinned by U00 into the design doc)

- **C-M28·1 — The time limit gates the whole platform, not a subset.** A
  restricted child is **signed out** (all routes), exactly like a blocked
  account (`BlockedAccountMiddleware`) — a sign-out, not a route-by-route
  403/404. (D5.)
- **C-M28·2 — The verdict is a pure function, not an authorization decision.**
  No `CanAsync` / `CanSeeAsync` call, no `Decide()` branch, no `AccessAudit`
  row on the enforcement read. (D3.)
- **C-M28·3 — The floor: a missing or disabled schedule is never
  restricted.** `null` schedule or `Enabled == false` → allowed, always. (D3.)
- **C-M28·4 — Guardian standing is from the active link, live; GlobalAdmin
  is the safety valve; strong consistency.** The write gate = active
  `GuardianLink` ∪ GlobalAdmin; a save is live on the next read. (D4.)
- **C-M28·5 — Zero new authorization surface.** No `AccessAction`, no
  `AccessVia`, no `Decide()` branch, no `IAuthorizationService` signature,
  no adapter. The frozen 4-method surface stays byte-identical. (D5/D7.)
- **C-M28·6 — The zone is the child's effective zone (ADR 0019),
  wall-clock-first.** (D3.)
- **C-M28·7 — The child cannot set/clear their own time limit (no self-
  lane).** The only write is the guardian-gated (∪ GlobalAdmin) seam. (D6.)

## FACES (pinned by U00)

- **F1 — Blocked mode, in-window → restricted; out-of-window → allowed.**
  (The "no platform at night" reading.)
- **F2 — Allowed mode, in-window → allowed; out-of-window → restricted.**
  (The "only allowed during these hours" reading.)
- **F3 — The floor: no schedule / disabled → always allowed.** (C-M28·3.)
- **F4 — Zone-dependent verdict.** (C-M28·6.)
- **F5 — Guardian write requires an active link (∪ GlobalAdmin); a
  non-guardian is denied (404).** (C-M28·4/C-M28·7.)
- **F6 — The enforcement signs out (not a 403) and lands on a distinct
  message.** (C-M28·1/C-M28·5.)

## What M28 builds on (frozen, verified seams)

- **The M20 quiet lane (ADR 0121) — the evaluator shape to mirror.**
  `src/Kumunita.Core/Notifications/QuietScheduleEvaluator.cs` (the pure
  `IsQuietNow(schedule, now, zone)` + the wall-clock-first ADR 0019
  discipline + the `null`/`Enabled` floor),
  `src/Kumunita.Core/Notifications/NotificationQuietSchedule.cs` (the
  per-subject doc: `Enabled` master, `Mode` enum, `Hours` int[], `DaysOfWeek`
  int[], `Updated?`), `src/Kumunita.Core/Notifications/NotificationService.cs`
  (the `GetQuietScheduleAsync` / `SetQuietScheduleAsync` owner-scope read /
  write shape U03 mirrors), `src/Kumunita.Core/M6DocTypes.cs` (the
  `NotificationQuietSchedule` registration U01 mirrors),
  `tests/Kumunita.Core.Tests/NotificationServiceTests.cs` (the
  `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` + the
  `IsQuietNow_Missing_Schedule_Is_Never_Quiet` pure tests U02 mirrors),
  `docs/adr/0121-notification-quiet-times.md` (the ADR shape U00 mirrors).
- **The GU guardian lane (ADR 0028 / ADR 0038) — the standing + surface to
  extend.** `src/Kumunita.Core/UserInfo/GuardianLink.cs` (the active-link
  standing the write gate reads), `src/Kumunita.Core/UserInfo/IUserInfoService.cs`
  + `UserInfoService.cs` (the GU standing lanes — `SuspendChildAsync` /
  `DissolveGuardianLinkAsync` / the active-link read seams U03 mirrors for
  the gate + the audit shape), `src/Kumunita.Web/Controllers/GuardianController.cs`
  + `src/Kumunita.Web/Models/GuardianViewModels.cs` (the GU Index / Detail
  surface + the `ChildAccountItem` 3-field pin U06 re-pins),
  `src/Kumunita.Core/M1DocTypes.cs` (the `GuardianLink` registration U01
  mirrors — the doc's new surface).
- **The blocked-account enforcement (the middleware + landing pattern to
  mirror).** `src/Kumunita.Web/Security/BlockedAccountMiddleware.cs` (the
  exact shape U04 mirrors — scoped-resolution, the single-row load, the
  `SignInManager.SignOutAsync` + the `?error=blocked` redirect),
  `src/Kumunita.Web/Controllers/AccountController.cs` (the
  `?error=blocked` → `kw-l` key mapping U04 adds `?error=time-limit` to),
  `src/Kumunita.Web/Program.cs` (the `UseMiddleware<BlockedAccountMiddleware>()`
  registration U04 adds the `TimeLimitMiddleware` after).
- **The ADR 0019 zone resolution (the wall-clock-first discipline).**
  `src/Kumunita.Web/Localization/EffectiveTimezoneResolver.cs` (the
  effective-zone chain the middleware + the evaluator resolution use),
  `src/Kumunita.Core/Events/EventReminderService.cs` (the
  `TryResolveZone` / `TimeZoneInfo.ConvertTime` wall-clock-first shape U04
  mirrors in the middleware).
- **The closed kw-l registry (the 4-dict parity pin).**
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (U05 adds the
  closed set × 4), `tests/Kumunita.Web.Tests/*ParityTests*` (the parity pin
  U05 must keep green).
- **The milestone-flip contract — moved in the close unit (U09).**
  `src/Kumunita.Web/Milestones.cs` (the M28 row — `StatusNext` →
  `StatusDone`; **no** next milestone — the roadmap is complete),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  `M28_Is_The_Single_InProgress_Milestone` pin U09 **reframes** to
  "the roadmap is fully shipped"), `README.md` §Roadmap, `docs/STATUS.md`,
  `docs/ARCHITECTURE.md`, `src/Kumunita.Web/WhatsNew.cs` (the `0.43.0` row
  — the required **sixth** member of the close flip).

## Assumptions

- **The schedule is the child's, set by the guardian.** One row per child
  (id = `ChildId`). A child with **two** guardians shares **one** schedule
  (the `GuardianLink` multi-guardian shape — the second guardian sets/clears
  the **same** row via the same guardian-gated seam; there is no per-
  guardian schedule). This is the "one row per subject" M20 shape, not the
  "one row per (guardian, child)" `GuardianLink` shape.
- **The effective zone is the child's.** The child's ADR 0019 chain
  (override → platform default → UTC floor) — **not** the guardian's zone.
  A parent in a different zone from the child still gates on the **child's**
  clock (the child is the one being restricted; their "night" is their own).
- **The enforcement is synchronous per request.** The middleware loads the
  schedule on every authenticated request (one `mt` round-trip — the
  `BlockedAccountMiddleware` cost profile). A schedule save is live on the
  next request (strong consistency, C-M28·4). **No** background flush, **no**
  scheduled job (unlike M20's cadence — M28 has no held-email to release).
- **The child sees the landing, not the cause-of-restriction in a
  privileged sense.** The `?error=time-limit` message tells the child they
  are outside their allowed hours; it does **not** reveal which hours
  (G·1-adjacent — the restriction is the fact; the window is the
  guardian's, and a child reading their own window is fine, but the
  **message** is a generic "outside your allowed hours," the
  `?error=blocked` generic-message precedent).
- **A `Blocked` (fully-suspended) account is handled by M1, not M28.** The
  `Profile.Blocked` flag + `BlockedAccountMiddleware` run **first** (the
  middleware order in `Program.cs`); a fully-blocked child never reaches
  the time-limit check. M28 is the **time-window** layer, not the
  total-block layer.

## The close-flip contract (U09, the required six members)

**M28 is the LAST milestone on the roadmap.** The close is a **six-member**
flip (the M27 C-M27·8 + the `WhatsNew.cs` sixth-member lesson, AGENTS.md):

1. **`src/Kumunita.Web/Milestones.cs`** — the `M28` row → `StatusDone`.
   **There is no next milestone** (the roadmap is complete). The **order**
   of `Milestones.All` is **unchanged** (`…, M26, M27, SITE, M28`) — a
   status bump, not a renumber (the "named lane, not a renumber"
   precedent).
2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** —
   - `Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order` —
     **unchanged** (the order does not move).
   - `Shipped_Milestones_Are_Marked_Done` — **add `"M28"`** to the
     asserted done set (now **every** M-milestone is done).
   - **Reframe** `M28_Is_The_Single_InProgress_Milestone` →
     `Roadmap_Is_Fully_Shipped_No_InProgress_Milestone`: assert
     **zero** `StatusNext` rows (the roadmap is complete) + **all** rows
     `StatusDone`. (A semantic reframe, not a rename — M28 is the last, so
     the "single in-progress" premise no longer holds.)
   - `No_Milestone_Has_Blank_Title` — **unchanged**.
3. **`README.md`** §Roadmap — the `M28` line → **Done** (the guardian time
   limits shipped). The roadmap is complete (every line Done).
4. **`docs/STATUS.md`** — the M28 line → **Done**; the "next" line (if any)
   removed or the "all shipped" note added (U09 reads the current shape and
   mirrors it — the M27 "M27 done, M28 next" flip is the **last**
   next-pointing flip; M28's flip points to **nothing**).
5. **`docs/ARCHITECTURE.md`** — the M28 line → **shipped**; the `UserInfo/`
   context's GU-lane description gains the time-limit seam (the "lane on
   the M1 surface" note, U01's doc registration).
6. **`src/Kumunita.Web/WhatsNew.cs`** — add the **`0.43.0`** row (newest-
   first) naming the lane + ADR 0151 (the "Guardian time limits — a
   parent/guardian sets when the child may use the platform" entry). The
   **required sixth member** of the close flip (the M27 lesson).

Plus (the loop-closing step, the M27 U13 / M3 U11 analog): append
`## M28 — Closed (recorded)` to
`docs/design/m28-guardian-time-limits-design.md` (last), move the M28
unit-plan files `docs/plans-milestones/in-progress/` →
`docs/plans-milestones/done/` (the **flat** `done/` convention, the M27
precedent + the user's stated storage instruction), and append the
`## Summary` to `docs/plans-milestones/in-progress/m28-handoff-notes.md`.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U09
below), one unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**
- **Primary tier** — `docs/design/m28-guardian-time-limits-design.md`
  (authored by **U00**) — the invariants + FACES + the doc / enum /
  evaluator / seam / middleware / kw-l pins + the acceptance gate + the
  drift guard. Locked before any code unit runs.
- **Secondary tier — this file**
  (`docs/plans-milestones/plan-m28-guardian-time-limits.md`) — the unit
  register: one row per unit, each with Goal / Entry reads / Deliverables /
  Exit.
- **Scratch tier — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m28-handoff-notes.md`, created by
  **U00**). One `## U#` section per unit, appended (never rewritten). Each
  unit writes exactly one short section before it exits; the next unit
  reads only that section + its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 3–6 files <~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~4 files / ~400 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects + the unit's seam
tests discovered; handoff-note entry appended *before* any follow-up
action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.8
drift-guard; (3) never introduces a test whose exact name is not in the
design doc §2.6 seam list (U00 pins the names); (4) never opens a *new*
seam on `IUserInfoService` / `IAuthorizationService` / `IIdentityService`
beyond what U00 pinned (D3–D7); (5) never re-shapes the `GuardianTimeLimitSchedule`
doc / `TimeLimitMode` enum outside the §2.2 pin; (6) never adds a new
`AccessAction` / `AccessVia` / `Decide()` branch / `IAuthorizationService`
signature (C-M28·5); (7) if entry reads reveal the design doc is out of
date, the unit pauses and records `## U<m> — Drift pause` in the handoff
note.

**The reliable test path (per AGENTS.md — the in-process xunit.v3 runner):**

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

(`dotnet` is NOT on PATH in the agent terminal — use the full path
`C:\Program Files\dotnet\dotnet.exe`; the in-process runner uses
**single-dash** flags (`-class`, `-method`, `-filter`), NOT `--`.)

---

## Units (10 total)

### U00 — Design doc + ADR 0151 (the sign-off gate)
- **Goal:** author `docs/design/m28-guardian-time-limits-design.md` (Context,
  Scope in/out, invariants C-M28·1–C-M28·7, FACES F1–F6, the
  `GuardianTimeLimitSchedule` field pin, the `TimeLimitMode` enum pin, the
  pure-evaluator pin, the three-seam pin on `IUserInfoService`, the
  middleware pin, the closed kw-l key list, the acceptance-gate test names,
  the D9 deferred-lane list, the drift-guard) + `docs/adr/0151-guardian-
  time-limits.md` + one `docs/adr/README.md` index row (after the 0150 row).
  **No code, no build.**
- **Entry reads:** the register's [PROPOSED] D1–D9 set (above), the
  invariants + FACES (above), `docs/design/m20-notification-quiet-times-
  design.md` § (the D2/D3/D7 invariants + the C-M20·3/5/7 pins + the §10
  kw-l + §11 gate — the **primary** shape to emulate),
  `docs/adr/0121-notification-quiet-times.md` (the ADR shape to mirror),
  `src/Kumunita.Core/Notifications/QuietScheduleEvaluator.cs` +
  `NotificationQuietSchedule.cs` (the pure-evaluator + doc shape U00 pins
  verbatim), `docs/adr/0028-guardian-controls-account-scope-supervision.md`
  (the GU standing + G·1–G·5 invariants + the §E audit verbs U00 references),
  `docs/adr/0149-*.md` or `docs/adr/0150-*.md` (the **most recent** ADR —
  the index + the "Amends/additive" doc-comment shape U00 mirrors),
  `docs/adr/README.md` (the index — confirm 0151 is free after the 0150
  row), `docs/philosophy/templates/design-doc.md` (the required section
  set).
- **Deliverables (3 files, new + 1 modify):**
  1. `docs/design/m28-guardian-time-limits-design.md` (~200 lines) — the
     full primary tier (the section set above, locked from the [PROPOSED]
     set, recording any refinement where the source says more).
  2. `docs/adr/0151-guardian-time-limits.md` (new) — Status Accepted /
     Context / Decision (the D1–D9 set + the C-M28·1–7 invariants + the
     F1–F6 FACES) / Consequences, mirroring ADR 0121's shape + ADR 0028's
     GU framing. **Amends** 0028 (a new GU seam + a new GU doc —
     compatible ADDs) + references 0121 (the evaluator shape it mirrors) +
     0019 (the zone discipline) + 0004 (the additive doc type).
  3. `docs/adr/README.md` (modify) — one index row after the 0150 row:
     `| 0151 | Guardian time limits: a per-child allow/block schedule that gates the child's platform access (guardian-set; mirrors ADR 0121's evaluator; the GU-lane inverse of the M20 quiet lane) | Accepted |`.
  4. `docs/plans-milestones/in-progress/m28-handoff-notes.md` (new) — the
     scratch-tier note, seeded with the `## U00 — design doc + ADR 0151`
     section (the 7 invariants by id, the 6 FACES by id, the 3 seam names,
     the kw-l key count, the ADR 0151 pointer).
- **Exit:** the design doc + ADR + index row exist and are internally
  consistent (the [PROPOSED] set locked, or refined + recorded). **No
  build.** Handoff note: 6–8 lines.

### U01 — `GuardianTimeLimitSchedule` doc + `TimeLimitMode` enum + `M1DocTypes` registration
- **Goal:** create the `GuardianTimeLimitSchedule` POCO + the
  `TimeLimitMode` enum (namespace `Kumunita.Core.UserInfo`) and register
  the doc on the existing `M1DocTypes` GU surface (next to
  `GuardianLink`). Mirrors the `NotificationQuietSchedule` doc shape
  (per-subject, the `Enabled`/`Mode`/`Hours`/`DaysOfWeek`/`Updated?`
  fields, identity pinned to `ChildId`). **No service, no test, no
  middleware yet.**
- **Entry reads:** the design doc §2.2 (the exact doc + enum pin),
  `src/Kumunita.Core/Notifications/NotificationQuietSchedule.cs` (the doc
  shape to mirror — the `Enabled` master, the `Mode` enum, the `Hours`
  int[], the `DaysOfWeek` int[], the `Updated?`),
  `src/Kumunita.Core/M6DocTypes.cs` (the `NotificationQuietSchedule`
  registration U01 mirrors — the identity pin + the `Schema.For` shape),
  `src/Kumunita.Core/M1DocTypes.cs` (the GU surface — the
  `opts.Schema.For<GuardianLink>()` line U01 adds the new doc next to),
  `docs/adr/0121-notification-quiet-times.md` §D2 (the doc's invariants —
  the `Enabled` floor, the empty-array = "all" reading).
- **Deliverables (≤ 3 files):**
  1. `src/Kumunita.Core/UserInfo/GuardianTimeLimitSchedule.cs` (new) — the
     POCO: `ChildId` (identity, the `RecipientId` analog), `Enabled`
     (default `false` = never restricted = the floor, C-M28·3), `Mode`
     (`TimeLimitMode`, default `Blocked`), `Hours` (int[], 0–23, empty =
     all), `DaysOfWeek` (int[], 0=Sun…6=Sat, empty = all), `Updated?`. +
     the `TimeLimitMode` enum: `Blocked = 0` (restricted DURING the window,
     the lean default), `Allowed = 1` (restricted EXCEPT the window, the
     allow-list). (One file, the doc + enum — the `NotificationQuietSchedule`
     + `QuietScheduleMode` same-file shape.)
  2. `src/Kumunita.Core/M1DocTypes.cs` (modify) — add
     `opts.Schema.For<GuardianTimeLimitSchedule>()` (identity pinned to
     `ChildId`, mirroring the M6DocTypes `NotificationQuietSchedule`
     registration) **next to** the `GuardianLink` line.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The doc + enum
  compile; the `M1DocTypes` line added. **No test** (U02's evaluator tests
  are the first M28 tests). Handoff note: 4 lines (the doc field list, the
  enum values, the `M1DocTypes` line number, any compile warnings).

### U02 — The pure `GuardianTimeLimitEvaluator` + the pure-evaluator tests
- **Goal:** author the `static` pure `GuardianTimeLimitEvaluator`
  (namespace `Kumunita.Core.UserInfo`) + the F1/F2/F3/zone pure tests. The
  exact mirror of `QuietScheduleEvaluator.IsQuietNow`, but
  `IsAllowedNow(schedule, now, zone)` → `bool` (true = the child MAY use
  the platform now). **No service, no middleware, no Web.**
- **Entry reads:** the design doc §2.3 (the evaluator pin) + §2.6 (the
  F1–F4 pure-test names), `src/Kumunita.Core/Notifications/QuietSchedule-
  Evaluator.cs` (the **primary** shape to mirror — the wall-clock-first
  ADR 0019 conversion + the `null`/`Enabled` floor + the `Hours`/
  `DaysOfWeek` window test + the `Mode` polarity),
  `src/Kumunita.Core/UserInfo/GuardianTimeLimitSchedule.cs` (U01's doc +
  enum), `tests/Kumunita.Core.Tests/NotificationServiceTests.cs` (the
  `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` +
  `IsQuietNow_Missing_Schedule_Is_Never_Quiet` tests U02 mirrors for the
  M28 polarity).
- **Deliverables (2 files):**
  1. `src/Kumunita.Core/UserInfo/GuardianTimeLimitEvaluator.cs` (new) —
     `public static bool IsAllowedNow(GuardianTimeLimitSchedule? schedule,
     DateTimeOffset now, TimeZoneInfo effectiveZone)`: `null` /
     `Enabled == false` → `true` (allowed, the floor, C-M28·3); else
     wall-clock-first (`TimeZoneInfo.ConvertTime(now, effectiveZone)`,
     ADR 0019); `Blocked` mode → allowed iff the wall-clock is
     **OUTSIDE** the listed window (i.e., `!windowMatches`); `Allowed` mode
     → allowed iff the wall-clock is **INSIDE** the listed window (i.e.,
     `windowMatches`); where `windowMatches = (Hours empty || Hours
     contains wall.Hour) && (DaysOfWeek empty || DaysOfWeek contains
     (int)wall.DayOfWeek)`. (The **mirror** of M20's `IsQuietNow` — the
     polarity is inverted because here **allowed** is the permission, not
     the restriction.)
  2. `tests/Kumunita.Core.Tests/GuardianTimeLimitEvaluatorTests.cs` (new) —
     the F1–F4 pure tests (the exact names pinned in the design doc §2.6):
     `F3_Missing_Schedule_Is_Always_Allowed`, `F3_Disabled_Schedule_Is_
     Always_Allowed`, `F1_Blocked_InWindow_Is_Restricted`, `F1_Blocked_
     OutOfWindow_Is_Allowed`, `F2_Allowed_InWindow_Is_Allowed`,
     `F2_Allowed_OutOfWindow_Is_Restricted`,
     `F4_Verdict_Differs_Between_Zones` (the M20
     `IsQuietNow_Verdict_Differs_Between_UTC2_And_UTC` analog).
- **Exit:** `dotnet build` green + the 7 pure tests discovered (pass/red
  recorded for U07's gate). Handoff note: 4–5 lines (the `IsAllowedNow`
  polarity note, the 7 test names + status, any compile warnings).

### U03 — The three `IUserInfoService` seams + implementation + the standing/audit tests
- **Goal:** the three additive seams on `IUserInfoService` (the GU
  surface) + the `UserInfoService` implementation (the active-link +
  GlobalAdmin gate, the one audit row, the strong-consistency read) + the
  F5-write / C-M28·4 / C-M28·7 standing + audit + consistency tests. **No
  middleware, no Web surface, no kw-l yet.**
- **Entry reads:** the design doc §2.4 (the three-seam pin) + §2.6 (the
  standing/audit test names), `src/Kumunita.Core/UserInfo/IUserInfoService.cs`
  (the GU standing lanes — `SuspendChildAsync` /
  `DissolveGuardianLinkAsync` / the active-link read seams U03 mirrors for
  the gate + the audit shape), `src/Kumunita.Core/UserInfo/UserInfoService.cs`
  (the GU implementation — the `session.Query<GuardianLink>().Where(l =>
  l.Status == Active)` standing read + the one-`AccessAudit`-row write
  lane U03 mirrors), `src/Kumunita.Core/UserInfo/GuardianLink.cs` (the
  active-link standing U03 reads), `src/Kumunita.Core/Notifications/
  NotificationService.cs` (the `GetQuietScheduleAsync` /
  `SetQuietScheduleAsync` read/write shape U03 mirrors for the
  `LoadAsync` / `Store` / `Delete` + the `null`-delete idiom),
  `docs/adr/0028-guardian-controls-account-scope-supervision.md` §E (the
  GU audit verbs — the `guardian.suspend` / `guardian.unsuspend` shape
  U03's `guardian.time-limit.set` mirrors).
- **Deliverables (≤ 3 files):**
  1. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (modify) — append
     **three** ADDs (verbatim from the design doc §2.4) with doc-comments
     anchored to C-M28·4/5/7 + the ADR 0006-E lane:
     - `Task<GuardianTimeLimitSchedule?> GetChildTimeLimitAsync(string guardianId, string childId);`
     - `Task SetChildTimeLimitAsync(string guardianId, string childId, GuardianTimeLimitSchedule? schedule);`
     - `Task<GuardianTimeLimitSchedule?> GetActiveTimeLimitAsync(string childId);`
  2. `src/Kumunita.Core/UserInfo/UserInfoService.cs` (modify) — implement
     the three seams:
     - `GetChildTimeLimitAsync` — the gate (active `GuardianLink` for
       (guardianId, childId) ∪ GlobalAdmin; deny →
       `UnauthorizedAccessException`); a `LoadAsync` read, **no** audit row.
     - `SetChildTimeLimitAsync` — the same gate; a **write** lane: `null`
       schedule → `session.Delete` (the M20 "clear" idiom); else
       `session.Store` (upsert the singleton-per-child row); **one**
       `AccessAudit` row (`Via: Guardian` or `Via: Admin` (G·5),
       `TargetKind` the child account, verb `guardian.time-limit.set`);
       `SaveChangesAsync` (C3 same-transaction). **Strong consistency**:
       the write is in the caller's session (the M20 owner-scope write
       shape, but guardian-gated).
     - `GetActiveTimeLimitAsync` — a child-keyed `LoadAsync` read (the
       middleware's single-row load), **no** guardian gate (the
       `BlockedAccountMiddleware` `GetProfileAsync` shape), **no** audit row.
  3. `tests/Kumunita.Core.Tests/GuardianTimeLimitStandingTests.cs` (new) —
     the F5-write / C-M28·4 / C-M28·7 tests (the exact names pinned in the
     design doc §2.6): `F5_NonGuardian_Set_Is_Denied`,
     `F5_Guardian_Set_EmitsOneAuditRow_ViaGuardian`,
     `F5_GlobalAdmin_Set_EmitsOneAuditRow_ViaAdmin`,
     `F5_Set_Is_StronglyConsistent_NextReadSeesNewValue`,
     `C_M28_7_Child_Cannot_Set_Own_TimeLimit`,
     `F5_Clear_SetsNull_DeletesRow`,
     `C_M28_5_IAuthorizationService_Surface_Count_Unchanged` (the structural
     pin — the frozen 4-method surface is unchanged).
- **Exit:** `dotnet build` green + the 7 standing/audit tests discovered
  (pass/red recorded for U07's gate). **The `IAuthorizationService` surface
  count is unchanged** (the C-M28·5 pin). Handoff note: 6–8 lines (the 3
  seam signatures, the one-audit-row shape, the gate shape, the 7 test
  names + status, any compile warnings).

### U04 — The `TimeLimitMiddleware` + `Program.cs` registration + the `?error=time-limit` login landing + the middleware tests
- **Goal:** the `TimeLimitMiddleware` (the `BlockedAccountMiddleware`
  analog) + its `Program.cs` registration (after `BlockedAccountMiddleware`)
  + the `?error=time-limit` code on `AccountController.Login` + the 3 F6
  middleware tests. **The enforcement gate (C-M28·1/C-M28·5) — the lane's
  headline mechanic.**
- **Entry reads:** the design doc §2.5 (the middleware pin) + §2.6 (the F6
  test names), `src/Kumunita.Web/Security/BlockedAccountMiddleware.cs` (the
  **primary** shape to mirror — the scoped-resolution, the single-row load,
  the `SignInManager.SignOutAsync` + the `?error=blocked` redirect),
  `src/Kumunita.Web/Controllers/AccountController.cs` (the
  `?error=blocked` → `kw-l` key mapping U04 adds `?error=time-limit` to —
  the `Get-Login` action's error-code switch),
  `src/Kumunita.Web/Program.cs` (the `UseMiddleware<BlockedAccountMiddleware>()`
  registration U04 adds the `TimeLimitMiddleware` **after**),
  `src/Kumunita.Core/Events/EventReminderService.cs` (the `TryResolveZone`
  / effective-zone resolution the middleware uses to resolve the child's
  ADR 0019 zone), `src/Kumunita.Web/Localization/EffectiveTimezoneResolver.cs`
  (the effective-zone chain), `src/Kumunita.Core/UserInfo/
  GuardianTimeLimitEvaluator.cs` (U02's `IsAllowedNow` the middleware
  calls), `tests/Kumunita.Web.Tests/*BlockedAccount*` (the
  `BlockedAccountMiddleware` test U04 mirrors for the F6 tests — if
  present; else the `LocaleControllerQuietSectionTests` shape).
- **Deliverables (≤ 4 files):**
  1. `src/Kumunita.Web/Security/TimeLimitMiddleware.cs` (new) — the exact
     `BlockedAccountMiddleware` shape: anonymous → pass; resolve the
     subject; `IUserInfoService.GetActiveTimeLimitAsync(subject)` → `null` /
     `Enabled == false` → pass (the floor, C-M28·3); resolve the subject's
     **effective zone** (the ADR 0019 chain via `EffectiveTimezoneResolver`
     / the `Profile.TimeZone` override → platform default → UTC floor);
     `GuardianTimeLimitEvaluator.IsAllowedNow(schedule, now, zone)` →
     `true` → pass; `false` → `SignInManager.SignOutAsync()` + redirect
     `/Account/Login?error=time-limit` (the **distinct** code — not
     `blocked`). Scoped-resolution from `RequestServices` (the
     `BlockedAccountMiddleware` captive-dependency note).
  2. `src/Kumunita.Web/Program.cs` (modify) — add
     `app.UseMiddleware<TimeLimitMiddleware>();` **after** the
     `app.UseMiddleware<BlockedAccountMiddleware>();` line (a blocked
     account hits the `blocked` landing first).
  3. `src/Kumunita.Web/Controllers/AccountController.cs` (modify) — the
     `?error=time-limit` code → a new `kw-l` key (the
     `account.time_limit.login_message` — U05 lands the key; U04 references
     it, the `?error=blocked` precedent in the same switch).
  4. `tests/Kumunita.Web.Tests/TimeLimitMiddlewareTests.cs` (new) — the 3
     F6 tests (the exact names pinned in the design doc §2.6):
     `F6_Restricted_SignsOutAndRedirects_TimeLimit`,
     `F6_Allowed_PassesThrough`, `F6_NoSchedule_PassesThrough` (C-M28·3).
- **Exit:** `dotnet build` green + the 3 middleware tests discovered
  (pass/red recorded for U07's gate). **Zero new authorization surface**
  (the C-M28·5 pin — the middleware does **not** call `IAuthorizationService`).
  Handoff note: 6–8 lines (the middleware registration line number, the
  `?error=time-limit` code + the `kw-l` key it maps to, the zone-resolution
  note, the 3 test names + status, any compile warnings).

### U05 — The closed kw-l key set (× 4) + the parity pin
- **Goal:** add the closed `kw-l` key set (the guardian Detail section +
  the login landing) to **all four** `KnownTranslationKeys` dicts
  (en/de/fr/da) + keep the parity pin green. **The M27 "all four dicts"
  lesson, held.**
- **Entry reads:** the design doc §2.7 (the exact closed key list),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
  `EnValues` / `DeValues` / `FrValues` / `DaValues` dicts — the M20
  `settings.quiet.*` / `admin.quiet.*` keys as the **shape** to mirror, the
  4-dict parity shape U05 adds the M28 keys to),
  `tests/Kumunita.Web.Tests/KnownTranslationKeys_ParityTests.cs` (the
  parity pin U05 must keep green — the "every key in EnValues is in all
  four" test), `src/Kumunita.Web/Views/Locale/Quiet.cshtml` (the M20
  quiet-section view — the **shape** of the `kw-l` keys U05's keys mirror
  in the Detail section U06 ships), `docs/adr/0121-notification-quiet-times.md`
  §10 (the M20 closed-key list — the D7 "closed, enumerated set" idiom
  U05 mirrors).
- **Deliverables (≤ 2 files):**
  1. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) —
     add the closed set (the design doc §2.7 list, verbatim) to **all
     four** dicts: `guardian.timelimit.title`,
     `guardian.timelimit.description`, `guardian.timelimit.enabled`,
     `guardian.timelimit.mode_label`, `guardian.timelimit.mode_blocked`,
     `guardian.timelimit.mode_allowed`, `guardian.timelimit.hours_label`,
     `guardian.timelimit.days_label`, `guardian.timelimit.save`,
     `guardian.timelimit.clear`, `guardian.timelimit.flash_saved`,
     `guardian.timelimit.flash_cleared`, `guardian.timelimit.badge_set`,
     `account.time_limit.login_message` (14 keys × 4 languages). The
     `en` values are the canonical source text; `de`/`fr`/`da` are the
     translations (the M20 `settings.quiet.*` translations as the shape —
     U05 provides the 3 non-English values for each key).
  2. `tests/Kumunita.Web.Tests/GuardianTimeLimitKwLParityTests.cs` (new) —
     the closed-set parity pin (the
     `Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages` shape —
     asserts the 14-key `guardian.timelimit.*` + `account.time_limit.*` set
     is present in **all four** dicts + the count is pinned).
- **Exit:** `dotnet build` green + the parity tests green (the 4-dict
  parity pin + the closed-set pin). **The M20 `settings.quiet.*` /
  `admin.quiet.*` keys are unchanged** (the C-M28·5 "M20 untouched" pin).
  Handoff note: 4–5 lines (the 14 key names, the 4-dict presence confirm,
  the parity test name + status, any compile warnings).

### U06 — The guardian's Detail "Time limits" section + the `ChildAccountItem` `HasTimeLimits` badge + the surface tests
- **Goal:** the guardian-facing surface (D6) — a "Time limits" section on
  the child's `/me/children/{childId}` Detail page (the GU Detail page's
  existing suspend / membership / invitation sections as the shape) + the
  `ChildAccountItem` `HasTimeLimits` badge (the `Blocked` flag precedent)
  + the 3 F5-surface tests. **The lane's user-facing surface.**
- **Entry reads:** the design doc §2.8 (the surface pin) + §2.6 (the
  F5-surface test names), `src/Kumunita.Web/Controllers/GuardianController.cs`
  (the GU Index / Detail surface — the `Detail` action U06 extends + the
  `ActiveChildrenAsync` / the `ChildAccountItem` projection U06 re-pins),
  `src/Kumunita.Web/Models/GuardianViewModels.cs` (the `ChildAccountItem`
  3-field record U06 re-pins to 4 fields — the `HasTimeLimits` badge),
  `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (the GU Detail view U06
  adds the "Time limits" section to — the suspend / membership / invitation
  sections as the shape), `src/Kumunita.Web/Views/Locale/Quiet.cshtml` (the
  M20 quiet-section view — the mode/hours/days form shape U06's section
  mirrors, bound to U05's `guardian.timelimit.*` keys),
  `tests/Kumunita.Web.Tests/*Guardian*` (the GU Index / Detail tests U06
  re-pins + the 3 F5-surface tests U06 adds).
- **Deliverables (≤ 4 files):**
  1. `src/Kumunita.Web/Models/GuardianViewModels.cs` (modify) — the
     `ChildAccountItem` record: add `bool HasTimeLimits` (the 4th field —
     the `Blocked` flag precedent; the GU Index view's "time-limits set?"
     badge). **Re-pin** the GU Index test that asserts the 3-field shape
     (U06 updates the pin to 4 fields).
  2. `src/Kumunita.Web/Controllers/GuardianController.cs` (modify) — the
     `Detail` action: seed the "Time limits" section with
     `GetChildTimeLimitAsync(subject, childId)` (the guardian-gated read);
     add a `TimeLimits` section view-model (the
     `LocaleSettingsViewModel.Quiet` shape — the `Enabled` / `Mode` /
     `Hours` / `DaysOfWeek` + the `HasTimeLimits` badge field); the
     `SaveTimeLimits` POST action (the `LocaleController.SaveQuiet` shape —
     `SetChildTimeLimitAsync(subject, childId, schedule)` + the
     `guardian.timelimit.flash_saved` / `_cleared` flash).
  3. `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (modify) — the
     "Time limits" section (the M20 `Views/Locale/Quiet.cshtml` shape —
     the `Enabled` checkbox, the `Mode` radio (Blocked/Allowed), the
     `Hours` + `DaysOfWeek` multi-select, the Save / Clear buttons), bound
     to U05's `guardian.timelimit.*` keys. The section is **guarded** to
     the child's Detail page (not a new route — D6).
  4. `tests/Kumunita.Web.Tests/GuardianTimeLimitSurfaceTests.cs` (new) —
     the 3 F5-surface tests (the exact names pinned in the design doc
     §2.6): `F5_Detail_Get_SeedsWithCurrentSchedule`,
     `F5_Detail_Post_SavesAndFlashes`, `F5_Detail_Post_Clear_SetsNull` +
     the re-pinned `ChildAccountItem` 4-field test (the
     `HasTimeLimits` badge).
- **Exit:** `dotnet build` green + the 3 F5-surface tests discovered
  (pass/red recorded for U07's gate). **The GU Index / Detail tests
  re-pinned** (the 3-field → 4-field `ChildAccountItem` pin). Handoff note:
  6–8 lines (the 4-field re-pin, the Detail section + the 2 controller
  actions, the `kw-l` keys bound, the 3 test names + status, any compile
  warnings).

### U07 — The acceptance gate tests (author the full seam list + run both suites)
- **Goal:** author the **full** M28 seam list (U02's 7 pure + U03's 7
  standing/audit + U04's 3 middleware + U06's 3 surface = **20 tests**,
  the exact names from the design doc §2.6) as the **part-vs-whole**
  evidence + run both suites. **The gate is not recorded yet** (U08
  records it) — U07 authors + runs.
- **Entry reads:** the design doc §2.6 (the 20-test seam list, the exact
  names) + §2.9 (the acceptance gate — the three-test shape),
  `tests/Kumunita.Core.Tests/GuardianTimeLimitEvaluatorTests.cs` (U02's 7
  pure tests), `tests/Kumunita.Core.Tests/GuardianTimeLimitStandingTests.cs`
  (U03's 7 standing/audit tests),
  `tests/Kumunita.Web.Tests/TimeLimitMiddlewareTests.cs` (U04's 3
  middleware tests), `tests/Kumunita.Web.Tests/GuardianTimeLimitSurfaceTests.cs`
  (U06's 3 surface tests), `tests/Kumunita.Core.Tests/PostgresFixture.cs`
  (the test harness the Core tests use).
- **Deliverables (≤ 1 file, new — the gate-evidence file):**
  1. `tests/Kumunita.Core.Tests/M28AcceptanceGateTests.cs` (new) — the
     **three** acceptance-gate tests (the M20 / M27 / M3 three-test shape):
     **closed-loop** (a guardian sets a `Blocked`-mode schedule covering
     "now" for their child → `IsAllowedNow` returns `false` → the
     `GetActiveTimeLimitAsync` read sees the schedule — the F1 + C-M28·4
     composition); **handoff** (a guardian sets an `Allowed`-mode schedule
     that allows "now" → the child is allowed; then clears it → the child
     is again always-allowed — the F2-in-window + F3 floor + C-M28·4
     strong-consistency); **part-vs-whole** (the full 20-test seam list
     passes together — the U02 7 pure + U03 7 standing + U04 3 middleware
     + U06 3 surface, all green).
- **Exit:** `dotnet build` green + **all 23 tests discovered** (the 20 seam
  + the 3 gate), the pass/red status of each recorded (for U08's gate
  record). **No gate recorded yet** (U08). Handoff note: 3–4 lines (the
  23 test names + status, the 3 gate test names + status, any
  pass/fail drift).

### U08 — Run + record the acceptance gate (the "run the record" step)
- **Goal:** execute + **record** the three-test acceptance gate (closed-
  loop / handoff / part-vs-whole) from the design doc §2.9, **using**
  U07's 23 tests as the part-vs-whole evidence. Append
  `### Run result (M28 acceptance gate — <date>)` to the design doc §2.9.
  **No code, no build.**
- **Entry reads:** the design doc §2.9 (the gate's three test names +
  definitions) + §2.8 (the drift guard — the `## U<m> — Drift pause`
  sections to resolve), `docs/plans-milestones/in-progress/m28-handoff-
  notes.md` (U07's section — the 23-test results the gate references),
  `tests/Kumunita.Core.Tests/M28AcceptanceGateTests.cs` (U07's 3 gate
  tests + the 20 seam tests they reference),
  `docs/design/m20-notification-quiet-times-design.md` § Acceptance Gate
  (the M20 recording shape to mirror verbatim — the `### Run result`
  section format).
- **Deliverables (1 file, modify):**
  1. `docs/design/m28-guardian-time-limits-design.md` (modify) — append
     `### Run result (M28 acceptance gate — <date>)`: the three test names
     (closed-loop / handoff / part-vs-whole) + their pass status, the 20-
     test seam count (from U07), and one line per any `## U<m> — Drift
     pause` section in the handoff note (each resolved or still open).
- **Exit:** the gate section is present and consistent with U07's results.
  Handoff note: 4–5 lines (the 3 gate test names + pass status + the date
  + the 20-test seam count + any still-open drift).

### U09 — Close: the milestone flip (M28 is the LAST) + the `MilestonesTests` reframe + `WhatsNew` + the design-doc "Closed" section + the handoff `## Summary` + the plan-file moves
- **Goal:** the **single close unit** (C-M28·8): flip M28 → `StatusDone` in
  the four doc surfaces **in one unit** + **reframe** the
  `MilestonesTests` pin (M28 is the last — "the roadmap is fully shipped")
  + add the `WhatsNew.cs` `0.43.0` row (the required **sixth** member) +
  append the `## M28 — Closed (recorded)` section to the design doc + move
  the M28 unit-plan files `in-progress/` → `done/` + append the handoff
  `## Summary`. **The loop-closing step** (the M27 U13 / M3 U11 analog).
- **Entry reads:** the design doc § (the D9 deferred-lane list + the
  invariants + the FACES + the gate record U08 appended),
  `src/Kumunita.Web/Milestones.cs` (the M28 row — `StatusNext` →
  `StatusDone`; **no** next milestone — the roadmap is complete),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the 4-test pin — the
  `M28_Is_The_Single_InProgress_Milestone` test U09 **reframes**),
  `README.md` §Roadmap (the M28 line → **Done**; the roadmap is complete),
  `docs/STATUS.md` (the M28 line → **Done**; the "next" note → "all
  shipped"), `docs/ARCHITECTURE.md` (the M28 line → **shipped**; the
  `UserInfo/` GU-lane description gains the time-limit seam note),
  `src/Kumunita.Web/WhatsNew.cs` (the `0.43.0` row U09 adds — the
  required sixth member), `docs/plans-milestones/in-progress/` (the M28
  unit-plan files U09 moves → `done/`).
- **Deliverables (≤ 8 files, modify/move):**
  1. `src/Kumunita.Web/Milestones.cs` (modify) — the `M28` row →
     `StatusDone`. **There is no next milestone** (the roadmap is
     complete). The **order** of `Milestones.All` is **unchanged**
     (`…, M26, M27, SITE, M28`) — a status bump, not a renumber.
  2. `tests/Kumunita.Web.Tests/MilestonesTests.cs` (modify) —
     - `Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order` —
       **unchanged** (the order does not move).
     - `Shipped_Milestones_Are_Marked_Done` — **add `"M28"`** to the
       asserted done set (now **every** M-milestone is done — the assert
       list is the full `Ids` set).
     - **Reframe** `M28_Is_The_Single_InProgress_Milestone` →
       `Roadmap_Is_Fully_Shipped_No_InProgress_Milestone`: assert
       **zero** `StatusNext` rows (`Assert.Empty(Milestones.All.Where(m =>
       m.Status == Milestones.StatusNext))`) + **all** rows `StatusDone`
       (`Assert.All(Milestones.All, m => Assert.Equal(Milestones.StatusDone,
       m.Status))`). (A semantic reframe — M28 is the last, so the
       "single in-progress" premise no longer holds.)
     - `No_Milestone_Has_Blank_Title` — **unchanged**.
  3. `README.md` (modify) — the §Roadmap `M28` line → **Done** (the
     guardian time limits shipped). The roadmap is complete (every line
     Done). (The M27 "M27 done, M28 next" flip is the **last**
     next-pointing flip; M28's flip points to **nothing** — the roadmap
     is done.)
  4. `docs/STATUS.md` (modify) — the M28 line → **Done**; the "next" line
     (if any) → the "all shipped / roadmap complete" note (U09 reads the
     current shape and mirrors it).
  5. `docs/ARCHITECTURE.md` (modify) — the M28 line → **shipped**; the
     `UserInfo/` context's GU-lane description gains the time-limit seam
     note (the "lane on the M1 surface, not a new bounded context" shape,
     the `GuardianTimeLimitSchedule` doc + the three `IUserInfoService`
     seams + the `TimeLimitMiddleware`).
  6. `src/Kumunita.Web/WhatsNew.cs` (modify) — add the **`0.43.0`** row
     (newest-first, **above** the `0.42.0` row): the M28 entry (the
     "Guardian time limits — a parent/guardian sets when the child may use
     the platform; a per-child allow/block schedule (hours × days, the
     child's effective zone) enforced as a sign-out gate (the
     `BlockedAccountMiddleware` analog); the GU-lane inverse of the M20
     quiet lane (ADR 0121); the GU standing + G·5 safety valve (ADR
     0028)") + the ADR 0151 pointer. **The required sixth member** of the
     close flip (the M27 "shipped with no entry until caught in review"
     lesson, AGENTS.md — held).
  7. `docs/design/m28-guardian-time-limits-design.md` (modify) — append
     the **`## M28 — Closed (recorded)`** section **last** (mirroring the
     M27 U13 close): the three acceptance tests from U08's record, the
     total M28 test count (Core + Web), the ADR 0151 pointer, the D9
     deferred-lane list (each item named), and the "roadmap complete"
     note (M28 is the last milestone — the loop is closed).
  8. `docs/plans-milestones/in-progress/m28-handoff-notes.md` (modify) —
     append `## Summary`: a table of the shipped units (U00–U08), with
     their one-liner goal + test count + any deviations + the D9
     deferred-lane list (each item named, each with a one-line follow-on
     ADR candidate or "resolved by U<m>").
  9. **The M28 unit-plan files** (`m28-u00.md` … `m28-u09.md`) **and the
     handoff note** (`m28-handoff-notes.md`) — **moved** from
     `docs/plans-milestones/in-progress/` to
     `docs/plans-milestones/done/` (the **flat** `done/` convention — the
     M27 precedent + the user's stated storage instruction). The master
     register `docs/plans-milestones/plan-m28-guardian-time-limits.md`
     stays at the top level (the sealed register, not a unit plan). Move
     the handoff note **after** appending its `## Summary` (step 8).
- **Rules:** the **order** of `Milestones.All` is **unchanged** (C-M28·8);
  the four doc surfaces (Milestones.cs / README / STATUS / ARCHITECTURE)
  must **agree** in the same unit: **M28 done, the roadmap complete**
  (C-M28·8, the AGENTS.md doc↔code parity contract). The
  `MilestonesTests` reframe (the "single in-progress" → "fully shipped"
  semantic change) must **pass** after the flip. The **`WhatsNew.cs`**
  `0.43.0` row is the **required sixth member** (the M27 lesson). The
  moves are the **the** "done" step — `in-progress/` must no longer hold
  the M28 unit plans; `done/` must. **Zero new authorization surface** is
  the final close pin (C-M28·5) — the `IAuthorizationService` surface
  count is unchanged (U07 confirmed it).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green + the
  `MilestonesTests` reframe **passes** (the "roadmap fully shipped"
  pin) + the `WhatsNewTests` green (the `0.43.0` row added) + the plan
  files moved `in-progress/` → `done/` + the handoff `## Summary` appended.
  **The loop is closed — M28 is done, the roadmap is complete.**
