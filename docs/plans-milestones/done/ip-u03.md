# IP · U03 — Rate-limit policies for message send + post/reply create

**You are the U03 agent.** This is a **Web** unit. It adds **two** new
fixed-window rate-limit policies (`message` and `write`) to the existing
`AddRateLimiter` block in `Program.cs`, and adds the `[EnableRateLimiting]`
attribute to the three highest-volume authenticated write actions
(`MessagesController.Send`, `PostsController.New`, `PostsController.Replies`).
It does **not** add a Postgres-backed or distributed rate limiter (the
"per-instance, in-memory" decision in SECURITY.md §6 is frozen), does
**not** add a CAPTCHA, does **not** change the existing five policies
(`login`, `signup`, `resend`, `report`, `setup`), and does **not** touch
the Core project. **Exit: `dotnet build Kumunita.slnx -c Debug` clean +
`Kumunita.Web.Tests` green.**

**Read the register first** — D3, C-IP·3, GATE-2, the §drift-guard.

## Goal

Complete the security integration's rate-limiting coverage. The existing
five policies cover the **anonymous write surfaces** (login, signup, resend)
and the **authorization-escalation surfaces** (report, setup). The two
**highest-volume authenticated write surfaces** — message send and
post/reply create — are unthrottled. At dozens of users this is a spam /
abuse gap, not a DoS gap (the per-IP partition is the SECURITY.md decision;
the platform's threat model is a single-neighborhood deployment, not a
public-internet one). The fix is two more policies on the same in-memory
partitioner, following the exact `AddWindow` idiom already in `Program.cs`.

## Entry reads (6)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D3, C-IP·3, the two policy names + limits).
2. `src/Kumunita.Web/Program.cs` lines 285–330 — the **real** `AddRateLimiter`
   block: the `AddWindow` helper (a local `static void` that takes
   `RateLimiterOptions o, string policyName, int limit, TimeSpan window` and
   calls `o.AddPolicy(policyName, …)` with a fixed-window limiter partitioned
   by `context.Connection.RemoteIpAddress`), and the five existing policy
   registrations (`login` 5/15min, `signup` 5/hr, `resend` 5/hr, `report`
   10/hr, `setup` 5/15min).
3. `src/Kumunita.Web/Controllers/AccountController.cs` lines 104, 233 —
   the **real** `[EnableRateLimiting("resend")]` and
   `[EnableRateLimiting("login")]` attribute placements (on the action
   method, above the method signature, alongside `[ValidateAntiForgeryToken]`
   where present).
4. `src/Kumunita.Web/Controllers/MessagesController.cs` lines 220–222 —
   the **real** `Send` action: `[HttpPost("/messages/{id}/send")]`,
   `public async Task<IActionResult> Send(string id, [FromForm] string? body)`.
5. `src/Kumunita.Web/Controllers/PostsController.cs` lines 828–830 and
   1220–1222 — the **real** `New` action (`[HttpPost("/posts/new")]`)
   and `Replies` action (`[HttpPost("/posts/{id}/replies")]`).
6. `src/Kumunita.Web/Controllers/PostsController.cs` line 1938–1941 — the
   **real** `[EnableRateLimiting("report")]` on the `Report` action (the
   closest existing precedent for an authenticated action's rate-limit
   attribute; note it has **no** `[ValidateAntiForgeryToken]` — the `New`
   action does, and the attribute order is: route, anti-forgery,
   rate-limiting).

## Deliverables (2)

### 1. `src/Kumunita.Web/Program.cs` — two new policies in the `AddRateLimiter` block

**Add** two `AddWindow` calls **after** the existing `setup` line (line ~327),
inside the same `builder.Services.AddRateLimiter(opts => { … });` lambda:

```csharp
    // Message send: 20 per 15 minutes per IP (the highest-volume
    // authenticated write surface — direct-message spam).
    AddWindow(opts, "message", limit: 20, window: TimeSpan.FromMinutes(15));

    // Resident write lane (post create, reply create): 30 per 15 minutes
    // per IP (a general write-lane guard — a resident posting or replying
    // more than 30 times in 15 minutes is an anomaly at this platform's
    // scale; the limit is generous for normal use and tight for spam).
    AddWindow(opts, "write", limit: 30, window: TimeSpan.FromMinutes(15));
```

> **Do not change** the existing five policies. The `AddWindow` helper is
> already defined in scope — the two new calls use it exactly like the
> existing five. No new `using`, no new DI registration, no new middleware.

### 2. `src/Kumunita.Web/Controllers/MessagesController.cs` + `PostsController.cs` — `[EnableRateLimiting]` on the three actions

**`MessagesController.cs`** — add the attribute to the `Send` action (line
~221). The existing attribute order in this file is `[HttpPost]` above the
method. Add `[EnableRateLimiting("message")]` between `[HttpPost]` and the
method signature:

```csharp
[HttpPost("/messages/{id}/send")]
[EnableRateLimiting("message")]
public async Task<IActionResult> Send(string id, [FromForm] string? body)
```

**`PostsController.cs`** — add the attribute to the `New` action (line ~828).
The existing attribute order is `[HttpPost]`, `[ValidateAntiForgeryToken]`.
Add `[EnableRateLimiting("write")]` after `[ValidateAntiForgeryToken]`:

```csharp
[HttpPost("/posts/new")]
[ValidateAntiForgeryToken]
[EnableRateLimiting("write")]
public async Task<IActionResult> New([FromForm] PostComposeViewModel model)
```

**`PostsController.cs`** — add the attribute to the `Replies` action (line
~1220). The existing attribute order is `[HttpPost]`. Add
`[EnableRateLimiting("write")]` between `[HttpPost]` and the method
signature:

```csharp
[HttpPost("/posts/{id}/replies")]
[EnableRateLimiting("write")]
public async Task<IActionResult> Replies([FromRoute] string id, [FromForm] string? body, [FromForm] string? languageCode)
```

> **Confirm the `using Microsoft.AspNetCore.RateLimiting;` import** is
> present in both `MessagesController.cs` and `PostsController.cs`.
> `PostsController.cs` already has it (it uses `[EnableRateLimiting("report")]`
> at line 1938). `MessagesController.cs` may not — check the `using` block
> and add it if absent.

> **Do not add** the attribute to `PostsController.Edit` (post edit),
> `PostsController.EditReply` (reply edit), or the translation actions
> (`AddReplyTranslation`, `UpdateReplyTranslation`, `RemoveReplyTranslation`)
> — these are lower-volume (edit, not create) and are not in the locked
> D3 set. The `write` policy covers the **create** surfaces only.

### 3. Pin test — the two policies exist

In `tests/Kumunita.Web.Tests/`, add a test (or extend the existing
`ProgramTests` / rate-limiting test file if one exists):

```csharp
[Fact]
public void RateLimiter_ContainsMessageAndWritePolicies()
{
    // Build the host and inspect the rate-limiter policy set.
    // (Follow the existing test pattern for asserting on Program.cs
    //  configuration — if there is no existing test for rate-limit
    //  policies, this test can be a simple compilation-level check:
    //  the [EnableRateLimiting] attributes are present on the
    //  three actions, which the build + Web.Tests exit gate already
    //  validates. A dedicated runtime test is optional for this unit.)

    // The exit gate (build + Web.Tests) is the primary pin:
    // if the policy names don't match, the host fails to start
    // (the rate-limiter middleware throws on an unknown policy name).
}
```

> **Note:** the primary pin is that the host starts and the three actions
> resolve their rate-limit policy. If the policy name in
> `[EnableRateLimiting("…")]` doesn't match a registered policy,
> `UseRateLimiter` throws at request time (not at startup) — so a test
> that hits the `Send` action with an in-memory `TestServer` and asserts
> a 200 (not a 500) is the most valuable pin. Follow the existing
> `Web.Tests` pattern for controller-level tests.

## Exit (build + Web test assembly)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Both must be green. Additionally:

- `grep -n 'AddWindow.*"message"' src/Kumunita.Web/Program.cs` returns
  **1** match.
- `grep -n 'AddWindow.*"write"' src/Kumunita.Web/Program.cs` returns
  **1** match.
- `grep -rn 'EnableRateLimiting("message")' src/Kumunita.Web/` returns
  **1** match (MessagesController.Send).
- `grep -rn 'EnableRateLimiting("write")' src/Kumunita.Web/` returns
  **2** matches (PostsController.New + PostsController.Replies).
