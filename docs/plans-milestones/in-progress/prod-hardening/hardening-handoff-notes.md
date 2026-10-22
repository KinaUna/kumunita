# Prod-hardening — rolling handoff notes

One short section per unit, **appended, never rewritten**. Each unit writes
exactly one section before it exits; the next agent reads only that section +
its own unit's entry reads. (M3 precedent: `docs/plans-milestones/done/m3/m3-handoff-notes.md`.)

Format per section:
```
## <fix>.U<n> — <unit title>
- what landed (files, one line each)
- the pin(s) this unit relied on (by id)
- test result (names + pass count) or "no code"
- any Drift pause / open item (owner-ops steps are listed here, NOT assumed done)
```

---

## Status
No units executed yet. First wave (per the plan): F1, F2, F6, F7, F8, F13.
