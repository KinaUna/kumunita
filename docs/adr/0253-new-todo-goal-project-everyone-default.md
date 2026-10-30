# ADR 0253 — New to-do, goal and project forms open with "Everyone" on

Status: Accepted
Date: 2026-10-29

## Context
Verifying the to-do Audience toggle and tag input live (rebuilt build 0.76.0, signed in as a resident) found that
`/projects/todos/new`, `/projects/goals/new` and `/projects/projects/new` opened with the "Everyone" switch **off** and the people/group
pickers showing, while the copy says "The default — everyone can see this to-do". `/projects/boards/new` opened correctly (switch on).
The views render the switch from `Model.Audience?.CommunityVisible ?? true`, but the three controller actions (`CreateGet`, `GoalNew`,
`ProjectNew`) seed a non-null `AudienceEditorModel` without `CommunityVisible`, which defaults to `false`, so the `?? true` never
applied. A form submitted as shown would have been author-only (an empty audience denies everyone else), the case the board
composer's seed comment already describes. Not caused by the section-card work (ADR 0244); the controllers are unchanged since.

## Decision
- The three actions seed `CommunityVisible = true`, as `BoardNew`, posts and events already do.
- Test `NewForms_SeedCommunityVisibleAudience` asserts it for all three view models.

## Verified live (before the fix)
Tag input: typing "gar" lists "Garden"; clicking the suggestion adds a chip and updates `TagIds`; Enter commits free text as a new chip;
the chip's close button removes it. Audience toggle: switching on hides the pickers, switching off shows them. Arrow keys do not move
through suggestions (by design: the module commits free text on Enter and picks by click).

## Not covered
Not compiled or re-run after the fix (no .NET SDK in the scratch environment). Existing to-dos, goals and projects keep their stored audience.
The tag hint on the to-do form reads "it attaches to this post" (shared `tag.input.hint` key); left alone, a copy question for a later pass.
