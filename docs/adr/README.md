# Architecture Decision Records

Lightweight records of significant technical decisions. Format: Status / Context /
Decision / Consequences. New decisions take the next number.

| ID   | Title                                        | Status   |
|------|----------------------------------------------|----------|
| 0001 | Core stack & identity model                  | Accepted |
| 0002 | Deployment topology: one instance per neighborhood | Accepted |
| 0003 | Roles & moderator scoping                    | Accepted |
| 0004 | Data persistence & schema evolution          | Accepted |
| 0005 | Multilingual support (UI & static pages)     | Accepted |
| 0006 | Module boundary contracts (Identity, UserInfo, Authorization) | Accepted |
| 0007 | Group management lane: owner ∪ GlobalAdmin for add and remove   | Accepted |
| 0008 | Member self-leave lane (owner excepted)   | Accepted |
| 0009 | Group description: resident-facing display + owner ∪ GlobalAdmin edit | Accepted |
| 0010 | Private groups: a membership/organizing unit, hidden from the audience pickers | Accepted |
| 0011 | Media & file storage: content-addressed local-volume bytes + a `mt` catalog | Accepted |
| 0012 | Community membership: mandatory communities + moderator-managed optional membership | Accepted |
| 0013 | Group posts: a membership-scoped group channel | Accepted |
| 0014 | Post edit lane: author-only (no moderator/admin branch) | Accepted |
| 0015 | UI view-localization mechanics (TagHelper + curated registry) | Accepted |
| 0016 | Group-post + reply edit lane: author-only (no moderator/admin branch) | Accepted |
| 0017 | Announcement edit lane: author-of-record ∪ GlobalAdmin (flat lane) | Accepted |
| 0018 | UGC authored-in language tag (posts, replies, announcements) | Accepted |
| 0019 | Timezone: platform default (admin) + per-resident override | Accepted |
| 0020 | Date & time format: platform default (admin) + per-resident override + custom | Accepted |
| 0021 | Translator role: delegate translation editing to non-admin residents | Accepted |
| 0022 | User-added post & reply translations (the lane ADR 0018 deferred) | Accepted |
| 0023 | Reply as a report target (extending the M3b report lane) | Accepted |
| 0024 | Author soft-delete lane (posts + replies, community + group) | Accepted |
| 0025 | Rich content: Markdown + content images | Accepted |
| 0026 | Group & community name/description translations | Accepted |
| 0027 | Post & reply translation display & swap: authored-in as a first-class variant + click-to-swap | Accepted |
| 0028 | Guardian controls: account-scope supervision of a child's account (suspend, communities/groups, invitation approval; no content reads) | Accepted |
| 0029 | Announcement user-added translations (GlobalAdmin/Translator any; community-Moderator their community's; add-only) | Accepted |
| 0030 | Role independence: elevated roles (GlobalAdmin / Moderator / Translator) are composable per resident, not mutually exclusive | Accepted |
| 0031 | WYSIWYG editor + toolbar over the RC Markdown lane (split-view live preview + Markdown-splice toolbar, `tsc`-only; Amends 0025) | Accepted |
| 0032 | Inline editor: the rendered pane is the default view (source hidden behind one toolbar toggle; pane stays visible as a split view when revealed, `tsc`-only, additive; Amends 0031) | Accepted |
| 0033 | WYSIWYG inline editing: the rendered pane is the editable surface (`contenteditable`); the Markdown source is a read-only mirror; the serializer is the inverse of `renderPreview`; the sanitizer strips paste to the pinned subset; `tsc`-only, no editor dependency, `Body` as Markdown, one renderer on the read path (Amends 0031 — the "hard non-negotiable" reversed by user approval 2026-09-15; Amends 0032 — the rendered-by-default view kept, now editable) | Accepted |
| 0034 | File attachments lane: downloads on post / reply / announcement (separate route + allowlist, `Content-Disposition: attachment`; reuses the ADR 0011 store; Amends 0025 + 0011) | Accepted |
| 0035 | Group-post media: the membership lane is the serve decision (the "single `Read` decision" for a group post's image/attachment is the ADR 0013 membership lane, not the audience lane; one shared routing seam `PostReadDecision`; Amends 0025 + 0034) | Accepted |
| 0036 | Community-visible audience: the default for new community posts (a new `Audience.Community` flag — a distinct grant, not a mode of the grant list; a 4th `Decide()` branch; `AccessVia.Community`; the composer seeds it `true`; the granular picker is hidden by default; group posts structurally unaffected; the C1 empty-audience-denies invariant still governs only the grant list) | Accepted |
| 0037 | Draft mode: saved-but-not-published, author-only (a first-class `IsDraft` state on community post / group post / announcement — excluded from every feed and the pinned list, detail gated to the author *before* the audience decision, an author-only idempotent publish seam that denies a `GlobalAdmin` non-author, no audit row, and a `/my/drafts` index for the author) | Accepted |
| 0038 | Guardian assignment: an existing guardian assigns a second guardian to a child's account (email-driven; one `IIdentityService` ADD + one `GuardianController` action + the Detail view's assign form; Amends 0028's G·4 non-decision for the existing-guardian case) | Accepted |
| 0039 | Pages: a hierarchical, audience-restricted, translatable knowledge tree (a `Page` + `PageTranslation` doc in a new `Kumunita.Core.Pages` context — reuses the `Audience` doc, the frozen `IAuthorizationService` via a `PageToAuditableResource` adapter, the ADR 0022/0027/0029 translation + display lane, the ADR 0025/0031/0033 WYSIWYG editor, the ADR 0025/0034 media idiom; a `MountPoint` string for UI slots; absorbs and retires the `LocalizedPage` static-page lane; Amends 0005 + 0018 + 0022 + 0026 + 0027 + 0037; the `PG` named lane) | Accepted |
| 0040 | Pages: system vs user (blog) kinds — a `PageKind` field (default `System`); the ADR 0039 §3.7 Moderator lane on pages is retired (system = GlobalAdmin only; blog = author ∪ GlobalAdmin; translate = GlobalAdmin ∪ Translator on either kind; a Moderator has no page standing at all); the two namespace guards + the blog-ownership guard; the (Kind, AuthorId)-scoped root-slug guard; the seeder's `system/` root + re-parent + kind normalization; the read-only, draft-gated `GET /blog` + `GET /blog/{userId}` per-resident feed; the kind-differentiated composer parent picker; Amends 0039 (retires §3.7's Moderator lane) + 0006; additive on 0004 + 0037; a follow-on refinement within the open `PG` lane — the roadmap stays unchanged | Accepted |
| 0041 | Page audience scope dropdown: "All residents" as a first-class flag (a new `Audience.AllResidents` flag — a distinct grant, not a mode of the grant list; a 4.5th `Decide()` branch between Community and Public; `AccessVia.Resident`; the page composer's community select retired in favor of a top-level `Scope` dropdown (All residents / a community / Individual access) with the granular editor revealed only for Individual; the post / announcement / group lanes structurally unaffected; the C1 empty-audience-denies invariant still governs only the grant list; Amends 0039 — the audience model on pages; additive on 0004 + 0036) | Accepted |
| 0042 | Bundled initial language pack: `de` / `fr` ship on first boot (a `SeedLanguages` seeder step — create-if-missing, idempotent, never overwriting a resident edit; Amends 0005 §B — the "every other language is community-provided" scope; additive on 0005 + 0015) | Accepted |
| 0043 | System pages shipped + discoverable (the four canonical pages — `/terms` `/help` `/privacy` `/conduct` — are seeded as `PageKind.System` rows on first boot; a read-only `GET` surface; the seeder is create-if-missing, idempotent, never overwrites a resident edit; Amends 0005 §B — the static-page coverage on a fresh instance; additive on 0039 + 0040 + 0042) | Accepted |
| 0044 | Tags: free author-set subject labels for posts and blog pages (a `Tag` + `TagTranslation` doc in a new `Kumunita.Core.Tags` context — the ADR 0011 shared-id-doc shape, referenced by `Post.TagIds` / `Page.TagIds` the way `ImageIds` references `MediaObject`; `Slug` is the language-neutral business key, `Name` display; attach = the object's existing edit standing (free), translate = the tag's creator ∪ GlobalAdmin; one access-scoped read seam serves the tag list + by-tag browse + autocomplete (all over "content the viewer may already read," so a tag never leaks a subject behind unread content); blog pages only, `System` pages out; Amends nothing — additive on 0004 + 0005 + 0013 + 0022 + 0026 + 0025 + 0040; the `TG` named lane) | Accepted |
| 0045 | Danish (`da`) pre-seeded, disabled by default (a `SeedLanguages` seeder step for `da` — create-if-missing, idempotent, `IsEnabled = false` on first boot; a GlobalAdmin toggles it on; Amends 0042 — the bundled initial pack; additive on 0042 + 0005 + 0015) | Accepted |
| 0046 | Default-language fallback: browser `Accept-Language` match (a new step in the ADR 0005 §C resolution chain — between the platform default and the hard-coded `en` floor, the viewer's `Accept-Language` header is matched against the enabled languages; the matched language becomes the default for that viewer; Amends 0005 §C — the resolution chain; additive on 0005 + 0042 + 0045) | Accepted |
| 0047 | Static-page localization: the four hard-coded routes (`/terms` `/help` `/privacy` `/conduct`) render the page body in the request's **effective language** (the matching `PageTranslation`, the `en` body as the floor — via a new `IPageService.ResolvePageAsync` one-read seam) + a **warm-boot page-translation backfill** (the four canonical pages' `de` / `fr` / `da` rows, create-if-missing, idempotent, never overwriting an admin edit or the `en` body); Amends 0043's "en-body by contract" Consequence (superseded — now the floor) + 0042 D1's "no warm-reseed mechanism" (narrow, scoped exception — the four pages' translation rows only); additive on 0046 + 0039 + 0040 + 0042 + 0045 + 0015; the `SL` named lane | Accepted |
| 0048 | Edit + remove lanes for user-added translations: lifts the add-only pin on every user-added translation surface (posts, replies, announcements, group/community name/description, pages) — an **update** and a **remove** lane per row, same parent, same language, same standing matrix the add lane already fixed; Amends 0022 + 0026 + 0027 + 0029 (the add-only shape); additive on 0021 + 0039 | Accepted |
| 0049 | Default-visible variant: the viewer's current language (when a translation exists) — a UGC post/reply/announcement/page that already carries a human-added translation in the viewer's resolved language renders that variant first (the authored-in variant demoted to a one-click swap, never removed); Amends 0005 §C (the "never automatic" display clause) + 0027 (the "always default-visible" authored-in clause); additive on 0046 (reuses the `Accept-Language` resolution chain, does not change it) | Accepted |
