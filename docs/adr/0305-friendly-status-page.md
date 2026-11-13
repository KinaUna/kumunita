---
id: adr-0305
title: "Friendly status page: one localized, nav-keeping /status/{code} page for HTML page requests — fetch/JSON and machine endpoints keep their bare responses"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0154-production-error-handling.md
  - docs/adr/0155-issue-submission-escalation.md
  - docs/archive/lanes/improve-ux/plan-improve-ux.md
supersedes: []
superseded_by: []
lane: improve-ux
updated: 2026-11-13
---

# ADR 0305 — Friendly status page: one localized, nav-keeping /status/{code} page for HTML page requests

Status: Accepted
Date: 2026-11-13
Builds on: [0154](0154-production-error-handling.md) (production error
handling — the report-an-issue affordance on the 500 error page),
[0155](0155-issue-submission-escalation.md) (issue submission — the
`/issues/new` target the 500 affordance links to). Amends: nothing — no
controller, service or middleware contract is replaced; this ADR adds the
status-page surface (R01) and its HTML-only re-execute gate (R02) above the
existing error pipeline.

> **Abstract:** IMPROVE-UX finding **E6** — ~550 bare status returns in
> resident controllers render as **blank 404s/403s** with no nav, no
> next step: a dead end for anyone following a link that has gone stale.
> This ADR records decision **D2** of
> [plan-improve-ux](../archive/lanes/improve-ux/plan-improve-ux.md):
> **one friendly status page site-wide** at `/status/{code}` — localized,
> keeping the site nav, offering Back / Home / Search — wired via a
> status-code re-execute **for HTML page requests only**, so fetch/JSON,
> `/health`, `.ics`, media, kiosk and `/api/` responses keep their bare
> status codes. **Who is it for:** the developer touching the error
> pipeline or adding a status code, and the SEC reviewer (the 403/404
> boundary text). **When to read the rest:** before adding a bare
> `NotFound()`/`Forbid()` return, or reviewing the status-page surface.

## Context

The platform's ~550 status-return call sites (bare `NotFound()`, `Forbid()`,
`BadRequest()` …) were written before any status-page surface existed. A
resident who follows a link to deleted content — or a notification whose
target has since been removed — lands on a **blank body**: no navigation,
no home link, no search, no explanation. IMPROVE-UX E6 rates this a dead
end (principle P3, the "dead end" anti-pattern): the platform's own
roadmap promise (a feedback loop that comes back) breaks at exactly the
moment content disappears.

What already exists and is kept: the 500 pipeline — `UseExceptionHandler("/Home/Error")`
with the report-an-issue affordance (ADR [0154](0154-production-error-handling.md),
linking to `/issues/new`, ADR [0155](0155-issue-submission-escalation.md)) —
and the per-endpoint rate limiting. What is **missing**: any rendered page
for the bodyless 4xx responses that make up the bulk of the resident's
failure surface.

The forces:

- **Breadth.** The call sites are ~550 and spread across every resident
  controller; the fix must be **central**, not per-action.
- **Machine clients.** The same pipeline serves fetch/JSON (`/api/`),
  `/health`, `.ics` calendars, `/media/` files and the kiosk's machine
  endpoints. A blanket status page would corrupt their contracts (a JSON
  caller receiving an HTML body; a calendar client receiving markup).
- **The framework gap.** `UseStatusCodePagesWithReExecute("/status/{0}")`
  re-executes on **every** failing response — the .NET 10
  `StatusCodePagesMiddleware` has no `Accept`/XHR/path gate (its
  `StatusCodePagesOptions` carries no `ShouldTransformResponse`). The
  HTML-only decision has to live in the handler itself.

## Decision

- **D1 — One page, seven codes.** A single status page renders at
  `GET /status/{code}` (`StatusController`) for **400, 403, 404, 413, 415,
  429, 500** — the codes the platform actually emits for resident
  requests. Every code gets a **localized** title + message
  (`status.{code}.title` / `status.{code}.message`, en/de/fr/da) plus the
  same three affordances: **Back** (the same-origin `Referer`, else `/`),
  **Home** (`/`), and **Search** (`/search`). An unsupported code falls
  back to the 500 text. The page **keeps the site nav** (it renders in the
  normal layout — the resident is still *on* the platform, not lost
  outside it).

- **D2 — HTML page requests only; the machine endpoints keep their bare
  responses.** The re-execute is gated by `StatusPageGate.ReExecuteHtmlOnly`
  (the `UseStatusCodePages` handler in `Program.cs`, registered **before**
  `UseRouting` so the re-executed `/status/{code}` passes routing → auth →
  MVC to the `StatusController`). The gate re-executes **iff** the
  `Accept` header contains `text/html` **and** the request is not XHR
  (`X-Requested-With` set) **and** the path is not one of the excluded
  machine surfaces: `/status` (no re-execute of a status page), `/health`,
  `/media`, `/kiosk/slide`, `/settings/kiosk/items`, `/api`, or a `*.ics`
  path. Everything else — fetch/JSON, API clients, health probes, calendar
  feeds, media downloads — keeps the **original status code and the bare
  (empty) body**. The re-execute preserves the status line: the framework
  invokes the handler only for bodyless 4xx/5xx responses, and the handler
  writes the body without touching the status code.

- **D3 — The 500 path is unchanged.** A **thrown exception** still lands on
  `UseExceptionHandler("/Home/Error")` (ADR [0154](0154-production-error-handling.md))
  — that page keeps its report-an-issue affordance (ADR
  [0155](0155-issue-submission-escalation.md), `/issues/new`). The status
  page's 500 text covers the rarer **bodyless** 5xx response; the two
  surfaces are complementary, not competing.

- **D4 — Rejected alternatives.** (a) *Per-action views* — hundreds of
  edits across the resident controllers, and a new error surface to keep in
  sync per call site; rejected on breadth. (b) A *"deleted on {date}"*
  variant of the 404 (which post, when) — deferred; it needs a content-tomb
  read the lane has not built, and is recorded as *Not in this lane* in the
  plan. (c) Letting the framework's blanket
  `UseStatusCodePagesWithReExecute` handle it — rejected because it cannot
  gate on `Accept`/XHR/path and would rewrite the machine endpoints'
  responses.

## Consequences

- A **new controller + view + gate** (`StatusController`,
  `Views/Status/Index.cshtml`, `Middleware/StatusPageGate`) join the Web
  project; the gate is the **only** place the HTML-vs-machine decision
  lives, so a new machine endpoint opts out by joining the excluded-path
  list.
- **~550 call sites are unchanged** — they keep returning bare status
  codes; the resident-facing rendering is now central.
- The **500-with-report** affordance (ADR [0154](0154-production-error-handling.md),
  ADR [0155](0155-issue-submission-escalation.md)) is intact on the
  exception path; the status page's 500 text is the bodyless-5xx
  complement.
- **Tests** pin both sides of the gate: `StatusControllerTests` (the
  rendered codes, the fallback, the nav) and `StatusPageGateTests`
  (HTML GET → re-executes with nav; fetch/JSON + excluded paths → bare
  body, status codes unchanged).
- **Delivery:** R01 (the page) + R02 (the HTML-only gate + tests) shipped
  together on `development` ahead of this ADR; the journey X02 walked live
  (J3: a stale link now lands on the 404 page with nav, while a JSON call
  still gets an empty 404 body).
