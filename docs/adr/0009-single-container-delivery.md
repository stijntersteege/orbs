# 0009. Single container delivery

- **Status**: Accepted
- **Date**: 2026-11-25

## Context

Self-hosting should be one command. Splitting API and web app into separate containers adds a reverse proxy and CORS setup for no benefit at this size.

## Decision

One multi-stage `Dockerfile` builds the web app with Node, publishes the API with the .NET SDK, and ships both on `aspnet:10.0-alpine`: the API serves the static web app from `wwwroot` and falls back to `index.html` for client routes. The container runs as a non-root user, exposes port 8080 and has a health check. `compose.yaml` adds PostgreSQL. Releases publish multi-architecture images to GHCR.

## Consequences

- One origin: no CORS in the default setup, simple reverse-proxy configuration.
- The web app can still be hosted separately (CDN, Capacitor) by setting `Orbs__Cors__Origins`.
- Frontend and backend release together, which keeps the API contract in sync.
