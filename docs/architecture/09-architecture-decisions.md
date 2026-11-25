# 9. Architecture decisions

Decisions are recorded as ADRs in [docs/adr](../adr/README.md). The most important ones:

| ADR | Decision |
| --- | --- |
| [0001](../adr/0001-record-architecture-decisions.md) | Record architecture decisions as ADRs |
| [0002](../adr/0002-dotnet-clean-architecture-backend.md) | .NET 10 backend with Clean Architecture and Minimal APIs |
| [0003](../adr/0003-react-vite-capacitor-client.md) | One React + Vite client for web and mobile, wrapped by Capacitor |
| [0004](../adr/0004-postgresql-and-sqlite-with-ef-core.md) | EF Core with PostgreSQL for production and SQLite for zero setup |
| [0005](../adr/0005-plugin-model-for-providers-and-context.md) | Providers and context sources are plugins found by kind |
| [0006](../adr/0006-server-sent-events-for-streaming.md) | Stream replies with server-sent events |
| [0007](../adr/0007-encrypt-secrets-with-data-protection.md) | Encrypt provider API keys with ASP.NET Core Data Protection |
| [0008](../adr/0008-testing-strategy.md) | Testing strategy: pyramid plus executable specifications |
| [0009](../adr/0009-single-container-delivery.md) | Ship one container for API and web app |
