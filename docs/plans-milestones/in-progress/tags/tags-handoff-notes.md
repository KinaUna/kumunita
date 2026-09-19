# Tags (`TG`) — rolling handoff notes

One section per unit, appended (never rewritten). Each unit writes exactly
one short section before it exits; the next unit reads only that section +
its own entry-read list from the register.

---

## U0 — Plan register authored (2026-09-19)

- ADR 0044 accepted; ADR index rows 0042/0043/0045/0046 backfilled.
- 13-unit register written (`plan-tags.md`), 769 lines, 9 invariants
  (C-TG·1–C-TG·9), 12 FACES (F1–F12), 24 pinned test names.
- U1 is the first unit to execute: design doc Part 1 (docs-only, no build).

## U1 — Design doc Part 1 (2026-09-19)

- `docs/design/tags-design.md` authored: **177 lines**, exactly the four
  Part 1 sections (`## Context`, `## Scope`, `## Invariants (pinned for the
  lane)`, `## FACES (pinned, 12)`); no C# blocks, no build steps.
- 12 invariant IDs pinned: C-TG·1 … C-TG·9 (TG-owned) + C1, C3 (ADR 0006)
  + ADR 0004 §B.1 — every one cites a D# from ADR 0044.
- 12 FACE IDs pinned: F1 … F12, each citing ≥ 1 invariant (C-TG·N / C1 / C3 /
  ADR 0004 §B.1).
- ADR 0044 D# citations used: D1, D2, D3, D4, D5, D6, D7, D8 (all eight) —
  D8 anchors the Out-of-scope list verbatim.
- No deviation from the register's U1 spec (12 invariants + 12 FACES as
  enumerated in the register body). U2 may proceed to Part 2.

## U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) (2026-09-19)

- `docs/design/tags-design.md` now **571 lines**: Part 1 (177) + Part 2 §2.0–§2.6
  appended (one `## Seams & contracts (Part 2, written by U2)` heading; Part 1
  untouched).
- 24 pinned test names (§2.4) target `tests/Kumunita.Core.Tests/TagServiceTests.cs`,
  verbatim from the register's U2 list — no rename/reorder/add/drop.
- `TagService` surface pinned at **11 members** (7 lane methods + 3 standing
  probes + the `TagItem` record), §2.1; `PostService` / `PageService` gain **no
  new public methods** (the no-new-methods pin is in §2.6).
- Acceptance gate (§2.5, recorded by U12): **closed-loop / handoff /
  part-vs-whole** — the three M2/M3-style tests per the register.
- D# lean per sub-section: §2.1 = D4/D5/D7/D8; §2.2 = D1/D2/D3/D6/D8; §2.3 =
  D4/D5/D6 (+ D7 via C-TG·8/9); §2.4 = D1–D8 all (per-row anchors listed); §2.5
  = D3/D4/D5/D7; §2.6 = D1/D4/D5 (+ ADR 0004 §B.1).
- Two register-idiom notes recorded in the doc (not deviations from its pins):
  (a) the register's `IReadOnlySet<Role>` is spelled that way in the §2.1
  signatures, with a §2.0 note that the repo idiom is `IReadOnlySet<string>`
  (`Kumunita.Core.Identity.Roles` constants) — U5 lands against the repo idiom;
  (b) the register's `string[]` `TagIds` shorthand is pinned in the repo idiom
  (`IReadOnlyList<string>`, the `Post.ImageIds` shape, ADR 0025) with a §2.2
  note — the additive/default-empty pin is what freezes. U3 may proceed.

## U3 — Tag/TagTranslation + TagDocTypes + boot (2026-09-19)

- **(a) `TagDocTypes` line count:** `src/Kumunita.Core/TagDocTypes.cs` — 2 `Schema.For` calls (`Tag`, `TagTranslation`) + 1 named `(TagId, LanguageCode)` unique index (`tg_tr_uidx_tag_lang`, the `pg_tr_uidx_page_lang` / `ann_tr_uidx_ann_lang` family idiom; the auto-derived name is ~44 chars, under the 64-char NAMEDATALEN limit, so the name is convention, not necessity).
- **(b) Boot-path line added:** 1 line in 1 file — `src/Kumunita.Web/Program.cs` **line 98** (`TagDocTypes.Configure(opts);`, comment block lines 94–97), immediately after the `PageDocTypes.Configure(opts);` call (line 92), inside the `AddMarten(opts => { … })` lambda. `SchemaBootstrap.cs` was **not** modified: it carries no doc-type registration cluster — it applies whatever the host registered via `ApplyAllConfiguredChangesToDatabaseAsync()` (the M3 handoff note pins the same "1 line in 1 file" shape).
- **(c) Non-blank invariants (C-TG·4 pin):** `Tag` — `Slug`, `Name`, `LanguageCode`, `CreatedBy` (all `string`, default `string.Empty`, non-blank per the §2.2 pin); `TagTranslation` — `TagId`, `LanguageCode`, `Name`, `AuthorId` (same shape). Both sealed, 6 fields each (Id + 5), no `Audience` field (D1/D5 absence-pin frozen by §2.6).
- **(d) Compile warnings on the new POCOs:** **none** — `dotnet build Kumunita.slnx -c Debug` → 0 errors, 0 warnings on the new files (the 3 `xUnit2029` warnings in the first build pass are pre-existing in `tests/Kumunita.Core.Tests/PageServiceTests.cs`, untouched).
- **(e) New files:** `src/Kumunita.Core/Tags/Tag.cs`, `src/Kumunita.Core/Tags/TagTranslation.cs` (ns `Kumunita.Core.Tags`), `src/Kumunita.Core/TagDocTypes.cs` (ns `Kumunita.Core`, root-level file — mirrors `M3DocTypes.cs` / `PageDocTypes.cs` exactly; the draft under `Tags/` would have made `Tags.Tag` ill-formed since `Kumunita.Core.Tags` is both the namespace and a type name in `Kumunita.Core` scope). No tests added (U4's F5/F11/F12 are the first TG tests). No changes to `Post.cs` / `Page.cs` (U4), `TagService` (U5/U6), or the design doc / register. U4 may proceed.

## U4 — TagIds additive + System refusal (2026-09-19)

- **(a) Two POCO fields added:** `src/Kumunita.Core/Posts/Post.cs` **line 172** and `src/Kumunita.Core/Pages/Page.cs` **line 235** — `public IReadOnlyList<string> TagIds { get; set; } = [];` (the `ImageIds` / `AttachmentIds` repo idiom, not the register's `string[]` shorthand — the U2 note (b) / §2.2 shape-note pin; the 8th additive `Post` field).
- **(b) `System`-page refusal branch:** `src/Kumunita.Core/Pages/PageService.cs` — **create** at **lines 519–522** (after `CheckCreateStanding`, `page.Kind == PageKind.System && page.TagIds is { Count: > 0 }` → `ArgumentException(nameof(page.TagIds))`); **update** at **lines 643–646** (after `CheckEditStanding`, `existing.Kind == PageKind.System && updated.TagIds is { Count: > 0 }` → `ArgumentException(nameof(updated.TagIds))`). Picked `ArgumentException` over `UnauthorizedAccessException` per §2.3 row 4 — a shape violation, not a standing denial; consistent across both lanes. No new public method (the §2.6 no-new-methods pin).
- **(c) F5 / F11 / F12 test names (verbatim from §2.4):** `F5_SystemPageRefusesNonEmptyTagIds`, `F11_PreExistingPostTagIdsReadBackEmptyAfterReboot`, `F12_FreshInstanceHasZeroTags` — all in `tests/Kumunita.Core.Tests/TagServiceTests.cs` (new file, the first TG tests).
- **(d) `TagIds` default value:** empty `IReadOnlyList<string>` (`= [];` collection expression) on both POCOs — the ADR 0004 §B.1 additive, default-empty, no-reseed pin (F11).
- **(e) `TagDocTypes.Configure` added to the test `BootStoreAsync`:** `tests/Kumunita.Core.Tests/TagServiceTests.cs` **line 207** (inside the `BootStore(connString)` helper, immediately after the `PageDocTypes.Configure(opts);` call — per the U3 handoff note, the test boot path is the one place the `Tags` doc types are registered here before `TagService` lands).

Exit: `dotnet build Kumunita.slnx -c Debug` green (0 errors; the 3 `xUnit2029` warnings are pre-existing in `PageServiceTests.cs`); `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` green — **Total: 586, Errors: 0, Failed: 0**; the three tests confirmed discovered via `-list methods`; a `PageKind.User` page accepts + round-trips a non-empty `TagIds` set, a `PageKind.System` page refuses one on create **and** update (stored value stays empty), and a pre-existing post's `TagIds` reads back empty after a warm re-open (F11). `PostService.cs` was **not** touched (the register's U4 `PageService` deliverable is only the refusal; the design doc's "threads through `PostService`'s create/update paths" wording is a no-op for U4 since `PostService` has no existing `TagIds`-carrying create/update seam — the *attach* is U5's `TagService`, the *composer* field is U8 — recorded here, not expanded). U5 may proceed.
