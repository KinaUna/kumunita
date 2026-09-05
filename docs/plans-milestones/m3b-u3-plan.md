# M3b — U3 · `Post.Status` + `HidePostAsync` / `RemovePostAsync` — unit plan

> Sealed unit **U3** of M3b (see `docs/plans-milestones/plan-m3b-moderation.md`).
> Self-contained: this file + the entry-read list at the top is the whole
> context U3 needs. Read the entry list first, then execute.

## Understanding

Add the **additive** `Post.Status` field (the single M3b ADD on the M3
`Post` POCO — ADR 0004 §B.1, no migration, no re-seed) and the two
`Moderate`-gated write lanes `PostService.HidePostAsync` / `RemovePostAsync`
(invariant **C-M3b·3**, FACES F3 / F4). This is the **first** code path in
M3b that passes `AccessAction.Moderate` into `IAuthorizationService.CanAsync`
(the plan's Assumptions pin: the reserved M1 vocabulary goes live under U3's
two lanes).

## Assumptions

- The design doc `docs/design/m3b-moderation.md` §2.2.1 / §2.2.2 is the
  authoritative C# pin. If a shape here diverges, **the doc wins** (drift-guard
  §2.7 — update the doc in the same commit and append a note to
  `docs/plans-milestones/m3b-handoff-notes.md`).
- No new `AccessAction` id is added; `AccessAction.Moderate` (M1, id
  `"moderate"`) already exists (U1 handoff confirmed).
- `PostStatus` is a **non-nullable** enum property on `Post`, defaulting to
  `Active`. `Null`-check idioms are not needed (doc §2.2.1).
- `HidePostAsync` / `RemovePostAsync` are `Task` (void return), take
  `(string postId, string actorId, IDocumentSession session)` — the caller's
  in-flight session (C3 same-transaction; ADR 0006-E "compatible lane"),
  mirroring `CreatePostAsync` / `CreateReplyAsync` style.
- The `IDocumentSession` overload of
  `IAuthorizationService.CanAsync(actorId, action, target, session)`
  writes the `AccessAudit` row **into** the caller's transaction — so
  `SaveChangesAsync` flushes the audit row *and* the `Status` write
  atomically (C3; ADR 0006-C: audit always on, Allow or Deny).
- On `Deny`, **no `Status` write, no partial state**. The audit row (Deny)
  still commits — that is the "no silent, unaudited access" guarantee.
- On `Allow`, the `Status` field is set (`Hidden` or `Removed`) and
  `Modified` is stamped. One `SaveChangesAsync`.
- A missing post in the session throws `KeyNotFoundException` (mirrors the
  fail-closed detail shape in `GetPostAsync`; the write lane has no
  "Deny" path that returns the missing-post — this is a moderator write,
  not a read).

## Approach

**Two-file change, additive only.**

1. `src/Kumunita.Core/Posts/Post.cs` —
   - Add `public enum PostStatus { Active, Hidden, Removed }` in the same
     namespace (file-scoped, per M3's existing `Post.cs` / `PostReply.cs`
     shape).
   - Add `public PostStatus Status { get; set; } = PostStatus.Active;` to
     `Post`, with a `<summary>` doc-comment naming it as the M3b single
     ADD (ADR 0004 §B.1 additive).
   - Update `Post`'s class doc-comment to mention `Status` as the M3b ADD
     (keep the M3 invariants intact).
   - **No other field changes** (rule 5: never reshape beyond the additive
     `Status`).

2. `src/Kumunita.Core/Posts/PostService.cs` —
   - Add `HidePostAsync(string postId, string actorId, IDocumentSession session)`
     and `RemovePostAsync(string postId, string actorId, IDocumentSession session)`
     in the **existing** class.
   - Both: load the `Post` from the caller's session — `KeyNotFoundException`
     if missing.
   - Both: call `_authz.CanAsync(actorId, AccessAction.Moderate,
     new PostToAuditableResource(post), session)` **before** writing
     (C3 / ADR 0006-C).
   - Both: on `decision.Allowed`, set `post.Status` to
     `PostStatus.Hidden` / `PostStatus.Removed` respectively, stamp
     `post.Modified`, `session.Store(post)`.
   - Both: `await session.SaveChangesAsync()` — one flush, both commits
     (the audit row + the Status write commit atomically — C3).
   - Style matches the existing `CreatePostAsync` / `CreateReplyAsync`
     (null/empty guards at the top, `ConfigureAwait(false)` at each
     await).

**No new seams on frozen interfaces** (unit-series rule 4). The only
`IAuthorizationService` call is the **existing frozen** `CanAsync` overload
(ADR 0006-D's single decision path).

## Key files

- `docs/design/m3b-moderation.md` §2.2.1 / §2.2.2 — the authoritative C#
  pin (signatures, `PostStatus` literal set, C3/ADR 0006-C notes).
- `src/Kumunita.Core/Posts/Post.cs` — additive change (enum + `Status`).
- `src/Kumunita.Core/Posts/PostService.cs` — additive change (two methods).
- `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — reuse **as-is**
  (M3, U5); not modified.
- `src/Kumunita.Core/Authorization/IAuthorizationService.cs` — frozen
  interface; `CanAsync(actor, action, target, session)` is the target call.
  Read-only here.

## Risks & open questions

- **`KeyNotFoundException` vs. silent no-op** on a missing post in the
  session — chosen: throw (mirrors `GetPostAsync`'s fail-closed shape and
  the M3 convention of surfacing a genuine missing-resource rather than a
  Deny-equivalent). The `Deny` path is the only "no-write" outcome the
  doc pins; "missing post" is a distinct, caller-visible error.
- **`Modified` stamp** on hide/remove — not pinned by the doc, but
  consistent with the existing POCO semantics (`PostService.CreatePostAsync`
  sets `Created`, `Post`'s doc-comment treats `Modified` as the last-write
  marker). Stamp it — low risk, matches convention.
- **Audit `Via` tag** (doc §2.3 pin #3) — the tag is written by the frozen
  `IAuthorizationService`'s decision row, **not** by the post lane (the
  lane passes the *action* = `Moderate`; the audit row's `Via` is
  determined by M1's decision algorithm). U3 does not name a `Via`
  literal here — that's M1's surface, not M3b's.

## Steps

1. Read entry-read list (already done): `m3b-moderation.md` §2.2.1/§2.2.2,
   `Post.cs`, `PostService.cs`, `IAuthorizationService.cs`,
   `PostToAuditableResource.cs`, `Decision.cs`.
2. **Create** the `PostStatus` enum + `Post.Status` property in
   `src/Kumunita.Core/Posts/Post.cs` (and update `Post`'s class
   doc-comment).
3. **Add** `HidePostAsync` + `RemovePostAsync` to `PostService`
   (verbatim signatures from §2.2.2).
4. **Verify** build green — `Kumunita.Core` (both Debug and Release).
5. **Append** the `## U3` section to
   `docs/plans-milestones/m3b-handoff-notes.md` (verbatim method
   signatures, confirmation no existing seam-test name broke).
6. **Close** the unit.
