# M19 — Guest accounts · handoff notes

## U00 — Design doc + ADR 0120 locked

- **Locked:** D1–D7 (standing-not-entity / admin-settled bounded `GuestAccess` doc + one audited write lane / bounded window "when" / closed `GuestSurface` set with empty floor / `Roles.Guest` claim mint, zero new authz surface / `/admin/guests` GlobalAdmin surface / five deferred lanes), C-M19·1…C-M19·6, FACES F1–F5, GATE-1…GATE-4, §drift-guard — all verbatim in `docs/design/m19-guest-accounts-design.md`.
- **§1.a amendment (representation-only, D4 decision unchanged):** D2's `AllowedSurfaces` was pinned as a **`[Flags]` scalar** (`GuestSurface AllowedSurfaces = GuestSurface.None`) rather than the register's prose `GuestSurface[]` array — the `GuestSurface` enum is `[Flags]` (D4), so the document carries one scalar, not an array. U01/U02/U04 all implement the scalar. The D4 *decision* (closed set, empty floor, a new surface = a new enum value) is unchanged.
- **§9 C# is ready for U01:** `GuestSurface` `[Flags]` enum (`None`/`Announcements`/`Events`/`Directory`), `GuestAccess` document (`SubjectId`/`ValidFrom`/`ValidUntil`/`AllowedSurfaces`/`SetByAdmin`), additive `Profile.IsGuest`, `IIdentityService.GetGuestAccessAsync`/`SetGuestAccessAsync` ADD, the `M1DocTypes` `Identity.GuestAccess` registration — exact types + signatures in design doc §4/§5/§9.
- **ADR 0120 Supersedes states "none"** (explicitly): M19 *adds a standing*; it rides ADR 0001-B / ADR 0030 / ADR 0004 §B.1 / ADR 0050 and leaves the frozen `IAuthorizationService` seams, the `AccessAudit` lane, and the resident standing byte-identical (C-M19·1/2/6). The index row 0120 is appended after 0119 in `docs/adr/README.md`.
- **No open questions.** U01 (pure Core types + seam) can start from design doc §4/§5/§9.
