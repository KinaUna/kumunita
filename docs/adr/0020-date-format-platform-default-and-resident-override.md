# ADR 0020 — Date & time format: platform default (admin) + per-resident override + custom

Status: Accepted
Date: 2026-09-13

## Context

ADR 0019 (timezone) fixed **which zone** a timestamp is rendered in, but it left
**how the date is written** to the .NET `g` custom format — the `format="g"`
attribute the `kw-dt` TagHelper was already applying. That format is
`M/d/yyyy h:mm tt` (e.g. **`1/2/2026 3:04 PM`**), which is exactly the
ambiguity this feature exists to remove: **is `1/2/2026` January 2 or February
1?** A resident who reads the feed has no way to tell, and there was no way to
ask for a clearer rendering.

The correct model is the **same three-tier fallback** ADR 0019 (timezone) and
ADR 0005 (language) use — platform default + user override + a hard floor —
applied to the *format string* rather than the *zone*:

- A **platform default** date-time format, set once by a GlobalAdmin, expresses
  "how this neighborhood writes dates and times" (e.g. the unambiguous
  `January 2, 2026 15:04`).
- A **per-resident override**, set in the resident's own settings, lets an
  individual with a different taste (or a different reading convention) see
  dates the way they expect.
- A **hard floor** ensures the system never renders with a blank or unusable
  format — the same `en` floor ADR 0005 uses for language and the `UTC` floor
  ADR 0019 uses for time zone.

This is the natural third member of the "instance default + user override"
trio (language / time zone / **date format**). Like its two siblings it is
server-rendered, data-driven (no rebuild, no restart), and uses the same
"admin writes a singleton + audited lane; resident writes their own setting"
shape. The one deliberate difference: **this one is a format string, not a
closed id**, because the requirement is a *short list of presets* plus a
*custom* escape hatch — "others may have different taste, so admins (and
residents) can type a custom format."

## Decision

- **Store the .NET custom datetime format string itself — not a preset id.**
  Both documents carry a `string`:
  - `Kumunita.Core.UserInfo.Profile.DateFormat` (`string?`) — the resident's
    personal override. `null` means "no override; use the platform default."
  - `Kumunita.Core.Localization.LocaleSettings.DefaultDateFormat` (`string`,
    default `DateFormat.FloorFormat`) — the platform default, on the **same
    singleton** that holds `DefaultLanguageCode` and `DefaultTimezone` (ADR
    0005 B / ADR 0019). One "platform locale defaults" document, not a fourth.
  - Both are **additive** fields (ADR 0004 §B.1) — no migration file (Marten
    applies the added `string` column automatically), exactly the shape of the
    `Profile.TimeZone` / `LocaleSettings.DefaultTimezone` additions.

- **Presets are just well-known format strings.** `Kumunita.Core.Localization.DateFormat`
  (a new Core helper) is the single shared source for:
  - `Presets` — a `(Label, Format)` list of four curated options:
    **Long** (`MMMM d, yyyy HH:mm` → `January 2, 2026 15:04`), **Short**
    (`MMM d, yyyy HH:mm` → `Jan 2, 2026 15:04`), **ISO**
    (`yyyy-MM-dd HH:mm` → `2026-01-02 15:04`), **Day-first**
    (`dd/MM/yyyy HH:mm` → `02/01/2026 15:04`).
  - `FloorFormat` — the floor, which is the **Long** preset (chosen because the
    month is fully spelled out, so day and year can never be confused — the
    least-ambiguous of the four).
  - `Match(format)` / `IsValid(format)` — the shared helpers the picker and the
    two write lanes use (`Match` tells the view whether the current value is a
    preset or "Custom…"; `IsValid` is the fail-closed validation).

  Because the stored value is the format string (not an id), a **custom** format
  (one not in `Presets`) is simply another value — **not a special case in
  storage**. Adding a preset later is a one-line change to `Presets` and no
  migration. The presets are all 24-hour (`HH:mm`) and differ only in the date
  portion (the ambiguity being fixed); a custom value may be any .NET custom
  datetime format string.

- **Two write seams, one per audience, mirroring the timezone ADR exactly:**
  - **Resident override:** `IUserInfoService.SetProfileDateFormatAsync(subjectId,
    formatString, actorBy)` — the **single write lane** on the UserInfo side,
    the exact shape of `SetProfileTimezoneAsync`: owner-scoped (the Web
    boundary's job), **no audit row** (a profile field write),
    `KeyNotFoundException` fail-closed (never load-or-create). `null` clears
    (fall back to the platform default). The user lane stores whatever it is
    given (no validation) — a bad value degrades gracefully at read time (see
    resolution), the ADR 0019 posture.
  - **Platform default:** `ILocalizationService.SetDefaultDateFormatAsync(
    formatString, actorId)` — the **single audited write lane** on the
    Localization side, the exact shape of `SetDefaultTimezoneAsync`: exactly
    one `AccessAudit` row (`Action = "dateformat.set-default"`,
    `TargetKind = "dateformat"`, `TargetId = formatString`, `Via = Admin`,
    `Outcome = Allow`, `ActorId = actorId`), fail-closed
    (`InvalidOperationException` on a blank or unusable string, validated via
    `DateFormat.IsValid` **before** any write → no audit row for the blocked
    attempt — the same `RemoveLanguageAsync` / M·7 pin).

- **Two read seams (no audit rows, matching the timezone read-side shape):**
  - `IUserInfoService.GetProfileAsync(subject)` (existing) — reads
    `Profile.DateFormat` for the override.
  - `ILocalizationService.GetDefaultDateFormatAsync()` (new) — returns
    `LocaleSettings.DefaultDateFormat` with the `FloorFormat` floor (a blank
    stored value reads as the floor), the same "read + floor" shape as
    `GetDefaultTimezoneAsync`.

- **Resolution (the feature's contract), per request:**
  1. The **signed-in actor's** `Profile.DateFormat` (override).
  2. The **platform default** (`LocaleSettings.DefaultDateFormat`).
  3. The **floor** (`DateFormat.FloorFormat` = Long).

  The resolution lives in the **Web layer** (`EffectiveDateFormatResolver`,
  scoped, per-request, cached) — the exact companion to ADR 0019's
  `EffectiveTimezoneResolver`. It walks the three tiers and returns the first
  one `DateFormat.IsValid` accepts (the floor is always valid, so it never
  returns null/blank) — the same "degrade to the next tier, never a throw"
  rule the timezone resolver applies to an unknown IANA id. Core never
  resolves; it only stores the two values. This is the exact split the ADR
  0005 / ADR 0015 `TranslationProvider` / `ITranslationProvider` and the ADR
  0019 resolver use (Core stores, Web resolves per request).

- **Rendering (`kw-dt` TagHelper, unchanged contract for the views):**
  - The `<kw-dt dt="…" format="…"></kw-dt>` element now renders with the
    **effective format** resolved by `EffectiveDateFormatResolver`, applied
    **after** the zone conversion (the two choices are independent: ADR 0019
    zone, ADR 0020 format; neither is the culture — the render still uses the
    invariant culture, so the zone/format, not the host locale, is what the
    resident sees).
  - The `format` attribute is kept for backward compatibility but is
    **superseded** by the effective format (which always resolves, so the
    attribute is the documented fallback only). **The 15 view sites are
    untouched** — they still emit `<kw-dt dt="…" format="g">`; the `g` they
    pass is now simply overridden by the setting. A **drop-in change that
    changes the format, not the views** — the lowest-risk option.
  - A `null`/blank `dt` still renders as the empty string (the unchanged
    "no value → no text" rule).

- **Admin surface (`/admin/dateformat`, GlobalAdmin-gated):** a new dedicated
  controller (`AdminDateFormatController`), **not** a new action on
  `AdminController` — the `AdminController`'s constructor is pinned by two
  Web-layer test harnesses, and adding a dependency there would break them. The
  dedicated controller mirrors the `/admin/timezone`
  (`AdminTimezoneController`) and `/admin/languages` precedents exactly. The
  picker offers the four presets **plus a "Custom…" option that reveals a
  free-text box** for a .NET format string (the admin's power-user path).
  `POST /admin/dateformat` accepts either a preset (`format`) or a custom
  string (`customFormat`); it maps the service's `InvalidOperationException`
  to **409** + a surfaced error (the `Remove` precedent).

- **Resident surface (the settings page, `[Authorize]`):** the resident's own
  override is a **section of `/settings/language`** (the `LocaleController`),
  alongside the language and time-zone sections — one settings page, three
  sections. Same picker shape (four presets + "Custom…"), self-scoped
  (`[Authorize]` + the actor being the subject), with a `clear=1` action that
  sets the override to `null` (fall back to the platform default) and a
  `KeyNotFoundException` fail-closed catch. `POST /settings/dateformat`
  keeps the same routes/semantics as the timezone section.

- **Seed (first boot):** `FirstBootSeeder.SeedLanguageCatalogAsync` sets
  `LocaleSettings.DefaultDateFormat = DateFormat.FloorFormat` (Long) on the
  fresh singleton — the floor is **materialized** on a fresh database, not
  just a read-time default (the same posture as `DefaultTimezone = "UTC"`).
  `SeedM1RowAsync` in the tests mirrors this.

- **No config, no restart:** both values are **data** on `mt`-schema documents
  (a `Profile` field + a `LocaleSettings` field), not appsettings. A change is
  live on the very next request — the same "M·4: data, not config" posture as
  ADR 0005's language default and ADR 0019's time zone.

## Consequences

Positive
- **The `1/2/2026` ambiguity is gone by default.** The platform default is now
  the unambiguous "Long" format (`January 2, 2026 15:04`), and any resident or
  admin can change it (or a resident can set their own) to the convention they
  expect — without a rebuild, without touching the host.
- **A power-user escape hatch, with no special case.** Because the stored value
  is the format string itself, "custom" is just another value; the presets are
  a curated convenience over the same underlying type. Adding a preset is a
  one-line change and a migration-free edit (an additive `string` field + a
  `Presets` entry).
- **Mirrors the ADR 0005 / ADR 0019 precedent exactly.** The "admin writes a
  singleton + audited lane; resident writes their own setting; resolution is
  per-request" shape is the same, the seams are the same, the test pattern is
  the same. A developer who knows the timezone feature knows this one.
  `EffectiveDateFormatResolver` is the exact analog of
  `EffectiveTimezoneResolver`; `kw-dt` already had the shape for it.
- **The 15 render sites are untouched.** The views still emit
  `<kw-dt dt="…" format="g">`; only the effective format the TagHelper applies
  changes. The `Modified is not null` guards and the `null → empty-string` rule
  are preserved.

Negative / accepted risks
- **The `format` attribute is now misleading.** The views still pass
  `format="g"`, but it is superseded by the effective format. This is
  **accepted** (the lowest-risk option — the 15 views are untouched); the
  attribute is documented as a fallback. A future pass could drop the attribute
  from the views now that it is inert.
- **The user lane does not validate (by design).** A resident's custom format
  is stored as-is; a malformed value degrades to the platform default / floor at
  read time (the resolver's `IsValid` tier-walk, the ADR 0019 posture). Only
  the **admin** lane fail-closes with a 409. This is the deliberate
  "resident is never blocked by their own typo; the admin sees a clear error"
  split the rest of the settings surface uses.
- **Two "default" code paths exist:** the Core service
  (`SetDefaultDateFormatAsync`) reads `LocaleSettings.DefaultDateFormat`
  directly (it runs inside a write session and already holds one), and the Web
  layer (`EffectiveDateFormatResolver`) reads the same row through
  `GetDefaultDateFormatAsync()` at read time. They read the same row, so they
  agree — the same "two call sites that must keep the same floor" risk the
  timezone ADR names. The `GetDefaultDateFormatAsync` seam is the single read
  path the Web uses; the Core service reads directly because it is in a write
  session.
- **A `DateTimeOffset` rendered in a zone, formatted in the chosen format.**
  The zone (ADR 0019) and the format (this ADR) are resolved independently and
  composed in the TagHelper (convert to zone first, then format). A resident
  who sets both a zone and a format gets the zone's wall-clock time written in
  the chosen format — the two choices do not interact, which is the point.
