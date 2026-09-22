# U00 — Sign-off + ADR 0054 + the `M4` milestone row (no code beyond the ADR)

- **Lane:** Events (`M4`)
- **Unit:** U00 (of U00–U12)
- **Kind:** sign-off / governance (docs + the roadmap trio — no domain code)

## Goal

Lock the **[PROPOSED]** decisions in `docs/design/m4-events-design.md` into
the accepted **ADR 0054**, add the **`M4`** named-lane row to the roadmap
trio (`Milestones.cs` + README + `MilestonesTests.cs`), and flip the design
doc's markers to **[DECIDED — ADR 0054]**. This unit **authors the design
doc** (the primary reference tier) and **creates the ADR** (the decision
record) — no domain code, no tests beyond `MilestonesTests`.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` — the **primary-tier template** to emulate
   (the §1 "What this lane is" + §2 "The existing surface" + §3 "The design
   decisions" + §4 "The seams" structure; the **[PROPOSED]** / **[DECIDED]**
   marker convention; the "The one thing every unit must respect" block).
2. `docs/adr/0039-pages-hierarchy-audience-translations.md` — the ADR format
   to emulate (Status / Date / Amends / Context / Decision / Consequences;
   the `Amends` clause that names the ADRs this one builds on).
3. `src/Kumunita.Web/Milestones.cs` — the `M4` row (currently
   `StatusPlanned`; U00 flips it to **`StatusNext`**) and the `PG` row
   (currently `StatusNext`; U00 flips it to **`StatusDone`** — PG already
   shipped: U00–U07 green, `LocalizedPage` retired — so it joins the
   `StatusDone` set; the single `StatusNext` pin moves to M4).
4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `Ids` order array is
   unchanged (it already reads `… PG, M4, M5, M6`); the `Shipped` list gains
   `PG`; and the single-in-progress pin test is **renamed** from
   `PG_Is_The_Single_InProgress_Milestone` to name `M4` (the `StatusNext`
   id becomes `M4`).
5. `README.md` — the Roadmap `M4` row flips from "**Planned.**" to
   "**In progress.**"; the `PG` row flips from "**Next.**" to
   "**Done.**".

## Deliverables (closed set, ≤ 5 files)

1. **`docs/design/m4-events-design.md`** — the **primary reference tier**
   (~250–350 lines). Sections:
   - `## 1. What this milestone is` — the coordination arrow; the `Event` +
     `EventRsvp` documents; the `EventToAuditableResource` adapter; the
     `EventService` read + write surface; the `EventReminders` §6.4 job;
     the `EventController` + views; the nav entry.
   - `## 2. The existing surface this milestone builds on (verified)` — the
     `Post` / `PostReply` + `PostToAuditableResource` + `PostService`
     (M3); the `Announcement` + `AnnouncementService` (M3b); the `Audience`
     doc + `AudienceEditorModel` (M2); the `MarkdownRenderer` +
     `bindRichEditor` (RC / RE); the `IMailerStage` + `OutboxEmail` +
     `OutboxEmailHandler` (M1 step 7); the `AuditPurgeHandler` +
     `AuditPurgeTick` (the §6.4 job precedent); the `kw-dt` TagHelper
     (ADR 0019 / 0020); the `LocaleSettings` + `ILocalizationService`
     (ADR 0005); the `FirstBootSeeder` (the M1 seeder); the
     `DependencyInjection.cs` (the registration shape).
   - `## 3. The design decisions` — each **[PROPOSED]** until U00 locks
     them:
     - `### 3.1 The `Event` field set` — `Id`, `Title`, `Body` (Markdown),
       `ComponentId?`, `AuthorId`, `Start` (DateTimeOffset), `End`
       (DateTimeOffset), `Location?`, `Capacity?` (int), `Audience`
       (the **exact** post `Audience`), `ReminderEnabled` (bool, default
       true), `IsDraft` (bool, default true — ADR 0037), `IsDeleted`
       (bool, default false — ADR 0024), `LanguageCode` (the ADR 0018
       authored-in language), `TagIds` (the ADR 0044 tag ids), `ImageIds`
       (the ADR 0025 content-image ids), `AttachmentIds` (the ADR 0034
       attachment ids), `Created`, `Modified?`.
     - `### 3.2 The `EventRsvp` shape` — `Id`, `EventId`, `UserId`,
       `Status` (enum: `Going` | `Maybe` | `No`), `At`. **Last-write-wins**
       concurrency exception (the `docs/ARCHITECTURE.md` §5 exception —
       keyed per `(EventId, UserId)`; a conflicting RSVP is a no-op, the
       resident's latest status is the truth). **No `AccessAudit` row**
       (a routine resident action, not an access decision).
     - `### 3.3 The `EventToAuditableResource` adapter` — the
       `PostToAuditableResource` shape verbatim: `Id => Event.Id`,
       `Name => Event.Title ?? Event.Body[..60]`, `OwnerId => AuthorId`,
       `Audience => Event.Audience`, `ComponentId => Event.ComponentId`,
       `TargetKind => "event"`.
     - `### 3.4 The standing matrix` — author-only edit (ADR 0014 / 0016 /
       0017 precedent); GlobalAdmin override; the `CheckCreateStanding` /
       `CheckEditStanding` gate (the `AnnouncementService.CreateAsync`
       C3 pattern).
     - `### 3.5 The draft / tag / media / language / delete lane reuse` —
       the `IsDraft` flag (ADR 0037); the `TagIds` (ADR 0044); the
       `ImageIds` + `AttachmentIds` (ADR 0025 / 0034); the `LanguageCode`
       (ADR 0018); the `IsDeleted` flag (ADR 0024). **No new mechanism**
       for any of these.
     - `### 3.6 The `EventReminders` §6.4 job` — the `EventReminderService`
       (Wolverine-free, the `AuditPurgeService` precedent); the
       `EventReminderHandler` + `EventReminderTick` (the
       `AuditPurgeHandler` + `AuditPurgeTick` precedent verbatim — the
       self-rescheduling `TimeoutMessage(TimeSpan.FromDays(1))` idiom);
       the 24-hour window (the "remind the day before" semantics); the
       `Going` RSVP filter (the `EventRsvp.Status = Going` rows are the
       recipients); the author-inclusion rule (the author is reminded
       even if they did not RSVP); the idempotency key
       `remind:{eventId}:{userId}` (the §6.2 per-email key scheme).
     - `### 3.7 The 23 pinned seam test names` — the exact test names
       (for U09's implementation).
     - `### 3.8 The three-test acceptance gate` — closed-loop / handoff /
       part-vs-whole (the M2 / M3 gate shape).
     - `### 3.9 The drift-guard` — the frozen pins (the `Event` field set,
       the `EventRsvp` shape, the `EventToAuditableResource` adapter, the
       `EventService` public methods, the 23 test names, the three-test
       gate).
   - `## 4. The seams (exact C#)` — the `IEventService` interface (the
     public method signatures); the `EventReminderService` static class
     (the `SendRemindersAsync` method); the `EventReminderHandler` static
     class (the `Handle` method); the `EventReminderTick` record (the
     `TimeoutMessage(TimeSpan.FromDays(1))` base).
2. **`docs/adr/0054-events-rsvp-reminders.md`** — the ADR (Status:
   **Accepted**, Date 2026-09-20). `Amends`: 0001-B (the `Audience` doc),
   0006 (the `IAuthorizationService` frozen surface), 0036 (the
   community-visible audience default), 0025 (the `MarkdownRenderer`),
   0031 (the `bindRichEditor`), 0037 (the `IsDraft` flag), 0024 (the
   `IsDeleted` flag), 0018 (the `LanguageCode`), 0044 (the `TagIds`),
   0034 (the `AttachmentIds`), 0019 / 0020 (the `kw-dt`). Decision
   section encodes the `Event` field set, the `EventRsvp` shape, the
   adapter, the standing matrix, the lane reuse, the §6.4 job shape, the
   idempotency key, the 23 test names, the gate, the drift-guard.
3. **`docs/adr/README.md`** — the `| 0054 | … | Accepted |` index row.
4. **`src/Kumunita.Web/Milestones.cs`** — the `M4` row flips from
   `StatusPlanned` to **`StatusNext`** (work starts); the `PG` row flips
   from `StatusNext` to **`StatusDone`** (PG already shipped — U00–U07
   green, `LocalizedPage` retired). This is the **single `StatusNext`
   pin** that `MilestonesTests` expects — M4 takes the slot; PG closes.
5. **`README.md`** — the Roadmap `M4` row flips from "**Planned.**" to
   "**In progress.**"; the `PG` row flips from "**Next.**" to
   "**Done.**".

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the `MilestonesTests` family passes; the single-in-progress pin
  holds).
- ADR 0054 present + Accepted; the design doc has no remaining
  **[PROPOSED]** markers (all resolved to **[DECIDED — ADR 0054]**).
- **No other code changed.** The `Post` / `Announcement` / `Page`
  surfaces are **untouched** (the M4 lane is additive on top of the
  existing lanes).
- Append a `## U00 — sign-off + ADR 0054` note to
  `m4-handoff-notes.md`: 5–6 lines listing (a) the ADR number + date,
  (b) the design doc path, (c) the 23 test names (by id), (d) the
  three-test gate (by name), (e) the `M4` row status in `Milestones.cs`
  (currently `StatusPlanned`), (f) any drift pauses.

## Notes / deviations

- U00 is a **governance** unit — it owns the ADR + the design doc + the
  roadmap trio and nothing else. It does **not** create any `Event` /
  `EventRsvp` doc, any controller, any service, any test beyond
  `MilestonesTests`. Those are U01+.
- The `M4` row is **`StatusNext`** (the single `StatusNext` pin —
  `PG` flips to `StatusDone` in the same commit, since PG already
  shipped: U00–U07 green, `LocalizedPage` retired). The repo's
  `MilestonesTests` contract (order + single-in-progress) holds with the
  new pin (PG in the shipped set, M4 the single in-progress).
- The design doc's §3.6 (the `EventReminders` §6.4 job) is the **only**
  new scheduled job in M4 — the `AuditPurgeHandler` precedent is the
  sanctioned idiom, and the `EventReminderHandler` follows it
  verbatim (the `TimeoutMessage(TimeSpan.FromDays(1))` shape, the
  self-rescheduling `Handle` method, the Wolverine-free
  `EventReminderService` business logic).
- The `Event` body is **rich content** (ADR 0025 / 0031) — the same
  `MarkdownRenderer` + `bindRichEditor` surface the post / announcement /
  group-post / page composers already use. **No new renderer, no new
  editor, no new TS module.**
- The `Event` timestamps are **`kw-dt`** (ADR 0019 / 0020) — the same
  per-request resolver the other surfaces already use. **No new timezone
  or format mechanism.**
