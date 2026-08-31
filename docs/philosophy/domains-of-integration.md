# Domains of Integration — the Lens

> The line to remember: the parts are cheap; the linkage is what's rare.
> These are the *kinds* of linkage a working system needs. When a design
> feels wrong but nothing is "broken," name the domain it is failing at.

Most failures are not missing parts — they are a missing integration in one of
these. Use this as a diagnostic: "this has components but no narrative," or
"the authorization is integrated but the trust isn't," is a specific,
actionable critique.

## The ones that matter most for Kumunita

| Domain | What it links | Emergent (the whole) | The smell (the failure) |
|---|---|---|---|
| **Narrative** | features ↔ one story (why this exists for *this* neighborhood) | coherence — it makes sense | a bag of features |
| **Authority** | identity ↔ permission ↔ audit | trust you can show | "who allowed that?" with no answer |
| **Cognition** | the resident's model ↔ the platform's model | the platform feels legible | "the system did something I can't explain" |
| **Memory** | what we learned ↔ what we do (ADRs, tests, the access model) | organizational learning | tribal knowledge / frozen legacy |
| **States** | dev ↔ prod ↔ degraded ↔ backup-restore | resilience — one truth across modes | works on a dev box, breaks in prod |
| **Interpersonal** | moderator ↔ moderator ↔ resident; platform ↔ the neighborhood | shared understanding | divergent mental models, silos |
| **Temporal** | history ↔ live state ↔ what's coming (events, reminders) | continuity, foresight | amnesia / firefighting only |
| **Transpirational** | past version ↔ current ↔ next; this team ↔ the next caretaker | a platform that outlives its authors | re-discovers the same lesson each cycle |

## A few, expanded

### Authority — *trust must be linkable*
Three things must agree: *who* (identity), *what they may do* (permission),
and *what actually happened* (audit). Kumunita's access model is this domain
made explicit: a thin token, a fat authorization service, and **audit that is
always on**. An access decision that can't be traced to an identity and a
reason is a broken link. The smell is the quiet one — a post visible to the
wrong audience with no record of why. *Heuristic:* pick any sensitive access
decision. Trace it forward to an audit record that names the identity, and
backward to the grant that allowed it. If either direction stops, the chain
isn't integrated.

### Narrative — *one story, not a bag of parts*
The story here is not a user-journey-to-churn; it is **why this neighborhood
has a shared place, and how each part serves that.** Directory, components,
events, projects, and moderation all read as one continuous whole when the
story is present; as a shopping list of features when it isn't. *Heuristic:*
can you tell a new resident, in one breath, what this place is for and why
each part exists?

### Cognition — *the resident's model must link to the platform's*
A resident has a model of what's public, what's private, and who can see what.
The platform has its own model. Where they de-link, trust breaks silently —
the resident assumes a post is private to neighbors and it isn't, or the
resident can't explain why they can (or can't) see something. *Heuristic:*
after a confusion, ask the resident to describe what happened in their words.
Where their story diverges from the platform's, that gap is the un-integrated
seam.

### Memory — *the past informs the present, without freezing it*
Kumunita's explicit memory is the ADRs, the access model, the tests, and the
written moderator scoping. Its implicit memory is "how things actually work
here." Integration: the past is retrievable and actionable, and informs the
present without forbidding change. *Heuristic:* for this decision, point at
the artifact that records *why*. If it doesn't exist, the memory is only
implicit — it will be lost when the person leaves.

### States — *one truth across every mode*
The platform runs in many states: dev, prod, degraded, and **restore-from-
backup**. Integration is the coherence across them and the design of the
transitions (deploy, rollback, restore). *Heuristic:* what does the platform
do when it can't do the happy path, and — critically — can we bring it back
from a backup cleanly? If the answer is "nothing designed," the state is
un-integrated. (See [`../OPS.md`](../OPS.md).)

### Interpersonal & Transpirational — *the ones we most often neglect*
The interpersonal domain links the people *in* the system (moderators,
admins, residents) and the platform to the neighborhood it serves. The
transpirational domain links the platform to the team that comes after the
one writing this — ADRs and docs written for the next reader, not the current
author. These aren't "code," but they are exactly as real as the rest, and a
self-hosted platform's lifespan is measured in caretakers. *Heuristic:* hand
this to a new moderator or a new maintainer next month. What do they inherit,
and what did we have to re-teach? The gap is the transmission we skipped.

## How to use this

- **In design reviews:** when a proposal feels wrong but nothing is "broken,"
  name the domain it is failing at.
- **In postmortems and retros:** don't just ask *what* broke — ask *which
  integration* was missing. The answer is the durable lesson, not the symptom.
- **When a smell names an anti-pattern:** [`anti-patterns.md`](anti-patterns.md)
  is the fix map.

The list is not a checklist to tick. It is a lens.
