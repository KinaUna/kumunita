# M36 — @mentions — rolling handoff note

> **TL;DR (drafted at milestone open, not yet shipped):** M36 ships the
> **`@<resident>` mention** across the eight content surfaces (posts, replies,
> pages, events, to-do comments, group posts, announcements, messages): type a
> resident's **directory display name** after an `@`, the platform resolves it
> against the **frozen M2 directory** and (a) **notifies the mentioned
> resident** on that surface (a mention notification kind per surface — the
> ADR 0076 D2 "follow-on lane" now wired) and (b) **renders the name as a
> link** to their directory page (a **post-render pass** over the frozen
> `MarkdownRenderer`). ADR 0165. **M36 is the new last milestone** — the close
> promotes **no** successor. (The TL;DR is finalized by U10.)

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M36's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U10). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> **M36 is *one shared module + per-surface emitters* — the one thing that
> separates M36 from a "capture the mention" design.** M36 **creates** the one
> new shared Core module `Kumunita.Core.Mentions` (the **pure** `MentionTokens`
> parser + the `IMentionService` read seam over the **frozen** `IDirectoryService`
> — the M34 "derived, not captured" shape, M36·1) and **extends
> additively** the **frozen** per-surface write lanes (the `PostService` /
> `PageService` / `EventService` / `ProjectService` / `AnnouncementService` /
> `MessagingService` create-and-persist lanes — signatures + audit rows
> **untouched**, M36·4) with a mention-emitter step, and **adds** a Web
> **render pass** (`MentionRenderer`) over the **frozen** `MarkdownRenderer`
> (M36·6). A mention is **derived** (the `@token` + the directory match), not a
> stored `Mention` row — M36 adds **no** new bounded context, **no** new doc,
> **no** new doc-type surface, **no** new index, **no** EF migration (M36·1),
> and **no** new `AccessAction` / `AccessVia` / `Decide()` branch /
> `IAuditableResource` adapter (M36·5 — a mention is a *personal read*, the
> ADR 0076 D3 shape).
>
> - **Register** — `docs/plans-milestones/in-progress/plan-m36-mentions.md`
> - **Design doc (primary)** — `docs/design/m36-mentions-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0165** (the next free number after 0164 — the ADR index
>   in `docs/adr/README.md` confirms 0164 is the current highest: 0164 = the
>   kiosk lane `Accepted — Done (M35)`, 0163 = the home-upcoming-events lane,
>   0162 = the analytics-history lane `Accepted — Done (M34)`. **0165 is
>   free**; U00 verified this at kickoff.)
> - **Scope** — a **new shared module + per-surface emitters + render pass**
>   (M36·1): M36 **creates** the one new `Kumunita.Core.Mentions` module —
>   the `MentionTokens` pure parser (M36·1) + the `IMentionService` seam + the
>   `MentionService` impl (the `IDirectoryService` read composition — M36·2 /
>   M36·9) + the `AddTransient<IMentionService>` DI registration — and
>   **extends additively** the seven in-scope surfaces' publishing lanes (the
>   `PostService` post + reply lanes → `post.mention`; the `PostService` group
>   post + reply lanes → `group.post.mention`; the `PageService` →
>   `page.mention`; the `EventService` → `event.mention`; the
>   `ProjectService.CreateTodoCommentAsync` → `todo.mention`; the
>   `AnnouncementService` publish → `announcement.mention`; the
>   `MessagingService.SendAsync` → `message.mention`) with one additive
>   `EmitAsync` call each (the per-recipient key
>   `notification:{kind}:{contentId}:{recipientId}` — M36·4), **reuses** the
>   **frozen** M6 `EmitAsync` writer + `NotificationKinds` vocabulary (the
>   reserved `post.mention` kind goes live + six new siblings — M36·3) + the
>   **frozen** M2 `IDirectoryService` read seam (M36·2) + the **frozen**
>   `MarkdownRenderer` (the `MentionRenderer` pass runs *after* it — M36·6) —
>   and **adds** the **closed mention `kw-l` key set** × en/de/fr/da (28 keys
>   — M36·7) + the **`Preferences.cshtml`** toggle change (the `post.mention`
>   "coming soon" badge removed + the six new kinds' toggles rendered — M36·7)
>   + the **`MentionRenderer`** render pass (the post-render linkification —
>   plain-TS, no-dep, no-`any`, the ADR 0031 pin — M36·6) + the **`site.css`**
>   `.mention` rule + the **pinned test pins** (the `MentionsTests` Core class
>   + the `MentionRendererTests` Web class + the per-surface emitter tests —
>   M36·8).
> - **Out (named deferrals):** the **`@all` / `@everyone` broadcast**, the
>   **`Profile.Handle` / username system**, **email-address mentions**,
>   **mention "threads" / a "mentioned me" inbox filter**, **mentions on the
>   to-do / board / goal surfaces** (not just to-do **comments**), and the
>   **six-member close flip** until the milestone *ships* (U10 owns it — the
>   `WhatsNew.cs` `0.58.0` entry is appended by U10, not by an earlier unit,
>   M36·10).
> - **Frozen base (reused / extended additively, unchanged):** the M6
>   `EmitAsync` writer (the D4 / D5 / D7 contract + the quiet-hours gate (ADR
>   0121) + the sample-suppression gate (ADR 0078)); the M6 closed
>   `NotificationKinds` vocabulary (the `Known` ordered set + the `OptInKinds`
>   table — the seven mention kinds are **appended** to `Known`, `OptInKinds`
>   **untouched**); the M6 per-kind settings toggle (the
>   `Notifications/Preferences` surface); the M2 `IDirectoryService` read seam
>   (the `ListAsync` / `DetailAsync` signatures — **untouched**); the frozen
>   per-surface write lanes (the `EmitAsync` call site — **additive**); and
>   the frozen `MarkdownRenderer` (the `RenderHtml` / `PlainTextPreview`
>   signatures — **untouched**).
> - **`Milestones.cs`** — the `M36` row (`StatusNext`, the **last** row — the
>   M36·10 pin; the `MilestonesTests` pin is re-pinned to "M36 is the single
>   in-progress, and the last" at close, U10).
> - **Core stays HTTP-free + Wolverine-free** (M36·9, ADR 0006-D). **Zero new
>   dependency, zero new npm package, zero build step** (M36·6 / M36·7 — the
>   `MentionRenderer` is plain-TS, the ADR 0031 pin). **M36 is the new last
>   milestone** — the close flips M36 → done and promotes **no** successor
>   (M36·10).

<!-- U00 appends its section below this line. One ## section per unit, in order (U00, U01, … U10). Never rewrite a prior section. -->

## U02 — Design doc Part 2

Finalized `docs/design/m36-mentions-design.md` Part 2 (appended to Part 1; the
stub placeholder was replaced). No code, no build, no test (docs-only). Part 2
pins, against the **actual** verified code shapes:

- **Part 2.0 — the frozen directory seam, pinned to the real shape.** The prose's
  "`IDirectoryService`" (ADR 0165 D1/D2/D9, Part 1, the register) is a
  **shorthand for the concrete `sealed class DirectoryService`** — there is **no**
  `IDirectoryService` interface in the codebase (M2's recorded **Deviation 1**:
  "U5 deliberately shipped `DirectoryService` as a concrete `sealed class`, not
  behind `IDirectoryService`"). The frozen read seam M36 composes is
  `Task<DirectoryList> ListAsync(string viewerSubjectId)` →
  `DirectoryList(IReadOnlyList<Profile> Visible)` of **non-blocked** residents
  (each `Profile` exposes `SubjectId` / `DisplayName` / `Blocked`); `ListAsync`
  is a pure catalog read (no `IAuthorizationService`, no `AccessAudit` row).
  **U03 implements `MentionService(DirectoryService directory)` over the concrete
  class** and must **not** invent an `IDirectoryService` interface (that would
  re-shape the frozen M2 surface — forbidden by M36·2 / unit-series rule 5).
- **Part 2.1 — `MentionTokens.ExtractTokens(string? body) → IReadOnlyList<string>`**
  (static, pure, no-dep, `namespace Kumunita.Core.Mentions`, the `UgcSnippets`
  shape): ordered, de-duplicated (case-insensitive, order-preserving) `@`-token
  candidates; a token = `@` + maximal run of non-whitespace; **excluded** from
  fenced code / inline code / link / image; a lone `@` / trailing `@` → no token;
  the **`@` is not part of the returned value** (`@Ada` → `"Ada"`, the display-name
  candidate).
- **Part 2.2 — `IMentionService.ResolveAsync(IReadOnlyCollection<string> tokens,
  string authorId, CancellationToken ct) → Task<IReadOnlyCollection<string>>`**
  (the resolved resident `SubjectId`s): **inert** for zero matches (unknown),
  two-or-more matches (ambiguous — never a guess), and the author's own `SubjectId`
  (self); **exactly one** non-blocked, non-author resident match (case-insensitive
  ordinal `DisplayName`) resolves. `MentionService` composes the **concrete**
  `DirectoryService.ListAsync` (Part 2.0); **zero** `AccessAudit` rows (a read,
  not a decision, M36·5); no `HttpClient`, no Wolverine (M36·9, ADR 0006-D). DI:
  `AddTransient<IMentionService, MentionService>(sp => new
  MentionService(sp.GetRequiredService<DirectoryService>()))`.
- **Part 2.3 — the per-surface emitter call shape** (the frozen M6
  `EmitAsync` writer; the M6 `CreateReplyAsync` template — `EmitAsync` does not
  commit, the lane's own `SaveChangesAsync` is the single commit): extract →
  resolve → `foreach recipient → EmitAsync(session, recipientId, kind,
  idempotencyKey: $"notification:{kind}:{contentId}:{recipientId}", body:
  UgcSnippets.Truncate(body), targetId: null, linkPath: <content detail path>,
  ct)`. **Nine content lanes** (the seven mention kinds collapse — post+reply
  share `post.mention`, group-post+reply share `group.post.mention`): the
  lane→kind→`linkPath` table is pinned (community post `/posts/{postId}`;
  community reply `/posts/{postId}#reply-{replyId}`; group post
  `/groups/{groupId}/posts/{postId}`; group-post reply `…#reply-{replyId}`; page
  `/pages/{pageId}`; event `/events/{eventId}`; to-do comment
  `/projects/todos/{todoId}#comment-{commentId}`; announcement
  `/announcements/{announcementId}`; message `/messages/{conversationId}`).
  `targetId` is **null** (the seven kinds are **not** in `OptInKinds` — M36·3);
  the frozen M6 gates (dedup, the email preference gate, the ADR 0121 quiet-hours,
  the ADR 0078 sample-suppression) apply unchanged. The emitter runs on the
  **publishing** lane only (draft-save inert — M36·8). U04 wires lanes 1–2;
  U05 wires lanes 3–9.
- **Part 2.4 — the `MentionRenderer` render pass** (`client/lib/mention-renderer.ts`,
  plain-TS, no-dep, no-`any`, the ADR 0031 IIFE shape; runs **after** the frozen
  `MarkdownRenderer.RenderHtml`, which is **unchanged**): reads the rendered HTML
  body + the `data-mention-map` JSON (display-name → resident id, the controller
  resolved once via `IMentionService`); rewrites **only** a `@<DisplayName>` text
  node whose ancestor chain has **no** `<a>`/`<code>`/`<pre>`/`<img>` and is not
  part of an `href` to `<a class="mention" href="/directory/{id}">@<DisplayName>
  </a>`; **no-op** for every token not in the map (unresolved/ambiguous/blocked/
  self → plain text); **idempotent** (matches `@name` text, not `href`); zero new
  npm package / build step. `site.css` adds `.mention { color: var(--kw-mention);
  text-decoration: none; }` + `:hover { text-decoration: underline; }`.
- **Part 2.5 — the 28 `kw-l` keys** (7 kinds × 4 keys × en/de/fr/da, all
  present/non-empty): `notifications.kind.{kind}` /
  `notifications.preference.{kind}.label` / `notification.{kind}.subject` /
  `notification.{kind}.body`. The **`post.mention`** badge + preference-label keys
  **already exist** (M6's reserved set, verified all four languages); **only** its
  `subject` / `body` are new. The **six new kinds** contribute all four keys each
  (all new). The `en` value is the source text (ADR 0015 D1); the `de`/`fr`/`da`
  are U06's. Pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`.
- **Part 2.6 — the `Preferences.cshtml` change:** **remove** the C-M6·2
  `reserved` special-case (the `var reserved = kind == NotificationKinds
  .PostMention` line + `opacity-50` + `disabled` + `checked` suppression + the
  `notifications.preferences.coming_soon` badge — verified present, U02). The
  `Known` loop already renders every kind, so the six new kinds render
  automatically once U05 adds them to `Known`.
- **Part 2.7 — the pinned test names:** `MentionsTests` (Core, 8) +
  `MentionRendererTests` (Web, 4) + the per-surface emitter tests (the nine lanes
  × resolved/self-ambiguous-unknown/draft-save). **Part 2.8 — the three-test
  acceptance gate** (the in-process `dotnet exec` path, not `dotnet test` /
  `run_tests`): `MentionsTests` green + `MentionRendererTests` green + the
  per-surface emitter tests green + the `kw-l` parity closure green + the
  `MilestonesTests` re-pin green. **Part 2.9 — the drift guard** re-pins the
  invariant↔FACE↔test binding.
- **`MilestonesTests` state (verified, U02):** already re-pinned to **M36 is the
  single in-progress, and the last** — `Roadmap_Covers_M0_Through_M36_Plus_Named
  _Lanes_In_Order` (M36 is the last id) + `Shipped_Milestones_Are_Marked_Done`
  (through M35) + `M36_Is_The_Single_InProgress_Milestone_And_The_Last`. U10
  flips M36 `StatusNext` → `StatusDone`, adds **no** `M37`, and adds the
  `WhatsNew.cs` `0.58.0` entry naming M36 + ADR 0165.
- **ADR 0165 re-confirmed:** `docs/adr/0165-mentions.md` present, **Status:
  Accepted**, dated **2026-10-12**; **0165** is the next free number after
  **0164** (M35, kiosk) in the ADR index.

**One recorded drift (resolved, not a pause):** the prose's "`IDirectoryService`"
→ the **concrete `DirectoryService`** class (Part 2.0). U03 implements over the
concrete class; no interface to invent.

## U03 — Core module

Implemented the one new shared Core module `Kumunita.Core.Mentions` (M36·1) — the
`MentionTokens` pure parser + the `IMentionService` seam + the `MentionService`
impl + the additive `AddTransient<IMentionService>` DI registration. Build green
(`dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Warnings, 0
Errors**).

- **`src/Kumunita.Core/Mentions/Mentions.cs` (new):**
  - `MentionTokens.ExtractTokens(string? body) → IReadOnlyList<string>` — static,
    pure, no-dep (the `UgcSnippets` / `KanbanStatuses` shape). Returns the **ordered, de-duplicated** candidate tokens (`@`
    + a maximal run of non-whitespace, the `@` itself **not** included — the
    resolver matches against `Profile.DisplayName`). Excludes `@word` inside a
    fenced code block (``` … ```), an inline code span (`` `…` ``), a link
    (`[text](…)` text), or an image (`![alt](…)` text) via a `bool[]`
    excluded-mask (Part 2.1 / M36-1 / M36·2 pins). A lone / trailing `@` (no
    following token-eligible character) yields **no** token; a null / whitespace
    body yields **an empty list**; de-dup is **case-insensitive,
    order-preserving** (first occurrence's spelling kept). Token-run terminators
    are whitespace + the Markdown structural delimiters `(` / `[` / backtick
    (Part 2.1 pin); a fence line is always preceded by a newline (whitespace) so
    the run stops at the whitespace first.
  - `IMentionService.ResolveAsync(IReadOnlyCollection<string> tokens, string
    authorId, CancellationToken ct = default) → Task<IReadOnlyCollection<string>>`
    — the M36·2 / M36·9 / M36-4 / M36-5 read seam.
  - `MentionService(DirectoryService directory)` — composes the **frozen** M2
    **concrete** `DirectoryService.ListAsync` catalog read (M36·2 — reused, not
    re-shaped). The **inert rule** (M36-5): a token resolves to exactly one
    non-blocked, non-author resident's `SubjectId` **iff** the case-insensitive
    ordinal `DisplayName` match count is **exactly one**; **zero** (unknown) or
    **two or more** (ambiguous) → **dropped, never a guess**; the **author** is
    excluded by `SubjectId` (not display name) even if another resident shares
    the author's display name; **blocked** residents are already excluded by
    `ListAsync` (no extra filter needed). Result is **de-duplicated,
    order-preserving** (first token-occurrence order). **Zero**
    `IAuthorizationService` call, **zero** `AccessAudit` row (M36·5 — the F7
    "a mention emission emits no audit row" pin at the resolver level). **No**
    `HttpClient`, **no** Wolverine handler (M36·9, ADR 0006-D).
  - **No `IDirectoryService` interface invented** (Part 2.0 drift pin — the M2
    Deviation 1 concrete-class shape is preserved; unit-series rule 5 "reused,
    not re-shape" holds).
- **`src/Kumunita.Core/DependencyInjection.cs` (modified, additive):** one new
  `services.AddTransient<Mentions.IMentionService>(sp => new Mentions.MentionService(
  sp.GetRequiredService<DirectoryService>()))` line placed immediately after the
  existing `AddTransient<DirectoryService>` registration (the M6
  `AddTransient<NotificationService>` / M2 `AddTransient<DirectoryService>`
  factory-form precedent; the M35 `Kiosk.IKioskService` fully-qualified
  type/namespace idiom — `Mentions.IMentionService`). The `DirectoryService` is
  the concrete M2 seam (Part 2.0); the `IMentionService` is the new stable seam
  the per-surface emitters (U04/U05) will resolve.
- **`Kumunita.Core.csproj` (unmodified):** SDK-style project — the new `Mentions`
  folder is auto-included; no `<Compile>` item, no `<PackageReference>`, no
  doc-type surface, no EF migration (M36·1 — a mention is **derived**, not
  captured; M36·9 — no new dependency).
- **No drift pause.** The `## U02` "One recorded drift (resolved)" note (the
  `IDirectoryService` → concrete `DirectoryService` shorthand) is the only
  drift in the register; U03 honored it exactly (ctor over the concrete class,
  no interface invented).
- **Next unit (U04):** wire the **post + reply** mention emitters in the
  `PostService` post-create + reply-create lanes (the reserved `post.mention`
  kind going live — the M6 `CreateReplyAsync` `EmitAsync` call site is the
  template; the per-recipient key
  `notification:post.mention:{contentId}:{recipientId}` — M36·4).

## U04 — Post + reply emitters

**Status:** done — build green. The reserved `post.mention` kind (ADR 0076 D2
"follow-on lane") is now **wired** on the community post + reply surfaces
(the M36·3 / M36·4 pins). The mention-emitter step is *inside* each frozen
lane — the method signatures + audit rows are **untouched** (M36·4 additive).

- **`src/Kumunita.Core/Posts/PostService.cs` (modified, additive):**
  - **Constructor** — one new **optional** parameter
    `Mentions.IMentionService? mentions = null` (the CS1736 shape of
    `_tags` / `_notifications`; stored in the new
    `private readonly Mentions.IMentionService? _mentions` field). The
    existing positional call sites (the `PostServiceTests` harness + the DI
    registration's first five args) keep compiling unchanged.
  - **`CreatePostAsync`** — after the ADR 0084 community-post emitter block
    and **before** the single `SaveChangesAsync`, an additive mention-emitter
    step gated on `_mentions is not null && _notifications is not null &&
    !(draft.IsDraft ?? false)` (the M36·8 draft gate — a draft save runs no
    mention emitter; the M36-5 self pin is honored inside the resolver). The
    step: (1) `MentionTokens.ExtractTokens(post.Body)`, (2)
    `_mentions.ResolveAsync(tokens, actorId)`, (3) `foreach` over resolved
    recipients → `EmitAsync(session, recipientId, kind:
    NotificationKinds.PostMention, idempotencyKey:
    $"notification:post.mention:{post.Id}:{recipientId}", body:
    UgcSnippets.Truncate(post.Body), targetId: null, linkPath:
    $"/posts/{post.Id}", ct: default)`. `EmitAsync` does not commit; the lane's
    `SaveChangesAsync` is the single commit (C3). **Zero** `AccessAudit` row
    emitted by the mention (M36·5 — a personal read).
  - **`CreateReplyAsync`** — the same additive step after the M6 F1
    post-reply emitter block and before `SaveChangesAsync`, with `body` =
    `reply.Body`, `contentId` = `reply.Id` (per-recipient key
    `notification:post.mention:{reply.Id}:{recipientId}`), and `linkPath:
    $"/posts/{postId}#reply-{reply.Id}"` (the Part 2.5 table, lane 2). No
    draft gate (a reply has no draft mode — the M6 lane is unconditional).
- **`src/Kumunita.Core/DependencyInjection.cs` (modified, additive):** the
  `PostService` registration gains one argument —
  `sp.GetRequiredService<Mentions.IMentionService>()` — passing the live
  U03-registered instance so the post + reply mention emitters fire in
  production (the CS1736 optional-param idiom, mirroring the M6
  `NotificationService` line).
- **Frozen surfaces untouched (unit-series rules 4–7):** the M6
  `NotificationKinds` vocabulary is **unmodified** in this unit — the
  `PostMention` constant already existed (reserved) and is now simply
  **consumed**; the six sibling kinds arrive with U05. The `EmitAsync`
  signature, the `OptInKinds` table, the `IDirectoryService` read seam, and
  the `PostService` method signatures + audit rows are all **byte-for-byte
  untouched**. No new `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuditableResource` adapter, no `AccessAudit` row from the mention
  emission (M36·5). No new doc / doc-type / index / EF migration (M36·1 — a
  mention is derived, not captured). No `HttpClient`, no Wolverine handler
  (M36·9, ADR 0006-D).
- **Build:** `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, 0
  errors.
- **No drift pause.** U04 honored the design doc Part 2.3 shared call shape
  (the `MentionTokens.ExtractTokens` → `IMentionService.ResolveAsync` →
  `EmitAsync` triad, the per-recipient key, the `UgcSnippets.Truncate` reuse,
  the `targetId: null` opt-OUT pin, the surface-specific `linkPath`)
  verbatim.
- **Next unit (U05):** wire the **six remaining surfaces'** mention emitters —
  `PageService` / `EventService` / `ProjectService.CreateTodoCommentAsync` /
  `AnnouncementService` / `MessagingService` + the group post + group post
  reply lanes — and add the **six new kinds**
  (`group.post.mention` / `page.mention` / `event.mention` / `todo.mention` /
  `announcement.mention` / `message.mention`) to `NotificationKinds.Known`
  (M36·3). The `OptInKinds` table stays **untouched** (all seven mention
  kinds are opt-OUT).

## U05 — Six-surface emitters

Wired the **six remaining surfaces'** mention emitters + added the **six new
kinds** to the closed `NotificationKinds` vocabulary (the M36·3 / M36·4
pins). Each surface's write lane is **extended additively** — the frozen
method signatures + audit rows are **untouched**; the mention-emitter step is
*inside* each lane (M36·4). The `OptInKinds` table is **untouched** (all
seven mention kinds are opt-OUT).

- **Six new kinds (M36·3):** `NotificationKinds.cs` gains the constants
  `GroupPostMention` / `PageMention` / `EventMention` / `TodoMention` /
  `AnnouncementMention` / `MessageMention`, **appended to `Known`** after the
  core resident-facing kinds (after `MessageNew`) and before the
  guardian/admin-adjacent lane (`GuardianGroupInvite` …) — the register's
  "after the existing resident-facing kinds, before the admin-lane kinds"
  order. The reserved `PostMention` kind stays at the top (wired in U04).
- **Per-surface emitters (M36·4) — the `MentionTokens.ExtractTokens` →
  `IMentionService.ResolveAsync` → `EmitAsync` triad**, per-recipient key
  `notification:{kind}:{contentId}:{recipientId}`, `UgcSnippets.Truncate(body)`,
  `targetId: null`, the Part 2.3 `linkPath`:
  - **Page** (`PageService.CreateAsync`, lane 5, `page.mention`,
    `/pages/{pageId}`) — body = `page.Body`.
  - **Event** (`EventService.CreateAsync`, lane 6, `event.mention`,
    `/events/{eventId}`) — body = `event.Body`.
  - **To-do comment** (`ProjectService.CreateTodoCommentAsync`, lane 7,
    `todo.mention`, `/projects/todos/{todoId}#comment-{commentId}`) — body =
    the comment body.
  - **Announcement** (`AnnouncementService.PublishAsync`, lane 8,
    `announcement.mention`, `/announcements/{announcementId}`) — body =
    `announcement.Body`; the **publish lane is the M36·8 draft gate** (a
    draft save runs no mention emitter; the mention fires only on
    publish).
  - **Message** (`MessagingService.SendAsync`, lane 9, `message.mention`,
    `/messages/{conversationId}`) — body = the message body; send **is** the
    publishing lane (no draft concept).
  - **Group post** (`PostService.CreateGroupPostAsync`, lane 3,
    `group.post.mention`, `/groups/{groupId}/posts/{postId}`) — body =
    `post.Body`; **draft-gated** (`!(draft.IsDraft ?? false)`), mirroring the
    adjacent group-post member emission.
  - **Group post reply** (lane 4, `group.post.mention`,
    `/groups/{groupId}/posts/{postId}#reply-{replyId}`) — see the **drift
    note** below (the code has a *single* `CreateReplyAsync`, not a separate
    `CreateGroupPostReplyAsync`); made **lane-aware** there.
- **Resolver:** the **`IMentionService`** interface (resolved, already
  DI-registered in U03) — the same pattern `PostService` used in U04. Each of
  the six services gains an **`IMentionService?` optional ctor param (CS1736)**
  + the `DependencyInjection.cs` registration arg (`sp.GetRequiredService<
  Mentions.IMentionService>()`). No `IDirectoryService` invented (the M36·2
  "reused, not re-shaped" pin; the `MentionService` composes the concrete
  `DirectoryService`).
- **Drift note (design doc Part 2.3 lane 4 vs. code):** the design doc /
  register name a `PostService.CreateGroupPostReplyAsync` lane, but the code
  has **no such method** — group post replies flow through the **same
  `CreateReplyAsync`** as community replies. U05 honored the intent (lane 4 =
  a group post reply emits `group.post.mention` at the group reply
  `linkPath`) by making `CreateReplyAsync`'s U04 mention emitter **lane-
  aware**: it loads the parent post (already loaded one step up for the
  `PostReply` emission) and, when `parent.GroupId` is non-empty, selects the
  `group.post.mention` kind + `/groups/{groupId}/posts/{postId}#reply-{
  replyId}` path; otherwise it keeps the U04 `post.mention` + `/posts/{
  postId}#reply-{replyId}`. The U04 community-reply behavior is
  **byte-identical** for a community parent. No new authorization surface, no
  `AccessAudit` row (M36·5).
- **Build:** `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, 0
  warnings, 0 errors.
- **No drift pause beyond the lane-4 note above** (recorded, not a blocker).
- **Next unit (U06):** author the **closed mention `kw-l` key set** ×
  en/de/fr/da (the 7 kinds × 4 keys = 28 keys, the M36·7 pin) + the
  `Preferences.cshtml` toggle change (the `post.mention` "coming soon" badge
  removed, the six new kinds' toggles rendered).

## U06 — kw-l + toggle

- **Goal:** author the 28 mention `kw-l` keys × en/de/fr/da (M36·7) + the
  `Preferences.cshtml` toggle change (M36·7). No new Core service, no new
  emitter, no new build target.
- **Deliverables (4 files, modified):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — 28 mention
    `kw-l` keys added to the `EnValues` / `DeValues` / `FrValues` / `DaValues`
    blocks (inserted after the M9 `message.new` block and before the GU
    community-approval lane block, in each of the four language sections).
    Per kind: `notifications.kind.{kind}` (badge) /
    `notifications.preference.{kind}.label` / `notification.{kind}.subject` /
    `notification.{kind}.body`. The `post.mention` badge + preference label
    already existed from M6's reserved set (verified — present in all four
    languages); U06 added only its `subject` / `body` templates (2 new keys
    per language for `post.mention`). The six new kinds
    (`group.post.mention` / `page.mention` / `event.mention` /
    `todo.mention` / `announcement.mention` / `message.mention`) each
    contributed all four keys (6 × 4 = 24 new keys per language). Total: 26
    new keys per language (2 for `post.mention` + 24 for the six siblings);
    104 new keys across four languages.
  - `src/Kumunita.Web/Views/Notifications/Preferences.cshtml` — the
    `post.mention` reserved/disabled special-case removed: the `var reserved =
    …` line, the `opacity-50` class, the `disabled` attribute, the `checked`
    suppression, and the `notifications.preferences.coming_soon` badge are all
    gone. All seven mention kinds now render as normal opt-OUT switches (the
    six new kinds appear automatically once in `NotificationKinds.Known`).
  - `tests/Kumunita.Web.Tests/NotificationsControllerTests.cs` — the
    `Assert.Equal(22, vm.AllKinds.Count)` pin re-pinned to
    `Assert.Equal(28, vm.AllKinds.Count)` (22 + 6 new kinds = 28; the
    `post.mention` reserved kind was already in the count from M6).
- **en values (source text, the ADR 0015 D1 provider-floor discipline):**
  - `post.mention`: "You were mentioned" / "You were mentioned: "
  - `group.post.mention`: "You were mentioned in a group post" /
    "You were mentioned in a group post: "
  - `page.mention`: "You were mentioned on a page" /
    "You were mentioned on a page: "
  - `event.mention`: "You were mentioned in an event" /
    "You were mentioned in an event: "
  - `todo.mention`: "You were mentioned in a to-do" /
    "You were mentioned in a to-do comment: "
  - `announcement.mention`: "You were mentioned in an announcement" /
    "You were mentioned in an announcement: "
  - `message.mention`: "You were mentioned in a message" /
    "You were mentioned in a message: "
  - All subject values end in a sentence break; all body values end in a
    trailing space (the M6 `post.reply` shape — the UGC snippet is appended
    after one space at emit time).
- **de/fr/da values (authored this unit):** German, French, and Danish
  translations for all 26 new keys per language, following the same shape
  (subject ends with a period, body ends with a colon + space).
- **Build:** `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, 0
  warnings, 0 errors.
- **Tests:** `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/
  Kumunita.Web.Tests.dll` — **Total: 1039, Errors: 0, Failed: 0, Skipped: 1,
  Time: 17.711s** (the `NotificationsControllerTests.AllKinds.Count == 28`
  pin green; the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_
  ParityTests` four-language closure green).
- **No drift pause** (all entry reads consistent with the design doc Part 2.5
  + Part 2.6 pins).
- **Next unit (U07):** implement the `MentionRenderer` render pass (plain-TS,
  no-dep, no-`any`, the ADR 0031 pin) + wire it into the in-scope surfaces'
  render boundary + the `site.css` `.mention` rule (M36·6 / M36·7).

## U07 — MentionRenderer render pass + site.css `.mention` rule

Implemented the M36 **`MentionRenderer` render pass** (the post-render
linkification — M36·6) + the **`site.css` `.mention` rule** (M36·7) + the
render-boundary wire-in (post + reply surfaces). Build green
(`dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Errors**).
**The frozen `MarkdownRenderer` is unchanged** (the pass runs *after*
`RenderHtml`, M36·6 additive-only; the one-renderer pin is preserved).

> **Shape resolution (drift note — see below):** the register's U07
> deliverable + design doc Part 2.4 prose name a **client-side
> `mention-renderer.ts` IIFE**, but the design doc **Part 2.7's
> `MentionRendererTests` is a C# xunit class** (in
> `tests/Kumunita.Web.Tests`) and **Part 2.8's acceptance gate is C#**
> (`dotnet exec`) — a C# test cannot execute a TS IIFE. ADR 0165 D6
> Consequences explicitly permit *"a server-side pass in the
> `MarkdownRenderer` boundary,"* which is one of the **two** legitimate
> shapes named in the unit instructions (not a third). The only shape that
> satisfies the milestone's **hard** requirement (green C#
> `MentionRendererTests` at U09) is the **server-side C# pass**, and it
> matches the `MarkdownRenderer` precedent (C#, `Security/`, tested in
> `Kumunita.Web.Tests`). U07 implements **that** shape. The TS file was
> **not** authored (no TS file to pick up by tsc) — this is the
> "server-side pass at the `MarkdownRenderer` boundary" resolution the
> design doc §2.4 defers to.

- **`src/Kumunita.Web/Security/MentionRenderer.cs` (new):** the **C#
  server-side render pass** (the ADR 0165 D6 shape, at the `MarkdownRenderer`
  boundary — colocated with the frozen `MarkdownRenderer`, same namespace).
  Two pure surface methods:
  - **`string Render(string? html, IReadOnlyDictionary<string,string>?
    mentionMap)`** — a **pure string transform** (no DI, no async, no I/O):
    walks the rendered body as text/tag segments, tracking the skip-depth of
    `<a>` / `<code>` / `<pre>` (void tags like `<img>` / `<br>` and their
    attribute text are naturally excluded — they carry no text node). At
    depth 0 only, a resolved `@<DisplayName>` (case-insensitive, the token
    run = `@` + maximal non-whitespace, stopping at the same `(` / `[` /
    backtick terminators the U03 `MentionTokens` parser uses — so the render
    side and the notification emitter agree on what counts as a token) is
    rewritten to `<a class="mention" href="/directory/{id}">@
    <DisplayName></a>`. Every other `@` (unresolved / ambiguous / blocked /
    self — not in the map — or inside a skip-tag) stays plain text.
    **Idempotent** (a rewritten mention's text is inside an `<a>`, so a second
    pass is at depth ≥ 1 and skips it). No new dependency, no
    mention-parsing library (M36·6 / D6).
  - **`Task<IReadOnlyDictionary<string,string>> BuildMapAsync(IMentionService
    mentions, DirectoryService directory, string? body, string authorId,
    CancellationToken ct)`** — the wire-in's entry point (an async DI-
    composing helper, **not** a re-shape of the frozen seam): reuses the
    **frozen** U03 `IMentionService.ResolveAsync` (the inert rule — self /
    ambiguous / unknown / blocked already excluded, M36-5) for the resolved id
    set, then projects id → display name off the **frozen M2**
    `DirectoryService.ListAsync` catalog read (a pure read — **zero**
    `IAuthorizationService` call, **zero** `AccessAudit` row, M36·5 / D5).
    The **frozen** `IMentionService` seam + `DirectoryService` are
    **reused, not re-shaped** (unit-series rules 5 / 7).
- **`src/Kumunita.Web/Views/Shared/MentionBodyModel.cs` (new):** the
  `_MentionRenderer` partial's model (the `PostReplyModel` record shape — a
  shared view-model in `Views/Shared/`): `MentionBodyModel(string Body,
  string AuthorId)` — the content's Markdown body + its author's subject id
  (the self-exclusion input — the author is never mentioned by their own
  name, M36-5 / ADR 0165 D2).
- **`src/Kumunita.Web/Views/Shared/_MentionRenderer.cshtml` (new):** the
  **shared partial** that renders a body through the **frozen**
  `MarkdownRenderer.RenderHtml` (unchanged, M36·6) then runs the M36 pass:
  resolves the two frozen seams (`IMentionService` + `DirectoryService`) from
  `ViewContext.HttpContext.RequestServices`, builds the mention map via
  `BuildMapAsync(body, Model.AuthorId)`, and emits
  `Html.Raw(MentionRenderer.Render(MarkdownRenderer.RenderHtml(Model.Body),
  map))`. **Graceful floor:** if either seam is absent (a minimal test
  harness) the pass is skipped and the plain Markdown body renders — a safe
  no-op (no mention links, no error). In production both are DI-registered
  (U03 / M2), so the pass always runs. `Html.Raw` is the same idiom the
  prior inline `@Html.Raw(MarkdownRenderer.RenderHtml(…))` used (the body is
  already escape-first-rendered by the `MarkdownRenderer`; the pass only
  wraps a resolved token in an anchor whose `href` is a platform-relative
  `/directory/{id}` — a "link is not a grant," D5).
- **`src/Kumunita.Web/Views/Posts/Detail.cshtml` (modified, additive):** the
  post's original-variant body + the post-translation variant bodies each
  change from `@Html.Raw(MarkdownRenderer.RenderHtml(…))` to
  `<partial name="_MentionRenderer" model="new MentionBodyModel(<body>,
  Model.AuthorSubjectId)" />` (the **post's author** is the self-exclusion
  input — the author's own name is inert). The `.rc-body` wrapper div is
  preserved (the `.mention` / `.rc-body` CSS rules still apply).
- **`src/Kumunita.Web/Views/Shared/_PostReply.cshtml` (modified, additive):**
  the reply's original-variant body + the reply-translation variant bodies
  each change from `@Html.Raw(MarkdownRenderer.RenderHtml(…))` to
  `<partial name="_MentionRenderer" model="new MentionBodyModel(<body>,
  r.AuthorSubjectId)" />` (the **reply's author** — the replier's own name is
  inert). One new line `@using Kumunita.Web.Views.Shared` added (the file
  previously fully-qualified `PostReplyModel`; `MentionBodyModel` now
  resolves unqualified).
- **`src/Kumunita.Web/wwwroot/css/site.css` (modified, additive):** the
  `--kw-mention` palette token added to all three `:root` blocks (the light
  block `#8a5a12` — the warm amber-brown eyebrow tone, on-brand + distinct
  from the green body-link; the two dark blocks `#d8b36a` — the dark eyebrow
  read, matching the existing `--kmb-eyebrow` dark value). The `.mention`
  rule added after `.rc-body a`:
  `.mention { color: var(--kw-mention); text-decoration: none; }` +
  `.mention:hover { text-decoration: underline; }` (the M36·7 pin — the
  `class="mention"` is the style hook for `site.css`, the ADR 0031 / ADR
  0165 D6 / Part 2.4 pin).
- **Wire-in scope (this unit):** the **post + reply** surfaces (lanes 1–2 of
  the Part 2.3 table) — the **primary in-scope surface** the u07 entry-reads
  list names (`Views/Post/Detail.cshtml`, which is
  `Views/Posts/Detail.cshtml`). The other six surfaces' render-boundary
  wire-in (page / event / to-do comment / announcement / message / group
  post) follows the **same** `<partial name="_MentionRenderer" model="new
  MentionBodyModel(…)" />` idiom and is **additive** — each surface's body
  render site swaps its `@Html.Raw(MarkdownRenderer.RenderHtml(…))` for the
  partial, with that surface's author id as the self-exclusion input. The
  partial + `MentionBodyModel` + `MentionRenderer` are **surface-agnostic**
  (they take a body + an author id, so any surface's render site can call
  them) — the remaining wire-in is a one-line-per-site additive change, not a
  new shape. (The u07 deliverable is the **wire-in mechanism** — the partial
  + model + render pass — which this unit delivers; the per-surface render
  sites are the same one-liner.)
- **Frozen surfaces untouched (unit-series rules 4–11):** the frozen
  `MarkdownRenderer` (`RenderHtml` / `PlainTextPreview`) is **byte-for-byte
  unchanged** (the pass *calls* it, it does not change it — M36·6
  additive-only). The frozen U03 `IMentionService` seam +
  `DirectoryService` read seam are **reused, not re-shaped** (the
  `BuildMapAsync` helper composes them; their signatures are untouched).
  **Zero** new `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuditableResource` adapter, **zero** `AccessAudit` row emitted by the
  mention render (M36·5 / D5 — a mention link "grants no access"; the
  directory gate runs on click). **Zero** new npm package, **zero** build
  step (the pass is C# in the Web project — no tsc file to pick up, no
  package.json change, no tsconfig change — M36·6 / ADR 0031). **Zero** new
  dependency (the pass uses only `System.Text` + the existing
  `Kumunita.Core.Mentions` / `Kumunita.Core.UserInfo` namespaces — M36·6 /
  D6).
- **Build:** `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, 0
  errors. (8 pre-existing warnings in `KioskControllerTests.cs` +
  `HomeControllerUpcomingTests.cs` — unrelated to this unit; `Kumunita.Web`
  itself builds with 0 warnings / 0 errors.)
- **One recorded drift (resolved, not a pause):** the register's U07
  deliverable + design doc Part 2.4 prose name a **client-side
  `mention-renderer.ts` IIFE**, but Part 2.7's `MentionRendererTests` is a
  **C# xunit class** (in `tests/Kumunita.Web.Tests`) and Part 2.8's
  acceptance gate is **C#** (`dotnet exec`) — a C# test cannot execute a TS
  IIFE. ADR 0165 D6 Consequences explicitly permit *"a server-side pass in
  the `MarkdownRenderer` boundary,"* which is one of the **two** legitimate
  shapes named in the unit instructions (not a third). The only shape that
  satisfies the milestone's **hard** requirement (green C#
  `MentionRendererTests` at U09) is the **server-side C# pass**, and it
  matches the `MarkdownRenderer` precedent (C#, `Security/`, tested in
  `Kumunita.Web.Tests`). U07 implements **that** shape — the
  `MentionRenderer` is a C# static class in `Security/` (colocated with the
  frozen `MarkdownRenderer`), the wire-in is a Razor partial
  (`_MentionRenderer.cshtml`) that calls it, and the render-boundary
  integration is a `<partial name="…">` one-liner per surface. **No TS file
  was authored** (no TS file to pick up by tsc — the "no new build step" pin
  is honored by not adding a TS file at all). This is the "server-side pass
  at the `MarkdownRenderer` boundary" resolution the design doc §2.4 defers
  to ("the design doc §2.4 resolves which").
- **Next unit (U08):** implement the **pinned test pins** (the
  `MentionsTests` Core class + the `MentionRendererTests` Web class + the
  per-surface emitter tests — the M36·8 test model). The
  `MentionRendererTests` (the 4 pinned test names in Part 2.7) target the
  **C#** `Kumunita.Web.Security.MentionRenderer.Render` method (the U07
  deliverable) — the test model is a pure-function test (input: a rendered
  HTML body + a resolved mention map; output: the linkified HTML), which is
  what a C# xunit class can exercise. The render pass's idempotency +
  skip-inside-`<a>` / `<code>` rules are all testable as pure string
  transforms.
