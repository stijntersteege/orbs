<div align="center">

<img src="frontend/public/favicon.svg" width="96" height="96" alt="A purple orb with a happy face" />

# Orbs

**Little orbs. Big context.**

An open-source, self-hosted home for AI companions.
Plug in any model, connect any context and give every orb its own face.

[![CI](https://github.com/stijntersteege/orbs/actions/workflows/ci.yml/badge.svg)](https://github.com/stijntersteege/orbs/actions/workflows/ci.yml)
[![CodeQL](https://github.com/stijntersteege/orbs/actions/workflows/codeql.yml/badge.svg)](https://github.com/stijntersteege/orbs/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-7C4DFF.svg)](LICENSE)
[![PRs welcome](https://img.shields.io/badge/PRs-welcome-2EE59D.svg)](CONTRIBUTING.md)

**[Live demo](https://stijntersteege.github.io/orbs/demo/)** · [Website](https://stijntersteege.github.io/orbs/) · [Docs](https://stijntersteege.github.io/orbs/docs/) · [Changelog](CHANGELOG.md)

[Quick start](#quick-start) · [Features](#features) · [Architecture](#architecture) · [Development](#development) · [Docs](docs/README.md) · [Contributing](#contributing)

</div>

---

## What is Orbs?

An **orb** is three things: **a model, its context and a face**.

- **A model**: any provider you connect. A hosted API, a model on your own machine, or any OpenAI-compatible endpoint.
- **Its context**: the sources it is allowed to read, such as notes and web pages. An orb sees only what you switch on.
- **A face**: color, shape, eyes, mouth and an accessory. It is how you tell your orbs apart at a glance.

Orbs runs on your server, keeps your data there, and works the same in the browser and on your phone.

### Principles

| | |
| --- | --- |
| **Open by default** | Any model, any context, self-hostable. Always show which model is talking and what it can see. |
| **Generic first** | Providers and context sources are plugins behind small interfaces. No vendor is special. |
| **Every orb is someone** | Customizing an orb is a first-class feature, not a skin. |
| **Playful, never childish** | The joy lives in the orbs. The interface around them stays calm, fast and accessible. |

## Features

- 🟣 **Orbs** with a name, a one-line purpose, a brief (instructions) and a customizable face
- 🧠 **Any model**: an OpenAI-compatible adapter covers OpenAI, OpenRouter, Ollama, LM Studio, vLLM, LiteLLM and more; per-orb model or "Auto"
- 🔗 **Context**: notes and web pages out of the box; connect once, share with the orbs you pick
- 💬 **Streaming chat** over server-sent events, with history per orb
- 🔐 **Secrets stay secret**: API keys are encrypted at rest and never returned by the API
- 🌗 **Light and dark themes**, reduced-motion support, keyboard and screen-reader friendly
- 📱 **Web and mobile** from one codebase (responsive web app, PWA manifest, Capacitor for iOS and Android)
- 🐳 **One container** serves the API and the web app; PostgreSQL or zero-setup SQLite
- 🧪 **Tested**: domain, application and adapter unit tests, API integration tests, architecture rules, Reqnroll (Gherkin) specs, Vitest and Playwright
- 🧩 **Extensible**: add a provider or a context connector by adding one class

> Emoji in this README are for skimming only; the app itself uses a custom icon set.

## Quick start

### With Docker (recommended)

```bash
git clone https://github.com/stijntersteege/orbs.git
cd orbs
cp .env.example .env     # optional: change the password, set an API token
docker compose up -d
```

Open **http://localhost:8080**. A demo orb called **Pip** and a demo model called **Echo** are ready, so you can try everything without an API key. Then go to **Models** and connect a real provider.

### Just the container (SQLite)

```bash
docker build -t orbs .
docker run -p 8080:8080 -v orbs-data:/data orbs
```

### Connect a model

| Provider | Kind | Base URL |
| --- | --- | --- |
| Ollama (local) | OpenAI-compatible | `http://host.docker.internal:11434/v1` |
| LM Studio (local) | OpenAI-compatible | `http://host.docker.internal:1234/v1` |
| OpenRouter | OpenAI-compatible | `https://openrouter.ai/api/v1` |
| OpenAI | OpenAI-compatible | `https://api.openai.com/v1` |
| Anything else that speaks the OpenAI chat API | OpenAI-compatible | its `/v1` URL |

The app has presets for these under **Models > Add provider**.

## Architecture

```mermaid
flowchart LR
  subgraph Clients
    W[Web app<br/>React + Vite]
    M[Mobile app<br/>Capacitor shell]
  end
  subgraph Server[Orbs server - ASP.NET Core]
    API[Minimal API<br/>REST + SSE]
    APP[Application<br/>use cases]
    DOM[Domain<br/>Orb, Provider, Context, Conversation]
    INF[Infrastructure<br/>EF Core, adapters, connectors]
  end
  DB[(PostgreSQL<br/>or SQLite)]
  LLM[Model providers<br/>OpenAI-compatible, ...]
  SRC[Context sources<br/>notes, web pages, ...]

  W & M -->|HTTPS| API --> APP --> DOM
  INF -.implements ports of.-> APP
  INF --> DB
  INF --> LLM
  INF --> SRC
```

- **Backend**: .NET 10, ASP.NET Core Minimal APIs, Clean Architecture (Domain, Application, Infrastructure, Api), EF Core with PostgreSQL or SQLite, OpenAPI with a Scalar reference at `/scalar`.
- **Frontend**: React 19, TypeScript, Vite, React Router, TanStack Query, CSS Modules on design tokens, Capacitor for native shells.
- **Extensibility**: model providers implement `IModelAdapter`, context sources implement `IContextConnector`. Both are registered in one place and discovered by kind.

Read the full design in the [arc42 architecture documentation](docs/architecture/README.md) and the [architecture decision records](docs/adr/README.md).

## Repository layout

```
orbs/
├── backend/                 .NET solution (Orbs.slnx)
│   ├── src/
│   │   ├── Orbs.Domain/          entities, value objects, rules; no dependencies
│   │   ├── Orbs.Application/     use cases and ports (interfaces)
│   │   ├── Orbs.Infrastructure/  EF Core, model adapters, context connectors, secrets
│   │   └── Orbs.Api/             HTTP endpoints, hosting, composition root
│   └── tests/
│       ├── Orbs.Domain.Tests/          unit tests for rules
│       ├── Orbs.Application.Tests/     use cases with in-memory fakes
│       ├── Orbs.Infrastructure.Tests/  adapter and connector units
│       ├── Orbs.Api.IntegrationTests/  the real API in memory
│       ├── Orbs.Architecture.Tests/    dependency rules (NetArchTest)
│       ├── Orbs.Specs/                 Gherkin features bound with Reqnroll
│       └── Orbs.Testing/               shared test host and API client
├── frontend/                React app for web and mobile
│   ├── src/  api/ app/ components/ features/ lib/ styles/
│   └── e2e/                 Playwright tests
├── docs/                    arc42, ADRs and guides
├── .github/                 CI, CodeQL, releases, templates
├── Dockerfile               one image: API + web app
└── compose.yaml             Orbs + PostgreSQL
```

## Development

**Prerequisites**: [.NET SDK 10](https://dotnet.microsoft.com/download), [Node.js 24](https://nodejs.org) with [pnpm](https://pnpm.io), and Docker if you want PostgreSQL or the full stack.

```bash
make setup      # restore tools and packages
make dev-api    # API on http://localhost:5080 (SQLite + demo data, API reference at /scalar)
make dev-web    # web app on http://localhost:5173 (proxies /api to the API)
```

No `make`? Every target is a plain command; see the [Makefile](Makefile) or the [development guide](docs/guides/development.md).

### Tests and checks

```bash
make test       # backend (unit, integration, architecture, specs) + frontend (Vitest)
make lint       # dotnet format, oxlint, TypeScript, Prettier
cd frontend && pnpm test:e2e   # Playwright, against a running stack
```

| Layer | Tooling | Where |
| --- | --- | --- |
| Domain and use cases | xUnit, Shouldly | `backend/tests/*.Tests` |
| API end to end in memory | WebApplicationFactory, SQLite | `Orbs.Api.IntegrationTests` |
| Behaviour (BDD) | Reqnroll + Gherkin | `Orbs.Specs/Features/*.feature` |
| Architecture rules | NetArchTest | `Orbs.Architecture.Tests` |
| Components and logic | Vitest, Testing Library | `frontend/src/**/*.test.tsx` |
| Browser end to end | Playwright (desktop and phone) | `frontend/e2e` |

See the [testing guide](docs/guides/testing.md) for the strategy.

## Configuration

Settings use standard ASP.NET Core configuration, so every key can be set as an environment variable (`:` becomes `__`).

| Setting | Default | Description |
| --- | --- | --- |
| `Database__Provider` | `Sqlite` | `Sqlite` or `Postgres` |
| `ConnectionStrings__Orbs` | `Data Source=orbs.db` | Connection string for the chosen database |
| `Orbs__ApiToken` | empty | Shared secret for `/api`. Set it before exposing Orbs publicly |
| `Orbs__SeedDemoData` | `true` | Create the Echo provider and Pip on an empty install |
| `Orbs__Cors__Origins__0` | none | Extra origins allowed to call the API (for a separately hosted web app) |

Full list in the [configuration guide](docs/guides/configuration.md).

## Mobile

The mobile apps are the same web build inside a [Capacitor](https://capacitorjs.com) shell.

```bash
cd frontend
pnpm cap:sync
npx cap add android   # or: npx cap add ios (macOS)
pnpm cap:android      # opens Android Studio
```

On first launch, set your server address under **Settings > Server**. Details in the [mobile guide](docs/guides/mobile.md).

## Extending Orbs

- [Add a model provider](docs/guides/adding-a-model-provider.md): implement `IModelAdapter`, register it, done. The UI picks it up from `/api/providers/kinds`.
- [Add a context connector](docs/guides/adding-a-context-connector.md): implement `IContextConnector` for a new kind of source (a repository, a calendar, an API).

## Roadmap

- [ ] Accounts and sign-in (passkeys, OAuth) for multi-user servers
- [ ] Retrieval with embeddings for large context sources
- [ ] File uploads (PDF, Markdown) as context
- [ ] Native adapters for providers without an OpenAI-compatible API
- [ ] Tool use, with orbs asking before they act
- [ ] Voice mode
- [ ] Scheduled context sync

Ideas welcome in [Discussions](https://github.com/stijntersteege/orbs/discussions).

## Contributing

Contributions of every size are welcome: bug reports, docs, tests, connectors, design. Start with [CONTRIBUTING.md](CONTRIBUTING.md), and look for issues labelled **good first issue**. Everyone taking part agrees to the [Code of Conduct](CODE_OF_CONDUCT.md).

Found a vulnerability? Please report it privately; see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © Orbs contributors.
