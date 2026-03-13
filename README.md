# Kumunita

A private neighborhood community platform. Residents can stay informed through announcements, connect via a private member directory, and participate in local life — without the noise of public social media.

Users can belong to multiple communities (e.g. primary residence, second home, delegated family access) and switch between them seamlessly. Communities are fully isolated from each other at the database level.

---

## Table of contents

- [Technology stack](#technology-stack)
- [Solution structure](#solution-structure)
- [Architecture overview](#architecture-overview)
- [Multi-tenancy](#multi-tenancy)
- [Authentication and authorization](#authentication-and-authorization)
- [Module reference](#module-reference)
- [Frontend](#frontend)
- [Routing](#routing)
- [Key architectural rules](#key-architectural-rules)
- [Development setup](#development-setup)
- [Deployment](#deployment)

---

## Technology stack

| Concern | Technology |
|---|---|
| Runtime | .NET 10 (LTS) |
| Web framework | ASP.NET Core modular monolith |
| Messaging / CQRS | WolverineFX 5.x |
| Event store / document DB | Marten 8.x (PostgreSQL) |
| Identity persistence | EF Core 10 (PostgreSQL) |
| Authentication | OpenIddict (Authorization Code + PKCE) |
| Frontend | Blazor WASM (classic hosted model) |
| UI components | MudBlazor |
| Database | PostgreSQL (single database, schema-per-tenant) |
| Dev orchestration | .NET Aspire 13 (development only, not in production) |
| Strongly typed IDs | StronglyTypedId 1.0.0-beta08 |
| CI/CD | GitHub Actions → GHCR → Coolify |

---

## Solution structure

```
Kumunita.sln
├── Host/
│   ├── Kumunita.Host               # ASP.NET entry point, serves Blazor WASM
│   └── Kumunita.AppHost            # Aspire orchestration (dev only)
├── Web/
│   └── Kumunita.Web.Client         # Blazor WASM client (all .razor files)
├── Modules/
│   ├── Kumunita.Identity           # Users, profiles, groups, OpenIddict
│   ├── Kumunita.Authorization      # Capability tokens, visibility policies
│   ├── Kumunita.Communities        # Community lifecycle, membership, invitations
│   ├── Kumunita.Announcements      # Announcements, moderation queue
│   └── Kumunita.Localization       # Languages, translations, fallback chain
├── Shared/
│   ├── Kumunita.Shared.Kernel      # Value objects, IDs, interfaces, events
│   └── Kumunita.Shared.Infrastructure  # Marten config, middleware, auth policies
├── Kumunita.ServiceDefaults        # Aspire service defaults
└── Kumunita.Tests
```

### Project dependency rules

```
Kumunita.Host
  → all modules
  → Kumunita.Shared.Infrastructure
  → Kumunita.Web.Client

Kumunita.Web.Client
  → Kumunita.Shared.Infrastructure
  → Kumunita.Announcements (for query result types)
  → Kumunita.Communities (for domain types)
  → Kumunita.Shared.Kernel

Kumunita.Shared.Infrastructure
  → Kumunita.Communities
  → Kumunita.Shared.Kernel

Kumunita.[Module]
  → Kumunita.Shared.Kernel
  (modules must NOT reference each other directly)

Kumunita.Shared.Kernel
  → (no internal dependencies)
```

**Critical rule:** Modules communicate exclusively via Wolverine in-process messages. No direct project references between modules.

**Critical rule:** `Kumunita.Shared.Infrastructure` references `Kumunita.Communities`. Therefore, anything `Shared.Infrastructure` depends on must live in `Kumunita.Shared.Kernel` — not in any module — to avoid circular references. This is why `CommunityRole` lives in `Shared.Kernel` rather than `Kumunita.Communities`.

---

## Architecture overview

Kumunita is a **modular monolith** — all modules run in a single process but are architecturally isolated. Each module owns its domain models, commands, queries, handlers, events, and exceptions. The host wires everything together via Wolverine's assembly discovery.

### Within a module

```
Commands/       ← input: what the system is asked to do
Queries/        ← input: what data is requested; also holds result types
Handlers/       ← Wolverine HTTP endpoints; one file per logical group
Domain/         ← Marten documents, enums, value objects
Events/         ← domain events published via Wolverine
Exceptions/     ← domain-specific exceptions mapped to HTTP status codes
```

### Cross-module communication

Modules publish domain events via Wolverine. Other modules subscribe to those events to maintain their own state. For example:

- `Kumunita.Communities` publishes `MemberJoinedCommunity`
- `Kumunita.Authorization` handles it → creates a `UserAuthorizationState` for that community
- `Kumunita.Identity` handles it → creates a community-scoped `UserProfile`

### Exception handling

`DomainExceptionHandler` in `Kumunita.Shared.Infrastructure` maps domain exceptions to HTTP status codes. Each module registers its exceptions there. Wolverine's `AutoApplyTransactions()` wraps handlers in a transactional outbox automatically.

---

## Multi-tenancy

The **community** is the isolation boundary. Each community gets its own PostgreSQL schema (e.g. `community_old_town_lausanne.*`). This is implemented using **Marten's built-in schema-per-tenant** support, with the community slug as the tenant key.

### How the tenant is set per request

`CommunityTenantMiddleware` (registered in `Kumunita.Shared.Infrastructure`) reads the `{slug}` route value on every request:

- If `{slug}` is present: validates the user is an active member, then sets the Marten tenant to that slug. All `IDocumentSession` and `IQuerySession` instances resolved in the request are automatically scoped to that community's schema.
- If `{slug}` is absent: the session is left in cross-tenant mode. Handlers that need cross-tenant data call `QueryAcrossAllTenants` explicitly.

### What is and is not tenant-scoped

| Document | Scoped? | Schema |
|---|---|---|
| `Community` | No | `communities` |
| `CommunityMembership` | No | `communities` |
| `CommunityInvitation` | No | `communities` |
| `AppUser` (EF Core) | No | `identity` |
| OpenIddict tables | No | `identity` |
| `UserProfile` | Yes | `community_{slug}` |
| `DirectoryEntry` | Yes | `community_{slug}` |
| `Group`, `GroupMembership` | Yes | `community_{slug}` |
| `UserAuthorizationState` | Yes | `community_{slug}` |
| `VisibilityPolicy` | Yes | `community_{slug}` |
| `CapabilityToken` | Yes | `community_{slug}` |
| `Announcement` | Yes | `community_{slug}` |

### Community provisioning

Communities are created by platform admins only (via `POST /platform/communities`). When `CommunityCreated` is published, a handler calls `store.Advanced.EnsureStorageExistsAsync(tenantId)` to provision the schema before any member can access it.

---

## Authentication and authorization

### Authentication

OpenIddict handles authentication using the **Authorization Code + PKCE** flow. The Blazor WASM client uses `Microsoft.AspNetCore.Components.WebAssembly.Authentication` which manages the token lifecycle, silent refresh, and redirect handling. All authentication callbacks are routed through `/authentication/{action}` in the client.

**Important:** `app.MapFallbackToFile("index.html")` must include `.AllowAnonymous()` in `Program.cs`, otherwise OIDC callbacks are blocked before the user is authenticated.

OpenIddict certificates (signing and encryption) are loaded from PFX files mounted at `/run/secrets/` in production. Always use `X509KeyStorageFlags.EphemeralKeySet` in container environments — this avoids X.509 store permission errors.

### JWT claims

```json
{
  "sub": "user-guid",
  "platform_admin": false,
  "communities": [
    { "slug": "old-town-lausanne", "role": "Manager" },
    { "slug": "petit-lancy",       "role": "Member" }
  ],
  "preferred_language": "fr"
}
```

`ClaimsPrincipalExtensions` in `Kumunita.Shared.Infrastructure.Auth` provides extension methods used by both backend handlers and Blazor components:

```csharp
user.GetUserId()                  // → UserId
user.IsPlatformAdmin()            // → bool
user.GetPreferredLanguage()       // → IReadOnlyList<string> (fallback order)
user.GetCommunitySlugs()          // → IReadOnlyList<string>
user.IsMemberOf(slug)             // → bool
user.GetCommunityRole(slug)       // → CommunityRole?
user.IsManagerOf(slug)            // → bool
user.IsModeratorOrAbove(slug)     // → bool
```

The client's `Kumunita.Web.Client/Auth/AppClaimsPrincipal.cs` re-exports these via a `global using` — no duplication.

### Community roles

`CommunityRole` lives in `Kumunita.Shared.Kernel` (not in `Kumunita.Communities`) to avoid circular references.

| Role | Operational capabilities |
|---|---|
| `Member` | View content, submit announcements (if enabled), manage own profile |
| `Moderator` | Approve/reject/retract announcements, invite members at Member level |
| `Manager` | All Moderator capabilities + invite at any level, change roles, suspend members, manage settings |
| Platform Admin | Provision/deactivate communities; no access to community data |

**Roles grant operational capabilities only — not elevated data access.** Personal data access is governed entirely by the capability token and visibility policy system, regardless of role.

### Authorization policies

Registered in `Kumunita.Shared.Infrastructure.MultiTenancy.CommunityAuthorizationExtensions`:

| Policy | Requirement |
|---|---|
| `PlatformAdmin` | `platform_admin` claim is `true` |
| `CommunityMember` | `CommunityRole >= Member` in `{slug}` |
| `CommunityModerator` | `CommunityRole >= Moderator` in `{slug}` |
| `CommunityManager` | `CommunityRole == Manager` in `{slug}` |

`CommunityRoleHandler` reads the `{slug}` route value and checks claims dynamically — no database lookup required.

### Capability token system

All personal data access within a community goes through capability tokens:

1. Handler calls `RequestCapabilityToken` (resource, action, target user)
2. Authorization module evaluates: suspended? → visibility policy → issue or deny
3. Handler validates the token (`ValidateCapabilityToken`) and returns data
4. Token is consumed (single-use for Sensitive tier) and an `AuditEntry` is written

Sensitivity tiers within communities:

| Tier | TTL | Notes |
|---|---|---|
| `Public` | 24h | No token needed for community members |
| `Standard` | 60 min | Session-scoped |
| `Sensitive` | 30s | Single-use, per-request |

---

## Module reference

### Kumunita.Shared.Kernel

Contains types shared across all modules. No dependencies on other internal projects.

- **Strongly typed IDs:** `UserId`, `CommunityId`, `CommunityMembershipId`, `CommunityInvitationId`, `AnnouncementId`, `CapabilityTokenId`, `GroupId`, `RoleId`, `PermissionId`, `TranslationKeyId`, `TranslationId`
- **Value objects:** `CommunitySlug`, `LanguageCode`, `LocalizedContent`, `Address`
- **Enums:** `CommunityRole` (Member / Moderator / Manager)
- **Interfaces:** `ITenantScoped`, `IAuditableEntity`, `IUserOwned`, `ISoftDeletable`, `IDomainEvent`

All strongly typed IDs and value types must be registered with Marten via `opts.AddSharedKernelTypes()` in `Program.cs`.

### Kumunita.Shared.Infrastructure

Cross-cutting infrastructure. References `Kumunita.Communities` and `Kumunita.Shared.Kernel`.

- `CommunityTenantMiddleware` — sets Marten tenant from `{slug}` route value
- `CommunityAuthorizationPolicies` — registers ASP.NET Core authorization policies
- `ClaimsPrincipalExtensions` — JWT claim helpers (used by both backend and frontend)
- `SharedKernelMartenExtensions` — `AddSharedKernelTypes()` for Marten registration
- `DomainExceptionHandler` — maps domain exceptions to HTTP status codes

### Kumunita.Communities

Platform-level module (not tenant-scoped). Manages community lifecycle, membership, and invitations.

- Documents: `Community`, `CommunityMembership`, `CommunityInvitation` (all in `communities` schema)
- Handlers: `PlatformCommunityHandler`, `InvitationHandler`, `MemberManagementHandler`, `CommunityQueryHandler`
- Key events: `CommunityCreated`, `MemberJoinedCommunity`, `MemberRoleChanged`, `MemberSuspendedFromCommunity`, `InvitationCreated`

### Kumunita.Identity

Manages user accounts, profiles, and groups. Split persistence:

- **EF Core** (`identity` schema): `AppUser` (thin auth record), `AppRole`, OpenIddict tables
- **Marten** (tenant-scoped): `UserProfile`, `DirectoryEntry`, `Group`, `GroupMembership`

`AppUser.DomainId` bridges the EF Core identity record to the `UserId` used throughout the domain.

### Kumunita.Authorization

Manages capability tokens, visibility policies, and audit logs. All documents are tenant-scoped.

- `UserAuthorizationState` — projection built from community and identity events; holds role, group memberships, suspension status, active token IDs per community
- `VisibilityPolicy` — per-user, per-resource visibility settings
- `CapabilityToken` — issued per data access request; consumed on use for Sensitive tier
- `AuditEntry` — records all token issuances, denials, and uses; requester identity is obscured for non-admins

### Kumunita.Announcements

Manages announcements with a staff path and a member submission path.

- Status lifecycle: `Draft → Published` (staff) or `Draft → PendingReview → Published/Rejected` (member)
- Targeting: `AnnouncementTarget` (roles + groups); `IsUniversal` for member submissions
- `AnnouncementSettings` — singleton document per community; controls `MemberSubmissionsEnabled` and `MaxPendingSubmissionsPerMember`
- Query results: `AnnouncementSummary`, `AnnouncementDetail`, `CommunityAnnouncementGroup`

### Kumunita.Localization

Manages platform languages and translations with a fallback chain.

- Fallback order: preferred language → browser Accept-Language → English → key itself
- `Language` (Code = identity, IsDefault, IsActive)
- `TranslationKey` (Module + Feature + Key hierarchy)
- `Translation` (Status: Draft / Approved / NeedsReview)
- Seeded languages: English (default), French, German, Italian

---

## Frontend

The Blazor WASM client (`Kumunita.Web.Client`) is hosted by `Kumunita.Host` using the **classic hosted WASM model** (`UseBlazorFrameworkFiles()` + `UseStaticFiles()`), not the Blazor Web App model. Do not mix these — they are incompatible hosting configurations.

### Key services

| Service | Purpose |
|---|---|
| `IApiClient` / `ApiClient` | Typed HTTP client; attaches bearer token automatically |
| `CommunityContext` | Scoped service tracking the currently focused community (UI-only concept) |
| `ILocalizationClient` / `LocalizationClient` | Fetches translations; manages preferred language state |

### Layout

`MainLayout.razor` is a single adaptive shell. `NavMenu.razor` and `AppBar.razor` adapt based on authentication state, `CommunityContext.ActiveSlug`, and community role claims. The community switcher in `AppBar` mirrors the Azure Portal pattern — no re-authentication on community switch.

### Components

| Component | Location | Purpose |
|---|---|---|
| `AnnouncementCard` | `Components/Announcements/` | Renders an `AnnouncementSummary` |
| `CommunitySwitcher` | `Layout/` | Community dropdown in AppBar |
| `LanguagePicker` | `Components/Shared/` | Language selection menu |
| `RoleGuard` | `Components/Shared/` | Wraps UI elements behind a role check |
| `AppPageTitle` | `Components/Shared/` | Consistent `Title — Kumunita` page titles |
| `RedirectToLogin` | `Auth/` | Redirects unauthenticated users to OIDC login |

### Pages

| Route | Page | Auth |
|---|---|---|
| `/` | `Home.razor` | Public (adapts for auth) |
| `/about` | `About.razor` | Public |
| `/authentication/{action}` | `Authentication.razor` | Public |
| `/announcements` | `AnnouncementsFeed.razor` (Combined) | Required |
| `/announcements/{slug}` | `AnnouncementsFeed.razor` (Community) | Required + member |
| `/communities` | _(TODO)_ | Required |
| `/communities/{slug}` | `CommunityLanding.razor` | Required + member |
| `/communities/{slug}/members` | `CommunityMembers.razor` | Moderator+ |
| `/communities/{slug}/manage` | `CommunityManage.razor` | Manager |
| `/platform` | `PlatformDashboard.razor` | Platform admin |
| `/platform/communities` | `CommunitiesList.razor` | Platform admin |

---

## Routing

The `{slug}` in the URL is the authoritative community context for every request — both on the backend (Marten tenant) and the frontend (`CommunityContext`).

| Pattern | Tenant context | Example |
|---|---|---|
| `GET /{resource}` | Cross-tenant (user's communities) | Combined announcements feed |
| `GET /{resource}/{slug}` | Single tenant | Community announcements |
| `POST /{resource}/{slug}` | Single tenant | Submit announcement |
| `GET/POST /platform/*` | None (platform admin) | Provision community |

---

## Key architectural rules

1. **Modules never reference each other directly.** Cross-module communication uses Wolverine in-process messages only.

2. **`CommunityRole` lives in `Shared.Kernel`.** `Shared.Infrastructure` depends on `Kumunita.Communities`, so anything `Shared.Infrastructure` needs must be in `Shared.Kernel` to avoid circular references. Apply this rule to any new shared type.

3. **The tenant is always in the URL.** Community context is never passed via headers or body. The `{slug}` route value is the single source of truth for tenant resolution.

4. **Roles grant operational capability, not data access.** A Manager sees no more personal data than a Member. Data access is always mediated by capability tokens and visibility policies.

5. **Platform admins have no community data access.** The `platform_admin` claim and `CommunityRole` are entirely separate concepts and must never intersect.

6. **`MapFallbackToFile("index.html")` must call `.AllowAnonymous()`.** Without this, OIDC callbacks are blocked by the global authorization policy before the user can authenticate.

7. **Use `X509KeyStorageFlags.EphemeralKeySet` for OpenIddict certificates in containers.** Standard X.509 store access fails in containerized environments without this flag.

8. **All strongly typed IDs must be registered with Marten.** Add new IDs to `SharedKernelMartenExtensions.AddSharedKernelTypes()` or they will be serialized as JSON objects rather than plain values.

9. **Blazor WASM classic hosted model and Blazor Web App model are incompatible.** The client uses `Microsoft.NET.Sdk.BlazorWebAssembly` and the host uses `UseBlazorFrameworkFiles()`. Do not use `MapRazorComponents` — it belongs to the Web App model.

---

## Development setup

### Prerequisites

- .NET 10 SDK
- Visual Studio 2026 or Rider
- Docker (for PostgreSQL via Aspire)

### Running locally

```bash
# Start the Aspire host — launches PostgreSQL and the app together
dotnet run --project Kumunita.AppHost
```

Aspire provisions PostgreSQL automatically in development. The dashboard is available at `https://localhost:15888`.

### Database migrations (Identity / EF Core)

```bash
# Apply EF Core migrations for the Identity module
dotnet run --project Kumunita.Host -- --migrate
```

Marten schemas are created automatically at startup via `AutoMigrateSchemaObjects`.

### OpenIddict certificates

In development, OpenIddict uses auto-generated ephemeral certificates. No setup required.

In production, generate certificates and mount them as file secrets:

```powershell
# Generate signing certificate
New-SelfSignedCertificate -Subject "CN=Kumunita Signing" `
  -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" |
  Export-PfxCertificate -FilePath signing.pfx -Password (ConvertTo-SecureString "password" -AsPlainText -Force)

# Generate encryption certificate
New-SelfSignedCertificate -Subject "CN=Kumunita Encryption" `
  -KeyUsage KeyEncipherment -CertStoreLocation "Cert:\CurrentUser\My" |
  Export-PfxCertificate -FilePath encryption.pfx -Password (ConvertTo-SecureString "password" -AsPlainText -Force)
```

Upload both files as Coolify file mounts at `/run/secrets/signing.pfx` and `/run/secrets/encryption.pfx`.

---

## Deployment

### Environment variables

| Variable | Purpose |
|---|---|
| `ConnectionStrings__neighborhoodapp` | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Staging` |
| `OpenIddict__SigningCertificatePath` | Path to signing PFX (e.g. `/run/secrets/signing.pfx`) |
| `OpenIddict__EncryptionCertificatePath` | Path to encryption PFX |
| `OpenIddict__CertificatePassword` | PFX password |

### CI/CD pipeline

- Any branch push → builds and pushes `staging-{sha}` image to GHCR → Coolify auto-deploys to staging
- `v*.*.*` tag → builds and pushes `{version}` + `latest` images → requires manual approval in GitHub → Coolify deploys to production

### Rollback

In Coolify: Application → Deployments → select previous deployment → Redeploy.
