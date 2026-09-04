# 🎯 U11 — M2 profile editor (ProfileController + /profile/edit + /profile/preview + audience editor partial)

## Understanding
Ship M2's write surface: a `ProfileController` with `Edit()` (GET+POST, writes through M1's `UpsertProfileAsync` — the single write seam) and `Preview(string asSubjectId)` (GET only, read-only view-as through `DirectoryService.PreviewAsAsync`), a `ProfileEditViewModel` (+ `ProfilePreviewViewModel` record), a single reusable `_AudienceEditor.cshtml` partial used for both `Visibility` and `ContactVisibility`, and one xUnit view-model test `ProfileEditViewModel_ContactVisibility_Gated` (the §9 pin at the view-model layer). M1's `AccountController.Profile` stays until U14. Close M1's `Profile.cshtml` "M2 adds the audience editor" line.

## Assumptions
- `DirectoryService` is the concrete sealed class (U7 deviation-1); no `IDirectoryService` exists.
- No `KumunitaClaimsPrincipal` type; actor id = `KumunitaPrincipal.SubjectId(User)` (U7 deviation-2).
- `IIdentityService` not needed in ctor (U9/U10 ctor precedent: `(IUserInfoService)`-only pattern); Preview uses `DirectoryService.PreviewAsAsync`.
- `Profile.SubjectId`/`Group.Id` are `string` (opaque), not `Guid`.
- M1 `AccountController.Profile` GET/POST remain untouched (U14 removes).

## Approach
1. Read entry files: `AccountController.cs`, `IUserInfoService.cs`, `Profile.cs`, `Authorization/Audience.cs` + mode/grant types, design doc § (U11-relevant: §2.4 truth table, view-as read-only line, any U11 pins), M1 `ProfileViewModel.cs` + `Profile.cshtml`, `DirectoryService.PreviewAsAsync` shape, `KumunitaPrincipal`, existing Web tests.
2. Create `ProfileEditViewModel.cs` (DisplayName, Email, PreviewAs, Audience Visibility, Audience? ContactVisibility, ProfilePreviewViewModel record) + gated validation rule (ContactVisibility settable only when Visibility non-null).
3. Create `ProfileController.cs` (Edit GET+POST via `UpsertProfileAsync`; Preview GET via `DirectoryService.PreviewAsAsync`).
4. Create `Views/Profile/Edit.cshtml` (form + two `<partial name="_AudienceEditor">` + ViewAs selector) and `Views/Profile/_AudienceEditor.cshtml` (Via radios + Any/All + grant list) and `Views/Profile/Preview.cshtml`.
5. Add test `ProfileEditViewModel_ContactVisibility_Gated` in `tests/Kumunita.Web.Tests/`.
6. `run_build` green for `src/Kumunita.Web` (+ tests), `run_tests` Web.Tests green.
7. Close M1's `Profile.cshtml` "M2 adds the audience editor" comment.
8. Append the U11 handoff section to `docs/plans-milestones/m2-handoff-notes.md` (lists `UpsertProfileAsync` call + `ProfileEditViewModel` fields + the two partial names + deviations).

## Key Files
- src/Kumunita.Web/Controllers/ProfileController.cs (new)
- src/Kumunita.Web/Models/ProfileEditViewModel.cs (new)
- src/Kumunita.Web/Views/Profile/Edit.cshtml, Preview.cshtml, _AudienceEditor.cshtml (new)
- tests/Kumunita.Web.Tests/ProfileEditViewModelTests.cs (new)
- docs/plans-milestones/m2-handoff-notes.md (append)

## Risks & Open Questions
- `Audience` form binding: how `AudienceGrant`/sub-fields bind to Razor inputs — need to check the `Audience` shape first (U11 risk line: editor "binds to a single Audience field — the same shape as the two Audience fields on Profile").
- `Preview` route vs reserved `AccountController` paths — `/profile` controller is fresh; confirm `Startup`/routing has no conflict.

**Progress**: 22% [██░░░░░░░░]

**Last Updated**: 2026-09-04 08:11:28

## 📝 Plan Steps
- ✅ **Read entry files (AccountController, IUserInfoService, Profile, Audience types, DirectoryService, KumunitaPrincipal, M1 ProfileViewModel/Profile.cshtml, Web tests)**
- ✅ **Read design doc U11-relevant pins (view-as read-only line, §2.4 truth table, U11 pin if any)**
- 🔄 **Create ProfileEditViewModel.cs (+ ProfilePreviewViewModel)**
-  **Create ProfileController.cs (Edit GET+POST + Preview GET)**
-  **Create Views/Profile/Edit.cshtml + _AudienceEditor.cshtml + Preview.cshtml**
-  **Close M1 Profile.cshtml "M2 adds the audience editor" comment**
-  **Add tests/Kumunita.Web.Tests/ProfileEditViewModelTests.cs (ProfileEditViewModel_ContactVisibility_Gated)**
-  **run_build green (Web + tests) + run_tests Web.Tests green**
-  **Append U11 section to m2-handoff-notes.md**

