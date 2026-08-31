# Postmortem: [incident]

## Summary & impact
Who was affected — a resident's privacy? the community's trust? the platform's
availability? State it plainly.

## Timeline

## What integrated
Which parts behaved as expected, working together? (Credit the linkage, not
just the parts.)

## Which seam broke
Name the boundary that failed — not the component. (The access model? the
report→moderator flow? the backup/restore path? a handler? a seam with the
resident's life?)

## Loop audit
- What signal should have told us earlier?
- Did the loop exist? Did it close? Who was supposed to act?
- Was the access decision audited? If not, why?

## Actions
Each action names which link of a loop it strengthens — **prevent / detect /
contain / recover** — with an owner and a deadline.

## Follow-up
Verify in the next retro that these loops actually closed.
