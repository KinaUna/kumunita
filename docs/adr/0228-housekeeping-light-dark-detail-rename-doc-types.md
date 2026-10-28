# ADR 0228 — Housekeeping: light-dark() palette tokens, `kmb-detail-*` rename, notice-strip anchoring, Documents allow-list

Status: Accepted

## Context
Four items from the UI refresh plan were left open: duplicated dark token scopes in `palettes.css` (Phase 0), the
`kmb-ann-*` names used by shared detail pages, scroll anchoring on the notice strip, and the Documents allow-list that
made the IMG and PPT tiles unreachable (ADR 0202).

## Decision
1. **light-dark().** `palettes.css` declares every colour token that differs between light and dark once, as
   `light-dark(<light>, <dark>)`, for Forest and Ember. `color-scheme` is set on `:root` (`light dark`, i.e. follow the OS)
   and forced by `data-kmb-theme='light'|'dark'`, the same three states as ADR 0133. Tokens that cannot use it stay in the
   small dark scopes: `r, g, b` triplets (`--bs-*-rgb`, not colours) and tokens that exist only in dark (`--kmb-band`, Forest
   `--kmb-surface-2`, Ember `--bs-light-border-subtle`). The file is 35 lines shorter and each dark token appears once
   instead of twice. Side effect: native controls and scrollbars now follow the theme through `color-scheme`.
2. **Rename.** Shared detail-page classes `kmb-ann-{detail,detail-chips,byline,title,comment,comments,composer}` become
   `kmb-detail-{page,chips,byline,title,comment,comments,composer}` (Views and CSS). The announcement list keeps
   `kmb-ann-{list,card,chips,actions}` because it is announcement-specific. Older ADRs keep the old names as history.
3. **`overflow-anchor: none`** on `.kmb-notices`, so the strip changing height when a notice hides cannot scroll the page.
4. **Documents allow-list** (amends ADR 0122 D3): the default list gains PowerPoint (`.ppt`, `.pptx`) and the four raster
   image types (jpeg, png, webp, gif). SVG, HTML and scripts stay refused; downloads are still `Content-Disposition:
   attachment` (D6); `Media:DocumentAllowedContentTypes` still overrides.

## Verified
- Live, old vs new `palettes.css` token section injected into `/admin/site`: all 78 palette/Bootstrap tokens resolve to
  identical values in Forest and Ember x light, dark and auto (OS dark): 0 differences.
- Ember dark renders as before (screenshot).
- `DocumentAllowListTests` added (needs a test run). Razor/C# not compiled here.

## Not changed / open
- `site.css`'s own `:root` dark scopes are not collapsed yet.
- Auto mode with an OS in light mode was not exercised live (the pane's OS is dark); it is the same `color-scheme: light dark` rule.
- IMG/PPT tiles still need a real upload check once the container is rebuilt.
