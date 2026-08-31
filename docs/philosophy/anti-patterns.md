# Anti-patterns

The eight failures below are **domain-general** — any differentiated system can
suffer them, from a workflow to a codebase to a community. Kumunita's own
catalogs (`in-code.md`, `in-product.md`) name them with local symptoms on top.

| Name | What it looks like | Kumunita symptom | The fix |
|---|---|---|---|
| **Part sprawl** | Parts accumulate with no linkage; a bag of parts nobody can navigate. | A pile of components and features with no story; a directory + feed + events + projects that don't connect. | Every new part states which seam it integrates. Prune parts that don't connect. |
| **The god part** | One part everyone depends on; nothing testable in isolation. | One admin who knows everything; one script that does all of moderation. | Find the stable contract; split on it. Give the part a boundary and an owner (a scoped moderator, a module interface). |
| **Distributed fragmentation** | Many parts, no integration; every change touches five of them. | Access logic scattered across components; group rules re-implemented per feature. | Re-differentiate on real seams; route everything through the one authorization model. |
| **Parts work, seams don't** | Every part passes its checks, but the handoff fails. | Green CI, then a post visible to the wrong audience; a report that never reaches a moderator. | Test and observe the seams — especially the access model — not just the parts. |
| **Signals without loops** | Dashboards or reports nobody acts on. | A moderation queue that fills and nothing happens; an audit log nobody reads. | Every signal gets an owner, a threshold, a response action. |
| **Accidental integration** | It works, but nobody knows why; tribal knowledge. | "The board is in that group" — an undocumented assumption; a moderator who *knows* what's private. | Make it explicit: the access model, tests, ADRs, written moderator scoping. |
| **Silent coupling** | Undocumented assumptions; one side changes and the other silently breaks. | A group renamed and a year of posts silently re-scoped; a delegate granted more than intended. | Explicit, versioned agreements; migrate dependents deliberately; audit the change. |
| **Reflection without actuation** | Retros and reviews with no follow-through. | "We should fix stale groups" said every month; incidents re-happening. | Every action gets an owner and a deadline; the next retro verifies the last closed. |

## Reading the table in Kumunita

The same eight names appear in code, in the community, and in the team,
wearing different clothes. In code they are *green CI, red access decision*
and the *god module*. In the community they might be *the one resident
everyone texts directly* (the god part) or *the spreadsheet that's the real
event list* (a part with no linkage to the platform). In the team, *the
deploy that only one person can do* (a god part) or *the "why" of an access
rule living in one head* (accidental integration).

Name the failure with the general name first, then the local one — the general
name is what keeps the diagnosis portable.

## Keeping this alive

When a failure keeps recurring, record it here with its Kumunita symptom —
that's principle 6 working: the loop closes where the work happens.
