## U1 — design doc Part 1
- Wrote `docs/design/m3-posts-design.md` Part 1: Context, Scope (in-scope
  list + explicit **M3b deferral** block as M3's M1-style close
  ["Out of scope — M3b deferral (this section is M3's M1-style close)"]),
  Invariants (table, 11 rows), FACES (table, 10 rows), Drift-guard & change
  policy, and an explicit **"## M3 — Closed (recorded)"** placeholder at the
  end (empty by design — U10 / U11 / U12 will append).
- **Invariants pinned in Part 1 (11, by id):**
  - M3-owned: **C-M3·1** (reply inherits the parent's single `Read`
    decision; no separate `Audience` on `PostReply`; no second authz call),
    **C-M3·2** (`Component` is a candidate filter / feed organizer — never an
    access decision; no own `AccessAudit` row), **C-M3·3** (audit shape:
    feed = one aggregate `AccessAudit` row with `targetKind "post"`; detail
    = one decision row; reply-visibility is not its own row; same-transaction
    commit via the `IDocumentSession` overloads).
  - ADR 0006 (frozen, M3 is a caller): **C1** (empty audience denies — with
    the owner-branch exception; `Post.Audience` required non-null), **C2** (
    delegation action-scoped), **C3** (audit always on — Allow *and* Deny),
    **C4** (strong-consistency membership resolution; live documents),
    **C5** (moderator default-OFF — M3 adds **no** `Moderate`-on-post
    branch; `Via = Report` stays dormant; tests assert the **absence** at
    F3/F8), **C6** (one matching pass — `CanAsync` and `CanSeeAsync` share
    `MatchGroups`).
  - Companion ADRs (not re-pinned, but the code must not violate):
    **ADR 0001-B** (author's choice is absolute; the composer writes the
    chosen audience verbatim), **ADR 0003 §Separation of duties** (M3's
    post/reply write surface is author-scoped; delegate posts with
    `Via = Delegation`; no `/admin` change in M3), **ADR 0006-D** (
    dependency direction; `PostService` never reads `GroupMembership` /
    `DelegationGrant` for its own access decisions; `Kumunita.Web` is a
    thin controller), **ADR 0006-E** (compatible-lane ADD = the single M3
    seam `IUserInfoService.GetComponentsAsync(bool enabledOnly)`).
- **FACES pinned in Part 1 (10, F1–F10):** F1 (feed shows only what I may
  read; aggregate hidden count), F2 (non-member sees no trace of a hidden
  post), F3 (empty-audience post denies from **all** incl. moderators),
  F4 (empty-audience post allows the author — owner branch), F5 (group
  member added after the post sees it on the next request), F6 (delegate
  with `Read` in scope inherits owner standing), F7 (delegate without `Read`
  in scope sees nothing), F8 (`Component` is a feed organizer, never a
  gate — no moderator peek), F9 (candidate-filter query emits **no**
  `AccessAudit` row), F10 (reply visible iff parent visible; parent
  denies ⇒ reply *not evaluated*, no own row).
- **Single M3 ADD on a frozen interface (ADR 0006-E lane, mirrors M2's
  `GetProfilesAsync`):**
  `Task<IReadOnlyList<Component>> IUserInfoService.GetComponentsAsync(bool
  enabledOnly)` — the composer's *component picker*, the `/community/{id}`
  *grouping*, and the feed's *candidate filter*. Doc-comment (U2 will pin
  verbatim in §2.1): **returns a *candidate* set; C-M3·2 says never a
  visible set; C-M3·2 says no own `AccessAudit` row; C-M3·3 says the row
  shape is fixed to feed aggregate + detail decision.**
- **Storage registration (M3DocTypes step):**
  `M3DocTypes.Configure(StoreOptions)` — new parallel document surface
  analogous to `M1DocTypes`.
  (the four components are already seeded in M1's `FirstBootSeeder`).
  **No hand-rolled `FeatureSchemaBase` in M3** (the carve-out is for
  operator-written tables like `AdminOverride`); U1 records the veto that
  U2 may flip before §2.2 is pinned.
- **Document register (pinned by Part 1, shapes pinned by Part 2 §2.2):**
  `Post` (ns `Kumunita.Core.Posts`), `PostReply`, `Report` (registered for
  forward compat; **dormant — no surface, no workflow, no tests in M3**;
  the *flow* is M3b's). `PostToAuditableResource.TargetKind = "post"`.
- **M3b deferral list (this section is M3's "out of scope" close, mirroring
  M2 §1):**
  - Report workflow: **file / assign / unlock / resolve** (the C5
    carve-out that M3's "absence" tests assert at F3/F8).
  - The **`Via = Report` read branch** on a post (moderator sees a
    previously-invisible post *through* a filed report).
  - **Moderator surfaces** — the queue, the resolve UI, the "assign to a
    moderator" form. `/admin` (M1's admin surface) is **unchanged** in M3.
  - The post **`Status`** field (hidden / removed) and the M3b removal
    path. M3's post has no `Status` column.
- **Plan-count note (recorded, U1):** the plan § U1 headline says "12
  invariants" but the body's bullet list enumerates **11**. This Part 1
  pins the 11 from the body (the body is authoritative). U2 owns the final
  count in the seam-test list (§2.5) and the acceptance gate (§2.6) — if
  the intended invariant count was 12 (e.g., a C-M3·4 that did not make it
  into U1's body bullets), U2 will surface it in §2.5 / §2.6. **Not a
  pinned-invariant drift** — a plan-documentation slip recorded here for the
  U2 review.
- **No code. No build.** — design-only unit, per the plan.
- **U2, your job:** append `## Seams & contracts (Part 2, written by U2)`
  at the end of `docs/design/m3-posts-design.md` (above the
  "## M3 — Closed (recorded)" placeholder — keep that section last):
  §2.1 frozen seam list (exact C# — verbatim quotes of
  `IAuthorizationService`, `IAuditableResource`, `Audience` / `AudienceGrant`
  / `AudienceMode`, `Decision` / `VisibleSet`, the M1 frozen `IUserInfoService`
  surface **plus** the single M3 ADD pinned above), §2.2 new M3-owned Core
  types (exact C# for `Post`, `PostReply`, `Report`,
  `PostToAuditableResource`, `PostService` ctor + public methods,
  `M3DocTypes.Configure` signature)
  (unauth ⇒ empty set; verified ⇒ `GetComponentsAsync(true)` candidate set;
  unverified ⇒ own component only or empty (U2 decision); moderator ⇒ same
  as verified; missing component ⇒ empty), §2.4 reply-inherits rule (C-M3
  ·1) — 4-shape table (parent Allow ⇒ replies rendered; parent Deny ⇒
  replies not evaluated/no own row; empty-audience parent ⇒ owner branch
  only; explicit `All`+empty ⇒ deny), §2.5 **seam-test names** (pinned test
  method names in `Kumunita.Core.Tests/PostServiceTests.cs`, each carrying
  its FACES row and invariant), §2.6 **acceptance gate** (three-test shape:
  closed-loop / handoff / part-vs-whole, named), §2.7 **drift-guard**
  (the test list, the ADD signature, the 4 reply-inherits shapes, the 5
  candidate-filter states, FACES count, and the invariant ids are frozen
  pins).
- **Drift-guard from Part 1 (repeats into U2's §2.7):** the invariant
  *numbers* (C-M3·1/2/3, ADR 0006 C1..C6, ADR 0001-B, ADR 0004 §B.1) are
  stable for the rest of M3; rename / renumber is a breaking change.
  Adding a new FACES row (F11+) is only by the unit that ships the outcome
  it pins, in the same commit as the feature; FACES count is a handoff
  field (U1 → U2, and forward).
