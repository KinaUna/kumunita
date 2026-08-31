# The Platform as an Integration Engine

Kumunita is a linkage machine. Its features are parts; its value is the
integration.

A neighborhood's life is differentiated and fragmented: a question about the
roofer here, a lost-cat post there, who's free to help move on Saturday, which
street light is out, knowledge scattered across heads, phones, and paper. The
platform's job is to **link these parts into a coherent whole the neighborhood
can act on.** That linkage — not any single feature — is what the community is
actually getting.

This is not a growth thesis. There is no market to win. The "customer" is one
neighborhood, and the measure of success is a shared place that holds: trust,
privacy, and problems that actually get resolved across people.

## The value chain: every arrow is where value is added

| Raw element (a part)        | Integration the platform performs            | Emergent value (the whole)              |
|-----------------------------|----------------------------------------------|------------------------------------------|
| **Signals** — posts, reports, RSVPs, questions | Differentiated and linked to the right audience | **Shared awareness** — the right people know the right thing |
| **Shared awareness**        | Integrated into relationships and context     | **Understanding** — who to ask, what's expected, what's already been tried |
| **Understanding**           | Linked to goals and constraints              | **Decisions** — the repair is funded, the event is scheduled, the conflict is addressed |
| **Decisions**               | Differentiated into tasks, then re-linked    | **Coordination** — owners, deadlines, who's helping |
| **Coordination**            | Executed; results fed back                   | **Outcomes** — the light is fixed, the move is done, the person is known |

Each arrow is an integration step, and each step is where value is added. A
platform that stops at any level delivers only that level's value:

- Stops at signals → it's a bulletin board.
- Reaches shared awareness → it's a feed / directory.
- Reaches understanding → it's a place where people actually know each other.
- Reaches decisions → it's a place where the community makes things happen.
- Closes the loop to outcomes → it's a **community platform** — the thing
  Kumunita is for.

The further the integration, the more irreplaceable the value — because the
*linkage*, not the parts, is what no competitor can copy piece by piece.

## Two consequences for how we build

**1. Evaluate every feature by the integration step it serves.**
A feature that adds a signal without linking it to the right audience is a
cost (noise, more to maintain, more surface to moderate). A feature that links
a signal to its audience — or a decision to an owner — is a value-add. In
feature review the first question is not "does it work?" but **"which arrow
does this move?"**

**2. Diagnose confusion as missing integration, not missing features.**
When the platform feels like a "bag of features," the parts are present but
the arrows are missing. The fix is usually not another feature; it is linking
two things that already exist (e.g. the report to the right moderator, the
event to the right groups).

## Integrating with the neighborhood's life

Make "the world" explicit: it is the residents' **attention, time, energy,
relationships, and well-being** — a *living life*, not just a next action.
See [`the-human-system.md`](the-human-system.md).

Kumunita is a system embedded in a larger one: the neighborhood, and the
individuals in it. Its boundary is where it meets that life — and that
boundary is full of seams, the highest-risk, highest-value places.

**Inbound — what we listen for.**
The platform receives differentiated signals: a report, a question, an RSVP, a
new resident's first sign-in. The quality of the platform is bounded by the
quality of its intake. Deciding *what to listen for* — and, critically, what
to keep **private** — is the core product design, not an afterthought. The
access model is the intake: the author's choice of audience is absolute by
default.

**Outbound — where value goes next.**
The platform emits outputs: a notification, a reminder, an iCal event, a
search result, an export. **An output that dies inside the platform is dead
value.** Every output should flow to a next action in the resident's life — or
respectfully not be emitted at all (privacy first).

**The seam principle, applied to the resident's life.**
The handoffs between the platform and the resident's other tools are seams.
Every manual copy into a chat group, a re-key into a paper list, or a re-type
into an email is an *un-integrated seam* — friction and a value leak. A clean
export, a shared iCal, a reliable notification is a *designed seam*. We invest
in designed seams at least as heavily as in features — but only where the
resident wants them, never as a way to pull them off-platform.

**Integration as feedback.**
The more the platform is linked into the neighborhood's real life, the more
honest signal we get, the faster our loops close. A platform that the
neighborhood keeps out of its real life is a closed system — assumptions drift,
content goes stale, the community stops using it.

## The closed-loop test (Kumunita edition)

Can a resident take a **problem** in and get an **outcome** out, without
leaving the platform — and without giving up privacy doing so?

- Where do they have to leave? → each exit is a seam we didn't integrate.
- Where do they have to re-explain who they are or re-state what's private? →
  each is an integration *failure* we must not ship.
- Every "just message them separately" the platform should be handling is an
  answer to that question.

## Heuristics for review

- **Which arrow does this serve?** (signal→awareness→understanding→decision→
  coordination→outcome)
- **World seams touched:** which handoffs to the resident's other tools does
  this create or depend on?
- **Output destination:** does every output flow to a next action — or does it
  sit in a feed nobody reads?
- **Intake honesty:** does this listen for what the resident's problem
  actually needs, and respect what they did *not* share?
- **Copy-paste test:** where does a resident still copy-paste in or out? Each
  one is an un-integrated seam — and a place to check whether the leak is
  friction or a privacy risk.
