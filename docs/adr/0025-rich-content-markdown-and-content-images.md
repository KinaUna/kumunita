# ADR 0025 — Rich content: Markdown bodies + in-content images

Status: Accepted
Date: 2026-09-14
Amends: 0011 (the "follow-on lanes" non-decision is resolved for the
post/reply/announcement/about lane specifically — group logos, badge icons,
and other follow-on consumers remain future lanes, as ADR 0011 said)

## Context

Two design questions were open in the platform:

**(a) Which renderer for UGC bodies?** Posts, replies, and announcements
are raw plain text today — stored verbatim, rendered with
`whitespace-pre-line` or raw `@Model.Body`, with no formatting at all. The
platform already ships an escape-first Markdown renderer
(`MarkdownRenderer`, `Kumunita.Web.Security`) that ADR 0005 A promised as
the "single page engine" for static pages — but every UGC surface
deliberately ignores it. Adopting a second library (`Markdig` /
`MarkdownSharp`) would add a dependency for a subset (headings, lists,
inline emphasis, links) that the existing ~200-line renderer already
covers — minus images, which is the one extension this ADR makes.

**(b) How are content images referenced and served?** Images exist nowhere
but the profile avatar (ADR 0011's single consumer). ADR 0011's "Not
decided here" section explicitly named "post attachments" as a follow-on
lane, gated on the owning resource's audience, and did not register any
seams, routes, or doc types for it.

The constraints the choice has to honor (all pre-existing, not new):

- **The XSS bar** — the renderer's escape-first construction (R·2); the
  image `src` is an additional exfiltration surface a remote `src` would
  open (tracking pixel, session-cookie-bearing fetch).
- **The audit model** — every audience-restricted read is logged
  (SECURITY.md §3, C-MED·2); a public path bypasses both.
- **`Core` stays HTTP-free** (ADR 0006-D) — the serving/upload boundary
  is Web-layer; the Core seam is a plain id-based read.
- **Marten owns the domain documents** (ADR 0004 §B) — any new field
  rides the additive path (ADR 0004 §B.1).
- **Lean + Boring, one database** (README principles, ADR 0002) — no new
  rendering library for a subset the existing renderer already covers;
  no new `AccessAction`, no new `IMediaStore` method.

## Decision

**(a) `MarkdownRenderer` is the one body renderer.** The existing
escape-first renderer is extended with `![alt](src)` image support under
a `src` allowlist **stricter** than the existing link whitelist: only
`/content-image/{1–128 lowercase hex}` (the platform serving route shape)
or a schemeless relative path is accepted; every scheme (`http:`,
`https:`, `data:`, `javascript:`) is rejected and the whole
`![alt](src)` renders as plain escaped text (the link-rejection
precedent, verbatim). A second rendering library is **not** adopted: the
existing renderer is small, auditable, and its escape-first construction
is the security story; the subset it lacks (tables, footnotes) is the
subset a one-neighborhood platform does not need.

**(b) Content images are `MediaObject` docs referenced by `ImageIds` on
the owning document.** Four additive fields — `Post.ImageIds`,
`PostReply.ImageIds`, `LocalizedPage.ImageIds`, `Announcement.ImageIds`
(all `IReadOnlyList<string>` of `MediaObject.Id` values, ADR 0004 §B.1
additive) — are populated **server-side** by parsing the body's
`/content-image/{id}` links on every create/edit write lane (the Web
layer's job; Core stays HTTP-free AND body-parse-free). Serving is
`GET /content-image/{id}` through the owning resource's **single**
`Read` decision (UGC owner — the avatar route's C-MED·1/2/3/6 idiom
verbatim) or directly (platform-page owner — public by construction).
**Every miss is a 404** — store-miss, owner-miss (orphan), and Deny —
so orphan bytes are inert and existence never leaks. **No new
`AccessAction`, no new `IMediaStore` seam, no new signature on the frozen
`IAuthorizationService`** — the route copies the existing avatar idiom.
Upload is `POST /content-image`, ADR 0011's boundary verbatim (allowlist,
5 MiB cap, guards-before-write 400/413/415, one `PutAsync`).

This resolves ADR 0011's "follow-on lanes" non-decision **for the
post/reply/announcement/about lane specifically**. Group logos, badge
icons, and any other follow-on consumer remain future lanes gated on
their own owning resource, as ADR 0011 already said.

## Consequences

- **The four `ImageIds` ADDs** ride the ADR 0004 §B.1 additive path
  (delta-detected, idempotent, no seed reset — **zero migrations**).
  `Body` stays a `string` of Markdown source on every document; existing
  plain-text content re-renders under the Markdown rules (plain text is
  valid Markdown — strictly better than today's raw text).
- **The serving route's 404-orphan posture** is the C-MED·7 "orphan file
  is inert" rule extended to "orphan doc is inert **and unfindable**" —
  there is no GlobalAdmin branch to find an ownerless image.
- **The renderer gains one rule and one stricter allowlist**
  (`IsSafeImageSrc`, the `<img>` emission with `class="rc-image"`); the
  existing escape-first construction is untouched (R·2).
- **The upload boundary is ADR 0011's guards verbatim** — the
  `POST /content-image` lane is the second consumer of the same
  chokepoint (`MediaOptions.AllowedContentTypes` + `Media__MaxBytes`),
  so OPS.md's tunables stay the extension point.
- **The composer is a textarea + a plain-TS module**
  (`client/lib/insert-image.ts`, `tsc`-only) — **no editor dependency
  enters `package.json`**.
- **Audit-by-default holds** (SECURITY.md §3): every served UGC image is
  the product of an audited `Read` Allow (or the logged Deny that 404s
  it); platform-page images are public by construction and emit zero
  rows; every 404 path (store-miss, owner-miss) emits zero rows.

## Not decided here (explicit non-decisions)

Each is a **future lane**, named, gated on its owning resource — the ADR
0011 precedent holds:

- **Video / audio attachment** — the `AllowedContentTypes` / `MaxBytes`
  boundary is the seam; a future lane names them explicitly.
- **Server-side image transforms** (crop / resize) — bytes are stored
  as uploaded (the ADR 0011 stance); a future lane adds the transform.
- **Multi-image reordering UI** — the body text is the order; a future
  lane may add drag-drop if the plain-text order proves insufficient.
- **Any WYSIWYG / third-party editor** — the `tsc`-only constraint
  stands; the composer is a textarea with a Markdown hint.
- **Tables / footnotes in the renderer** — the "intentionally out of
  scope" set stays; the renderer's smallness is the point.

(Group logos, badge icons, and other follow-on ADR 0011 consumers are
already deferred by ADR 0011's own "not decided here" — this ADR's
`Amends:` line resolves ADR 0011's "post attachments" lane for
post/reply/announcement/about only; the rest remain future lanes, each
gated on its *owning* resource's audience, as ADR 0011 said.)

## Revisit when

- A resident requests a feature in the "not decided here" list — open a
  future-lane design doc + ADR (the ADR 0011 precedent), not an amendment
  to this ADR.
- The **audit policy on Deny** changes for a follow-on lane — C-MED·2 is
  the invariant; a change is itself a new ADR, not a code change (ADR
  0011's own "revisit when" clause, unchanged).
- The `src` allowlist needs a new accepted shape (e.g. a future lane
  serves from a different route prefix) — extend `IsSafeImageSrc` in the
  same commit as the route that earns it; a new *rejected* shape is not a
  revisit (rejection is the default).
