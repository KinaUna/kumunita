---
id: dev-razor-verification
title: "Razor verification doctrine"
tier: living
kind: guide
status: current
audiences: [developer, agent]
related:
  - AGENTS.md
  - docs/dev/README.md
  - docs/plans-milestones/improve-docs/plan-improve-docs.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** how to verify a rendering bug — the rendered HTML is the
> evidence, not the `.cshtml`, and the user's report outranks your source
> reading. Includes the known Razor traps in this codebase (each has actually
> bit us) and how to get a live server to verify against. **Who is it for:**
> a developer or agent fixing a rendering / markup bug. **When to read the
> rest:** the moment a user reports a page looks wrong, or before you tell
> them "it's stale, refresh it".

# Razor verification doctrine

When a user reports a rendering bug, **the rendered HTML is the evidence, not
the source file** — and the user's report outranks your source reading.

- **Never conclude "stale build / stale process / stale deployment"** unless you
  have confirmed the served bytes differ from what the current source would
  produce. If you're about to tell the user "this is stale, refresh it", you
  own that claim and it has been wrong before.
- **Verify against the served page**, not the `.cshtml`: fetch the real HTML
  (`curl` with a session cookie, or the integrated browser) and
  inspect the actual markup. "The view file looks right" is not verification.
- Known Razor traps in this codebase (each of these has actually bit us):
  - **Un-awaited `async Task<string>` helpers** interpolated into a string render
    the type name (`System.Runtime.CompilerServices.AsyncTaskMethodBuilder...`).
    When a banner/label looks wrong, grep every call site of the async
    helper for a missing `await` (this hit `SeedLanguageName` in 9 places).
  - **Razor HTML-encodes expressions inside attribute values** — building a
    `style`/`data-*` attribute with `@(cond ? " style=\"display:none\"" : null)`
    produces broken HTML (`style= display:none`). Concatenate or use `Html.Raw`
    for such attributes; never interpolate quoted attribute strings via `@(...)`.
  - **A TagHelper tag inside a quoted attribute value renders as raw HTML text**
    — `<kw-l>` cannot live inside `title="..."`. Put the localized label in a
    proper inline element (`<span>`/`<button>`) instead.
  - **Boolean TagHelper flags bind differently as bare vs. quoted**
    (`<kw-dt TimeOnly>` vs `TimeOnly="true"`) in .NET 10's Razor source
    generator. When a flag "doesn't take effect", check the generated
    descriptor or the rendered output — don't trust the markup at face value.
  - **`@checked(...)` is not a Razor expression** — it renders as a bogus
    literal attribute (`value="true" true=""`) with **no** `checked`
    attribute, so the box is never checked on load and (worse) an unchecked
    box posts nothing, silently binding `false` on save. The proven pattern
    is the conditional-attribute expression:
    `@(Model.X ? "checked" : null)`. `Views/AdminSite/Index.cshtml` shipped
    with `@checked(...)` on all nine toggles (bug reported 2026-10-22; every
    other checkbox in the repo uses the conditional form) — fix markup with
    `@(Model.X ? "checked" : null)`, never `@checked(...)`.
  - **`@($"...")` in an attribute is a C# interpolated string, not Razor** — the
    only interpolation holes are `{var}`; a bare `@var` inside is a **literal**
    character. So `action="@($"/notes/@row.Id/delete")"` renders the literal URL
    `/notes/@row.Id/delete`, while the working form is
    `action="@($"/notes/{row.Id}/delete")"`. Plain `href="/notes/@row.Id/edit"`
    (no `@$`) is a real Razor expression and is fine. This bit the Notes +
    Bookmarks per-row/folder write controls in 9 places (bug reported
    2026-10-22; commit `b481499a` introduced it by converting the delete
    `<a href>` links into POST forms and dropping the `{…}` braces). Grep the
    whole codebase with `grep -rnoE '@\(\$"[^"]*@[A-Za-z_]' src/Kumunita.Web/Views/`
    — any hit is this bug.
  - **A bare `@expr` inside a `<script>` block IS HTML-encoded in .NET 10** —
    a `<script type="application/json" id="x">@myJson</script>` block does
    **not** carry raw JSON: the `"` becomes `&quot;`, so the client's
    `JSON.parse(el.textContent)` fails ("Expected property name at position
    1"). Emit the value via `@Html.Raw(json)` (outputs as-is); if a value can
    contain a literal `</script>`, neutralize it first:
    `@Html.Raw(json.Replace("</", "<\\/"))` (`<\/` is a valid JSON escape that
    cannot close the script). **Fixed 2026-10-27:** the `_Layout.cshtml`
    `#kumunita-strings` block (the P0-6 client strings bundle) used to wrap
    its JSON in `HttpUtility.HtmlEncode` — under this rule that
    **double-encoded** it (served bytes showed `&amp;quot;`), so every
    client module (`rich-editor.ts`, `tag-suggest.ts`, `kiosk.ts`,
    `note-modal.ts`, `onboarding-modal.ts`, `notifications-bell.ts`,
    `image-editor.ts`) was silently falling back to the English floor
    strings. It now uses the correct `@Html.Raw(...)` form. The ADR [0195](../adr/0195-kanban-card-edit-modal.md)
    `#card-edit-prefills` block uses the same form.
- A URL or link that shows a **literal `@Model.X`** (404s, `/groups/@Model.GroupId/posts`)
is the same class of bug — an uninterpolated Razor attribute value, not a
  routing problem. Fix the markup; don't debug the route.

## Getting a live server

`dotnet run` requires a `ConnectionStrings:Kumunita`
value the agent does not have; the docker-compose stack carries all credentials
and seeds sample data on first boot. To render a page end-to-end:

```bash
docker compose build app && docker compose up -d app   # rebuild + recreate (volumes persist)
```

The app needs ~8 s to finish booting (Marten schema + Wolverine leadership).
App URL: `http://localhost:5080`. Sample-data GlobalAdmin: `admin@examplium.com`
/ `Admin123!` (the `SampleDataSeeder` constants in `Kumunita.Core/Bootstrap/`).
Sign in in the integrated browser (or `curl` with a session cookie
+ anti-forgery token), then navigate to the target page. The browser snapshot
is the evidence.
