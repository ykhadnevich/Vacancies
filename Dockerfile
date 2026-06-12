# syntax=docker/dockerfile:1.7
# ----------------------------------------------------------------------------
# Vacancies API — production Dockerfile
#
# Multi-stage build that produces a slim self-contained runtime image. Base
# images are multi-arch (linux/amd64 + linux/arm64) so the same Dockerfile
# builds for t3.micro (x86) and t4g.* (ARM) without changes — Docker picks
# the right manifest at pull time.
#
# Stages:
#   - sdk-restore : warm the NuGet cache against the .csproj files only. This
#                   layer gets reused unless project dependencies change.
#   - sdk-build   : copy the rest of the source and `dotnet publish`.
#   - runtime     : ASP.NET runtime + the published output. Final image
#                   exposes port 8080 (the value ASPNETCORE_URLS is bound to).
#
# Build the image:
#   docker build -t vacancies-api:latest -f Dockerfile .
#
# Run locally (with appsettings.Development.json values via env):
#   docker run --rm -p 8080:8080 \
#     -e ASPNETCORE_ENVIRONMENT=Production \
#     -e ConnectionStrings__DefaultConnection="Host=...;SslMode=Require;..." \
#     -e Cors__AllowedOrigins__0="https://example.com" \
#     vacancies-api:latest
# ----------------------------------------------------------------------------

ARG DOTNET_VERSION=8.0

# ---------------------------------------------------------------------------
# Stage 1 — restore (cached by project file fingerprint)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS sdk-restore
WORKDIR /src

# Copy ONLY the .csproj files first so `dotnet restore` produces a layer that
# only invalidates when dependencies actually change. Source edits below this
# point reuse the restored package graph.
COPY ["API/API.csproj",                       "API/"]
COPY ["Application/Application.csproj",       "Application/"]
COPY ["Domain/Domain.csproj",                 "Domain/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "API/API.csproj"

# ---------------------------------------------------------------------------
# Stage 2 — build + publish
# ---------------------------------------------------------------------------
FROM sdk-restore AS sdk-build
WORKDIR /src
COPY . .
WORKDIR /src/API
RUN dotnet publish "API.csproj" \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# ---------------------------------------------------------------------------
# Stage 3 — minimal runtime image
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Install curl so the HEALTHCHECK below works. The aspnet:8.0 image is
# Debian-slim and ships without wget/curl — the bare runtime is all you need
# for ASP.NET but Docker probes need a network tool. Cleaning apt caches in
# the same RUN keeps the layer small.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

# Run as a non-root user. The aspnet base image has an `app` user (UID 1654)
# from .NET 8 onward; falling back to creating one keeps this portable.
RUN useradd -u 1000 -ms /bin/bash app 2>/dev/null || true
USER app

COPY --from=sdk-build --chown=app:app /app/publish .

# ASP.NET listens on 8080 by convention in non-root images.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=false

EXPOSE 8080

# Container-level health probe. /health is the cheap liveness route added in
# HealthController; it intentionally does NOT touch the DB so a slow Postgres
# doesn't restart the container.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "API.dll"]
