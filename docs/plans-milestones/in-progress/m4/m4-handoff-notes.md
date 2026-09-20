# M4 — Events, RSVPs & reminders — rolling handoff log

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-read list.

## Lane open

M4 plan created 2026-09-20. The M4 milestone is the **coordination** arrow
(events + RSVP + reminders). 13 units (U00–U12), each sized for a ~32K-context
fresh agent. The design doc is `docs/design/m4-events-design.md` (U00 authors
it). ADR 0054 is the decision record (U00 locks it). The §6.4 scheduled job
(`EventReminders`) is U07/U08. The acceptance gate is U11. The close is U12.

**Open decisions for U00 (resolve in the design doc §3.1 + §3.2):**

1. **Event field naming drift vs `docs/ARCHITECTURE.md` §5.** The plan and
   the ADR use `Body` (rich content, mirroring `Announcement.Body`) and
   `ReminderEnabled` (bool, opt-out). `docs/ARCHITECTURE.md` §5's Events
   sketch uses `description` and `rsvpRequired`. The design doc is the
   primary tier and locks the canonical name; **U12 (close) must sync the
   ARCHITECTURE.md §5 `Events` block to whatever the design doc settles**
   (per the AGENTS.md doc↔code parity rule). If the design doc picks the
   ARCHITECTURE.md names, U01–U10 plans all shift (`Body` → `description`,
   `ReminderEnabled` → `rsvpRequired`); if it keeps the plan names, U12
   rewrites the ARCHITECTURE.md §5 block.
2. **Milestones pin handoff.** PG **already shipped** (its lane
   `U00–U07` is green and `LocalizedPage` is retired — see
   `docs/plans-milestones/pages/pages-handoff-notes.md`). The roadmap trio
   is stale: `Milestones.cs` still shows PG as `StatusNext` and M4 as
   `StatusPlanned`. U00's roadmap step **closes PG (`StatusDone`) and
   opens M4 (`StatusNext`)** in the same commit, and renames the
   single-in-progress pin from `PG_...` to `M4_...`. This is **not** a
   pull-forward over in-flight work — it is a clean close of the finished
   lane and the open of the next one.

**Both open decisions are resolved by the Lane-open section above** (U00
resolves #1 in the design doc §3.1 + ADR 0054 Context — keeping the plan's
`Body` / `ReminderEnabled` names; and resolves #2 in this commit). No
further open decisions carried into U01.

## U00 — sign-off + ADR 0054

- **ADR:** **0054** — `docs/adr/0054-events-rsvp-reminders.md`, Status
  **Accepted**, dated **2026-09-20**. The ADR's **Context** records the
  roadmap order (PG SHIPPED / M4 opened, not a pull-forward) and the
  **`Body` / `ReminderEnabled` naming decision** (over
  ARCHITECTURE.md §5's `description` / `rsvpRequired` sketch — the close
  unit U12 syncs §5).
- **Design doc:** `docs/design/m4-events-design.md` (primary reference tier)
  — all §3.x sections marked **[DECIDED — ADR 0054]**; **zero `[PROPOSED]`
  markers remain** (verified by grep). §4 lists the exact C# seams
  (`IEventService`, `EventReminderService`, `EventReminderHandler`,
  `EventReminderTick`, `EventReminderOptions`, the `M4DocTypes` surface).
- **The 23 pinned seam test names** (design doc §3.7 = the master list, ADR
  0054 Decision): T01 `M4_MemberSeesUpcomingEventFeed` · T02
  `M4_NullAudienceEventIsPublic` · T03 `M4_CommunityAudienceSeesFeed` · T04
  `M4_GrantsAudienceOnlyGranteeSees` · T05 `M4_DraftInvisibleToNonAuthor`
  · T06 `M4_PlainMemberCreateAllowed` · T07 `M4_AuthorCanEditOwnEvent` ·
  T08 `M4_PlainMemberEditDenied` · T09 `M4_GlobalAdminOverrideEdit` · T10
  `M4_PublishAuthorOnly` · T11 `M4_SoftDeleteExcludesFromFeedAndDetail` ·
  T12 `M4_AuthorSoftDeleteOwnEvent` · T13 `M4_RsvpLastWriteWins` · T14
  `M4_RsvpUniqueIndexOneRowPerUser` · T15 `M4_RsvpListOwnerOnly` · T16
  `M4_RsvpWritesNoAccessAuditRow` · T17 `M4_AuditRowShape_Create` · T18
  `M4_EventToAuditableResourceShape` · T19 `M4_ReminderWindowFiltersOutsideEvents`
  · T20 `M4_ReminderGoingRsvpsOnly` · T21 `M4_ReminderAuthorAlwaysIncluded`
  · T22 `M4_ReminderIdempotencyKeyShape` · T23 `M4_ReminderWritesNoAccessAuditRow`.
- **The three-test acceptance gate** (design doc §3.8, ADR 0054): **1 —
  closed loop** (author creates a published event → feed shows it, the
  `TargetKind = "event"` aggregate row; author RSVPs Going → visible in the
  owner-only list); **2 — handoff** (a user added to `Audience.Grants` after
  creation sees the event on the next request; the `Delegation` branch is
  handoff-onto-a-delegate); **3 — part-vs-whole** (the 23 names are the
  whole; tests 1–2 are the parts; all pass together in the same
  `Kumunita.Core.Tests` run as the inherited M1–M3 / PG anchors).
- **Roadmap trio (this commit):** `src/Kumunita.Web/Milestones.cs` — **M4
  row = `StatusNext`**, **PG now `StatusDone`** (M5 / M6 stay
  `StatusPlanned` — the M-letter order is untouched); `README.md` — PG
  `**Next.**` → `**Done.**`, M4 `**Planned.**` → `**In progress.**`; the
  Events feature bullet now says "M4 — in progress"; the "next is" line now
  says **PG is done … next is M4** (ADR 0054). `MilestonesTests.cs` —
  `Shipped` set gains `"PG"`; the pin is renamed
  `PG_Is_The_Single_InProgress_Milestone` →
  `M4_Is_The_Single_InProgress_Milestone` (asserts `M4`); the ordered `Ids`
  list is **unchanged** (`… PG, M4, M5, M6`).
- **ADR index:** `docs/adr/README.md` — row **0054** added after the
  existing **0053** row (the index already carried 0051 / 0052 / 0053; this
  commit adds only 0054). Row **0054** present, `Accepted`; the full index
  is 0040→0054 with each row exactly once (verified by count).
- **Exit gate (all green):** `dotnet build Kumunita.slnx -c Debug` →
  **Build succeeded** (zero warnings); `dotnet exec Kumunita.Web.Tests.dll`
  → **Total: 332, Errors: 0, Failed: 0** (the renamed `M4_Is_The_Single_InProgress_Milestone` pin passes). The 23 seam tests themselves are
  implemented by **U09**, not U00 — the gate *names* them; U11 records the
  first green run of the full set.
- **Drift pauses:** **none.** Both open decisions resolved cleanly (naming →
  design doc §3.1 + ADR 0054 Context; pin → this commit). No new ADR was
  needed, no standing cell was inexpressible with existing role claims,
  and no existing seam (`IAuthorizationService` / `IUserInfoService` /
  `IIdentityService` / `IMailerStage`) was re-shaped.
- **Untouched (per scope):** `Post` / `Announcement` / `Page` surfaces,
  the translation lane (events are authored-in-language only in M4), and the
  M5 / M6 roadmap letters.

**U00 exit gate met. STOP — do NOT start U01.**
