# ADR 0204 — Profile preview avatar monogram

Status: Accepted

## Context
Light-mode check of the Directory, member page and profile preview (ADR 0197).
Directory and member page were fine, including the "no contact shared" state.
The preview showed a blank pale circle when the author has no photo (the view
had no display-name channel), which reads as a missing image next to the member page.

## Decision
- The preview reads the signed-in author's own profile (IUserInfoService, as _AccountNav does)
  and shows their initial and AvatarHue, like the Directory. No display name -> the previous blank circle.
- No view-model change; the contact-peek pin (no raw subject id in the model) is unchanged.

## Open
- Not previewed in the browser yet (Razor edit only; `Kumunita.Web` compiles clean).
