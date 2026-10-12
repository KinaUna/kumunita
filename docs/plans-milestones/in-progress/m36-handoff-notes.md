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
