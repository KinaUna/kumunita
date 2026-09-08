# Domains of Integration — the Lens

> **The line to remember:** the parts are cheap; the linkage is what's rare.

> **The line to remember, sharpened:** the mind is a system of integration,
> and so is a system. Siegel named nine ways the mind integrates to produce
> awareness — here are the same nine, re-mapped from the mind onto the
> systems we build — and then the axes that only exist once the system is
> software, and once it reaches into a life.

## Where this comes from

Daniel Siegel's Interpersonal Neurobiology describes the mind in terms of
**domains of integration** — the linking of differentiated aspects of
consciousness (see Siegel, *Mindsight*). That is, in other words, the whole
philosophy of this repo: *differentiated parts whose integrated functioning
produces properties no part has on its own.*

(Want Siegel at the source, rather than our re-mapping of him? See the
Siegel entry in [`further-reading.md`](further-reading.md).)

Use this as a diagnostic: when a design feels wrong but nothing is
"broken" — a "bag of features," a "god module," a seam that keeps failing —
name the domain that is failing. Most failures are not missing parts; they
are a missing integration in one of these. The list grows as you look
closer: **nine the mind already knows, six native to software, six that
belong to the product reaching out into a life.**

## The nine, at a glance

| Domain (Siegel)    | In the mind it links                  | In what we build it links                            | Emergent (the whole)         | The smell (the failure)         |
|--------------------|---------------------------------------|------------------------------------------------------|------------------------------|---------------------------------|
| **Consciousness**  | alertness ↔ background flow           | signal ↔ attention                                   | calm, coherent awareness     | alert fatigue / total blindness |
| **Bilateral**      | left ↔ right hemisphere               | the two sides of a seam (producer ↔ consumer)        | a seam that actually holds   | silent coupling                 |
| **Vertical**       | lower/emotional ↔ upper/cognitive     | the layers of the stack (runtime ↔ strategy)         | design true to reality       | "designing in the cloud"        |
| **Memory**         | past ↔ present, explicit ↔ implicit   | what we learned ↔ what we do (ADRs, tests, the access model) | organizational learning   | tribal knowledge / frozen legacy |
| **Narrative**      | events ↔ a coherent self-story        | features ↔ one story (why this exists for *this* neighborhood) | coherence — it makes sense | a bag of features |
| **States**         | waking ↔ dreaming ↔ flow              | the system's modes (dev, prod, degraded, restore)    | resilience — one truth, all states | works in dev, breaks in prod |
| **Interpersonal**  | mind ↔ mind                           | role ↔ role; platform ↔ the residents' lives         | shared understanding         | divergent mental models, silos  |
| **Temporal (time)**| past ↔ present ↔ future               | history ↔ live state ↔ what's coming (events, reminders) | continuity, foresight | amnesia / firefighting only |
| **Transpirational**| across generations                    | across versions, teams, and caretakers               | a platform that outlives its authors | re-discovers the same lesson each cycle |

---

## 1. Integration of consciousness — *see without blinding or flooding*

**In the mind:** consciousness isn't all-or-nothing. It's a range between
hyper-arousal and hypo-arousal, and health is living in the workable middle.

**In what we build:** a system has awareness. The integration is between
*signal* (logs, the moderation queue, the audit log, failed handlers) and
*attention* (who looks, who acts). Too much signal → alert fatigue, nobody
responds. Too little → blind. The workable middle: enough signal to know
what's true, each one routed to an owner with a response. (See
[`in-product.md`](in-product.md): every loop needs an actuator.)

**The smell:** dashboards nobody reads, or a moderation queue that fills and
nothing happens, or no visibility at all.
**Heuristic:** if a signal has no owner and no response, it isn't awareness
— it's noise.

## 2. Bilateral integration — *both sides of the seam must agree*

**In the mind:** the two hemispheres are specialized and must talk to each
other; awareness needs both halves linked.

**In what we build:** every seam has two sides — a producer and a consumer,
a writer and a reader, the left and the right. Value and failure both live
in the *agreement* between the sides. One side optimized against the other's
silent assumption is a seam that hasn't actually integrated. In Kumunita
this is most acute across the access model: the code that *resolves* an
audience on one side, and the code that *renders* what a resident sees on
the other, must agree — or a post is visible to the wrong people with no
record of why.

**The smell:** [`accidental integration`](anti-patterns.md) — it works until
one side changes its assumption.
**Heuristic:** name both sides of the seam and write down what each assumes
the other does. If you can't, the contract isn't real.

## 3. Vertical integration — *design that stays true to reality*

**In the mind:** the lower, older, emotional layers and the upper, newer,
cognitive layers must link. A mind that can't bind the two is either
flooded by feeling or untethered from it.

**In what we build:** link the *levels* of the system — the fast, low
(realities: latency, post volume, the Postgres we actually run, failure
modes) with the slow, high (the strategy, the access model, the intent).
The high level's decisions must be constrained by the low level's realities,
and the low level's signals must shape the high level's choices.
Architecture that ignores its runtime is a mind untethered from its body.

**The smell:** "designing in the cloud" — an access model or a value chain
that is elegant on the doc and impossible or ruinously expensive one layer
down.
**Heuristic:** can the top-level design survive the constraints of the
bottom layer? Run it down the stack once before you call it done.

## 4. Integration of memory — *the past informs the present, without freezing it*

**In the mind:** past experience and present moment are linked; explicit
(declarative) and implicit (non-declarative) memory are linked — so the past
can guide the now without dominating it.

**In what we build:** link what we have *learned* to what we are *doing*.
Explicit memory = ADRs, the access model, tests, written moderator scoping.
Implicit memory = the instinct of how this platform actually behaves here.
Integration: the past is retrievable and actionable, and it informs the
present without forbidding change.

**The smell:** [`accidental integration`](anti-patterns.md) — the lesson
lives in one head (the one moderator who knows what's private) and leaves
when they do — or frozen legacy (the past forbids all change).
**Heuristic:** for this decision, point at the artifact that records *why*.
If it doesn't exist, the memory is only implicit — it will be lost when the
person leaves.

## 5. Integration of narrative — *one story, not a bag of parts*

**In the mind:** raw events are linked into a coherent self-story. Meaning
is the narrative that binds experience.

**In what we build:** the story here is not a user-journey-to-churn. It is
**why this neighborhood has a shared place, and how each part serves that.**
Directory, components, events, projects, and moderation all read as one
continuous whole when the story is present; as a shopping list of features
when it isn't. Kumunita's narrative is carried by
[`the-platform-as-integrator.md`](the-platform-as-integrator.md) and
[`how-it-works.md`](how-it-works.md).

**The smell:** [`part sprawl`](anti-patterns.md) — every feature works, and
nobody can say what the place is *for*.
**Heuristic:** can you tell a new resident, in one breath, what this place is
for and why each part exists? If not, you have features, not a platform.

## 6. Integration of states — *one truth across every mode*

**In the mind:** the different states of consciousness — waking, dreaming,
flow, meditation — are linked, so one state can inform and repair another.

**In what we build:** a system runs in many states — dev, prod, degraded,
and **restore-from-backup**. Integration is the *coherence across them* and
the *design of the transitions* (deploy, rollback, restore). A system whose
behavior is a different truth in each state hasn't integrated its states.

**The smell:** [`green CI, red production`](anti-patterns.md), and no
graceful path through failure — states are walls, not a continuum.
**Heuristic:** what does the platform do when it can't do the happy path —
and can we bring it back from a backup cleanly? If the answer is "nothing
designed," the degraded state is un-integrated. (See [`../OPS.md`](../OPS.md).)

## 7. Interpersonal integration — *the mind is not in one head*

**In the mind:** the mind isn't contained in the skull. It is made in the
linking of mind to mind — relating, empathy, attachment.

**In what we build:** the "system" is not contained in one service or one
person. Integration is the linking of *person to person* and *platform to
the neighborhood it serves*. The team, the moderators, and the admins must
share one end-to-end model — especially of the access model; where their
mental models diverge, seams break silently (see
[`in-product.md`](in-product.md), cross-role integration).

**The smell:** divergent mental models — the moderator who assumes a group
"obviously" includes the board; the resident who assumes their post is
private to neighbors.
**Heuristic:** if the admin, the moderator, and a new maintainer each drew
the access model "from memory," they drew three pictures. That divergence is
the bug.

## 8. Temporal integration — *the system spans time, not just a request*

**In the mind:** past, present, and future are held together; the past
informs the present, and the future shapes it. The sense of time as
continuous is itself an integration.

**In what we build:** link the *time axes* of the system. Past = history,
the audit log, a year of posts. Present = the live request, the current
membership. Future = events, reminders, deprecation, the next release.
Integration: the system reads its past to act in the present, and plans for
the future (a group renamed today must re-scope a year of past posts,
deliberately and audited). This is distinct from memory: memory is *what we
know*; temporal is *how the system behaves across time*.

**The smell:** amnesia (a group renamed and a year of posts silently re-scoped)
or present-only firefighting (a reminder that never became an event).
**Heuristic:** does this design hold its invariants over time — under a
group's membership change, a delegate's grant, a year of data? If it only
holds for one request, it isn't integrated temporally.

## 9. Transpirational integration — *the platform outlives its authors*

**In the mind:** integration reaches across *generations* — patterns,
lessons, even predispositions are transmitted to the minds that come next.
A mind is a link in a lineage: it carries the integration of the minds before
it, and hands some of its own forward to the minds after it.

**In what we build:** link the *generations* of the system — past version →
current → next, and past team → current team → successor caretaker.
Integration is the *transmission* of intent, constraints, and hard-won
knowledge: ADRs, contracts, tests, and docs written for the next reader, not
the current author. A codebase only its creator understands has no
transpirational integration — and a self-hosted platform's lifespan is
measured in caretakers.

**The smell:** each cycle re-discovers the same lesson; a "throwaway" that
was never handed to a next generation; a team that can't tell you why the
access model is the way it is.
**Heuristic:** hand this to a new moderator or a new maintainer next month.
What do they inherit, and what did we have to re-teach? The gap is the
transmission we skipped.

---

## Six more, that exist only in software

Siegel's nine come from the mind. A working system needs them all — but
software has axes the mind has no counterpart for. These six are native to
the systems we build.

| Domain             | What it links                            | Emergent (the whole)              | The smell (the failure)         |
|--------------------|------------------------------------------|-----------------------------------|---------------------------------|
| **Spatial**        | one node ↔ the whole deployment          | a platform, not one server        | "works on one box", split brain |
| **Data / meaning** | format ↔ semantics (types, invariants)   | data that can be reasoned about   | magic values, `raw` columns     |
| **Cognition**      | the resident's model ↔ the platform's model | the platform feels legible    | error states nobody can read    |
| **Authority**      | identity ↔ permission ↔ audit            | trust you can show                | god admins, "who allowed that?" |
| **Capacity**       | workload ↔ resources                     | steadiness under load             | unbounded queues, cost spikes   |
| **Artifact**       | intent ↔ build ↔ environment             | "works on my machine" is meaningless | environment drift        |

## 10. Spatial integration — *the whole is more than the node*

**The problem:** a mind is one place; a system can be many. Replicas,
shards, and caches are differentiated copies that must link back into one
consistent whole.

**In what we build:** Kumunita is deliberately a single Postgres, one
instance per neighborhood (ADR 0001), so most of this is *latent* — but the
moment a neighborhood outgrows one node, the local truth and the global
truth must link. Keep the seams for that seam, don't bake the assumption
that "one node is the whole."

**The smell:** "works on this server"; a backup that's the *only* copy;
behavior that depends on which machine answered.
**Heuristic:** can we move this to a second box and still tell one
consistent story? If the answer is "it was designed as one," the spatial
integration is an accident we'll pay for.

## 11. Integration of data — *form is not meaning*

**The problem:** data has two halves — its *form* (bytes, columns, the
wire) and its *meaning* (what the values stand for, what they may be, how
they relate). Form without meaning is storage; meaning without form is a
vague idea.

**In what we build:** the integration is between *representation* and
*semantics*: types over raw columns, invariants over magic values,
identifiers you can dereference. In Kumunita the sharpest link is the
authorization domain — a "group," a "delegation," an "audience" is not a
list of resident ids, it's a *grant with a scope and a reason*. Every place
the meaning lives only in someone's head is a leak.

**The smell:** a `scope` that is a magic string; an audience that's a raw
array of ids with no record of *why* they can see it.
**Heuristic:** for a sensitive value, can you name its type and its
invariants? If a change to it is a silent re-scoping of access, the
form-meaning link isn't real.

## 12. Cognition — *the resident's model must link to the platform's*

**The problem:** the resident has a model of what's public, what's private,
and who can see what. The platform has its own model. Where they de-link,
trust breaks silently.

**In what we build:** link the resident's mental model to the platform's —
the audience picker must *read as* the platform's access model. If the
resident assumes a post is private to neighbors and it isn't, or can't
explain why they can (or can't) see something, the cognitive link is broken.
This is where the platform *feels* legible or opaque.

**The smell:** "the platform did something I can't explain"; a privacy rule
that only works the way the author intended.
**Heuristic:** after a confusion, ask the resident to describe what
happened in their words. Where their story diverges from the platform's,
that gap is the un-integrated seam.

## 13. Integration of authority — *trust must be linkable*

**The problem:** identity, permission, and the action taken are three
different parts. A system where they drift apart is a system whose trust is
not integrated.

**In what we build:** the integration is the chain from identity → permission
→ action → record, held intact. Kumunita's access model is this domain made
explicit: a thin token, a fat authorization service, and **audit that is
always on**. An access decision that can't be traced to an identity and a
reason is a broken link.

**The smell:** god admins, a grant with no scope, "who allowed that?" with
no answer — a post visible to the wrong audience with no record of why.
**Heuristic:** pick any sensitive access decision. Trace it forward to an
audit record that names the identity, and backward to the grant that
allowed it. If either direction stops, the chain isn't integrated.

## 14. Integration of capacity — *the system must link load to resources*

**The problem:** workload is differentiated from resources. A system that
can't link the two is either drowning in load or burning capacity it doesn't
need.

**In what we build:** the integration is the *feedback between demand and
supply* — a reminder that fires, a notification delivered, a moderation
queue draining at a pace the team can actually sustain. At a few dozen to a
few hundred residents this is not the risk (see
[`in-code.md`](in-code.md)); the risk is the *wrong* access decision, not
the load. Design the link anyway, so the whole doesn't collapse the moment
the neighborhood grows.

**The smell:** a reminder that fires every hour; a moderation queue with no
owner; cost nobody predicted.
**Heuristic:** double the load. What does the platform do? If the answer is
"it dies" or "the only thing that scales is the moderation backlog," the
load/resource link isn't designed.

## 15. Integration of the artifact — *intent must survive the build*

**The problem:** there are three artifacts in a chain — *intent* (the code,
the design), *build* (the compiled, configured thing), and *environment*
(where it runs, with what secrets). If any link in that chain is lossy, the
system that ships is not the system that was decided.

**In what we build:** the integration is the *reproducible path* from intent
to running platform: the deploy pipeline (Coolify, one instance per
neighborhood), Marten's versioned migrations, the upgrade path, and a clean
restore from backup — all part of the design, not an afterthought (see
[`../OPS.md`](../OPS.md)).

**The smell:** environment drift; a migration that only works on one box's
laptop; "it worked in CI" that doesn't transfer to prod.
**Heuristic:** on a clean machine with only this repo, can you reproduce the
running neighborhood exactly? If the answer requires someone's laptop or
their memory, the artifact chain is broken at that link.

---

## Six more, native to the product that reaches into a life

The mind gives us nine, software six more. A product reaches into the world
— into trust, attention, and the lives of many different people — and so it
has axes of its own. For Kumunita these are not market axes (no price, no
churn to manage); they are *community* axes. These are the ones that only
exist once a platform has to earn its place in the lives it serves.

| Domain      | What it links (Kumunita)                 | Emergent (the whole)           | The smell (the failure)        |
|-------------|------------------------------------------|--------------------------------|--------------------------------|
| **Value**   | what a resident gives ↔ what they get back | a place that's worth using     | a feed that adds no trust      |
| **Fit**     | the platform ↔ the *this* neighborhood    | a place that fits our streets  | built for a market, not a block|
| **Trust**   | the promise (privacy) ↔ the delivery      | trust you can actually keep    | private post that leaks        |
| **Lifecycle**| the neighborhood's phases ↔ each other    | a relationship, not a launch   | great onboarding, no return    |
| **Agency**  | guidance (defaults) ↔ autonomy            | the resident stays in charge   | dark patterns / paralysis      |
| **Access**  | one design ↔ many residents               | a place the whole neighborhood can use | "it works for me" |

## 16. Integration of value — *what a resident gives must link to what they get back*

**The problem:** a platform is an exchange. The resident gives something —
time, attention, trust, a post — and expects something back. The parts are
differentiated; the integration is whether the *total given* links to the
*total returned*.

**In Kumunita:** the resident's cost is attention and trust; the return is
trust, privacy, and problems that actually get resolved across people. A
feature that costs a resident's attention and returns none is a leak, even
when it's heavily "used." The internal value chain this complements is in
[`the-platform-as-integrator.md`](the-platform-as-integrator.md).

**The smell:** a feed that "works" while residents feel surveilled or
drained; engagement up, trust down.
**Heuristic:** what does a typical resident actually give, and what do they
actually get back? If you can't state the exchange in one sentence, the value
isn't integrated — it's assumed.

## 17. Integration of fit — *the platform must link to the neighborhood it's for*

**The problem:** a platform's capabilities are differentiated from the needs
of the people it serves. They only become a *place* when the two link.
Capabilities without a fit are a toy.

**In Kumunita:** the "customer" is one neighborhood — one set of streets, one
set of households, one set of concerns. "Built for everyone" is the smell; it
means built for no one. A platform that fits a market but not *our* streets is
just a nicer version of nothing.

**The smell:** topics and flows that make sense for a generic community and
not for this one; "who is this for?" with no crisp answer.
**Heuristic:** name the one resident this flow serves and the one problem it
solves for them on our streets. If you can't, the capability and the
neighborhood aren't linked.

## 18. Integration of trust — *the promise must link to the delivery*

**The problem:** a platform makes a claim — "your private post stays
private" — and then delivers behavior. These are two parts. When they diverge,
trust breaks; and trust is the emergent property no single feature can restore
afterward.

**In Kumunita:** the gap between the *stated* privacy promise and the *actual*
access behavior. A private post that leaks, or a report that vanishes, is a
promise/delivery seam — not a feature gap.

**The smell:** a private post visible to the wrong audience; a report that
"went to someone who'd seen it" but nothing happened.
**Heuristic:** write down the top three promises a resident buys ("my private
post stays private," "my report reaches a person," "a file I share is kept"),
and check each against the actual first-week behavior. Any promise the
delivery can't keep is a broken trust link.

## 19. Integration of lifecycle — *the phases must link to each other*

**The problem:** a neighborhood's relationship with a platform runs in
phases — a new resident, active use, a hard moment (a report, a dispute),
and a quiet stretch. Each phase is a part. The integration is the
*transitions* between them.

**In Kumunita:** a platform can excel in every phase and still fail at the
transitions — a great onboarding that doesn't lead to an active resident; a
strong moderation loop with no follow-through after a dispute; a quiet
neighborhood the platform never re-engages.

**The smell:** great onboarding, no return; a strong moderation loop with no
follow-through; the community that quietly stops using it.
**Heuristic:** trace one resident from first sign-in to their six-month self.
Where did a handoff drop them? The phase with a strong signal but a weak
transition is the un-integrated seam.

## 20. Integration of agency — *guidance must link to autonomy*

**The problem:** a platform both guides (defaults, reminders, suggestions) and
leaves the resident free to choose. These are two forces that must be
*integrated*, not stacked. Too much guidance overpowers the resident (dark
patterns); too little abandons them (paralysis).

**In Kumunita:** the default audience, the reminder cadence, the moderate
nudge — and whether the nudge serves the resident's goal or a feature's
metric. The author's choice of audience being absolute by default is the
agency link at its cleanest: the platform suggests a default, the
neighborhood decides.

**The smell:** a default that quietly over-publishes; a reminder that
creates guilt; a power user resenting the handrail while a beginner drowns
without it.
**Heuristic:** for a key decision, ask who is choosing — the resident or the
platform? And is that the right one, for that person, at that moment? If the
platform is choosing for a metric where the resident should choose for
themselves, the agency link is corrupted.
(See [`the-human-system.md`](the-human-system.md) — the whole-person test:
a nudge that optimizes a count at the cost of the resident's attention is
exactly the local optimization that doc warns against.)

## 21. Integration of access — *one design must link to many residents*

**The problem:** a platform is one design; its residents are many — different
abilities, literacies, contexts, and constraints. The integration is how one
differentiated design links to a differentiated population without excluding
a slice of it.

**In Kumunita:** low-literacy and low-technical residents, low-connectivity
contexts, and "who can actually use this" as design inputs, not
afterthoughts. The median user is a part; the whole is the neighborhood.
Kumunita is multilingual (ADR 0005) precisely because the design must reach
the whole population, not just the median one.

**The smell:** "it works for me"; a feature that quietly requires a
capability the target resident doesn't have; a flow that presumes literacy
most of the neighborhood doesn't have.
**Heuristic:** who is the least-served resident who still needs this? Run
their path end to end. Every place their path hits a wall is an access seam
the design didn't integrate.
(See [`the-human-system.md`](the-human-system.md) — the resident's world
includes their body, attention, and context; access is integrating with all
of it, not just the median one.)

---

## How to use this

- **In design reviews:** when a proposal feels wrong but nothing is "broken,"
  name the domain it is failing at. "This has parts but no narrative" is a
  specific, actionable critique.
- **In postmortems and retros:** don't just ask *what* broke — ask *which
  integration* was missing. The answer is the durable lesson, not the symptom.
- **As a self-check for the team:** the interpersonal and transpirational
  domains are the ones we most often neglect, because they aren't "code."
  They are exactly as real as the rest.
- **When a smell names an anti-pattern:** [`anti-patterns.md`](anti-patterns.md)
  is the fix map — each domain's smell points at the row where the fix lives.

The list is not a checklist to tick. It is a lens: **the parts are cheap;
the linkage is what's rare** — and these are the kinds of linkage a working
system needs. Nine of them the mind already knows; six are native to
software; six belong to the platform that reaches into a life.
