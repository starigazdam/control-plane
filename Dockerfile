# syntax=docker/dockerfile:1

# Build identity is mandatory provenance metadata for this image. It must be
# exactly 40 lowercase hexadecimal characters; a missing, empty, or malformed
# value fails the build so no apparently identified image is produced.
FROM alpine:3.20 AS build-identity
ARG BUILD_SHA
RUN set -eux; \
    value="${BUILD_SHA:-}"; \
    [ "${#value}" -eq 40 ] || { echo "BUILD_SHA must be exactly 40 lowercase hexadecimal characters; got '${value}'" >&2; exit 1; }; \
    case "$value" in \
      *[!0-9a-f]*) echo "BUILD_SHA must be lowercase hexadecimal; got '${value}'" >&2; exit 1 ;; \
    esac

# Compile the React UI. The identity stage is a prerequisite so an invalid
# BUILD_SHA fails the build before any expensive work starts.
FROM node:22-alpine AS ui-build
COPY --from=build-identity /etc/alpine-release /tmp/build-identity
# The browser-test dependency is not needed to compile the UI bundle.
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
WORKDIR /src/ui
COPY ui/package.json ui/package-lock.json ./
RUN npm ci
COPY ui/ ./
RUN npm run build

# Publish the .NET API. The identity stage is a prerequisite so an invalid
# BUILD_SHA fails the build before any expensive work starts.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
COPY --from=build-identity /etc/alpine-release /tmp/build-identity
WORKDIR /src
COPY global.json ./
COPY src/ ./src/
RUN dotnet restore src/ControlPlane.Api/ControlPlane.Api.csproj
RUN dotnet publish src/ControlPlane.Api/ControlPlane.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# Single-origin runtime image. Serves the published API and the compiled UI from
# one process and one origin as a non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG BUILD_SHA
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Build__CommitSha=${BUILD_SHA}
LABEL org.opencontainers.image.title="control-plane" \
      org.opencontainers.image.revision="${BUILD_SHA}"
WORKDIR /app
COPY --from=api-build /app/publish ./
# The compiled UI is served as static files from wwwroot.
COPY --from=ui-build /src/ui/dist ./wwwroot/
# Prepare the named SQLite volume and application directory for the non-root
# runtime user (app, UID 1654) so an empty volume initializes and is writable.
RUN mkdir -p /data && chown -R app /data /app
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ControlPlane.Api.dll"]
