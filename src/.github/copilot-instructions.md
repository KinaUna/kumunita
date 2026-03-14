# Kumunita — Copilot Instructions

## Architecture Overview

Kumunita is a **modular monolith** community platform built on .NET 10, using **vertical-slice architecture** within each module. The Host project serves both a server-rendered Razor Pages layer (Identity/login) and a **Blazor WebAssembly** client over a single origin.

> **Prototype stage.** The codebase is actively evolving — some modules don't yet follow all conventions described below. When generating new code, follow these patterns; when modifying existing code, align it with them where practical.

### Key Technology Stack

| Concern | Technology |
|---|---|
| Messaging & handlers | **Wolverine** (command/query dispatch, durable local queues) |
| Document store | **Marten** (PostgreSQL-backed, multi-tenant) |
| Identity & auth | ASP.NET Core Identity + **OpenIddict** (OIDC Authorization Code + PKCE) |
| HTTP endpoints | **Wolverine.Http** (`[WolverineGet]`, `[WolverinePost]`, etc.) |
| Identity persistence | **EF Core** (PostgreSQL, separate from Marten) |
| UI framework | **MudBlazor** components in Blazor WASM |
| Orchestration (dev) | **.NET Aspire** (`Kumunita.AppHost`) |
| Deployment | Docker → **Coolify** (behind **Traefik** reverse proxy) |

### Module Boundaries

Each module is its own class library following this layout:

```
Kumunita.<Module>/
  Domain/           # Aggregates, entities, value objects, domain events
  Features/         # Vertical slices: command/query record + static Handler class
  Endpoints/        # Wolverine.Http endpoint classes (thin, delegate to handlers)
  Exceptions/       # Module-specific domain exceptions
  Infrastructure/   # Marten schema config, seed data
  <Module>Module.cs # Extension method registering module services
```

**Contracts projects** (`Kumunita.<Module>.Contracts`) hold commands, queries, and result DTOs shared across module boundaries and consumed by the WASM client. Domain types stay internal to the module.

> Not every module has its own Contracts project or `Add<Module>Module()` extension yet (e.g. Communities). When working in a module that's missing these, add them to align with the established pattern.

### Module List

- **Identity** — User accounts, roles, user groups (EF Core + Marten hybrid)
- **Communities** — Community CRUD, membership, invitations (Marten)
- **Announcements** — Authoring, moderation, publishing workflow (Marten)
- **Authorization** — Capability tokens, visibility policies, audit log (Marten)
- **Localization** — Translation keys, language management (Marten)
- **Shared.Kernel** — Strongly-typed IDs, domain interfaces, value objects
- **Shared.Infrastructure** — Cross-cutting: multi-tenancy, exception handling, Marten schema registration, domain event routing

## Critical Patterns

### Wolverine Handler Convention

Handlers are **static classes with static methods** discovered by convention. The method signature determines DI injection:

```csharp
public static class CreateAnnouncementHandler
{
    public static async Task<(AnnouncementId, AnnouncementSubmitted?)> Handle(
        CreateAnnouncement cmd,       // first param = message
        IDocumentSession session,     // injected by Wolverine
        CancellationToken ct)
    { ... }
}
```

- Wolverine auto-wraps handlers touching `IDocumentSession` in a Marten transaction (`AutoApplyTransactions`).
- Returning a tuple `(result, event?)` cascades the event to Wolverine's messaging pipeline.

### Multi-Tenancy

All Marten documents are multi-tenanted (`AllDocumentsAreMultiTenanted`). The `{slug}` route value is the tenant ID.

- `CommunityTenantMiddleware` extracts `{slug}`, validates membership, and sets the Marten tenant.
- `TenantAwareSessionFactory` creates scoped `IDocumentSession`/`IQuerySession` with the tenant from `HttpContext`.
- Cross-tenant queries use `IDocumentStore` directly with explicit `SessionOptions`.

### Domain Event Routing

Domain events implement `IDomainEvent` and are routed to **durable local queues** by namespace prefix via `DomainEventModuleRoutingConvention`:

- `Kumunita.Identity.*` → `"identity"` queue
- `Kumunita.Localization.*` → `"localization"` queue
- `Kumunita.Announcements.*` → `"announcements"` queue

When adding a new module with domain events, register its queue in `Program.cs` (`opts.LocalQueue(...)`) and add its prefix mapping in `DomainEventModuleRoutingConvention`.

### Strongly-Typed IDs

All entity IDs use the `[StronglyTypedId]` source generator (defined in `Kumunita.Shared.Kernel/Ids.cs`). New IDs must also be registered in `Kumunita.Shared.Infrastructure/MartenExtensions.cs` via `opts.RegisterValueType(typeof(NewId))`.

### Exception Handling

Module-specific exceptions are mapped to HTTP status codes in `DomainExceptionHandler`. When adding new domain exceptions, add the mapping there.

### Localized Content

Multi-language text uses `LocalizedContent` (a `Dictionary<string, string>` keyed by language code). Resolve for the user's preferred language with `.Resolve(preferredLanguages)`.

## Blazor WASM Client

- Render mode: `InteractiveWebAssembly` with `prerender: false` (set in `App.razor`).
- All API calls go through `IApiClient` / `ApiClient` — a typed `HttpClient` that auto-attaches the bearer token and returns `null` on 404.
- Authentication: OIDC via `AddOidcAuthentication` (Authorization Code + PKCE). Authority defaults to the host base address (co-hosted OpenIddict).
- Bearer requests are forwarded from cookie auth to `OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme` via `ForwardDefaultSelector` in `Program.cs`.

## Developer Workflow

Solo-developer project. No branching strategy or PR gates — work happens directly on `main`.

### Running Locally

Start via the **Aspire AppHost** (`Kumunita.AppHost`). It provisions PostgreSQL + pgAdmin and launches the Host project. The `ServiceDefaults` project is dev-only and is excluded from production builds.

### Database Migrations

- **EF Core** (Identity only): `dotnet ef migrations add <Name> --project src/Kumunita.Identity --startup-project src/Kumunita.Host`
- **Marten**: Schema changes auto-apply on startup (`ApplyAllDatabaseChangesOnStartup`).
- Production migration entrypoint: `dotnet run --project Kumunita.Host -- --migrate`

### Docker & Deployment

Build from the repo root (not `src/`). The Dockerfile expects the solution file at root level. The `wasm-tools` workload is restored during build.

Two Coolify environments exist — **Production** and **Staging** — both currently set `ASPNETCORE_ENVIRONMENT=Production`. Traefik handles TLS termination and forwards `X-Forwarded-For` / `X-Forwarded-Proto` headers (trusted in `Program.cs` via `UseForwardedHeaders`).

### Tests

Integration tests use **Aspire's `DistributedApplicationTestingBuilder`** with **xUnit v3**. Run via `dotnet test` or Visual Studio Test Explorer on `Kumunita.Tests`. Testing patterns and conventions are not yet established — keep new tests in `Kumunita.Tests` and follow the existing `WebTests.cs` style until a strategy is decided.

## Code Conventions

- **C# 13 / .NET 10** — use latest language features (primary constructors, collection expressions, file-scoped namespaces).
- **Nullable reference types** enabled everywhere.
- **Records** for commands, queries, events, and DTOs.
- **No comments** unless explaining a non-obvious architectural decision.
- Module registration follows the `Add<Module>Module()` extension method pattern on `IServiceCollection`, plus `Add<Module>Schema()` on Marten `StoreOptions`.
- Endpoint classes use **Wolverine.Http attributes** (`[WolverineGet]`, `[WolverinePost]`), not minimal API `MapGet/MapPost`.
- The WASM client references `*.Contracts` projects only — never domain or infrastructure types.
