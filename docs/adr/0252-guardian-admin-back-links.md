# ADR 0252 — Guardian and admin pages use the accent back link

Status: Accepted
Date: 2026-10-29

## Context
After ADR 0250 the group forms matched Post edit's accent back link. The Guardian pages and eleven admin pages still showed a grey
link (`text-muted`, the admin ones also `small`). `Guardian/AddChild` also hard-coded "Back to your children".

## Decision
- `Guardian/Detail`, `Guardian/AddChild`: `<div class="mb-2"><a class="btn btn-link p-0" href="…">← …</a></div>`.
  `AddChild` now uses the existing `guardian.back` key (no new keys).
- The eleven admin pages with the identical `← Admin` link (Timezone, Messaging, Signup, Site, Portability, Quiet,
  AnnouncementComments, SampleData, Guests, DateFormat, SurfaceLabels) get the same markup; the text still comes from `nav.admin`.
- Not changed: back links that already use `btn btn-link`, the storage pager "Prev" button and the "View the byte-store metrics" link.

## Not covered
Not compiled and not looked at in a browser. Admin links were small grey text; they are now the larger accent link like the rest of the app,
which was the chosen direction rather than a measured problem.
