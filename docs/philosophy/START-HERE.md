# START HERE — Kumunita's Philosophy, in one hour

> **The line to remember:** the parts are cheap; the linkage is what's rare.
> Integration — the linking of differentiated parts — is the whole game, at
> every scale, from a module to a neighborhood to a life.

## What "integration" means here

A system is differentiated parts whose integrated functioning produces
properties no part has alone. Integration is the linking of those parts.
Quality is the quality of the linkage — not the sum of the parts.

A neighborhood is the clearest example: many different people, households, and
concerns, whose *linkage* produces a community no single person is. Kumunita
is software that builds and holds that linkage, so we hold the platform to the
same rule we hold the community. That is the entire philosophy.

## The same idea, in everyday life

Before the work scales, the idea in things you can touch:

| Everyday       | Parts (the cheap)        | Linkage (the rare)                             | Emergent property              | The smell                                  |
|----------------|--------------------------|------------------------------------------------|--------------------------------|--------------------------------------------|
| A meal         | ingredients              | the cooking — heat, order, balance             | a dish; satisfaction           | a buffet where nothing goes together       |
| A relay race   | runners                  | the baton handoff — timing, trust, rehearsal   | a team time no runner has alone | dropped batons — the seam fails            |
| A shared event | people, supplies, a date | the coordination — who brings what, who does what | a neighborhood that shows up | a pile of good intentions that never become a plan |

The next table is the same shape, at work scales. Plain-language version,
with the cooking example written out in full:
[`everyday-life.md`](everyday-life.md).

## The same idea, at every scale

| Scale         | Parts (the cheap)                          | Linkage (the rare)                              | Emergent property                 | Local-optimization smell |
|---------------|--------------------------------------------|--------------------------------------------------|-----------------------------------|--------------------------|
| **A piece of work** | tasks, tools, data                     | handoffs, review, feedback                       | quality, trust in the outcome     | "my part is done"        |
| **Code**      | functions, modules, handlers, projections | contracts, tests, feedback loops                 | reliability, maintainability      | "my function is fast"    |
| **The platform** | components, features, screens          | access model, groups, delegation, value chains   | trust, belonging, a shared place  | "a bag of features"      |
| **The team**  | roles, tools, people                       | rituals, review, shared model                    | judgment, calm velocity           | silos, heroics           |
| **The neighborhood** | residents, households, concerns      | who-knows-whom, shared routines, mutual aid      | a community                       | a list of isolated people |
| **A person**  | body, mind, attention, relationships       | boundaries, rest, meaning, recovery              | health, well-being, sustainability | burnout, "grinding"      |

Read that table once and you've read the whole philosophy. Every doc in this
folder is one row, expanded.

## The six principles (short version)

1. Quality is the quality of the linkage.
2. Differentiation is deliberate; integration is the work.
3. Bugs and value both live at the seams.
4. Feedback loops are a feature — every loop needs an owner and a response.
5. Emergent properties can't be delegated — judge the whole, not the part.
6. Integration decays — review, reflection, and maintenance close the loop.

(Full version: [`README.md`](README.md).)

## The three tests

- **Closed-loop:** can a resident take a problem in and get an outcome out,
  without leaving the platform? Every exit — every point where the work must
  leave the system — is a seam we didn't integrate.
- **Handoff:** every manual transfer between parts, systems, or people — a
  copy into a chat app, a re-key into a spreadsheet — is an un-integrated seam
  and a value leak.
- **Part-vs-whole:** does this optimize a part (a feature, a count, a sprint)
  at the cost of the whole (the resident's privacy, the team's sustainability,
  the trust the community stands on)?

Run these in every review and retro — and whenever a decision feels cheap.

## The five faces of a healthy system — FACES

Daniel Siegel, working in Interpersonal Neurobiology, names what a healthy
*relationship* looks like with five words: **F**lexible, **A**daptive,
**C**oherent, **E**nergizing, **S**table. We use the same shape to ask the
same question of a system — the platform, the team, the community it serves:

| Face        | A healthy Kumunita is…                                                                                       |
|-------------|--------------------------------------------------------------------------------------------------------------|
| **F**lexible   | a component can change or degrade without the whole breaking; a restore-from-backup is clean; adding a group or a component doesn't require a redesign |
| **A**daptive   | a resident's report actually reaches a moderator and changes what happens; the audit log, read, produces a response |
| **C**oherent   | a new resident can narrate what this place is for in one breath; a moderator can trace any access decision backward to a grant |
| **E**nergizing | the platform gives the residents' time and attention back, not take them; the team can sustain this pace for a year |
| **S**table     | the place holds under the neighborhood's load and across time — the trust survives a handoff of the admin, a restore, a release |

Run FACES alongside the three tests: the tests ask where the system
*fails*; FACES asks where it is *healthy*. The templates carry a FACES
score, so a cycle can move one of the five faces on purpose — and a design
can name what it trades away before it's built.

## Read it in this order (60 minutes)

| # | Doc                                        | Time  | What it answers                |
|---|--------------------------------------------|-------|--------------------------------|
| − | `everyday-life.md`                         | 10 min| the same idea in plain language — start here if jargon isn't your language |
| 0 | `START-HERE.md` (this page)                | 5 min | the one argument               |
| 1 | `README.md`                                | 5 min | the six principles, in full    |
| 2 | `the-platform-as-integrator.md`            | 10 min| why the platform exists        |
| 3 | `in-code.md`                               | 10 min| how we build code              |
| 4 | `in-product.md`                            | 5 min | how we run the platform & community |
| 5 | `the-human-system.md`                      | 10 min| the outermost scale: the person |
| 6 | `domains-of-integration.md`                | 5 min | the lenses of linkage          |
| 7 | `anti-patterns.md`                         | 5 min | how integration fails          |
| 8 | `templates/` (skim)                        | 5 min | the tools for the rituals      |

The order is deliberate: idea → why the platform exists → how we build → how
we run it → the lives it all lives in → the lenses that connect it all → how
it fails → the tools.

**Short on time?** Read the six principles, run the three tests on something
you touched today, and stop. Come back to the full pass when you have the
hour — and to `anti-patterns.md` the next time something goes wrong.

## Do this first (20 minutes, so it sticks)

Don't just read it. Pick one thing you touched this week — a flow, a screen,
a piece of the access model:

1. List its **parts** (features, modules, data, people).
2. Draw the **seams** — where the parts meet, and where value flows.
3. Ask the three tests: closed-loop? handoff? part-vs-whole?
4. Find **one un-integrated seam.** That is your first integration task.

You've now run the philosophy once, on a real system. Write it up — even
five lines in `templates/design-doc.md` is enough. That's the loop closing.

## The line to leave with

> We will not trade the whole — whether the whole is a platform, a team, a
> neighborhood, or a person — for a part's output.

Disagree with something here? That's principle 4 — the loop wants your signal.
