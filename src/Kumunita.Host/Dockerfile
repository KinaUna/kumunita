# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY *.sln .
COPY ["Directory.Packages.props", "."]
COPY Directory.Build.props .

# Copy all project files
COPY ["Kumunita.Host/Kumunita.Host.csproj", "Kumunita.Host/"]
COPY ["Kumunita.Announcements/Kumunita.Announcements.csproj", "Kumunita.Announcements/"]
COPY ["Kumunita.Authorization/Kumunita.Authorization.csproj", "Kumunita.Authorization/"]
COPY ["Kumunita.Identity/Kumunita.Identity.csproj", "Kumunita.Identity/"]
COPY ["Kumunita.Shared.Kernel/Kumunita.Shared.Kernel.csproj", "Kumunita.Shared.Kernel/"]
COPY ["Kumunita.Localization/Kumunita.Localization.csproj", "Kumunita.Localization/"]
COPY ["Kumunita.ServiceDefaults/Kumunita.ServiceDefaults.csproj", "Kumunita.ServiceDefaults/"]
COPY ["Kumunita.Shared.Infrastructure/Kumunita.Shared.Infrastructure.csproj", "Kumunita.Shared.Infrastructure/"]
COPY ["Kumunita.Web/Kumunita.Web.csproj", "Kumunita.Web/"]

# Restore dependencies
RUN dotnet restore "./Kumunita.Host/Kumunita.Host.csproj"

# Copy all source code
COPY . .

# Publish the Host project
RUN dotnet publish "./Kumunita.Host/Kumunita.Host.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app
USER appuser

# Health check — Aspire ServiceDefaults registers /health automatically
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "Kumunita.Host.dll"]