# Design Doc — [Title]

> The "Seams & contracts" section is mandatory. Run the three tests
> (closed-loop? handoff? part-vs-whole?) before you call this ready.

## Context
## Goals / Non-goals

## Human cost
Does this give the resident's time and attention back, or take them? Does it
optimize a part (engagement, a count) at the cost of the whole (their privacy,
their life, the neighborhood's trust)? For our team: can this pace hold, and
does it respect the boundary?

## Parts affected
Which components, modules, handlers, projections, and roles does this touch?

## Seams & contracts (mandatory)
Which interfaces does this create, change, or depend on?
Does it touch the **access model** (audiences, groups, delegation, moderator
scope)? What is the new contract? Who depends on it? What's the migration
path? Is the change audited?

## Feedback loops
How will we know it works? Tests at which seams? Which signals, which
thresholds, who watches them, and what happens when they trip?

## Emergent impact
Privacy, trust, reliability, legibility, cost — for the system, not the part.

## Local-optimization check
Which part is this optimizing? What does the whole (the neighborhood, the
team, the platform) pay for it?

## FACES check
Which of the five faces does this design strengthen — **f**lexible,
**a**daptive, **c**oherent, **e**nergizing, **s**table — and which does it
consume? Name at least one trade; a design that claims no cost is a design
that hasn't priced it.

## Rollout & rollback
Deployment path, migration, and a clean rollback / restore. (See
[`../../docs/OPS.md`](../../docs/OPS.md).)

## Risks
Where is the integration most likely to break? Where could privacy or trust
leak?

## Integration step served
Which arrow does this move? (signal→awareness→understanding→decision→
coordination→outcome). If "none," say what value this actually adds.

## World seams
Which handoffs to the resident's other tools does this create or depend on?
Does every output it produces flow to a next action — or sit in a feed nobody
reads, or cross a privacy boundary it shouldn't?
