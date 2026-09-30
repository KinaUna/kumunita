# M19 — Guest accounts · handoff notes

## U00 — Design doc + ADR 0120 locked

- **Locked:** D1–D7 (standing-not-entity / admin-settled bounded `GuestAccess` doc + one audited write lane / bounded window "when" / closed `GuestSurface` set with empty floor / `Roles.Guest` claim mint, zero new authz surface / `/admin/guests` GlobalAdmin surface / five deferred lanes), C-M19·1…C-M19·6, FACES F1–F5, GATE-1…GATE-4, §drift-guard — all verbatim in `docs/design/m19-guest-accounts-design.md`.
- **§1.a amendment (representation-only, D4 decision unchanged):** D2's `AllowedSurfaces` was pinned as a **`[Flags]` scalar** (`GuestSurface AllowedSurfaces = GuestSurface.None`) rather than the register's prose `GuestSurface[]` array — the `GuestSurface` enum is `[Flags]` (D4), so the document carries one scalar, not an array. U01/U02/U04 all implement the scalar. The D4 *decision* (closed set, empty floor, a new surface = a new enum value) is unchanged.
- **§9 C# is ready for U01:** `GuestSurface` `[Flags]` enum (`None`/`Announcements`/`Events`/`Directory`), `GuestAccess` document (`SubjectId`/`ValidFrom`/`ValidUntil`/`AllowedSurfaces`/`SetByAdmin`), additive `Profile.IsGuest`, `IIdentityService.GetGuestAccessAsync`/`SetGuestAccessAsync` ADD, the `M1DocTypes` `Identity.GuestAccess` registration — exact types + signatures in design doc §4/§5/§9.
- **ADR 0120 Supersedes states "none"** (explicitly): M19 *adds a standing*; it rides ADR 0001-B / ADR 0030 / ADR 0004 §B.1 / ADR 0050 and leaves the frozen `IAuthorizationService` seams, the `AccessAudit` lane, and the resident standing byte-identical (C-M19·1/2/6). The index row 0120 is appended after 0119 in `docs/adr/README.md`.
- **No open questions.** U01 (pure Core types + seam) can start from design doc §4/§5/§9.

## U01 — Core guest seam

- **Delivered:** `GuestSurface` `[Flags]` enum + `GuestAccess` document (`AllowedSurfaces` is the `[Flags]` scalar, §1.a), additive `Profile.IsGuest`, the `IIdentityService.GetGuestAccessAsync`/`SetGuestAccessAsync` ADD + `IdentityService` impl (one `AccessAudit` row, `Via=Admin`, `Action="guest.set-standing"`, `TargetKind="guest"`, `TargetId="guest:{subjectId}"`), and the `M1DocTypes` `Identity.GuestAccess` registration (`Identity(g => g.SubjectId)`).
- **Tests green:** `GuestAccessSeamTests` — `SetGuestAccess_Stores_One_Document_And_One_Audit_Row` (GATE-3), `GetGuestAccess_With_No_Allowance_Returns_Null` (C-M19·4 floor), `GuestSurface_Closed_Set_Typo_Grants_Nothing` (C-M19·4 / D4). Build + full `Kumunita.Core.Tests` (1049) green.
- **Divergence (test example, not a D#):** the unit plan's `(GuestSurface)999` "typo" is unsatisfiable for a `[Flags]` enum — 999 has bits 0 & 1 set, so `HasFlag(Announcements)`/`HasFlag(Events)` are *true*. The closed-set guarantee ("a typo grants nothing") only holds for a value composed *entirely of out-of-union bits*; I pinned that with `(GuestSurface)8` (bit 3) and `(GuestSurface)0x80` (bit 7) instead. The D4 decision and the C# are unchanged.
- **Seams for the next units:** `SetGuestAccessAsync` is the single audited write lane U04's `AdminGuestsController` will call; `GetGuestAccessAsync` is the read U02's claim-mint rides (signatures match design doc §9 exactly).
- **No open questions.** U02 (Web claim mint, `Roles.Guest` + factory branch) can start from design doc §6/§9.

## U02 — Claim mint at the seam

- **Delivered:** `Roles.Guest = "Guest"` constant (after `Translator`, the `ClaimTypes.All` set **untouched** — C-M19·6), the `IIdentityService` constructor dependency on `KumunitaClaimsPrincipalFactory`, and the guest branch in `BuildRoleListAsync(User, bool verified, Profile? profile)` (mint `Roles.Guest` iff `IsGuest` && window live `ValidFrom ≤ now < ValidUntil`, riding U01's `GetGuestAccessAsync`; independent of the `verified → Member` branch, non-guests short-circuit, `blocked → Array.Empty` runs first so the branch is unreachable for a blocked account — C-M19·2/·3).
- **Tests green:** `GuestClaimMintTests` — `Guest_Within_Window_Mints_Guest_Not_Member` (GATE-1), `Guest_Outside_Window_Mints_No_Guest` (GATE-1), `Claim_Shape_Pin_Only_New_Claim_String_Is_Guest` (GATE-4). Build + full `Kumunita.Web.Tests` (660) green.
- **Divergence (test harness, not a D#):** `UserManager<T>`/`RoleManager<T>` have no parameterless constructor so they can't be NSubstituted directly — the test backs a *real* `UserManager<User>` with a substituted `IUserRoleStore<User>` (stubbed empty) and a nested `EmptyServiceProvider` helper (the Core.Tests one is a private class, not a library type). The seam behavior is unchanged.
- **Seams for the next units:** U04's `AdminGuestsController` reads `GetGuestAccessAsync` for the index/set-window view model and writes via `SetGuestAccessAsync`; the `Guest` role string it now mints is what `CanSeeAsync`-shaped gates and the nav will key off.
- **No open questions.** U03 (`kw-l` key set × en/de/fr/da + parity pin) can start from design doc §8/§9.

## U03 — `kw-l` key set

- **Delivered:** the closed 10-key M19 set (design doc §8) added to **all four** of `EnValues`/`DeValues`/`FrValues`/`DaValues` in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the en baselines verbatim from the §8 table; idiomatic de/fr/da authored to match the existing entries (surface labels reuse the established register: Ankündigungen/Veranstaltungen/Verzeichnis, Annonces/Événements/Annuaire, Meddelelser/Arrangementer/Kontaktliste). All present and non-empty in all four.
- **Tests green:** no new test file authored — the existing `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pins (key-set equality across all four dictionaries + non-emptiness) pick up the new keys automatically. Build + full `Kumunita.Core.Tests` (1049) green.
- **Seams for the next unit:** U04's `AdminGuestsController` **consumes** `admin.guests_*` (title/empty/create/window_label/surfaces_label/surface_announcements/surface_events/surface_directory/saved) + `account.guest_welcome` via the `kw-l` TagHelper; it adds none of these keys.
- **U05 note:** the close unit's docs-only milestone flip needs no new `kw-l` keys.
- **No open questions.** U04 (Web `/admin/guests` GlobalAdmin controller + views + nav) can start from design doc §7 + §9.7 (the `AdminGuestsController` actions); the §8 key set above is what it consumes.

## U04 — /admin/guests surface

- **Delivered (Web-only):** `AdminGuestsController` (`[Route("admin/guests")]`, `[Authorize(Roles = Roles.GlobalAdmin)]`, `GET Index(string? subjectId)` seeds the view model from `GetGuestAccessAsync` / `POST Save(...)` composes the `[Flags]` `GuestSurface` and delegates to the single audited `SetGuestAccessAsync` lane — GATE-2) + `GuestAdminViewModel` (`Seed` decomposes the flags into the three checkboxes, null → empty floor) + `Views/AdminGuests/Index.cshtml` (subjectId + window + three surface checkboxes + anti-forgery) + the `/admin` index card (hidden-not-disabled via `KumunitaPrincipal.IsGlobalAdmin`).
- **Tests green:** `AdminGuestsControllerTests` — index seeds the existing standing, the no-subject floor does not read, `Save` delegates to the single audited lane with the composed surfaces + `SetByAdmin` asserted as the second argument, `Save` with no surfaces composes the `None` floor, inverted window (boundary `ValidUntil <= ValidFrom`) rejects, the GlobalAdmin role-`Authorize` gate-attribute pin, the `/admin` link is hidden-not-disabled for a non-GlobalAdmin (string pin, no TestServer), and the 10 `admin.guests_*` keys are present + non-empty in all four languages. Build + full `Kumunita.Web.Tests` (669) green.
- **Divergence (test example, not a D#):** the U04 register's prose `GuestSurface[]` array was implemented as the `[Flags]` scalar per the §1.a amendment (matching `GuestAccess.AllowedSurfaces`); the controller composes a single value, not an array.
- **Seams for the close unit:** U05 (docs-only) needs no new `kw-l` keys; it flips M19 → done in the README Roadmap + `Milestones.cs` + `MilestonesTests.cs`.
- **No open questions.** M19 is now feature-complete pending U05's docs flip.
