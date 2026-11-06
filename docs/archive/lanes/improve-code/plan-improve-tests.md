---
id: plan-improve-tests
title: "Plan: fix the 10 stale test pins after the IMPROVE-CODE-2/3 refactors"
tier: archive
kind: plan
status: archived
audiences: [developer, agent]
related:
  - docs/archive/lanes/improve-code/plan-improve-code-3.md
  - docs/archive/lanes/improve-code/improve-code-3-handoff-notes.md
  - docs/dev/testing-and-ci.md
lane: improve-tests
updated: 2026-11-06
---

> **Abstract:** the follow-up to the `IMPROVE-CODE-2/3` lanes — the refactors
> (C2-04 session-owning overloads, C3-04 partial-class splits, the view
> reorganization, the rich-editor module split, the board-translation route
> rename) changed the code shape and left **10 test assertions** pinning the
> pre-refactor shape. The plan: re-point each pin to the current code, one
> cluster per commit, no source changes. **Shipped 2026-11-06** — both suites
> green (`Kumunita.Web.Tests` 1293/0 failed, `Kumunita.Core.Tests` 1860/0
> failed). **Who is it for:** anyone re-running the test suite or auditing
> why a pin changed. **When to read the rest:** to see the per-cluster
> mapping from stale pin to current code.

# Plan — Fix 10 Stale Test Pins (no code regressions)

Root cause: The IMPROVE-CODE-2/3 refactors (C2-04 session-owning overloads, C3-04 partial-class splits, view reorganization, rich-editor module split, route rename) changed the code shape. The test suite has 10 assertions that still pin the pre-refactor shape. All are fixable by updating the test expectations to match the current shipped code.

Cluster A — Rich-editor module split (3 tests)
What changed: The monolithic rich-editor.ts was split into three modules:

rich-editor.ts → main editor (now re-exports isSafeImageSrc as export { isSafeImageSrc };)
rich-editor-shared.ts → shared utils (defines export function isSafeImageSrc)
rich-editor-commands.ts → commands (contains surroundContents)
Failing tests → fix:

Test file	Stale assertion	Fix
RichEditorTests.cs:213	asserts "export function isSafeImageSrc" in compiled rich-editor.js	Change to assert "export { isSafeImageSrc }" (the new re-export form)
InlineEditorTests.cs:107	same assertion	same fix
WysiwygEditorTests.cs:409	asserts "surroundContents" in compiled rich-editor.js	Change to read rich-editor-commands.js (where it now lives)
Cluster B — View path reorganization (3 tests)
What changed: Views were moved:

Views/Projects/_ProjectsTabs.cshtml → Views/Shared/_ProjectsTabs.cshtml
Views/Projects/BoardDetail.cshtml → Views/Boards/BoardDetail.cshtml
kmb-discussion class moved from BoardDetail.cshtml to a shared partial (_TodoComments.cshtml / _GroupPostReplies.cshtml pattern)
Failing tests → fix:

Test file	Stale path/assertion	Fix
IconSpriteTests.cs:80 (ReadViewAt)	reads Projects/_ProjectsTabs.cshtml	Change to Shared/_ProjectsTabs.cshtml
KioskControllerTests.cs:367 (M35_6)	reads Views/Projects/BoardDetail.cshtml	Change to Views/Boards/BoardDetail.cshtml
KioskControllerTests.cs:456 (ADR0285)	reads Views/Projects/BoardDetail.cshtml + asserts kmb-discussion in it	Update path to Views/Boards/BoardDetail.cshtml; for kmb-discussion, either assert it in the view that now carries the discussion section (check _TodoComments.cshtml or the Kiosk-specific partial), or update the assertion to match where the kiosk discussion wiring actually lives now
Cluster C — Layout partial extraction (2 tests)
What changed: _Layout.cshtml was split into partials (IMPROVE-CODE C11):

<footer> → _LayoutFooter.cshtml (carries the 5 hrefs: /about, /terms, /help, /privacy, /conduct)
<script> block → _LayoutScripts.cshtml (carries nav-more-fold.js)
Failing tests → fix:

Test file	Stale read	Fix
NavMoreFoldTests.cs:89	reads _Layout.cshtml for the <script type="module" src="~/js/lib/nav-more-fold.js"> tag	Change to read _LayoutScripts.cshtml (or read both)
StaticPagesSP_U04Tests.cs:351	reads _Layout.cshtml for the 5 footer hrefs	Change ResolveLayout() to read _LayoutFooter.cshtml (or both)
Cluster D — Route snapshot drift (1 test)
What changed: The C2-02 translation-lane collapse renamed the board translation route from POST /projects/boards/{id}/translations to the explicit /translations/update + /translations/remove pair (matching the pattern already used by Projects and Todos).

Failing test → fix:

Test file	Stale pin	Fix
ProjectsRouteTableTests.cs:67	pins "POST /projects/boards/{id}/translations" in the expected set	Remove the old route from the snapshot; the /translations/update and /translations/remove entries (already in the snapshot at lines 68–69) are correct. If the test asserts "no EXTRA routes", the old bare /translations was removed from code — just delete that one line from the expected array.
Cluster E — Overload ambiguity (1 test, Core)
What changed: C2-04 added session-owning overloads to IDocumentFolderService (e.g., CreateAsync(string, string?, string) and CreateAsync(string, string?, string, IDocumentSession)). The test calls t.GetMethod("CreateAsync") which throws AmbiguousMatchException when multiple overloads exist.

Failing test → fix:

Test file	Stale assertion	Fix
DocumentServiceTests.cs:924	t.GetMethod("CreateAsync") (and same for the other 5 methods)	Replace with Assert.True(t.GetMethods().Any(m => m.Name == "CreateAsync")) (or filter by name) — the intent is "the interface exposes a method with this name", not "exactly one overload". Same pattern for ListByParentAsync, MoveDocumentAsync, RenameAsync, MoveAsync, DeleteAsync.
Execution order (one commit per cluster, gate green after each)
Cluster E (1 test, Core) — smallest, no dependencies, unblocks the Core suite.
Cluster A (3 tests) — all in Web.Tests, self-contained.
Cluster B (3 tests) — path corrections + one assertion re-target.
Cluster C (2 tests) — file-read path corrections.
Cluster D (1 test) — route snapshot line removal.
Each commit: update the test → run dotnet build + both test assemblies → confirm green → commit.

No source code changes needed. All 10 are test-pin updates to match the already-shipped refactors.