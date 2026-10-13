# M36 — @mentions — sealed unit register

> **In progress.** This is the **lane register** (secondary tier of the
> milestone's three-tier contract) for **M36** — the README / `Milestones.cs`
> line, verbatim:
> "**@mentions — a cross-surface `@<resident>` reference token: type a
> resident's directory display name after an `@`, the platform resolves it
> against the resident directory and notifies the mentioned resident on that
> surface (posts, replies, pages, events, to-dos, group posts, announcements,
> messages) and renders the name as a link to their directory page.**"
>
> M36 is a **milestone** (it takes the `M36` letter; it is the **new last**
> milestone on the roadmap — there is **no** `M37` to promote), **not a named
> lane**. It is a **new capability on the cross-surface notification surface**
> (M36·1): it **creates** the one new shared Core module `Kumunita.Core.Mentions`
> (the **pure** `MentionTokens` parser + the `IMentionService` read seam over
> the **frozen** `IDirectoryService` — the M34 "derived, not captured" shape) and
> **extends** the **frozen** per-surface write lanes (the `PostService` /
> `PageService` / `EventService` / `ProjectService` / `AnnouncementService` /
> `MessagingService` create-and-persist lanes — **additive**, signatures + audit
> rows untouched) with a mention-emitter step, and **adds** a Web **render
> pass** (`MentionRenderer`) over the **frozen** `MarkdownRenderer`. It
> **extends** the **closed `NotificationKinds` vocabulary** with the **seven**
> mention kinds (the reserved `post.mention` goes live + six new siblings —
> M36·3) and the **closed `kw-l` registry** with the seven kinds' full key set
> × en/de/fr/da (M36·7). It adds **no** new bounded context (M36·1), **no** new
> doc / doc-type surface (a mention is *derived*, M36·1), **no** new
> `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
> adapter (M36·5), and **no** EF migration.
>
> **U00** verifies the surface (the frozen M6 `EmitAsync` / `NotificationKinds`
> / settings-toggle seams + the frozen M2 `IDirectoryService` read seam + the
> frozen per-surface write lanes + the frozen `MarkdownRenderer` + the ADR index
> — confirm **0165** is free) and authors the handoff-note skeleton. **U01/U02**
> author the primary-tier design doc (the 10 invariants M36·1–M36·10 + FACES
> M36-1–M36-9 + the `MentionTokens` parser contract + the `IMentionService` seam
> + the per-surface emitter call shape + the `MentionRenderer` render-pass
> contract + the closed mention `kw-l` key set + the `Preferences.cshtml` toggle
> change + the pinned test names + the acceptance gate + the drift guard) and
> draft **ADR 0165**. **U03** implements the Core shared module (the
> `MentionTokens` pure parser + the `IMentionService` seam + the `MentionService`
> impl + the `AddTransient<IMentionService>` DI registration). **U04** wires the
> **post + reply** mention emitters (the reserved `post.mention` kind going live
> — the `PostService` post-create + reply-create lanes). **U05** wires the
> **six remaining surfaces'** mention emitters (the `PageService` /
> `EventService` / `ProjectService.CreateTodoCommentAsync` / `AnnouncementService`
> / `MessagingService` + the **group post** create/reply lanes). **U06** authors
> the **closed mention `kw-l` key set** × en/de/fr/da (the 7 kinds × 4 keys) +
> the `Preferences.cshtml` toggle change (the `post.mention` "coming soon" badge
> removed, the six new kinds' toggles rendered). **U07** implements the
> **`MentionRenderer`** render pass (the post-render linkification — plain-TS,
> no-dep, no-`any`, the ADR 0031 pin) + wires it into the in-scope surfaces'
> render boundary. **U08** implements the **pinned test pins** (the
> `MentionsTests` Core class + the `MentionRendererTests` Web class + the
> per-surface emitter tests). **U09** runs + records the acceptance gate. **U10**
> flips the close (the six-member close flip; M36 is the **new last** milestone
> — no successor to promote).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M33 / M34 / M35 style
> (≤ ~5 files / ~500 LOC, 4–8 entry reads, one build). **One new shared Core
> module** (M36·1 — `Kumunita.Core.Mentions`; the M6 `Notifications` context +
> the M2 `UserInfo` / `Directory` context + the per-surface contexts are
> **reused / extended additively**, not re-added). **Zero new bounded context,
> zero new doc, zero new doc-type surface, zero new index, zero EF migration**
> (M36·1 — a mention is **derived**, not captured). **Zero new `AccessAction` /
> `AccessVia` / `Decide()` branch / `IAuditableResource` adapter** (M36·5 — a
> mention is a *personal read*, the ADR 0076 D3 shape; the mention emission
> emits **zero** `AccessAudit` rows). **The frozen `MarkdownRenderer` is
> unchanged** (M36·6 — the `MentionRenderer` pass runs *after* it; the
> one-renderer pin is preserved). **Zero new dependency, zero new npm package,
> zero build step** (M36·6 / M36·7 — the `MentionRenderer` is plain-TS, no-dep,
> no-`any`, the ADR 0031 pin). **Core stays HTTP-free + Wolverine-free**
> (M36·9, ADR 0006-D — the `MentionService` is a plain store-composing read; no
> `HttpClient` in Core). **The seven mention kinds are resident-facing,
> opt-OUT** (M36·3 — the ADR 0076 D2 reserved kind goes live + six new
> siblings; the `NotificationKinds.OptInKinds` table is **untouched**).
> **M36 is the new last milestone** — the close flip (U10) flips M36 → done and
> promotes **no** successor (M36·10).

## Understanding (one paragraph)

M1–M35 shipped the full resident surface: a directory, posts, group posts,
pages, events, projects, boards, to-dos, announcements, messaging, and the M6
shared-awareness lane (the inbox + the email + the closed kind vocabulary).
Every one of those content surfaces is a **broadcast to an audience** — but none
of them can **address one resident by name**. M6 (ADR 0076) shipped the
shared-awareness lane and deliberately **reserved** one kind — `post.mention` —
as *"a follow-on lane."* **M36 is that lane**, broadened across surfaces. A
resident composes a post, a reply, a page, an event, a to-do comment, a group
post, an announcement, or a direct message and types `@Ada can you bring the
projector?` — the platform **resolves `Ada` against the frozen M2 directory**
(the `IDirectoryService` read, **untouched**), **notifies Ada** on that surface
(a mention notification kind, one per in-scope surface, riding the frozen M6
`EmitAsync` — inbox row + best-effort email, the ADR 0076 D5 shape), and
**renders `@Ada` as a link** to Ada's directory page (a **post-render pass**
over the frozen `MarkdownRenderer` — the one-renderer pin is preserved). M36
**creates** one new shared Core module (`Kumunita.Core.Mentions` — the
`MentionTokens` pure parser + the `IMentionService` read seam) and
**reuses / extends additively** everything else: the frozen M6 `EmitAsync` /
`NotificationKinds` / settings-toggle seams, the frozen M2 `IDirectoryService`
read seam, the frozen per-surface write lanes, and the frozen `MarkdownRenderer`.
After M36, a resident types `@Ada` in any content surface and Ada is told — and
everyone reading sees the name link to Ada's page.

## The one thing every unit must respect

**@mention semantics (locked in ADR 0165, U00):**

- **M36 is one new shared Core module `Kumunita.Core.Mentions`, not a new
  bounded context and not a new doc (M36·1).** M36 **creates** the one new
  shared module (the `MentionTokens` pure helper + the `IMentionService` /
  `MentionService` — the M34 "derived, not captured" shape, the M13
  `IUsageAnalyticsService` / M25 `IStorageMetricsService` read-seam precedent)
  and **reuses / extends additively** the frozen M6 `EmitAsync` /
  `NotificationKinds` / settings-toggle seams (M36·4 / M36·7), the frozen M2
  `IDirectoryService` read seam (M36·2), the frozen per-surface write lanes
  (M36·4), and the frozen `MarkdownRenderer` (M36·6). M36 adds **no** new
  bounded context, **no** new doc, **no** new doc-type surface, **no** new
  index, and **no** EF migration (a mention is **derived**, not captured).
- **The reference token is `@` + a resident's directory `DisplayName`, matched
  against the frozen M2 directory — no new `Profile` field, no new handle
  (M36·2).** A token that matches **no** resident is **inert**; a token that
  matches **two or more** residents is **also inert** (ambiguous = no mention,
  never a guess — M36-5). The **author** is **never** notified of their own
  mention.
- **The `post.mention` reserved kind is now wired, and six sibling kinds join
  the closed vocabulary — one per in-scope surface (M36·3).** ADR 0076 D2
  reserved `post.mention` as the follow-on lane; M36 wires it on the **community
  post + reply** surface and adds **six** new `NotificationKinds` members
  (`group.post.mention` / `page.mention` / `event.mention` / `todo.mention` /
  `announcement.mention` / `message.mention`). All seven are **resident-facing,
  opt-OUT** (not in `OptInKinds` — the ADR 0076 D2 `post.reply` / `group.post`
  posture). They trail the existing resident-facing kinds in `Known`; the
  admin-lane kinds stay a separate lane (ADR 0077).
- **The mention idempotency key is per-recipient:
  `notification:{kind}:{contentId}:{recipientId}` (M36·4).** A mention is
  **multi-recipient** — one body may mention several residents. The recipient id
  is **part of the key** (`:{recipientId}`) so each mentionee stores their own
  inbox row + gets their own email, while a re-emission of the same (content,
  recipient) pair is still a no-op (ADR 0076 D4 / F10 preserved). The `EmitAsync`
  call is in the **same transaction** as the domain write (ADR 0076 D5 / C3).
- **A mention is a *personal read*, not an `AccessAction` decision. Zero new
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
  adapter (M36·5).** The emitter stores an inbox row + stages a best-effort
  email for the recipient — no `IAuthorizationService` call, **no**
  `AccessAudit` row emitted *by* the mention emission (F7). The render pass
  links `@Ada` to `/directory/{adaId}` — that link rides the **frozen** M2
  directory gate on click (the detail surface runs its own `ContactVisibility`
  decision), so a mention link **grants no access** beyond what the resident
  could already see (the M8 / M12 "a link is not a grant" posture).
- **The render pass is a no-op for an unresolved `@` token, and a mention is a
  post-render pass — not a new Markdown construct (M36·6).** The one-renderer
  pin (ADR 0025 / 0031 / 0033) is preserved: `MarkdownRenderer.RenderHtml` is
  **unchanged**. The `MentionRenderer` runs *after* the Markdown → HTML render,
  rewriting **only** the resolved, non-blocked, non-ambiguous, non-author
  `@<DisplayName>` tokens into `<a href="/directory/{id}">@<DisplayName></a>`.
  Every other `@` stays **plain text**. The pass is **idempotent** and **never**
  rewrites inside an existing `<a>` / `<code>` element.
- **The `kw-l` closed registry gains the seven kinds' full key set ×
  en/de/fr/da; the `post.mention` toggle goes from reserved/disabled to enabled
  (M36·7).** Per mention kind: the four keys
  `notifications.kind.{kind}` / `notifications.preference.{kind}.label` /
  `notification.{kind}.subject` / `notification.{kind}.body` (the `post.mention`
  kind + preference labels already exist from M6's reserved set — only its
  `subject` / `body` templates are new) — **7 kinds × 4 keys = 28 keys × four
  languages**, pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The `Preferences.cshtml` **removes** the
  `post.mention` reserved/disabled special-case.
- **Detection runs on the **publishing** write lane, not on draft-save
  (M36·8).** ADR 0037 drafts are author-only; a mention in a **draft** body must
  **not** notify. M36's emitters run in the lane that makes the content
  **visible** — the **publish** seam for the draft-capable surfaces (post /
  group post / announcement — the ADR 0037 author-only idempotent publish lane)
  and the **create** lane for the non-draft surfaces (page / event / to-do
  comment / message).
- **Core stays HTTP-free + Wolverine-free; the shared module is a plain
  store-composing read over the frozen directory seam (M36·9).** No
  `HttpClient` in Core (ADR 0006-D), no Wolverine handler, no outbound channel
  — the only outbound channel is the frozen M6 email lane (`IMailerStage`,
  already present).
- **The six-member close flip lands in the final unit; M36 is the new last
  milestone (M36·10).** The `Milestones.cs` / README / `STATUS.md` /
  `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip
  lands in U10. The `WhatsNew.cs` registry gains one new entry (newest-first,
  the next `0.x.0` row) naming M36 + ADR 0165. **M36 is the new last
  milestone** — the close flips M36 → done and promotes **no** successor (the
  `MilestonesTests` pin changes from "no in-progress milestone" (M35's terminal
  state) to "`M36` is the single in-progress, and the last").

## Assumptions

- **Scope (in):** the **`MentionTokens` pure parser** (the M36·1 pin) + the
  **`IMentionService` read seam** + the **`MentionService` impl** (the
  `IDirectoryService` read composition — the M36·2 / M36·9 pins) + the
  **`AddTransient<IMentionService>`** DI registration (the `DependencyInjection.cs`
  additive — the M6 `AddTransient<NotificationService>` precedent) + the
  **seven mention kinds** on the closed `NotificationKinds` vocabulary (the
  `post.mention` reserved kind now **wired** + the six new siblings — the
  M36·3 pin) + the **per-surface mention emitters** in the **seven** in-scope
  surfaces' publishing lanes (the M36·4 pin — one additive `EmitAsync` call each,
  the per-recipient key, the `linkPath` to the content's detail page) + the
  **`MentionRenderer` Web render pass** (the post-render linkification — the
  M36·6 pin) + the **closed mention `kw-l` key set** × en/de/fr/da (the 28 keys
  — the M36·7 pin) + the **`Views/Notifications/Preferences.cshtml`** toggle
  change (the `post.mention` "coming soon" badge removed + the six new kinds'
  toggles rendered — the M36·7 pin) + the **pinned test pins** (the M36·8 test
  model).
- **Scope (out / named deferrals):** the **`@all` / `@everyone` broadcast**
  (a token that notifies **every** member of the audience — a future lane; it
  would need a new kind + a new recipient-universe shape + a settings posture),
  the **`Profile.Handle` / username system** (a `Profile` schema delta, an
  ADR 0004 §B migration — a future lane; v1 matches the **directory
  `DisplayName`**, M36·2), **email-address mentions** (a mention is a
  **directory display name**, not an address), **mention "threads" / a
  "mentioned me" inbox filter** (M6's inbox is a flat 50-row list with no
  per-kind sub-filter — a future lane), **mentions on the to-do / board / goal
  surfaces** (not just to-do **comments** — the body text of those surfaces is
  not part of the "content a resident composes to reach someone" shape), and the
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip until the milestone
  *ships* (U10 owns it). **M36 is the new last milestone** — the close flips
  M36 → done and promotes **no** successor (M36·10).
- **The `MentionTokens` + `IMentionService` shapes (locked by the design doc
  §2.2 pin).** The `MentionTokens` pure helper: `public static
  IReadOnlyList<string> ExtractTokens(string? body)` (the `UgcScnippets` /
  `KanbanStatuses` shape — a static, pure, no-dep helper — returns the
  **ordered, de-duplicated** list of `@<token>` occurrences; a token = `@` + a
  run of non-whitespace characters, **not** inside a Markdown code span / inline
  code / link / image; a lone `@` or a trailing `@` yields no token). The
  `IMentionService` seam: `Task<IReadOnlyCollection<string>>
  IMentionService.ResolveAsync(IReadOnlyCollection<string> tokens, string
  authorId, CancellationToken ct = default);` (the **resolved** resident ids —
  the `MentionService` composes the **frozen** `IDirectoryService` read seam to
  map each token to **exactly one** non-blocked resident matching the token's
  display name (case-insensitive), **excluding the author**, and **dropping** any
  token that matches **zero** (unknown) or **two or more** (ambiguous)
  residents — the M36-5 inert rule; **zero** `AccessAudit` rows — a read, not a
  decision, M36·5). The `MentionService` impl: `MentionService(IDirectoryService
  directory)` (the `AddTransient<IMentionService>` shape — the M13
  `IUsageAnalyticsService` / M25 `IStorageMetricsService` precedent; no
  `HttpClient`, no Wolverine, M36·9).
- **The per-surface emitter call shape (locked by the design doc §2.3 pin).**
  In each of the **seven** in-scope surfaces' publishing lane, **after** the
  content is stored and **before** the single `SaveChangesAsync`, the additive
  mention-emitter step: (1) `var tokens = MentionTokens.ExtractTokens(body)`;
  (2) `var recipients = await _mentions.ResolveAsync(tokens, authorId, ct)`;
  (3) `foreach (var recipientId in recipients) { await _notifications.EmitAsync(session, recipientId, kind: <surface's mention kind>, idempotencyKey: $"notification:{kind}:{contentId}:{recipientId}", body: UgcSnippets.Truncate(body), targetId: null, linkPath: $"<content's detail path>", ct: ct); }` — the frozen
  `EmitAsync` (M36·4), the frozen `UgcSnippets.Truncate` (the M6 U04
  precedent), the per-recipient key (M36·4). The **seven** surfaces + their
  mention kind + content path: **community post** (`post.mention`,
  `/posts/{postId}`), **community reply** (`post.mention`,
  `/posts/{postId}#reply-{replyId}`), **group post** (`group.post.mention`,
  `/groups/{groupId}/posts/{postId}`), **group post reply**
  (`group.post.mention`, `/groups/{groupId}/posts/{postId}#reply-{replyId}`),
  **page** (`page.mention`, `/pages/{pageId}`), **event** (`event.mention`,
  `/events/{eventId}`), **to-do comment** (`todo.mention`,
  `/projects/todos/{todoId}#comment-{commentId}`), **announcement**
  (`announcement.mention`, `/announcements/{announcementId}`), **message**
  (`message.mention`, `/messages/{conversationId}`). The frozen method
  signatures + audit rows of each surface's write lane are **untouched** (the
  additive step is *inside* the lane, not a new lane).
- **The `MentionRenderer` render-pass contract (locked by the design doc §2.4
  pin).** A plain-TS, no-dep, no-`any` (the ADR 0031 pin — the `expand-page.ts`
  / `calendar-fullscreen.ts` precedent) module `client/lib/mention-renderer.ts`
  (or a server-side pass in the `MarkdownRenderer` boundary — the design doc
  §2.4 resolves which): (1) takes the **rendered** HTML body (the output of
  `MarkdownRenderer.RenderHtml`) + the **resolved mention map** (the
  `IReadOnlyDictionary<string,string>` — display name → resident id — the
  controller resolved once via `IMentionService`); (2) scans the rendered body
  for `@<DisplayName>` text nodes (the `<DisplayName>` key is the **exact**
  resolved display name, case-insensitive) that are **not** inside an existing
  `<a>` / `<code>` / `<pre>` / `<img>` element and **not** already part of an
  `href`; (3) rewrites **only** those to
  `<a class="mention" href="/directory/{residentId}">@<DisplayName></a>`
  (the `class="mention"` is a **style hook** for `site.css` — the ADR 0031
  plain-TS pin; the `site.css` rule `.mention { color: var(--kw-mention);
  text-decoration: none; }` + `:hover { text-decoration: underline; }` is the
  M36·7 pin); (4) **idempotent** — running the pass twice over the same body
  yields the same output (it matches `@name` text, not `href`); (5) a **no-op**
  for every unresolved / ambiguous / blocked / self token (the `resolved map`
  only contains the resolved names, so the pass only rewrites those). **Zero**
  new npm package, **zero** build step (the M36·6 pin).
- **The `kw-l` key set is closed and four-language (28 keys).** The mention
  `kw-l` keys are authored by U06 and consumed by U04/U05's emitters (the
  `notification.{kind}.subject` / `notification.{kind}.body` templates) + U06's
  `Preferences.cshtml` change (the `notifications.kind.{kind}` +
  `notifications.preference.{kind}.label` toggles). The closed set (per kind):
  `notifications.kind.{kind}` / `notifications.preference.{kind}.label` /
  `notification.{kind}.subject` / `notification.{kind}.body`. The **seven**
  kinds: `post.mention` (the `kind` + `preference` labels already exist from
  M6's reserved set — only its `subject` / `body` templates are new),
  `group.post.mention`, `page.mention`, `event.mention`, `todo.mention`,
  `announcement.mention`, `message.mention`. Every key is present, non-empty, in
  **all four** languages (en/de/fr/da); the `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` closure pins it (the M36·7 pin). The `en`
  values are the source text (the ADR 0015 D1 `kw-l` provider-floor discipline);
  the `de` / `fr` / `da` values are U06's to author (the M36·7 four-language
  pin). The **existing** `kw-l` key sets (the M6 notification set, the M2
  directory set, the per-surface sets) are **untouched** (the M36·7 additive-only
  pin).
- **The test model (pinned in the design doc §2.6).** The `Core.Tests` pins
  (the `MentionsTests` class, in `tests/Kumunita.Core.Tests/Mentions/`):
  (a) `MentionTokens.ExtractTokens` over `@Ada @Ada @Bob` yields `[Ada, Bob]`
  (the de-dup pin, M36-1), (b) `MentionTokens.ExtractTokens` over a body with
  `@code` inside a code span yields **no** `code` token (the code-span-exclude
  pin, M36-1 / M36-2), (c) `MentionTokens.ExtractTokens` over a lone `@` or a
  trailing `@` yields **no** token (M36-1), (d) `IMentionService.ResolveAsync`
  over a token matching **exactly one** non-blocked resident (not the author)
  yields that resident's id (the M36·2 pin), (e) `IMentionService.ResolveAsync`
  over a token matching **zero** residents yields **no** id (the M36-5 inert
  pin), (f) `IMentionService.ResolveAsync` over a token matching **two or more**
  residents yields **no** id (the M36-5 ambiguous pin), (g)
  `IMentionService.ResolveAsync` over the **author's** own display name yields
  **no** id (the M36-5 self pin), (h) `IMentionService.ResolveAsync` writes
  **zero** `AccessAudit` rows (the M36·5 pin). The `Web.Tests` pins (the
  `MentionRendererTests` class, in `tests/Kumunita.Web.Tests/`): (a) the
  `MentionRenderer` pass over a rendered body + a resolved map rewrites the
  resolved `@Ada` to `<a class="mention" href="/directory/{id}">@Ada</a>` (the
  M36·6 / M36-3 pin), (b) the pass over a body with an **unresolved** `@Unknown`
  is a **no-op** (plain text — the M36-6 pin), (c) the pass is **idempotent**
  (running twice = same output — the M36-6 pin), (d) the pass **skips** an
  `@Ada` inside an existing `<a>` / `<code>` element (the M36-6 pin). The
  **per-surface emitter tests** (in the existing per-surface test classes —
  additive): each of the **seven** surfaces' publishing lane, when the body
  mentions a resolved resident, stores **one** `Notification` inbox row for the
  recipient (kind = the surface's mention kind, key =
  `notification:{kind}:{contentId}:{recipientId}`) + stages a best-effort email
  (the M36·4 / M36-2 pin); when the body mentions the **author** or an
  **ambiguous** / **unknown** name, stores **no** `Notification` row (the M36-5
  pin); a **draft** save stores **no** `Notification` row (the M36·8 pin).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core module + post/reply emitters + six-surface emitters + kw-l +
render pass + test pins + close**, sequenced. **U00** verifies the surface
(the frozen M6 `EmitAsync` / `NotificationKinds` / settings-toggle seams + the
frozen M2 `IDirectoryService` read seam + the frozen per-surface write lanes +
the frozen `MarkdownRenderer` + the ADR index — confirm **0165** is free) +
authors the handoff-note skeleton. **U01/U02** author the primary-tier design
doc (the 10 invariants M36·1–M36·10 + FACES M36-1–M36-9 + the `MentionTokens`
parser contract + the `IMentionService` seam + the per-surface emitter call
shape + the `MentionRenderer` render-pass contract + the closed mention `kw-l`
key set + the `Preferences.cshtml` toggle change + the pinned test names + the
acceptance gate + the drift guard) and draft **ADR 0165**. **U03** implements
the Core shared module (the `MentionTokens` pure parser + the `IMentionService`
seam + the `MentionService` impl + the `AddTransient<IMentionService>` DI
registration). **U04** wires the **post + reply** mention emitters (the
reserved `post.mention` kind going live — the `PostService` post-create +
reply-create lanes). **U05** wires the **six remaining surfaces'** mention
emitters (the `PageService` / `EventService` / `ProjectService.CreateTodoCommentAsync`
/ `AnnouncementService` / `MessagingService` + the **group post** create/reply
lanes). **U06** authors the **closed mention `kw-l` key set** × en/de/fr/da
(the 7 kinds × 4 keys) + the `Preferences.cshtml` toggle change (the
`post.mention` "coming soon" badge removed, the six new kinds' toggles
rendered). **U07** implements the **`MentionRenderer`** render pass (the
post-render linkification — plain-TS, no-dep, no-`any`, the ADR 0031 pin) +
wires it into the in-scope surfaces' render boundary + the `site.css` `.mention`
rule. **U08** implements the **pinned test pins** (the `MentionsTests` Core
class + the `MentionRendererTests` Web class + the per-surface emitter tests).
**U09** runs + records the acceptance gate. **U10** flips the close (the
six-member close flip; M36 is the **new last** milestone — no successor to
promote).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U10) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U10
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc**
  (`docs/design/m36-mentions-design.md`, U01/U02 author) — pins
  the invariants (the 10: M36·1–M36·10), the FACES
  (M36-1–M36-9), the `MentionTokens` parser contract, the `IMentionService`
  seam, the per-surface emitter call shape (the seven surfaces), the
  `MentionRenderer` render-pass contract, the closed mention `kw-l` key set
  (7 kinds × 4 keys), the `Preferences.cshtml` toggle change, the pinned test
  names, the acceptance gate, and the drift guard.
- **Secondary — this file**
  (`docs/plans-milestones/in-progress/plan-m36-mentions.md`) — the unit
  registry with each unit's deliverables and exit criteria. (The unit plans
  sit in `docs/plans-milestones/in-progress/` as `m36-u00.md` … `m36-u10.md`,
  and move to `docs/plans-milestones/done/m36/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m36-handoff-notes.md`) — one section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 4–8 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~5 files / ~500 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the
§pinned-test-names list; (4) never re-shapes the **frozen** M6 `EmitAsync`
/ `NotificationKinds` / settings-toggle seams (the M36·4 / M36·7
"reused / extended additively, not re-shaped" pin — the `EmitAsync` signature,
the `NotificationKinds.Known` order, and the `OptInKinds` table are
**untouched**; the seven mention kinds are **appended** to `Known`); (5) never
re-shapes the **frozen** M2 `IDirectoryService` read seam (the M36·2
"reused, not re-shape" pin — the `ListAsync` / `DetailAsync` signatures are
**untouched**; the `MentionService` composes them, it does not modify them);
(6) never re-shapes the **frozen** per-surface write lanes' method signatures
+ audit rows (the M36·4 "additive, not re-shape" pin — the mention-emitter step
is *inside* each lane, not a new lane; the `PostService` / `PageService` /
`EventService` / `ProjectService` / `AnnouncementService` / `MessagingService`
method signatures + their existing audit rows are **untouched**); (7) never
adds a new `AccessAction`, `AccessVia`, `Decide()` branch, `IAuditableResource`
adapter, or an **`AccessAudit` row** emitted *by* the mention emission (the
M36·5 pin — a mention is a *personal read*; the frozen surface's own audit row
for the content write is **untouched**); (8) never puts a Wolverine **handler**
or an outbound `HttpClient` in `Kumunita.Core` (the M36·9 pin — the
`MentionService` is a plain store-composing read; no `HttpClient` in Core,
ADR 0006-D); (9) never adds a **new** doc / doc-type surface / index to an
**existing** surface (the M36·1 pin — a mention is **derived**, not captured;
the M6 `Notifications` context + the M2 `UserInfo` / `Directory` context + the
per-surface contexts are **reused / extended additively**, not re-added); (10)
never adds a new npm package / a build step / a mention-parsing library (the
M36·6 pin — the `MentionRenderer` is plain-TS, no-dep, no-`any`, the ADR 0031
pin); (11) never re-author the **frozen** `MarkdownRenderer` (the M36·6
additive-only pin — the `MentionRenderer` pass runs *after* it, it does not
change it); (12) never notify the **author** of their own mention, or notify a
resident on an **ambiguous** / **unknown** / **blocked** token, or notify on a
**draft** save (the M36-5 / M36·8 pins — the `IMentionService.ResolveAsync`
inert rule + the publish-lane detection rule); (13) if entry reads reveal the
design doc is out of date, the unit pauses and records `## U<m> — Drift pause`
in the handoff note.

---

## Units (11 total: U00–U10)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the frozen M6 `EmitAsync` / `NotificationKinds`
  / settings-toggle seams + the frozen M2 `IDirectoryService` read seam + the
  frozen per-surface write lanes + the frozen `MarkdownRenderer` + the ADR
  index — confirm **0165** is free) and author the handoff-note skeleton (the
  "Milestone open" section). **No code, no build, no test.**
- **Entry reads:**
  `src/Kumunita.Core/Notifications/NotificationService.cs` (the M6 `EmitAsync`
  writer M36 reuses — the signature + the dedup + the quiet-hours gate + the
  sample-suppression gate — M36·4);
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the M6 closed kind
  vocabulary M36 extends — the `Known` ordered set + the `OptInKinds` table +
  the reserved `PostMention` constant — M36·3);
  `src/Kumunita.Core/UserInfo/DirectoryService.cs` (the M2 `IDirectoryService`
  read seam M36 composes — the `ListAsync` / `DetailAsync` signatures — M36·2);
  `src/Kumunita.Core/Posts/PostService.cs` (the M3 post + reply write lanes
  M36 extends additively — the `CreateReplyAsync` `EmitAsync` call site as the
  emitter template — M36·4);
  `src/Kumunita.Web/Security/MarkdownRenderer.cs` (the frozen renderer M36's
  `MentionRenderer` pass rides — the `RenderHtml` / `PlainTextPreview`
  signatures — M36·6);
  `src/Kumunita.Web/Views/Notifications/Preferences.cshtml` (the M6
  per-kind settings toggle M36 changes — the `post.mention` reserved/disabled
  special-case — M36·7);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M6 `kw-l`
  registry M36 extends — the `notification.post.reply.subject` / `.body` key
  shape + the `notifications.kind.post.mention` /
  `notifications.preference.post.mention.label` reserved keys — M36·7);
  `src/Kumunita.Web/Milestones.cs` (the `M36` row — confirm it is
  `StatusNext` + the **last** row — the M36·10 pin);
  `docs/adr/README.md` (the ADR index — confirm **0165** is free after the
  0164 row);
  `docs/plans-milestones/done/m35/plan-m35-kiosk-mode.md` (the M35 register —
  the structural template for this register + the "last milestone" framing M36
  shares, now re-applied to M36).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m36-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U10). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m35-handoff-notes.md` shape (the "Milestone open"
  section names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals, and the frozen base — the M6 `EmitAsync` /
  `NotificationKinds` / settings-toggle seams are **reused / extended
  additively** (M36·4 / M36·7), the M2 `IDirectoryService` read seam is
  **reused** (M36·2), the frozen per-surface write lanes are **extended
  additively** (M36·4), the frozen `MarkdownRenderer` is **reused** (M36·6),
  and M36 **creates** the one new `Kumunita.Core.Mentions` shared module
  (M36·1)).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the M6 `EmitAsync` / `NotificationKinds` / settings-toggle
  seams (the M36·4 / M36·7 reuse), (b) the M2 `IDirectoryService` read seam
  (the M36·2 reuse), (c) the frozen per-surface write lanes (the M36·4 additive
  extension), (d) the frozen `MarkdownRenderer` (the M36·6 reuse), (e) the
  `M36` row in `Milestones.cs` (the `StatusNext` **last** row — the M36·10
  pin), (f) the **ADR 0165** (the next free number after 0164 — the ADR index
  in `docs/adr/README.md` confirms 0164 is the current highest).
  Handoff note: a `## U00 — Kickoff verified` section with the current surface
  shape (the M6 `EmitAsync` signature, the M6 `NotificationKinds.Known` set +
  the reserved `PostMention` constant, the M2 `IDirectoryService` `ListAsync` /
  `DetailAsync` signatures, the M3 `PostService.CreateReplyAsync` `EmitAsync`
  call site, the frozen `MarkdownRenderer.RenderHtml` / `PlainTextPreview`
  signatures, the M6 `Preferences.cshtml` `reserved` special-case line, the ADR
  number [0165] + the precedent ADR list [0076 [the reserved `post.mention` +
  the `EmitAsync` seam], 0005 / 0015 [the `kw-l` registry], 0025 / 0031 / 0033
  [the frozen renderer], 0013 / 0039 / 0054 / 0067 / 0105 / 0012 [the frozen
  per-surface write lanes], 0023 [the frozen directory read seam], 0006 [zero
  new authorization surface], 0037 [publish-lane detection], 0110 [the
  `WhatsNew` registry — the required sixth close-flip member]]).

### U01 — Primary-tier design doc (Part 1): invariants + FACES + scope

- **Goal:** author the primary-tier design doc Part 1
  (`docs/design/m36-mentions-design.md`) — the **10 invariants**
  (M36·1–M36·10), the **9 FACES** (M36-1–M36-9), the **Scope** (In / Out, incl.
  the named deferrals), and the **frozen-base assumptions** (the M6 `EmitAsync`
  / `NotificationKinds` / settings-toggle seams + the M2 `IDirectoryService`
  read seam + the frozen per-surface write lanes + the frozen `MarkdownRenderer`
  — all **reused / extended additively**). **No code, no build, no test.**
- **Entry reads:**
  `docs/adr/0165-mentions.md` (the ADR — the D1–D10 decisions + the
  Consequences — the contract Part 1 fills);
  `docs/design/m35-kiosk-mode-design.md` (the M35 design doc — the structural
  template for this design doc: the Abstract, the three-tier contract, the
  Context, the Scope, the Invariants, the FACES, the Part 1 / Part 2 split);
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the M6 closed kind
  vocabulary — the `Known` ordered set + the `OptInKinds` table + the reserved
  `PostMention` constant — the M36·3 pin; U01 names the seven mention kinds +
  their `Known` position + their opt-OUT posture);
  `src/Kumunita.Core/Notifications/NotificationService.cs` (the M6 `EmitAsync`
  writer — the D4 / D5 / D7 contract + the per-target subscription gate + the
  quiet-hours gate + the sample-suppression gate — the M36·4 pin; U01 names the
  per-recipient idempotency key shape + the same-transaction pin);
  `src/Kumunita.Core/UserInfo/DirectoryService.cs` (the M2 `IDirectoryService`
  read seam — the `ListAsync` / `DetailAsync` signatures + the `VisibleProfile`
  projection — the M36·2 pin; U01 names the `IMentionService.ResolveAsync`
  composition over this seam);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M6 `kw-l`
  registry — the `notification.post.reply.subject` / `.body` key shape + the
  `notifications.kind.post.mention` /
  `notifications.preference.post.mention.label` reserved keys — the M36·7 pin;
  U01 names the 28 mention `kw-l` keys + the four-language closure).
- **Deliverables (1 file, new):**
  `docs/design/m36-mentions-design.md` — **Part 1 only** (the Abstract + the
  three-tier contract + the Context + the Scope (In / Out, incl. the named
  deferrals) + the **10 invariants** M36·1–M36·10 + the **9 FACES**
  M36-1–M36-9 + the frozen-base assumptions + the "Part 2 (U02)" stub section
  — the Part 2 seam / contract / test-pin / acceptance-gate / drift-guard
  sections are authored by U02).
- **Exit:** the design doc Part 1 is present. The 10 invariants (M36·1–M36·10)
  + the 9 FACES (M36-1–M36-9) are named + pinned (each FACE bound to one or
  more invariants). The Scope names the **In** set (the `MentionTokens` parser
  + the `IMentionService` seam + the seven mention kinds + the per-surface
  emitters + the `MentionRenderer` pass + the 28 `kw-l` keys + the
  `Preferences.cshtml` change + the test pins) and the **Out** deferrals (the
  `@all` / `@everyone` broadcast, the `Profile.Handle` system, email-address
  mentions, mention threads / inbox filter, board / goal body mentions, and the
  six-member close flip). Handoff note: a `## U01 — Design doc Part 1`
  section.

### U02 — Primary-tier design doc (Part 2): seams + contracts + test pins + ADR

- **Goal:** author the primary-tier design doc Part 2
  (`docs/design/m36-mentions-design.md`) — the **seams & contracts** (the
  exact `MentionTokens` parser contract, the `IMentionService` seam shape, the
  per-surface emitter call shape (the seven surfaces), the `MentionRenderer`
  render-pass contract, the closed mention `kw-l` key set (7 kinds × 4 keys,
  with the en floor + the de/fr/da baselines), the `Preferences.cshtml` toggle
  change, the **pinned test names**, the **three-test acceptance gate**, and
  the **drift guard**) and confirm **ADR 0165** (the next free number after
  0164 — U00 verified). **No code, no build, no test.**
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 1 — the 10 invariants + 9 FACES +
  the Scope + the frozen-base assumptions — the contract Part 2 fills);
  `src/Kumunita.Core/Notifications/UgcSnippets.cs` (the M6 UGC-snippet
  truncation helper the per-surface emitters reuse — the `Truncate` signature
  + the 200-char cap + the `…` ellipsis — the M36·4 pin);
  `src/Kumunita.Core/Posts/PostService.cs` (the M3 post + reply write lanes —
  the `CreateReplyAsync` `EmitAsync` call site as the emitter template — the
  M36·4 pin; U02 names the per-surface emitter call shape);
  `src/Kumunita.Web/Views/Notifications/Preferences.cshtml` (the M6 per-kind
  settings toggle — the `reserved` special-case line + the `Known` loop — the
  M36·7 pin; U02 names the toggle change);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M6 `kw-l`
  registry — the en/de/fr/da key shape — the M36·7 pin; U02 names the 28
  mention `kw-l` keys + the en floor + the de/fr/da baselines);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M35 terminal-state test
  pins — the "Roadmap_Covers_M0_Through_M35" + the "No_Milestone_Is_InProgress"
  pins — the M36·10 pin; U02 names the re-pin to "M36 is the single in-progress,
  and the last");
  `docs/plans-milestones/done/m35/plan-m35-kiosk-mode.md` (the M35 register —
  the Part 2 structural template: the seams & contracts, the pinned test names,
  the acceptance gate, the drift guard).
- **Deliverables (1 file, modified):**
  `docs/design/m36-mentions-design.md` — **Part 2** (the seams & contracts
  section + the pinned test names + the acceptance gate + the drift guard —
  appended to Part 1).
- **Exit:** the design doc Part 2 is present. The seams & contracts name (a)
  the `MentionTokens.ExtractTokens` signature + the token shape + the
  code-span-exclude rule (the M36·1 / M36·2 pin), (b) the
  `IMentionService.ResolveAsync` signature + the inert rule (the M36·2 / M36-5
  pin), (c) the per-surface emitter call shape (the seven surfaces + their
  mention kind + content path + the per-recipient key — the M36·4 pin), (d) the
  `MentionRenderer` render-pass contract (the idempotent + the skip-inside-`<a>`
  / `<code>` rules — the M36·6 pin), (e) the 28 mention `kw-l` keys + the en
  floor + the de/fr/da baselines (the M36·7 pin), (f) the `Preferences.cshtml`
  toggle change (the M36·7 pin), (g) the pinned test names (the `MentionsTests`
  Core class + the `MentionRendererTests` Web class + the per-surface emitter
  tests — the M36·8 pin), (h) the three-test acceptance gate, and (i) the
  drift guard. Handoff note: a `## U02 — Design doc Part 2` section.

### U03 — Core shared module: `MentionTokens` + `IMentionService` + `MentionService`

- **Goal:** implement the Core shared module `Kumunita.Core.Mentions` (the
  **`MentionTokens`** pure parser + the **`IMentionService`** seam + the
  **`MentionService`** impl + the **`AddTransient<IMentionService>`** DI
  registration). **One new namespace, one new file (the `MentionTokens` +
  `IMentionService` + `MentionService` in one `Mentions.cs` file, the
  `UgcSnippets` / `KanbanStatuses` shape), one DI registration.**
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the `MentionTokens.ExtractTokens`
  signature + the `IMentionService.ResolveAsync` signature + the inert rule —
  the M36·1 / M36·2 / M36-5 pins);
  `src/Kumunita.Core/Notifications/UgcSnippets.cs` (the `UgcSnippets` pure
  helper shape — the static, pure, no-dep, namespace `Kumunita.Core.Notifications`
  — the structural template for `MentionTokens`);
  `src/Kumunita.Core/UserInfo/DirectoryService.cs` (the M2 `IDirectoryService`
  read seam — the `ListAsync` / `DetailAsync` signatures + the `VisibleProfile`
  projection — the M36·2 pin; the `MentionService` composes `ListAsync`);
  `src/Kumunita.Core/DependencyInjection.cs` (the M6 `AddTransient<NotificationService>`
  DI registration shape — the additive precedent for
  `AddTransient<IMentionService>`);
  `src/Kumunita.Core/Kumunita.Core.csproj` (the project file — confirm the new
  `Mentions` namespace folder needs no csproj change, the ADR 0004 §B "no new
  doc-type surface" pin — M36·1).
- **Deliverables (2 files, 1 new + 1 modified):**
  `src/Kumunita.Core/Mentions/Mentions.cs` (new — the `MentionTokens` pure
  parser + the `IMentionService` seam + the `MentionService` impl, the
  `UgcSnippets` / `KanbanStatuses` shape — namespace `Kumunita.Core.Mentions`);
  `src/Kumunita.Core/DependencyInjection.cs` (modified — the
  `AddTransient<IMentionService, MentionService>(sp => new MentionService(sp.GetRequiredService<IDirectoryService>()))`
  additive registration, the M6 `AddTransient<NotificationService>` precedent).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `Mentions.cs` file
  is present with the `MentionTokens.ExtractTokens` (the `UgcSnippets` shape —
  the static, pure, no-dep helper) + the `IMentionService.ResolveAsync` (the
  `IMentionService` seam) + the `MentionService` impl (the `IDirectoryService`
  composition — no `HttpClient`, no Wolverine, M36·9). The DI registration is
  additive (the `AddTransient<IMentionService>` line). Handoff note: a
  `## U03 — Core module` section.

### U04 — Post + reply mention emitters (the reserved `post.mention` kind goes live)

- **Goal:** wire the **post + reply** mention emitters (the reserved
  `post.mention` kind going live — the `PostService` post-create + reply-create
  lanes). **The `PostService` post-create + reply-create lanes are extended
  additively** (the frozen method signatures + audit rows are **untouched**; the
  mention-emitter step is *inside* each lane, the M36·4 pin). **The
  `NotificationKinds` vocabulary gains the `post.mention` kind** (the reserved
  `PostMention` constant is already present — this unit **wires** it, the M36·3
  pin).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the per-surface emitter call
  shape + the per-recipient key + the `UgcSnippets.Truncate` reuse — the M36·4
  pin);
  `src/Kumunita.Core/Posts/PostService.cs` (the M3 post + reply write lanes —
  the `CreateReplyAsync` `EmitAsync` call site as the emitter template — the
  M36·4 pin);
  `src/Kumunita.Core/Mentions/Mentions.cs` (the `MentionTokens.ExtractTokens` +
  the `IMentionService.ResolveAsync` — the M36·1 / M36·2 pins);
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the M6 closed kind
  vocabulary — the `PostMention` constant — the M36·3 pin);
  `src/Kumunita.Core/Notifications/UgcSnippets.cs` (the `Truncate` helper —
  the M36·4 pin);
  `src/Kumunita.Core/DependencyInjection.cs` (the `PostService` DI registration
  — confirm the `_mentions` + `_notifications` constructor params are already
  present or additive).
- **Deliverables (1 file, modified):**
  `src/Kumunita.Core/Posts/PostService.cs` (modified — the post-create +
  reply-create lanes each gain the additive mention-emitter step: (1)
  `var tokens = MentionTokens.ExtractTokens(body)`, (2) `var recipients = await
  _mentions.ResolveAsync(tokens, authorId, ct)`, (3) `foreach (var recipientId
  in recipients) { await _notifications.EmitAsync(session, recipientId, kind:
  NotificationKinds.PostMention, idempotencyKey: $"notification:post.mention:{contentId}:{recipientId}", body: UgcSnippets.Truncate(body), targetId: null, linkPath: $"<content's detail path>", ct: ct); }` — the M36·4
  pin; the frozen method signatures + audit rows are **untouched**).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `PostService`
  post-create + reply-create lanes each emit a `post.mention` notification per
  resolved recipient (the M36·4 pin). The frozen method signatures + audit rows
  are **untouched** (the M36·4 additive pin). Handoff note: a `## U04 — Post +
  reply emitters` section.

### U05 — Six remaining surfaces' mention emitters

- **Goal:** wire the **six remaining surfaces'** mention emitters (the
  `PageService` / `EventService` / `ProjectService.CreateTodoCommentAsync` /
  `AnnouncementService` / `MessagingService` + the **group post** create/reply
  lanes). **The `NotificationKinds` vocabulary gains the six new kinds**
  (`group.post.mention` / `page.mention` / `event.mention` / `todo.mention` /
  `announcement.mention` / `message.mention` — the M36·3 pin). **Each surface's
  write lane is extended additively** (the frozen method signatures + audit
  rows are **untouched**; the mention-emitter step is *inside* each lane, the
  M36·4 pin).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the per-surface emitter call
  shape (the seven surfaces + their mention kind + content path) — the M36·4
  pin);
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the M6 closed kind
  vocabulary — the `Known` ordered set + the `OptInKinds` table — the M36·3
  pin; this unit adds the six new kinds to `Known`);
  `src/Kumunita.Core/Pages/PageService.cs` (the M2/Pages page write lane — the
  `EmitAsync` call site for the `PageChild` kind as the emitter template — the
  M36·4 pin);
  `src/Kumunita.Core/Events/EventService.cs` (the M4 event write lane — the
  `EmitAsync` call site for the `EventRsvp` kind as the emitter template — the
  M36·4 pin);
  `src/Kumunita.Core/Projects/ProjectService.cs` (the M5 to-do comment write
  lane — the `CreateTodoCommentAsync` method + the `EmitAsync` call site for
  the `TodoAssign` kind as the emitter template — the M36·4 pin);
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the M3b
  announcement write lane — the `EmitAsync` call site for the `Announcement`
  kind as the emitter template — the M36·4 pin);
  `src/Kumunita.Core/Messaging/MessagingService.cs` (the M9 message write lane
  — the `EmitAsync` call site for the `MessageNew` kind as the emitter template
  — the M36·4 pin);
  `src/Kumunita.Core/Posts/PostService.cs` (the M3 group post + group post
  reply write lanes — the `EmitAsync` call site for the `GroupPost` kind as the
  emitter template — the M36·4 pin).
- **Deliverables (7 files, modified):**
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (modified — the six
  new kinds `group.post.mention` / `page.mention` / `event.mention` /
  `todo.mention` / `announcement.mention` / `message.mention` added to `Known`
  after the existing resident-facing kinds and before the admin-lane kinds —
  the M36·3 pin; the `OptInKinds` table is **untouched** — the seven kinds are
  opt-OUT);
  `src/Kumunita.Core/Pages/PageService.cs` (modified — the page write lane
  gains the additive `page.mention` emitter step — the M36·4 pin);
  `src/Kumunita.Core/Events/EventService.cs` (modified — the event write lane
  gains the additive `event.mention` emitter step — the M36·4 pin);
  `src/Kumunita.Core/Projects/ProjectService.cs` (modified — the to-do comment
  write lane (`CreateTodoCommentAsync`) gains the additive `todo.mention`
  emitter step — the M36·4 pin);
  `src/Kumunita.Core/Announcements/AnnouncementService.cs` (modified — the
  announcement publish lane gains the additive `announcement.mention` emitter
  step — the M36·4 pin);
  `src/Kumunita.Core/Messaging/MessagingService.cs` (modified — the message
  write lane (`SendAsync`) gains the additive `message.mention` emitter step —
  the M36·4 pin);
  `src/Kumunita.Core/Posts/PostService.cs` (modified — the group post + group
  post reply write lanes each gain the additive `group.post.mention` emitter
  step — the M36·4 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The six new kinds are
  in `Known` (the M36·3 pin). Each of the six surfaces' write lane emits its
  mention-kind notification per resolved recipient (the M36·4 pin). The frozen
  method signatures + audit rows are **untouched** (the M36·4 additive pin).
  Handoff note: a `## U05 — Six-surface emitters` section.

### U06 — Closed mention `kw-l` key set × en/de/fr/da + `Preferences.cshtml` toggle change

- **Goal:** author the **closed mention `kw-l` key set** × en/de/fr/da (the 7
  kinds × 4 keys = 28 keys — the M36·7 pin) + the **`Preferences.cshtml`**
  toggle change (the `post.mention` "coming soon" badge removed + the six new
  kinds' toggles rendered — the M36·7 pin). **No code, no build, no test** (a
  `kw-l` + view unit — the M35-U06 / M34-U06 precedent).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the 28 mention `kw-l` keys +
  the en floor + the de/fr/da baselines + the `Preferences.cshtml` toggle
  change — the M36·7 pin);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M6 `kw-l`
  registry — the `notification.post.reply.subject` / `.body` key shape + the
  `notifications.kind.post.mention` /
  `notifications.preference.post.mention.label` reserved keys + the en/de/fr/da
  key shape — the M36·7 pin);
  `src/Kumunita.Web/Views/Notifications/Preferences.cshtml` (the M6 per-kind
  settings toggle — the `reserved` special-case line + the `Known` loop — the
  M36·7 pin);
  `src/Kumunita.Core/Notifications/NotificationKinds.cs` (the M6 closed kind
  vocabulary — the `Known` ordered set — the M36·3 pin; the seven mention kinds
  are the loop's input);
  `tests/Kumunita.Core.Tests/KwLRegistryConsistencyTests.cs` (the M6 `kw-l`
  parity test — the four-language closure pin — the M36·7 pin);
  `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` (the M6
  `kw-l` parity test — the four-language closure pin — the M36·7 pin).
- **Deliverables (2 files, modified):**
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modified — the 28
  mention `kw-l` keys added × en/de/fr/da: the `notifications.kind.{kind}` /
  `notifications.preference.{kind}.label` (the `post.mention` kind + preference
  labels already exist from M6's reserved set — only the six new kinds'
  `kind` + `preference` labels are new) / `notification.{kind}.subject` /
  `notification.{kind}.body` (the `post.mention` `subject` / `body` templates
  are new; the six new kinds' four keys are all new) — the M36·7 pin);
  `src/Kumunita.Web/Views/Notifications/Preferences.cshtml` (modified — the
  `post.mention` reserved/disabled special-case removed (the `reserved`
  variable + the `disabled` attribute + the "coming soon" badge are gone) + the
  six new kinds' toggles render (the `Known` loop already renders every kind —
  the change is purely the removal of the `reserved` special-case) — the M36·7
  pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 28 mention `kw-l`
  keys are present, non-empty, in **all four** languages (en/de/fr/da) — the
  M36·7 pin. The `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it. The `Preferences.cshtml` `post.mention` "coming soon" badge
  is removed + the six new kinds' toggles render — the M36·7 pin. Handoff note:
  a `## U06 — kw-l + toggle` section.

### U07 — `MentionRenderer` render pass + `site.css` `.mention` rule

- **Goal:** implement the **`MentionRenderer`** render pass (the post-render
  linkification — plain-TS, no-dep, no-`any`, the ADR 0031 pin — the M36·6 pin)
  + wire it into the in-scope surfaces' render boundary + the **`site.css`
  `.mention` rule** (the M36·7 pin). **The frozen `MarkdownRenderer` is
  unchanged** (the M36·6 additive-only pin).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the `MentionRenderer`
  render-pass contract (the idempotent + the skip-inside-`<a>` / `<code>`
  rules) — the M36·6 pin);
  `src/Kumunita.Web/Security/MarkdownRenderer.cs` (the frozen renderer — the
  `RenderHtml` / `PlainTextPreview` signatures — the M36·6 pin; the
  `MentionRenderer` pass runs *after* `RenderHtml`);
  `src/Kumunita.Web/client/lib/expand-page.ts` (the ADR 0031 plain-TS
  no-dep no-`any` IIFE shape — the structural template for `mention-renderer.ts`);
  `src/Kumunita.Web/client/lib/calendar-fullscreen.ts` (the ADR 0031 plain-TS
  no-dep no-`any` IIFE shape — the structural template for `mention-renderer.ts`);
  `src/Kumunita.Web/wwwroot/css/site.css` (the existing CSS rules — the
  `.mention` rule's insertion point);
  `src/Kumunita.Web/Program.cs` (the client-lib registration — confirm the new
  `mention-renderer.ts` is picked up by the existing tsc build, no new build
  step — the M36·6 pin).
- **Deliverables (3 files, 1 new + 2 modified):**
  `src/Kumunita.Web/client/lib/mention-renderer.ts` (new — the plain-TS,
  no-dep, no-`any` IIFE (the ADR 0031 pin — the `expand-page.ts` /
  `calendar-fullscreen.ts` precedent): (1) reads the **rendered** HTML body +
  the **resolved mention map** (the `data-mention-map` JSON attribute on the
  body element, the server-rendered map) from the server-rendered page; (2)
  scans the rendered body for `@<DisplayName>` text nodes that are **not**
  inside an existing `<a>` / `<code>` / `<pre>` / `<img>` element and **not**
  already part of an `href`; (3) rewrites **only** those to
  `<a class="mention" href="/directory/{residentId}">@<DisplayName></a>`;
  (4) **idempotent** — running the pass twice over the same body yields the
  same output; (5) a **no-op** for every unresolved / ambiguous / blocked /
  self token (the `resolved map` only contains the resolved names) — the M36·6
  pin; **zero** new npm package, **zero** build step);
  `src/Kumunita.Web/wwwroot/css/site.css` (modified — the `.mention { color:
  var(--kw-mention); text-decoration: none; }` + `:hover { text-decoration:
  underline; }` rules — the M36·7 pin);
  `src/Kumunita.Web/Views/Shared/_MentionRenderer.cshtml` (new — the shared
  partial that `@await`s the `MentionRenderer` pass over the body element —
  the in-scope surfaces' render boundary wire-in; **or** the pass is wired
  inline in each surface's view — the design doc §2.4 resolves which; the
  deliverable is the wire-in mechanism, ≤ ~5 files / ~500 LOC).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `mention-renderer.ts` file is present (the plain-TS, no-dep, no-`any` IIFE
  — the ADR 0031 pin). The `site.css` `.mention` rule is present (the M36·7
  pin). The frozen `MarkdownRenderer` is **unchanged** (the M36·6 additive-only
  pin). Handoff note: a `## U07 — MentionRenderer + CSS` section.

### U08 — Pinned test pins

- **Goal:** implement the **pinned test pins** (the `MentionsTests` Core class
  + the `MentionRendererTests` Web class + the per-surface emitter tests — the
  M36·8 test model). **No production code change** (a test-only unit — the
  M35-U08 / M34-U08 precedent).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the pinned test names + the
  three-test acceptance gate — the M36·8 pin);
  `src/Kumunita.Core/Mentions/Mentions.cs` (the `MentionTokens.ExtractTokens` +
  the `IMentionService.ResolveAsync` — the M36·1 / M36·2 pins; the
  `MentionsTests` class targets these);
  `src/Kumunita.Core/Posts/PostService.cs` (the M3 post + reply write lanes —
  the `post.mention` emitter — the per-surface emitter test target);
  `src/Kumunita.Web/client/lib/mention-renderer.ts` (the `MentionRenderer`
  render pass — the `MentionRendererTests` class target);
  `tests/Kumunita.Core.Tests/Kiosk/KioskServiceTests.cs` (the M35 Core test
  class — the structural template for `MentionsTests`);
  `tests/Kumunita.Web.Tests/KioskControllerTests.cs` (the M35 Web test class —
  the structural template for `MentionRendererTests`).
- **Deliverables (3 files, new / modified):**
  `tests/Kumunita.Core.Tests/Mentions/MentionsTests.cs` (new — the `MentionsTests`
  Core class: (a) `MentionTokens.ExtractTokens` over `@Ada @Ada @Bob` yields
  `[Ada, Bob]` (the de-dup pin, M36-1), (b) `MentionTokens.ExtractTokens` over a
  body with `@code` inside a code span yields **no** `code` token (the
  code-span-exclude pin, M36-1 / M36-2), (c) `MentionTokens.ExtractTokens` over a
  lone `@` or a trailing `@` yields **no** token (M36-1), (d)
  `IMentionService.ResolveAsync` over a token matching **exactly one**
  non-blocked resident (not the author) yields that resident's id (the M36·2
  pin), (e) `IMentionService.ResolveAsync` over a token matching **zero**
  residents yields **no** id (the M36-5 inert pin), (f)
  `IMentionService.ResolveAsync` over a token matching **two or more**
  residents yields **no** id (the M36-5 ambiguous pin), (g)
  `IMentionService.ResolveAsync` over the **author's** own display name yields
  **no** id (the M36-5 self pin), (h) `IMentionService.ResolveAsync` writes
  **zero** `AccessAudit` rows (the M36·5 pin));
  `tests/Kumunita.Web.Tests/MentionRendererTests.cs` (new — the
  `MentionRendererTests` Web class: (a) the `MentionRenderer` pass over a
  rendered body + a resolved map rewrites the resolved `@Ada` to
  `<a class="mention" href="/directory/{id}">@Ada</a>` (the M36·6 / M36-3
  pin), (b) the pass over a body with an **unresolved** `@Unknown` is a
  **no-op** (plain text — the M36-6 pin), (c) the pass is **idempotent**
  (running twice = same output — the M36-6 pin), (d) the pass **skips** an
  `@Ada` inside an existing `<a>` / `<code>` element (the M36-6 pin));
  the **per-surface emitter tests** (in the existing per-surface test classes —
  additive): each of the **seven** surfaces' publishing lane, when the body
  mentions a resolved resident, stores **one** `Notification` inbox row for the
  recipient (kind = the surface's mention kind, key =
  `notification:{kind}:{contentId}:{recipientId}`) + stages a best-effort email
  (the M36·4 / M36-2 pin); when the body mentions the **author** or an
  **ambiguous** / **unknown** name, stores **no** `Notification` row (the M36-5
  pin); a **draft** save stores **no** `Notification` row (the M36·8 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `MentionsTests`
  Core class (8 tests) + the `MentionRendererTests` Web class (4 tests) + the
  per-surface emitter tests (the seven surfaces × the three scenarios — the
  M36·8 pin) are present. The test names match the §pinned-test-names list
  exactly (the unit-series rule 3). Handoff note: a `## U08 — Test pins`
  section.

### U09 — Acceptance gate + record

- **Goal:** run + record the acceptance gate (the three-test acceptance gate
  from the design doc Part 2 — the M36·8 pin). **No code change** (a
  gate-recording unit — the M35-U09 / M34-U09 precedent).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 2 — the three-test acceptance
  gate — the M36·8 pin);
  `tests/Kumunita.Core.Tests/Mentions/MentionsTests.cs` (the `MentionsTests`
  Core class — the gate's Core half);
  `tests/Kumunita.Web.Tests/MentionRendererTests.cs` (the `MentionRendererTests`
  Web class — the gate's Web half);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M35 terminal-state test
  pins — the M36·10 pin; the gate confirms the M36 row is `StatusNext`).
- **Deliverables (0 files, new / modified):**
  none (a gate-recording unit).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green +
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  green + `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  green (the in-process `dotnet exec` path — the `AGENTS.md` test-runner quirk).
  The three-test acceptance gate is **PASS**. Handoff note: a `## U09 —
  Acceptance gate PASS` section with the test-run output.

### U10 — Close flip (the six-member contract)

- **Goal:** flip the close (the **six-member close flip** — the
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` contract — the M36·10 pin). **M36 is the
  new last milestone** — the close flips M36 → done and promotes **no**
  successor (the M36·10 pin).
- **Entry reads:**
  `docs/design/m36-mentions-design.md` (Part 1 + Part 2 — the M36·10 pin);
  `src/Kumunita.Web/Milestones.cs` (the `M36` row — the `StatusNext` **last**
  row — the M36·10 pin);
  `README.md` (the Roadmap section — the `M36` entry — the M36·10 pin);
  `docs/STATUS.md` (the status block — the `M36` entry — the M36·10 pin);
  `docs/ARCHITECTURE.md` (the M36 surface note — the M36·10 pin);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the M35 terminal-state test
  pins — the M36·10 pin; the re-pin to "M36 is the single in-progress, and the
  last");
  `src/Kumunita.Web/WhatsNew.cs` (the `WhatsNew` registry — the next `0.x.0`
  entry naming M36 + ADR 0165 — the M36·10 pin).
- **Deliverables (6 files, modified):**
  `src/Kumunita.Web/Milestones.cs` (modified — the `M36` row: `StatusNext` →
  `StatusDone`; **no** `M37` row added — the M36·10 pin);
  `README.md` (modified — the Roadmap section: the `M36` entry added (newest-
  first, after `M35`) + the `M35` entry's `StatusNext` → `StatusDone` — the
  M36·10 pin);
  `docs/STATUS.md` (modified — the status block: the `M36` entry added (the
  `M36` → done, no successor) — the M36·10 pin);
  `docs/ARCHITECTURE.md` (modified — the M36 surface note added (the
  `Kumunita.Core.Mentions` module + the seven mention kinds + the
  `MentionRenderer` pass — the M36·10 pin);
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (modified — the re-pin: the
  `Roadmap_Covers_M0_Through_M36_Plus_Named_Lanes_In_Order` test + the
  `M36_Is_The_Single_InProgress_Milestone` test (replacing the
  `No_Milestone_Is_InProgress` test) + the `Shipped_Milestones_Are_Marked_Done`
  test gains `M36` — the M36·10 pin);
  `src/Kumunita.Web/WhatsNew.cs` (modified — the next `0.x.0` entry added
  (newest-first) naming M36 + ADR 0165 — the M36·10 pin).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green +
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  green (the `MilestonesTests` re-pin — the M36·10 pin). The six-member close
  flip is complete. **M36 is the new last milestone** — the close flips M36 →
  done and promotes **no** successor (the M36·10 pin). Handoff note: a `## U10
  — Close flip` section.
