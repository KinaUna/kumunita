# Status (detailed)

The detailed status report referenced from the README's `## Status`
section.

**M3 complete** (`docs/design/m3-posts-design.md`,
`docs/design/m3b-moderation.md`). The live loop now includes M1 identity /
groups / delegation / authorization, M2 directory + profile editor + groups,
M3 posts + audience-scoped feeds + the M3b moderation lane
(file/assign/resolve + `PostStatus`) — all server-rendered MVC + Razor, with
a durable Wolverine outbox, retry + dead-letter, and the `/health` degraded
gate. The deployable surface has grown but the topology hasn't changed:
`Kumunita.slnx` (`Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`, `Kumunita.Web.Tests`),
multi-stage Docker image, the versioned schema boot (Marten feature + EF
Identity migration + first-boot seeder), a `/health` liveness probe, and a
`Coolify`-based deploy (Coolify `app` + dedicated Postgres 18, image parity
with `dev-db-init` + `docker-compose.yml`: **18**).
Profile **avatars** have also landed — the reference lane of the content-addressed
local-volume media store (ADR 0011, its own design doc); it adds a second restore
surface next to the Postgres dump (OPS.md §4/§5).
**Multilingual** has landed as **two lanes**. `ML` (ADR 0005) shipped the
**seam**: the admin-managed language catalog + instance default, the per-request
translation provider (preference cookie → default → `en`, per-string/per-page
fallback), the `/admin/languages` surface, and the `/terms` + `/help`
static-page routes. `ML-UI` (ADR 0015) then wired the **live UI**: every in-scope
view resolves per request, a seeded `en` floor is always present, the admin
edits the **closed** key list at `/admin/languages`, a signed-out visitor can
pick a language at `/language`, and `/about` is a static page.
**Rich content** (`RC`, ADR 0025) has landed — Markdown bodies + in-content
images on posts, replies, announcements & static pages: one escape-first
renderer extension (`![alt](src)` under a stricter `src` allowlist), the
`GET /content-image/{id}` serving route (decision-deferred to the owning
resource, Deny → 404 not 403, one `Read` audit row), the `POST /content-image`
upload lane (ADR 0011's boundary verbatim), and the composer control on all
four surfaces.
**GU is done** — guardian controls, the account-scope supervision of a child's
account (ADR 0028); **PG is done** — the hierarchical, audience-restricted,
translatable pages tree (ADR 0039); **M4 is done** — events, RSVPs, reminders
(ADR 0054); **EV-DWM is done** — events calendar day/week/month views
(ADR 0064); **M5 is done** — projects (ADR 0067); **M6 is done** — notifications (ADR 0076); **M7 is done** — pagination & filtering (the `HasMore` signal on every paged seam, the `FeedResult.Total` correction, the `PagedViewModel` + `_Pager` partial, the pager wired into the 11 list surfaces, the D7 filter-reset pin; ADR 0090); **M8 is done** — search (one `/search` surface over the four resident content surfaces on the frozen authorization seams, zero schema change; ADR 0091); **next is M9** — PWA and responsive design. Then M10–M13 (portability, iCal, logging & analytics, Events+Projects integration — see the "Roadmap" below).
