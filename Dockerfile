# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Restore-affecting files first so the NuGet layer only rebuilds when dependencies change.
COPY *.sln* Directory.Build.props ./
COPY src/Libris.Api/*.csproj            ./src/Libris.Api/
COPY src/Libris.Application/*.csproj    ./src/Libris.Application/
COPY src/Libris.Domain/*.csproj         ./src/Libris.Domain/
COPY src/Libris.Infrastructure/*.csproj ./src/Libris.Infrastructure/
COPY tests/Libris.Api.Tests/*.csproj    ./tests/Libris.Api.Tests/
COPY tests/Libris.Domain.Tests/*.csproj ./tests/Libris.Domain.Tests/

RUN dotnet restore

COPY . .
RUN dotnet publish src/Libris.Api \
    -c Release \
    -o /out \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

RUN apk add --no-cache curl

# The app runs as non-root and must own the mount point, or every cover upload fails with permission denied.
RUN mkdir -p /data/covers && chown app:app /data/covers
VOLUME /data/covers

USER app

COPY --from=build --chown=app:app /out ./

# nginx in front terminates TLS.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/v1/health || exit 1

ENTRYPOINT ["dotnet", "Libris.Api.dll"]
