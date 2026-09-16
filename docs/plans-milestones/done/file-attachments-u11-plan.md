# ATT U11 — Web tests: 10 pinned names (writable vs drift-paused, mirroring the image lane)

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U7 + U8 + U9 + U10 all shipped (the Web parse helper, the
> upload route, the serve route, and the editor are all in place). This unit is
> **code (tests only)** — it ends with a green `dotnet build` **and** the
> writable tests passing under the in-process runner.

## Understanding — read this before anything else

The 10 pinned Web test names (design doc §2.9) split into **two groups** with
**different executability**, and you **must** handle them the way the image
lane did — not the way a naive "write all 10" would:

- **Writable (5 names)** — driven against a **NSubstitute-only** controller
  harness (no Postgres). These are the upload guards (F6 ×3) and the
  renderer/editor round-trip (F7, F9). They mirror the **executable**
  `ContentImageUploadTests` (the 4 `Upload_*` tests there run) + the
  `MarkdownRendererTests` / `RichEditorTests` (the render/serialize tests run).
- **Drift-paused (5 names)** — the serve-route tests (F1–F5) are
  **unwritable** against the existing seam, for the **exact same reason** the
  image lane's `ContentImageServingTests` are all paused: the serve route's
  owner-resolution calls the **sealed** `PostService` reverse-lookup seams
  (`FindPostByAttachmentIdAsync` / `FindReplyByAttachmentIdAsync`), which are
  Postgres-backed (Marten), and `Kumunita.Web.Tests` has **no Testcontainers**
  (that lives only in `Kumunita.Core.Tests`). `PostService` is **sealed**, so
  NSubstitute **cannot proxy it**. The faithful state for these 5 is a
  **documented drift-pause** (a class that **names** the 5 tests, documents
  each one's intended assertion + the seam gap, and carries **zero `[Fact]`
  methods**) — **exactly** the shape of `ContentImageServingTests.cs`. **Do not
  author executable serve tests** (you'd need a production edit to make
  `PostService` substitutable, or Testcontainers in Web.Tests — both forbidden
  by the unit-series rules and the 3-file-diff shape). **Do not author
  non-pinned probe tests** either (the "exact-name-only" rule).

**The Core half of the serve behavior is already pinned** (U6's
`AttachmentOwnershipTests` — the reverse-lookup + the verbatim-write, driven
against a real Postgres in `Kumunita.Core.Tests`). The 5 Web serve tests would
only **additionally** pin the route's **decision + HTTP** layer (the 404
mapping, the single `CanAsync`, the `Content-Disposition` header) — which is
what the drift-pause record documents, per-name, for a future unit to lift.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — the **pinned Web seam-test
   names** (the 10 names, verbatim) and the **acceptance gate** (the runner
   commands). These live in U2's Part-2 sections (the register lists them as
   "§2.5 pinned seam tests (exact names)" + "§2.x the acceptance gate" — the
   exact subsection number may vary; **grep the doc for `AttachServe_F1` and
   `AttachUpload_F6` to find the canonical names**, and for the `dotnet exec`
   gate). Read the relevant subsections fully.
2. `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` — the **mirror
   source** for the 3 writable upload tests (F6). Read the **whole file**:
   the `TestFormFile` (the byte-backed `IFormFile` carrier), the `Build(…)`
   helper (the NSubstitute `IMediaStore` + `Options.Create<MediaOptions>` + the
   real sealed `PostService`-over-substitutes + the `KumunitaPrincipal` minting
   shape), and the 4 `Upload_*` tests (the `DidNotReceiveWithAnyArgs().PutAsync`
   guard-assert idiom, the `StatusCodeResult` / `JsonResult` assertions).
3. `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` — the **mirror
   source** for the 5 drift-paused serve tests. Read the **whole file**: the
   file-header drift-pause note (the seam-gap paragraph), the class with **zero
   `[Fact]`** methods, and the per-name "Intended (drift-paused) / Why it is
   paused" comment blocks. **Your `AttachmentServingTests` is the same shape.**
4. `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` — the **mirror source**
   for F7 (a `/attachment/` link with a remote/non-route `url` renders as plain
   escaped text) — read the `/content-image/` reject tests (≈ L95–L110) and
   mirror the link-reject shape for the attachment route.
5. `tests/Kumunita.Web.Tests/RichEditorTests.cs` — the **mirror source** for
   F9 (the editor round-trip: `attachLink` → `renderPreview` → the `<a href>`
   is preserved). Read the `/content-image/` round-trip tests (≈ L66–L77,
   L171–L180) + the `imageLink`-shape test (≈ L433) and mirror for
   `attachLink`.
6. `src/Kumunita.Web/Controllers/AttachmentController.cs` — the **SUT** (U8/U9).
   Read the `Upload` action + the `Serve` action so your tests' assertions match
   the real return types (`BadRequestObjectResult` / `StatusCodeResult` /
   `JsonResult` / `FileResult`) and the real header names.
7. `src/Kumunita.Web/Security/AttachmentIds.cs` — the **SUT** for F7 (U7).
   Confirm the `ExtractAttachmentIds` reject shape (a body with a remote
   `/attachment/…` URL — or no `/attachment/` link at all — returns empty).
8. `src/Kumunita.Web/client/lib/rich-editor.ts` — the `attachLink` fn (U10).
   Confirm the exact `[label](/attachment/{id})` form your F9 test asserts.

## Deliverables (3 new test files, mirroring the image lane's 3-file shape)

### 1. `tests/Kumunita.Web.Tests/AttachmentUploadTests.cs` (5 writable tests → 3 of the 10 pinned names)
Mirror `ContentImageUploadTests.cs` **structurally** (the `TestFormFile`, the
`Build(…)` helper, the guard-assert idiom), but target `AttachmentController.Upload`
and the **attachment** allowlist. The 3 pinned names you author here:

- `AttachUpload_F6_Empty400` — null + zero-byte file → `BadRequestObjectResult`,
  `media.DidNotReceiveWithAnyArgs().PutAsync(…)`. (Mirror
  `Upload_Empty_400_NoFileWritten`.)
- `AttachUpload_F6_Oversize413` — a valid content type over `MaxBytes` (set
  `MaxBytes = 16`, 32-byte payload) → `StatusCodeResult`
  `413RequestEntityTooLarge`, `DidNotReceiveWithAnyArgs().PutAsync(…)`.
  (Mirror `Upload_Oversize_413_NoFileWritten`.)
- `AttachUpload_F6_WrongType415` — a non-allowlisted type →
  `StatusCodeResult` `415UnsupportedMediaType`, `DidNotReceiveWithAnyArgs().
  PutAsync(…)`. **Two asserts** (the image test's shape): (a) SVG
  (`image/svg+xml` — the named exclusion, C-ATT·6), (b) a type **not** in the
  attachment allowlist at all (e.g. `application/x-tar` or `video/mp4` — pick
  one the U8 default clearly excludes). (Mirror
  `Upload_DisallowedType_Svg_415_NoFileWritten`.)

(You may **also** add the `Upload_Valid_200_ReturnsJsonId`-equivalent as a
**non-pinned** helper test to pin the "one write + JSON id" shape — the image
lane does — **but** it is **not** one of the 10 pinned names, so label it
clearly as a support test and **do not** rename a pinned name to it.)

### 2. `tests/Kumunita.Web.Tests/AttachmentServingTests.cs` (5 drift-paused names, zero `[Fact]`)
Mirror `ContentImageServingTests.cs` **exactly in shape**: a file-header
drift-pause note (the **same** seam-gap paragraph — the sealed `PostService`
reverse-lookup + no Testcontainers in Web.Tests + the "exact-name-only" +
"3-file-diff" rules), a class `AttachmentServingTests` with **zero `[Fact]`
methods**, and **per-name** "Intended (drift-paused) / Why it is paused"
comment blocks for:

- `AttachServe_F1_AudienceMemberDownloads` — intended: post-owner `CanAsync`
  Allow → `FileResult` with the stored content type + `nosniff` +
  `Content-Disposition: attachment`; `CanAsync` received exactly once.
- `AttachServe_F2_NonMember404` — intended: `CanAsync` Deny → **404** (not 403)
  + `CanAsync` once + **no** `OpenReadAsync` call.
- `AttachServe_F3_Orphan404` — intended: store returns a `MediaObject`, **all**
  owner lookups null → **404** even for a GlobalAdmin (zero audit rows).
- `AttachServe_F4_ReplyParentDeny404` — intended: reply-owner → parent resolved
  (C-ATT·8) → parent `CanAsync` Deny → **404** + one `Deny` row. **This is the
  ATT-lane-specific one** (the image lane's reply test is the 404-drift-pause;
  yours documents the **parent-resolution** behavior that a future unit would
  lift).
- `AttachServe_F5_AnnouncementPublicServes` — intended: announcement-owner →
  the flat `GetAsync` scope gate passes → `FileResult` + `nosniff` +
  `Content-Disposition: attachment` (no `CanAsync`, no audit row).

Each "Why it is paused" block states the **same** seam gap as the image file
(sealed `PostService` reverse-lookup undrivable from the NSubstitute-only
harness) **plus**, for F4, the note that the parent-resolution seam (U9's) is
what a future lift would drive.

### 3. `tests/Kumunita.Web.Tests/AttachmentLinkTests.cs` (2 writable tests → F7 + F9)
Mirror the `MarkdownRendererTests` / `RichEditorTests` shape:

- `AttachLink_F7_RemoteUrlRendersText` — a body whose "attachment" URL is
  **remote** or malformed (e.g. `https://example.com/attachment/x` or
  `/attachment/notahexid`) is **not** treated as an attachment: the renderer
  emits plain escaped text (no `<a href>` to a remote), and
  `AttachmentIds.ExtractAttachmentIds` returns **empty** for it. (Mirror the
  image lane's remote-`src` reject test.)
- `AttachRoundtrip_F9_HrefPreserved` — `attachLink("Docs",
  "deadbeefcafe…")` produces `[Docs](/attachment/deadbeefcafe…)`; that body,
  through `RichEditorSpec.RenderPreview` (or the `renderPreview` path the image
  lane's round-trip test uses), emits a working `<a
  href="/attachment/deadbeefcafe…">`; and `AttachmentIds.ExtractAttachmentIds`
  on that body returns the id. (Mirror the image lane's round-trip test.)

## Build + test gate (both must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on Core + Web. **Then** run the **Web** suite **in-process** (the runner
quirk — **never** `dotnet test`, AGENTS.md):

```
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```
The **7 writable** tests (F6 ×3 + F7 + F9 + the optional valid-upload support
test) **must pass**. The 5 drift-paused serve tests **carry no `[Fact]`** (so
they trivially "pass" as an empty class — that is the faithful state, not a
failure). If a **pre-existing** Web test fails, that is not yours to fix —
record it and move on. (The Web run is <1 s.)

## Risks & open questions

- **The drift-pause is the faithful state, not a failure.** The 5 serve tests
  being unexecutable is **correct** — it mirrors the image lane exactly. If
  you're tempted to "make them pass" by adding Testcontainers to Web.Tests or
  un-sealing `PostService`, **stop** — that is a production edit / new-infra
  file the unit-series rules forbid, and it would break C-ATT·9 (touching
  `PostService`). The `ContentImageServingTests` precedent is the model.
- **Do not rename the 10 pinned names.** They are cross-referenced by the
  design doc §2.9 and the register. Your 3 upload tests use **exactly**
  `AttachUpload_F6_Empty400` / `AttachUpload_F6_Oversize413` /
  `AttachUpload_F6_WrongType415`; your 2 link tests use **exactly**
  `AttachLink_F7_RemoteUrlRendersText` / `AttachRoundtrip_F9_HrefPreserved`;
  your 5 paused tests are named **exactly** `AttachServe_F1_…` …
  `AttachServe_F5_…` in the comment blocks.
- **The F6 wrong-type test needs a type the U8 default excludes.** Read the U8
  default (11 types: pdf, msword, docx, ms-excel, xlsx, text/plain, text/csv,
  zip, + 4 raster). SVG (`image/svg+xml`) is the named exclusion; pick a
  second type clearly absent (e.g. `video/mp4` or `application/x-tar`). Do not
  pick a type that's *in* the default.
- **`PostService` is sealed (confirmed).** Do not attempt to proxy it. The
  upload tests satisfy the `PostService posts` ctor arg with a **real**
  `PostService` over never-used substitutes (the image test's idiom — its ctor
  null-checks only, and `Upload` never calls it).
- **Do not touch the image test files** (`ContentImageUploadTests` /
  `ContentImageServingTests` / `MarkdownRendererTests` / `RichEditorTests`) —
  they stay byte-for-byte (C-ATT·9). You are adding **three new** files.
- **Do not add tests to `Kumunita.Core.Tests`** in this unit — U6 owns those
  (they already exist). These are **Web** tests only.

## Steps

1. Read the 8 entry reads (design doc §2.9/2.10 first, then the 4 image-lane
   Web test files, then the 2 SUT files + the `attachLink` fn).
2. Create `AttachmentUploadTests.cs` (3 pinned F6 tests + the optional valid-
   upload support test), mirroring `ContentImageUploadTests`.
3. Create `AttachmentServingTests.cs` (the 5 pinned F1–F5 names, **zero**
   `[Fact]`, per-name drift-pause blocks), mirroring `ContentImageServingTests`.
4. Create `AttachmentLinkTests.cs` (F7 + F9), mirroring the renderer/editor
   round-trip tests.
5. `dotnet build Kumunita.slnx -c Debug` → green. `dotnet exec tests\Kumunita.
   Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` → the 7 writable tests
   pass; the 5 paused are an empty class.
6. Re-read the 3 files: confirm the 10 pinned names are present **exactly**
   (3 in upload, 5 in the paused comments, 2 in link), the paused class has
   **zero** `[Fact]`, and no image test file changed.
7. Append a `## U11` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Added 3 Web test files: `AttachmentUploadTests` (F6 ×3, executable),
   `AttachmentServingTests` (F1–F5, **drift-paused** — the sealed-`PostService`
   seam gap, mirroring the image lane), `AttachmentLinkTests` (F7 + F9,
   executable). Build green (Core+Web); the 7 writable tests pass under the
   in-process runner. Drift: <none / describe — esp. the F6 wrong-type pick
   and the F9 serializer path>. Next agent (U12) is the **close** unit — ADR
   0034 (Amends 0025 + 0011), the ADR index row, SECURITY/OPS/ARCHITECTURE/
   README, the design doc `## Closed`, the handoff `## Summary`, and the
   `in-progress/` → `done/` file moves."
8. Done.
