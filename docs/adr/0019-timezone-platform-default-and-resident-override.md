# ADR 0019 — Timezone: platform default (admin) + per-resident override

Status: Accepted
Date: 2026-09-15

## Context

A neighborhood is a **local** thing, but its residents are **distributed in
time**: a single community can span time zones (a resident who has moved away,
a family split across regions, a shared community whose members are
geographically dispersed). The platform renders timestamps — a post's `Created`
/ `Modified`, a reply's, an announcement's, a moderation decision's `At` — and
until now those were rendered with the **server-local** clock (`.LocalDateTime`
/ `.ToLocalTime()`). That is wrong in two distinct ways:

1. **It depends on the host's clock.** A container's TZ environment variable —
   which is an operator detail, not a product decision — silently determines
   what time every resident sees. That is the opposite of "boring where it can
   be": a deployment detail leaks into the user-visible surface.
2. **It is the same time for everyone.** There is no notion that the *author's*
   or the *reader's* local time might be the meaningful one.

The correct model is a **three-tier fallback**, mirroring the ADR 0005
multilingual precedent exactly (platform default + user override + a hard
floor):

- A **platform default** time zone, set once by a GlobalAdmin, expresses "this
  neighborhood's home time zone" (e.g. the town the community serves).
- A **per-resident override**, set in the resident's own settings, lets an
  individual who lives elsewhere see their own local time.
- A **hard floor** of `UTC` ensures the system never renders in an
  unspecified or OS-dependent zone — the same `en` floor ADR 0005 uses for
  language, and the same "data, not config" posture (a runtime data value on a
  document, not a restart-required config).

This is the natural companion to ADR 0005 (multilingual). Both are "a
platform-wide default that a resident can override" features, both are
server-rendered, both are data-driven (no rebuild, no restart), and both use
the same "admin writes a singleton + audited lane; resident writes their own
setting" shape. ADR 0005 is about **language** (a *string* resolved per view);
this ADR is about **time** (an *instant* resolved per rendered timestamp).
They share the "instance default + user override" shape but the resolution
mechanics differ: language resolves a *string* to a *string*; timezone
converts a *stored instant* to a *wall-clock string* in the effective zone.

## Decision

- **Add a `TimeZone` (IANA id, e.g. `UTC`, `Europe/Warsaw`, `America/New_York`)
  field to two documents, both additive (ADR 0004 §B.1):**
  - `Kumunita.Core.UserInfo.Profile.TimeZone` (`string?`) — the resident's
    personal override. `null` means "no override; use the platform default."
  - `Kumunita.Core.Localization.LocaleSettings.DefaultTimezone` (`string`,
    default `"UTC"`) — the platform default, on the **same singleton** that
    holds `DefaultLanguageCode` (ADR 0005 B). A single "platform locale
    defaults" document, not a second singleton.

- **Two write seams, one per audience, mirroring the language ADR exactly:**
  - **Resident override:** `IUserInfoService.SetProfileTimezoneAsync(subjectId,
    timezone, actorBy)` — the **single write lane** on the UserInfo side, the
    exact shape of the avatar lane (`SetProfileAvatarAsync`): owner-scoped (the
    Web boundary's job), **no audit row** (the resident is changing their own
    setting, not a platform state), `KeyNotFoundException` fail-closed (never
    load-or-create). `null` clears (fall back to the platform default).
  - **Platform default:** `ILocalizationService.SetDefaultTimezoneAsync(
    timezoneId, actorId)` — the **single audited write lane** on the
    Localization side, the exact shape of `SetDefaultLanguageAsync`: exactly
    one `AccessAudit` row (`Action = "timezone.set-default"`, `TargetKind =
    "timezone"`, `TargetId = timezoneId`, `Via = Admin`, `Outcome = Allow`,
    `ActorId = actorId`), fail-closed (`InvalidOperationException` on a blank
    or unknown id, thrown **before** any write → no audit row for the blocked
    attempt — the same `RemoveLanguageAsync` M·7 pin).

- **Two read seams (no audit rows, matching the read-side shape of the
  language ADR):**
  - `IUserInfoService.GetProfileAsync(subject)` (existing) — reads
    `Profile.TimeZone` for the override.
  - `ILocalizationService.GetDefaultTimezoneAsync()` (new) — returns
    `LocaleSettings.DefaultTimezone` with the `"UTC"` floor (a blank stored
    value reads as `UTC`), the same "read + floor" shape as
    `GetDefaultLanguageCodeAsync`.

- **Resolution (the feature's contract), per request:**
  1. The **signed-in actor's** `Profile.TimeZone` (override).
  2. The **platform default** (`LocaleSettings.DefaultTimezone`).
  3. The **`UTC` floor** (never a throw, never a blank).

  The resolution lives in the **Web layer** (`EffectiveTimezoneResolver`,
  scoped, per-request, cached) — the Web boundary that already owns the
  per-request principal (the `kw-l` TagHelper's seam). Core never resolves; it
  only stores the two values. This is the exact split the ADR 0005
  `TranslationProvider` / `ITranslationProvider` uses (Core stores, Web
  resolves per request).

- **Rendering (`kw-dt` TagHelper, the ADR 0015 `kw-l` precedent):**
  - A new `<kw-dt dt="…" format="…"></kw-dt>` element (a Razor view emits the
    real values, e.g. `<kw-dt dt="@Post.Created" format="g">`), a `TagHelper`
    in `Kumunita.Web.TagHelpers` (mirroring `kw-l`), that takes a
    `DateTimeOffset?` and a .NET custom format string and emits the instant
    **converted to the effective zone** resolved by
    `EffectiveTimezoneResolver`.
  - The conversion is a **wall-clock conversion** (UTC instant → the effective
    zone's local time), applied **before** the format string — the same
    "resolve first, render second" shape as `kw-l`.
  - A `null`/blank `dt` renders as the empty string (a `Modified` the author
    has not set renders nothing — the same "no value → no text" shape the
    views already use when the `Modified is not null` check gates the edit
    line).
  - The 15 timestamp render sites (posts, replies, announcements, moderation)
    replace their `.LocalDateTime.ToString(fmt)` / `.ToLocalTime().ToString(fmt)`
    calls with `<kw-dt dt="…" format="fmt"></kw-dt>` — a **drop-in replacement
    that changes the zone, not the format string** (the format contract is the
    same .NET custom format string the views were already passing to `ToString`).

- **Admin surface (`/admin/timezone`, GlobalAdmin-gated):** a new dedicated
  controller (`AdminTimezoneController`), **not** a new action on
  `AdminController` — the `AdminController`'s constructor is pinned by two
  Web-layer test harnesses (`AdminControllerBlockTests` /
  `AdminControllerMandatoryTests`), and adding a dependency there would break
  them. The dedicated controller mirrors the `/admin/languages`
  (`LanguagesController`) precedent (the platform-default **language** is also
  on its own controller), the convention-consistent shape for a new platform
  default. `POST /admin/timezone` maps the service's `InvalidOperationException`
  to **409** + a surfaced error (the optimistic-concurrency story, the
  `Remove` precedent).

- **Resident surface (`/settings/timezone`, `[Authorize]`):** a new dedicated
  controller (`TimezoneController`), the exact shape of the
  `/settings/language` (`LocaleController`) page — the resident's own
  override, self-scoped (the `[Authorize]` gate + the actor being the subject),
  a `clear=1` action that sets the override to `null` (fall back to the
  platform default), and a `KeyNotFoundException` fail-closed catch. The
  account nav gets a `Time zone` link next to the existing `Language` link.

- **Seed (first boot):** `FirstBootSeeder.SeedLanguageCatalogAsync` sets
  `LocaleSettings.DefaultTimezone = "UTC"` on the fresh singleton (explicit,
  alongside `DefaultLanguageCode = SourceLanguage`) — the `UTC` floor is
  **materialized** on a fresh database, not just a read-time default.
  `SeedM1RowAsync` in the tests mirrors this.

- **No config, no restart:** both values are **data** on `mt`-schema
  documents (a `Profile` field + a `LocaleSettings` field), not
  appsettings. A change is live on the very next request — the same
  "M·4: data, not config" posture as ADR 0005's language default.

## Consequences

Positive
- **The host's clock is no longer a product detail.** A deployment's TZ
  environment variable no longer determines what time every resident sees. The
  platform renders in the **platform default** (an explicit, admin-set,
  audited value) unless a resident overrides it. This is the
  "boring where it can be" principle applied correctly: the *user-visible*
  surface is a *product* decision, not an *operator* accident.
- **The resident is in control of their own time.** A resident who lives in a
  different time zone from the neighborhood's default can see their own local
  time without affecting anyone else. The override is per-resident, persisted
  on their own `Profile`, and a single `null` write clears it (fall back to
  the platform default).
- **The `UTC` floor is a hard invariant.** Neither a blank admin entry nor a
  missing resident override can produce a render in an unspecified zone. The
  `EffectiveTimezoneResolver` enforces it; `SetDefaultTimezoneAsync` validates
  before write; the seed materializes it on first boot.
- **Mirrors the ADR 0005 precedent exactly.** The "admin writes a singleton +
  audited lane; resident writes their own setting; resolution is per-request"
  shape is the same, the seams are the same, the test pattern is the same.
  A developer who knows one knows the other. The `kw-dt` TagHelper is the
  exact analog of the `kw-l` TagHelper (ADR 0015) — the same "per-request
  value rendered across many views" problem, the same solution.
- **The 15 render sites are a drop-in replacement.** The format string is
  unchanged; only the zone changes. The views' `Modified is not null` guards
  are preserved (the `kw-dt` TagHelper's `null` → empty-string rule is the
  same "no value → no text" shape the guards already express).

Negative / accepted risks
- **Two "default" code paths exist:** the Core service
  (`SetDefaultTimezoneAsync`) reads `LocaleSettings.DefaultTimezone` directly
  (it runs inside a write session and already holds one), and the Web layer
  (`EffectiveTimezoneResolver`) reads the same row through
  `GetDefaultTimezoneAsync()` at read time. They read the same row, so they
  agree — the same "two call sites that must keep the same `UTC` floor" risk
  ADR 0005's language ADR names. The `GetDefaultTimezoneAsync` seam is the
  single read path the Web uses; the Core service reads directly because it is
  in a write session.
- **The `Zones()` picker is OS-dependent.** The `<select>` options are
  `TimeZoneInfo.GetSystemTimeZones()` (the IANA/Windows ids the OS knows),
  filtered to exclude the `*` pseudo-zones. A Windows host and a Linux host
  will enumerate different ids (Windows uses `America/New_York`, Linux uses
  `Europe/Warsaw`, etc.). This is **accepted**: the platform is
  single-deployment (ADR 0002), the host's OS is the deployment's choice, and
  the `EffectiveTimezoneResolver.TryConvert` `catch` block treats an
  unrecognized id as "fall through to the next tier" (never a throw). A
  cross-OS admin (an admin on Windows setting a default that a Linux host
  doesn't recognize) would see the `UTC` floor, not an error. This is the
  "boring where it can be" principle: the system degrades gracefully to the
  floor rather than erroring on an OS-specific id.
- **The `TimeZone` field is a `string`, not a `TimeZoneInfo`.** Storing the
  id (not the object) is the correct choice (a `TimeZoneInfo` is not
  serializable, not comparable, and not a stable value to persist), and it
  matches the ADR 0005 "store the code, resolve the string" pattern. The
  cost is that a `TimeZoneInfo` must be re-resolved from the id on every
  render (the `EffectiveTimezoneResolver.TryConvert` lookup) — but the
  resolver caches its first resolution per request, so this is a single lookup
  per request, not per timestamp.

## Related

- **ADR 0005** — multilingual support (the "platform default + user override"
  shape this ADR mirrors; §C UGC "never translated" is *unchanged* by this
  ADR — this is a *display* change, not a *translation*).
- **ADR 0004 §B.1** — additive schema-evolution pattern both new fields
  follow (no re-seed, no breaking change).
- **ADR 0002** — deployment topology (single-deployment, single-neighborhood;
  the "host's OS is the deployment's choice" risk in the Consequences is
  scoped by this ADR's single-deployment assumption).
- **ADR 0015** — UI view-localization mechanics (the `kw-l` TagHelper this
  ADR's `kw-dt` TagHelper mirrors; the same "per-request value rendered across
  many views" problem, the same solution).
- **ADR 0018** — UGC authored-in language tag (a different axis: that ADR
  stores *what language* a piece of UGC is in; this ADR stores *what time zone*
  to render timestamps in — the two are orthogonal and compose cleanly).
