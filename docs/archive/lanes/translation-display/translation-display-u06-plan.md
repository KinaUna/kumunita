# TD U06 — C# data-shape tests + acceptance gate + doc sync

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-translation-display.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **This is the final TD unit** — it also appends the lane-closing handoff
> section.

## Goal

Pin the U02 data ADDs with controller/VM-level tests, record the FACES
acceptance gate, and sync the durable docs (the ADR index, ARCHITECTURE.md,
README roadmap note) so the lane is honest. **Touch no view markup, no TS.**

## Context you need (read these first, in this order)

1. `docs/design/translation-display-design.md` §**Pinned contract** →
   `### pinned tests (exact names)` (the four tests to write, verbatim) + the
   three VM ADDs + §**FACES** TD1–TD8 (the acceptance to record).
2. `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` — the **harness to
   mirror**: it constructs a real `PostService` with fakes
   (`new PostService(userInfo, authz, Substitute.For<IDocumentStore>())` —
   see also `ContentImageUploadTests.cs` line ~279) and drives a detail
   action, asserting on the returned view model. That is the closest
   existing pattern for the TD tests.
3. The two detail controllers' **primary-constructor signatures** (read the
   first lines of each): `PostsController(PostService posts,
   ModerationService moderation, IUserInfoService userInfo,
   ILocalizationService localization, IDocumentStore store)` and
   `GroupsController(IUserInfoService userInfo, PostService posts,
   ILocalizationService localization, IDocumentStore store)`. Instantiate
   with fakes the same way `ContentImageServingTests` does — a fake
   `PostService`/`IUserInfoService`/`ILocalizationService` and
   `Substitute.For<IDocumentStore>()`. Also read `PostsController.cs` §`Detail`
   and `GroupsController.cs` §`GroupPostDetail` to confirm the exact
   `OriginalLanguageCode` **source** from U02 (`result.Post.LanguageCode` /
   `reply.LanguageCode`).
4. `tests/Kumunita.Web.Tests/GroupsDetailViewModelTests.cs` — the
   **group-lane** VM-shape test to mirror for `GroupPostDetailViewModel`
   (same fake pattern; assert `OriginalLanguageCode`).
5. The durable docs to sync: `docs/adr/README.md` (the ADR index table — append
   0027 after 0026), `docs/ARCHITECTURE.md` (the persistence / detail-VM area
   — note the additive VM fields, **no** new document), and `README.md`
   §**Roadmap** (around lines 157–170 — the `ML` / `ML-UI` / `RC` named-lane
   entries to mirror).

## Deliverables (1 new test file + 3 doc edits)

**`tests/Kumunita.Web.Tests/TranslationDisplayTests.cs`** (new) — the **four
pinned** tests (exact names from the design doc), each driving the detail
action through the mirrored fake harness and asserting the U02 ADD:
1. `PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn` — a post authored
   in, e.g., `en` → `PostDetailViewModel.OriginalLanguageCode == "en"` (the
   `Post.LanguageCode` source, U02).
2. `Reply_OriginalLanguageCode_EqualsReplyAuthoredIn` — a reply authored in,
   e.g., `pl` → that `ReplyItem.OriginalLanguageCode == "pl"` (the
   `PostReply.LanguageCode` source, U02).
3. `GroupPostDetail_OriginalLanguageCode_EqualsPostAuthoredIn` — the group
   lane → `GroupPostDetailViewModel.OriginalLanguageCode` equals the post's
   authored-in language (the `GroupPostDetail` action, U02).
4. `PostDetail_OriginalNotAmongAddedTranslationCodes` — a post authored in one
   language **with** an added translation in a *different* language →
   `OriginalLanguageCode` is **not** among the `PostTranslations`'
   `LanguageCode`s (the original is distinct from the added rows — TD·5, and
   the premise of the TD·4 exclusion).
Mirror the existing controller-test harness (fakes for `IUserInfoService` /
`IStore` / the post service). **No new Core test** (TD·7 — no Core change).

**`docs/adr/README.md`** — append the **0027** row after 0026:
`| 0027 | Post/reply translation display + click-to-swap (authored-in as the first variant) | Accepted |`.

**`docs/ARCHITECTURE.md`** — one short note in the persistence / detail-VM
area: the TD lane adds **no** document and **no** migration (ADR 0004 §B.1 is
not engaged); the authored-in code is surfaced **additively** on
`PostDetailViewModel`, `GroupPostDetailViewModel`, and `ReplyItem` from the
existing ADR 0018 field, and the swap is a server-rendered / client-toggled
display concern (reference ADR 0027).

**`README.md`** §**Roadmap** — add the **TD** lane as a **named** (non-M-letter)
entry under the current in-progress work, mirroring how `ML` / `RC` are
listed (e.g. "**Translation display** (`TD`, ADR 0027) — the authored-in
language is the first, default-visible variant on the post/reply detail; the
chip row is a click-to-swap selector; the add-lane no longer offers the
authored-in language."). **Do not** move M4/M5/M6.

## Invariants honored

**TD·1–TD·8** — the tests pin the data ADDs (TD·1 / TD·6 / TD·7), the doc
sync records the lane honestly, and the acceptance gate (below) covers the
visual FACES TD1–TD8.

## Acceptance gate (record in the handoff note)

Observe (author, per FACES) and record a verdict for **TD1–TD8**:
- **TD1** community post authored in English, no added translation → English
  chip first, original title+body shown, **no** "None yet".
- **TD2** that post's "Add a …" lane does **not** offer English.
- **TD3** English post + added French → chips English, French; clicking
  French swaps the title+body to the French row; clicking English swaps back.
- **TD4** a Polish reply (no added translation) → Polish chip first, original
  body shown, no "none yet".
- **TD5** Polish reply + added German → German swaps the body; Polish swaps
  back.
- **TD6** the **group** post/reply detail behaves identically.
- **TD7** a soft-deleted post/reply shows the placeholder and **no** swap.
- **TD8** with JS **disabled**, the original is visible and the added
  variants are present as static text.

## Exit

`dotnet build` green **and** both test assemblies run green **via the repo's
xunit.v3 in-process runner** (per `AGENTS.md` "Running the tests" — `dotnet
build Kumunita.slnx -c Debug`, then `dotnet exec tests\Kumunita.Web.Tests\
bin\Debug\net10.0\Kumunita.Web.Tests.dll` and `dotnet exec tests\Kumunita.
Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`; **not** `dotnet test` /
Test Explorer, which hit the discovery bug). The four pinned tests pass. The
ADR index, ARCHITECTURE.md, and README roadmap reflect the lane.

**Lane close:** append the final handoff section to `docs/plans-milestones/
done/translation-display-handoff-notes.md` starting `## U06 — tests + doc sync`,
recording (a) the four test names + pass count, (b) the TD1–TD8 acceptance
verdicts, (c) the three doc edits (ADR index row 0027, ARCHITECTURE.md note,
README roadmap TD entry), and (d) the closing line
**`Lane complete — TD (ADR 0027) is closed.`** so the next reader sees the
lane is done.
