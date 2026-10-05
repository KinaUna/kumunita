# M22 — Onboarding · rolling handoff notes

> Every unit appends a short `## U##` section before it moves its own plan to
> `done/`. This is the cross-unit memory: read the `## U##` sections before
> yours, append yours after. Do not re-derive what an earlier unit already
> settled (a D# amendment, the key set, the `OnboardingCompletedAt` shape, the
> owner-scope lane signature, the banner-eligibility read).

## U00 — Design doc + ADR 0132 (docs-only; no build/test)

**Delivered (3 files):**
- `docs/design/m22-onboarding-design.md` — §0–§11 complete (Scope & non-scope;
  Decisions D1–D8 + §1.a amendments; Invariants C-M22·1…C-M22·6; FACES F1–F5 +
  named trade; §4 additive field exact C#; §5 the two owner-scope seams exact
  C#; §6 the frozen-lane table with exact signatures; §7 the
  `OnboardingController`/`OnboardingViewModel` route/action shape; §8 the closed
  `onboarding.*` 15-key set; §9 the six GATEs; §10 the drift-guard; §11 the
  five deferred D8 lanes).
- `docs/adr/0132-onboarding.md` — `Status: Accepted`, Context / Decision /
  Consequences, mirroring the ADR 0123 shape.
- `docs/adr/README.md` — one `0132` row appended after the `0131` row.

**D#s locked (no deviation from the register's [PROPOSED] set):**
- **D1 — `Profile` member confirmed** as `OnboardingCompletedAt: DateTimeOffset?`
  (nullable, `null` = not completed). Verified against
  `src/Kumunita.Core/UserInfo/Profile.cs` (already carries ten additive fields
  incl. the M23 `Bio`/`TagIds` precedent; registered in `M1DocTypes`). Locked as
  one nullable `DateTimeOffset?` (not a `bool + DateTime` pair) in §1.a-C1.
- **D4 — skip-vs-finish confirmed** at the register's single-required-`finish` +
  **optional-`skip`** shape (both → the same `CompleteOnboardingAsync`); neither
  gates sign-in. Locked in §1.a-C2 (the two-action shape is the locked surface;
  only the action count is the amendment surface).
- D2/D3/D5/D6/D7/D8 locked verbatim from the register.

**FACES by id:** F1 (guided account setup) · F2 (finish/skip clears banner) ·
F3 (every step lands in the owning surface) · F4 (non-blocking, claim-free) ·
F5 (localized in the resident's language).

**§gate by name:** GATE-1 one additive field `null`=not-completed · GATE-2 one
owner-scope write, no audit row, no load-or-create · GATE-3 zero new field-write
lanes (rides frozen lanes) · GATE-4 zero new authorization surface · GATE-5
banner gated on completion read, non-blocking · GATE-6 closed `onboarding.*` set
parity-pinned ×4 languages.

**Frozen-lane signatures locked in §6** (the D3 table, exact from the live
`IUserInfoService.cs`): `UpsertProfileAsync(Profile, ProfileUpdate, string?
actorBy=null)` · `SetProfileAvatarAsync(string subjectId, string? avatarId,
string actorBy)` · `SetProfileTimezoneAsync(string subjectId, string? timezone,
string actorBy)` · `SetProfileDateFormatAsync(string subjectId, string?
formatString, string actorBy)` · `SetProfileEmailLanguageAsync(string subjectId,
string? emailLanguage, string actorBy)` — plus the `LocaleController`
language-**cookie** lane for the UI-language step (a per-request string, **not**
a `Profile` field).

**The `OnboardingCompletedAt` shape:** `public DateTimeOffset? OnboardingCompletedAt
{ get; set; }` on the existing `Profile` doc; `null` = not completed (the floor);
written only by `CompleteOnboardingAsync`; read through `GetProfileAsync` /
`GetOnboardingCompletedAsync`; additive per ADR 0004 §B.1 (no new DocTypes
surface / boot line / EF migration / context / adapter / storage lane).

**U01 next —** read the design doc **§4–§6** + ADR 0132 **§Decision** (D1/D2/D3).
Own the additive `Profile.OnboardingCompletedAt` field + the two owner-scope
seams (`CompleteOnboardingAsync` / `GetOnboardingCompletedAsync`) on
`IUserInfoService` + the `UserInfoService` impl + DI. Exit on
`Kumunita.Core.Tests`. Do **not** add an `AccessAudit` row, **not**
load-or-create, **not** write any field other than `OnboardingCompletedAt`,
**not** add any authorization surface (the §10 drift-guard, §9 of the design
doc).

## U01 — the additive `Profile.OnboardingCompletedAt` field + the two owner-scope seams (Core)

**Delivered (3 code files, Core only):**
- `src/Kumunita.Core/UserInfo/Profile.cs` — one additive field
  `public DateTimeOffset? OnboardingCompletedAt { get; set; }` placed after the
  M23 `Bio`/`TagIds` fields (the ADR 0004 §B.1 additive placement, the 11th
  additive field on the same doc). **Not** part of `ProfileUpdate` (its own
  owner-scope lane, D2). No new DocTypes surface / boot line / EF migration.
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — two seams added immediately
  after `SetProfileEmailLanguageAsync` (the ADR 0006-E compatible-addition
  idiom, the `SetProfileTimezoneAsync` doc-comment shape verbatim):
  `Task CompleteOnboardingAsync(string subjectId, string actorBy)` (write) +
  `Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId)` (read).
- `src/Kumunita.Core/UserInfo/UserInfoService.cs` — the two impls mirroring
  `SetProfileTimezoneAsync` **verbatim** (load → `KeyNotFoundException` if null
  → stamp `OnboardingCompletedAt = UtcNow` → `Store` → one `SaveChangesAsync`,
  **no `AccessAudit` row**); the read mirrors `GetProfileAsync` (returns
  `profile?.OnboardingCompletedAt`, `null` = floor). **No new DI registration.**

**Seam signatures (locked, for U02 to call):** `CompleteOnboardingAsync(string
subjectId, string actorBy)` · `GetOnboardingCompletedAsync(string subjectId)`.
`OnboardingCompletedAt` shape: nullable `DateTimeOffset?`, `null` = not
completed (the banner floor).

## U02 — the `OnboardingController` + the `OnboardingViewModel` (Web)

**Delivered (2 Web files + 1 test file):**
- `src/Kumunita.Web/Controllers/OnboardingController.cs` — `[Authorize]` +
  `Controller` base (the `LocaleController` settings-tabs shape). Three actions:
  `GET /onboarding` (`Index` — seeds the model from the owner-scope
  `GetProfileAsync` read + the per-step hints + the `LocaleCookie.Read` UI-
  language hint), `POST /onboarding/finish` (`Finish` — the **one** write: calls
  U01's `CompleteOnboardingAsync(subjectId, subjectId)`, `KeyNotFoundException`
  catch → redirect home, else `TempData["info"]` = the `onboarding.flash_done`
  flash + `RedirectToAction("Index","Home")`), and `POST /onboarding/skip`
  (`Skip` — `§1.a-C2`, routes to the **same** `Finish()` lane). Mirrors the real
  `FlashAsync` idiom **verbatim** — i.e. it takes **both** `ILocalizationService?`
  **and** `ITranslationProvider?` (default-null) and guards `FlashAsync` on both
  being non-null. **Deviation from the plan snippet (corrected against the
  codebase):** the unit-plan snippet passed `null` for the `ILocalizationService`
  to `EffectiveLanguageCode.ResolveAsync` — but the real `ResolveAsync` requires
  a non-null `ILocalizationService` (it dereferences it), so I followed the
  real `LocaleController.FlashAsync` / `ProfileController.FlashAsync` shape (both
  seams optional, both checked). The flash key is still `onboarding.flash_done`
  (U03 authors it).
- `src/Kumunita.Web/Models/OnboardingViewModel.cs` — the D4/D5 read-only model
  per design doc §7: `OnboardingCompletedAt` (the completion read) + derived
  `BannerEligible` (`=> OnboardingCompletedAt is null`) + the per-step hints
  `HasDisplayName`/`HasAvatar`/`HasTimezone`/`HasDateFormat`/`HasEmailLanguage`
  + `UiLanguageCode` (the `LocaleCookie.Read` string — a cookie, not a
  `Profile` field, the thin-token rule). All `{ get; init; }` — no setter, no
  write, no `Can`/`Decide`/`AccessVia`.

**Controller routes (locked):** `GET /onboarding` · `POST /onboarding/finish` ·
`POST /onboarding/skip`. The only write the walk-through performs is
`CompleteOnboardingAsync` (D3/C-M22·4) — it **calls** U01's lane, it never
writes `Profile` itself, and it calls **no** other field-write lane.

**Test added:** `tests/Kumunita.Web.Tests/OnboardingControllerTests.cs` — 5
facts pinning GATE-3/GATE-4/GATE-5: `Index` not-completed → `BannerEligible`
`true` + `CompletedAt` null (GATE-5) · `Index` completed → `BannerEligible`
`false` + all hints `true` + the `UiLanguageCode` cookie read (GATE-5) ·
`Finish` calls `CompleteOnboardingAsync` exactly once for the signed-in subject
+ **no** other field-write lane (NSubstitute `DidNotReceive` on the five frozen
lanes) + redirect to home + a flash written (GATE-3/C-M22·4) · `Skip` routes to
the same single lane (GATE-3/§1.a-C2) · the controller injects **no**
`IAuthorizationService` and the view model exposes no `Can*`/`Decide*`/
`AccessVia` member (GATE-4/C-M22·2). (The test project uses **NSubstitute**, not
the `Mock<>` prose the unit plan suggested — matched the real idiom.)

**Exit:** `dotnet build Kumunita.slnx -c Debug` clean; `dotnet exec
…\Kumunita.Web.Tests.dll` **760 passed, 0 failed, 0 skipped** (the 5 new facts
+ the pre-existing suite). No drift: zero new authorization surface, zero new
field-write lane, zero `AccessAudit` write, zero claim read. The `onboarding.*`
`kw-l` keys are **not** authored here — U03 owns them (U02 consumes).

**U03 next —** read the design doc **§8** + ADR 0132 **§Decision** (D7). Own the
`Views/Onboarding/*` + the home/nav **banner** (gated on
`OnboardingViewModel.BannerEligible`) + **author the full closed
`onboarding.*` key set × en/de/fr/da** (the 15 keys, §8) in
`KnownTranslationKeys`. U03 consumes the `OnboardingViewModel` + the
`OnboardingController` routes U02 shipped. Exit on `Kumunita.Web.Tests`.

**Test added:** `tests/Kumunita.Core.Tests/UserInfo/OnboardingCompletionLaneTests.cs`
— 4 facts pinning GATE-1/GATE-2/C-M22·3: fresh profile reads `null` (both
seams) · complete-then-read returns the stamp (strong consistency C4, and the
write touched no other field) · missing profile throws `KeyNotFoundException`
(never load-or-create, and the read seam returns `null`) · **zero**
`AccessAudit` rows after the write.

**Exit:** `dotnet build Kumunita.slnx -c Debug` clean;
`dotnet exec …\Kumunita.Core.Tests.dll` **1130 passed, 0 failed, 0 skipped**
(the 4 new facts + the pre-existing suite). No drift: zero new authorization
surface, zero new Marten surface, owner-scope only.

**U02 next —** read the design doc **§7** + ADR 0132 **§Decision** (D2/D4). Own
the `OnboardingController` (`GET /onboarding` + `POST /onboarding/finish`) + the
`OnboardingViewModel` (banner-eligibility read via `GetOnboardingCompletedAsync`
/ `GetProfileAsync`). U02 **calls** `CompleteOnboardingAsync`; it never writes
`Profile` itself. Exit on `Kumunita.Web.Tests`.

## U03 — `Views/Onboarding/*` + the home/nav **banner** + the closed `onboarding.*` set (Web)

**Delivered (1 registry edit + 3 new Web files + 1 new JS module + 1 test file):**
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the **complete
  closed 15-key `onboarding.*` set (D7)** added to **all four** dictionaries
  (`EnValues`/`DeValues`/`FrValues`/`DaValues`), same keys, same order, one
  `M22 (ADR 0132)` section banner each, immediately after the M23 `profile.*`
  / `documents.*` block (the `documents.flash_edited` closing). The 15 keys,
  verbatim: `title` · `intro` · `step_displayname` · `step_avatar` ·
  `step_language` · `step_timezone` · `step_dateformat` · `step_email` ·
  `step_contact` · `visit` · `finish` · `skip` · `flash_done` · `banner.text` ·
  `banner.action`. Non-empty in all four (the parity pin closure).
- `src/Kumunita.Web/Views/Onboarding/Index.cshtml` — the `/onboarding` step
  sequence (consumes U02's `OnboardingViewModel`, `@model OnboardingViewModel`).
  The 7 step cards each link into the **frozen-lane** target (D3): display name
  → `/profile/edit` · avatar → `/profile` · UI language → `/settings/language` ·
  timezone → `/settings/timezone` · date format → `/settings/dateformat` · email
  language → `/settings/language` · contact → `/profile/edit`. Each card: the
  `onboarding.step_*` label + a **decorative** set/not-set marker (a `✓`/`–`
  glyph, `aria-hidden` — a *symbol, not a translatable string*, so it stays
  inside the closed 15-key set while still giving the "set / not set" hint off
  the owner-scope `Has*` read) + the `onboarding.visit` link. The finish/skip
  actions are two POST forms to `/onboarding/finish` + `/onboarding/skip` with
  anti-forgery (the `onboarding.finish`/`onboarding.skip` buttons). **Every
  user-visible string is a `<kw-l key="onboarding.*"/>` key** — no inline
  English (the `KwLRegistryConsistencyTests` view↔registry scan enforces it).
- `src/Kumunita.Web/Views/Shared/_OnboardingBanner.cshtml` — the dismissible
  home/nav **banner** (D5, C-M22·5). Injects `IUserInfoService`, reads the
  owner-scope `GetProfileAsync(subjectId)` → `OnboardingCompletedAt is null`
  gate (the `BannerEligible` shape, **never a claim** — thin-token rule
  ADR 0001-B/D6). Signed-in residents only (unauthenticated → no subject → no
  banner). Renders `onboarding.banner.text` + the `onboarding.banner.action`
  CTA link to `/onboarding` + a `btn-close` (`data-dismiss-key`).
- `src/Kumunita.Web/client/lib/onboarding-banner.ts` (+ compiled
  `wwwroot/js/lib/onboarding-banner.js`) — the **non-blocking** dismiss: a
  `sessionStorage` flag (the `_PinnedAnnouncement` idiom verbatim), the close
  button sets it, on load it hides if set. An *affordance only* — it **never**
  writes `OnboardingCompletedAt` and never gates sign-in (the only way to clear
  the banner for real is the explicit finish/skip POST, D2/D4). CSP-safe
  (`script-src 'self'`).
- `_Layout.cshtml` — mounted `@await Html.PartialAsync("_OnboardingBanner")`
  right after the M19 guest-welcome notice (the existing signed-in-resident
  banner slot).
- `tests/Kumunita.Web.Tests/Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages.cs`
  — the **GATE-6 / C-M22·6** pin: the closed set is exactly 15 keys; every key
  present + non-empty in **en/de/fr/da**; and the `onboarding.*` set is
  **closed** (no 16th key in the registry). (U02 already pins GATE-3/4/5 +
  the `BannerEligible` shape on the controller side.)

**Exit:** `dotnet build Kumunita.slnx -c Debug` **clean**; `npm --prefix
src/Kumunita.Web run build` **clean**; `dotnet exec
…\Kumunita.Web.Tests.dll` **763 passed, 0 failed, 0 skipped** (my 3 new GATE-6
facts + U02's 5 controller facts + the `KwLRegistryConsistencyTests` view↔
registry scan + the rest of the suite). **Browser-verified against the live
server** (the Razor verification doctrine): the banner renders on home for a
not-completed resident ("Finish setting up your account?" + "Start setup" →
`/onboarding`); the `/onboarding` step page shows the 7 cards with the correct
frozen-lane links + set/not-set markers + finish/skip; the finish POST
redirects home with the `onboarding.flash_done` flash ("Setup complete —
welcome to your neighborhood.") and the **banner clears** (completion stamped
→ `BannerEligible` false). No drift: zero new field-write lane (rides the
frozen lanes), zero new authorization surface, zero claim read, no inline
English, no 16th key, no per-step POST, no persisted cursor, no modal, no
sign-in gating.

**U04 next —** read the register **§"done/ layout note"** + the ADR index.
**Close M22**: flip M22 `StatusDone`, promote **M24** to `StatusNext` (the
**order unchanged** — `…"M20","M21","M23","M22","M24"`), re-pin
`MilestonesTests` (replace
`M22_Is_The_Single_InProgress_Milestone` with
`M24_Is_The_Single_InProgress_Milestone` + append `"M22"` to the
`Shipped_Milestones_Are_Marked_Done` done-list), README/STATUS/ARCHITECTURE
parity, tag the ADR 0132 index row `**Done** (M22)`, and move **all** M22
artifacts (the register, the `m22-uNN.md` plans, the handoff notes) to
`done/m22/` (the **subfolder** convention — `done/m22/` already holds
`m22-u01.md` + `m22-u02.md`, which U01/U02 moved). U04 owns the roadmap flip;
no unit before it touches `Milestones.cs` / `MilestonesTests`.

## U04 — Close M22 (Web + docs; the single close-flip)

**Delivered (5 edits + 1 artifact move):**
- `src/Kumunita.Web/Milestones.cs` — flipped **M22** `StatusNext` →
  `StatusDone` (title now carries `(ADR 0132)`) and **promoted M24**
  `StatusPlanned` → `StatusNext`; the **order is unchanged**
  (`…"M20","M21","M23","M22","M24"` — the "named lane, not a renumber"
  precedent); the M22/M23 comment updated to read "M22 has now shipped".
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — **replaced**
  `M22_Is_The_Single_InProgress_Milestone` with
  `M24_Is_The_Single_InProgress_Milestone` (asserts M24 is the single
  in-progress + M22/M23/M21 all `StatusDone`) and **appended `"M22"`** to the
  `Shipped_Milestones_Are_Marked_Done` done-list. The single-in-progress pin
  still holds, now on M24.
- `README.md` — intro "M22 is in progress" → "M22 is done … ADR 0132" + new
  "M24 is next" line; Roadmap list `**M22** … **In progress.**` → `**Done**
  (ADR 0132)` and `**M24** … **Planned.**` → `**In progress.**`.
- `docs/STATUS.md` — "M22 is in progress" → "M22 is done … ADR 0132" + new
  "M24 is next" clause.
- `docs/ARCHITECTURE.md` — the value-chain table gains an **M22 onboarding**
  row (after M23, ADR 0132) — M22 was the only shipped M-letter milestone
  missing from the table.
- `docs/adr/README.md` — the `0132` index row tagged `**Done** (M22)`; the
  ADR itself already reads `Status: Accepted` (verified).

**Moved to `done/m22/`:** `plan-m22-onboarding.md` (the register),
`m22-u00.md`, `m22-u04.md`, and `m22-onboarding-handoff-notes.md` (joining the
already-moved `m22-u01`…`m22-u03`). The **subfolder** convention is followed
(matching the real `done/m21/`, `done/m23/` tree).

**Exit:** `dotnet build Kumunita.slnx -c Debug` **clean** (0 errors);
`dotnet exec …\Kumunita.Web.Tests.dll` **763 passed, 0 failed, 0 skipped**.
The `MilestonesTests` pin passes with **M24** as the single in-progress
milestone and **M22** in the done-list. No feature code touched (the
do-not-touch list held: no controller / view model / registry / view / banner /
Core lane / adapter / DocTypes / boot line / `.csproj`). **M22 is closed.**
