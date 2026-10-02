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
