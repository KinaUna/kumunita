# U9 execution plan (working) — Web seam tests: the upload guard (U7a/b/c) + the FACES M1–M6 serving gate

> My (unit U9's) working plan for this pass. The **authoritative spec** is the
> U9 entry in `plan-media-file-storage.md` + the authored `media-u9-plan.md` +
> the **design doc** `../../design/media-file-storage-design.md` **§2.5**
> (the exact Web test names, L529–559) + **§2.4** (the `AvatarUpload` guard
> shape, L498–527) + **§2.3** (the `Avatar` serving-action shape, L463–496) +
> FACES (L116–140) + §2.6 (the gate) + §2.7 rules 1/3 (deliverable scoping +
> the test-name pin). Prior handoff sections read: **U8** (views + the route
> "typo" note + the upload-failure presentation pin), **U7** (the serving
> action + the FACES M1–M6 row table + the seven resolutions), **U6** (the
> upload action + its "§2.5 U7a/b/c test names U9 will lock" note, L427–434).

## Scope (in / out)

- **In (mine):** the register's Deliverables — **2 new test files, zero
  source changes** (U6's `AvatarUpload` and U7's `Avatar` are frozen and
  correct; U9 only asserts):
  - `tests/Kumunita.Web.Tests/ProfileAvatarServingTests.cs` — FACES M1–M6
    (the six pinned names, §2.5 L547–553, verbatim).
  - `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs` — the U7a/b/c
    guards (the four pinned names, §2.5 L555–559, verbatim).
  - Per-unit standing additions: this exec-plan, the `## U9` handoff section,
    and the status-table U9 row flip (line 27 of
    `media-file-storage-handoff-notes.md`, `pending` → `done (2026-09-11)`;
    the U8 precedent).
- **Out:** anything under `src/` (§2.7 rule 1 — I never touch a file outside
  this Deliverables set, including *fixing* the one doc-vs-prose discrepancy
  I found, FACES U7a's "403" vs §2.4's `Unauthorized()`: flagged for U10, not
  changed — see note 5). `Kumunita.Core.Tests` (I touch no Core file — the
  223/223 baseline stands from U5; not re-run, the U6/U7/U8 precedent). The
  "no avatar set → 404" branch (§2.3 L424) and the U7a defensive line —
  **neither has a §2.5-pinned name**, so per §2.7 rule 3 (exactly the §2.5
  names; no test drift) there are **exactly 10 tests, no more**. Follow-on
  lanes (group logos / post attachments / badge icons) are U10+/own-feature
  territory (§2.7 rule 7).

## The authoritative test names (doc wins, §2.7 — recorded)

The U9 register entry cites *short-form* names (`Upload_Owner_Roundtrip`,
`Upload_NonOwner_Rejected`, `Upload_Oversize_Rejected`,
`Upload_WrongType_Rejected` + `Serving_M1..M6`). Those are **not** the exact
names the design doc §2.5 pins (L547–559). The doc is the primary reference
tier ("units match verbatim") and §2.7 rule 3 names "the §2.5 list" as the
name source — **the §2.5 names win**. U6's handoff (L427–434) already resolved
the same question for U9 ("the §2.5 U7a/b/c test names U9 will lock (design
doc L555–559, verbatim)") and U8's note repeats it — this plan merely
re-records the choice with its basis.

| Pinned name (§2.5, verbatim) | File | Locks |
|---|---|---|
| `Serving_SignedAuthorizedOwner_Returns200_CorrectContentType` | Serving | FACES M1 — owner self-serve, `200` + stored `Content-Type` + nosniff + the `CanAsync` call (audit committed by the seam) |
| `Serving_SignedAuthorizedOther_Returns200` | Serving | FACES M2 — authorized other, `200` + stored `Content-Type` + nosniff |
| `Serving_SignedDeniedAudience_Returns404_And_Audits` | Serving | FACES M3 — `404` **after** the seam; `CanAsync` received exactly once (the audit row the name's "And_Audits" half pins); no `IMediaStore` calls |
| `Serving_BlockedProfile_Returns404` | Serving | FACES M4 — `404` **before** the decision: `CanAsync` **not** received (no audit row), no `IMediaStore` calls; blocked supersedes even with `AvatarId` set |
| `Serving_UnknownProfile_Returns404` | Serving | FACES M5 — `GetProfileAsync` → `null` → `404`; nothing downstream called |
| `Serving_Unsigned_Challenges_No200` | Serving | FACES M6 — no principal → `ChallengeResult` (never `200`); `userInfo` never called |
| `Upload_OwnerValidRaster_SetsAvatarAndServes` | Upload | U6 happy lane: guards pass → `PutAsync` (store-first) → `SetProfileAvatarAsync(subject, storedId, subject)` → `RedirectToAction("Edit")`; "AndServes" = the store's returned `Id` is the id the serving lane later resolves (the round-trip pin) |
| `Upload_Oversize_Returns413_NoFileWritten` | Upload | U7b — size guard; `NoFileWritten` = `PutAsync` + `SetProfileAvatarAsync` both `DidNotReceive` |
| `Upload_WrongType_Returns415_NoFileWritten` | Upload | U7c type — `image/svg+xml` (C-MED·5's named exemplar); both seams `DidNotReceive` |
| `Upload_Empty_Returns400` | Upload | U7c empty — `BadRequestResult` with the U6-pinned body `"Choose an image."`; both seams `DidNotReceive` |

**File layout:** the register suggests *one* `ProfileAvatarTests.cs` but its
own deliverable clause cedes the layout ("or fold into an existing … test
file — the unit chooses, notes it"). I choose §2.5's own two-file split
(L547 / L555) — the doc names both files and the register's escape clause
permits the choice. Noted here + carried to the handoff.

## Harness (mirrors `AnnouncementControllerTests` — the repo's controller-test idiom)

Verified pins I write against (shipped code, not the doc snippets):

```csharp
// ProfileController (frozen — U6 U7):
public ProfileController(
    IUserInfoService userInfo,
    DirectoryService directory,                            // CONCRETE + SEaled (Core UserInfo, L89-98)
    Kumunita.Core.Authorization.IAuthorizationService authz,
    IMediaStore media,
    IOptions<MediaOptions> mediaOpts)
```

- **NSubstitute** for the three interfaces (`IUserInfoService`,
  `Kumunita.Core.Authorization.IAuthorizationService`, `IMediaStore`) — the
  `AnnouncementControllerTests` idiom (NSubstitute 5.3.0 already in the
  test csproj).
- **`DirectoryService` (ctor param #2) is `sealed` — NSubstitute cannot
  proxy it** (and its ctor takes two interfaces). The two actions under test
  (`AvatarUpload` L359–384, `Avatar` L416–440) reference **only**
  `userInfo` / `authz` / `media` / `mediaOpts` (verified by reading the
  action bodies), so I construct the **real** `new DirectoryService(
  userInfoSub, authzSub)` and pass it — no null literal (the
  `Nullable enable` test project would flag CS8625 against the 0-warning
  gate), no proxy.
- `ControllerContext = new ControllerContext { HttpContext = new
  DefaultHttpContext { User = … } }` — `DefaultHttpContext.Response.Headers`
  is a mutable `HeaderDictionary`, so the action's
  `Response.Headers["X-Content-Type-Options"] = "nosniff"` (L438) is
  directly assertable.
- The subject claim the controller mints:
  `KumunitaPrincipal.SubjectId` reads claim type **`"Kumunita.Sub"`**
  (= `Kumunita.Core.Identity.ClaimTypes.Subject`, ThinPrincipal.cs L55) —
  principal = `new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(
  Kumunita.Core.Identity.ClaimTypes.Subject, "<id>" ) },
  authenticationType: "test"))` (the `AnnouncementControllerTests` L338–345
  shape). M6 = no principal (`User` absent / empty `ClaimsPrincipal`).
- `authz.CanAsync(viewer, AccessAction.Read, Arg.Is<ProfileToAuditableResource>(a => a.Id == "<subject>"))`
  → `Returns(new Decision(Allowed: …, AccessVia: Owner|Audience, "<id>"))`.
  The controller consumes **only** `Allowed` (L429) — `AccessVia.Owner` for
  M1, `AccessVia.Audience` for M2/M3 (the denied decision's `Via` is
  irrelevant to the action; the decision record's other fields are seam-
  internal).
- **`[ValidateAntiForgeryToken]` is transparent** to direct invocation (no
  filter pipeline) — noted already in U6's handoff resolution 1; no token in
  the fixture. Same for class-level `[Authorize]` — the M6 test exercises
  the *in-action* defensive guard only (the register's FACES M6 row).
- Upload payloads: a real `Microsoft.AspNetCore.Http.FormFile`
  (`new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)`
  + ContentType) so `CopyToAsync` / `Length` are native; **if the
  `ContentType` setter is unavailable in this ASP.NET Core version, fall back
  to an NSubstitute `IFormFile` whose `CopyToAsync` writes the bytes into
  the passed stream** (assertion surface is identical). The build settles
  which form compiles; noted to the handoff if the fallback is taken.

`MediaOptions` for the guard tests: default instance for happy/415/400
(`MaxBytes` 5 MiB, default `jpeg|png|webp|gif` allowlist — `IsAllowed` is
exact-match, `MediaOptions.cs` L26–29); `new MediaOptions { MaxBytes = 16 }`
+ a 32-byte png payload for the 413 row (type *valid*, size violated —
isolates the size guard). `MediaObject` for the serving tests:
`new MediaObject { Id = mediaId, ContentType = "image/png", SizeBytes =
payload.Length, CreatedById = owner }`.

## Test specs (10 — the exact §2.5 set)

Serving fixtures (per test, one `DefaultHttpContext` each):

- **M1** `Serving_SignedAuthorizedOwner_Returns200_CorrectContentType` —
  principal = `owner`; `Avatar(owner)`. `GetProfileAsync(owner)` → profile
  (`SubjectId=owner`, `DisplayName="Ada"`, `Blocked=false`,
  `AvatarId=mediaId`). `CanAsync(owner, Read, …Id==owner)` → `Decision(true,
  Owner, owner)`. `GetAsync(mediaId)` → mediaObject; `OpenReadAsync(mediaId)`
  → `MemoryStream(payload)`. Asserts: `FileStreamResult`
  (≡ the `200`), `ContentType == "image/png"` (stored type, C-MED·5),
  `FileContentLength == payload.Length` + the stream's bytes == `payload`,
  `Response.Headers["X-Content-Type-Options"] == "nosniff"`,
  `authz.Received(1).CanAsync(…)` (the owner-branch audit commit, C-MED·2),
  `media.Received(1).OpenReadAsync(mediaId)` (serving through the seam only,
  C-MED·3 — no static path exists to bypass).
- **M2** `Serving_SignedAuthorizedOther_Returns200` — principal = `viewer`;
  `Avatar(other)`. Same profile (owner = `other`). `CanAsync(viewer, Read, …)`
  → `Decision(true, Audience, other)`. Same serving asserts as M1 (incl.
  nosniff + `Received(1)`).
- **M3** `Serving_SignedDeniedAudience_Returns404_And_Audits` — principal =
  `viewer`; `Avatar(other)`. `CanAsync(viewer, Read, …)` → `Decision(false,
  Audience, other)`. Asserts: `NotFoundResult` (the "404, never 403" fail-
  closed idiom), `authz.Received(1)` (the audit row the name pins), and
  `media` `DidNotReceive` `GetAsync`/`OpenReadAsync` (a denied serve never
  reaches the bytes).
- **M4** `Serving_BlockedProfile_Returns404` — principal = `viewer`;
  `Avatar(other)`; profile `Blocked = true` **and `AvatarId = mediaId`**
  (the ordering pin — blocked supersedes the avatar, the §2.3 L423
  before-the-decision ordering). Asserts: `NotFoundResult`,
  `authz.DidNotReceive` `CanAsync` (the "no audit row" half of M4's pin),
  `media` `DidNotReceive`.
- **M5** `Serving_UnknownProfile_Returns404` — principal = `viewer`;
  `Avatar("subj-unknown-001")`; `GetProfileAsync` → `null`. Asserts:
  `NotFoundResult`; `authz.DidNotReceive`; `media.DidNotReceive`.
- **M6** `Serving_Unsigned_Challenges_No200` — no principal; `Avatar(owner)`.
  Asserts: `ChallengeResult` (≡ never `200` / never `FileStreamResult`),
  `userInfo.DidNotReceive` `GetProfileAsync` (the viewer guard precedes the
  seam).

Upload fixtures (principal = `owner` for all four — self-only by
structural identity; the foreign-subject scenario is structurally
unreachable, §2.4 L524–527):

- **`Upload_OwnerValidRaster_SetsAvatarAndServes`** — defaults; file
  `("pixel.png", "image/png", 12-byte payload)`. `PutAsync` →
  mediaObject(`Id=mediaId`). Asserts: `RedirectToActionResult` +
  `ActionName == "Edit"`; `media.Received(1).PutAsync(<bytes name="=payload">,
  "pixel.png", "image/png", owner)` (C-MED·6 boundary — bytes cross the
  seam); `userInfo.Received(1).SetProfileAvatarAsync(owner, mediaId, owner)`
  (C-MED·8 single lane, store-first order — the `mediaId` the store returned
  is exactly the id the `Serving` lane resolves, the "AndServes" half of the
  name).
- **`Upload_Oversize_Returns413_NoFileWritten`** — `MaxBytes = 16`; file
  `("big.png", "image/png", 32-byte payload)`. Asserts: `StatusCodeResult`
  with `413`; `media.DidNotReceive` `PutAsync`; `userInfo.DidNotReceive`
  `SetProfileAvatarAsync` (no volume file exists, the `NoFileWritten` half).
- **`Upload_WrongType_Returns415_NoFileWritten`** — defaults; file
  `("logo.svg", "image/svg+xml", 16-byte payload)`. Asserts:
  `StatusCodeResult` with `415`; both seams `DidNotReceive` (C-MED·5's SVG
  exemplar is the type that must never reach a `Put`).
- **`Upload_Empty_Returns400`** — defaults; file `("empty.png", "image/png",
  0-byte payload)`. Asserts: `BadRequestResult` with
  `Error == "Choose an image."` (the U6-pinned U7c-empty body); both seams
  `DidNotReceive`.

## Gate (Exit, per §2.6 + the register)

1. `dotnet build Kumunita.slnx -c Debug` → **0 errors, 0 warnings** (new
   test code compiles clean under `Nullable enable`).
2. `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
   (the AGENTS.md path — **not** `dotnet test`, not the `run_tests` bridge)
   → **70 total / 0 failed / 0 errors** (= the U6/U7/U8 60/60 baseline + the
   10 U9 tests).
3. `Kumunita.Core.Tests` **not run** (no Core file touched — the 223/223
   baseline stands from U5; U6/U7/U8 precedent) — noted in the handoff.
4. **Nothing staged or committed** — the user reviews first.

## Deviations / notes to carry into the handoff (the "noted per §2.7" list)

1. **Two-file layout** (`ProfileAvatarServingTests.cs` +
   `ProfileAvatarUploadTests.cs`) over the register's one-file suggestion —
   the design doc §2.5 names both files; the register's own escape clause
   ("the unit chooses, notes it") + doc-wins.
2. **Name source**: §2.5 L547–559 verbatim over the register's short-form
   names (U6 handoff L427 already made the same call for U9).
3. **Exactly 10 tests** — the "no avatar set → 404" branch (§2.3 L424) has no
   §2.5 name and is not tested here (rule 3); the action's fail-closed shape
   covers it at runtime (U8's 404-tolerant `onerror` render contract + the
   §2.3 L479 comment pin it in the code).
4. **`DirectoryService` ctor param** — real instance built from NSubstitute
   deps (sealed → unproxyable; unused by the two actions under test) over a
   null literal (CS8625 against the 0-warning gate).
5. **FACES U7a prose says "403" (L134–136); the shipped code + §2.4's pinned
   code say `Unauthorized()` = 401 (L507; `ProfileController` L363)** — the
   code matches the doc's *code* pin, so §2.7 rule 2 (doc wins → fix code)
   does **not** trigger; the intra-doc 403-vs-401 prose discrepancy has no
   §2.5 test name behind it and is **flagged to U10** for the
   `SECURITY.md` (e) reconciliation, not fixed here.
6. **FormFile carrier** — real `FormFile` if its `ContentType` setter
   compiles on this ASP.NET Core; else an `IFormFile` substitute with a
   copy-back (assertion surface identical). Whichever compiles is noted.

## Steps

1. Write `ProfileAvatarServingTests.cs` (6 tests, the fixtures above).
2. Write `ProfileAvatarUploadTests.cs` (4 tests, the fixtures above).
3. Gate 1 (build) → Gate 2 (`dotnet exec`, expected 70/70). Fix and re-run
   until green.
4. Append **`## U9`** to `media-file-storage-handoff-notes.md`: the two test
   file paths, the 10 names (verbatim), the pass count (70/70, verified via
   `dotnet exec`), the FACES M1–M6 row table (row ↔ assertion ↔
   `ProfileController` line — the contract every follow-on serving lane
   copies), the deviation notes 1–6 above, "nothing staged or committed",
   and the Next line (U10: `## Summary` + ADR 0011 + the four doc
   reconciliations + the design doc `## Media — Closed (recorded)` + the
   note-5 403-vs-401 line). Flip the status-table U9 row
   (`pending` → `done (2026-09-11)`).
