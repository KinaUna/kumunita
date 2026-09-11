# U4 — Avatar reference lane (UserInfo side): `Profile.AvatarId` + `SetProfileAvatarAsync`

> Self-contained: this file + the **entry reads** below is the whole context.
> The exact seam shapes are `docs/design/media-file-storage-design.md` §2.2
> (the `Profile.AvatarId` + `SetProfileAvatarAsync` signatures) + §2.7 rule 7.
> A prior handoff section (if any) is **U3**.

## Understanding

Add the **additive** `Profile.AvatarId` field (ADR 0004 §B.1 — like M3's
`Post.Status`) + the **single** write lane on `IUserInfoService` /
`UserInfoService` (`SetProfileAvatarAsync` — C-MED·8: owner scope is enforced
Web-side in U6; this lane only writes the field). This is the bridge between
the Core media store (U2) and the Web avatar surface (U6–U8) — the **one**
seam a future group-logo / post-attachment / badge lane will copy.

## Assumptions

- `Profile.AvatarId` is an **additive** `string?` field (C-MED·4 / ADR 0004 §B.1) —
  **no** existing `Profile` seam is reshaped (C-MED·1).
- `SetProfileAvatarAsync(subjectId, avatarId, actorBy)` is a **new method** on
  `IUserInfoService` (the **single** write lane, C-MED·8) — a `null` `avatarId`
  **clears** the field.
- `actorBy` is recorded (C-MED·8) — the owner-scoped write is enforced Web-side
  (U6 self-only); this lane records who did it.

## Approach

Add `Profile.AvatarId` (additive field). Add
`Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy)`
to `IUserInfoService` + implement in `UserInfoService` (load profile →
set field → save; if profile is `null`, no-op/throw per the existing lane
idiom — **verify** the existing `SetGroupModerator` shape first; if the
existing lane throws on a missing profile, do the same).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (the
  `Profile.AvatarId` + `SetProfileAvatarAsync` shapes) + §2.7 rule 7.
- `docs/plans-milestones/in-progress/media-u2-plan.md` + the **U2** handoff
  (the `IMediaStore` + `MediaObject` to reference by id).
- `src/Kumunita.Core/UserInfo/Profile.cs` — the POCO to add the field to.
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs` + `UserInfoService.cs` —
  the existing lane idiom to mirror (`SetGroupModerator` / `UpsertProfile`).
- the `Profile.ToAuditableResource` (already present, the serving lane reuses
  it in U7 — **context only**, U4 does **not** change it).

## Deliverables (3 files, modify)

- `src/Kumunita.Core/UserInfo/Profile.cs` — add
  `public string? AvatarId { get; set; }` (additive; ADR 0004 §B.1).
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — add
  `Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);`.
- `src/Kumunita.Core/UserInfo/UserInfoService.cs` — implement
  `SetProfileAvatarAsync` (load → set `AvatarId` → save).

## Risks & open questions

- **Profile missing:** if the `subjectId` has no profile, the lane should
  **fail closed** (throw or no-op — **verify** the existing
  `SetGroupModerator` idiom first; match it). A "silently create a profile
  with just an avatar" drifts the C-MED·8 lane.
- **`null` `avatarId`:** clears the field (C-MED·8 — the owner removes their
  own avatar). The lane **must** handle `null` explicitly (a `null`-coalescing
  assignment `Profile.AvatarId = avatarId;` handles it — but the lane must
  still load + save so the clear persists).
- The `actorBy` parameter is recorded (C-MED·8) — the lane **must** accept it
  and pass it to the existing audit trail if there is one (verify the
  existing lane idiom; if the existing lane does **not** record
  `actorBy`, note the deviation in the handoff — the design doc pins the
  lane shape with `actorBy`, so the lane **must** accept it).

## Steps

1. Add `Profile.AvatarId` (additive field — C-MED·4 / ADR 0004 §B.1).
2. Add `SetProfileAvatarAsync` to `IUserInfoService` (signature verbatim
   from the design doc §2.2).
3. Implement it in `UserInfoService` (load → set field → save; `null`
   clears; missing profile fails closed — match the existing lane idiom).
4. `run_build` → green on `Kumunita.Core`.
5. Append **`## U4`** to the handoff notes: the `Profile.AvatarId` + lane
   signature (both verbatim from the design doc §2.2), and a line confirming
   "no existing `IUserInfoService` seam was reshaped" (C-MED·1).
