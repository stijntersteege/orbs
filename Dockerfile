# syntax=docker/dockerfile:1
# One image serves the API and the web client. Build: docker build -t orbs .

# ---- Web client ---------------------------------------------------------------
FROM node:26-alpine AS web
WORKDIR /src/frontend
RUN npm install -g pnpm@12.8.1
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ ./
RUN pnpm build

# ---- API ----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/backend
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props ./
COPY backend/src/Orbs.Domain/Orbs.Domain.csproj src/Orbs.Domain/
COPY backend/src/Orbs.Application/Orbs.Application.csproj src/Orbs.Application/
COPY backend/src/Orbs.Infrastructure/Orbs.Infrastructure.csproj src/Orbs.Infrastructure/
COPY backend/src/Orbs.Api/Orbs.Api.csproj src/Orbs.Api/
RUN dotnet restore src/Orbs.Api/Orbs.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/Orbs.Api/Orbs.Api.csproj -c Release -o /app --no-restore

# ---- Runtime ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ConnectionStrings__Orbs="Data Source=/data/orbs.db"
COPY --from=api /app ./
COPY --from=web /src/frontend/dist ./wwwroot
RUN apk add --no-cache krb5-libs && mkdir -p /data && chown app:app /data
USER app
VOLUME /data
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Orbs.Api.dll"]

LABEL org.opencontainers.image.title="Orbs" \
      org.opencontainers.image.description="Open-source home for AI companions: any model, any context." \
      org.opencontainers.image.licenses="MIT" \
      org.opencontainers.image.source="https://github.com/your-org/orbs"
