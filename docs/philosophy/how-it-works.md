# How Kumunita Works — for the Neighborhood

> No technical background needed. If you can use a website, you can read this,
> and you can help shape what we build. The goal is that anyone in the
> neighborhood can understand the platform well enough to give feedback that
> actually helps us.

## What it is

Kumunita is a website for **our** neighborhood — one deployment, one
community. It lives on a server that we run ourselves, and the data belongs to
the neighborhood, not to a company.

Think of it as the noticeboard, the phone tree, and the community center
meeting room — except it's always open, it doesn't lose the paper, and you get
to choose who sees what you post.

## What you can do

- **Find people.** A directory of residents. Each person decides what they
  share — a profile is always there, but phone numbers and similar details are
  only shown if the person opts in.
- **Post and discuss.** Announcements and conversations, organized into
  topics like Safety, Maintenance, Social, and Governance — so a question
  about a street light isn't lost in a pile of birthday wishes.
- **Plan events.** Events with sign-ups (RSVP) and reminders, so "who's
  coming?" has an answer.
- **Work on projects together.** A goal, a list of tasks, and the people
  helping — the roofer, the move on Saturday, the garden.
- **Keep things safe.** If something on the platform needs attention, you can
  report it (below).

## The most important rule: you choose who sees your post

This is the heart of the platform, so here it is in full:

- Every post has an **audience** — who is allowed to see it. You pick.
- You can post to everyone, to a **group** (a named list of people you've
  built once and reuse everywhere), or to just one person.
- The default is the safest choice: if you don't pick anyone, no one sees it.
- **Groups** are the trick that makes this easy. Build a "Maple Street" group
  or a "Book club" group once, and every post you address to it reaches
  exactly the right people — and when membership changes, all your past posts
  follow automatically.
- **Delegation** is for when you're not around. You can give a family member
  or caretaker access to act for you — with a scope you set ("see my posts,
  but not my contact details"), and it can be taken back at any time.

**What you should check:** before you post something personal, look at the
audience you've chosen. The platform won't second-guess you — your choice is
final. That's a feature, not a bug: no one else, not even the people running
the site, gets to decide who sees your private post.

## What the people running it can and can't see

You might wonder what "the people running it" means. There are three kinds of
people with extra access:

- **Admins** run the site itself. There are a few, and the neighborhood
  should know who they are.
- **Moderators** keep individual topics civil. A moderator for Safety can
  moderate Safety — and only Safety.
- **Everyone else** — including the admins — can see what *you have shared
  with them*. Nothing more.

Two rules make this trustworthy:

1. **Private stays private by default.** If a post is addressed to a group a
   moderator isn't part of, the moderator cannot see it. A moderator only
   gains access to a private post if you (or someone) **files a report** on
   it — and that access is recorded.
2. **Every peek is logged.** Whenever someone with extra access views
   something they don't normally see, it's written to a log that can be
   reviewed. Nothing is read invisibly.

So the honest answer to "can the admins see my private posts?" is: *not
unless there's a filed report on that post, and even then it's recorded.*

## What happens when you report something

A report is the neighborhood's way of saying "this needs a person." Here's
the whole path, because it's short and every step is checked:

1. You file a report on a post or profile.
2. It goes to the moderator for that topic.
3. The moderator gets temporary, **recorded** access to look at it.
4. They resolve it (talk it through, remove it, do nothing) and it's noted.

No step is invisible. If reports pile up and go unanswered, that's a problem
with the *people* running moderation — and it's exactly the kind of thing we
want to hear about (see below).

## Who builds it, and why you matter

A small team builds and runs this for the neighborhood. The code is open in
this repository — meaning anyone can read what it does, and anyone can check
the promises on this page against the actual behavior.

But here's the part that matters most to us: **the people who will use it are
the best reviewers of it.** We can test whether the software works. Only you
can tell us whether it *fits* — whether the topics make sense for our
streets, whether the privacy rules feel right, whether a report actually gets
answered. Software that works but doesn't fit is just a nicer version of
nothing.

## How to give feedback — no code required

You don't need to write code. In fact, most useful feedback is in plain
language. Here's how:

**1. Write a story, not a complaint.** The most useful feedback is a short
account of something that happened to you:

> "I wanted to ask just my street about the closed road. I created a post,
> but I couldn't work out how to make it visible only to Maple Street
> residents. I ended up posting it to everyone."

That tells us the *where* (audience picker), the *what* (groups were unclear),
and the *consequence* (private thing went public). A complaint like "the
privacy stuff is confusing" is harder to act on — not because it's wrong, but
because we don't know which moment confused you.

**2. Tell us what you were trying to do.** Feedback about a specific thing you
were trying to do — find someone, plan something, keep something private —
lands better than feedback about a feature in the abstract. We judge
everything by "does this help a resident get their thing done?"

**3. Flag the seams.** (Seam = the moment two things have to hand off.) The
hardest problems to find are the ones where you had to leave the platform to
get something done: copy a phone number into a text, re-type an event into a
paper list, forward a post to someone who should have seen it. Every time you
notice that, tell us. Those are exactly the places we invest most.

**4. Say when something is *missing* in a way that matters.** "I have no idea
who to ask about roofers" is a design problem we can't see from inside the
code. "I had to ask three people before I found out how reporting works" is
the same.

**5. Disagree out loud.** If you think a rule is wrong — the default
audience, what a moderator can see, how reports are handled — say so. These
are decisions made in writing, in this repository, and they're meant to be
argued with. A rule nobody questions is a rule nobody is checking.

### Where to put it

- **Issues in this repository** are the main channel. Write the story from
  step 1 and file it. You don't need an account with any special rights to
  start a conversation there.
- **Translation:** the platform supports multiple languages, and the team
  decides which languages to offer. If your neighborhood needs a language
  that isn't there yet, tell us — and if you can write in it, that's an
  immediate way to contribute without touching code.
- **Moderation help:** keeping the topics civil is a shared job. If you'd
  like to be a moderator for a topic, ask the admins.
- **Just talking:** if none of that fits, tell a team member in person.
  "I don't know how to use this thing" is the most valuable sentence in this
  document, because it tells us our explanations failed — and we can fix
  them.

## A checklist you can use when you try something new

The next time you use a new part of the platform, you can check it with three
questions (these are the same ones we use internally, in plain language):

1. **Did it come back around?** Did the thing you started — a question, a
   report, a plan — end up where it was supposed to, with the people who
   started it? Or did it have to leave (a forwarded text, a "let's discuss
   separately")?
2. **Where did you have to pass it by hand?** Did you copy anything out,
   re-type anything, or re-explain something that was already written down?
3. **Did it depend on one person?** Did the thing only work because one
   specific person remembered, carried, or held it together?

If any answer made you pause, that's feedback worth writing down.

## If you want to go deeper

[`everyday-life.md`](everyday-life.md) explains *why* we build the platform
the way we do — the idea that a neighborhood's value is in how its parts are
linked, not in any single part. [`README.md`](README.md) lists the principles
in full. Neither requires technical background; both are written for people
like you.
