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
(ADR 0064); **M5 is done** — projects (ADR 0067); **M6 is done** — notifications (ADR 0076); **M7 is done** — pagination & filtering (the `HasMore` signal on every paged seam, the `FeedResult.Total` correction, the `PagedViewModel` + `_Pager` partial, the pager wired into the 11 list surfaces, the D7 filter-reset pin; ADR 0090); **M8 is done** — search (one `/search` surface over the four resident content surfaces on the frozen authorization seams, zero schema change; ADR 0091); **M9 is done** — messaging (1:1 resident messaging, admin-toggleable, off by default; ADR 0105); **M10 is done** — PWA and responsive design (an installable app shell + offline shell + a single 360-px responsive pass with two pinned a11y floors; a GET-only allowlist service worker that never caches signed-in content; zero Core change; ADR 0107); **M11 is done** — portability (import/export; the whole-instance `*.kumunita` archive, the fail-closed restore of a versioned archive, the GlobalAdmin admin plane with one `AccessAudit` row per action, zero new authorization surface; ADR 0108); **M12 is done** — iCal (the per-event file `GET /events/{id}.ics` + the subscription feed `GET /events.ics`, both `[Authorize]` on the frozen `IEventService` read seams; the hand-written BCL-only `IcsWriter` over already-authorized rows on the RFC 5545 pinned subset; two `kw-l` affordances × en/de/fr/da; zero schema, zero new dependency, zero new authorization surface; ADR 0112); **M13 is done** — logging & analytics (the BCL-only file sink, the `UsageEvent` capture lane, the `/admin/analytics` GlobalAdmin surface, the 365-day retention tick; zero new authorization surface, zero per-account rendered data, zero third-party telemetry; ADR 0114); **M14 is done** — integration of Events and Projects (the `TodoItem.EventId?` association — a feed filter / association, never a gate — C-M14·1, the `ListTodosForEventAsync` reverse read seam + the `SetTodoEventAsync` set-event write lane, the both-direction display links — the to-do detail's event chip + the event detail's "linked to-dos" section, both access-scoped + dangling-safe — C-M14·2, and the VTODO iCal surface — the `TodoIcsWriter` pure emitter + the two `GET /projects/todos.ics` / `GET /projects/todos/{id}.ics` routes, no `RRULE` / recurrence — C-M14·6; zero new authorization surface — C-M14·4 — the frozen `AccessAction` set unchanged; ADR 0115); **M15 is done** — translation bulk (import/export, review & extend translations as a batch, new languages without going through them one at a time; the bulk read/export/import + batch-editor seams ride the frozen localization lanes, zero schema change, zero new authorization surface; ADR 0116); **M16 is done** — inventory (check-out / check-in shared, community-owned, or private resources — equipment, sports-team clothes, books — where they are, and optionally who uses them how much; ADR 0117); **M17 is done** — bookmarks (save posts, events, todos, etc.; ADR 0118); **M18 is done** — repeating / recurring events (ADR 0119); **M19 is done** — guest accounts (limited-privilege accounts for consultants, coaches, teachers, speakers, entertainers, etc.; admins set the limits on what a guest may access and when; ADR 0120). **M20 is in progress** — notification quiet times (per-resident quiet schedules — allowed/blocked hours of day and days of week on the M6 notification lane, plus an admin-set check cadence for pending notifications; ADR 0121); M19 remains the last **shipped** milestone; the still-planned horizon is **M21** — document management (a shared repository for official documents, contracts, etc., with per-document access controls), and **M22** — onboarding (a guided walk-through for new-user account setup).
