---
id: adr-0204
title: "Profile preview avatar monogram"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0204](0204-profile-preview-avatar.md) — Profile preview avatar monogram

Status: Accepted

## Context
Light-mode check of the Directory, member page and profile preview (ADR [0197](0197-profile-directory.md)).
Directory and member page were fine, including the "no contact shared" state.
The preview showed a blank pale circle when the author has no photo (the view
had no display-name channel), which reads as a missing image next to the member page.

## Decision
- The preview reads the signed-in author's own profile (IUserInfoService, as _AccountNav does)
  and shows their initial and AvatarHue, like the Directory. No display name -> the previous blank circle.
- No view-model change; the contact-peek pin (no raw subject id in the model) is unchanged.

## Open
- Not previewed in the browser yet (Razor edit only; `Kumunita.Web` compiles clean).
