# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Prototype stage.** The codebase is actively evolving — some modules don't yet follow all conventions below. When generating new code, follow these patterns; when modifying existing code, align it with them where practical.

## Build and Development Commands

```bash
# Run the full app (Aspire orchestration — starts PostgreSQL + app)
dotnet run --project src/Kumunita.AppHost

# Build the entire solution
dotnet build src/Kumunita.slnx

# Run tests (xUnit v3 with Aspire.Hosting.Testing)
dotnet test src/Kumunita.Tests

# Run a single test by name
dotnet test src/Kumunita.Tests --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Apply EF Core migrations (Identity module)
dotnet run --project src/Kumunita.Host -- --migrate

# Add a new EF Core migration (Identity module only)
dotnet ef migrations add <Name> --project src/Kumunita.Identity --startup-project src/Kumunita.Host
```

Marten schemas are auto-created at startup via `.ApplyAllDatabaseChangesOnStartup()` — no manual migration needed for document DB changes.

Solo-developer project. No branching strategy or PR gates — work happens directly on `main`.

## Architecture

**Modular monolith** on .NET 10 / ASP.NET Core. All modules run in one process but are architecturally isolated.

### Key technologies
- **CQRS/messaging:** WolverineFX (commands, queries, domain events, HTTP endpoints)
- **Document DB / event store:** Marten (PostgreSQL)
- **Identity persistence:** EF Core (PostgreSQL, separate `identity` schema)
- **Auth:** OpenIddict (Authorization Code + PKCE)
- **Frontend:** Blazor Web App (InteractiveWebAssembly) with MudBlazor
- **Dev orchestration:** .NET Aspire (dev only, not deployed)

### Project dependency rules

- **Modules never reference each other directly.** Cross-module communication uses Wolverine in-process messages only.
- **`Shared.Infrastructure`** references all domain modules (Announcements, Authorization, Identity, Localization). Any type it needs from a module must live in **`Shared.Kernel`** instead to avoid circular refs. This is why `CommunityRole`, `CommunityMembership`, and `AnnouncementStatus` are in `Shared.Kernel`.
- **`.Contracts` projects** (`Communities.Contracts`, `Announcements.Contracts`) expose a module's public surface to the Blazor WASM client without pulling in the full module. Each references only `Shared.Kernel`.
- **`Web.Client`** references `.Contracts` projects only — never full module projects.

Not every module has its own `.Contracts` project or `Add<Module>Module()` extension yet. When working in a module that's missing these, add them to align with the pattern.

### Multi-tenancy

Community = tenant. Each community gets its own PostgreSQL schema (`community_{slug}`). `CommunityTenantMiddleware` reads `{slug}` from the route and sets the Marten tenant. The `{slug}` in the URL is the single source of truth for tenant context — never passed via headers or body.

Platform-level documents (`Community`, `CommunityMembership`, `CommunityInvitation`) live in the `communities` schema and are not tenant-scoped.

Cross-tenant queries use `IDocumentStore` directly with explicit `SessionOptions`. Cross-tenant endpoints (no `{slug}`) live in `Kumunita.Host.Endpoints` rather than in a module, because they need to query across multiple module domain types in a single request.

### Module structure (within each module)

```
Commands/    — input DTOs
Queries/     — query DTOs + result types
Handlers/    — Wolverine HTTP endpoints (one file per logical group)
Domain/      — Marten documents, enums, value objects
Events/      — domain events published via Wolverine
Exceptions/  — domain exceptions (mapped to HTTP status codes in DomainExceptionHandler)
```

Module registration follows the `Add<Module>Module()` extension on `IServiceCollection`, plus `Add<Module>Schema()` on Marten `StoreOptions`.

### Wolverine handler convention

Handlers are **static classes with static methods** discovered by convention. The method signature determines DI injection:

- First parameter = message/command
- `IDocumentSession` for writes (auto-wrapped in Marten transaction via `AutoApplyTransactions`)
- `IQuerySession` for reads
- `ClaimsPrincipal user` for auth context
- `CancellationToken ct`
- Returns `IResult`, or tuple `(result, event?)` to cascade events to Wolverine's messaging pipeline

Use **Wolverine.Http attributes** (`[WolverineGet]`, `[WolverinePost]`), not minimal API `MapGet/MapPost`.

### Domain event routing

Domain events implement `IDomainEvent` and are routed to durable local queues by namespace prefix via `DomainEventModuleRoutingConvention`:

- `Kumunita.Identity.*` → `"identity"` queue
- `Kumunita.Localization.*` → `"localization"` queue
- `Kumunita.Announcements.*` → `"announcements"` queue

When adding a new module with domain events: register its queue in `Program.cs` (`opts.LocalQueue(...)`) and add its prefix mapping in `DomainEventModuleRoutingConvention`.

### Exception handling

Module-specific exceptions are mapped to HTTP status codes in `DomainExceptionHandler` (`Shared.Infrastructure`). When adding new domain exceptions, add the mapping there.

### Authorization model

- **Roles** (`Member`, `Moderator`, `Manager`) grant operational capabilities only — not data access.
- **Platform admins** can provision/deactivate communities but have zero community data access.
- **Capability tokens** mediate all personal data access (Public/Standard/Sensitive tiers).

`ClaimsPrincipalExtensions` in `Kumunita.Shared.Kernel.Auth` provides helpers usable from both backend and Blazor WASM: `GetUserId()`, `IsPlatformAdmin()`, `GetCommunitySlugs()`, `IsMemberOf(slug)`, `GetCommunityRole(slug)`, `IsManagerOf(slug)`, `IsModeratorOrAbove(slug)`, `GetPreferredLanguage()`.

### URL routing conventions

| Pattern | Tenant context | Example |
|---|---|---|
| `GET /{resource}` | Cross-tenant (user's communities) | Combined announcements feed |
| `GET /{resource}/{slug}` | Single tenant | Community announcements |
| `POST /{resource}/{slug}` | Single tenant | Submit announcement |
| `GET/POST /platform/*` | None (platform admin) | Provision community |

All API routes use the `/api/` prefix (e.g. `/api/{slug}/announcements`).

### Frontend (Blazor Web App)

- `Kumunita.Host` contains `App.razor` (server-side root) rendering with `InteractiveWebAssembly` (prerender: false).
- `Pages/CatchAll.razor` (`@page "/{*path}"`) ensures Wolverine API endpoints take priority; Blazor handles all other URLs.
- **Do not use** `AddAdditionalAssemblies`, `UseBlazorFrameworkFiles()`, or `MapFallbackToFile("index.html")`.
- `MapRazorComponents<App>()` must include `.AllowAnonymous()` — required for OIDC callbacks.
- All API calls go through `IApiClient` / `ApiClient` — auto-attaches bearer token, returns `null` on 404.
- Bearer requests are forwarded from cookie auth to OpenIddict validation via `ForwardDefaultSelector` in `Program.cs`.

### Localized content

Multi-language text uses `LocalizedContent` (a `Dictionary<string, string>` keyed by language code). Resolve for the user's preferred language with `.Resolve(preferredLanguages)`.

### Important registration requirements

- All strongly typed IDs must be registered in `MartenExtensions.ConfigureModuleSchemas()` or they serialize as JSON objects instead of plain values.
- Host project must include `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` for WASM `_framework/*` files in production.
- OpenIddict certs in containers require `X509KeyStorageFlags.EphemeralKeySet`.

## Code Conventions

- **C# 13 / .NET 10** — use latest language features (primary constructors, collection expressions, file-scoped namespaces).
- **Nullable reference types** enabled everywhere.
- **Records** for commands, queries, events, and DTOs.
- **No comments** unless explaining a non-obvious architectural decision.

## CI/CD

- Branch push (develop, feature/*, fix/*) → staging image to GHCR → Coolify auto-deploys
- `v*.*.*` tag → production image → manual approval → Coolify deploys
- Docker builds from the **repo root** (not `src/`). The `wasm-tools` workload is restored during build.
