# Development

## Prerequisites

| Tool | Version | Check |
| --- | --- | --- |
| .NET SDK | 10.0 (see `backend/global.json`) | `dotnet --version` |
| Node.js | 24 LTS | `node --version` |
| pnpm | 12 (see `frontend/package.json`) | `pnpm --version` |
| Docker | optional, for PostgreSQL and the full stack | `docker --version` |

Recommended editor extensions are listed in `.vscode/extensions.json` (C# Dev Kit, Reqnroll, oxc, Prettier).

## First run

```bash
make setup
make dev-api   # http://localhost:5080, API reference at /scalar
make dev-web   # http://localhost:5173
```

Without `make`:

```bash
cd backend && dotnet tool restore && dotnet run --project src/Orbs.Api --launch-profile http
cd frontend && pnpm install && pnpm dev
```

The API uses SQLite (`backend/src/Orbs.Api/orbs.db`) and seeds demo data. Delete the file to start fresh.

## Running against PostgreSQL

```bash
docker compose up -d db
cd backend
dotnet run --project src/Orbs.Api --launch-profile http \
  --Database:Provider=Postgres \
  --ConnectionStrings:Orbs="Host=localhost;Database=orbs;Username=orbs;Password=orbs"
```

## Database migrations

Migrations target PostgreSQL and live in `backend/src/Orbs.Infrastructure/Persistence/Migrations`.

```bash
make migration name=AddOrbTags
# or
cd backend && dotnet ef migrations add AddOrbTags --project src/Orbs.Infrastructure --output-dir Persistence/Migrations
```

Review the generated code, then run the integration tests. Migrations apply automatically on start.

## Where does my code go?

| I want to... | Put it in |
| --- | --- |
| add a business rule | the aggregate in `Orbs.Domain` |
| add a use case | a service in `Orbs.Application/<Feature>` |
| call an external system | a port in `Orbs.Application/Abstractions`, an adapter in `Orbs.Infrastructure` |
| expose it over HTTP | an endpoint in `Orbs.Api/Endpoints` |
| show it in the app | a hook in `frontend/src/api/queries.ts`, a screen in `frontend/src/features` |

## Useful commands

```bash
make test             # every test suite
make lint             # formatting and static checks
make format           # fix formatting
make docker-up        # full stack on http://localhost:8080
cd frontend && pnpm test:watch
cd backend && dotnet watch --project src/Orbs.Api
```

## Debugging chat

Use the Echo provider to isolate the UI from real models. To watch the raw stream:

```bash
curl -N -X POST http://localhost:5080/api/conversations/<id>/messages \
  -H 'Content-Type: application/json' -d '{"content":"hello"}'
```
