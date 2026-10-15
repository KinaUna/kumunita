# ADR 0169 — First day of week: a per-resident display preference

Status: Accepted
Date: 2026-10-15
Extends the **ADR 0004 §B.1 delta-detected, idempotent, zero-migration
document-surface shape** (the additive `Profile` field on the existing
`UserInfo` context — the *primary* precedent this ADR re-applies to a
single per-resident preference), the **`SetProfilePageSizeAsync` owner-scope
write-lane shape** (the fail-closed `KeyNotFoundException`, the single
`IUserInfoService` seam, **zero** audit rows), the **frozen
`IAuthorizationService` seam + the frozen `AccessAction` / `AccessVia` sets +
the frozen `Decide()`** (ADR 0006 — "adds **no adapter at all**"), and the
**closed `KnownTranslationKeys` registry + the en/de/fr/da parity pins**
(ADR 0015 / ADR 0052). This ADR makes **the day-of-week pickers and the
events calendar respect each resident's preferred first day of the week**
(Monday by default) with **zero new authorization surface** and **zero
migration**.

## Context

Kumunita renders day-of-week pickers in two places — the **Quiet-hours**
settings (`/settings/quiet`, the `daysOfWeek` multi-select) and the **Guardian
time-limit** settings (`/guardian/{childId}`, the time-limit days multi-select)
— and the **events calendar** anchors its week and month grids to a first day.

Two problems shipped:

1. **The day pickers rendered numbers, not names.** The `<option>` labels were
   the raw `int` day index (`0`–`6`) instead of the localized day name
   (`Monday` / `Dienstag` / `lundi` / `mandag`). A resident could not tell which
   day they were selecting.
2. **The week began on Sunday, which is wrong for the audience.** The calendar
   and the day pickers were implicitly anchored to Sunday (`DayOfWeek` index
   `0`). In the communities this platform serves (DACH + the wider European
   week), the week conventionally begins on **Monday**. There was no way to
   express a preference.

The preference is *display*, not *data*: the `daysOfWeek` values the Quiet and
Guardian pickers submit are already a set of `int` day-indices — reordering the
*display* of those options does not change the *value* submitted. The calendar
anchoring is likewise pure presentation. No entity's stored meaning changes.

The constraint that keeps it safe is the one that already keeps the other
per-resident preferences safe (the `Profile` fields): **compose the frozen
seams, never extend them** — no new `AccessAction`, **no** new `AccessVia`,
**no** branch in `Decide()`, **no** method on `IAuthorizationService`, and **no
adapter at all** (a display preference is *not* an audience decision — the
resident reads and writes only their own, exactly the `SetProfilePageSizeAsync`
shape).

## Decision

The preference is a **single additive `Profile` field** + a **single pure
resolver** + the **existing owner-scope write lane**, surfaced as a radio group
on the **Date & time format** page (the natural home, beside the date/time
format it orders).

### D1 — the preference: additive `Profile.FirstDayOfWeek`

`Profile` (the existing `UserInfo` context doc) gains one field:

```csharp
/// <summary>ADR 0169 — the resident's preferred first day of the week
/// (0 = Sunday … 6 = Saturday; the .NET `DayOfWeek` index). `null` = the
/// platform default, Monday. Additive on the existing doc — delta-detected,
/// idempotent, zero migration (ADR 0004 §B.1).</summary>
public int? FirstDayOfWeek { get; set; }
```

`null` means "use the default" — the exact `PageSize` shape (an optional
preference the resident has not yet set). **Zero migration**: Marten's
delta-detector adds the column on the next `ApplyAllConfiguredChangesToDatabaseAsync`;
existing rows read back as `null` (the default). No EF migration (this is not an
Identity table — ADR 0004 §B).

### D2 — the resolver: pure `WeekAnchor`

The ordering + anchoring logic lives in one small, testable, dependency-free
type so the view, the Quiet picker, the Guardian picker, and the calendar all
share one definition of "order the days starting at first day":

```csharp
namespace Kumunita.Core.Query;

/// <summary>ADR 0169 — resolves a resident's first-day-of-week preference
/// (0 = Sunday … 6 = Saturday, the .NET `DayOfWeek` index) and orders the
/// week's seven days to start at it. `Default` (Monday) is the platform
/// default when a resident has not chosen one.</summary>
public static class WeekAnchor
{
    /// <summary>The platform default first day — Monday (`DayOfWeek.Monday`).</summary>
    public const int Default = 1;

    /// <summary>Floors an out-of-range preference to the default.</summary>
    public static int Resolve(int? preferred) =>
        preferred is >= 0 and <= 6 ? preferred.Value : Default;

    /// <summary>The seven day-indices of a week ordered to start at
    /// <paramref name="firstDay" />, wrapping around (e.g. firstDay = 0 →
    /// [0,1,2,3,4,5,6]; firstDay = 1 → [1,2,3,4,5,6,0]).</summary>
    public static int[] OrderDays(int firstDay)
    {
        var days = new int[7];
        for (var i = 0; i < 7; i++) days[i] = (firstDay + i) % 7;
        return days;
    }
}
```

`Resolve` is total: any input (including `null` and out-of-range values) yields
a valid first-day. `OrderDays` is a pure permutation of `0`–`6` — the same set,
a different order, so the *submitted value* of any picker is unchanged by the
reorder. `Default = 1` is Monday (`.NET` `DayOfWeek.Monday`), the audience's
convention, and the value every pre-existing code path effectively assumed for
the *calendar*.

### D3 — the write lane: `SetProfileFirstDayOfWeekAsync`

`IUserInfoService` gains one seam, mirroring `SetProfilePageSizeAsync` exactly
(load-or-fail-closed, set, store, save):

```csharp
/// <summary>ADR 0169 — sets the resident's preferred first day of the week
/// (0 = Sunday … 6 = Saturday; pass the default Monday, 1, to clear the
/// preference back to the platform default). Owner-scope, the
/// <c>SetProfilePageSizeAsync</c> shape — fail-closed on a missing profile,
/// zero audit rows (a display preference is not an audience decision).</summary>
Task SetProfileFirstDayOfWeekAsync(string subjectId, int? firstDayOfWeek, string actorBy);
```

`UserInfoService.SetProfileFirstDayOfWeekAsync` loads the profile (fail-closed
`KeyNotFoundException` when absent — the same contract as every other
owner-scope preference write), sets the field, stores, and saves. **Zero** audit
rows (a display preference carries no audience/permission meaning — the
`SetProfilePageSizeAsync` precedent), **zero** new authorization surface.

### D4 — the UI: a radio group on the Date & time format page

The preference is set on **`/settings/dateformat`** (the "Date & time format"
page — the natural home beside the date/time format it orders). The view gains
a "First day of week" radio group, rendered in the resident's own preferred
order (so the chosen first day is always the first option):

```html
<fieldset>
  <legend>First day of week</legend>
  @foreach (var d in Kumunita.Core.Query.WeekAnchor.OrderDays(Model.FirstDayOfWeek))
  {
    <label>
      <input type="radio" name="firstDayOfWeek" value="@d"
             @(Model.FirstDayOfWeek == d ? "checked" : "") />
      @DayName(d)   @* localized name via the effective language's DateTimeFormatInfo *@
    </label>
  }
</fieldset>
```

`LocaleController.BuildModel()` populates
`FirstDayOfWeek = WeekAnchor.Resolve(profile?.FirstDayOfWeek)` (the same
`Resolve` the pickers use — one definition). `SaveDateFormat` posts
`firstDayOfWeek` and writes **only** when a valid `0`–`6` value is posted:

```csharp
if (int.TryParse(firstDayOfWeek, out var fd) && fd is >= 0 and <= 6)
    await userInfo.SetProfileFirstDayOfWeekAsync(subject, fd, subject);
```

The **reset** form (the "reset to platform default" button) posts `clear=1`
**without** a day value, so the preference is left untouched — resetting the
date *format* does not clobber the day preference. This is a deliberate
separation: the two are independent preferences on independent fields.

### D5 — the day pickers: reordered, named

Both day pickers (Quiet-hours `daysOfWeek`, Guardian time-limit days) now:

- render the **localized day name** (the D0 fix — no more `0`–`6` numbers), via
  the effective language's `DateTimeFormatInfo.GetDayName` /
  `CultureInfo.CurrentCulture.DateTimeFormat.GetDayName`; and
- **order** their options via `WeekAnchor.OrderDays(firstDay)`, where
  `firstDay` is the *relevant* resident's preference — the resident themselves
  for `/settings/quiet`, and the **child's** preference for the Guardian
  time-limit page (`ViewData["ChildFirstDayOfWeek"] = WeekAnchor.Resolve(childProfile?.FirstDayOfWeek)`).

The **value** submitted is unchanged (the same `int` set, reordered) — so the
existing Quiet/Guardian write lanes, validation, and stored data are all
untouched; only the *presentation order* and the *label* change.

### D6 — the calendar: re-anchored, default byte-identical

The events calendar (`EventController.Calendar`) anchors its week and month
grids to the *viewer's* first day (`WeekAnchor.Resolve(viewerProfile?.FirstDayOfWeek)`):

- **Week view**: the week start is the Monday (or the viewer's chosen first day)
  of the anchor date's week — `anchorDate.AddDays(-WeekOffset(anchorDate))` where
  `WeekOffset(d) = ((int)d.DayOfWeek - firstDay + 7) % 7`.
- **Month view**: the grid spans the weeks covering the anchor month, with the
  first and last rows aligned to the chosen first day.

**The default (Monday) path is byte-identical to the pre-existing formula.** The
pre-existing month-grid code produced a *ragged* grid (e.g. 34 days for Sep-2026,
not a clean 35) — the pinned test
`Calendar_ViewMonth_WindowIsAnchorCalendarMonth` asserts `WindowDays[0] == gridMonday`
+ the exact count. To keep that invariant (and the pinned test) passing, the
month case keeps the **original formula verbatim** when `firstDay` is the Monday
default, and uses the clean full-weeks formula only for a non-Monday preference:

```csharp
if (firstDay == WeekAnchor.Default)
{
    // original formula, verbatim — ragged-month invariant + pinned test preserved
    gridStart = monthStart.AddDays(-(((int)monthStart.DayOfWeek + 6) % 7));
    gridEndExclusive = monthEndInclusive.AddDays(1 + ((6 - (int)monthEndInclusive.DayOfWeek) % 7));
}
else
{
    gridStart = monthStart.AddDays(-WeekOffset(monthStart));
    // …clean full-weeks formula, firstDay-anchored
}
```

A Monday default is therefore **byte-identical** to the shipped behavior (the
reduced `WeekOffset` is `(dow + 6) % 7`, the original expression) — no existing
calendar rendering or pinned test changes for the default case; only a resident
who *chose* Sunday gets the re-anchored grid.

## Consequences

- **Day pickers show names, ordered to the resident's week.** The numeric
  `0`–`6` labels are gone (both Quiet and Guardian); the options are localized
  day names ordered to begin on the resident's (or, for Guardian, the child's)
  preferred first day. The **submitted value is unchanged** — the existing
  write lanes, validation, and stored data are all untouched.
- **The week begins on Monday by default, and each resident can choose
  Sunday.** The preference is per-resident (additive `Profile` field), set on
  the Date & time format page, defaulting to Monday (`WeekAnchor.Default`).
  `Resolve` is total (out-of-range / `null` → default), and `OrderDays` is a pure
  permutation of `0`–`6` (the same set, reordered).
- **The calendar respects the viewer's first day, with a byte-identical
  default.** A Monday default reduces to the pre-existing formula (the ragged-
  month invariant and the pinned `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`
  test are preserved); a non-Monday preference re-anchors the week/month grid.
- **Zero new authorization surface, zero new audit rows.** The write lane is the
  existing owner-scope `IUserInfoService` seam (the `SetProfilePageSizeAsync`
  shape) — fail-closed on a missing profile, no `AccessAction` / `AccessVia` /
  `Decide()` / `IAuthorizationService` change, no adapter. A display preference
  is not an audience decision.
- **Zero migration.** The preference is one additive `Profile` field (the ADR
  0004 §B.1 shape — delta-detected, idempotent, zero migration); no EF
  migration (not an Identity table).
- **`WhatsNew.cs` 0.65.0 (the required close-flip member, ADR 0110):** "First day
  of week — a per-resident display preference (default Monday) that orders the
  Quiet-hours and Guardian time-limit day pickers and anchors the events
  calendar's week/month grid; additive `Profile` field + `WeekAnchor` resolver +
  owner-scope write lane; zero migration; zero new authorization surface"
  (2026-10-15).
- **A named lane, not a milestone.** The M-letter milestone ordering
  (`Milestones.cs`, README Roadmap, `MilestonesTests.cs`) is **byte-identical**
  — the ADR 0013 `GP` / ADR 0011 `M4-adjacent` precedent. The close flips the
  doc-sync pair (README Roadmap + `STATUS.md` + `ARCHITECTURE.md`) + the
  `WhatsNew.cs` entry above.
