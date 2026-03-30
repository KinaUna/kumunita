# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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
```

Marten schemas are auto-created at startup via `.ApplyAllDatabaseChangesOnStartup()` — no manual migration needed for document DB changes.

## Architecture

**Modular monolith** on .NET 10 / ASP.NET Core. All modules run in one process but are architecturally isolated.

### Key technologies
- **CQRS/messaging:** WolverineFX (commands, queries, domain events, HTTP endpoints)
- **Document DB / event store:** Marten (PostgreSQL)
- **Identity persistence:** EF Core (PostgreSQL)
- **Auth:** OpenIddict (Authorization Code + PKCE)
- **Frontend:** Blazor Web App (InteractiveWebAssembly) with MudBlazor
- **Dev orchestration:** .NET Aspire (dev only, not deployed)

### Project dependency rules

- **Modules never reference each other directly.** Cross-module communication uses Wolverine in-process messages only.
- **`Shared.Infrastructure`** references all domain modules (Announcements, Authorization, Identity, Localization). Any type it needs from a module must live in **`Shared.Kernel`** instead to avoid circular refs. This is why `CommunityRole`, `CommunityMembership`, and `AnnouncementStatus` are in `Shared.Kernel`.
- **`.Contracts` projects** (`Communities.Contracts`, `Announcements.Contracts`) expose a module's public surface to the Blazor WASM client without pulling in the full module. Each references only `Shared.Kernel`.
- **`Web.Client`** references `.Contracts` projects only — never full module projects.

### Multi-tenancy

Community = tenant. Each community gets its own PostgreSQL schema (`community_{slug}`). `CommunityTenantMiddleware` reads `{slug}` from the route and sets the Marten tenant. The `{slug}` in the URL is the single source of truth for tenant context — never passed via headers or body.

Platform-level documents (`Community`, `CommunityMembership`, `CommunityInvitation`) live in the `communities` schema and are not tenant-scoped.

### Module structure (within each module)

```
Commands/    — input DTOs
Queries/     — query DTOs + result types
Handlers/    — Wolverine HTTP endpoints (one file per logical group)
Domain/      — Marten documents, enums, value objects
Events/      — domain events published via Wolverine
Exceptions/  — domain exceptions (mapped to HTTP status codes in DomainExceptionHandler)
```

### Authorization model

- **Roles** (`Member`, `Moderator`, `Manager`) grant operational capabilities only — not data access.
- **Platform admins** can provision/deactivate communities but have zero community data access.
- **Capability tokens** mediate all personal data access (Public/Standard/Sensitive tiers).

### Frontend (Blazor Web App)

- `Kumunita.Host` contains `App.razor` (server-side root) rendering with `InteractiveWebAssembly` (prerender: false).
- `Pages/CatchAll.razor` (`@page "/{*path}"`) ensures Wolverine API endpoints take priority; Blazor handles all other URLs.
- **Do not use** `AddAdditionalAssemblies`, `UseBlazorFrameworkFiles()`, or `MapFallbackToFile("index.html")`.
- `MapRazorComponents<App>()` must include `.AllowAnonymous()` — required for OIDC callbacks.

### Important registration requirements

- All strongly typed IDs must be registered in `MartenExtensions.ConfigureModuleSchemas()` or they serialize as JSON objects instead of plain values.
- Host project must include `<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` for WASM `_framework/*` files in production.
- OpenIddict certs in containers require `X509KeyStorageFlags.EphemeralKeySet`.

## CI/CD

- Branch push (develop, feature/*, fix/*) → staging image to GHCR → Coolify auto-deploys
- `v*.*.*` tag → production image → manual approval → Coolify deploys
