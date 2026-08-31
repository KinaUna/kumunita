# Kumunita — multi-stage build (README "Deployment": compile TS with tsc, publish, runtime).
# Build context: repository root. The front-end is plain TypeScript compiled with tsc
# (no bundler, no dev server).

# --- 1. Front-end: compile the plain-TS client with tsc ---
FROM node:22 AS ts
WORKDIR /src
COPY src/Kumunita.Web/package.json src/Kumunita.Web/package-lock.json src/Kumunita.Web/tsconfig.json ./
COPY src/Kumunita.Web/client/ ./client/
RUN npm ci && npm run build

# --- 2. .NET: restore + publish the web app (with the compiled front-end in wwwroot) ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/Kumunita.Core/ ./src/Kumunita.Core/
COPY src/Kumunita.Web/ ./src/Kumunita.Web/
COPY --from=ts /src/wwwroot/js ./src/Kumunita.Web/wwwroot/js/
RUN dotnet publish src/Kumunita.Web -c Release -o /app/publish

# --- 3. Runtime: slim ASP.NET Core image ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kumunita.Web.dll"]
