# M23 — Extended user profiles (rolling handoff notes)

> **The cross-unit memory.** Every unit reads the `## U##` sections before it
> and appends its own after. A unit does not re-derive what an earlier unit
> already settled (a D# amendment, the key set, a seam shape, the `Tag.Id`
> reference convention, the two-gate posture, the aggregate-row shape). Created
> by U00 (the docs + ADR sign-off gate).

## U00

- **Delivered:** the design doc
  `docs/design/m23-extended-profiles-design.md` (all 11 sections, 0–10, incl.
  §1.a) + the ADR `docs/adr/0123-extended-profiles.md` (Status: **Accepted** —
  `**Done** (M23)` is tagged by U06 at close) + the one ADR 0123 index row in
  `docs/adr/README.md` (after the 0122 row). The register's [PROPOSED] set
  (D1–D8, C-M23·1…6, FACES F1–F6, the closed 12-key `kw-l` set, GATE-1…5, the
  §drift-guard) is **locked** in the design doc + ADR.
- **D#s locked / amended:** **D1–D8 locked verbatim** (no D# text rewritten).
  **One clarifying amendment — §1.a C1:** the **D4 gate mechanism** is locked at
  the **register's** **single `CanSeeAsync` pass** over
  `candidates.Select(p => new ProfileToAuditableResource(p))`. The unit plan
  `m23-u00.md` §2.4 restated it as "gate each survivor (one `CanAsync` per
  survivor)"; the **register wins** (tier-1 authority). Locked at the
  `CanSeeAsync` bulk pass because (1) it is the frozen M3/M21 feed idiom and
  returns `VisibleSet` (`Visible` ids + `HiddenCount`) → maps directly to the
  C-M23·4 aggregate-row shape (`targetKind "directory"`,
  `visibleCount`/`hiddenCount` set, `targetId = null`); (2) a per-survivor
  `CanAsync` loop would emit **one decision row per profile** (the "multiple
  rows" drift the §drift-guard forbids); (3) it resolves the **owner branch
  internally** (F3) without a special case. **U03 implements the single
  `CanSeeAsync` pass; the unit plan's per-survivor wording is superseded.**
- **FACES:** F1 (author sets bio+tags), F2 (detail bio+tags behind
  `Visibility`, contact block behind the unchanged `ContactVisibility` — two
  independent gates), F3 (owner always sees own), F4 (fresh profile =
  owner-only, the lean default), F5 (find-people over visible profiles, one
  aggregate row), F6 (a profile tag is a label, never a gate).
- **§gate:** GATE-1 (bio+tags gated by `Visibility`, owner sees own, contact
  block independent), GATE-2 (find-people visible-only + one aggregate row + no
  leak), GATE-3 (single write lane, verbatim bio + tag create-or-get + no
  profile-write audit row), GATE-4 (zero new authorization surface), GATE-5
  (bio is rich content via `MarkdownRenderer.RenderHtml`, null-safe).
- **Key mechanical pins locked (codebase verified):** the two `Profile` fields
  (`Bio` `string?`, `TagIds` `string[] = []`) are **additive POCO fields** on
  the M1-registered `Profile` doc (ADR 0004 §B.1 — **no new DocTypes surface, no
  migration**); `ProfileUpdate` gains `Bio = null` + `TagIds = null` **after**
  the frozen `Address` default (existing codebase = five fields + `Address`);
  `ProfileToAuditableResource` is `TargetKind "directory"`, `Audience =>
  Profile.Visibility`, `OwnerId => SubjectId`; `DirectoryService` composes
  `IUserInfoService` + `IAuthorizationService` and filters
  `GetProfilesAsync(false)` by `!p.Blocked`; `CanSeeAsync(actorId, AccessAction,
  IEnumerable<IAuditableResource>)` → `VisibleSet(Visible, HiddenCount)`; the
  `Tag` POCO is `Slug`/`Name`/`LanguageCode`/`CreatedBy` (the `TagService.
  AttachToPostAsync` create-or-get = one `tag.create` row per **newly created**
  tag, a present `Slug` reused with no row); the bio renders via the existing
  `MarkdownRenderer.RenderHtml(string?)`.
- **`TargetKind` pin:** `"directory"` (reused — **no new `TargetKind`**; the
  aggregate audit discriminator for bio+tags + find-people is the same existing
  value the directory already uses).
- **`kw-l` authorship split (locked §7):** **U04** authors
  `profile.edit.bio` / `profile.edit.tags` / `profile.edit.tags.placeholder` /
  `profile.detail.bio` / `profile.detail.tags` / `profile.detail.tags.empty` /
  `profile.flash.saved`; **U05** authors `nav.people` / `profile.find.title` /
  `profile.find.by_tag` / `profile.find.by_bio` / `profile.find.results` /
  `profile.find.empty`. All four languages (en/de/fr/da); no unit consumes a key
  it has not authored.
- **U01 next** — read the design doc §4 (the two `Profile` fields + the
  `ProfileUpdate` extension, exact C#) + §2 (C-M23·3/6) + the ADR 0123 §Decision
  (D1/D3). U01 owns the **two additive `Profile` fields** + the **`ProfileUpdate`
  extension** (the data model only — no write-lane logic, no find-people); it
  exits on `Kumunita.Core.Tests`.
