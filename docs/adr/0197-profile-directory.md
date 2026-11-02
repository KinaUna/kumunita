---
id: adr-0197
title: "Profile and Directory"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/adr/0182-form-look.md, docs/adr/0184-announcement-detail.md, docs/adr/0196-pages.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0197](0197-profile-directory.md) — Profile and Directory

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR [0196](0196-pages.md) (pages), ADR [0184](0184-announcement-detail.md) (detail pages), ADR [0182](0182-form-look.md) (forms)

## Decision

- Directory cards (`Directory/Index`): equal-height surface cards; the monogram avatar takes the per-resident hue
  (`mono-hue`, `AvatarHue.For`); Verified is a quiet `.kmb-chip.kmb-chip-verified` with a check, beside the name
  (the glyph is `aria-hidden`; the word stays); "Send a message" is the quiet outline button pinned to the card bottom.
  The Verified label is now the existing `directory.detail_verified` key instead of hard-coded text.
- Member page (`Directory/Detail`): a header card holds avatar, name, Verified chip and Send a message (full width
  on phones); About, Interests (chips) and Contact are `.kmb-detail-card`s. The contact-block gate logic is untouched.
- Profile preview: contact block in a detail card. Profile edit: the avatar card (outside the form) uses the detail
  card look to match the form cards below it; the rest already follows the form look (ADR [0182](0182-form-look.md)).

## Open

- The directory has no search or filter; not part of this patch.
- Alerts on the preview page (`alert-success` / `alert-secondary`) keep Bootstrap colours.
- Not verified in light mode or at phone width; a member with no avatar and no contact block should be checked.
