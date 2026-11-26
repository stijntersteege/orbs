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

## Website, docs and live demo

The project website is built from this repository by `site/build.mjs` and published to GitHub Pages by `.github/workflows/pages.yml` on **every push to `main`**, so it always matches the repository. Nothing on the website is edited by hand:

| Page | Source |
| --- | --- |
| Home, Get Orbs, Open source and 404 | `site/pages/*.mjs`, with the shared header and footer in `site/lib/layout.mjs` |
| Docs (`/docs/`) | every Markdown file in `docs/`, with GitHub-style tables, heading anchors and Mermaid diagrams |
| Policies (`/privacy/`, `/security/`, `/code-of-conduct/`, `/contributing/`, `/license/`) | `PRIVACY.md`, `SECURITY.md`, `CODE_OF_CONDUCT.md`, `CONTRIBUTING.md` and `LICENSE` in the repository root, with their last-changed date from Git |
| Changelog (`/changelog/`) | `CHANGELOG.md` (an empty `[Unreleased]` section is hidden) |
| Live demo (`/demo/`) | the real web app, built with `pnpm build:demo` |

The orb mascots on the website come from `site/lib/orb.mjs`, a plain-SVG copy of `frontend/src/components/orb/Orb.tsx`. Keep the two in step when the orb changes. The site follows the app's design tokens: dark by default, light when the visitor's system prefers it.

Docs pages follow the folders: `docs/guides/`, `docs/architecture/` (arc42) and `docs/adr/` each get a section in the sidebar, in file-name order, titled by each file's first heading. Links between Markdown files become website links; links to other repository files point to GitHub. A new file in one of these folders shows up on the website without any extra step.

The build fails on a relative link to a file that does not exist. CI runs that check (`Website` job) on every pull request.

In the live demo, `frontend/src/demo/` answers every `/api/...` call in the browser with sample data kept in `localStorage`, replies like the Echo provider and never calls real model providers. Normal builds contain none of this code. When you change an API contract, update `frontend/src/demo/server.ts` too, so the demo keeps working.

Build and preview the website locally:

```bash
cd site && pnpm install && pnpm check   # render everything and check links
cd frontend && pnpm build:demo && cd ..
node site/build.mjs _site frontend/dist-demo
python3 -m http.server -d _site 8000   # open http://localhost:8000
```
