# RC U07 — the seam tests (the 13 pinned tests, 3 files)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (§Pinned seam tests is authoritative — the names below are
> cross-checked against it; on a mismatch, **the design doc wins and
> you record `## U07 — Drift pause`** in the handoff note — do not
> silently rename).

## Goal

Author the **three remaining** test files (the 4th,
`MarkdownRendererTests.cs`, is U02's) — 13 tests total (5 + 4 + 4) — pinning the
R·3/R·4/R·6/R·7 behaviors the register's §Pinned seam tests names, so
the U08 acceptance gate has an executable definition. **Test code
only: no production file is modified by this unit** (if a test cannot
be written against the existing seam — a missing method, a shape
mismatch — that is a **drift pause**, not a production edit; record
it and stop).

## Entry reads (≤ 5 files)

1. `docs/design/rich-content-design.md` — §Invariants R·3–R·7,
   §Pinned seam tests (the **authoritative** 22-name list),
   §FACES R3/R4/R5/R7 (the behaviors the tests encode).
2. `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` — does not
   exist yet (you author it). Instead read
   `tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the
   NSubstitute controller-test harness: how `IAuthorizationService`,
   `IMediaStore`, and the services are substituted; the
   `User.Identity?.Name` actor-id convention) — the harness for the
   serving + upload tests.
3. `tests/Kumunita.Core.Tests/` — the one file exercising a
   `PostService` read seam (grep `PostService` in
   `tests/Kumunita.Core.Tests/`) — the Core-test harness (the
   `PostgresFixture`/Testcontainers shape — **note: Core tests take
   ~20 s and leave Docker containers behind if killed — record in the
   handoff note that U07's Core file joins that suite**; clean up
   with `docker container prune` if the process is killed, AGENTS.md).
4. `src/Kumunita.Web/Controllers/ContentImageController.cs` (U03+U04)
   — the exact action signatures + the guard ordering + the 5-step
   serving sequence (the tests assert **against this file's behavior**
   — read it top-to-bottom once; the test assertions mirror its
   order).
5. `src/Kumunita.Web/Security/ContentImageIds.cs` (U04 — or wherever
   U04's note says the helper lives) — the regex + the
   dedupe/order rule the `R3_*` tests assert.

## Deliverables (3 files, new — no modifications to any existing file)

### 1. `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` (new)

The 5 pinned names (the design doc is authoritative; the harness is
the one from entry read 3 — the `PostService` write-path shape,
substituted store/services per the codebase's existing Core-test
pattern):

1. **`R3_ImageIdsPopulatedFromBodyLinks`** — a draft whose body is
   `![a](/content-image/aaaa)` + `![b](/content-image/bbbb)` (+ a
   duplicate `![a](/content-image/aaaa)` again) → the stored doc's
   `ImageIds` is **exactly** `[aaaa, bbbb]` (deduped, first-
   occurrence order — R·3/U04's pinned rule).
2. **`R3_ImageIdsEmpty_WhenNoLinks`** — a body with a **link**
   (`[x](https://example.com)`) but **no** image link → `ImageIds`
   is empty (a link is not an image reference — the parse must not
   over-match the link pattern onto `[label](url)` text; also the
   remote-src case: `![x](https://evil.example/i.png)` → `ImageIds`
   **still empty** — the parse uses the route-shape predicate, not a
   blanket `![…](…)` match).
3. **`R5_ReverseLookup_FindsOwningPost`** — two posts, one with
   `ImageIds = [abc]`, one without → `FindPostByImageIdAsync("abc")`
   returns the owner; `FindPostByImageIdAsync("zzz")` returns null
   (the route's 404 branch, R·4 step 3).
4. **`R5_ReverseLookup_FindsOwningReply`** — same shape for
   `FindReplyByImageIdAsync` (the owner is a `PostReply`; the null
   case too).
5. **`R7_PostPoco_FieldSetUnmodifiedExceptImageIds`** — the R·7
   zero-migration pin made executable: construct a `Post` the way the
   service does (the existing M3 field set — read
   `src/Kumunita.Core/Posts/Post.cs` for the exact field list), assert
   **every** pre-RC field is present with its pre-RC type (a
   reflection-free compile-time assertion: assign each field, then
   `ImageIds = [..]`; the test passing **compiles** is the pin — if
   a pre-RC field's name/type changed, this file fails to compile,
   which is the R·7 alarm). Record the exact field list you asserted
   in the handoff note (it is the executable record of "5th additive
   field").

### 2. `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` (new)

The 4 pinned names — the NSubstitute harness (entry read 2), the
route's 5 steps asserted as **call-order + response** (the
substitutes record `Received()` calls — assert **exactly** the
pinned call counts, R·4):

1. **`R4_Member_Allows_200_OneAllowAuditRow`** — the store returns a
   `MediaObject`; the post-owner lookup returns a post;
   `authz.CanAsync(actor, Read, owner)` → **Allow** → assert the
   result is a `FileResult` with the stored content type, the
   response carries `X-Content-Type-Options: nosniff`, and
   `authz` received `CanAsync` **exactly once** (no double-audit —
   the route or the service performs it, exactly once, per U03's
   note).
2. **`R4_NonMember_Denies_404_OneDenyAuditRow`** — same setup,
   `CanAsync` → **Deny** → assert **404** (not 403 — R·4's no-leak
   rule) + `CanAsync` received exactly once + **no**
   `OpenReadAsync` call (the bytes never stream on Deny — assert the
   store received no read).
3. **`R5_Orphan_404_ForGlobalAdmin_ZeroAuditRows`** — the store
   returns a `MediaObject` (the bytes **exist**), **all** owner
   lookups return null (orphan — R·4's inert posture) → assert **404**
   even though the actor is a GlobalAdmin (the "including
   GlobalAdmin" case — there is no branch that serves an orphan,
   FACES R5) + `authz.CanAsync` received **zero** times (no owner ⇒
   no decision ⇒ no row — the audit belongs to the decision, not the
   fetch).
4. **`R4_PlatformPageOwner_ZeroCanAsyncCalls`** — the page-owner
   lookup returns a `LocalizedPage` (the post/reply/announcement
   lookups return null) → assert a `FileResult` served +
   `authz.CanAsync` received **zero** times (the platform branch —
   public by construction, FACES R6's serving half; the R6 render
   half is U06's grep proof).

### 3. `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` (new)

The 4 pinned names — the guards asserted as **status + no-write**
(the store substitute must record **zero** `PutAsync` calls on every
guard branch — R·6's "no file written on any guard"):

1. **`Upload_Empty_400_NoFileWritten`** — `file` null or
   `Length == 0` (an `IFormFile` substitute — check how the codebase's
   existing upload tests shape an empty form file; if no precedent,
   NSubstitute the interface) → **400** + `PutAsync` received zero
   times.
2. **`Upload_Oversize_413_NoFileWritten`** — a valid content type,
   `Length` above the cap (the seam's `MaxBytes` — the avatar test, if
   one exists, shows the property; if not, read
   `MediaOptions`/`IMediaStore`) → **413** + zero writes.
3. **`Upload_DisallowedType_Svg_415_NoFileWritten`** — the allowlist's
   explicit exclusion: `image/svg+xml` (ADR 0011's named exclusion —
   SVG is the canonical "disallowed type" case) → **415** + zero
   writes. (A second assert for a type not in the allowlist at all,
   e.g. `image/tiff`, may share this test — the **name** is the pin,
   the asserts inside are yours; record both in the handoff note.)
4. **`Upload_Valid_200_ReturnsJsonId`** — a small valid PNG
   (`image/png`, under the cap) → the store's `PutAsync` returns a
   `MediaObject` with `Id = "deadbeef"` (substitute) → assert **200**
   + the JSON body contains `"id":"deadbeef"` (the design doc's
   pinned response shape) + `PutAsync` received **exactly once** with
   the stored filename/content type (the guard→write ordering
   made executable).

## Exit criteria

- `dotnet build` green. **Run all three files' tests** via the
  in-process xunit.v3 path from `AGENTS.md` — the exact commands:
  ```powershell
  dotnet build Kumunita.slnx -c Debug
  dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
  dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
  ```
  Record the pass/fail counts per file (the Web file is the 8 new
  tests + the pre-existing suite — the **pre-existing** tests must
  pass **unmodified** — that is the R·7 "the pre-existing suites
  pass" half of the U08 gate; the Core file joins the ~20 s
  Testcontainers suite — record the total).
- **No production file modified** (the 3 new test files are the
  entire diff — verify with `git --no-pager status` and record the
  file list).
- **The `run_tests` / VS Test Explorer "No tests found" quirk is a
  known runner bug** (AGENTS.md §Running the tests) — the
  `dotnet exec` path is authoritative; do not retry the broken path.
- **Handoff note** (append): `## U07 — seam tests (13 tests, 3
  files)`, 6–8 lines: (a) the 15 test names grouped by file (3
  lines, one per file); (b) the pass counts (e.g. "Web: 8 new
  green, suite N/M; Core: 5 new green, suite P/Q" — the exact
  numbers); (c) the `R7_PostPoco_FieldSetUnmodifiedExceptImageIds`
  field list (the exact field names asserted); (d) the
   `Upload_DisallowedType_Svg_415_NoFileWritten` second assert (the
  non-SVG disallowed type used); (e) any drift pause (a test that
  could not be written as-pinned + the seam gap found); (f) the
  `docker container prune` reminder (only if the Core run was
  killed/interrupted).
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u07-plan.md
  docs\plans-milestones\done\rich-content-u07-plan.md`.
