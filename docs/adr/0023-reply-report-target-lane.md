# ADR 0023 — Reply as a report target (extending the M3b report lane)

Status: Accepted
Date: 2026-09-17

## Context

M3b's report lane is **post-targeted**: a resident files a report against a
`Post`, a GlobalAdmin sees it in the `/moderation` queue, and the
assign / unlock / resolve workflow runs against the post's
`ComponentId` scope (M3b `C-M3b·1` / `C-M3b·4`, the four
`Status`-literal pins in the design doc). A **reply** — the
`PostReply` POCO, M3 — has no own audience and no own audit row
(C-M3·1 "reply-inherits"): its visibility is the parent post's single
`Read` decision. That is right for *reading* (a reply is never
independently authorized), but it leaves a **real** gap for
*reporting*: a neighbor who sees a hostile or abusive reply under a
post they can otherwise see has no way to flag **that reply** — they
can only flag the post as a whole, which mis-describes the target and
weakens the signal a moderator is acting on.

The request is narrow: "a reply could also be problematic, so it
should be possible to report a reply too." The fix is not a new
bounded context, not a new moderation lane, and not a new `Status`
literal — it is a **target discriminator** on the existing
`Report` POCO plus a parallel write seam and the two read
surfaces (queue + resolve view) that need to render which target a
row points at.

## Decision

- **One additive field, not a new document.**
  `Report` (M3-registered, `M3DocTypes`) gains
  `public string? ReplyId { get; set; }` — a **null = post-target**
  / **non-null = reply-target** discriminator. `PostId` stays
  populated in both cases: for a reply-targeted row it carries the
  reply's parent post id (the reply's own `PostReply.PostId`), so the
  queue / resolve read path stays on the single post key and
  `C-M3·1`'s "the reply has no own audience; seeing the post is what
  makes the reply visible" invariant is preserved verbatim. This is
  an ADR 0004 §B.1 additive change: Marten delta-detects the new
  column on boot (`ApplyAllConfiguredChangesToDatabaseAsync`),
  idempotent across re-seed, and pre-existing rows keep
  `ReplyId = null` with no migration or back-fill.

- **One parallel write seam, mirroring the post-report lane exactly.**
  `ModerationService.FileReplyReportAsync(replyId, actorId, reason,
  session)` — same shape as `FileReportAsync`:
  - A **resident-facing intake** action, **no `IAuthorizationService`
    call** (it is not an access decision; the C-M3b·1 pin holds
    verbatim).
  - Loads the `PostReply` from the caller's session; a missing reply
    is a `KeyNotFoundException` (no partial write).
  - Loads the parent `Post` (the reply's `PostId`) to derive
    `Report.PostId` + `Report.ComponentId` (the same
    `ComponentId` carry the post-report lane does).
  - Writes one `Report` row (`Status = "filed"`, the §2.3 item-2
    literal pin; `ReplyId = replyId`, `PostId = parent.Id`) and one
    `AccessAudit` row in the **same** session, one
    `SaveChangesAsync` (C3 / ADR 0006-C — same-transaction, no
    partial write).
  - The audit row carries a **distinct** `Action` + `TargetKind`
    from the post lane so the log can tell the two apart:
    `Action = "report.reply.file"`, `TargetKind = "reply"`,
    `TargetId = replyId` — vs the post lane's
    `Action = "report.file"`, `TargetKind = "post"`,
    `TargetId = postId`. The `Via` tag is the same pinned filing
    tag `AccessVia.Admin` (two negatives: NOT
    `AccessVia.Report` — reserved for the read branch,
    `C-M3b·2`; NOT `AccessVia.Owner` — the C1 owner-branch).
    `Outcome = AccessOutcome.Allow` (intake lane; no Deny path
    without a `CanAsync` call).

- **Web layer: the gate is `C-M3·1` "reply-inherits," not a new
  authorization surface.** The new action
  `POST /posts/{id}/replies/{replyId}/report`
  (`PostsController.ReportReply`) is the exact Web shape of the
  existing `POST /posts/{id}/report` lane — the same
  `GetPostAsync(id, actor).Post is not null` fail-closed gate
  (C-M3·1: the resident must be able to see the **parent post**; a
  reply has no own audience, so seeing the post is what makes its
  reply reportable), the same `LightweightSession()` + one
  `SaveChangesAsync` session shape (C3), the same `TempData["info"]`
  + `Redirect($"/posts/{id}")` return. One defensive add on top:
  the Web layer verifies the reply's `PostId` matches the route's
  `{id}` before delegating (a reply belonging to a different post is
  `NotFound`, not filed under the wrong parent).

- **UI surface: one per-reply "Report this reply" disclosure in the
  post detail view.** `Views/Posts/Detail.cshtml` adds, inside each
  reply `<li class="list-group-item">` (after the ADR 0016
  author-only edit `<details>` block), a native `<details>`
  disclosure (the ADR 0016 per-reply-edit precedent — one per reply,
  no JS dependency, no id collision) containing a `reason` textarea +
  a submit button, posting to
  `POST /posts/{postId}/replies/{r.Id}/report` with an
  `@Html.AntiForgeryToken()`. The report form is shown on **every**
  reply (not gated on `r.IsAuthor`) — any resident who can see the
  parent post can report any reply under it, including their own;
  the ADR 0016 author-only edit `<details>` is a separate concern
  (it stays gated on `r.IsAuthor`). The button / labels reuse the
  existing `posts.report_*` keys plus one new
  `posts.reply_report_button` key ("Report this reply") — the
  closed `KnownTranslationKeys` registry (ADR 0015 D2) is the single
  source of truth; the seeder materializes the `en` floor row at
  first boot and the `kw-l` provider's floor covers `en`, so no new
  provider seam is needed.

- **Queue + resolve render the reply target when present.**
  - `ModerationQueueViewModel.ReportRow` gains two trailing
    optional fields: `string? ReplyId` and
    `string? ReplyAuthorName`. `ModerationController.Index` loads
    the `PostReply` + the reply author's `GetProfileAsync` when
    `r.ReplyId` is non-null (a read, not a decision — the UGC name
    renders as-is, never translated — M·3). `Views/Moderation/Index.cshtml`
    shows a "reply by X" line under the post title only when
    `r.ReplyId` is non-null (a new `moderation.queue_reply_by` key).
  - `ModerationResolveViewModel` gains `ReplyId` / `ReplyBody` /
    `ReplyAuthorName` (all optional, `null` when the report is
    post-targeted). `ModerationController.Resolve` loads them the
    same way. `Views/Moderation/Resolve.cshtml` shows a "Reply (target
    of this report)" blockquote after the post-body blockquote, only
    when `Model.ReplyId` is non-null and `Model.ReplyBody` is
    non-empty (new keys `moderation.resolve_reply_label` +
    `moderation.resolve_reply_by`).

- **The rest of the M3b workflow is untouched.** `AssignReportAsync`
  / `UnlockAsync` / `ResolveReportAsync` operate on the
  `Report`'s `ComponentId` + `Status` — both are populated identically
  whether the row is post- or reply-targeted, so a reply-targeted
  report flows through assign / unlock / resolve **unchanged**. The
  `C-M3b·4` SoD gate (GlobalAdmin-only write lanes, standing-moderator
  scoped-read on the read lanes) holds verbatim: a reply-targeted
  report is scoped to the same `ComponentId` as a post-targeted one,
  so the same `GetAssignmentsAsync(actor)` +
  `Component.ModeratorAccess == true` scope read covers both.

- **Tests.** Three new seam tests in
  `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`, mirroring
  the existing `FileReportAsync_Filing_*` shape (same
  `PostgresFixture`, same `BootStoreAsync`, same `Services(store)`
  trio, same `Plant` / `RunInSession` helpers):
  - `FileReplyReportAsync_Filing_WritesReportWithReplyId` — the
    reply-targeted row carries `ReplyId = <replyId>`,
    `PostId = <parent id>` (C-M3·1), `ComponentId = <parent's>`,
    `Status = "filed"` (the §2.3 item-2 literal), `Reason` as
    supplied.
  - `FileReplyReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner` —
    the filing audit row carries the same pinned
    `AccessVia.Admin` literal (two negatives: not
    `AccessVia.Report`, not `AccessVia.Owner`), `Action =
    "report.reply.file"`, `TargetKind = "reply"`, `TargetId =
    <replyId>`, `Outcome = Allow`.
  - `FileReplyReportAsync_MissingReply_ThrowsKeyNotFound` — a
    missing reply is a failed call (`KeyNotFoundException`),
    mirroring the post-report lane's "missing post" branch.
  A `ReplyAudits` test helper (scoped to `TargetKind = "reply"`)
  sits alongside the existing `PostAudits` (scoped to
  `TargetKind = "post"`).

## Consequences

Positive
- A neighbor can now flag the specific reply that is the problem, not
  just the post that contains it. The moderator's queue row and the
  resolve view both say "reply by X" and quote the reply body, so
  the signal a GlobalAdmin acts on is the actual offending text,
  not the surrounding post.
- The M3b workflow (assign / unlock / resolve) is unchanged for
  reply-targeted rows — they flow through the same
  `ComponentId`-scoped, `Status`-driven lanes with the same
  `C-M3b·4` SoD gate. No new standing, no new route shape, no new
  `Status` literal.
- The additive field (`Report.ReplyId`) is ADR 0004 §B.1: idempotent
  at boot, no migration, pre-existing rows keep `null` (post-target
  by default), and the four `Status`-literal pins in the design doc
  hold verbatim for both target kinds.
- The audit log now distinguishes the two lanes at the row level
  (`Action` + `TargetKind`), so a later "how many reply-reports have
  we had" question is a single filtered query on the existing
  `AccessAudit` table — no new audit shape.

Negative / accepted risks
- **The queue's Post column now carries two lines** (the post title
  + an optional "reply by X" line) for reply-targeted rows.
  Accepted: the queue is a GlobalAdmin surface, the post is still
  the primary anchor (the row's `PostId` is the parent post id, the
  reply is the qualifier), and the resolve view disambiguates with
  the full reply body.
- **`C-M3·1` "reply-inherits" means the reportable surface is
  bounded by the post's audience.** A reply under a
  community-audience post is reportable only by a resident who can
  see that post — a reply under a private-audience post is
  reportable only by the author + the granted audience. That is the
  same bound as the post itself (you cannot report a reply you
  cannot see), and it matches the platform's "visibility is the
  post's single `Read` decision" invariant — accepted as the
  correct scope.
- **A reply that is deleted (hard-removed by the M3b F4 lane) before
  the report is reviewed** still leaves a reply-targeted `Report`
  row pointing at a missing `PostReply`. The queue / resolve view
  handle this gracefully (the `LoadAsync<PostReply>` returns
  `null`, the "reply by X" line and the reply blockquote are simply
  omitted, the row still shows the post + reason + status).
  Accepted: the report's existence and its `Status` lifecycle are
  independent of the reply's existence — a report is a record of an
  intake action, not a pointer that must out-resolve the target.

## Revisit when

- A **reply-edit** surface is wanted that lets a resident edit their
  own reply after a report is filed (ADR 0016 already lets the
  author edit the body pre-report; post-report editing is a new
  decision).
- A **reply-removal** lane (distinct from the M3b post-hide /
  post-remove) is wanted — today the F3/F4 lanes operate on the
  post; a reply-own hide/remove is a new seam + a new `PostStatus`
  value (or a new `PostReply` field) on the `PostReply` POCO.
- **Announcement replies** (if/when announcements grow a reply
  surface) are requested — today the report lane is post-scoped, and
  an announcement reply would need its own discriminator + the same
  two-negative filing-tag pin.
- A **second target kind** (e.g. a translation row, a group
  description edit) is reportable — the `Report` POCO would need a
  `TargetKind` discriminator beyond post/reply, and the
  `Action` / `TargetKind` audit-row convention established here
  would extend to the new kind.
