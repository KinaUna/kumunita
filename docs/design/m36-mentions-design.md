# M36 — @mentions (design doc)

> **Abstract:** This design settles the **"reach a neighbor by name"** question
> M1–M35 never answered. Every content surface shipped so far — posts, group
> posts, pages, events, to-dos, announcements, messages — is a *broadcast to an
> audience*, but none of them can **address one resident by name**. M6 (ADR
> 0076) shipped the shared-awareness lane (the inbox + the email + a closed,
> code-owned kind vocabulary) and deliberately reserved **one** kind,
> `post.mention`, as **"a follow-on lane"** — that lane is M36. A resident
> composes a post, a reply, a page, an event, a to-do comment, a group post, an
> announcement, or a direct message and types `@Ada can you bring the projector?`
> — the platform **resolves `Ada` against the frozen M2 directory**, **notifies
> Ada** on that surface (a mention notification kind, one per in-scope surface,
> riding the frozen M6 `EmitAsync`), and **renders `@Ada` as a link** to Ada's
> directory page (a post-render pass over the frozen `MarkdownRenderer`). Out of
> scope: `@all` / `@everyone` broadcast, a `Profile.Handle` / username system,
> email-address mentions, mention "threads", and the six-member close flip
> (U10's). **M36 is the new last milestone** — the close promotes **no**
> successor.
>
> **Milestone M36 — @mentions.** The README / `Milestones.cs` line, verbatim:
> "**@mentions** — a cross-surface `@<resident>` reference token: type a
> resident's directory display name after an `@`, the platform resolves it
> against the resident directory and notifies the mentioned resident on that
> surface (posts, replies, pages, events, to-dos, group posts, announcements,
> messages) and renders the name as a link to their directory page." M36 is a
> **new capability on the cross-surface notification surface** (M36·1): it
> **creates** the one new shared Core module `Kumunita.Core.Mentions` (the
> **pure** `MentionTokens` parser + the `IMentionService` read seam over the
> **frozen** `IDirectoryService` — the M34 "derived, not captured" shape) and
> **extends** the **frozen** per-surface write lanes (the `PostService` /
> `PageService` / `EventService` / `ProjectService` / `AnnouncementService` /
> `MessagingService` create-and-persist lanes — **additive**, signatures + audit
> rows untouched) with a mention-emitter step, and **adds** a Web **render
> pass** (`MentionRenderer`) over the **frozen** `MarkdownRenderer` (M36·6). It
> **extends** the **closed `NotificationKinds` vocabulary** with the **seven**
> mention kinds (the reserved `post.mention` goes live + six new siblings —
> M36·3) and the **closed `kw-l` registry** with the seven kinds' full key set
> × en/de/fr/da (M36·7). It adds **no** new bounded context (M36·1), **no** new
> doc / doc-type surface (a mention is *derived*, M36·1), **no** new
> `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource`
> adapter (M36·5), and **no** EF migration.
>
> **The one thing that separates M36 from the M34 "derived, not captured"
> precedent it otherwise mirrors — one shared module + per-surface emitters
> (M36·1).** M34 added **no** new bounded context — it extended the existing
> M13 `Usage` surface with a derived read. M36 is the **same shape** on the
> *notification* surface, but with one twist: the **detection** (who was
> mentioned) is a **shared** concern (one parser + one directory resolver —
> `Kumunita.Core.Mentions`) while the **emission** (the per-surface
> `EmitAsync` call) is **local** to each surface's existing write lane (the
> `PostService.CreateReplyAsync` precedent, the `MessagingService.SendAsync`
> precedent). So M36 **creates** one new shared module (`Kumunita.Core.Mentions`
> — the `MentionTokens` pure helper + the `IMentionService` seam) and
> **reuses** the **frozen** directory read seam (`IDirectoryService`, the M2
> surface) to resolve display names → resident ids (M36·2), the **frozen** M6
> `EmitAsync` writer to store the inbox row + stage the email (M36·4), and the
> **frozen** `MarkdownRenderer` (the `MentionRenderer` pass runs *after* it,
> M36·6). A mention is **not** stored — it is **derived** (the `@token` + the
> directory match), so M36 adds **no** new doc, **no** new doc-type surface, and
> **no** new index (M36·1).
>
> **Three-tier contract.** This file is the **primary** tier of M36's contract:
> it pins the **invariants (the 10: M36·1–M36·10)**, the **FACES
> (M36-1–M36-9)**, and (in Part 2, U02) the `MentionTokens` parser contract, the
> `IMentionService` seam shape, the per-surface emitter call shape, the
> `MentionRenderer` render-pass contract, the closed mention `kw-l` key set, the
> pinned test names, the acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/in-progress/plan-m36-mentions.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m36-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the three
> disagree, **this file wins for the pinned shapes**; the register wins for
> *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **ten invariants** (M36·1–M36·10), and the **nine
> FACES** (M36-1–M36-9), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `MentionTokens` parser
> contract, the `IMentionService` seam shape, the per-surface emitter call shape
> (the seven surfaces), the `MentionRenderer` render-pass contract, the closed
> mention `kw-l` key set (7 kinds × 4 keys), the `Preferences.cshtml` toggle
> change, the pinned test names, the three-test acceptance gate, the drift
> guard) + **ADR 0165** (the next free number after 0164 — U00 verified).
>
> **The frozen base (reused, unchanged).** M36 is built **on top of** the
> existing M6 notification lane + the M2 directory + the frozen per-surface
> write lanes + the frozen renderer: the **M6 `EmitAsync` writer**
> (`NotificationService.EmitAsync` — the inbox row + the best-effort email +
> the dedup + the quiet-hours gate + the sample-suppression gate — M6 D4/D5/D7),
> the **M6 closed `NotificationKinds` vocabulary** (the `Known` ordered set +
> the `OptInKinds` table — M36 adds the seven mention kinds, M36·3), the **M6
> per-kind settings toggle** (the `Notifications/Preferences` surface —
> M36 enables `post.mention` + adds the six new kinds' toggles, M36·7), the
> **M2 `IDirectoryService` read seam** (`ListAsync` / `DetailAsync` — the
> non-blocked resident catalog, **reused verbatim** — M36·2), the **frozen
> per-surface write lanes** (the `PostService` / `PageService` / `EventService`
> / `ProjectService` / `AnnouncementService` / `MessagingService`
> create-and-persist lanes — the `EmitAsync` call site, **additive** — M36·4),
> and the **frozen `MarkdownRenderer`** (`RenderHtml` / `PlainTextPreview` — the
> one-renderer pin, **untouched** — M36·6). It also rides the ADR 0006-D
> dependency-direction rule (**`Kumunita.Core` references no ASP.NET HTTP
> types** — the `IMentionService` is a plain store-composing read, no
> `HttpClient` in Core, M36·9), the ADR 0031 tsc-only discipline (the
> `MentionRenderer` is a plain-tsc, no-dep, no-`any` module, M36·6), and the
> ADR 0015 D1 `kw-l` provider-floor discipline (the `en` value is the source
> text, M36·7). All of these still bind **unchanged**. M36 adds **one new
> shared module** (`Kumunita.Core.Mentions` — the `MentionTokens` pure helper +
> the `IMentionService` seam, M36·1), **one Web render pass**
> (`MentionRenderer`, M36·6), the **seven mention kinds** on the closed
> `NotificationKinds` vocabulary (M36·3), and the **closed mention `kw-l` key
> set** × en/de/fr/da (M36·7) — but it adds **no** new bounded context (M36·1),
> **no** new doc / doc-type surface (M36·1), **no** new `AccessAction` /
> `AccessVia` / `Decide()` branch / `IAuditableResource` adapter (M36·5), **no**
> new npm package / build step (M36·6), and **no** EF migration (a mention is
> derived, M36·1). It is **additive**.
>
> **The one thing every unit must respect:** M36 is **one shared module +
> per-surface emitters** (M36·1) on the notification surface. The detection
> (`MentionTokens` + `IMentionService.ResolveAsync`) is **shared and pure** —
> it parses `@<DisplayName>` tokens and resolves them against the **frozen**
> `IDirectoryService` read seam (M36·2), **excluding the author**, and treats
> **ambiguous / blocked / unknown** tokens as **inert** (M36-5). The emission is
> **per-surface and local** — each frozen write lane calls the **frozen** M6
> `EmitAsync` (M36·4) with the per-recipient idempotency key
> `notification:{kind}:{contentId}:{recipientId}` (M36·4) **in the same
> transaction** as the content write (M36·8). The render is a **post-render
> pass** (`MentionRenderer`) over the **frozen** `MarkdownRenderer` — it links
> **only** the resolved, non-blocked, non-ambiguous `@<DisplayName>` tokens, and
> leaves every other `@` **plain text** (M36·6, M36-6). The **seven** mention
> kinds are **resident-facing, opt-OUT** (M36·3), and the **`kw-l`** closed set
> is **four-language** (M36·7). Core stays **HTTP-free + Wolverine-free**
> (M36·9, ADR 0006-D). **M36 is the new last milestone** — the close flips M36 →
> done and promotes **no** successor (M36·10). After M36, a resident types
> `@Ada` in any content surface and Ada is told — and everyone sees the name
> link to Ada's page.

## Context

M1–M35 shipped the full **resident** surface: a directory, posts, group posts,
pages, events, projects, boards, to-dos, announcements, messaging, and the M6
shared-awareness lane (the inbox + the email + the closed kind vocabulary). The
platform already has the pieces a mention needs; it lacks only the **detection**
(who was mentioned) and the **render pass** (link the name):

- **The notification lane** is all there: the M6 `EmitAsync` writer (the inbox
  row + the best-effort email + the dedup + the quiet-hours gate (ADR 0121) +
  the sample-suppression gate (ADR 0078)), the closed `NotificationKinds`
  vocabulary, and the per-kind settings toggle. M36 **reuses** these verbatim —
  it does not add a new notification mechanism (M36·4).
- **The directory** is all there: the M2 `IDirectoryService` read seam
  (`ListAsync` / `DetailAsync`) already lists every **non-blocked** resident
  with their `DisplayName`. M36 **reuses** this to resolve display names →
  resident ids — it does not add a new identity surface, a `Profile.Handle`, or
  a username system (M36·2).
- **The per-surface write lanes** are all there: each surface's create-and-persist
  lane already stores the content and (for the M6-kinds) already calls `EmitAsync`
  in the same transaction (the `PostService.CreateReplyAsync` + the
  `MessagingService.SendAsync` precedent). M36 **extends** each with one mention
  emission — the frozen method signatures + audit rows are **untouched** (M36·4).
- **The renderer** is all there: the **one** `MarkdownRenderer` (the ADR 0025 /
  0031 / 0033 one-renderer pin) already renders every content surface's Markdown
  body to HTML. M36's `MentionRenderer` runs a **post-render pass** over that
  output — the one-renderer pin is **preserved** (M36·6).

What is missing is the **glue**: a way to **detect** `@<resident>` in a body,
**resolve** it to a directory resident (excluding the author, and treating
ambiguous / blocked tokens as inert), **notify** that resident on the surface,
and **render** the name as a link. M36 **is** that glue. It adds **one shared
module** (`Kumunita.Core.Mentions` — the pure `MentionTokens` parser + the
`IMentionService` read seam), **one Web render pass** (`MentionRenderer`),
**seven mention kinds** on the closed `NotificationKinds` vocabulary, and the
**closed `kw-l` key set** — and **reuses** everything else.

**Critically, M36 stores no mention.** This is the **one invariant that
separates M36 from a "capture the mention" design**. A mention is **derived** —
the `@token` in the body + the directory match — not a stored `Mention` row. So
M36 adds **no** new doc, **no** new doc-type surface, **no** new index (M36·1),
adds **no** new `AccessAction` / `AccessVia` / `Decide()` branch /
`IAuditableResource` adapter (M36·5), and the audit story is unchanged: the
mention **emission** emits **zero** `AccessAudit` rows (a personal read, M36·5),
and the **render pass** is a display transform (the directory detail surface —
the link's target — runs its own frozen `ContactVisibility` decision on click).
M36 **reuses** the M6 `EmitAsync` / `NotificationKinds` / settings-toggle seams
(untouched, M36·4 / M36·7), the M2 `IDirectoryService` read seam (untouched,
M36·2), the frozen per-surface write lanes (extended additively, M36·4), and the
frozen `MarkdownRenderer` (untouched, M36·6). M36 does **not** re-shape any
existing seam, does **not** add a `Profile` field, does **not** change
`MarkdownRenderer.RenderHtml`, does **not** add an `@all` / `@everyone` broadcast,
and does **not** add a username system (the named deferrals, §Scope).

## Scope

**In (M36's closed surface):**

- the **`MentionTokens` pure parser** (M36·1) — a static helper (the
  `UgcSnippets` / `KanbanStatuses` shape — no docs, no deps) that scans a body
  string for `@<token>` occurrences and returns the **ordered, de-duplicated**
  list of candidate tokens (a token = `@` + a run of non-whitespace characters,
  **not** inside a Markdown code span / inline code / link / image — the
  `PlainTextPreview` "plain-text view" pin);
- the **`IMentionService` read seam** (M36·1 / M36·2) —
  `ResolveAsync(IReadOnlyCollection<string> tokens, string authorId,
  CancellationToken) → IReadOnlyCollection<string>` (the **resolved** resident
  ids — the `IMentionService` composes the **frozen** `IDirectoryService` read
  seam to map each token to **exactly one** non-blocked resident **matching the
  token's display name** (case-insensitive), **excluding the author**, and
  **dropping** any token that matches **zero** (unknown) or **two or more**
  (ambiguous) residents — the M36-5 inert rule; **zero** `AccessAudit` rows, a
  read, not a decision, M36·5);
- the **`MentionService` impl** (the `AddTransient<IMentionService>` shape, the
  M13 `IUsageAnalyticsService` / M25 `IStorageMetricsService` precedent, M36·9)
  — the `IDirectoryService` read composition; no `HttpClient`, no Wolverine
  (M36·9);
- the **seven mention kinds** on the closed `NotificationKinds` vocabulary
  (M36·3) — `post.mention` (the reserved kind, now **wired**) + the **six new**
  `group.post.mention` / `page.mention` / `event.mention` / `todo.mention` /
  `announcement.mention` / `message.mention` — all **resident-facing, opt-OUT**
  (**not** in `NotificationKinds.OptInKinds`), appended to `Known` after the
  existing resident-facing kinds and before the admin-lane kinds (M36·3);
- the **per-surface mention emitters** (M36·4) — one additive `EmitAsync` call
  in each of the **seven** in-scope surfaces' publishing lane (the
  `PostService` post-create + reply-create lanes, the `PageService`
  create/update-published lane, the `EventService` create/publish lane, the
  `ProjectService.CreateTodoCommentAsync` lane, the `AnnouncementService`
  publish lane, the `MessagingService.SendAsync` lane, and the **group
  post**'s create/reply lane) — each resolving mentions via `IMentionService`
  and emitting **one** `EmitAsync` per resolved recipient (the kind per surface,
  the per-recipient idempotency key `notification:{kind}:{contentId}:{recipientId}`,
  a `linkPath` to the content's detail page) **in the same transaction** as the
  content write (M36·8); the frozen method signatures + audit rows are
  **untouched**;
- the **`MentionRenderer` Web render pass** (M36·6) — a plain-tsc, no-dep,
  no-`any` module (the ADR 0031 pin — the `expand-page.ts` / `calendar-fullscreen.ts`
  precedent) **or** a server-side pass in the `MarkdownRenderer` boundary, that
  runs **after** `MarkdownRenderer.RenderHtml` and rewrites **only** the
  `@<DisplayName>` tokens that resolve (via the same `IMentionService`
  resolution the controller ran once) to a non-blocked, non-ambiguous,
  non-author resident into `<a href="/directory/{id}">@<DisplayName></a>` —
  **idempotent** (it matches `@name` text, not `href`), **never** inside an
  existing `<a>` / `<code>` element, and a **no-op** for every unresolved /
  ambiguous / blocked / self token (M36-6);
- the **closed mention `kw-l` key set** × en/de/fr/da (M36·7) — for each of the
  **seven** mention kinds, the **four** keys `notifications.kind.{kind}` /
  `notifications.preference.{kind}.label` / `notification.{kind}.subject` /
  `notification.{kind}.body` (the `post.mention` kind + preference labels
  already exist from M6's reserved set — only its `subject` / `body` templates
  are new) — **seven kinds × four keys = 28 keys × four languages**, pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`; and
- the **`Views/Notifications/Preferences.cshtml`** toggle change (M36·7) — the
  `post.mention` reserved/disabled special-case (the "coming soon" badge) is
  **removed** so the toggle is a normal opt-OUT switch, and the **six** new
  kinds' toggles render (the `Known` loop already renders every kind — the
  change is purely the removal of the `reserved` special-case); and
- the **pinned test pins** (the `MentionsTests` Core class + the
  `MentionRendererTests` Web class + the per-surface emitter tests — the
  M35·12 12-test model, M36·8).

**Out (named deferrals):**

- **`@all` / `@everyone` broadcast** — a token that notifies **every** member of
  the audience (a group post's members, an event's audience). In v1 a mention
  resolves to **exactly one** resident; a broadcast mention is a **future lane**
  (it would need a new kind + a new recipient-universe shape, and a settings
  posture — the ADR 0084 per-target subscription shape).
- **A `Profile.Handle` / username system** — v1 matches against the **directory
  `DisplayName`** (the M2 surface, **untouched**); a stable, unique `@handle`
  (a `Profile` schema delta, an ADR 0004 §B migration) is a **future lane**
  (it would let a rename never break an old mention, and would make ambiguous
  names impossible — both of which v1 sidesteps by the inert rule, M36-5).
- **Email-address mentions** (`@ada@example.com`) — out of scope; a mention is
  a **directory display name**, not an address.
- **Mention "threads" / "mentioned me" inbox filter** — M6's inbox is a flat
  50-row list with no per-kind sub-filter (a named M6 deferral); a
  "mentioned me" filter is a **future lane** (the `post.mention` + six sibling
  kinds already give the per-kind badge data it would need).
- **Mentions on the to-do / board / goal surfaces** (not just to-do **comments**)
  — v1 mentions ride the **comment** lane (`ProjectService.CreateTodoCommentAsync`)
  and the **board/goal** body text is out of scope (those surfaces' body fields
  are not part of the "content a resident composes to reach someone" shape).
- **The six-member close flip** until the milestone *ships* (U10 owns it — the
  `WhatsNew.cs` next-`0.x.0` entry is appended by U10, not by an earlier unit,
  M36·10). And **M36 is the new last milestone** — the close flips `M36` from
  `StatusNext` to `StatusDone` and promotes **no** successor (M36·10).

## Invariants (pinned for M36)

The **10 invariants** below are the **frozen** ids for M36 — once written, they
are the cross-reference for every subsequent unit (U02–U10). The FACES
(M36-1–M36-9) in the next section are each **bound** to one or more of these
invariants; the drift-guard (authored by U02 in Part 2) re-pins them, and the
close unit (U10) re-pins them again. M36 carries **10 invariants** — the ids
run **M36·1 through M36·10** (contiguous).

- **M36·1 — M36 is one new shared Core module `Kumunita.Core.Mentions`, not a
  new bounded context and not a new doc.** M36 **creates** the one new shared
  module (the `MentionTokens` pure helper + the `IMentionService` /
  `MentionService` — the M34 "derived, not captured" shape, the M13
  `IUsageAnalyticsService` / M25 `IStorageMetricsService` read-seam precedent)
  and **reuses** the **frozen** M6 `EmitAsync` / `NotificationKinds` /
  settings-toggle seams (M36·4 / M36·7), the **frozen** M2 `IDirectoryService`
  read seam (M36·2), the **frozen** per-surface write lanes (extended
  additively, M36·4), and the **frozen** `MarkdownRenderer` (M36·6). M36 adds
  **no** new bounded context, **no** new doc, **no** new doc-type surface, **no**
  new index, and **no** EF migration (a mention is **derived**, not captured).
- **M36·2 — The reference token is `@` + a resident's directory `DisplayName`,
  matched against the frozen M2 directory. No new `Profile` field, no new
  handle.** The mention is `@<DisplayName>` — the resident's display name
  exactly as it appears in the M2 directory (`/directory`). Detection resolves
  each `@` token against the directory's **non-blocked** residents (the frozen
  `IDirectoryService` read) and collects the matching resident ids,
  **excluding the author**. A token that matches **no** resident is **inert**;
  a token that matches **two or more** residents is **also inert** (ambiguous =
  no mention, never a guess — M36-5).
- **M36·3 — The `post.mention` reserved kind is now wired, and six sibling kinds
  join the closed vocabulary — one per in-scope surface.** ADR 0076 D2
  reserved `post.mention` as the follow-on lane; M36 wires it on the **community
  post + reply** surface and adds **six** new `NotificationKinds` members, one
  per other in-scope surface: `group.post.mention` (group post + reply),
  `page.mention`, `event.mention`, `todo.mention` (to-do comment),
  `announcement.mention`, and `message.mention`. All seven are
  **resident-facing, opt-OUT** (in the `post.reply` / `group.post` posture —
  **not** in `NotificationKinds.OptInKinds`; a fresh install with no preference
  row fires them). They trail the existing resident-facing kinds in
  `NotificationKinds.Known` (the closed, ordered set). The **admin-lane** kinds
  (`account.signup` / `account.verified`) stay a separate lane (ADR 0077).
- **M36·4 — The mention idempotency key is per-recipient:
  `notification:{kind}:{contentId}:{recipientId}`.** ADR 0076 D4 pins the key
  shape as `notification:{kind}:{stable-source-id}` and dedups on the **key
  alone** — the single-recipient kinds are safe because the recipient is
  deterministic. A mention is **multi-recipient**: one body may mention several
  residents. The recipient id is therefore **part of the key** (`:{recipientId}`)
  so each mentionee stores their own inbox row + gets their own email, while a
  re-emission of the same (content, recipient) pair is still a no-op (ADR 0076
  D4 / F10 preserved). The `EmitAsync` call is in the **same transaction** as
  the domain write (ADR 0076 D5 / C3 — one `SaveChangesAsync` commits the content
  + the inbox row + the outbox envelope atomically).
- **M36·5 — A mention is a *personal read*, not an `AccessAction` decision.
  Zero new `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuditableResource` adapter.** A mention notification is **the mentioned
  resident's own data** (the ADR 0076 D3 shape, verbatim): the emitter stores an
  inbox row + stages a best-effort email for the recipient — no
  `IAuthorizationService` call, **no** `AccessAudit` row emitted *by* the mention
  emission (F7). The **render pass** links `@Ada` to `/directory/{adaId}` — that
  link rides the **frozen** M2 directory gate on click (the detail surface runs
  its own `ContactVisibility` decision), so a mention link **grants no access**
  to the source content beyond what the resident could already see (the M8 search
  / M12 iCal "a link is not a grant" posture).
- **M36·6 — The render pass is a no-op for an unresolved `@` token, and a
  mention is a post-render pass — not a new Markdown construct.** The one-renderer
  pin (ADR 0025 / 0031 / 0033) is preserved: `MarkdownRenderer.RenderHtml` is
  **unchanged**. The mention is a **post-render pass** (`MentionRenderer` in the
  Web boundary) that runs *after* the Markdown → HTML render, scanning the
  rendered body for `@<DisplayName>` tokens that resolve to a non-blocked,
  non-ambiguous, non-author resident and rewriting **only those** to
  `<a href="/directory/{id}">@<DisplayName></a>`. Every other `@` (a lone `@`, an
  `@` inside a code span, an unresolved name, an ambiguous name) stays **plain
  text** — the pass never invents a link and never changes Markdown grammar. The
  pass is **idempotent** (it matches `@name` text, not `href`), so an
  already-linked mention is not double-wrapped, and **never** rewrites inside an
  existing `<a>` / `<code>` element.
- **M36·7 — The `kw-l` closed registry gains the seven kinds' full key set ×
  en/de/fr/da; the `post.mention` toggle goes from reserved/disabled to
  enabled.** Every new user-visible string M36 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M6 U07 / M34·11 closed-set precedent).
  Per mention kind: the **four** keys `notifications.kind.{kind}`,
  `notifications.preference.{kind}.label`, `notification.{kind}.subject`,
  `notification.{kind}.body`. The `Views/Notifications/Preferences.cshtml`
  **removes** the `post.mention` reserved/disabled special-case (the "coming
  soon" badge) so the toggle is a normal opt-OUT switch, and the six new kinds'
  toggles render.
- **M36·8 — Detection runs on the **publishing** write lane, not on draft-save;
  a mention in a draft body is inert until the content is made visible.** ADR
  0037 drafts are author-only, excluded from every feed, and a resident may never
  publish a draft. A mention in a **draft** body must **not** notify (a resident
  is not told about content that may never be seen by anyone else). So M36's
  emitters run in the lane that makes the content **visible** — the **publish**
  seam for the draft-capable surfaces (post / group post / announcement — the
  ADR 0037 author-only idempotent publish lane) and the **create** lane for the
  non-draft surfaces (page / event / to-do comment / message).
- **M36·9 — Core stays HTTP-free + Wolverine-free; the shared module is a plain
  store-composing read over the frozen directory seam.** `Kumunita.Core.Mentions`
  (the `MentionTokens` pure helper + the `IMentionService` / `MentionService`) is
  a plain store-composing service (the `AddTransient<IMentionService>` shape, the
  M13 `IUsageAnalyticsService` / M25 `IStorageMetricsService` precedent).
  `IMentionService.ResolveAsync` composes the **frozen** `IDirectoryService` read
  seam to map display names → resident ids (a read, not a decision). There is
  **no** `HttpClient` in Core (ADR 0006-D), **no** Wolverine handler, **no**
  outbound channel — the only outbound channel is the frozen M6 email lane
  (`IMailerStage`, already present).
- **M36·10 — The six-member close flip lands in the final unit; M36 is the new
  last milestone.** The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md`
  / `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in the final
  unit (the M35-U10 / M34-U08 precedent). The `WhatsNew.cs` registry gains one
  new entry (newest-first, the next `0.x.0` row) naming M36 + ADR 0165. **M36 is
  the new last milestone** — the close flips `M36` from `StatusNext` to
  `StatusDone` and promotes **no** successor (the `MilestonesTests` pin changes
  from "no in-progress milestone" (M35's terminal state) to "`M36` is the single
  in-progress, and the last" — the M34-U08 "last milestone, no successor"
  framing, re-applied).

## FACES (pinned for M36)

The **nine FACES** below are the **observable** behaviors of M36 — each is **bound**
to one or more of the invariants (M36·1–M36·10). The U08 test pins (Part 2) cover
each FACE; the drift-guard (Part 2) re-pins the invariant ↔ FACE binding.

- **M36-1 — The `@` token is parsed, de-duplicated, and code-span-excluded
  (M36·1 / M36·2).** A body `@Ada @Ada @Bob` yields the ordered, de-duplicated
  token list `[Ada, Bob]`. A body with `@code` inside a Markdown code span /
  inline code / link / image yields **no** `code` token (the `MentionTokens`
  parser skips code spans, the `PlainTextPreview` pin). A body with a lone `@`
  or `@` at the very end (no following non-whitespace) yields **no** token.
- **M36-2 — A resolved mention notifies the resident on that surface (M36·3 /
  M36·4).** Publishing a post / reply / page / event / to-do comment / group
  post / announcement / message whose body mentions a non-blocked, non-ambiguous,
  non-author resident stores **one** `Notification` inbox row **per mentionee**
  (kind = the surface's mention kind) + stages **one** best-effort email (unless
  the recipient is quiet / has the kind disabled / is a sample account in
  production — the frozen M6 gates, ADR 0078 / 0121). The idempotency key is
  `notification:{kind}:{contentId}:{recipientId}` (M36·4).
- **M36-3 — The render pass links a resolved mention to the directory page
  (M36·5 / M36·6).** The rendered body of any in-scope surface shows `@Ada` as
  `<a href="/directory/{adaId}">@Ada</a>` **only** when `Ada` resolves to
  exactly one non-blocked, non-ambiguous, non-author resident; every other `@`
  is plain text (M36-6).
- **M36-4 — An ambiguous, blocked, unknown, or self mention is inert (M36·2 /
  M36·5 / M36-5).** A body mentioning a display name shared by two residents, a
  blocked resident, a name no resident has, or the author's own name stores
  **no** `Notification` row and renders **no** link — plain text only. The
  author is **never** notified of their own mention.
- **M36-5 — The seven kinds' settings toggles render + the `post.mention` badge
  is removed (M36·7).** The `Notifications/Preferences` surface shows **all**
  seven mention kinds as normal opt-OUT switches (the `post.mention`
  "coming soon" badge is gone) and the resident's per-kind choice gates the
  **email** (not the inbox — the M6 D7 shape), as it does for every other kind.
- **M36-6 — The render pass is idempotent and never rewrites inside an
  existing link / code (M36·6).** Running the `MentionRenderer` pass twice over
  the same body yields the same output (it matches `@name` text, not `href`).
  An `@Ada` inside an existing `<a>` or `<code>` element is **not** rewritten
  (the pass skips text inside those elements).
- **M36-7 — A mention in a draft body is inert (M36·8).** Saving a post / group
  post / announcement **as a draft** with a mention stores **no** `Notification`
  row (the ADR 0037 draft is author-only); the mention **does** notify when the
  author later publishes the draft. A page / event / to-do comment / message has
  no draft state — the mention notifies on create.
- **M36-8 — Zero new authorization surface; the mention link grants no access
  (M36·5).** M36 adds **no** `AccessAction`, **no** `AccessVia`, **no**
  `Decide()` branch, and **no** `IAuditableResource` adapter. Clicking a
  mention link lands on the resident's **directory detail** surface, which runs
  its own frozen `ContactVisibility` decision (the M2 gate) — the mention never
  bypasses or grants access to the source content.
- **M36-9 — The `kw-l` closed set is four-language and the parity tests pass
  (M36·7).** Every one of the 28 mention `kw-l` keys is present, **non-empty,
  in all four** languages (en/de/fr/da); `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` green (a missing / empty / single-language
  key fails the build).

## Part 2 (U02) — the seams & contracts

*(Authored by U02. This section — the exact `MentionTokens` parser contract, the
`IMentionService` seam shape, the per-surface emitter call shape (the seven
surfaces), the `MentionRenderer` render-pass contract, the closed mention `kw-l`
key set (7 kinds × 4 keys, with the en floor + the de/fr/da baselines), the
`Preferences.cshtml` toggle change, the pinned test names, the three-test
acceptance gate, and the drift guard — is authored by U02 on top of this
Part 1. Part 1's invariants (M36·1–M36·10) + FACES (M36-1–M36-9) are the
contract Part 2 fills.)*
