# U4 execution plan (working) — Avatar reference lane (UserInfo side): `Profile.AvatarId` + `SetProfileAvatarAsync`

> My (unit U4's) working plan for this pass. The **authoritative spec** is
> the U4 entry in `plan-media-file-storage.md` + the authored
> `media-u4-plan.md` + **design doc**
> `../../design/media-file-storage-design.md` **§2.2** (the exact
> `Profile.AvatarId` + `SetProfileAvatarAsync` shapes, lines ~440–461) +
> §2.7 (drift-guard — **the doc wins** on shape mismatches). Prior handoff
> section read: **U3** (latest) in `media-file-storage-handoff-notes.md`
> (plus U1/U2 for the `IMediaStore` / `MediaObject` it references by id).

## Scope (in / out)

- **In (mine):** exactly the U4 deliverables — 3 files, all modify:
  - `src/Kumunita.Core/UserInfo/Profile.cs` — add the **additive**
    `public string? AvatarId { get; set; }` field (ADR 0004 §B.1 — like M3's
    `Post.Status`; summary verbatim from design doc §2.2 lines 442–447).
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — add the **single**
    write lane `Task SetProfileAvatarAsync(string subjectId, string? avatarId,
    string actorBy);` (C-MED·8), summary verbatim from design doc §2.2
    lines 452–457, in a named "Media additions" lane block (the ADR 0006-E
    compatible-addition idiom the M2/M3 blocks use in this file).
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — implement it:
    load `Profile` by `SubjectId`, **`KeyNotFoundException` if missing**
    (design doc §2.2 line 459–460 pins `KeyNotFoundException` — the doc
    wins over the group lanes' `InvalidOperationException`, noted in the
    handoff), set `profile.AvatarId = avatarId` (a `null` `avatarId`
    clears), `Store` + one `SaveChangesAsync` — "the same shape as
    `UpsertProfileAsync` / `UpdateGroupDescriptionAsync`" (line 461).
- **Out:** U5's lane tests, U6–U9's Web surface (`AvatarUpload` / `Avatar`
  actions, views, seam tests), ADR 0011 + doc sync (U10). No new seam on
  `IAuthorizationService` / `IUserInfoService` beyond this one (drift-guard
  rule 4); no new `AccessAction` / `AccessVia` id (C-MED·1).
- **Not mine to touch:** `Profile.ToAuditableResource` (context only — U7
  reuses it), the `Media/` module files (U1/U2, done), the `ProfileUpdate`
  record (the "null ⇒ don't touch" patch lane is a *different* contract: a
  null *patch field* means "leave untouched", which is exactly why the
  avatar needs its **own** explicit lane — `null` here *clears*, per the
  doc). Drift-guard rule 1: only the three files above.

## Constraints I keep pinned while writing

- **Verbatim shapes (doc wins, §2.7 rule 7 / "additive shapes pinned in
  §2.2"):**
  - `public string? AvatarId { get; set; }` — `string?`, nullable, on
    `Profile` (the pinned field).
  - `Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);`
    — exact parameter names/signature from design doc §2.2 line 457.
- **Missing profile fails closed:** design doc §2.2 says "`KeyNotFoundException`
  if missing". The existing group lanes (`UpdateGroupDescriptionAsync`,
  `SetGroupPrivacyAsync`) throw `InvalidOperationException` on a missing
  group — different surface (groups), and the **doc pins the avatar lane's
  exception**; I follow the doc and record the deviation from the group-lane
  idiom in the handoff (per §2.7 rule 3: note, don't silently drift).
  `media-u4-plan.md` §Risks says "match the existing lane idiom" — that is
  subordinated to the pinned doc shape (which is the higher authority per
  the plan's own rules); I still *verify* the existing lane (done: both
  group lanes throw on missing, `UpsertProfileAsync` load-or-creates) and
  take the fail-closed posture (throw) with the doc's exception type.
- **No audit row in this lane:** design doc §2.2's impl line names
  `UpsertProfileAsync` first ("the same shape as `UpsertProfileAsync` /
  `UpdateGroupDescriptionAsync`") — the *Profile-touching* read lane appends
  **no** `AccessAudit` row (its own doc-comment: "not an access decision, so
  invariant C3's audit lane does not apply here"), unlike the group lanes.
  The audit row for the avatar *serving* lane is owned by U7's existing
  `CanAsync(…Read…)` gate (C-MED·2). So: this lane writes the field only.
  **`actorBy` is therefore accepted per the pinned signature but not
  persisted here** — anticipated explicitly in `media-u4-plan.md` §Risks
  ("if the existing lane does not record `actorBy`, note the deviation in
  the handoff — the design doc pins the lane shape with `actorBy`, so the
  lane *must* accept it"). I accept it (signature verbatim) and note the
  recording deviation.
- **Session/commit shape (C3):** one `store.OpenSession(new
  SessionOptions())` session, `await session.LoadAsync<Profile>(subjectId)`,
  one `Store` + one `SaveChangesAsync` — the exact `UpsertProfileAsync` /
  `UpdateGroupDescriptionAsync` idiom (this codebase's proven Marten usage;
  the U2 "Marten 9 async-only" note is consistent with these `await using`
  sessions).
- **Clear with `null`:** `profile.AvatarId = avatarId;` with a `null`
  `avatarId` writes `null` — the lane must still `Store` + save so the clear
  persists (`media-u4-plan.md` §Risks).
- **Core stays HTTP-free (ADR 0006-D):** no Web types; `AvatarId` is a
  `string?` content hash id (C-MED·4) that points at `MediaObject.Id` — the
  lane does **not** load the `MediaObject` (no `IMediaStore` dependency here
  — the catalog reference is a plain id; U6/U7 validate existence at the
  Web/store seams).

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| plan `plan-media-file-storage.md` §U4 + `media-u4-plan.md` | the unit scope / deliverables / risks |
| design doc §2.2 lines ~440–463 | the pinned `AvatarId` + lane shapes (verbatim) |
| handoff `## U1` / `## U2` (+ verification) / `## U3` | prior seams; the `IMediaStore`/`MediaObject` this lane references by id |
| `src/Kumunita.Core/UserInfo/Profile.cs` | the POCO to extend (field placement + `ProfileUpdate` record) |
| `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (full) | lane block idiom ("── M2/M3 additions ──"), `UpsertProfileAsync` seam, frozen-surface pattern |
| `src/Kumunita.Core/UserInfo/UserInfoService.cs` (L1–120, 360–445, 820–910) | the impl idioms: `UpsertProfileAsync` (load-or-create, no audit), `UpdateGroupDescriptionAsync` / `SetGroupPrivacyAsync` (throw on missing + audit row) |

## Steps (each leaves the tree in a buildable state)

1. **Exec plan** (this file) — `docs/plans-milestones/in-progress/` (done).
2. `Profile.cs` — append `AvatarId` (additive, after `Address`, before the
   class close), summary verbatim from design doc §2.2.
3. `IUserInfoService.cs` — new "── Media additions ──" lane block before the
   M3 block, with the verbatim summary + the verbatim `SetProfileAvatarAsync`
   method (no `exception`-doc needed beyond what the doc pins; keep it to
   the pinned shape).
4. `UserInfoService.cs` — implement `SetProfileAvatarAsync` right after
   `UpsertProfileAsync` (cohesion: the other `Profile`-field lane), per the
   Constraints above.
5. **Exit:** `run_build` green on `Kumunita.Core` (unit register U4 Exit) —
   then the regression sanity pass the prior units ran: `dotnet build
   Kumunita.slnx -c Debug` + `dotnet exec` on both test assemblies (AGENTS.md
   path, **not** `dotnet test`) — additive POCO field + one new interface
   method must not regress the U3 220 `Core.Tests` / 60 `Web.Tests` baseline.
6. Append **`## U4`** to `media-file-storage-handoff-notes.md`: the
   `Profile.AvatarId` + lane signature (both verbatim from design doc §2.2),
   "no existing `IUserInfoService` seam was reshaped" (C-MED·1), the two
   noted deviations (exception type vs the group lanes; `actorBy` accepted,
   not persisted), the gate result. Flip the Status-table U4 row to done.

## Verification / risks

- **Exception-type tension** (design doc §2.2 `KeyNotFoundException` vs the
  group lanes' `InvalidOperationException`) — resolved by doc-wins (§2.7
  "the doc pins the lane shape"), noted in the handoff per rule 3.
- **`actorBy` unused in the impl** — accepted by the interface signature
  (pinned); C# emits no unused-parameter warning; noted in the handoff per
  `media-u4-plan.md` §Risks.
- **`ProfileUpdate` patch record untouched** — the "null ⇒ don't touch"
  patch rule cannot express "clear the avatar", hence the separate lane
  (the doc's explicit call: C-MED·8 single write lane).
- **Marten session idiom risk** — verified against the existing
  `UpsertProfileAsync` implementation in the same file (it compiles and is
  test-proven; I copy it, not the design doc's prose).
- **Additive-only field** — no positional `Profile` constructor call sites
  exist (object-initializer POCO); the field is nullable so no schema
  migration beyond Marten's own additive detection at next boot (U10 docs
  cover the restore-surface implications).
- **Gate (AGENTS.md):** the full-suite gate is §2.6's `dotnet exec … .dll`
  path (U10 is the only unit *required* to run it; I run it as a regression
  sanity pass, matching U1–U3).
