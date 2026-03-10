# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution and project files first for layer caching
COPY *.sln .
COPY ["./src/Directory.Packages.props", "src/"]

# Copy all project files (destinations mirror the src/ layout)
COPY ["./src/Kumunita.Host/Kumunita.Host.csproj", "src/Kumunita.Host/"]
COPY ["./src/Kumunita.Announcements/Kumunita.Announcements.csproj", "src/Kumunita.Announcements/"]
COPY ["./src/Kumunita.Authorization/Kumunita.Authorization.csproj", "src/Kumunita.Authorization/"]
COPY ["./src/Kumunita.Identity/Kumunita.Identity.csproj", "src/Kumunita.Identity/"]
COPY ["./src/Kumunita.Shared.Kernel/Kumunita.Shared.Kernel.csproj", "src/Kumunita.Shared.Kernel/"]
COPY ["./src/Kumunita.Localization/Kumunita.Localization.csproj", "src/Kumunita.Localization/"]
COPY ["./src/Kumunita.ServiceDefaults/Kumunita.ServiceDefaults.csproj", "src/Kumunita.ServiceDefaults/"]
COPY ["./src/Kumunita.Shared.Infrastructure/Kumunita.Shared.Infrastructure.csproj", "src/Kumunita.Shared.Infrastructure/"]
COPY ["./src/Kumunita.Web/Kumunita.Web.csproj", "src/Kumunita.Web/"]

# Restore dependencies
RUN dotnet restore "src/Kumunita.Host/Kumunita.Host.csproj"

# Copy all source code
COPY . .

# Publish the Host project
RUN dotnet publish "src/Kumunita.Host/Kumunita.Host.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install necessary packages and create non-root user
RUN apt-get update && apt-get install -y adduser
# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser

RUN apt-get install -y curl

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